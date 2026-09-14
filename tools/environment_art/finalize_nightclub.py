"""Re-export the saved editable room after bounded export/material fixes."""
import bpy
import json
from pathlib import Path
from collections import defaultdict
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/environments/nightclub-v1'
DEST=ROOT/'Unity/Assets/EnvironmentArt/Generated'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Nightclub_Authored.blend'))
for o in bpy.data.objects:
    if o.type=='MESH' and len(o.data.uv_layers):o.data.uv_layers[0].name='SurfaceUV'
    if o.type=='MESH' and o.name.startswith('Sign '):
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
        mod=o.modifiers.new('Sign detail budget','DECIMATE');mod.ratio=.25
        bpy.ops.object.modifier_apply(modifier=mod.name)
if (DEST/'materials.json').exists():
    CONFIG=json.loads((DEST/'materials.json').read_text())['materials']
else:
    CONFIG=[]
    for m in bpy.data.materials:
        if not m.use_nodes or not m.users:continue
        bs=m.node_tree.nodes.get('Principled BSDF')
        if not bs:continue
        tex=''
        for link in m.node_tree.links:
            if link.to_node==bs and link.to_socket.name=='Base Color' and link.from_node.type=='TEX_IMAGE':
                tex=Path(link.from_node.image.filepath).name
        CONFIG.append(dict(name=m.name,color=list(bs.inputs['Base Color'].default_value)[:3],roughness=bs.inputs['Roughness'].default_value,metallic=bs.inputs['Metallic'].default_value,emission=bs.inputs['Emission Strength'].default_value,texture=tex))
source=(ROOT/'tools/environment_art/build_nightclub.py').read_text()
exec(compile((ROOT/'tools/environment_art/lounge_details.py').read_text(),'<lounge details>','exec'))
for o in bpy.data.objects:
    if o.type=='MESH' and len(o.data.uv_layers):o.data.uv_layers[0].name='SurfaceUV'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Nightclub_Dressed_Authored.blend'))
exec(source[source.index('groups=defaultdict(list)'):])
