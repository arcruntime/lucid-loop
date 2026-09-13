# Ren H first assembled visual review

Reviewed 2026-09-14 against `art/characters/ren-model-sheet.png`.
This is a failed likeness/material checkpoint, not approval of finished Ren.

The actual Blender front capture is
`art/generated/characters/ren/parts-workflow-v1/h-complete-head-v1/cap-on-front.png`
(the assembler is preserving this initial image under `evidence/initial-provisional-front.png`).
Image SHA-256: `cb1ee0d198555417619b593690da8b5ab82aa3dd69e8434dc9afe41c94430ccc`.
Its source blend hash was
`b83b233451147992c7dad4c9c358d600605811b860def95f028a1f24eebd5bf1`;
the working assembly is being revised, so its current filename alone does not
identify this reviewed state. This is a 1100-square orthographic Blender capture,
not Unity NPR or device-performance evidence.

## Visible findings and next checks

- Complete head silhouette, detailed layered hair, cap and ear jewelry can now
  be judged together. Preserve the selected H cheeks and jaw through corrections.
- The central face is too pale and loses nose and cheek definition. Compare
  texture sampling, color management and illumination before changing geometry.
- Closed lips read as a tall, blurred rose patch with a pale horizontal seam.
  The artist uses a distinct upper/lower lip contour and controlled highlights.
  Separate UV/paint problems from closure deformation using matched open/rest
  captures; passing mouth collision tests does not resolve this appearance.
- Much of the hair reads dark gray/brown instead of the artist's pale blond.
  Inspect material response and cap shadow alongside actual texture colors.
- Dark fragments remain above the brows and along the cheeks and ear roots.
  Classify extraction remnants versus texture contamination before repair.
- The eyes read as an anime construction, but that alone does not establish the
  artist's lash, lid and brow design. Review the whole head at front, quarter,
  half blink and closed blink after the material correction.

The material owner is investigating the color/UV response; the assembler is
incorporating the cleaned native ears and producing exportable review assets.
Unity integration may proceed with these defects labeled, while corrections
continue. Neither the provisional eye seam nor the dense construction mesh
satisfies final topology, likeness or the approximately 40k whole-character budget.

## Integration requirements retained

Ren uses the single shared female skeleton; the cast also has one shared male
skeleton. The bust's temporary `Head/CapSocket` hierarchy is not proof of final
female rig binding. Keep the cap a separate accessory and verify that toggling
its visibility and hair-fit shapes preserves all facial and head-motion state.

The engineering animation handoff supplies original FBXs, not accepted retargets.
Ren's DJ idle remains reference-only in that handoff. Navigation owns movement
through transforms/NavMesh; character animation must expose one body-motion input
through its existing graph without a competing Animator controller.
