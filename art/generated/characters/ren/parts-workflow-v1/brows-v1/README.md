# Local brow relief correction

`relief-v4/ren-p2-brow-repair.blend` is the selected **integration trial**, not
final Ren likeness approval. Its matched before/after front, quarter, profile
and brow closeup renders show the broad generated brow shelves reduced to a
smoother forehead-to-eye transition. Slight outer brow pinches remain visible;
these require another look with the repaired eyelids and actual brow painting.

The fixed-boundary minimum-curvature patch changes 439 native main-head vertices.
Maximum displacement is 0.03291 in source units (the original head is about one
unit tall). The eye aperture at Z <= 0.111, nose, mouth, cheeks, jaw, ears, neck,
and remaining skull vertices stay unchanged. Polygon connectivity and UVs stay
exact. Normals are recomputed for the actual geometry in both comparison stages.
`construction.json` records every changed original vertex and its before/after
coordinates. All vertices retain the `source_vertex_id` integration attribute.

Rebuild from the repository root with system Python/SciPy and Blender 5.1.1:

```powershell
python tools/character_art/repair_ren_p2_brows.py --prepare-biharmonic
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background `
  --factory-startup --python tools/character_art/repair_ren_p2_brows.py -- `
  --id relief-v4 --biharmonic
```

The source FBX is hash-checked and never overwritten. The first three local
fairing experiments are not selected for integration: simple iterative smoothing
left creases, and a polynomial depth fit accentuated an unwanted lower fold.
