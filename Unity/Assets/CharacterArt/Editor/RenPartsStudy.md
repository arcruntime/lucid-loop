# Ren parts construction study

`RenPartsStudyBuilder` prepares a separate `Assets/CharacterArt/Generated/Preview/Scenes/RenPartsStudy.unity` using the existing comparison runtime. It writes its own materials, prefabs, and evidence under `Assets/CharacterArt/Generated/RenPartsStudy/`. It does not rebuild or edit earlier comparison assets, source FBX files, or texture imports.

The checked-in study uses the real, untextured Tripo P2-20260801 native FBX head and hair, with the limited raw-construction placement review recorded in `art/generated/characters/ren/parts-workflow-v1/assembly/placement-v2/review.json`. The original artist image is visible by default. The new scene contains only these two parts; earlier comparison models remain in their separate scene.

The initial pair is **Head only / open source** and **Head + hair / open source**. Both contain the identical placed head and use one normalization transform derived once from the placed head-and-hair union. This common frame includes the raised hair crown; both views preserve the exact same head size and position. Earlier Tripo/Meshy candidates can be appended from committed prefabs; those retain their previous bounds-based normalization and must not be treated as precisely aligned anatomy comparisons.

The new meshes are labeled **Construction / open mouth** and **Untextured clay**. An open-mouth generation is not a neutral face or a functioning speech rig. The evidence records imported vertices, triangles, quads, and blendshape counts per part. Unity's triangulated import does not establish whether the original FBX contains useful native facial edge loops.

The unchanged viewer offers these diagnostics:

- **Neutral lighting:** default diffuse clay shape review, using imported geometry normals.
- **Base color / unlit:** uniform clay silhouette, without directional shape shading.
- **Nightclub lighting:** the same clay under subdued magenta/cyan lighting and a soft front key.

No generated texture or normal maps are bound to the new parts. The existing source-normal toggle has no effect on them. Head and hair use different neutral clay values so their placement and intersections remain visible; these are review materials, not proposed character colors.

The study's directional keys face toward the model's front: neutral Euler `(25,155,0)`, intensity `0.9`; club Euler `(20,165,0)`, intensity `0.55`. Neutral point fill/rim are `1.5/0.6`; club magenta/cyan are `1.0/1.4`. Materials are diffuse-only, with no specular highlights or environment reflections. Source geometry normals are retained. There is no shadow casting, HDR, or postprocessing.

## Actual coordinate and framing verification

`RenPartsStudyImportAudit.Audit` exports native imported FBX world vertices without changing source bytes or importer settings. Comparing those exported vertices with the Blender audit positions confirmed that both parts map as `UnityNative = (-BlenderX, BlenderZ, -BlenderY)` at unit scale, with maximum vertex error below `8e-8`. The Unity native root already rotates X by `-90°`. The reviewed study uses absolute root Euler `(-90,90,0)` to face the camera along Unity `+Z`; the complete display mapping is `(-BlenderY, BlenderZ, BlenderX)`.

The head remains at the origin. The reviewed Blender hair translation `(-0.09,0,0.22)` maps to study Unity `(0,0.22,-0.09)`; there is no additional scale or relative rotation. Source FBX copies are byte-identical to the downloaded originals. `RenPartsStudyCoordinateMapping.json` records source hashes and measured conversion error.

Placed assembly height is `1.21951175`; the shared normalization is scale `1.35300052`, offset `(0,0.67616993,0.12177009)`. Both head-only and assembly use these exact values, resulting in an assembly height of `1.65` in the common camera frame. Unity imports the head as 19,152 triangles and the hair as 51,054 triangles, with zero blendshapes. Native facial topology must be assessed from the source FBX audit, not Unity's triangulated mesh.

Actual captures are under `art/generated/characters/ren/parts-workflow-v1/unity-review/`: two candidates × front/three-quarter/profile × clay-unlit/clay-neutral/clay-club, 18 PNGs. These show raw construction geometry, including filled eyes, raised brows, mouth irregularities, hair crossbars, wisps and intersections. They do not demonstrate accepted likeness, completed eye assemblies, mouth controls, or mobile performance.

## Reviewed input

Create `Assets/CharacterArt/Generated/RenPartsStudy/RenPartsStudyManifest.json` only after real parts and placements are available. This example deliberately leaves paths and review information unset:

```json
{
  "schemaVersion": 1,
  "placementReviewed": false,
  "placementReviewNote": "",
  "originalArtistAsset": "",
  "head": {
    "fbxAsset": "",
    "modelLabel": "",
    "notes": "",
    "position": { "x": 0, "y": 0, "z": 0 },
    "eulerAngles": { "x": 0, "y": 0, "z": 0 },
    "scale": { "x": 1, "y": 1, "z": 1 }
  },
  "hair": {
    "fbxAsset": "",
    "modelLabel": "",
    "notes": "",
    "position": { "x": 0, "y": 0, "z": 0 },
    "eulerAngles": { "x": 0, "y": 0, "z": 0 },
    "scale": { "x": 1, "y": 1, "z": 1 }
  },
  "additionalReferences": [],
  "existingCandidateIds": []
}
```

Paths are explicit Unity `Assets/...` paths. Per-part position, Euler angles, and scale are absolute local transforms on the imported FBX root before shared normalization; copy reviewed values, including any required axis conversion. Scales must be positive and all values finite. Record the actual provider model label and a placement-review note, then set `placementReviewed` to true. Validation rejects absent models or unreviewed placement before creating a scene or writing output assets.

Each additional reference is `{ "label": "...", "textureAsset": "Assets/..." }`. Existing candidate IDs are optional and must resolve to the committed comparison manifest, prefabs, and material sets.

## Run after placement review

Interactive menu: **Lucid Loop → Character Art → Build Ren Parts Study**. Building is additive and restores the previous active scene. Open the new scene explicitly when ready; the original comparison scene is unchanged.

In an isolated scratch Editor, use `LucidLoop.CharacterArt.Editor.RenPartsStudyBuilder.BuildAndCapture` with optional `-renPartsManifest Assets/.../RenPartsStudyManifest.json` and `-renPartsOutput ABSOLUTE_DIRECTORY`. Captures cover the real head-only and assembled versions at front, three-quarter, and profile in all three modes. Inspect every capture; shared framing may require more zoom-out for unusually tall hair.

`BuildPlayerFromSavedScene` packages the reviewed scene without rebuilding assets. Supply `-renPartsPlayerOutput ABSOLUTE_EXE_PATH`; `--burst-disable-compilation` remains an optional process-only choice for this static review build.

The reused runtime retains the existing screenshot arguments. For a visible Windows launch, select `-renBustLeft parts-head-open-source -renBustRight parts-assembled-open-source -renBustLighting neutral -renBustShowReference -renBustReferenceIndex 0 -renBustCapture ABSOLUTE_PNG_PATH`. Windows D3D11 captures need a visible, non-minimized viewer window; inspect the resulting image rather than relying solely on a capture log marker.
