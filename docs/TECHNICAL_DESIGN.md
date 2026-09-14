# Before the Drop — Technical Design and Build Scope

**Shipping scope override, 2026-09-14:** the user paused Japanese voice work and excluded Japanese voice/lip-sync support from this release. Ship English conversations. Earlier bilingual requirements below remain source/history records; Japanese implementation and acceptance are deferred and must not block this release. Existing Japanese text/font work is unaffected by this voice-specific decision.

**Project:** Lucid Loop  
**Source audit date:** 2026-09-14  
**Status:** Source requirements plus reconciled implementation status, 2026-09-14. Recorded tests establish their stated scope; final game/device acceptance remains incomplete.

**Implementation update:** The user-authorized [A Quiet Way Out scenario](DEMO_SCENARIO.md) now settles the victim, violence, prevention route, reset and 180-active-second set. Authoritative world execution, Live/Responses action interpretation, Unity club presentation and approach-to-talk are implemented. The [validation ledger](IMPLEMENTATION_VALIDATION.md) records actual Unity checks and real-provider prevention/correction tests, plus native Apple plugin compilation and Xcode packaging. It does not establish a signed, installed, physically tested iPhone game or final character/animation acceptance. [LIVE_GAMEPLAY_PROTOCOL.md](LIVE_GAMEPLAY_PROTOCOL.md), [WORLD_PROTOCOL.md](WORLD_PROTOCOL.md) and [UNITY_ENCOUNTER_INTEGRATION.md](UNITY_ENCOUNTER_INTEGRATION.md) supply detailed contracts; where their historical proposal passages remain, current code and the dated validation ledger govern implementation status.

## 1. Authority, provenance, and scope

**Relay hosting decision (2026-09-14):** Use a local development relay for now. The Editor connects over loopback; a physical iPhone connects to the development computer's LAN address. OpenAI credentials remain on the relay. A hosted deployment is outside the current setup decision.

