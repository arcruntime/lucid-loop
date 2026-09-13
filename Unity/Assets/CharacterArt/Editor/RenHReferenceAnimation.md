# H reference animation study — preparation only

The controller and builder compile against the installed Unity 6.3 LTS assemblies. **No H animation scene, generated H asset, H live playback proof, or accepted H expression export exists yet.** Source-native H mouth and eye construction is undergoing review. P2 is not an identity fallback. The independent real-video transport test described below has passed.

New code is `Runtime/RenHReferenceAnimationController.cs`, its shared `RenHReferenceVideoTransport.cs`, and `Editor/RenHReferenceAnimationBuilder.cs`. The future scene is `Assets/CharacterArt/Generated/Preview/Scenes/RenHReferenceAnimation.unity`, with new assets under `Assets/CharacterArt/Generated/RenHReferenceAnimation/`. Existing P2/bust/NPR scenes and assets are preserved. The builder refuses to create a scene without an actual reviewed FBX, full material mappings, explicit mouth convention, timeline, imported video and original artist reference.

The reference input is repository-local `DanielDuguay87_2093375826557296673.mp4`, SHA256 `d479c1eeb5eab7a15fa0c489766ef1e22983289d33a5ef51ab32398dc73ea2f1`. Import a byte-identical copy as a VideoClip in the new generated folder. No absolute workstation URL is serialized. The manual timeline is `art/generated/characters/ren/video-reference-v1/control-curves-unity.json`: 32 channels and 152 full keyframes, linear interpolation, manually estimated from visible frames rather than solved tracking, phonemes or audio amplitude. The H manifest requires explicit `headShiftX` and `lean` translation bindings with manually interpreted amplitudes.

The timeline separately records file/audio duration **15.092971 s**, video span **15.066693 s**, and final decoded frame PTS **15.033333 s**. Seek targets clamp to the actual last frame timestamp; the last image/pose is held through the remaining interval. Video presentation time drives the model. Paused scrubbing requests a decoder seek and updates the pose from the resulting video time. Reference audio defaults off and has an explicit toggle. A later rendered comparison must record whether audio was played or muxed from the original; neither method demonstrates realtime lip-sync.

The proposed layout is landscape 16:9: actual H-derived model, portrait video, untouched original artist sheet, then play/pause/replay and a time scrubber. Unsupported timeline controls and failed bindings are listed explicitly. No tongue, smile, brow, squint, pucker, gaze or blink control is treated as implemented merely because a timeline channel exists.

## Required manifest

`RenHReferenceAnimationManifest.json` is not created with empty inputs. Once the source owner supplies and root reviews the real export, populate the C# `RenHReferenceManifest` fields:

- Exact imported `sourceFbxAsset`/SHA256, `sourceLabel`, `reviewNote`, and `mouthConvention`. Labels must disclose H-fitted topology or other derivative provenance, without presenting a P2 source as H.
- `timelineAsset`/hash and imported `videoAsset`/hash, plus untouched `originalArtistAsset`.
- NPR shader/include/preset asset paths and hashes. Per-source material entries explicitly map source linear tint, texture/hash, vertex-color encoding and preset name. No provider/source texture is edited by the builder.
- Reviewed Unity `frontYaw`, normalized height and head pivot; exact paths for the head renderer and animated lighting frame.
- `morphBindings`: `{control, rendererPath, shape, multiplier, offset}`. Paths are relative to the `Motion` wrapper and its imported child `Source`; shape names are exact. Weights apply directly, with no inherited P2 seal/open-A normalization.
- `rotationBindings`: transform path and ordered `{control, localAxis, degreesPerUnit}` entries. Each axis is a reviewed unit vector in that node's local basis; deltas multiply after the captured rest rotation. Missing controls remain unsupported.
- `translationBindings`: transform path and `{control, localAxis, distancePerUnit}` entries added to captured rest local position. Bind `headShiftX` and `lean` on the Motion parent; record their heuristic calibration in `translationAmplitudeNote`. These distances are not measured motion capture. Opposite signed eye yaw for `gazeConverge` can use the rotation interface only if the actual eye contract supports it.
- Perspective `cameraDistance`/`cameraFieldOfView` calibrated on the real H candidate. The preparation defaults are distance 4 and vertical FOV 28 degrees; orthographic is an explicit diagnostic toggle. Forward lean therefore changes projected size in performance mode. Compare actual start and 13-second frames before accepting calibration.
- `capAccessory`: a separate source FBX/hash, source contract/hash, the existing shared female rig definition/hash, explicit cap-only material mappings, reviewed imported-root local TRS, and the socket/hair binding described below. A combined head/cap mesh is rejected.

The timeline uses semantic closed rest (`mouthSeal=1`, `jawOpen_A=0`). The source author must explicitly calibrate that against the actual H mesh basis. If the final source uses an open-A basis or coupled corrections, agree and implement its exact formula before binding; a free-text convention alone does not solve that mismatch.

Binding definitions reject unknown controls, duplicate morph targets and nonfinite multipliers, offsets, axes or degree ranges before runtime dictionary lookup. Material teardown restores a renderer slot only if it still owns the clone in that slot; disabling the component releases its material/pipeline state, and re-enabling recreates the owned frame materials.

The NPR material frame attaches to `AnimatedHeadFrame`, not the static display root. Its center/width come from cached head geometry in that frame's local space; each displayed material clone follows the animated frame without MaterialPropertyBlocks. Final integration must capture an actual head turn under fixed lights to verify this relationship, along with paused video seeks and combined real facial controls.

Build only in the existing isolated scratch Unity project, under one editor owner. Do not launch a second Editor on the main project. No Unity build or capture for this H scene has been run during preparation. Standalone export and deterministic reference/model capture should follow the reviewed real import, not substitute placeholder geometry.

