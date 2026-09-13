# Tripo Ren bust comparison: API choices

Checked 2026-09-13 against current official Tripo Developer documentation. No generated bust has yet been accepted for production. All four standalone inputs have been visually reviewed. API funding was unavailable when production began, so the first closed-rest H job is being generated through the same user's signed-in Tripo Studio credits; its provenance is in `studio-h3.1-closed-rest/`. Studio displays `v3.1 – Best Quality`, not an exact dated served-model identity.

The live Studio multiview UI established **RIGHT** for the supplied screen-right-facing profiles. See `SIDE-CONVENTION.md`. Four corrected API request records end in `-right-v2`; they are prepared only, with no uploads or paid submission. The original four prepared records preserve the earlier LEFT assumption as history and must not be submitted unchanged. Do not submit any API counterpart while its Studio generation is pending without coordinating the batch.

## Approved comparison

| Job | Model requested | Geometry | Texture |
| --- | --- | --- | --- |
| `h3.1-closed-rest` | `v3.1-20260211` | Ultra, up to 2,000,000 triangles | Extreme / 8K, PBR |
| `h3.1-a-open` | `v3.1-20260211` | Ultra, up to 2,000,000 triangles | Extreme / 8K, PBR |
| `p2-closed-rest` | `P2-20260801` | Up to 25,000 quads | Extreme / 8K, PBR |
| `p2-a-open` | `P2-20260801` | Up to 25,000 quads | Extreme / 8K, PBR |

The H-series page identifies H3.1 as its latest quality model. `geometry_quality=detailed` selects Ultra; its documented triangle ceiling is two million. `texture_quality=extreme` requests 8K textures. We disable `smart_low_poly`, request original-image texture alignment, retain UVs, and omit compression. The face limit is a maximum, not a promise of exactly that number of faces. Preserve the provider's downloaded geometry unchanged; any mobile derivatives belong elsewhere. [Official H multiview API](https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/standard).

P2 is the newest P-series model, marked Preview. This family aims at clean low-poly topology. The quad ceiling is 25,000; triangle mode permits 50,000. Extreme textures are also available. Comparing P2 separately avoids implying that it has the highest raw geometry ceiling. It is relevant to later facial editing, but generated topology and mouth construction still require inspection. We do not pass H-only geometry quality options to P2. [Official P multiview API](https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/p). P2 was added to the API in August 2026. [Official changelog](https://developers.tripo3d.ai/en/docs/changelog).

## Image contract

Use separate unlabelled portrait images, with matching neutral lighting and the same expression. Front is mandatory; one or more true side/back images must accompany it. The API accepts exactly the directional keys `front`, `left`, `back`, and `right` (or four positional slots). It has no three-quarter slot. Our three-quarter sheet view is for artistic review, not a disguised side or back input. The precise side label must be determined from the inspected profile and provider convention; image-facing direction alone should not be described as a verified anatomical side. [Official H multiview API](https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/standard).

Images upload to `POST https://openapi.tripo3d.ai/v3/files` as multipart form data. The documented image formats are PNG/JPEG, with a 20 MB ceiling. Upload tokens subsequently appear inside directional input objects. [Official upload API](https://developers.tripo3d.ai/en/docs/files).

## Reproduction and provenance

`tools/character_art/tripo_bust_jobs.py` reads `TRIPO_API_KEY` from BWS into memory. It never places that key in command arguments or logs. Both presets fix geometry and texture seeds to 20260913; seeds do not make outputs comparable across different model families.

The runner creates a unique job state before a paid POST. It never repeats a creation after an ambiguous response. Resuming a task uses its saved task ID; an interrupted submission requires reconciling the provider task history before explicit adoption. It verifies that input hashes have not changed and that already downloaded output hashes still match. Raw upload tokens and signed download links remain in ignored `.local/character-art/tripo-bust-comparison-v1/` state; public request/response records remove authentication data and URL queries.

Each job directory contains the requested settings, source paths and SHA-256 hashes, sanitized response, task ID, any returned model-identity fields, credit consumption when reported, and downloaded file hashes. A requested model is not silently relabelled as a confirmed served model when the response omits that information. The task query reports success/failure and output URLs. [Official task API](https://developers.tripo3d.ai/en/docs/task-query).

Example after input review:

```powershell
python tools/character_art/tripo_bust_jobs.py --job-id h3.1-closed-rest-right-v2 --submit --wait-seconds 3600
```

Resume with the same `--job-id` and `--wait-seconds 3600`; input paths and preset are optional on resume. Omitting `--submit` for a new job prepares provenance without uploading or creating a task.
