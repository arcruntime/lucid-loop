# Dressed nightclub

Open `Unity/Assets/Gyms/Scenes/BeforeTheDrop.unity`. The room is an authored 3D set, with a curved bar and illuminated bottle bays, detailed DJ equipment and stage, animated LED vortex, dance-floor and ceiling inlays, raised VIP seating, enclosed front booths, glass rails, palms, cocktail lamps and glasses, entrances, restroom doors, truss and fixtures.

The user identified [loop.png](../art/loop.png) and [loop2.png](../art/loop2.png) as the visual target for the whole environment and rejected the v1 art quality. The revised pass keeps a fairly distant isometric camera, with blue bottle displays, warm practical lamps, upholstered stools, fuller foliage and a skyline behind lounge windows. The game retains its established encounter coordinates and routes. The earlier room-layout images supply spatial context; they do not supersede these visual references.

Actual Unity review views:

- [Revised overview](validation/nightclub-v2/overview.png)
- [Aggressive mood](validation/nightclub-v2/overview-aggressive.png)
- [Intimate mood](validation/nightclub-v2/overview-intimate.png)
- [Entrance](validation/nightclub-v2/entrance.png)
- [Ren conversation](validation/nightclub-v2/conversation-ren.png)
- [Luca conversation](validation/nightclub-v2/conversation-luca.png)
- [Maya conversation](validation/nightclub-v2/conversation-maya.png)
- [Theo conversation](validation/nightclub-v2/conversation-theo.png)

These capture the existing placeholder cast in the dressed environment. They are not evidence of final character models, LOD switching or expressive animation.

## Rendering allocation

The imported environment, including the ceiling and four haze meshes, measures **121,850 triangles** in the recorded audit. It uses **54 renderers, 208 material slots and 21 lights**. Thirteen added point lights have short ranges and no shadows; they serve individual bar bays, lamps and plants. The existing per-object additional-light limit remains four. Material slots are a submission inventory, not a measured frame draw-call count. One 128px neutral baked reflection serves the room; no realtime reflection captures are added. Surface textures use mipmaps and ASTC 6×6 overrides on iOS, with a 2048px cap for the skyline.

Five 12k main characters, twelve crowd members at their 6k ceiling and a 6k affair-partner allowance bring the conservative total to **259,850 triangles**, including environment zones not all visible at once. The revised environment allowance is 125k. The 40k conversation character replaces the visible cast in the close camera; the room still renders. Physical iPhone 15 Plus performance, thermal behavior and memory acceptance remain to be measured.

The skyline is a panoramic skybox and supplies the view through lounge windows. Exterior glazing and skybox background are omitted from the cutaway overview so the city does not surround a floating room. Gameplay uses a 42-degree pitch, -38-degree yaw and 11.8 orthographic size. Conversations use perspective with the character's existing framing size, a lightweight Gaussian depth-of-field profile, dimmed LED artwork and a temporary unshadowed face key. Studio review cameras retain orthographic projection.

## Gameplay integration

The six low booth blockers come from `server/src/nightclub-dressing.json`, consumed by both the server's `club-plan-2` and the Unity builder. Low dividers do not block eye-height recognition. The recognition area, authored fall table, spawns in XZ, conversation gates and prevention rules are unchanged.

`ClubEnvironmentPresentation` sets display-only floor height for the stage, stage stairs and raised VIP platform. The coordinator continues to own XZ motion. Existing VIP furniture colliders are raised to match; stool colliders are aligned with the server's six stool positions. The saved offline gym and its navigation bake are separate from the authoritative encounter.

## Editable assets and reproduction

See [environment sources and provenance](../art/generated/environments/nightclub-v1/README.md) for Blender files, procedural authoring, the accepted Tripo chair, the rejected palm, the 60-credit generation record and the new generated skyline. Unity import and rebuild actions are under **Lucid Loop > Environment**. The build menu replaces the named environment set, bakes its neutral reflection and captures review images.

The [Unity geometry audit](validation/nightclub-v2/geometry-audit.json) and [Blender export report](../art/generated/environments/nightclub-v1/geometry-report.json) record separate counts: Unity can remove degenerate exported faces. The [surface audit](validation/nightclub-v2/surface-normals.json) records the repair of inward-facing rounded furniture. Render captures are review evidence, not device-performance measurements or a claim of visual acceptance.

[Current validation evidence](validation/nightclub-v2/README.md). Earlier v1 captures and test results remain in their original folder for comparison.
