from pathlib import Path
import bpy,json,numpy as np
from mathutils import Vector
ROOT=Path('B:/lucid-loop');OUT=ROOT/'art/generated/characters/ren/gaze-final-v1';OUT.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/generated/characters/ren/lod0-final-v1/Ren_LOD0.blend'))
eye=bpy.data.objects['RenEyesShallow'];p=np.array([v.co[:]for v in eye.data.vertices]);adj=[[]for _ in p]
for e in eye.data.edges:a,b=e.vertices;adj[a].append(b);adj[b].append(a)
seen=set();comps=[]
for i in range(len(p)):
 if i in seen:continue
 stack=[i];seen.add(i);ids=[]
 while stack:
  a=stack.pop();ids.append(a)
  for b in adj[a]:
   if b not in seen:seen.add(b);stack.append(b)
 comps.append(ids)
image=next(n.image for n in eye.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE'and n.image);w,h=image.size;pix=np.array(image.pixels[:]).reshape(h,w,4)
uv=eye.data.uv_layers.active.data;colors=np.zeros((len(p),3));counts=np.zeros(len(p))
for l in eye.data.loops:
 u=uv[l.index].uv;colors[l.vertex_index]+=pix[int(u.y*h)%h,int(u.x*w)%w,:3];counts[l.vertex_index]+=1
colors/=np.maximum(counts[:,None],1)
report={'components':[{'count':len(ids),'min':p[ids].min(0).tolist(),'max':p[ids].max(0).tolist(),'mean_color':colors[ids].mean(0).tolist()}for ids in comps]}
np.savez(OUT/'eye-audit.npz',positions=p,colors=colors)
(OUT/'eye-audit.json').write_text(json.dumps(report,indent=2));print(report)
scene=bpy.context.scene;scene.render.resolution_x=1000;scene.render.resolution_y=500;scene.camera.data.ortho_scale=.21;scene.camera.location=(0,-2,1.623);scene.camera.rotation_euler=(Vector((0,0,1.623))-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(OUT/'neutral-eyes.png');bpy.ops.render.render(write_still=True)
source_keys={k.name:np.array([v.co[:]for v in k.data])for k in eye.data.shape_keys.key_blocks}
controls=[]
for ids in comps:
 q=p[ids];center=(q.min(0)+q.max(0))/2;side='L'if center[0]>0 else 'R'
 # Front-facing iris-bearing interior; rim and rear vertices are stationary.
 front=np.clip((center[1]-q[:,1])/.007,0,1);rad=np.sqrt(((q[:,0]-center[0])/.016)**2+((q[:,2]-center[2])/.016)**2)
 fade=np.clip((1-rad)/.30,0,1);weight=front*fade
 for direction,dx,dz in [('Left',.0025,0),('Right',-.0025,0),('Up',0,.0018),('Down',0,-.0018)]:
  name='gaze'+direction+side;key=eye.shape_key_add(name=name,from_mix=False)
  for i,wgt in zip(ids,weight):key.data[i].co.x+=dx*wgt;key.data[i].co.z+=dz*wgt
  controls.append(name)
assert all(np.array_equal(source_keys[k.name],np.array([v.co[:]for v in k.data]))for k in eye.data.shape_keys.key_blocks if k.name in source_keys)
for direction in ['Left','Right','Up','Down']:
 key=eye.shape_key_add(name='gaze'+direction,from_mix=False)
 for i in range(len(p)):
  key.data[i].co=eye.data.shape_keys.key_blocks['gaze'+direction+'L'].data[i].co+eye.data.shape_keys.key_blocks['gaze'+direction+'R'].data[i].co-Vector(p[i])
 controls.append(key.name)
report.update({'controls':controls,'neutral_max_delta':0,'existing_shapes_max_delta':0,'uv_changed':False,'topology_changed':False,'range_metres':{'horizontal':.0025,'vertical':.0018},'construction':'Measured interior tangent displacement of original iris-bearing eye surfaces; rear and outer rim fixed. Iris and sclera share original baked texture; no atlas redraw.','runtime':'Normalize opposite gaze weights per eye; suppress gaze by (1-blink)^2 to preserve exact existing closure. L means character left. Horizontal Left is character left (+X).','limitations':['Small bounded shifts, not eyeball rotation or independent iris anatomy.','Sclera pixels within interior transition move slightly with iris; rim, eyelids and lashes remain fixed.']})
for direction in ['Left','Right','Up','Down']:
 for key in eye.data.shape_keys.key_blocks:
  if key.name.startswith('gaze'):key.value=1 if key.name in ['gaze'+direction+'L','gaze'+direction+'R']else 0
 scene.render.filepath=str(OUT/(direction.lower()+'.png'));bpy.ops.render.render(write_still=True)
for o in bpy.context.scene.objects:
 if o.type=='MESH'and o.data.shape_keys:
  for k in o.data.shape_keys.key_blocks:
   if k.name in ['eyeBlinkL','eyeBlinkR']:k.value=.5
   if k.name.startswith('gaze'):k.value=.25 if k.name in ['gazeLeftL','gazeLeftR']else 0
scene.render.filepath=str(OUT/'left-halfblink.png');bpy.ops.render.render(write_still=True)
for o in bpy.context.scene.objects:
 if o.type=='MESH'and o.data.shape_keys:
  for k in o.data.shape_keys.key_blocks:
   if k.name in ['eyeBlinkL','eyeBlinkR']:k.value=1
   if k.name.startswith('gaze'):k.value=0
scene.render.filepath=str(OUT/'left-fullblink.png');bpy.ops.render.render(write_still=True)
for o in bpy.context.scene.objects:
 if o.type=='MESH'and o.data.shape_keys:
  for k in o.data.shape_keys.key_blocks:
   if k.name in ['eyeBlinkL','eyeBlinkR']or k.name.startswith('gaze'):k.value=0
(OUT/'gaze-manifest.json').write_text(json.dumps(report,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ren_LOD0_Gaze.blend'))
bpy.ops.object.select_all(action='DESELECT');eye.select_set(True);bpy.context.view_layer.objects.active=eye
rig=next(m.object for m in eye.modifiers if m.type=='ARMATURE');rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'RenEyesGaze.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
for o in bpy.context.scene.objects:
 if o.type in ('MESH','ARMATURE'):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ren_LOD0_Gaze.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
