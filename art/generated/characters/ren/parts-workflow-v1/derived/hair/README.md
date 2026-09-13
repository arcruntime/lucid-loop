# Unfitted P2 hair derivatives

These are local Blender 5.1.1 exports of the original P2 hair FBX, preserving its shape and imported topology. No fitting, decimation, welding, face rebuilding, UV changes or artistic rotation was performed.

- `ren-p2-hair-native-quads.obj` with `.mtl`: 26,361 vertices, 22,820 quads and 5,414 triangular polygons. Original UVs and normals are exported. This geometry-only generation has no texture images.
- `ren-p2-hair-triangulated.glb`: 51,054 triangles. Standard glTF export triangulates the native polygons and can duplicate render vertices at UV/normal seams.
- `provenance.json`: native FBX hash, derived file hashes, Blender version and coordinate/export policy.
- `build_derivatives.py`: reproducible export and additional clay inspection views, using the isolated native audit scene.

The native front faces Blender +X; these exports retain that artistic orientation in Y-up export coordinates. They are not aligned or fitted to a head yet. The original FBX remains the native source of truth. See [the audit](../../audits/p2-hair-native/README.md) and [visual findings](../../audits/p2-hair-native/VISUAL_REVIEW.md) before choosing this candidate for production.
