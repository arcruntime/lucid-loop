"""Local, provenance-tracked diagnosis and minimal cleanup of Ren's P2 hair.

Run with ordinary Python. Only the separate hair-cleanup-v1 directory is written;
native FBXs and the reviewed head remain immutable. Diagnostic variants preserve
all source vertex positions and do not establish production acceptance.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "art/generated/characters/ren/parts-workflow-v1/hair-cleanup-v1"
HAIR = ROOT / "art/generated/characters/ren/parts-generation-v1/tripo/hair/originals/model.fbx"
HEAD = ROOT / "art/generated/characters/ren/parts-generation-v1/tripo/head/originals/model.fbx"
HAIR_HASH = "9f8c2cb79c763bb048b4c0b62c058e2357802e86012badf0ce0a15f240a083c9"
HEAD_HASH = "cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819"
BLENDER = "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe"
FIT_DIRECTORY = OUTPUT / "fitted-v5"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(content, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def vertex_hash(mesh):
    import numpy as np
    coordinates = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", coordinates)
    return hashlib.sha256(coordinates.tobytes()).hexdigest()


def material(name, color):
    import bpy
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = .85
    return result


def setup_scene():
    import bpy
    from mathutils import Vector
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("CleanupWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs[0].default_value = (.04, .045, .055, 1)
    scene.world.node_tree.nodes.get("Background").inputs[1].default_value = .5
    scene.view_settings.view_transform = "AgX"
    camera = bpy.data.objects.new("CleanupCamera", bpy.data.cameras.new("CleanupCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.2
    camera.data.clip_start, camera.data.clip_end = .001, 20
    for name, location, energy in (("Key", (2,-2,3),230), ("Fill", (2,2,1),120), ("Rim",(-2,1,2),180)):
        light = bpy.data.objects.new(name, bpy.data.lights.new(name,"AREA"))
        light.data.energy, light.data.size = energy, 2
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (-light.location).to_track_quat("-Z","Y").to_euler()
    return scene


def render_views(scene, directory, prefix, target=(0,0,0), scale=1.2):
    import bpy
    from mathutils import Vector
    directory.mkdir(parents=True, exist_ok=True)
    target = Vector(target)
    records = []
    scene.camera.data.ortho_scale = scale
    for name, yaw, elevation in (("front",0,0),("three-quarter",-35,0),("profile",-90,0),("underside",0,-65)):
        azimuth, altitude = math.radians(yaw), math.radians(elevation)
        direction = Vector((math.cos(azimuth)*math.cos(altitude), math.sin(azimuth)*math.cos(altitude), math.sin(altitude)))
        scene.camera.location = target + direction * 3
        scene.camera.rotation_euler = (target-scene.camera.location).to_track_quat("-Z","Y").to_euler()
        path = directory / f"{prefix}-{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        records.append({"path":path.relative_to(OUTPUT).as_posix(),"sha256":digest(path)})
        print("RENDERED", path.name, flush=True)
    return records


def import_mesh(path):
    import bpy
    previous = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False, automatic_bone_orientation=False)
    meshes = [obj for obj in bpy.data.objects if obj not in previous and obj.type == "MESH"]
    assert len(meshes) == 1
    return meshes[0]


def ranked_components(mesh):
    """Rank by face count, with deterministic minimum-vertex tie ordering."""
    import numpy as np
    parents=list(range(len(mesh.vertices)))
    def root(i):
        while parents[i]!=i:
            parents[i]=parents[parents[i]];i=parents[i]
        return i
    for edge in mesh.edges:
        a,b=(root(i) for i in edge.vertices)
        if a!=b:parents[b]=a
    ids={};labels=[]
    for i in range(len(parents)):
        r=root(i)
        if r not in ids:ids[r]=len(ids)
        labels.append(ids[r])
    labels=np.array(labels,dtype=np.int32)
    counts=np.bincount([labels[face.vertices[0]] for face in mesh.polygons],minlength=len(ids))
    order=np.argsort(-counts,kind="stable");ranks=np.empty(len(ids),dtype=np.int32)
    ranks[order]=np.arange(len(ids))
    return ranks[labels]


def diagnose():
    import bpy
    import bmesh
    import numpy as np
    assert digest(HAIR) == HAIR_HASH and digest(HEAD) == HEAD_HASH
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = import_mesh(HAIR)
    source.name = "Hair_source"
    original_vertex_hash = vertex_hash(source.data)
    clay = material("HairClay",(.3,.34,.39))
    source.data.materials.clear(); source.data.materials.append(clay)
    for face in source.data.polygons:
        face.material_index = 0
    positions = np.array([vertex.co[:] for vertex in source.data.vertices], dtype=np.float64)
    corners = [list(face.vertices) for face in source.data.polygons]
    quad_indices = [index for index,face in enumerate(corners) if len(face)==4]
    quads = positions[np.array([corners[index] for index in quad_indices])]
    n0 = np.cross(quads[:,1]-quads[:,0],quads[:,2]-quads[:,0])
    n1 = np.cross(quads[:,2]-quads[:,0],quads[:,3]-quads[:,0])
    cosine = np.sum(n0*n1,axis=1)/np.maximum(np.linalg.norm(n0,axis=1)*np.linalg.norm(n1,axis=1),1e-30)
    angles = np.degrees(np.arccos(np.clip(cosine,-1,1)))
    edge_incidence = np.zeros(len(source.data.edges),dtype=np.int32)
    for loop in source.data.loops:
        edge_incidence[loop.edge_index] += 1
    source_bm = bmesh.new(); source_bm.from_mesh(source.data)
    inconsistent = [edge.index for edge in source_bm.edges if edge.is_manifold and not edge.is_contiguous]
    source_bm.free()
    report = {"source":str(HAIR),"source_sha256":HAIR_HASH,"head_sha256":HEAD_HASH,
              "blender_version":bpy.app.version_string,"source_vertex_sha256":original_vertex_hash,
              "source_has_custom_normals": bool(source.data.has_custom_normals),
              "source_smooth_faces":sum(face.use_smooth for face in source.data.polygons),
              "quads":len(quads),"quad_diagonal_02_triangle_normal_angle_degrees":{
                  "median":float(np.median(angles)),"p95":float(np.percentile(angles,95)),
                  "above_30":int((angles>30).sum()),"above_90":int((angles>90).sum()),
                  "above_150":int((angles>150).sum())},
              "inconsistently_wound_manifold_edges":len(inconsistent),
              "nonmanifold_edges":int((edge_incidence>2).sum()),"variants":[],"renders":[]}
    write_json(OUTPUT/"diagnosis/topology.json", report)
    variants = [("source",source)]
    for name in ("automatic-normals","consistent-winding","triangulated-beauty","flat-normals"):
        obj = source.copy(); obj.data = source.data.copy(); obj.name = "Hair_"+name
        bpy.context.scene.collection.objects.link(obj)
        obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))
        if name in {"consistent-winding","triangulated-beauty"}:
            mesh_bm = bmesh.new(); mesh_bm.from_mesh(obj.data)
            bmesh.ops.recalc_face_normals(mesh_bm, faces=list(mesh_bm.faces))
            if name == "triangulated-beauty":
                bmesh.ops.triangulate(mesh_bm,faces=list(mesh_bm.faces),quad_method="BEAUTY",ngon_method="BEAUTY")
            mesh_bm.to_mesh(obj.data); mesh_bm.free()
        if name == "flat-normals":
            for face in obj.data.polygons: face.use_smooth = False
        obj.data.update()
        variants.append((name,obj))
    head = import_mesh(HEAD)
    head.name = "Head_clearance_readonly"
    head_hash = vertex_hash(head.data)
    head_material = material("HeadClay",(.45,.47,.50))
    head.data.materials.clear();head.data.materials.append(head_material)
    for face in head.data.polygons:face.material_index = 0
    head.hide_render = True
    scene = setup_scene()
    for name,obj in variants:
        for _,other in variants:other.hide_render = other is not obj
        changed = vertex_hash(obj.data) != original_vertex_hash
        assert not changed, "Diagnostic variants must not alter source vertex positions"
        report["variants"].append({"name":name,"vertices":len(obj.data.vertices),"faces":len(obj.data.polygons),
                                   "vertex_positions_unchanged":True,"has_custom_normals":bool(obj.data.has_custom_normals)})
        report["renders"] += render_views(scene,OUTPUT/"diagnosis",name)
        write_json(OUTPUT/"diagnosis/report.json",report)
    # Rigid reviewed placement only. The head's positions and source bytes do not change.
    for _,obj in variants:obj.hide_render = obj is not source
    source.location = (-.09,0,.22)
    head.hide_render = False
    report["renders"] += render_views(scene,OUTPUT/"diagnosis","source-clearance",target=(0,0,.1),scale=1.55)
    source.location = (0,0,0)
    head.hide_render = True
    report["head_vertex_positions_unchanged"] = vertex_hash(head.data)==head_hash
    assert report["head_vertex_positions_unchanged"]
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    report["native_sources_unchanged"] = True
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/"diagnosis/diagnostic-workspace.blend"))
    write_json(OUTPUT/"diagnosis/report.json",report)
    print("HAIR_DIAGNOSIS_COMPLETE",flush=True)


def prune():
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = import_mesh(HAIR);source.name="Hair_source"
    head = import_mesh(HEAD);head.name="Head_clearance_readonly"
    head_hash=vertex_hash(head.data)
    hair_hash=vertex_hash(source.data)
    clay=material("HairClay",(.3,.34,.39));red=material("RemovedFaces",(.7,.06,.035))
    head_clay=material("HeadClay",(.45,.47,.50))
    source.data.materials.clear();source.data.materials.append(clay)
    head.data.materials.clear();head.data.materials.append(head_clay)
    for obj in (source,head):
        for face in obj.data.polygons:face.material_index=0
    mesh=source.data
    original=mesh.attributes.new("source_face_index","INT","FACE")
    original.data.foreach_set("value",np.arange(len(mesh.polygons),dtype=np.int32))
    original_vertex=mesh.attributes.new("source_vertex_index","INT","POINT")
    original_vertex.data.foreach_set("value",np.arange(len(mesh.vertices),dtype=np.int32))
    bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table();bm.edges.ensure_lookup_table()
    rejection=np.zeros(len(mesh.polygons),dtype=np.int32)
    support=np.zeros(len(mesh.polygons),dtype=np.int32)
    edges=[]
    for edge in bm.edges:
        faces=list(edge.link_faces)
        if len(faces)<=2:continue
        options=[]
        for a in range(len(faces)):
            for b in range(a+1,len(faces)):
                la=next(loop for loop in faces[a].loops if loop.edge==edge)
                lb=next(loop for loop in faces[b].loops if loop.edge==edge)
                if la.vert==lb.vert:continue
                options.append((faces[a].normal.dot(faces[b].normal),a,b))
        if not options:continue
        agreement,a,b=max(options)
        if agreement<.85:continue
        support[faces[a].index]+=1;support[faces[b].index]+=1
        rejected=[]
        for index,face in enumerate(faces):
            if index in (a,b):continue
            if max(face.normal.dot(faces[a].normal),face.normal.dot(faces[b].normal))<.5:
                rejection[face.index]+=1;rejected.append(face.index)
        if rejected:edges.append({"edge":edge.index,"retained_pair":[faces[a].index,faces[b].index],"rejected":rejected,"normal_dot":agreement})
    branch_faces={int(i) for i in np.flatnonzero((rejection>=2)&(support==0))}
    # Only identify hidden sheets with a conservative scalp-volume test; no head edits.
    head_positions=[head.matrix_world@vertex.co for vertex in head.data.vertices]
    head_bvh=BVHTree.FromPolygons(head_positions,[list(face.vertices) for face in head.data.polygons],all_triangles=False)
    world_points=[source.matrix_world@vertex.co+Vector((-.09,0,.22)) for vertex in mesh.vertices]
    inside=np.zeros(len(mesh.vertices),dtype=bool)
    directions=(Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
    for index,point in enumerate(world_points):
        if point.z<.12:continue
        nearest,normal,_,distance=head_bvh.find_nearest(point)
        if nearest is None or distance<.005 or (point-nearest).dot(normal)>=0:continue
        parities=[]
        for direction in directions:
            origin=point.copy();hits=0
            for _ in range(20):
                location,_,face,ray_distance=head_bvh.ray_cast(origin,direction,2)
                if location is None:break
                hits+=1;origin=location+direction*1e-5
            parities.append(hits%2==1)
        inside[index]=all(parities)
    inside_faces={face.index for face in mesh.polygons if all(inside[vertex] for vertex in face.vertices)}
    bm.free()
    report={"source_sha256":HAIR_HASH,"head_sha256":HEAD_HASH,"branch_face_rule":"At least two nonmanifold-edge rejections and zero continuation-pair support. Pair normal dot >= .85, rejected face dot < .5 to both pair faces; pair winds oppositely around shared edge.",
            "buried_sheet_rule":"Every face vertex above world z=.12, at least .005 inside nearest head surface, and odd +X/+Y/+Z ray parity using unchanged head at reviewed hair placement.",
            "branch_removed_faces":sorted(branch_faces),"buried_removed_faces":sorted(inside_faces),
            "branch_faces":len(branch_faces),"buried_faces":len(inside_faces),"edge_votes":edges,"variants":[],"renders":[]}
    variants=[("source",source)]
    for name,removed in (("branch-pruned",branch_faces),("branch-and-buried-pruned",branch_faces|inside_faces)):
        obj=source.copy();obj.data=source.data.copy();obj.name="Hair_"+name
        bpy.context.scene.collection.objects.link(obj)
        obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))
        edit=bmesh.new();edit.from_mesh(obj.data);edit.faces.ensure_lookup_table()
        bmesh.ops.delete(edit,geom=[face for face in edit.faces if face.index in removed],context="FACES")
        bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces))
        edit.to_mesh(obj.data);edit.free();obj.data.update()
        ids=np.array([item.value for item in obj.data.attributes["source_vertex_index"].data])
        candidate_positions=np.array([vertex.co[:] for vertex in obj.data.vertices])
        source_positions=np.array([mesh.vertices[index].co[:] for index in ids])
        assert np.array_equal(candidate_positions,source_positions)
        report["variants"].append({"name":name,"removed_faces":len(removed),"faces":len(obj.data.polygons),"vertices":len(obj.data.vertices),"retained_vertex_positions_exact":True})
        variants.append((name,obj))
    overlay=source.copy();overlay.data=source.data.copy();overlay.name="Hair_removal_overlay"
    bpy.context.scene.collection.objects.link(overlay);overlay.data.materials.append(red)
    for face in overlay.data.polygons:face.material_index=1 if face.index in branch_faces|inside_faces else 0
    variants.append(("removal-overlay",overlay))
    scene=setup_scene();head.hide_render=True
    for name,obj in variants:
        for _,other in variants:other.hide_render=other is not obj
        report["renders"]+=render_views(scene,OUTPUT/"pruning",name)
    head.hide_render=False
    for name,obj in variants:
        if name=="removal-overlay":continue
        for _,other in variants:other.hide_render=other is not obj
        obj.location=(-.09,0,.22)
        report["renders"]+=render_views(scene,OUTPUT/"pruning",name+"-clearance",target=(0,0,.1),scale=1.55)
        obj.location=(0,0,0)
    head.hide_render=True
    for _,obj in variants:obj.hide_render=obj is not variants[2][1]
    assert vertex_hash(source.data)==hair_hash and vertex_hash(head.data)==head_hash
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    report["native_sources_and_head_vertices_unchanged"]=True
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/"pruning/pruning-workspace.blend"))
    write_json(OUTPUT/"pruning/report.json",report)
    print("HAIR_PRUNING_READY",len(branch_faces),len(inside_faces),flush=True)


def reconstruct():
    """Trace side/back clumps against native surface samples; retain front locks."""
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source=import_mesh(HAIR);source.name="Hair_source"
    head=import_mesh(HEAD);head.name="Head_clearance_readonly"
    head_hash=vertex_hash(head.data)
    positions=np.array([vertex.co[:] for vertex in source.data.vertices],dtype=float)
    data_dir=ROOT/"art/generated/characters/ren/parts-workflow-v1/audits/p2-hair-native/analysis-data"
    labels=ranked_components(source.data)
    cache=data_dir/"mesh-000-component-labels.npz"
    if cache.exists():
        cached=np.load(cache)["vertex_component"]
        # Only ranks below 30 are traced/retained. Smaller equal-face-count
        # components may have a different stable tie order in SciPy's graph.
        assert np.array_equal(labels[labels<30],cached[labels<30])
    assert len(labels)==len(positions)
    clay=material("HairClay",(.3,.34,.39));head_clay=material("HeadClay",(.45,.47,.50))
    source.data.materials.clear();source.data.materials.append(clay)
    head.data.materials.clear();head.data.materials.append(head_clay)
    for obj in (source,head):
        for face in obj.data.polygons:face.material_index=0
    # Preserve the principal source front bangs, with the front decorative locks.
    # These have named source component ranks, not anonymous generic additions.
    keep_ranks={7,10,17,18,29}
    crown_ranks={0,1,2,3,4,5,8,9}
    crown_faces=[face.index for face in source.data.polygons if int(labels[face.vertices[0]]) in crown_ranks
                 and min(positions[index,2] for index in face.vertices)>.31
                 and min(positions[index,0] for index in face.vertices)>.10]
    keep_faces=sorted(set(crown_faces)|{face.index for face in source.data.polygons if int(labels[face.vertices[0]]) in keep_ranks})
    retained=source.copy();retained.data=source.data.copy();retained.name="Hair_retained_front_bangs"
    bpy.context.scene.collection.objects.link(retained)
    edit=bmesh.new();edit.from_mesh(retained.data);edit.faces.ensure_lookup_table()
    bmesh.ops.delete(edit,geom=[face for face in edit.faces if face.index not in set(keep_faces)],context="FACES")
    edit.to_mesh(retained.data);edit.free()
    retained.data.normals_split_custom_set([(0,0,0)]*len(retained.data.loops))
    source_angles=np.arctan2(positions[:,1],positions[:,0])
    source_radius=np.linalg.norm(positions[:,:2],axis=1)
    rebuilt=[];clump_records=[]

    def trace(theta,end_z,root_z,width,layer):
        """Sample the original outer envelope along a crown-to-tip trajectory."""
        controls=[]
        for t in np.linspace(0,1,9):
            z=root_z*(1-t)+end_z*t
            angle=theta*(.42+.58*math.sin(t*math.pi/2))
            delta=np.abs(np.arctan2(np.sin(source_angles-angle),np.cos(source_angles-angle)))
            score=(delta/.27)**2+((positions[:,2]-z)/.045)**2
            nearest=np.argsort(score)[:120]
            r=float(np.percentile(source_radius[nearest],82))
            # Extra offset is a small clump layering allowance, not a fitted head.
            r+={"outer":.004,"upper":.022,"lower":-.012}[layer]
            point=np.array([r*math.cos(angle),r*math.sin(angle),z])
            controls.append(point)
        # Smooth the sampled outer envelope instead of copying native crossbars
        # into new traces. A cubic envelope retains the measured large curvature.
        samples=np.linspace(0,1,len(controls));raw=np.array(controls)
        for axis in (0,1):
            coefficients=np.polyfit(samples,raw[:,axis],3)
            raw[:,axis]=np.polyval(coefficients,samples)
        controls=[point.copy() for point in raw]
        if layer in {"outer","upper"}:
            controls[0]=np.array([.25-.22*abs(theta)/math.pi,-.04+.04*math.sin(theta),root_z+.008])
            controls[1][:2]*=.85
        controls[-1][:2]*=1.10 if layer=="upper" else .96
        return controls

    def catmull(points,t):
        scaled=t*(len(points)-1);i=min(int(scaled),len(points)-2);u=scaled-i
        a=points[max(0,i-1)];b=points[i];c=points[i+1];d=points[min(len(points)-1,i+2)]
        return .5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)

    def make_clump(name,controls,width):
        rings,around=49,12;vertices=[];faces=[];previous_lateral=None
        for i in range(rings):
            t=i/(rings-1);center=catmull(controls,t)
            tangent=Vector(catmull(controls,min(1,t+.002))-catmull(controls,max(0,t-.002))).normalized()
            radial=Vector((center[0],center[1],.25)).normalized()
            if previous_lateral is None:
                lateral=tangent.cross(radial).normalized()
            else:
                lateral=(previous_lateral-tangent*previous_lateral.dot(tangent)).normalized()
            previous_lateral=lateral.copy()
            outward=lateral.cross(tangent).normalized()
            # Broad flattened lanceolate lock, little root taper, crisp pointed tip.
            span=width*((1-t)**.5)*(.05+1.3*math.sin(math.pi*t)**.65)
            span=max(span,.0004)
            thickness=max(.0015,span*.16)
            for j in range(around):
                angle=2*math.pi*j/around
                co=Vector(center)+lateral*(span*math.cos(angle))+outward*(thickness*math.sin(angle))
                vertices.append(tuple(co))
        for i in range(rings-1):
            for j in range(around):faces.append((i*around+j,i*around+(j+1)%around,(i+1)*around+(j+1)%around,(i+1)*around+j))
        faces.extend([tuple(reversed(range(around))),tuple((rings-1)*around+j for j in range(around))])
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
        edit=bmesh.new();edit.from_mesh(mesh);bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces));edit.to_mesh(mesh);edit.free()
        obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj);mesh.materials.append(clay)
        for face in mesh.polygons:face.use_smooth=True
        uv=mesh.uv_layers.new(name="LongitudinalClumpUV")
        for face in mesh.polygons:
            for loop in face.loop_indices:
                vi=mesh.loops[loop].vertex_index;ring,j=divmod(vi,around)
                uv.data[loop].uv=(ring/(rings-1),j/around)
        return obj

    # Native +X is front. Offset side/back paths follow the asymmetric V2 shag.
    specs=[]
    for side,angles,ends in ((-1,[52,72,94,116,140,163],[-.28,-.36,-.41,-.40,-.36,-.35]),
                            (1,[52,76,100,126,151,176],[-.23,-.32,-.37,-.37,-.34,-.31])):
        for index,(angle,end_z) in enumerate(zip(angles,ends)):
            specs.append((f"outer_{'left' if side<0 else 'right'}_{index}",side*angle,end_z,.43-.006*index,.052+.005*(index%3),"outer"))
            specs.append((f"upper_{'left' if side<0 else 'right'}_{index}",side*(angle+8),-.09-.025*(index%4),.405-.004*index,.050+.004*(index%2),"upper"))
    for i,angle in enumerate([-72,-103,-135,-167,162,131,100,70]):
        specs.append((f"lower_{i}",angle,-.43+.015*(i%3),.15,.049,"lower"))
    for name,angle,end_z,root_z,width,layer in specs:
        controls=trace(math.radians(angle),end_z,root_z,width,layer)
        obj=make_clump("Hair_rebuilt_"+name,controls,width);rebuilt.append(obj)
        clump_records.append({"name":obj.name,"source_trace_points":[point.tolist() for point in controls],
                              "max_half_width":width,"layer":layer,"source_envelope_percentile":82})
    # The two main native bangs contain branch notches themselves. Trace each
    # component independently through horizontal sections, preserving its role.
    for rank,width in ((3,.043),(4,.047)):
        cloud=positions[labels==rank]
        z_top,z_tip=float(cloud[:,2].max()),float(cloud[:,2].min())
        controls=[]
        for z in np.linspace(z_top,z_tip,9):
            closest=np.argsort(np.abs(cloud[:,2]-z))[:max(16,len(cloud)//20)]
            center=np.median(cloud[closest],axis=0);center[2]=z;controls.append(center)
        samples=np.linspace(0,1,len(controls));raw=np.array(controls)
        for axis in (0,1):raw[:,axis]=np.polyval(np.polyfit(samples,raw[:,axis],3),samples)
        controls=[point.copy() for point in raw]
        obj=make_clump(f"Hair_rebuilt_source_bang_{rank}",controls,width);rebuilt.append(obj)
        clump_records.append({"name":obj.name,"source_component_rank":rank,"source_trace_points":[point.tolist() for point in controls],
                              "max_half_width":width,"layer":"front_source_component_trace"})
    # Three explicitly traced crown sweeps cover the replaced aggregate's roots,
    # following the front/side/back V2 part direction instead of a scalp/skin cap.
    crown_traces=[]
    for name,width,points in crown_traces:
        controls=[np.array(point,dtype=float) for point in points]
        obj=make_clump("Hair_rebuilt_"+name,controls,width);rebuilt.append(obj)
        clump_records.append({"name":obj.name,"source_trace_points":points,"max_half_width":width,
                              "layer":"crown_sweep","trace_policy":"Manual native-coordinate crown/part contour anchors checked against V2 sheets"})
    # Intentional hair-root foundation under the locks. Copy only scalp crown
    # faces; the original head is unchanged. No forehead/brow/eye faces included.
    hair_offset=Vector((-.09,0,.22))
    root_vertices=[tuple(head.matrix_world@(vertex.co+vertex.normal*.007)-hair_offset) for vertex in head.data.vertices]
    root_faces=[]
    for face in head.data.polygons:
        center=head.matrix_world@face.center
        if center.z>.28 and center.x<.24+.4*(center.z-.28):root_faces.append(tuple(face.vertices))
    root_mesh=bpy.data.meshes.new("Hair_root_foundation")
    root_mesh.from_pydata(root_vertices,[],root_faces);root_mesh.update()
    edit=bmesh.new();edit.from_mesh(root_mesh)
    bmesh.ops.delete(edit,geom=[vertex for vertex in edit.verts if not vertex.link_faces],context="VERTS")
    edit.to_mesh(root_mesh);edit.free()
    roots=bpy.data.objects.new("Hair_root_foundation",root_mesh);bpy.context.scene.collection.objects.link(roots)
    root_mesh.materials.append(clay)
    for face in root_mesh.polygons:face.use_smooth=True
    # The copied scalp foundation was a rejected experiment: its edge showed on
    # the forehead. Keep it hidden for diagnosis; restore native crown contours.
    roots.hide_render=True
    candidate=[retained]+rebuilt
    scene=setup_scene();head.hide_render=True
    report={"source_sha256":HAIR_HASH,"head_sha256":HEAD_HASH,"source_front_components_retained":sorted(keep_ranks),
            "source_front_face_indices_retained":keep_faces,"source_front_faces_retained":len(keep_faces),
            "source_crown_face_indices_retained":crown_faces,"source_crown_faces_retained":len(crown_faces),
            "root_foundation":{"included_in_candidate":False,"status":"Rejected visible forehead edge; native front crown retained instead"},
            "source_faces_replaced":len(source.data.polygons)-len(keep_faces),"clumps":clump_records,
            "method":"Retain named native front components. Rebuild side/back as closed flattened tapered profile meshes with smooth centerlines sampled from native outer hair envelope along V2 crown-to-tip directions. Remove fine source wisps from replaced region.",
            "placement":{"head":"unchanged","hair_position":[-.09,0,.22],"hair_scale":1},"renders":[]}
    for stage in ("source","candidate"):
        source.hide_render=stage!="source"
        for obj in candidate:obj.hide_render=stage!="candidate"
        report["renders"]+=render_views(scene,OUTPUT/"reconstruction",stage)
        head.hide_render=False
        source.location=(-.09,0,.22)
        for obj in candidate:obj.location=(-.09,0,.22)
        report["renders"]+=render_views(scene,OUTPUT/"reconstruction",stage+"-assembled",target=(0,0,.1),scale=1.55)
        source.location=(0,0,0)
        for obj in candidate:obj.location=(0,0,0)
        head.hide_render=True
    source.hide_render=True
    assert vertex_hash(head.data)==head_hash
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    report["native_sources_and_head_vertices_unchanged"]=True
    report["geometry_counts"]=[]
    for obj in candidate:
        mesh=obj.data;mesh.calc_loop_triangles()
        incidence=np.zeros(len(mesh.edges),dtype=np.int32)
        for loop in mesh.loops:incidence[loop.edge_index]+=1
        report["geometry_counts"].append({"object":obj.name,"vertices":len(mesh.vertices),"faces":len(mesh.polygons),
                                          "triangles":len(mesh.loop_triangles),"boundary_edges":int((incidence==1).sum()),
                                          "nonmanifold_edges":int((incidence>2).sum()),"uv_layers":[layer.name for layer in mesh.uv_layers]})
    report["uv_status"]="Neutral geometry trial: source bangs retain provider UVs; new clumps have longitudinal working coordinates, overlapping between objects, not an accepted painted atlas."
    bpy.ops.object.select_all(action="DESELECT")
    for obj in candidate:obj.select_set(True)
    bpy.context.view_layer.objects.active=candidate[0]
    exports=[]
    for extension in ("fbx","glb"):
        path=OUTPUT/"reconstruction"/("ren-p2-hair-cleanup-trial."+extension)
        if extension=="fbx":
            bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},use_mesh_modifiers=False,
                                     use_triangles=False,add_leaf_bones=False,bake_anim=False)
        else:
            bpy.ops.export_scene.gltf(filepath=str(path),export_format="GLB",use_selection=True,export_apply=False,
                                      export_yup=True,export_animations=False)
        exports.append({"path":path.name,"sha256":digest(path),"bytes":path.stat().st_size})
    report["exports"]=exports
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/"reconstruction/reconstruction-workspace.blend"))
    write_json(OUTPUT/"reconstruction/report.json",report)
    print("HAIR_RECONSTRUCTION_READY",flush=True)


def fit():
    """Fit the clean traced locks to the unchanged head in a new trial."""
    import bpy
    import bmesh
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    directory=FIT_DIRECTORY
    directory.mkdir(parents=True,exist_ok=True)
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    workspace=OUTPUT/"reconstruction/reconstruction-workspace.blend"
    if not workspace.exists():reconstruct()
    bpy.ops.wm.open_mainfile(filepath=str(workspace))
    head=bpy.data.objects["Head_clearance_readonly"]
    source=bpy.data.objects["Hair_source"]
    head_hash=vertex_hash(head.data)
    head_points=[head.matrix_world@vertex.co for vertex in head.data.vertices]
    bvh=BVHTree.FromPolygons(head_points,[list(face.vertices) for face in head.data.polygons],all_triangles=False)
    for obj in bpy.context.scene.objects:
        if obj.type=="MESH":obj.hide_render=True
    originals=[obj for obj in bpy.context.scene.objects if obj.type=="MESH" and obj.name.startswith("Hair_rebuilt_")]
    candidate=[];changes=[]
    offset=Vector((-.09,0,.22));skull_center=Vector((-.06,0,.20))
    clay=bpy.data.materials["HairClay"]
    # The provider's two front branches fold through the forehead after fitting.
    # Re-anchor only those paths to the approved asymmetric part and fringe.
    front_paths={
        "3":[(.075,.018,.475),(.19,-.075,.452),(.29,-.150,.392),(.345,-.190,.300),
             (.366,-.204,.200),(.373,-.170,.100),(.354,-.108,.045)],
        "4":[(.075,.035,.475),(.19,.130,.438),(.267,.213,.361),(.305,.264,.260),
             (.310,.277,.140),(.285,.253,.026),(.250,.217,-.025)]}
    def front_curve(points,t):
        points=[Vector(p) for p in points]
        scaled=t*(len(points)-1);i=min(int(scaled),len(points)-2);u=scaled-i
        a=points[max(0,i-1)];b=points[i];c=points[i+1];d=points[min(len(points)-1,i+2)]
        return .5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u)
    for index,original in enumerate(originals):
        obj=original.copy();obj.data=original.data.copy();obj.name=original.name.replace("Hair_rebuilt_","Hair_fitted_")
        bpy.context.scene.collection.objects.link(obj)
        original_points=[vertex.co.copy() for vertex in obj.data.vertices]
        front="source_bang" in obj.name
        maximum_displacement=0
        fitted_centers=[];ring_spans=[]
        # Each traced source lock has 49 rings x 12 profile vertices.
        assert len(obj.data.vertices)==49*12
        for ring in range(49):
            t=ring/48
            old_center=sum((original_points[ring*12+j] for j in range(12)),Vector())/12+offset
            nearest,normal,_,distance=bvh.find_nearest(old_center)
            if nearest is None:raise RuntimeError("Scalp fit point missing")
            weight=max(0,1-t/.4) if front else max(0,1-(t/.88)**2)
            target=nearest+normal*(.009 if front else .010+.023*math.sin(math.pi*t))
            new_center=old_center.lerp(target,weight)
            if not front and t>.55:
                new_center.z+=(.035+.015*(index%3))*((t-.55)/.45)
                radial=Vector((new_center.x-skull_center.x,new_center.y,new_center.z*0))
                if radial.length:new_center-=radial.normalized()*(.025*t)
            fitted_centers.append(new_center)
            ring_spans.append(max((original_points[ring*12+j]+offset-old_center).length for j in range(12))*(.94 if front else .88))
        if front:
            rank=obj.name.rsplit("_",1)[-1]
            fitted_centers=[];ring_spans=[]
            for ring in range(49):
                t=ring/48;point=front_curve(front_paths[rank],t)
                # Use an actual front ray, not the nearest face normal: the
                # provider's eyelash normals point away from the scalp and can
                # pull a lower fringe ring upward into a folded blunt tip.
                hit,_,_,_=bvh.ray_cast(Vector((1,point.y,point.z)),Vector((-1,0,0)),2)
                if hit is not None:point.x=hit.x+.013+.025*math.sin(math.pi*t)
                fitted_centers.append(point)
                ring_spans.append(max(.0004,.061*(1-t)**.55*(.42+1.05*math.sin(math.pi*t)**.7)))
        # Projection changes centerline tangents. Rebuild a transported profile
        # frame instead of retaining old ring angles, which creates fold bands.
        for _ in range(10):
            prior=[point.copy() for point in fitted_centers]
            for i in range(1,48):fitted_centers[i]=prior[i-1]*.18+prior[i]*.64+prior[i+1]*.18
        previous_lateral=None
        for ring,new_center in enumerate(fitted_centers):
            tangent=(fitted_centers[min(48,ring+1)]-fitted_centers[max(0,ring-1)]).normalized()
            radial=(new_center-skull_center).normalized()
            lateral=tangent.cross(radial).normalized() if previous_lateral is None else (previous_lateral-tangent*previous_lateral.dot(tangent)).normalized()
            previous_lateral=lateral.copy();outward=lateral.cross(tangent).normalized()
            for j in range(12):
                theta=2*math.pi*j/12;vi=ring*12+j
                fitted=new_center+lateral*(ring_spans[ring]*math.cos(theta))+outward*(max(.0006,ring_spans[ring]*.13)*math.sin(theta))
                obj.data.vertices[vi].co=fitted
                maximum_displacement=max(maximum_displacement,(fitted-original_points[vi]-offset).length)
        edit=bmesh.new();edit.from_mesh(obj.data);bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces));edit.to_mesh(obj.data);edit.free()
        obj.data.update()
        candidate.append(obj)
        changes.append({"object":obj.name,"source_object":original.name,"maximum_displacement_from_old_rigid_placement":maximum_displacement,
                        "fit":"V2 asymmetric front fringe controls fitted to unchanged head" if front else "Ring centers projected toward unchanged scalp; root contact, reduced side clearance, varied shorter tips",
                        "front_controls":front_paths[obj.name.rsplit("_",1)[-1]] if front else None})
    # Continuous crown surface sampled from the unchanged skull. High smooth
    # front border is under the fringe, rather than cut across the forehead.
    def scalp(direction):
        hit,normal,_,_=bvh.ray_cast(skull_center,direction.normalized(),2)
        if hit is None:raise RuntimeError("Scalp crown ray missed")
        return hit,normal
    columns,rings=96,24;vertices=[]
    hit,normal=scalp(Vector((0,0,1)));vertices.append(tuple(hit+normal*.007))
    for ring in range(1,rings+1):
        u=ring/rings
        for j in range(columns):
            theta=2*math.pi*j/columns;backness=(1-math.cos(theta))*.5
            phi=(.63+1.18*backness**.65)*u
            direction=Vector((math.sin(phi)*math.cos(theta),math.sin(phi)*math.sin(theta),math.cos(phi)))
            hit,normal=scalp(direction)
            vertices.append(tuple(hit+normal*(.007*(1-.9*max(0,(u-.83)/.17)))))
    faces=[(0,1+j,1+(j+1)%columns) for j in range(columns)]
    for ring in range(rings-1):
        for j in range(columns):
            a=1+ring*columns+j;b=1+ring*columns+(j+1)%columns
            faces.append((a,b,b+columns,a+columns))
    mesh=bpy.data.meshes.new("Fitted_root_base");mesh.from_pydata(vertices,[],faces);mesh.update()
    edit=bmesh.new();edit.from_mesh(mesh);bmesh.ops.recalc_face_normals(edit,faces=list(edit.faces));edit.to_mesh(mesh);edit.free()
    cap=bpy.data.objects.new("Fitted_root_base",mesh);bpy.context.scene.collection.objects.link(cap);mesh.materials.append(clay)
    for face in mesh.polygons:face.use_smooth=True
    candidate.append(cap)
    # Two short, attached deliberate flyaways replace the floating native wire.
    for index,points in enumerate([
        [(.04,-.025,.495),(.025,-.075,.540),(-.040,-.145,.548),(-.14,-.22,.507)],
        [(-.035,.045,.486),(-.09,.10,.528),(-.17,.16,.529),(-.24,.21,.478)]]):
        curve=bpy.data.curves.new("Short_flyaway","CURVE");curve.dimensions="3D";curve.resolution_u=12
        curve.bevel_depth=.0065;curve.bevel_resolution=2;curve.use_fill_caps=True
        spline=curve.splines.new("BEZIER");spline.bezier_points.add(len(points)-1)
        for point,co,radius in zip(spline.bezier_points,points,[.45,1,.65,.015]):
            point.co=co;point.radius=radius;point.handle_left_type=point.handle_right_type="AUTO"
        root=spline.bezier_points[0];nearest,normal,_,_=bvh.find_nearest(root.co);root.co=nearest+normal*.005
        curve_obj=bpy.data.objects.new(f"Short_flyaway_{index}",curve);bpy.context.scene.collection.objects.link(curve_obj)
        bpy.context.view_layer.update()
        converted=bpy.data.meshes.new_from_object(curve_obj.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        obj=bpy.data.objects.new(f"Hair_short_flyaway_{index}",converted);bpy.context.scene.collection.objects.link(obj)
        obj.data.materials.append(clay);bpy.data.objects.remove(curve_obj,do_unlink=True);candidate.append(obj)
    scene=bpy.context.scene
    report={"status":"FITTED_NEUTRAL_TRIAL_REQUIRES_VISUAL_REVIEW","hair_source_sha256":HAIR_HASH,"head_source_sha256":HEAD_HASH,
            "coordinates":"Head source world frame; +X front +Z up; exported hair uses identity transform. Old (-.09,0,.22) transform is comparison only.",
            "native_hair_faces_retained":0,"fit_changes":changes,"root_base":"Smooth ray-projected scalp crown; high front border recessed beneath clumps; no head edits.",
            "renders":[],"objects":[],"exports":[]}
    report["head_vertex_sha256"]=head_hash
    report["reconstruction_workspace_sha256"]=digest(workspace)
    report["references"]=[{"path":path.relative_to(ROOT).as_posix(),"sha256":digest(path)} for path in [
        ROOT/"art/characters/ren-model-sheet.png",
        *[(ROOT/"art/generated/characters/ren/parts-reference-v1/hair/generation-inputs"/name) for name in
          ("hair-front-v2.png","hair-right-v2.png","hair-back-v2.png")]]]
    report["uv_status"]="Neutral geometry only. Clumps retain overlapping longitudinal working UV coordinates; flyaways have automatic curve UVs; root base has no UVs. A packed painting atlas is still required."
    source.location=offset
    for stage in ("source","candidate"):
        source.hide_render=stage!="source"
        for obj in candidate:obj.hide_render=stage!="candidate"
        head.hide_render=True
        report["renders"]+=render_views(scene,directory,stage,target=(0,0,.12),scale=1.35)
        head.hide_render=False
        report["renders"]+=render_views(scene,directory,stage+"-assembled",target=(0,0,.08),scale=1.45)
    source.hide_render=True
    for obj in candidate:
        mesh=obj.data;mesh.calc_loop_triangles();incidence=np.zeros(len(mesh.edges),dtype=int)
        for loop in mesh.loops:incidence[loop.edge_index]+=1
        report["objects"].append({"name":obj.name,"vertices":len(mesh.vertices),"faces":len(mesh.polygons),"triangles":len(mesh.loop_triangles),
                                  "boundary_edges":int((incidence==1).sum()),"nonmanifold_edges":int((incidence>2).sum()),
                                  "uv_layers":[layer.name for layer in mesh.uv_layers],
                                  "location":list(obj.location),"scale":list(obj.scale)})
    assert vertex_hash(head.data)==head_hash and digest(HEAD)==HEAD_HASH and digest(HAIR)==HAIR_HASH
    report["head_vertices_and_native_sources_unchanged"]=True
    bpy.ops.object.select_all(action="DESELECT")
    for obj in candidate:obj.select_set(True)
    bpy.context.view_layer.objects.active=candidate[0]
    for extension in ("fbx","glb"):
        path=directory/("ren-fitted-hair-trial."+extension)
        if extension=="fbx":bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},use_mesh_modifiers=False,use_triangles=False,add_leaf_bones=False,bake_anim=False)
        else:bpy.ops.export_scene.gltf(filepath=str(path),export_format="GLB",use_selection=True,export_apply=False,export_yup=True,export_animations=False)
        report["exports"].append({"path":path.name,"sha256":digest(path),"bytes":path.stat().st_size})
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(directory/"fitted-hair-workspace.blend"))
    write_json(directory/"report.json",report)
    print("FITTED_HAIR_READY",flush=True)


def verify():
    """Reimport final exports in an isolated scene and check artifact integrity."""
    import bpy
    import numpy as np
    report=json.loads((FIT_DIRECTORY/"report.json").read_text(encoding="utf-8"))
    expected={key:sum(obj[key] for obj in report["objects"]) for key in ("vertices","faces","triangles")}
    for render in report["renders"]:assert digest(OUTPUT/render["path"])==render["sha256"]
    results=[]
    for export in report["exports"]:
        path=FIT_DIRECTORY/export["path"];assert digest(path)==export["sha256"]
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if path.suffix==".fbx":bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
        else:bpy.ops.import_scene.gltf(filepath=str(path))
        objects=[obj for obj in bpy.context.scene.objects if obj.type=="MESH"]
        assert len(objects)==len(report["objects"])
        counts={key:0 for key in expected};points=[]
        for obj in objects:
            assert "Head" not in obj.name
            mesh=obj.data;mesh.calc_loop_triangles()
            counts["vertices"]+=len(mesh.vertices);counts["faces"]+=len(mesh.polygons);counts["triangles"]+=len(mesh.loop_triangles)
            points.extend(tuple(obj.matrix_world@v.co) for v in mesh.vertices)
        assert counts["triangles"]==expected["triangles"]
        assert counts["faces"]==(expected["faces"] if path.suffix==".fbx" else expected["triangles"])
        points=np.array(points);assert np.isfinite(points).all()
        results.append({"file":path.name,"sha256":digest(path),"objects":len(objects),**counts,
                        "bounds_min":points.min(axis=0).tolist(),"bounds_max":points.max(axis=0).tolist(),
                        "objects_with_uv":sum(bool(obj.data.uv_layers) for obj in objects),
                        "objects_with_material":sum(bool(obj.data.materials) for obj in objects)})
    assert np.allclose(results[0]["bounds_min"],results[1]["bounds_min"],atol=1e-5)
    assert np.allclose(results[0]["bounds_max"],results[1]["bounds_max"],atol=1e-5)
    assert digest(HAIR)==HAIR_HASH and digest(HEAD)==HEAD_HASH
    bpy.ops.wm.read_factory_settings(use_empty=True)
    native_head=import_mesh(HEAD);native_head_vertices=vertex_hash(native_head.data)
    native_head_matrix=[list(row) for row in native_head.matrix_world]
    native_hair=import_mesh(HAIR);ranks=ranked_components(native_hair.data)
    cache=ROOT/"art/generated/characters/ren/parts-workflow-v1/audits/p2-hair-native/analysis-data/mesh-000-component-labels.npz"
    if cache.exists():
        cached=np.load(cache)["vertex_component"]
        assert np.array_equal(ranks[ranks<30],cached[ranks<30])
    bpy.ops.wm.open_mainfile(filepath=str(FIT_DIRECTORY/"fitted-hair-workspace.blend"))
    review_head=bpy.data.objects["Head_clearance_readonly"]
    assert vertex_hash(review_head.data)==native_head_vertices
    assert np.array_equal(native_head_matrix,[list(row) for row in review_head.matrix_world])
    refs=[ROOT/"art/characters/ren-model-sheet.png",*[
        ROOT/"art/generated/characters/ren/parts-reference-v1/hair/generation-inputs"/name
        for name in ("hair-front-v2.png","hair-right-v2.png","hair-back-v2.png")]]
    write_json(FIT_DIRECTORY/"export-verification.json",{"status":"PASS","blender_version":bpy.app.version_string,
        "native_sources_unchanged":True,"render_hashes_checked":len(report["renders"]),"expected_counts":expected,"reimported_exports":results,
        "head_local_vertices_and_world_matrix_unchanged":True,"native_and_review_head_vertex_sha256":native_head_vertices,
        "native_component_ranking_recomputed_without_cache":True,
        "references":[{"path":path.relative_to(ROOT).as_posix(),"sha256":digest(path)} for path in refs],
        "uv_status":"34 main clumps use overlapping longitudinal working coordinates; two flyaways have automatic curve UVs; root base lacks UVs. No accepted painting atlas.",
        "scope":"Checks integrity/counts/finite coordinates/export framing; does not establish likeness, UV acceptance, collision-free assembly, or mobile performance."})
    print("FITTED_EXPORT_VERIFICATION_PASS",flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage",choices=["diagnose","prune","reconstruct","fit","verify"],default="diagnose")
    parser.add_argument("--blender-phase",choices=["diagnose","prune","reconstruct","fit","verify"])
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else None)
    if args.blender_phase:
        {"diagnose":diagnose,"prune":prune,"reconstruct":reconstruct,"fit":fit,"verify":verify}[args.blender_phase]();return
    OUTPUT.mkdir(parents=True,exist_ok=True)
    settings = OUTPUT/".blender-user"
    env = os.environ.copy()
    for key,relative in (("BLENDER_USER_RESOURCES",""),("BLENDER_USER_CONFIG","config"),
                         ("BLENDER_USER_SCRIPTS","scripts"),("BLENDER_USER_EXTENSIONS","extensions")):
        path = settings/relative;path.mkdir(parents=True,exist_ok=True);env[key]=str(path)
    command=[BLENDER,"--background","--factory-startup","--offline-mode","--threads","8",
             "--python-exit-code","1","--python",str(Path(__file__).resolve()),"--","--blender-phase",args.stage]
    with (OUTPUT/(args.stage+"-blender.log")).open("w",encoding="utf-8") as log:
        process=subprocess.Popen(command,env=env,stdout=log,stderr=subprocess.STDOUT,stdin=subprocess.DEVNULL,
                                  creationflags=subprocess.CREATE_NO_WINDOW if os.name=="nt" else 0)
        while process.poll() is None:
            try:process.wait(timeout=30)
            except subprocess.TimeoutExpired:print("Hair",args.stage,"running; own PID",process.pid,flush=True)
    if process.returncode:raise RuntimeError(f"Blender {args.stage} failed; see hair-cleanup-v1/{args.stage}-blender.log")
    report_directory={"diagnose":"diagnosis","prune":"pruning","reconstruct":"reconstruction","fit":FIT_DIRECTORY.name,"verify":FIT_DIRECTORY.name}[args.stage]
    print("HAIR_CLEANUP_READY",str(OUTPUT/report_directory/"report.json"),flush=True)


if __name__=="__main__":
    main()
