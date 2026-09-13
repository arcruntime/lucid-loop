# Ren eye rebuild: designer contours govern construction

Status: the layered construction direction and visible curve data were reviewed and authorized as first modeling targets. A separate shallow-eye prototype now exists in [h-designer-eyes-v1](../art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/); it has **no artist-likeness acceptance**. The earlier H eye v1 and visual v2 remain rejected for style. Ren's accepted mouth, nose, cheeks and jaw remain fixed.

[Trial v4 source comparison](../art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v4/source-eye-comparison.png) and [whole-head views](../art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v4/whole-head-view-plate.png) document the first coherent shallow construction after actual surface-attachment repairs. Review found improved aperture/iris visibility and front balance, but insufficient layered ink and smoky pigment. Subsequent v5/v6 refinements address those layers. Root selected [v6's frozen neutral handoff](../art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v6/NEUTRAL_REVIEW_HANDOFF.md) for a bounded scratch Unity material comparison, with fresh eye-only export checks. This is not likeness acceptance; blink/gaze engineering remains deferred until neutral visual review.

The user's acceptance requirement is:

> ABIDE BY THE DESIGNER'S DRAWN SILHOUETTES. if you cannot achieve ren's smoky, aloof yet dripping with sex appeal eyes 1:1 with our character designer's sketches, you have FAILED.

This document does not claim that a match has been achieved. It changes the construction authority from a plausible eyeball to the actual drawn opening, ink, iris exposure and silhouette. Unity-chan supplies implementation evidence, not Ren's identity.

## Reference authority and current tracing

The authority is [the original Ren designer sheet](../art/characters/ren-model-sheet.png), 1536 × 1024 pixels. The generated head reference sheets are not substitutes for that drawing.

- [Original pixel reference plate](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/artist-eye-reference-plate.png).
- [Refined layered source contours](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/artist-layered-contours-refined-v1.png) and [source-pixel curve data](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/artist-layered-contours-refined-v1.json).
- [Enlarged main portrait, image-left](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/pixel-detail-main-left.png) and [image-right](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/pixel-detail-main-right.png), retaining original pixels and coordinate grids.

The refined visible curves were **approved as first modeling targets, not certified numerical 1:1 silhouettes**. They use manually digitized boundary landmarks and shape-preserving piecewise cubic interpolation. Separate endpoints retain pointed corners; fan tips remain explicit corners. This is not an automatic segmentation or a measured numerical 1:1 result. Subpixel curve coordinates do not create detail absent from the source raster. Actual rendered overlays may expose areas requiring trace correction.

The plate separates cyan colored-eye opening, green visible iris arc, amber upper ink exterior, pink fan outlines, salmon soft lower pigment and violet crease. Dashed paths mark hair/cap occlusion or uncertain ink/hair junctions and are excluded from any accepted visible mask. The old coarse cyan-path draft is retained as history and is not the silhouette authority.

| Drawing | What it establishes | What it does not establish |
| --- | --- | --- |
| Main portrait, image-left | Broad sloped upper ink, asymmetric opening, large partly covered iris, long tapered dark inner corner | Hidden temporal lash connections; a complete upper iris circle; frontal canthus tilt |
| Main portrait, image-right | Its own foreshortened opening, clear pale sclera wedge, visible iris crop, temporal ink branches and separated crease | A mirrored copy of the other eye; certainty where dark hair crosses a lash tip |
| NEUTRAL panel | Neutral expression's aperture and lid relationship | Subpixel certainty from an eye only roughly 25 source pixels wide |
| FOCUSED panel | Pitched-down, sharp narrow aperture that remains iris-dominant | Neutral eye shape; hidden superior lid/crease beneath the brim |

The main portrait includes head roll and yaw. Its image-plane eye slope cannot be copied directly into a frontal model as anatomical canthus tilt. Each panel retains its own camera and expression. The six emotions must not be averaged into a generic neutral. The available sheet does not give an unambiguous isolated fully closed-eye drawing or a clean isolated profile eye; those views need explicit review of the later construction rather than a false claim of traced equivalence.

## Why the rejected construction missed the drawing

The rejected construction began with an ellipsoid, an annular skin opening and a rotating iris. Even after upper-lid compression, the geometry continued to determine a regular almond-shaped aperture. Thin boundary strips, a separate subtle crease and small lash triangles decorated that opening. The resulting rounded backing, iris presentation and uniform lid logic remained visible at quarter view.

The designer's eye is organized differently. A substantial smoky upper mass overlaps a large iris and establishes the dominant angle. The colored opening ends inside the longer dark canthus. The upper boundary has local sharp interruptions from ink and lashes; the lower boundary curves and tapers asymmetrically. Lashes are part of the dark silhouette. Soft pigment and a lighter separated crease give the heavy upper lid its character without turning the entire border into a uniform ring.

Increasing lash thickness around the same ellipsoid does not change those underlying relationships. Passing closure and gaze tests addresses mechanics only.

## What the actual Unity-chan files demonstrate

The inspected source is the original model inside [original-unity-chan.zip](../third_party/unity-chan/original-unity-chan.zip), imported read-only from `.local/unity-chan-reference/unitychan.fbx`. Its SHA256 is `2a69b65b898aac6e3aa568fb4766f5453c6f3d2c0e86ce3a0266645f76dee79e`. Source bytes were checked unchanged.

| Actual mesh | Vertices / triangles | Relevant structure |
| --- | --- | --- |
| `eye_L_old`, `eye_R_old` | 24 / 22 each | Open iris patches; 24 boundary edges each; no facial shape keys |
| `eye_base_old` | 74 / 96 | Separate open eye backing; 48 boundary edges; no facial shape keys |
| `EYE_DEF` | 363 / 626 | Deforming skin around eyes; seven expression/closure targets |
| `EL_DEF` | 316 / 372 | Separate eyeline geometry with the same seven targets |
| `BLW_DEF` | 40 / 36 | Separate brow geometry and six brow targets |

These are actual imported mesh counts, not an inference from a screenshot. Each iris patch departs from its best-fit plane by at most about 0.00133 imported world units. It is an open, shallow form, not a closed eyeball. This depth measurement is descriptive of the reference; it is not a scale prescription for Ren.

The [measured geometry report](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/unitychan-eye-geometry.json), [actual mesh arrays](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/unitychan-eye-reference-arrays.npz) and [front/quarter/profile layer diagnostic](../art/generated/characters/ren/parts-workflow-v1/h-eye-design-audit-v1/unitychan-layer-geometry-plate.png) make that structure inspectable. The diagnostic deliberately makes skin transparent; visible iris behind it at closure is not a rendered closure failure. It is not a faithful material render and is not a Ren proposal image.

Actual material and shader bindings in the supplied files:

- `eyebase.mat` uses `Unitychan_chara_eye.shader`, opaque Geometry queue, with `eyeline_00.tga`.
- `eye_L1.mat` uses `Unitychan_chara_eye_blend.shader`, alpha blending in Geometry+1, with `eye_iris_L_00.tga`.
- `eyeline.mat` uses `Unitychan_chara_eyelash_blend.shader`, alpha blending in Geometry+2, with `eyeline_00.tga`.
- The shared `CharaSkin.cg` uses texture alpha, view-normal falloff, rim response and shadow attenuation. Its view-dependent shading is **not evidence of camera-facing mesh correction or iris deformation**.

The model's `AutoBlink.cs` explicitly drives shape index 6 on **both** `ref_SMR_EYE_DEF` and `ref_SMR_EL_DEF`. Open/half/close weights are 0/20/85 in that script; its scheduled state changes are stepped. It establishes coordinated skin/ink closure. Our future smooth realtime blink is an additional implementation requirement, not a feature attributed to that legacy script.

The Blender FBX importer reports unsupported proprietary Maya texture links. The shader/material findings above come from the actual source files, not an assumption that the imported Blender materials rendered faithfully.

## Proposed construction, after contour review

**First author the projected drawn layers; depth serves those shapes.** Keep H's accepted face and source boundaries fixed. Do not carry over v1's ellipsoid radii, gaze basis or regular annulus as the design template.

1. Build a shallow open backing tucked behind the accepted face opening. Its front contour comes from the reviewed aperture, with just enough depth/camber for quarter and profile occlusion. It must not protrude as a spherical globe. The forehead, nose, cheeks and jaw are not reshaped to make it fit.
2. Use a separate grey-blue iris patch with the reviewed visible lower/side arc, pupil, dark rim and deliberate highlights. Author the large underlying iris and let the upper ink/skin cover it. Do not expose a small centered circle merely because it fits a regular opening. The hidden iris continuation is a construction choice, identified as such.
3. Make the upper lid a broad shaped skin shutter carrying its smoky pigment. Place the dark upper ink mass on the moving shutter. Give that ink its own exterior contour, tapered inner endpoint, irregular thickness and integrated temporal branches. Its edge is not a constant-distance offset of the aperture.
4. Give the lower lid its own shallow asymmetric curvature and soft tapered pigment. Preserve the drawing's pale wedges and lower iris overlap. Do not add a complete dark lower ring or a rounded anatomical waterline by default.
5. Put the crease on the moving upper lid with separate soft pigment response. Its open, half and closed positions must follow the shutter. No stationary painted open-eye line remains on closed skin.
6. Connect only the generated eye surface to the frozen H boundaries with a reviewed local transition. The old provisional overlay seam and unpromoted shading-band experiment are not accepted foundations. Verify actual surface continuity and color in whole-head quarter views before promotion.

The skin and ink may be separate mesh/material layers, as in the implementation reference, while sharing correspondences for deformation. Ren's shape comes solely from Ren's drawing. Any view-conditioned correction discussed below is a Ren-specific proposal, not copied or claimed from Unity-chan.

### Front, quarter and profile rules

| View | Construction and review |
| --- | --- |
| Source-matched portrait | Match head pose/camera first. Overlay aperture, ink silhouette and visible iris separately on the original pixels. Keep each drawn eye's asymmetry and foreshortening. |
| Front | Use the reviewed neutral relationship after separating head roll from eye design. Check the large covered iris, angled smoky mass and lower taper at whole-head size. Do not infer a frontal eye merely by averaging or mirroring portrait traces. |
| Quarter | Let face/skin depth occlude the backing. The near-eye ink retains its mass; the far eye narrows coherently and disappears behind the nose when appropriate. An iris must not become a protruding oval globe or a bright patch floating outside the lid. |
| Profile | Backing and iris sit behind the lid silhouette. No plate edge, gap, bulging sphere, floating fan root or through-face iris is visible. This is a construction review because the sheet lacks an isolated profile-eye authority. |

Start with fixed shallow geometry and review the full camera sweep. If that cannot preserve the designer's reading at quarter view, propose bounded yaw/pitch corrective shapes for the generated eye layers. Such corrections must interpolate continuously, retain lid/ink attachment and respect face occlusion. Do not use unrestricted billboarding or stretch the entire face to preserve a front image at all angles.

If source-matched eye placement requires changing the accepted nose/cheeks/jaw, report the conflict. Do not silently alter those accepted features.

### Blink, gaze and expression behavior

Blink controls the shaped upper shutter, lower response, ink and crease together. The closure line needs an authored contour; it is not the straight chord of a collapsed almond. Review open, quarter, half, three-quarter and full closure in the source pose, front and quarter. Full closure must cover iris/backing at every allowed gaze. A designer-extrapolated full blink is explicitly subject to review because no exact closed-eye source is available.

Keep `eyeBlinkL/R`, `eyeWideL/R` and `eyeSquintL/R` as the semantic interface, but regenerate their geometry from the approved new neutral. Wide changes aperture; squint includes its own lower-lid response and is not just partial blink. Do not automatically reuse v1/v2 additive weights or call the old successful mechanics evidence for the new assembly. Emotion shapes must retain the corresponding drawn silhouette and be evaluated separately from neutral.

Gaze should move the iris over the shallow backing within an authored visible envelope, using translation or a surface mapping as appropriate. It need not rotate a physical sphere. Recalibrate local axes and practical gaze limits from the new geometry; test screen-left/right and up/down in the actual exported viewer. No inherited ±12°/±8° range is accepted merely because the rejected assembly used it. Eye convergence and extreme gaze require separate clearance checks.

### Materials and runtime scope

Give the upper ink, soft smoky pigment, lower pigment, crease, skin and iris deliberate separate roles. The dark smoky mass should survive nightclub lighting; the iris keeps its grey-blue identity and drawn highlights. Broad polished corneal reflections must not replace the graphic iris. Material and lighting decisions are reviewed on the complete head, including cap/hair occlusion, at the same lighting baseline as the source comparison.

Unity-chan's legacy shader queues are evidence of layer organization, not a drop-in Unity 6.3 URP implementation. The future export must explicitly verify depth/occlusion, alpha edges and sorting in URP. Target remains sustained 30 fps on iPhone 15 Plus and approximately 40,000 rendered triangles for complete Ren. Record actual new eye meshes, draw calls and total triangles; no device-performance claim is made by this proposal.

## Review gates before any promotion

1. Review the refined visible source curves, especially upper aperture interruptions, neutral lower taper and hair-adjacent fan tips. Freeze only visible segments that match the actual pixels. Keep uncertain sections and source limitations recorded.
2. Approve the layer/depth construction direction above before producing another eye mesh. This direction was subsequently authorized for the separate prototype; likeness remains subject to the following gates.
3. Build the separate candidate on the accepted H face, with matched source camera/pose and unchanged non-eye geometry. Compare source and render using one documented translation, rotation and uniform scale; no independent X/Y stretching. Do not use a warped reference to hide mismatches.
4. Display source/render aperture masks, dark ink silhouettes, visible iris masks and direct overlays. Measure bidirectional contour deviations in original-source pixels for visible segments after the camera alignment is fixed. Report uncertainty and mismatched regions. Thresholds and likeness acceptance come from review; this document invents neither a pass threshold nor a numerical 1:1 result.
5. Review complete-head front/quarter/profile, small whole-head presentation, blink intermediates and full closure, gaze extremes, and the designer's separate emotion poses. Mechanically clean exports cannot substitute for the designer-silhouette review.

The evidence is reproducible through [audit_ren_anime_eye_reference.py](../tools/character_art/audit_ren_anime_eye_reference.py). This proposal preserves the rejected geometry history and all accepted face assets.
