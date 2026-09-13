# Unity encounter integration plan

**Inspection:** 2026-09-15, read-only code review. No Unity process launched, tests run, transport implemented, or character-art files edited. This is a proposed implementation plan against the inspected gyms, not completion evidence. Gameplay authority and unresolved story rules remain governed by [TECHNICAL_DESIGN.md](TECHNICAL_DESIGN.md).

## 1. Minimal integration direction

Keep the club as the playable space and reuse its navigation, hold gesture, DI root, safe-area UI, and camera interpolation. Extract the Live gym's session/audio lifecycle into a scene-independent controller used by club conversations. Connect a separate encounter adapter to server-owned state. Do not attach `LiveGym` wholesale to the club: its `Start` constructs a lab HUD, its character selection deactivates every other NPC, and its scene switching moves to the separate studio.

Preserve the two gyms as regression fixtures. Prefer an explicit encounter mode in the builder and interaction controller for the first integration; avoid large renames of `OfflineGym` while smoke checks and generated scenes reference that concrete class. Once the integrated encounter is established, naming cleanup can be a separate change.

## 2. Exact existing seams and edits

All short runtime paths below are under `Unity/Assets/Gyms/Runtime/`. Proposed new names are contracts to agree with implementers, not existing symbols.

| Existing file / method | Reuse | Minimal change |
| --- | --- | --- |
| `GymRoot.cs`: `GymRoot`, `GymSession` | Hierarchy DI with `[Cached]` / `[Resolved]`; 30-fps target | Keep `GymSession` as local interaction presentation state. Cache an `EncounterClientState` and coordinator separately; never put authoritative game decisions into `GymSession` |
| `OfflineGym.cs`: pointer handling, `Walk`, `Approach` | Hold cancellation; UI hit rejection; NavMesh complete-path checks; sampled approach positions | Route encounter-mode intents through coordinator. Preserve local player movement responsiveness under the agreed spatial trust contract; do not locally approve NPC actions |
| `OfflineGym.cs`: `BeginConversation` | Stop player movement, face actor, select camera target | Replace sample text branch in encounter mode with `OpenConversation(actor.Id)` after authoritative eligibility. Selection/range validation must finish before microphone submission |
| `OfflineGym.cs`: `EndConversation` | Gesture reset, floor/camera return | Call session stop and reset playback/facial output before releasing presentation; reject late callbacks by conversation generation |
| `Live/LiveGym.cs`: `Connect`, `Handle`, `ReadAudio`, `StopMic`, `Shutdown` | Permission checks, capture, startup timeout, mute acknowledgement, close/final usage, audio queue | Extract reusable lifecycle/audio code into `LiveConversationController`; lab UI becomes an adapter. Controller exposes typed state/events, not direct `Text`/`Button` dependencies |
| `Live/LiveConnection.cs` | Threaded WebSocket, serialized send, bounded events/audio upload, receive validation, disposal | Keep provider/relay transport owned by transport engineer. Add only agreed application commands/envelopes; do not invent unsupported `session.*` events |
| `CharacterActor.cs` | Stable ID, visual/reference association, face anchor | Add explicit talkability and optional world-action/speech bindings. Current `!IsPlayer` test is inadequate for a non-interactable affair partner. Keep amplitude mouth as a labeled primitive fallback |
| `GymCamera.cs`: `Present`, `Overview`, `Apply` | In-room interpolation and close-up offsets | Make solo-study renderer hiding explicit opt-in; integrated encounter cannot globally hide actors performing causal actions. Preserve restoring only renderers this component hid |
| `GymUI.cs` | Safe area, Canvas, basic buttons/input | Build conversation view with a real scrollable history and typed submission; do not use the current fixed-height `Label` as permanent history |
| `Editor/GymBuilder.cs`: `BuildClub`, `Actor` | Durable generated scene ownership, room geometry, NavMesh bake | Add encounter mode bindings, movable NPC agents, separate non-talkable affair partner, world adapters and audio output. Keep final character prefab/import work artist-owned |
| `GymSmoke.cs` | Existing four-NPC approach/camera and fixture transport exercise | Retain baseline paths; add an encounter-specific smoke path once server fixture contract is available |

