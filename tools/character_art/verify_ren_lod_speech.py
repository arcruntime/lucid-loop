"""Render matched actual LOD0/LOD1 articulation; never save over source models."""
from pathlib import Path
import hashlib
import json
import bpy
from mathutils import Vector

root=Path('B:/lucid-loop/art/generated/characters/ren')
out=root/'lod1-final-v1'/'speech-validation'
out.mkdir(exist_ok=True)
report={'sources':{},'poses':{}}
poses={'neutral':{},'A':{'speech_A':1},'MBP':{'speech_MBP':1},
       'FV':{'speech_FV':1},'blink-A':{'speech_A':.65,'eyeBlinkL':1,'eyeBlinkR':1}}
for level,source in [('lod0',root/'lod0-final-v1/Ren_LOD0.blend'),('lod1',root/'lod1-final-v1/Ren_LOD1.blend')]:
    report['sources'][level]=hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.context.scene
    scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100
    if hasattr(scene,'cycles'):scene.cycles.samples=12
    target=Vector((-.002,-.025,1.622))
    scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=.265
    scene.camera.location=target+Vector((0,-2,0))
    scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    for name,weights in poses.items():
        for obj in scene.objects:
            if obj.type=='MESH' and obj.data.shape_keys:
                for key in obj.data.shape_keys.key_blocks:
                    key.value=weights.get(key.name,1 if key.name=='capOn' else 0)
        bpy.context.view_layer.update()
        scene.render.filepath=str(out/f'{level}-{name}.png')
        bpy.ops.render.render(write_still=True)
        report['poses'][f'{level}-{name}']=weights
report['scope']='Matched Blender renders, source shape endpoints and combined blink/open mouth. Visual review required; no Unity or phone-performance claim.'
(out/'manifest.json').write_text(json.dumps(report,indent=2))
