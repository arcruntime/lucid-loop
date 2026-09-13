# Ren H construction status

Updated 2026-09-14 JST. Character art owner: `astra-character-artist`.
Shared bus topic: `lucid-loop` (`b6db061cab`); character topic:
`lucid-loop/character-art` (`085285922e`). The other Astra owns non-character work.

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

## What can be opened in Unity now

The newest neutral designer-eye player is
`.local/ren-designer-eye-v6/RenDesignerEyeReview.exe`. It shows the v6 eyes,
selected Tokon materials and experimental closed-lip paint. Its 18 actual captures
are in `h-designer-eyes-v1/trial-v6/unity-review-v1/`, relative to the construction
root below. This version is explicitly diagnostic: shading seams and hair defects
remain, and its full-sheet reference panel has an inherited NPOT resize error.
Do not judge a 1:1 match from that panel. A separate corrected comparison is in work.

To inspect this scene in Unity, open the existing isolated project
`C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification`
with Unity 6000.3.24f1, then open
`Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeReview.unity`.
The scene exists in that project, not the main `B:\lucid-loop\Unity` project.
The viewer worker uses this scratch project for batch builds; wait for its active
Editor process to exit before opening the same project interactively. The local
player can be inspected independently after its capture process exits.

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
