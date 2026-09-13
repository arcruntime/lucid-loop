# Support placement correction v2

Transform-only correction to the failed v1 export. Source pigment library retained support parent-relative (+0.06,0,+0.20) after detaching Head; the approved full bridge review and original bridge library have support world identity. V2 removes exactly that offset from support only. Bridge and frame markers remain unchanged. No local mesh, pigment, UV or shape edits.

FBX SHA `a74d5cfd934bf8ab177b6922ea0434be7baaae04da36d06e3f10f578ffcb7d0d`. Keep v1 failed proof frozen. Reuse the existing marker calibration unchanged; both meshes now occupy correct shared-world positions.

Fresh Blender import passes; v1-v2-difference.json proves translation-only change with exact UV/color/morph arrays. The old support's first574 retained vertices differ from the refined support only by the previously reviewed local X repair <=0.002647915; 290 extra points subdivide seven existing faces. Full forensic matrices/bounds are in ../support-export-v1/support-placement-forensics.json.

Canonical bridge A/seal arrays are identical to Basis: no functional deformation is claimed. Linear pigment is retained exactly and must still be consumed once. Its original nearest-source pigment correspondence may reflect the detached-world offset; review color joins independently after placement correction. Do not claim all seams fixed.

Run exporter with -- --placement-v2 and fresh verifier with -- --placement-v2 --verify. No frozen sources changed.
