# Ren separate hair reference — first review

`hair-three-view-v1.png` is the first hair-only front / screen-right profile / back sheet for the separate-part production workflow. It is **1774 × 887 RGB**, generated with the built-in imagegen tool and copied byte-for-byte into this folder. It has not been cropped, resized, repainted or submitted to a 3D service.

The original artist sheet at `art/characters/ren-model-sheet.png` is authoritative. Its cap-off casual view supplies the uncovered crown; the larger portrait supplies the layered locks. `head-reference-sheets-v1/00-closed-rest-v2.png` supplies supporting volume information. Neither input is an edit target.

The result keeps the pale-blond layered shag, loose fringe and anime shading, with no visible head, skin, mannequin or accessories. Several thin flyaways remain and should be simplified in generation plates. The cap-off back is an extrapolation: the original rear drawing includes the cap. Cross-view registration is not guaranteed by an illustrated sheet. The supporting generated reference also makes the hair somewhat fuller and more polished than the small original cap-off drawing.

**Status: awaiting root review before standalone geometry plates.** No paid 3D job has been submitted by this task.

- Exact prompt: `prompts/hair-three-view-v1.txt`
- Input/output hashes, original generator path and review notes: `provenance.json`

## V2 geometry-reference cleanup

`hair-three-view-v2.png` revises V1 after root review: broad and medium locks replace most nested strand outlines and wire-like flyaways; the crown is slightly lower while the asymmetric fringe and messy layered silhouette remain. It is **1774 × 887 RGB**, preserved byte-for-byte from built-in imagegen.

A few outer wisps remain, and the back view still contains many tapered ends. The uncapped rear remains an extrapolation. Root accepted V2 for preparation of the standalone plates below; those plates still require review before 3D submission.

- Exact edit prompt: `prompts/hair-three-view-v2.txt`
- Input/output hashes and review: `provenance-v2.json`

## Standalone geometry inputs from V2

Root accepted V2 as the geometry reference, retaining the caveat that the uncapped back is inferred. Three standalone plates now await root review before 3D submission:

| Plate | View | Actual dimensions |
| --- | --- | --- |
| `generation-inputs/hair-front-v2.png` | Front | 1122 × 1402 RGB |
| `generation-inputs/hair-right-v2.png` | Profile facing screen-right | 1122 × 1402 RGB |
| `generation-inputs/hair-back-v2.png` | Back | 1122 × 1402 RGB |

Each is a separate built-in imagegen edit using the exact accepted V2 sheet. Labels and other views were removed; the hair design was visually preserved. Files retain the generator's original bytes, with no crop, resize or post-processing. These illustrated views are not mathematically registered projections of an existing mesh.

Exact prompts are `prompts/hair-front-v2.txt`, `prompts/hair-right-v2.txt` and `prompts/hair-back-v2.txt`. `generation-inputs/provenance.json` records input/output hashes, original generated paths and review notes. No paid 3D jobs were performed by this task.
