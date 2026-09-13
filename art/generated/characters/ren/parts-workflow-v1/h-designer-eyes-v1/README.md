# Ren designer-contour eye prototype

Current review handoff: **[trial-v6/NEUTRAL_REVIEW_HANDOFF.md](trial-v6/NEUTRAL_REVIEW_HANDOFF.md)**. Root selected its frozen neutral geometry for a scratch Unity material comparison. No artist-likeness, production, blink or performance acceptance is implied.

This new construction follows Ren's designer-drawn opening, large covered iris, heavy upper ink, temporal lashes and smoky lid relationships. Unity-chan supplied implementation evidence for separate shallow layers; its identity and the rejected H ellipsoid lids were not used as geometry templates. Accepted H mouth, nose, cheeks and jaw remain fixed.

| Trial | Actual state |
| --- | --- |
| v1 | Initial quadratic depth fit caused backing/iris/ink intersections; rejected mechanically. |
| v2 | Common plane fixed sampled source depth ordering, but actual front view exposed a major width imbalance and pale backing chips. |
| v3 | Shallow frontal plane and opening-bound backing fixed sampled aperture visibility. Pigment integration exposed incomplete generated boundary attachment. |
| v4 | Native-H parameterization and CDT original-input identities preserve all 274 L / 299 R actual boundary vertices and edges exactly. Sampled H pigment integrated. First coherent shallow working construction, not likeness approval. |
| v5 | Skin-attached ink/crease restores the visible inner-corner tail; stronger moving smoky pigment. Temporal spans still read as broad prongs. |
| v6 | Finer separated temporal strokes and visible ink negative space. Selected only for frozen neutral scratch integration; eye-only exports and fresh import proof supplied. |

The frozen accepted context and exact source-camera solve are recorded in this package. Main portrait eye placement is aligned with one orthographic camera and uniform scale; the full face is not declared aligned, and no independent X/Y warping is used. Hair-hidden source diagnostics distinguish actual eye visibility from current hair overlap.

`boundary-pigment-v1/` is the root-authored original-H sample handoff. It is preserved. Prior rejected eye packages remain outside this folder as history. Reproduction helper: `tools/character_art/build_ren_designer_eyes.py`.

## Reproducibility and missing-source limits

The checkpoint includes the frozen v4-to-v6 inputs and v6 export replay. **It is not a self-contained end-to-end fresh-checkout reconstruction of the full H source, calibration and reference audit.** Run replay commands in a separate working copy; they write the named trial outputs. Keep the reviewed checkpoint immutable.

Direct v6 replay uses:

- `trial-v4/Ren_DesignerEyes_Pigment.blend`, SHA `23b32084c563a751134f5f93df7325627996ebbcda7dbb80e5e364f3c0f708f7`, plus `trial-v4/construction.json`. The pigment report is provenance; v1-v3 and v5 outputs are not required.
- `portrait-camera-fit-v1.json`, the reviewed `h-eye-design-audit-v1/artist-layered-contours-refined-v1.json`, and `h-eye-controls-v1/seam-contract-v1/native-study-cut-boundaries.json`.
- **External full-source dependency:** `h-complete-head-v1/Ren_H_CompleteHead_Portable.blend`, SHA `317b8f7379dcf8d2fe34501385e1da7cd91b3d057008d488db0c65ed3727b8f5`. It is not included in this neutral-eye checkpoint; the builder requires its exact presence for source-immutability checks.
- `art/characters/ren-model-sheet.png` for source comparison plates, and `tools/character_art/build_ren_designer_eyes.py`.

The tested Blender is **5.2.1 LTS, build `9e2066aef7ef`**, at `C:/Users/jetha/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe`. Blender operations use its bundled NumPy and mathutils. External plate/curve tooling was run with Python 3.11.9, NumPy 2.4.3, SciPy 1.17.1 and Matplotlib 3.10.9. Rebuilt geometry/color can be compared against the recorded fingerprints; byte-identical `.blend`/FBX container hashes are not promised across rebuilds or application versions.

From the repository root in the replay copy:

```powershell
$renBlender = 'C:/Users/jetha/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe'
$renTool = 'tools/character_art/build_ren_designer_eyes.py'
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --refine-layers-v5 --fan-v6
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --render-prototype --fan-v6 --pigment
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --distinct-layer-mask --fan-v6
python $renTool --prototype-plate --fan-v6
python $renTool --layer-review --fan-v6
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --profiles-only --fan-v6
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --export-neutral --fan-v6
& $renBlender --background --threads 2 --python-exit-code 1 --python $renTool -- --verify-portable-only --fan-v6
```

`--refine-layers-v5` retains its historical operation name; `--fan-v6` selects the actual v6 branch and output directory. The export operation writes `trial-v6/neutral-export-v2`. Each Blender process should finish successfully before the next dependent command. These commands do not perform Unity integration.

To regenerate v4 instead of using its included frozen pigment blend, first provide the full accepted H source above and the included boundary-pigment JSON. Run `--inspect-source`, then `--build-prototype --boundary-v4 --no-render`, then `--apply-boundary-pigment --boundary-v4`. Inspection generates the accepted native snapshot used by the v4 builder. Do not re-solve the reviewed camera as part of neutral replay.

Regenerating the **original boundary sampling/calibration/reference audit** has additional dependencies that are not all included: original H `bust-comparison-v1/tripo/studio-h3.1-a-open/ren-tripo-studio-h3.1-a-open-original-8k.glb` (SHA `9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406`), its `embedded-original-textures/image-0.jpg` (SHA `9750e636f595a01e2c1bfd00699b5bcd083e8f99a39a43adf8b1da68436e09ce`), the full accepted H portable context, and `third_party/unity-chan/original-unity-chan.zip` / its extracted FBX and shader/script references. The root sampler also imports `tools/character_art/audit_ren_tripo_texture.py`; reference-audit diagnostics may require the preserved earlier H eye study assets. The frozen traced curves, camera, source-boundary data and sampled pigment are the authority for this replay; this README does not claim that every earlier audit can be regenerated from the neutral checkpoint alone.
