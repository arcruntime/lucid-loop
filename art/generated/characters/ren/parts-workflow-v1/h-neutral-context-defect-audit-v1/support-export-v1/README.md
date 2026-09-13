# Existing support and bridge: independent Unity handoff

Exported only the frozen matched-pigment support and jaw bridge. No eye/head/hair/cap mesh or new geometry/paint is included.

- FBX: `Ren_H_Support_JawBridge_Linear.fbx`, SHA `40792f0f151bf297b438f10abf1aa9dae6f3d32a0b2c5feab48caac85691fe8e`.
- Source pigment blend SHA `7a1e3fe11778aa033969aaaf53988e547e5bc416e0f1d6c8f14628761a852883`, unchanged.
- Exactly two mesh objects: support 864 vertices / 1,478 triangles; bridge 72 vertices / 70 triangles. Four empty axis markers carry the shared frame.
- Fresh Blender import: Basis error <=1.014e-7; bridge jawOpen_A/mouthSeal endpoint errors <=4.215e-8; linear RGBA and UV errors exactly zero. Read `fresh-import-verification.json`.

Replace old `Ren_H_ConcealedScalpBack` and append `Ren_H_JawSideBridge_R` in a new comparison derivative. Calibrate the exported markers into the existing shared frame, preserving each object's world placement. The support source library has world translation (+0.06,0,+0.20); bridge is identity. Do not apply that offset twice or assume both objects have identity placement. Parent under the same animated Head after alignment. No accepted face or eye positions change.

The FBX exports all morph weights at zero (Basis). Set neutral bridge jawOpen_A=0, mouthSeal=1, then follow the existing head's effective weights including seal suppression during opening. Canonical absolute shared-world morph arrays and source boundary IDs are in `export-contract.json`. Validate Unity endpoint import against these arrays; do not silently accept sparse morph loss.

FBX color encoding is explicitly LINEAR. Canonical RGBA floats are the original `HSupportSkinLinear` values. Verify Unity colors against them; if necessary restore those values once onto a private import-copy. Use vertex color, no base map, white tint and `_VertexColorSrgb=0`. A second sRGB decode would darken the pigment incorrectly. Bind the same selected skin-lighting response as the surrounding head; do not use the Blender carrier as production shading.

This closes the previously reviewed profile jaw wedge and supplies matched support pigment. It does not finish forehead extraction topology, all side seams, or mobile LOD. Previous f/q/profile/open/half bridge proof is in `../../h-visible-fragment-cleanup-v1/`. Actual Unity combined comparison remains required.

Reproduce with `tools/character_art/export_ren_neutral_support.py` in Blender, then run a fresh Blender process with the same script plus `-- --verify`. Original source is read only; derivatives are written only here.
