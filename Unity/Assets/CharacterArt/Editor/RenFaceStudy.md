# Ren local face study, V2

Open `Assets/CharacterArt/Generated/Preview/Scenes/RenFaceStudy.unity` and enter Play mode. The left face stays at closed-rest; the right face has continuous mouth-seal/open-A, independent left/right blink, and two-axis gaze controls. The original artist sheet stays visible in the center. This is an unpainted construction review, with a known intermediate-gaze defect described below.

The mouth weights share the source-open basis and sum to at most 100%. Closed-rest, source opening, and open A have explicit presets. Blink and gaze compose independently with the mouth. Idle blink uses a seeded 2–6 second interval, smooth 65 ms closure, 40 ms hold, and smooth 140 ms reopening. Moving a manual blink slider disables idle blinking; its toggle re-enables it. Reset returns all facial channels to closed-rest and disables idle blink.

The fitted-hair toggle affects both panels without changing the head size or position. Each panel's `< >` selector switches between clay and temporary unpainted colors. Clay covers the head and lip slots by default; eye, cavity, tongue, and teeth colors remain readable. The broad temporary rose lip mask is not finished lip painting. Synchronized orbit/zoom, reference navigation, front/quarter/profile resets, neutral/club lighting, and the unlit diagnostic reuse the unchanged bust-comparison runtime.

## Verification and limitations

Actual Unity renders verify independent eyelid closure from the front and both quarter views, continuous mouth movement, and idle blinking while other facial channels remain applied. Full blinks hide the irises and sclera. A live Windows player, using live SkinnedMeshRenderers, also verified the controls after rendered frames; its reset screenshot is byte-identical to the initial closed-rest screenshot.

**Intermediate gaze is not accepted.** At approximately half travel, white scalloped patches from the sclera appear through the irises, including with the eyelids open. This occurs in both weighted geometry captures and the actual live player. The captured endpoint poses look intact, but those endpoints do not establish acceptable continuous gaze. The existing source remains unchanged to preserve the audited checkpoint. See `RenFaceStudy--clay--combined-half--neutral--front.png` and `live/RenFaceStudy-live--idle-reopened-combined.png` in the evidence directory.

The fitted hair is a 45,536-triangle geometry trial with visible raw clumps and unresolved UV/material work. No final likeness, painted face, full English/Japanese speech coverage, expression set, or iPhone performance acceptance is claimed. The layout was reviewed at 1600×900; sustained 30 fps on iPhone 15 Plus remains a separate device test.

## Import and materials

The face comes from `art/generated/characters/ren/parts-workflow-v1/face-integration-v2/Ren_P2_Face_Study.fbx`, SHA256 `69b51acd3edffa00da11779be36467a78572a114db85a36cad593810dd523fda`. The optional hair comes from `parts-workflow-v1/hair-cleanup-v1/fitted-v5/ren-fitted-hair-trial.fbx`, SHA256 `79d9aae56f4b51453179532b06c873047e5ea670fd19cddb8ad717eb414c29a0`. Copies under `Generated/RenFaceStudy/Sources/` retain the original bytes. The manifest binds both hashes.

Both native FBX imports have an identity container and axis/scale conversion on their children. Their native coordinates map Blender `(X,Y,Z)` to Unity `(-X,Z,-Y)`. The study adds only `(0,90,0)` container rotation to both sources so the face points toward Unity `+Z`; added position is zero and scale is one. Hair V5 already shares the head frame. Do not apply the older parts-study hair offset or an additional FBX child rotation.

One union of the face's closed/source/open-A poses and full fitted-hair bounds sets the shared normalization: scale `1.562424898147583`. Hair visibility never recalculates framing. The face assembly contains 24,420 triangles before adding hair. The imported head has real `mouthSeal`, `jawOpen_A`, `eyeBlinkL`, and `eyeBlinkR` deltas; liner/lashes carry corresponding blink channels, and irises carry four gaze directions. Source mesh and shape counts are recorded in the import/evidence JSON files.

FBX vertex colors are exported as `LINEAR`. `RenFaceStudyDiffuse.shader` directly uses those colors for `Ren_IrisGreyBlue`, replacing its fallback base color as the Blender source does. Other colors come from the export metadata, converted to the manifest's sRGB colors and then to explicit linear shader vectors. The shader does not project textures or paint new eyes.

