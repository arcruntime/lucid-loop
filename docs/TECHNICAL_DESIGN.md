# Before the Drop — Technical Design and Build Scope

**Project:** Lucid Loop  
**Source audit date:** 2026-09-14  
**Status:** Design baseline for implementation; not a claim that these systems exist or have passed acceptance.

**Implementation update:** The user has authorized the [authored demo scenario](DEMO_SCENARIO.md) to settle victim, lethal staging and prevention, and confirmed iOS as the primary Unity target. [iOS build setup](IOS_BUILD.md) and [rendering policy](IOS_RENDERING.md) cover target activation and feature stripping. Encounter, transcript, session registry and Live delegation foundations now have automated tests; integrated scene, real provider behavior and iPhone performance remain separate acceptance gates. The source-era open-decision tables below remain useful provenance, with the scenario taking over its explicitly authored decisions.

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
| **Proposed** | Engineering structure introduced here to implement source requirements; not a quotation or already-approved gameplay rule. |
| **Open** | Missing or conflicting authoring/integration decision. |

The target is one playable club encounter: investigator player; Maya, Ren, Luca, and Theo as interactive NPCs; a non-interactable affair partner; two music moods; visible failure and rewind; and a meaningful changed attempt. Additional rooms, extra moods, procedural mysteries, multiplayer, and a general-purpose narrative platform are outside this build.

Current baseline is two separate movement/conversation and live-voice gyms described by the README. The initial code review reports static character prompts and no gameplay tool bridge in the relay. Treat the architecture below as work to build until a code audit and the corresponding acceptance evidence establish otherwise. A voice demo does not establish a playable encounter.

### Current implementation evidence

The read-only implementation audit inspected `server/src` and the Unity gyms; it did not execute Unity, rerun tests, or assess separate character-art development.

| Existing foundation | Specific evidence | Remaining integration |
| --- | --- | --- |
| Character voices and static instructions | [prompts.mjs](../server/src/prompts.mjs) `buildSessionStart` | Dynamic filtered context, memory, tools and refresh; `store: false` does not implement application memory |
| Live relay/session lifecycle | [server.mjs](../server/src/server.mjs) permits audio append/mute/unmute/close after ready | Gameplay validation/commit path and typed-player-input transport |
| Microphone, transcript deltas and playback | [LiveGym.cs](../Unity/Assets/Gyms/Runtime/Live/LiveGym.cs) opens fresh connections and maintains clipped transcript labels | Durable ordered history, confirmed facts, club integration and reset fencing |
| Player pathfinding and sample conversations | [OfflineGym.cs](../Unity/Assets/Gyms/Runtime/OfflineGym.cs), [GymBuilder.cs](../Unity/Assets/Gyms/Editor/GymBuilder.cs) | NPC locomotion/actions, authored affair partner, recognition and event simulation |
| Overview/close camera interpolation | [GymCamera.cs](../Unity/Assets/Gyms/Runtime/GymCamera.cs) | Conversation currently hides other actors/dancers; decide visibility while world actions continue |
| Decorative light pulsing | [ClubLighting.cs](../Unity/Assets/Gyms/Runtime/ClubLighting.cs) | Mood state, soundtrack requests/transitions, coherent presentation and AI context |
| Gym mouth amplitude scaling | [CharacterActor.cs](../Unity/Assets/Gyms/Runtime/CharacterActor.cs) | Connect the separately developed character/animation pipeline; amplitude scaling alone is not bilingual phoneme lip-sync |

No encounter loop, retained-clue system, social witness/exposure rules, catastrophe, or victory implementation was found in the inspected gym/server scope. This is a bounded audit finding, not a claim about every experimental file or the character artist's work.

## 2. Source-derived requirement register

Requirement IDs are stable references for implementation tasks and acceptance evidence. Proposed implementation choices are described separately in later sections.

