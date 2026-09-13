# Multiview side-slot evidence

Checked 2026-09-13. **The current signed-in Studio UI establishes that its RIGHT slot expects a profile whose nose points screen-right.** The live multiview panel has labelled Front / Left / Right / Back tiles. Its visible Left reference icon faces screen-left; its visible Right reference icon faces screen-right. Read-only inspection of the rendered icon masks confirms that the Right illustration places its eye near x=13.412 and nose near x=15.6 in its 20 px icon, while the Left illustration places the eye near x=6.592 and nose near x=4.4. This is a UI view convention, not a claim about the anatomical side of the character.

The reviewed `closed-rest-profile.png` faces screen-right and was uploaded to **RIGHT** for Studio asset `67b0819b-9627-444c-aa7d-5785b46ef026`. Parent reviewed and released this mapping before submission. No additional side-comparison generation was needed to establish the UI convention. The live UI evidence supersedes the unresolved historical research below.

The four original API prepared records (`h3.1-closed-rest`, `p2-closed-rest`, `h3.1-a-open`, `p2-a-open`) used an explicitly recorded LEFT assumption before the live UI was available. They were **never submitted**. They are retained as historical prepared records and must not be submitted unchanged; a new RIGHT request version is required for any future API run.

## Earlier research, preserved for provenance

The official public sources reviewed initially did not establish which API side-key corresponded to a portrait whose nose points screen-right.

Confirmed API contract: keyed inputs support front, left, back, right; the positional representation uses that same order. Three-quarter is not a supported direction. [Multiview API](https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/standard).

The following official visual references were inspected:

- [Tripo's image-versus-text article](https://www.tripo3d.ai/blog/how-to-choose-from-image-text-to-3d) includes a Studio screenshot of Mario. From left to right its tiles show front, nose screen-left, nose screen-right, back. **The tiles are unlabelled**, so their order cannot safely be mapped onto API order. [Original screenshot](https://tripo-cdn.holymolly.ai/blog/5/c/5c03cbe4-f647-44f8-b1c3-7ad9bfd8da6b.png).
- [Tripo's scale-consistency article](https://www.tripo3d.ai/blog/how-to-keep-consistent-when-generating-multiple-models-with-ai) includes another Studio screenshot with the same visual ordering and no direction labels. [Original screenshot](https://tripo-cdn.holymolly.ai/blog/5/9/59ab464d-4717-438d-8cbf-866d62867def.png).
- [Official ComfyUI workflow](https://github.com/VAST-AI-Research/ComfyUI-Tripo/blob/main/workflows/multiview_to_model.png) exposes `image`, `image_left`, `image_back`, and `image_right` sockets. Its input thumbnails are blank, so it supplies no facing-direction evidence.

A separate browser research tab was opened for the Studio UI, but navigation timed out before its view-slot illustrations could be inspected. No account storage, cookies, keys, private application state, or existing billing tabs were inspected. No paid task was submitted for this investigation.

If the current Studio UI cannot supply a labelled example, record the selected slot as an explicit convention assumption. An additional H3.1 closed-rest generation with the opposite side-slot would give an empirical comparison using identical images/settings; it would add one generation to the approved four-job batch. Inspect asymmetric hair, facial continuity, and the reconstructed profile before using that result to settle the remaining submissions.
