# Ren polygon budget and reduction decision

2026-09-13. Planning checkpoint for Unity 6.3 LTS, landscape 16:9, sustained 30 fps on iPhone 15 Plus. The user suggested about 40k polygons for Ren including clothing and accessories. **This plan interprets that as a maximum of 40,000 rendered triangles for one complete LOD0 Ren, not 40,000 Blender quad faces.** It is an allocation proposal, not a measured phone-performance guarantee or a completed low-poly export.

Keep the current facial surface at LOD0. Reduce the oversampled hair and sclera first, then fit and retopologize the body/clothing around the shared female rig. Ren's eyes, lips, face silhouette, and moving mouth matter more than hidden scalp, densely tessellated eye backs, or small clothing folds. The existing face surface already fits a 14k allowance; a uniform whole-character decimation would spend facial quality to solve excess elsewhere.

## Exact LOD0 allocation

The budget counts all triangles in enabled renderers for the selected outfit and expression assembly once, including hidden back-facing triangles still present in those meshes. Disabled alternate expressions and alternate outfits do not count simultaneously; do not leave duplicate source/reference meshes enabled. Additional shadow/depth passes and outline geometry have their own rendering cost. Explicit duplicated outline/back-face geometry consumes this allocation too.

| Part | Maximum triangles | Construction priority |
| --- | ---: | --- |
| Head skin, ears, short neck, stitched eyelids, lips and integrated oral cavity | 14,000 | Preserve facial contours, native lip aperture and authored lid topology. Cavity is included here. |
| Both sclera, irises, pupils/catchlights, liner and lashes | 1,800 | Lower-resolution eye shells; keep the narrow eye aperture and continuous gaze. |
| Upper and lower teeth together | 600 | Graphic dental arches; preserve visible tooth strip and lip clearance. |
| Tongue | 400 | Enough topology for speech silhouette and contact; no dense hidden underside. |
| Hair, including fitted root base and deliberate flyaways | 8,000 | Keep fringe and outer silhouette; reduce longitudinal/radial samples. |
| Cap | 1,400 | Silhouette, brim and fit; seams and stitching in textures. |
| Ear jewelry and necklace/chain | 600 | Silhouette-scale loops; chain links can be painted where appropriate. |
| Headphones | 1,000 | Earcup/headband contours; small grooves baked or painted. |
| Exposed body, arms and hands, excluding head/neck above | 3,500 | Preserve wrist/knuckle/elbow/shoulder deformation and visible silhouette. |
| Clothing, including layered hems/collar/cuffs | 5,500 | Silhouette and joint folds; remove permanently covered body surfaces. |
| Shoes | 1,200 | Toe/heel/sole silhouette and foot bending. |
| Other outfit accessories | 600 | Belt hardware, straps and any remaining worn objects. |
| Unallocated integration reserve | 1,400 | Facial repair, hand deformation, neck join and silhouette corrections. |
| **Allocated geometry subtotal** | **38,600** | |
| **Total including reserve** | **40,000** | |

The head and visible facial assemblies receive 16,800 triangles before hair/accessories. The first cap/ear-accessory trial totals 1,530 triangles (cap 1,174; ear jewelry 356), so their combined 2,000 allowance leaves room for the planned rear opening/strap and small necklace details. Those features still need fit review; the allocation does not approve them. Reserve is held centrally; it is not a reason for each part to independently exceed its line. Do not spend unused body triangles on rejected facial geometry. A cap-off hairstyle may use a different hair mesh, but either selected state must fit the same complete-character maximum.

## Actual sources measured

The local Blender 5.2.1 audit opened existing files without saving them. Detailed per-object counts, source hashes and reduction measurements are in [the audit report](../art/generated/characters/ren/parts-workflow-v1/lod-budget-v1/audit.json); [the audit script](../tools/character_art/audit_ren_lod_budget.py) runs in background Blender. The original experiment ran under `.local/ren-lod-audit/`; its unchanged evidence and script logic are preserved at these committable paths. They are not runtime dependencies.