| ID | Required behavior or constraint | Source |
| --- | --- | --- |
| ENC-01 | One isometric club preserves the entrance/reset point, direct recognition route, left preparation route/bar, Theo/partner encounter, and elevated Ren booth/sightlines. | F |
| ENC-02 | Maya follows the player until an accepted request changes her behavior; agreeing to wait visibly makes her stay. | F |
| ENC-03 | Recognition occurs once per loop only when Maya is in the recognition area and can see both Theo and the affair partner. Player-only entry is insufficient. | F |
| ENC-04 | Proposed opening stages recognition, Maya recording, Theo approaching, Luca intervening, lethal outcome in Ren's view, and reset. Exact violence remains unauthored. | F |
| ENC-05 | Confrontation-area entry alone cannot cause violence. Preparation/avoidance alone cannot win. | F |
| ENC-06 | A later attempt supports changed music and/or Maya waiting before approach and demonstrates a changed causal sequence. | F |
| ENC-07 | Authored danger must be resolved and the set completed; final resolution and timing predicates are open. | F, D |
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
| LOOP-01 | Reset encounter state, including recognition, while retaining the intended player clue/learning. Physical recording persistence and Ren memory are open. | F, D, P |
| UI-01 | Communicate loop, music/mood, retained clue, available interaction, and request outcome. | F, UI |
| UI-02 | Conversation UI supports speaker identity, history, typed reply, and microphone input. A 2D portrait fallback and symmetrical portrait framing are described. | UI |
| UI-03 | Distinguish available, focused, and out-of-reach interaction states; distinguish people from objects; show the green player beacon. | UI |
| UI-04 | Provide exploration/conversation camera transition and pause presentation. Exact timing and whether game time pauses are open. | UI |
| ART-01 | Reuse two canonical shared skeletons; Ren uses female; her cap remains a separate toggleable accessory. | U, R |
| ART-02 | Download prepared animations with source provenance, then assess their actual gameplay coverage and rig compatibility. | User, A |
| TECH-01 | Unity project `Unity/`, pinned Unity 6000.3.24f1/URP 17.3; phone-first landscape, 16:9 target, sustained 30 fps on iPhone 15 Plus. | R |
| TECH-02 | Expressive real-time English/Japanese lip-sync using open-source/project-owned code; use the repository's specified dependency injection dependency. | R |

## 3. Proposed responsibility and component boundaries

Use the existing Unity client and relay as integration points. These are logical components; they do not require separate deployed services or a new distributed platform.

| Component | Owns | Consumes / produces |
| --- | --- | --- |
| Authored encounter data | Character definitions, immutable facts, clue/disclosure rules, legal actions, initial state, encounter prerequisites | Versioned content with stable IDs; no generated replacement for authored truth |
| Authoritative encounter service | Loop/world state, knowledge attribution, legal action validation, progression, reset and win predicates | Player intents and validated observations → committed state changes and events |
| NPC context builder | Character-specific projection, permitted actions, relevant same-loop memories | Authoritative state → instructions and context revision |
| Live session coordinator | Session identity/lifecycle, voice configuration, instruction refresh, transcription, tool routing | Input audio/text and projected context ↔ model; validated actions → world |
| Memory/fact store | Transcript records, claims, confirmed disclosures, character memory, retained player discoveries | Committed events and conversation records; retrieval for context/journal |
| Unity world adapter | Navigation, scene objects, range/visibility observations, action playback | Accepted actions → world execution; progress/failure observations → server |
| Presentation | HUD, conversations, camera, mood audio/lighting, animation/facial playback, pause | Player input → intents; committed events → visible feedback |

The client may present responsive walking, but it must not independently unlock clues, decide death, approve persuasion, or declare victory. **Open integration choice:** whether navigation/visibility is fully simulated server-side or Unity reports observations that the server validates against shared scene constraints. The first playable integration must explicitly document that trust boundary. Do not describe unvalidated client coordinates as server-authoritative physics.

Use one active foreground NPC session initially as a **proposed resource policy**, while making all four NPCs conversation-capable. This is not a source restriction on simultaneous/background speech; group conversation and overhearing require separate authoring if added.

## 4. Proposed data model and persistence boundaries

| Record | Minimum contents | Lifetime |
| --- | --- | --- |
| Encounter definition | Content version; actor/fact/clue/action IDs; initial mood/placements; recognition region; authored escalation, disclosure, reset, and success rules | Immutable for a playthrough |
| World state | Playthrough ID, loop ID/index, state revision, mood/transition, set progress, positions/actions, recognition flag, recording/exposure state, encounter phase, actor alive/dead state | Current loop |
| NPC state | Known fact IDs and provenance; beliefs; secret/disclosure status; claims/lies; attitude/relationship values if authored; current objective/action; same-loop memory | Current loop unless a specific exception is authored |
| Player progression | Discovered clue IDs, retained journal entries grounded in events, prior outcome knowledge, loop count | Across loops |
| Conversation record | Conversation/loop/NPC IDs; timestamped speaker turns; partial/final transcription distinction; tool requests/results; completion reason | Stored independently of live socket; retrieval follows loop rules |
| Claim | Speaker, content or authored claim ID, supporting fact IDs if any, audience, time, source event, whether a known deliberate lie | Record of speech, not truth |
| Confirmed fact/disclosure | Existing fact/clue ID, confirming rule/event, eligible recipients, source/provenance | NPC knowledge or player progression as appropriate |
| Action record | Request ID, session/loop IDs, actor/target, action type/args, evaluated revision, decision/reason, execution state | Until resolved; retained for diagnosis as needed |

