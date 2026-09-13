# Ren parts audit: verification-triangle-glb

Source: `B:\lucid-loop\art\generated\characters\ren\parts-workflow-v1\audits\_verification\triangle-fixture.glb`

SHA-256: `e5f4a8cd1fc1bfc51d500e9b01de9b89d43f651554dc01640256319c3debdac5`

Blender 5.1.1; original source hash checked before and after.

| Object | Vertices | Faces | Triangles | Face types (corners: count) | Imported components | Coincident components |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| SeamedQuads | 8 | 4 | 4 | {"3": 4} | 2 | 1 |
| NgonAndTriangle | 8 | 4 | 4 | {"3": 4} | 2 | 2 |

The source mesh was not welded, decimated, fitted, or rebuilt. The second component count joins only quantized coincident positions in an analysis graph, accounting for many GLB UV-seam duplicates. It can also join touching parts; it does not identify anatomical regions.

Neutral renders use imported materials under a fixed light setup. Clay removes material appearance. Component colors show position-coincidence connectivity; different colors do not automatically mean separate anatomical parts.

Rendering was skipped for this audit.

| Object / UV layer | UV islands | Largest island (% of faces) | Near-zero UV faces |
| --- | ---: | ---: | ---: |
| SeamedQuads / UVMap | 2 | 50.00% | 0 |
| NgonAndTriangle / UVMap | 2 | 75.00% | 0 |

Near-zero UV faces have absolute polygon UV area <= 1e-12. Summed UV areas are not overlap-free atlas coverage; island counts do not establish projection quality.

Open `audit-scene.blend` for the isolated imported scene. `report.json` holds full statistics and limitations; CSV tables enumerate component bounds/counts and UV islands. `analysis-data/` contains read-only extracted arrays and labels for reproducible graph analysis. These files are audit copies, not edited replacement assets.

## Limits

- Counts are imported base topology per object instance; modifiers and rigs are not applied.
- Position coincidence is a quantized analysis graph, not a geometry edit or proof of semantic parts.
- Objects are analyzed independently; touching parts may coincide and separate surfaces may intersect.
- Component colors show coincidence connectivity, not automatically identified head/hair/eyes.
- Quad percentage alone does not establish eyelid/mouth loops, deformation quality, or mobile readiness.
- GLB triangle topology cannot certify native FBX quads; compare the actual FBX import.
- UV counts do not prove absence of overlap or acceptable distortion/texel density.
