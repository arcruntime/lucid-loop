# Frozen texture-trial input

Submitted file: `Ren_P2_Texture_ClosedRest.glb`, **594,324 bytes**, SHA256
`a690b11aa070b846f2aaac3f472dbc4bd5a83132a0efa992fc07f15d7d753e70`.
Do not replace this file: task `daa7fda6-47ca-4ba9-ab51-6e4c74f2348d` used these
exact bytes. No additional paid task was submitted by this preparation worker.

This is a static evaluated copy of the corrected head checkpoint
`6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959`.
The working blend is unchanged. Mouth seal is 1; jaw opening, blinking and other
shapes are 0. The upload contains 14 head/eye/oral mesh objects and 24,420
triangles, without hair, body, skins, animation or morph targets.

The head's `FaceUV_v1` is the upload's sole UV layer / glTF `TEXCOORD_0`.
Other source head UV layers were removed from the **upload copy only**. Muted
neutral skin replaces the broad temporary rose lip material. Separate eyes,
liners, teeth and tongue remain present for context.

Axis handling is explicit: Blender `(X,Y,Z)` exports as glTF `(X,Z,-Y)`.
**Front +X remains +X; Blender up +Z becomes glTF up +Y.** Node world transforms
were decoded directly from the GLB and then independently checked through a
fresh Blender import. Maximum position error was `2.24e-8` native units; maximum
UV-corner error was `2.98e-8`. Triangle counts match. Export triangulates the
static copy as required by glTF; it does not remesh or alter the working topology.

`input-front.png`, `input-quarter.png`, `input-profile.png` show the prepared
Blender copy. `glb-reimport-front.png` shows the actual exported GLB reimport.
All were inspected. **Known export limitation:** iris colors survive, but white
catchlight dots darken in the GLB because its `COLOR_0` multiplies primitives
whose original Blender materials did not read vertex color. This was found after
submission and the submitted file was deliberately preserved. The reference
painting contains catchlights; final texture transfer can retain the separate
authored eyes. No claim of exact eye-material parity is made.

Evidence for later result auditing:

- `input-manifest.json`: source/file hashes, pose, matrices, material changes,
  export and reimport checks.
- `mesh-*.npz`: exact evaluated local/world positions, triangles, loop indices,
  material indices and UV arrays before export.
- `glb-structure.json`, `glb-audit.json`: raw GLB structure, material factors,
  transforms and numerical comparisons.
- `Ren_ClosedRest_Upload.blend`: static preparation snapshot, without working rigs.

Preparation used Blender 5.1.1 and exited 0. No Unity assets or working character
geometry were changed. Returned textures/mesh still require their own audit;
this input preparation does not approve the texture trial result.
