from pathlib import Path
import json,hashlib,re
import numpy as np
from scipy.spatial import cKDTree
repo=Path(r'B:\lucid-loop');project=Path(r'C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification')
run=repo/'.local/ren-fbx-weld-ab-v1';raw=run/'imported'
sourceDir=repo/'art/generated/characters/ren/parts-workflow-v1/h-tokon-outline-v1/import-audit/canonical-source'
outlineDir=sourceDir.parent.parent
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text(encoding='utf-8'))
im=load(raw/'imported-manifest.json');source=load(sourceDir/'manifest.json');outline=load(outlineDir/'morph-supplement-contract.json');tol=2e-6
authored={}
for m in source['meshes']:
 frames={}
 for f in m['frames']:
  p=sourceDir/f['worldPositions']['file'];assert h(p)==f['worldPositions']['sha256'];frames[f['name']]=np.fromfile(p,dtype='<f4').reshape(-1,3).astype(np.float64)
 assert len(frames['Basis'])==m['vertices'];authored[('source',m['name'])]=frames
for m in outline['objects']:
 if not m['world_positions']:continue
 frames={}
 for f in m['world_positions']:
  p=outlineDir/f['file'];assert h(p)==f['sha256'];frames[f['morph']]=np.fromfile(p,dtype='<f4').reshape(-1,3).astype(np.float64)
 authored[('outline',m['shell'])]=frames
def meta_normalized(path):
 s=path.read_text(encoding='utf-8');s=re.sub(r'^guid: .+$','guid: GUID',s,flags=re.M);s=re.sub(r'^    weldVertices: [01]$','    weldVertices: WELD',s,flags=re.M);return s
config=load(run/'audit-input.json');meta_checks=[]
for kind in ['source','outline']:
 pair=[x for x in config['inputs'] if x['kind']==kind]
 pa=[project/(x['asset']+'.meta') for x in pair]
 assert meta_normalized(pa[0])==meta_normalized(pa[1])
 meta_checks.append({'kind':kind,'onlyWeldAndGuidDiffer':True,'metas':[{'path':str(p.relative_to(project)),'sha256':h(p)} for p in pa]})
