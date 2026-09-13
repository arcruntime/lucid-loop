# Cap highlight-placement comparison

The only material input change is control-map G: the previous constant 31/255 permission is restricted to existing crown seam neighborhoods and a narrow outer-brim strip. R=0, B=77/255 and A=0 remain exact at every pixel; maximum highlight permission is unchanged. The mask permits highlights on 5.09% of the atlas. Base/shadow palettes, geometry, face controls and shader strengths remain frozen.

Placement follows the designer's matte cap with fine seam/edge accents and the existing cap construction's six crown UV columns. It is procedural control authoring, not a repainted albedo or final artist highlight map. `cap-control-contract.json` records source hashes and UV bounds.

Import as linear data with `alphaIsTransparency=false`. A=0 is the skin-classification flag, not transparency. Blender diagnostic images explicitly use `alpha_mode=NONE` to inspect RGB data; otherwise Blender premultiplies RGB by zero alpha and misleadingly displays a black mask. The source PNG bytes are unchanged by that diagnostic setting.

`mapped-cap-G-front.png` and `mapped-cap-G-quarter.png` display G normalized to white for visibility. They show permission regions, not emitted white lines in the actual material. Actual matched Unity captures determine whether the large circular/block highlight is removed without losing cap legibility.
