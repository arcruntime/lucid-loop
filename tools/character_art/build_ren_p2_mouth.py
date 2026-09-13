"""Local P2 Ren oral reconstruction, with immutable-source vertex provenance.

The aperture is traced on native inner-lip edges. Exterior native positions and
loop UVs are retained. This is a limited source-rest/seal/open-A construction
study, not completed language or expression coverage.
"""
from __future__ import annotations

from collections import defaultdict
import hashlib
import heapq
import json
from pathlib import Path
import sys

import numpy as np

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx'
SOURCE_HASH='cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819'
AUDIT=ROOT/'art/generated/characters/ren/parts-workflow-v1/audits/p2-head-native'
OUT=ROOT/'art/generated/characters/ren/parts-workflow-v1/mouth-v1'
if '--variant' in sys.argv:
    tag=sys.argv[sys.argv.index('--variant')+1]
    if not tag or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in tag):
        raise RuntimeError('Variant must be a simple output-directory name')
    OUT=OUT/tag
# Native upper and lower aperture silhouette chains, inspected against the
# original FBX. These are not the four external 44-edge vermilion bands.
UPPER=[104,229,233,234,212,213,268,270,282,285,284,5182,5183,5185,5186,5205,5212,5286,5293,5326,5389,5390,5408]
LOWER=[107,230,239,235,248,272,273,286,288,5187,5188,5190,5207,5284,5311,5323,5404,5388,5407]


def write_json(name,value):
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')


