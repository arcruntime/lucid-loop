# Ren P2 local oral reconstruction

The current handoff is [corner-clearance-v3/Ren_P2_Oral_Reconstruction.blend](corner-clearance-v3/Ren_P2_Oral_Reconstruction.blend), SHA-256 `d163c041427ffff60390f2dcae57ca131f8d97b0946fa2499bb5a26ca48f47d5`. This is a limited mouth construction and two-pose prototype for combined face review. It does not complete Ren's likeness, facial expressions, speech coverage, rig, or Unity integration.

The native P2 source remains immutable at `parts-generation-v1/tripo/head/originals/model.fbx`, SHA-256 `cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819`. Front is +X; up is +Z. Dimensions here are normalized source units, not physical metres. The original Ren artist and cleaned closed-rest/open-A plates govern the intended appearance. Unity-chan supplied construction guidance; no Unity-chan or generic replacement head was transplanted.

## What changed

The cavity is attached to an explicitly traced 55-edge native aperture. All 55 attachment edges each have exactly one retained native exterior face and one new cavity face. The cut removes the folded native oral interior and crude dental insert, replacing them with a closed cavity backing, separate upper and lower dental arches, and a closed tongue mesh.

The initial corner trace exposed an inherited source defect: native edge 85–93 crosses native face 1265. The final trace encloses that small folded left commissure web. Relative to the preceding reconstruction it additionally removes faces 8, 1263, 1264, 1265, 1272 and vertices 85, 86, 96, 103. No retained native Basis vertex was moved to make this repair.

| Provenance | Current result |
|---|---:|
| Retained native vertices | 9,129 |
| Removed native vertices | 1,113 |
| Retained native faces | 8,829 |
| Removed native faces | 1,318 |
| New cavity vertices / faces | 166 / 220 |
| Retained native local Basis position error | 0 |
| Retained native loop UV error, including saved readback | 0 |
| Saved retained face corner/source-ID mismatches | 0 |

Every retained point has `source_vertex_id` as an INT POINT attribute containing its native FBX vertex index. New points are `-1`. `source_face_id` is an INT FACE attribute with the same convention. Separate teeth and tongue carry `-1` IDs throughout. The [source-ID patch](corner-clearance-v3/source-id-patch.json) records removed IDs, every intentional native shape delta, and the empty list of changed native Basis positions. Retained source custom normals are copied; later integrated color/UV studies are separate derivatives.

## Prototype controls

The source native open-rest is the Basis. `mouthSeal=1` produces the closed rest, and `jawOpen_A=1` produces the controlled open-A. Apply matching values to the head, upper teeth, lower teeth, and tongue. The upper arch remains fixed; the lower arch and tongue follow the opening. There is no jaw skeleton in this study.

Five reviewed states are native-rest, seal-half, sealed, open-half, and open-A. Only one non-Basis key is active at a time. The two keys have not been certified in arbitrary simultaneous combinations. A review slider can traverse sealed → seal-half → native-rest → open-half → open-A. Rebasing to a closed production neutral and adding the full speech/expression set come after neutral review.

## Rendered evidence

All images below are actual 768×768 Blender EEVEE renders using matched camera, lighting, and framing. The full [36-image manifest](corner-clearance-v3/review-renders.json) records their hashes. Face renders include the untouched native brow and eye construction, which separate workers are correcting.

| State | Front mouth | Quarter mouth | Profile mouth |
|---|---|---|---|
| Native source | [Front](corner-clearance-v3/renders/source/mouth-front.png) | [Quarter](corner-clearance-v3/renders/source/mouth-three-quarter.png) | [Profile](corner-clearance-v3/renders/source/mouth-profile.png) |
| Reconstructed native-rest | [Front](corner-clearance-v3/renders/native-rest/mouth-front.png) | [Quarter](corner-clearance-v3/renders/native-rest/mouth-three-quarter.png) | [Profile](corner-clearance-v3/renders/native-rest/mouth-profile.png) |
| Closed seal | [Front](corner-clearance-v3/renders/sealed/mouth-front.png) | [Quarter](corner-clearance-v3/renders/sealed/mouth-three-quarter.png) | [Profile](corner-clearance-v3/renders/sealed/mouth-profile.png) |
| Open-A | [Front](corner-clearance-v3/renders/open-A/mouth-front.png) | [Quarter](corner-clearance-v3/renders/open-A/mouth-three-quarter.png) | [Profile](corner-clearance-v3/renders/open-A/mouth-profile.png) |

Each state directory also contains matching `face-front.png`, `face-three-quarter.png`, and `face-profile.png`. Intermediate captures are under `seal-half/` and `open-half/`. The inspected open-A shows a restrained upper tooth strip and a low pink tongue. Native mouth-corner angularity remains visible at enlarged scale; this is not final aesthetic approval.

## Verification and limits

The [saved verification](corner-clearance-v3/saved-shape-verification.json) reopens this exact Blender hash and checks source attributes, retained face corners, UVs, shape deltas, cavity attachment, and oral clearance. Native world position and shape-delta readback errors are below `1.6e-8` source units from float transforms.

All six separate oral-part collision pairs are clear in all five poses. Cavity self/nonadjacent-head triangle tests are clear in the four nonsealed poses. The sealed pose intentionally overlaps at the wet lip line: 106 first-flange contact pairs remain, all within `0.000950` source units of the aperture. Of these, 105 are between new cavity flange triangles and one contacts a nearby native lip face. This is an explicit seal construction choice, not a claim of zero intersections. A grid of 25,677 frontal rays exposes no cavity through the closed seal.

Collision checks use nonparallel segment/triangle intersections and exclude shared-vertex adjacency. Coplanar overlap is not certified. The separate arches and tongue have no boundary or nonmanifold edges; the full native head retains unrelated source boundaries and one nonmanifold edge. Full bilingual visemes, tongue articulation, continuous pose sweeps, skinning, Unity behavior, and phone performance remain outside this prototype.

## Rebuild

Run from the repository root, in order, waiting for each command to finish. Ordinary Python needs NumPy and SciPy; Blender runs with two threads.

```powershell
python tools/character_art/build_ren_p2_mouth.py --variant corner-clearance-v3
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --factory-startup --offline-mode --threads 2 --python-exit-code 1 --python tools/character_art/build_ren_p2_mouth.py -- --variant corner-clearance-v3 --build
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --factory-startup --offline-mode --threads 2 --python-exit-code 1 --python tools/character_art/build_ren_p2_mouth.py -- --variant corner-clearance-v3 --verify
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --factory-startup --offline-mode --threads 2 --python-exit-code 1 --python tools/character_art/build_ren_p2_mouth.py -- --variant corner-clearance-v3 --render
```

The top-level `Ren_P2_Oral_Reconstruction.blend` and `renders/` preserve the preceding `d79c8595…` review snapshot, which has a residual native corner intersection. Top-level `preview/` captures are older still. `rejected-clearance-v1/` preserves the first construction with failed oral-part clearance. Use the `corner-clearance-v3/` files linked above for current integration and evidence.
