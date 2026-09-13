# Ren complete-head review

This separate review combines the assembled head, continuous ellipsoid gaze, painted materials, and fitted hair/cap. The current V2 assembly revises the cap and fringe; its face and gaze source inputs remain identical to V1. It does not overwrite `RenFaceStudy.unity`, the V2 facial source, or the static Tripo source. A cap-fitting runtime bug in the first Unity build was corrected: its binding loop had cleared every morph, including `capOn`, without restoring the hair fit. **Hair crossing the cap in the initial Unity captures was caused by this runtime bug; the earlier attribution to the source was incorrect.** Those captures are marked superseded for cap-state review. Source geometry and texture files were not changed by the fix.

The scene is `Assets/CharacterArt/Generated/Preview/Scenes/RenCompleteHead.unity`, with assets under `Assets/CharacterArt/Generated/RenCompleteHead/`. It presents new clay and painted candidates, with the original artist reference visible, plus copies of earlier review prefabs as optional comparisons. One common normalization is applied to all candidates; comparison copies do not modify their source prefabs. The current player is `.local/ren-complete-head-v2/RenCompleteHead.exe`. The corrected V1 player remains `.local/ren-complete-head-v1-cap-fix/RenCompleteHead.exe`; no visible interactive launch of either is claimed. The earlier `.local/ren-complete-head-v1/` player has the superseded cap-state bug.

## Gaze and facial controls

`RenCompleteHeadRig` drives `mouthSeal`, `jawOpen_A`, independent `eyeBlinkL/R`, and the six hair meshes' `capOn` blendshapes. It preserves unrelated morphs. `SetCapVisible` synchronizes `Ren_Cap` visibility with fitting weights of 100 when on and zero when off. It reapplies this state after binding and facial updates; facial reset preserves a requested cap-off state. Hair visibility is independent of cap visibility, and the cap toggle defaults on. Old additive gaze keys are rejected by the new source importer and also forced to zero defensively by the runtime. The eyelid deformation stays on the head, liner, and lash meshes. Continuous gaze rotates each `Ren_GazeRotate_L/R` inside its nonuniformly scaled `Ren_GazeEllipsoid_L/R`; it does not rotate an unnormalized iris around an estimated world pivot.

The bound source contract gives the native rotation `Rz(gazeX * 0.16199795457112545) * Ry(gazeY * -0.11693296146237843)`, with both inputs clamped to `[-1,1]`. The importer derives the actual FBX-local axes from the exported `Ren_GazeAxis_L_X/Y/Z` and `Ren_GazeAxis_R_X/Y/Z` markers. It validates orthogonality and uniform marker scale, computes the basis handedness, and maps the two rotation axes as pseudovectors. The runtime applies the resulting yaw-then-pitch quaternion after the imported neutral local rotation. A numerical check of all 48 signed axis permutations across 25 gaze samples each matched full matrix conjugation exactly.

Actual V1 import yields approximately `(-X,+Y,+Z)` local axes, handedness `-1`, and identity neutral rotation. An independent analytic comparison of all 282 imported iris vertices per eye at four partial diagonals and two extreme corners matches Unity's transformed positions within `8.65e-8` source units. This verifies actual imported basis and rotation order, not only a synthetic formula.

The new controller keeps mouth, manual blink, gaze, and hair visibility independent. Idle blink reuses the verified V2 timing: smooth 65 ms closure, 40 ms hold, smooth 140 ms reopening, and seeded intervals of 2–6 seconds. Earlier comparison candidates are held at rest and do not expose the new controls.

## Actual import and verification route

The builder requires a real `RenCompleteHeadManifest.json`; it does not create a placeholder manifest or scene. Required fields are `schemaVersion:1`, `sourceFbxAsset/sourceSha256`, `gazeContractAsset/gazeContractSha256`, and an explicit `reviewNote`. `displayEulerAngles` is applied only after the native imported FBX root is checked as identity. `materials` maps exact source names to explicit sRGB base colors, optional original texture asset/hash, and vertex-color encoding/usage. `hairObjectNames` and `capObjectNames` list the separate visibility groups. The normalization includes both hair fitting states so toggling the cap does not change camera framing. All new source paths must stay under the complete-head asset root.