def source_arrays():
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest()!=SOURCE_HASH:
        raise RuntimeError('Immutable native source hash differs')
    data=np.load(AUDIT/'analysis-data/mesh-000.npz')
    labels=np.load(AUDIT/'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    faces=[data['loop_vertex'][s:s+n].tolist() for s,n in zip(data['face_start'],data['face_size'])]
    return data,labels,faces


def trace_aperture():
    data,labels,faces=source_arrays();p=data['positions'];edges=data['edges']
    neighbors=defaultdict(set);edge_ids={}
    for ei,(a,b) in enumerate(edges):
        neighbors[int(a)].add(int(b));neighbors[int(b)].add(int(a));edge_ids[tuple(sorted((int(a),int(b))))]=ei
    def corner_path(start,end):
        center=(p[start]+p[end])*.5
        allowed=set(np.flatnonzero((labels==0)&(abs(p[:,1]-center[1])<.013)&(abs(p[:,2]-center[2])<.009)&(p[:,0]>.260)))
        queue=[(0,start)];dist={start:0};previous={}
        while queue:
            cost,v=heapq.heappop(queue)
            if v==end:break
            if cost!=dist[v]:continue
            for n in neighbors[v]&allowed:
                # Prefer the short front-facing commissure route over the folded
                # posterior pocket while staying on actual native mesh edges.
                new=cost+np.linalg.norm(p[v]-p[n])*(1+max(0,.271-(p[v,0]+p[n,0])*.5)*250)
                if new<dist.get(n,float('inf')):dist[n]=new;previous[n]=v;heapq.heappush(queue,(new,n))
        if end not in dist:raise RuntimeError('No native lip-corner edge path')
        path=[end]
        while path[-1]!=start:path.append(previous[path[-1]])
        return list(reversed(path))
    right=corner_path(UPPER[-1],LOWER[-1]);left=corner_path(LOWER[0],UPPER[0])
    # The front-biased route 85->93 crosses native face 1265 at segment t=.283.
    # Enclose that folded source web using the adjacent native edge route. All
    # retained external vertices stay fixed; removed source faces are recorded.
    left=[107,92,94,84,83,87,93,104]
    rim=UPPER+right[1:]+list(reversed(LOWER))[1:]+left[1:-1]
    if len(set(rim))!=len(rim):raise RuntimeError('Aperture repeats a native vertex')
    boundary={tuple(sorted((a,b))) for a,b in zip(rim,rim[1:]+rim[:1])}
    if any(e not in edge_ids for e in boundary):raise RuntimeError('Aperture contains a non-native edge')
    edge_faces=defaultdict(list)
    for fi,face in enumerate(faces):
        for a,b in zip(face,face[1:]+face[:1]):edge_faces[tuple(sorted((a,b)))].append(fi)
    main_faces={i for i,f in enumerate(faces) if labels[f[0]]==0}
    face_neighbors=defaultdict(set)
    for edge,owners in edge_faces.items():
        if edge in boundary:continue
        for a in owners:
            for b in owners:
                if a!=b:face_neighbors[a].add(b)
    groups=[];unseen=set(main_faces)
    while unseen:
        stack=[min(unseen)];group=set()
        while stack:
            v=stack.pop()
            if v in group:continue
            group.add(v);unseen.discard(v);stack.extend((face_neighbors[v]&main_faces)-group)
        groups.append(sorted(group))
    if len(groups)<2:raise RuntimeError('Rim does not separate exterior/interior')
    external=max(groups,key=len)
    interior=sorted(i for group in groups if group is not external for i in group)
    cut_vertices=np.unique(np.concatenate([faces[i] for i in interior]));bounds=[p[cut_vertices].min(axis=0).tolist(),p[cut_vertices].max(axis=0).tolist()]
    if bounds[0][2]<-.25 or bounds[1][2]>-.04 or bounds[0][0]<0 or bounds[1][0]>.31:
        raise RuntimeError(f'Cut extends outside the actual oral interior: {bounds}')
    report={'source_sha256':SOURCE_HASH,'method':'native inner-lip silhouette chains; right native-edge commissure connection; local left-corner fold enclosure',
            'left_corner_repair':{'reason':'Native edge 85 to 93 crosses source face 1265 at segment t=0.2830188679',
                                 'additional_source_faces_removed_vs_first_trace':[8,1263,1264,1265,1272],
                                 'retained_native_positions_changed':False},
            'upper_native_vertex_ids':UPPER,'lower_native_vertex_ids':LOWER,'right_corner_vertex_ids':right,'left_corner_vertex_ids':left,
            'rim_native_vertex_ids':rim,'rim_native_edge_ids':[edge_ids[e] for e in sorted(boundary)],
            'rim_positions_world':p[rim].tolist(),'interior_source_face_ids':interior,
            'exterior_main_source_face_count':len(external),'interior_bounds_world':bounds,
            'interior_source_face_count':len(interior),'interior_face_connected_group_sizes':[len(g) for g in groups if g is not external],
            'outer_native_lip_loops_used_as_inner_rim':False}
    write_json('aperture-trace.json',report)
    print('REN_P2_APERTURE_TRACED',json.dumps({k:v for k,v in report.items() if k not in ('rim_native_edge_ids','rim_positions_world','interior_source_face_ids')}),flush=True)
    return report


def prepare_shapes():
    """Biharmonic extension of rim motion over local retained native skin only."""
    from scipy.sparse import coo_matrix,eye
    from scipy.sparse.csgraph import dijkstra
    from scipy.sparse.linalg import spsolve
    trace=json.loads((OUT/'aperture-trace.json').read_text());data,labels,faces=source_arrays();p=data['positions']
    removed=set(trace['interior_source_face_ids'])|{i for i,f in enumerate(faces) if labels[f[0]]==4}
    kept=[f for i,f in enumerate(faces) if i not in removed]
    edges=np.array(sorted({tuple(sorted((a,b))) for f in kept for a,b in zip(f,f[1:]+f[:1])}))
    lengths=np.linalg.norm(p[edges[:,0]]-p[edges[:,1]],axis=1)
    ii=np.r_[edges[:,0],edges[:,1]];jj=np.r_[edges[:,1],edges[:,0]]
    graph=coo_matrix((np.r_[lengths,lengths],(ii,jj)),shape=(len(p),len(p))).tocsr()
    rim=np.asarray(trace['rim_native_vertex_ids']);distance=dijkstra(graph,directed=False,indices=rim,min_only=True)
    inv=1/np.maximum(lengths,.00015)
    adjacency=coo_matrix((np.r_[inv,inv],(ii,jj)),shape=graph.shape).tocsr()
    rowsum=np.asarray(adjacency.sum(axis=1)).ravel()
    lap=eye(len(p),format='csr')-adjacency.multiply((1/np.maximum(rowsum,1))[:,None])
    active=np.flatnonzero((distance<.080)&~np.isin(np.arange(len(p)),rim))
    free=lap[:,active].tocsr();system=(free.T@free+eye(len(active),format='csr')*1e-6).tocsc()
    side=np.ones(len(rim))
    # The furthest lateral native corner vertices define the two arc splits.
    left=int(np.argmin(p[rim,1]));right=int(np.argmax(p[rim,1]))
    lower_indices=[];i=right
    while i!=left:lower_indices.append(i);i=(i+1)%len(rim)
    side[lower_indices]=-1
    lateral=p[rim,1];width=max(abs(lateral.min()),abs(lateral.max()))
    t=np.clip(lateral/width,-1,1);across=np.maximum(0,1-t*t)**.65
    targets={}
    # Both arcs seal against one shared shallow curve. A 0.0006-unit wet-line
    # overlap prevents numerical slivers between differently sampled native arcs.
    seal=p[rim].copy();seal[:,0]=.301-.033*np.abs(t)**1.7
    seal[:,2]=-.1455-.009*np.abs(t)**1.5-.0005*t-side*.0003*across
    # Corner motion is intentionally small; retaining their actual Y ordering
    # avoids narrowing or stretching the designer's mouth placement.
    targets['mouthSeal']=seal-p[rim]
    opened=np.zeros((len(rim),3));opened[:,0]=np.where(side<0,-.0025,0)*across
    opened[:,2]=np.where(side<0,-.014,.0018)*across
    targets['jawOpen_A']=opened
    arrays={'rim_ids':rim,'rim_side':side,'native_skin_distance_to_rim':distance}
    counts={}
    for name,delta in targets.items():
        full=np.zeros_like(p);full[rim]=delta
        full[active]=spsolve(system,-free.T@(lap[:,rim]@delta))
        if np.max(np.linalg.norm(full,axis=1))>.060:raise RuntimeError('Unexpectedly large local lip deformation')
        arrays[name]=full
        moved=np.flatnonzero(np.linalg.norm(full,axis=1)>1e-9)
        counts[name]={'moving_native_vertices':len(moved),'max_delta_source_units':float(np.linalg.norm(full,axis=1).max()),
                      'moved_native_bounds':[p[moved].min(axis=0).tolist(),p[moved].max(axis=0).tolist()]}
    np.savez(OUT/'native-mouth-shapes.npz',**arrays)
    write_json('shape-preparation.json',{'method':'biharmonic local native-skin extension with exact traced aperture Dirichlet targets',
                                      'support_geodesic_radius_source_units':.080,'shape_names':list(targets),
                                      'native_shape_summary':counts,'declared_wet_line_seal_overlap_source_units':.0006})
    print('REN_P2_LOCAL_SHAPES_PREPARED',counts,flush=True)


def make_material(name,color,roughness=.6):
    import bpy
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    shader=mat.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Roughness'].default_value=roughness
    return mat


def provenance_attribute(mesh,name,domain,values):
    attr=mesh.attributes.new(name=name,type='INT',domain=domain);attr.data.foreach_set('value',np.asarray(values,dtype=np.int32))


def new_oral_object(name,vertices,faces,material,pose_offsets):
    import bpy
    import bmesh
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.materials.append(material)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for p in mesh.polygons:p.use_smooth=True
    provenance_attribute(mesh,'source_vertex_id','POINT',np.full(len(vertices),-1))
    provenance_attribute(mesh,'source_face_id','FACE',np.full(len(faces),-1))
    obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj)
    obj.shape_key_add(name='Basis')
    for pose,offset in pose_offsets.items():
        key=obj.shape_key_add(name=pose)
        delta=np.asarray(offset)
        if delta.shape==(3,):delta=np.tile(delta,(len(vertices),1))
        for v,position in zip(key.data,np.asarray(vertices)+delta):v.co=position
    return obj


