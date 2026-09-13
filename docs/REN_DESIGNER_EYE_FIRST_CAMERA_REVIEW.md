# First designer-eye camera review

This is a construction review, not visual acceptance of Ren.

The current authority is the designer's American-anime eye silhouettes and the
Tōkon NPR reference in `B:\openai-hackathon-game`. Slight Arcane influence may
inform paint and color; it does not justify realistic eyes or noisy facial shading.

## Evidence inspected

Actual images in
`art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/`:

- `portrait-camera-comparison-v1.png`: original portrait, fixed H render and
  50% overlay. Old eye assemblies are hidden; no replacement eyes are present yet.
- `portrait-camera-landmarks-v1.png`: projected and source landmark pairs.
- `portrait-camera-fit-v1.json`: one orthographic scale and camera rotation,
  with the accepted portable H source hash recorded.

The two orbital position residuals are approximately 1.9 and 0.5 original-source
pixels. The nose residual is approximately 3.9 pixels. Mouth and chin residuals
remain approximately 18 pixels. These position anchors do not prove matching
eye contours or a solved whole-face camera. The illustrated mouth is parted;
the rendered rest is sealed. Preserve the accepted mouth, nose, cheeks and jaw.

## Construction decisions

Proceed with the first shallow eye-layer prototype using this fixed comparison
camera. The existing orbital cut boundary is an attachment boundary, not the
visible eye aperture. New skin shutters must cover it to the designer's drawn
opening; old realistic eyelids must not supply the final silhouette.

Review aperture, upper ink, visible iris and lash silhouettes separately against
the original pixels. Also inspect front, quarter and profile views: matching a
single camera cannot excuse exposed plate edges, floating lashes or poor depth
occlusion elsewhere. Hair currently obscures different regions than the drawing;
show complete-head and unobscured construction views, and retain uncertainty for
source segments hidden by hair.

## First constructed trial: not accepted

Inspected actual `h-designer-eyes-v1/trial-v1/portrait-complete-head.png` and
`portrait-unobscured-eyes.png`. The image-left eye loses most of its visible iris
and heavy upper ink even with hair hidden. This is an eye-layer/interface problem
to diagnose, not a successful match obscured only by the hairstyle.

The trial's `construction.json` records exact outer-boundary coordinates and no
ray-surface fallback, but those checks do not prove correct visible depth ordering.
The image-right opening has only 86.8% of sampled aperture points inside the old
projected cut. The image-left fitted depth surface differs from its boundary by
up to 0.013 native units, comparable to or larger than the layer offsets. Review
object visibility and intersections before another likeness claim.

The old eye cut may need a local topology correction to expose the designer's
opening. Preserve accepted facial surface positions and non-eye silhouettes;
retaining a rejected eye interface is not a reason to clip the designer's eye.
Show any such local correction explicitly in the next construction handoff.

The subsequent `trial-v1/source-pixel-visibility.json` confirms actual intersecting
layers with hair and cap excluded: the R pupil sample hits Backing before Pupil
and Iris, while a lower iris sample correctly hits Iris before Backing. Backing
also occludes an upper-ink sample. The correction is a shared shallow interior
surface with boundary blending outside the graphic layers, followed by another
visibility check and actual renders. Do not reshape the traced contours to conceal
this depth-ordering bug.

Trial v2's expanded opening audit covers 561 source-pixel samples. The reported
backing/iris/ink crossings are removed. It finds 15 no-hit pixels at the L opening
outside the old backing outline, with no accepted-head occluder in the sampled
grid. Extend the backing to the designer opening first; these results do not
justify cutting the accepted face. Actual rendered review remains pending.

The actual v2 front/quarter/profile plate subsequently exposed excessive eye
width asymmetry from the two fitted orbital plane slopes. Correcting source-view
visibility was insufficient. A shallower, more frontal interior-plane derivative
keeps the source camera and traced contours fixed for the next multi-view check.

Pigment integration then caught a separate attachment error: projected float
matching had preserved only 91 of 274 L boundary vertices. Trial v4 uses the
triangulator's original-input identities. Its pigment report records all 274 L
and 299 R boundary vertices, zero boundary-color error and unchanged accepted
non-eye geometry, UVs, morphs and hierarchy. Original-H skin samples are now
applied with a separate interior smoky-pigment field. This report is an attachment
check; actual v4 likeness and material-seam review remains pending.

## Neutral v6 review handoff

The user reiterated that slight Arcane inspiration is acceptable, while the
previous character quality is not. The designer's American-anime forms and the
Tokon NPRS reference remain the acceptance criteria.

The v6 front, quarter and contour plate were inspected. Finer angled lash strokes
replace the earlier broad parallel prongs. This selects v6 for a neutral Unity
material comparison only; it does not establish a 1:1 likeness or accept blinking,
gaze or expression controls. Hair-obscured contour endpoints remain uncertain.

Both `trial-v6/profile-L-hair-hidden.png` and
`trial-v6/profile-R-hair-hidden.png` were also inspected. The eyes present shallow
side apertures without an obvious exposed spherical globe or detached backing
slab. Removing the hair exposes existing forehead, scalp and face extraction
boundaries; these diagnostic images are not complete-head presentation renders.

The next combined neutral Unity comparison uses the selected face and cap masks,
with outlines off and the closed-lip paint explicitly identified as part of this
material candidate. Preserve the accepted non-eye surface. Compare against the
designer's sketch before expanding the new eye controls.

## Parallel shading finding

The actual first Tokon player captures in
`.local/ren-tokon-review-v1/live-review/` show a clearer pale-blond palette, but
`tokon--rest-front.png` and `tokon--club-a.png` still show dark socket rings and
jagged lower-lip/chin shadows, plus a broad block of cap highlight. These fail the
intended deliberate graphic shading. They are diagnostic captures of the rejected
old eyes, not a new accepted character.

The next controlled comparison must address facial light response and mask
coverage, particularly the transition near lips and chin, while retaining the
accepted geometry. Both comparison materials must receive the same lighting,
pose and unlit state. The viewer identified a synchronization bug in the previous
shader's unlit state; affected first-run comparisons are superseded evidence.

The v2 retry captures in `.local/ren-tokon-review-v2/live-review-retry/` were
inspected at front, quarter, profile, head turn and club-a. The refined control
mask removes the jagged lip/chin shadow visible in the paired original-control
front capture. Select it as the provisional next shading baseline. This does not
accept the old eye region, original lip paint, large cap highlight or overall
character appearance. The next cap comparison should change authored highlight
placement while holding this refined face baseline fixed.

The actual cap original/placed quarter pair in
`h-anime-paint-v1/tokon-study-v1/cap-controls-v1/unity-review/` was inspected.
Select the placed mask as the next cap baseline: it removes the broad panel
highlight while retaining the matte color and seams. A broad brim/rim band is
still reported during head turns; this G-only correction does not resolve it.
The separate smoothed outline-shell candidate is selected for scratch Unity
validation, with normal restoration required and 184,132 additional diagnostic
triangles explicitly outside a production budget claim.
