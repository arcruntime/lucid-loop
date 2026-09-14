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
  hair=bpy.data.objects['RenLiveHair'];hair.shape_key_clear()
  for m in hair.modifiers:m.show_viewport=False
  mod=hair.modifiers.new('Protected floor measurement only','DECIMATE');mod.ratio=0;mod.use_collapse_triangulate=True;mod.vertex_group='LOD1_CollapseInterior';mod.vertex_group_factor=1000
  bpy.context.view_layer.objects.active=hair;bpy.ops.object.modifier_apply(modifier=mod.name)
  floor=sum(len(p.vertices)-2 for p in hair.data.polygons)
  report=json.loads((OUT/'manifest.json').read_text());report['hair_zero_ratio_protected_floor']=floor;report['minimum_with_other_current_components']=report['triangles']-next(p['triangles']for p in report['parts']if p['name']=='RenLiveHair')+floor;report['shape_max_deltas']=shapes
  report['status']='PROTECTED_CORRECTION_OVER_12K_BUDGET';(OUT/'manifest.json').write_text(json.dumps(report,indent=2));print('PROTECTED_HAIR_FLOOR',floor)
