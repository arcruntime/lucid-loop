# Ren designer eyes: frozen neutral for scratch review

Root selected this neutral construction for a bounded Unity material comparison after viewing the actual v6 front and quarter renders. **This is not designer 1:1 acceptance or a production character asset.** No blink, gaze, eye-wide, squint, expression or lipsync controls are provided.

The frozen reviewed source is [Ren_DesignerEyes_Pigment.blend](Ren_DesignerEyes_Pigment.blend), SHA256 `715617c4842029775dfdef0cd289648ea87883624fe693c61a5d374812a2b13d`. Its accepted non-eye context is `h-complete-head-v1/Ren_H_CompleteHead_Portable.blend`, SHA256 `317b8f7379dcf8d2fe34501385e1da7cd91b3d057008d488db0c65ed3727b8f5`. The accepted mouth, nose, cheeks, jaw, source head and existing v4 aperture/backing/iris/shutter geometry remain unchanged.

## Actual visual evidence

- [Source appearance and aperture comparison](source-eye-comparison.png).
- [Actual upper-ink, iris and crease contours over source pixels](layer-contour-comparison.png).
- [Whole-head front, quarter and profile](whole-head-view-plate.png).
- [Right profile with hair/cap hidden](profile-R-hair-hidden.png) and [left profile with hair/cap hidden](profile-L-hair-hidden.png).

The ink mask uses the actual rendered geometry. Crease extraction uses its own monochrome render, avoiding false color-classification flecks where red ink and blue iris meet. The separate manually traced visible curves remain review targets, not certified source segmentation. Uncertain distal hair junctions and hidden iris continuation are explicitly excluded from a numerical likeness claim. The main portrait camera is an eye-region anchor only: mouth/chin alignment remains about 18 original source pixels off, and no independent X/Y image warp is applied.

V6 separates the former broad temporal branches into thinner angled strokes and restores visible negative space within the upper ink. The R inner-corner ink tail now sits in front of the actual skin instead of disappearing behind it. Shallow side apertures remain visible in the hair-hidden profiles; no spherical globe or detached backing slab is apparent. Removing hair exposes pre-existing H extraction/scalp boundaries, which this eye task has not changed.

## Use these exports

Use **[neutral-export-v2](neutral-export-v2/contract.json)**. The sibling `neutral-export` is superseded because its initial glTF material graph did not export the final color binding correctly.

| Artifact | SHA256 |
| --- | --- |
| [Eye-only FBX](neutral-export-v2/Ren_DesignerEyes_Neutral.fbx) | `ee7fbae3066f0576328f32fd635583da0d419d8dcfd6033ed102d2695f6338fc` |
| [Eye-only GLB](neutral-export-v2/Ren_DesignerEyes_Neutral.glb) | `bba7f0844a022b4400b18f9854974e4fc69582b4df6cd5b3b13cae0f514b5af7` |
| [Eye-only Blender](neutral-export-v2/Ren_DesignerEyes_Neutral.blend) | `d58734be395ce5a5d5edf16d6a88eeca7beb1732a654f09389c712d170c010be` |

The package contains 22 eye meshes, 5,145 authoring triangles, an identity `Ren_DesignerEyes_NeutralRoot`, and four native-axis marker empties. It contains no face, body, rig or animation. Keep its neutral geometry frozen during this comparison. Use the accepted already-cut H head; hide the rejected `Ren_H_Eye_*` meshes. Material owners may apply the selected Tokon face/cap and closed-lip pigment diagnostic without moving accepted geometry.

All final pigments are encoded in active **`Ren_DesignerColor` / `COLOR_0`, linear RGB, alpha 1**. Consume that color once with a white material tint. The contract lists every mesh/material, color range, transform and source dependency. Skin shutters need the same skin shading response as the selected Tokon face; upper ink, lashes, crease and iris retain their distinct graphic roles. There is no corneal sphere or broad corneal specular layer. Blender source captures use unlit eye pigment; Unity lighting is a separate visual-review variable.

The FBX uses `colors_type=LINEAR`. Its fresh Blender import retained `FLOAT_COLOR` with zero color and UV error; maximum world-position error was `4.7707e-8`. Raw GLB `COLOR_0` uses normalized unsigned 16-bit components, with maximum stored linear color error `7.6430e-6`. Blender's GLB importer converts this to `BYTE_COLOR`, producing up to `0.00391525` linear / `0.00201911` sRGB difference. That extra import quantization is disclosed; float identity is not claimed. GLB world-position and UV errors were `7.5981e-8` and `2.9802e-8`. See [fresh import proof](neutral-export-v2/roundtrip-verification.json).

These checks establish export integrity under the recorded interpretation. They do not establish designer likeness, Unity shader behavior, final polygon/material budgets, or sustained 30 fps on iPhone 15 Plus. Blink/gaze engineering waits for neutral visual review.

No main Unity project, accepted source file, old rig or prior trial was modified by this package.
