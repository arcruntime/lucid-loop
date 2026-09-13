from pathlib import Path
import ast,json,hashlib,struct,zlib,numpy as np
repo=Path(r'B:\lucid-loop')
source=repo/'art/generated/characters/ren/parts-workflow-v1/h-tokon-outline-v1/import-audit/audit_raw_fbx.py'
tree=ast.parse(source.read_text(encoding='utf-8'));fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='parse');scope=globals();exec(compile(ast.Module(body=[fn],type_ignores=[]),str(source),'exec'),scope)
fbx=repo/'art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v6/neutral-export-v2/Ren_DesignerEyes_Neutral.fbx'
objs=next(n[2] for n in parse(fbx) if n[0]==b'Objects');out=[]
for obj in objs:
 if obj[0]!=b'Geometry' or obj[1][2]!=b'Mesh':continue
 layers=[]
 for layer in obj[2]:
  if layer[0]!=b'LayerElementColor':continue
  d={c[0]:c[1][0] for c in layer[2] if c[1]};colors=d[b'Colors'].reshape(-1,4)
  layers.append({'index':layer[1][0],'name':d.get(b'Name',b'').decode(),'mapping':d.get(b'MappingInformationType',b'').decode(),'color_min':colors.min(axis=0).tolist(),'color_max':colors.max(axis=0).tolist()})
 out.append({'mesh':obj[1][1].decode().split('\x00')[0],'layers':layers})
result={'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'meshes':out}
(repo/'.local/ren-designer-eye-v6/raw-fbx-color-layers-v2.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps([m for m in out if len(m['layers'])>1],indent=2))
