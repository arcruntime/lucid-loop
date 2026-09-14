"""Reapply the reference bar/window pass to the current editable nightclub."""
import bpy,ast,math,json,random
import numpy as np
from pathlib import Path
from collections import defaultdict
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/environments/nightclub-v1'
DEST=ROOT/'Unity/Assets/EnvironmentArt/Generated'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
source=(ROOT/'tools/environment_art/build_nightclub.py').read_text()
tree=ast.parse(source)
functions=[n for n in tree.body if isinstance(n,ast.FunctionDef)]
exec(compile(ast.Module(body=functions,type_ignores=[]),'<kit helpers>','exec'))
CONFIG=json.loads((DEST/'materials.json').read_text())['materials'];MATS={}
for n in tree.body:
    if isinstance(n,ast.Assign) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Name) and n.value.func.id=='material':
        name=n.value.args[0].value
        if bpy.data.materials.get(name):globals()[n.targets[0].id]=bpy.data.materials[name]
        else:exec(compile(ast.Module(body=[n],type_ignores=[]),'<new material>','exec'))
for o in list(bpy.data.objects):
    if str(o.get('zone','')).startswith('bar') or o.name.startswith(('VIP wall','VIP window sill','Skyline window','Window brass mullion','Window upper frame','Window warm sill')):
        bpy.data.objects.remove(o,do_unlink=True)
ZONE='shell'
start=source.index("box('VIP window sill wall'")
end=source.index('for side in [-1,1]:',start)
exec(source[start:end])
start=source.index('# Curved bar endcaps')
end=source.index('# Seating: final furniture',start)
exec(source[start:end])
for o in bpy.data.objects:
    if o.type=='MESH' and not o.data.uv_layers:
        uv=o.data.uv_layers.new(name='SurfaceUV')
        for face in o.data.polygons:
            drop=max(range(3),key=lambda i:abs(face.normal[i]));axes=[i for i in range(3) if i!=drop]
            for li in face.loop_indices:
                co=o.data.vertices[o.data.loops[li].vertex_index].co
                uv.data[li].uv=(co[axes[0]]*.65,co[axes[1]]*.65)
    if o.type=='MESH' and len(o.data.uv_layers):o.data.uv_layers[0].name='SurfaceUV'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
exec(source[source.index('groups=defaultdict(list)'):])
