"""Continuous ellipsoid-space iris rotation; immutable input, isolated derivative."""
from pathlib import Path
import hashlib
import json
import math
import sys

import bpy
import numpy as np
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'art/generated/characters/ren/parts-workflow-v1'
SOURCE=BASE/'face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend'
EXPECTED='6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959'
OUT=BASE/'gaze-repair-v1'
RADII=np.array([.060,.062,.060])
YAW=math.asin(.010/.062)
PITCH=math.asin(.007/.060)


def write(name,data):
    (OUT/name).write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')


def triangle_origin_distance(a,b,c):
    e=b-a;f=c-a
    matrix=np.array([[e@e,e@f],[e@f,f@f]])
    uv=np.linalg.lstsq(matrix,-np.array([a@e,a@f]),rcond=None)[0]
    values=[]
    if min(uv)>=0 and sum(uv)<=1:values.append(float(np.linalg.norm(a+uv[0]*e+uv[1]*f)))
    for p,q in ((a,b),(b,c),(c,a)):
        v=q-p;t=np.clip(-p@v/max(v@v,1e-20),0,1)
        values.append(float(np.linalg.norm(p+t*v)))
    return min(values)


def signature(o):
    mesh=o.data
    h=hashlib.sha256()
    h.update(np.array([v.co[:] for v in mesh.vertices],dtype='<f4').tobytes())
    for p in mesh.polygons:h.update(np.array(p.vertices,dtype='<i4').tobytes())
    for layer in mesh.uv_layers:h.update(np.array([uv.uv[:] for uv in layer.data],dtype='<f4').tobytes())
    if mesh.shape_keys:
        for k in mesh.shape_keys.key_blocks:
            h.update(k.name.encode());h.update(np.array([v.co[:] for v in k.data],dtype='<f4').tobytes())
    return h.hexdigest()


def empty(name,parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(o);o.parent=parent
    return o


def configure_scene():
    s=bpy.context.scene
    s.render.resolution_x=s.render.resolution_y=768;s.render.resolution_percentage=100
    if hasattr(s,'cycles'):s.cycles.samples=24
    s.view_layers[0].material_override=None
    return s


def pose(gx,gy,blink):
    for o in bpy.context.scene.objects:
        if o.name.startswith('Ren_GazeRotate_'):
            o['gazeX']=gx;o['gazeY']=gy
            o.update_tag()
        if o.type=='MESH' and o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks:
                if k.name.startswith('eyeBlink'):k.value=blink
    bpy.context.view_layer.update()


def render(name,quarter=False,macro=True):
    s=bpy.context.scene;c=s.camera
    target=Vector((.23,0,.085)) if macro else Vector((.1,0,0))
    c.data.type='ORTHO';c.data.ortho_scale=.40 if macro else 1.1
    c.location=target+Vector((2,-1.25 if quarter else 0,0))
    c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)


