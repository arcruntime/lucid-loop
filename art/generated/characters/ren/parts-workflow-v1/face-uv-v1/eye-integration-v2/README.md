# Welded eye annulus UVs

`Ren_P2_Face_EyeUV.blend` extends `FaceUV_v1` across both new lid annuli on the
frozen integrated head, SHA256
`b15a34d87e0442e3dd9b73d789770d6613436276ea26421e5963018cbdc4952b`.
This includes the final eye-worker contracts captured by the root integration.
The source blend remains unchanged.

Both annuli join the **existing facial UV chart**. Each of the 43 shared outer
anchors uses the exact UV from its surviving adjacent face corners. Thin-plate
interpolation in native YZ extends those coordinates to the generated vertices.
No new island or seam was required at either outer boundary. All pre-existing
face and cavity UV corners remain exact.

Validation completed in Blender 5.1.1 with exit 0:

- Left: 411 triangles, 249 vertices; right: 409 triangles, 248 vertices.
- Zero positive-area UV intersections across the full head above `1e-11` UV area.
- No degenerate annulus UV triangles; exact outer-boundary UV agreement.
- Every mesh position, shape key and recorded settings, vertex/face attribute,
  material slot/index, corner normal and object matrix remains exact.
- Saved-file reopening preserves those signatures and the new UV coordinates.

At 1024², the annuli occupy approximately **7,397 / 7,185 texels**. Anisotropy
median is 1.60 / 1.66, p95 2.29 / 2.24, maximum about 4.23. These are geometric
UV measurements, not proof of final texture quality. The synthetic annulus test
also reproduced an affine UV field within `4.4e-8` with no intersections.

Actual checker captures were inspected:

- `checker-front.png`, `checker-quarter.png`, `checker-profile.png`: mouth seal
  preset, eyes open; checker continuity follows the welded outer lid boundaries.
- `checker-blink-front.png`, `checker-blink-quarter.png`: both blink shapes at
  100%; the checker follows the closing lid surfaces.

Checker materials and screenshot poses are transient and **not saved into the
derivative**. The derivative retains the source materials and original shape
settings. Existing brow/checker distortion reflects the supplied geometry/UVs;
this task does not change that geometry or approve character likeness. Artist
painting, final seam/color review and runtime testing remain separate work.

`integration-report.json`, `preservation-signatures.json`,
`exact-overlap-report.json` and `provenance.json` record checks and hashes.

```powershell
& $blender --background --threads 2 --factory-startup --offline-mode `
  --python-exit-code 1 --python tools/character_art/unwrap_ren_p2_head.py -- `
  --eye-input art/generated/characters/ren/parts-workflow-v1/face-integration-v2/Ren_P2_Face_Integrated.blend `
  --output art/generated/characters/ren/parts-workflow-v1/face-uv-v1/eye-integration-v2
```

The reusable `extend_eye_chart()` reads integrated `face_part` and
`source_vertex_id` attributes directly; it does not depend on mutable external
contract vertex ordering. An ambiguous boundary seam, degenerate triangle or
UV intersection causes rejection rather than silent fallback.