`GymBuilder.Actor` currently creates carving `NavMeshObstacle`s for ordinary NPCs; `BuildClub` adds a `NavMeshAgent` only to the player. A moving Maya must not retain an active carving obstacle fighting her agent. Decide stationary/moving obstacle policy explicitly and test player approach near her. `Characters` currently serves multiple purposes; use an ID registry for all actors and a separate talkable set for the four NPCs.

## 3. Proposed classes/interfaces and ownership

Use logical boundaries within the current project; there is no need for a new service per class.

| Proposed type | Responsibility / minimal contract | Suggested owner |
| --- | --- | --- |
| `EncounterClientState` | Read-only latest accepted snapshot: loop ID, revision, actor action states, mood, player-visible clues. Apply ordered updates; ignore duplicates and obsolete loop data | Unity encounter engineer, schema agreed with server engineer |
| `IEncounterGateway` | Open/resume playthrough, send player intent, receive snapshot/events, report action execution/position observations. The methods are application concepts, not proposed wire event names | Server/transport engineer implements protocol; Unity engineer consumes |
| `EncounterCoordinator` | Main-thread queue drain; bind stable actor IDs; route intents; apply committed updates; orchestrate loop/session fences and reset | Unity encounter engineer |
| `NpcActionExecutor` | Execute accepted follow/wait/approach state using NavMeshAgent; report completed/failed/interrupted observations with loop/action ID | Unity encounter engineer |
| `LiveConversationController` | One selected NPC foreground session; ready/connecting/closing/error events; mic mute; player text intent; transcript/audio events; start/stop generation | Voice integration engineer |
| `ConversationView` | Name/role, transcript history, typed input/send, mic state, leave, connection feedback; calls controller and renders events | Unity UI engineer |
| `TranscriptStore` | Ordered timed fragments keyed by conversation and application sequence; retain speaker/timing. Grouping into turns is revisable presentation, not authoritative completion | Voice/UI engineer, server persistence remains authoritative |
| `ICharacterSpeechSink` | Apply a timed speech frame to the chosen actor, clear all speech on stop, surface missing-pose diagnostics | Lipsync engineer supplies adapter; character artist supplies profile/rig |
| `CharacterMotionBinding` | Exactly one body animation owner maps accepted actor action + expression to reviewed clips on the correct shared rig | Character artist and Unity animation engineer agree seam |

The existing Gyms asmdef does not reference `LucidLoop.CharacterArt`. If the speech adapter uses `CharacterFaceDriver`, either add a one-way Gyms reference to `LucidLoop.CharacterArt` or place the adapter in a small integration assembly referencing both. Do not create a reverse dependency from artist runtime to Gyms. Start with the one-way reference unless a concrete assembly constraint justifies the extra assembly.

## 4. State flow and follow/wait behavior

1. Opening the encounter loads the server's current snapshot and stable actor IDs. The client reports readiness after scene bindings exist; missing/duplicate IDs are a setup error, not silently omitted actors.
2. The player approaches one of Maya, Ren, Luca, Theo. Coordinator requests a conversation with the relevant loop/revision/actor. Server prepares the filtered context and selected voice; UI moves from connecting to ready before capture/text submission is enabled.
3. A natural-language wait request produces a server decision. Unity receives the committed actor action and applies it exactly once. Spoken agreement alone never calls `SetDestination` or changes action mode.
4. For accepted wait: stop the agent and reset its path; capture the approved hold position; turn off follow repathing. Leave waiting active after the player closes conversation and walks toward Luca.
5. For accepted follow: maintain the authorized mode while periodically updating a reachable destination near the moving target, with a dead band to avoid repathing each frame. Navigation failure is reported; it does not become permission to teleport or abandon wait.
6. For accepted approach: path toward the specific existing target or authored position; report arrival/failure/interruption tagged with action ID. Server decides ensuing facts or progression.
7. On reset: advance the loop fence first, stop old conversation/capture/playback and action execution, clear pending approach/UI selection, apply authoritative placements and actor modes, then resume. Use `NavMeshAgent.Warp` only for an accepted reset placement on the NavMesh; verify the return value and report invalid scene authoring.

