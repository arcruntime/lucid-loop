# Ren guarded gesture

Integration inputs:

- `RenGuarded_WeightAndDigitCorrection.fbx`: neutral corrected body mesh and complete rig, no animation. Original neutral vertex positions and UVs are byte-identical; 764 vertices have localized hand/elbow/underarm weight changes.
- `RenGuarded_Animation.fbx`: small armature-only enter/hold/exit clip; 30 fps, frames 1–121, hold frames 37–79. Sample at 1.8 seconds for the guarded pose.
- `RenGuarded.blend`: complete posed character and action. `RenGuarded_EnterHoldExit.fbx` includes the full character for portable review.
- `RenGuarded_WeightAndDigitCorrection.blend`: neutral body/rig derivative.

The 15 left digit rest matrices are explicitly corrected to the current hand. Canonical names/hierarchy and all non-digit rests remain unchanged. Apply these same digit rest corrections consistently to the shared female rig and LOD1; this is not a hidden Ren-only skeleton. `digit-rest-correction.json` records old/new armature-space matrices; `digit-local-rests.json` records parent-local transforms for retargeting existing idle curves. Unity should use the imported FBX local rest transforms for axis conversion.

The gesture contains real keyed rotations on all 15 corrected digit bones, plus left upper arm/forearm/wrist and right upper arm. `animation-fbx-check.json` verifies animation after fresh FBX import. `weight-changes.json` is the exact per-vertex weight delta, and `guarded-qa.json` records invariant hashes and collision samples. Nine sampled frames have no hand triangle intersections with head skin, neck repair/join, or headphones; these samples are not an exhaustive continuous collision test.

`front.png` and `quarter.png` show the actual held pose against the designer's bottom-right GUARDED reference in `art/characters/ren-model-sheet.png`. The large underarm spike was removed by localized weight smoothing. Source underarm bunching and angular low-poly finger detail remain visible and require final visual acceptance; no claim of 1:1 designer approval is made. The original final LOD0 is unchanged (SHA256 `aafa6f9a17c0d6da47930ea9c2f9f6bb82284635059af47970c7f966d6983df7`).

Rebuild: `author_ren_guarded_final.py`, `export_ren_guarded_final.py`, `export_ren_guarded_static.py`, `render_ren_guarded_final.py`, `verify_ren_guarded_final.py` under `tools/character_art/`.
