# Complete native-H Ren head — initial assembly

This is the first complete **native H** head assembly for whole-character likeness review. The frozen construction asset is `Ren_H_CompleteHead_Review.blend` (SHA256 `216be081c450f81f60efc1ad1517b7c52246ecb7dd2afa97c1d6f06308615377`). It contains the material-ready native face, mouth controls and oral parts, separate eye assemblies, detailed hair, native ears/jewelry, concealed scalp/back support, and a separate toggleable cap attached through `Head/CapSocket`.

**This is a dense review prototype, with provisional eye seams and unfinished style/likeness. It is not a production 40k-triangle character or an iPhone performance result.** The first emission-shaded face looked washed out beside the lit hair and ears; [the original preview](evidence/initial-provisional-front.png) and its source metadata are retained as evidence. The current captures use the material owner's coherent three-tone derivative, with identical geometry/UVs/morphs. Exposed forehead/cheek extraction fragments and the rose pigment contour under closure remain review issues rather than hidden approval claims.

## Current complete-head views

| View | Cap on | Cap off |
| --- | --- | --- |
| Front | [PNG](cap-on-front.png) | [PNG](cap-off-front.png) |
| Quarter | [PNG](cap-on-quarter.png) | [PNG](cap-off-quarter.png) |
| True profile | [PNG](cap-on-profile.png) | [PNG](cap-off-profile.png) |

Basic controls: [native open A](controls-open-a-front.png), [full blink](controls-full-blink-front.png), [half blink](controls-half-blink-quarter.png), [gaze](controls-gaze-quarter.png). All ten actual 1100×1100 renders were visually inspected. [review-captures.json](review-captures.json) records exact source, camera, pose and image hashes; [review-observations.json](review-observations.json) records the worker's bounded findings.

The preferred **Blender lighting review** is the separate [coherent-tone source](../h-anime-paint-v1/whole-head-tone-study-v2/Ren_H_CoherentTone_Review.blend), SHA256 `aab20a4d6e51310af85b059952fb7699c5a97233d95f10cbd826234a18ff2f9b`. It preserves the construction geometry and supplies consistent light-driven bands. It is not numerically identical to Unity's URP NPR shader. The construction blend and portable geometry exports remain frozen at their recorded hashes.

## Verified portable assets

- **Open `Ren_H_CompleteHead_Portable.blend` for a self-contained Blender checkpoint.** SHA256 `317b8f7379dcf8d2fe34501385e1da7cd91b3d057008d488db0c65ed3727b8f5`, 38,050,551 bytes. All eleven image payloads are packed. Exact coordinates, topology, every shape endpoint/weight, UV corners, custom source-ID attributes, material assignments, hierarchy/matrices and driver expressions/targets match the frozen construction blend after save/reopen. [portable-blend-contract.json](portable-blend-contract.json) contains the per-mesh hashes and image audit.
- `Ren_H_CompleteHead_Review.fbx`: SHA256 `073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad`, 9,240,988 bytes.
- `Ren_H_CompleteHead_Review.glb`: SHA256 `6ace9af043ce8fcf12dd9da650565804172d81655b9836e47058facf0daf4254`, 37,490,928 bytes.
- [exports.json](exports.json) and [export-verification.json](export-verification.json): actual Blender reimports of both files, rather than exporter-success checks alone. Both retain **334,768 rendered triangles**, all mesh names and morph names, Head/CapSocket hierarchy and calibration markers. Maximum checked world-space morph error is below `4.48e-7` native authoring units.
- [material-contract.json](material-contract.json): exact texture files/hashes under `textures/`, material names, UV layer, color-space, culling and lid texture scale/offset. Unity should apply its NPR shader from this contract. Raw imported FBX materials are not the rendering authority.
- [control-contract.json](control-contract.json) and [hierarchy-contract.json](hierarchy-contract.json): semantic controls, nonlinear mappings, exact parent/local/world matrices, separate cap subtree and exported axis markers.

The export copy uses rough diffuse atlas carriers and removes unused authoring vertex colors so exported colors do not multiply the atlas twice. Original authoring materials, UVs and color attributes remain in the frozen Blender source. One FBX-specific problem was caught and corrected: an oral source retained native-open raw mesh positions while its evaluated `Basis` was closed. The export process now copies the actual `Basis` into raw mesh positions before export; it does not alter the frozen source, morph endpoints or source library.

