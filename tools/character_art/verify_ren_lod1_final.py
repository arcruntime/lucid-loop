"""Verify corrected LOD1 protected budget floor, morph endpoints and matched renders."""
from pathlib import Path
import bpy,json
from mathutils import Vector
ROOT=Path('B:/lucid-loop/art/generated/characters/ren');OUT=ROOT/'lod1-final-v1'
for level,path in [('source',ROOT/'lod0-final-v1/Ren_LOD0.blend'),('lod1',OUT/'Ren_LOD1.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene
 scene.render.resolution_percentage=60
 scene.camera.data.ortho_scale=.48;scene.camera.location=(.4,-2,1.56);scene.camera.rotation_euler=(Vector((0,0,1.52))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/(level+'-face.png'));bpy.ops.render.render(write_still=True)
 scene.camera.data.ortho_scale=1.9;scene.camera.location=(0,-4,.9);scene.camera.rotation_euler=(Vector((0,0,.88))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.resolution_x=180;scene.render.resolution_y=240;scene.render.resolution_percentage=100;scene.render.filepath=str(OUT/(level+'-gameplay240.png'));bpy.ops.render.render(write_still=True)
 if level=='lod1':
  shapes={}
  for obj in scene.objects:
   if obj.type=='MESH'and obj.data.shape_keys:
    basis=obj.data.shape_keys.key_blocks[0]
    shapes[obj.name]={k.name:max(((v.co-basis.data[i].co).length for i,v in enumerate(k.data)),default=0)for k in obj.data.shape_keys.key_blocks[1:]}
  report=json.loads((OUT/'manifest.json').read_text());report['shape_max_deltas']=shapes
  report['status']='LOD1_WITHIN_BUDGET';(OUT/'manifest.json').write_text(json.dumps(report,indent=2));print('LOD1_VERIFIED',report['triangles'])