def dental_arch(name,front_x,root_z,edge_z,material,jaw_motion):
    vertices=[];faces=[];samples=25
    for i,y in enumerate(np.linspace(-.059,.059,samples)):
        t=y/.059;x=front_x-.043*abs(t)**1.75
        low,high=sorted((root_z,edge_z));bevel=.0014
        contour=[(x-.004,low+bevel),(x-.0025,low),(x+.001,low),(x+.0024,low+bevel),
                 (x+.0024,high-bevel),(x+.001,high),(x-.0025,high),(x-.004,high-bevel)]
        vertices.extend((px,y,pz+.002*t*t) for px,pz in contour)
        if i:
            for j in range(8):faces.append(((i-1)*8+j,(i-1)*8+(j+1)%8,i*8+(j+1)%8,i*8+j))
    faces.extend([tuple(reversed(range(8))),tuple(range((samples-1)*8,samples*8))])
    return new_oral_object(name,vertices,faces,material,{'mouthSeal':(0,0,0),'jawOpen_A':jaw_motion})


def tongue_surface(material):
    vertices=[];faces=[];rows=15;columns=13
    for side in (1,-1):
        for i,t in enumerate(np.linspace(0,1,rows)):
            x=.117+.140*t;width=.006+.038*np.sin(np.pi*t)**.65
            for u in np.linspace(-1,1,columns):
                top=-.163+.006*np.sin(np.pi*t)-.003*u*u
                z=top if side>0 else top-.013*np.sqrt(max(0,1-u*u))-.001
                vertices.append((x,width*u,z))
    sheet=rows*columns
    for side in range(2):
        for i in range(rows-1):
            for j in range(columns-1):
                a=side*sheet+i*columns+j;f=(a,a+1,a+columns+1,a+columns)
                faces.append(f if side==0 else tuple(reversed(f)))
    perimeter=list(range(columns))+[i*columns+columns-1 for i in range(1,rows)]+list(range(sheet-2,sheet-columns-1,-1))+[i*columns for i in range(rows-2,0,-1)]
    for a,b in zip(perimeter,perimeter[1:]+perimeter[:1]):faces.append((a,b,b+sheet,a+sheet))
    return new_oral_object('Ren_P2_Tongue',vertices,faces,material,{'mouthSeal':(0,0,0),'jawOpen_A':(-.001,0,-.004)})


