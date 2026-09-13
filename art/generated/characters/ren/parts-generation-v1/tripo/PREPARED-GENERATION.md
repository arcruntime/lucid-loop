# Ren separate head and hair: Tripo P2 generation

Prepared and completed 2026-09-13. **Both reviewed geometry-only jobs succeeded for 100 credits each, 200 total.** Hair task `1a8b7598-cddf-4b6c-a344-9e9ae16dcd5d` produced `hair/originals/model.fbx`; head task `91cffa7c-85c1-4bd0-a680-5d60ac518631` produced `head/originals/model.fbx`. Native FBX files and provider previews are preserved unchanged. See the [hair receipt](hair/receipt.json), [head receipt](head/receipt.json), [hair source provenance](hair/provenance.json), and [head source provenance](head/provenance.json). The `prepared-head.json` and `prepared-hair.json` files are historical recipe templates, not current task status. This separate-parts experiment is distinct from the completed Studio bust jobs.

Neither generated part is accepted as final production art or animation-ready topology. Initial provider-preview review found fine outer filaments and a central hanging clump in the hair; the head's mouth aperture appears narrower than the approved open-mouth input, with strong raised brow planes and filled-looking eye surfaces. These are targets for the native Blender audit, not conclusions about hidden geometry. No reroll, vendor conversion, or texture task was run.

The authenticated official CLI reported **2,600 available API credits, 0 frozen** at **2026-09-13T07:37:50.266561Z**. The separate immutable check is in `../../bust-comparison-v1/tripo/cli-balance-funded-20260913.json`; the earlier zero-balance evidence remains unchanged. Credentials came from BWS through the child environment and were not logged.

## Selected generation

| Setting | Hair-free head | Separate hair |
| --- | --- | --- |
| Equivalent CLI alias (reference only) | `tripo-p2` | `tripo-p2` |
| Requested wire model | `P2-20260801` | `P2-20260801` |
| `quad` | `true` | `true` |
| `face_limit` | `10000` | `25000` |
| `texture`, `pbr` | `false`, `false` | `false`, `false` |
| `export_uv` | `true` | `true` |
| `model_seed` | `20260913` | `20260914` |

