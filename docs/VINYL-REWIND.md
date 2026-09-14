# Vinyl time-loop transition

A six-second in-game URP transition, starting from the real gameplay camera. Registered actors retrace a bounded history before the authoritative checkpoint is restored under black cover. No movie, storyboard image, CPU screenshot capture, level rotation, or negative time scale is used by the runtime effect.

## Run it

The main `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity` scene and its existing `GymRenderer.asset` are configured. Enter Play mode, start the encounter using the local relay, reach catastrophe/unresolved/victory, then press **Rewind**. No additional confirmation is needed after the transition.

For an offline rehearsal, choose **Lucid Loop → Preview vinyl rewind** while disconnected. This uses a local rehearsal checkpoint; it never creates or resets a server game. It records and replays the actual scene, but is not a substitute for the live server reset test.

The local checkpoint demo at `.local/btd-review/Unity` uses the same shared runtime with a `FirstLoop` adapter. Its authored rewind triggers the controller automatically. **Lucid Loop → Preview vinyl rewind** also performs a real demo checkpoint reset. A [focused checkpoint patch](integrations/README.md) is included for teammate integration. Main and the checkpoint remain different games: main has server-authoritative movement; the checkpoint has local NavMesh movement. In this repository Maya is a separate NPC from the player. Their existing roots, visual children, feet placement, and billboard hierarchy are preserved.

For another scene, stop Play mode and run **Lucid Loop → Time Loop → Set up current scene**. Setup reuses the scene controller and the project's existing Universal Renderer; it does not create cameras, EventSystems, or duplicate renderer features. A runtime auto-setup pass discovers the project's registered characters, name labels, HUD canvases, audio, and known movement writers. Existing explicit registrations and asset assignments are retained. New UI and one runtime material are created when triggered and cleaned up/reused appropriately.

## Controls

Select **Time loop transition**. The Inspector exposes eight stage durations and easing curves, effect center (viewport coordinates, default 0.5/0.5), optional music-source transform, groove opacity, rotation, distortion, blur, spiral contraction, tonearm visibility, glow, darkening, reduced motion, music playback offset, clips, and emblem/font overrides. The runtime Inspector provides **Trigger loop** and **Cancel and restore control** buttons.

`TimeLoopTransitionController.TriggerLoop()` is the public entry point; `IsTransitioning` and static `InputBlocked` expose the gate. `TransitionStarted`, `CheckpointRestored`, and `TransitionCompleted` are Inspector UnityEvents. Duplicate calls during an active transition are ignored. Debug input defaults to F8 and can be disabled or changed with `DebugKey`. This project uses legacy Unity input; no Input System package or new input asset was introduced. Main's live reset retains the existing terminal-phase restriction. Use offline Preview for rehearsal without a finished encounter.

| Stage | Default interval | Behavior |
| --- | --- | --- |
| Freeze | 0–0.20 s | Clear pending input, pause registered activity/audio, dim and fade HUD/labels |
| Grooves | 0.20–0.80 s | Uneven magenta grooves and center point over the stationary club |
| Rewind begins | 0.80–1.40 s | Backward screen rotation, restrained smear, separate tonearm and caption |
| Recent history | 1.40–2.90 s | Registered actor paths and visual samples replay backward |
| Spiral inward | 2.90–3.70 s | Club disappears into black vinyl with pink spiral and corner reflections |
| Loop closes | 3.70–4.70 s | Covered checkpoint restore, spiral/emblem morph, confirmed dynamic loop title |
| Reset cover | 4.70–5.00 s | Emblem fades while title and memory subtitle remain |
| Return | 5.00–6.00 s | Gameplay/HUD and restarted music fade in; input stays locked until complete |

Checkpoint acknowledgement may extend the covered interval. A failed restoration stays covered and shows **Return to game**; cancellation releases transition-owned state. A server command already sent cannot be unsent: the server remains authoritative, and reconnecting resynchronizes any late committed reset. The controller never retries the reset automatically or guesses a loop increment.

## Responsibilities and source

- `Assets/Gyms/Runtime/TimeLoop/TimeLoopTransitionController.cs`: presentation timeline, input shield, undistorted text, music/SFX lifecycle, public controls, events, failure/cancellation cleanup.
- `TimeLoopFullScreenFeature.cs` and `Assets/Gyms/Resources/Rewind/TimeLoopFullscreen.shader`: built-in URP Full Screen Pass subclass, camera color fetch after post-processing (including transparent scene objects), aspect-correct grooves, screen-only twist/blur, vinyl, tonearm and procedural emblem. URP owns the intermediate render resources. Only the selected gameplay camera receives the pass.
- `LoopRewindActor.cs`: preallocated circular history buffer, default six seconds at 20 Hz. Records root position/rotation/scale, explicitly registered child local transforms/active flags, and displayed SpriteRenderer frame/enabled/flip/color states. Positions and rotations interpolate; discrete sprite/active states select the earlier sample. Static sprites are preserved. The buffer is bounded, not the checkpoint.
- `LoopActivityLease.cs`: cooperating reference-counted pause ownership. Disabled components remain disabled; another lease keeps its lock after the transition releases its own. Existing time scale and global audio pause are never changed.
- `LoopResetAdapter.cs`, `LocalLoopCheckpoint.cs`, `LoopObjectCheckpoint.cs`: reset contract and an offline checkpoint implementation with actor poses, participant state, clock, and separate persistent clue/unlock lists. The checkpoint is captured independently from recent history. Objects/doors with active-state changes can use `LoopObjectCheckpoint`; other routine/state machines implement `LoopCheckpointParticipant`.
- `Assets/Gyms/Runtime/Encounter/EncounterLoopResetAdapter.cs`: existing server reset, pause acknowledgement, complete new-loop world acknowledgement, actor visual restoration. The server owns the real clock, routines, interactables, discoveries, dialogue unlocks and loop increment. `EncounterRewindTransition` remains a thin compatibility bridge for the existing HUD.
- `Assets/Gyms/Editor/TimeLoopSetup.cs`: repeatable scene/renderer setup. `TimeLoopTransitionInspector.cs` supplies testing buttons.

