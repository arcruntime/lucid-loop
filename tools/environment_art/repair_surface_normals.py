"""Audit and repair the closed rounded furniture shells, then export the set."""
import bpy,bmesh,json
from pathlib import Path
from collections import defaultdict
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/environments/nightclub-v1'
DEST=ROOT/'Unity/Assets/EnvironmentArt/Generated'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
records=[]
for o in bpy.data.objects:
    if not o.name.startswith(('Bar curved stone counter','Bar upholstered body','Stool upholstered','Booth brass plinth','Curved booth')):continue
    bm=bmesh.new();bm.from_mesh(o.data);before=bm.calc_volume(signed=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));after=bm.calc_volume(signed=True)
    if after<=0:raise RuntimeError('Closed furniture has non-positive signed volume: '+o.name)
    bm.to_mesh(o.data);bm.free();records.append(dict(name=o.name,before=before,after=after))
(ROOT/'docs/validation/nightclub-v2/surface-normals.json').write_text(json.dumps(records,indent=2)+'\n')
CONFIG=json.loads((DEST/'materials.json').read_text())['materials']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
source=(ROOT/'tools/environment_art/build_nightclub.py').read_text()
exec(source[source.index('groups=defaultdict(list)'):])
