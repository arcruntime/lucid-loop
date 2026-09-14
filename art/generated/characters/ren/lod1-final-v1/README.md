# Ren LOD1 — 11,675 rendered triangles

Ren_LOD1.blend and Ren_LOD1.fbx are ready for integration. The Blender mesh totals 11,689 triangles; an independent FBX reimport measures **11,675**, within the requested 10–12k budget. The export/import path drops 14 triangles (body 2, ear jewelry 3, hair 9); the counts are recorded separately.

This finishes the corrected 13,418-triangle model by reducing the cap to 240, headphones to 280, body to 1,399 and head to 2,199 triangles. Head reduction protects the visible front and neck while simplifying the rear. Corrected hair remains **5,651 triangles**, preserving its protected boundary/tip positions exactly. All protected neck endpoints remain exact. No uniform hair reduction was made.

The source final LOD0 remains unchanged. The finishing script preserves a private copy of the corrected intermediate under .local/ren-lod1-final-preserved; manifest.json records that input hash and the authoritative LOD0 hash. Rig rest transforms and hierarchy remain unchanged by reduction. All material names remain identical to LOD0, allowing the same mapping. The 67-bone hierarchy, named speech/blink/emotion shape counts and weighted rig-bone sets pass independent FBX reimport validation.

Eight per-eye gaze shapes were subsequently transferred from gaze-final-v1/Ren_LOD0_Gaze.blend by nearest-triangle barycentric interpolation: gazeLeftL, gazeRightL, gazeUpL, gazeDownL and the corresponding R names. Existing eye shape coordinates and neutral geometry remain exactly unchanged; gaze-transfer.json records hashes. Fresh FBX validation after this addition retains all eight shapes with the same triangle total and materials.

Matched face and 240px gameplay renders compare final LOD0 and this LOD1. The full LOD1 image shows its complete body. Hair and facial structure remain recognizable; cap/headphone faceting and lower body detail are intentionally coarser and visible at close range. Use LOD0 for close conversation shots. Render inspection does not certify final live animation contacts or phone performance.

Evidence: manifest.json, fbx-validation.json, gaze-transfer.json; source-face.png/lod1-face.png and source-gameplay240.png/lod1-gameplay240.png. No Unity Editor changes or paid generations occurred. A separately planned guarded left-finger rig/weight update is not yet included and awaits its authoritative handoff.

## Guarded hand and corrected export units

The authoritative guarded-final-v1/RenGuarded_WeightAndDigitCorrection.blend correction is now included. Fifteen left digit rest transforms match that donor; corrected body weights were interpolated onto the reduced body, normalized to four influences. Non-digit rest transforms, neutral vertex coordinates and all preexisting shape coordinates remain unchanged. See guarded-transfer.json for donor hash and correspondence evidence.

All LOD1 FBX exporters now match LOD0: apply_unit_scale=True, apply_scale_options=FBX_SCALE_UNITS, use_mesh_modifiers=False. This addresses the prior Unity Hips scale100 import rather than compensating in runtime. Fresh Blender FBX reimport still measures 11,675 triangles with all67 bone names/hierarchy, shape names/counts and materials preserved. Unity must reimport the updated FBX to verify its local scale contract.
