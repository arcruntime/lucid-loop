import bpy, json, argparse, sys
from pathlib import Path
from mathutils import Vector, Matrix
AUDIT=Path(__file__).resolve().parent
ROOT=next(p for p in AUDIT.parents if (p/'art/characters').is_dir())
parser=argparse.ArgumentParser(description='Reproduce matched H/P2 clay audit from immutable source assets.')
parser.add_argument('--output-dir', type=Path, default=AUDIT/'reproduced')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=args.output_dir.resolve(); OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'art/generated/characters/ren/bust-comparison-v1/tripo/studio-h3.1-a-open/ren-tripo-studio-h3.1-a-open-original-8k.glb'))
s=bpy.context.scene
s.world=bpy.data.worlds.new('AuditWorld'); s.world.use_nodes=True
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.12,.12,1)
s.world.node_tree.nodes['Background'].inputs[1].default_value=1
mat=bpy.data.materials.new('AuditClay'); mat.use_nodes=True
mat.diffuse_color=(.55,.55,.55,1)
bsdf=mat.node_tree.nodes['Principled BSDF']
bsdf.inputs['Base Color'].default_value=(.55,.55,.55,1)
bsdf.inputs['Metallic'].default_value=0
bsdf.inputs['Roughness'].default_value=.5
bsdf.inputs['IOR'].default_value=1.5
bsdf.inputs['Alpha'].default_value=1
s.view_layers[0].material_override=mat
s.render.resolution_percentage=100; s.render.film_transparent=False
s.render.image_settings.file_format='PNG'; s.render.image_settings.color_mode='RGBA'; s.render.image_settings.color_depth='8'
s.display_settings.display_device='sRGB'
s.view_settings.view_transform='AgX'; s.view_settings.look='None'; s.view_settings.exposure=0; s.view_settings.gamma=1
h=next(o for o in bpy.context.scene.objects if o.type=='MESH'); h.name='Selected_H_ShapeMaster_Audit'
# Single similarity only: source face -Y becomes +X. Visible canthus span 0.344 -> P2 0.360.
scale=.360/.344
h.matrix_world=Matrix(((0,-scale,0,.235-scale*.31),(scale,0,0,scale*.007),(0,0,scale,.085-scale*.548),(0,0,0,1)))
with bpy.data.libraries.load(str(ROOT/'art/generated/characters/ren/parts-workflow-v1/complete-head-v2/Ren_CompleteHead_Review.blend'),link=False) as (a,b): b.objects=['Ren_Head']
p=b.objects[0];bpy.context.collection.objects.link(p)
print('shapes',[(k.name,k.value) for k in p.data.shape_keys.key_blocks])
s=bpy.context.scene
for o in list(s.objects):
 if o.type in ['LIGHT','CAMERA']: bpy.data.objects.remove(o,do_unlink=True)
s.render.engine='CYCLES';s.cycles.samples=12;s.render.resolution_x=s.render.resolution_y=700
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.15,.15,1)
center=Vector((.15,0,-.075))
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.85
for pos,energy,size in [((2,-1,2),100,2),((1,2,.5),50,2)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(pos));l=bpy.context.object;l.data.energy=energy;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
records={}
for state in ['H-selected-open','P2-current-rest','P2-open-A']:
 h.hide_render=not state.startswith('H');p.hide_render=state.startswith('H')
 if state=='P2-open-A':
  for k in p.data.shape_keys.key_blocks:
   if 'mouthSeal' in k.name: k.value=0
   if 'jawOpen_A' in k.name: k.value=1
 records[state]={k.name:k.value for k in p.data.shape_keys.key_blocks if k.value}
 for view,vec in [('front',(3,0,0)),('quarter',(3,-1.9,0)),('profile',(0,-3,0))]:
  cam.location=center+Vector(vec);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(OUT/f'{state}-{view}.png');bpy.ops.render.render(write_still=True)
(OUT/'registration.json').write_text(json.dumps({'uniform_scale':scale,'H_to_P2_matrix':[list(r) for r in h.matrix_world],'landmarks':'Approximate visible outer canthi H X -0.179/+0.165, Z0.548 -> P2 Y +/-0.180, Z0.085. Hair obscures H corners; not a final registration fit. Depth H Y -0.31 -> P2 X0.235, approximate eyelid plane.','shape_states':records},indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'matched-comparison.blend'))