The original `Ren_H_CompleteHead_Review.blend` is preserved as exact provenance, **but it is not independently portable**: main face, two cheek patches, iris and two eyelid images reference external authoring paths. The packed copy resolves those six images from this checkpoint's exact `textures/` bytes. It also retains five already embedded images, including the native ear normal/roughness maps that the simplified Unity material contract does not use. No linked Blender libraries exist in either checkpoint.

The portable copy was reopened in an isolated directory with **all eleven external image paths deliberately pointing to nonexistent files**. Every packed hash and decoded image dimension passed, and the actual [384px smoke render](evidence/portable-packed-smoke-front.png) shows the complete textured model with no missing-image surface. Blender emitted shutdown messages saying it kept packed images for missing external paths; the render and decoded-image checks passed. This small image uses the original mixed emission/PBR construction shaders and is only a portability check. The ten large coherent-tone images above still require their separate material-review source to reproduce exactly; packing does not improve or approve their style.

## Geometry and provenance

The assembly starts from `h-anime-paint-v1/final-material/Ren_H_NativeFace_MaterialReady.blend`, SHA256 `a0e1260bdbfbd85fa7797be0bd0745d1f963f4e8772a7dcdd0d713987ba1a405`. Its three face materials, exact retained `HNativeUV` corners, winding repairs, and native POINT/FACE source IDs are preserved. No P2 face or old reconstructed head replaces it.

Mouth data comes from the accepted `closure-assembly-contract-v2` / welded oral v9 study. The native skin displacement maps directly to **120,045 source vertex IDs** with zero native coordinate mismatch and no spatial fallback. The **334 generated cheek-repair vertices** use the face owner's explicit thin-plate boundary weights (`contact-repair-boundary-weights.npz`); exact float32 stored source positions establish correspondence. Native open-A positions return exactly, and cheek patch endpoints return to the frozen material-ready geometry. [mouth-integration.json](mouth-integration.json) records these checks.

Only the documented native oral face IDs are separated from the skin. Teeth, tongue, cavity and the newly constructed inner lip wall remain individual objects. The cavity and tongue each receive their own `jawOpen_A` motion; they do not receive the lip-seal field. Source contracts establish the tested oral boundary continuity; no jaw bone is introduced.

For eyes, v1 uses the exact source-ID removal of the accepted ContextFit study, intersected with the current face. Original remaining positions, source IDs, UVs and patch materials are retained. The eye's generated 64-point outer ring is **not yet joined by an accepted watertight transition**. The separate experimental seam study was visually rejected and is not included. Painted eyelids use their own `H_EyeLocalUV` texture and explicit scale/offset, never the face atlas applied to incompatible UVs.

Cleaned native ears use `h-native-ears-v1/strand-cleanup-v1/Ren_H_NativeEars.blend`, SHA256 `5cfd5b57ad8adf30f334b15fd21bc5e09ecb5c32dabcd1d98bc7c1cfc00948da`. The concha-crossing strand was removed by its owner; cropped root hair and source boundaries still need concealment/cleanup. Native ears and jewelry are extremely dense source geometry.

| Visible cap-on component | Triangles |
| --- | ---: |
| Native face, patched cheeks and neck | 195,660 |
| Teeth, tongue, cavity and inner lip wall | 21,470 |
| Separate eyes, eyelids and graphic lashes | 3,460 |
| Detailed hair and root base | 9,496 |
| Concealed scalp/back support | 933 |
| Separate cap | 1,110 |
| Native ears and ear jewelry | 102,639 |
| **Total, cap on** | **334,768** |

Cap off removes 1,110 visible triangles, for 333,658. Hair's `capOn` shape changes fit without changing its triangle count. These counts exclude any future body, clothes, shoes or other equipment.

## Controls and rig contract

`Ren_H_HeadControls` exposes `jawOpen_A`, `mouthSeal` and `capOn`. Given semantic opening `a` and seal `s`, the actual morph weights are **`jawOpen_A=a` and `mouthSeal=s*(1-a)`**. The default is `a=0, s=1`; open A is `a=1`, with actual seal zero. Apply the formulas to all relevant head/oral shapes listed in the contract, rather than mapping only the skin renderer.

