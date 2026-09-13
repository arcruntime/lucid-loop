# Ren Tokon shader study — implementation checkpoint

New project-owned shader: `Unity/Assets/CharacterArt/NPR/TokonStudy/RenTokonNPR.shader`, shared `RenTokonNPR.hlsl`, and separate `RenTokonInk.shader`. Historical PaintedAnimeNPR is unchanged. The forward/depth/depth-normal/shadow passes share one material CBUFFER. All four NPR passes and the separate Ink pass compiled without ShaderUtil messages in the isolated Unity project. Actual GPU comparisons are retained under unity-review/ and cap-controls-v1/unity-review/. SRP batching remains unverified: the Editor query returned `Not initialized ()`; iPhone performance is also unverified.

`ren-tokon-presets.json` contains provisional **material categories**, not automatic source-name assignments. Preserve each source material's base texture, UV transform, linear tint, and pigment vertex-color decoding. Skin has no global face-normal replacement: a dedicated face-region map is required before enabling it on shared head/neck/ear skin.

| Input | Encoding and meaning | Current status |
|---|---|---|
| Base / closed base | sRGB complete albedo | Original H and constrained closed-lip bake exist |
| Shadow / closed shadow | sRGB complete shadow albedo; direct interpolation, not base multiplication | Derived warm skin, pale-blond hair and cap shadow maps now in maps/; provisional palette decisions, not hand-painted |
| Control RGBA | Linear: face stabilization, spec permission, outline suppression, skin | Native-head spatial/source-ID controls now baked; hair remains constant semantic control; cap-controls-v1 adds spatial seam/brim G placement |
| Pigment vertex colors | Existing linear/sRGB flag retained | Never repurposed as controls |
| Face frame | World-space forward/right/up from animated character | Existing binder-compatible fields |

Closed endpoint weight is supplied by the controller; the shader does not infer mouth pose. Use the exact formula and unsupported-pose guard in `../closed-lip-corrective-v1/atlas-blend-contract.json`. When enabling closed weight with a shadow map, provide the matching closed shadow endpoint too.

The outline is a separate shell material, intended for an independently smoothed, shape-key-compatible shell. No shell has been built by this material task. Control UV transformation must match the control atlas, independent of source pigment UV transforms. The outline has no shadow-caster pass.

Additional lights use URP's unshadowed lookup, first-N budget, and a capped combined color lift. The main key retains actual shadow attenuation. First-N order can change; this is not a stable nightclub light-priority system. Per-pixel additional lighting is required for this study; vertex-light mode is not the acceptance configuration.

Reference implementation inspected read-only: hackathon `CharacterToon.shader`, `InkOutline.shader`, and `docs/ART_DIRECTION.md`. No Steph-specific orange recoloring or source mesh axis was carried over. The outline math follows the local reference's reference-932px-scaled capped inverted hull approach. Actual Ren silhouette and painted shadow artistry still require review.

## Map bake evidence

maps/map-contract.json binds all three head slots to exact existing HNativeUV, with 4K open/closed main-head shadow endpoints, 2K main controls and 512px independent cheek-chart maps. Source-ID lists describe the face-lighting region; the smooth native-frame taper excludes neck and rear/lateral skin. This is a broad lighting mask, not an artist-authored face SDF. Skin G=0 and A=1; iris/liner color attributes are untouched.

maps/accessory-map-contract.json binds the existing hair/cap base texture hashes and UV layers to 2K derived shadow atlases. Their small constant control textures encode material-semantic spec permission and outline suppression; no spatially painted hair highlight mask is claimed. Existing hair pigment streaks remain visible in the derived shadow map and need separate art review.

Nine mapped front/quarter/profile images show controls and both shadow endpoints on the actual head. Black in the control view means no face stabilization/outline suppression, not missing skin classification: RGB control maps sample A=1. Hair/cap RGBA controls explicitly store A=0. Maps remain provisional art inputs. Root selected the refined face controls and placed cap highlights after actual Unity comparisons; this is not final character/style acceptance.

## Tested checkpoint and selected inputs

Main shader/include now exactly mirror the tested-source snapshots:

- RenTokonNPR.shader SHA256 `32354c6a49ef1ea14d6562445cb5e7339a4cccd548b7c570b759353acbf919a6`.
- RenTokonNPR.hlsl SHA256 `d4c43b883d965e036c56157ee798bcfd714310bdc8c736859361e32d5bd3fa1a`.

The tested fixes apply the same controlled facial response to additional lights and remove the unused vertex-light variant; the study builder requires per-pixel additional lighting. No tuning was added during the mirror.

The provisional selected face input is maps-face-v2 with FaceMode 0.90 on mapped head and diagnostic lid materials. The controlled GPU comparison removed jagged lower-lip/chin shadow bands while retaining neck shading; all three unlit images were pixel-identical. Facial lighting is now comparatively flat, and the normal-right term still contributes to the broad response. This is neither an SDF nor final authored facial shading. Do not raise strength further without a new controlled experiment.

The provisional cap input is cap-controls-v1. Seven matching pose/light pairs show the large rounded panel highlight removed while base color and seams remain intact; the unlit pair is pixel-identical. A broad brim/rim band remains visible during head turns. Only G permission changed; other controls and palette were preserved.

Primary shader comparisons retain exact original H base texture SHA9750e636..., with closed lip corrective disabled. The separately labeled closed-paint diagnostic demonstrates the thin rose contour; it must not be confused with a shader-only improvement. Paired closed shadow/base endpoints and unsupported-pose guards remain required.

Existing eye geometry is rejected historical context. These shader/material results do not approve the eye design, facial likeness, complete character, hull outlines, phone LOD, or sustained device performance. Deliberate facial accents should next be assessed against the redesigned eye/material assembly.
