# Native P2 face UV study

This is a UV-only study of the immutable native P2 head. Positions, polygon
topology and the original native UV layer are preserved. `FaceUV_v1` is a separate
new layer. No Ren painting, rigging, Unity integration or likeness approval is
implied by this study.

Open `Ren_P2_FaceUV_Study.blend`. Inspect `checker-front.png`,
`checker-quarter.png`, `checker-profile.png` and `atlas-checker.png`.
The checker follows the actual UVs on the actual source surface. Native brow,
eyelash and oral defects remain visible because this study does not repair them.

The main front island contains **3,140 polygons**, continuously covering the
forehead, nose, cheeks and lips through the chin. Peripheral seams divide ears,
rear skull and neck. Four small patches contain 14 pinched source polygons at
outer eyelid corners and mouth commissures: keeping these inside the initial
angle-based island produced measured UV intersections. Local seams eliminate
those overlaps without moving a single geometric vertex.

After the separate geometry cleanup removes old interior and disconnected
components, the 7,071 surviving exterior source polygons use **15 islands**.
The full source-preserving study includes 102 islands because it also retains
87 obsolete interior/component islands. Those are not intended as the final
oral or eye layout. Blender reports one unsolved island during the full-source
unwrap; this warning has not been attributed to a specific island. All final packed triangles are still tested
for degeneracy and positive-area intersections. The final integrated head must
be rechecked after replacement eye/mouth geometry is attached.

The face owns the large left rectangle of the atlas. Secondary pieces occupy
the right strip; corner patches receive more space than discarded fragments.
The packed source study has no degenerate UV triangles, no overlapping interior
samples at 2048², and **zero positive-area triangle intersections** above the
documented numerical tolerance of `1e-11` UV area. The exact test clips candidate
triangle pairs, including adjacent triangles; shared edges alone do not fail.
See `uv-report.json` and `exact-overlap-report.json` for the actual measurements.

At 1024², the main-island eye regions receive about **31,900 texels** and mouth
region about **13,000 texels**. Median density is approximately 759 and 590
texels per native source unit respectively (these units are not claimed to be
meters). Eye anisotropy is median 1.09, p95 1.54; mouth median 1.06, p95 1.62.
The mouth maximum remains approximately 16.7 on a small source triangle, so
local distortion still warrants closeup review after mouth repair. These regional
statistics exclude the four separate corner patches and old internal geometry.

## Rebuild and integrate

Run with the installed Blender 5.1.1, from any working directory:

```powershell
& $blender --background --threads 2 --factory-startup --offline-mode `
  --python-exit-code 1 --python tools/character_art/unwrap_ren_p2_head.py
```

The script pins the native geometry hash before using its reviewed seam IDs,
adds the new UV layer, packs islands, validates intersections and renders the
checker views. It never overwrites the input audit scene.

`source-corner-uv-map.json` stores original polygon/corner IDs and UVs. In an
isolated derived combined scene, import the helper and call:

```python
mapping = json.loads(mapping_path.read_text())
result = unwrap_ren_p2_head.apply_mapping(head_object, mapping)
```

Every target vertex must carry `source_vertex_id`. Faces are matched by their
native corner set, then UVs assigned by the corresponding native vertex ID;
object vertex order and polygon order may change. New cavity/teeth/tongue faces
with `-1` IDs reject by default. `allow_unmapped=True` is an explicit integration
mode: it returns those polygon IDs and leaves their UVs untouched. It does not
invent a cavity layout or claim complete coverage. The new face layer must be
selected explicitly when rendering/projecting; original UVs remain available.

`mapping-verification.json` records saved-file and reordered-subset checks.
Approval still requires the integrated neutral geometry, reviewed paintings,
seam inspection, actual final texture coverage and deformation testing.
