from pathlib import Path
import re,json,hashlib,subprocess

repo=Path('B:/lucid-loop')
source=Path('C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification')
dest=repo/'Unity'
out=repo/'.local/ren-main-mouth-migration-v1'
scene='Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerMouthReview.unity'
guidre=re.compile(r'\bguid:\s*([0-9a-fA-F]{32})\b')
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return p.read_text(encoding='utf-8-sig',errors='replace')
guidpaths={}; duplicate=[]
for meta in (source/'Assets').rglob('*.meta'):
    m=re.search(r'^guid:\s*([0-9a-f]{32})',read(meta),re.M)
    if m:
        rel=meta.relative_to(source).as_posix()[:-5]
        if m[1] in guidpaths: duplicate.append([m[1],guidpaths[m[1]],rel])
        guidpaths[m[1]]=rel
mainGuidPaths={}
for meta in (dest/'Assets').rglob('*.meta'):
    m=re.search(r'^guid:\s*([0-9a-f]{32})',read(meta),re.M)
    if m: mainGuidPaths.setdefault(m[1],[]).append(meta.relative_to(dest).as_posix()[:-5])
runtime={p.stem:p.relative_to(source).as_posix() for p in (source/'Assets/CharacterArt/Runtime').glob('*.cs')}
queue=[scene]; seen=set(); unresolved={}; includes=[]; reasons={scene:['selected verified mouth scene']}
def add(p,reason):
    reasons.setdefault(p,[]).append(reason)
    if p not in seen: queue.append(p)
while queue:
    rel=queue.pop()
    if rel in seen: continue
    seen.add(rel); p=source/rel
    if not p.exists(): raise RuntimeError('Missing source '+rel)
    files=[p] if p.is_file() else []
    meta=Path(str(p)+'.meta')
    if meta.exists(): files.append(meta)
    for f in files:
        if f.suffix.lower() not in {'.meta','.unity','.prefab','.mat','.asset','.cs','.shader','.hlsl','.asmdef','.json'}: continue
        text=read(f)
        for guid in guidre.findall(text):
            if guid in guidpaths: add(guidpaths[guid],f'{f.relative_to(source).as_posix()} GUID {guid}')
            elif int(guid,16) not in (0,) and not guid.startswith('0000000000000000'): unresolved.setdefault(guid,[]).append(f.relative_to(source).as_posix())
        if f.suffix=='.cs':
            for token in set(re.findall(r'\bRen[A-Za-z0-9_]+\b',text)):
                if token in runtime: add(runtime[token],rel+' C# type '+token)
            parent=f.parent
            while parent!=source:
                defs=list(parent.glob('*.asmdef'))
                if defs:
                    for definition in defs: add(definition.relative_to(source).as_posix(),rel+' assembly')
                    break
                parent=parent.parent
        if f.suffix in {'.shader','.hlsl'}:
            for include in re.findall(r'#include\s+"([^"]+)"',text):
                candidate=f.parent/include
                if candidate.exists(): add(candidate.relative_to(source).as_posix(),rel+' local include')
                elif (source/include).exists(): add(include,rel+' project include')
                else: includes.append({'from':rel,'include':include})

tracked=set(subprocess.check_output(['git','ls-files','-z'],cwd=repo).decode().split('\0'))
files=set()
for rel in seen:
    p=source/rel
    if p.is_file(): files.add(rel)
    if Path(str(p)+'.meta').exists(): files.add(rel+'.meta')
    for parent in p.parents:
        if parent==source: break
        meta=Path(str(parent)+'.meta')
        if meta.exists() and not (dest/parent.relative_to(source)).exists(): files.add(meta.relative_to(source).as_posix())
rows=[]
for rel in sorted(files):
    p=source/rel; target=dest/rel; exists=target.exists(); h=digest(p)
    same=exists and digest(target)==h
    textEquivalent=False
    if exists and not same and p.suffix in {'.cs','.meta','.shader','.hlsl','.asmdef'}:
        textEquivalent=p.read_bytes().replace(b'\r\n',b'\n')==target.read_bytes().replace(b'\r\n',b'\n')
    status='already-identical' if same else 'already-equivalent-line-endings' if textEquivalent else 'destination-conflict' if exists else 'copy-new'
    guid=None
    if rel.endswith('.meta'):
        m=re.search(r'^guid:\s*([0-9a-f]{32})',read(p),re.M); guid=m[1] if m else None
    collisions=[] if guid is None else [x for x in mainGuidPaths.get(guid,[]) if x!=rel[:-5]]
    rows.append({'relativePath':rel,'status':status,'bytes':p.stat().st_size,'sourceSha256':h,'destinationSha256':digest(target) if exists else None,'alreadyTracked':'Unity/'+rel in tracked,'guid':guid,'guidOtherPaths':collisions})
report={'status':'READ_ONLY_CLOSURE_MAIN_MUTATION_PENDING','sourceProject':str(source),'destinationProject':str(dest),'scene':scene,'assetCount':len(seen),'fileCount':len(rows),'requiredNewBytes':sum(x['bytes'] for x in rows if x['status']=='copy-new'),'counts':{s:sum(x['status']==s for x in rows) for s in sorted(set(x['status'] for x in rows))},'files':rows,'unresolvedGuids':unresolved,'sourceDuplicateGuids':duplicate,'externalIncludes':includes,'reasons':reasons}
(out/'dependency-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['assetCount','fileCount','requiredNewBytes','counts','unresolvedGuids']},indent=2))
print('CONFLICTS',json.dumps([x for x in rows if x['status']=='destination-conflict' or x['guidOtherPaths']],indent=2))