Use only the existing isolated scratch project: `C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification`. Do not launch a second Editor on the open main project. Build entry points:

- `LucidLoop.CharacterArt.Editor.RenCompleteHeadBuilder.Build`: import/audit and save the new scene.
- `RenCompleteHeadBuilder.BuildWindowsViewer`: build the new scene and separate player.
- `RenCompleteHeadBuilder.BuildPlayerFromSavedScene`: build only the saved scene.

Use `-renCompletePlayerOutput B:/lucid-loop/.local/ren-complete-head-v2/RenCompleteHead.exe` to keep all earlier players separate. `-renCompleteAuditOutput ABSOLUTE_DIRECTORY` records per-mesh import counts, calibrated local axes, and imported iris positions in the native FBX frame for neutral, partial diagonals, and extreme poses. These position samples must be compared to the source's analytic positions; they are transform evidence, not live screenshots.

The running player's `-renCompleteLiveCapture ABSOLUTE_DIRECTORY` route holds each actual pose for 12 player frame updates before submitting an explicit URP GPU camera render request. It uses live renderers and never `BakeMesh` capture snapshots. Eighteen actual images cover the complete head from front, both quarters, and profile, plus partial diagonal gaze with partial blink, independent full blinks, open A with gaze, and reset. Evidence records frame numbers and actual controls. The reset PNG is byte-identical to the initial closed-rest PNG. This route works in a hidden `-batchmode -force-d3d11` player and exits afterward, without opening a visible application window.

## V1 source and findings

The native source is `parts-workflow-v1/complete-head-v1/Ren_CompleteHead_Review.fbx`, SHA256 `e1c56169a9c86fef72f947d6024b4bc6a9b7caa1296b9a3d5c7787fb4b0cfa2f`. The copied source, gaze contract, original assembly metadata, and three original texture PNGs retain their original hashes. The exported head maps `FaceUV_v1` to UV0. For slots with a directly connected image, the image replaces Blender's unused fallback socket color, so Unity uses a white tint; explicit fallback colors are used for untextured slots. The shared diffuse review shader does not recreate Blender's 65% unlit / 35% diffuse paint mix and does not establish the final game shader.

Shared normalization scale is `1.5326744318008423`. Blender reports 26,278 source triangles; Unity imports 26,274. The only difference is `Ren_Hair_Back`, 1,820 source versus 1,816 imported triangles; the import cause has not been established. Every other mesh count matches, including the 1,702-triangle eye assembly. No source mesh was edited to alter this count.

Live construction findings:

- Partial diagonal gaze with 50% blink shows no visible iris/sclera intersection at front or either quarter view.
- Independent L/R blinks, full closure, and open A visibly move the real renderer. Full closure hides iris and sclera.
- The corrected cap-on render tucks the crown hair beneath the cap, while cap-off restores loose hair. The earlier crossing was introduced by the runtime. Cap proportions and fringe coverage still require artist review.
- Eyelid painting stretches at full closure. Painting and final likeness are unaccepted.

Corrected evidence is under `art/generated/characters/ren/parts-workflow-v1/complete-head-v1/unity-review/`: `RenCompleteHeadCapFixReview.json` and `live-cap-fix/`. All 25 new captures record the actual cap object's visibility and all six hair fitting weights. Every recorded value matches the requested state. Cap-off reset and rebinding images are byte-identical to cap-off rest; restoring the cap is byte-identical to the initial cap-on image. The original `RenCompleteHeadReview.json` and `live/` evidence are retained with a superseded-cap-state annotation and the exact cause.

