"""Build a low-poly, painted asymmetric Ren shag from the reviewed local hair.

Writes only ren-shag-v1. Original hair, working head and frozen fitted-v5 stay
read-only. Run with ordinary Python; Blender is isolated in the background.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/generated/characters/ren/parts-workflow-v1/ren-shag-v1'
TRIAL=OUT/'trial-v7'
HEAD=ROOT/'art/generated/characters/ren/parts-workflow-v1/face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend'
HEAD_HASH='6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959'
PREVIOUS=ROOT/'art/generated/characters/ren/parts-workflow-v1/hair-cleanup-v1/fitted-v5/fitted-hair-workspace.blend'
BLENDER='C:/Program Files/Blender Foundation/Blender 5.1/blender.exe'


def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def dump(path,value):path.write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')


def texture():
    """Paint coherent root-to-tip bands in a fixed 8x8 material atlas."""
    import numpy as np
    from PIL import Image
    previous=OUT/'trial-v2/Ren_Hair_BaseColor_4K.png'
    if previous.exists():
        import shutil
        path=TRIAL/'Ren_Hair_BaseColor_4K.png';shutil.copyfile(previous,path);return path
    size=4096;tile=512
    atlas=np.empty((size,size,4),dtype=np.uint8);atlas[:]=[203,181,159,255]
    u=np.linspace(0,1,tile)[None,:];t=np.linspace(1,0,tile)[:,None]
    for k in range(64):
        rng=np.random.default_rng(735+k)
        # Closed-profile wrap: broad top/bottom planes get calm longitudinal
        # paint; narrow edges get warmer, darker rim strokes.
        wrap=.84+.16*np.sin(u*math.pi*2)**2
        root=.78+.22*np.minimum(1,t/.27)
        highlights=np.zeros((tile,tile),dtype=float)
        for _ in range(22):
            x=rng.uniform(0,1);width=rng.uniform(.002,.018)
            curve=x+.007*np.sin(t*4+rng.uniform(0,6))
            strength=rng.uniform(-.07,.07)
            highlights+=strength*np.exp(-((u-curve)/width)**2)*(np.sin(np.clip(t,0,1)*math.pi)**.3)
        broad=.008*np.sin(u*math.pi*14+t*.5+k)+.018*np.cos(t*5+k)
        value=np.clip(wrap*root+highlights+broad,.58,1.08)
        base=np.array([226,207,184],dtype=float)*(1+((k%5)-2)*.015)
        pixels=np.clip(value[:,:,None]*base,0,255).astype(np.uint8)
        row,col=divmod(k,8);atlas[row*tile:(row+1)*tile,col*tile:(col+1)*tile,:3]=pixels
    path=TRIAL/'Ren_Hair_BaseColor_4K.png';Image.fromarray(atlas).save(path)
    return path


def build():
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    assert sha(HEAD)==HEAD_HASH
    previous_hash=sha(PREVIOUS)
    bpy.ops.wm.open_mainfile(filepath=str(PREVIOUS))
    old=[];old_front=[]
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.name.startswith('Hair_fitted_'):
            centers=[];spans=[]
            for i in range(49):
                points=[obj.matrix_world@obj.data.vertices[i*12+j].co for j in range(12)]
                center=sum(points,Vector())/12
                centers.append(list(center));spans.append(max((v-center).length for v in points))
            (old_front if 'source_bang' in obj.name else old).append({'name':obj.name,'centers':centers,'spans':spans})
    assert len(old)==32
    bpy.ops.wm.open_mainfile(filepath=str(HEAD))
    head=bpy.data.objects['Ren_Head'];original_vertices=np.array([v.co[:] for v in head.data.vertices],dtype=np.float32)
    head_matrix=np.array(head.matrix_world)
    head_points=[head.matrix_world@v.co for v in head.data.vertices]
    bvh=BVHTree.FromPolygons(head_points,[list(p.vertices) for p in head.data.polygons])
    for obj in list(bpy.context.scene.objects):
        if obj.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(obj,do_unlink=True)
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks[1:]:key.value=1 if key.name=='mouthSeal' else 0
    # Material-only neutral context: the draft broad lip mask is not the hair
    # review subject. The source material and source head file remain unchanged.
    skin=bpy.data.materials.get('Ren_Draft_Skin');lips=bpy.data.materials.get('Ren_Draft_RoseLips')
    if skin and lips:
        src=skin.node_tree.nodes.get('Principled BSDF');dst=lips.node_tree.nodes.get('Principled BSDF')
        if src and dst:dst.inputs['Base Color'].default_value=src.inputs['Base Color'].default_value
    hairmat=bpy.data.materials.new('Ren_PaleBlond_PaintedHair');hairmat.use_nodes=True
    shader=hairmat.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.64
    shader.inputs['Specular IOR Level'].default_value=.24
    image=bpy.data.images.load(str(TRIAL/'Ren_Hair_BaseColor_4K.png'));image.colorspace_settings.name='sRGB'
    tex=hairmat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
    hairmat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
    groups={};records=[];tile_index=0
    center=Vector((-.06,0,.20))

    def mesh_obj(name,vertices,faces,uvs):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
        uv=mesh.uv_layers.new(name='RenHairAtlas')
        for face,coords in zip(mesh.polygons,uvs):
            for loop,coord in zip(face.loop_indices,coords):uv.data[loop].uv=coord
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        for p in mesh.polygons:p.use_smooth=True
        obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj);mesh.materials.append(hairmat)
        return obj

    def clump(name,centers,widths,group):
        nonlocal tile_index
        rings=len(centers);around=6;verts=[];faces=[];uvs=[];previous=None
        for i,(point,width) in enumerate(zip(centers,widths)):
            tangent=(centers[min(i+1,rings-1)]-centers[max(0,i-1)]).normalized()
            radial=(point-center).normalized()
            lateral=tangent.cross(radial).normalized() if previous is None else (previous-tangent*previous.dot(tangent)).normalized()
            previous=lateral.copy();normal=lateral.cross(tangent).normalized()
            if group.startswith('Fringe'):
                hitxs=[]
                for fraction in (-1,-.5,0,.5,1):
                    probe=point+lateral*(width*fraction)
                    hit,_,_,_=bvh.ray_cast(Vector((1,probe.y,probe.z)),Vector((-1,0,0)),2)
                    if hit is not None:hitxs.append(hit.x)
                if hitxs:point=point.copy();point.x=max(point.x,max(hitxs)+.017+.0017*(tile_index%5))
            for j in range(around):
                angle=math.pi*2*j/around
                vertex=point+lateral*(width*math.cos(angle))+normal*(max(.00035,width*.10)*math.sin(angle))
                # Moving a fringe sideways changes the forehead depth. Keep
                # every profile corner outside the current head, so a hidden
                # middle segment cannot re-emerge as a detached blunt tip.
                if not group.startswith('Fringe') and vertex.z>.17:
                    ray=vertex-center;hit,_,_,_=bvh.ray_cast(center,ray.normalized(),2)
                    if hit is not None and (hit-center).length>ray.length-.005:vertex=hit+ray.normalized()*.005
                verts.append(tuple(vertex))
        tile=tile_index;tile_index+=1;row,col=divmod(tile,8);pad=10/4096
        def uv(j,i):return(col/8+pad+(j/around)*(1/8-2*pad),1-(row+1)/8+pad+(i/(rings-1))*(1/8-2*pad))
        for i in range(rings-1):
            for j in range(around):
                faces.append((i*around+j,i*around+(j+1)%around,(i+1)*around+(j+1)%around,(i+1)*around+j))
                uvs.append([uv(j,i),uv(j+1,i),uv(j+1,i+1),uv(j,i+1)])
        faces.extend([tuple(reversed(range(around))),tuple((rings-1)*around+j for j in range(around))])
        uvs.extend([[uv(j,0) for j in reversed(range(around))],[uv(j,rings-1) for j in range(around)]])
        obj=mesh_obj(name,verts,faces,uvs);groups.setdefault(group,[]).append(obj)
        records.append({'name':name,'group':group,'atlas_tile':tile,'centerline':[list(p) for p in centers],
                        'half_widths':widths,'vertices':len(verts),'faces':len(faces),'triangles':(rings-1)*around*2+8})

    def sample(points,t):
        scaled=t*(len(points)-1);i=min(int(scaled),len(points)-2);u=scaled-i
        return Vector(points[i])*(1-u)+Vector(points[i+1])*u

    for index,source in enumerate(old):
        points=source['centers'];spans=source['spans'];centers=[];widths=[]
        end=1 if 'lower' in source['name'] else [.91,1,.83,.96][index%4]
        for ring in range(12):
            t=ring/11;s=t*end;p=sample(points,s)
            angle=math.atan2(p.y,p.x+.06)
            side=Vector((-math.sin(angle),math.cos(angle),0))
            p+=side*(math.sin(math.pi*t)*(.011 if index%2 else -.008))
            p.z+=math.sin(math.pi*t)*(.012 if index%3==0 else -.004)
            width=max(spans)*(1.03+.045*(index%3))*(.10+1.05*math.sin(math.pi*t)**.60) * (1-t)**.48
            centers.append(p);widths.append(max(.0002,width))
        group='Back' if abs(math.atan2(centers[6].y,centers[6].x+.06))>2.3 else ('Side_L' if centers[6].y<0 else 'Side_R')
        clump(source['name'].replace('Hair_fitted_','Shag_'),centers,widths,group)
        # Eight staggered silhouette accents, not one duplicate for every lock.
        if index in {0,4,8,13,17,21,26,30}:
            accents=[];accentwidth=[]
            for ring in range(12):
                t=ring/11;s=.12+t*.88;p=sample(points,s)
                angle=math.atan2(p.y,p.x+.06);side=Vector((-math.sin(angle),math.cos(angle),0))
                p+=side*(.013+.024*t)*(1 if index%2 else -1)
                p.z+=.018*math.sin(math.pi*t)-.018*t
                accents.append(p);accentwidth.append(max(.00015,max(spans)*.24*(1-t)**.65))
            clump('Shag_accent_'+str(index),accents,accentwidth,group)

    # Eight uneven front layers around the asymmetric part. Their varied tips
    # cross/clear different parts of the eyebrow, cheek and temple like Ren.
    sweeps=[
        ([ (.080,.485),(-.040,.440),(-.140,.340),(-.150,.210),(-.080,.075)],.067),
        ([ (.065,.480),(-.100,.425),(-.240,.300),(-.250,.120),(-.200,-.040)],.067),
        ([ (.030,.475),(-.180,.390),(-.280,.220),(-.290,.010),(-.230,-.115)],.053),
        ([ (.110,.480),(.020,.430),(-.060,.300),(-.080,.185),(-.020,.115)],.040),
        ([(-.050,.470),(-.280,.360),(-.340,.175),(-.340,-.010),(-.270,-.140)],.047),
        ([ (.085,.480),(.210,.415),(.290,.285),(.280,.100),(.210,-.060)],.067),
        ([ (.060,.480),(.290,.380),(.360,.220),(.340,.030),(.300,-.135)],.054),
        ([ (.050,.485),(.130,.410),(.170,.280),(.180,.125),(.130,.020)],.044)]
    for index in range(8):
        side=-1 if index<5 else 1
        controls,span=sweeps[index]
        centers=[];widths=[]
        for ring in range(12):
            t=ring/11;scaled=t*4;i=min(int(scaled),3);u=scaled-i
            a,b,c,d=[Vector(controls[k]) for k in (max(0,i-1),i,i+1,min(4,i+2))]
            yz=.5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)
            hit,_,_,_=bvh.ray_cast(Vector((1,yz.x,yz.y)),Vector((-1,0,0)),2)
            x=(hit.x if hit is not None else .075)+.024+.006*(index%3)
            centers.append(Vector((x,yz.x,yz.y)))
            widths.append(max(.0002,span*(.15+1.05*math.sin(math.pi*t)**.6)*(1-t)**.45))
        for _ in range(3):
            previous=[p.copy() for p in centers]
            for i in range(1,11):centers[i]=previous[i-1]*.18+previous[i]*.64+previous[i+1]*.18
        clump('Shag_fringe_'+str(index),centers,widths,'Fringe_L' if side<0 else 'Fringe_R')

    # Rear source sweeps diverge from the middle and leave a bald V. Five
    # staggered short locks cover that local region and continue to the nape.
    for index,tip_y in enumerate([-.20,-.10,.005,.115,.22]):
        points=[[-.10+.01*index,-.05+.025*index,.48],[-.22,-.08+.04*index,.46],
                [-.34,-.13+.065*index,.34],[-.39,-.17+.085*index,.18],
                [-.38,tip_y,.02],[-.31,tip_y*1.08,[-.10,-.15,-.11,-.16,-.085][index]]]
        centers=[];widths=[]
        for ring in range(12):
            t=ring/11;scaled=t*5;i=min(int(scaled),4);u=scaled-i
            a,b,c,d=[Vector(points[k]) for k in (max(0,i-1),i,i+1,min(5,i+2))]
            p=.5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)
            centers.append(p);widths.append(max(.0002,.083*(.10+1.08*math.sin(math.pi*t)**.60)*(1-t)**.48))
        clump('Shag_nape_fill_'+str(index),centers,widths,'Back')

    # Compact root shell under clumps, deliberately open along hairline.
    cols,rings=32,8;verts=[];faces=[];uvs=[]
    def scalp(direction):
        hit,n,_,_=bvh.ray_cast(center,direction.normalized(),2)
        if hit is None:raise RuntimeError('Root shell ray missed')
        return hit+n*.006
    verts.append(tuple(scalp(Vector((0,0,1)))))
    for ring in range(1,rings+1):
        for j in range(cols):
            theta=math.pi*2*j/cols;phi=(.59+1.2*((1-math.cos(theta))*.5)**.65)*ring/rings
            verts.append(tuple(scalp(Vector((math.sin(phi)*math.cos(theta),math.sin(phi)*math.sin(theta),math.cos(phi))))))
    tile=63;row,col=divmod(tile,8)
    def capuv(j,r):return(col/8+.003+(j/cols)*.119,1-(row+1)/8+.003+(r/rings)*.119)
    for j in range(cols):faces.append((0,1+j,1+(j+1)%cols));uvs.append([capuv(j,0),capuv(j,1),capuv(j+1,1)])
    for r in range(rings-1):
        for j in range(cols):
            a=1+r*cols+j;b=1+r*cols+(j+1)%cols;faces.append((a,b,b+cols,a+cols));uvs.append([capuv(j,r+1),capuv(j+1,r+1),capuv(j+1,r+2),capuv(j,r+2)])
    cap=mesh_obj('Ren_Hair_RootShell',verts,faces,uvs);groups['RootShell']=[cap]

    # Consolidate draw objects by semantic hair region. Locks stay disconnected
    # so silhouette editing and the deliberate atlas mapping remain simple.
    hair=[]
    for group,objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join();obj=bpy.context.object;obj.name='Ren_Hair_'+group;hair.append(obj)
    assert tile_index==53
    assert np.array_equal(original_vertices,np.array([v.co[:] for v in head.data.vertices],dtype=np.float32))
    assert np.array_equal(head_matrix,np.array(head.matrix_world))
    assert sha(HEAD)==HEAD_HASH and sha(PREVIOUS)==previous_hash
    report={'head_source':str(HEAD.relative_to(ROOT)),'head_sha256':HEAD_HASH,'previous_hair_workspace_sha256':previous_hash,
            'head_vertices_and_transform_unchanged':True,'coordinates':'+X front, +Z up, identity hair transforms in native head frame',
            'locks':records,'atlas':{'path':'Ren_Hair_BaseColor_4K.png','sha256':sha(TRIAL/'Ren_Hair_BaseColor_4K.png'),
               'dimensions':[4096,4096],'layout':'8x8 tiles, 53 unique closed-profile clump maps plus root tile 63; 10px padding',
               'method':'Deterministic root-to-tip painted color bands and narrow longitudinal streaks; no alpha cards or paid generation'},
            'objects':[],'renders':[],'exports':[],'status':'FIRST_VISUAL_REVIEW_REQUIRED'}
    refs=[ROOT/'art/characters/ren-model-sheet.png',*[
        ROOT/'art/generated/characters/ren/parts-reference-v1/hair/generation-inputs'/name
        for name in ('hair-front-v2.png','hair-right-v2.png','hair-back-v2.png')]]
    report['references']=[{'path':str(path.relative_to(ROOT)),'sha256':sha(path)} for path in refs]
    report['context_only']={'mouthSeal':1,'draft_lips_neutralized':True,'head_geometry_changed':False}
    for obj in hair:
        mesh=obj.data;mesh.calc_loop_triangles();incidence=np.zeros(len(mesh.edges),dtype=int)
        for loop in mesh.loops:incidence[loop.edge_index]+=1
        world=np.array([obj.matrix_world@v.co for v in mesh.vertices])
        report['objects'].append({'name':obj.name,'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'triangles':len(mesh.loop_triangles),
            'bounds_min':world.min(axis=0).tolist(),'bounds_max':world.max(axis=0).tolist(),
            'branched_edges':int((incidence>2).sum()),'boundary_edges':int((incidence==1).sum()),'uv_layers':[uv.name for uv in mesh.uv_layers]})
    report['totals']={key:sum(o[key] for o in report['objects']) for key in ('vertices','faces','triangles')}
    assert report['totals']['triangles']<=8000
    setup_and_render(hair,report)
    if PREVIEW_ONLY:
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(TRIAL/'Ren_Shag.blend'))
        dump(TRIAL/'report.json',report);print('REN_SHAG_PREVIEW_READY',flush=True);return
    bpy.ops.object.select_all(action='DESELECT')
    for obj in hair:obj.select_set(True)
    bpy.context.view_layer.objects.active=hair[0]
    for extension in ('fbx','glb'):
        path=TRIAL/('Ren_Shag.'+extension)
        if extension=='fbx':bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,
            use_triangles=False,add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
        else:bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=False,export_yup=True,export_animations=False)
        report['exports'].append({'path':path.name,'sha256':sha(path),'bytes':path.stat().st_size})
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(TRIAL/'Ren_Shag.blend'))
    report['exports'].append({'path':'Ren_Shag.blend','sha256':sha(TRIAL/'Ren_Shag.blend'),'bytes':(TRIAL/'Ren_Shag.blend').stat().st_size})
    dump(TRIAL/'report.json',report);print('REN_SHAG_READY',report['totals'],flush=True)


def setup_and_render(hair,report):
    import bpy
    from mathutils import Vector
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
    scene.world=bpy.data.worlds.new('Shag_ReviewWorld');scene.world.use_nodes=True
    scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.035,.045,.055,1)
    scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.6
    camera=bpy.data.objects.new('Shag_ReviewCamera',bpy.data.cameras.new('Shag_ReviewCamera'))
    scene.collection.objects.link(camera);scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=1.27
    for name,location,power in [('Key',(2,-2,3),170),('Fill',(2,2,1),110),('Rim',(-2,1,2),150)]:
        light=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));scene.collection.objects.link(light)
        light.data.energy=power;light.data.size=2;light.location=location;light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
    target=Vector((0,0,.07))
    views=[('front',0),('three-quarter',-35)] if PREVIEW_ONLY else [('front',0),('three-quarter',-35),('profile',-90),('back',180)]
    for name,yaw in views:
        angle=math.radians(yaw);camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        path=TRIAL/('combined-'+name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
        report['renders'].append({'path':path.name,'sha256':sha(path)});print('RENDER_READY',str(path),flush=True)


def verify():
    import bpy
    import numpy as np
    report=json.loads((TRIAL/'report.json').read_text(encoding='utf-8'))
    verified=[]
    for artifact in report['exports']:
        path=TRIAL/artifact['path'];assert sha(path)==artifact['sha256']
        if path.suffix=='.blend':continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==6
        vertices=faces=triangles=0;points=[];uv_coordinates=[]
        for obj in meshes:
            mesh=obj.data;mesh.calc_loop_triangles();vertices+=len(mesh.vertices);faces+=len(mesh.polygons);triangles+=len(mesh.loop_triangles)
            points.extend(tuple(obj.matrix_world@v.co) for v in mesh.vertices)
            assert len(mesh.uv_layers)==1
            uv_coordinates.extend(tuple(value.uv) for value in mesh.uv_layers.active.data)
            assert len(mesh.materials)==1
        assert triangles==7900
        assert faces==(3860 if path.suffix=='.fbx' else 7900)
        points=np.array(points);uvs=np.array(uv_coordinates)
        assert np.isfinite(points).all() and np.isfinite(uvs).all()
        assert uvs.min()>=0 and uvs.max()<=1
        maps=[im for im in bpy.data.images if tuple(im.size)==(4096,4096)]
        assert maps,'4K atlas missing after import'
        verified.append({'path':path.name,'sha256':sha(path),'objects':len(meshes),'vertices':vertices,'faces':faces,'triangles':triangles,
                         'bounds_min':points.min(axis=0).tolist(),'bounds_max':points.max(axis=0).tolist(),
                         'uv_bounds_min':uvs.min(axis=0).tolist(),'uv_bounds_max':uvs.max(axis=0).tolist(),
                         'atlas_dimensions':[list(im.size) for im in maps],'all_meshes_one_uv_layer_and_material':True})
    for a,b in zip(verified[0]['bounds_min'],verified[1]['bounds_min']):assert abs(a-b)<1e-5
    for a,b in zip(verified[0]['bounds_max'],verified[1]['bounds_max']):assert abs(a-b)<1e-5
    assert sha(HEAD)==HEAD_HASH
    bpy.ops.wm.open_mainfile(filepath=str(HEAD));source=bpy.data.objects['Ren_Head']
    headpoints=np.array([v.co[:] for v in source.data.vertices],dtype=np.float32);headmatrix=np.array(source.matrix_world)
    headkeys={k.name:np.array([v.co[:] for v in k.data],dtype=np.float32) for k in source.data.shape_keys.key_blocks}
    bpy.ops.wm.open_mainfile(filepath=str(TRIAL/'Ren_Shag.blend'));head=bpy.data.objects['Ren_Head']
    assert np.array_equal(headpoints,np.array([v.co[:] for v in head.data.vertices],dtype=np.float32))
    assert np.array_equal(headmatrix,np.array(head.matrix_world))
    for k in head.data.shape_keys.key_blocks:assert np.array_equal(headkeys[k.name],np.array([v.co[:] for v in k.data],dtype=np.float32))
    assert head.data.shape_keys.key_blocks['mouthSeal'].value==1
    for image in bpy.data.images:
        if tuple(image.size)==(4096,4096):image.pack();image.filepath='//Ren_Hair_BaseColor_4K.png'
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(TRIAL/'Ren_Shag.blend'))
    for artifact in report['exports']:
        if artifact['path'].endswith('.blend'):
            path=TRIAL/artifact['path'];artifact['sha256']=sha(path);artifact['bytes']=path.stat().st_size
    for capture in report['renders']:assert sha(TRIAL/capture['path'])==capture['sha256']
    refs=[ROOT/'art/characters/ren-model-sheet.png',*[
        ROOT/'art/generated/characters/ren/parts-reference-v1/hair/generation-inputs'/name
        for name in ('hair-front-v2.png','hair-right-v2.png','hair-back-v2.png')]]
    report['references']=[{'path':str(path.relative_to(ROOT)),'sha256':sha(path)} for path in refs]
    report['context_only']={'mouthSeal':1,'draft_lips_neutralized':True,'head_geometry_changed':False}
    report['status']='LOCAL_HAIR_CANDIDATE_REQUIRES_ARTIST_REVIEW'
    dump(TRIAL/'report.json',report)
    dump(TRIAL/'verification.json',{'status':'PASS','exports':verified,'head_source_sha256':HEAD_HASH,
        'head_vertices_world_matrix_and_all_shape_key_coordinates_unchanged':True,'closed_context_mouth_seal':True,
        'packed_atlas_in_blend':True,'render_hashes_checked':len(report['renders']),
        'atlas_layout_checks':'53 unique clump tiles plus root tile63; separated tile rectangles within 0..1. Tiny closed root/tip caps intentionally collapse to UV tile boundary.',
        'limitations':'Render review required for likeness/intersections. No device performance measured. Atlas is procedural longitudinal paint, not artist-approved final paint.'})
    print('REN_SHAG_VERIFY_PASS',flush=True)


def capfit():
    import bpy
    import numpy as np
    from mathutils import Vector,Euler
    directory=OUT/'selected-v1';directory.mkdir(parents=True,exist_ok=True)
    accessories=ROOT/'art/generated/characters/ren/parts-workflow-v1/head-accessories-v1/Ren_Head_Accessories.blend'
    source_hash=sha(TRIAL/'Ren_Shag.blend');accessory_hash=sha(accessories)
    bpy.ops.wm.open_mainfile(filepath=str(TRIAL/'Ren_Shag.blend'))
    hair=[obj for obj in bpy.context.scene.objects if obj.type=='MESH' and obj.name.startswith('Ren_Hair_')]
    import shutil
    shutil.copyfile(TRIAL/'Ren_Hair_BaseColor_4K.png',directory/'Ren_Hair_BaseColor_4K.png')
    basis_before={obj.name:hashlib.sha256(np.array([v.co[:] for v in obj.data.vertices],dtype=np.float32).tobytes()).hexdigest() for obj in hair}
    ear_changes=[]
    for obj in hair:
        moved=[]
        if obj.name=='Ren_Hair_Side_L':
            for vertex in obj.data.vertices:
                co=vertex.co
                if co.y<-.24 and -.20<co.x<.04 and .02<co.z<.20:
                    w=math.exp(-((co.x+.095)/.10)**2-((co.y+.32)/.10)**2-((co.z-.095)/.08)**2)
                    delta=Vector((-.055*w,.045*w,0))
                    if delta.length>.001:
                        before=list(co);vertex.co+=delta;moved.append({'vertex':vertex.index,'before':before,'after':list(vertex.co)})
        if moved:ear_changes.append({'object':obj.name,'vertices':moved,'policy':'Small local tuck behind the negative-Y upper helix; all other cap-off vertices unchanged.'})
    head=bpy.data.objects['Ren_Head'];head_hash=hashlib.sha256(np.array([v.co[:] for v in head.data.vertices],dtype=np.float32).tobytes()).hexdigest()
    with bpy.data.libraries.load(str(accessories),link=False) as (available,loaded):
        loaded.objects=[name for name in available.objects if name=='Ren_Cap' or name.startswith('Ren_RightEar_')]
    for obj in loaded.objects:bpy.context.scene.collection.objects.link(obj)
    center=Vector((-.035,0,.19));rotation=Euler(tuple(math.radians(a) for a in (3,3,-7)),'XYZ').to_matrix();inverse=rotation.transposed()
    records=[]
    for obj in hair:
        obj.shape_key_add(name='Basis');key=obj.shape_key_add(name='capOn')
        moved=0;maximum=0
        for vertex,target in zip(obj.data.vertices,key.data):
            point=inverse@(obj.matrix_world@vertex.co-center)
            if point.z<=-.10:continue
            blend=max(0,min(1,(point.z+.10)/.10));blend=blend*blend*(3-2*blend)
            corrected=point.copy();corrected.z=min(corrected.z,.36)
            available=math.sqrt(max(.02,1-(max(0,corrected.z)/.38)**2))*.92
            radius=math.sqrt((point.x/.39)**2+(point.y/.33)**2)
            if radius>available:
                corrected.x*=available/radius;corrected.y*=available/radius
            fitted=point.lerp(corrected,blend);new=rotation@fitted+center
            delta=(new-vertex.co).length
            if delta>1e-7:moved+=1;maximum=max(maximum,delta)
            target.co=new
        key.value=1
        records.append({'object':obj.name,'moved_vertices':moved,'maximum_displacement_native_units':maximum,
                        'policy':'Upper roots compressed inside fixed cap ellipsoid; smooth blend from local z=-0.10 to0; lower ends unchanged.'})
    scene=bpy.context.scene;target=Vector((0,0,.07));scene.camera.data.ortho_scale=1.35;renders=[]
    for state in ('cap-on','cap-off'):
        bpy.data.objects['Ren_Cap'].hide_render=state=='cap-off'
        for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=1 if state=='cap-on' else 0
        for name,yaw in [('front',0),('three-quarter',-35),('profile',-90),('back',180)]:
            angle=math.radians(yaw);scene.camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
            scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
            path=directory/(state+'-'+name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
            renders.append({'path':path.name,'sha256':sha(path)})
    assert head_hash==hashlib.sha256(np.array([v.co[:] for v in head.data.vertices],dtype=np.float32).tobytes()).hexdigest()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in hair:obj.select_set(True);obj.data.shape_keys.key_blocks['capOn'].value=0
    bpy.context.view_layer.objects.active=hair[0];exports=[]
    for extension in ('fbx','glb'):
        path=directory/('Ren_Shag_CapState.'+extension)
        if extension=='fbx':bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,
            use_triangles=False,add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
        else:bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=False,export_yup=True,export_animations=False)
        exports.append({'path':path.name,'sha256':sha(path),'bytes':path.stat().st_size})
    for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=1
    bpy.data.objects['Ren_Cap'].hide_render=False
    for image in bpy.data.images:
        if image.size[0]:
            try:image.pack()
            except RuntimeError:pass
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(directory/'Ren_Shag_CapState.blend'))
    assert sha(TRIAL/'Ren_Shag.blend')==source_hash and sha(accessories)==accessory_hash
    dump(directory/'report.json',{'status':'SELECTED_FOR_ASSEMBLED_PAINTED_REVIEW_NOT_FINAL_LIKENESS','hair_source':str((TRIAL/'Ren_Shag.blend').relative_to(ROOT)),
        'hair_source_sha256':source_hash,'cap_source_sha256':accessory_hash,'head_vertices_unchanged':True,'hair_triangles':7900,
        'controls':'All six hair meshes: capOn=0 for cap-off basis, capOn=1 with cap visible; exported default0, review blend default1.',
        'cap_geometry_unchanged':True,'coordinates':'+X front +Z up, identity placement in current head frame',
        'changes':records,'cap_off_basis_before_ear_edit_sha256':basis_before,'ear_clearance_local_edit':ear_changes,
        'renders':renders,'exports':exports,
        'remaining_visible_defects':['Scalloped abrupt root joins are overlapping geometric junctions, not a demonstrated normals-only defect.',
          'Flattened lock surfaces retain ridge shading; the reviewed silhouette is preserved.',
          'Procedural hair paint lacks the artist reference\'s final strand breakup and graphic highlight placement.'],
        'hair_only_export_objects':[obj.name for obj in hair],
        'context_only':'Review blend includes unchanged source head and appended accessory context. FBX/GLB contain hair only.'})
    print('REN_SHAG_CAP_STATE_READY',str(directory),flush=True)


def verify_selected(directory=None):
    import bpy
    import numpy as np
    directory=directory or OUT/'selected-v1';report=json.loads((directory/'report.json').read_text(encoding='utf-8'))
    source=ROOT/report['hair_source'];assert sha(source)==report['hair_source_sha256']
    bpy.ops.wm.open_mainfile(filepath=str(source))
    original={o.name:np.array([v.co[:] for v in o.data.vertices],dtype=np.float32) for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Ren_Hair_')}
    native_head=bpy.data.objects['Ren_Head'];headvertices=np.array([v.co[:] for v in native_head.data.vertices],dtype=np.float32)
    headmatrix=np.array(native_head.matrix_world)
    headkeys={k.name:np.array([v.co[:] for v in k.data],dtype=np.float32) for k in native_head.data.shape_keys.key_blocks}
    bpy.ops.wm.open_mainfile(filepath=str(directory/'Ren_Shag_CapState.blend'))
    head=bpy.data.objects['Ren_Head'];assert np.array_equal(headvertices,np.array([v.co[:] for v in head.data.vertices],dtype=np.float32))
    assert np.array_equal(headmatrix,np.array(head.matrix_world))
    for k in head.data.shape_keys.key_blocks:assert np.array_equal(headkeys[k.name],np.array([v.co[:] for v in k.data],dtype=np.float32))
    ear={edit['object']:{v['vertex']:v for v in edit['vertices']} for edit in report.get('ear_clearance_local_edit',[])}
    expected_states={0:[],1:[]};basis_changes=[];counts=[]
    for obj in bpy.context.scene.objects:
        if obj.name not in original:continue
        basis=np.array([v.co[:] for v in obj.data.shape_keys.key_blocks['Basis'].data],dtype=np.float32)
        changed=np.flatnonzero(np.any(original[obj.name]!=basis,axis=1));expected=ear.get(obj.name,{})
        assert set(changed)==set(expected)
        for index in changed:assert np.allclose(basis[index],expected[int(index)]['after'],atol=1e-7)
        basis_changes.append({'object':obj.name,'changed_vertices':len(changed),'matches_recorded_ear_edit_only':True})
        obj.data.calc_loop_triangles()
        counts.append({'object':obj.name,'vertices':len(obj.data.vertices),'faces':len(obj.data.polygons),'triangles':len(obj.data.loop_triangles)})
        for state,keyname in [(0,'Basis'),(1,'capOn')]:expected_states[state].extend(tuple(obj.matrix_world@v.co) for v in obj.data.shape_keys.key_blocks[keyname].data)
    expected_bounds={state:[np.min(points,axis=0).tolist(),np.max(points,axis=0).tolist()] for state,points in expected_states.items()}
    assert sum(o['triangles'] for o in counts)==7900
    if directory.name=='selected-v2':
        assert sum(len(c['insufficient_cap_head_corridors']) for c in report['changes'])==0
    results=[]
    for artifact in report['exports']:
        path=directory/artifact['path'];assert sha(path)==artifact['sha256']
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        hair=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(hair)==6
        triangles=0;states={0:[],1:[]};names=[]
        for obj in hair:
            mesh=obj.data;mesh.calc_loop_triangles();triangles+=len(mesh.loop_triangles)
            assert len(mesh.uv_layers)==1 and len(mesh.materials)==1
            assert mesh.shape_keys and len(mesh.shape_keys.key_blocks)==2
            key=next(k for k in mesh.shape_keys.key_blocks if k.name.endswith('capOn'))
            assert key.value==0;names.append({'object':obj.name,'morph':key.name,'default_value':key.value})
            for state,block in [(0,mesh.shape_keys.key_blocks[0]),(1,key)]:states[state].extend(tuple(obj.matrix_world@v.co) for v in block.data)
        assert triangles==7900
        bounds={state:[np.min(points,axis=0).tolist(),np.max(points,axis=0).tolist()] for state,points in states.items()}
        for state in (0,1):assert np.allclose(bounds[state],expected_bounds[state],atol=1e-5)
        assert any(tuple(image.size)==(4096,4096) for image in bpy.data.images)
        results.append({'file':path.name,'sha256':sha(path),'triangles':triangles,'objects':6,'cap_states_bounds':bounds,
                        'morph_controls':names,'uv_and_material_present_on_all_meshes':True,'texture_dimensions':[4096,4096]})
    assert sha(HEAD)==HEAD_HASH
    for capture in report['renders']:assert sha(directory/capture['path'])==capture['sha256']
    check={'status':'PASS','head_vertices_matrix_and_all_shape_coordinates_unchanged':True,
           'reviewed_cap_off_basis_changes':basis_changes,'cap_off_and_cap_on_import_bounds_match':True,
           'triangles':7900,'source_mesh_counts':counts,'exports':results,'render_hashes_checked':len(report['renders']),
           'limits':'Checks integrity, local edit scope and export morph/material survival. Artist likeness and device performance remain unapproved.'}
    dump(directory/'verification.json',check)
    paths=[directory/'Ren_Shag_CapState.blend',directory/'Ren_Shag_CapState.fbx',directory/'Ren_Shag_CapState.glb',
           directory/'Ren_Hair_BaseColor_4K.png',directory/'report.json',directory/'verification.json',
           *[directory/item['path'] for item in report['renders']]]
    dump(directory/'manifest.json',{'selected_for':'Combined painted Ren review; not final artist approval','coordinates':'+X front, +Z up, identity with current head',
        'preferred_hair_glb':'Ren_Shag_CapState.glb','hair_fbx':'Ren_Shag_CapState.fbx','combined_review_blend':'Ren_Shag_CapState.blend',
        'texture':'Ren_Hair_BaseColor_4K.png','material':'Ren_PaleBlond_PaintedHair','mesh_objects':report['hair_only_export_objects'],
        'triangles':7900,'controls':{'cap_off':{'cap_visible':False,'all_hair_capOn':0},'cap_on':{'cap_visible':True,'all_hair_capOn':1}},
        'head_source_sha256':HEAD_HASH,'reviewed_hair_source_sha256':report['hair_source_sha256'],'accessory_source_sha256':report['cap_source_sha256'],
        'files':[{'path':p.name,'sha256':sha(p),'bytes':p.stat().st_size} for p in paths],
        'remaining_visible_defects':report['remaining_visible_defects']})
    print('REN_SHAG_SELECTED_VERIFY_PASS',flush=True)


def fringe_v2():
    """Early painted-context review of stronger below-brim diagonal fringe."""
    import bpy
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    directory=OUT/'selected-v2/early-fringe-v2';directory.mkdir(parents=True,exist_ok=True)
    assembly=ROOT/'art/generated/characters/ren/parts-workflow-v1/complete-head-v1/Ren_CompleteHead_Review.blend'
    source_hash=sha(assembly);bpy.ops.wm.open_mainfile(filepath=str(assembly))
    head=bpy.data.objects['Ren_Head'];headpoints=np.array([v.co[:] for v in head.data.vertices],dtype=np.float32)
    head_bvh=BVHTree.FromPolygons([head.matrix_world@v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    bvh=BVHTree.FromPolygons([head.matrix_world@v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks[1:]:key.value=1 if key.name in ('mouthSeal','capOn') else 0
    obj=bpy.data.objects['Ren_Hair_Fringe_L'];basis=obj.data.shape_keys.key_blocks['Basis'];cap=obj.data.shape_keys.key_blocks['capOn']
    assert len(basis.data)==360
    deltas=[(-.15,.045),(-.07,.018),(0,0),(-.21,.035),(0,0)];changes=[]
    for lock,(dz,dy) in enumerate(deltas):
        for ring in range(12):
            t=ring/11;ids=[lock*72+ring*6+j for j in range(6)]
            old=[basis.data[i].co.copy() for i in ids];oldcap=[cap.data[i].co.copy() for i in ids]
            updated=[co+Vector((0,dy*t**1.5,dz*t**1.15)) for co in old]
            hits=[]
            for co in updated:
                hit,_,_,_=bvh.ray_cast(Vector((1,co.y,co.z)),Vector((-1,0,0)),2)
                if hit is not None:hits.append(hit.x)
            if hits:
                shift=max(0,max(hits)+.020-min(co.x for co in updated))
                for co in updated:co.x+=shift
            z=sum(co.z for co in updated)/6
            # Visible fringe is not drawn back into the cap. Only its upper
            # roots inherit the prior cap's compression during this preview.
            blend=max(0,min(1,(z-.135)/.07));blend=blend*blend*(3-2*blend)
            for i,co,oldco,oldcapco in zip(ids,updated,old,oldcap):
                basis.data[i].co=co;obj.data.vertices[i].co=co
                from mathutils import Euler
                rot=Euler(tuple(math.radians(a) for a in (3,3,-7)),'XYZ').to_matrix()
                local=rot.transposed()@(co-Vector((-.035,0,.19)))
                fitted=local.copy();radius=math.sqrt((local.x/.39)**2+(local.y/.33)**2)
                available=math.sqrt(max(.02,1-(max(0,local.z)/.38)**2))*.89
                if radius>available:fitted.x*=available/radius;fitted.y*=available/radius
                cap.data[i].co=co.lerp(rot@fitted+Vector((-.035,0,.19)),blend)
                if (co-oldco).length>1e-7:changes.append({'vertex':i,'before':list(oldco),'after':list(co)})
    scene=bpy.context.scene;target=Vector((0,0,.05));renders=[]
    for name,yaw in [('front',0),('quarter',-35)]:
        angle=math.radians(yaw);scene.camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
        scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        path=directory/(name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
        renders.append({'path':path.name,'sha256':sha(path)})
    assert np.array_equal(headpoints,np.array([v.co[:] for v in head.data.vertices],dtype=np.float32))
    assert sha(assembly)==source_hash
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(directory/'Ren_Shag_FringePreview.blend'))
    dump(directory/'report.json',{'status':'EARLY_FRINGE_DIRECTION_REVIEW_USING_V1_CAP_PENDING_V2_CAP','source_assembly_sha256':source_hash,
        'hair_triangles':7900,'head_vertices_unchanged':True,'changed_object':'Ren_Hair_Fringe_L','changes':changes,
        'cap_state_policy':'Visible frontal fringe below z0.135 retained exactly; smooth cap compression on upper roots z0.135..0.205',
        'renders':renders,'limitations':'This preview still uses the old cap. New lower-crown accessory fit is pending.'})
    print('REN_SHAG_FRINGE_V2_PREVIEW_READY',str(directory),flush=True)


def select_v2():
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    directory=OUT/'selected-v2';directory.mkdir(parents=True,exist_ok=True)
    source=directory/'early-fringe-v2/Ren_Shag_FringePreview.blend'
    caps=ROOT/'art/generated/characters/ren/parts-workflow-v1/head-accessories-v2/band-clearance-v1/Ren_Head_Accessories.blend'
    sourcehash,caphash=sha(source),sha(caps)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    head=bpy.data.objects['Ren_Head'];headpoints=np.array([v.co[:] for v in head.data.vertices],dtype=np.float32)
    head_bvh=BVHTree.FromPolygons([head.matrix_world@v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    bpy.data.objects.remove(bpy.data.objects['Ren_Cap'],do_unlink=True)
    with bpy.data.libraries.load(str(caps),link=False) as (available,loaded):loaded.objects=['Ren_Cap']
    cap=loaded.objects[0];bpy.context.scene.collection.objects.link(cap)
    positions=[cap.matrix_world@v.co for v in cap.data.vertices]
    cap_bvh=BVHTree.FromPolygons(positions,[list(p.vertices) for p in cap.data.polygons])
    # A short virtual skirt continues the crown boundary beneath the band.
    # Otherwise a below-band vertex can remain far outside the shell and its
    # connecting quad pierces the crown above the band despite valid endpoints.
    fit_points=list(positions);fit_faces=[list(p.vertices) for p in cap.data.polygons]
    skirt_start=len(fit_points)
    for i in range(33):fit_points.append(positions[i]-Vector((0,0,.09)))
    for i in range(32):fit_faces.append([i,i+1,skirt_start+i+1,skirt_start+i])
    cap_fit_bvh=BVHTree.FromPolygons(fit_points,fit_faces)
    origin=Vector((-.060,0,.20));hair=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Ren_Hair_')]
    records=[]
    for obj in hair:
        basis=obj.data.shape_keys.key_blocks['Basis'];key=obj.data.shape_keys.key_blocks['capOn'];moved=0;maximum=0;misses=[];corridors=[]
        for vertex,target in zip(basis.data,key.data):
            point=obj.matrix_world@vertex.co;theta=math.atan2(point.y,point.x+.043)
            band=.218+.030*math.cos(theta)+.011*math.sin(theta)
            blend=max(0,min(1,(point.z-(band-.050))/.030));blend=blend*blend*(3-2*blend)
            fitted=point.copy()
            if blend>0:
                ray=point-origin;direction=ray.normalized()
                skin,_,_,skin_distance=head_bvh.ray_cast(origin,direction,2)
                hit,_,_,cap_distance=cap_fit_bvh.ray_cast(origin,direction,2)
                minimum=(skin_distance+.003) if skin is not None else 0
                maximum_radius=(cap_distance-.003) if hit is not None else ray.length
                if hit is not None and skin is not None and maximum_radius<minimum:
                    corridors.append({'point':list(point),'head_radius':skin_distance,'cap_radius':cap_distance,'clearance':cap_distance-skin_distance})
                    target_radius=minimum
                else:target_radius=max(minimum,min(ray.length,maximum_radius))
                # Cap intersections are a hard constraint. A partially blended
                # projection still lies outside the cap and creates pale chips.
                fitted=origin+direction*target_radius
                if skin is not None and (fitted-origin).length<minimum:fitted=origin+direction*minimum
                if hit is None and point.z>band+.02:misses.append(list(point))
            target.co=fitted
            delta=(point-fitted).length
            if delta>1e-7:moved+=1;maximum=max(maximum,delta)
        key.value=1;obj.data.update()
        records.append({'object':obj.name,'moved_capOn_vertices':moved,'max_displacement':maximum,'upper_root_ray_misses':misses,'insufficient_cap_head_corridors':corridors})
    scene=bpy.context.scene
    if scene.render.engine=='CYCLES':scene.cycles.samples=24
    target=Vector((0,0,.05));renders=[]
    for state in (('cap-on',) if PREVIEW_ONLY else ('cap-on','cap-off')):
        cap.hide_render=state=='cap-off'
        for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=1 if state=='cap-on' else 0
        for name,yaw in ([('front',0),('quarter',-35)] if PREVIEW_ONLY else [('front',0),('quarter',-35),('profile',-90),('back',180)]):
            angle=math.radians(yaw);scene.camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
            scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
            path=directory/(state+'-'+name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
            renders.append({'path':path.name,'sha256':sha(path)})
    # An actual material-only base-color pass separates pale paint from cap
    # shadows. Restore each original material surface link immediately after.
    cap.hide_render=False
    for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=1
    restore=[]
    for mat in {m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m and m.use_nodes}:
        tree=mat.node_tree;output=next((n for n in tree.nodes if n.type=='OUTPUT_MATERIAL'),None)
        shader=tree.nodes.get('Principled BSDF')
        if not output or not shader:continue
        oldlink=output.inputs['Surface'].links[0] if output.inputs['Surface'].links else None
        if not oldlink:continue
        oldsocket=oldlink.from_socket;emission=tree.nodes.new('ShaderNodeEmission');base=shader.inputs['Base Color']
        if base.links:tree.links.new(base.links[0].from_socket,emission.inputs['Color'])
        else:emission.inputs['Color'].default_value=base.default_value
        tree.links.new(emission.outputs[0],output.inputs['Surface']);restore.append((tree,output,oldsocket,emission))
    for name,yaw in [('front',0),('quarter',-35)]:
        angle=math.radians(yaw);scene.camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
        scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        path=directory/('diffuse-'+name+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
        renders.append({'path':path.name,'sha256':sha(path)})
    for tree,output,oldsocket,emission in restore:tree.links.new(oldsocket,output.inputs['Surface']);tree.nodes.remove(emission)
    assert np.array_equal(headpoints,np.array([v.co[:] for v in head.data.vertices],dtype=np.float32))
    import shutil
    shutil.copyfile(OUT/'selected-v1/Ren_Hair_BaseColor_4K.png',directory/'Ren_Hair_BaseColor_4K.png')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in hair:obj.select_set(True);obj.data.shape_keys.key_blocks['capOn'].value=0
    bpy.context.view_layer.objects.active=hair[0];exports=[]
    for extension in ('fbx','glb'):
        path=directory/('Ren_Shag_CapState.'+extension)
        if extension=='fbx':bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},use_mesh_modifiers=False,use_triangles=False,add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
        else:bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_apply=False,export_yup=True,export_animations=False)
        exports.append({'path':path.name,'sha256':sha(path),'bytes':path.stat().st_size})
    for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=1
    for image in bpy.data.images:
        if image.size[0]:
            try:image.pack()
            except RuntimeError:pass
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(directory/'Ren_Shag_CapState.blend'))
    assert sha(source)==sourcehash and sha(caps)==caphash
    dump(directory/'report.json',{'status':'SELECTED_V2_NEW_CAP_AND_BELOW_BRIM_FRINGE_REQUIRES_ASSEMBLED_REVIEW',
        'hair_source':str(source.relative_to(ROOT)),'hair_source_sha256':sourcehash,'cap_source':str(caps.relative_to(ROOT)),'cap_source_sha256':caphash,
        'head_vertices_unchanged':True,'hair_triangles':7900,'coordinates':'+X front +Z up identity source frame',
        'hair_only_export_objects':[obj.name for obj in hair],'changes':records,'exports':exports,'renders':renders,
        'controls':'Set capOn=1 on all six hair meshes with V2 cap visible; capOn=0 with cap hidden. Export default0, review blend default1.',
        'fit_method':'Hard radial interval between head+0.003 and actual V2 cap-0.003; temporary0.09 fitting skirt continues crown boundary beneath band to constrain crossing quads. No skirt is added to output geometry. Basis matches extended-fringe preview.',
        'remaining_visible_defects':['Broad flattened lock profiles and abrupt root junctions remain.','Procedural paint lacks final artist strand breakup.'],
        'diffuse_views':'Emission material pass sourced from the actual base-color inputs; original PBR materials restored in exports and blend.'})
    print('REN_SHAG_SELECTED_V2_READY',str(directory),flush=True)


def main():
    global PREVIEW_ONLY
    parser=argparse.ArgumentParser();parser.add_argument('--blender',action='store_true');parser.add_argument('--verify',action='store_true');parser.add_argument('--capfit',action='store_true');parser.add_argument('--preview',action='store_true');parser.add_argument('--verify-selected',action='store_true');parser.add_argument('--fringe-v2',action='store_true');parser.add_argument('--select-v2',action='store_true');parser.add_argument('--verify-v2',action='store_true');args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else None)
    PREVIEW_ONLY=args.preview
    TRIAL.mkdir(parents=True,exist_ok=True)
    if args.blender:
        if args.verify_v2:verify_selected(OUT/'selected-v2');return
        (select_v2 if args.select_v2 else fringe_v2 if args.fringe_v2 else verify_selected if args.verify_selected else capfit if args.capfit else verify if args.verify else build)();return
    if not args.verify and not args.capfit and not args.verify_selected and not args.fringe_v2 and not args.select_v2 and not args.verify_v2:texture()
    env=os.environ.copy()
    for key,part in [('BLENDER_USER_RESOURCES',''),('BLENDER_USER_CONFIG','config'),('BLENDER_USER_SCRIPTS','scripts'),('BLENDER_USER_EXTENSIONS','extensions')]:
        path=OUT/'.blender-user'/part;path.mkdir(parents=True,exist_ok=True);env[key]=str(path)
    command=[BLENDER,'--background','--factory-startup','--offline-mode','--threads','8','--python-exit-code','1','--python',str(Path(__file__).resolve()),'--','--blender']
    if args.verify:command.append('--verify')
    if args.capfit:command.append('--capfit')
    if args.preview:command.append('--preview')
    if args.verify_selected:command.append('--verify-selected')
    if args.fringe_v2:command.append('--fringe-v2')
    if args.select_v2:command.append('--select-v2')
    if args.verify_v2:command.append('--verify-v2')
    with (TRIAL/('verify.log' if args.verify else 'build.log')).open('w',encoding='utf-8') as log:
        process=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
        while process.poll() is None:
            try:process.wait(timeout=30)
            except subprocess.TimeoutExpired:print('Ren shag building; isolated Blender PID',process.pid,flush=True)
    if process.returncode:raise RuntimeError('Build failed; see ren-shag-v1/trial-v1/build.log')
    print('REN_SHAG_READY',TRIAL/'report.json')


if __name__=='__main__':main()