## Separate cap and shared female rig

Ren targets the shared **female** rig. The existing definition is `art/generated/characters/shared-rigs/female_base_v2/rig.json`, archetype `female_base`, version 2. This viewer creates no armature and does not modify the shared rig. The source export must provide the reviewed `Head/CapSocket` transforms; the separate rigid cap FBX is instantiated directly under that socket. Its root TRS must be calibrated from actual imported axis markers and the head assembly. Native Blender coordinates are not directly copied into Unity transforms.

`Runtime/RenHReferenceCapAttachment.cs` owns only cap visibility and explicitly enumerated cap-fitting hair morphs. `capAccessory.binding` contains:

- `rigArchetype`: `female`.
- `mode`: `temporary-bust-socket` until the bust-to-canonical placement is reviewed, or `shared-female-humanoid` once bound to the existing shared female Avatar. The temporary route is a transform-only review socket and is visibly labeled **female rig calibration pending**.
- `headPath`, `socketPath`: exact paths relative to Motion. The socket must be named `CapSocket`, be below the reviewed Head, and be the separate cap root's direct parent.
- `animatorPath`: required to resolve the real Animator in shared-rig mode (empty means Motion itself). `capAccessory.sharedFemaleAvatarAsset` and its source-file hash identify the exact existing Avatar; runtime requires `Animator.GetBoneTransform(HumanBodyBones.Head)` to resolve the declared Head and the Avatar to match exactly. Temporary mode cannot claim that Avatar.
- `reviewNote`, plus `hairFits`: `{rendererPath, shape, offWeight, onWeight}`. The hair owner currently names `Ren_Hair_H_DetailedLayers` and `Ren_Hair_H_RootBase`, each with `capOn`; actual paths await the reviewed export. Unity weights are 0 uncapped and 100 capped. No facial control may target the same renderer/morph slot.

The separate source is required under this study's own `Sources` folder and must differ from the head FBX. `contractAsset`/`contractSha256` preserves the reviewed native socket/pivot evidence; `sharedFemaleRigDefinitionAsset`/`sharedFemaleRigDefinitionSha256` is a byte-identical imported copy of the existing canonical definition. Cap-only material assets use a separate `Cap-` prefix. The builder rejects missing real renderers, a cap skin/Animator, cap/head mesh reuse, a cap containing the face, invalid/nonfinite placement and overlapping hair/facial bindings before initializing visibility.

The **Baseball cap** toggle calls `SetCapVisible`, which sets cap active state and the explicit hair-fit weights together. `SetVisibilityOnly` and `SetHairFitted` remain separately addressable for diagnostics. Replay, scrubbing and facial playback do not reset the cap; cap changes do not apply a facial pose, rotate a transform or reset any other morph. Rebinding preserves the current cap and hair choices. The cap component itself lives outside the toggled cap hierarchy.

This addition is compile-checked preparation. A real cap-on/off comparison while paused on a mixed facial pose, with identical mouth/blink/gaze/head weights before and after, and an actual head turn proving socket following are still required after source review. No cap fit, shared-rig calibration or H runtime result is accepted by this code alone.

## Independent decoder verification

`RenHReferenceVideoSmokeBuilder` and `RenHReferenceVideoSmokeProbe` create a scratch-only technical scene with the actual imported MP4 and the same `RenHReferenceVideoTransport` used by the H controller. No character or placeholder mesh is instantiated. The harness waits for editor imports to settle before entering Play mode, then exits automatically with explicit success/failure. Its batch entry is `LucidLoop.CharacterArt.Editor.RenHReferenceVideoSmokeBuilder.Run`, with a fresh `-renHVideoSmokeOutput` directory. It refuses to run outside the authorized scratch batch Editor.

The successful run is `.local/ren-h-video-smoke-v2/transport-smoke.json`: 26 actual frame-ready events, seven seek-completed events and zero recovery attempts. It verified initial decoded frame 0, paused seek at 3.2 s, eight-update paused-frame stability, rapid 6 → 7.8 → 2.4 s seeks honoring the latest request, replay/advance/pause, end on actual frame 451, tail seek hold, replay after end, and the real decoder audio-track mute toggle. Seven invalid binding definitions were rejected, including unknown controls, duplicate morph targets and nonfinite values. First-frame and replay PNGs match byte-for-byte.

`.local/ren-h-video-smoke-v2/source-frame-comparison.json` compares actual decoded video texture PNGs with ffmpeg frames from the immutable original. Sampled frames 0, 12, 72, 96 and 451 have clock offsets no greater than 0.333 microseconds against the original ffprobe table; no source acting timestamps were rescaled. Frames 0, 72, 96 and 451 were checked visually and numerically, with mean absolute RGB differences of about 2.4–2.7 byte values. WindowsMediaFoundation still emits H.264 timestamp and unknown-color-primary warnings, so exact color identity is not claimed. The audio test checks mute/unmute state, not audible output or a muxed performance.

The first run under `.local/ren-h-video-smoke-v1/` failed explicitly before any decoded frame when a synchronous material import blocked Play mode; that evidence is preserved. The revised harness waited for stable editor updates and then passed with the same transport bytes. This demonstrates decoder/control transport only, not a working H face, final aesthetic, or phone performance.

Curated passed evidence is under `art/generated/characters/ren/video-reference-v1/unity-transport-review/`. Its source hashes describe the **pre-cap** controller and builder, preserved with their exact metas in commit `ae8f437`. Use `git show ae8f437:<source-path>` to inspect those tested source bytes on a fresh checkout. The local backup is `.local/ren-h-reference-pre-cap-source/`; the curated JSON was not rewritten to imply a rerun. `RenHReferenceVideoTransport.cs` and both smoke files are unchanged by the cap addition, and the decoder test has not been repeated.