def main():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
    OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));configure_scene()
    all_before={o.name:signature(o) for o in bpy.context.scene.objects if o.type=='MESH'}
    eyes=[]
    for side,sign in [('L',1),('R',-1)]:
        iris=bpy.data.objects[f'Ren_Eye_{side}_Iris'];center=np.array([.185,sign*.119,.085])
        world=np.array([iris.matrix_world@v.co for v in iris.data.vertices])
        normalized=(world-center)/RADII
        iris.data.calc_loop_triangles();triangles=[tuple(t.vertices) for t in iris.data.loop_triangles]
        minimum=min(triangle_origin_distance(*normalized[list(t)]) for t in triangles)
        assert minimum>1.0005,(side,minimum)
        # Keep topology/UVs/vertex colors; unsafe additive keys cannot remain active.
        iris.shape_key_clear()
        for v,q in zip(iris.data.vertices,normalized):v.co=q
        frame=empty(f'Ren_GazeEllipsoid_{side}')
        frame.location=center;frame.scale=RADII
        rotation=empty(f'Ren_GazeRotate_{side}',frame)
        rotation.rotation_mode='XYZ'
        rotation['gazeX']=0.;rotation['gazeY']=0.
        for axis,prop,factor in [(1,'gazeY',-PITCH),(2,'gazeX',YAW)]:
            driver=rotation.driver_add('rotation_euler',axis).driver
            variable=driver.variables.new();variable.name='gaze';variable.type='SINGLE_PROP'
            variable.targets[0].id=rotation;variable.targets[0].data_path=f'["{prop}"]'
            driver.expression=f'min(1,max(-1,gaze))*{factor:.17g}'
        iris.parent=rotation;iris.matrix_parent_inverse.identity()
        iris.location=(0,0,0);iris.rotation_euler=(0,0,0);iris.scale=(1,1,1)
        bpy.context.view_layer.update()
        roundtrip=np.array([iris.matrix_world@v.co for v in iris.data.vertices])
        error=float(np.max(np.linalg.norm(roundtrip-world,axis=1)))
        assert error<5e-8,error
        # Continuous-domain proof: R is orthogonal; every point of every iris
        # triangle stays outside the unit sphere under every rotation.
        # The normalized sclera is a convex inscribed sphere triangulation.
        sclera=bpy.data.objects[f'Ren_Eye_{side}_Sclera']
        sp=(np.array([sclera.matrix_world@v.co for v in sclera.data.vertices])-center)/RADII
        sclera_max=float(np.linalg.norm(sp,axis=1).max())
        assert sclera_max<1.00001
        eyes.append({'side':side,'center':center.tolist(),'radii':RADII.tolist(),'iris_object':iris.name,'frame_object':frame.name,'rotation_object':rotation.name,'minimum_iris_triangle_radius':minimum,'maximum_sclera_vertex_radius':sclera_max,'conservative_euclidean_clearance':float(RADII.min()*(minimum-sclera_max)),'neutral_world_max_error':error,'triangles':len(triangles)})
    changed={e['iris_object'] for e in eyes}
    preserved={o.name:signature(o)==all_before[o.name] for o in bpy.context.scene.objects if o.type=='MESH' and o.name not in changed}
    assert all(preserved.values())
    contract={'source_sha256':EXPECTED,'method':'Rigid rotation in ellipsoid-normalized coordinates; continuous-domain triangle clearance proof','gaze_domain':{'x':[-1,1],'y':[-1,1]},'radians':{'yaw_per_x':YAW,'negative_pitch_per_y':-PITCH},'local_rotation_order':'Rz(gazeX*yaw) @ Ry(-gazeY*pitch), equivalent Blender XYZ Euler(0,-pitch,yaw)','eyes':eyes,'preserved_noniris_mesh_uv_shape_signatures':preserved,'runtime':'Set localRotation of Ren_GazeRotate_L/R; do not apply removed four additive gaze shapes. Parent frame has center translation and radii scale. Coordinate-convert full basis if FBX import changes axis convention.'}
    pose(0,0,0)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_P2_GazeRotation.blend'))
    contract['blend_sha256']=hashlib.sha256((OUT/'Ren_P2_GazeRotation.blend').read_bytes()).hexdigest()
    write('gaze-controller-contract.json',contract)
    states=[('neutral',0,0,0),('partial-diagonal',-.5,.5,0),('extreme-left-up',-1,1,0),('extreme-right-down',1,-1,0),('combined-half',-.5,.5,.5),('extreme-half',1,1,.5)]
    for name,x,y,blink in states:
        pose(x,y,blink)
        render(name+'-front');render(name+'-quarter',True)
    pose(0,0,0);render('neutral-fullface',macro=False)
    # Optional isolated budget derivative: inscribed coarse sclera, iris unchanged.
    counts=[]
    for side in ('L','R'):
        old=bpy.data.objects[f'Ren_Eye_{side}_Sclera'];materials=list(old.data.materials)
        center=next(e['center'] for e in eyes if e['side']==side)
        bpy.data.objects.remove(old,do_unlink=True)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=10,location=center)
        ob=bpy.context.object;ob.name=f'Ren_Eye_{side}_Sclera';ob.scale=RADII
        bpy.ops.object.transform_apply(location=True,rotation=False,scale=True)
        for m in materials:ob.data.materials.append(m)
        for p in ob.data.polygons:p.use_smooth=True
        attr=ob.data.attributes.new('source_vertex_id','INT','POINT')
        for v in attr.data:v.value=-1
    for o in bpy.context.scene.objects:
        if o.type=='MESH' and o.name.startswith('Ren_Eye_'):
            o.data.calc_loop_triangles();counts.append({'object':o.name,'triangles':len(o.data.loop_triangles)})
    pose(0,0,0);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_P2_GazeRotation_LowSclera.blend'))
    write('low-sclera-budget.json',{'eye_triangles':sum(c['triangles'] for c in counts),'objects':counts,'sclera_segments':16,'sclera_ring_count':10,'proof':'Every new sclera vertex lies on the same normalized unit sphere; its triangles are inside by convexity. Same iris triangle clearance bound holds for all gaze rotations.','blend_sha256':hashlib.sha256((OUT/'Ren_P2_GazeRotation_LowSclera.blend').read_bytes()).hexdigest()})
    for name,x,y,blink in states:
        pose(x,y,blink);render('low-'+name+'-front');render('low-'+name+'-quarter',True)
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==EXPECTED
    print('REN_GAZE_REPAIRED',OUT,flush=True)


