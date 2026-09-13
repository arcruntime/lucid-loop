# Ren asymmetric shag: local implementation trials

**Current frozen viewer candidate: [selected-v2](selected-v2/README.md).** Its [diffuse front](selected-v2/diffuse-front.png) and [shaded quarter](selected-v2/cap-on-quarter.png) show the extended diagonal fringe under the lower cap, with corrected continuous stems. It is accepted for the working viewer, not final Ren likeness. The original artist sheet and approved V2 wig plates remain the visual authority. [Selected-v1](selected-v1/README.md) stays frozen as the prior checkpoint.

The preserved trial-v7 Blender scene is [Ren_Shag.blend](trial-v7/Ren_Shag.blend). The selected derivative has verified [FBX](selected-v1/Ren_Shag_CapState.fbx), [GLB](selected-v1/Ren_Shag_CapState.glb), a combined review [Blender scene](selected-v1/Ren_Shag_CapState.blend), and an exact [manifest](selected-v1/manifest.json). Trial-v6 is older evidence whose front was rejected. Do not substitute a successful export check for visual acceptance.

## Construction and budget

Current geometry is **7,900 rendered triangles**, including a 480-triangle root shell and 53 closed tapered locks. It uses six semantic mesh objects: back, left/right sides, left/right fringe, and root shell. Each lock has 12 longitudinal stations and a flattened six-point cross-section. The mesh was constructed at this density; no generic decimation was applied.

The target supports the parent's provisional 8k hair allocation inside the full character's approximately 40k rendered-triangle budget. This is a geometric count, not an iPhone performance measurement.

The side/back curves begin from the frozen fitted-v5 source traces, with varied lengths, widths, staggered accents, and five local rear-gap replacement locks. The latest front uses explicit asymmetric crown-to-eye/cheek sweeps instead of retaining the earlier failed front paths. A recorded 136-vertex tuck in the negative-Y side mesh exposes both upper cuffs in the selected quarter view; all other cap-off vertices match reviewed trial7.

## UV and paint

The opaque 4K base-color atlas has an 8x8 tile layout. Each of 53 locks occupies a distinct padded rectangle; tile63 belongs to the root shell. These are coherent longitudinal profile UVs rather than the provider's fragmented charts. Tiny root/tip caps collapse onto the tile boundary because they are not intended painting surfaces.

The atlas supplies pale-blond root shading, longitudinal bands, and fine painted streaks. This is deterministic procedural paint, not approved final artist painting. There are no alpha cards, paid generations, or additional strand geometry. Trial-v6 export verification confirms all six objects have one UV layer and one material and that the 4K image survives FBX and GLB import.

## Head and coordinate contract

The fixed context is `face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend`, SHA256 `6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959`.

Hair uses identity object placement in the native head frame: **+X front, +Z up**. No old rigid-source fitting translation should be applied. The current context uses `mouthSeal=1` and a material-only neutral override for the draft lip mask. Source head vertices, world matrix, and all existing facial shape-key coordinates remain unchanged; the source file is read-only.

## Cap state proof

[cap-on-v1](cap-on-v1/report.json) preserves the earlier trial-v6 cap-fit proof. The selected package applies that construction to the reviewed trial-v7 fringe and current accessory-only source. Its [front](selected-v1/cap-on-front.png) and [quarter](selected-v1/cap-on-three-quarter.png) show the upper tufts tucked inside the cap.

The six exported hair meshes have `capOn`: 0 restores the cap-off basis; 1 compresses upper roots inside the cap with a smooth transition to unchanged lower ends. Exports default to 0; the combined review `.blend` defaults to 1. Cap and jewelry are context in that scene and are not included in hair-only exports. The accessory geometry is not altered by this fit.

## Reproduce

```powershell
python tools/character_art/refine_ren_shag.py --preview
python tools/character_art/refine_ren_shag.py
python tools/character_art/refine_ren_shag.py --verify
python tools/character_art/refine_ren_shag.py --capfit
python tools/character_art/refine_ren_shag.py --verify-selected
python tools/character_art/refine_ren_shag.py --verify-v2
```

The script's `TRIAL` constant selects the base directory, currently trial-v7. `--preview` creates front/quarter captures and a Blender scene; ordinary execution creates all four views and hair-only FBX/GLB. `--capfit` writes selected-v1 from that reviewed base and the accessory-only source. `--verify-selected` checks both exported cap states, source-relative local edit scope, UV/material/4K presence, unchanged head shapes, and artifact hashes. It writes the selected manifest.

All work runs in isolated background Blender. Original source files, frozen fitted-v5, Unity, and interactive Blender scenes are untouched. Earlier rejected trials stay on disk to explain the fixes. No final likeness, rigging, simulation, mobile performance, or artist-approval claim is made.

Selected-v2 is frozen for integration. `--verify-v2` checks it without resaving the blend. Construction switches `--fringe-v2` and `--select-v2` exist to reproduce its intermediate steps, but direct a future build into a new version directory before making further changes.
