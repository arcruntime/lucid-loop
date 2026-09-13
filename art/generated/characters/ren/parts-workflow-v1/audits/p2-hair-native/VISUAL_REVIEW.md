# Actual P2 native hair FBX review

The broad locks, asymmetric part and outer wisps follow the approved V2 hair reference direction. This is useful as an unfitted shape candidate, but its visible surface defects and fragmented UVs need resolution before it becomes production hair.

All nine corrected source-only renders were inspected: neutral, clay and component colors from front, three-quarter and profile. Five additional cardinal/underside views were inspected. The source was generated without textures; the white/grey surface is expected and is not a failed hair texture.

## Orientation

The native FBX's front faces Blender +X. This was checked visually against `hair-front-v2.png` and the provider preview. The corrected audit uses `--front-yaw 90`: front camera +X, three-quarter azimuth 55 degrees, profile azimuth 0 degrees. Initial zero-yaw renders were preserved locally under ignored `previous-renders/`; the current named PNGs are the corrected views. Exported OBJ/GLB retain the imported artistic orientation while converting coordinates to Y-up.

## Actual topology and UVs

| Measurement | Actual native FBX import |
| --- | ---: |
| Vertices | 26,361 |
| Polygons | 28,234 |
| Quads | 22,820 (80.82%) |
| Triangular polygons | 5,414 |
| Derived render triangles | 51,054 |
| Connected components, imported and coincident-position graphs | 79 |
| Boundary edges | 4,298 |
| Edges incident to more than two faces | 2,013 |
| UV islands | 3,345 |
| Single-face UV islands | 1,191 |
| Largest UV island | 310 faces (1.10%) |

The 25,000 face request is not the returned polygon count. The 79 connected pieces are genuine separations in this FBX, unlike the earlier H bust's UV-seam fragments. However, the largest two components contain about 54% of all polygons and span multiple locks, so component separation does not yield one clean component per hair lock.

Supplemental analysis found no repeated-vertex faces or duplicate unordered polygon vertex sets. The nonmanifold edges have 3–9 incident faces; simple duplicate polygon removal would not resolve that finding. UVs exist, with two near-zero-area faces, but their many small charts are a poor starting point for straightforward directional hair painting. These measurements do not certify UV overlap/distortion or animation suitability.

## Visual construction

The front silhouette preserves the side-parted shag and broad tapered pieces. There are also long inward hanging sheets through the central opening, numerous thin outer strands, and conspicuous bands, triangular shards and abrupt surface transitions across the side/back locks. These appear in source-only neutral and clay views before any fitting or modification.

[Front clay](clay-front.png), [profile clay](clay-profile.png), and [component profile](components-profile.png) provide the clearest exterior evidence. [Underside](inspection-underside.png) and [front underside](inspection-front-underside.png) show open space with many overlapping inward hair surfaces. There is no obvious continuous skin cap or generated head in these views. This does not establish a clean hollow wig volume: the internal sheets still need inspection against the eventual scalp, and the source does not supply a clean inner shell.

Recommendation: retain as a separate shape/construction candidate and compare against the incoming head without assuming it is ready to rig, paint or ship. Resolve the surface branches/visible shards and establish usable hair UVs before accepting it for production. Hair/scalp fit, eye clearance, deformation, final Ren likeness and iPhone 15 Plus performance remain untested. No fitting or topology repair was performed in this audit.

Native source SHA-256 remains `9f8c2cb79c763bb048b4c0b62c058e2357802e86012badf0ce0a15f240a083c9`. Derived OBJ and GLB counts are independently checked in `supplemental-structure.json`; all 14 current review PNG hashes and three derivative file hashes are verified there as well.
