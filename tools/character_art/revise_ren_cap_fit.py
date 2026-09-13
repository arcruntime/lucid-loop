"""Cap-only V2 direction study on the frozen painted Ren assembly.

Writes only head-accessories-v2. Export and refitted-hair review are explicit
modes following direction review; no source part or Unity asset is modified.
"""
from pathlib import Path
import hashlib
import json
import math
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'art/generated/characters/ren/parts-workflow-v1'
SOURCE=BASE/'complete-head-v1/Ren_CompleteHead_Review.blend'
OUT=BASE/'head-accessories-v2'
sys.path.insert(0,str(Path(__file__).parent))
import build_ren_head_accessories as common
common.OUT=OUT

CENTER=Vector((-.043,0,.218))
TOP=.520
RX=.368
RY=.310


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def base_height(angle):
    return .218+.030*math.cos(angle)+.011*math.sin(angle)


def cap_surface(angle,rho):
    # A lower, slightly offset crown following the original scalp proportions.
    latitude=math.acos(rho)
    top_weight=math.sin(latitude)
    panel=1+.009*math.cos(6*angle+.4)*math.sin(2*latitude)
    panel+=.006*math.sin(3*angle+4*latitude)*math.sin(2*latitude)
    radial=Vector((RX*rho*math.cos(angle)*panel,RY*rho*math.sin(angle)*panel,0))
    x=CENTER.x+radial.x-.016*top_weight**2
    y=radial.y+.006*top_weight**2
    z=base_height(angle)+(TOP-base_height(angle))*top_weight
    z+=.0025*math.sin(3*angle+.5)*math.sin(2*latitude)
    return Vector((x,y,z))


def paint_cloth(mat):
    size=1024
    yy,xx=np.mgrid[0:size,0:size];u=xx/(size-1);v=yy/(size-1)
    grey=np.full((size,size),.068)
    grey+=.001*np.random.default_rng(721).normal(size=(size,size))
    # Broad angular tonal breaks suggest the sheet's drawn fabric planes.
    def triangle(a,b,c,delta):
        x1,y1=a;x2,y2=b;x3,y3=c
        d=(y2-y3)*(x1-x3)+(x3-x2)*(y1-y3)
        a1=((y2-y3)*(u-x3)+(x3-x2)*(v-y3))/d
        a2=((y3-y1)*(u-x3)+(x1-x3)*(v-y3))/d
        mask=(a1>=0)&(a2>=0)&(a1+a2<=1)
        grey[mask]+=delta
    for panel in range(6):
        start=panel/6;w=1/6
        triangle((start+.08*w,.18),(start+.72*w,.69),(start+.53*w,.21),-.012)
        triangle((start+.2*w,.28),(start+.84*w,.52),(start+.63*w,.62),.013)
        triangle((start+.73*w,.17),(start+.91*w,.43),(start+.45*w,.32),.007)
    crown=(v>=.12)&(v<=.76)
    seam=abs(((u*6+.5)%1)-.5)/6
    grey[crown&(seam<.0017)]-=.012
    grey[crown&(seam>.003)&(seam<.004)&((v*115)%1<.6)]+=.012
    grey[v<.11]-=.012
    for row in [.79,.822,.855,.89,.926,.961]:
        grey[abs(v-row)<.001]+=.010
    rgb=np.stack((grey*.96,grey*.96,grey*1.045),axis=-1)
    def stroke(a,b,width):
        ax,ay=a;bx,by=b
        t=np.clip(((u-ax)*(bx-ax)+(v-ay)*(by-ay))/((bx-ax)**2+(by-ay)**2),0,1)
        mask=(u-ax-t*(bx-ax))**2+(v-ay-t*(by-ay))**2<width**2
        rgb[mask]=(.24,.24,.26)
    for a,b in [((.930,.26),(.968,.282)),((.938,.25),(.954,.310)),((.948,.25),(.963,.300)),((.93,.284),(.973,.275))]:stroke(a,b,.0007)
    image=bpy.data.images.new('Ren_Cap_V2_BaseColor',width=size,height=size,alpha=True)
    image.pixels.foreach_set(np.dstack((rgb,np.ones((size,size)))).astype(np.float32).ravel())
    image.filepath_raw=str(OUT/'Ren_Cap_V2_BaseColor.png');image.file_format='PNG';image.save()
    bpy.data.images.remove(image)
    image=bpy.data.images.load(str(OUT/'Ren_Cap_V2_BaseColor.png'),check_existing=False);image.pack()
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
    mat.node_tree.links.new(node.outputs['Color'],mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])