The shader uses diffuse URP Forward main/additional lights and ambient spherical harmonics, with no specular response, shadow casting, HDR, or postprocessing. Neutral lighting uses a front-facing key at Euler `(25,155,0)`, intensity `0.9`, white fill `1.5`, and cool rim `0.6`. Club lighting uses a cool front key `(20,165,0)`, intensity `0.55`, magenta `1.0`, and cyan `1.4`. These are shared review conditions, not a final character shader.

## Rebuild and evidence

Use the isolated project `C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification`. Do not launch a second Editor on the open main project or replace an executable while it is running. Copy only the owned face-study code/assets into scratch before rebuilding; copy the generated face-study scene/assets and their metas back after review.

`LucidLoop.CharacterArt.Editor.RenFaceStudyBuilder.AuditImport` records native transforms, mesh/color counts, shape deltas, and native head vertices. It accepts `-renFaceAuditAsset ASSET_PATH` and `-renFaceAuditOutput ABSOLUTE_DIRECTORY`. `BuildAndCapture` verifies hashes and genuine mouth/eye deformations, builds the separate scene, and renders its evidence:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' `
  -batchmode -quit -force-d3d11 --burst-disable-compilation `
  -projectPath 'C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification' `
  -executeMethod LucidLoop.CharacterArt.Editor.RenFaceStudyBuilder.BuildAndCapture `
  -renFaceOutput 'B:/lucid-loop/art/generated/characters/ren/parts-workflow-v1/face-integration-v2/unity-review' `
  -logFile 'B:/lucid-loop/.local/ren-bust-comparison/unity-face-v2-build-capture.log'
```

The evidence directory contains 32 geometry images, 14 actual live UI images, source/import/manifest evidence, and `RenFaceStudyReview.json` with the intermediate-gaze rejection. Geometry captures cover front/both quarters, selected club views, independent blinks, gaze endpoints, combined intermediate/full poses, optional hair, and temporary colors. Synchronous Editor capture temporarily bakes the actual weighted SkinnedMeshRenderers to avoid reusing stale GPU poses within one Editor frame. Nine distinct facial geometry hashes are required; temporary meshes are destroyed and the saved scene retains live SkinnedMeshRenderers.

The live player uses no baked capture meshes. It captures 12 static poses after 12 rendered frames each, then an automatically timed idle blink's full closure and reopening with mouth/gaze applied. `live/RenFaceStudyLiveCapture.json` records effective weights, seed, hair state, and successful idle peak/reopening. All 32 geometry images and all 14 live images were visually inspected.

Build a separate V2 Windows player from the saved scene without repeating heavy imports/captures:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' `
  -batchmode -quit -force-d3d11 --burst-disable-compilation `
  -projectPath 'C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification' `
  -executeMethod LucidLoop.CharacterArt.Editor.RenFaceStudyBuilder.BuildPlayerFromSavedScene `
  -renFacePlayerOutput 'B:/lucid-loop/.local/ren-face-study-v2/RenFaceStudy.exe' `
  -logFile 'B:/lucid-loop/.local/ren-bust-comparison/unity-face-v2-player-build.log'
```

For interactive review when requested, launch without the capture flag and leave the player running:

```powershell
Start-Process -FilePath 'B:/lucid-loop/.local/ren-face-study-v2/RenFaceStudy.exe' `
  -ArgumentList @('-force-d3d11', '-screen-fullscreen', '0') -WindowStyle Normal
```

Adding `-renFaceLiveCapture ABSOLUTE_DIRECTORY` enables the deterministic live verification sequence and exits afterward. This mode explicitly selects the live clay candidate on both panels before testing, even when a static trial is available. The V1 face player and previous bust/parts scenes remain separate.

## Optional Tripo static texture trial

The optional `textureTrial` block in `RenFaceStudyManifest.json` adds a third candidate labeled **Tripo texture trial · static**. It displays the entire returned FBX with its actual per-material UVs and maps. No generated eye is substituted with an authored eye. No texture, UV, or topology transfer onto the working facial meshes is implied. With a valid trial present, the initial pair is left clay/right static trial. Selecting a static right candidate removes facial/hair controls and displays an explicit explanation; returning to a live candidate restores them.

All trial source files must be copied under `Generated/RenFaceStudy/TextureTrial/`. The imported face frame must be audited before setting its placement. The trial reuses the V2 union normalization exactly; it does not independently resize or reframe itself. Manifest structure:

