# Arkane Studios art style (wrong target)

**Superseded.** The Lucid Loop FigJam note is *Arcane* (Riot / Fortiche / Netflix), not Arkane Studios. Current report: [arcane-fortiche-art-style.md](arcane-fortiche-art-style.md). This file is the earlier Dishonored / Deathloop survey, kept for reference.

**Date:** 2026-09-12
**Question:** Has anyone published a Blender / Unity / Unreal / academic reimplementation of Arkane Studios' painterly, NPR-adjacent look (Dishonored, Dishonored 2, Deathloop), suitable as a base for recreating it in Unity?
**Verdict:** No dedicated NPR shader study of that look exists. The look is stylized realism: painted, frequency-controlled materials plus illustration-grade lighting on a conventional PBR-ish renderer. Closest published work is first-party material-pipeline talks, a 2026 Void Engine frame analysis, and Dishonored-*inspired* environment recreations.

---

## 1. Direct answer

There is not a published reverse-engineering of "the Arkane lighting model" the way people reverse-engineer *Wind Waker* toon, *Okami* ink, or *Borderlands* rims.

What exists, and is more useful for Unity:

1. **First-party art direction** that says the look lives in painted albedos, reduced high-frequency noise, exaggerated shapes, and unlit-first composition — with photoreal-ish light on top.
2. **First-party renderer talks** that show Void Engine is Forward+ (Dishonored 2) then deferred + ray tracing (Deathloop), using standard PBR maps, not a cel BRDF.
3. **Third-party recreations** of *effects* (Crack in the Slab portal, Dark Vision-like X-ray), *levels*, and *generic oil-paint post* (Kuwahara, Voronoi brushstrokes). Those last ones will not get you Dunwall or Blackreef.

A Kuwahara or toon shader on a photoreal scene produces "oil paint post," not Arkane.

---

## 2. What the style actually is

Arkane Lyon (Dishonored 1/2, Deathloop) is **stylized realism**, not cartoon NPR.

Sébastien Mitton, from the making-of:

