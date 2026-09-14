# Ren blink / body material repair

Ren's Game view flashed skin-colored patches on thighs, lower legs and arms
during idle blinks. The eyelid correction shader was sampling the face atlas on
body and clothing UVs. This was a material-assignment bug; the pants geometry
did not need to be cut or rebuilt.

`JsonUtility` can instantiate an empty nested `closedEye` object for a manifest
entry that omits the field. The builder now requires both authored texture paths
before enabling `_CLOSED_EYE_CORRECTION`. The existing material assets have been
repaired, retaining correction on the head skin only. Lip-patch face lighting is
independent of eye-atlas sampling.

In Unity, open versus fully closed blink captures produced zero changed pixels
in the bottom 62% of the image for both LOD0 and LOD1. The before image visibly
reproduces the reported thigh/calf patches. Evidence is under
`Unity/Assets/CharacterArt/Generated/RenLOD0/Evidence/body-artifacts/`:

- `blink-before-closed.png`: original defect.
- `blink-fixed-closed.png`: repaired LOD0.
- `blink-fixed-lod1-closed.png`: repaired LOD1.
- `blink-body-render.txt` and `blink-materials.txt`: assertions.
- `gaze-speech-tests.xml`: seven passing tests, including the separate-cap /
  uncompressed-hair contract.

Use **Lucid Loop → Ren LOD0 → Repair blink material assignments** to migrate
materials, and **Verify blink does not repaint body** in the playing RenLOD0
scene to repeat the image check. The latter preserves the original before
capture and temporarily freezes idle motion/lights for deterministic images.

This repair does not resolve the separate neck seam. The source neck's baked
tonal band and uneven transition ring are being corrected separately.
