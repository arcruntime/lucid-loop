# Unity FBX vertex-welding A/B

Turning vertex welding off did not recover the missing morph data. Unity 6000.3.24f1 imported fresh copies of the exact source `073e47cc…` and outline `3812d52d…` FBXs with otherwise identical importer metadata. No rendering or mesh repair was performed. Existing source imports, scenes and the historical eight-endpoint shell patch stayed unchanged.

| Input | Authored meshes / frames | Endpoint errors, weld on | Weld off | Zeroed moved endpoints, either |
|---|---:|---:|---:|---:|
| Complete H source | 14 / 70 | 1,876 | 1,876 | 1,873 |
| Outline morph meshes | 3 / 7 | 57 | 57 | 57 |

Tolerance is 2e-6 Blender shared-world units; physical units have not been independently established. Three source rows per case have movement at or below that tolerance but endpoint error slightly above it after Basis precision. They account for the difference between 1,876 and 1,873. All above-tolerance errors have zero imported delta; none has an incorrect nonzero imported delta. Largest source endpoint error is 0.001043215 on the inner lip wall. The earlier 56-row report was only the outline-visible head subset.

All full per-row error arrays are byte-identical between welding settings. Every failed authored endpoint/error is also identical. All authored Basis rows found an imported match and no best-match branch remained ambiguous. Matching considers Basis plus every shape jointly, rather than trusting imported vertex order or selecting the first coincident vertex.

`comparison-summary.json` is the original complete result. `comparison/` retains every failed endpoint, including authored/imported Basis and target values. `review-evidence.json` adds the independent A/B equality and residual classification. `imported/imported-manifest.json` records every raw output hash and importer setting. The four actual importer metas and tested editor/comparison/preparation sources are retained. Bulky full imported position arrays, error arrays and correspondence NPZ remain under `.local/ren-fbx-weld-ab-v1/`.

To reproduce, use a separate scratch copy of the Unity project and these exact FBXs, the existing source H marker hierarchy and the canonical payloads in `../canonical-source/` and `../../morph-supplement-contract.json`. Adapt only the explicitly workstation-specific project/repository roots in the supplied preparation/comparison scripts and scratch-path guard. Place `RenFbxWeldAudit.cs` in the scratch Editor folder, run `prepare_fbx_weld_audit.py`, then invoke Unity batch mode with `-executeMethod LucidLoop.CharacterArt.Editor.RenFbxWeldAudit.Run`. After Unity exits, run `compare_fbx_weld_audit.py`, then `curate_fbx_weld_audit.py`. Python requires NumPy and SciPy. Do not run this in the open main project.

This rules out disabling welding as a fix for these exact inputs/settings. It does not establish the importer root cause, restore canonical morphs or approve a production import policy. No new-eye source or neutral material comparison was changed by this experiment.
