"""Isolated directional pigment study; never changes the accepted hair mesh."""
from pathlib import Path
import hashlib,json,os,subprocess,sys

sys.path.insert(0,str(Path(__file__).resolve().parent))
from paint_ren_h_hair_candidate import ROOT,BASE,HAIR,ARTIST,BLENDER,NAMES,sha,dump,append,fingerprint,emission

OUT=BASE/'h-hair-appearance-v2'
if '--coherent-v2' in sys.argv:OUT=OUT/'coherent-v2'
PREVIOUS=BASE/'h-hair-appearance-v1/gutter-safe-v2'
COORDINATE_SOURCE=PREVIOUS/'Ren_H_Hair_MaterialCandidate.blend'
PALETTE={
    'base_srgb':[235,225,208], 'strand_ink_srgb':[136,132,141],
    'strand_light_srgb':[251,244,228], 'root_srgb':[199,187,180],
    'shadow_base_srgb':[186,178,176], 'shadow_ink_srgb':[118,114,129],
    'shadow_light_srgb':[215,204,191], 'shadow_root_srgb':[165,153,158],
    'note':'Authored pale ivory-blond with muted grey-lilac separation strokes after direct artist-portrait comparison. These are proposed pigment swatches, not falsely claimed exact samples. Shadow is an independent cooler pigment family, not the old (.56,.48,.40) multiplier.'}

def linear(rgb):
    return [v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in rgb]

def normals(obj):
    import numpy as np
    return hashlib.sha256(np.asarray([n.vector[:] for n in obj.data.corner_normals],'<f4').tobytes()).hexdigest()