Keep **truth**, **NPC belief**, **NPC claim**, **player discovery**, and **current physical evidence** distinct. A transcribed accusation cannot become a confirmed fact solely because a model repeated it. A player remembering the recording does not mean the new loop contains that recording.

Proposed memory implementation begins with structured records and bounded recent conversational context. Use summaries only as non-authoritative context tied to source records. Add embeddings only if concrete retrieval needs justify them; retrieval must still filter by NPC and loop before context assembly. Storage engine, transcript retention duration, and save/resume scope remain implementation decisions.

## 5. Encounter simulation and authoring surface

### 5.1 Recognition, witnessing, and escalation

Proposed recognition evaluation is a conjunction: current loop has not recognized; Maya is in the authored region; Theo is visible to Maya; affair partner is visible to Maya. The recognition event updates the flag and Maya's knowledge once. Recognition must be reproducible from actor positions and visibility evidence, not the model's opinion.

Author the room's recognition region, walkable routes, occluders, actor visibility targets, and Ren sightlines explicitly. The board's arrows are not final coordinates. **Open:** visual radius, field of view, ray targets, occlusion policy, and whether partial visibility counts. Debug overlays can display these in development; hidden geometric thresholds should not substitute for readable character behavior.

Track separate events for recognition, recording started/stopped, recording noticed, perceived exposure, threatening action, intervention, and catastrophe. Proposed opening progression follows the source's order, but exact causal prerequisites must be authored before a lethal outcome can be enabled. Entering a zone never directly supplies the missing threat action.

Witnessing an event and hearing a claim are different channels. Proposed events carry explicit recipients selected through visibility/hearing/disclosure rules. Private conversation with Luca does not broadcast to Theo. **Open:** whether overhearing is supported in the demo; if supported, author range, music effects, and audience attribution instead of giving all NPCs the transcript.

### 5.2 Mood and exposure

Encode source dispositions as character-specific rules/context, not one global compliance multiplier:

| NPC | Intimate | Aggressive | Additional conditions |
| --- | --- | --- | --- |
| Maya | Reflective, receptive to discretion | Impulsive, outspoken, more confrontational | Existing knowledge, player approach, current follow/wait state; default opening records |
| Theo | More approachable calmly, but audible accusations can heighten exposure | Defensive/forceful; loud music can also make him feel less observed | Actual recording, whether he notices it, audience and perceived exposure |
| Luca | More willing to disclose limited gossip when asked directly | Guarded, alert to threats | Knows fragments only; intervention requires an authored threat condition |
| Ren | Source supplies request/booth role | Source supplies request/booth role | Do not invent missing matrix values or request-acceptance thresholds |

Proposed mood transition commits an accepted target, coordinates track/lighting/UI transition, and refreshes active NPC context at the designated effective point. Define that point once so the player does not hear Intimate while simulation evaluates Aggressive without explanation. Transition duration, cooldown, request refusal, and autonomous DJ changes are open.

### 5.3 Navigation and action execution

Implement follow, hold position, and approach as persistent actor behaviors rather than one-shot dialogue effects. A wait acceptance must override following until its authored resume condition. Path failure, target movement, or interruption must report an execution result; acceptance alone is not proof that the actor reached the target.

Proposed action lifecycle: `proposed → rejected` or `accepted → executing → completed / failed / interrupted`. The simulation decides consequences; animation depicts them. An animation marker may report progress but cannot create an unrelated death or clue. Movement during conversation, wait timeout/resume policy, interruption priorities, and which actions require leaving conversation remain open.

## 6. Live conversation, context, and action contracts

### 6.1 Session lifecycle

