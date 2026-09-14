# Ren LOD0 integration

The complete viewer is `Unity/Assets/CharacterArt/Generated/RenLOD0/Scenes/RenLOD0.unity`.
Open it in Unity 6.3 LTS and press Play. Delivery commit `2e985eb` is on `origin/main`.

The model imports at 42,170 triangles in Unity. It uses the 54-bone female-v3
body skeleton plus a hair anchor and 12 moving hair bones. Do not retarget it
to the older female-v2 rest pose. The idle clip is the derived
`RenObserverIdleFemaleV3.anim`, whose translation units and root curves were
corrected for this skeleton; the original animation FBX is not interchangeable.

For a reusable prefab, open the complete viewer outside Play mode and select
**Lucid Loop → Ren LOD0 → Save gameplay prefab**. The exporter saves
`Assets/CharacterArt/Generated/RenLOD0/Prefabs/RenLOD0.prefab`, reloads it, and
checks that all skin bones are internal and the idle/head/cap/hair references
survive. It removes viewer camera/light references and hides the controls.
`Evidence/prefab.txt` is written only after this validation succeeds.

The current viewer model faces world -Z at its identity container rotation.
Rotate the whole container when placing it. The runtime captures the model's
initial forward/right vectors and follows the Head rotation for NPR lighting.

The cap is `RenCap_Static`, parented to Head. Its active state must agree with
the hair's `capOn` shape and `RenAssemblyHairMotion.CapOn`. Headphones are a
rigid Spine attachment. Hair is skinned and must retain its internal bones.

`RenLOD0Controller` owns body idle sampling and face weights. Disable its body
idle before a gameplay animation system takes ownership of those bones.
Do not simultaneously attach another component that overwrites the same facial
weights. `RenLiveSpeechFaceAdapter.FaceController` forwards English speech
snapshots through `RenLOD0Controller.SetSpeechWeights`; the controller remains
the final mesh writer and applies blink, expression and lip-closure rules.
Resetting speech sends an empty snapshot, clearing stale articulation.

To install the complete prefab into the existing conversation gym, use
**Lucid Loop → Ren LOD0 → Install into Live gym** outside Play mode. It replaces
the old Ren face visual, retains the other cast entries and relay settings,
and focuses the conversation camera on the animated head. The base prefab itself
has no network connection; the gym owns relay/audio playback.

Editor captures verify A, MBP, blink and cap-off controls. They do not establish
designer likeness approval, full English speech quality, walking/contact quality,
LOD1 transitions, or sustained 30 fps on iPhone 15 Plus. Those gates remain open.
