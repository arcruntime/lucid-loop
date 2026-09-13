# Native P2 Ren head: construction review

The native FBX has a real mouth opening and four useful closed quad edge loops around the external lips. Its mouth interior is incomplete: the dental insert is crude, the lower cavity is folded and has a branched open boundary, and there is no independently connected tongue or lower dental arch. Preserve the external lip geometry as a local construction scaffold; this is not a finished speech rig or an approved likeness pass.

The original source SHA-256 remains `cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819`. No source geometry, fitting, shape keys, or Unity assets were changed. Diagnostic cutaways and overlays use temporary audit copies. The native model faces **+X**, with **+Z up**, verified from actual front and profile renders; the general audit used `--front-yaw 90`.

## Native topology facts

| Property | Actual result |
| --- | ---: |
| Mesh objects / materials | 1 / 1 |
| Vertices / edges | 10,242 / 20,333 |
| Native polygons | 10,147 |
| Quads / triangles | 9,005 / 1,142 |
| Quad fraction of native polygons | 88.75% |
| Triangulated rendering count | 19,152 |
| Connected components, raw and position-coincident | 61 / 61 |
| UV layers / UV islands | 1 / 765 |
| Single-face UV islands | 204 |
| Largest UV island | 638 faces, 6.29% of polygons |
| Zero-area UV faces / corners outside 0–1 | 0 / 0 |
| Textures / shape keys / modifiers | 0 / 0 / 0 |

The requested 10k quad target did not produce an all-quad mesh. The native topology is still materially more useful for lip construction than a triangulated painted-closed surface. Quad count and UV validity do not establish deformation readiness or texture-paint quality. The fragmented UV layout needs review before facial painting.

The main head component has 7,785 vertices and 8,120 polygons (7,321 quads and 799 triangles). Three large eye-region components have 597, 305, and 291 vertices; two occupy nearly the same negative-Y eye region, so they must be inspected before treating the result as a clean two-eye assembly. The isolated oral component has 212 vertices and 264 polygons (130 quads and 134 triangles). The remaining 56 smaller components are predominantly visible lash geometry. Component ranks are analysis labels, not object names or anatomical guarantees.

There are 1,221 boundary edges across the full model and one nonmanifold edge. Most boundaries belong to separate thin surfaces; this count does not mean every edge is a defect. The nonmanifold edge is in a lash-region component, with exact source vertex IDs 6733 and 6737, not in the mouth. Full tables and tolerances are in `report.json` and `mouth-construction-topology.json`.

## What the actual mouth contains

`mouth-clay-front.png`, `mouth-clay-three-quarter.png`, and `mouth-clay-profile.png` show genuinely separated upper and lower lips. Front rays and exact sagittal triangle intersections confirm recessed surfaces behind the opening; it is not a painted dark line. The central aperture is approximately 0.023 in normalized source units, while whole-head height is approximately 1.0. These are source coordinates, not established physical meters. The opening reads narrower and more filled by the dental insert than the supplied open-A references.

The main head surface includes an upper cavity and lower interior. Its deep mouth boundary is a **51-vertex, 53-edge connected group with two degree-four branch vertices**, rather than one simple loop. Its bounds run from X 0.071–0.243, Y −0.085–0.085, Z −0.226–−0.141. The cutaway and section plot show folded and overlapping-looking interior sheets near the lower mouth. Exact self-intersection counts were not computed, so this is a visual construction finding, not an intersection-free/colliding certification.

`mouth-insert-isolated-front.png` and `mouth-insert-isolated-profile.png` identify the separate piece as a rudimentary upper dental arch, with coarse scalloped incisor ends and a broad joined backing. It has one open 28-vertex boundary. It is not separate upper/lower teeth plus a tongue. Any tongue-like floor currently belongs to the main head component; independent tongue articulation is absent.

## Usable native lip loops

The wire render shows circumferential quad bands surrounding the opening. A stricter topology traversal independently finds **four closed loops of 44 edges each**, crossing only valence-four vertices with all-quad incident faces. Exact edge and vertex IDs are recorded under `native_lip_edge_loop_traces.closed_loops` in `mouth-construction-topology.json`. The colored native-loop overlays show these paths on the unchanged surface.

Other sampled paths stop at poles or triangles; this check does not certify all mouth topology. The four loops give a concrete starting point for local lip controls and thickness, without replacing the head. They are external loops, not the irregular deep cavity boundary described above.

These paths follow outer lip bands; they do not supply a verified 44-point inner aperture rim. The actual inner rim still needs explicit identification and local cleanup before constructing its oral attachment.

## Recommended local construction sequence

1. Keep this native FBX immutable and establish the desired closed-rest and open-A lip silhouettes against the supplied front and right references. The raised brow masses and filled-looking eye surfaces visible in `clay-front.png` are already present in the native generation; they still need a likeness decision before broader facial engineering.
2. Preserve the existing external lip loops and approved surrounding face. Rebuild the irregular **inside** mouth surfaces with a clean upper cavity, lower cavity, and lip thickness, using the exact existing rim as the attachment guide. Do not transplant a generic head or use an anatomical fit that changes Ren's proportions.
3. Author a restrained anime upper dental arch, lower dental arch, and a separate tongue. Keep upper teeth attached to the upper face, lower teeth to the jaw, and tongue controls independent. Use the current insert only as positional evidence until its appearance and clearance are accepted.
4. Author closed-rest/seal and open-A examples first, then intermediate values and lip-corner motion. Check front, right profile, and three-quarter views for thickness, self-intersection, dental exposure, and crease artifacts before expanding speech targets.
5. Review or locally reorganize facial UVs after geometric decisions, retaining an auditable correspondence to the untouched source. The 765-island atlas is not yet approved for coherent facial painting.

This review recommends a local rebuild of the unfinished oral interior. It does not approve generation rerolls, whole-head fitting, final material/rig readiness, English/Japanese coverage, or device performance.

## Evidence and reproduction

- `README.md`, `report.json`: reusable native audit, all nine full-head renders, component/UV tables.
- `mouth-wire-front.png`: actual native polygon layout around the mouth.
- `mouth-native-loops-front.png`, `mouth-native-loops-three-quarter.png`: traced native loops, with small diagnostic line offsets for visibility.
- `mouth-interior-cutaway.png`: temporary half-head copy exposing the native dental insert and cavity.
- `mouth-sections.png`, `mouth-sagittal-sections.json`: exact geometry-plane intersections.
- `mouth-front-rays.json`: sampled front hits on the main head and isolated oral insert.
- `mouth-detail-renders.json`, `mouth-native-loop-renders.json`: rendered artifact hashes.

Run the general audit with `python tools/character_art/audit_ren_parts.py --source art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx --id p2-head-native --front-yaw 90 --resume`.

Run topology inspection with `python tools/character_art/inspect_ren_p2_head.py`. Run the detail renders with Blender 5.1.1 in background mode and `--python tools/character_art/inspect_ren_p2_head.py -- --blender-audit`; append `--loops-only` for the native-loop overlays. Both scripts verify the immutable source hash.