1. Player focuses an eligible NPC and reaches conversation range; UI identifies the target.
2. Server loads the current loop and NPC memory, builds filtered context and allowed actions, and allocates a conversation/session identity.
3. Configure the intended NPC voice and initial instructions before enabling player speech/text submission. Expose connecting/ready/error states to the UI.
4. Route microphone or typed turns through the conversation. Produce transcript history and voiced character responses. Apply consequential requests through validation immediately when needed for gameplay.
5. Refresh context when relevant mood, witnessed events, knowledge, relationships, or action eligibility change. Record the context revision used for each consequential proposal.
6. Persist validated memory/claims/disclosures and finalized transcript turns as they become available, so a dropped connection does not erase already-committed gameplay.
7. On exit, NPC switch, reset, or terminal failure, close the session, stop its audio, release microphone resources, and finalize its completion reason. A future conversation rebuilds context from stored state.

Programming's `session.update` / `session.instructions` are source examples. They are not a verified provider transport recipe. Before implementation, verify the selected API/model's instruction updates, tool events, voice selection, transcript events, interruption, audio formats, and client/relay responsibilities against current official documentation and the actual relay. Do not silently substitute a different provider or unsupported event spelling.

**Voice mapping:** the Prompt for Astra specifies the following selections, also present in [server/src/prompts.mjs](../server/src/prompts.mjs). Preserve them in a single server-owned configuration. Source labels describe the intended performance; their presence in code does not independently verify provider availability or audible output.

| NPC | Voice | Source performance direction |
| --- | --- | --- |
| Luca | `meridian` | Masculine North American; measured, dry humor; distinguish heard information from known facts |
| Maya | `gleam` | Feminine North American; lively, affectionate teasing; soften when concerned |
| Ren | `quartz` | Feminine Australian; concise, dryly amused; distinguish what she saw from what she heard |
| Theo | `vesper` | Masculine British; polished, animated; quieter and deliberate about secrets |

Ren's art sheet identifies her as 29 and emphasizes that she cannot hear every conversation. Theo's age conflicts: art/prompt say 30, Character Guide says 32. Preserve this as a character-data decision instead of letting generated context alternate between ages; it does not block the action architecture.

### 6.2 Proposed NPC context contract

Provide character identity/style/objective; relevant situation and mood; known facts with origin; beliefs explicitly marked as beliefs; the character's own secrets/permitted lies/disclosure rules; appropriate relationships and prior exchanges; permitted actions and prerequisites; loop/session/context revision. Omit inaccessible world truth entirely. A director-level view, if used, must not be reused as a speaking NPC's instructions.

Do not place other NPCs' secrets or the player's whole journal into every prompt. User speech is conversation content, not permission to replace rules, access the full solution, or execute arbitrary commands. Reconstructed memory must distinguish an NPC's earlier lie from a fact it later learned.

### 6.3 Proposed action interface

This is a transport-independent design, not implemented endpoint names:

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
| Follow / wait / approach | Figma's Maya example; exact names proposed | Active actor/session; legal target; current action compatibility; navigation/range prerequisites; authored resume/interruption rules |
| Reveal clue | Programming `reveal_clue(clueId)` | Existing clue; NPC knows it; disclosure prerequisites met; recipient allowed; no duplicate award |
| Record claim | Programming `record_npc_claim(claim)` | Attribute speaker/audience and conversation; retain as claim; do not promote unsupported prose to truth |
| Change relationship | Programming `change_relationship(npcId, delta)` | Existing relevant actor; authored allowed dimensions/bounds; valid context and cause |
| Music request | Figma Ren/booth interaction | Supported mood; correct interaction authority; request acceptance/transition rules |
| Public speech / intervention | Figma encounter | Allowed audience/target; authored threat and action prerequisites; execution constraints |
| End conversation | Programming `end_conversation(reason)` | Valid active conversation and bounded reason; persist/teardown idempotently |

Validate schema, IDs, session ownership, current loop, relevant state freshness, prerequisites, and bounded effects. Deduplicate request IDs. Re-evaluate or reject stale proposals that depended on changed state; do not blindly apply a delayed acceptance after reset. Return only information the speaking character can receive.

Dialogue must reflect the decision. The integration must prevent an unvalidated model promise from being presented as completed gameplay. **Open transport design:** how speech is coordinated with tool completion in the selected Live API. A design that lets Maya assert success before a rejected tool returns needs correction or a clearly staged acknowledgement.

### 6.4 Failure and interruption behavior

Proposed behavior preserves committed state on model timeout, invalid tools, disconnection, and duplicate results; displays a retry/connection state; and never treats infrastructure failure as persuasion success or catastrophe. Reconnecting creates a session from authoritative state. Reset cancels or fences outstanding requests, stops prior audio, and clears stale subtitles/interaction targets. Track latency/failures for diagnosis without exposing internal reason codes in the player HUD.

