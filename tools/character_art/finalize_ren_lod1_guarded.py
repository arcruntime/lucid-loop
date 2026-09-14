"""Transfer authoritative guarded weights/digit rests and export Unity-compatible units."""
import bpy,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).resolve().parent))
from recover_ren_lod1_controls import barycentric
OUT=ROOT/'art/generated/characters/ren/lod1-final-v1';SOURCE=ROOT/'art/generated/characters/ren/guarded-final-v1/RenGuarded_WeightAndDigitCorrection.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));source=bpy.data.objects['RenBody_LOD0'];rig=bpy.data.objects['RenFemaleV3'];source.data.calc_loop_triangles()
points=[source.matrix_world@v.co for v in source.data.vertices];triangles=[tuple(t.vertices)for t in source.data.loop_triangles]
tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
bone_names=set(rig.data.bones.keys());groups={g.index:g.name for g in source.vertex_groups if g.name in bone_names}
weights=[{groups[g.group]:g.weight for g in v.groups if g.group in groups}for v in source.data.vertices]
digit_names=[n for n in bone_names if n.startswith('LeftHand')and n!='LeftHand']
assert len(digit_names)==15
digit_rest={n:(rig.data.bones[n].matrix_local.copy(),rig.data.bones[n].length)for n in digit_names}
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_LOD1.blend'));body=bpy.data.objects['RenBody_LOD0'];rig=bpy.data.objects['RenFemaleV3']
neutral={o.name:[tuple(v.co)for v in o.data.vertices]for o in bpy.context.scene.objects if o.type=='MESH'}
shape_hash=lambda:hashlib.sha256(str({o.name:{k.name:[tuple(v.co)for v in k.data]for k in o.data.shape_keys.key_blocks}for o in bpy.context.scene.objects if o.type=='MESH'and o.data.shape_keys}).encode()).hexdigest()
before_shapes=shape_hash();unchanged_rest={b.name:b.matrix_local.copy()for b in rig.data.bones if b.name not in digit_names}
for p in rig.pose.bones:p.matrix_basis.identity()
rig.animation_data_clear()
for g in list(body.vertex_groups):
 if g.name in bone_names:body.vertex_groups.remove(g)
for n in bone_names:body.vertex_groups.new(name=n)
maxdist=0
for v in body.data.vertices:
 hit,_,tid,dist=tree.find_nearest(body.matrix_world@v.co);ids=triangles[tid];fractions=barycentric(hit,*[points[i]for i in ids]);values={};maxdist=max(maxdist,dist)
 for i,f in zip(ids,fractions):
  for n,w in weights[i].items():values[n]=values.get(n,0)+w*f
 top=sorted(values.items(),key=lambda x:x[1],reverse=True)[:4];total=sum(w for n,w in top)
 assert total>0
 for n,w in top:body.vertex_groups[n].add([v.index],w/total,'REPLACE')
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for n,(matrix,length)in digit_rest.items():rig.data.edit_bones[n].matrix=matrix;rig.data.edit_bones[n].length=length
bpy.ops.object.mode_set(mode='OBJECT')
error=max(abs(rig.data.bones[n].matrix_local[i][j]-m[i][j])for n,m in unchanged_rest.items()for i in range(4)for j in range(4));assert error<2e-6
assert shape_hash()==before_shapes
assert all([tuple(v.co)for v in bpy.data.objects[n].data.vertices]==p for n,p in neutral.items())
hair=bpy.data.objects.get('RenLiveHair')
if hair and hair.data.shape_keys and 'capOn'in hair.data.shape_keys.key_blocks:hair.data.shape_keys.key_blocks['capOn'].value=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_LOD1.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type in ('ARMATURE','MESH','EMPTY')and o.name.startswith('Ren'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ren_LOD1.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,path_mode='COPY',embed_textures=True)
(OUT/'guarded-transfer.json').write_text(json.dumps({'source':str(SOURCE),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'digit_names':sorted(digit_names),'non_digit_rest_max_error':error,'neutral_vertices_unchanged':True,'old_shape_coordinates_unchanged':True,'shape_sha256':before_shapes,'maximum_body_weight_projection_distance_m':maxdist,'export_apply_scale_options':'FBX_SCALE_UNITS','export_apply_unit_scale':True,'export_use_mesh_modifiers':False},indent=2));print('GUARDED_LOD1_FINAL')