def build():
    import bpy
    from mathutils import Vector
    obj=import_source();old=obj.data;data,labels,source_faces=source_arrays()
    trace=json.loads((OUT/'aperture-trace.json').read_text());shapes=np.load(OUT/'native-mouth-shapes.npz')
    world=np.array([obj.matrix_world@v.co for v in old.vertices]);local=np.array([v.co[:] for v in old.vertices])
    if np.max(np.abs(world-data['positions']))>1e-7:raise RuntimeError('Native source ID ordering changed on FBX import')
    rim=trace['rim_native_vertex_ids'];removed_faces=set(trace['interior_source_face_ids'])|{i for i,f in enumerate(source_faces) if labels[f[0]]==4}
    face_ids=[i for i in range(len(source_faces)) if i not in removed_faces]
    kept_ids=sorted({v for i in face_ids for v in source_faces[i]});old_to_new={v:i for i,v in enumerate(kept_ids)}
    vertices=local[kept_ids].tolist();vertex_ids=list(kept_ids)
    polygons=[[old_to_new[v] for v in source_faces[i]] for i in face_ids]
    materials=[old.polygons[i].material_index for i in face_ids]
    uv_arrays={layer.name:[list(layer.data[l].uv) for i in face_ids for l in old.polygons[i].loop_indices] for layer in old.uv_layers}
    source_normals=[tuple(old.corner_normals[l].vector) for i in face_ids for l in old.polygons[i].loop_indices]
    native_loop_count=len(source_normals)
    cavity=make_material('Ren_Oral_Cavity',(.031,.006,.010),.82)
    teeth=make_material('Ren_Anime_Teeth',(.68,.62,.50),.55)
    tongue_mat=make_material('Ren_Tongue',(.32,.070,.09),.67)
    inv=obj.matrix_world.inverted();rim_world=world[rim]
    lengths=np.linalg.norm(np.roll(rim_world,-1,axis=0)-rim_world,axis=1)
    # Start each half-ellipse at the actual lateral commissure, not at the first
    # upper chain sample. Native edge sampling is asymmetric around the corners.
    left=int(np.argmin(rim_world[:,1]));right=int(np.argmax(rim_world[:,1]));phase=np.zeros(len(rim))
    for start,end,angle,direction in ((left,right,np.pi,-1),(right,left,0,-1)):
        indices=[];i=start
        while i!=end:indices.append(i);i=(i+1)%len(rim)
        total=sum(lengths[indices]);distance=0.
        for i in indices:phase[i]=angle+direction*np.pi*distance/total;distance+=lengths[i]
    source_side=shapes['rim_side'];rim_layers=[[old_to_new[v] for v in rim]]
    new_vertex_pose={name:[] for name in ('mouthSeal','jawOpen_A')}
    for layer in (1,2,3):
        points=[]
        for j,point in enumerate(rim_world):
            sin=float(np.sin(phase[j]));cos=float(np.cos(phase[j]))
            if layer==1:
                q=point.copy();q[0]-=.011
                q[2]+= (.025 if source_side[j]>0 else -.038)*abs(sin)
                q[1]=.70*q[1]+.30*.091*cos
            else:
                q=np.array((.213 if layer==2 else .115,(.091 if layer==2 else .067)*cos,
                            -.155+(.078 if sin>=0 else .084)*sin))
            points.append(q)
        indices=[]
        for j,q in enumerate(points):
            indices.append(len(vertices));vertices.append(list(inv@Vector(q)));vertex_ids.append(-1)
            for name in new_vertex_pose:
                follow=(.15 if layer==1 else 0) if name=='mouthSeal' else (.92 if layer==1 else .45 if layer==2 else .08)
                new_vertex_pose[name].append(shapes[name][rim[j]]*follow)
        rim_layers.append(indices)
    # New cavity shares the exact original rim vertices, closing the removed
    # interior against native exterior edges with no overlapping surface copy.
    boundary_direction={}
    rim_edges={tuple(sorted((a,b))) for a,b in zip(rim,rim[1:]+rim[:1])}
    for i in face_ids:
        f=source_faces[i]
        for a,b in zip(f,f[1:]+f[:1]):
            if tuple(sorted((a,b))) in rim_edges:boundary_direction[tuple(sorted((a,b)))]=(a,b)
    forward=sum(boundary_direction[tuple(sorted((a,b)))]==(a,b) for a,b in zip(rim,rim[1:]+rim[:1]))
    if forward not in (0,len(rim)):raise RuntimeError('Native aperture has inconsistent exterior face orientation')
    for layer in range(3):
        for j in range(len(rim)):
            k=(j+1)%len(rim);f=[rim_layers[layer][k],rim_layers[layer][j],rim_layers[layer+1][j],rim_layers[layer+1][k]]
            if not forward:f.reverse()
            polygons.append(f);face_ids.append(-1);materials.append(len(old.materials))
            for name in uv_arrays:uv_arrays[name].extend([(k/len(rim),layer/3),(j/len(rim),layer/3),(j/len(rim),(layer+1)/3),(k/len(rim),(layer+1)/3)])
    center=np.mean(np.array([vertices[v] for v in rim_layers[-1]]),axis=0)
    center_id=len(vertices);vertices.append(center.tolist());vertex_ids.append(-1)
    for name in new_vertex_pose:new_vertex_pose[name].append(np.mean(new_vertex_pose[name][-len(rim):],axis=0))
    for j in range(len(rim)):
        k=(j+1)%len(rim);f=[rim_layers[-1][k],rim_layers[-1][j],center_id]
        if not forward:f.reverse()
        polygons.append(f);face_ids.append(-1);materials.append(len(old.materials))
        for name in uv_arrays:uv_arrays[name].extend([(k/len(rim),1),(j/len(rim),1),(.5,.5)])
    mesh=bpy.data.meshes.new('Ren_P2_SourceExterior_CleanCavity');mesh.from_pydata(vertices,[],polygons)
    for mat in old.materials:mesh.materials.append(mat)
    mesh.materials.append(cavity)
    for poly,mat in zip(mesh.polygons,materials):poly.material_index=mat;poly.use_smooth=True
    for name,uv in uv_arrays.items():
        layer=mesh.uv_layers.new(name=name);layer.data.foreach_set('uv',np.asarray(uv,dtype=np.float32).ravel())
    normals=[tuple(n.vector) for n in mesh.corner_normals];normals[:native_loop_count]=source_normals
    mesh.normals_split_custom_set(normals)
    provenance_attribute(mesh,'source_vertex_id','POINT',vertex_ids)
    provenance_attribute(mesh,'source_face_id','FACE',face_ids)
    obj.data=mesh;obj.shape_key_add(name='Basis')
    pose_changes={}
    for name in new_vertex_pose:
        key=obj.shape_key_add(name=name);full=np.concatenate([shapes[name][kept_ids],np.asarray(new_vertex_pose[name])])
        # Offset vectors are in world coordinates; source object transform is
        # retained instead of silently applying it to every unaffected vertex.
        for v,basis,delta in zip(key.data,mesh.vertices,full):v.co=basis.co+inv.to_3x3()@Vector(delta)
        moved=np.flatnonzero(np.linalg.norm(shapes[name],axis=1)>1e-9)
        pose_changes[name]=[{'source_vertex_id':int(i),'delta_world':shapes[name][i].tolist()} for i in moved if i in old_to_new]
    dental_arch('Ren_P2_UpperTeeth',.279,-.128,-.143,teeth,(0,0,0))
    dental_arch('Ren_P2_LowerTeeth',.271,-.190,-.170,teeth,(-.002,0,-.013))
    tongue_surface(tongue_mat)
    # Check every retained source corner, not just a sample or a position hash.
    max_position_error=max(float((mesh.vertices[old_to_new[i]].co-old.vertices[i].co).length) for i in kept_ids)
    uv_error=0;faces_exact=True
    for poly,source_id in zip(mesh.polygons,face_ids):
        if source_id<0:continue
        faces_exact &= [vertex_ids[v] for v in poly.vertices]==source_faces[source_id]
        for layer in old.uv_layers:
            before=np.array([layer.data[v].uv[:] for v in old.polygons[source_id].loop_indices])
            after=np.array([mesh.uv_layers[layer.name].data[v].uv[:] for v in poly.loop_indices])
            uv_error=max(uv_error,float(np.max(abs(before-after))))
    if max_position_error or uv_error or not faces_exact:raise RuntimeError('Retained native source surface/UV changed')
    removed_ids=sorted(set(range(len(old.vertices)))-set(kept_ids))
    report={'source':str(SOURCE),'source_sha256':SOURCE_HASH,'source_object_name':obj.name,
            'source_vertex_count':len(old.vertices),'source_face_count':len(old.polygons),
            'retained_source_vertex_ids':kept_ids,'removed_source_vertex_ids':removed_ids,
            'removed_source_face_ids':sorted(removed_faces),'retained_source_face_ids':[i for i in face_ids if i>=0],
            'intentionally_changed_native_positions_basis':[],'native_shape_deltas':pose_changes,
            'aperture_source_vertex_ids':rim,'aperture_derived_vertex_ids':rim_layers[0],
            'new_cavity_vertex_count':len(vertex_ids)-len(kept_ids),'new_cavity_face_count':sum(i<0 for i in face_ids),
            'retained_native_local_position_max_error':max_position_error,'retained_native_loop_uv_max_error':uv_error,
            'retained_native_faces_source_id_exact':bool(faces_exact),'basis_preserves_native_open_rest':True,
            'shapes':['mouthSeal','jawOpen_A'],'limitations':['Only local oral reconstruction and two prototype poses','Final likeness, oral clearance and pose review still required']}
    bpy.context.preferences.filepaths.save_version=0
    output=OUT/'Ren_P2_Oral_Reconstruction.blend';bpy.ops.wm.save_as_mainfile(filepath=str(output))
    report['derived_blend_sha256']=hashlib.sha256(output.read_bytes()).hexdigest()
    write_json('source-id-patch.json',report)
    print('REN_P2_ORAL_RECONSTRUCTION_SAVED',json.dumps({k:v for k,v in report.items() if k not in ('retained_source_vertex_ids','removed_source_vertex_ids','removed_source_face_ids','retained_source_face_ids','native_shape_deltas','aperture_source_vertex_ids','aperture_derived_vertex_ids')}),flush=True)


