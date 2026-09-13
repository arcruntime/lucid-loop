# Ren parts audit: verification-native-fbx

Source: `B:\lucid-loop\art\generated\characters\ren\parts-workflow-v1\audits\_verification\native-fixture.fbx`

SHA-256: `3aab32a3a50ad25c44078b0be4859a2c23e7c8aebc4c4ea293166fb1cf6a8c88`

Blender 5.1.1; original source hash checked before and after.

| Object | Vertices | Faces | Triangles | Face types (corners: count) | Imported components | Coincident components |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| NgonAndTriangle | 8 | 2 | 4 | {"3": 1, "5": 1} | 2 | 2 |
| SeamedQuads | 8 | 2 | 4 | {"4": 2} | 2 | 1 |

The source mesh was not welded, decimated, fitted, or rebuilt. The second component count joins only quantized coincident positions in an analysis graph, accounting for many GLB UV-seam duplicates. It can also join touching parts; it does not identify anatomical regions.

Neutral renders use imported materials under a fixed light setup. Clay removes material appearance. Component colors show position-coincidence connectivity; different colors do not automatically mean separate anatomical parts.

| Pass | Front | Three-quarter | Profile |
| --- | --- | --- | --- |
| neutral | [front](neutral-front.png) | [three-quarter](neutral-three-quarter.png) | [profile](neutral-profile.png) |
| clay | [front](clay-front.png) | [three-quarter](clay-three-quarter.png) | [profile](clay-profile.png) |
| components | [front](components-front.png) | [three-quarter](components-three-quarter.png) | [profile](components-profile.png) |

| Object / UV layer | UV islands | Largest island (% of faces) | Near-zero UV faces |
| --- | ---: | ---: | ---: |
| NgonAndTriangle / FixtureUV | 2 | 50.00% | 0 |
| SeamedQuads / FixtureUV | 2 | 50.00% | 0 |

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