```json
{
  "textureTrial": {
    "sourceFbxAsset": "Assets/CharacterArt/Generated/RenFaceStudy/TextureTrial/Sources/RenFaceStudyTripoTexture.fbx",
    "sourceSha256": "actual reviewed FBX SHA256",
    "reviewNote": "Actual returned static texture trial; painting and likeness unaccepted.",
    "vertexColorEncoding": "sRGB",
    "allowMissingUv0OnRenderers": ["exact names audited as missing source UV0"],
    "position": {"x": 0, "y": 0, "z": 0},
    "eulerAngles": {"x": 0, "y": 90, "z": 0},
    "scale": {"x": 1, "y": 1, "z": 1},
    "materials": [{
      "sourceName": "exact imported material name",
      "baseColorAsset": "Assets/CharacterArt/Generated/RenFaceStudy/TextureTrial/Textures/exact-source-basecolor.jpg",
      "baseColorSha256": "actual original-byte texture SHA256",
      "baseColorSrgb": {"r": 1, "g": 1, "b": 1, "a": 1},
      "normalAsset": "optional exact-source normal map asset path",
      "normalSha256": "optional actual normal map SHA256",
      "normalScale": 1,
      "useVertexColor": false
    }]
  }
}
```

The array must map every imported source material exactly once. Base colors are imported as sRGB; source normals use Unity's explicit normal-map importer with linear sampling. Original image bytes are preserved. Desktop textures are uncompressed with a maximum import size of 8192, mipmaps enabled, and streaming disabled. This can use substantial desktop GPU memory and makes no claim of device readiness. Metallic/roughness maps stay in raw provenance and are not used by this diffuse review. Source normals default off and are available through the existing optional normal-map diagnostic.

The actual returned trial has 14 objects and 24,420 triangles. Only the head and two scleras retain UV0; the other 11 objects lack that attribute in the provider GLB itself. Their explicit omission list allows a missing UV0 input to stay at its default coordinate, without creating new UVs. Both irises retain provider vertex colors, which multiply the sampled source base color. Unlike the V2 facial FBX, the static trial FBX exported these colors as sRGB; `_VertexColorSrgb` explicitly decodes them in the shader. The irises consequently look flat grey, consistent with the actual provider output. The static output is more realistic in its skin, lips, and makeup than the artist design; no aesthetic acceptance is implied.

In scratch, `RenFaceStudyBuilder.BuildTextureTrialAndCapture` builds the trial scene and captures matching front, both quarters, and profile in basecolor, neutral, and club modes for the clay source and static trial. Use `-renFaceOutput` pointing to the trial's separate evidence directory. It writes separate `RenFaceStudyTextureEvidence.json` and `RenFaceStudyTextureManifest.json`, preserving the V2 facial capture evidence. Build the saved scene with `BuildPlayerFromSavedScene` and an explicit `-renFacePlayerOutput B:/lucid-loop/.local/ren-tripo-texture-viewer/RenFaceStudy.exe`; do not overwrite the V1 or V2 player.

For the shortest complete build, `RenFaceStudyBuilder.BuildTextureWindowsViewer` builds the scene and player in one invocation, without the full capture permutations. It accepts `-renFaceQuickCapture ABSOLUTE_PNG` for one neutral front geometry image, plus `-renFaceAuditAsset` and `-renFaceAuditOutput` for the actual native import audit. The initial handoff used this route; the full capture-permutation entry point is available but has not yet been run for this texture trial.

The final static build exited successfully. Its actual neutral front render was visually inspected, all 28 imported basecolor/normal files match their original hashes, and the 8192×8192 head maps are retained. An independent comparison of all 7,505 imported head points against the returned GLB confirms native Unity `(-X,Y,Z)` mapping within `1.21e-7` units; the shared viewer then applies Y90. The 94 scene/prefab/material dependency files resolve without missing asset GUIDs or metas. See `tripo-texture-v1/unity-review/RenFaceStudyTextureReview.json` and `RenFaceStudyTextureAxisAudit.json`.

For one actual UI screenshot with the original artist shown, launch that player with `-renBustCapture ABSOLUTE_PNG -renBustLeft ren-local-face-clay -renBustRight ren-tripo-texture-static -renBustLighting neutral -renBustView front -renBustShowReference -renBustReferenceIndex 0`. This screenshot mode exits afterward. A normal interactive launch uses no capture flag and should remain open for the user.

No visible static Unity player has been launched for this handoff, because the user switched to the browser viewer. A hidden `-batchmode` player did not advance the screenshot's end-of-frame coroutine and was stopped; no static-trial UI screenshot is claimed. The earlier V2 live-control screenshots remain valid evidence for that separate checkpoint.
