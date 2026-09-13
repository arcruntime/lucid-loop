# Meshy Ren bust comparison

Completed 2026-09-13. Both reviewed expression references now have original native geometry and textured 8K results.

| Candidate | Textured result | Native source triangles | Textured triangles | Vertices after UV splitting |
| --- | --- | ---: | ---: | ---: |
| Closed rest | [model.glb](closed-rest-8k/model.glb) | 3,051,192 | 3,028,819 | 1,708,194 |
| Open A | [model.glb](a-open-8k/model.glb) | 2,558,256 | 2,439,591 | 3,663,151 |

The successful route is **single-image Meshy 7 Ultra/native generation followed by separate Meshy 7 8K multiview texturing**. Geometry uses the reviewed front only; texturing uses front and profile. This is a best-output comparison with disclosed input differences, not a controlled multiview benchmark. The original untextured GLBs remain in `closed-rest-geometry-single-image` and `a-open-geometry-single-image`.

Use each textured result's **external `texture-0-base-color.png`** in Blender/Unity: it is a true 8192 × 8192 PNG, whereas the GLB embeds a JPEG of the same resolution. Both models have one material (index 0), whose base-color texture index and embedded image index are also 0. External normal, metallic, and roughness PNGs are 4096 × 4096. Preserve these original files; do not re-encode the embedded JPEG as the source texture.

[results-manifest.json](results-manifest.json) records all completed task IDs, source and output hashes, actual dimensions, geometry counts, and material binding. The four successful stages consumed 80 credits total. Six failed diagnostic attempts were refunded; their exact settings and errors are recorded in [SERVICE_DIAGNOSTICS.md](SERVICE_DIAGNOSTICS.md).

## Quality configuration

Both generation tasks explicitly request `meshy-7`, `ultra_mode: true`, `should_remesh: false`, and `image_enhancement: false` through the [Image-to-3D API](https://docs.meshy.ai/en/api/image-to-3d). Both texture tasks explicitly request Meshy 7, 8K, and PBR. The service echoes Ultra and texture resolution but omits served-model identity, so Meshy 7 is the recorded explicit request rather than independently verified routing.

Native geometry has no UVs, so texturing uses `enable_original_uv: false` to create them. No client decimation or pose conversion was performed. Meshy's texture stage recenters and scales the model and removes some source triangles (0.73% closed rest; 4.64% open A). Every sampled textured vertex in each deterministic 100,000-point check matches an original source vertex within one millionth of bust height after uniform alignment. This supports preserved sampled vertex positions; triangle connectivity is not certified identical. See each result's `geometry-stage-comparison.json` for measurements and limitations.

## Commands

Use a new directory for any new paid candidate. The successful two-stage route is:

```powershell
python tools/character_art/meshy_bust_jobs.py --job-dir NEW_GEOMETRY_DIR --front FRONT.png --expression closed-rest --geometry-only --minimal-export --single-image --submit --max-polls 20
python tools/character_art/meshy_bust_jobs.py --job-dir NEW_TEXTURE_DIR --front FRONT.png --profile PROFILE.png --expression closed-rest --retexture-source NEW_GEOMETRY_DIR --submit --max-polls 30
```

Use `--dry-run` instead of `--submit` to inspect a new request first. Resume an existing task without input flags or `--submit`:

```powershell
python tools/character_art/meshy_bust_jobs.py --job-dir EXISTING_JOB_DIR --max-polls 20
```

The runner reads `MESHY_API_KEY` from BWS in memory, saves task identity before polling, and never retries ambiguous paid submission. `task-state.json`, `generation-provenance.json`, and `asset-inspection.json` preserve requested settings, returned model identity when available, input hashes, download hashes, geometry counts, and actual embedded/standalone texture dimensions. Signed URLs and credentials are excluded.

The generation is an appearance comparison source. It does not automatically provide animation topology, separate eyeballs, or production speech blendshapes.