def build_cap():
    cloth=common.material('Ren_Cap_V2_Cloth',(.07,.07,.073),.94)
    cloth.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.05
    paint_cloth(cloth)
    lining=common.material('Ren_Cap_V2_InnerCloth',(.04,.04,.046),.96)
    lining.node_tree.nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value=.04
    b=common.Builder();n=32;rings=10
    for j in range(rings):
        rho=math.cos(j/rings*math.pi/2)
        for i in range(n+1):
            a=2*math.pi*i/n;p=cap_surface(a,rho)
            back=abs((a-math.pi+math.pi)%(2*math.pi)-math.pi)
            if j==0:p.z+=.053*max(0,1-(back/.43)**2)**.5
            b.vertex(p,(i/n,.12+.64*j/rings))
    top=b.vertex(cap_surface(0,0),(.5,.76))
    for j in range(rings-1):
        for i in range(n):
            a=j*(n+1)+i;b.face((a,a+1,a+n+2,a+n+1))
    for i in range(n):b.face(((rings-1)*(n+1)+i,(rings-1)*(n+1)+i+1,top))
    crown_tree=BVHTree.FromPolygons(b.vertices,b.faces)
    # Narrow lining with a reduced upper radius, safely inside the shell.
    start=len(b.vertices)
    for j in range(2):
        for i in range(n+1):
            a=2*math.pi*i/n;p=cap_surface(a,1)
            p.x=CENTER.x+(p.x-CENTER.x)*(.99 if j==0 else .969)
            p.y*=.99 if j==0 else .969;p.z+=(-.004 if j==0 else .017)
            b.vertex(p,(i/n,.025+j*.04))
    for i in range(n):
        if abs(2*math.pi*(i+.5)/n-math.pi)<.43:continue
        a=start+i;b.face((a,a+1,a+n+2,a+n+1),1)
    # Swept-down bill with an asymmetric curve, higher above the eyes.
    start=len(b.vertices);segments=28;rows=4;slab=rows*(segments+1)
    for side in range(2):
        for j in range(rows):
            t=j/(rows-1)
            for i in range(segments+1):
                a=math.radians(-72+144*i/segments);p=cap_surface(a,1)
                p.x+=.25*math.cos(a)*t;p.y+=.024*math.sin(a)*t
                p.z-=.032*t+.070*math.sin(a)**2*t+.006*math.sin(a*2)*t+.0035*side
                b.vertex(p,(i/segments,.78+.19*t))
    for side in range(2):
        for j in range(rows-1):
            for i in range(segments):
                a=start+side*slab+j*(segments+1)+i;b.face((a,a+1,a+segments+2,a+segments+1),side)
    for i in range(segments):
        a=start+(rows-1)*(segments+1)+i;b.face((a,a+1,a+1+slab,a+slab),1)
    for j in range(rows-1):
        for i in (0,segments):
            a=start+j*(segments+1)+i;b.face((a,a+segments+1,a+segments+1+slab,a+slab),1)
    start=len(b.vertices)
    for zoff in (0,.016):
        for i in range(5):
            a=math.pi-.45+.9*i/4;p=cap_surface(a,1);p.x-=.002;p.z+=zoff;b.vertex(p,(i/4,.03))
    for i in range(4):b.face((start+i,start+i+1,start+i+6,start+i+5))
    start=len(b.vertices);center=cap_surface(0,0)
    for j in range(2):
        for i in range(12):
            a=2*math.pi*i/12;b.vertex(center+Vector((.008*math.cos(a),.008*math.sin(a),.0035*j)),(i/12,.05))
    for i in range(12):b.face((start+i,start+(i+1)%12,start+12+(i+1)%12,start+12+i))
    b.face(tuple(start+12+i for i in range(12)))
    cap=b.object('Ren_Cap',[cloth,lining])
    cap['part']='cap';cap['hide_for_face_qa']=True;cap['hide_for_contact_qa']=True
    return cap,crown_tree