for entry in load(run/'input-protection.json'):assert h(Path(entry['path']))==entry['sha256']
results=[]
for case in im['cases']:
 caseout=run/'comparison'/case['id'];caseout.mkdir(parents=True,exist_ok=True)
 meshes=[]
 for m in case['meshes']:
  ref=authored[(case['kind'],m['name'])];names=['Basis']+sorted(n for n in ref if n!='Basis')
  assert {x['shape'] for x in m['frames']}==set(names)
  actual={}
  for f in m['frames']:
   p=raw/case['id']/f['file'];assert h(p)==f['sha256'];actual[f['shape']]=np.fromfile(p,dtype='<f4').reshape(-1,3).astype(np.float64)
  rb=ref['Basis'];ab=actual['Basis'];tree=cKDTree(ab);candidates=tree.query_ball_point(rb,tol,workers=4)
  count=len(rb);chosen=np.full(count,-1,dtype=np.int32);maxErrors=np.full(count,np.inf);ambiguous=0
  ar=np.stack([actual[n] for n in names]);rr=np.stack([ref[n] for n in names])
  for i,ids in enumerate(candidates):
   if not ids:continue
   ids=np.asarray(ids,dtype=np.int32);errors=np.linalg.norm(ar[:,ids,:]-rr[:,i,None,:],axis=2);scores=errors.max(axis=0)
   order=np.lexsort((ids,errors.sum(axis=0),scores));best=int(order[0]);chosen[i]=ids[best];maxErrors[i]=scores[best]
   tied=ids[np.abs(scores-scores[best])<1e-9]
   if len(tied)>1 and np.max(np.linalg.norm(ar[:,tied,:]-ar[:,ids[best],None,:],axis=2))>tol:ambiguous+=1
  valid=chosen>=0;frameStats=[];failed=[]
  # Every authored row gets a chosen imported correspondence plus an explicit unmatched/ambiguous status.
  np.savez_compressed(caseout/(m['name']+'-correspondence.npz'),sourceOrdinal=np.arange(count,dtype=np.int32),importedVertex=chosen,jointMaxError=maxErrors,candidateCount=np.asarray([len(x) for x in candidates],dtype=np.int32))
  for name in names:
   n=count;actualEnds=np.full((n,3),np.nan);actualBasis=np.full((n,3),np.nan);actualEnds[valid]=actual[name][chosen[valid]];actualBasis[valid]=ab[chosen[valid]]
   error=np.linalg.norm(actualEnds-ref[name],axis=1);expectedMove=np.linalg.norm(ref[name]-rb,axis=1);actualMove=np.linalg.norm(actualEnds-actualBasis,axis=1)
   bad=valid & (error>tol);zeroed=bad & (expectedMove>tol) & (actualMove<=1e-10);nonzero=bad & (actualMove>1e-10)
   # Full numeric per-row evidence stays local; curated JSON retains every failed endpoint.
   stats=np.column_stack([error,expectedMove,actualMove]).astype('<f4');stats.tofile(caseout/(m['name']+'--'+name+'-errors.f32'))
   for i in np.flatnonzero(bad):failed.append({'shape':name,'sourceOrdinal':int(i),'importedVertex':int(chosen[i]),'basis':rb[i].tolist(),'authoredEndpoint':ref[name][i].tolist(),'importedBasis':actualBasis[i].tolist(),'importedEndpoint':actualEnds[i].tolist(),'error':float(error[i]),'authoredDeltaMagnitude':float(expectedMove[i]),'importedDeltaMagnitude':float(actualMove[i]),'zeroed':bool(zeroed[i]),'candidateCount':len(candidates[i])})
   frameStats.append({'shape':name,'authoredRows':count,'matchedBasisRows':int(valid.sum()),'unmatchedBasisRows':int((~valid).sum()),'authoredMovedAboveTolerance':int((expectedMove>tol).sum()),'errorRows':int(bad.sum()),'zeroedMovedRows':int(zeroed.sum()),'nonzeroErrorRows':int(nonzero.sum()),'maxError':float(np.nanmax(error)),'rmsError':float(np.sqrt(np.nanmean(error*error)))})
  failuresPath=caseout/(m['name']+'-all-endpoint-failures.json');failuresPath.write_text(json.dumps(failed,indent=2)+'\n',encoding='utf-8',newline='\n')
  meshes.append({'mesh':m['name'],'authoredVertices':count,'importedVertices':m['vertices'],'triangles':m['triangles'],'unmatchedBasisRows':int((~valid).sum()),'jointFullyMatchingRows':int((maxErrors<=tol).sum()),'ambiguousBestBranches':ambiguous,'frames':frameStats,'failureFile':str(failuresPath.relative_to(run)),'failureSha256':h(failuresPath)})
  print(case['id'],m['name'],'rows',count,'unmatched',int((~valid).sum()),'errors',sum(x['errorRows'] for x in frameStats),'zeroed',sum(x['zeroedMovedRows'] for x in frameStats),flush=True)
 results.append({'id':case['id'],'kind':case['kind'],'weldVertices':case['weldVertices'],'meshCount':len(meshes),'frameCount':sum(len(x['frames']) for x in meshes),'meshes':meshes,'totalEndpointErrors':sum(f['errorRows'] for m in meshes for f in m['frames']),'totalZeroedMovedEndpoints':sum(f['zeroedMovedRows'] for m in meshes for f in m['frames']),'totalNonzeroErrors':sum(f['nonzeroErrorRows'] for m in meshes for f in m['frames'])})
report={'status':'NUMERIC_WELD_AB_COMPLETE_REQUIRES_REVIEW','unityVersion':im['unityVersion'],'editorSourceSha256':im['editorSourceSha256'],'comparisonSourceSha256':h(Path(__file__)),'sourceCanonicalManifestSha256':h(sourceDir/'manifest.json'),'outlineSupplementSha256':h(outlineDir/'morph-supplement-contract.json'),'toleranceSourceUnits':tol,'units':'Blender shared world; physical units not independently established.','matching':'For each authored ordinal, search all imported Basis vertices within2e-6, then minimize maximum error jointly over Basis and every shape endpoint. Different coincident branches are not assumed identical; ambiguous best branches and unmatched rows are explicit. This is a diagnostic comparison, not a repair or automatic source-index remap.','allAuthoredEndpointsRecorded':'Each compared mesh/frame has raw imported positions, local per-row error arrays and correspondence NPZ. Every above-tolerance endpoint is retained in a JSON failure file.','metadataOnlyWeldAndGuidDiff':meta_checks,'originalInputAndSceneHashesUnchanged':True,'cases':results,'renderingPerformed':False,'mainUnityTouched':False,'futureImportPolicy':'Do not alter the existing eight-point historical shell patch or production imports based solely on this diagnostic. Root reviews numeric findings first.'}
(run/'comparison-summary.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'cases':[{k:v for k,v in x.items() if k!='meshes'} for x in results]}),flush=True)