| Source or component | Rendered triangles | Meaning |
| --- | ---: | --- |
| Corrected `Ren_Head` in EyeUV derivative | 13,790 | Includes stitched lids/lips and cavity; **can stay within 14,000**. |
| Both sclera | 7,936 | Two 64-segment, 32-ring ellipsoids: 3,968 each. Main eye reduction opportunity. |
| Both irises | 944 | 472 each; current geometry includes graphic color treatment. |
| Both upper/lower liner sets and outer lashes | 182 | Already inexpensive; do not flatten the silhouette to save these few triangles. |
| Teeth / tongue | 792 / 776 | Can be resampled locally with pose/clearance verification. |
| **Complete current face** | **24,420** | 14 exported mesh objects; not a complete character. |
| Fitted V5 hair | 45,536 | 37 selected mesh objects; **hair alone exceeds the full-character goal**. |
| **Current face plus selected hair** | **69,956** | Before cap, headphones, body or clothes. |
| Original Meshy `model.glb` | 41,259 | Whole character, single mesh; includes its old head/hair/outfit. Not a body-only count. |
| Meshy `model-pre-remeshed.glb` | 1,944,104 | Dense immutable source for contour/detail reference or baking, never the game LOD0. |
| Meshy rigged `char1` | 41,231 | Whole-character mesh, no facial blendshapes. Another 80 triangles belong to an `Icosphere` helper. |
| Canonical-body V2 `char1` | 41,231 | Same whole-character geometry with provisional shared-rig work. Helpers add 84 triangles and must be excluded from character export. |
| Old shared-female facial candidate | 48,283 | Historical whole-character/eyelid study; not the selected new facial surface. |

The selected facial source is [EyeUV integration](../art/generated/characters/ren/parts-workflow-v1/face-uv-v1/eye-integration-v2/README.md), SHA-256 `6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959`. [FBX export metadata](../art/generated/characters/ren/parts-workflow-v1/face-integration-v2/fbx-export.json) records the same object counts. [V5 hair topology](../art/generated/characters/ren/parts-workflow-v1/hair-cleanup-v1/fitted-v5/report.json) records 22,556 Blender polygons but **45,536 triangles**. The local fitted workspace also contains other source/reference objects; its entire scene total is not the selected hair total.

There is **no approved, semantically separated body/clothes/shoes/accessory count yet**. The current body options are whole-character monoliths, so adding their full counts to the new head would double-count old facial parts. Extract the selected outfit/body, remove replaced head/hair and confirmed permanently hidden surfaces, then measure those parts against the allocation. Shared skeleton standardization does not itself reduce mesh geometry. The canonical V2 hand QA is still provisional, so preserve and review finger deformation during retopology.

## Reduction order

1. **Finish facial correctness at the current density.** Keep the 43-anchor eyelid boundaries and 55-edge mouth aperture contracts intact. Preserve Ren's compact nose-to-mouth distance, tapered chin, rose vermilion contour, narrow eyes and lip seal. The known intermediate-gaze sclera exposure must be fixed before reducing the eye shell. A triangle target does not approve that defect.
2. **Rebuild eye sampling from the existing analytic surface.** A 24-segment, 12-ring UV sphere has 528 triangles, so two full shells would use 1,056 instead of 7,936. This leaves 744 within the 1,800 eye allowance for irises and the existing 182 liner/lash triangles. Start around 560 triangles for both iris assemblies and review them at maximum face size. These are candidate sampling settings, not visually accepted meshes. Preserve the eye's curvature, occlusion and gaze relationship; do not simply expose a flat card at profile.
3. **Lower hair sampling while keeping authored centerlines.** V5 has 34 dense locks, each 49 rings by 12 profile vertices and 1,172 triangles, plus a 4,512-triangle root base and two 588-triangle flyaways. Resampling is more controllable than rediscovering the style through AI retopology. An illustrative topology allocation is 32 side/back locks at 17 rings × 6 profile points (6,400 triangles total), two fringe locks at 21 × 8 (664), a 32-column × 12-ring root base (736), and two eight-ring × six-point capped flyaways (184): **7,984 triangles**. Sample along curvature and preserve tip/front silhouette points; this arithmetic does not certify visual equivalence. Rebuild normals and check assembled front, quarter, profile, top and underside.
4. **Retopologize body/clothes around visible silhouette and joints.** Preserve garment thickness where visible, cuffs, neckline, armholes, hands and shoe contours. Delete body under permanently opaque clothes only after pose tests. Use fewer loops on broad covered torso/leg regions; preserve elbow/knee/shoulder/hip/knuckle loops. Transfer skin weights to the same shared female skeleton, normalize, and test extreme poses before rebaking textures.
5. **Simplify cap, headphones and jewelry locally.** Model round profiles with the lowest adequate segment count; bake bevels, seams and small relief. Use a separate worn/cap-off hair variant where it reduces occluded hair without changing visible fringe. Keep all chosen variants inside the same full-character ceiling.

