from pathlib import Path
import bpy,json,hashlib,numpy as np
from mathutils import Vector
P=Path('B:/lucid-loop/art/generated/characters/ren/gaze-final-v1')
bpy.ops.wm.open_mainfile(filepath=str(P/'Ren_LOD0_Gaze.blend'));eye=bpy.data.objects['RenEyesShallow'];basis=eye.data.shape_keys.key_blocks[0]
proof={}
for key in eye.data.shape_keys.key_blocks:
 if not key.name.startswith('gaze'):proof[key.name]=hashlib.sha256(np.array([v.co[:]for v in key.data],dtype=np.float32).tobytes()).hexdigest()
for direction in ['Left','Right','Up','Down']:
 key=eye.shape_key_add(name='gaze'+direction,from_mix=False)
 for i in range(len(basis.data)):key.data[i].co=eye.data.shape_keys.key_blocks['gaze'+direction+'L'].data[i].co+eye.data.shape_keys.key_blocks['gaze'+direction+'R'].data[i].co-basis.data[i].co
manifest=json.loads((P/'gaze-manifest.json').read_text());manifest['standard_controls']=['gazeLeft','gazeRight','gazeUp','gazeDown'];manifest['mixing']='Use either standard binocular controls OR per-eye variants, not both. Sum opposite weights <=1; clamp gaze vector to unit circle, multiply each eye by (1-blink)^2.'
(P/'gaze-manifest.json').write_text(json.dumps(manifest,indent=2));(P/'neutral-existing-shape-sha256.json').write_text(json.dumps(proof,indent=2))
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type in ('MESH','ARMATURE'):o.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Ren_LOD0_Gaze.blend'))
bpy.ops.export_scene.fbx(filepath=str(P/'Ren_LOD0_Gaze.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=False,path_mode='COPY',embed_textures=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
