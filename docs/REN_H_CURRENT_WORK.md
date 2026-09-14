# Ren H construction status

Updated 2026-09-14 JST. Character art owner: `astra-character-artist`.
Shared bus topic: `lucid-loop` (`b6db061cab`); character topic:
`lucid-loop/character-art` (`085285922e`). The other Astra owns non-character work.

**Current delivery correction:** the user requires the review in
`B:\lucid-loop\Unity`. The working mouth scene and 235 dependency files have
been copied there and verified against the migration manifest. Main Editor
import and Play Mode mouth validation passed, with an actual render in
`docs/validation/ren-main-review/main-render.png`. External scratch paths below
are historical source locations.
Japanese voice work is deferred for this release; English articulation continues.
The user reiterates that current art quality is insufficient. Slight Arcane
influence does not relax the designer-eye or Tōkon NPRS acceptance requirements.

**Updated user budget:** complete Ren 50–80k triangles; head/face 20–25k,
hair 15–30k, visible body/clothing 15–25k, with component sums constrained by
the overall target. Whole-frame target is 250–300k. Earlier 40k allocations in
historical studies are superseded. Dense review assets still exceed these
targets; see [iOS rendering requirements](IOS_RENDERING.md).

**Latest user review rejects the current H v1/v2 eye construction.** Its
quasi-realistic eye structure does not match the designer's American-anime
silhouettes. Existing captures and players are diagnostic history. Eye work is
now reference-first structural redesign; the accepted mouth, cheeks, jaw and
nose must stay intact. Tokon-esque NPRS from `B:\openai-hackathon-game` is the
primary shading reference, with restrained Arcane influence allowed. See the
[updated goal](REN_CHARACTER_GOAL.md#latest-user-correction-designer-eye-silhouettes-and-nprs).

The replacement work is in `h-designer-eyes-v1/`. A source-camera comparison and
layered eye prototype now exist. The first trial had intersecting eye layers;
the second exposed excessive front-view asymmetry. Trial v6 corrects plane
orientation and exact shutter attachment, incorporates original-H boundary
pigment, and refines the visible lash fans. Its neutral eye-only export is in
`h-designer-eyes-v1/trial-v6/neutral-export-v2/`: 22 meshes, 5,145 authoring
triangles. Fresh Blender FBX/GLB imports verify geometry, UVs and color within
their documented storage precision. The first actual Unity player produced 18
captures and passed rendered-corner import checks, but eye-region shading,
fragmented far lashes and exposed support still fail visual review. The export
has no blink or gaze controls. None has passed likeness review. See the
[construction review](REN_DESIGNER_EYE_FIRST_CAMERA_REVIEW.md) for inspected
evidence and the distinction between camera alignment and actual visible shape.

The Tōkon scratch GPU studies have established two provisional material choices:
extended face-mask coverage removes jagged lip/chin shadows, and the cap's
authored highlight mask removes the broad panel highlight. Their paired evidence
is under `h-anime-paint-v1/tokon-study-v1/unity-review/` and
`h-anime-paint-v1/tokon-study-v1/cap-controls-v1/unity-review/`. These studies still
use the rejected historical eyes. The first outline-shell Unity comparison
preserved pose synchronization but introduced unwanted internal nose lines and
broken hair strokes, so its style is rejected. A separate head-stencil correction
remains pending. The combined new-eye material review is now inspecting separate
received-shadow and support/bridge corrections; these are not shipping assets.

The [corrected support comparison](../art/generated/characters/ren/parts-workflow-v1/h-neutral-context-defect-audit-v1/unity-support-review-v2/README.md)
now has eighteen actual Unity captures. The scalp stays beneath the cap and the
profile jaw gap improves. The earlier standalone support export carried an
incorrect world offset; its passing import checks reproduced that error faithfully.
The corrected placement is the next review baseline, not a finished skin interface.
It exposes a 280-pixel background slit beneath the image-right iris, present under
neutral, club and unlit rendering. This needs geometry closure, not darker paint
or a shadow adjustment. The bridge is static; its A/seal targets do not deform.

A [donor replay](../art/generated/characters/ren/parts-workflow-v1/h-neutral-context-defect-audit-v1/support-pigment-audit-v1/README.md)
reproduces every stored support and bridge color at the corrected placement within
2.98e-8 linear RGB. The misplaced saved matrix was introduced after sampling;
there is no evidence for a compensating repaint. Visible shading boundaries remain.
The next assembled player will compare receiver classification, restrained facial
shadow strength, clean paired hair maps and a local eye-interface patch separately
before showing their combination. The resulting six-variant Unity player has now
produced 42 captures in `.local/ren-designer-refinement-v1/live-review/`.
Root inspected combined front, quarter, club and artist-portrait views, plus the
baseline and receiver-only front pair. Corrected receiver classification with
facial strength 0.05 reduces the hard eye band and is the provisional next lighting
baseline. Residual interface differences remain. Clean hair maps remove marbling
but read too flat and brown against the designer's pale-blond strands; they need
another painted-detail pass. The combined head is not visually accepted.

The local player is `.local/ren-designer-refinement-v1/RenDesignerRefinementReview.exe`.
The eye-interface patch also passes the focused actual quarter-view comparison:
594 background pixels become covered, with seven previously covered pixels also
changing. Root inspected the matched close-ups and selected this repair for the
upper temporal notch. It does not address the separate lower-iris slit or remaining
pale gaps between lash strokes. No blink/gaze or speech completion is implied by
these neutral captures.

The subsequent forehead-only comparison has fourteen valid actual captures in
`.local/ren-designer-forehead-v1-retry2/live-review/`. Its baseline matches the
frozen combined front exactly, and both unlit views match. Matching face response
on 189 support triangles reduces the targeted jagged patches; the 1,698 changed
front pixels stay within the forehead, with no neck/back change. Remaining edge
and transition marks still need continuous control values. The first attempt's
viewer initialization failed and produced invalid camera framing; those captures
are rejected and preserved separately.

## What can be opened in Unity now

The latest successfully captured local player is
`.local/ren-designer-mouth-v1/RenDesignerMouthReview.exe`.
It has working open-A and seal sliders on the current head, with graded forehead
lighting, blue-grey irises, the lower-eye return and directional pale hair maps.
Eighteen actual front/quarter/profile mouth captures passed weight, placement and
lip-map checks. Each view returns pixel-identically to its starting rest image.
The four restored meshes retain the original source's rendered UVs and normals;
tiny neutral raster differences from the earlier import are documented. Teeth,
tongue and open-lip painting remain provisional. Full speech, expressions and
new-eye blink/gaze integration are not complete.

The preceding `.local/ren-designer-eye-hair-v1/RenDesignerEyeHairReview.exe`
keeps the iris, return and directional hair changes independently selectable.
Forty actual
captures are in `.local/ren-designer-eye-hair-v1/live-review/`. The return fills all
273 exact-background pixels in the disclosed lower-eye region. Root inspected
combined front, quarter and artist-paired portrait and retained these three changes
as the next provisional baseline. Hair currently reads cool/silver; skin boundaries
and the overall style/likeness gap remain. New-eye blinks are still separate studies.

For the six-way material and geometry comparison preserved in commit `54b5417`,
run `.local/ren-designer-refinement-v1/RenDesignerRefinementReview.exe`.
Its corrected artist-reference pairs and 42 captures are preserved in
`h-designer-eyes-v1/trial-v6/unity-refinement-review-v1/`.

To inspect the migrated scene in Unity, open `B:\lucid-loop\Unity`
with Unity 6000.3.24f1, then open
`Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity`.
Press Play for the mouth controls after compilation/import finishes. The copied
scene has passed main Editor import and Play Mode mouth validation, and an actual
main-project render is preserved in `docs/validation/ren-main-review/`.
No scripts, meshes, materials or shaders were missing. The earlier eighteen
captures above came from the historical project. Active scene renderers total
337,248 triangles before visibility and extra-pass accounting: still over budget.
Migration preserves existing main files and remaps 45 copied YAML references to
identical main shaders. Do not open another Editor on the already-open project.
The six-way comparison and other external scratch scenes remain historical;
future work belongs under `B:\lucid-loop`.

### Historical H animation prototype

The actual H Windows player is `.local/ren-h-reference-v1/RenHReference.exe`;
see [historical launch instructions](../README.md#inspect-the-rejected-h-eye-prototype).
Its scene, `Assets/CharacterArt/Generated/Preview/Scenes/RenHReferenceAnimation.unity`,
has built in the isolated scratch Unity project. Main-project asset copying is
withheld because the eye appearance was rejected. The older main-project
`RenNprReview.unity` remains a P2/V2 shading study, not this H assembly.

Actual player renders verify basic mouth, blink/gaze and cap pose preservation.
Visible UI inspection confirms the source video and artist panels, playback and
paused seeking in the historical transport review. A separate material-review
build fixes the timestamp/slider background; later click-based pause/replay/seek
checks were inconclusive because of focus, so they are not another UI pass.
Seventeen performance channels remain unsupported in that historical player.
The H face is still a dense construction prototype with unresolved visual issues.

## Current construction sources

### Facial morph import limitation

The isolated Unity FBX welding A/B found the same missing authored movements with
welding enabled and disabled: 1,873 zeroed moved endpoints across the complete
source's 14 morph meshes and 70 frames, plus three precision-threshold residuals.
The earlier 56-row report covered only an outline-visible head subset. Disabling
welding does not fix these inputs. See the
[full A/B evidence](../art/generated/characters/ren/parts-workflow-v1/h-tokon-outline-v1/import-audit/unity-weld-ab-v1/README.md).
Canonical restoration is being prepared separately; existing source imports and
the neutral-eye comparison have not been modified by this test. A successful
Blender roundtrip alone therefore does not establish Unity speech-shape fidelity.

Actual `AddBlendShapeFrame` readback also drops small submitted position deltas.
A private bone-free experiment scaling Basis and deltas together by 100, with an
inverse instance scale, reduced but did not eliminate the error: 292 returned
endpoints still exceed the 2e-6 native-unit tolerance, with a maximum error of
1.045e-5. No restored assets were saved. This is measured API behavior under these
inputs, not proof of a universal threshold or a production rig solution.

The following scale-10,000 experiment passed the declared 2e-6 position tolerance
after saving and reloading all fourteen scratch assets. All 280 actual `BakeMesh`
samples (five weights for each of 56 frames) passed; maximum endpoint and pose error
was 1.740e-7 native units. There are still 161 tiny zeroed nonzero rows, so this is
tolerance-qualified fidelity, not bit-exact preservation. Source FBX/meta and
normal/tangent frame arrays remain unchanged. These are unbound diagnostic meshes
requiring the recorded inverse-scale attachment. Only current head/oral integration
is being prepared; rejected historical eye meshes must not replace the new eyes.
Actual assembled GPU mixtures and shared-rig binding remain unverified.

### Source inventory

All paths below are relative to
`art/generated/characters/ren/parts-workflow-v1/`.

- Exact user-approved H master: the immutable source in
  `../bust-comparison-v1/tripo/studio-h3.1-a-open/`.
- Coherent H face checkpoint:
  `h-face-fit-v1/native-cleanup-v1/Ren_H_NativeFace_Cleanup.blend`, SHA-256
  `4bdbdda04bad885c14eb4b2456b4925d386e3633d842dfabc5c236a1a0936e57`.
  This retains 129,778 H vertices at exactly their original positions. It is a
  dense construction surface, not the final polygon budget. New cheek patches
  require the separate UV/material derivative and a winding audit; eyes, oral
  deformation, ears and concealed crown/back support are separate work.
- Detailed hair geometry/UV source:
  `h-hair-fit-v1/hair-only-selected-v2/Ren_H_DetailedHair.blend`, SHA-256
  `a49688a84c3654900ce80576f2b9a020c4be852656159afb9656d07cb65c9b9c`.
  9,496 triangles; geometry and UV reimport checks pass. Head support and cap
  attachment are not established by the hair-only export.
- Basic mouth now uses `h-expression-controls-v1/closure-assembly-contract-v2/`
  and the frozen `welded-oral-study-v9/` source. Its closed surface retains the
  visually reviewed v6 corrective, with exact H open-A return. Full projected
  aperture coverage passes at eight sampled views/poses, and tested oral boundary
  attachments have zero gap. The apparent gray triangle in profile is outside
  the actual lip opening. Basic assembly is accepted; painted whole-face,
  performance shapes and Unity playback remain unverified.
- `h-eye-controls-v1/` is rejected eye-style history. Its 3,460-triangle exports
  and blink/gaze checks do not approve the new eye design. The original H cut
  coordinates remain useful attachment provenance, but must not dictate the
  designer's visible aperture. Replacement layers live in `h-designer-eyes-v1/`.
- `h-anime-paint-v1/` holds exact H texture/UV provenance, cheek-patch UV work,
  and a muted rose-lip study. The rose direction is suitable for assembled NPR
  review; it is not final artist approval.

`h-complete-head-v1/` preserves the initial assembly. Its portable source is the
fixed non-eye context for comparison, not a request to overwrite it with ongoing
trials. Build a separate derivative once the new eye construction is reviewed;
retain cheek repairs, UVs and winding corrections.

The first complete front render has now been inspected against the artist sheet.
It has not passed likeness review: pale facial definition, blurred lips and dark
extraction fragments require correction. See the [assembled visual review](REN_H_ASSEMBLED_VISUAL_REVIEW.md)
for the exact capture hash and assigned corrections. Current Unity material
comparisons stay isolated from main-project character asset promotion.

## Rig and accessory contract

Exactly two shared skeletons govern the cast: male and female. Ren uses female.
`SHARED_CHARACTER_RIGS.md` records the 54-bone version-2 definitions and their
remaining validation requirements. Do not introduce a Ren-specific body rig.

The cap is a separate asset attached to the head bone through a socket. A bust
uses an explicit temporary head/socket hierarchy until shared-rig integration.
Cap toggling changes only accessory visibility and the separate cap-on hair
state; it must preserve facial expression, blink, gaze and head animation.

## Reference performance

`art/generated/characters/ren/video-reference-v1/` contains the immutable-source
timing study: 32 manual control channels and 152 keys, not motion capture or
phoneme alignment. `unity-transport-review/` contains actual Unity decoder proof
for startup, seeks, replay and end handling. It does not demonstrate an animated
H character. The final deliverable still needs the corrected H assembly, mapped
controls, synchronized reference viewer and actual captured test animation.

Do not roll this unfinished construction out to the other four characters.
After Ren passes the full pipeline, dedicated subagents can apply the same
verified process to their individual designs and the two shared rigs.