def import_source():
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if hashlib.sha256(SOURCE.read_bytes()).hexdigest()!=SOURCE_HASH:raise RuntimeError('Source hash differs')
    bpy.ops.import_scene.fbx(filepath=str(SOURCE),use_anim=False,automatic_bone_orientation=False)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if len(objects)!=1:raise RuntimeError('Native head object count changed')
    return objects[0]


def setup_review():
    import bpy
    from mathutils import Vector
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.seed=17
    scene.render.resolution_x=scene.render.resolution_y=768;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.view_settings.look='None'
    world=bpy.data.worlds.new('MOUTH_REVIEW_WORLD');scene.world=world;world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.055,.065,.08,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
    def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    for name,position,power,size in [('Key',(2,-3,3),150,2.5),('Fill',(3,2,.5),90,2.5)]:
        light=bpy.data.lights.new('MOUTH_REVIEW_'+name,'AREA');light.energy=power;light.size=size
        o=bpy.data.objects.new(light.name,light);scene.collection.objects.link(o);o.location=position;aim(o,(.1,0,0))
    camera_data=bpy.data.cameras.new('MOUTH_REVIEW_CAMERA');camera_data.type='ORTHO';camera_data.clip_start=.001;camera_data.clip_end=20
    camera=bpy.data.objects.new(camera_data.name,camera_data);scene.collection.objects.link(camera);scene.camera=camera
    return scene,camera,aim


