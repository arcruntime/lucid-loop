"""Ren designer-contour eye prototype. Accepted context is strictly read-only.

This package replaces the rejected ellipsoid design; it does not edit that
history, the accepted H face, or Unity. Camera calibration precedes modeling.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import numpy as np

ROOT=Path(__file__).resolve().parents[2]
PARTS=ROOT/'art/generated/characters/ren/parts-workflow-v1'
OUT=PARTS/'h-designer-eyes-v1'
SOURCE=PARTS/'h-complete-head-v1/Ren_H_CompleteHead_Portable.blend'
SOURCE_SHA='317b8f7379dcf8d2fe34501385e1da7cd91b3d057008d488db0c65ed3727b8f5'
ART=ROOT/'art/characters/ren-model-sheet.png'
TRACE=PARTS/'h-eye-design-audit-v1/artist-layered-contours-refined-v1.json'


def sha(path):
    with Path(path).open('rb') as h:return hashlib.file_digest(h,'sha256').hexdigest()


def dump(name,data):
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/name).write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')


def coords(data):
    a=np.empty((len(data),3),np.float32);data.foreach_get('co',a.ravel());return a


def fingerprint(obj):
    mesh=obj.data;mesh.calc_loop_triangles()
    uv_hashes={}
    for uv in mesh.uv_layers:
        values=np.empty((len(uv.data),2),np.float32);uv.data.foreach_get('uv',values.ravel());uv_hashes[uv.name]=hashlib.sha256(values.tobytes()).hexdigest()
    return {'name':obj.name,'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),
        'positions_sha256':hashlib.sha256(coords(mesh.vertices).tobytes()).hexdigest(),
        'triangle_indices_sha256':hashlib.sha256(np.asarray([t.vertices[:] for t in mesh.loop_triangles],np.int32).tobytes()).hexdigest(),
        'uv_sha256':uv_hashes,'parent':obj.parent.name if obj.parent else None,'matrix_parent_inverse':np.asarray(obj.matrix_parent_inverse).tolist(),
        'shape_keys':{k.name:{'sha256':hashlib.sha256(coords(k.data).tobytes()).hexdigest(),'value':k.value} for k in mesh.shape_keys.key_blocks} if mesh.shape_keys else {},
        'matrix_world':np.asarray(obj.matrix_world).tolist(),
        'materials':[m.name if m else None for m in mesh.materials]}


def inspect_source():
    import bpy
    assert sha(SOURCE)==SOURCE_SHA
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));scene=bpy.context.scene
    meshes=[fingerprint(o) for o in scene.objects if o.type=='MESH']
    controls=[{'name':o.name,'properties':{k:o[k] for k in o.keys() if isinstance(o[k],(int,float,str))}} for o in scene.objects if o.type=='EMPTY']
    face=bpy.data.objects['Ren_H_Head'];mesh=face.data;mesh.calc_loop_triangles()
    p=coords(mesh.vertices).astype(np.float64);f=np.asarray([t.vertices[:] for t in mesh.loop_triangles]);matrix=np.asarray(face.matrix_world)
    evaluated=face.evaluated_get(bpy.context.evaluated_depsgraph_get());eval_mesh=evaluated.to_mesh();ep=coords(eval_mesh.vertices).astype(np.float64);evaluated.to_mesh_clear()
    arrays={'face_basis_native':p,'face_evaluated_native':ep,'face_triangles':f,'face_matrix':matrix}
    if 'source_h_vertex_id' in mesh.attributes:
        a=mesh.attributes['source_h_vertex_id'];v=np.empty(len(a.data),np.int32);a.data.foreach_get('value',v);arrays['source_h_vertex_id']=v
    if 'source_h_face_id' in mesh.attributes:
        a=mesh.attributes['source_h_face_id'];v=np.empty(len(a.data),np.int32);a.data.foreach_get('value',v);arrays['source_h_face_id']=v
    OUT.mkdir(parents=True,exist_ok=True);np.savez_compressed(OUT/'accepted-context-snapshot.npz',**arrays)
    dump('accepted-context-inspection.json',{'source':str(SOURCE.relative_to(ROOT)),'source_sha256':SOURCE_SHA,'source_unchanged':sha(SOURCE)==SOURCE_SHA,'meshes':meshes,'controls':controls,'face_native_bounds':[ep.min(0).tolist(),ep.max(0).tolist()],'camera':{'name':scene.camera.name,'matrix_world':np.asarray(scene.camera.matrix_world).tolist(),'type':scene.camera.data.type,'ortho_scale':scene.camera.data.ortho_scale}})
    print('ACCEPTED_H_INSPECTED',len(meshes),len(p),len(f),'NATIVE_BOUNDS',ep.min(0),ep.max(0),flush=True)


def camera_axes(yaw,elevation,roll):
    a,e,r=np.radians([yaw,elevation,roll])
    view=np.array([math.sin(a)*math.cos(e),-math.cos(a)*math.cos(e),math.sin(e)])
    right=np.array([math.cos(a),math.sin(a),0.]);up=np.cross(view,right)
    return math.cos(r)*right+math.sin(r)*up,-math.sin(r)*right+math.cos(r)*up,view


def project_native(points,parameters):
    right,up,_=camera_axes(*parameters[:3]);scale,tx,ty=parameters[3:]
    return np.column_stack([np.asarray(points)@right*scale+tx,-np.asarray(points)@up*scale+ty])


def trace_opening(record):
    upper=np.vstack([x['sampled_source_pixel_curve'] for x in record['curves'] if x['layer']=='upper aperture'])
    lower=np.vstack([x['sampled_source_pixel_curve'] for x in record['curves'] if x['layer']=='lower aperture'])
    return np.vstack([upper,lower[::-1]])


def polygon_centroid(p):
    a=np.asarray(p);b=np.roll(a,-1,axis=0);cross=a[:,0]*b[:,1]-b[:,0]*a[:,1]
    return ((a+b)*cross[:,None]).sum(0)/(3*cross.sum())


def solve_camera():
    from scipy.optimize import least_squares
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    snapshot=np.load(OUT/'accepted-context-snapshot.npz');points=snapshot['face_evaluated_native'];traces=json.loads(TRACE.read_text())['traces']
    def nearest(target):
        idx=int(np.argmin(np.linalg.norm(points-np.asarray(target),axis=1)))
        return idx,points[idx]
    candidates=[
        ('H right orbital location / main portrait image-left',[-.112,-.255,.552],polygon_centroid(trace_opening(traces[0])),3.,'Position anchor in the native H orbital surface; the rejected eyelid geometry is not used.'),
        ('H left orbital location / main portrait image-right',[.107,-.276,.552],polygon_centroid(trace_opening(traces[1])),3.,'Position anchor; source-colored-opening centroid does not assert a full hidden eyeball center.'),
        ('nose tip',[-.02232,-.37981,.48179],[524,228],1.,'Manually located drawn tip; painterly nose shadow makes this approximate.'),
        ('mouth center',[-.003,-.334,.387],[480,269],.6,'Low weight: source lips are parted while accepted neutral is sealed.'),
        ('chin tip',[-.007,-.263,.213],[458,334],.8,'Source silhouette tip; no jaw proportion change is permitted.'),
    ]
    rows=[]
    for label,target,pixel,weight,note in candidates:
        index,p=nearest(target);rows.append({'label':label,'accepted_head_vertex_index':index,'native_position':p.tolist(),'target_source_pixel':np.asarray(pixel).tolist(),'weight':weight,'note':note})
    p=np.array([r['native_position'] for r in rows]);q=np.array([r['target_source_pixel'] for r in rows]);w=np.sqrt([r['weight'] for r in rows])
    def residual(x):return ((project_native(p,x)-q)*w[:,None]).ravel()
    solutions=[]
    for yaw in [-55,-35,-15]:
        for elevation in [-15,5,25]:
            seed=[yaw,elevation,30,480,450,470]
            fit=least_squares(residual,seed,bounds=([-75,-40,-5,250,-500,-500],[20,40,55,850,1400,1400]),loss='soft_l1',f_scale=4,max_nfev=1200)
            solutions.append(fit)
    fit=min(solutions,key=lambda r:np.linalg.norm(residual(r.x)));projected=project_native(p,fit.x)
    for row,actual in zip(rows,projected):row['projected_source_pixel']=actual.tolist();row['residual_pixels']=float(np.linalg.norm(actual-row['target_source_pixel']))
    right,up,view=camera_axes(*fit.x[:3]);scale,tx,ty=fit.x[3:]
    center=right*((1536/2-tx)/scale)+up*((ty-1024/2)/scale)
    report={'status':'FIRST_ORTHOGRAPHIC_PORTRAIT_CAMERA_FIT_REQUIRES_ACTUAL_RENDER_REVIEW','source_context_sha256':SOURCE_SHA,'source_art_sha256':sha(ART),'trace_sha256':sha(TRACE),'parameters':{'yaw_degrees':fit.x[0],'elevation_degrees':fit.x[1],'image_roll_degrees':fit.x[2],'pixels_per_native_unit':scale,'translation_x_pixels':tx,'translation_y_pixels':ty},'parameter_vector':fit.x.tolist(),'native_right':right.tolist(),'native_up':up.tolist(),'native_toward_camera':view.tolist(),'native_camera_target':center.tolist(),'native_camera_location':(center+view*3).tolist(),'source_canvas_pixels':[1536,1024],'native_ortho_width':1536/scale,'landmarks':rows,'maximum_landmark_residual_pixels':max(r['residual_pixels'] for r in rows),'rms_landmark_residual_pixels':float(np.sqrt(np.mean((projected-q)**2))),'constraints':['Camera changes only. Accepted H mesh coordinates, proportions, materials and morphs are not fitted to the drawing.','A single orthographic camera/similarity is used; no independent horizontal/vertical scaling.','Pose calibration is approximate because the illustrated face is not a calibrated photograph and source mouth pose differs.','Portrait roll exists only in camera axes; it is not added to neutral canthus geometry.','Eye depth and later contour construction require a separate actual view review.']}
    dump('portrait-camera-fit-v1.json',report)
    fig,ax=plt.subplots(figsize=(9,10));ax.imshow(plt.imread(ART),interpolation='nearest');ax.set_xlim(340,605);ax.set_ylim(352,110)
    for i,row in enumerate(rows):
        a=q[i];b=projected[i];ax.plot([a[0],b[0]],[a[1],b[1]],color='#00d5ff',lw=1);ax.scatter(*a,color='#ffd43b',s=24,marker='x');ax.scatter(*b,color='#00d5ff',s=18,marker='+');ax.annotate(str(i+1),a+np.array([3,-4]),color='white',fontsize=10)
    ax.set_title('First camera fit on fixed H landmarks\nYellow source target / cyan projected H — camera only, no mesh fitting');ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y');fig.tight_layout();fig.savefig(OUT/'portrait-camera-landmarks-v1.png',dpi=150)
    print('PORTRAIT_CAMERA',report['parameters'],'RESIDUALS',[round(r['residual_pixels'],3) for r in rows],flush=True)


def install_portrait_camera(scene):
    import bpy
    from mathutils import Matrix,Vector
    from bpy_extras.object_utils import world_to_camera_view
    fit=json.loads((OUT/'portrait-camera-fit-v1.json').read_text());face=bpy.data.objects['Ren_H_Head'];registration=face.matrix_world.copy()
    native_basis=Matrix(np.column_stack([fit['native_right'],fit['native_up'],fit['native_toward_camera']]).tolist())
    rotation=registration.to_3x3().normalized()@native_basis
    camera=scene.camera;camera.parent=None;camera.matrix_world=rotation.to_4x4();camera.location=registration@Vector(fit['native_camera_location'])
    camera.data.type='ORTHO';camera.data.sensor_fit='HORIZONTAL';camera.data.ortho_scale=fit['native_ortho_width']*registration.to_scale()[0];camera.data.shift_x=camera.data.shift_y=0
    scene.render.resolution_x=1536;scene.render.resolution_y=1024;scene.render.resolution_percentage=100
    scene.render.pixel_aspect_x=scene.render.pixel_aspect_y=1
    bpy.context.view_layer.update();errors=[]
    for row in fit['landmarks']:
        uv=world_to_camera_view(scene,camera,registration@Vector(row['native_position']));actual=np.array([uv.x*1536,(1-uv.y)*1024]);errors.append(float(np.linalg.norm(actual-row['projected_source_pixel'])))
    assert max(errors)<.002,errors
    return {'matrix_world':np.asarray(camera.matrix_world).tolist(),'ortho_scale':camera.data.ortho_scale,'actual_blender_projection_max_error_pixels':max(errors)}


def camera_render():
    import bpy
    assert sha(SOURCE)==SOURCE_SHA;bpy.ops.wm.open_mainfile(filepath=str(SOURCE));scene=bpy.context.scene
    for obj in scene.objects:
        if obj.name.startswith('Ren_H_Eye_') and obj.type=='MESH':obj.hide_render=True
    contract=install_portrait_camera(scene)
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.film_transparent=True
    scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    scene.render.filepath=str(OUT/'portrait-camera-context-v1.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_DesignerEye_CameraStudy.blend'))
    bpy.ops.render.render(write_still=True)
    assert sha(SOURCE)==SOURCE_SHA
    contract.update({'source_sha256':SOURCE_SHA,'source_unchanged':True,'old_eye_meshes_hidden_for_camera_diagnostic':True,'new_eye_geometry_created':False,'render':scene.render.filepath,'status':'CAMERA_REVIEW_ONLY_ORBITAL_HOLES_EXPECTED'})
    dump('portrait-camera-render-v1.json',contract);print('PORTRAIT_CAMERA_RENDERED',flush=True)


def camera_plate():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    art=plt.imread(ART);render=plt.imread(OUT/'portrait-camera-context-v1.png');fig,axes=plt.subplots(1,3,figsize=(16,7))
    for i,ax in enumerate(axes):
        if i in (0,2):ax.imshow(art,interpolation='nearest')
        if i in (1,2):ax.imshow(render,interpolation='nearest',alpha=1 if i==1 else .50)
        ax.set_xlim(290,680);ax.set_ylim(370,0);ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y')
        ax.set_title(['Actual designer main portrait','Fixed accepted H / camera only','Source + fixed H at 50% alpha'][i])
        ax.set_facecolor('#ced7df')
    fig.suptitle('First source-camera review: old eyes hidden, no replacement geometry yet\nEye-region pose anchor; remaining mouth/chin projection differences are reported, not fitted away',fontsize=13);fig.tight_layout();fig.savefig(OUT/'portrait-camera-comparison-v1.png',dpi=150)


def linear(rgb):
    a=np.asarray(rgb,dtype=float);return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)


def selected_trial():
    return 'trial-v6' if '--fan-v6' in sys.argv else ('trial-v5' if '--layered-v5' in sys.argv else ('trial-v4' if '--boundary-v4' in sys.argv else ('trial-v3' if '--frontal-v3' in sys.argv else ('trial-v2' if '--shallow-v2' in sys.argv else 'trial-v1'))))


def resample_closed(points,count):
    p=np.asarray(points);p=p[np.r_[True,np.linalg.norm(np.diff(p,axis=0),axis=1)>1e-8]]
    if np.linalg.norm(p[-1]-p[0])<1e-8:p=p[:-1]
    p=np.vstack([p,p[0]]);t=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))];q=np.arange(count)/count*t[-1]
    return np.column_stack([np.interp(q,t,p[:,j]) for j in range(p.shape[1])])


def sample_curve(points,count=90):
    p=np.asarray(points);t=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))];q=np.linspace(0,t[-1],count)
    try:
        from scipy.interpolate import PchipInterpolator
        return np.column_stack([PchipInterpolator(t,p[:,j])(q) for j in range(2)])
    except ImportError:
        # Blender has no SciPy: chord-length cubic Hermite for the authored,
        # hidden iris continuation only. Reviewed visible source curves are loaded.
        slope=np.diff(p,axis=0)/np.diff(t)[:,None];tangents=np.vstack([slope[0],(slope[:-1]+slope[1:])/2,slope[-1]])
        i=np.minimum(np.searchsorted(t,q,side='right')-1,len(p)-2);dt=t[i+1]-t[i];u=(q-t[i])/dt
        return (2*u**3-3*u**2+1)[:,None]*p[i]+(u**3-2*u**2+u)[:,None]*dt[:,None]*tangents[i]+(-2*u**3+3*u**2)[:,None]*p[i+1]+(u**3-u**2)[:,None]*dt[:,None]*tangents[i+1]


class PolygonRegion:
    def __init__(self,points):self.points=np.asarray(points)
    def contains_points(self,points,radius=0):
        q=np.atleast_2d(points);p=self.points;a=p[:,0];b=p[:,1];c=np.roll(a,-1);d=np.roll(b,-1)
        y=q[:,1,None];x=q[:,0,None];dy=d-b;safe=np.where(abs(dy)<1e-12,1e-12,dy)
        crossing=((b>y)!=(d>y))&(x<(c-a)*(y-b)/safe+a)
        return crossing.sum(1)%2==1
    def contains_point(self,point):return bool(self.contains_points([point])[0])


def build_prototype():
    import bpy
    from mathutils import Matrix,Vector
    from mathutils.geometry import delaunay_2d_cdt,tessellate_polygon
    assert sha(SOURCE)==SOURCE_SHA;bpy.ops.wm.open_mainfile(filepath=str(SOURCE));scene=bpy.context.scene
    before={o.name:fingerprint(o) for o in scene.objects if o.type=='MESH' and not o.name.startswith('Ren_H_Eye_')}
    for obj in scene.objects:
        if obj.name.startswith('Ren_H_Eye_') and obj.type=='MESH':obj.hide_render=True
    context=bpy.data.objects['Ren_H_Head'];registration=context.matrix_world.copy();parent=bpy.data.objects['Head']
    camera=install_portrait_camera(scene);fit=json.loads((OUT/'portrait-camera-fit-v1.json').read_text());parameters=fit['parameter_vector'];right=np.array(fit['native_right']);up=np.array(fit['native_up']);toward=np.array(fit['native_toward_camera']);scale,tx,ty=parameters[3:]
    trace=json.loads(TRACE.read_text())['traces'];boundary_file=PARTS/'h-eye-controls-v1/seam-contract-v1/native-study-cut-boundaries.json'
    # This is original-H cut provenance only. No rejected eye mesh coordinates are loaded.
    boundaries={e['side']:e['geometric_paths'][0] for e in json.loads(boundary_file.read_text())['eyes']}
    snapshot=np.load(OUT/'accepted-context-snapshot.npz');accepted_positions={tuple(p) for p in snapshot['face_evaluated_native']}
    boundary_fix='--boundary-v4' in sys.argv;frontal='--frontal-v3' in sys.argv or boundary_fix;shallow='--shallow-v2' in sys.argv or frontal;trial_name=selected_trial()
    trial=OUT/trial_name;trial.mkdir(parents=True,exist_ok=True);report=[]
    collection=bpy.data.collections.new('Ren_DesignerEyes_Prototype');scene.collection.children.link(collection)
    def material(name,color):
        mat=bpy.data.materials.new(name);mat.use_nodes=True;nodes=mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission');emit.inputs[0].default_value=(*linear(color),1);mat.node_tree.links.new(emit.outputs[0],out.inputs['Surface']);mat.diffuse_color=(*linear(color),1);return mat
    skin=material('Ren_DesignerEyes_SkinStudy',(0.89,0.755,0.668));sclera_mat=material('Ren_DesignerEyes_Sclera',(0.765,0.715,0.745));ink=material('Ren_DesignerEyes_UpperInk',(.075,.06,.09));lower_ink=material('Ren_DesignerEyes_LowerInk',(.215,.17,.21));crease=material('Ren_DesignerEyes_Crease',(.49,.35,.37));pupil_mat=material('Ren_DesignerEyes_Pupil',(.073,.065,.115));highlight_mat=material('Ren_DesignerEyes_Highlight',(.79,.80,.85));smoke=material('Ren_DesignerEyes_SmokyLid',(.64,.46,.46))
    iris_mat=bpy.data.materials.new('Ren_DesignerEyes_IrisGraphic');iris_mat.use_nodes=True;nodes=iris_mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission');vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='DesignerIrisColor';iris_mat.node_tree.links.new(vc.outputs['Color'],emit.inputs[0]);iris_mat.node_tree.links.new(emit.outputs[0],out.inputs[0])
    for side,record in [('R',trace[0]),('L',trace[1])]:
        outer=np.array(boundaries[side]['native_points'],dtype=float);assert all(tuple(p) in accepted_positions for p in outer);distance=np.zeros(len(outer))
        center=outer.mean(0);cx,cz=center[[0,2]]
        def features(x,z):
            u=(np.asarray(x)-cx)/.09;v=(np.asarray(z)-cz)/.06
            return np.stack([np.ones_like(u),u,v,u*u,u*v,v*v],axis=-1)
        matrix=features(outer[:,0],outer[:,2]);matrix=matrix[:,:3] if shallow else matrix;coefficients=np.linalg.lstsq(matrix,outer[:,1],rcond=None)[0]
        for _ in range(12):
            error=matrix@coefficients-outer[:,1];weight=(1+(error/.005)**2)**-.25
            coefficients=np.linalg.lstsq(matrix*weight[:,None],outer[:,1]*weight,rcond=None)[0]
        surface_error=matrix@coefficients-outer[:,1]
        if shallow:coefficients=np.r_[coefficients,[0.,0.,0.]]
        if frontal:
            # Front/quarter review rejected the steep fitted orbital-plane angles.
            # Keep separate source contours, but choose shallow front-oriented
            # interior depth; the source camera projection remains unchanged.
            anchor=np.asarray(fit['landmarks'][0 if side=='R' else 1]['native_position'])
            coefficients[1]=np.clip(coefficients[1]/.09,-.20,.20)*.09
            coefficients[2]=np.clip(coefficients[2]/.06,-.12,.12)*.06
            coefficients[0]=anchor[1]+.003-coefficients[1]*(anchor[0]-cx)/.09-coefficients[2]*(anchor[2]-cz)/.06
        def depth(x,z):return features(x,z)@coefficients
        failures=[]
        def to_native(pixels,offset=0.):
            pixels=np.atleast_2d(pixels);origin=(pixels[:,0,None]-tx)/scale*right+(ty-pixels[:,1,None])/scale*up;result=[]
            for pixel,o in zip(pixels,origin):
                def value(t):
                    p=o+t*toward;return p[1]-depth(p[0],p[2])-offset
                fm,f0,fp=value(-1),value(0),value(1);a=(fp+fm)/2-f0;b=(fp-fm)/2
                roots=np.roots([a,b,f0]) if abs(a)>1e-12 else np.array([-f0/b]);real=[float(r.real) for r in roots if abs(r.imag)<1e-7]
                if not real:failures.append(pixel.tolist());real=[-f0/b]
                candidates=[o+t*toward for t in real];p=min(candidates,key=lambda p:np.linalg.norm((p-center)*np.array([1,1,1.2])))
                result.append(p)
            return np.array(result)
        outer2=project_native(outer,parameters);opening=resample_closed(trace_opening(record),144);opening_path=PolygonRegion(opening)
        outer_path=PolygonRegion(outer2);inside_fraction=float(outer_path.contains_points(opening,radius=.05).mean())
        named=[]
        def newmesh(name,native,faces,mat,uv_pixels=None,colors=None):
            native=np.asarray(native);clean=[]
            for f in faces:
                f=list(map(int,f));normal=np.cross(native[f[1]]-native[f[0]],native[f[2]]-native[f[0]])
                if np.linalg.norm(normal)<1e-12:continue
                if normal@toward<0:f.reverse()
                clean.append(f)
            mesh=bpy.data.meshes.new(name);mesh.from_pydata(native.tolist(),[],clean);mesh.materials.append(mat);mesh.update();obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj.parent=parent;obj.matrix_world=registration
            for p in mesh.polygons:p.use_smooth=True
            pixel_coords=project_native(native,parameters) if uv_pixels is None else np.asarray(uv_pixels)
            uv=mesh.uv_layers.new(name='DesignerEyeUV');lo=pixel_coords.min(0);span=np.maximum(np.ptp(pixel_coords,axis=0),1)
            for loop,value in zip(mesh.loops,uv.data):value.uv=(pixel_coords[loop.vertex_index]-lo)/span
            if colors is not None:
                attr=mesh.color_attributes.new(name='DesignerIrisColor',type='FLOAT_COLOR',domain='POINT')
                for dst,color in zip(attr.data,colors):dst.color=(*linear(color),1)
            obj['prototype_status']='FIRST_DESIGNER_CONTOUR_CONSTRUCTION_NOT_FINAL_LIKENESS';obj['source_side']=side;named.append(obj)
            return obj
        def polygon_mesh(label,pixels,mat,offset=-.001):
            pixels=resample_closed(pixels,min(160,max(12,len(pixels))));poly=[Vector((p[0],p[1],0)) for p in pixels];tris=tessellate_polygon([poly]);lookup={id(p):i for i,p in enumerate(poly)}
            # tessellate_polygon preserves input Vector instances in this Blender build.
            faces=[]
            for tri in tris:
                faces.append([int(v) if isinstance(v,int) else next(i for i,p in enumerate(poly) if (p-v).length<1e-7) for v in tri])
            return newmesh(f'Ren_DesignerEye_{side}_{label}',to_native(pixels,offset),faces,mat,pixels)
        # Fresh skin shutter: original H outer cut plus designer-derived inner opening.
        # CDT in source projection retains both boundary cycles and local sharp notches.
        interior_native=to_native(opening,-.001)
        domain_outer=outer[:,[0,2]] if boundary_fix else outer2
        domain_inner=interior_native[:,[0,2]] if boundary_fix else opening
        domain_outer_path=PolygonRegion(domain_outer);domain_inner_path=PolygonRegion(domain_inner)
        cloud=np.vstack([domain_outer,domain_inner]);edges=[]
        for start,n in [(0,len(outer2)),(len(outer2),len(opening))]:edges.extend([(start+i,start+(i+1)%n) for i in range(n)])
        center2=polygon_centroid(domain_inner)
        mid=[]
        for p in resample_closed(domain_outer,100):
            for factor in [.40,.70]:
                q=p*(1-factor)+center2*factor
                if domain_outer_path.contains_point(q) and not domain_inner_path.contains_point(q):mid.append(q)
        cloud=np.vstack([cloud,mid]);cdt=delaunay_2d_cdt([Vector(p) for p in cloud],edges,[],0,1e-9 if boundary_fix else 1e-5,True)
        generated=np.array([p[:] for p in cdt[0]]);faces=[f for f in cdt[2] if domain_outer_path.contains_point(generated[f].mean(0)) and not domain_inner_path.contains_point(generated[f].mean(0))]
        if boundary_fix:
            native=np.column_stack([generated[:,0],depth(generated[:,0],generated[:,1])-.001,generated[:,1]])
            original_map={}
            for vi,original_ids in enumerate(cdt[3]):
                for oi in original_ids:
                    if oi<len(outer):
                        if vi in original_map.values():assert np.array_equal(native[vi],outer[oi]),'Distinct 3D H boundaries merged in parameter plane'
                        native[vi]=outer[oi];original_map[int(oi)]=vi
            assert len(original_map)==len(outer),(side,'missing CDT boundary input identity',len(original_map),len(outer))
            shutter=newmesh(f'Ren_DesignerEye_{side}_SkinShutter',native,faces,skin,project_native(native,parameters))
            generated_positions=coords(shutter.data.vertices).astype(np.float64);actual_edges={tuple(sorted(e.vertices)) for e in shutter.data.edges}
            boundary_errors=[float(np.linalg.norm(generated_positions[original_map[i]]-outer[i])) for i in range(len(outer))]
            missing_edges=[i for i in range(len(outer)) if tuple(sorted([original_map[i],original_map[(i+1)%len(outer)]])) not in actual_edges]
            assert max(boundary_errors)==0.,(side,'generated boundary position error',max(boundary_errors))
            assert not missing_edges,(side,'actual generated boundary edges missing',missing_edges)
            boundary_attr=shutter.data.attributes.new(name='source_h_boundary_vertex_id',type='INT',domain='POINT');values=np.full(len(native),-1,np.int32)
            for i,vi in original_map.items():values[vi]=boundaries[side]['source_h_vertex_ids'][i]
            boundary_attr.data.foreach_set('value',values)
            attachment={'parameter_domain':'native H XZ, not portrait projection','source_boundary_vertices':len(outer),'actual_generated_boundary_vertices':len(original_map),'maximum_actual_position_error':max(boundary_errors),'actual_retained_boundary_edges':len(outer)-len(missing_edges),'missing_boundary_edges':missing_edges,'mapping':'CDT original input vertex identities, with original source H ID attribute distinct from geometric vertex index'}
        else:
            native=to_native(generated,-.001);all_distance=np.linalg.norm(generated[:,None,:]-outer2[None,:,:],axis=2);indices=all_distance.argmin(1);dist=all_distance[np.arange(len(generated)),indices]
            exact=dist<1e-5;native[exact]=outer[indices[exact]]
            newmesh(f'Ren_DesignerEye_{side}_SkinShutter',native,faces,skin,generated)
            attachment={'status':'LEGACY_NEAREST_PROJECTED_POINT_ATTACHMENT_REQUIRES_VERIFICATION'}
        # Opaque shallow backing extends beneath the shutters; no closed eyeball.
        backing_poly=resample_closed(opening if frontal else outer2,144 if frontal else 90);backing=np.vstack([polygon_centroid(backing_poly),backing_poly]);backing_faces=[(0,i+1,(i+1)%len(backing_poly)+1) for i in range(len(backing_poly))]
        newmesh(f'Ren_DesignerEye_{side}_Backing',to_native(backing,.012),backing_faces,sclera_mat,backing)
        lower=np.vstack([c['sampled_source_pixel_curve'] for c in record['curves'] if c['layer']=='lower aperture'])
        upper=np.vstack([c['sampled_source_pixel_curve'] for c in record['curves'] if c['layer']=='upper aperture'])
        exterior=np.vstack([c['sampled_source_pixel_curve'] for c in record['curves'] if c['layer']=='upper ink exterior' and c['visibility']=='visible'])
        if side=='L':
            # Explicit construction-only connections between the approved visible fragments.
            # Hair-adjacent uncertain fan tips are omitted, not declared traced.
            exterior=np.vstack([exterior,[[557.0,207.2],[561.3,208.9],[565.4,208.7],[566.7,212.5],[565.3,218.9],[563.7,224.3],[560.5,224.5]]])
        ink_pixels=np.vstack([exterior,upper[::-1]])
        polygon_mesh('UpperInk',ink_pixels,ink,-.0018)
        for i,c in enumerate([c for c in record['curves'] if c['layer']=='lash fan exterior' and c['visibility']=='visible']):
            polygon_mesh(f'VisibleFan_{i}',np.array(c['source_pixel_landmarks']),ink,-.002)
        def ribbon(label,path,width,mat,offset=-.002):
            p=np.asarray(path)[::max(1,len(path)//100)];tangent=np.gradient(p,axis=0);normal=np.column_stack([-tangent[:,1],tangent[:,0]]);normal/=np.linalg.norm(normal,axis=1)[:,None]
            taper=np.sin(np.linspace(0,math.pi,len(p)))**.65;half=width*taper/2
            if frontal and label=='LowerInk':
                outsign=np.where(np.sum(normal*(p-polygon_centroid(opening)),axis=1)>=0,1.,-1.)
                xy=np.stack([p,p+normal*outsign[:,None]*(2*half[:,None])],axis=1).reshape(-1,2)
            else:xy=np.stack([p+normal*half[:,None],p-normal*half[:,None]],axis=1).reshape(-1,2)
            faces=[]
            for k in range(len(p)-1):faces.extend([(2*k,2*k+1,2*k+2),(2*k+1,2*k+3,2*k+2)])
            return newmesh(f'Ren_DesignerEye_{side}_{label}',to_native(xy,offset),faces,mat,xy)
        ribbon('LowerInk',lower,1.25,lower_ink)
        for c in record['curves']:
            if c['layer']=='crease' and c['visibility']=='visible':ribbon('Crease',np.array(c['sampled_source_pixel_curve']),.60,crease)
        # Iris lower arc is the actual traced visible arc. Hidden continuation is
        # authored explicitly beneath the skin and is not treated as source evidence.
        arc=np.array(next(c['source_pixel_landmarks'] for c in record['curves'] if c['layer']=='visible iris arc'))
        if side=='R':
            continuation=[[473.4,158.0],[470.4,150.3],[462.0,147.9],[454.0,149.0],arc[0].tolist()];pupil_center=np.array([461.8,160.2]);pupil_radius=np.array([3.0,3.5]);highlight_centers=[([464.0,158.5],[1.6,2.0]),([459.9,155.7],[1.1,.65])]
        else:
            continuation=[[550.4,207.4],[546.4,200.8],[539.2,198.7],[531.5,201.7],arc[0].tolist()];pupil_center=np.array([537.9,208.5]);pupil_radius=np.array([2.4,2.55]);highlight_centers=[([540.1,206.6],[1.25,1.35]),([534.8,206.7],[.7,.7])]
        perimeter=resample_closed(np.vstack([sample_curve(arc,100),sample_curve(np.vstack([arc[-1],continuation]),100)]),64);iris_center=polygon_centroid(perimeter);iris_vertices=[iris_center];colors=[(.39,.42,.54)];rings=[.34,.70,.93,1.]
        for radius in rings:
            for p in perimeter:
                q=iris_center+(p-iris_center)*radius;iris_vertices.append(q);v=np.clip((q[1]-perimeter[:,1].min())/np.ptp(perimeter[:,1]),0,1)
                color=np.array([.28,.31,.42])*(1-v)+np.array([.54,.56,.66])*v
                if radius>=.93:color=color*(.45 if radius==1 else .86)
                colors.append(color)
        iris_faces=[(0,i+1,(i+1)%64+1) for i in range(64)]
        for r in range(len(rings)-1):
            a=1+r*64;b=1+(r+1)*64
            for k in range(64):j=(k+1)%64;iris_faces.extend([(a+k,b+k,a+j),(a+j,b+k,b+j)])
        newmesh(f'Ren_DesignerEye_{side}_Iris',to_native(iris_vertices,.007),iris_faces,iris_mat,iris_vertices,colors)
        angle=math.atan2(upper[-1,1]-upper[0,1],upper[-1,0]-upper[0,0]);rot=np.array([[math.cos(angle),-math.sin(angle)],[math.sin(angle),math.cos(angle)]])
        def ellipse(center,radii,n=32):
            theta=np.linspace(0,2*math.pi,n,endpoint=False);return np.asarray(center)+np.column_stack([np.cos(theta),np.sin(theta)])*np.asarray(radii)@rot.T
        polygon_mesh('Pupil',ellipse(pupil_center,pupil_radius),pupil_mat,.0065)
        for i,(c,r) in enumerate(highlight_centers):polygon_mesh(f'Highlight_{i}',ellipse(c,r,20),highlight_mat,.006)
        rows=[fingerprint(o) for o in named]
        report.append({'side':side,'source_trace_name':record['name'],'original_H_boundary_points':len(outer),'maximum_boundary_coordinate_error':float(distance.max()),'native_depth_field':('Common shallow affine interior, with exact retained H positions at the outer skin boundary' if shallow else 'Robust quadratic fitted only to original H cut-boundary points; no eyeball/ellipsoid'),'quadratic_coefficients':coefficients.tolist(),'depth_fit_rms_native':float(np.sqrt(np.mean(surface_error**2))),'depth_fit_maximum_native':float(abs(surface_error).max()),'aperture_points_inside_original_projected_cut_fraction':inside_fraction,'ray_surface_fallbacks':failures,'opening_native_bounds':[to_native(opening).min(0).tolist(),to_native(opening).max(0).tolist()],'objects':rows,'triangles':sum(r['triangles'] for r in rows),'provisional_items':['Uniform shutter pigment requires boundary material integration.','Exact source coordinates retained at outer edge; normal/material continuity not yet final.','Hidden upper iris continuation is authored, not traced.','Uncertain hair-adjacent fan tips omitted.','No blink/gaze shape keys yet; silhouette prototype first.']})
        report[-1]['actual_shutter_attachment']=attachment
    after={o.name:fingerprint(o) for o in scene.objects if o.name in before};assert before==after
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    bpy.ops.wm.save_as_mainfile(filepath=str(trial/'Ren_DesignerEyes_Prototype.blend'))
    construction={'status':'FIRST_SHALLOW_DESIGNER_CONTOUR_PROTOTYPE_REQUIRES_RENDER_REVIEW','accepted_source_sha256':SOURCE_SHA,'accepted_non_eye_geometry_uv_morph_hierarchy_changed':False,'non_eye_fingerprints_equal':before==after,'source_unchanged':True,'camera':camera,'source_curve_sha256':sha(TRACE),'source_boundary_provenance_sha256':sha(boundary_file),'rejected_eye_meshes_used_as_geometry_templates':False,'rejected_eye_meshes_retained_hidden_in_context':True,'eyes':report,'triangles':sum(r['triangles'] for r in report)}
    dump(trial_name+'/construction.json',construction)
    if '--no-render' in sys.argv:
        print('DESIGNER_EYE_PROTOTYPE_SAVED_FOR_VISIBILITY',trial_name,flush=True);return
    scene.render.filepath=str(trial/'portrait-complete-head.png');bpy.ops.render.render(write_still=True)
    # Unobscured diagnostic changes visibility only; no source hair or cap edits.
    hidden=[]
    for obj in scene.objects:
        if obj.type=='MESH' and (obj.name.startswith('Ren_Hair_H_') or obj.name=='Ren_Cap'):
            hidden.append((obj,obj.hide_render));obj.hide_render=True
    scene.render.filepath=str(trial/'portrait-unobscured-eyes.png');bpy.ops.render.render(write_still=True)
    for obj,state in hidden:obj.hide_render=state
    assert sha(SOURCE)==SOURCE_SHA
    dump(trial_name+'/construction.json',construction)
    print('DESIGNER_EYE_PROTOTYPE_READY',[(r['side'],r['triangles'],r['aperture_points_inside_original_projected_cut_fraction']) for r in report],flush=True)


def visibility_audit():
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    trial_name=selected_trial();trial=OUT/trial_name;source=trial/'Ren_DesignerEyes_Prototype.blend';before=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source))
    fit=json.loads((OUT/'portrait-camera-fit-v1.json').read_text());p=fit['parameter_vector'];right,up,view=camera_axes(*p[:3]);scale,tx,ty=p[3:];matrix=bpy.data.objects['Ren_H_Head'].matrix_world
    deps=bpy.context.evaluated_depsgraph_get();objects=[]
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or obj.hide_render or obj.name.startswith('Ren_Hair_H_') or obj.name=='Ren_Cap':continue
        objects.append((obj,BVHTree.FromObject(obj,deps),obj.matrix_world.inverted()))
    tests=[('R pupil',[461.8,160.2]),('R iris lower',[465,165]),('R iris left',[455,158]),('R sclera',[479,170]),('R upper ink',[459,152]),('R upper ink inner',[481,162]),('L pupil',[537.9,208.5]),('L iris lower',[544,213]),('L sclera',[554,218]),('L upper ink',[545,204]),('L inner corner',[530,205]),('L outer fan',[571,215])]
    grid=[]
    if '--grid' in sys.argv:
        traces=json.loads(TRACE.read_text())['traces']
        for side,record in [('R',traces[0]),('L',traces[1])]:
            opening=trace_opening(record);region=PolygonRegion(opening);low=np.floor(opening.min(0)).astype(int);high=np.ceil(opening.max(0)).astype(int)
            for y in range(low[1],high[1]+1):
                for x in range(low[0],high[0]+1):
                    if region.contains_point([x+.5,y+.5]):tests.append(('grid '+side,[x+.5,y+.5]))
    rows=[]
    for label,pixel in tests:
        o=(pixel[0]-tx)/scale*right+(ty-pixel[1])/scale*up+view*3;origin=matrix@Vector(o);direction=(matrix.to_3x3()@Vector(-view)).normalized();hits=[]
        for obj,tree,inverse in objects:
            local_o=inverse@origin;local_d=(inverse.to_3x3()@direction).normalized();hit=tree.ray_cast(local_o,local_d)
            if hit[0] is not None:
                at=obj.matrix_world@hit[0];hits.append({'object':obj.name,'world_distance':(at-origin).length,'native_hit':list(matrix.inverted()@at),'face':int(hit[2])})
        hits.sort(key=lambda h:h['world_distance'])
        if label.startswith('grid'):
            side=label[-1];name=hits[0]['object'] if hits else None;allowed=bool(name and name.startswith('Ren_DesignerEye_'+side+'_') and any(s in name for s in ['_Backing','_Iris','_Pupil','_Highlight']))
            grid.append({'side':side,'source_pixel':pixel,'front_object':name,'colored_eye_visible':allowed})
        else:rows.append({'label':label,'source_pixel':pixel,'front_to_back_hits':hits})
    counts={side:{'samples':sum(r['side']==side for r in grid),'occluded_samples':sum(r['side']==side and not r['colored_eye_visible'] for r in grid),'occluders':{name:sum(r['side']==side and not r['colored_eye_visible'] and r['front_object']==name for r in grid) for name in set(r['front_object'] for r in grid if r['side']==side and not r['colored_eye_visible'])}} for side in ['R','L']}
    assert sha(source)==before;dump(trial_name+'/source-pixel-visibility.json',{'source_sha256':before,'source_unchanged':True,'hair_and_cap_excluded':True,'tests':rows,'opening_pixel_grid':grid,'opening_grid_summary':counts})
    print('ACTUAL_VISIBILITY',[(r['label'],[h['object'] for h in r['front_to_back_hits'][:4]]) for r in rows],flush=True)
    if grid:print('OPENING_GRID_VISIBILITY',counts,flush=True)


def render_prototype():
    import bpy
    from mathutils import Vector
    trial_name=selected_trial();trial=OUT/trial_name;source=trial/('Ren_DesignerEyes_Pigment.blend' if '--pigment' in sys.argv else 'Ren_DesignerEyes_Prototype.blend');before=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
    captures=[]
    def capture(name):
        scene.render.filepath=str(trial/name);bpy.ops.render.render(write_still=True);captures.append({'file':name,'sha256':sha(trial/name)});print('DESIGNER_CAPTURE',name,flush=True)
    install_portrait_camera(scene);capture('portrait-complete-head.png')
    hair=[o for o in scene.objects if o.type=='MESH' and (o.name.startswith('Ren_Hair_H_') or o.name=='Ren_Cap')]
    states=[o.hide_render for o in hair]
    for o in hair:o.hide_render=True
    capture('portrait-unobscured-eyes.png')
    for o,s in zip(hair,states):o.hide_render=s
    registration=bpy.data.objects['Ren_H_Head'].matrix_world;target=registration@Vector((0,-.10,.40));camera=scene.camera
    scene.render.resolution_x=scene.render.resolution_y=800;camera.data.ortho_scale=.96*registration.to_scale()[0]
    for name,direction in [('front',(0,-3,0)),('quarter',(-1.3,-2.7,0)),('profile',(-3,-.035,0))]:
        camera.location=target+registration.to_3x3()@Vector(direction);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();capture('whole-head-'+name+'.png')
    # Actual flat-color layer render, with head/shutters occluding normally.
    install_portrait_camera(scene)
    for o in hair:o.hide_render=True
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.cycles.samples=8;scene.cycles.use_denoising=False
    palette={'other':(0,0,0),'backing':(0,1,0),'iris':(0,0,1),'upperInk':(1,0,0),'lowerInk':(1,1,0),'crease':(1,0,1)};materials={}
    for name,color in palette.items():
        m=bpy.data.materials.new('DESIGNER_MASK_'+name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');emit=n.new('ShaderNodeEmission');emit.inputs[0].default_value=(*color,1);m.node_tree.links.new(emit.outputs[0],out.inputs[0]);materials[name]=m
    assignments={}
    for o in scene.objects:
        if o.type!='MESH':continue
        category='other'
        if o.name.startswith('Ren_DesignerEye_'):
            if o.name.endswith('_Backing'):category='backing'
            elif any(s in o.name for s in ['_Iris','_Pupil','_Highlight']):category='iris'
            elif any(s in o.name for s in ['_UpperInk','_VisibleFan']):category='upperInk'
            elif o.name.endswith('_LowerInk'):category='lowerInk'
            elif o.name.endswith('_Crease'):category='crease'
        assignments[o.name]=category
        for slot in o.material_slots:slot.material=materials[category]
    capture('portrait-layer-visibility.png')
    assert sha(source)==before
    dump(trial_name+'/captures.json',{'source_sha256':before,'source_unchanged':True,'captures':captures,'flat_layer_palette':palette,'flat_layer_assignments':assignments,'flat_mask_hair_cap_hidden':True,'status':'ACTUAL_RENDERED_VIEWS_REQUIRE_VISUAL_REVIEW_NO_LIKENESS_APPROVAL'})


def prototype_plate():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    trial=OUT/selected_trial();art=plt.imread(ART);beauty=plt.imread(trial/'portrait-unobscured-eyes.png');labels=plt.imread(trial/'portrait-layer-visibility.png');traces=json.loads(TRACE.read_text())['traces']
    fig,axes=plt.subplots(2,3,figsize=(17,9))
    for row,record in enumerate(traces[:2]):
        x0,x1,y0,y1=record['bounds']
        for column,ax in enumerate(axes[row]):
            if column in (0,2):ax.imshow(art,interpolation='nearest')
            elif column==1:ax.imshow(beauty,interpolation='nearest');ax.set_facecolor('#c9d0d8')
            if column==2:
                actual=(labels[:,:,1]>.5)&(labels[:,:,0]<.25)|(labels[:,:,2]>.5)&(labels[:,:,0]<.25)
                crop=actual[y0:y1+1,x0:x1+1];ax.contour(np.arange(x0,x1+1),np.arange(y0,y1+1),crop,levels=[.5],colors=['#ff922f'],linewidths=.9)
                opening=trace_opening(record);ax.plot(opening[:,0],opening[:,1],color='#00d5ff',lw=.8)
            ax.set_xlim(x0,x1);ax.set_ylim(y1,y0);ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y');ax.set_title(record['name']+'\n'+['Actual designer source','Actual 3D, hair hidden','Cyan traced opening / orange visible 3D opening'][column],fontsize=10)
    fig.suptitle('Actual source-plane construction comparison — '+selected_trial()+'\nOne fixed camera and uniform scale; no image-plane stretch. This is not likeness approval.',fontsize=13);fig.tight_layout();fig.savefig(trial/'source-eye-comparison.png',dpi=160)
    fig,axes=plt.subplots(1,3,figsize=(17,7))
    for ax,name in zip(axes,['front','quarter','profile']):
        ax.imshow(plt.imread(trial/f'whole-head-{name}.png'));ax.set_title(name);ax.axis('off');ax.set_facecolor('#c9d0d8')
    fig.suptitle('Actual whole-head views — fixed accepted face, '+selected_trial()+' eye prototype; not final likeness',fontsize=13);fig.tight_layout();fig.savefig(trial/'whole-head-view-plate.png',dpi=150)


def apply_boundary_pigment():
    import bpy
    assert sha(SOURCE)==SOURCE_SHA;bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    approved={o.name:fingerprint(o) for o in bpy.context.scene.objects if o.type=='MESH' and not o.name.startswith('Ren_H_Eye_')}
    trial=OUT/selected_trial();source=trial/'Ren_DesignerEyes_Prototype.blend';before_sha=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source))
    present={n:fingerprint(bpy.data.objects[n]) for n in approved};assert approved==present,'Accepted non-eye context drift'
    before={o.name:fingerprint(o) for o in bpy.context.scene.objects if o.type=='MESH'}
    donor=OUT/'boundary-pigment-v1/boundary-pigment.json';pigment=json.loads(donor.read_text());records=[];traces=json.loads(TRACE.read_text())['traces'];parameters=json.loads((OUT/'portrait-camera-fit-v1.json').read_text())['parameter_vector']
    for row in pigment['eyes']:
        side=row['side'];obj=bpy.data.objects[f'Ren_DesignerEye_{side}_SkinShutter'];mesh=obj.data;p=coords(mesh.vertices).astype(np.float64)
        boundary=np.array([s['native_position'] for s in row['samples']]);raw=np.array([s['filtered_srgb'] for s in row['samples']]);ids=np.array([s['source_h_vertex_id'] for s in row['samples']])
        distances=np.linalg.norm(p[:,None,:]-boundary[None,:,:],axis=2);nearest=distances.argmin(1);exact=distances[np.arange(len(p)),nearest]<1e-7
        assert exact.sum()>=len(boundary),(side,int(exact.sum()),len(boundary))
        base=np.tile(linear(np.median(raw,axis=0)),(len(p),1));base[exact]=linear(raw[nearest[exact]])
        adjacency=[set() for _ in p]
        for edge in mesh.edges:
            a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
        free=np.array([i for i in range(len(p)) if not exact[i] and adjacency[i]],dtype=int);lookup={v:i for i,v in enumerate(free)};laplace=np.zeros((len(free),len(free)));rhs=np.zeros((len(free),3))
        for vertex in free:
            i=lookup[vertex];laplace[i,i]=len(adjacency[vertex])
            for neighbor in adjacency[vertex]:
                if neighbor in lookup:laplace[i,lookup[neighbor]]-=1
                else:rhs[i]+=base[neighbor]
        if len(free):base[free]=np.linalg.solve(laplace,rhs)
        attr=mesh.color_attributes.new(name='DesignerSkinBase',type='FLOAT_COLOR',domain='POINT')
        for value,color in zip(attr.data,base):value.color=(*color,1)
        source_attr=mesh.attributes.get('source_h_boundary_vertex_id') or mesh.attributes.new(name='source_h_boundary_vertex_id',type='INT',domain='POINT');source_ids=np.full(len(p),-1,np.int32);source_ids[exact]=ids[nearest[exact]];source_attr.data.foreach_set('value',source_ids)
        # Distinct moving smoky-pigment field; it does not alter the source skin samples.
        record=traces[0 if side=='R' else 1];ink_curve=np.vstack([c['sampled_source_pixel_curve'] for c in record['curves'] if c['layer']=='upper ink exterior' and c['visibility']=='visible'])
        screen=project_native(p,parameters);boundary_screen=project_native(boundary,parameters)
        ink_distance=np.linalg.norm(screen[:,None,:]-ink_curve[None,::4,:],axis=2).min(1);edge_distance=np.linalg.norm(screen[:,None,:]-boundary_screen[None,:,:],axis=2).min(1)
        smoky=.34*np.exp(-(ink_distance/3.7)**2)*np.minimum(1,edge_distance/2.5);smoky[exact]=0
        weight=mesh.attributes.new(name='DesignerSmokyLidWeight',type='FLOAT',domain='POINT');weight.data.foreach_set('value',smoky.astype(np.float32))
        material=bpy.data.materials.new(f'Ren_DesignerEye_{side}_ShutterPigment');material.use_nodes=True;n=material.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');emit=n.new('ShaderNodeEmission');color=n.new('ShaderNodeVertexColor');color.layer_name='DesignerSkinBase';mix=n.new('ShaderNodeMixRGB');mix.blend_type='MIX';mix.inputs[2].default_value=(*linear([.46,.32,.36]),1);scalar=n.new('ShaderNodeAttribute');scalar.attribute_name='DesignerSmokyLidWeight';material.node_tree.links.new(color.outputs['Color'],mix.inputs[1]);material.node_tree.links.new(scalar.outputs['Fac'],mix.inputs[0]);material.node_tree.links.new(mix.outputs[0],emit.inputs[0]);material.node_tree.links.new(emit.outputs[0],out.inputs[0]);mesh.materials[0]=material
        records.append({'side':side,'boundary_samples':len(boundary),'exact_boundary_vertices':int(exact.sum()),'free_vertices':len(free),'base_linear_range':[base.min(0).tolist(),base.max(0).tolist()],'maximum_boundary_color_error':float(abs(base[exact]-linear(raw[nearest[exact]])).max()),'source_ids_attribute':source_attr.name,'base_pigment_attribute':attr.name,'smoky_pigment_attribute':weight.name,'maximum_smoky_weight':float(smoky.max()),'boundary_smoky_weight':0.,'status':'Source skin pigment plus distinct authored moving smoky field; no stationary open-eye paint.'})
    after={o.name:fingerprint(o) for o in bpy.context.scene.objects if o.type=='MESH'}
    for name,a in before.items():
        b=after[name];a={k:v for k,v in a.items() if k!='materials'};b={k:v for k,v in b.items() if k!='materials'};assert a==b,name
    assert approved=={n:fingerprint(bpy.data.objects[n]) for n in approved}
    output=trial/'Ren_DesignerEyes_Pigment.blend';bpy.ops.wm.save_as_mainfile(filepath=str(output));assert sha(source)==before_sha and sha(SOURCE)==SOURCE_SHA
    dump(selected_trial()+'/pigment-integration.json',{'source_prototype_sha256':before_sha,'boundary_pigment_json_sha256':sha(donor),'output_sha256':sha(output),'geometry_uv_morph_hierarchy_unchanged':True,'accepted_non_eye_geometry_uv_morph_material_hierarchy_equal':True,'source_unchanged':True,'eyes':records,'artist_likeness_approval':False})
    print('DESIGNER_PIGMENT_INTEGRATED',records,flush=True)


def layer_review():
    """Review actual rendered masks, without treating manual traces as ground truth."""
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from scipy.spatial import cKDTree
    trial=OUT/selected_trial();art=plt.imread(ART);mask_file=trial/'portrait-layer-visibility-distinct.png';distinct=mask_file.exists();mask_file=mask_file if distinct else trial/'portrait-layer-visibility.png';labels=plt.imread(mask_file);beauty=plt.imread(trial/'portrait-unobscured-eyes.png')
    traces=json.loads(TRACE.read_text())['traces'];red,green,blue=labels[:,:,:3].transpose(2,0,1)
    crease_file=trial/'portrait-crease-visibility.png'
    crease_mask=(plt.imread(crease_file)[:,:,:3].min(2)>.5) if crease_file.exists() else (((red>.5)&(green>.5)&(blue>.5)) if distinct else ((red>.5)&(blue>.5)&(green<.25)))
    masks={'opening':((green>.5)&(red<.25))|((blue>.5)&(red<.25)),
           'iris':(blue>.5)&(red<.25)&(green<.25),
           'upper ink':(red>.5)&(green<.25)&(blue<.25),
           'crease':crease_mask}
    colors={'opening':'#00d5ff','iris':'#65ef6b','upper ink':'#ffb42e','crease':'#e079ff'}
    fig,axes=plt.subplots(2,4,figsize=(20,10));rows=[]
    for index,record in enumerate(traces[:2]):
        x0,x1,y0,y1=record['bounds'];xx=np.arange(x0,x1+1);yy=np.arange(y0,y1+1)
        for column,ax in enumerate(axes[index]):
            ax.imshow(beauty if column==1 else art,interpolation='nearest');ax.set_xlim(x0,x1);ax.set_ylim(y1,y0);ax.set_xlabel('Original source pixel X');ax.set_ylabel('Original source pixel Y');ax.set_facecolor('#c9d0d8')
            ax.set_title(record['name']+'\n'+['Designer source','Actual 3D, hair hidden','Actual opening + iris boundary','Actual upper ink + crease boundary'][column],fontsize=10)
        item={'side':'R' if index==0 else 'L','source_trace':record['name'],'visible_curve_to_render_mask_boundary':[]}
        for category,column in [('opening',2),('iris',2),('upper ink',3),('crease',3)]:
            crop=masks[category][y0:y1+1,x0:x1+1]
            paths=axes[index,column].contour(xx,yy,crop,levels=[.5],colors=[colors[category]],linewidths=.9).allsegs[0]
            paths=[p for p in paths if len(p)>1]
            if not paths:continue
            dense=[]
            for path in paths:
                for a,b in zip(path[:-1],path[1:]):
                    dense.extend(a+(b-a)*np.linspace(0,1,max(2,int(np.linalg.norm(b-a)*12)))[:,None])
            tree=cKDTree(dense)
            matching={'opening':['upper aperture','lower aperture'],'iris':['visible iris arc'],'upper ink':['upper ink exterior','lash fan exterior'],'crease':['crease']}[category]
            for curve in record['curves']:
                if curve['layer'] not in matching or curve['visibility']!='visible':continue
                points=np.asarray(curve['sampled_source_pixel_curve']);distance=tree.query(points)[0]
                item['visible_curve_to_render_mask_boundary'].append({'layer':curve['layer'],'points':len(points),'median_source_pixels':float(np.median(distance)),'p95_source_pixels':float(np.percentile(distance,95)),'maximum_source_pixels':float(distance.max())})
        rows.append(item)
    fig.suptitle('Actual rendered layer boundaries over the designer source — '+selected_trial()+'\nCyan opening, green iris, amber upper ink, violet crease. Original pixels; uncertain source junctions remain excluded.',fontsize=13);fig.tight_layout();fig.savefig(trial/'layer-contour-comparison.png',dpi=150)
    dump(selected_trial()+'/rendered-layer-review.json',{'status':'DIAGNOSTIC_ONLY_NOT_A_ONE_TO_ONE_LIKENESS_PASS','rendered_mask':mask_file.name,'rendered_mask_sha256':sha(mask_file),'distinct_crease_mask':distinct,'source_trace_sha256':sha(TRACE),'camera_fit_sha256':sha(OUT/'portrait-camera-fit-v1.json'),'independent_xy_warp':False,'measurement':'One-way distance from reviewed manually digitized visible curves to actual rendered categorical mask boundaries; original source-pixel units. Rasterization and manual tracing uncertainty remain. No acceptance threshold is invented.','excluded':'Hair-occluded and uncertain source segments; hidden iris continuation; whole-face alignment and pigment appearance are not certified by these distances.','eyes':rows})


def render_distinct_layer_mask():
    import bpy
    trial=OUT/selected_trial();source=trial/'Ren_DesignerEyes_Pigment.blend';before=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;install_portrait_camera(scene)
    scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=False;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    palette={'other':(0,0,0),'backing':(0,1,0),'iris':(0,0,1),'upperInk':(1,0,0),'lowerInk':(1,1,0),'crease':(1,1,1)};materials={}
    for name,color in palette.items():
        m=bpy.data.materials.new('DISTINCT_MASK_'+name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');emit=n.new('ShaderNodeEmission');emit.inputs[0].default_value=(*color,1);m.node_tree.links.new(emit.outputs[0],out.inputs[0]);materials[name]=m
    for obj in scene.objects:
        if obj.type!='MESH':continue
        if obj.name.startswith('Ren_Hair_H_') or obj.name=='Ren_Cap':obj.hide_render=True
        category='other'
        if obj.name.startswith('Ren_DesignerEye_'):
            if obj.name.endswith('_Backing'):category='backing'
            elif any(s in obj.name for s in ['_Iris','_Pupil','_Highlight']):category='iris'
            elif any(s in obj.name for s in ['_UpperInk','_VisibleFan']):category='upperInk'
            elif obj.name.endswith('_LowerInk'):category='lowerInk'
            elif obj.name.endswith('_Crease'):category='crease'
        for slot in obj.material_slots:slot.material=materials[category]
    scene.render.filepath=str(trial/'portrait-layer-visibility-distinct.png');bpy.ops.render.render(write_still=True)
    for obj in scene.objects:
        if obj.type!='MESH':continue
        m=materials['crease'] if obj.name.startswith('Ren_DesignerEye_') and obj.name.endswith('_Crease') else materials['other']
        for slot in obj.material_slots:slot.material=m
    scene.render.filepath=str(trial/'portrait-crease-visibility.png');bpy.ops.render.render(write_still=True);assert sha(source)==before
    dump(selected_trial()+'/distinct-layer-mask.json',{'source_sha256':before,'source_unchanged':True,'palette':palette,'note':'White crease avoids confusion with antialiased red upper ink adjacent to blue iris. Same geometry, source camera, and visibility as the previous mask.'})


def verify_layer_refinement():
    import bpy
    old=OUT/'trial-v4/Ren_DesignerEyes_Pigment.blend';new=OUT/'trial-v5/Ren_DesignerEyes_Pigment.blend';old_sha,new_sha=sha(old),sha(new)
    def fixed(obj):return not (obj.name.startswith('Ren_DesignerEye_') and any(s in obj.name for s in ['_UpperInk','_VisibleFan','_Crease']))
    def skin_data():
        result={}
        for side in ['L','R']:
            mesh=bpy.data.objects[f'Ren_DesignerEye_{side}_SkinShutter'].data;ids=np.empty(len(mesh.vertices),np.int32);mesh.attributes['source_h_boundary_vertex_id'].data.foreach_get('value',ids);pigment=np.empty(len(mesh.vertices)*4,np.float32);mesh.color_attributes['DesignerSkinBase'].data.foreach_get('color',pigment);weight=np.empty(len(mesh.vertices),np.float32);mesh.attributes['DesignerSmokyLidWeight'].data.foreach_get('value',weight)
            result[side]={'source_ids_sha256':hashlib.sha256(ids.tobytes()).hexdigest(),'base_skin_color_sha256':hashlib.sha256(pigment.tobytes()).hexdigest(),'boundary_count':int((ids>=0).sum()),'boundary_smoky_maximum':float(abs(weight[ids>=0]).max())}
        return result
    bpy.ops.wm.open_mainfile(filepath=str(old));before={o.name:fingerprint(o) for o in bpy.context.scene.objects if o.type=='MESH' and fixed(o)};skin_before=skin_data()
    bpy.ops.wm.open_mainfile(filepath=str(new));after={o.name:fingerprint(o) for o in bpy.context.scene.objects if o.type=='MESH' and fixed(o)};skin_after=skin_data();assert before==after and skin_before==skin_after
    counts=[{'object':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Ren_DesignerEye_')]
    assert sha(old)==old_sha and sha(new)==new_sha and sha(SOURCE)==SOURCE_SHA
    dump('trial-v5/saved-refinement-verification.json',{'source_v4_sha256':old_sha,'output_v5_sha256':new_sha,'accepted_source_sha256':SOURCE_SHA,'saved_fixed_geometry_uv_morph_material_names_hierarchy_exact':before==after,'saved_source_boundary_ids_base_pigment_and_zero_boundary_smoke_exact':skin_before==skin_after,'skin':skin_after,'new_eye_objects':counts,'new_eye_total_triangles':sum(r['triangles'] for r in counts),'runtime_optimization_status':'Authoring candidate; not a final mobile eye polygon/material allocation.','source_files_unchanged':True})
    print('V5_SAVED_VERIFICATION',sum(r['triangles'] for r in counts),'triangles',skin_after,flush=True)


def export_neutral_candidate():
    import bpy
    from mathutils import Matrix,Vector
    trial=OUT/selected_trial();source=trial/'Ren_DesignerEyes_Pigment.blend';source_sha=sha(source);captures=json.loads((trial/'captures.json').read_text());assert captures['source_sha256']==source_sha
    bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;eyes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('Ren_DesignerEye_')];registration=bpy.data.objects['Ren_H_Head'].matrix_world.copy()
    preserved={o.name:{'position_sha256':fingerprint(o)['positions_sha256'],'matrix_world':[list(r) for r in o.matrix_world],'shape_keys':list(o.data.shape_keys.key_blocks.keys()) if o.data.shape_keys else []} for o in eyes}
    root=bpy.data.objects.new('Ren_DesignerEyes_NeutralRoot',None);scene.collection.objects.link(root);root.matrix_world=Matrix.Identity(4);rows=[]
    for obj in eyes:
        mesh=obj.data;n=len(mesh.vertices);material=mesh.materials[0]
        if obj.name.endswith('_SkinShutter'):
            colors=np.empty(n*4,np.float32);mesh.color_attributes['DesignerSkinBase'].data.foreach_get('color',colors);colors=colors.reshape(n,4);weight=np.empty(n,np.float32);mesh.attributes['DesignerSmokyLidWeight'].data.foreach_get('value',weight);mix=next(n for n in material.node_tree.nodes if n.type=='MIX_RGB');smoke=np.array(mix.inputs[2].default_value[:3]);colors[:,:3]=colors[:,:3]*(1-weight[:,None])+smoke*weight[:,None];semantic='skin: original H boundary pigment plus separately authored moving smoky pigment'
        elif mesh.color_attributes.get('DesignerInkPigment'):
            colors=np.empty(n*4,np.float32);mesh.color_attributes['DesignerInkPigment'].data.foreach_get('color',colors);colors=colors.reshape(n,4);semantic='upper ink/lash: dark plum graphic pigment'
        elif mesh.color_attributes.get('DesignerIrisColor'):
            colors=np.empty(n*4,np.float32);mesh.color_attributes['DesignerIrisColor'].data.foreach_get('color',colors);colors=colors.reshape(n,4);semantic='iris: grey-blue graphic pigment, with separate pupil/highlight meshes'
        else:
            emit=next(n for n in material.node_tree.nodes if n.type=='EMISSION');colors=np.tile(np.array(emit.inputs[0].default_value),(n,1));semantic='crease: soft taupe' if obj.name.endswith('_Crease') else 'uniform graphic eye layer'
        colors[:,3]=1;attr=mesh.color_attributes.new(name='Ren_DesignerColor',type='FLOAT_COLOR',domain='POINT');attr.data.foreach_set('color',colors.astype(np.float32).ravel());index=list(mesh.color_attributes).index(attr);mesh.color_attributes.active_color_index=index;mesh.color_attributes.render_color_index=index
        # Explicit portable unlit material, preserving the reviewed final pigment.
        mat=bpy.data.materials.new(obj.name+'_Portable');mat.use_nodes=True;mat.diffuse_color=(1,1,1,1);nodes=mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');vc=nodes.new('ShaderNodeVertexColor');vc.layer_name=attr.name;mat.node_tree.links.new(vc.outputs['Color'],out.inputs['Surface']);mesh.materials.clear();mesh.materials.append(mat)
        world=obj.matrix_world.copy();obj.parent=root;obj.matrix_world=world
        rows.append({'object':obj.name,'semantic':semantic,'material':mat.name,'vertex_color_attribute':attr.name,'vertex_color_space':'linear RGB; alpha 1','vertices':n,'triangles':sum(len(p.vertices)-2 for p in mesh.polygons),'color_min':colors.min(0).tolist(),'color_max':colors.max(0).tolist(),'color_sha256':hashlib.sha256(colors.astype(np.float32).tobytes()).hexdigest(),'position_sha256':fingerprint(obj)['positions_sha256'],'matrix_world':[list(r) for r in obj.matrix_world]})
        assert fingerprint(obj)['positions_sha256']==preserved[obj.name]['position_sha256'] and [list(r) for r in obj.matrix_world]==preserved[obj.name]['matrix_world']
    for obj in list(scene.objects):
        if obj not in eyes and obj!=root:bpy.data.objects.remove(obj,do_unlink=True)
    markers=[]
    for name,p in [('Origin',(0,0,0)),('X',(.01,0,0)),('Y',(0,.01,0)),('Z',(0,0,.01))]:
        obj=bpy.data.objects.new('Ren_H_NativeAxis_'+name,None);scene.collection.objects.link(obj);obj.parent=root;obj.location=registration@Vector(p);markers.append({'name':obj.name,'world_position':list(obj.location),'native_position':p})
    folder=trial/'neutral-export-v2';folder.mkdir(exist_ok=True);blend=folder/'Ren_DesignerEyes_Neutral.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend));bpy.ops.object.select_all(action='SELECT')
    glb=folder/'Ren_DesignerEyes_Neutral.glb';bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,export_apply=False,export_materials='EXPORT',export_animations=False,export_cameras=False,export_lights=False,export_vertex_color='NAME',export_vertex_color_name='Ren_DesignerColor',export_all_vertex_colors=False)
    fbx=folder/'Ren_DesignerEyes_Neutral.fbx';kwargs={'filepath':str(fbx),'use_selection':True,'object_types':{'MESH','EMPTY'},'axis_forward':'-Z','axis_up':'Y','apply_unit_scale':True,'bake_space_transform':False,'add_leaf_bones':False,'bake_anim':False,'path_mode':'AUTO'};properties=bpy.ops.export_scene.fbx.get_rna_type().properties.keys()
    if 'colors_type' in properties:kwargs['colors_type']='LINEAR'
    if 'prioritize_active_color' in properties:kwargs['prioritize_active_color']=True
    bpy.ops.export_scene.fbx(**kwargs)
    assert sha(source)==source_sha and sha(SOURCE)==SOURCE_SHA
    dump(selected_trial()+'/neutral-export-v2/contract.json',{'status':'NEUTRAL_ONLY_SCRATCH_UNITY_VISUAL_REVIEW_NOT_PRODUCTION_OR_LIKENESS_APPROVAL','source_geometry_blend':str(source),'source_geometry_sha256':source_sha,'accepted_context':str(SOURCE),'accepted_context_sha256':SOURCE_SHA,'accepted_context_required':'Use its already cut H head geometry unchanged. Hide/remove rejected Ren_H_Eye_* meshes. Do not append a second face from this package; it contains eyes only. Material-only Tokon face/cap/closed-lip derivatives may be applied by their owners.','native_H_frame':'X horizontal, front -Y, Z up','native_to_review_world':[list(r) for r in registration],'neutral_root_world_transform':'identity; child mesh transforms retain exact reviewed registered coordinates','source_neutral_mesh_positions_unchanged':True,'eye_controls':'NONE: no blink, gaze, wide, squint, expression or lipsync claims. Preserve neutral geometry during this integration review.','vertex_color_export':'All final reviewed pigments are explicit active Ren_DesignerColor / COLOR_0 in linear RGB. FBX colors_type LINEAR. Runtime shader must use mesh vertex color once; white material tint. Do not multiply the same pigment a second time. Verify imported values against ranges/hash in this contract.','surface_material_guidance':'Skin shutters may use matching Tokon skin response; ink/iris/pupil/backing/crease retain graphic color and no broad corneal specular. Blender reference captures use unlit eye pigment; changed Unity lighting is a separate review variable.','uv':'Existing DesignerEyeUV retained; no raster textures required for this neutral color export. Original DesignerSkinBase and smoky attributes retained in Blender, final result encoded for portability.','native_axis_markers':markers,'eyes':rows,'total_eye_triangles':sum(r['triangles'] for r in rows),'runtime_budget_status':'Unoptimized authoring candidate; does not establish 40k complete Ren or iPhone 15 Plus 30fps performance.','exports':{p.name:sha(p) for p in [blend,glb,fbx]},'export_geometry_and_color_verification':'Source positions/matrices asserted equal before export. Fresh importer verification is required and recorded separately.','previous_export':'Sibling neutral-export is superseded: glTF exporter did not recognize the first emission-node vertex-color binding.'})
    print('DESIGNER_NEUTRAL_EXPORTED',str(folder),flush=True)


def verify_portable_candidate():
    import bpy
    import struct
    from mathutils import Vector
    from mathutils.kdtree import KDTree
    folder=OUT/selected_trial()/'neutral-export-v2';source=folder/'Ren_DesignerEyes_Neutral.blend';source_sha=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source));expected={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        mesh=obj.data;p=np.array([obj.matrix_world@v.co for v in mesh.vertices]);c=np.empty(len(mesh.vertices)*4,np.float32);mesh.color_attributes['Ren_DesignerColor'].data.foreach_get('color',c);tree=KDTree(len(p))
        for i,point in enumerate(p):tree.insert(point,i)
        tree.balance();uv_by_vertex=[[] for _ in p]
        for loop,value in zip(mesh.loops,mesh.uv_layers.active.data):uv_by_vertex[loop.vertex_index].append(np.array(value.uv))
        local_tree=KDTree(len(p))
        for i,point in enumerate(coords(mesh.vertices)):local_tree.insert(point,i)
        local_tree.balance();expected[obj.name]={'world':p,'colors':c.reshape(-1,4),'tree':tree,'local_tree':local_tree,'uv':uv_by_vertex,'triangles':sum(len(f.vertices)-2 for f in mesh.polygons)}
    # Validate stored glTF colors before Blender's importer quantizes them to BYTE_COLOR.
    glb_bytes=(folder/'Ren_DesignerEyes_Neutral.glb').read_bytes();json_length=struct.unpack_from('<I',glb_bytes,12)[0];gltf=json.loads(glb_bytes[20:20+json_length]);binary=glb_bytes[28+json_length:];stored=[]
    def accessor(index):
        a=gltf['accessors'][index];view=gltf['bufferViews'][a['bufferView']];dtype={5121:'u1',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']];components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];item=np.dtype(dtype).itemsize;offset=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',item*components);values=np.ndarray((a['count'],components),dtype=dtype,buffer=binary,offset=offset,strides=(stride,item)).astype(float)
        if a.get('normalized'):values/=np.iinfo(np.dtype(dtype)).max
        return values,a
    for node in gltf['nodes']:
        if 'mesh' not in node:continue
        reference=expected[node['name']]
        for primitive in gltf['meshes'][node['mesh']]['primitives']:
            p,_=accessor(primitive['attributes']['POSITION']);p=p[:,[0,2,1]]*np.array([1,-1,1]);colors,metadata=accessor(primitive['attributes']['COLOR_0']);error=0.
            for point,color in zip(p,colors):
                _,nearest,d=reference['local_tree'].find(Vector(point));candidates=[i for _,i,dd in reference['local_tree'].find_range(Vector(point),max(5e-7,d+1e-8))];error=max(error,min(float(abs(color-reference['colors'][i]).max()) for i in candidates))
            assert error<1/65535+1e-7,(node['name'],'stored glTF color',error);assert 'KHR_materials_unlit' in gltf['materials'][primitive['material']].get('extensions',{})
            stored.append({'object':node['name'],'component_type':metadata['componentType'],'normalized':metadata.get('normalized',False),'maximum_stored_linear_color_error':error,'unlit_material':True})
    reports=[]
    for extension in ['glb','fbx']:
        bpy.ops.wm.read_factory_settings(use_empty=True);path=folder/('Ren_DesignerEyes_Neutral.'+extension);file_sha=sha(path)
        if extension=='glb':bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            kwargs={'filepath':str(path)}
            if 'colors_type' in bpy.ops.import_scene.fbx.get_rna_type().properties.keys():kwargs['colors_type']='LINEAR'
            bpy.ops.import_scene.fbx(**kwargs)
        rows=[];found=[]
        for obj in bpy.context.scene.objects:
            if obj.type!='MESH':continue
            assert obj.name in expected,('Unexpected exported mesh',obj.name);found.append(obj.name);reference=expected[obj.name];mesh=obj.data;points=np.array([obj.matrix_world@v.co for v in mesh.vertices]);triangles=sum(len(f.vertices)-2 for f in mesh.polygons);assert triangles==reference['triangles']
            attribute=mesh.color_attributes.get('Ren_DesignerColor') or mesh.color_attributes.active_color;assert attribute is not None,(extension,obj.name,'Missing vertex pigment');colors=np.empty(len(attribute.data)*4,np.float32);attribute.data.foreach_get('color',colors);colors=colors.reshape(-1,4);indices=np.arange(len(points)) if attribute.domain=='POINT' else np.array([l.vertex_index for l in mesh.loops]);position_error=0.;color_error=0.;storage_adjusted_error=0.;srgb_storage_error=0.;uv_error=0.;reference_colors=reference['colors'].copy()
            def srgb_value(rgb):return np.where(rgb<=.0031308,12.92*rgb,1.055*np.maximum(rgb,0)**(1/2.4)-.055)
            if attribute.data_type=='BYTE_COLOR':
                if extension=='glb':reference_colors=np.round(reference_colors*65535)/65535
                rgb=reference_colors[:,:3];srgb=np.where(rgb<=.0031308,12.92*rgb,1.055*np.maximum(rgb,0)**(1/2.4)-.055);reference_colors[:,:3]=linear(np.floor(np.clip(srgb,0,1)*255+.5)/255)
            for index,color in zip(indices,colors):
                point=points[index];_,nearest,distance=reference['tree'].find(Vector(point));position_error=max(position_error,distance);candidates=[i for _,i,d in reference['tree'].find_range(Vector(point),max(5e-7,distance+1e-8))];color_error=max(color_error,min(float(abs(color-reference['colors'][i]).max()) for i in candidates));storage_adjusted_error=max(storage_adjusted_error,min(float(abs(color-reference_colors[i]).max()) for i in candidates))
                srgb_storage_error=max(srgb_storage_error,min(float(abs(srgb_value(color[:3])-srgb_value(reference['colors'][i,:3])).max()) for i in candidates))
            for loop,value in zip(mesh.loops,mesh.uv_layers.active.data):
                point=points[loop.vertex_index];_,nearest,distance=reference['tree'].find(Vector(point));candidates=[i for _,i,d in reference['tree'].find_range(Vector(point),max(5e-7,distance+1e-8))];available=[uv for i in candidates for uv in reference['uv'][i]];uv_error=max(uv_error,min(float(abs(np.array(value.uv)-uv).max()) for uv in available))
            assert position_error<2e-6,(extension,obj.name,'position',position_error)
            if attribute.data_type=='BYTE_COLOR':assert srgb_storage_error<=1/255+1e-6,(extension,obj.name,'BYTE_COLOR exceeds one sRGB8 channel step',srgb_storage_error)
            else:assert color_error<2e-5,(extension,obj.name,'float color',color_error)
            assert uv_error<2e-6,(extension,obj.name,'uv',uv_error)
            rows.append({'object':obj.name,'imported_vertices':len(points),'triangles':triangles,'vertex_color_attribute':attribute.name,'vertex_color_domain':attribute.domain,'vertex_color_storage':attribute.data_type,'maximum_world_position_error':position_error,'maximum_linear_color_error':color_error,'maximum_difference_from_standard_sRGB8_rounding':storage_adjusted_error,'maximum_sRGB_color_error':srgb_storage_error,'color_precision_check':'BYTE_COLOR within one sRGB8 channel step; stored raw16 verified separately' if attribute.data_type=='BYTE_COLOR' else 'FLOAT_COLOR within 0.00002 linear','maximum_uv_error':uv_error})
        assert set(found)==set(expected) and sha(path)==file_sha;reports.append({'format':extension,'file_sha256':file_sha,'fresh_import':True,'FBX_color_interpretation':'LINEAR' if extension=='fbx' else None,'meshes':rows})
    assert sha(source)==source_sha;dump(selected_trial()+'/neutral-export-v2/roundtrip-verification.json',{'status':'FRESH_BLENDER_IMPORT_GEOMETRY_COLOR_UV_PASS_UNITY_REVIEW_STILL_REQUIRED','source_blend_sha256':source_sha,'source_files_unchanged':True,'stored_glTF_color_checks':stored,'reports':reports,'color_precision_note':'Stored GLB uses normalized unsigned16 COLOR_0. Blender glTF importer explicitly creates BYTE_COLOR; its extra sRGB8 conversion is reported separately and checked within one encoded channel step. Float identity is not claimed. Standard analytic rounding also differs at a few byte thresholds and its error is preserved in the report.','limitation':'Verifies stored neutral geometry/color/UV through fresh Blender import, not Unity shader behavior, device performance, blink or artist likeness.'});print('DESIGNER_EXPORT_ROUNDTRIP_PASS',flush=True)


def render_unobscured_profiles():
    import bpy
    from mathutils import Vector
    trial=OUT/selected_trial();source=trial/'Ren_DesignerEyes_Pigment.blend';before=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;install_portrait_camera(scene)
    for obj in scene.objects:
        if obj.type=='MESH' and (obj.name.startswith('Ren_Hair_H_') or obj.name=='Ren_Cap'):obj.hide_render=True
    registration=bpy.data.objects['Ren_H_Head'].matrix_world;target=registration@Vector((0,-.10,.42));camera=scene.camera;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=scene.render.resolution_y=800;scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';camera.data.ortho_scale=.88*registration.to_scale()[0];captures=[]
    for side,direction in [('R',(-3,-.035,0)),('L',(3,-.035,0))]:
        camera.location=target+registration.to_3x3()@Vector(direction);camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();name='profile-'+side+'-hair-hidden.png';scene.render.filepath=str(trial/name);bpy.ops.render.render(write_still=True);captures.append({'file':name,'sha256':sha(trial/name)});print('UNOBSCURED_PROFILE_CAPTURE',name,flush=True)
    assert sha(source)==before;dump(selected_trial()+'/unobscured-profile-captures.json',{'source_sha256':before,'source_unchanged':True,'hidden':'Both actual H hair objects and cap; accepted skin and scalp/back remain present. No eye/face geometry change.','captures':captures})


def refine_layers_v5():
    """Only ink, crease and moving pigment change; v4 opening/skin stay exact."""
    import bpy
    from mathutils import Vector
    from mathutils.geometry import tessellate_polygon,delaunay_2d_cdt
    from mathutils.bvhtree import BVHTree
    fan_refine='--fan-v6' in sys.argv;trial_name='trial-v6' if fan_refine else 'trial-v5'
    source=OUT/'trial-v4/Ren_DesignerEyes_Pigment.blend';source_sha=sha(source);bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    before={o.name:fingerprint(o) for o in scene.objects if o.type=='MESH'}
    fit=json.loads((OUT/'portrait-camera-fit-v1.json').read_text());parameters=fit['parameter_vector'];right,up,toward=camera_axes(*parameters[:3]);scale,tx,ty=parameters[3:]
    construction=json.loads((OUT/'trial-v4/construction.json').read_text());traces=json.loads(TRACE.read_text())['traces'];coeff={r['side']:np.array(r['quadratic_coefficients']) for r in construction['eyes']}
    boundary={r['side']:np.array(r['geometric_paths'][0]['native_points']) for r in json.loads((PARTS/'h-eye-controls-v1/seam-contract-v1/native-study-cut-boundaries.json').read_text())['eyes']}
    parent=bpy.data.objects['Head'];registration=bpy.data.objects['Ren_H_Head'].matrix_world.copy();collection=bpy.data.collections['Ren_DesignerEyes_Prototype'];deps=bpy.context.evaluated_depsgraph_get();attachment=[];changes=[];mutable=set()
    def make_material(name,color=None):
        m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');emit=n.new('ShaderNodeEmission');m.node_tree.links.new(emit.outputs[0],out.inputs[0])
        if color is None:
            attr=n.new('ShaderNodeVertexColor');attr.layer_name='DesignerInkPigment';m.node_tree.links.new(attr.outputs['Color'],emit.inputs[0])
        else:emit.inputs[0].default_value=(*linear(color),1)
        return m
    ink_mat=make_material('Ren_DesignerEyes_V5_LayeredInk');crease_mat=make_material('Ren_DesignerEyes_V5_Crease',(.40,.265,.29))
    for side,record in [('R',traces[0]),('L',traces[1])]:
        c=coeff[side];center=boundary[side].mean(0);a,b=c[1]/.09,c[2]/.06
        def to_native(pixels,offset=-.0018):
            p=np.asarray(pixels);o=(p[:,0,None]-tx)/scale*right+(ty-p[:,1,None])/scale*up
            t=(c[0]+a*(o[:,0]-center[0])+b*(o[:,2]-center[2])+offset-o[:,1])/(toward[1]-a*toward[0]-b*toward[2]);return o+t[:,None]*toward
        surfaces=[]
        for name in ['Ren_H_Head',f'Ren_DesignerEye_{side}_SkinShutter']:
            obj=bpy.data.objects[name];evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();mesh.calc_loop_triangles();p=coords(mesh.vertices).astype(float);tris=np.array([t.vertices[:] for t in mesh.loop_triangles]);tree=BVHTree.FromPolygons(p,tris,all_triangles=True);surfaces.append((name,tree,p,tris));evaluated.to_mesh_clear()
        def attached_native(pixels):
            native=to_native(pixels);rows=[]
            for i,(pixel,base) in enumerate(zip(pixels,native)):
                hits=[]
                for name,tree,p,tris in surfaces:
                    hit=tree.ray_cast(Vector(base+toward*2),Vector(-toward),4)
                    if hit[0] is not None:hits.append((float(np.dot(hit[0],toward)),name,np.array(hit[0]),int(hit[2]),p[tris[hit[2]]]))
                used=None
                if hits:
                    depth,name,point,face,tri=max(hits,key=lambda h:h[0]);delta=max(0.,depth+.00065-float(base@toward));native[i]=base+toward*delta
                    weights=np.linalg.lstsq(np.vstack([tri.T,np.ones(3)]),np.r_[point,1],rcond=None)[0]
                    used={'mesh':name,'loop_triangle_index':face,'barycentric':weights.tolist(),'source_surface_native':point.tolist(),'camera_facing_offset_native':.00065,'forward_displacement_native':delta}
                rows.append({'source_pixel':np.asarray(pixel).tolist(),'native_position':native[i].tolist(),'attachment':used})
            return native,rows
        def new_layer(label,pixels,faces=None,is_crease=False,notes='Visible designer contour',holes=None):
            pixels=np.asarray(pixels);keep=np.r_[True,np.linalg.norm(np.diff(pixels,axis=0),axis=1)>1e-7];pixels=pixels[keep]
            if np.linalg.norm(pixels[0]-pixels[-1])<1e-7:pixels=pixels[:-1]
            outline=pixels.copy()
            if holes:
                cloud=pixels.copy();edges=[(i,(i+1)%len(pixels)) for i in range(len(pixels))]
                for hole in holes:
                    start=len(cloud);cloud=np.vstack([cloud,hole]);edges.extend([(start+i,start+(i+1)%len(hole)) for i in range(len(hole))])
                cdt=delaunay_2d_cdt([Vector(p) for p in cloud],edges,[],0,1e-6,True);pixels=np.array([p[:] for p in cdt[0]]);outer_region=PolygonRegion(outline);hole_regions=[PolygonRegion(p) for p in holes];faces=[f for f in cdt[2] if outer_region.contains_point(pixels[f].mean(0)) and not any(r.contains_point(pixels[f].mean(0)) for r in hole_regions)]
            native,rows=attached_native(pixels)
            if faces is None:faces=[list(map(int,t)) for t in tessellate_polygon([[Vector((x,y,0)) for x,y in pixels]])]
            cleaned=[]
            for f in faces:
                f=list(f)
                if np.cross(native[f[1]]-native[f[0]],native[f[2]]-native[f[0]])@toward<0:f.reverse()
                cleaned.append(f)
            name=f'Ren_DesignerEye_{side}_{label}';mesh=bpy.data.meshes.new(name);mesh.from_pydata(native.tolist(),[],cleaned);mesh.materials.append(crease_mat if is_crease else ink_mat);mesh.update();obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj.parent=parent;obj.matrix_world=registration
            uv=mesh.uv_layers.new(name='DesignerEyeUV');lo=pixels.min(0);span=np.maximum(np.ptp(pixels,axis=0),1)
            for loop,value in zip(mesh.loops,uv.data):value.uv=(pixels[loop.vertex_index]-lo)/span
            if not is_crease:
                upper=np.vstack([r['sampled_source_pixel_curve'] for r in record['curves'] if r['layer']=='upper aperture']);exterior=np.vstack([r['sampled_source_pixel_curve'] for r in record['curves'] if r['layer']=='upper ink exterior' and r['visibility']=='visible'])
                di=np.linalg.norm(pixels[:,None,:]-upper[None,::3,:],axis=2).min(1);do=np.linalg.norm(pixels[:,None,:]-exterior[None,::3,:],axis=2).min(1);weight=np.clip(di/(di+do+1e-8),0,1)
                colors=linear([.075,.052,.08])*(1-weight[:,None])+linear([.20,.145,.18])*weight[:,None];attr=mesh.color_attributes.new(name='DesignerInkPigment',type='FLOAT_COLOR',domain='POINT')
                for d,color in zip(attr.data,colors):d.color=(*color,1)
            obj['prototype_status']=trial_name.upper()+'_VISIBLE_INK_REFINEMENT_PENDING_REVIEW';attachment.append({'object':name,'note':notes,'vertices':rows});changes.append({'object':name,'vertices':len(native),'triangles':len(cleaned),'note':notes,'source_pixel_outline':outline.tolist(),'source_pixel_negative_spaces':holes or []});mutable.add(name)
            return obj
        for obj in list(scene.objects):
            if obj.name.startswith(f'Ren_DesignerEye_{side}_') and any(s in obj.name for s in ['_UpperInk','_VisibleFan','_Crease']):mutable.add(obj.name);bpy.data.objects.remove(obj,do_unlink=True)
        upper=np.vstack([r['sampled_source_pixel_curve'] for r in record['curves'] if r['layer']=='upper aperture']);outer=np.vstack([r['sampled_source_pixel_curve'] for r in record['curves'] if r['layer']=='upper ink exterior' and r['visibility']=='visible'])
        if side=='L':outer=np.vstack([outer,[[558,206.5],[561.0,207.8],[564.5,207.6],[566.2,207.2],[565.0,211.4],[566.2,215.5],[565.8,220.0],[563.4,225.1],[560.5,224.5]]])
        skin_window=[[564.45,209.75],[565.4,209.1],[565.95,210.45],[565.0,211.65],[564.35,211.0]]
        new_layer('UpperInk',np.vstack([outer,upper[::-1]]),notes='Retains reviewed visible exterior and aperture. L temporal core between visible branches is construction-only; aperture unchanged.',holes=[skin_window] if fan_refine and side=='L' else None)
        for i,r in enumerate([r for r in record['curves'] if r['layer']=='lash fan exterior' and r['visibility']=='visible']):
            gap=[[568.0,216.0],[571.7,215.7],[570.5,216.35],[568.35,217.0]]
            new_layer(f'VisibleFan_{i}',r['source_pixel_landmarks'],holes=[gap] if fan_refine and side=='L' else None,notes='Original visible outer fan endpoints retained; v6 separates the visible thin-stroke gap inside the former filled span.' if fan_refine else 'Visible designer contour')
        if side=='L':
            new_layer('VisibleFan_UpperSpan',[[554.9,207.5],[559.2,208.7],[565.8,208.5],[566.5,206.0],[566.0,209.9],[563.9,212.1],[559.3,209.7]],notes='Visible upper branch retained to source x566.5. Distal junction toward x567.4/y204.2 remains uncertain and is excluded; local closure is construction-only.',holes=[skin_window] if fan_refine else None)
            if fan_refine:
                new_layer('VisibleFan_LowerSpan',[[565.1,219.3],[571.8,220.2],[575.7,220.7],[573.2,221.6],[569.1,221.6],[565.4,221.3]],notes='Upper stroke within formerly merged lower span: same visible top boundary and x575.7 truncation; fine return edge follows visible inter-stroke light interval. Hair-overlapped distal tip remains excluded.')
                new_layer('VisibleFan_LowerFine',[[564.9,221.8],[568.1,222.5],[571.2,223.1],[573.2,224.0],[568.3,224.2],[564.1,222.8]],notes='Separate lower stroke retains the source lower points (573.2,224) and (568.3,224.2); proximal connection under temporal core remains construction-only.')
            else:new_layer('VisibleFan_LowerSpan',[[565.1,219.3],[571.8,220.2],[575.7,220.7],[573.2,223.8],[568.3,224.2],[564.1,222.8]],notes='Visible lower branch to x575.7; farther hair-overlapped x578.5–578.8 endpoint is excluded. Last closure is construction-only.')
        # A separately attached crease remains lighter than the dark moving liner.
        path=np.vstack([r['sampled_source_pixel_curve'] for r in record['curves'] if r['layer']=='crease' and r['visibility']=='visible'])[::2];tangent=np.gradient(path,axis=0);normal=np.column_stack([-tangent[:,1],tangent[:,0]]);normal/=np.linalg.norm(normal,axis=1)[:,None];half=.58*np.sin(np.linspace(0,math.pi,len(path)))**.55
        pixels=np.vstack([path+normal*half[:,None],(path-normal*half[:,None])[::-1]]);new_layer('Crease',pixels,is_crease=True,notes='Original visible crease path; width 1.16 source pixels before taper, attached to actual v4 skin, distinct soft taupe pigment.')
        shutter=bpy.data.objects[f'Ren_DesignerEye_{side}_SkinShutter'];mesh=shutter.data;p=coords(mesh.vertices);screen=project_native(p,parameters);source_attr=mesh.attributes['source_h_boundary_vertex_id'];ids=np.empty(len(p),np.int32);source_attr.data.foreach_get('value',ids);exact=ids>=0
        outer_distance=np.linalg.norm(screen[:,None,:]-outer[None,::3,:],axis=2).min(1);edge_distance=np.linalg.norm(screen[:,None,:]-project_native(boundary[side],parameters)[None,:,:],axis=2).min(1)
        smoky=.76*np.exp(-(outer_distance/5.1)**2)*np.minimum(1,edge_distance/2.0);smoky[exact]=0;mesh.attributes['DesignerSmokyLidWeight'].data.foreach_set('value',smoky.astype(np.float32));m=mesh.materials[0]
        for n in m.node_tree.nodes:
            if n.type=='MIX_RGB':n.inputs[2].default_value=(*linear([.37,.235,.28]),1)
        changes.append({'object':shutter.name,'geometry_unchanged':True,'boundary_pigment_unchanged':True,'maximum_smoky_weight':float(smoky.max()),'boundary_smoky_weight':float(abs(smoky[exact]).max()),'note':'Only existing moving pigment weights and smoky color change; base skin source color attribute unchanged.'})
    after={o.name:fingerprint(o) for o in scene.objects if o.type=='MESH'}
    for name,record in before.items():
        if name not in mutable:assert record==after[name],('Unapproved v4 geometry/UV/morph/material/hierarchy change',name)
    assert sha(source)==source_sha and sha(SOURCE)==SOURCE_SHA
    trial=OUT/trial_name;trial.mkdir(exist_ok=True);output=trial/'Ren_DesignerEyes_Pigment.blend';bpy.ops.wm.save_as_mainfile(filepath=str(output))
    dump(trial_name+'/layer-attachment.json',{'source_v4_sha256':source_sha,'definition':'Attachments preserve the original fixed-camera source pixel. Ink/crease is placed in front of the actual v4 skin only where it would otherwise be occluded. Skin never moves.','layers':attachment})
    dump(trial_name+'/refinement.json',{'status':trial_name.upper()+'_LAYERED_INK_AND_SMOKY_PIGMENT_PENDING_ACTUAL_RENDER_REVIEW','source_v4_sha256':source_sha,'output_sha256':sha(output),'accepted_non_eye_changed':False,'v4_aperture_backing_iris_shutter_geometry_uv_morph_hierarchy_changed':False,'source_camera_changed':False,'unknown_source_hair_junctions_reconstructed_as_known':False,'changes':changes,'artist_likeness_approval':False})
    print('DESIGNER_LAYER_REFINEMENT_SAVED',str(output),flush=True)


if __name__=='__main__':
    if '--inspect-source' in sys.argv:inspect_source()
    elif '--solve-camera' in sys.argv:solve_camera()
    elif '--camera-render' in sys.argv:camera_render()
    elif '--camera-plate' in sys.argv:camera_plate()
    elif '--build-prototype' in sys.argv:build_prototype()
    elif '--visibility-audit' in sys.argv:visibility_audit()
    elif '--render-prototype' in sys.argv:render_prototype()
    elif '--prototype-plate' in sys.argv:prototype_plate()
    elif '--apply-boundary-pigment' in sys.argv:apply_boundary_pigment()
    elif '--layer-review' in sys.argv:layer_review()
    elif '--refine-layers-v5' in sys.argv:refine_layers_v5()
    elif '--distinct-layer-mask' in sys.argv:render_distinct_layer_mask()
    elif '--verify-layer-refinement' in sys.argv:verify_layer_refinement()
    elif '--export-neutral' in sys.argv:export_neutral_candidate()
    elif '--verify-portable-and-profile' in sys.argv:verify_portable_candidate();render_unobscured_profiles()
    elif '--profiles-only' in sys.argv:render_unobscured_profiles()
    elif '--verify-portable-only' in sys.argv:verify_portable_candidate()
    else:raise SystemExit('Select an explicit prototype operation.')
