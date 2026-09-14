"""Production local LOD1 from final female-v3 LOD0 only; component-aware reduction."""
import bpy,bmesh,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from recover_ren_lod1_controls import barycentric
SRC=ROOT/'art/generated/characters/ren/lod0-final-v1/Ren_LOD0.blend'
OUT=ROOT/'art/generated/characters/ren/lod1-final-v1'
BUDGET={'RenBody_LOD0':2000,'RenNeckChest_LOD0':400,'RenHeadSkin':2850,'RenLiveHair':4000,'RenCap_Static':450,'RenHeadphones_Static':550,'RenEyesShallow':400,'RenEarJewelry':100,'RenInkBrowsLashes':250,'RenUpperTeeth':70,'RenLowerTeeth':70,'RenUpperGums':60,'RenLowerGums':60,'RenTongue':140,'RenNeckJoin':293}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def tris(o):return sum(len(p.vertices)-2 for p in o.data.polygons)
def main():
 OUT.mkdir(parents=True,exist_ok=True);source_hash=sha(SRC);bpy.ops.wm.open_mainfile(filepath=str(SRC))
 scene=bpy.context.scene;rig=bpy.data.objects['RenFemaleV3'];rest={b.name:[list(r)for r in b.matrix_local]for b in rig.data.bones};parents={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
 meshes=[bpy.data.objects[n]for n in BUDGET];report={'source':str(SRC),'source_sha256':source_hash,'parts':[],'rig_bones':rest,'rig_parents':parents}
 # Save truly matched views of the same final source with identical camera/lighting.
 scene.render.resolution_percentage=60
 scene.camera.data.ortho_scale=1.9;scene.camera.location=(0,-4,.9);scene.camera.rotation_euler=(Vector((0,0,.88))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
 for label in ('source',):
  scene.render.filepath=str(OUT/(label+'-full.png'));bpy.ops.render.render(write_still=True)
 for obj in meshes:
  before=tris(obj);target=BUDGET[obj.name];old=obj.data.copy();old.calc_loop_triangles()
  triangles=[tuple(t.vertices)for t in old.loop_triangles];coords=[v.co.copy()for v in old.vertices];tree=BVHTree.FromPolygons(coords,triangles,all_triangles=True)
  triangle_loops=[tuple(t.loops)for t in old.loop_triangles];source_normals=[n.vector.copy()for n in old.corner_normals]
  bm=bmesh.new();bm.from_mesh(old);bm.verts.ensure_lookup_table()
  protected=set()
  if obj.name=='RenLiveHair':
   for edge in bm.edges:
    if edge.is_boundary or edge.seam:protected.update(v.index for v in edge.verts)
   for v in bm.verts:
    if len(v.link_edges)<=2:protected.add(v.index)
  elif obj.name in ('RenHeadSkin','RenNeckChest_LOD0','RenBody_LOD0'):
   for edge in bm.edges:
    if edge.is_boundary and all(v.co.z>1.40 for v in edge.verts):protected.update(v.index for v in edge.verts)
  if globals().get('PROTECT_VISIBLE_HEAD',False) and obj.name=='RenHeadSkin':
   for v in bm.verts:
    if v.co.y<.025 or v.co.z<1.565:protected.add(v.index)
  bm.free()
  keys=[]
  if obj.data.shape_keys:
   for k in obj.data.shape_keys.key_blocks:keys.append((k.name,[v.co.copy()for v in k.data],k.value,k.slider_min,k.slider_max,k.relative_key.name))
   obj.shape_key_clear()
  # Keep existing armature modifier, but apply decimation to undeformed mesh only.
  armstates=[(m,m.show_viewport)for m in obj.modifiers];
  for m,_ in armstates:m.show_viewport=False
  if target<before:
   mod=obj.modifiers.new('LOD1 component reduction','DECIMATE');mod.ratio=target/before;mod.use_collapse_triangulate=True
   if protected:
    group=obj.vertex_groups.new(name='LOD1_CollapseInterior');group.add([i for i in range(len(coords)) if i not in protected],1,'REPLACE')
    mod.vertex_group=group.name;mod.vertex_group_factor=1000
   bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.modifier_apply(modifier=mod.name)
  for m,state in armstates:m.show_viewport=state
  from mathutils.kdtree import KDTree
  kd=KDTree(len(obj.data.vertices))
  for v in obj.data.vertices:kd.insert(v.co,v.index)
  kd.balance();protected_error=max((kd.find(coords[i])[2]for i in protected),default=0)
  maps=[];max_distance=0
  for v in obj.data.vertices:
   hit,normal,tid,dist=tree.find_nearest(v.co);corners=triangles[tid];weights=barycentric(hit,*[coords[i]for i in corners]);maps.append((corners,weights));max_distance=max(max_distance,dist)
  if keys:
   byname={k[0]:k[1]for k in keys}
   for name,points,value,lo,hi,relative in keys:
    key=obj.shape_key_add(name=name);key.slider_min=lo;key.slider_max=hi;key.value=value
    if name!=keys[0][0]:
     for i,(corners,weights)in enumerate(maps):key.data[i].co+=sum(((points[c]-byname[relative][c])*w for c,w in zip(corners,weights)),Vector())
   # All deltas are relative to Basis after interpolation, preserving independent snapshots.
  if obj.name in ('RenHeadSkin','RenNeckChest_LOD0','RenNeckJoin','RenLiveHair'):
   normals=[]
   for loop in obj.data.loops:
    hit,normal,tid,dist=tree.find_nearest(obj.data.vertices[loop.vertex_index].co);corners=triangles[tid];w=barycentric(hit,*[coords[i]for i in corners]);n=sum((source_normals[l]*f for l,f in zip(triangle_loops[tid],w)),Vector()).normalized();normals.append(n)
   obj.data.normals_split_custom_set(normals)
  unbound=sum(not v.groups for v in obj.data.vertices)if any(m.type=='ARMATURE'for m in obj.modifiers)else 0
  if unbound:raise ValueError('Unbound reduced vertices: '+obj.name)
  report['parts'].append({'name':obj.name,'before_triangles':before,'triangles':tris(obj),'shape_names':[k[0]for k in keys],'max_projection_distance_local':max_distance,'protected_vertices':len(protected),'protected_vertex_max_error':protected_error,'parent':obj.parent.name if obj.parent else None,'parent_bone':obj.parent_bone,'unbound_vertices':unbound})
  bpy.data.meshes.remove(old)
 report['triangles']=sum(tris(o)for o in meshes)
 report['within_budget']=10000<=report['triangles']<=12000
 report['protected_boundaries_preserved']=all(p['protected_vertex_max_error']<1e-6 for p in report['parts'])
 if rest!={b.name:[list(r)for r in b.matrix_local]for b in rig.data.bones}:raise ValueError('Rig rest changed')
 report['rig_rest_unchanged']=True;report['native_source_unchanged']=sha(SRC)==source_hash
 hair=bpy.data.objects.get('RenLiveHair')
 if hair and hair.data.shape_keys and 'capOn'in hair.data.shape_keys.key_blocks:hair.data.shape_keys.key_blocks['capOn'].value=0
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_LOD1.blend'))
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
 for o in meshes:o.select_set(True)
 # Include accessory socket ancestors when exporting separate attachment objects.
 for o in meshes:
  parent=o.parent
  while parent:parent.select_set(True);parent=parent.parent
 bpy.ops.export_scene.fbx(filepath=str(OUT/'Ren_LOD1.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,path_mode='COPY',embed_textures=True)
 scene.render.filepath=str(OUT/'lod1-full.png');bpy.ops.render.render(write_still=True)
 report['limitations']=['Local decimation with nearest source triangle morph delta transfer; endpoint shapes require visual review.','Component boundaries are preserved as separate objects; neck seam edge correspondence is not guaranteed after reduction.','LOD1 hair retains inherited bone weights and capOn morph; no new secondary-motion tuning.']
 (OUT/'manifest.json').write_text(json.dumps(report,indent=2));print('LOD1_DONE',report['triangles'])
if __name__=='__main__':main()

