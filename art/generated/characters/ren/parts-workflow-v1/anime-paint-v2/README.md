# Ren anime paint v2 — material review candidate

`Ren_AnimePaint_v2.blend` preserves the frozen EyeUV working head and its complete existing mouth/blink controls. It changes only the head skin/lip material slots. `material-transfer.json` is the integration contract: append `Ren_AnimePaint_v2_BakedSkin` to `Ren_Head` slots 0 and 2; preserve cavity slot 1 and the independently authored eyes/gaze assembly. Do not import this study's eyes over the newer gaze correction.

The 2048px `Ren_AnimePaint_v2_BaseColor.png` uses sRGB and the exact existing `FaceUV_v1` atlas. Export that UV layer as Unity UV0 in an export copy. Skin is opaque; atlas background transparency is not cutout skin. The 2048px linear `ProjectionCoverage.png` is provenance, not runtime opacity. Both are baked by Blender with 12px padding. The runtime material needs one color sample, matte diffuse shading and restrained dynamic lighting. Blender review uses a 65% color-emission / 35% diffuse energy blend, rather than additive emission; no normal, metal or glossy specular maps are introduced.

The paint donor is the existing reviewed `face-paint-v1/painted-views/front-paint-v1.png`, unchanged SHA256 `391c592f00b25e2627bce72e403d09614a5af3fd40636a5e31f21f9d5c62d4c8`. No new image generation or Tripo request was used. A fitted surface projector aligns recorded eye, mouth and nose landmarks, supplemented with actual neutral lid aperture anchors. Smooth frontal-facing/side weighting prevents the front image wrapping onto the rear and chin underside. Hidden/sidelit areas use an explicitly authored neutral skin base; they are not claimed as recovered reference detail. A local shader exclusion replaces pale donor sclera leakage on head skin with nearby donor cheek pigment, while preserving dark liner and all separate actual eye objects. Pigment is then baked into UVs, so it follows blink and jaw deformation without a stationary camera projector.

`neutral-front.png`, `neutral-quarter.png`, `neutral-profile.png`, `mouth-open-front.png`, `blink-front.png` and `blink-quarter.png` are actual baked-material renders in Blender 5.1.1. Front/quarter were reviewed before baking. The final closed-lid and profile corrections were also inspected. This is a usable assembly review candidate, not final likeness approval. The donor includes painted form shading and has only a front view; it is not a measured albedo map or a finished full-character texture.

`paint-report.json`, `source-signatures.json`, `source-uv-signatures.json` and `projector-fit.json` preserve checks and provenance. All geometry, triangle connectivity, UV corners, shape-key coordinates/settings, existing mesh attributes, normals and object matrices match source `6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959`. Head geometry hash is `a123978b09fcfa08b9811db9a0361f9fe8341f9ce13cb68dff2d9c9329380bb6`; all-UV hash is `9e575bed9dcbe1888b5c7341e7f5935e183a77fc176e93e419b3f62698ec70db`. No polygon budget changes were made.

Reproduction (Blender 5.1.1, repository root):

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --threads 2 --factory-startup --offline-mode --python-exit-code 1 --python tools/character_art/paint_ren_anime_face.py -- --preview --finish
```

Do not rerun over a frozen integration artifact; choose a new output study path for further art changes. `Ren_AnimePaint_Procedural_Study.blend` retains the authoring projector; the actual deliverable is `Ren_AnimePaint_v2.blend` with the baked UV material.
