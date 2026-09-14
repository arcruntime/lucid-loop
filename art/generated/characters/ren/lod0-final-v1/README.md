# Complete Ren LOD0

`Ren_LOD0.blend` and `Ren_LOD0.fbx` assemble the current expressive head, corrected cap-on hair, detachable source cap, replacement headphones, and final headless female-v3 body. Original inputs remain unchanged. `Ren_LOD0-front.png`, `Ren_LOD0-quarter.png`, and `Ren_LOD0-neck.png` show the complete assembly in Blender.

The FBX contains **42,168 rendered triangles**, 15 mesh objects, one armature, and **67 bones**: all 54 female-v3 bones retain their exact rest transforms, with 13 hair bones added. Blender counts 42,170 triangles; the FBX exporter drops two degenerate body triangles. The FBX contains no animation actions. Use the existing verified female-v3 idle animation in Unity.

All nine facial meshes retain Basis plus 35 expression/speech controls with finite, matching vertex counts. The lower neck joins the actual 233-vertex body ring through a 293-triangle strip with matching edge positions and weights. The new head attachment boundary has zero expression deltas. Head, hair and cap share one uniform similarity transform; facial proportions are unchanged. A sampled endpoint-color texture blends the short neck strip.

`RenCap_Static` is a rigid Head attachment. Toggle it together with `RenLiveHair.capOn`: 100 when worn, 0 when removed. `RenHeadphones_Static` is a rigid Spine attachment. Covered crown motion needs to remain constrained while wearing the cap. No skinning is required for either rigid accessory.

Unity orientation is +Z forward, +Y up, metres. `unity-materials.json` contains the requested material wrapper and texture paths under `textures/`. The head skin entry includes closed-eye map metadata; the existing NPR shader must drive its left/right blink correction. `assembly-manifest.json` records all input hashes, exact transforms, parts, controls and bones. `fbx-validation.json` records the independent FBX reimport checks.

These Blender previews are not Unity NPR approval or iPhone performance measurements. Existing source texture artifacts and near-rim hair facets remain visible at close range. The assembled model is ready for the final Unity integration and review, without claiming new artist approval of the existing head candidate.
