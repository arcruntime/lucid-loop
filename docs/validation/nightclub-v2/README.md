# Reference revision — September 14, 2026

The visual targets are `art/loop.png` and `art/loop2.png`. The user rejected v1 and asked to retain a fairly distant gameplay view while adding readable lights, stools, bottles, plants and a skyline. This folder contains the revised Unity renders, not generated illustrations of the result.

The set has upholstered stools with brass footrests, 81 shelf bottles plus nine counter bottles, three counter candles, eight fuller palms, a Monarch sign and a narrower service counter. Thirteen short-range unshadowed point lights illuminate shelf bays, tables and foliage. A new panoramic skybox supplies the lounge window view. The cutaway overview omits the exterior window panes and skybox background. Conversations use perspective; the studio camera path remains orthographic.

- `overview.png`, the two mood overviews, `entrance.png` and four conversation images: actual 1920×1080 scene captures.
- `geometry-audit.json`: 121,850 environment triangles, 54 renderers, 208 material slots, 21 total lights; no missing meshes, materials or scripts. Conservative populated allocation: 259,850 triangles.
- `surface-normals.json`: negative-to-positive signed-volume repairs for the closed rounded bar, stools and booth surfaces. The source generator now emits outward-facing shells.
- `installed-assets.json`: before/after hashes for 82 environment files installed from the isolated review. Installation verified that the source encounter had not changed while the review was running.
- `review-copy-manifest.json`: final source dependency hashes, including rendering assets and the shared dressing collision plan.
- `editmode.xml`: 86/86 Gyms EditMode tests passed.
- `playmode.xml`: 2/2 live tests passed: conversation approach eligibility and the opening movement, catastrophe, music silence, held fall and retained reset. These used the real local relay on port 8796 without paid voice.
- `smoke/`: ready, catastrophe and reset scene-camera captures from the successful live run. HUD text is asserted separately; the batch camera capture excludes the screen-overlay HUD.

The scene still contains placeholder characters. These checks establish import and interaction behavior, not visual approval against the references. Physical iPhone 15 Plus frame time, memory and sustained thermal behavior remain unmeasured, especially for the added practical lights and conversation post-processing.

## Publication scope

The environment-only publication preserves baseline character code and applies only the perspective change in `GymCamera`; unrelated per-character framing controls and character assets remain outside this commit. `publication-copy-manifest.json` records the exact isolated publication dependencies. The original capture and validation manifests above remain preserved as the v2 art-review evidence.

Publication verification passed 86/86 Gyms EditMode checks (`publication-editmode.xml`) against those baseline character dependencies, plus 11/11 server world tests. This fresh import compiled the isolated publication code successfully.
