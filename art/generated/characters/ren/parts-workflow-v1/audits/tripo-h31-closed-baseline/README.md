# Ren parts audit: tripo-h31-closed-baseline

Source: `B:\lucid-loop\art\generated\characters\ren\bust-comparison-v1\tripo\studio-h3.1-closed-rest\ren-tripo-studio-h3.1-closed-rest-original-8k.glb`

SHA-256: `4743a85883df56b2adcca72ed9f429eb537d92b93eff28eb2495966e95a39f0a`

Blender 5.1.1; original source hash checked before and after.

| Object | Vertices | Faces | Triangles | Face types (corners: count) | Imported components | Coincident components |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| tripo_node_5e31d589-61b1-4f73-8956-7487823ff534 | 988,591 | 1,859,218 | 1,859,218 | {"3": 1859218} | 1,120 | 1 |

The source mesh was not welded, decimated, fitted, or rebuilt. The second component count joins only quantized coincident positions in an analysis graph, accounting for many GLB UV-seam duplicates. It can also join touching parts; it does not identify anatomical regions.

Neutral renders use imported materials under a fixed light setup. Clay removes material appearance. Component colors show position-coincidence connectivity; different colors do not automatically mean separate anatomical parts.

| Pass | Front | Three-quarter | Profile |
| --- | --- | --- | --- |
| neutral | [front](neutral-front.png) | [three-quarter](neutral-three-quarter.png) | [profile](neutral-profile.png) |
| clay | [front](clay-front.png) | [three-quarter](clay-three-quarter.png) | [profile](clay-profile.png) |
| components | [front](components-front.png) | [three-quarter](components-three-quarter.png) | [profile](components-profile.png) |

| Object / UV layer | UV islands | Largest island (% of faces) | Near-zero UV faces |
| --- | ---: | ---: | ---: |
| tripo_node_5e31d589-61b1-4f73-8956-7487823ff534 / UVMap | 1,120 | 2.56% | 58,371 |

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
