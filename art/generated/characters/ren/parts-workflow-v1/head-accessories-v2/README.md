# Ren cap V2 — frozen fit candidate

Use the standalone `.blend`, `.fbx` or `.glb` in
**[band-clearance-v1](band-clearance-v1/README.md)** for the current cap and ear
pieces. The matching frozen hair is
`../ren-shag-v1/selected-v2/Ren_Shag_CapState.blend`, with `capOn=1`.
The top-level V2 exports remain the immutable pre-clearance revision.

The final painted context is `Ren_Head_Accessories_CombinedV2.blend`. Actual
[front](combined-front.png), [quarter](combined-quarter.png) and
[profile](combined-profile.png) captures use the exact frozen cap/hair pair.
[combined-review.json](combined-review.json) records their hashes and review.
The lower crown and curved bill preserve the reviewed V2 direction; the
frontal fringe stems are continuous and no large crown protrusions are visible.
Two tiny pale specks remain on the ear-side black band in quarter/profile.
The fit is therefore a working-viewer candidate with a recorded visual defect,
not final artist-likeness approval or a claim of intersection-free geometry.

This is a cap-only revision against the frozen painted complete-head assembly.
The head, painted materials and ear jewelry are unchanged. V1 remains intact.
The lower crown and raised bill direction was reviewed by the root art worker,
then exported for the hair worker's independent cap-on refit.

The cap top is now `z=0.5235`, including the small button, compared with V1's
`z=0.5758`. The crown itself tops out at `z=0.520`. The original scalp reaches
`z=0.499756`; forcing the cap down to `z=0.48` would intersect that unchanged
head. The front band rises to `z=0.248`, exposing more forehead, eyes and room
for the swept fringe. The bill is shorter and more curved, with an asymmetric
edge. The new cloth map adds broad angular panel shading without extra seams
or stitch geometry.

- [Front](front.png) and [quarter](quarter.png): preserved initial diagnostics
  with obsolete V1-fitted hair and known large crown intersections.
- [Cap-only front](cap-only-front.png) and [quarter](cap-only-quarter.png): same
  painted head with hair hidden so the cap silhouette can be judged clearly.
- `Ren_Cap_V2_PaintedStudy.blend`: native editable context; select `Ren_Cap` for
  cap inspection. The standalone cap is **1,110 rendered triangles**.
- [direction-study.json](direction-study.json): source hash, head-preservation
  check, crown dimensions and sampled radial scalp clearance.

The current standalone files in `band-clearance-v1` contain the cap and unchanged
V1 ear pieces only: **1,466 triangles** (cap 1,110; cuffs 200; drop 156).
[band-clearance-v1/export-verification.json](band-clearance-v1/export-verification.json) verifies ear geometry,
UVs and matrices against V1, all three exported counts/positions/material slots,
and the exact embedded cap PNG. GLB placement is exact; maximum FBX reimport
error is approximately `8.73e-8` native units. The standalone file is the
assembler input; the painted direction study contains contextual parts.
[fitting-accessories.json](fitting-accessories.json) now points to the current
derivative. The three toggle meshes have `Head` bind metadata and no armature
or skin weights. Preserve the exported cloth roughness/two-sided setting and
silver metallic value when rebuilding URP materials.

The matching selected-v2 hair is now frozen against the corrected cap.
The local-only `interim-hair-v2-unreviewed/` preserves the rejected intermediate hair fit.
Use the `combined-*` captures above when reviewing the current pair. The
recorded remaining specks require future local cleanup; the sampled corridor
measurements do not prove every hair triangle clears cloth.

The first radial shell check covered 1,076 scalp vertices and found a minimum
clearance of approximately `0.00967` native units. Ten additional rays exit
through the intentional rear opening. This is a sampled scalp-fit check, not
an animation or collision certificate. Eight additional narrow lower-band
corridors prompted a local correction of 112 cap vertices, limited to `0.002`
native units with a `1e-7` floating-point tolerance. Their minimum gap increased
from `0.004475` to `0.006469`. Units remain the head's unnormalized
native frame, +X front and +Z up.

Reproduce with `tools/character_art/revise_ren_cap_fit.py` in background Blender
using at most two threads. `--cap-only` renders the saved candidate with hair
hidden. `--hair` selects the matching frozen hair; `--cap-source` must select
the standalone blend in `band-clearance-v1`. All output remains in this V2
directory. No source head, hair, shared rig, body, main Unity asset, commit or
push changed.
