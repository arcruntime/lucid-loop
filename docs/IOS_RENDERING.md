# iOS nightclub rendering and shader budget

Updated 2026-09-14. iOS is the target platform. The initial acceptance device is the project's iPhone 15 Plus target, landscape, sustained 30 fps. These are engineering budgets pending device measurements.

The club should look full of lights while using a small, stable set of lighting features. Adding another light of the same kind increases lighting work; it does not inherently add another independent shader keyword combination. Shadow modes, rendering paths, cookies, material features, and inconsistent pipeline assets cause the combination growth. Intimate and aggressive moods must share shaders and pipeline features; animate colors, intensities, emission values and music without enabling mood keywords.

## What is actually in the project

Reproduce this read-only audit without opening Unity:

```powershell
python tools/audit_ios_rendering.py
```

| Serialized baseline | Result |
|---|---|
| CharacterGym lights | 8: one directional, three point, four moving spot lights |
| LiveGym lights | 3: one directional, two point lights |
| Shadow requests | One directional light in each scene; additional lights have no shadows |
| Gym materials | 25, all reference URP Lit; 18 have no material keywords, seven use `_EMISSION` |
| Effective quality pipelines | Six quality levels currently resolve to GymPipeline: five inherit default, Ultra explicitly references it |
| Renderer | Forward, no renderer features |
| Global stripping | Strip Unused Variants already enabled; variant JSON export already enabled; unused post-processing stripping remains disabled |

These are serialized counts, not measured visible light overlap, draw calls, compiled variants, or GPU timing. The original pipeline already used four additional per-pixel lights per object, one 1024 main shadow cascade, no additional-light shadows, no soft-shadow support, HDR, 2x MSAA, and SRP Batcher. Scene lights request soft directional shadows, but the pipeline does not support them, so soft shadows are not the current rendered contract.

## Implemented policy

[IosRenderingPolicy.cs](../Unity/Assets/Gyms/Editor/IosRenderingPolicy.cs) makes the game pipeline configuration repeatable when [GymBuilder](../Unity/Assets/Gyms/Editor/GymBuilder.cs) rebuilds the gyms. [GymPipeline.asset](../Unity/Assets/Gyms/Generated/GymPipeline.asset) already contains the reduced flags.

| Feature | Game policy | Reason |
|---|---|---|
| Rendering path | Forward only | Avoid compiling/maintaining Forward+ and Deferred configurations for this small light budget |
| Additional lights | Per pixel, maximum four per object | Preserve current local color lighting, with bounded shading work |
| Additional shadows, soft shadows, light layers | Off | No local shadow maps or alternate shadow quality/layer features |
| Main shadow | One hard cascade, 1024 map, 40-unit distance | Existing baseline; tighten distance after both camera extremes are tested |
| Light cookies | Now off | No gym light uses a cookie |
| Mixed lighting | Now off | Current scene has no mixed bake; ordinary baked lightmaps/probes remain a future option |
| Terrain holes and LOD crossfade | Now off | No terrain/LOD crossfade content in this demo |
| Data-driven and screen-space lens flares | Now off | No corresponding authored effect |
| HDR, 2x MSAA, SRP Batcher | On | Preserve existing emissive bloom and edge quality while keeping CPU batching |
| Required camera depth/opaque texture | Off | Avoid forcing copies without a consuming effect |

`ValidateForIosBuild()` rejects an unexpected default/quality pipeline, additional renderer features, or a drift in the lean lighting flags before the game export. It does not silently alter a character review pipeline. `EnableBuildReporting()` enables all-shader logging and JSON export for the build.

URP's built-in stripping removes unused feature variants according to the included pipeline settings. Every included pipeline must agree: introducing a second quality pipeline can retain both sides of feature switches. A global custom keyword blacklist is deliberately absent because the character shaders have their own alpha-cutout, paint-region, lighting and instancing needs. See [Unity's stripping rules](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shader-stripping-features.html).

## Club art and lighting construction

Use emissive mesh strips, fixture surfaces and inexpensive beam geometry for most visible neon. Keep emission enabled on glow materials in both moods; change its value, including zero when necessary. Do not make every fixture a realtime Light component. Reuse shared materials and shader families; use opaque/cutout geometry where it produces the intended image. Large transparent beams can become a fill-rate problem even when shader counts are low.

Bake the static room's neutral base lighting and use baked probes for moving characters when the room is ready. Add a small realtime mood layer so changing the music does not leave incompatible colored baked illumination behind. Do not switch an entire lightmap set per mood without a measured memory and transition plan. Mixed-lighting/shadowmask would require an explicit revision of this policy.

Keep the current seven local lights as an audit baseline, not a mandate to add seven more. First device experiments should compare four versus seven visible local lights, and two versus four per-object lights. Split the floor/walls into sensible spatial meshes so one enormous mesh does not consume its entire per-object light selection in a distant part of the room. Test face readability and moving spotlight selection before accepting any limit reduction. URP Forward supports a bounded number of additional lights per object; our limit is lower than its maximum. See [Unity's light limits](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/light-limits-in-urp.html).

One main shadow is the initial ceiling. Avoid shadowed point lights and realtime reflection probes. Use blob/contact art if another grounding cue is needed. Preserve facial readability under both mood palettes and the player's green beacon. The art team owns character shader changes; confirm `_ALPHATEST_ON`, `_PAINT_REGIONS`, shadows, skinning and any required instancing combinations in the actual player build.

SRP Batcher is enabled, but shared shaders do not mean one draw call. Keep material constant buffers compatible, avoid gratuitous shader/keyword switching, and check custom character shader compatibility in Frame Debugger. MaterialPropertyBlock use can remove renderers from the SRP Batcher path, so choose the material-update strategy with that tradeoff in mind. See [Unity's SRP Batcher requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/SRPBatcher.html).

## Verification and next measurements

The static audit and direct C# compilation of the policy against the installed Unity 6000.3/URP assemblies have passed without opening the shared Editor. No iOS shader build, device capture, or visual acceptance has been completed for this policy. Reducing flags is an actual configuration change; a percentage reduction in compiled variants or milliseconds saved is not yet established.

1. Export a development iOS build with strict shader variant matching enabled for diagnosis. Preserve `Editor.log`, `Temp/shader-stripping.json`, and `Temp/compute-shader-stripping.json`; compare the same scenes, materials, platform, Unity version and build options before/after. Read the retained/total variants per shader and pass, especially Lit and the character shaders. [Unity documents these counters and exports](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shader-stripping-check.html).
2. Exercise both moods, every character/material, close and wide cameras, microphone UI, death and rewind effects. Check missing-variant logs and pink/error rendering; warm the actual transitions before judging first-use stutter. Add only observed missing runtime combinations to a focused variant collection when needed.
3. Capture a sustained ten-minute run on iPhone 15 Plus. Target the 33.3 ms frame budget; record CPU/GPU frame times, draw calls, SetPass calls, shadow passes, texture/render-target memory and thermal behavior. Compare the worst wide shot with the closest character view. Do not infer a pass from desktop Editor FPS.
4. Profile bloom, transparent beams, shadow distance, render scale and local-light overlap individually. Prefer fewer local lights, reduced overlap, a tighter shadow distance, and reduced bloom resolution before introducing another rendering path or material feature family.

Post-processing variant stripping stays off until the volume inventory and runtime effect creation are settled. The builder already serializes bloom and tonemapping into a profile, but death/rewind effects are still being authored. Once their profiles are represented in project assets, review enabling that additional stripping option and rerun the complete transition test.
