# Ren speech in the encounter

`EncounterSpeechBinding` routes accepted PCM16 packets and consumed-output sample counts from `EncounterVoiceController` to the speaking actor's active face adapter. An assembled Ren uses `RenLiveSpeechFaceAdapter`; actors with the earlier face rig use `LiveSpeechFaceAdapter` and `CharacterFaceDriver`.

The Ren adapter analyzes English with the pinned splatterfacegames lip-sync model. It forwards semantic speech weights to `RenLOD0Controller`, which composes speech, expression, blink and cap state and owns the final renderer weights. Playback timing comes from the active audio renderer, including the native iOS output path; receiving a network packet does not advance the visible mouth.

A new stream clears the previous speaker. Completion, cancellation, disable and unsupported sample rates clear the current speech pose. Packets and stop/progress callbacks from previous generations cannot change a newer conversation. The supported output is mono PCM16 at 24 kHz; Japanese voice remains paused.

The actor's active hierarchy must contain the Ren adapter with its `FaceController` reference set. Merely installing the prefab in LiveGym does not change the build scene: the BeforeTheDrop scene needs the same actor presentation binding. Prefab installation is coordinated with the character artist. Actual provider conversations, iPhone audio timing and perceived lip-sync quality still require device verification.

Validation: the assembled prefab is installed in `Assets/Gyms/Scenes/BeforeTheDrop.unity`. The shared Unity Editor passed all 91 encounter EditMode tests, including five new stream-routing/lifecycle regressions; see [test results](validation/ren-encounter/editmode.xml). This verifies routing and resets, not live provider or physical-device lip-sync quality. Reapply the placement through **Lucid Loop → Encounter → Install assembled Ren** if rebuilding the encounter scene.

The actual scene also passes the [PlayMode presentation test](validation/ren-encounter/playmode.xml): assembled bounds/anchor, controller-only mesh writes, A with independent blink, MBP contact suppression, stale-generation stop protection and reset across rendered frames. The [conversation capture](validation/ren-encounter/04-conversation-rest.png) uses the real Ren selection and Connection toggle while disconnected. This is deterministic presentation evidence, not provider or iPhone acceptance. Run **Lucid Loop → Validate assembled Ren encounter PlayMode** with the Game view visible.

## Real-provider diagnostic

**Lucid Loop → Validate Ren real-provider encounter (paid)** opens the actual encounter, approaches Ren through the authoritative local relay on port 8790, sends one typed question with microphone capture disabled, and observes accepted PCM, consumed playback and final mesh speech weights. It closes the session and requires final usage confirmation and a neutral mouth. Recordings and bounded diagnostics remain private under `.local/validation/ren-live-encounter-*`.

The first run on September 14 received real speech and confirmed cleanup, but failed its five-moving-frame requirement (four observed). Its coroutine compared current playback progress with the preceding frame's mesh pose. The corrected observer runs in `LateUpdate` at order 200, after Ren's compositor at order 100, and records paired consumption, starvation and mesh weights without lowering the requirement.

The corrected rerun also failed (four advancing speech frames, 43 nonzero held poses). Its diagnostics show the Unity clip reader advancing 9,600 input samples approximately every 400 ms. This is too coarse for live articulation. A replacement using the actual DSP output callback is being implemented; it has not passed the integrated provider test yet. Unity documents [OnAudioFilterRead](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) as an audio-thread DSP filter receiving frequent output blocks; the implementation must resample into those blocks and count only source samples represented in output. Native iOS voice audio uses a separate renderer and still needs physical-device timing verification.

The captured first 15 seconds contain about 2.5 seconds of speech. Reanalysis with the pinned model produced 129 non-silence targets between 976 and 3392 milliseconds. Continuous silent output must not be described as continuous speech. Neither this analysis nor the failed test establishes smooth live lip sync, acoustic playback or physical-device quality.
