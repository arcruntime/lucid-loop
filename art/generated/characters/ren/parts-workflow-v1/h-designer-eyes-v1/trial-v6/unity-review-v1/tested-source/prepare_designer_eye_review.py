from pathlib import Path
import json,hashlib,shutil,uuid,ast,struct,zlib,numpy as np
repo=Path(r'B:\lucid-loop'); project=Path(r'C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification')
root=project/'Assets/CharacterArt/Generated/RenDesignerEyeReview';root.mkdir(exist_ok=True)
src=repo/'art/generated/characters/ren/parts-workflow-v1/h-designer-eyes-v1/trial-v6/neutral-export-v2'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
fbx=src/'Ren_DesignerEyes_Neutral.fbx';assert h(fbx)=='ee7fbae3066f0576328f32fd635583da0d419d8dcfd6033ed102d2695f6338fc'
shutil.copy2(fbx,root/fbx.name);shutil.copy2(src/'contract.json',root/'SourceContract.json')
d=json.loads((src/'contract.json').read_text());matrix=lambda m:[v for row in m for v in row]
c={'sourceSha256':h(fbx),'sourceContractSha256':h(src/'contract.json'),'sourceFbxAsset':(root/fbx.name).relative_to(project).as_posix(),'nativeToReviewWorld':matrix(d['native_to_review_world']),'eyes':[{'name':e['object'],'triangles':e['triangles'],'vertices':e['vertices'],'colorMin':e['color_min'],'colorMax':e['color_max'],'skin':e['object'].endswith('SkinShutter')} for e in d['eyes']]}
parser=repo/'art/generated/characters/ren/parts-workflow-v1/h-tokon-outline-v1/import-audit/audit_raw_fbx.py'
tree=ast.parse(parser.read_text(encoding='utf-8'));fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='parse');exec(compile(ast.Module(body=[fn],type_ignores=[]),str(parser),'exec'),globals())
nodes=parse(fbx);objects=next(n[2] for n in nodes if n[0]==b'Objects');connections=next(n[2] for n in nodes if n[0]==b'Connections');byid={n[1][0]:n for n in objects}
(root/'CanonicalColorCorners').mkdir(exist_ok=True)
for obj in objects:
 if obj[0]!=b'Geometry' or obj[1][2]!=b'Mesh':continue
 model=next(byid[n[1][2]] for n in connections if n[1][0]==b'OO' and n[1][1]==obj[1][0] and n[1][2] in byid and byid[n[1][2]][0]==b'Model')
 name=model[1][1].decode().split('\x00')[0];spec=next(x for x in c['eyes'] if x['name']==name)
 children={n[0]:n for n in obj[2]};vertices=children[b'Vertices'][1][0].reshape(-1,3);indices=children[b'PolygonVertexIndex'][1][0];indices=np.where(indices<0,-indices-1,indices)
 layer=next(n for n in obj[2] if n[0]==b'LayerElementColor' and n[1][0]==0);a={n[0]:n[1][0] for n in layer[2] if n[1]}
 assert a[b'MappingInformationType']==b'ByPolygonVertex';colors=a[b'Colors'].reshape(-1,4);colors=colors[a[b'ColorIndex']] if b'ColorIndex' in a else colors
 layer=next(n for n in obj[2] if n[0]==b'LayerElementUV' and n[1][0]==0);a={n[0]:n[1][0] for n in layer[2] if n[1]}
 assert a[b'MappingInformationType']==b'ByPolygonVertex';uv=a[b'UV'].reshape(-1,2);uv=uv[a[b'UVIndex']] if b'UVIndex' in a else uv
 assert len(colors)==len(indices)==len(uv)
 path=root/'CanonicalColorCorners'/(name+'.f32');np.column_stack([vertices[indices],uv,colors]).astype('<f4').tofile(path)
 spec['cornersAsset']=path.relative_to(project).as_posix();spec['cornersSha256']=h(path);spec['cornerCount']=len(indices)
(root/'Manifest.json').write_text(json.dumps(c,indent=2)+'\n',encoding='utf-8',newline='\n')
for rel in ['Runtime/RenDesignerEyeReview.cs','Editor/RenDesignerEyeReviewBuilder.cs']:
 p=project/'Assets/CharacterArt'/rel;meta=Path(str(p)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8',newline='\n')
out=repo/'.local/ren-designer-eye-v6';out.mkdir(exist_ok=True)
print(json.dumps({'manifest':h(root/'Manifest.json'),'fbx':h(fbx),'output':str(out)}))