def painter(shadow=False):
    """Finite, differently curved/terminated brush marks in each lock's own frame."""
    import bpy
    mat=bpy.data.materials.new('Ren_H_Hair_Directional_'+('Shadow' if shadow else 'Base')+'_Authoring');mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission');links.new(emit.outputs[0],output.inputs[0])
    a=nodes.new('ShaderNodeAttribute');a.attribute_name='HairClumpPigmentCoordinates'
    split=nodes.new('ShaderNodeSeparateXYZ');links.new(a.outputs['Vector'],split.inputs[0]);t,s,seed=split.outputs
    strength=nodes.new('ShaderNodeAttribute');strength.attribute_name='HairDirectionalPaintStrength'
    def op(operation,*args):
        n=nodes.new('ShaderNodeMath');n.operation=operation
        for i,v in enumerate(args):
            if isinstance(v,(int,float)):n.inputs[i].default_value=v
            else:links.new(v,n.inputs[i])
        return n.outputs[0]
    # Stable integer clump ID. Interpolated float seeds may differ by one ULP
    # across pixels; feeding those directly into WhiteNoise causes dithering.
    seed=op('ROUND',op('MULTIPLY',seed,100000))
    def clamp(x):return op('MINIMUM',op('MAXIMUM',x,0),1)
    def smooth(x):
        x=clamp(x);return op('MULTIPLY',op('MULTIPLY',x,x),op('SUBTRACT',3,op('MULTIPLY',2,x)))
    def random_value(index,slot):
        # White noise is sampled once per clump. No noise evaluated over theta/t/s.
        vector=nodes.new('ShaderNodeCombineXYZ');links.new(seed,vector.inputs[0]);vector.inputs[1].default_value=index+.537;vector.inputs[2].default_value=slot+7.913
        n=nodes.new('ShaderNodeTexWhiteNoise');n.noise_dimensions='3D';links.new(vector.outputs[0],n.inputs['Vector']);return n.outputs['Value']
    def mix(factor,c1,c2):
        n=nodes.new('ShaderNodeMixRGB');n.blend_type='MIX'
        if isinstance(factor,(float,int)):n.inputs[0].default_value=factor
        else:links.new(factor,n.inputs[0])
        for i,value in enumerate((c1,c2),1):
            if isinstance(value,list):n.inputs[i].default_value=(*linear(value),1)
            else:links.new(value,n.inputs[i])
        return n.outputs[0]
    prefix='shadow_' if shadow else ''
    root=op('MULTIPLY',.24,smooth(op('DIVIDE',op('SUBTRACT',.29,t),.29)))
    base=mix(root,PALETTE[prefix+'base_srgb'],PALETTE[prefix+'root_srgb'])
    clump=op('MULTIPLY',.10,random_value(33,1));base=mix(clump,base,PALETTE[prefix+'root_srgb'])
    dark=0;bright=0
    brush_records=[]
    for i in range(10):
        highlight=i>=6;local=i-6 if highlight else i;count=4 if highlight else 6
        # Uneven spacing, substantial seed-specific drift, different start/end,
        # thickness and bow per authored stroke. No wrapping angular coordinate.
        s0=op('ADD',(local+.30)/count,op('MULTIPLY',op('SUBTRACT',random_value(i,0),.5),.11))
        bend=op('MULTIPLY',op('SUBTRACT',random_value(i,1),.5),.24)
        tilt=op('MULTIPLY',op('SUBTRACT',random_value(i,2),.5),.22)
        center=op('ADD',s0,op('ADD',op('MULTIPLY',tilt,op('SUBTRACT',t,.5)),op('MULTIPLY',bend,op('MULTIPLY',t,op('SUBTRACT',1,t)))))
        start=op('MULTIPLY',random_value(i,3),.38)
        end=op('SUBTRACT',.98,op('MULTIPLY',random_value(i,4),.30))
        window=op('MULTIPLY',smooth(op('DIVIDE',op('SUBTRACT',t,start),.15)),smooth(op('DIVIDE',op('SUBTRACT',end,t),.18)))
        # Lines taper as they terminate rather than retaining constant ribbon width.
        width=op('MULTIPLY',op('ADD',.012 if not highlight else .015,op('MULTIPLY',random_value(i,5),.016 if not highlight else .024)),op('ADD',.25,op('MULTIPLY',.75,window)))
        delta=op('DIVIDE',op('SUBTRACT',s,center),width)
        mark=op('EXPONENT',op('MULTIPLY',op('MULTIPLY',delta,delta),-1.8))
        opacity=op('ADD',.25 if not highlight else .35,op('MULTIPLY',random_value(i,6),.40))
        mark=op('MULTIPLY',mark,op('MULTIPLY',window,opacity))
        if highlight:bright=op('MAXIMUM',bright,mark)
        else:dark=op('MAXIMUM',dark,mark)
        brush_records.append({'index':i,'kind':'light' if highlight else 'cool separation','nominal_across':(local+.30)/count})
    dark=op('MULTIPLY',dark,strength.outputs['Fac']);bright=op('MULTIPLY',bright,strength.outputs['Fac'])
    color=mix(dark,base,PALETTE[prefix+'strand_ink_srgb'] if not shadow else PALETTE['shadow_ink_srgb'])
    color=mix(bright,color,PALETTE['shadow_light_srgb' if shadow else 'strand_light_srgb'])
    links.new(color,emit.inputs['Color'])
    return mat,brush_records

def bake(hair,material,name,resolution,background):
    import bpy
    image=bpy.data.images.new(name,width=resolution,height=resolution,alpha=False);image.colorspace_settings.name='sRGB'
    image.generated_color=tuple(v/255 for v in background)+(1,)
    target=material.node_tree.nodes.new('ShaderNodeTexImage');target.image=image;material.node_tree.nodes.active=target
    for obj in hair:obj.data.materials[0]=material
    bpy.ops.object.select_all(action='DESELECT')
    for obj in hair:obj.select_set(True)
    bpy.context.view_layer.objects.active=hair[0]
    scene=bpy.context.scene;scene.render.bake.margin=4;scene.render.bake.use_clear=False;scene.render.bake.use_selected_to_active=False
    bpy.ops.object.bake(type='EMIT')
    path=OUT/(name+'.png');image.filepath_raw=str(path);image.file_format='PNG';image.save();image.pack()
    print('BAKED',path.name,sha(path),flush=True);return image,path

