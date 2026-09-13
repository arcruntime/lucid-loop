# Fitted hair trial v5: combined-scene integration candidate

The parent reviewed the front, three-quarter, and profile and selected this exact version for a combined head/material trial. The crown and root attachments are coherent enough for that step. It is **not final Ren hair or accepted exact likeness**: the front still reads as two broad smooth bangs and the side/back as regular leaf-shaped clumps. Finer asymmetric tousled layers, painted detail, and fringe refinement remain explicit.

## What changed

- Removed all retained native crown faces and their branch connections. A continuous scalp-sampled base now closes the crown, with a high front border beneath the fringe. Its perimeter stays open intentionally as a wig shell.
- Moved the 32 source-traced side/back lock roots toward the unchanged scalp, reduced clearance and width, and shortened the ends by varied amounts. Transported frames follow the fitted centerlines to avoid the crease bands seen in the earlier ring-offset trial.
- Rebuilt the two front curves around the approved asymmetric part. Front rays determine clearance; nearest eyelash normals are not used to fit the fringe.
- Replaced floating fine native wisps with two deliberate short flyaways.
- Kept the head's vertices and world matrix unchanged. Hair is authored directly in that head's coordinate frame, **identity placement, +X front and +Z up**. Do not apply the old `(-0.09, 0, 0.22)` raw-source study transform.

## Matched visual evidence

| View | Original at raw rigid-study placement | Fitted candidate |
| --- | --- | --- |
| Front | [Source](source-assembled-front.png) | [Candidate](candidate-assembled-front.png) |
| Three-quarter | [Source](source-assembled-three-quarter.png) | [Candidate](candidate-assembled-three-quarter.png) |
| Profile | [Source](source-assembled-profile.png) | [Candidate](candidate-assembled-profile.png) |
| Underside | [Source](source-assembled-underside.png) | [Candidate](candidate-assembled-underside.png) |

Each corresponding pair uses the same neutral material, lighting, orthographic framing, and unchanged head. Hair-only versions also exist for all four views. The [hair-only underside](candidate-underside.png) exposes the root shell and layered construction.

The authority is the original artist's cap-off Ren drawing, with approved V2 front/right/back plates guiding the simplified lock layout. Reference hashes are in `export-verification.json`. The unchanged head's current eyes, mouth, and proportions are outside this hair-cleanup task; these captures do not establish accepted head likeness.

## Actual counts and verification

| Artifact | Mesh objects | Vertices | Polygon faces | Triangles |
| --- | ---: | ---: | ---: | ---: |
| Original native hair FBX | 1 | 26,361 | 28,234 | 51,054 |
| Fitted v5 source / reimported FBX | 37 | 22,921 | 22,556 | 45,536 |
| Reimported v5 GLB | 37 | 22,995 | 45,536 | 45,536 |

The GLB vertex count includes export splits. Both formats reimport with matching world bounds and finite coordinates. Sixteen render hashes and both export hashes pass. A fresh native head import has the same local vertex SHA256 and world matrix as the head in the fitted review workspace. Both immutable native FBX file hashes still match.

Native hair had 2,013 edges with more than two incident faces. Fitted v5 has **zero such branched edges**. The 34 main lofted locks are closed. The root shell has 96 intentional boundary edges. The two visibly capped flyaways each have 32 boundary edges at coincident but unwelded curve-cap seams; these should be welded when consolidating the hair. No claim of a single watertight or intersection-free simulation mesh is made. Overlap between layered locks and their root shell is intentional.

The source hair is a contour reference; **no native hair faces are retained** in this fitted version. Per-lock source tracing, actual fit displacements, topology, and artifact hashes are in `report.json`.

## UV and production limits

Thirty-four main locks use overlapping longitudinal working UVs, two flyaways have automatic curve UVs, and the root shell has no UVs. Thirty-six of the 37 exported objects therefore have UV layers, but there is **no accepted painting atlas**. A packed atlas and authored hair streaks are still required.

The candidate uses neutral material only. It has no textures, rig, simulation, LOD, merged renderer setup, or measured iPhone performance. Use it to evaluate the combined head and material before refining the fringe and finer layers.

## Preserved earlier and auxiliary versions

`reconstruction/` preserves the rejected rigid-source placement and native crown. `fitted-v2/` preserves the first closed-crown fit with creased side rings. `fitted-v3/` fixes ring frames but exposes faulty front paths. `fitted-v4/` records the nearest-eyelash fitting failure. An auxiliary `fitted-v6/` only welds coincident flyaway cap rims and was generated before the decision to freeze v5; it is not the selected integration candidate. No further hair geometry changes are authorized by this handoff.