This document turns the [GDD](GDD.md) and the additional source review into engineering requirements. **The [BTD Battlemap FigJam board](https://www.figma.com/board/pCX5z7C3Izwe6sfIg53nC1/BTD-Battlemap?node-id=0-1) overrides the [Google design document](https://docs.google.com/document/d/1JxbEgu6D5qHtuyGlwKqmO1IJJdrY7MZikN055f87FXA/edit) where they conflict.** Draft labels in either source remain draft labels. A missing rule is a decision to author, not permission for runtime AI to invent it.

See the [design source audit](DESIGN_SOURCE_AUDIT.md) for extraction coverage and reconciliation, and the [animation handoff](../art/animation-handoff/README.md) for downloaded assets and outstanding import work.

| Label | Evidence and meaning |
| --- | --- |
| **F** | Figma battlemap, Draft Rules, Starting Loops, AI note, and Music/Mood Matrix. |
| **D** | Google Doc GDD, Character Guide, Game Design, and sync notes. |
| **P** | Google Doc Programming tab: server truth, filtered context, disposable voice sessions, persistent memory, validated tools. |
| **N** | Google Doc Prompt for Astra: all four interactive NPCs use Live; automatic transcripts and confirmed-fact storage; embeddings suggested. |
| **UI** | Google Doc UI notes and reference images, including conversation history, reply/microphone controls, interaction states, camera transition, and pause. |
| **A** | [Animation workbook](https://docs.google.com/spreadsheets/d/1pdyPs5SUAmrL_mCwHOMPpTHCXX6Ez9td/edit?gid=1109901444#gid=1109901444), tab `1109901444`; download and import status must be tracked separately from its Ready labels. |
| **R** | [README](../README.md) and [shared-rig contract](SHARED_CHARACTER_RIGS.md): repository constraints, separate from source gameplay decisions. |
| **U** | User-confirmed production direction relayed through the `lucid-loop` agent bus: two shared skeletons, Ren on the female skeleton, separate toggleable cap; character artist owns character import. |
| **Authored** | User-authorized demo decisions in [DEMO_SCENARIO.md](DEMO_SCENARIO.md), separate from the original Figma/Google source. |
| **Proposed** | Engineering structure introduced here to implement source requirements; not a quotation or already-approved gameplay rule. |
| **Open** | Missing or conflicting authoring/integration decision. |

The target is one playable club encounter: investigator player; Maya, Ren, Luca, and Theo as interactive NPCs; a non-interactable affair partner; two music moods; visible failure and rewind; and a meaningful changed attempt. Additional rooms, extra moods, procedural mysteries, multiplayer, and a general-purpose narrative platform are outside this build.

The original two gyms remain useful development tools. The playable integration is now `BeforeTheDrop`, with server movement, conversation, speech, mood and primitive event-presentation bindings. The initial audit's static-prompt/no-gameplay findings describe the starting point, not current code.

### Current implementation evidence

| Implemented area | Code/contract | Evidence and remaining boundary |
| --- | --- | --- |
| Authored progression and reset | [demo-scenario.mjs](../server/src/demo-scenario.mjs), [scenario](DEMO_SCENARIO.md) | Deterministic causal rules and full real-provider prevention recorded in the [ledger](IMPLEMENTATION_VALIDATION.md); final authored acting remains incomplete |
| Truth, filtered knowledge and game leases | [encounter.mjs](../server/src/encounter.mjs), [game-sessions.mjs](../server/src/game-sessions.mjs) | Server owns facts, state and authorization; process-local memory does not establish durable saves |
| Natural-language gameplay bridge | [gameplay-intent.mjs](../server/src/gameplay-intent.mjs), [gameplay-delegation.mjs](../server/src/gameplay-delegation.mjs), [protocol](LIVE_GAMEPLAY_PROTOCOL.md) | Responses interpretation, typed requests, Live delegation, confirmed commits and late-user-evidence reinterpretation tested; not keyword-based action recognition |
| Transcript history | [transcripts.mjs](../server/src/transcripts.mjs) | Attributed timed fragments and typed records stay separate from confirmed facts; no authoritative completed-turn event is invented |
| World simulation and approach | [encounter-world.mjs](../server/src/encounter-world.mjs), [world protocol](WORLD_PROTOCOL.md), [EncounterCoordinator.cs](../Unity/Assets/Gyms/Runtime/Encounter/EncounterCoordinator.cs) | Server positions, visibility, staging and eligibility; actual Unity approach test verifies overview until eligibility and cancellation |
| Club HUD and presentation | [EncounterHud.cs](../Unity/Assets/Gyms/Runtime/Encounter/EncounterHud.cs), [integration notes](UNITY_ENCOUNTER_INTEGRATION.md) | Phone Editor preview, retained clues, held primitive fall/reset and mood checks passed; physical keyboard/touch, marker fidelity and final art remain separate |
| Voice and native audio | [EncounterVoiceController.cs](../Unity/Assets/Gyms/Runtime/Encounter/EncounterVoiceController.cs), [native voice](IOS_NATIVE_VOICE.md) | Provider and controller lifecycle evidence, native plugin Apple compile/link and Xcode packaging are recorded; physical microphone/AEC, complete app signing/install and target performance remain pending |

Use the [validation ledger](IMPLEMENTATION_VALIDATION.md) for current run IDs, exact scopes and limitations. This document reconciles that evidence; it does not claim to have rerun the tests during this documentation repair.

## 2. Source-derived requirement register

Requirement IDs are stable references for implementation tasks and acceptance evidence. Proposed implementation choices are described separately in later sections.

| ID | Required behavior or constraint | Source |
| --- | --- | --- |
| ENC-01 | One isometric club preserves the entrance/reset point, direct recognition route, left preparation route/bar, Theo/partner encounter, and elevated Ren booth/sightlines. | F |
| ENC-02 | Maya follows the player until an accepted request changes her behavior; agreeing to wait visibly makes her stay. | F |
| ENC-03 | Recognition occurs once per loop only when Maya is in the recognition area and can see both Theo and the affair partner. Player-only entry is insufficient. | F |
| ENC-04 | Proposed opening stages recognition, Maya recording, Theo approaching, Luca intervening, lethal outcome in Ren's view, and reset. Exact violence was left open by the source; the authored scenario now fixes it. | F; Authored |
| ENC-05 | Confrontation-area entry alone cannot cause violence. Preparation/avoidance alone cannot win. | F |
| ENC-06 | A later attempt supports changed music and/or Maya waiting before approach and demonstrates a changed causal sequence. | F |
| ENC-07 | Authored danger must be resolved and the set completed; the source left exact predicates open; the authored scenario now defines them. | F, D; Authored |
| MOOD-01 | Exactly Intimate and Aggressive for the demo; opening uses Aggressive. | F |
| MOOD-02 | A player interaction with Ren/the booth can request music. Mood modifies disposition alongside evidence, approach, and perceived exposure; it cannot guarantee compliance. | F |
| MOOD-03 | Intimate lighting uses rose pink/violet and selective green contrast; Aggressive uses crimson/ultraviolet purple. | UI |
| AI-01 | All four named NPCs use Live conversations; runtime language interpretation must produce actual legal game actions. | N, F |
| AI-02 | Server owns truth, clues, progression, and consequential changes. Models cannot invent canonical evidence or rewrite the mystery. | P, D |
| AI-03 | Build separate NPC context containing role, objective, personality, known facts, beliefs, secrets, permitted lies, current situation, relationships, and disclosure rules. | P |
| AI-04 | Create voice sessions on demand with instructions supplied before conversation; refresh relevant context during the session; dispose of sessions after conversation. | P |
| AI-05 | Preserve validated character memory between conversations: topics, claims, lies, attitude/trust, and secrets revealed. This does not grant memory across loops. | P |
| AI-06 | Validate consequential tool calls on the server. Source examples cover clue reveal, claim recording, relationship change, and ending conversation. | P |
| AI-07 | Automatically transcribe conversation and store confirmed facts separately from raw conversation. Embeddings are a suggestion, not a required dependency. | N |
| LOOP-01 | Reset encounter state, including recognition, while retaining the intended player clue/learning. The authored demo resets physical recording and ordinary NPC memory, including Ren. | F, D, P; Authored |
| UI-01 | Communicate loop, music/mood, retained clue, available interaction, and request outcome. | F, UI |
| UI-02 | Conversation UI supports speaker identity, history, typed reply, and microphone input. A 2D portrait fallback and symmetrical portrait framing are described. | UI |
| UI-03 | Distinguish available, focused, and out-of-reach interaction states; distinguish people from objects; show the green player beacon. | UI |
| UI-04 | Provide exploration/conversation camera transition and pause presentation. The authored demo pauses simulation during foreground conversation; camera polish remains separate. | UI; Authored |
| ART-01 | Reuse two canonical shared skeletons; Ren uses female; her cap remains a separate toggleable accessory. | U, R |
| ART-02 | Download prepared animations with source provenance, then assess their actual gameplay coverage and rig compatibility. | User, A |
| TECH-01 | Unity project `Unity/`, pinned Unity 6000.3.24f1/URP 17.3; phone-first landscape, 16:9 target, sustained 30 fps on iPhone 15 Plus. | R |
| TECH-02 | Expressive real-time English/Japanese lip-sync using open-source/project-owned code; use the repository's specified dependency injection dependency. | R |

## 3. Responsibility and component boundaries

Use the existing Unity client and relay as integration points. These are logical components; they do not require separate deployed services or a new distributed platform.

| Component | Owns | Consumes / produces |
| --- | --- | --- |
| Authored encounter data | Character definitions, immutable facts, clue/disclosure rules, legal actions, initial state, encounter prerequisites | Versioned content with stable IDs; no generated replacement for authored truth |
| Authoritative encounter service | Loop/world state, knowledge attribution, legal action validation, progression, reset and win predicates | Player intents and validated observations → committed state changes and events |
| NPC context builder | Character-specific projection, permitted actions, relevant same-loop memories | Authoritative state → instructions and context revision |
| Live session coordinator | Session identity/lifecycle, voice configuration, instruction refresh, transcription, tool routing | Input audio/text and projected context ↔ model; validated actions → world |
| Memory/fact store | Transcript records, claims, confirmed disclosures, character memory, retained player discoveries | Committed events and conversation records; retrieval for context/journal |
| Server world / Unity presentation adapter | Server navigation, range/visibility and staging; Unity interpolation and action presentation | Player movement intentions → server; accepted world frames → Unity visuals |
| Presentation | HUD, conversations, camera, mood audio/lighting, animation/facial playback, pause | Player input → intents; committed events → visible feedback |

The spatial trust boundary is implemented: the server simulates a bounded planar club and derives observations from its accepted positions. Unity sends sequenced movement/approach/stop intentions and renders world frames. It cannot submit authoritative NPC positions, visibility, elapsed time, death, evidence or victory. See [WORLD_PROTOCOL.md](WORLD_PROTOCOL.md) for geometry and validation.

One foreground NPC conversation is the implemented demo resource policy; all four named NPCs are conversation-capable. Background group speech and generalized overhearing are not implemented requirements for this authored route.

## 4. Data model and persistence boundaries

| Record | Minimum contents | Lifetime |
| --- | --- | --- |
| Encounter definition | Content version; actor/fact/clue/action IDs; initial mood/placements; recognition region; authored escalation, disclosure, reset, and success rules | Immutable for a playthrough |
| World state | Playthrough ID, loop ID/index, state revision, mood/transition, set progress, positions/actions, recognition flag, recording/exposure state, encounter phase, actor alive/dead state | Current loop |
| NPC state | Known fact IDs and provenance; beliefs; secret/disclosure status; claims/lies; attitude/relationship values if authored; current objective/action; same-loop memory | Current loop unless a specific exception is authored |
| Player progression | Discovered clue IDs, retained journal entries grounded in events, prior outcome knowledge, loop count | Across loops |
| Conversation record | Conversation/loop/NPC IDs; timestamped attributed fragments and explicit typed records; revisable UI grouping; tool requests/results; completion reason | Stored independently of live socket; retrieval follows loop rules |
| Claim | Speaker, content or authored claim ID, supporting fact IDs if any, audience, time, source event, whether a known deliberate lie | Record of speech, not truth |
| Confirmed fact/disclosure | Existing fact/clue ID, confirming rule/event, eligible recipients, source/provenance | NPC knowledge or player progression as appropriate |
| Action record | Request ID, session/loop IDs, actor/target, action type/args, evaluated revision, decision/reason, execution state | Until resolved; retained for diagnosis as needed |

Keep **truth**, **NPC belief**, **NPC claim**, **player discovery**, and **current physical evidence** distinct. A transcribed accusation cannot become a confirmed fact solely because a model repeated it. A player remembering the recording does not mean the new loop contains that recording.

The implementation uses structured encounter/transcript records and bounded NPC-filtered conversational context. Use summaries only as non-authoritative context tied to source records. Add embeddings only if concrete retrieval needs justify them; retrieval must still filter by NPC and loop before context assembly. The current registry/history are process-local with game credentials held in client memory; reconnect within that lifetime is supported. Durable storage, retention/deletion policy and resume across relay restarts remain product/implementation decisions.

Same-loop utterance recall now supplements the 2,000-character startup transcript excerpt with `speechMemory`: the earliest 128 validated NPC output fragments, capped at 16,000 text characters and 32 KiB of serialized JSON per NPC/loop. Only prior sessions enter this memory; ongoing session output is excluded. Source identifiers over 128 UTF-8 bytes are represented by a labeled SHA-256 digest of the stored original. It preserves contradictory assertions with session/event/timing provenance; user input and other NPCs' conversations are excluded. These are explicitly unverified, potentially incomplete utterances, not adjudicated claims, known lies, confirmed facts, or proof that audio was heard. The prefix is retained rather than silently replaced by later speech; `incomplete` and per-observation `excerpt` expose capacity loss. Existing transcript storage remains bounded and process-local. Rewind removes access from fresh NPC context while retaining player history/discoveries under the existing contract. This improves AI-05 recall without claiming semantic lie classification or generalized trust memory. Regression coverage includes the real relay WebSocket event path with a provider-shaped fixture; natural provider recall remains a separate acceptance gate.


## 5. Encounter simulation and authoring surface

### 5.1 Recognition, witnessing, and escalation

Implemented recognition evaluation is a conjunction: current loop has not recognized; Maya is in the authored region; Theo is visible to Maya; affair partner is visible to Maya. The recognition event updates the flag and Maya's knowledge once. Recognition must be reproducible from actor positions and visibility evidence, not the model's opinion.

[CLUB_WORLD_CONFIG and world simulation](../server/src/encounter-world.mjs) define walkable bounds, obstacle footprints, recognition region, spawns and sightlines. Conversation eligibility uses a 2.2 m player/NPC distance and unobstructed eye-height sightline. These are authored demo geometry choices, not coordinates supplied by the FigJam arrows. Changes to final scene geometry must preserve agreement with the server layout.

The authored chain is recognition → recording → Theo approach/reach → Luca intervention → Theo shove → Luca's fatal backward fall beside the VIP table. Offsets after recognition are 0, 4, 10, 14 and at least 20 active seconds, with physical staging prerequisites; time alone or zone entry cannot kill. Ren must see the staging but does not thereby know unheard dialogue. [Scenario rules/tests](../server/test/demo-scenario.test.mjs) and [world tests](../server/test/encounter-world.test.mjs) cover the executable predicates.

Private conversation is attributed to its actual NPC and does not broadcast another NPC's transcript. Generalized acoustic overhearing/music-dependent audibility remains unimplemented; the authored scenario uses explicit knowledge, disclosures and visual witnessing.

### 5.2 Mood and exposure

Encode source dispositions as character-specific rules/context, not one global compliance multiplier:

| NPC | Intimate | Aggressive | Additional conditions |
| --- | --- | --- | --- |
| Maya | Reflective, receptive to discretion | Impulsive, outspoken, more confrontational | Existing knowledge, player approach, current follow/wait state; default opening records |
| Theo | More approachable calmly, but audible accusations can heighten exposure | Defensive/forceful; loud music can also make him feel less observed | Actual recording, whether he notices it, audience and perceived exposure |
| Luca | More willing to disclose limited gossip when asked directly | Guarded, alert to threats | Knows fragments only; intervention requires an authored threat condition |
| Ren | Source supplies request/booth role | Source supplies request/booth role | Do not invent missing matrix values or request-acceptance thresholds |

An accepted Ren music request commits mood in the encounter; Unity drives lighting/audio from that state and the relay refreshes filtered context. The Intimate route has explicit disclosure/agreement prerequisites; music alone cannot win or guarantee compliance. Mood presentation tests are recorded, while musical audition, transition polish and physical-device mix remain acceptance work. Autonomous DJ changes and extra cooldown systems are not required for the current route.

### 5.3 Navigation and action execution

The server implements follow, hold position, approach, intervention, distance keeping, separation and fall as persistent actor behaviors. A wait acceptance must override following until its authored resume condition. Path failure, target movement, or interruption must report an execution result; acceptance alone is not proof that the actor reached the target.

Conceptual action lifecycle (not a claim of a generic public event enum): `proposed → rejected` or `accepted → executing → completed / failed / interrupted`. The simulation decides consequences; animation depicts them. An animation marker may report progress but cannot create an unrelated death or clue. The authored clock freezes movement/escalation while a foreground conversation connects or runs. Accepted actions persist and execute when the player leaves; wait persists until another accepted behavior/reset. The HUD explains Leave to resume movement. The bounded action/state semantics are in [WORLD_PROTOCOL.md](WORLD_PROTOCOL.md).

## 6. Live conversation, context, and action contracts

### 6.1 Session lifecycle

1. Player focuses an eligible NPC and reaches conversation range; UI identifies the target.
2. Server loads the current loop and NPC memory, builds filtered context and allowed actions, and allocates a conversation/session identity.
3. Configure the intended NPC voice and initial instructions before enabling player speech/text submission. Expose connecting/ready/error states to the UI.
4. Route microphone or typed turns through the conversation. Produce transcript history and voiced character responses. Apply consequential requests through validation immediately when needed for gameplay.
5. Refresh context when relevant mood, witnessed events, knowledge, relationships, or action eligibility change. Record the context revision used for each consequential proposal.
6. Store attributed transcript fragments/typed records and validated memory/disclosures as they become available, so a dropped connection does not erase already-committed gameplay.
7. On exit, NPC switch, reset, or terminal failure, close the session, stop its audio, release microphone resources, and finalize its completion reason. A future conversation rebuilds context from stored state.

Programming's `session.update` / `session.instructions` are source examples, not the implemented provider event recipe. The reviewed [Live protocol](LIVE_GAMEPLAY_PROTOCOL.md) uses explicit client delegation, startup filtered history/instructions, and server-owned context/result appends. Typed requests enter the backend interpretation path; a typed line is not misrepresented as a provider audio-input event. Timed transcript fragments have no authoritative item ID or completed-turn signal. Actual provider tests and their limits are recorded in [IMPLEMENTATION_VALIDATION.md](IMPLEMENTATION_VALIDATION.md).

**Voice mapping:** the Prompt for Astra specifies the following selections, also present in [server/src/prompts.mjs](../server/src/prompts.mjs). Preserve them in a single server-owned configuration. Source labels describe the intended performance; their presence in code does not independently verify provider availability or audible output.

| NPC | Voice | Source performance direction |
| --- | --- | --- |
| Luca | `meridian` | Masculine North American; measured, dry humor; distinguish heard information from known facts |
| Maya | `gleam` | Feminine North American; lively, affectionate teasing; soften when concerned |
| Ren | `quartz` | Feminine Australian; concise, dryly amused; distinguish what she saw from what she heard |
| Theo | `vesper` | Masculine British; polished, animated; quieter and deliberate about secrets |

Ren's art sheet identifies her as 29 and emphasizes that she cannot hear every conversation. Theo's age conflicts: art/prompt say 30, Character Guide says 32. Preserve this as a character-data decision instead of letting generated context alternate between ages; it does not block the action architecture.

### 6.2 NPC context contract

Provide character identity/style/objective; relevant situation and mood; known facts with origin; beliefs explicitly marked as beliefs; the character's own secrets/permitted lies/disclosure rules; appropriate relationships and prior exchanges; permitted actions and prerequisites; loop/session/context revision. Omit inaccessible world truth entirely. A director-level view, if used, must not be reused as a speaking NPC's instructions.

Do not place other NPCs' secrets or the player's whole journal into every prompt. User speech is conversation content, not permission to replace rules, access the full solution, or execute arbitrary commands. Reconstructed memory must distinguish an NPC's earlier lie from a fact it later learned.

### 6.3 Proposed action interface

The following remains conceptual notation, not endpoint names. The implemented discriminated interpreter result, trusted authority fields and bounded action catalog are defined in [LIVE_GAMEPLAY_PROTOCOL.md](LIVE_GAMEPLAY_PROTOCOL.md) and [gameplay-intent.mjs](../server/src/gameplay-intent.mjs); the source tool examples below are not all exposed provider tools:

```text
ActionProposal {
  requestId, conversationId, loopId, contextRevision,
  actorId, actionType, targetId?, arguments, referencedFactIds[]
}

ActionDecision {
  requestId, accepted, reasonCode, committedRevision,
  actionId?, executionState?, permittedResultForSpeaker
}
```

| Action family | Source/example | Required validation |
| --- | --- | --- |
| Follow / wait / approach | Figma's Maya behavior; implemented action names in the world contract | Active actor/session; legal target; current action compatibility; navigation/range prerequisites; authored resume/interruption rules |
| Reveal clue | Programming `reveal_clue(clueId)` | Existing clue; NPC knows it; disclosure prerequisites met; recipient allowed; no duplicate award |
| Record claim | Programming `record_npc_claim(claim)` | Attribute speaker/audience and conversation; retain as claim; do not promote unsupported prose to truth |
| Change relationship | Programming `change_relationship(npcId, delta)` | Existing relevant actor; authored allowed dimensions/bounds; valid context and cause |
| Music request | Figma Ren/booth interaction | Supported mood; correct interaction authority; request acceptance/transition rules |
| Public speech / intervention | Figma encounter | Allowed audience/target; authored threat and action prerequisites; execution constraints |
| End conversation | Programming `end_conversation(reason)` | Valid active conversation and bounded reason; persist/teardown idempotently |

Validate schema, IDs, session ownership, current loop, relevant state freshness, prerequisites, and bounded effects. Deduplicate request IDs. Re-evaluate or reject stale proposals that depended on changed state; do not blindly apply a delayed acceptance after reset. Return only information the speaking character can receive.

Dialogue must reflect the decision. The integration must prevent an unvalidated model promise from being presented as completed gameplay. The bridge now validates and commits before sending a consequential acknowledgment; append acknowledgments do not prove completed speech. Its lease/revision fences and bounded reinterpretation of newer user evidence prevent obsolete requests from committing. Real spoken-correction evidence is recorded separately from deterministic fencing. A late speech-delivery failure does not undo a committed action; the HUD reports the authoritative outcome.

### 6.4 Failure and interruption behavior

Implemented lifecycle/fencing preserves committed state on model timeout, invalid tools, disconnection, and duplicate results; displays a retry/connection state; and never treats infrastructure failure as persuasion success or catastrophe. Reconnecting creates a session from authoritative state. Reset cancels or fences outstanding requests, stops prior audio, and clears stale subtitles/interaction targets. Track latency/failures for diagnosis without exposing internal reason codes in the player HUD.

The authored set lasts 180 active simulation seconds. One server clock pauses both movement and progression for foreground connecting/active conversation, explicit pause, suspension and loss of world connection; it bounds catch-up after stalls. Rewind resets loop-local state, plans, commitments, recording, positions and time while retaining player discoveries. Ren has no cross-loop memory. Victory requires recognized/mediated resolution, stopped recording, distance agreement, physical separation and survival to set end; avoidance ends unresolved. See [scenario](DEMO_SCENARIO.md) and [world timing](WORLD_PROTOCOL.md).

## 7. Unity UI, camera, audio, and character integration

| Surface | Build scope | Proposed implementation detail / open point |
| --- | --- | --- |
| Exploration | Isometric room, readable movement, green loop-shaped overhead player beacon with gentle bob/pulse | Final beacon readability remains a visual gate. The source floor-ring mockup does not establish destination semantics; runtime walking uses validated server intentions |
| Interaction | People use speech-bubble markers; objects use diamond-dot markers. Outlined available, filled focused, dim out-of-reach; one contextual prompt at a time | Central interaction target model drives marker/HUD; `E` talk/inspect appears in desktop references and requires mobile adaptation |
| Conversation | Gold/black Art Deco panel, name/role, speech text, typed-reply field, mic/send, History and Leave; optional right portrait in symmetrical frame | References show Enter to send and hold V to speak; The phone implementation uses a compact exploration dock, expanded Talk/History, typed reply and mic toggle; device usability remains to validate. Maintain connecting/listening/responding/error states |
| Camera | Three stages: overview, move closer, close conversation in the club | Implemented approach remains in overview, then targets the speaker after eligibility; Leave restores overview/navigation. Final framing, transition duration and collision quality remain polish gates |
| HUD/journal | Loop, mood/music, retained clue, understandable request consequence | Server set progress is displayed; confirmed discoveries and conversation claims must not be visually conflated |
| Pause | Reference menu: Resume, Settings, Controls, Main Menu, Quit; Esc resumes | Modal Resume, device-persisted music volume Settings, current Controls and Main Menu connection return are implemented. Main Menu preserves resumable credentials. Quit is desktop-only; Escape backs out of submenus or resumes. Physical iOS touch acceptance remains pending. |
| Audio | Two mood tracks, intelligible voice, reset cue | Mood/audio binding and speech playback cancellation are integrated; musical audition, ducking and transition quality still require validation |
| Character | Room-scale silhouettes and close facial performance; English/Japanese coverage | Integrate shared facial interface with body acting; body clips must not overwrite speech/blink or add a second jaw transform |

Follow the existing project/DI conventions rather than introducing an alternative UI or dependency framework solely for this document. Performance acceptance is device evidence at sustained 30 fps on iPhone 15 Plus, not an editor frame-rate observation. Voice buffering, facial deformation, crowds, lighting, and animation must be measured together in the club scene.

## 8. Prepared animation inventory and import boundary

The reviewed workbook contains **25 rows: 23 Ready and 2 To Do**. **All 17 unique Drive assets have been downloaded as original FBX files; none are missing.** The [handoff README](../art/animation-handoff/README.md) maps all sheet rows to actual filenames, and the [manifest](../art/animation-handoff/manifest.json) records URLs/Drive IDs, row reuse, local paths, byte sizes, SHA-256 hashes, and read-only FBX metadata inspection. Suggested `.gif` filenames in the sheet are labels, not the actual downloaded file type.

Collection is complete for the Drive originals; **import, retargeting, motion suitability, and visual acceptance are not established**. Two rows remain URL-only references: Ren DJ Idle / Mixing (row 11, To Do) and world Rewind / Reset Reference (row 26, Ready). No reference video was downloaded. Theo React Open (row 16) is still To Do even though its reused FBX is downloaded. Neither a sheet Ready label nor FBX metadata proves a usable gameplay clip; playback FPS remains unverified.

Several mappings need explicit review: Player Run / Fast Walk reuses Luca's `Standard Walk.fbx`; Maya Listen Guarded and Theo Listen Impatient reuse `Walking_maya.fbx`; Maya React Upset reuses `Talking_theo.fbx`; Maya React Vulnerable and Theo React Open share `Thoughtful Head Nod.fbx`. These may be intentional reuse, but the workbook does not establish that the motion fits every action. Unflagged files also require review.

**Import ownership:** the character artist imports/retargets character assets. Lead-engineer work defines action/animation integration and checks coverage against the encounter. User-confirmed constraints are two skeletons, Ren on female, and a separate toggleable cap. [SHARED_CHARACTER_RIGS.md](SHARED_CHARACTER_RIGS.md) defines the actual hierarchy/rest/binding invariants and shared facial interface; matching bone names alone is insufficient.

| Required gameplay coverage | What the animation integration must establish |
| --- | --- |
| Idle, locomotion, follow, wait | Reusable body playback on each applicable canonical rig; foot contacts; navigation agreement; no unintended root/calibration override |
| Maya recording | Readable recording pose/action; phone/prop attachment if needed; start/stop state agrees with authoritative recording |
| Theo approach/escalation | Approach distinct from threat; escalation clip choice follows an authored action |
| Luca intervention | Target and contact staging communicates intervention rather than assigning unapproved aggression |
| Conversation/gestures/reactions | Body acting coexists with blink, gaze, facial expressions and bilingual speech; appropriate finger controls where required |
| Catastrophe and rewind | Luca/Theo/table staging is authored and server-validated. Primitive held fall/reset is tested; final imported action clips and contact staging remain unaccepted |

For each imported clip, record canonical rig/version, source provenance, duration, looping, root-motion policy, props/contacts, supported actors, action mapping, and known defects. A Ready sheet row still requires runtime visual verification. **The sheet does not supply explicitly authored Maya recording or Luca intervention coverage.** The source workbook's `Dying Backwards.fbx` did not select a victim; the later authored scenario selects Luca, but final retargeted fall/contact playback remains unaccepted; the rewind remains reference-only, and Ren's mixing remains To Do. Turn these gaps into authoring/animation tasks after artist review rather than treating collection as encounter coverage.

## 9. Build sequence: delivered foundations and remaining work

The original sequence has been substantially implemented; it is not a queue of untouched assignments. Keep the following ownership boundaries while finishing the demo.

| Work package | Delivered baseline | Remaining exit work |
| --- | --- | --- |
| Content contract | User-authorized victim, threat, prevention, timing and reset in [A Quiet Way Out](DEMO_SCENARIO.md) | Narrative/interaction playtesting and final staging polish, not a new murder solution |
| Authoritative core | Validated facts/actions, progression, reset and success predicates; deterministic tests | Regression coverage for subsequent content changes; no redesign prerequisite |
| World execution | Server planar geometry, movement, visibility/staging, pause, separation and conversation gate; [world contract](WORLD_PROTOCOL.md) | Keep final club art/colliders consistent with the server layout; physical-device playtesting |
| Live gameplay | All four configured NPCs, typed and delegated request interpretation, filtered context/history, committed results and stale-user-evidence fencing | Broader natural speech/ambiguity/failure testing; physical microphone and playback quality |
| Unity integration | Separate club scene, interpolated world frames, explicit approach intent/cancellation, compact phone HUD, scrolling clues/history, mood and primitive recording/fall/reset | Full reference marker/menu fidelity, device keyboard/touch and camera/framing polish |
| Authored encounter completion | Real-provider test reaches witnessed catastrophe, rewind, legal prevention, mediation, separation and the 180-active-second end predicate | The same full route through physical iPhone input/audio; authored visual acting |
| Character/animation | Download provenance; shared-rig/body/face contracts and character runtime tests; primitive encounter fallback | Reviewed clip-to-action mapping on both rigs, missing recording/intervention, final contacts and expressive/bilingual acceptance |
| Device/audio delivery | iOS project export and inspected native packaging; full unsigned arm64 Unity app compile/link in hosted Xcode CI; controller cleanup checks | Sign/install/launch, physical iPhone AEC/route/latency/network/thermal/performance evidence |

Use [IMPLEMENTATION_VALIDATION.md](IMPLEMENTATION_VALIDATION.md) as the evidence ledger. Coordinate shared-scene and generated-art edits through the `lucid-loop` bus; character import remains artist-owned. Passing tests does not transfer that ownership or establish final visual acceptance.

Conversation reconstruction includes an explicit per-NPC `ownState` projection: Maya receives `recording` and `privateApproachAgreed`, Theo receives `distanceAgreed`, Luca receives `mediationAccepted`, and Ren receives an empty object. These are server-confirmed self-state, not model memory or access to the full scenario. The Live startup and intent interpreter receive the same projection; rewind clears it while retaining player discoveries.

## 10. Acceptance matrix and current evidence boundary

These are product acceptance requirements. Some now have deterministic, real-provider or actual Unity evidence; others still need device/content work. The [ledger](IMPLEMENTATION_VALIDATION.md) contains precise run IDs and scopes, including [complete real-provider prevention](https://github.com/jethac/lucid-loop/actions/runs/34777703855), [spoken correction](https://github.com/jethac/lucid-loop/actions/runs/34779808422), and [native Apple compilation](https://github.com/jethac/lucid-loop/actions/runs/34781433954). Those recorded runs are evidence for their tested path, not exhaustive gameplay or physical-device acceptance.

| Check | Existing evidence / remaining gate | Requirements |
| --- | --- | --- |
| A01 Opening causality | Actual Unity scene/local relay captures and tests verify opening, held catastrophe and rewind; real-provider route also exercises the chain. Final shove/contact acting remains placeholder | ENC-01, ENC-04 |
| A02 Recognition boundaries | Deterministic scenario/world tests validate region, visibility, once-per-loop and reset conditions. Final art layout must preserve these sightlines | ENC-03 |
| A03 Alternative agency | Real typed, spoken wait and complete prevention tests commit legal requests; world tests execute their consequences. Full physical-phone natural-input route remains pending | ENC-02, ENC-06, AI-01 |
| A04 No shortcut outcomes | Authored tests and full prevention predicate exclude zone-only death and passive-avoidance victory | ENC-05, ENC-07 |
| A05 Mood behavior | Rules/context and Unity mood tests exist; musical audition and final device mix remain pending | MOOD-01–03, AI-04 |
| A06 Four NPC conversations | All four voices/contexts are configured and the real prevention route uses the required NPC interactions; Unity approach gate is tested separately. Audible character-performance/device acceptance remains open | AI-01, UI-02 |
| A07 Knowledge isolation | Filtered context, canonical disclosures, claim/fact separation and lease isolation are tested. Broader adversarial conversational evaluation remains useful | AI-02, AI-03, AI-07 |
| A08 Memory reconstruction | Process-local attributed history, same-loop reconstruction and retained-discovery reset are implemented. Durable saves/retention are not | AI-05, LOOP-01 |
| A09 Validation | Rules/bridge tests exercise stale authority, bounded actions, duplicate/freshness handling and late corrections; real spoken correction commits follow without an intermediate wait | AI-06 |
| A10 Transcript/fact separation | Timed fragments and explicit typed records are stored independently of canonical facts. UI grouping is revisable; no completed-turn signal or spoken fact is fabricated | AI-07 |
| A11 Session lifecycle | Context/result delivery, reset/lease fencing and controller cleanup have test evidence. Actual physical route switching and interruption latency remain unproven | AI-04, LOOP-01 |
| A12 Presentation coherence | Phone Editor preview, approach/cancellation, history/clues, held fall/reset and mood checks pass. Player beacon and NPC range/selection markers passed actual scene PlayMode and visual review. Inspectable-object markers, reference menu fidelity and physical keyboard remain acceptance work | UI-01–04 |
| A13 Animation provenance/playback | Original asset manifest and character runtime evidence exist; final imported gameplay coverage on both rigs, Ren cap presentation and authored contact quality require review | ART-01, ART-02 |
| A14 Target quality | Xcode packaging and native plugin Apple compilation pass. Complete signed iPhone app, sustained 30 fps/thermal evidence and physical duplex audio remain pending; Japanese speech production is not accepted | TECH-01, TECH-02 |

Local Unity XML/captures are under ignored `.local/validation/`; the ledger identifies their filenames. [Character runtime evidence](validation/character-runtime/) and [native packaging report](validation/ios-native/export-packaging.json) are committed artifacts where available. Synthetic speech/controlled timing prove provider-path behavior, not physical microphone quality or natural interruption latency.

## 11. Settled authored decisions and real remaining gaps

The following are settled demo decisions, not unresolved source questions:

- **Violence:** Theo tries to take Maya's phone, Luca intervenes, Theo shoves Luca into the fixed low VIP table; Luca dies. Ren witnesses the staged sequence without omniscient hearing.
- **Prevention:** Intimate music, grounded exposure disclosure, Maya's private plan, Theo's distance agreement, stopped recording, physically present mediation and confirmed separation lead to survival through the set. No exact dialogue wording or model-declared victory substitutes for those predicates.
- **Time:** 180 active simulation seconds; foreground conversation/connection, explicit pause and suspension/world disconnection pause movement and progression together. Terminal catastrophe waits for explicit authoritative rewind.
- **Reset:** Positions, physical recording, NPC memory, commitments, staging and time reset. Player discoveries persist; Ren does not remember the prior loop.
- **Trust/transport:** Server simulation owns spatial observations. Unity submits intents. Live client delegation and the Responses interpreter validate actions before reporting their committed outcome. Transcripts remain timed fragments.

| Remaining gap | Concrete required work / boundary |
| --- | --- |
| Final action animation and staging | Artist-reviewed canonical-rig imports, recording/phone and intervention/shove/fall contacts, held death/reset behavior and clear silhouettes. Primitive tests are not final animation acceptance |
| Physical iPhone acceptance | Sign/install/launch the compiled app; test LAN connection, landscape safe area/keyboard/touch, route changes, microphone privacy/muting, double talk/AEC and full prevention input flow |
| Performance and sound quality | Measure sustained 30 fps, thermal behavior, latency/buffering and intelligibility with club music; native same-engine NPC output does not guarantee cancellation of Unity's separate background music |
| English facial performance | Final combined English speech, expression and body review on the accepted art, using actual audible output. Japanese voice support is deferred by user decision |
| UI/reference fidelity | Validate physical-device marker/beacon readability, pause Settings/Controls/navigation and final conversation framing; finish people/object distinctions when authored objects are available |
| Durable persistence | Decide whether relay restart/device restart saves are needed; implement storage and transcript retention/deletion if required. Current process-memory resume is a deliberate limited baseline |
| Narrative/content polish | Review player comprehension, character voice/age consistency, hints and route discoverability against the authored solution; do not reopen fixed victim/time rules without an explicit new authoring decision |

Generalized overhearing, autonomous DJ behavior, embeddings, extra service layers and narrative editors are optional extensions, not blockers for the existing authored prevention route. Future requirements in those areas must specify actual need and acceptance before expanding the system.