def render_direction(hide_hair=False,prefix=None,include_profile=False):
    if hide_hair:
        for obj in bpy.context.scene.objects:
            if obj.name.startswith('Ren_Hair_'):obj.hide_render=True
    scene=bpy.context.scene;scene.cycles.samples=24;scene.cycles.device='CPU'
    scene.render.resolution_x=scene.render.resolution_y=1000
    target=Vector((.03,0,.01))
    views=[('front',(3,0,.05)),('quarter',(2.5,-1.7,.05))]
    if include_profile:views.append(('profile',(0,-3,.05)))
    for name,location in views:
        scene.camera.location=location
        scene.camera.rotation_euler=(target-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera.data.ortho_scale=1.35
        image_prefix=prefix if prefix is not None else ('cap-only-' if hide_hair else '')
        scene.render.filepath=str(OUT/(image_prefix+name+'.png'));bpy.ops.render.render(write_still=True)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    before=sha(SOURCE);bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    bpy.context.preferences.filepaths.save_version=0
    head=bpy.data.objects['Ren_Head'];head_signature=common.signature(head)
    old=bpy.data.objects['Ren_Cap'];bpy.data.objects.remove(old,do_unlink=True)
    cap,tree=build_cap();bpy.context.view_layer.update()
    clearances=[];misses=[]
    for vertex in head.data.vertices:
        p=head.matrix_world@vertex.co
        a=math.atan2(p.y/RY,(p.x-CENTER.x)/RX)
        if p.z<base_height(a)+.014:continue
        d=(p-CENTER).normalized();hit,normal,index,distance=tree.ray_cast(CENTER,d,2)
        if hit is None:misses.append(vertex.index)
        else:clearances.append((distance-(p-CENTER).length,vertex.index))
    clearances.sort()
    cap.data.calc_loop_triangles()
    report={'status':'CAP_ONLY_DIRECTION_STUDY_NO_RUNTIME_EXPORT','source':str(SOURCE.relative_to(ROOT)),'source_sha256':before,'head_unchanged':common.signature(head)==head_signature,'scalp_max_z':max((head.matrix_world@v.co).z for v in head.data.vertices),'cap_max_z':max(v.co.z for v in cap.data.vertices),'cap_base_front_z':base_height(0),'cap_radii_xy':[RX,RY],'cap_center':list(CENTER),'crown_top':TOP,'cap_triangles':len(cap.data.loop_triangles),'sampled_scalp_vertices':len(clearances),'minimum_radial_scalp_clearance':clearances[:10],'missed_cap_rays':misses,'hair_state':'Existing selected-v1 capOn=1 unchanged; requires independent V2 refit after direction review.'}
    assert report['head_unchanged'] and sha(SOURCE)==before
    common.write('direction-study.json',report)
    # Save native candidate early so the hair worker can inspect the cap only.
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_Cap_V2_PaintedStudy.blend'))
    render_direction()
    print('REN_CAP_V2_DIRECTION',json.dumps(report),flush=True)


def export_v2():
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_Cap_V2_PaintedStudy.blend'))
    cap=bpy.data.objects['Ren_Cap'];cap['part']='cap'
    cap['hide_for_face_qa']=True;cap['hide_for_contact_qa']=True
    before=common.signature(cap)
    old_ears={name:common.signature(bpy.data.objects[name]) for name in ('Ren_RightEar_HelixCuffs','Ren_RightEar_DropPiercing')}
    v1=BASE/'head-accessories-v1/Ren_Head_Accessories.blend'
    with bpy.data.libraries.load(str(v1),link=False) as (available,loaded):
        loaded.objects=list(old_ears)
    for source_name,loaded_ear in zip(old_ears,loaded.objects):
        bpy.context.scene.collection.objects.link(loaded_ear)
        bpy.context.view_layer.update()
        assert common.signature(loaded_ear)==old_ears[source_name]
        bpy.data.objects.remove(loaded_ear,do_unlink=True)
    assert common.signature(cap)==before
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_Head_Accessories_Study.blend'))
    common.export_assets(texture_name='Ren_Cap_V2_BaseColor.png')
    report=json.loads((OUT/'export-verification.json').read_text(encoding='utf-8'))
    report['ear_geometry_uv_and_matrices_identical_to_v1']=True
    report['v1_ear_source_sha256']=sha(v1)
    report['painted_context_source_sha256']=sha(SOURCE)
    common.write('export-verification.json',report)
    common.write('fitting-accessories.json',[
        {'path':str((OUT/'Ren_Head_Accessories.blend').relative_to(ROOT)),'objects':['Ren_Cap'],'bind_bone':'Head','default_visible':True,'hide_for_face_qa':True,'hide_for_contact_qa':True},
        {'path':str((OUT/'Ren_Head_Accessories.blend').relative_to(ROOT)),'objects':list(old_ears),'bind_bone':'Head','default_visible':True,'hide_for_face_qa':False,'hide_for_contact_qa':False}
    ])


def correct_band_clearance():
    directory=OUT/'band-clearance-v1';directory.mkdir(parents=True,exist_ok=True)
    input_blend=OUT/'Ren_Head_Accessories_Study.blend'
    hair_report=BASE/'ren-shag-v1/selected-v2/report.json'
    frozen_input=OUT/'band-corridor-input.json'
    if frozen_input.is_file():
        points=json.loads(frozen_input.read_text(encoding='utf-8'))['points']
    else:
        raw=json.loads(hair_report.read_text(encoding='utf-8'))
        points=[row['point'] for part in raw['changes'] for row in part.get('insufficient_cap_head_corridors',[])]
        common.write('band-corridor-input.json',{'hair_report_sha256':sha(hair_report),'points':points})
    assert len(points)==8, 'This bounded correction requires the eight measured corridors.'
    bpy.ops.wm.open_mainfile(filepath=str(input_blend))
    head=bpy.data.objects['Ren_Head'];cap=bpy.data.objects['Ren_Cap']
    frozen={obj.name:common.signature(obj) for obj in bpy.context.scene.objects if obj.type=='MESH' and obj!=cap}
    head_tree=BVHTree.FromPolygons([head.matrix_world@v.co for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    origin=Vector((-.060,0,.20));directions=[(Vector(p)-origin).normalized() for p in points]
    def probe():
        tree=BVHTree.FromPolygons([cap.matrix_world@v.co for v in cap.data.vertices],[list(p.vertices) for p in cap.data.polygons])
        rows=[]
        for point,d in zip(points,directions):
            skin,_,_,hs=head_tree.ray_cast(origin,d,2);cloth,_,face,cs=tree.ray_cast(origin,d,2)
            assert skin is not None and cloth is not None
            rows.append({'point':point,'head_radius':hs,'cap_radius':cs,'clearance':cs-hs,'cap_face':face})
        return rows
    before=probe();changed=[]
    def smooth(x):x=max(0,min(1,x));return x*x*(3-2*x)
    inv=cap.matrix_world.inverted()
    for vertex in cap.data.vertices:
        p=cap.matrix_world@vertex.co;r=p-origin;d=r.normalized()
        angle=math.acos(max(-1,min(1,max(d.dot(target) for target in directions))))
        w=smooth((.32-angle)/(.32-.14))*smooth((.34-p.z)/(.34-.29))*smooth((.50-r.length)/(.50-.445))
        delta=d*(.002*w)
        if delta.length>1e-10:
            old=list(vertex.co);vertex.co=inv@(p+delta)
            changed.append({'vertex':vertex.index,'before':old,'after':list(vertex.co),'displacement':delta.length})
    cap.data.update();bpy.context.view_layer.update()
    after=probe()
    assert max(row['displacement'] for row in changed)<=.002000001
    assert min(row['clearance'] for row in after)>=.006
    assert all(common.signature(bpy.data.objects[name])==sig for name,sig in frozen.items())
    cap.data.calc_loop_triangles()
    report={'status':'MINIMAL_BAND_CLEARANCE_CORRECTION','source_blend':str(input_blend.relative_to(ROOT)),'source_sha256':sha(input_blend),'source_standalone_sha256':sha(OUT/'Ren_Head_Accessories.blend'),'head_and_ears_and_hair_unchanged':True,'cap_triangles':len(cap.data.loop_triangles),'max_vertex_displacement':max(row['displacement'] for row in changed),'changed_vertex_count':len(changed),'before_corridors':before,'after_corridors':after,'changed_vertices':changed,'crown_peak_unchanged':max(v.co.z for v in cap.data.vertices),'hair_contract':'Refit capOn against this derivative; parent V2 files remain unchanged.'}
    report['max_stored_vertex_displacement']=max(math.dist(row['before'],row['after']) for row in changed)
    report['requested_displacement_limit']=.002;report['numeric_tolerance']=1e-7
    assert report['max_stored_vertex_displacement']<=.002+1e-7
    common.OUT=directory
    common.write('band-clearance-report.json',report)
    bpy.ops.wm.save_as_mainfile(filepath=str(directory/'Ren_Head_Accessories_Study.blend'))
    # Keep the exact source texture bytes under the derived export directory.
    (directory/'Ren_Cap_V2_BaseColor.png').write_bytes((OUT/'Ren_Cap_V2_BaseColor.png').read_bytes())
    common.export_assets(texture_name='Ren_Cap_V2_BaseColor.png')
    print('REN_CAP_BAND_CLEARANCE_READY',min(row['clearance'] for row in after),flush=True)


def combine_refitted_hair(path,cap_path=None):
    path=path if path.is_absolute() else ROOT/path
    hair_hash=sha(path)
    cap_path=cap_path or OUT/'Ren_Head_Accessories.blend'
    cap_path=cap_path if cap_path.is_absolute() else ROOT/cap_path
    cap_hash=sha(cap_path)
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_Head_Accessories_Study.blend'))
    accessory_names=('Ren_Cap','Ren_RightEar_HelixCuffs','Ren_RightEar_DropPiercing')
    for name in accessory_names:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
    with bpy.data.libraries.load(str(cap_path),link=False) as (available,loaded):
        loaded.objects=list(accessory_names)
    for obj in loaded.objects:bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.update()
    frozen={obj.name:common.signature(obj) for obj in bpy.context.scene.objects if obj.type=='MESH' and not obj.name.startswith('Ren_Hair_')}
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith('Ren_Hair_'):bpy.data.objects.remove(obj,do_unlink=True)
    with bpy.data.libraries.load(str(path),link=False) as (available,loaded):
        loaded.objects=[name for name in available.objects if name.startswith('Ren_Hair_')]
    hair=[]
    for obj in loaded.objects:
        bpy.context.scene.collection.objects.link(obj);obj.hide_render=False;obj.hide_set(False)
        if obj.data.shape_keys and obj.data.shape_keys.key_blocks.get('capOn'):
            obj.data.shape_keys.key_blocks['capOn'].value=1
        hair.append(obj)
    bpy.context.view_layer.update()
    assert sha(path)==hair_hash
    assert all(common.signature(bpy.data.objects[name])==signature for name,signature in frozen.items())
    blend=OUT/'Ren_Head_Accessories_CombinedV2.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    render_direction(prefix='combined-',include_profile=True)
    assert sha(path)==hair_hash and sha(cap_path)==cap_hash, 'Source changed while rendering; recapture after it freezes.'
    report={'status':'REFITTED_HAIR_COMBINED_REVIEW_CANDIDATE','hair_source':str(path.relative_to(ROOT)),'hair_sha256':hair_hash,'cap_source':str(cap_path.relative_to(ROOT)),'cap_source_sha256':cap_hash,'head_cap_ear_geometry_uv_shapes_and_matrices_unchanged':True,'capOn':1,'combined_blend_sha256':sha(blend),'renders':{view:sha(OUT/('combined-'+view+'.png')) for view in ('front','quarter','profile')},'visual_review':'Pending actual combined render inspection.'}
    common.write('combined-review.json',report)
    print('REN_CAP_V2_COMBINED_READY',flush=True)


if __name__=='__main__':
    if '--correct-band' in sys.argv:correct_band_clearance()
    elif '--hair' in sys.argv:combine_refitted_hair(Path(sys.argv[sys.argv.index('--hair')+1]),Path(sys.argv[sys.argv.index('--cap-source')+1]) if '--cap-source' in sys.argv else None)
    elif '--export' in sys.argv:export_v2()
    elif '--cap-only' in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_Cap_V2_PaintedStudy.blend'))
        render_direction(True)
    else:main()