P2 is currently Preview. Its official multiview API permits up to 25,000 quad faces. Front is mandatory, with at least one other true side/back view. `export_uv` is documented independently of texture generation, with no texture-enabled prerequisite. We request UVs on the bare geometry and verify actual returned UV layers. By contrast, `orientation=align_image` is explicitly ineffective without textures, so it is omitted. [Official P multiview API](https://developers.tripo3d.ai/en/docs/generation-multiview-to-model/p).

No `geometry_quality`, `smart_low_poly`, or `generate_parts` is sent: P2 does not support them. Each part gets its own generation. No scenario preset, texture step, rigging chain, decimation, or provider conversion is appended. `export_orientation` stays unset to avoid the documented downstream orientation issue. These are source geometry budgets; neither budget establishes mobile performance or usable facial deformation loops.

**Geometry review comes before texturing.** Inspect the head silhouette, eye sockets, iris construction, lips, mouth interior, and polygon layout first. HOS projection is the planned texture route on an accepted UV-mapped head. If UVs are absent, record the required UV work; do not enable paid texture generation implicitly. Vendor 8K is available as a later, separately chosen experiment if useful.

## Input binding

The submitted sources are standalone reviewed images from `art/generated/characters/ren/parts-reference-v1/`. Head uses `head-front-open-a.png` and `head-right-open-a.png`; hair uses `hair/generation-inputs/hair-front-v2.png`, `hair-right-v2.png`, and `hair-back-v2.png`. Exact source paths, byte counts and hashes are recorded in each part's provenance. No full multi-drawing sheet was submitted. Unset source fields in the historical prepared templates describe their pre-review state only.

The dedicated launcher binds `--front`, `--right`, and optional `--back` directly to the API view keys, so it does not depend on filename inference. Our inspected screen-right-facing profile matches the Studio UI's RIGHT illustration; this is established UI evidence, not an independently verified anatomical naming rule. Three-quarter views remain review references. For any future use of the official CLI to create multiview assets, its filename inference falls back to front/left/back/right positional order when directional hints are missing.

## Reviewed-input launcher

`tools/character_art/tripo_parts_jobs.py` requires explicit reviewed front/right paths and a review note. Optional `--back` adds a true back view. Omitting `--submit` prepares immutable input copies, hashes and settings without reading credentials, uploading images, or creating a task. The historical JSON templates target this dedicated launcher; `tripo_cli_bws.py` keeps its existing read-only guard.

The original submission command had this form. These two part slots are already in use; do not run another `--submit` for them. Use the recorded task and `--resume` for any interrupted download.

```powershell
python tools/character_art/tripo_parts_jobs.py --part head --front REVIEWED_FRONT.png --right REVIEWED_RIGHT.png --review-note "Root reviewed these exact standalone inputs." --submit
python tools/character_art/tripo_parts_jobs.py --part hair --front REVIEWED_FRONT.png --right REVIEWED_RIGHT.png --back REVIEWED_BACK.png --review-note "Root reviewed these exact standalone inputs." --submit
```

The installed official CLI 0.4.0 retries transient HTTP failures, including task-creation POSTs, without exposing a generation retry switch. To prevent duplicate billable requests, this launcher uses the existing non-retrying `TripoApi.request` for exactly one V3 generation POST. It flushes an exclusive submission intent before POST and saves the returned task ID immediately. API authentication comes from BWS in memory; the key is never in command arguments. Official CLI is used only for version/doctor and blocking `task watch --download`, with no shorter wrapper timeout.

Private state, staged inputs, upload tokens, raw CLI logs, task metadata and CLI history/context are confined to ignored `.local/character-art/tripo-parts-v1/<head|hair>/`. Supported `TRIPO_HOME` selects that job's private CLI state; the user's account profiles are untouched. Public provenance and receipts remove credentials and signed URL queries. Provider files are copied byte-for-byte to this directory's `<head|hair>/originals/` and verified by SHA-256.

Resume a known task with `python tools/character_art/tripo_parts_jobs.py --part head --resume`; this never creates another task. An interrupted create with no saved ID remains blocked until an exact API task is identified and reconciled using `--reconcile-task-id ACTUAL_ID --reconciliation-note "How the task was identified"`, then `--resume`. Reconciliation authenticates the task identity/type and rejects conflicting echoed parameters. It records the operator's source association explicitly. A stale invocation lock after a hard process crash requires checking that its process ended before removing that exact lock file; the immutable `submission-intent.json` must remain. Existing Studio task IDs are unrelated to this batch.

Focused mocked checks: creation timeout issues exactly one underlying POST even after rerun; input identity/settings cannot drift; public output is redacted; native FBX copies match their original bytes; paid CLI commands are rejected and polling has no wrapper timeout. No paid operation runs in these tests.

## Native outputs and review derivatives

The installed official Tripo skill and CLI docs state that `quad=true` forces **FBX** output. Preserve every downloaded native artifact byte-for-byte, including the FBX and preview; retain any original GLB/OBJ or maps if the provider also returns them. Hash the files before further work.

Inspect FBX polygon sizes in Blender and report actual triangle/quad/n-gon counts and UV layers. Export a local OBJ preserving the imported polygons and a triangulated GLB review derivative; label both as local derivatives, not provider originals. Do not add a new quad conversion/remesh step to manufacture the topology being evaluated. Native quads alone do not establish closed eyelids, useful lip loops, mouth interior, or blendshape suitability.

## Cost estimate

The two actual receipts report **100 credits for hair and 100 for head, 200 credits total**. No additional quad surcharge was charged for these jobs. At the official list rate of one cent per credit, that consumption corresponds to $2.00. The pre-batch balance was 2,600 credits. [Official pricing](https://developers.tripo3d.ai/en/pricing).

No paid provider format conversion is needed for the proposed local derivatives. If later requested, basic/advanced conversion is separately priced at 5/10 credits. Failed or cancelled jobs release their frozen credits. [Official billing](https://developers.tripo3d.ai/en/docs/billing).

Local official evidence: installed `tripo-3d@openai-curated-remote` 0.2.1 skills, CLI 0.4.0 previously verified; fresh CLI generate/make documentation records are in ignored `.local/tripo-parts-docs-generate-20260913.json` and `.local/tripo-parts-docs-make-20260913.json`.