`Ren_H_EyeControls` exposes left/right blink, wide, squint, and gaze. Blink correctives at 25%, 50% and 75% use the explicit hat weights from the eye contract; a 50% blink includes the full 50% corrective in addition to the 0.5 main blink. Wide/squint are attenuated by `1-blink`. The normalized ellipsoid gaze hierarchy and its static basis must remain intact. Export-only `Ren_H_GazeAxis_L/R_X/Y/Z` markers document imported local axes for the runtime.

The cap is already included as the separate subtree **`Head/CapSocket/CapAssetRoot/Ren_Cap`**. Do not instantiate the cap-only source FBX a second time. Toggle its renderer independently; drive `capOn` on the two hair objects as the fit adjustment. The cap stays an attachable asset, not merged into skin or hair geometry.

**Head and CapSocket are temporary bust transforms, not a new character rig.** No armature is added here. Ren's eventual body binding remains the one canonical `female_base_v2` rig; the project standard is one male and one female rig. [control-contract.json](control-contract.json) references that rig and explicitly marks the unfinished binding. Export-only `Ren_H_HeadAxis_X/Y/Z` markers preserve the prototype's native +X-forward/+Z-up frame through importer axis conversion. Native units have not been declared metres.

Full smile, lip curl, pucker, funnel, mouth lateral movement, brow acting, tongue protrusion and convergence controls are not part of this basic assembly yet. The video reference's unsupported channels must stay visibly unsupported until actual safe controls exist. This assembly does not establish full English/Japanese speech coverage or live latency.

## Reproduction

Opening the packed `.blend`, using the existing `.glb`, or importing the existing `.fbx` with the included texture/material/control contracts needs no external authoring assets. **Rebuilding from independently authored source parts is a different operation and is not self-contained in this checkpoint's staging list.** Before running the construction helper, obtain the frozen libraries in `assembly.json` plus these source inputs under `art/generated/characters/ren/parts-workflow-v1/`:

- `h-anime-paint-v1/final-material/Ren_H_NativeFace_MaterialReady.blend` and its original face/cheek images; `h-anime-paint-v1/lid-material/Ren_H_Eyes_Pigment.blend`, eyelid images and `lid-material-contract.json`.
- `h-expression-controls-v1/closure-assembly-contract-v2/mouth-control-contract.json`, `mouth-assembly-arrays.npz`, and its referenced `welded-oral-study-v9/Ren_H_MouthClosure_Study.blend`.
- `h-face-fit-v1/native-cleanup-v1/contact-repair-boundary-weights.npz` and `contact-repair-data.npz`.
- `h-eye-controls-v1/study-removed-source-face-ids.json`, `integration.json`, and original iris image.
- Hair, support, socket and cap libraries at the exact paths/hashes in `assembly.json`; `h-native-ears-v1/strand-cleanup-v1/Ren_H_NativeEars.blend` and `ear-contract.json`.
- For the current ten review images specifically, the separate coherent-tone v2 blend and any images it references. The canonical female rig contract is a future binding dependency, not required to open this unbound bust.

With those source dependencies present, run the owned helper using the local Blender installation. These are reconstruction commands that write the assembly outputs; use a separate working copy to preserve the frozen checkpoint:

```powershell
blender --background --threads 2 --python tools/character_art/assemble_ren_h_complete_head.py -- --inspect
blender --background --threads 2 --python tools/character_art/assemble_ren_h_complete_head.py -- --prepare-mouth
blender --background --threads 2 --python tools/character_art/assemble_ren_h_complete_head.py -- --assemble
blender --background --threads 2 --python tools/character_art/assemble_ren_h_complete_head.py -- --export
blender --background --threads 2 --python tools/character_art/assemble_ren_h_complete_head.py -- --render
```

`--render --review-source PATH --review-sha256 SHA` permits a verified material-only derivative without overwriting the frozen assembly. `--front-only` renders the first matched view. Frozen source hashes are checked; output is confined to this assembly folder. The intermediate material/mouth blend and native snapshot are reproducible construction artifacts, not separate candidate identities. The worker made no Unity edits, paid calls, staging or commits.

`verify_portable_blend.py` records the bounded packing/audit operation. It resolves unpacked images from the included material contract and texture bytes, preserves already packed payloads, and refuses to overwrite an existing portable output. Run it with Blender `--background --threads 2 --python` in a separate working copy if reproducing the packed artifact. Its deliberately broken-path test file is private under `.local/ren-h-portable-smoke/` and is not part of the milestone.
