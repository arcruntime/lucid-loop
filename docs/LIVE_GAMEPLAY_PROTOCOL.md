# Live gameplay protocol

Verified against official OpenAI documentation on 2026-09-14. This is a proposed bridge contract, not a claim that gameplay delegation is implemented. The voice model remains `gpt-live-1`; the current character voices remain Maya `gleam`, Ren `quartz`, Luca `meridian`, and Theo `vesper`.

## Current relay and recommended path

[server.mjs](../server/src/server.mjs) was an audio-only relay when this investigation began. Omitted delegation selects **client delegation**, so receiving delegation events does not mean a backend is handling them. Parallel work has now added standalone encounter and transcript modules; this document describes the required integration independently of whether the relay has wired each part yet.

Keep client delegation for the minimum gameplay bridge and make `delegation: { "type": "client" }` explicit. This lets the server select the NPC's permitted knowledge, interpret requests, validate encounter actions, and return only committed results. A backend interpreter is still required for natural-language actions; its implementation and model are separate choices. Do not substitute keyword matching for unrestricted conversational understanding or claim it works without that component. [Official delegation guide](https://developers.openai.com/api/docs/guides/live-delegation#configure-client-delegation).

## Verified Live wire contract

The examples below are valid event shapes with illustrative game content and IDs. IDs are opaque; preserve those received from OpenAI. Application request IDs are a separate namespace.

### Startup history

Add these fields inside the existing `session.start.session` configuration:

```json
{
  "delegation": { "type": "client" },
  "input": [
    {
      "type": "message",
      "role": "user",
      "content": [{ "type": "input_text", "text": "Please wait near the bar." }]
    },
    {
      "type": "message",
      "role": "assistant",
      "content": [{ "type": "output_text", "text": "All right, I'll wait here." }]
    }
  ]
}
```

Only include history that actually occurred and belongs to this NPC's permitted memory. Startup `input` allows at most 128 messages and 8,192 combined tokens. Roles are `developer`, `user`, and `assistant`, with one text part per message; `system` is not supported here. `instructions` has a separate 16,384-token limit. `input`, frontend `instructions`, model, voice/audio, and storage are startup fields, not mutable `session.update` fields. A new session is needed to change model, voice, or delegation mode. [Session configuration and history](https://developers.openai.com/api/docs/guides/live-conversations#provide-history-and-context).

### Transcripts and delegation

```json
{
  "type": "session.input_transcript.delta",
  "event_id": "event_transcript_1",
  "delta": "Could you wait here",
  "start_ms": 1000,
  "end_ms": 1800
}
```

