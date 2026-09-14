from pathlib import Path
import bpy,json,numpy as np,hashlib
P=Path('B:/lucid-loop/art/generated/characters/ren/gaze-final-v1');S=P.parent/'lod0-final-v1/Ren_LOD0.blend'
bpy.ops.wm.open_mainfile(filepath=str(S));o=bpy.data.objects['RenEyesShallow'];old={k.name:hashlib.sha256(np.array([v.co[:]for v in k.data],dtype=np.float32).tobytes()).hexdigest()for k in o.data.shape_keys.key_blocks};new=json.loads((P/'neutral-existing-shape-sha256.json').read_text());assert old==new
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(P/'Ren_LOD0_Gaze.fbx'));meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];n=0
for o in meshes:o.data.calc_loop_triangles();n+=len(o.data.loop_triangles)
eye=bpy.data.objects['RenEyesShallow'];keys=[k.name for k in eye.data.shape_keys.key_blocks];assert len(keys)==48
r={'source_existing_shapes_all_sha256_equal':True,'triangles':n,'meshes':len(meshes),'eye_keys':keys,'bones':len(next(o for o in bpy.context.scene.objects if o.type=='ARMATURE').data.bones),'actions':len(bpy.data.actions)};(P/'export-validation.json').write_text(json.dumps(r,indent=2));print(r)
