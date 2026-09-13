from pathlib import Path
import json,hashlib,re,uuid,shutil
repo=Path(r'B:\lucid-loop');project=Path(r'C:\Users\jetha\AppData\Local\LucidLoopScratch\ren-eye-import-verification')
root=project/'Assets/CharacterArt/Generated/RenFbxWeldAudit';root.mkdir(exist_ok=True)
out=repo/'.local/ren-fbx-weld-ab-v1';out.mkdir(exist_ok=True)
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
originals=[('source',project/'Assets/CharacterArt/Generated/RenHReferenceAnimation/Sources/Ren_H_CompleteHead_Review.fbx','073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad'),('outline',project/'Assets/CharacterArt/Generated/RenTokonReview/Outline/Ren_H_Tokon_Outline.fbx','3812d52df7bcb82b187b80294b6e0bf76377b5a5069ee3facf018d01fd995b6c')]
inputs=[];protection=[]
for kind,source,sha in originals:
 assert h(source)==sha
 meta=source.with_suffix('.fbx.meta');s=meta.read_text(encoding='utf-8')
 protection.extend([{'path':str(p),'sha256':h(p)} for p in [source,meta]])
 for weld in [True,False]:
  label=kind+'-weld-'+str(weld).lower();dest=root/(label+'.fbx');shutil.copy2(source,dest)
  text=re.sub(r'^guid: .+$','guid: '+uuid.uuid4().hex,s,flags=re.M)
  assert text.count('    weldVertices: 1')==1
  text=text.replace('    weldVertices: 1','    weldVertices: '+str(int(weld)))
  dest.with_suffix('.fbx.meta').write_text(text,encoding='utf-8',newline='\n')
  inputs.append({'id':label,'kind':kind,'asset':dest.relative_to(project).as_posix(),'sha256':sha,'weld':weld})
for asset in ['Assets/CharacterArt/Generated/Preview/Scenes/RenHReferenceAnimation.unity','Assets/CharacterArt/Generated/Preview/Scenes/RenTokonReview.unity']:
 p=project/asset;protection.append({'path':str(p),'sha256':h(p)})
script=project/'Assets/CharacterArt/Editor/RenFbxWeldAudit.cs';meta=script.with_suffix('.cs.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8',newline='\n')
config={'inputs':inputs,'output':str(out/'imported')}
(root/'audit-input.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8',newline='\n')
(out/'input-protection.json').write_text(json.dumps(protection,indent=2)+'\n',encoding='utf-8',newline='\n')
shutil.copy2(root/'audit-input.json',out/'audit-input.json');shutil.copy2(script,out/script.name);shutil.copy2(meta,out/meta.name)
print(json.dumps({'output':str(out),'copies':len(inputs),'editorSha256':h(script)}))