def render_trace():
    import bpy
    obj=import_source();trace=json.loads((OUT/'aperture-trace.json').read_text());scene,camera,aim=setup_review()
    curve=bpy.data.curves.new('ACTUAL_NATIVE_APERTURE_RIM','CURVE');curve.dimensions='3D';curve.bevel_depth=.0007
    poly=curve.splines.new('POLY');poly.points.add(len(trace['rim_native_vertex_ids'])-1);poly.use_cyclic_u=True
    for point,index in zip(poly.points,trace['rim_native_vertex_ids']):
        q=obj.matrix_world@obj.data.vertices[index].co;q.x+=.0006;point.co=(*q,1)
    material=bpy.data.materials.new('RimDiagnostic');material.use_nodes=True
    tree=material.node_tree;emission=tree.nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=(1,.1,.015,1)
    tree.links.new(emission.outputs[0],tree.nodes.get('Material Output').inputs['Surface']);curve.materials.append(material)
    guide=bpy.data.objects.new(curve.name,curve);scene.collection.objects.link(guide)
    camera.data.ortho_scale=.31;target=(.23,0,-.142)
    for name,location in [('front',(2,0,-.142)),('three-quarter',(1.6,-1,-.142))]:
        camera.location=location;aim(camera,target);scene.render.filepath=str(OUT/f'aperture-trace-{name}.png');bpy.ops.render.render(write_still=True)
    print('REN_P2_APERTURE_TRACE_RENDERED',OUT,flush=True)