For lower LODs, create one stable target topology per LOD, then transfer/re-author every required morph onto that topology. Do not independently decimate each expression. Keep the original construction meshes and vertex/source-ID contracts immutable. A constrained facial retopology can reduce hidden scalp/back/neck density while manually protecting the wet lip line, commissures, eyelid rims and expression loops; a vertex-group weighting alone is not proof those structures survived. Blender documents both vertex-group-controlled Collapse and boundary delimiters for Planar reduction. [Blender Decimate manual](https://docs.blender.org/manual/en/5.0/modeling/modifiers/generate/decimate.html)

## Representative local face decimation result

A throwaway Blender test reduced only evaluated static copies of the selected 13,790-triangle head to 50%, without modifying its source file. The source SHA remained unchanged.

| Independent static pose | Output vertices | Output triangles | Same topology as rest? |
| --- | ---: | ---: | --- |
| Closed rest | 3,509 | 6,894 | Baseline |
| Open A | 3,509 | 6,894 | **No: triangle-index hash differs** |
| Full blink | 3,501 | 6,894 | **No: vertex count and triangle-index hash differ** |

Blender refused applying Decimate to a duplicate that still had shape keys. Static evaluated copies had no shape keys, as expected. These independently reduced poses therefore cannot be installed as corresponding blendshapes merely because their triangle totals match.

Nearest reduced-surface distances sampled at source vertices were small: closed-rest p95 approximately `0.000443` source units, maximum `0.001871`. In the blink-affected regions, maximum reached `0.001317` left and `0.001247` right during closure. These are one-way geometric samples, **not** a silhouette, closed-eyelid, oral-clearance or animation pass; nearby opposing lip/lid surfaces can make nearest-distance metrics look deceptively good. No reduced face was accepted or installed. The experiment supports keeping the existing LOD0 face and testing any later constrained reduction separately.

## Tripo options verified on 2026-09-13

The currently cached official CLI is **`tripo-cli` 0.4.0**. Its `skill/commands/process.md`, `dist/knowledge/models.js`, `params.js` and `chains.js` were read locally. Its package identifies `vast-enterprise/Tripo-API-CLI` as its repository, but that GitHub URL returned 404 during this review. The current [official v3 retopology page](https://developers.tripo3d.ai/en/docs/mesh-decimate) was opened directly and matches the installed CLI's two algorithm tiers. Older public OpenAPI v2 pages use different field names; keep those interfaces separate. No paid job, model upload or generation was run for this research.

| Operation | Exact interface and limits | Cost / fit for Ren |
| --- | --- | --- |
| Smart LowPoly, public OpenAPI v2 | `type=highpoly_to_lowpoly`, `model_version=P-v2.0-20251225`; target 500–20,000 triangle faces, or 500–10,000 quad faces; `part_names` selects names from segmentation; `bake=true` by default. | Base 30 credits; quad documentation adds 5. Suitable for isolated static hair/clothes/accessory experiments, not a preservation guarantee for the facial rig. |
| Current official v3 / CLI retopology | `tripo mesh decimate INPUT --face-limit N`; CLI sends `POST https://openapi.tripo3d.ai/v3/mesh/decimate`, `model=v2.0` by default. Same 500–20,000 triangle / 500–10,000 quad window; `bake` and `part_names` supported. | Official v3 lists 30 credits. The target is not a guaranteed final ceiling: recount returned triangles. |
| Current v3 basic decimation | `-p model=v1.0`; required face limit, 500–2,000,000 triangles or up to 150,000 quads; no `bake` or `part_names`. | Official v3 lists 10 credits. Its wider limit is not an advantage for protected facial loops and existing paint. This supported v3 `model=v1.0` must not be confused with deprecated v2 `P-v1.0-20250506`. |
| Conversion/remeshing | `type=convert_model`; `quad=true` enables retopology; `face_limit`, optional `force_symmetry`, `part_names`, `pack_uv`, `bake`, format selection. `with_animation=true` describes skeletal binding/animation structures. | Base 5 credits, plus 5 for reduction/custom conversion options. A paid conversion can alter topology and UVs; use local FBX export for a format-only need. |
| Import / segmentation | Existing model import below 150 MB; mesh segmentation obtains selectable part names. | Import free; segmentation 40 credits. Existing locally separated objects can be uploaded individually, avoiding semantic segmentation cost and ambiguity. |
| New P2 generation | Explicit `P2-20260801` / CLI `--model tripo-p2`; 48–50,000 triangle target or 48–25,000 quads; supports `export_uv`. | Cached CLI lists 100 credits bare / 110 textured; project's native head and hair receipts each recorded 100 bare. This creates a new mesh; it is not retopology that preserves the repaired face. |

The public [Smart LowPoly page](https://docs.tripo3d.ai/mesh-editing/smart-low-poly-p-v2-0-20251225.html) and [mesh-editing page](https://platform.tripo3d.ai/docs/editing) establish the v2 fields and target window. [Conversion](https://docs.tripo3d.ai/export/conversion.html), [import](https://docs.tripo3d.ai/model-generation/import-model.html), and [pricing](https://docs.tripo3d.ai/get-started/pricing.html) support those separate operations and costs. The current [v3 retopology reference](https://developers.tripo3d.ai/en/docs/mesh-decimate) confirms v2.0/v1.0 costs, limits and bake/part-selection differences. The basic upper limit also varies by source H-series version/geometry tier in its detailed table; 2,000,000 is the highest documented triangle limit, not universal. P2 generation details above are from the installed 0.4.0 package and existing project receipts, not attributed to the older public v2 generation pages. Actual submitted-task consumption remains the authoritative charge; no dollar estimate or account balance is needed for this decision.

For a future isolated static experiment, the matching CLI form would be `npx tripo-cli@0.4.0 mesh decimate INPUT.glb --face-limit 8000 -p model=v2.0 --no-bake`. This is a documented proposal, **not executed**. Bake should be enabled only when a valid texture source is intentionally being transferred. Use triangles for this proposed trial. The v3 retopology page exposes `quad` but also says output GLB and no `format` parameter; that is not a dependable promise of stored native quad topology. Public v2 conversion and installed CLI differ on GLTF-with-quad behavior too. If editable native quads are needed, verify the actual returned representation and a supported FBX path separately. Count triangles after reimport either way.

**No reviewed Tripo documentation promises preservation of our existing blendshape names, vertex order, source IDs, eye/mouth loop connectivity, skin weights, UV coordinates or facial deformation.** Conversion's `with_animation` option is not a blendshape-preservation contract. Retopology necessarily changes vertex correspondence unless an explicit transfer mechanism is provided and verified. Keep the authored facial mesh out of automated vendor reduction for LOD0. If a later vendor result is tested, compare geometry, UVs, shape-key counts/names/deltas, material assignment and actual animated output before using it.

`bake=true` cannot invent an accepted missing source atlas or repair likeness. V5 hair currently has overlapping longitudinal working UVs, automatic flyaway UVs and an unwrapped root-base gap; it has no approved painting atlas. Establish its final topology and valid UV packing before painting or baking. The corrected head has validated FaceUV/EyeUV construction, but the first Tripo texture trial is a separate static candidate and not a completed animated texture pipeline. UV repacking or retopology invalidates direct use of old coordinates and demands a new transfer/bake review.

## Actual Tripo hair trial after the local visual review

The local 7,900-triangle hair met its count but still looked too schematic. A single
authorized smart-retopology job then tested the original native hair with the v3
`v2.0` model, an 8,000-triangle target, `quad=false` and `bake=false`. It cost
**30 credits** and returned **9,026 triangles**, 12.825% over target. The service
returned FBX, despite the current documentation describing GLB output. Exact
provider bytes and a clearly labeled local GLB derivative are preserved in the
[trial package and matched comparisons](../art/generated/characters/ren/parts-workflow-v1/tripo-hair-lowpoly-v1/README.md).

The returned layered shag preserves the original lock layout and silhouette
better than the compact local reconstruction. It also has no UVs, visible inner
crossbars, damaged crown transitions, 193 position-connected components and 82
edges shared by more than two triangles. Those are actual inspected limitations,
not a claim that every open boundary is defective. Use it as a stronger potential
repair basis, with local cleanup, head/cap fitting and a fresh unwrap before paint.
It is not installed as the production hair. The count miss confirms that Tripo's
requested target must be checked after import.

This test supports using Tripo for isolated static parts while retaining the
authored facial topology. It does not establish preservation of blendshapes,
skinning or animation. The original source remained byte-identical; there was one
task and no paid conversion or second submission.

## LOD and phone validation

Start with complete-character targets of **40k / 24k / 12k / 6k triangles** for LOD0–3. These are proposed production budgets, not measured transition thresholds. Choose transitions from actual face height on screen and hide eye-interior/detail work only when it cannot be resolved. Preserve a higher face LOD independently of body detail during conversation if necessary, while recounting the combined active state. Background partygoers need their own lower budgets; 40k for every visible crowd member is not assumed.

Unity 6.3 offers Mesh LOD and LOD Group approaches; LOD reduces polygons, material complexity and renderer count as objects become smaller on screen. Use an explicit LOD Group for authored, reviewed animated variants until the chosen automatic path is shown to support our skinning/morph requirements. Check transition frames for doubled geometry and visible facial popping. [Unity 6.3 LOD documentation](https://docs.unity3d.com/6000.3/Documentation/Manual/LevelOfDetail.html)

Triangle count alone does not establish 30 fps. Record imported vertices (UV/normal/material splits increase them), skinned renderer/material counts, active blendshape cost, texture memory, transparent overdraw, shadows and colored-light passes. Combine compatible materials/objects after authoring while retaining independent facial controls. Opaque graphic hair geometry may be preferable to many overlapping transparent cards in the nightclub, but profile the actual shader and lighting. Unity specifically calls out vertex splitting, material count and skinned-mesh overhead. [Unity model performance guidance](https://docs.unity3d.com/6000.3/Documentation/Manual/ModelingOptimizedCharacters.html)

Acceptance requires actual Unity captures at the maximum conversation face size in front, quarter and profile; full and partial blink; intermediate gaze; lip seal; open speech; combined gaze/blink/speech; and cap/hair/headphone fit. Compare silhouettes separately from eyelid/lip closure and interior clearance. Test neutral diffuse and moving colored lighting. Run the dressed character, selected NPC load, live audio/lip sync and full nightclub on iPhone 15 Plus long enough to observe sustained thermals, targeting 33.3 ms per frame with frame-time headroom. No desktop viewer or triangle spreadsheet substitutes for that measurement.

**Current implementation:** the assembled V2 head uses the unchanged 13,790-triangle head surface, a reduced 1,702-triangle eye assembly, 1,568 triangles of teeth/tongue, 7,900 local hair triangles and 1,466 cap/jewelry triangles. Total: **26,426 source triangles / 26,422 imported Unity triangles**. The four-triangle back-hair import difference remains unexplained. Source geometry leaves 13,574 triangles against the full 40k target for the remaining character and reserve. This is a working head review, not a dressed-character count or final likeness approval.

**Next implementation decision:** use the Tripo trial as a possible basis for a locally repaired layered shag, preserving the verified facial surface and controls. Fit, unwrap and review that hair before adopting it. Then extract and retopologize the body/outfit to the remaining budget. Retain all original vendor and frozen review sources.
