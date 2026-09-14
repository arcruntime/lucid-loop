from pathlib import Path
import re,json,hashlib,shutil

repo=Path('B:/lucid-loop'); out=repo/'.local/ren-main-mouth-migration-v1'
original=json.loads((out/'dependency-manifest.json').read_text())
source=Path(original['sourceProject']); target=Path(original['destinationProject']); payload=out/'payload/Unity'
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def bytehash(b): return hashlib.sha256(b).hexdigest()
def guid(p): return re.search(rb'^guid:\s*([0-9a-f]{32})',p.read_bytes(),re.M)[1].decode()
shader_paths=['Assets/CharacterArt/NPR/TokonStudy/'+name for name in ['RenTokonNPR.shader','RenTokonInk.shader','RenTokonNPR.hlsl']]
maps=[]
for rel in shader_paths:
    assert (source/rel).read_bytes()==(target/rel).read_bytes(), 'Existing shader bytes differ: '+rel
    maps.append({'asset':rel,'sourceGuid':guid(source/(rel+'.meta')),'mainGuid':guid(target/(rel+'.meta')),'identicalShaderSha256':digest(source/rel),'preservedMainMetaSha256':digest(target/(rel+'.meta'))})
asm='Assets/CharacterArt/Runtime/LucidLoop.CharacterArt.asmdef'
srcasm=json.loads((source/asm).read_text()); mainasm=json.loads((target/asm).read_text())
assert srcasm['name']==mainasm['name'] and set(srcasm['references'])<=set(mainasm['references'])
package={}
wanted=set(original['unresolvedGuids'])
for meta in (target/'Library/PackageCache').rglob('*.meta'):
    m=re.search(rb'^guid:\s*([0-9a-f]{32})',meta.read_bytes(),re.M)
    if m and m[1].decode() in wanted:
        g=m[1].decode(); asset=Path(str(meta)[:-5]); assert g not in package
        package[g]={'meta':meta.relative_to(target).as_posix(),'asset':asset.relative_to(target).as_posix(),'metaSha256':digest(meta),'assetSha256':digest(asset) if asset.is_file() else None}
assert set(package)==wanted, 'Unresolved main package GUIDs remain'
files=[]; reused=[]
for row in original['files']:
    rel=row['relativePath']; src=source/rel; dest=target/rel
    assert digest(src)==row['sourceSha256'], 'Source changed after closure: '+rel
    if row['status']!='copy-new':
        assert dest.exists() and digest(dest)==row['destinationSha256'], 'Existing main dependency changed: '+rel
        assert row['status']!='destination-conflict' or rel==asm or rel[:-5] in shader_paths
        reused.append({'relativePath':rel,'mainSha256':digest(dest),'reason':'preserve main shader GUID' if rel[:-5] in shader_paths else 'compatible main asmdef superset' if rel==asm else 'identical existing dependency'})
        continue
    assert not dest.exists(), 'Destination unexpectedly exists: '+rel
    data=src.read_bytes(); replacements=[]
    for mapping in maps:
        pattern=rb'(\bguid:\s*)'+mapping['sourceGuid'].encode()+rb'\b'
        count=len(re.findall(pattern,data))
        if count:
            assert src.suffix in {'.unity','.mat'}, 'Remap scope must remain scene/material YAML: '+rel
            data=re.sub(pattern,lambda m:m[1]+mapping['mainGuid'].encode(),data)
            replacements.append({'sourceGuid':mapping['sourceGuid'],'mainGuid':mapping['mainGuid'],'count':count})
    bundle=payload/rel; bundle.parent.mkdir(parents=True,exist_ok=True);bundle.write_bytes(data)
    files.append({'relativePath':rel,'bytes':len(data),'sourceSha256':digest(src),'migratedSha256':bytehash(data),'guidReplacements':replacements})
plan={'status':'PREPARED_MAIN_MIGRATION_AUTHORIZED_AFTER_PREFLIGHT','sourceScene':original['scene'],'destinationScene':original['scene'],'sourceProject':str(source),'destinationProject':str(target),'payload':str(payload),'copyCount':len(files),'copyBytes':sum(f['bytes'] for f in files),'shaderGuidMappings':maps,'changedYaml':[f for f in files if f['guidReplacements']],'reusedExisting':reused,'resolvedPackageGuids':package,'files':files,'scope':'Copy new verified mouth scene dependencies only. Preserve every existing main file. Remap only copied scene/material YAML to identical existing main shader GUIDs. No project settings, editor launch, blink candidate, shader duplication or source mutation.'}
(out/'migration-plan.json').write_text(json.dumps(plan,indent=2)+'\n')
print(json.dumps({'copyCount':plan['copyCount'],'copyBytes':plan['copyBytes'],'changedYaml':[(f['relativePath'],sum(r['count'] for r in f['guidReplacements'])) for f in plan['changedYaml']],'shaderGuidMappings':maps,'resolvedPackageGuids':len(package)},indent=2))
