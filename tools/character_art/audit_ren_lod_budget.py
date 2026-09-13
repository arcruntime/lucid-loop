"""Read sources in isolated Blender; write only local quantitative audit JSON."""
from pathlib import Path
import hashlib, json
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/characters/ren/parts-workflow-v1/lod-budget-v1'
BASE=ROOT/'art/generated/characters/ren'

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def meshstats(o):
    m=o.data;m.calc_loop_triangles()
    return dict(name=o.name,vertices=len(m.vertices),polygons=len(m.polygons),triangles=len(m.loop_triangles),
        shape_keys=[k.name for k in m.shape_keys.key_blocks] if m.shape_keys else [],
        uv_layers=[u.name for u in m.uv_layers],materials=[x.name if x else None for x in m.materials],
        hidden_render=o.hide_render)
def inventory(path):
    before=sha(path)
    if path.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(path))
    meshes=[meshstats(o) for o in bpy.data.objects if o.type=='MESH']
    return dict(path=str(path.relative_to(ROOT)),sha256=before,meshes=meshes,
        raw_mesh_triangles=sum(m['triangles'] for m in meshes),
        visible_mesh_triangles=sum(m['triangles'] for m in meshes if not m['hidden_render']),
        source_unchanged=before==sha(path))

paths=[BASE/'meshy/model.glb',BASE/'meshy/model-pre-remeshed.glb',BASE/'meshy/rigged-character.glb',
       BASE/'canonical-body-v2/ren_canonical_v2.blend',BASE/'anime-face/rig-study/Ren_source_shared_female_candidate.blend']
report=dict(status='LOCAL_FEASIBILITY_ONLY_NO_ASSET_MODIFICATIONS',blender=bpy.app.version_string,inventories=[])
for p in paths:
    if p.exists():report['inventories'].append(inventory(p))
    print('AUDITED',p,flush=True)
headpath=BASE/'parts-workflow-v1/face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend'
report['head_inventory']=inventory(headpath)
head=bpy.data.objects['Ren_Head'];keys=head.data.shape_keys.key_blocks
original_arrays={k.name:np.array([v.co[:] for v in k.data],dtype=float) for k in keys}
regions={k:np.flatnonzero(np.linalg.norm(v-original_arrays['Basis'],axis=1)>1e-8) for k,v in original_arrays.items() if k!='Basis'}

# Applying topology-changing decimation to the original keyed mesh is tested only on a duplicate.
copy=head.copy();copy.data=head.data.copy();bpy.context.scene.collection.objects.link(copy)
bpy.context.view_layer.objects.active=copy
for o in bpy.context.selected_objects:o.select_set(False)
copy.select_set(True);mod=copy.modifiers.new('AuditDecimate','DECIMATE');mod.ratio=.5
try:
    result=bpy.ops.object.modifier_apply(modifier=mod.name)
    report['apply_on_shape_keyed_duplicate']=dict(result=list(result),remaining_shape_keys=len(copy.data.shape_keys.key_blocks) if copy.data.shape_keys else 0)
except Exception as exc:report['apply_on_shape_keyed_duplicate']=dict(error=str(exc))
bpy.data.objects.remove(copy,do_unlink=True)

def static(pose):
    for k in keys:k.value=pose.get(k.name,0)
    bpy.context.view_layer.update()
    return bpy.data.meshes.new_from_object(head.evaluated_get(bpy.context.evaluated_depsgraph_get()),preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
def decimate(mesh):
    obj=bpy.data.objects.new('AuditStaticHead',mesh);bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj
    for o in bpy.context.selected_objects:o.select_set(False)
    obj.select_set(True);mod=obj.modifiers.new('NaiveHalf','DECIMATE');mod.ratio=.5;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj
report['naive_independent_pose_decimation']=[]
for name,pose in [('closed_rest',{'mouthSeal':1}),('open_a',{'jawOpen_A':1}),('blink',{'mouthSeal':1,'eyeBlinkL':1,'eyeBlinkR':1})]:
    source=static(pose);coords=np.array([v.co[:] for v in source.vertices]);source.calc_loop_triangles()
    tri_before=len(source.loop_triangles)
    obj=decimate(source);m=obj.data;m.calc_loop_triangles()
    coords_after=[tuple(v.co) for v in m.vertices];triangles=[tuple(t.vertices) for t in m.loop_triangles]
    tree=BVHTree.FromPolygons(coords_after,triangles,all_triangles=True)
    distances=np.array([tree.find_nearest(Vector(p))[3] for p in coords])
    entry=dict(pose=name,vertices=len(m.vertices),triangles=len(triangles),source_triangles=tri_before,
        triangle_index_sha256=hashlib.sha256(np.asarray(triangles,np.int32).tobytes()).hexdigest(),
        shape_keys_remaining=len(m.shape_keys.key_blocks) if m.shape_keys else 0,
        sampled_source_vertex_surface_error_units=dict(p95=float(np.percentile(distances,95)),max=float(distances.max())),
        changed_vertex_regions={k:dict(samples=len(ids),p95=float(np.percentile(distances[ids],95)),max=float(distances[ids].max())) for k,ids in regions.items()},
        metric_caveat='One-way source-vertex to reduced-surface sample distances. Not silhouette, eye/mouth aperture, shading, or animation acceptance; thin opposing surfaces can bias nearest-surface distances.')
    report['naive_independent_pose_decimation'].append(entry)
    bpy.data.objects.remove(obj,do_unlink=True)
report['naive_decimation_has_corresponding_triangle_indices']=len(set(x['triangle_index_sha256'] for x in report['naive_independent_pose_decimation']))==1
report['head_source_unchanged']=sha(headpath)==report['head_inventory']['sha256']
hair=BASE/'parts-workflow-v1/hair-cleanup-v1/fitted-v5/fitted-hair-workspace.blend'
report['hair_inventory']=inventory(hair)
selected=json.loads((hair.parent/'report.json').read_text(encoding='utf-8-sig'))
selected_names={x['name'] for x in selected['objects']}
hair_objects=[o for o in bpy.data.objects if o.type=='MESH' and o.name in selected_names]
report['hair_candidate_meshes']=[meshstats(o) for o in hair_objects]
report['hair_candidate_triangles']=sum(x['triangles'] for x in report['hair_candidate_meshes'])
report['hair_selection_note']='Exact object names from fitted-v5/report.json; raw/rejected/reference workspace objects excluded.'
report['reproduction_script']=str(Path(__file__).resolve().relative_to(ROOT))
report['script_sha256']=sha(Path(__file__))
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('COMPLETE',OUT/'audit.json',flush=True)
