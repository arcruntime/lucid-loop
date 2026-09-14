"""Transfer eight per-eye gaze deltas onto existing final reduced eyes only."""
import bpy,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).resolve().parent))
from recover_ren_lod1_controls import barycentric
OUT=ROOT/'art/generated/characters/ren/lod1-final-v1';SOURCE=ROOT/'art/generated/characters/ren/gaze-final-v1/Ren_LOD0_Gaze.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));source=bpy.data.objects['RenEyesShallow'];source.data.calc_loop_triangles()
points=[source.matrix_world@v.co for v in source.data.vertices];triangles=[tuple(t.vertices)for t in source.data.loop_triangles]
names=['gaze'+direction+side for side in ('L','R')for direction in ('Left','Right','Up','Down')]
basis=source.data.shape_keys.key_blocks[0];linear=source.matrix_world.to_3x3()
deltas={n:[linear@(v.co-basis.data[i].co)for i,v in enumerate(source.data.shape_keys.key_blocks[n].data)]for n in names}
tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_LOD1.blend'));eye=bpy.data.objects['RenEyesShallow']
def keyhash(k):return hashlib.sha256(str([tuple(v.co)for v in k.data]).encode()).hexdigest()
old={k.name:keyhash(k)for k in eye.data.shape_keys.key_blocks};inverse=eye.matrix_world.to_3x3().inverted();mapping=[]
for v in eye.data.vertices:
 hit,_,tid,dist=tree.find_nearest(eye.matrix_world@v.co);ids=triangles[tid];mapping.append((ids,barycentric(hit,*[points[i]for i in ids]),dist))
for name in names:
 if name in eye.data.shape_keys.key_blocks:raise ValueError('Gaze already exists; do not duplicate')
 key=eye.shape_key_add(name=name)
 for i,(ids,w,dist)in enumerate(mapping):key.data[i].co+=inverse@sum((deltas[name][j]*weight for j,weight in zip(ids,w)),Vector())
assert all(keyhash(eye.data.shape_keys.key_blocks[n])==h for n,h in old.items())
hair=bpy.data.objects.get('RenLiveHair')
if hair and hair.data.shape_keys and 'capOn'in hair.data.shape_keys.key_blocks:hair.data.shape_keys.key_blocks['capOn'].value=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_LOD1.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type in ('ARMATURE','MESH','EMPTY')and o.name.startswith('Ren'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ren_LOD1.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,path_mode='COPY',embed_textures=True)
(OUT/'gaze-transfer.json').write_text(json.dumps({'source':str(SOURCE),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'added_shapes':names,'previous_shape_hashes':old,'previous_shapes_unchanged':True,'neutral_geometry_unchanged':True,'max_surface_distance_m':max(m[2]for m in mapping)},indent=2));print('LOD1_GAZE_ADDED',names)

