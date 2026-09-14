# Ren speech in the encounter

`EncounterSpeechBinding` routes accepted PCM16 packets and consumed-output sample counts from `EncounterVoiceController` to the speaking actor's active face adapter. An assembled Ren uses `RenLiveSpeechFaceAdapter`; actors with the earlier face rig use `LiveSpeechFaceAdapter` and `CharacterFaceDriver`.

The Ren adapter analyzes English with the pinned splatterfacegames lip-sync model. It forwards semantic speech weights to `RenLOD0Controller`, which composes speech, expression, blink and cap state and owns the final renderer weights. Playback timing comes from the active audio renderer, including the native iOS output path; receiving a network packet does not advance the visible mouth.

A new stream clears the previous speaker. Completion, cancellation, disable and unsupported sample rates clear the current speech pose. Packets and stop/progress callbacks from previous generations cannot change a newer conversation. The supported output is mono PCM16 at 24 kHz; Japanese voice remains paused.

The actor's active hierarchy must contain the Ren adapter with its `FaceController` reference set. Merely installing the prefab in LiveGym does not change the build scene: the BeforeTheDrop scene needs the same actor presentation binding. Prefab installation is coordinated with the character artist. Actual provider conversations, iPhone audio timing and perceived lip-sync quality still require device verification.

Validation: the assembled prefab is installed in `Assets/Gyms/Scenes/BeforeTheDrop.unity`. The shared Unity Editor passed all 91 encounter EditMode tests, including five new stream-routing/lifecycle regressions; see [test results](validation/ren-encounter/editmode.xml). This verifies routing and resets, not live provider or physical-device lip-sync quality. Reapply the placement through **Lucid Loop → Encounter → Install assembled Ren** if rebuilding the encounter scene.

The actual scene also passes the [PlayMode presentation test](validation/ren-encounter/playmode.xml): assembled bounds/anchor, controller-only mesh writes, A with independent blink, MBP contact suppression, stale-generation stop protection and reset across rendered frames. The [conversation capture](validation/ren-encounter/04-conversation-rest.png) uses the real Ren selection and Connection toggle while disconnected. This is deterministic presentation evidence, not provider or iPhone acceptance. Run **Lucid Loop → Validate assembled Ren encounter PlayMode** with the Game view visible.
