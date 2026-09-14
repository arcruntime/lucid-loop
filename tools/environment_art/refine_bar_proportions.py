"""Fit the bar counter to a credible service depth within its blocked footprint."""
import bpy,json
from pathlib import Path
from collections import defaultdict
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/environments/nightclub-v1'
DEST=ROOT/'Unity/Assets/EnvironmentArt/Generated'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
for o in bpy.data.objects:
    if o.get('reference_bar_depth'):continue
    if o.name=='Bar curved stone counter':
        for v in o.data.vertices:v.co.x=-9.05+(v.co.x+9.05)*.42
        o['reference_bar_depth']=True
    elif o.name=='Bar upholstered body':
        for v in o.data.vertices:v.co.x=-9.15+(v.co.x+9.15)*(.95/2.3)
        o['reference_bar_depth']=True
    elif o.name.startswith('Counter spirits'):
        o.location.x+=1.05;o['reference_bar_depth']=True
CONFIG=json.loads((DEST/'materials.json').read_text())['materials']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
source=(ROOT/'tools/environment_art/build_nightclub.py').read_text()
exec(source[source.index('groups=defaultdict(list)'):])
