# Ren P2 part placement study

2026-09-13. This is an untextured source assembly, not an accepted neutral Ren or
an animated character. Native Tripo originals remain unchanged.

`tools/character_art/assemble_ren_parts_study.py` imports the actual head and hair
FBX files into isolated Blender 5.1.1. It records source hashes and imported vertex,
polygon and UV hashes, then verifies those mesh hashes again after placement.
Only object-parent transforms and temporary clay materials change.

Both native parts face Blender +X, with Z up. The head stays in its original
coordinate frame. Placement V1 used hair scale 0.85 and position (0, 0, 0.115).
Its profile revealed a large intersection exposing the rear scalp, so it was
rejected. Placement V2 uses hair scale 1.0 and position (-0.09, 0, 0.22), with no
rotation. This clears that broad rear intersection and keeps the face visible.

The parent reviewed V2 for a raw construction viewer only. Remaining native hair
crossbars, shards and fine wisps are visible; the head's raised brows, filled eyes
and unfinished mouth also remain visible. These transforms do not establish final
hair clearance, the artist's likeness, or a production-ready assembly.

V2's matched head-only and assembled renders use one fixed orthographic camera
setup, widened to include the full crown. The exact camera and placement values
are recorded in [placement.json](placement-v2/placement.json).

- [Assembled front](placement-v2/assembled-front.png)
- [Assembled three-quarter](placement-v2/assembled-three-quarter.png)
- [Assembled profile](placement-v2/assembled-profile.png)
- [Head only](placement-v2/head-front.png)

The saved workspace is `placement-v2/ren-p2-parts-placement.blend`. A Unity import
must account for FBX axis and unit conversion explicitly, then preserve identical
head scale and position in the head-only and assembled views.
