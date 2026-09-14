"""Copy the encounter's actual saved dependencies into an isolated Unity review."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'.local/environment-review-project/Unity'
records=[]
for path in (ROOT/'Unity/Assets').glob('*.asset*'):
    relative=path.relative_to(ROOT/'Unity');dest=DEST/relative
    dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    before=hashlib.sha256(path.read_bytes()).hexdigest();after=hashlib.sha256(dest.read_bytes()).hexdigest()
    if before!=after:raise RuntimeError('Source changed during copy: '+str(relative))
    records.append(dict(path=relative.as_posix(),sha256=after,bytes=dest.stat().st_size))
for folder in ['Assets/Gyms','Assets/LiveSpeech','Assets/Plugins','Assets/Resources','Assets/EnvironmentArt','Assets/CharacterArt/Runtime','Packages','ProjectSettings']:
    source=ROOT/'Unity'/folder
    for path in source.rglob('*'):
        if not path.is_file():continue
        relative=path.relative_to(ROOT/'Unity');dest=DEST/relative
        dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
        before=hashlib.sha256(path.read_bytes()).hexdigest();after=hashlib.sha256(dest.read_bytes()).hexdigest()
        if before!=after:raise RuntimeError('Source changed during copy: '+str(relative))
        records.append(dict(path=relative.as_posix(),sha256=after,bytes=dest.stat().st_size))
for folder in ['Gyms','LiveSpeech','Plugins','Resources','EnvironmentArt']:
    p=ROOT/'Unity/Assets'/(folder+'.meta')
    if p.exists():shutil.copy2(p,DEST/'Assets'/p.name)
out=ROOT/'docs/validation/nightclub-v2/review-copy-manifest.json'
out.parent.mkdir(parents=True,exist_ok=True)
plan=ROOT/'server/src/nightclub-dressing.json';plan_copy=DEST.parent/'server/src/nightclub-dressing.json'
plan_copy.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(plan,plan_copy)
plan_hash=hashlib.sha256(plan.read_bytes()).hexdigest()
if plan_hash!=hashlib.sha256(plan_copy.read_bytes()).hexdigest():raise RuntimeError('Dressing plan changed during copy')
out.write_text(json.dumps(dict(project=str(DEST),files=records,externalFiles=[dict(path='server/src/nightclub-dressing.json',sha256=plan_hash)]),indent=2)+'\n')
print('Verified isolated review copy:',len(records),'files,',sum(r['bytes'] for r in records),'bytes')