Set-clock behavior during inference, conversation, pause, and reconnect remains an authored decision. Implement a single explicit clock policy after that decision, not independent timers in UI, server, and animations.

## 7. Unity UI, camera, audio, and character integration

| Surface | Build scope | Proposed implementation detail / open point |
| --- | --- | --- |
| Exploration | Isometric room, readable movement, green loop-shaped overhead player beacon with gentle bob/pulse | Beacon remains readable in all lighting; a floor ring appears in the mockup, but whether it marks selection, interaction, or destination remains open |
| Interaction | People use speech-bubble markers; objects use diamond-dot markers. Outlined available, filled focused, dim out-of-reach; one contextual prompt at a time | Central interaction target model drives marker/HUD; `E` talk/inspect appears in desktop references and requires mobile adaptation |
| Conversation | Gold/black Art Deco panel, name/role, speech text, typed-reply field, mic/send, History and Leave; optional right portrait in symmetrical frame | References show Enter to send and hold V to speak; mobile gestures/input semantics remain to be finalized. Maintain connecting/listening/responding/error states |
| Camera | Three stages: overview, move closer, close conversation in the club | Preserve selected speaker and restore navigation context; do not use the separate voice studio as the final conversation transition. Duration, input lock, framing/collision details open |
| HUD/journal | Loop, mood/music, retained clue, understandable request consequence | Set-progress display depends on timer rules; confirmed discoveries and conversation claims must not be visually conflated |
| Pause | Reference menu: Resume, Settings, Controls, Main Menu, Quit; Esc resumes | Desktop Quit/Esc need mobile adaptation; simulation/audio/session clock effects and exact settings remain open |
| Audio | Two mood tracks, intelligible voice, reset cue | Mixer/ducking and transition rules to choose; speech/lip-sync use actual played audio and cancellation state |
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
| Catastrophe and rewind | Exact clip/staging remains blocked on authored victim/action; rewind visibly restores the configured attempt |

For each imported clip, record canonical rig/version, source provenance, duration, looping, root-motion policy, props/contacts, supported actors, action mapping, and known defects. A Ready sheet row still requires runtime visual verification. **The sheet does not supply explicitly authored Maya recording or Luca intervention coverage.** Its possible-victim `Dying Backwards.fbx` does not settle who dies or establish the final lethal action/staging; the rewind remains reference-only, and Ren's mixing remains To Do. Turn these gaps into authoring/animation tasks after artist review rather than treating collection as encounter coverage.

## 9. Proposed build sequence and ownership seams

| Step | Concrete deliverable | Exit evidence / dependencies |
| --- | --- | --- |
| 1. Content contract | Stable IDs, initial state, knowledge table, legal actions; decision log for violence, success, timing, and disclosure | Designers can explain the opening and one valid alternative without missing causal rules |
| 2. Authoritative encounter core | State store, validation, recognition/witness attribution, reset boundary, event/action records | Deterministic tests for authored transitions, once-per-loop recognition, invalid actions, and stale-loop rejection |
| 3. World execution | Club routes/regions, Maya follow/wait/approach, action progress/failure reporting | Accepted wait holds while player leaves; zone-only catastrophe cannot happen |
| 4. Live gameplay integration | All four contexts/voices, on-demand sessions, refresh, validated tool bridge, transcript and memory/fact stores | Real natural-language action affects world; later conversation preserves same-loop memory; knowledge isolation holds |
| 5. Music and interaction presentation | Mood transition, coherent lighting/audio/HUD, interaction states, history/text/mic, camera and pause | Changed mood is perceptible and subsequent context uses it; UI accurately reflects range/session/action state |
| 6. Authored encounter completion | Opening chain, chosen catastrophe, rewind, retained clue, alternative attempt, resolution/end-of-set win | Whole encounter passes acceptance, including no win by passive avoidance |
| 7. Character/animation integration | Use completed download manifest; artist-owned imports, action mappings, missing-coverage work and facial/body coexistence | Actual imported clips demonstrate required coverage on both shared rigs; Ren cap toggles independently |
| 8. Device and failure validation | Packaged phone-target playthrough, sustained performance evidence, network/session interruption checks | Target-device performance plus recoverable failures without corrupting progression |

Character preparation and content authoring can proceed alongside encounter/session work through the agreed rig and action contracts. Avoid simultaneous edits to the same Unity scene or generated character assets; coordinate ownership in the `lucid-loop` agent-bus space under `astra-lead-engineer` and the relevant specialist. These work packages are implementation scope, not completed assignments.

## 10. Acceptance plan