Spatial authority remains an explicit integration decision. Current Unity navigation can act as the motion executor with bounded position/path observations, while the server owns actions, facts, and progression. Calling that arrangement server-authoritative physics would be inaccurate unless positions/LOS are independently simulated or validated there. Recognition/witness reports cannot bypass server checks, and an animation callback cannot directly decide death or success.

Use one transition coordinator for NPC switch, Leave, app suspend, reset and connection failure. `LiveGym.generation` already protects permission completion; extend that fence to incoming events, typed submissions, audio/viseme buffers and world action observations. Old final transcript/usage records may be stored under their original conversation, but must not appear as the new speaker or mutate the new loop.

## 5. Four-NPC conversation and transcript UI

Do not reuse `LiveGym.Select` in the club: it calls `gameObject.SetActive(i == index)` on all actors. Instead resolve the active speaker by ID and leave the world active. The existing server voice mapping is Maya `gleam`, Ren `quartz`, Luca `meridian`, Theo `vesper`; the server owns this configuration.

`LiveGym` currently appends input/output deltas into two strings, truncates each to 420 characters, and clears both on connect. The actual Live stream provides timed fragments with no authoritative completed-turn events or item IDs. Store source fragments with conversation, speaker, timing and application sequence independently of the view. Grouping them into readable turns is revisable presentation; a time gap or locally assigned ID does not establish provider turn completion. Do not invent final events or treat grouped captions as confirmed gameplay facts. The UI may page bounded history while the application memory/fact store remains separate.

Typed replies need an actual end-to-end supported relay/provider input path; `LiveConnection.Command(type)` carries no text payload and the inspected relay whitelist permits audio/mute/close only. Define the interface now, and keep Send disabled with clear unavailable state until transport implements it. Do not simulate sent text with local sample NPC responses. Text-only use should not be blocked by a blanket microphone permission request: request microphone access when enabling voice, preserving typed conversation when the selected transport supports it.

Keep name/role, current captions, History, typed input/send, mic and Leave on the in-room panel. Final history needs `ScrollRect`/content sizing or equivalent using the current uGUI dependency. Preserve literal text rendering (`supportRichText = false`) for model/user content. UI focus must prevent Enter/Escape/hold-V and pointer gestures from also causing floor movement. Mobile keyboard/safe-area behavior needs actual device validation; desktop key examples do not settle mobile controls.

## 6. Speech and animation integration evidence

The inspected `CharacterArt/Runtime` has real facial interfaces beyond the primitive gym mouth:

- `CharacterFaceDriver.SetViseme(label, weight)` and `SetVisemes(IReadOnlyList<VisemeWeight>)` support the legacy 15-label adapter; normalized weights are converted to 0–100 blendshape weights.
- `SetSpeechPose(SpeechLanguage, poseId, weight)` resolves character-specific bilingual profiles and reports missing targets without silently substituting English. Calling it repeatedly clears previous speech weights; it is not a public multi-pose blending API.
- `ResetSpeech()` clears speech state; `LastSpeechDiagnostic` and `MissingShapeNames` expose binding failures.
- `BilingualSpeechProfiles.cs` has English/Japanese pose metadata and review timelines. Review sequences are authored demonstration timing, not live audio alignment.
- `FacePoseComposer` gives speech priority over contact-critical lower-face expression channels and composes independent blinks. Required Japanese U/FU/tap R and English L targets are recorded in [BILINGUAL_SPEECH_RIG.md](BILINGUAL_SPEECH_RIG.md).

No live acoustic analyzer or played-audio-to-bilingual-frame scheduler was found in the inspected Gyms/CharacterArt runtime. The existing HeadAudio reference in the contract does not prove a Japanese-capable implementation. This bounded finding does not rule out independent work elsewhere; ask the lipsync owner for their active handoff before creating a competing analyzer.

**Immediate integration seam:** the audio controller exposes conversation-tagged PCM with application sequence and played sample position; provider utterance IDs are not supplied; a lipsync adapter receives validated analyzer frames and schedules them against actual playback. Network receipt time and transcript arrival time are not audible time. The existing `ReadAudio` callback computes energy from samples read out of the ring; preserve this only as the primitive diagnostic fallback. Do not call Unity mesh/Transform APIs from the audio callback; queue timed data for main-thread facial application. Reset/interrupt/underrun must clear or explicitly hold the appropriate scheduled pose, not continue the previous NPC's mouth.