> Remove all the noise of what they call the "hi-res, next-gen" texture and view it as an illustration or painting… That forced us to produce textures that were less hi-res than other games, but the gain was the legibility and the cool impressionist aspect of it.
>
> — [GamesRadar / Edge, *The making of Dishonored*](https://www.gamesradar.com/making-dishonored/)

Harvey Smith said Void Engine existed because they needed **a painterly style and yet photorealistic lighting**, plus custom shaders for that mix ([IGN, 2016](https://www.youtube.com/watch?v=aWe2UicOosA)). Mitton later: **realism, not photorealism** ([Mashable](https://mashable.com/article/dishonored-2-arkane-artwork-sebastien-mitton)). He also insisted scenes should already read in **unlit** mode; post should not be used to hide mismatched assets ([GamesRadar](https://www.gamesradar.com/making-dishonored/)).

Yannick Gombart (Arkane environment artist): they **embedded a painterly look in the art** to kill noise, aimed at **18th-century painting** and a **Ghibli-like painting sensation**, shared brushes, then in Dishonored 2 moved the *shaders* toward PBR without abandoning the painted albedo ([80.lv, 2017](https://80.lv/articles/asset-material-production-in-dishonored-2)). Mitton's shape brief is the French "fou-fou" — crazy, unprecedented silhouettes. Geoffrey Rosin: Mitton wanted it to **feel like a painting**, treated as **frequency control** (less high-frequency "dots," more masses) ([80.lv, 2017](https://80.lv/articles/environment-storytelling-in-dishonored-2)).

Named painter / illustrator references: Canaletto, Gustave Doré, John Atkinson Grimshaw, Jean-Léon Gérôme, Norman Rockwell, Dean Cornwell, Jacek Malczewski, Carl Spitzweg, plus 1920s–40s pulp and "mystical photography" ([PCGamesN](https://www.pcgamesn.com/dishonored/dishonored-began-life-game-set-medieval-japan), [The Art of Dishonored](https://www.youtube.com/watch?v=qtLfj6lQeJE), [Austin Germer](https://austingermer.artstation.com/projects/nQJyxK)).

Deathloop keeps the material discipline and shifts the graphic language: Saul Bass, Robert McGinnis, 1960s Bond, Frank Lloyd Wright interiors — not Victorian oil paint ([WIRED](https://www.wired.com/story/deathloop-game-art-style/), [Adobe / JB Ferder](https://www.adobe.com/products/substance3d/magazine/deathloops-award-winning-art-pipeline-with-substance.html)).

### Layer breakdown

| Layer | What they do | What they do not do |
| --- | --- | --- |
| Albedo | Hand-painted / brush-shared; medium frequencies; masses like a painting | Photo-sourced high-frequency noise |
| Shape | Exaggerated, "fou-fou" silhouettes; caricatured faces (clay sculptures) | Generic realistic props |
| Lighting | Real lights, probes, SSS, fog, volumetrics; image already reads unlit | Cel bands; ink outlines as the style |
| Post | LUTs, bloom, TAA, grading, sharpen (Deathloop) | Oil-paint / Kuwahara as the look |
| Outlines | Interactive pickups only (Dishonored 2) | Contour NPR |

### Engine timeline

| Game | Engine | Shading notes |
| --- | --- | --- |
| Dishonored (2012) | Unreal Engine 3 | Painterly textures, lower texel density, illustration read ([Digital Foundry](https://www.digitalfoundry.net/articles/digitalfoundry-dishonored-face-off)) |
| Dishonored 2 (2016) | Void Engine (id Tech 5/6 heavily rewritten) | Forward+, PBR maps, SSS, HBAO+, TAA, 3D LUT. Painterly albedo kept ([Rodriguez 2026](https://blog.simonrodriguez.fr/articles/2026/05/a_frame_analysis_of_dishonored_2.html)) |
| Deathloop (2021) | Void Engine 1.5, DX12 / PS5 | Deferred, RT, FSR2, decal GBuffer, sharper TAA because D2 was "too soft" ([GDC 2022](https://youtube.com/watch?v=DgdCznuNDKY)) |

Custom shaders they talk about in public are SSS, hair, glass, fog, water, portals — not a toon BRDF.

---

## 3. First-party technical sources

These are the closest things to a study. They describe **pipeline and renderer**, not an NPR model.

### Art / materials (this is the look)

- [Yannick Gombart — Asset & Material Production in Dishonored 2](https://80.lv/articles/asset-material-production-in-dishonored-2) (2017). Painterly albedos, shared brushes, D1 tool for a painted starting base, D2 PBR without betraying the tone. Medium-size details over micro-noise. Gérôme copied for the Golden Cat hammam in D1.
- [Geoffrey Rosin — Environment Storytelling in Dishonored 2](https://80.lv/articles/environment-storytelling-in-dishonored-2) (2017). "Feel like a painting." Frequency vs noise. Time goes into albedo and gloss because those read in ambient. Small details as flat color + matching gloss, not extra maps.
- [Mihaela Dragan — Crafting the Characters of Dishonored 2](https://80.lv/articles/crafting-the-characters-of-dishonored-2-a-detailed-guide-and-strategy) (2024). "Stylized realism." Skin/clothes painted like a painter. Shared palettes. In-house dx11 preview shaders in Maya. Brightness/dirt gradient toward the ground.
- [Austin Germer, Lead Environment Artist, Dishonored](https://austingermer.artstation.com/projects/nQJyxK). Worked with the art director and tech artists "to achieve the painterly look." Painters: Gérôme, Delort, Cortès.
- [GDC 2021 — Texturing pipeline for the characters of Deathloop](https://www.youtube.com/watch?v=f8w88FrB6C4) and [Adobe writeup by JB Ferder](https://www.adobe.com/products/substance3d/magazine/deathloops-award-winning-art-pipeline-with-substance.html). Designer master materials: CORE → ALTER → WEAR (Polish / Blemish / Tarnish). Painter stack: BASECOLOR → DETAIL (hand-painted albedo) → TEXTURE overlay → PAINT → DIRT → FINALISER (blend a little world-space normal / AO into albedo for volume). Limited curated palettes. NPC paint as a carnival layer.

### Renderer (not the look, but what the GPU does)

- [Simon Rodriguez — *A frame analysis of Dishonored 2*](https://blog.simonrodriguez.fr/articles/2026/05/a_frame_analysis_of_dishonored_2.html) (2026-05-13). Best reverse-engineering of Void on D2. DX11 Forward+ / tiled lighting. Maps: diffuse, normal, gloss, metalness, baked AO. SH irradiance probes, cubemap specular, SSS, HBAO+, cascaded sun shadows, volumetric fog, TAA, distortion, histogram auto-exposure, 3D LUT grading. Outlines are a gameplay pass for pickups, not art-style contours.
- [GDC 2022 — Gilles Marion / Lou Kramer, *A Guided Tour of Blackreef*](https://youtube.com/watch?v=DgdCznuNDKY), [slides (PDF)](https://gpuopen.com/download/GDC_A_Guided_Tour_Of_Blackreef.pdf). Deathloop Void: deferred, DX12, ray tracing, FSR2, AMD work. Decals into their own GBuffer. Sharpening added because Dishonored 2's image was often too soft.

### Art-direction interviews (style, not shaders)

- [GamesRadar / Edge — The making of Dishonored](https://www.gamesradar.com/making-dishonored/)
- [IGN — Harvey Smith on Void Engine](https://www.youtube.com/watch?v=aWe2UicOosA)
- [Mashable — Mitton on realism vs photorealism](https://mashable.com/article/dishonored-2-arkane-artwork-sebastien-mitton)
- [VG247 — Mitton on SSS / hair / not photoreal](https://www.vg247.com/dishonored-2s-art-director-says-theres-nothing-he-would-change-about-the-extraordinarily-beautiful-stealth-sandbox)
- [Polygon — Mitton on Dishonored concept pieces](https://www.polygon.com/2012/10/17/3515608/arkanes-art-director-talks-about-the-art-of-dishonored)
- [WIRED — Deathloop island art](https://www.wired.com/story/deathloop-game-art-style/)
- [GameBanshee / Mitton on Arkane style](https://www.gamebanshee.com/news/123855-the-art-of-arkane-studios.html) — dense exploration, foreground detail vs stylized background silhouettes, stick to the style.

---

## 4. Third-party reimplementations

Searched: Blender, Unity, Unreal, GitHub, 80.lv, GDC, Shadertoy-adjacent NPR repos, academic-style NPR labs. No paper or project claims "this is the Dishonored lighting model."

### Shader / VFX (Unity)

| Project | What it is | Useful for Lucid Loop? |
| --- | --- | --- |
| [Broxxar / Dan Moran — Crack in the Slab](https://github.com/Broxxar/ACrackInTheSlab) ([video](https://www.youtube.com/watch?v=dBsmaSJhUsc), 2017) | Dual-universe portal: additive cameras, perspective remap, depth hacks | Yes as a VFX case study; not the look |
| [Broxxar — stealth X-ray](https://www.youtube.com/watch?v=OJkGGuudm38) | Dark Vision–like highlight through walls | Gameplay shader |
| [AnirudhMukherjee — time shard](https://github.com/AnirudhMukherjee/dishonored-time-shard) | Glass shard showing another time | One effect |

### Environment / material recreations (closest to the look)

| Project | What it is |
| --- | --- |
| [Calvin Simpson — Dishonored-inspired UE4 env + shaders](https://80.lv/articles/dishonored-environment-art-shaders) (2018) | Fan pipeline: Substance tileables, slight slope-blur for painted look, curvature into albedo, flattened plaster normals, moss/paint master shaders, detail-normal distance fade. **Closest practical recreation of the material language.** Unreal, not Unity. |
| [Matthew Hickey — Strange City](https://80.lv/articles/creating-dishonored-inspired-cinematic-urban-scene-with-blender) (2024) | Blender / Substance / Unreal urban scene, Dishonored-*inspired* architecture and dirt. Art direction, not shading research. |
| [Sara Molina — D1 scene in D2 style, UE5](https://80.lv/articles/bringing-dishonored-environment-into-dishonored-2-s-world-with-unreal-engine-5) | Modular rebuild. Same. |
| Fan character sculpts in Blender / Substance / Cycles | Confirm the look lives in maps and caricature, not a special Eevee NPR tree. |

Simpson's albedo process is worth stealing: height + grunge → gradient map (subtle, desaturated) → curvature overlay (still "physical") → slight AO for depth → tiny normal-derived directionality → dirt/moss → **slight slope blur for the painted look** → sharpen. That slope-blur-then-sharpen pass is a procedural stand-in for the shared-brush painting Gombart describes.

### Generic painterly NPR (wrong target, useful as contrast)

These produce oil-paint / watercolor / hatch. They are not how Arkane shaded.

- [tantaneity — painterly shader in Unity URP](https://jettelly.com/blog/recreating-a-painterly-shader-in-unity-urp/) (2026): fullscreen Voronoi patches in world space, stretched like brushstrokes, optional cel. Port of a Blender shader. No Arkane claim.
- Kuwahara family: [Three.js painterly (Kuwahara + outlines)](https://www.youtube.com/watch?v=CmIfhwSyswk), Godot kuwahara, ReShade Oilify (anisotropic Kuwahara), Unity Kuwahara URP packs, Minecraft PaintBound, Fab "Akuwahara."
- [Blender Studio — painterly shadows](https://studio.blender.org/blog/painterly-shadows/) (Overgrown): brushstroke silhouette into the shadow. Film pipeline, not games.
- NPR repos: [candycat1992/NPR_Lab](https://github.com/candycat1992/NPR_Lab) (Gooch, cel, hatch), [lalunru/npr-shaders](https://github.com/lalunru/npr-shaders), Silent's Cel Shading, Arktoon. Different family.

### Not shading studies

- [Psyop Dishonored prequels](https://motionographer.com/2012/10/15/process-psyops-prequels-for-dishonored/): hand-wrought illustration, intentionally a departure from in-game look.
- Dishonored UPK toolkits, VR ports, ReShade "graphical upgrade" addons: asset extraction or IQ, not style reverse-engineering.
- Gameplay recreations (Blink, powers in UE5): out of scope.

---

## 5. Unity implications for Lucid Loop

Treat this as **stylized PBR**, not NPR. The FigJam board's "Arcane-inspired" character sheets plus a stylized 3D nightclub fit that: caricatured faces, painted materials, readable silhouettes, real lighting.

### Materials (~80% of the look)

- Paint albedo. Kill photo noise. Masses, not pores.
- Shared brush set, or a mild slope-blur on tileables.
- Overlay a little curvature / AO into albedo for illustration volume (Deathloop FINALISER; Simpson does the same).
- Small details as flat color + roughness, not extra normal noise (Rosin).
- Flatten or soften normals on painted surfaces so lighting does not read as photogrammetry.
- Curated palettes; dirt/value gradient toward the floor (Dragan).

### Composition (Mitton)

- If it is ugly with lights off, LUTs will not save it.
- Do not use a brown wash to hide mismatched assets.

### Lighting

- Real lights. HDRP Lit (or URP Lit with a slightly wrapped diffuse) is closer than a toon ramp.
- SSS on skin.
- Fog and shafts do a lot of the "mystical photography" Antonov described.
- Optional lighting cheat that still reads illustration: tint shadows with a palette color instead of pure multiply-black. That is not how Void lights, but it is a cheap Unity stand-in for painted shadow masses.

### Shape language

Non-negotiable. Crazy silhouettes, caricatured heads. A perfect shader on generic furniture will not look like Arkane.

### Post

- Volume LUT / controlled bloom.
- TAA + sharpen (Deathloop added sharpen because D2 was too soft).
- Optional **very** light anisotropic Kuwahara only as a finishing oil sheen — not the foundation.
- Interactive-object outline as a gameplay pass only.

### One custom Shader Graph, if needed

1. Flatten normals (amount by material).
2. Blend a painted detail map; fade it with camera distance.
3. Shadow tint from a palette color.
4. Vertex-paint paint-stripping on plaster / walls (Simpson's master plaster).

HDRP is the closer lighting feature set (SSS, volumes, LUTs). URP can do the material side; lighting will need more manual work.

### Three looks, not one shader

| Target | Material | Light / grade |
| --- | --- | --- |
| D1 Dunwall | Lower texel density, more obvious brush, cooler metal + gold/orange decay | Unlit-first, impressionist |
| D2 Karnaca | Same painting, higher texel density, PBR roughness/metal, warmer south | Forward+ PBR, SSS, fog |
| Deathloop | Graphic 60s, limited bold palette, hand-painted character paint layer | Deferred, sharper, more specular contrast |

Lucid Loop's nightclub is closer to Deathloop's graphic/party layer than to Dunwall plague brick, but character sheets still want the Dishonored caricature + painted skin.

---

## 6. Sources

### First-party / interviews

1. Edge Staff. "The making of… Dishonored." *GamesRadar+*, 2015. https://www.gamesradar.com/making-dishonored/
2. IGN. "How Dishonored 2 Improves on the Original Game." YouTube, 2016. https://www.youtube.com/watch?v=aWe2UicOosA
3. Bogle, Ariel. "'Dishonored 2' is gaming's best argument yet against photorealism." *Mashable*, 2016. https://mashable.com/article/dishonored-2-arkane-artwork-sebastien-mitton
4. Lien, Tracey. "Arkane's art director talks about the art of Dishonored." *Polygon*, 2012. https://www.polygon.com/2012/10/17/3515608/arkanes-art-director-talks-about-the-art-of-dishonored
5. "Dishonored: Creating Steampunk Beauty." YouTube, 2012. https://www.youtube.com/watch?v=xHKn58eqlx4
6. "The Art of Dishonored." YouTube, 2012. https://www.youtube.com/watch?v=qtLfj6lQeJE
7. Gombart, Yannick. "Asset & Material Production in Dishonored 2." *80.lv*, 2017. https://80.lv/articles/asset-material-production-in-dishonored-2
8. Rosin, Geoffrey. "Environment Storytelling in Dishonored 2." *80.lv*, 2017. https://80.lv/articles/environment-storytelling-in-dishonored-2
9. Dragan, Mihaela. "Crafting the Characters of Dishonored 2." *80.lv*, 2024. https://80.lv/articles/crafting-the-characters-of-dishonored-2-a-detailed-guide-and-strategy
10. Germer, Austin. "Dishonored." ArtStation. https://austingermer.artstation.com/projects/nQJyxK
11. Ferder, JB / Arkane Lyon. "DEATHLOOP's Award-winning Art Pipeline with Substance." Adobe, 2021. https://www.adobe.com/products/substance3d/magazine/deathloops-award-winning-art-pipeline-with-substance.html
12. Adobe Substance 3D. "Arkane Studios: Texturing pipeline for the characters of Deathloop (GDC 2021)." YouTube. https://www.youtube.com/watch?v=f8w88FrB6C4
13. GDC Vault. "Texturing Pipeline for the Characters of Deathloop (Presented by Adobe)." https://www.gdcvault.com/play/1027519/Texturing-Pipeline-for-the-Characters
14. Marion, Gilles and Kramer, Lou. "A Guided Tour of Blackreef: Rendering Technologies in Deathloop." GDC 2022. Video: https://youtube.com/watch?v=DgdCznuNDKY — Slides: https://gpuopen.com/download/GDC_A_Guided_Tour_Of_Blackreef.pdf
15. WIRED. "The '60s-Inspired Island in Deathloop Is Worth a Closer Look." 2021. https://www.wired.com/story/deathloop-game-art-style/
16. VG247. "Dishonored 2's art director says there's 'nothing' he would change…" 2016. https://www.vg247.com/dishonored-2s-art-director-says-theres-nothing-he-would-change-about-the-extraordinarily-beautiful-stealth-sandbox
17. "How Dishonored's artists created an oppressive, Victorian, steampunk world from scratch." *GamesBeat*, 2012. https://gamesbeat.com/how-dishonoreds-artists-created-an-oppressive-world-from-scratch/
18. "Dishonored began life as a game set in Medieval Japan." *PCGamesN*, 2013. https://www.pcgamesn.com/dishonored/dishonored-began-life-game-set-medieval-japan
19. Mitton, Sébastien, quoted in "The Art of Arkane Studios." *GameBanshee*. https://www.gamebanshee.com/news/123855-the-art-of-arkane-studios.html

### Renderer / analysis

20. Rodriguez, Simon. "A frame analysis of Dishonored 2." 2026-05-13. https://blog.simonrodriguez.fr/articles/2026/05/a_frame_analysis_of_dishonored_2.html
21. Digital Foundry. "Face-Off: Dishonored." 2012. https://www.digitalfoundry.net/articles/digitalfoundry-dishonored-face-off

### Third-party recreations

22. Moran, Dan (Broxxar). *ACrackInTheSlab*. GitHub. https://github.com/Broxxar/ACrackInTheSlab — Video: https://www.youtube.com/watch?v=dBsmaSJhUsc
23. Moran, Dan. "Shaders Case Study - Stealth Games' XRay Vision." https://www.youtube.com/watch?v=OJkGGuudm38
24. Mukherjee, Anirudh. *dishonored-time-shard*. https://github.com/AnirudhMukherjee/dishonored-time-shard
25. Simpson, Calvin. "Dishonored: Environment Art & Shaders Analysis." *80.lv* / original WordPress, 2018. https://80.lv/articles/dishonored-environment-art-shaders
26. Hickey, Matthew. "How to Set Up Urban Scene with Unique Architecture Using Blender." *80.lv*, 2024. https://80.lv/articles/creating-dishonored-inspired-cinematic-urban-scene-with-blender
27. Molina, Sara. "Bringing Dishonored Environment Into Dishonored 2's World With Unreal Engine 5." *80.lv*. https://80.lv/articles/bringing-dishonored-environment-into-dishonored-2-s-world-with-unreal-engine-5

### Adjacent painterly NPR (not Arkane)

28. Vicente C. "Recreating a Painterly Shader in Unity URP." *Jettelly*, 2026. https://jettelly.com/blog/recreating-a-painterly-shader-in-unity-urp/
29. Cortiz Dev. "How to Create a Painterly Shader in Three.js (Kuwahara Filter)." https://www.youtube.com/watch?v=CmIfhwSyswk
30. Blender Studio. "Painterly Shadows." https://studio.blender.org/blog/painterly-shadows/
31. candycat1992. *NPR_Lab*. https://github.com/candycat1992/NPR_Lab

### Lucid Loop context

32. Lucid Loop FigJam board (exported 2026-09-12): character-sheet direction labeled Arcane-inspired; stylized full-3D nightclub. See [art/README.md](../art/README.md).

---

## 7. Search notes / gaps

Searches covered Arkane / Dishonored / Deathloop + painterly, NPR, shader, Blender, Unity, Unreal, GDC, Kuwahara, toon, reimplementation, Void Engine. GitHub searches for Dishonored painterly shaders returned VFX and toolkits, not a lighting model.

**Not found:** academic paper on Arkane shading; Blender NPR node-tree billed as Dishonored; Unity asset billed as Arkane/Dishonored look (as opposed to generic painterly).

**Not claimed here:** that Void's BRDF has zero stylization knobs. Artists mention in-house preview shaders and "custom shaders" for the unusual mix of paint + light. Rodriguez's capture still shows a standard-looking Forward+ PBR stack; any stylization is likely in maps, SSS profiles, gloss, and grading rather than a published toon ramp.

**Not verified in-engine for this repo:** no Unity project exists yet. This document is research only.