Assistant speech uses the same fields with `type: "session.output_transcript.delta"`. Both may also contain optional `client_event_id`. Keep fragments exactly, including whitespace, timestamps, and receipt order. There is no item ID or authoritative transcript-turn-completed event. Intervals may overlap across speakers. UI grouping is revisable presentation logic, not an action trigger. Neither transcript arrival nor the absence of another fragment establishes audio playback or silence. [Transcript contract](https://developers.openai.com/api/docs/guides/live-conversations#transcript-deltas).

```json
{
  "type": "session.delegation.created",
  "event_id": "event_delegation_1",
  "offset_ms": 1900,
  "delegation": {
    "id": "item_9tA2bF3h7K9m2P5q8R1s4",
    "type": "delegation",
    "target": "client"
  }
}
```

The task text is **not present**. Assemble relevant transcript history and current encounter state in the server. Save `delegation.id`; do not confuse it with `event_id`, a Responses response ID, or a function `call_id`. A Responses-target delegation can additionally contain `delegation.response_id`. Handle late transcript data, ambiguous references, and corrections without inventing intent. [Delegation event schema](https://developers.openai.com/api/reference/resources/live/sideband-websocket#session.delegation.created).

### Context and committed results

```json
{
  "type": "session.thinking.append",
  "event_id": "context_loop2_revision17",
  "delegation_id": null,
  "content": "Current loop: 2. Music is intimate. Maya is waiting near the bar. This replaces the previous following state."
}
```

Use `session.thinking.append` for concise factual context, `session.instructions.append` for trusted application-authored behavior, and `session.commentary.append` for a result the NPC should communicate. Each requires a plain-string `content` of at most 500 tokens and a `delegation_id`, which is nullable. General context uses `null`; task results can use an existing client delegation ID:

```json
{
  "type": "session.commentary.append",
  "event_id": "result_wait_27",
  "delegation_id": "item_9tA2bF3h7K9m2P5q8R1s4",
  "content": "Maya has agreed to wait near the bar and is now waiting there. Acknowledge this briefly in character."
}
```

That content is appropriate only after the encounter has committed it. The model may paraphrase. Use factual content for externally supplied material; never append player text as trusted instructions. Quiet context may later be spoken, so it cannot hold knowledge the NPC must never reveal. [Append semantics](https://developers.openai.com/api/docs/guides/live-delegation#send-the-right-kind-of-update).

Each append type has a corresponding `session.thinking.appended`, `session.instructions.appended`, or `session.commentary.appended` acknowledgment with `event_id`, `start_ms`, `end_ms`, and optional `client_event_id` matching the outgoing ID. These acknowledge estimated context injection, **not** an executed action, consumed instruction, or completed speech. Pending appends may fail on close. Process `error` events and correlate `error.client_event_id` when present; error codes and correlation may be absent. [Acknowledgment schema](https://developers.openai.com/api/reference/resources/live/sideband-websocket#session.thinking.appended), [context timing](https://developers.openai.com/api/docs/guides/live-conversations#understand-when-context-reaches-the-model).

### Typed player input

There is no documented direct typed-user-turn event for the Live audio frontend in the reviewed protocol. Startup history is not an ongoing text-input mechanism. In **client mode**, the documented path is to send typed text to the application's backend, preserve it as user data, and return the backend's result through the append events. A short factual summary can be mirrored with `session.thinking.append`; raw text must not be promoted to instructions. Typed conversation therefore needs an application endpoint and the same intent/context pipeline as voice delegation. Full natural-language typed replies remain incomplete until that backend exists. [Typed input guide](https://developers.openai.com/api/docs/guides/live-delegation#accept-typed-input).

## Minimum application bridge proposal

These are internal responsibilities, not additional OpenAI events. Final module names should follow the encounter implementation.

1. **Bind the session.** The server associates an authenticated player, game/run ID, loop generation, NPC ID, and conversation ID with the Live connection. It constructs character context from authoritative state and seeds only relevant same-loop history.
2. **Retain history.** Store raw transcript fragments with session/NPC/loop IDs and sequence numbers. Store typed inputs separately as submitted user messages. Preserve a display history independent of the short context sent upstream. Do not make transcripts confirmed facts automatically.
3. **Interpret requests.** On a client delegation, pass accumulated permitted conversation plus current state and a bounded action catalog to the backend interpreter. Typed submissions use the same path. If intent is incomplete or ambiguous, return a clarification without mutating state. A delegation event is a request for help, not itself proof of a player command.
4. **Validate proposed actions.** The encounter module checks argument schema, actor identity, current loop/revision, spatial eligibility, mood, NPC knowledge, authored action gates, and idempotency before commit. Examples of proposed operations are requesting Maya wait/follow, asking Ren for a supported mood, or disclosing an authored clue. Names and eligibility belong to the game contract; they are not built-in Live tools.
5. **Commit and publish.** Produce a server-owned action result and updated state. Notify Unity from that result, refresh relevant NPC context, and then return a concise in-character speakable result. Spoken agreement alone must never change world state. Rejected actions should carry a useful reason without exposing hidden game facts.
6. **Handle corrections and resets.** Serialize conflicting mutations per encounter. Deduplicate by application action ID and retain the outcome. Invalidate pending work when its loop generation or conversation binding changes; suppress late results before both commit and speech. A socket failure does not undo a committed action. Reconnect from known state rather than retrying blindly.

### Current pending-request behavior

The bridge records a user-transcript sequence watermark for the current NPC and loop. If newer user fragments arrive while interpretation is pending, it reinterprets the original trigger with the latest attributed history before committing. The interpreter distinguishes continuation, replacement, cancellation and ambiguity semantically; fragment arrival, overlapping timestamps and individual words do not directly cancel an action. Assistant fragments do not authorize a new action. After at most three reinterpretations, continued new evidence produces a clarification without a mutation. The user-evidence check and authoritative commit have no asynchronous gap; lease and world-revision checks remain independent.

Transcript `sessionId` is a separate non-authorizing conversation correlation ID. Actual lease credentials remain server-side and are not included in transcript history or model context.

Remaining interruption work: additional delegations while an interpretation is pending currently receive `busy`; they are not queued as separate tasks. A correction after an action committed must become a new validated action. Unity preserves buffered PCM order and has not yet qualified interruption latency on a physical iPhone. Do not describe these pending checks as complete conversational interruption support. This follows the [official guidance on verified updates and corrections](https://developers.openai.com/api/docs/guides/live-delegation#keep-updates-accurate-and-useful).

A useful internal interpreter result is a discriminated union: `clarification`, `no_action`, or `action_proposal`. An action proposal contains the action ID and schema-checked arguments; the server supplies trusted actor, loop, and revision fields. The backend must not choose the authority fields. Keep proposed actions separate from facts, reported claims, and dialogue memory. A claim that Theo said something is not evidence that the statement is true.

The current client whitelist should stay restricted. Add deliberate application commands for typed input and encounter requests; do not expose arbitrary `session.instructions.append`, backend configuration, or tool-result forwarding from Unity. The server sends validated context updates to its existing primary WebSocket; another sideband is unnecessary when the relay already owns that connection. [Server connection guidance](https://developers.openai.com/api/docs/guides/voice-server-controls#observe-events-and-send-commands).

## Alternative: managed Responses delegation

This is documented, but choosing it requires a backend model configured at **session creation**, rather than changing the current client session in place:

```json
{
  "delegation": {
    "type": "responses",
    "responses": {
      "model": "<explicitly selected backend model>",
      "instructions": "<backend game rules>",
      "parallel_tool_calls": false,
      "tools": []
    }
  }
}
```

The placeholders are configuration decisions, not executable values. Function definitions go in `delegation.responses.tools` as `{type: "function", name, description, parameters, strict}`. Supported backend configuration can be changed with `session.update`; frontend instructions still use append. The live model's decision to delegate is separate from backend `tool_choice`. [Responses configuration](https://developers.openai.com/api/docs/guides/live-delegation#configure-responses-delegation).

Consume nested events inside the top-level `response.event` envelope, preserving its nullable/optional `delegation_id`. Collect completed function items from nested `response.output_item.done`, using `item.call_id`, `item.name`, and parsed `item.arguments`. Track the response ID from nested `response.created`. Do not execute partial argument deltas. Lifecycle snapshots deliberately clear `response.output`, so an empty `response.completed` output cannot establish that no calls are pending.

After validation and execution, submit every pending result, then continue:

```json
{
  "type": "response.item.create",
  "event_id": "tool_result_27",
  "item": {
    "type": "function_call_output",
    "call_id": "call_123",
    "output": "{\"status\":\"accepted\",\"actionId\":\"wait_27\"}"
  }
}
```

```json
{ "type": "response.create", "event_id": "continue_27" }
```

`response.item.create` has no standalone success acknowledgment. `response.create` accepts only its type and optional event ID, not a standalone Responses request body, model override, or delegation ID. These commands require Responses mode. Continue only after all pending function results have been returned. [Function-result workflow](https://developers.openai.com/api/docs/guides/live-delegation#complete-a-client-actionable-function-call).

Typed input in Responses mode queues a user item through `response.item.create` with `item: {type: "message", role: "user", content: [{type: "input_text", text: "..."}]}`, followed by `response.create`. It goes to the backend, not directly to a new frontend voice turn. Queuing text does not cancel running work. [Typed input](https://developers.openai.com/api/docs/guides/live-delegation#accept-typed-input).

## Required verification before claiming gameplay integration

- A spoken request reaches the interpreter, passes encounter validation, changes observable Unity behavior, and produces a truthful NPC acknowledgment.
- Typed input reaches the same backend with user-role semantics; ambiguous requests ask for clarification.
- Duplicate delegation/action delivery commits once; late results after a rewind or NPC switch do nothing.
- Interrupted and overlapping speech preserves readable history without inventing complete turns or confirmed facts.
- A failed/rejected action never publishes success; a context acknowledgment never triggers a mutation.
- Closing or reconnecting reconciles pending work and preserves eligible history. Close reads `session.closed` before transport cleanup when possible; transport closure alone does not establish finalization.

No API calls or relay edits were made by this investigation. Exact backend selection, runtime access, natural-language action success, latency, and voice/text behavior require integration and an end-to-end test.

## Implemented standalone interpreter boundary

[gameplay-intent.mjs](../server/src/gameplay-intent.mjs) now builds a non-streaming standalone Responses request using `text.format: {type: "json_schema", name, strict: true, schema}`. It requires an explicitly configured backend model and accepts only typed submissions or actual client-delegation events as triggers. The schema has an object root, all fields required, and `additionalProperties: false`; nullable fields represent unused action arguments. The parser checks a completed response, rejects refusals and unexpected tool output, and validates the result independently. These shapes follow [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) and [Responses text output](https://developers.openai.com/api/docs/guides/text).

Exports:

- `buildIntentRequest({model, context, trigger, fragments})`: returns a request body without sending it. `context` must come from `encounter.context(npcId)`; fragments use the transcript store's shape and are filtered to that NPC and loop. A typed trigger is `{type: "typed", requestId, text}`. A voice trigger is the received `session.delegation.created` event with client target.
- `parseIntentResponse(response, {npcId})`: accepts one completed JSON text result; reasoning output is ignored. Refused, incomplete, malformed, or unexpected output throws and cannot produce an action.
- `validateIntentResult(result, {npcId})`: checks the discriminant and every action argument. Supported proposals are Maya `wait`, `follow` targeting `player`, `agree_private_approach`, and `stop_recording`; Ren `request_music` with `Intimate` or `Aggressive`; Theo `agree_distance`; and Luca `mediate` or `ask_about_exposure`. The additional scenario actions are argument-free and come from the newly authored [demo scenario](DEMO_SCENARIO.md).
- `toEncounterCommand(result, {npcId, loopId, revision, requestId})`: returns an encounter command using server-provided authority fields, or `null` for clarification/no-action. It does not execute the command. The caller still invokes `encounter.submitAction(command, {npcId})`, handles its acceptance/refusal, and suppresses stale results.

The boundary cannot insert arbitrary facts, reset loops, or record claims. Luca's exposure question invokes one fixed authored disclosure gate; only its accepted server result supplies the spoken fact. Requests above the bounded context/history budgets fail explicitly; integration must select an appropriate excerpt rather than silently omitting context. [Tests](../server/test/gameplay-intent.test.mjs) cover proposal validation, player-text isolation, same-loop/NPC history filtering, refusal/incomplete output, authority binding, policy refusal, duplicate rejection, reset fencing, and the complete scenario proposal route. No network behavior or model-quality result is implied by these tests.

Two authored refusal codes have bounded player-facing explanations: `calmer_music_needed` for Luca's exposure question or socially eligible mediation under Aggressive music, and `mediation_group_not_ready` when mediation's social/music gates pass but physical assembly does not. The bridge sends these as `session.commentary.append`, explicitly stating that the action was not performed. The HUD independently maps the same codes from a confirmed refusal, including when speech delivery fails. Unknown/internal refusal codes retain generic feedback; neither code reveals a missing private prerequisite or changes world state.

[gameplay-delegation.mjs](../server/src/gameplay-delegation.mjs) provides the asynchronous registry bridge. It snapshots the NPC/loop/revision before interpretation, rejects obsolete leases or revisions after waiting, bounds one pending request and remembered IDs, and aborts pending work on disposal. Accepted action acknowledgments follow the server commit. Typed submissions are persisted through `registry.appendTyped` with explicit `source: "typed"` and null voice timestamps; they survive same-loop reconnects. Full typed text reaches Live as bounded factual-context fragments, while only a constant server-authored instruction requests an in-character reply for ordinary conversation. Sending those appends does not establish that the model consumed them or finished speaking. [Bridge tests](../server/test/gameplay-delegation.test.mjs) verify these boundaries with injected interpretation and transport functions.