These checks are future gates, not test results. Combine deterministic rule tests, relay/session integration checks, and visible playable/device evidence. Mocked model responses can prove validation, but cannot alone prove real Live gameplay or voice quality.

| Check | Required evidence | Requirements |
| --- | --- | --- |
| A01 Opening causality | Capture actual recognition → recording → approach → intervention → authored catastrophe → rewind; no missing causal action | ENC-01, ENC-04 |
| A02 Recognition boundaries | Player-only entry; Maya sees only one participant; occluded participant; both visible; repeated entry; new loop | ENC-03 |
| A03 Alternative agency | Natural-language Maya wait request accepted at runtime; Maya stays as player goes to Luca; later intervention changes the sequence | ENC-02, ENC-06, AI-01 |
| A04 No shortcut outcomes | Confrontation-area entry cannot kill; indefinite avoidance cannot complete; authored resolution can complete at set end | ENC-05, ENC-07 |
| A05 Mood behavior | Both requested moods render/play correctly; active NPC context refreshes; evidence/exposure still affects Theo and Intimate does not guarantee Maya compliance | MOOD-01–03, AI-04 |
| A06 Four NPC conversations | Each named NPC opens with the intended voice/context and accepts speech/text under the agreed UI contract | AI-01, UI-02 |
| A07 Knowledge isolation | Tell Luca something privately; inspect Theo context and behavior; no leak. Distinguish false claim from confirmed fact | AI-02, AI-03, AI-07 |
| A08 Memory reconstruction | End/reopen same-loop session; preserve claims, disclosures, relationships. Reset loop; ordinary NPC memory clears and intended player clue remains | AI-05, LOOP-01 |
| A09 Validation | Reject unknown clue, unavailable action, excessive relationship delta, duplicate request, wrong session, stale loop; valid action commits once | AI-06 |
| A10 Transcript/fact separation | Finalized conversation is stored automatically; partial/repeated events do not duplicate turns; an unsupported statement never enters confirmed facts | AI-07 |
| A11 Session lifecycle | Relevant mid-session update affects subsequent response; switch/reset/disconnect stops old audio/tools; reconnect restores committed state | AI-04, LOOP-01 |
| A12 Presentation coherence | Available/focused/out-of-reach and people/object markers, green beacon, history/text/mic, camera return, pause behave consistently with actual world/session state | UI-01–04 |
| A13 Animation provenance and playback | Every downloaded asset is traceable; coverage table names actual imported clips; both rig archetypes play correctly; Ren female rig and separate cap verified | ART-01, ART-02 |
| A14 Target quality | Sustained 30 fps on iPhone 15 Plus in the integrated club; readable landscape UI; real English/Japanese speech performance without prohibited lip-sync dependency | TECH-01, TECH-02 |

## 11. Decisions that still block final implementation

| Priority | Open decision | Required output |
| --- | --- | --- |
| Before final encounter rules | Victim, lethal action, responsible actor, prerequisites, and Ren visibility significance | Authored event/action chain and staging; no inferred murder solution |
| Before progression completion | Valid danger-resolution route(s), set duration, end predicate | Executable success rule that excludes avoidance |
| Before clock integration | Time during conversation, inference, pause, disconnection and camera transition | Single clock policy |
| Before visibility validation | LOS/occlusion details; overhearing support; client/server spatial trust boundary | Scene/rule contract and validation policy |
| Before persuasion completion | Action eligibility, relationship bounds, disclosure conditions, exposure/escalation prerequisites | Per-character rule data and legal action schemas |
| Before companion/music polish | Wait/resume/interruption semantics; track request acceptance/cooldowns/effective transition point | Behavior and transition contracts |
| Before session integration | Selected provider/model/transport compatibility, instruction refresh and speech/tool ordering | Verified integration configuration/event contract preserving the recorded voice mapping |
| Before loop narrative lock | Initial retained clue, physical evidence reset, Ren cross-loop memory, opening guidance/agency | Reset policy and authored opening configuration |
| Before animation lock | Artist import/retargeting records against the completed download manifest; actual clip-to-action coverage, props and contact staging | Reviewed asset mapping; missing-animation work list |
| Before shipping persistence | Save/resume requirements and transcript retention scope | Storage/lifecycle policy appropriate to this demo |

Embeddings, additional service layers, generic narrative editors, and extra AI agents are not prerequisites unless a specific implemented need establishes them. The immediate technical goal is a bounded authored encounter whose conversation, memory, actions, and presentation remain consistent across a visible rewind.
