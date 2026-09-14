"""Update the active shared female-v3 canonical rig to authoritative guarded digits."""
import bpy,json,sys,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).resolve().parent))
import standardize_rigs as core
SHARED=ROOT/'art/generated/characters/shared-rigs/female_base_v3';SOURCE=ROOT/'art/generated/characters/ren/guarded-final-v1/RenGuarded_WeightAndDigitCorrection.blend'
old=json.loads((SHARED/'rig.json').read_text());previous=old['rig_identity_sha256']
bpy.ops.wm.open_mainfile(filepath=str(SOURCE));donor=bpy.data.objects['RenFemaleV3'];names=[b.name for b in donor.data.bones if b.name.startswith('LeftHand')and b.name!='LeftHand'];assert len(names)==15
corrections={n:(donor.data.bones[n].matrix_local.copy(),donor.data.bones[n].length)for n in names}
bpy.ops.wm.open_mainfile(filepath=str(SHARED/'female_base_v3.blend'));bpy.context.preferences.filepaths.save_version=0
rig=bpy.data.objects['Armature'];assert len(rig.data.bones)==54
assert not any(o.type=='MESH'for o in bpy.context.scene.objects),'Canonical mesh present: explicit weight transfer required'
before={b.name:b.matrix_local.copy()for b in rig.data.bones};hierarchy={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for name,(matrix,length)in corrections.items():rig.data.edit_bones[name].matrix=matrix;rig.data.edit_bones[name].length=length
bpy.ops.object.mode_set(mode='OBJECT')
error=max(abs(rig.data.bones[n].matrix_local[i][j]-m[i][j])for n,m in before.items()if n not in names for i in range(4)for j in range(4));assert error<2e-6
assert hierarchy=={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
definition=core.definition(rig,'female_base');definition.update(schema='lucid-loop/canonical-rig/v3',version=3,revision='left-digit-articulation-2026-09-15',previous_rig_identity_sha256=previous,status='Ren-approved-core-placement; left-digit-guarded-articulation-corrected; right-digit-weighting-pending',source=old['source'],source_sha256=old['source_sha256'],geometry_policy=old['geometry_policy'],digit_placement_policy='Authoritative guarded correction applied to15leftdigits;24core and15rightdigit rests preserved',digit_correction_source=str(SOURCE.relative_to(ROOT)),digit_correction_source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest())
rig['rig_identity_sha256']=definition['rig_identity_sha256'];rig['canonical_version']=3
(SHARED/'rig.json').write_text(json.dumps(definition,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(SHARED/'female_base_v3.blend'))
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(SHARED/'female_base_v3.fbx'),use_selection=True,object_types={'ARMATURE'},use_mesh_modifiers=False,use_custom_props=True,add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
report={'previous_identity':previous,'new_identity':definition['rig_identity_sha256'],'revised_bones':sorted(names),'preserved_bone_count':39,'preserved_rest_max_matrix_error':error,'canonical_mesh_count':0,'canonical_names_and_hierarchy_unchanged':True,'source_sha256':definition['digit_correction_source_sha256']}
(SHARED/'digit-revision-proof.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
