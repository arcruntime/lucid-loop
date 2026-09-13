from pathlib import Path
import json,hashlib,shutil
import numpy as np
repo=Path(r'B:\lucid-loop'); project=Path(r'C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification')
run=repo/'.local/ren-fbx-weld-ab-v1'
out=repo/'art/generated/characters/ren/parts-workflow-v1/h-tokon-outline-v1/import-audit/unity-weld-ab-v1'
out.mkdir(parents=True,exist_ok=True)
load=lambda p:json.loads(p.read_text(encoding='utf-8')); h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
summary=load(run/'comparison-summary.json'); checks=[]
for kind in ['source','outline']:
 a,b=[next(x for x in summary['cases'] if x['id']==kind+'-weld-'+flag) for flag in ['true','false']]
 for ma,mb in zip(a['meshes'],b['meshes']):
  assert ma['mesh']==mb['mesh']
  pa=run/ma['failureFile'];pb=run/mb['failureFile'];fa,fb=load(pa),load(pb)
  assert len(fa)==len(fb)
  for ra,rb in zip(fa,fb):
   assert {k:v for k,v in ra.items() if k not in ['importedVertex','candidateCount']}=={k:v for k,v in rb.items() if k not in ['importedVertex','candidateCount']}
  for frame in ma['frames']:
   name=ma['mesh']+'--'+frame['shape']+'-errors.f32'
   assert (run/'comparison'/a['id']/name).read_bytes()==(run/'comparison'/b['id']/name).read_bytes()
  checks.append({'kind':kind,'mesh':ma['mesh'],'allPerRowErrorArraysByteIdentical':True,'allFailureEndpointsAndErrorsIdentical':True})
for rel in ['comparison-summary.json','input-protection.json','audit-input.json','imported/imported-manifest.json','RenFbxWeldAudit.cs','RenFbxWeldAudit.cs.meta']:
 src=run/rel;dest=out/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
for case in summary['cases']:
 for m in case['meshes']:
  p=Path(m['failureFile']); dest=out/p;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(run/p,dest)
for name in ['prepare_fbx_weld_audit.py','compare_fbx_weld_audit.py','curate_fbx_weld_audit.py']:
 shutil.copy2(repo/'.local/ren-h-reference-preparation'/name,out/name)
(out/'importer-metas').mkdir(exist_ok=True)
for p in (project/'Assets/CharacterArt/Generated/RenFbxWeldAudit').glob('*.fbx.meta'):shutil.copy2(p,out/'importer-metas'/p.name)
residual=[]
for case in summary['cases']:
 for m in case['meshes']:
  for row in load(run/m['failureFile']):
   if not row['zeroed'] and row['importedDeltaMagnitude']<=1e-10:
    residual.append({'case':case['id'],'mesh':m['mesh'],**row})
assert len(residual)==6
report={'status':'WELDING_OFF_DOES_NOT_CHANGE_OBSERVED_IMPORT_LOSS','unityVersion':summary['unityVersion'],'comparisonScope':'Numeric import only; no rendering, repair, main-project changes or production acceptance.','onlyModelImporterSettingChanged':'weldVertices; copied assets also require distinct GUIDs','originalInputAndSceneHashesUnchanged':True,'cases':[{k:v for k,v in c.items() if k!='meshes'} for c in summary['cases']],'pairEquality':checks,'nearThresholdResiduals':residual,'residualExplanation':'Three rows per source case have exactly zero imported deltas and authored movement <=2e-6; their endpoint error is slightly above 2e-6 after Basis precision. These are included in 1876 total errors but excluded from 1873 zeroed moved-above-tolerance rows.','conclusion':'Welding on/off produces byte-identical full per-row error arrays and identical failed authored endpoints in these inputs. The actual importer root cause remains unresolved.','scopeExpansion':'The earlier 56 differences covered the outline-visible head subset. This audit covers all 14 morph-bearing source meshes/70 frames, plus 3 outline meshes/7 frames.','protectedHistoricalPatch':'Existing eight-endpoint shell compatibility patch and source imports remain untouched.','localRawEvidence':'.local/ren-fbx-weld-ab-v1/imported and comparison contain full raw positions, all per-row errors and correspondence NPZ; not duplicated in the portable checkpoint.'}
(out/'review-evidence.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
(out/'README.md').write_text('''# Unity FBX vertex-welding A/B

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
''',encoding='utf-8',newline='\n')
files=[{'file':p.relative_to(out).as_posix(),'sha256':h(p),'bytes':p.stat().st_size} for p in out.rglob('*') if p.is_file() and p.name!='files.json']
(out/'files.json').write_text(json.dumps({'files':files},indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'output':str(out),'files':len(files)+1,'bytes':sum(x['bytes'] for x in files),'pairMeshesVerified':len(checks)}))