## Supported scope and remaining assignments

No visual asset assignment is necessary to try it. Grooves, record, tonearm and loop emblem have procedural defaults. For exact art matching, assign `EmblemSprite`. Assign a redistributable serif font to `LoopTitleFont` for consistent mobile typography; the editor currently uses an available Georgia/Times/Noto Serif fallback. The transition subtitle uses Unity's built-in font.

Existing `MvpAudio/RecordScratch` or `Backspin` is discovered automatically. `RewindClip` and `CloseClip` are optional; assign an approved musical click/impact to `CloseClip` if desired. Transition SFX use a separate 2D AudioSource that ignores listener pause. Music sources explicitly pause, seek to the configured loop-start position, and fade back. Muted/previously paused sources are not silently started.

Add a `LoopRewindActor` to additional actors and register their movement writers. Known project scripts are paused automatically; NavMeshAgent paths are cleared and agents/Animators are suspended while transforms are replayed. A root Rigidbody is temporarily kinematic; successful resets clear its velocity. Arbitrary physics simulations, particles, Animator state machines, custom inventories, and arbitrary scripts are **not** reversed. Their game-specific checkpoint state needs a participant/adapter. Cooperative additional pause owners should use `LoopActivityLease` and keep their own input gates; unrelated systems must not overwrite the transition's owned component flags.

The checkpoint adapter also pauses its separate `ExpandedClub`/`PaintedBackdrop` camera drivers, cancels old story/chat callbacks, restores its defined starts, phone/routine/music state and loop clock, and calls `LoopState.Rewind()` once. Remembered discoveries and existing conversation history survive. It resumes directly to gameplay.

## Validation

Unity 6000.3.24f1, URP 17.3.0, Universal Renderer, Render Graph enabled, Metal on this Mac. Packages were not upgraded. The final batch compile and idempotence check passed: one controller, one renderer feature, unchanged camera/EventSystem counts, and no shader compilation errors (`.local/time-loop-final-setup.log`).

Tests cover bounded/interpolated history and sprite frames, duplicate triggers, checkpoint versus history separation, retained clue/unlock IDs, delayed and failed reset handling, cancellation/disable before and after commit, cooperative pause ownership, unscaled-time playback, UI cleanup, and the actual server encounter reset. Screenshot helpers intentionally skip capture in batch mode; interactive Editor captures validate the rendered result separately. Ten distinct Play Mode checks passed across the latest runs: seven transition tests (`.local/time-loop-cover-tests.xml`), the actual server encounter and pause-menu test (`.local/time-loop-final-tests.xml`), and the voice pause/reconnect test (`.local/time-loop-audio-pause-tests.xml`). The earlier aggregate run had one fixture-dependent timeout because localhost:8790 was absent; that exact test passed after the synthetic fixture was started. No OpenAI calls were used. The test fixture was stopped afterward; the pre-existing game relay was left running.

The interactive checkpoint validation passed two real resets: NavMesh path replay visibly moved backward, the gameplay camera stayed fixed, each transition incremented once, remembered information survived, paths were cleared, and control returned. Editor captures were inspected at 2560×1440 (16:9) and 2520×1080 (21:9). The checkpoint preserves its authored 16:9 camera viewport on ultrawide displays; transition covers now hide stale UI in the pillarboxes. Main's normal full-viewport renderer supports the actual camera aspect.

Showcase: [10-second checkpoint demo recording (MOV, no audio)](images/time-loop/vinyl-time-loop-demo.mov). This is an interactive Unity capture of the local checkpoint demo, whose environment and dialogue differ from main. The shared transition runtime is the same; the main integration uses its server-authoritative reset adapter.

Screenshots: [black vinyl at 16:9](images/time-loop/vinyl-16x9.png) · [covered loop title at 21:9](images/time-loop/loop-21x9.png).

The focused checkpoint patch was applied to a temporary copy of the exact checkpoint source and checked against the shared runtime byte-for-byte. The local review project was compiled and tested directly; a separate clean Unity import of the portable patch was not run.

Physical iPhone performance, device microphone behavior during interruption, and mobile font appearance have not been tested in this change. Reduced motion preserves reset timing/authority and replaces strong rotation, distortion and visual history motion with grooves and fades.