The original reference and controls are configured in the saved scene, but no complete-head UI screenshot or visible interactive launch is claimed. Full speech/emotions and sustained 30 fps on iPhone 15 Plus remain separate milestones. A later source revision must retain the V1 evidence and player for comparison. `-renCompleteFocusedCapture` limits subsequent source checks to key views, combined motion, and cap/reset/rebinding states; any reuse of earlier facial checks must be justified by unchanged relevant source/contract hashes.

## V2 source, focused captures, and motion

The current source is `complete-head-v2/Ren_CompleteHead_Review.fbx`, SHA256 `74283a65dfb977e2ec0976cd36a019ab54489f5867fbb8702f72c478a6ee9bda`. Dedicated copies live in `Sources/RenCompleteHead-v2.fbx`, `Sources/assembly-v2.json`, and `Textures/V2/`; the V1 files remain. Gaze and paint input hashes, all 14 facial object records, and the embedded gaze contract exactly match V1. `RenCompleteHeadV1Reuse.json` records the basis for reusing the full V1 facial checks. The new import still receives analytic calibration verification; maximum error is `7.95e-8` source units.

Unity imports 26,422 triangles from the 26,426-triangle V2 assembly. The same four-triangle `Ren_Hair_Back` difference remains; its cause has not been established. Shared normalization scale is `1.6125003099441529`. Original texture bytes are preserved: 2048² face, 1024² cap, and 4096² hair. Desktop import uses uncompressed RGBA32, an 8192 ceiling, sRGB, and mipmaps. All 56 painted/clay/lit/unlit material variants use the existing diffuse review shader. Painted image slots sample UV0 with white tint; iris colors are interpreted as linear; source normal maps remain disabled.

Thirteen actual hidden-player captures are under `complete-head-v2/unity-review/live/`. They include neutral front/quarter/profile, nightclub front, combined half blink and partial diagonal gaze, open A with gaze, full front closure, cap-off front/quarter, cap-off reset/rebinding, and cap restoration. All six hair fitting weights and actual cap visibility match the requested state in all 13 captures. Reset/rebinding preserves cap-off byte-identically, and restoring the cap reproduces cap-on rest byte-identically. Ten distinct images were visually inspected; the remaining three were verified as those exact image duplicates. Full closure hides iris/sclera. The nightclub front retains face detail. Cap proportions, hair strand edges, stretched eyelid painting, and final likeness remain review questions.

The five-second clip is `complete-head-v2/unity-review/motion/RenCompleteHead-v2-motion.mp4`: 150 actual Unity GPU-rendered frames, 1280×720, H.264, 30 output fps. It shows gentle gaze, a blink, mouth seal to open A, and mouth rest again. It contains no audio and demonstrates one mouth gesture, not speech lip-sync. This is visual evidence, **not** a phone-performance measurement. All 150 source frame hashes and runtime/source versions are recorded in `RenCompleteHeadMotionEvidence.json`. `RenCompleteHeadMotionEncoding.json` records ffmpeg version, exact arguments, output hash, ffprobe metadata, and the full decode check. Four original keyframes and two decoded MP4 keyframes were visually inspected. All 150 intermediate PNGs remain under ignored `.local/ren-complete-head-v2-motion/`; only the MP4, metadata, and four key screenshots belong to the review artifact.

To reproduce captures using the separate built player, run these PowerShell commands from the repository root. They launch hidden capture processes that exit when finished. Omit capture arguments when an interactive viewer launch is requested.

```powershell
$viewer = 'B:/lucid-loop/.local/ren-complete-head-v2/RenCompleteHead.exe'
Start-Process -FilePath $viewer -WindowStyle Hidden -Wait -ArgumentList @(
  '-batchmode', '-force-d3d11', '-renCompleteLiveCapture',
  'B:/lucid-loop/art/generated/characters/ren/parts-workflow-v1/complete-head-v2/unity-review/live',
  '-renCompleteFocusedCapture')
Start-Process -FilePath $viewer -WindowStyle Hidden -Wait -ArgumentList @(
  '-batchmode', '-force-d3d11', '-renCompleteMotionCapture',
  'B:/lucid-loop/.local/ren-complete-head-v2-motion')
```
