# Nightclub environment

The user's `art/loop.png` and `art/loop2.png` govern the visual treatment across the set. This source folder now includes the v2 revision; v1 was rejected on visual quality. The authored room retains the encounter's 28 × 24 metre shell, stage, bar, recognition area and catastrophe table. Six booth blockers are shared through `server/src/nightclub-dressing.json`; the server plan remains `club-plan-2`. The narrower visual counter leaves a staff service aisle within the existing blocked bar footprint. Restroom and service doors are visual context.

`Nightclub_Dressed_Authored.blend` retains the final individual editable objects, including the lounge and ceiling pass. The delivered `Nightclub_Authored.blend` is the initial construction checkpoint; a fresh full build updates both authored files. `Nightclub_Runtime.blend` combines the final objects into spatial zones. `Unity/Assets/EnvironmentArt/Generated/Nightclub.fbx` and its shared materials are the runtime export.

Rebuild the exported room with Blender 5.2.1:

```powershell
& 'C:/Users/jetha/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe' --background --factory-startup --threads 4 --python tools/environment_art/build_nightclub.py
```

Then use **Lucid Loop > Environment > Build dressed nightclub** in the existing Unity project, outside Play Mode. The builder refuses unsaved edits to BeforeTheDrop and leaves other open scenes loaded. It replaces its own named set and disables the earlier room renderers. It aligns stool blockers with the server, raises VIP furniture colliders, adds the shared booth blockers and traversable VIP platforms, and grounds actors on the authored floor heights. Actor identities and encounter state remain server-owned. The original offline gym is separate.

The build menu also bakes the neutral room reflection and captures the result. These actions remain available separately. The reflection is a 128px baked cubemap, not a realtime probe. The ceiling and exterior window panes are cut away in the orthographic overview. A generated metropolitan panorama supplies the skybox and the city seen through windows at eye level. Solo conversations use perspective, dim the LED, hide haze, configure light Gaussian depth of field and reuse the existing directional key without its distant shadow map; camera completion restores the temporary lighting state. Captures are written to `docs/validation/nightclub-v2/`.

## Production provenance

- Architecture, fixtures, DJ equipment, banquettes, tables, bottles, glazing, authored palms and material textures: project-owned Blender/Python geometry and procedural surface patterns in `tools/environment_art/build_nightclub.py`.
- LED artwork: project-owned animated opaque vortex shader, `ClubVortex.shader`.
- Four subtle fixture haze meshes follow the existing moving lights; they add no light components. Small lounge glass panels use a shared transparent material.
- Lounge chair: Tripo H3.1 text-to-model job `b313e5bb-ab6c-4bfc-ab67-8cb0a19df422`, requested 1,800 faces. Original GLB, provider preview, prompt, download hashes and provenance remain in `provider/velvet-lounge-chair/`. Normalized and fitted to the existing seat footprints before export.
- Rejected palm: Tripo job `f9e1e408-3455-4e3b-923a-d67c35e93bff`, requested 2,500 faces. Its provider preview showed collapsed, intersecting foliage. Retained for provenance and excluded from runtime; replaced by clean authored fronds.
- Both provider jobs reported 30 credits, 60 total. The once-only job runner resolves `TRIPO_API_KEY` from BWS in memory. Rerunning `poll` resumes downloads without creating new tasks. Ambiguous submissions require reconciliation; no automatic retries of paid creation.
- V2 skyline: built-in imagegen output, generated September 14, copied into `Unity/Assets/EnvironmentArt/Generated/MetropolitanNightSky.png`. Original generation identifier `01a09ea5-a2ee-7c92-acf8-5f04d94dd8e4/exec-a8984af1-1c23-4c04-8500-21af3153701b`. Prompt requested a nighttime metropolitan panorama with amber office windows, restrained blue/magenta lights, a dark indigo sky and no foreground interior or people. No additional Tripo or Meshy jobs were used for v2.

Generated outputs remain subject to the applicable provider terms. No claim of an exclusive copyright in provider-generated output is made. The original reference images retain their existing project provenance.

## Budget and validation

Environment ceiling: 125k triangles; imported v2 is 121,850. Main cast: 12k each in gameplay, about 40k for the solo close-up. Dance crowd: at most 6k per person. Character LOD integration is separate work; the scene's current characters remain in place.

`geometry-report.json` records Blender export totals; `docs/validation/nightclub-v2/geometry-audit.json` records imported Unity totals. The rounded-furniture winding fix is incorporated into the full authoring script, with signed-volume evidence in `surface-normals.json`. `revise_reference.py`, `refine_bar_proportions.py` and `repair_surface_normals.py` record the bounded revision operations used on the delivered editable checkpoint; a fresh full build already incorporates their final design. `finalize_nightclub.py` is the older v1 recovery path. Actual device performance, texture residency and thermal acceptance require an iPhone 15 Plus capture. Desktop renders are visual evidence only.
