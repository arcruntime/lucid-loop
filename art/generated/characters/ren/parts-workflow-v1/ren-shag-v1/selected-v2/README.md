# Frozen selected-v2 hair for the working Ren viewer

This package carries the reviewed diagonal fringe into the lower cap. Corrected root continuity makes it a **working viewer candidate**; final artist likeness remains unfinished. No broad hair revision is included after that review.

Use [Ren_Shag_CapState.glb](Ren_Shag_CapState.glb) or [Ren_Shag_CapState.fbx](Ren_Shag_CapState.fbx), both hair-only. The [combined Blender scene](Ren_Shag_CapState.blend) includes the unchanged painted head and cap/jewelry context. [manifest.json](manifest.json) gives exact hashes, materials and controls; [verification.json](verification.json) records fresh export reimport checks.

The frozen Blender SHA256 is `f160e66c8b611796c284c396508ba46f3c052b3ea8a219dceb603d18694565cc`. Its matching cap is `head-accessories-v2/band-clearance-v1/Ren_Head_Accessories.blend`, SHA256 `bff2a53430d58535e6f2aef8d271e8a8774a6c8015185a4cd5f2b74168ab805d`. Use that exact accessory derivative, which adds only the authorized small band clearance and leaves crown height unchanged.

## Placement and controls

Hair has six semantic mesh objects and **7,900 rendered triangles**. Use identity placement in the native head frame: +X front, +Z up in Blender. Each object has one `capOn` shape. Set all six to 1 with the cap visible, or all six to 0 with the cap hidden. Exports default to 0; the combined review blend defaults to 1. FBX/GLB include no head, cap or jewelry.

`Ren_PaleBlond_PaintedHair` uses the adjacent `Ren_Hair_BaseColor_4K.png`, also embedded in the interchange files and packed in the blend. The opaque 4K atlas has separate padded longitudinal tiles for 53 locks plus the root shell. It is procedural paint; the artist's final strand breakup and highlight arrangement are unfinished.

## Actual review views

| View | Shaded cap on | Cap off | Diffuse/base color |
| --- | --- | --- | --- |
| Front | [Image](cap-on-front.png) | [Image](cap-off-front.png) | [Image](diffuse-front.png) |
| Quarter | [Image](cap-on-quarter.png) | [Image](cap-off-quarter.png) | [Image](diffuse-quarter.png) |
| Profile | [Image](cap-on-profile.png) | [Image](cap-off-profile.png) | — |
| Back | [Image](cap-on-back.png) | [Image](cap-off-back.png) | — |

The diffuse images are real geometry renders using emission sourced from each material's actual base-color input. They make the pale painted hair readable without the cap's deep cast shadows. The original lit materials are restored in every exported artifact and in the saved blend; no emission override is baked into the deliverable.

## What was fixed

The left sweeps extend diagonally across one eye toward the bridge/midnose. Their cap-off basis is unchanged from the reviewed extended-fringe preview. The earlier local tuck around the upper ear cuffs remains in place. The source head's vertices, world matrix and all facial shape-key coordinates remain unchanged.

The cap-on fit constrains roots to the interval between the head surface plus 0.003 native units and the cap surface minus 0.003. A virtual 0.09-long continuation of the cap's lower edge bounds the transition beneath the band; this prevents a quad from crossing the crown while one endpoint sits below the open cap edge. That fitting surface is **not output geometry**. The final authorized cap derivative resolves all eight measured narrow corridors, and the final sampled deficit count is zero. Visible stems now continue into the brim instead of disappearing behind the forehead and reappearing as blunt cuts.

Both exported formats reimport with six objects, 7,900 triangles, one UV/material on every mesh, the 4K atlas, and matching cap-off/cap-on bounds. Every `capOn` target survives with default0. Ten final render hashes pass. Verification does not resave or change the frozen blend.

## Explicit limits

The hair remains simplified and ribbon-like, with broad flat lock profiles and abrupt root junctions. It is not final Ren likeness. Final strand painting, artist refinement, rigging, simulation and measured iPhone performance remain unfinished. The [native Tripo reduction review](../../tripo-hair-lowpoly-v1/README.md) documents a stronger possible basis for future source-shag cleanup; it does not replace this frozen viewer candidate.

`selected-v1` is preserved unchanged. Early fringe subdirectories retained only in the local workspace are pre-fit direction studies, not part of this checked-in handoff. The final files in this directory are frozen for the assembler. Reproduce into a new directory before another geometry revision; do not overwrite this handoff during integration.