**Required handoff:** a public weighted bilingual speech-frame method that can compose neighboring poses, report unknown/missing targets and accept playback timing. `SetSpeechPose` suffices for isolated-pose smoke checks, but does not prove coarticulation or the full English/Japanese target. The lipsync/artist owner should define or extend this interface; do not bypass the existing composer by writing competing blendshape weights from Gyms.

**Body animation conflict to resolve before import:** `CharacterFaceDriver.StartMotion` already creates an `AnimationLayerMixerPlayable`, drives idle and masked expression gesture clips, and binds it to the character Animator. Its `LateUpdate` adds post-animation head/eye offsets. A second Animator controller or PlayableGraph for follow/wait/recording can compete with that graph. Agree one animation owner, either extending that mixer through an artist-owned action interface or allowing external ownership while retaining facial/blink composition. Setting `IdleEnabled = false` only pauses idle playback; it does not transfer ownership of the graph. Keep the user-confirmed two skeletons and separate Ren cap; imported FBX metadata is not proof of runtime compatibility.

## 7. Implementation order and verification routes

1. Agree DTOs, session/event fences, spatial trust boundary and motion ownership with server, lipsync and artist owners. Build an encounter gateway fixture capable of snapshots and accepted/rejected wait/follow decisions.
2. Add main-thread state/coordinator and NPC executor; verify wait survives conversation exit and player movement, and only accepted follow resumes it.
3. Extract Live controller without changing provider event names; keep existing lab functional. Wire club approach to all four NPC sessions without switching scenes or disabling other actors.
4. Add durable-turn UI and supported typed input once transport lands; join server memory/context refresh and confirmed-fact updates.
5. Attach the reviewed speech/motion adapters and add reset fencing. Integrate authored recognition/mood/progression only from agreed server rules; do not invent the missing lethal action or resolution.

Existing test/build routes, inspected but **not run here**:

| Route | What it establishes / limitation |
| --- | --- |
| `Unity/Assets/Gyms/Tests/Editor` (`GestureTests`, `AudioTests`) | Existing hold/audio utility regressions; add meaningful state/fence/transcript tests to this test assembly. They cannot prove navigation or actual Live audio |
| `Unity/Assets/CharacterArt/Tests/Editor/BilingualSpeechProfileTests.cs` | Existing profile mapping/diagnostic/composition tests; artist/lipsync owner extends where their interface changes |
| `python -m unittest discover -s tools/ci -v` | Build-script logic only; does not compile C# or launch Unity |
| `npm test` in `server/` | Server test script; see current package scripts and tests for exact coverage. Fixture transport is not actual provider gameplay acceptance |
| `python tools/ci/build_gyms.py --platform windows` | Launches pinned Unity, runs EditMode tests and packages committed scenes via `GymCIBuild`. Use coordinated dedicated build host; not while another editor owns the same project |
| `.github/workflows/unity-builds.yml` | Existing Windows CI uses the above route and publishes diagnostics/packages. Verify the build includes the new encounter scene/configuration; a green two-gym package alone does not prove integration |
| Development player `-gym-smoke <output>` with fixture | Existing smoke covers DI, four-NPC navigation/framing and basic audio/transcript/close. Add a distinct encounter acceptance mode rather than relabeling this as gameplay proof |

The current CI script accepts at least eight passing EditMode tests; that numerical threshold does not prove newly added tests ran. Check named tests/results and the packaged scene list. Local `.local/*-compile` projects found during inspection are character-study scaffolds, not a verified Gyms compiler route. Do not claim a Roslyn/stub compilation or a server test substitutes for Unity import, UPM source generation, NavMesh behavior, device performance or audible bilingual animation.

Minimum new encounter evidence: all four real sessions in the club; one accepted and one rejected wait request; player leaves while accepted wait persists; server-directed follow resumes; duplicate and stale-loop actions cannot replay; reopening preserves server memory; switching/reset stops old audio and mouth; timed transcript fragments remain ordered without duplicates and presentation grouping stays revisable; failed path reports honestly; other actors remain visible during causal actions. Full-game completion still needs the authored catastrophe, resolution and device gates in the technical design.
