"""Export only frozen H support/bridge; invoke --verify in a fresh Blender process."""
import bpy,numpy as np,json,hashlib,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];B=ROOT/'art/generated/characters/ren/parts-workflow-v1';O=B/'h-neutral-context-defect-audit-v1/support-export-v1';S=B/'h-anime-paint-v1/support-boundary-pigment/Ren_H_Support_BoundaryPigment.blend';EXPECTED='7a1e3fe11778aa033969aaaf53988e547e5bc416e0f1d6c8f14628761a852883';NAMES=['Ren_H_ConcealedScalpBack','Ren_H_JawSideBridge_R']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def coords(data):
 a=np.empty(len(data)*3,np.float32);data.foreach_get('co',a);return a.reshape(-1,3)
def world(p,m):return (p@np.asarray(m)[:3,:3].T+np.asarray(m)[:3,3]).astype('<f4')
def save(name,a):
 p=O/name;np.asarray(a).tofile(p);return {'file':name,'sha256':sha(p),'shape':list(a.shape),'dtype':str(a.dtype)}
assert sha(S)==EXPECTED
if '--verify' not in sys.argv:
 O.mkdir(parents=True,exist_ok=True);bpy.ops.wm.open_mainfile(filepath=str(S));rows=[];bpy.ops.object.select_all(action='DESELECT')
 for name in NAMES:
  ob=bpy.data.objects[name];m=ob.data;m.calc_loop_triangles();keys=m.shape_keys.key_blocks if m.shape_keys else [];basis=coords(keys[0].data) if keys else coords(m.vertices);a=m.color_attributes['HSupportSkinLinear'];c=np.empty(len(a.data)*4,np.float32);a.data.foreach_get('color',c);c=c.reshape(-1,4).astype('<f4');assert a.domain=='POINT' and len(c)==len(basis)
  row={'name':name,'vertices':len(basis),'triangles':len(m.loop_triangles),'matrix_world':np.asarray(ob.matrix_world).tolist(),'parent':None,'basis_world':save(name+'-Basis-world.f32',world(basis,ob.matrix_world)),'color_linear_rgba':save(name+'-linear-rgba.f32',c),'morphs':{},'source_defaults':{k.name:k.value for k in keys[1:]},'uv_layers':[u.name for u in m.uv_layers]}
  for k in keys[1:]:row['morphs'][k.name]=save(name+'-'+k.name+'-world.f32',world(coords(k.data),ob.matrix_world));k.value=0
  if keys:m.vertices.foreach_set('co',basis.ravel())
  m.color_attributes.active_color=a;m.color_attributes.render_color_index=m.color_attributes.find(a.name)
  uv=m.uv_layers.active;row['uv_corners']=save(name+'-uv.f32',np.array([x.uv[:] for x in uv.data],dtype='<f4')) if uv else None
  for attrname in ['source_h_vertex_id','head_boundary_vertex_index','support_source_vertex_id']:
   at=m.attributes.get(attrname)
   if at and at.domain=='POINT':row[attrname]=save(name+'-'+attrname+'.i32',np.array([x.value for x in at.data],dtype='<i4'))
  rows.append(row);ob.select_set(True)
 # Calibration markers identify the shared +X front/+Y left/+Z up frame after FBX axis conversion.
 for name,loc in [('RenSupport_FrameOrigin',(0,0,0)),('RenSupport_FrameX',(.1,0,0)),('RenSupport_FrameY',(0,.1,0)),('RenSupport_FrameZ',(0,0,.1))]:
  ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.location=loc;ob.select_set(True)
 bpy.context.view_layer.update();fbx=O/'Ren_H_Support_JawBridge_Linear.fbx';bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',colors_type='LINEAR',prioritize_active_color=True,path_mode='AUTO')
 contract={'status':'EXPORTED_FRESH_IMPORT_PENDING','source':str(S.relative_to(ROOT)),'source_sha256':EXPECTED,'source_unchanged':sha(S)==EXPECTED,'fbx':{'file':fbx.name,'sha256':sha(fbx)},'objects':rows,'triangles':sum(r['triangles'] for r in rows),'frame':'+X front, +Y character left, +Z up; markers at origin and +0.1 along each axis','fbx_axes':{'forward':'-Z','up':'Y'},'canonical_arrays':'Little-endian float32 absolute shared-world positions and linear RGBA. Source vertex row order; use joint Basis/morph/color correspondence, not arbitrary coincident vertex selection.','color_contract':'Exported LINEAR once. Runtime mesh.colors must match canonical linear RGBA; _UseVertexColor=1, _VertexColorSrgb=0, _UseBaseMap=0, white base tint. Do not decode linear colors a second time. Restore on owned import-copy if importer converts or quantizes unexpectedly.','binding':'Replace only old Ren_H_ConcealedScalpBack; append only bridge. Preserve calibrated shared-world alignment under existing Head; do not apply +.06/+.20 twice. Bridge copies effective head jawOpen_A and mouthSeal weights; support is static. Export poses are Basis/zero weights; neutral requires jaw0/mouthSeal1.','limitations':['No new geometry or pigment; previous support/skin join shading and forehead extraction limits remain.','No eyes/hair/cap/head source replacement.','Fresh Blender FBX import is not Unity importer proof; Unity sparse morph deltas and color fidelity need exact canonical checks.']}
 (O/'export-contract.json').write_text(json.dumps(contract,indent=2),encoding='utf-8');print('EXPORT_READY',contract['triangles'],sha(fbx),flush=True)
