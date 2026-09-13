"""Inventory and render only the isolated static native-hair reduction trial."""
from pathlib import Path
from collections import Counter
import hashlib,json,math,sys
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'art/generated/characters/ren/parts-workflow-v1'
OUT=BASE/'tripo-hair-lowpoly-v1/review'
NATIVE=ROOT/'art/generated/characters/ren/parts-generation-v1/tripo/hair/originals/model.fbx'
RETURNED=OUT.parent/'originals/model.fbx'
LOCAL=BASE/'ren-shag-v1/selected-v1/Ren_Shag_CapState.glb'
OFFSET=Vector((-.09,0,.22))

def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,indent=2)+'\n',encoding='utf-8')
def geometry(mesh):
    mesh.calc_loop_triangles()
    return np.array([v.co[:] for v in mesh.vertices]),np.array([t.vertices[:] for t in mesh.loop_triangles],dtype=np.int32)
def topology(v,t):
    # Position connectivity is independent of UV-split glTF import vertex duplication.
    keys=np.round(v,7);_,idx=np.unique(keys,axis=0,return_inverse=True);tris=idx[t]
    edges=Counter(tuple(sorted((int(a),int(b)))) for tri in tris for a,b in zip(tri,np.roll(tri,-1)))
    parents=np.arange(len(np.unique(idx)))
    def find(a):
        while parents[a]!=a:parents[a]=parents[parents[a]];a=int(parents[a])
        return a
    for a,b in edges:
        a,b=find(a),find(b)
        if a!=b:parents[b]=a
    areas=np.linalg.norm(np.cross(v[t[:,1]]-v[t[:,0]],v[t[:,2]]-v[t[:,0]]),axis=1)*.5
    return dict(position_weld_tolerance_decimal_places=7,position_vertices=int(len(parents)),position_components=len({find(i) for i in range(len(parents))}),
        boundary_edges=sum(n==1 for n in edges.values()),edges_more_than_two_faces=sum(n>2 for n in edges.values()),zero_area_triangles=int(np.sum(areas<=1e-12)))
def load(path,name,offset):
    before=set(bpy.data.objects)
    if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    else:bpy.ops.import_scene.gltf(filepath=str(path))
    meshes=[o for o in bpy.data.objects if o not in before and o.type=='MESH'];records=[];world_v=[];world_t=[]
    for obj in meshes:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:key.value=0
        v,t=geometry(obj.data);w=np.array([tuple(obj.matrix_world@Vector(co)) for co in v])
        world_t.extend((t+len(world_v)).tolist());world_v.extend(w.tolist())
        uvs=[]
        for layer in obj.data.uv_layers:
            uv=np.array([x.uv[:] for x in layer.data]);uvtri=np.array([[uv[i] for i in tri.loops] for tri in obj.data.loop_triangles])
            ab=uvtri[:,1]-uvtri[:,0];ac=uvtri[:,2]-uvtri[:,0]
            area=np.abs(ab[:,0]*ac[:,1]-ab[:,1]*ac[:,0])*.5
            uvs.append(dict(name=layer.name,min=uv.min(axis=0).tolist(),max=uv.max(axis=0).tolist(),zero_area_triangles=int(np.sum(area<=1e-12)),positive_overlap_test='not performed'))
        records.append(dict(object=obj.name,vertices=len(v),polygons=len(obj.data.polygons),triangles=len(t),uv_layers=uvs,topology=topology(w,t),matrix_world=[list(x) for x in obj.matrix_world]))
        # Temporary review placement only; original file and local shape-key geometry are unchanged.
        obj.matrix_world.translation+=offset
        obj.data.materials.clear();obj.data.materials.append(CLAY)
        for poly in obj.data.polygons:poly.use_smooth=True
        obj.name=name+'__'+obj.name
    w=np.asarray(world_v);t=np.asarray(world_t,dtype=np.int32)
    return meshes,dict(path=path.relative_to(ROOT).as_posix(),sha256=digest(path),objects=records,triangles=len(t),
        source_world_bounds=dict(min=w.min(axis=0).tolist(),max=w.max(axis=0).tolist()),review_translation=list(offset),topology=topology(w,t)),w,t

OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
CLAY=bpy.data.materials.new('Matched_Neutral_Clay');CLAY.use_nodes=True
bsdf=CLAY.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.47,.43,.36,1);bsdf.inputs['Roughness'].default_value=.85
native,nrecord,nv,nt=load(NATIVE,'Native',OFFSET)
returned,rrecord,rv,rt=load(RETURNED,'Tripo8000',OFFSET)
# The service returned FBX despite quad=false. Preserve it and make a labeled local
# GLB convenience derivative, at source placement, without another service request.
for o in bpy.context.selected_objects:o.select_set(False)
for o in returned:o.matrix_world.translation-=OFFSET;o.select_set(True)
bpy.context.view_layer.objects.active=returned[0]
DERIVED=OUT.parent/'derived/Ren_NativeHair_8000_Local.glb';DERIVED.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(DERIVED),export_format='GLB',use_selection=True,export_animations=False,export_materials='NONE')
for o in returned:o.matrix_world.translation+=OFFSET
local,lrecord,lv,lt=load(LOCAL,'Local7900',Vector((0,0,0)))
audit=dict(schema_version=1,status='STATIC_COMPARISON_NOT_PRODUCTION_APPROVAL',blender=bpy.app.version_string,
    native=nrecord,returned=rrecord,local_candidate=lrecord,
    local_glb_derivative=dict(path=DERIVED.relative_to(ROOT).as_posix(),sha256=digest(DERIVED),provider_original=False,materials='omitted',frame='unaltered provider source frame',note='Service returned FBX despite quad=false; this GLB is a local geometry convenience export, not provider bytes.'),
    count_contract='Rendered triangle count after import; source native quads preserved only in original FBX.',
    alignment='No fit, scale, or axis correction applied. Native and returned get the identical historical rigid review translation(-.09,0,.22) into head frame. Local fitted candidate already uses that frame and receives identity. All cameras/materials shared.')
for label,src,dst,dt in [('native_to_returned',nv,rv,rt),('returned_to_native',rv,nv,nt)]:
    tree=BVHTree.FromPolygons([Vector(p) for p in dst],dt.tolist(),all_triangles=True)
    distances=np.array([tree.find_nearest(Vector(p))[3] for p in src])
    audit[label+'_source_vertex_samples']=dict(p50=float(np.percentile(distances,50)),p95=float(np.percentile(distances,95)),p99=float(np.percentile(distances,99)),max=float(distances.max()),units='native source units',note='Surface proximity only, not visual/silhouette approval.')
write(OUT/'geometry-audit.json',audit)
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.eevee.taa_render_samples=24
scene.render.resolution_x=scene.render.resolution_y=900;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
scene.world=bpy.data.worlds.new('Matched_World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.07,.075,.09,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
scene.view_settings.view_transform='AgX'
cam=bpy.data.objects.new('MatchedCamera',bpy.data.cameras.new('MatchedCamera'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=1.4
for name,pos,energy in [('Key',(2,-2,3),230),('Fill',(2,2,1),120),('Rim',(-2,1,2),160)]:
    o=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));o.data.energy=energy;o.data.size=2;scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
target=Vector((-.045,0,.15));captures=[]
for label,objects in [('native',native),('tripo8000',returned),('local7900',local)]:
    for o in native+returned+local:o.hide_render=o not in objects
    for view,yaw in [('front',0),('quarter',-35),('profile',-90)]:
        a=math.radians(yaw);cam.location=target+Vector((math.cos(a),math.sin(a),0))*3;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        file=OUT/f'{label}-{view}.png';scene.render.filepath=str(file);bpy.ops.render.render(write_still=True)
        captures.append(dict(file=file.name,sha256=digest(file),subject=label,view=view,camera_position=list(cam.location),target=list(target),ortho_scale=1.4,resolution=[900,900]))
        write(OUT/'captures.json',captures);print('RENDERED',file.name,flush=True)
audit['sources_unchanged']=all(digest(ROOT/r['path'])==r['sha256'] for r in [nrecord,rrecord,lrecord]);write(OUT/'geometry-audit.json',audit)
# The scene is review-only and contains all three candidates as distinct hidden states.
for o in native+returned+local:o.hide_render=o not in returned
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_Hair_Retopology_Comparison.blend'))
print('AUDIT_COMPLETE',OUT,flush=True)
