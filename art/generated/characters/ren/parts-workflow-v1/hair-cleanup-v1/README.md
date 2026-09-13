# Ren P2 hair: local cleanup and fitted reconstruction

Current review trial: **[fitted-v5](fitted-v5/VISUAL_REVIEW.md)**. This is neutral hair geometry for review, not an approved likeness or a textured production wig. The native hair FBX and head FBX are immutable references. The head's vertices and world transform are checked against a fresh native import.

## Review artifacts

- [Assembled front](fitted-v5/candidate-assembled-front.png), [three-quarter](fitted-v5/candidate-assembled-three-quarter.png), [profile](fitted-v5/candidate-assembled-profile.png), [underside](fitted-v5/candidate-assembled-underside.png).
- [Hair-only underside](fitted-v5/candidate-underside.png) shows the fitted root base and layered clumps.
- [FBX](fitted-v5/ren-fitted-hair-trial.fbx) retains polygon faces; [GLB](fitted-v5/ren-fitted-hair-trial.glb) is triangulated.
- [Fit and per-object topology evidence](fitted-v5/report.json), [independent export reimport and source checks](fitted-v5/export-verification.json).

**Placement:** the latest hair is authored directly in the unchanged head's Blender coordinate frame, +X front and +Z up. Use identity placement with that head. The old `(-0.09, 0, 0.22)` rigid translation is used only for the matched raw-source comparison. Applying it to the fitted export would move the hair out of place.

## Why reconstruction was necessary

The native mesh had 2,013 edges with more than two incident faces and 296 inconsistently wound manifold edges. Its quads also included severe nonplanarity. Clearing custom normals, recalculating winding, changing triangulation, and using flat shading all left the visible crossbars and inner shards. This was real generated geometry, not just a shading problem. See [diagnosis/report.json](diagnosis/report.json) and the matched diagnostic PNGs.

A conservative pruning test removed 115 branch faces and 908 faces wholly inside the reviewed head, without moving retained vertices. It left visible crossbars and introduced holes. That result is preserved in [pruning](pruning/report.json) and is not the candidate.

The side/back replacement traces the native outer envelope, then forms closed tapered locks. The fitted version replaces the remaining branched native crown as well. It contains 32 source-envelope side/back clumps, two front clumps following the approved asymmetric fringe, two short flyaways, and one continuous scalp-fitted root base. No native hair faces remain in the fitted candidate. The source influences contour and lock placement, not retained defective topology.

## Reproduce locally

From the repository root with Python and Blender 5.1.1 installed at the path in the script:

```powershell
python tools/character_art/clean_ren_p2_hair.py --stage fit
python tools/character_art/clean_ren_p2_hair.py --stage verify
```

The fit stage uses the local reconstruction workspace when present; otherwise it builds that intermediate from the native FBXs. Component ranks are recomputed without requiring the auditor's ignored NPZ cache. Separate `diagnose`, `prune`, and `reconstruct` stages reproduce earlier evidence. Rerunning a stage replaces that stage's derived outputs, never the native inputs.

Each Blender process runs in the background with isolated preferences. No interactive Blender or Unity scene is touched. Local `.blend` workspaces, Blender user settings, and temporary logs remain on disk but are excluded by the narrow local `.gitignore`.

## Remaining work

The clumps currently have overlapping longitudinal working UVs, the flyaways have automatic curve UVs, and the root base has no UVs. A coherent packed painting atlas and authored streaks are still required. No rigging, hair simulation, LOD, Unity integration, or sustained-phone performance acceptance is implied by these files. The unchanged provider head's eyes, mouth, proportions, and expression topology are separate tasks.