def build():
    import bpy,math
    from mathutils import Vector
    assert sha(HAIR)=='c759804c2826ab04ed29c2cf64456f893b0d2fc9c426782f0368457b52598c97'
    assert sha(COORDINATE_SOURCE)=='56312e215975184d8e81bc74a0528c040825f0ec8f2b6b24a66a003af2de405d'
    sources={str(p.relative_to(ROOT)):sha(p) for p in (HAIR,ARTIST,COORDINATE_SOURCE,Path(__file__).resolve())}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    canonical=append(HAIR,NAMES);expected=[fingerprint(o) for o in canonical];expected_normals={o.name:normals(o) for o in canonical}
    for obj in canonical:bpy.data.objects.remove(obj,do_unlink=True)
    hair=append(COORDINATE_SOURCE,NAMES)
    assert expected==[fingerprint(o) for o in hair]
    for obj in hair:
        assert normals(obj)==expected_normals[obj.name]
        attr=obj.data.attributes.new('HairDirectionalPaintStrength','FLOAT','CORNER')
        for d in attr.data:d.value=1 if obj.name==NAMES[0] else .22
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1;scene.cycles.shading_system=True
    scene.render.threads_mode='FIXED';scene.render.threads=2
    base_author,brushes=painter();shadow_author,_=painter(True)
    base,basepath=bake(hair,base_author,'Ren_H_Hair_DirectionalBlond_Base_4K',4096,PALETTE['base_srgb'])
    shadow,shadowpath=bake(hair,shadow_author,'Ren_H_Hair_DirectionalBlond_Shadow_2K',2048,PALETTE['shadow_base_srgb'])
    candidate=emission('Ren_H_Hair_DirectionalBlond_UnlitReview',image=base)
    shadow_review=emission('Ren_H_Hair_DirectionalBlond_ShadowPigmentReview',image=shadow)
    prior_image=bpy.data.images.load(str(PREVIOUS/'Ren_H_Hair_ClumpPigment_4K.png'));prior_image.colorspace_settings.name='sRGB'
    previous=emission('Ren_H_Hair_PreviousQuiet_UnlitReview',image=prior_image)
    portable=bpy.data.materials.new('Ren_H_Hair_DirectionalBlond');portable.use_nodes=True
    tex=portable.node_tree.nodes.new('ShaderNodeTexImage');tex.image=base
    portable.node_tree.links.new(tex.outputs['Color'],portable.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    portable.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.82
    for obj in hair:obj.data.materials[0]=portable
    library=OUT/'Ren_H_Hair_DirectionalPigment.blend';bpy.data.libraries.write(str(library),set(hair)|{base_author,shadow_author,shadow_review},path_remap='RELATIVE_ALL',fake_user=True,compress=True)
    report={'status':'DIRECTIONAL_HAIR_PIGMENT_CANDIDATE_REQUIRES_ROOT_AND_UNITY_REVIEW','sources':sources,'palette':PALETTE,
        'geometry_uv_shapes':expected,'corner_normal_hashes':expected_normals,'mesh_unchanged':True,'uv_layer':'HairPaintUV_H_v1',
        'strokes':brushes,'authorship':'Finite differently curved/terminated tapered marks in each existing clump coordinate frame. Stable integer clump ID before hashing prevents barycentric float roundoff from dithering marks. 6 separation and4 light accents with clump-specific spacing, bow, tilt, start/end, width and opacity. Root shell accents attenuated. No theta/sine pattern, AO, normals or lighting in pigment.',
        'maps':[{'role':role,'file':str(p.relative_to(ROOT)),'sha256':sha(p),'dimensions':size,'color_space':'sRGB'} for role,p,size in [('base',basepath,[4096,4096]),('shadow',shadowpath,[2048,2048])]],
        'authoring_blend':{'path':str(library.relative_to(ROOT)),'sha256':sha(library)},
        'integration':'MAPS ONLY on existing repaired Unity hair mesh. Base and independent shadow map are a required pair. White material tint, no additional brown shadow multiplier. Preserve controls, shader settings, alpha-free surface, UV0, geometry, normals and capOn. This derivative blend is authoring proof, not a replacement mesh import.'}
    dump(OUT/'candidate-contract.json',report);print('MAP_PAIR_READY',flush=True)
    face=append(BASE/'h-face-fit-v1/native-cleanup-v1/Ren_H_NativeFace_Cleanup.blend',['Ren_Head_H_Native'])[0]
    support=append(BASE/'h-hair-fit-v1/h-support-cap-v2/normal-fix-v1/contact-backing-v1/Ren_H_ConcealedSupport.blend',['Ren_H_ConcealedScalpBack'])[0]
    clay=emission('HairReview_NeutralContext',color=(.42,.42,.42))
    for obj in (face,support):obj.data.materials.clear();obj.data.materials.append(clay)
    cap=append(BASE/'head-accessories-v2/band-clearance-v1/Ren_Head_Accessories.blend',['Ren_Cap'])[0];cap.data.materials.clear();cap.data.materials.append(emission('HairReview_CapContext',color=(.009,.01,.012)))
    scene.world=bpy.data.worlds.new('HairReviewWorld');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.045,.05,.06,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=1
    scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0;scene.view_settings.gamma=1
    scene.render.resolution_x=scene.render.resolution_y=900;scene.render.resolution_percentage=100
    bpy.ops.object.camera_add();camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=1.05
    target=Vector((-.03,0,.02));captures=[]
    # Candidate first, so front/quarter are available for early root review.
    for label,material in [('directional',candidate),('shadow-pigment',shadow_review),('previous-quiet',previous)]:
        for obj in hair:obj.data.materials[0]=material
        for view,angle,on in [('front',0,True),('quarter',-35,True),('profile',-90,True),('cap-off-quarter',-35,False)]:
            for obj in hair:obj.data.shape_keys.key_blocks['capOn'].value=float(on)
            cap.hide_render=not on;angle=math.radians(angle);camera.location=target+Vector((math.cos(angle),math.sin(angle),0))*3
            camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();path=OUT/(label+'--'+view+'.png')
            scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
            captures.append({'file':path.name,'sha256':sha(path),'display':'unlit pigment; shadow view displays independent shadow pigment directly, not simulated Unity lighting','capOn':int(on),'camera':[list(r) for r in camera.matrix_world]})
            dump(OUT/'captures.json',captures);print('RENDERED',path.name,flush=True)
    assert expected==[fingerprint(o) for o in hair]
    for obj in hair:assert normals(obj)==expected_normals[obj.name]
    assert all(sha(ROOT/p)==h for p,h in sources.items());report['captures']=captures;report['frozen_sources_unchanged']=True;dump(OUT/'candidate-contract.json',report)
    print('DIRECTIONAL_PAINT_REVIEW_COMPLETE',flush=True)

def verify():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    report=json.loads((OUT/'candidate-contract.json').read_text());p=ROOT/report['authoring_blend']['path'];assert sha(p)==report['authoring_blend']['sha256']
    hair=append(p,NAMES);assert [fingerprint(o) for o in hair]==report['geometry_uv_shapes']
    for o in hair:assert normals(o)==report['corner_normal_hashes'][o.name] and o.data.shape_keys.key_blocks['capOn'].value==0
    assert all(sha(ROOT/p)==h for p,h in report['sources'].items())
    assert all(sha(ROOT[m['file']])==m['sha256'] for m in report['maps'])
    dump(OUT/'verification.json',{'status':'PASS','geometry_topology_UV_loop_order_Basis_capOn_corner_normals_exact':True,'source_and_output_hashes_verified':True,'authoring_blend_reopened_without_resave':True,'capOn_default_zero':True,'canonical_hair_sha256':sha(HAIR),'authoring_blend_sha256':sha(p)})

def main():
    if '--worker' in sys.argv:build();return
    if '--verify-worker' in sys.argv:verify();return
    OUT.mkdir(parents=True,exist_ok=True);(OUT/'.gitignore').write_text('.blender-user/\n*.log\n*.blend1\n',encoding='utf-8')
    env=os.environ.copy();env['BLENDER_USER_RESOURCES']=str(OUT/'.blender-user');env['OMP_NUM_THREADS']='2'
    with (OUT/('verify.log' if '--verify' in sys.argv else 'build.log')).open('w',encoding='utf-8') as log:
        command=[BLENDER,'--background','--factory-startup','--threads','2','--python-exit-code','1','--python',str(Path(__file__).resolve()),'--','--verify-worker' if '--verify' in sys.argv else '--worker']
        if '--coherent-v2' in sys.argv:command.append('--coherent-v2')
        result=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,env=env,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    print('Blender exit',result.returncode);raise SystemExit(result.returncode)

if __name__=='__main__':main()
