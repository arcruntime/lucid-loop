from pathlib import Path
import re,json,hashlib,shutil,time

repo=Path('B:/lucid-loop'); out=repo/'.local/ren-main-mouth-migration-v1'
plan=json.loads((out/'migration-plan.json').read_text())
target=Path(plan['destinationProject']).resolve(); payload=Path(plan['payload']).resolve()
assert target==Path('B:/lucid-loop/Unity').resolve()
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
for row in plan['files']:
    dest=(target/row['relativePath']).resolve(); assert dest.is_relative_to(target)
    assert not dest.exists(), 'Refuse overwrite: '+str(dest)
    assert digest(payload/row['relativePath'])==row['migratedSha256']
for row in plan['reusedExisting']:
    assert digest(target/row['relativePath'])==row['mainSha256'], 'Existing dependency changed since preflight: '+row['relativePath']
records=[]
# Copy .meta before its asset. Preserve GUIDs without importing any source project settings.
ordered=sorted(plan['files'],key=lambda r:(r['relativePath'].count('/'),r['relativePath'].removesuffix('.meta'),not r['relativePath'].endswith('.meta')))
for row in ordered:
    rel=row['relativePath']; dest=target/rel; dest.parent.mkdir(parents=True,exist_ok=True)
    with dest.open('xb') as stream: stream.write((payload/rel).read_bytes())
    records.append({'relativePath':rel,'sha256':digest(dest)})
for row in plan['files']: assert digest(target/row['relativePath'])==row['migratedSha256']
for row in plan['reusedExisting']: assert digest(target/row['relativePath'])==row['mainSha256']
guids={}
for meta in (target/'Assets').rglob('*.meta'):
    m=re.search(rb'^guid:\s*([0-9a-f]{32})',meta.read_bytes(),re.M)
    if m: guids.setdefault(m[1].decode(),[]).append(meta.relative_to(target).as_posix()[:-5])
newguids={}
for row in plan['files']:
    if row['relativePath'].endswith('.meta'):
        m=re.search(rb'^guid:\s*([0-9a-f]{32})',(target/row['relativePath']).read_bytes(),re.M)
        if m: newguids[m[1].decode()]=row['relativePath'][:-5]
duplicates={g:guids[g] for g in newguids if len(guids[g])!=1}
assert not duplicates, 'New GUID duplicate after copy: '+str(duplicates)
report={'status':'COPIED_HASH_VERIFIED_MAIN_IMPORT_PENDING','scene':plan['destinationScene'],'copiedFiles':len(records),'mainExistingFilesPreserved':True,'newGuidDuplicates':duplicates,'files':records,'limitation':'Filesystem dependency handoff only. Main Unity import/compile/open/Play validation is owned by engineering. No second Editor was launched; blink remains unintegrated.'}
(out/'migration-result.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['status','scene','copiedFiles','mainExistingFilesPreserved','newGuidDuplicates']},indent=2))