def verify():
    original_contract=json.loads((OUT/'gaze-controller-contract.json').read_text())
    results=[]
    for filename in ['Ren_P2_GazeRotation.blend','Ren_P2_GazeRotation_LowSclera.blend']:
        bpy.ops.wm.open_mainfile(filepath=str(OUT/filename))
        neutral={o.name:np.array([v.co[:] for v in o.data.vertices]) for o in bpy.context.scene.objects if o.type=='MESH' and o.name.endswith('Iris')}
        eyes=[]
        for item in original_contract['eyes']:
            iris=bpy.data.objects[item['iris_object']];sclera=bpy.data.objects[f'Ren_Eye_{item["side"]}_Sclera']
            center=np.array(item['center']);iris.data.calc_loop_triangles()
            minimum=min(triangle_origin_distance(*neutral[iris.name][list(t.vertices)]) for t in iris.data.loop_triangles)
            q=(np.array([sclera.matrix_world@v.co for v in sclera.data.vertices])-center)/RADII
            maximum=float(np.linalg.norm(q,axis=1).max())
            assert minimum>maximum+1e-4
            eyes.append({'side':item['side'],'minimum_iris_triangle_radius':minimum,'maximum_sclera_vertex_radius':maximum,'guaranteed_gap_source_units':float(RADII.min()*(minimum-maximum))})
        max_error=0;max_pose_error=0;max_travel=0
        for x in np.linspace(-1,1,21):
            for y in np.linspace(-1,1,21):
                pose(float(x),float(y),0)
                for item in original_contract['eyes']:
                    iris=bpy.data.objects[item['iris_object']];center=np.array(item['center'])
                    q=(np.array([iris.matrix_world@v.co for v in iris.data.vertices])-center)/RADII
                    yaw=float(x)*YAW;pitch=-float(y)*PITCH
                    rz=np.array([[math.cos(yaw),-math.sin(yaw),0],[math.sin(yaw),math.cos(yaw),0],[0,0,1]])
                    ry=np.array([[math.cos(pitch),0,math.sin(pitch)],[0,1,0],[-math.sin(pitch),0,math.cos(pitch)]])
                    expected=neutral[iris.name]@(rz@ry).T
                    max_pose_error=max(max_pose_error,float(np.linalg.norm(q-expected,axis=1).max()))
                    max_travel=max(max_travel,float(np.linalg.norm((q-neutral[iris.name])*RADII,axis=1).max()))
                    error=float(np.max(np.abs(np.linalg.norm(q,axis=1)-np.linalg.norm(neutral[iris.name],axis=1))))
                    max_error=max(max_error,error)
        assert max_error<2e-6,max_error
        assert max_pose_error<2e-6,max_pose_error
        assert max_travel>.009,max_travel
        occlusion=[]
        # Capture-driven local samples check moving irises remain behind full lids.
        for x,y in [(0,0),(-1,-1),(-1,1),(1,-1),(1,1),(-.5,.5)]:
            pose(x,y,1);deps=bpy.context.evaluated_depsgraph_get()
            for view,d in [('front',Vector((1,0,0))),('quarter',Vector((.848,-.530,0)))]:
                hits=0
                for sign in [-1,1]:
                    for yy in np.linspace(.06,.18,40):
                        for zz in np.linspace(.045,.12,26):
                            target=Vector((.24,sign*float(yy),float(zz)))
                            hit,loc,n,face,o,m=bpy.context.scene.ray_cast(deps,target+d*.6,-d,distance=1)
                            if hit and o.name.endswith(('Iris','Sclera')):hits+=1
                occlusion.append({'gaze':[x,y],'view':view,'visible_eye_samples_at_full_blink':hits})
        results.append({'file':filename,'sha256':hashlib.sha256((OUT/filename).read_bytes()).hexdigest(),'eyes':eyes,'gaze_grid_points':441,'maximum_normalized_radius_error_after_transform':max_error,'maximum_normalized_pose_error_against_analytic_rotation':max_pose_error,'maximum_actual_iris_travel_source_units':max_travel,'full_blink_samples':occlusion})
    write('reopen-verification.json',results)
    print('REN_GAZE_REOPEN_VERIFIED',json.dumps([{'file':r['file'],'radius_error':r['maximum_normalized_radius_error_after_transform'],'max_closed_hits':max(v['visible_eye_samples_at_full_blink'] for v in r['full_blink_samples'])} for r in results]),flush=True)


if __name__=='__main__':
    if '--verify' in sys.argv:verify()
    else:main()
