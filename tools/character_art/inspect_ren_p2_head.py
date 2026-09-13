"""Read-only construction audit of Ren's native P2 head after audit_ren_parts.

All written artifacts stay in the p2-head-native audit folder. Optional Blender
renders create temporary diagnostic copies only; no source or fitted asset saves.
"""
from __future__ import annotations

from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import sys

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/generated/characters/ren/parts-workflow-v1/audits/p2-head-native'
SOURCE = ROOT / 'art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx'
EXPECTED = 'cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, data):
    (OUT/name).write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')


def connected_edges(edges):
    adjacency = defaultdict(set)
    for a,b in edges:
        adjacency[int(a)].add(int(b));adjacency[int(b)].add(int(a))
    unseen=set(adjacency);groups=[]
    while unseen:
        seed=min(unseen);stack=[seed];group=set()
        while stack:
            v=stack.pop()
            if v in group:continue
            group.add(v);unseen.discard(v);stack.extend(adjacency[v]-group)
        groups.append((sorted(group), {str(k):v for k,v in Counter(len(adjacency[v]) for v in group).items()}))
    return groups


def topology():
    if digest(SOURCE)!=EXPECTED:raise RuntimeError('Source hash differs from assigned head')
    data=np.load(OUT/'analysis-data/mesh-000.npz')
    labels=np.load(OUT/'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    p=data['positions'];lv=data['loop_vertex'];starts=data['face_start'];sizes=data['face_size']
    faces=[lv[start:start+size] for start,size in zip(starts,sizes)]
    counts=np.bincount(data['loop_edge'],minlength=len(data['edges']))
    boundary=data['edges'][counts==1];nonmanifold=data['edges'][counts>2]
    rows=[]
    for rank in sorted(set(labels)):
        indices=np.flatnonzero(labels==rank)
        fs=np.array([i for i,f in enumerate(faces) if labels[f[0]]==rank])
        be=boundary[labels[boundary[:,0]]==rank]
        loops=[]
        for group,degree in connected_edges(be):
            points=p[group]
            loops.append({'vertices':len(group),'degree_histogram':degree,
                          'closed_simple_loop':degree=={'2':len(group)},
                          'min':points.min(axis=0).tolist(),'max':points.max(axis=0).tolist(),
                          'vertex_ids':group})
        loops.sort(key=lambda row:-row['vertices'])
        valence=Counter(data['edges'][np.isin(data['edges'][:,0],indices)|np.isin(data['edges'][:,1],indices)].ravel())
        rows.append({'rank':int(rank),'vertices':len(indices),'faces':len(fs),
                     'face_types':{str(k):int(v) for k,v in Counter(sizes[fs]).items()},
                     'boundary_edges':len(be),'boundary_groups':loops,
                     'vertex_valences':{str(k):int(v) for k,v in Counter(valence.values()).items()},
                     'min':p[indices].min(axis=0).tolist(),'max':p[indices].max(axis=0).tolist()})
    nm=[]
    for edge in nonmanifold:
        nm.append({'vertex_ids':edge.tolist(),'positions':p[edge].tolist(),'component':int(labels[edge[0]])})
    # Explicit mouth region, for local edge flow and wire views. +X is face front.
    selected=[i for i,f in enumerate(faces) if p[f,0].max()>.16 and
              p[f,1].min()<.12 and p[f,1].max()>-.12 and
              p[f,2].min()<-.045 and p[f,2].max()>-.2]
    region_vertices=np.unique(np.concatenate([faces[i] for i in selected]))
    # Trace true native edge loops from central upper/lower lip-band edges.
    # A loop only crosses ordinary valence-four, all-quad vertices; poles and
    # triangles are reported as stops instead of guessed-through geometry.
    edge_faces=defaultdict(list);vertex_edges=defaultdict(set);vertex_faces=defaultdict(set)
    for fi,(start,size,face) in enumerate(zip(starts,sizes,faces)):
        for ei in data['loop_edge'][start:start+size]:edge_faces[int(ei)].append(fi)
        for vi in face:vertex_faces[int(vi)].add(fi)
    for ei,(a,b) in enumerate(data['edges']):
        vertex_edges[int(a)].add(ei);vertex_edges[int(b)].add(ei)
    native_loops=[];seen_loops=set();seed_stops=[]
    for seed,(a,b) in enumerate(data['edges']):
        pair=p[[a,b]];direction=pair[1]-pair[0]
        if labels[a]!=0 or pair[:,0].min()<.275 or abs(pair[:,1]).max()>.027 or pair[:,2].min()<-.2 or pair[:,2].max()>-.075 or abs(direction[1])<abs(direction[2]):continue
        visited=[seed];verts=[int(a),int(b)];current=seed;at=int(b);reason='iteration limit';closed=False
        for _ in range(10000):
            if len(vertex_edges[at])!=4:
                reason=f'vertex valence {len(vertex_edges[at])}';break
            if any(sizes[fi]!=4 for fi in vertex_faces[at]):
                reason='triangle at vertex';break
            blocked={current}
            for fi in edge_faces[current]:
                blocked.update(int(ei) for ei in data['loop_edge'][starts[fi]:starts[fi]+sizes[fi]] if at in data['edges'][ei])
            options=vertex_edges[at]-blocked
            if len(options)!=1:reason=f'{len(options)} opposite edge candidates';break
            following=next(iter(options))
            if following==seed:closed=True;reason='closed';break
            if following in visited:reason='revisits another edge';break
            visited.append(following)
            ea,eb=data['edges'][following];at=int(eb if ea==at else ea);verts.append(at);current=following
        if closed:
            key=tuple(sorted(visited))
            if key in seen_loops:continue
            seen_loops.add(key);coords=p[verts]
            native_loops.append({'edges':len(visited),'edge_ids':visited,'vertex_ids':verts,
                                 'min':coords.min(axis=0).tolist(),'max':coords.max(axis=0).tolist()})
        else:seed_stops.append({'seed_edge':seed,'traversed_edges':len(visited),'stop_vertex':at,'reason':reason})
    report={'source_sha256':EXPECTED,'front_direction':'+X, verified against actual front/profile renders',
            'coordinate_units':'Source normalized dimensions; no physical meters claimed',
            'components':rows,'nonmanifold_edges':nm,
            'mouth_region':{'face_ids':selected,'vertices':len(region_vertices),
                            'faces':len(selected),'component_ranks':sorted(map(int,set(labels[region_vertices]))),
                            'face_types':{str(k):int(v) for k,v in Counter(sizes[selected]).items()}},
            'native_lip_edge_loop_traces':{'method':'central horizontal lip seeds; strict valence-four all-quad opposite-edge traversal',
                                          'closed_loops':native_loops,'stopped_traces':seed_stops}}
    write('mouth-construction-topology.json',report)
    print('HEAD_CONSTRUCTION',json.dumps({'top_components':[{k:v for k,v in r.items() if k not in ('boundary_groups','vertex_valences')} for r in rows[:5]],
                                         'main_boundary_groups':[{k:v for k,v in r.items() if k!='vertex_ids'} for r in rows[0]['boundary_groups']],
                                         'nonmanifold':nm,'mouth_faces':len(selected),
                                         'strict_closed_lip_loops':[{k:v for k,v in row.items() if k not in ('edge_ids','vertex_ids')} for row in native_loops],
                                         'stopped_lip_traces':len(seed_stops)}),flush=True)


def blender_audit():
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    if digest(SOURCE)!=EXPECTED:raise RuntimeError('Source hash differs from assigned head')
    bpy.ops.wm.open_mainfile(filepath=str(OUT/'audit-scene.blend'))
    metadata=json.loads((OUT/'import-metadata.json').read_text())
    record=metadata['objects'][0];obj=bpy.data.objects[record['object']];mesh=obj.data
    labels=np.load(OUT/'analysis-data/mesh-000-component-labels.npz')['vertex_component']
    points=[obj.matrix_world@v.co for v in mesh.vertices]
    faces=[tuple(p.vertices) for p in mesh.polygons]
    face_components=np.array([labels[p.vertices[0]] for p in mesh.polygons])
    groups={rank:[i for i,c in enumerate(face_components) if c==rank] for rank in (0,1,2,3,4)}
    trees={rank:BVHTree.FromPolygons(points,[faces[i] for i in ids]) for rank,ids in groups.items()}
    rayrows=[]
    for z in np.arange(-.19,-.045,.0025):
        for y in (-.08,-.06,-.04,-.02,0,.02,.04,.06,.08):
            hits={}
            for rank in (0,4):
                loc,normal,idx,distance=trees[rank].ray_cast(Vector((.6,y,z)),Vector((-1,0,0)),1.1)
                if loc is not None:hits[str(rank)]={'position':list(loc),'normal':list(normal),'face':groups[rank][idx]}
            rayrows.append({'lateral_y':y,'height_z':float(z),'first_hits':hits})
    write('mouth-front-rays.json',{'component_0':'main head surface','component_4':'isolated mouth insert, not yet semantic teeth/tongue acceptance','rays':rayrows})
    # Geometry-plane cross section records exact triangle intersections, not a fit.
    mesh.calc_loop_triangles();sections=[]
    for ycut in (0,.02,.06):
        segments=[]
        for tri in mesh.loop_triangles:
            coords=[points[i] for i in tri.vertices]
            if max(v.z for v in coords)<-.21 or min(v.z for v in coords)>-.035 or max(v.x for v in coords)<.075:continue
            hits=[]
            for a,b in zip(coords,coords[1:]+coords[:1]):
                if (a.y-ycut)*(b.y-ycut)<0:
                    q=a+(b-a)*(ycut-a.y)/(b.y-a.y);hits.append(list(q))
            if len(hits)==2:segments.append({'positions':hits,'component':int(face_components[tri.polygon_index]),'source_face':tri.polygon_index})
        sections.append({'y':ycut,'segments':segments})
    write('mouth-sagittal-sections.json',sections)

    def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    s=bpy.context.scene;s.render.resolution_x=s.render.resolution_y=1024;s.render.resolution_percentage=100
    s.view_layers[0].material_override=bpy.data.materials.get('AUDIT_Clay')
    camera=s.camera;camera.data.ortho_scale=.34
    center=Vector((.23,0,-.115))
    # The mouth's one disconnected insert is colored for identification only.
    clay=bpy.data.materials.get('AUDIT_Clay')
    insert_mat=clay.copy();insert_mat.name='AUDIT_MouthInsert'
    insert_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.36,.12,.055,1)
    def surface_copy(name,indices,material):
        m=bpy.data.meshes.new(name);m.from_pydata(points,[],[faces[i] for i in indices]);m.materials.append(material)
        for p in m.polygons:p.use_smooth=True
        o=bpy.data.objects.new(name,m);s.collection.objects.link(o);return o
    inserted=surface_copy('AUDIT_MouthInsert',groups[4],insert_mat)
    inserted.hide_render=True
    renders=[]
    def render(name,loc,target=center,scale=.34):
        camera.data.ortho_scale=scale;camera.location=loc;aim(camera,target)
        s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
        renders.append({'file':name+'.png','sha256':digest(OUT/(name+'.png'))})
    if '--loops-only' in sys.argv:
        s.view_layers[0].material_override=None
        mesh.materials.clear();mesh.materials.append(clay)
        loops=json.loads((OUT/'mouth-construction-topology.json').read_text())['native_lip_edge_loop_traces']['closed_loops']
        for index,(loop,color) in enumerate(zip(loops,[(1,.06,.02,1),(.04,.7,.2,1),(.03,.3,1,1),(1,.65,.02,1)])):
            curve=bpy.data.curves.new(f'AUDIT_NativeLipLoop{index}','CURVE');curve.dimensions='3D';curve.bevel_depth=.00048;curve.resolution_u=1
            line=curve.splines.new('POLY');line.points.add(len(loop['vertex_ids'])-1);line.use_cyclic_u=True
            for vertex,vi in zip(line.points,loop['vertex_ids']):
                q=points[vi].copy();q.x+=.00035;vertex.co=(*q,1)
            material=bpy.data.materials.new(curve.name);material.use_nodes=True
            tree=material.node_tree;emission=tree.nodes.new('ShaderNodeEmission');emission.inputs['Color'].default_value=color
            output=next(n for n in tree.nodes if n.type=='OUTPUT_MATERIAL');tree.links.new(emission.outputs[0],output.inputs['Surface'])
            curve.materials.append(material);guide=bpy.data.objects.new(curve.name,curve);s.collection.objects.link(guide)
        render('mouth-native-loops-front',(2,0,-.115))
        render('mouth-native-loops-three-quarter',(1.6,-1,-.115))
        write('mouth-native-loop-renders.json',renders)
        if digest(SOURCE)!=EXPECTED:raise RuntimeError('Source changed during native-loop overlay audit')
        print('P2_HEAD_LIP_LOOPS_RENDERED',OUT,flush=True)
        return
    render('mouth-clay-front',(2,0,-.115))
    render('mouth-clay-three-quarter',(1.6,-1,-.115))
    render('mouth-clay-profile',(.23,-2,-.115))
    # Wire overlay is an ephemeral mesh copy, never written into the asset.
    region=json.loads((OUT/'mouth-construction-topology.json').read_text())['mouth_region']['face_ids']
    wiremat=clay.copy();wiremat.name='AUDIT_Wire';wiremat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.008,.02,.035,1)
    wire=surface_copy('AUDIT_MouthWire',region,wiremat)
    modifier=wire.modifiers.new('Diagnostic wireframe','WIREFRAME');modifier.thickness=.00022;modifier.offset=1
    modifier.use_replace=True
    s.view_layers[0].material_override=None
    saved=list(mesh.materials);mesh.materials.clear();mesh.materials.append(clay)
    render('mouth-wire-front',(2,0,-.115))
    wire.hide_render=True
    obj.hide_render=True;inserted.hide_render=False
    render('mouth-insert-isolated-front',(2,0,-.115))
    render('mouth-insert-isolated-profile',(.23,-2,-.115))
    # Cutaway removes the camera-side half from an audit COPY. Originals remain
    # intact on disk and in the imported source datablock.
    cut_indices=[i for i in groups[0] if min(points[v].y for v in faces[i])>=0]
    cut=surface_copy('AUDIT_HalfHead',cut_indices,clay)
    render('mouth-interior-cutaway',(.23,-2,-.115),scale=.42)
    cut.hide_render=True;inserted.hide_render=True;obj.hide_render=False
    mesh.materials.clear()
    for material in saved:mesh.materials.append(material)
    write('mouth-detail-renders.json',renders)
    if digest(SOURCE)!=EXPECTED:raise RuntimeError('Source changed during read-only audit')
    print('P2_HEAD_MOUTH_AUDIT_COMPLETE',OUT,flush=True)


if __name__=='__main__':
    if '--blender-audit' in sys.argv:blender_audit()
    else:topology()
