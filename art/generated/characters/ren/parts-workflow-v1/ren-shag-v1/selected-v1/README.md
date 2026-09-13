# Selected Ren hair derivative for assembled painted review

Use **[Ren_Shag_CapState.glb](Ren_Shag_CapState.glb)** or **[Ren_Shag_CapState.fbx](Ren_Shag_CapState.fbx)**. Both are hair-only: six semantic meshes, 53 closed locks plus a root shell, **7,900 rendered triangles**. The combined [Blender scene](Ren_Shag_CapState.blend) also includes the unchanged working head and appended cap/jewelry as review context. The [manifest](manifest.json) records exact file hashes and integration settings.

## Controls and placement

Use identity placement against the current head: +X front, +Z up in Blender. Each of the six hair meshes has one `capOn` shape:

| View | Cap visibility | All six `capOn` values |
| --- | --- | ---: |
| Cap off | Hidden | 0 |
| Cap on | Visible | 1 |

FBX and GLB default to cap-off 0. The combined review `.blend` defaults to cap-on 1. Intermediate values are a geometric transition, not a reviewed animated hat-removal sequence. The hair-only exports do not include the cap or jewelry; use the accessory agent's selected files separately.

Material `Ren_PaleBlond_PaintedHair` uses the adjacent `Ren_Hair_BaseColor_4K.png`, also embedded in both interchange exports and packed in the review blend. It is an opaque base-color map with an 8x8 padded layout: 53 lock tiles plus root tile63. Each mesh has one UV layer and one material.

## Reviewed local changes

The parent selected the broader trial-v7 fringe for the combined painted review, not final likeness approval. This derivative preserves it. Exactly **136 vertices** in `Ren_Hair_Side_L` are tucked slightly behind the upper helix to expose both cuffs. Per-vertex before/after coordinates are recorded in `report.json`; verification proves all other cap-off vertices match trial-v7.

The cap-on shape compresses upper roots inside the existing cap shell and blends to unchanged lower ends. It does not enlarge or modify the cap. The working head's vertex coordinates, object matrix, and every existing facial shape-key coordinate remain unchanged.

## Matched views

| View | Cap off | Cap on |
| --- | --- | --- |
| Front | [Image](cap-off-front.png) | [Image](cap-on-front.png) |
| Three-quarter | [Image](cap-off-three-quarter.png) | [Image](cap-on-three-quarter.png) |
| Profile | [Image](cap-off-profile.png) | [Image](cap-on-profile.png) |
| Back | [Image](cap-off-back.png) | [Image](cap-on-back.png) |

The context has `mouthSeal=1` and a temporary neutral material override for the draft rose lip mask. No painted-face acceptance is implied. Both upper cuffs are visible in the cap-on quarter image. Crown tufts no longer protrude through the cap in the inspected front/quarter/profile views.

## Verification and remaining defects

[verification.json](verification.json) passes fresh FBX and GLB imports: six objects, 7,900 triangles, preserved `capOn` controls, default0, matching cap-off and cap-on world bounds, UV/material presence on every mesh, and a surviving 4K atlas. Eight rendered-image hashes pass, along with the unchanged head and exact ear-edit scope checks.

The visible root joins still have abrupt scalloped overlaps. These are geometric junctions; a normals-only fix was not demonstrated. Flattened lock profiles retain ridged shading. The procedural paint lacks the reference's final strand breakup and graphic highlight placement. These remain explicit for the assembled painted review; the reviewed silhouette was preserved instead of beginning another broad shape iteration.

This is a selected review candidate. Final artist likeness, rigging, simulation, LOD behavior, and sustained iPhone performance remain unapproved.
