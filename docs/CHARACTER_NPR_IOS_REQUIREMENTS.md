# Character NPR requirements for the iOS renderer

Source audit: 2026-09-14. Target remains Unity 6.3 LTS, iPhone 15 Plus,
landscape 16:9 and sustained 30 fps. This records current character dependencies;
it is not an iOS shader compilation or performance result.

The current visual target is the designer's American-anime forms with the
Tōkon-style NPR treatment referenced by `B:\openai-hackathon-game`.
`Unity/Assets/CharacterArt/NPR/PaintedAnimeNPR.shader` and its adjacent HLSL
remain the historical shader used by existing diagnostic viewers. Preserve them
while comparisons are active; they are not the accepted shipping appearance.

The separate `Unity/Assets/CharacterArt/NPR/TokonStudy/` candidate supplies painted
base/shadow albedo, sharp light bands, masked highlights and an ink pass. Its
newest fixes and materials currently live in the viewer owner's isolated Unity
scratch project. Main-source presence does not prove those fixes are integrated.
Actual Windows H captures exist, but the first facial shadows and old eye
construction failed visual review. No new shipping character material is approved.

## Required behavior and current study configuration

- Per-pixel additional lights preserve local nightclub color response and the
  analytic face lighting field. The current pipeline has four additional lights
  per object; use bounded scene lights rather than many shadowed fixtures.
- The study uses a shadowed main light, one cascade, a 2048 shadow map, six-unit
  shadow distance and soft shadows. These are study settings, not measured iOS
  quality requirements; changes need matched face/hair captures.
- Additional-light shadows and light layers are disabled in the study pipeline.
  Character art currently has no requirement to enable them for the shipping
  renderer. Forward+ is a supported source path, not an acceptance requirement.
- Material numeric parameters drive tone, color, light strength and the unlit
  diagnostic mode. Current Ren NPR/H review controllers do not switch shader
  keywords at runtime for nightclub moods or facial animation.
- Keep the oral material double-sided (`_Cull=0`): current mouth coverage checks
  were explicitly performed without backface culling. Enabling culling requires
  new mouth-interior validation.

## Variant ownership

The historical PaintedAnimeNPR shader declares main/cascade/screen shadows, vertex/per-pixel additional
lights, cluster lighting, additional shadows, soft-shadow quality, light layers,
fog and instancing alternatives. Its local features are `_ALPHATEST_ON` and
`_PAINT_REGIONS`. Depth-normal and shadow-caster passes add their own variants.
Declaration does not establish that every combination is used.

Retain the variants implied by the selected renderer, included quality assets
and actual H materials. Do not strip a mode based only on this earlier P2 study.
The `_MaxAdditionalLights` uniform limits accumulated contributions to 0–8 in
the fragment shader; it does not cap scene culling or cluster construction cost,
and the light loop still visits candidate lights.

## Tōkon candidate integration requirements

- Keep additional lights per pixel. The scratch candidate removes its unused
  vertex-light path and checks the pipeline setting; do not infer compatibility
  with vertex lighting from the historical shader's variants.
- Use an explicit head-relative lighting frame. Main and additional lights must
  use the same stabilized facial response through head motion. The first study's
  lower-face mask coverage and response strength are under visual correction.
- Base and shadow maps are independent complete albedos. A closed-mouth texture
  corrective requires both closed base and closed shadow inputs and the same
  interpolation weight. It remains a separate diagnostic experiment.
- Import control maps as linear data, with R for face response, G for highlights,
  B for outline suppression and A for skin. Preserve individual atlas transforms;
  iris/support vertex pigmentation is not a substitute for those controls.
  The tested candidate currently samples all maps through `_BaseMap_ST`; its
  supplied maps share each material's atlas coordinates. Independently packed
  control/shadow atlases require separate transforms before use. Do not assign
  an unrelated control atlas to an existing pigment transform.
- Keep skin specular/rim disabled in this study. Hair and cap highlights require
  authored placement; a broad normal-derived cap highlight is not visually accepted.
- Additional lights currently provide unshadowed accents with a capped combined
  contribution. First-N evaluation is not stable light prioritization and does
  not bound scene light-culling cost. Actual nightclub fixtures still need profiling.
- The ink shader alone does not establish working outlines. A separate shell
  with suitable smoothed normals, facial/internal-surface suppression and cap
  toggle behavior is required; the first player had no such shell. Count its
  submitted triangles and draw calls in the final character budget.
- SRP Batcher behavior, Metal compilation and device performance remain to be
  verified on the integrated candidate. A shared material constant buffer and a
  successful Windows shader build are insufficient evidence by themselves.

Engineering owns the global iOS renderer/quality/stripping configuration.
CharacterArt owns these shader sources, material requirements and face review.
After integration, capture a head turn under fixed light, moving nightclub
lights, closed/open mouth, intermediate blink and cap on/off. A successful
Windows capture cannot prove Metal compilation, mobile memory use or sustained
iPhone performance. Dense H construction geometry remains over the final
approximately 40k rendered-triangle budget for the complete dressed character.
