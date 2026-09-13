# Tripo Studio comparison export handoff

Updated 2026-09-14 JST. All four requested geometry assets were generated in the same user's signed-in Tripo Studio account. H closed-rest and H A-open are now downloaded. **Do not repeat generation to recover the remaining two assets.**

| Variant | Studio asset | Local state |
| --- | --- | --- |
| H closed-rest | [67b0819b-9627-444c-aa7d-5785b46ef026](https://studio.tripo3d.ai/workspace/generate/67b0819b-9627-444c-aa7d-5785b46ef026) | Complete original GLB, embedded textures, metadata, screenshots and provenance in `studio-h3.1-closed-rest/` |
| H A-open | [4dd5c828-eed8-4ded-9aa2-b2c3ef66c6be](https://studio.tripo3d.ai/workspace/generate/4dd5c828-eed8-4ded-9aa2-b2c3ef66c6be) | Original 8K GLB recovered; **user-selected cheek/jaw shape reference**. [Source and export receipt](studio-h3.1-a-open/README.md) |
| P2 closed-rest | [ece79f26-5f86-46ee-87b1-25647bf685ed](https://studio.tripo3d.ai/workspace/generate/ece79f26-5f86-46ee-87b1-25647bf685ed) | Completed thumbnail verified; original export and actual texture resolution pending. `studio-p2-closed-rest/provenance.json` |
| P2 A-open | [da64bb0e-6e34-4f1e-a0bf-aafeaa660870](https://studio.tripo3d.ai/workspace/generate/da64bb0e-6e34-4f1e-a0bf-aafeaa660870) | Completed thumbnail verified; original export and actual texture resolution pending. `studio-p2-a-open/provenance.json` |

## Actual settings and credit observations

H displayed `v3.1 – Best Quality`, with Ultra Mesh Quality, Triangle, 2,000,000 polycount, 8K texture, PBR, Remove Lighting and Private. Its exact dated served model was not disclosed. H closed-rest consumed the available 0-credit trial; H A-open displayed 65 credits and balance fell from 24,980 to 24,915.

P2 displayed `P2.0 - Preview`. Quad mode supports a displayed maximum of 25,000; both jobs were configured to that maximum and Private. P2's generation form exposes only Topology, with **no texture-quality or PBR controls**. Do not claim that these two assets contain 8K textures until their actual exports have been inspected. If untextured or lower-resolution, a separate native Studio Texture/8K pass may be required. The coordinating agent authorized the remaining highest-quality outputs and necessary provider-specific workflow differences.

P2 had two available trials, each showing 100 struck through and 0. Both were used; the last observed balance remained 24,915. No separate transaction receipt was available. The later Generate control showed 100 struck through and 65.

Navigating back to a blank generation form and selecting P2 restored its 5,000 default despite the dialog's general save-settings wording. Set or verify the maximum for every new preparation. P2 A-open's 25,000 maximum was verified and captured **after** both references were loaded and immediately before submission (`ready-topology-ui.png`). P2 closed-rest was set to 25,000 before switching input mode and loading references; verify its returned polygon count and Property settings rather than silently assuming the maximum persisted. Preserve any original result if a correction is needed.

## Export recovery

H A-open was recovered on 2026-09-14 JST through a fresh supported browser tab
and the normal visible Export dialog, retaining GLB and `8k Current`. No new job
was generated. Its original 58,961,168-byte GLB is preserved with SHA-256
`9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406`.
It contains 1,934,041 triangles, matching the Studio display, and 1,031,818
accessor vertices versus 1,031,819 displayed. The base-color image is 8192-square;
the other two maps are 4096-square. The previous stalled-export history below
explains the earlier missing source; it is no longer a blocker for H A-open.

The owned Studio tab was `1432647605`. No parent Stripe or existing user Studio tab was touched. The first H closed-rest export succeeded through the normal visible Studio **Export** dialog: name the file, choose **GLB**, retain **8k Current**, then Export. Chrome downloaded into `C:/Users/jetha/Downloads/`; the artifact was copied byte-for-byte into the project and SHA-256 verified.

H A-open's completed asset was opened, but its main viewport remained on a loading spinner for several minutes. Screenshot and locator calls subsequently hung and reset the automation session after 60 seconds. Navigation back to the blank generation form also timed out after 120 seconds. The last fresh documented recovery could no longer complete `browser.nameSession()` / `browser.tabs.list()`. No source export was silently treated as downloaded. No additional paid job was submitted during recovery.

Prefer a card-level export action from the visible Assets panel if it avoids loading the heavy H preview. Inspect available actions before clicking. For P2, preserve a native **OBJ and/or FBX** export with original polygons as well as the GLB used for Unity review. GLB triangles and `FB_ngon_encoding` are not proof that Blender's importer retains the original quads. Count the original polygons after download.

Do not inspect hidden application/session state, cookies or auth tokens. Do not fetch Studio resources through a separate authenticated script. Continue through the supported browser runtime or a user-completed download. Downloading an existing generated asset does not require a new cost approval.

## Already verified H closed-rest original

`studio-h3.1-closed-rest/ren-tripo-studio-h3.1-closed-rest-original-8k.glb`

- 56,769,408 bytes; SHA-256 `4743a85883df56b2adcca72ed9f429eb537d92b93eff28eb2495966e95a39f0a`.
- 1,859,218 triangles and 988,591 GLB accessor vertices. The Studio UI displayed 988,593 vertices; both observations are recorded separately.
- One mesh / one primitive; no Draco extension. Asset generator is `Tripo`; extensions include `KHR_materials_volume` and `FB_ngon_encoding`.
- Actual embedded base-color JPEG is 8192×8192. Normal and packed roughness/metallic JPEG maps are each 4096×4096.
- Extracted images are untouched original embedded bytes in `embedded-original-textures/`. Details and hashes are in `glb-inspection.json`.

## Shared source convention

All submitted jobs use exact reviewed standalone portrait PNGs from `art/generated/characters/ren/head-reference-sheets-v1/generation-inputs/`: front and **RIGHT** profile. The live Studio RIGHT reference icon faces screen-right, matching the supplied profile. See `SIDE-CONVENTION.md` for evidence and the preserved earlier unsubmitted API LEFT assumption.

The API remains a separate route. The old prepared API LEFT records were never submitted, and corrected `-right-v2` records are also prepared only. Do not run an API counterpart while recovering a completed Studio asset.
