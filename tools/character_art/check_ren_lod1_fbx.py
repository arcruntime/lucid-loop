"""Independent FBX reimport and source identity proof for corrected provisional LOD1."""
from pathlib import Path
import bpy,json,hashlib
ROOT=Path('B:/lucid-loop/art/generated/characters/ren');OUT=ROOT/'lod1-final-v1'
def inventory():
 meshes={o.name:{'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'keys':[k.name.rsplit('.',1)[-1]for k in o.data.shape_keys.key_blocks][1:]if o.data.shape_keys else [],'materials':[m.name if m else None for m in o.data.materials],'parent_bone':o.parent_bone,'skin_bones':sorted({o.vertex_groups[g.group].name for v in o.data.vertices for g in v.groups if o.vertex_groups[g.group].name!='LOD1_CollapseInterior'})}for o in bpy.context.scene.objects if o.type=='MESH'and o.name.startswith('Ren')}
 rigs={o.name:{b.name:b.parent.name if b.parent else None for b in o.data.bones}for o in bpy.context.scene.objects if o.type=='ARMATURE'}
 return {'meshes':meshes,'rigs':rigs,'triangles':sum(m['triangles']for m in meshes.values())}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'lod0-final-v1/Ren_LOD0.blend'));source=inventory()
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Ren_LOD1.blend'));expected=inventory()
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(OUT/'Ren_LOD1.fbx'),automatic_bone_orientation=False);actual=inventory()
assert expected['triangles']==13418
assert actual['rigs']==expected['rigs'],'FBX bone hierarchy differs'
checks={}
for name,row in expected['meshes'].items():
 got=actual['meshes'][name]
 assert sorted(got['keys'])==sorted(row['keys']),name+' morphs'
 rig_names={n for rig in expected['rigs'].values()for n in rig}
 assert set(got['skin_bones'])&rig_names==set(row['skin_bones'])&rig_names,name+' skin groups'
 checks[name]={'triangles':got['triangles'],'blend_triangles':row['triangles'],'triangle_delta':got['triangles']-row['triangles'],'shape_count':len(got['keys']),'shape_names':got['keys'],'material_names':got['materials'],'same_material_names_as_lod0':got['materials']==source['meshes'][name]['materials'],'skin_groups_match':True,'parent_bone':got['parent_bone']}
manifest=json.loads((OUT/'manifest.json').read_text());source_path=ROOT/'lod0-final-v1/Ren_LOD0.blend';unchanged=hashlib.sha256(source_path.read_bytes()).hexdigest()==manifest['source_sha256'];assert unchanged
report={'source_lod0_sha256':manifest['source_sha256'],'source_file_unchanged':unchanged,'triangles':actual['triangles'],'rigs':actual['rigs'],'rig_hierarchy_exact':True,'parts':checks,'protected_source_vertices':{p['name']:{'vertices':p['protected_vertices'],'maximum_position_error':p['protected_vertex_max_error']}for p in manifest['parts']if p['protected_vertices']},'budget_met':False,'status':'PROVISIONAL_CORRECTED_LOD1_13418_TRIANGLES'}
(OUT/'fbx-validation.json').write_text(json.dumps(report,indent=2));print('FBX_PROOF_PASS',actual['triangles'],[len(v)for v in actual['rigs'].values()])

