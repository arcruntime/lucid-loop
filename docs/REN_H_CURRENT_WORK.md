# Ren H construction status

Updated 2026-09-14 JST. Character art owner: `astra-character-artist`.
Shared bus topic: `lucid-loop` (`b6db061cab`); character topic:
`lucid-loop/character-art` (`085285922e`). The other Astra owns non-character work.

## What can be opened in Unity now

`Unity/Assets/CharacterArt/Generated/Preview/Scenes/RenNprReview.unity`
is the implemented shading comparison, using the older P2/V2 head. It is not
the corrected H character. `RenHReferenceAnimation.unity` has not been built.
Its runtime and actual-video transport are prepared; geometry remains in Blender.

## Current construction sources

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
- `h-eye-controls-v1/` supplies verified 3,460-triangle eye-only exports with
  H-specific blink/gaze calibration and a separate skin-pigment library. Neutral
  and full-blink views pass construction review. The original overlay cut is
  still a provisional attachment; an exact native-cycle transition is in work.
- `h-anime-paint-v1/` holds exact H texture/UV provenance, cheek-patch UV work,
  and a muted rose-lip study. The rose direction is suitable for assembled NPR
  review; it is not final artist approval.

`h-complete-head-v1/` is the initial assembly workspace. Combine the current
contracts there; do not replace the material-ready face with an older study
head that would discard cheek repairs, UVs or winding corrections.

The first complete front render has now been inspected against the artist sheet.
It has not passed likeness review: pale facial definition, blurred lips and dark
extraction fragments require correction. See the [assembled visual review](REN_H_ASSEMBLED_VISUAL_REVIEW.md)
for the exact capture hash and assigned corrections. Unity integration is proceeding
with the prototype status explicit.

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
