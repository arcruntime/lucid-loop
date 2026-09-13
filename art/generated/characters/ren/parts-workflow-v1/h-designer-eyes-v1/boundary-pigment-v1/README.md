# Original H eye-boundary pigment samples

Provisional data for the new designer-shaped skin shutters. These files do not
approve eye geometry, likeness, final pigment, or Unity integration.

`boundary-pigment.json` contains 274 L and 299 R rows keyed by original
`source_h_vertex_id`, with native positions, original GLB UVs, raw and filtered
sRGB colors, eligibility reasons and interpolation neighbors. The NPZ contains
the corresponding IDs, colors and eligibility arrays; its hash is in the JSON.
`boundary-pigment-review.png` visualizes the actual sampled colors and boundaries.

Source texture is the immutable original H `image-0.jpg` (SHA-256
`9750e636f595a01e2c1bfd00699b5bcd083e8f99a39a43adf8b1da68436e09ce`).
Original GLB `TEXCOORD_0` supplies UVs, using glTF's top-left image convention.
All 573 boundary positions were checked against the accepted portable H mesh.
Coincident UV-split source IDs need not survive as the same representative ID in
the extracted mesh; sample the original ID's UV, not a coincident substitute.

All L samples and 297 R samples met the recorded warm-skin eligibility filter.
The two rejected R samples use interpolation along the cyclic boundary between
valid neighbors. Raw samples and reasons remain available for inspection. This
filter is a provisional color heuristic, not semantic segmentation.

Colors are sRGB in `[0,1]`. Convert to linear before assigning Blender color
attributes. Match by source identity and verified boundary mapping; do not use
approximate projected-pixel matching for generated shutter attachment. Preserve
the boundary colors and author smoky pigment separately on the shutter interior.

Rebuild from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --threads 2 --python-exit-code 1 --python tools/character_art/sample_ren_designer_eye_boundary.py -- --extract
python tools/character_art/sample_ren_designer_eye_boundary.py
```

The Blender step reads and hashes the source without saving it. The Python step
requires NumPy, Pillow and Matplotlib. No source geometry or texture is modified.
