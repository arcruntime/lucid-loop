# Character NPR requirements for the iOS renderer

Source audit: 2026-09-14. Target remains Unity 6.3 LTS, iPhone 15 Plus,
landscape 16:9 and sustained 30 fps. This records current character dependencies;
it is not an iOS shader compilation or performance result.

`Unity/Assets/CharacterArt/NPR/PaintedAnimeNPR.shader` and its adjacent HLSL
implement the character shader. Preserve these sources while engineering selects
the shipping renderer and strips unused variants. The existing desktop shader
study uses the older P2 head; the H assembly still needs actual Unity review.

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

The shader declares main/cascade/screen shadows, vertex/per-pixel additional
lights, cluster lighting, additional shadows, soft-shadow quality, light layers,
fog and instancing alternatives. Its local features are `_ALPHATEST_ON` and
`_PAINT_REGIONS`. Depth-normal and shadow-caster passes add their own variants.
Declaration does not establish that every combination is used.

Retain the variants implied by the selected renderer, included quality assets
and actual H materials. Do not strip a mode based only on this earlier P2 study.
The `_MaxAdditionalLights` uniform limits accumulated contributions to 0–8 in
the fragment shader; it does not cap scene culling or cluster construction cost,
and the light loop still visits candidate lights.

Engineering owns the global iOS renderer/quality/stripping configuration.
CharacterArt owns these shader sources, material requirements and face review.
After integration, capture a head turn under fixed light, moving nightclub
lights, closed/open mouth, intermediate blink and cap on/off. A successful
Windows capture cannot prove Metal compilation, mobile memory use or sustained
iPhone performance. Dense H construction geometry remains over the final
approximately 40k rendered-triangle budget for the complete dressed character.
