# Ren eye repair: integration candidate

2026-09-13. This candidate preserves Ren's current elongated aperture, replaces the defective native socket/lid rolls, and closes with one moving lash seam. Root reviewed the front, quarter and profile studies as sufficient for a combined mouth/brow/eye integration trial. It is not final likeness, texture or Unity acceptance: residual inner-corner and brow shading needs review in the combined head.

## Frozen assets

| Asset | SHA-256 |
| --- | --- |
| `Ren_P2_Eyes_Neutral.blend` | `75a14bcb63ba1fb61bd23b23c2f8ce861d04b1e73c8317653fa94c54474bf054` |
| `annulus-L-contract.json` | `ce93e2cc5a62bb40242b30ac5253323601d0c1c5e600d02fe1d70343cb732bc2` |
| `annulus-R-contract.json` | `b8ffabdf7ad4d672858f66d8b65c76c02b57c17b4632140e187d607dd23e8444` |
| `construction.json` | `c91c3d58d2c122ae96ea64200667ce27591f12cda57d523ea1ae98037930370e` |

Source: `parts-generation-v1/tripo/head/originals/model.fbx`, SHA-256 `cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819`. Native coordinates face +X with +Z up. The original artist sheet remains the aesthetic authority; Unity-chan informed component coordination, not facial proportions or copied geometry.

## Construction and integration

- Remove 285 original main-head faces on the positive-Y side and 286 on the negative-Y side, identified explicitly by `removed_source_face_ids`. The old detached eye/lash components are also removed. Retain the mouth and healthy peripheral head.
- Each repair has 43 exact native outer boundary anchors and 44 paired inner aperture points. The left annulus has 249 vertices/411 triangles; the right 248/409. Constrained triangulation respects the concave native outer edge. Interior depth uses smooth thin-plate interpolation; blink displacement uses a harmonic field. A small generated outer-canthus correction prevents reversal when the aperture closes to a line.
- `source_vertex_id` is an INT POINT attribute: original FBX index on retained native vertices, −1 on new vertices. There are no neutral native-position patches. Retained positions differ from the double-precision audit arrays by at most `1.853e-8` through Blender float storage.
- `annulus-{L,R}-contract.json` includes ordered outer IDs, outer/inner ring indices, all faces, all positions, shape positions and per-generated-vertex boundary weights. `boundary_weights` propagate X-only brow corrections identically into Basis and blink. `yz_boundary_weights` describe the separate harmonic mapping. Paired aperture points stay independent of outer boundary depth changes.
- `Ren_Eye_L_SkinAnnulus` and `Ren_Eye_R_SkinAnnulus` remain independently exportable but are hidden in the saved scene. The visible `Ren_P2_EyeRepair_StitchedHead` welds their source-ID anchors into a review copy of the head. **Use either the stitched head or the independent patches with their removal contracts; integrating both duplicates skin.** The hidden `Ren_NativeMain_Inspection` is a component reference.
- Each side has coordinated `eyeBlinkL` or `eyeBlinkR` keys on skin, liner and lashes. The separate iris has `gazeLeft`, `gazeRight`, `gazeUp` and `gazeDown`. The iris and sclera sit 0.003 source units behind the lid construction surface to provide actual depth clearance. All saved controls are zero.
- Separate liner geometry follows the repaired neutral/closed surface. After applying substantial brow depth changes, review its clearance against the changed skin; the skin boundary-weight contract does not automatically reproject every independent liner vertex.

The material agent has the exact topology contracts. New annuli intentionally have no UV assignment yet. Extend the surviving FaceUV_v1 chart from original boundary **corner** UVs, respecting local corner seams. The grey-blue iris uses a Blender vertex-color material study; URP texture/material conversion remains part of integration.

## Actual evidence

- `reference-source-{front,quarter,profile}.png` and `neutral-{front,quarter,profile}.png` use matched cameras and lighting.
- `neutral-eyes-detail.png` shows the actual liner/iris/socket construction.
- `blink-{partial,closed}-{front,quarter,profile}.png` show 50% and 100% deformation of the saved topology.
- `saved-scene-verification.json` verifies retained IDs/positions and zero saved weights. Front-grid visible eye samples decrease 3185→2399→1582→798→0 at 0/25/50/75/100% closure. Both quarter-direction grids also return zero visible iris/sclera samples at full closure.
- `annulus-deformation-check.json` checks 41 weights per side: zero collapsed 3D triangles and zero negative front-projected triangles beyond `1e-10` tolerance; maximum edge-length ratio 2.520. A few full-closure canthus faces become side-facing in projection, while retaining nonzero 3D area.

These sampled checks do not prove continuous self-intersection freedom, all-angle visibility, final eye-state swaps, gaze/blink composition, Unity rendering, or device performance. The asset currently contains one neutral eye construction and its blink study, not Ren's six complete expressions.

## Reproduce

Run Blender 5.1.1 with `--background --threads 2 --offline-mode --python-exit-code 1 --python tools/character_art/build_ren_p2_eyes.py -- --build --lid-repair` from the repository root. Verification uses the same command with `--verify --lid-repair` after `--`. The script verifies the immutable source hash. Omitting `--lid-repair` rebuilds the earlier shutter checkpoint and should not be used for the integration candidate.
