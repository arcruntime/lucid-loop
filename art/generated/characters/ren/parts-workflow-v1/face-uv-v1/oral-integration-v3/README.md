# Final mouth-corner topology: UV derivative

This separate derivative uses the frozen
`mouth-v1/corner-clearance-v3/Ren_P2_Oral_Reconstruction.blend`, SHA256
`d163c041427ffff60390f2dcae57ca131f8d97b0946fa2499bb5a26ca48f47d5`.
The older `oral-integration/` viewer asset remains untouched.

`Ren_P2_Oral_FaceUV.blend` transfers the existing per-corner UV mapping to the
**8,829 surviving native faces**, skipping removed source polygons. The new
**55-column, 220-face cavity** receives a single circular island in the same
freed old-interior atlas rectangle. No source shape, position or topology is
changed by the transfer.

The helper now derives the ring count from the explicit native aperture list;
it validates the 4N-face, 4N+1-vertex cup topology and discovers each successive
ring by adjacency. The manifest beside the input blend supplies the current
aperture and must match the input file hash.

`integration-report.json` and `exact-overlap-report.json` record actual validation:
full-head UV intersections, all mesh geometry and world transforms, every existing
shape key, original UV layers, saved/reopened UV equality and unchanged source
bytes. New eye annuli still require separate chart extension and another combined
check. No texture painting or Unity changes are included.

Blender 5.1.1 completed with exit 0: **zero full-head UV intersections** above
`1e-11` UV area; all preservation and saved-file checks passed. The old viewer
blend also still matches its recorded SHA256. Ruff and Python compilation pass.

```powershell
& $blender --background --threads 2 --factory-startup --offline-mode `
  --python-exit-code 1 --python tools/character_art/unwrap_ren_p2_head.py -- `
  --oral-input art/generated/characters/ren/parts-workflow-v1/mouth-v1/corner-clearance-v3/Ren_P2_Oral_Reconstruction.blend `
  --output art/generated/characters/ren/parts-workflow-v1/face-uv-v1/oral-integration-v3
```
