# Ren parts audits

Run the reusable auditor from the workspace with ordinary Python (NumPy/SciPy installed). It launches its own isolated Blender 5.1.1 background processes and writes only into this directory.

```powershell
python tools/character_art/audit_ren_parts.py --source PATH_TO_NATIVE.fbx --id p2-head-native
python tools/character_art/audit_ren_parts.py --source PATH_TO_MODEL.glb --id p2-head-glb
```

Use separate IDs for native FBX and GLB; the GLB's triangle topology cannot certify FBX quads. `--resume` reuses completed extraction/analysis/render checkpoints only with the same source hash, tolerances and camera settings. To correct an inspected orientation explicitly, add `--resume --rerender --front-yaw ANGLE`; previous PNGs/camera metadata remain locally under ignored `previous-renders/`. Default renders are 1024 square. `--front-yaw` changes the front camera azimuth around Blender +Z; zero looks from -Y. `--skip-render` provides an analysis-only run.

Each audit includes actual imported vertices/edges/faces/derived triangles, face corner counts, world bounds, materials/UVs, shape keys/modifiers, connected-component CSVs, UV-island CSVs and a machine-readable `report.json`. Neutral, clay and component-color renders show front, three-quarter and profile. `audit-scene.blend` is an isolated imported copy with studio fixtures; original meshes are never welded, decimated, fitted or rebuilt. The source hash is verified before and after. The audit stores bulky extracted arrays locally for resumability; they are diagnostic intermediates rather than production assets.

Imported vertex components and position-coincidence components are separate measurements. The latter is an analysis-only graph over quantized positions, useful for recognizing GLB UV-seam duplication. It can join touching surfaces and does not classify anatomy. Colors show this connectivity, not automatic head/hair/eye segmentation. Native quads also do not prove animation edge flow, oral interior, blink quality or phone performance.

- [Existing Tripo H 3.1 closed-rest baseline](tripo-h31-closed-baseline/README.md)
- [Baseline visual findings and limitations](tripo-h31-closed-baseline/VISUAL_REVIEW.md)
- [Actual P2 native hair audit](p2-hair-native/README.md)
- [P2 hair visual findings and limitations](p2-hair-native/VISUAL_REVIEW.md)
- [Native FBX fixture result](verification-native-fbx/report.json)
- [Equivalent triangulated GLB fixture result](verification-triangle-glb/report.json)
- [Verification results](_verification/results.json)

The synthetic fixture contains two seamed quads plus a disconnected pentagon and triangle. FBX imports as 4 polygons / 8 derived triangles; GLB imports as 8 triangular polygons. Both correctly yield 4 imported components and 3 position-coincidence components. This checks actual Blender import behavior without modifying Ren. Reproduce the fixture using `_verification/create_native_fixture.py` inside Blender, then audit its two outputs and run `_verification/check_results.py` with ordinary Python.