else:
 c=json.loads((O/'export-contract.json').read_text());bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(O/c['fbx']['file']),colors_type='LINEAR',use_custom_normals=True);report=[]
 from mathutils.kdtree import KDTree
 assert sorted(o.name for o in bpy.data.objects if o.type=='MESH')==sorted(NAMES)
 for row in c['objects']:
  ob=bpy.data.objects[row['name']];m=ob.data;m.calc_loop_triangles();keys=m.shape_keys.key_blocks if m.shape_keys else [];b=world(coords(keys[0].data) if keys else coords(m.vertices),ob.matrix_world);ref=np.fromfile(O/row['basis_world']['file'],dtype='<f4').reshape(-1,3);assert len(b)==len(ref);err=float(np.max(np.linalg.norm(b-ref,axis=1)));assert err<2e-6
  a=m.color_attributes.active_color;cols=np.array([v.color[:] for v in a.data],np.float32);orig=np.fromfile(O/row['color_linear_rgba']['file'],dtype='<f4').reshape(-1,4);expected=orig if a.domain=='POINT' else orig[np.array([l.vertex_index for l in m.loops])];ce=float(np.max(np.abs(cols-expected)));assert ce<2e-6
  morph={}
  for name,entry in row['morphs'].items():
   p=world(coords(keys[name].data),ob.matrix_world);r=np.fromfile(O/entry['file'],dtype='<f4').reshape(-1,3);e=float(np.max(np.linalg.norm(p-r,axis=1)));assert e<2e-6;morph[name]=e
  uvref=np.fromfile(O/row['uv_corners']['file'],dtype='<f4').reshape(-1,2);uv=np.array([x.uv[:] for x in m.uv_layers.active.data],np.float32);ue=float(np.max(np.abs(uv-uvref)));assert ue<2e-6
  assert len(m.loop_triangles)==row['triangles'];report.append({'object':ob.name,'vertices':len(b),'triangles':len(m.loop_triangles),'max_basis_world_error':err,'max_linear_color_error':ce,'import_color_domain':a.domain,'max_uv_error':ue,'morph_endpoint_errors':morph,'matrix_world':np.asarray(ob.matrix_world).tolist()})
 result={'status':'PASS_FRESH_BLENDER_IMPORT','fbx_sha256':sha(O/c['fbx']['file']),'source_unchanged':sha(S)==EXPECTED,'objects':report,'markers':{o.name:list(o.matrix_world.translation) for o in bpy.data.objects if o.type=='EMPTY'},'note':'Imported linear colors with colors_type=LINEAR; this explicit FBX reader choice is not evidence of Unity default conversion.'};(O/'fresh-import-verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result),flush=True)
