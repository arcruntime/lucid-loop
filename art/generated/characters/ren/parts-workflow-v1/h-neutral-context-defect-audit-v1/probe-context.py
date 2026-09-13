import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=Path('B:/lucid-loop/art/generated/characters/ren/parts-workflow-v1');S=R/'h-designer-eyes-v1/trial-v6/Ren_DesignerEyes_Pigment.blend';O=R/'h-neutral-context-defect-audit-v1';before=hashlib.sha256(S.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(S));sc=bpy.context.scene;dg=bpy.context.evaluated_depsgraph_get();trees=[]
for ob in sc.objects:
 if ob.type!='MESH' or ob.hide_render:continue
 eo=ob.evaluated_get(dg);m=eo.to_mesh();v=[ob.matrix_world@x.co for x in m.vertices];f=[list(p.vertices) for p in m.polygons];a=m.attributes.get('source_h_face_id');ids=[int(x.value) for x in a.data] if a else [];trees.append((ob.name,BVHTree.FromPolygons(v,f),ids));eo.to_mesh_clear()
reg=bpy.data.objects['Ren_H_Head'].matrix_world;target=reg@Vector((0,-.10,.40));cam=sc.camera;cam.data.ortho_scale=.96*reg.to_scale()[0];rows=[]
probes={'front':[('forehead-left',307,192),('forehead-right',519,204),('forehead-mid',424,226),('temple-right',576,286),('jaw-left',311,510),('jaw-right',516,478)],'quarter':[('forehead-mid',484,227),('forehead-right',574,205),('temple-far',593,304),('posterior-jaw-dark',276,507)],'profile':[('rear-support',344,467),('jaw-background',424,471),('ear-support',353,338),('cap-hair-shelf',177,200)]}
for view,di in [('front',(0,-3,0)),('quarter',(-1.3,-2.7,0)),('profile',(-3,-.035,0))]:
 cam.location=target+reg.to_3x3()@Vector(di);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();bpy.context.view_layer.update();dr=cam.matrix_world.to_3x3()@Vector((0,0,-1))
 for label,x,y in probes[view]:
  origin=cam.matrix_world@Vector((((x+.5)/800-.5)*cam.data.ortho_scale,(.5-(y+.5)/800)*cam.data.ortho_scale,0));hits=[]
  for name,bvh,ids in trees:
   pt,n,fi,dist=bvh.ray_cast(origin,dr)
   if pt is not None:hits.append({'object':name,'face':fi,'source_h_face_id':ids[fi] if ids else None,'distance':dist,'world':list(pt)})
  hits.sort(key=lambda a:a['distance']);rows.append({'view':view,'label':label,'pixel':[x,y],'hits':hits[:4]})
(O/'pixel-probes.json').write_text(json.dumps({'source_sha256':before,'source_unchanged':before==hashlib.sha256(S.read_bytes()).hexdigest(),'probes':rows},indent=2));print([(r['label'],r['hits'][0]['object'] if r['hits'] else 'BACKGROUND') for r in rows],flush=True)

materials={}
for name in ["Ren_H_Head","Ren_H_ConcealedScalpBack","Ren_Hair_H_DetailedLayers"]:
 ob=bpy.data.objects[name];materials[name]=[{"name":m.name,"nodes":[{"type":n.type,"name":n.name} for n in m.node_tree.nodes] if m and m.use_nodes else []} for m in ob.data.materials if m]
(O/"material-node-audit.json").write_text(json.dumps(materials,indent=2))
