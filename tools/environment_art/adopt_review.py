"""Install the reviewed environment outputs without overwriting a changed encounter."""
import hashlib,json,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
REVIEW=ROOT/'.local/environment-review-project'
manifest=json.loads((ROOT/'docs/validation/nightclub-v2/review-copy-manifest.json').read_text())
scene='Assets/Gyms/Scenes/BeforeTheDrop.unity'
expected=next(r['sha256'] for r in manifest['files'] if r['path']==scene)
digest=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
if digest(ROOT/'Unity'/scene)!=expected:
    raise RuntimeError('The source encounter changed since review began; reconcile it before installing.')
backup=ROOT/'.local/environment-art/before-v2/BeforeTheDrop.unity'
backup.parent.mkdir(parents=True,exist_ok=True)
if not backup.exists():shutil.copy2(ROOT/'Unity'/scene,backup)
paths=[p for p in (REVIEW/'Unity/Assets/EnvironmentArt/Generated').rglob('*') if p.is_file()]
paths.extend([REVIEW/'Unity'/scene,REVIEW/'Unity/Assets/Gyms/Shaders/CityWindow.shader.meta'])
records=[]
for path in paths:
    relative=path.relative_to(REVIEW/'Unity');dest=ROOT/'Unity'/relative
    before=digest(dest) if dest.exists() else None
    dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    if digest(dest)!=digest(path):raise RuntimeError('Copy verification failed: '+str(relative))
    records.append(dict(path=relative.as_posix(),before=before,sha256=digest(dest)))
for path in (REVIEW/'docs/validation/nightclub-v2').iterdir():
    if path.is_file():shutil.copy2(path,ROOT/'docs/validation/nightclub-v2'/path.name)
(ROOT/'docs/validation/nightclub-v2/installed-assets.json').write_text(json.dumps(records,indent=2)+'\n')
print('Installed and hash-verified',len(records),'reviewed environment files.')