def render_reviews(quick=False):
    import bpy
    from mathutils import Vector
    blend=OUT/'Ren_P2_Oral_Reconstruction.blend'
    captures=[]
    for source in ((False,) if quick else (True,False)):
        if source:import_source()
        else:bpy.ops.wm.open_mainfile(filepath=str(blend))
        scene,camera,aim=setup_review()
        if quick:scene.render.resolution_x=scene.render.resolution_y=512;scene.cycles.samples=8
        else:
            try:scene.render.engine='BLENDER_EEVEE_NEXT'
            except TypeError:scene.render.engine='BLENDER_EEVEE'
        poses=[('native-rest',{})] if source else [('native-rest',{}),('seal-half',{'mouthSeal':.5}),('sealed',{'mouthSeal':1}),('open-half',{'jawOpen_A':.5}),('open-A',{'jawOpen_A':1})]
        for label,values in poses:
            for obj in scene.objects:
                if obj.type=='MESH' and obj.data.shape_keys:
                    for key in obj.data.shape_keys.key_blocks:
                        if key.name!='Basis':key.value=values.get(key.name,0)
            bpy.context.view_layer.update()
            views=[('front',(1,0,0))] if quick else [('front',(1,0,0)),('three-quarter',(.82,-.57,0)),('profile',(0,-1,0))]
            for crop,target,scale in ([('mouth',(.23,0,-.143),.31)] if quick else [('face',(0,0,0),1.10),('mouth',(.23,0,-.143),.31)]):
                for view,direction in views:
                    camera.data.ortho_scale=scale;camera.location=Vector(target)+Vector(direction)*2;aim(camera,target)
                    directory=OUT/('preview' if quick else 'renders')/('source' if source else label);directory.mkdir(parents=True,exist_ok=True)
                    path=directory/f'{crop}-{view}.png';scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
                    captures.append({'path':str(path.relative_to(OUT)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'pose':values,'source':source})
    write_json('preview-renders.json' if quick else 'review-renders.json',captures)
    print('REN_P2_ORAL_REVIEW_RENDERED',len(captures),OUT,flush=True)


def triangle_intersection_points(a,b):
    """Narrow-phase segment/triangle contacts; excludes parallel cases."""
    points=[]
    for first,second in ((a,b),(b,a)):
        edge1=second[1]-second[0];edge2=second[2]-second[0]
        for start,end in zip(first,np.roll(first,-1,axis=0)):
            direction=end-start;h=np.cross(direction,edge2);det=np.dot(edge1,h)
            if abs(det)<1e-13:continue
            s=start-second[0];u=np.dot(s,h)/det
            if u < -1e-7 or u > 1+1e-7:continue
            q=np.cross(s,edge1);v=np.dot(direction,q)/det;t=np.dot(edge2,q)/det
            if v>=-1e-7 and u+v<=1+1e-7 and 1e-6<t<1-1e-6:points.append(start+t*direction)
    return points


def triangle_intersects(a,b):
    return bool(triangle_intersection_points(a,b))


def verify():
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    path=OUT/'Ren_P2_Oral_Reconstruction.blend';bpy.ops.wm.open_mainfile(filepath=str(path))
    patch=json.loads((OUT/'source-id-patch.json').read_text());data,_,native_faces=source_arrays();expected=np.load(OUT/'native-mouth-shapes.npz')
    head=bpy.data.objects[patch['source_object_name']]
    ids=np.array([a.value for a in head.data.attributes['source_vertex_id'].data]);valid=ids>=0
    native_world=np.array([head.matrix_world@v.co for v in head.data.vertices])[valid]
    err=float(abs(native_world-data['positions'][ids[valid]]).max())
    if err>1e-7 or len(set(ids[valid]))!=valid.sum():raise RuntimeError('Saved native positions or source IDs invalid')
    checks={'source_sha256':SOURCE_HASH,'derived_blend_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'reopened_native_world_max_error':err,'unique_retained_native_source_ids':int(valid.sum()),
            'shape_native_delta_error':{},'poses':[]}
    face_ids=np.array([a.value for a in head.data.attributes['source_face_id'].data])
    uv_error=0.;face_errors=[];edge_owners=defaultdict(list)
    for poly in head.data.polygons:
        source_id=int(face_ids[poly.index])
        if source_id>=0:
            if ids[list(poly.vertices)].tolist()!=native_faces[source_id]:face_errors.append(source_id)
            start=int(data['face_start'][source_id]);size=int(data['face_size'][source_id])
            actual=np.array([head.data.uv_layers[0].data[l].uv[:] for l in poly.loop_indices])
            uv_error=max(uv_error,float(np.max(abs(actual-data['uv_0'][start:start+size]))))
        for edge in poly.edge_keys:edge_owners[edge].append(source_id)
    rim=patch['aperture_derived_vertex_ids']
    attachments=[edge_owners[tuple(sorted((a,b)))] for a,b in zip(rim,rim[1:]+rim[:1])]
    source_ids={int(i) for i in ids if i>=0}
    checks['saved_native_face_mismatches']=face_errors
    checks['saved_native_loop_uv_max_error']=uv_error
    checks['saved_removed_source_vertex_ids_exact']=sorted(set(range(len(data['positions'])))-source_ids)==patch['removed_source_vertex_ids']
    checks['aperture_edges_with_exactly_one_native_and_one_new_face']=sum(len(a)==2 and sum(i>=0 for i in a)==1 for a in attachments)
    checks['aperture_edge_count']=len(rim)
    checks['source_attribute_contract']={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        attr=obj.data.attributes['source_vertex_id'];fattr=obj.data.attributes['source_face_id']
        checks['source_attribute_contract'][obj.name]={'vertex':{'type':attr.data_type,'domain':attr.domain},
            'face':{'type':fattr.data_type,'domain':fattr.domain},
            'all_new_ids_minus_one':all(a.value==-1 for a in attr.data) if obj!=head else None}
    if face_errors or uv_error or not checks['saved_removed_source_vertex_ids_exact'] or checks['aperture_edges_with_exactly_one_native_and_one_new_face']!=len(rim):
        raise RuntimeError('Saved source topology, UVs or actual cavity attachment invalid')
    for name in ('mouthSeal','jawOpen_A'):
        key=head.data.shape_keys.key_blocks[name]
        actual=np.array([head.matrix_world.to_3x3()@(v.co-b.co) for v,b in zip(key.data,head.data.vertices)])[valid]
        checks['shape_native_delta_error'][name]=float(abs(actual-expected[name][ids[valid]]).max())
    poses=[('native-rest',{}),('seal-half',{'mouthSeal':.5}),('sealed',{'mouthSeal':1}),('open-half',{'jawOpen_A':.5}),('open-A',{'jawOpen_A':1})]
    for label,values in poses:
        for obj in bpy.context.scene.objects:
            if obj.type=='MESH' and obj.data.shape_keys:
                for key in obj.data.shape_keys.key_blocks:
                    if key.name!='Basis':key.value=values.get(key.name,0)
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
        meshes={}
        for obj in bpy.context.scene.objects:
            if obj.type!='MESH':continue
            evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh();mesh.calc_loop_triangles()
            points=np.array([evaluated.matrix_world@v.co for v in mesh.vertices]);tri=np.array([t.vertices[:] for t in mesh.loop_triangles])
            source_faces=np.array([a.value for a in mesh.attributes['source_face_id'].data])
            triangles_source=source_faces[[t.polygon_index for t in mesh.loop_triangles]]
            tree=BVHTree.FromPolygons([Vector(p) for p in points],tri.tolist(),all_triangles=True)
            edge_use=defaultdict(int)
            for face in mesh.polygons:
                for edge in face.edge_keys:edge_use[edge]+=1
            meshes[obj.name]={'points':points,'triangles':tri,'tree':tree,'source_faces':triangles_source,
                              'boundary_edges':sum(n==1 for n in edge_use.values()),'nonmanifold_edges':sum(n>2 for n in edge_use.values())}
            evaluated.to_mesh_clear()
        main=meshes[head.name];collisions={}
        names=[name for name in meshes if name!=head.name]
        pairs=[(name,head.name) for name in names]+[(names[i],names[j]) for i in range(len(names)) for j in range(i+1,len(names))]
        for an,bn in pairs:
            a,b=meshes[an],meshes[bn];overlaps=a['tree'].overlap(b['tree']);hits=[]
            for ai,bi in overlaps:
                if triangle_intersects(a['points'][a['triangles'][ai]],b['points'][b['triangles'][bi]]):
                    hits.append({'first_triangle':int(ai),'second_triangle':int(bi),'second_source_face':int(b['source_faces'][bi])})
            collisions[an+' / '+bn]={'bvh_candidates':len(overlaps),'segment_triangle_intersections':hits}
        pose={'name':label,'values':values,'mesh_topology':{n:{'boundary_edges':v['boundary_edges'],'nonmanifold_edges':v['nonmanifold_edges']} for n,v in meshes.items()},
              'collisions':collisions,'intersection_limit':'Nonparallel segment/triangle narrow phase; coplanar overlap is not certified by this test.'}
        self_hits=[]
        for ai,bi in main['tree'].overlap(main['tree']):
            if ai>=bi or (main['source_faces'][ai]>=0 and main['source_faces'][bi]>=0):continue
            ta,tb=main['triangles'][ai],main['triangles'][bi]
            if set(ta)&set(tb):continue
            contacts=triangle_intersection_points(main['points'][ta],main['points'][tb])
            if contacts:
                self_hits.append({'first_triangle':int(ai),'second_triangle':int(bi),
                                  'source_faces':[int(main['source_faces'][ai]),int(main['source_faces'][bi])],
                                  'contact_positions_world':[p.tolist() for p in contacts]})
        pose['new_cavity_vs_nonadjacent_head_triangle_intersections']=self_hits
        if label=='sealed':
            rim_points=main['points'][rim];next_points=np.roll(rim_points,-1,axis=0);segments=next_points-rim_points
            distances=[]
            for hit in self_hits:
                for point in np.array(hit['contact_positions_world']):
                    t=np.clip(np.sum((point-rim_points)*segments,axis=1)/np.sum(segments*segments,axis=1),0,1)
                    distances.append(float(np.linalg.norm(point-(rim_points+t[:,None]*segments),axis=1).min()))
            pose['sealed_contact_max_distance_to_aperture_source_units']=max(distances,default=0)
            visible=[]
            for y in np.linspace(-.079,.079,317):
                for z in np.linspace(-.17,-.13,81):
                    _,_,index,_=main['tree'].ray_cast(Vector((.6,y,z)),Vector((-1,0,0)),1)
                    if index is not None and main['source_faces'][index]<0:visible.append((float(y),float(z)))
            pose['sealed_front_visible_cavity_samples']=visible
            pose['sealed_front_ray_grid']={'y_samples':317,'z_samples':81,'spacing':.0005}
        checks['poses'].append(pose)
        print('ORAL_POSE_VERIFIED',label,{k:len(v['segment_triangle_intersections']) for k,v in collisions.items()},flush=True)
    sealed=next(p for p in checks['poses'] if p['name']=='sealed')
    checks['acceptance']={
        'oral_parts_clear_in_five_poses':all(not c['segment_triangle_intersections'] for p in checks['poses'] for c in p['collisions'].values()),
        'cavity_clear_in_four_nonsealed_poses':all(not p['new_cavity_vs_nonadjacent_head_triangle_intersections'] for p in checks['poses'] if p['name']!='sealed'),
        'sealed_contacts_confined_within_001_source_units_of_wet_line':sealed['sealed_contact_max_distance_to_aperture_source_units']<=.001,
        'sealed_front_sampling_exposes_no_cavity':not sealed['sealed_front_visible_cavity_samples'],
        'native_shape_delta_readback_within_1e7_tolerance':max(checks['shape_native_delta_error'].values())<1e-7}
    write_json('saved-shape-verification.json',checks)
    if not all(checks['acceptance'].values()):raise RuntimeError('Oral prototype verification failed; inspect saved report')
    print('REN_P2_ORAL_VERIFICATION_COMPLETE',OUT,flush=True)


if __name__=='__main__':
    if '--trace-only' in sys.argv:trace_aperture()
    elif '--render-trace' in sys.argv:render_trace()
    elif '--verify' in sys.argv:verify()
    elif '--preview' in sys.argv:render_reviews(quick=True)
    elif '--render' in sys.argv:render_reviews()
    elif '--build' in sys.argv:build()
    else:trace_aperture();prepare_shapes()
