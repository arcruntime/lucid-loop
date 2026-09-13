"""Non-destructive topology and render audit for Ren FBX/native-quads or GLB.

Run with ordinary Python (NumPy/SciPy installed):
  python tools/character_art/audit_ren_parts.py --source model.fbx --id p2-head

The launcher uses isolated Blender 5.1.1 background processes for import/render,
and ordinary Python for sparse-graph analysis. All writes stay below the audits
directory. Source geometry is never fitted, decimated, welded, or overwritten.
Coincident-position welding is an analysis-only graph, not a mesh operation.
"""
from __future__ import annotations

import argparse
import colorsys
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time


ROOT = Path(__file__).resolve().parents[2]
AUDITS = ROOT / "art/generated/characters/ren/parts-workflow-v1/audits"
BLENDER = Path("C:/Program Files/Blender Foundation/Blender 5.1/blender.exe")
VIEWS = {"front": 0.0, "three-quarter": -35.0, "profile": -90.0}


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(data, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--id", required=True)
    parser.add_argument("--blender", type=Path, default=BLENDER)
    parser.add_argument("--resolution", type=int, default=1024)
    parser.add_argument("--front-yaw", type=float, default=0,
                        help="Camera azimuth around Blender +Z; zero views from -Y")
    parser.add_argument("--position-epsilon", type=float, default=1e-6,
                        help="Coincidence grid cell width as fraction of whole-source max extent")
    parser.add_argument("--uv-epsilon", type=float, default=1e-6)
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--rerender", action="store_true",
                        help="With --resume, preserve previous PNGs locally and explicitly replace review camera views")
    parser.add_argument("--skip-render", action="store_true")
    parser.add_argument("--phase", choices=("extract", "render"), help=argparse.SUPPRESS)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", args.id):
        parser.error("id must be a simple filesystem-safe name")
    args.source = args.source.resolve()
    args.output = (AUDITS / args.id).resolve()
    if not args.output.is_relative_to(AUDITS.resolve()):
        parser.error("output must stay within the Ren audits directory")
    if args.source.suffix.lower() not in {".fbx", ".glb", ".gltf"} or not args.source.is_file():
        parser.error("source must be an existing FBX, GLB, or glTF")
    if not 256 <= args.resolution <= 2048 or not 0 < args.position_epsilon <= 1e-3:
        parser.error("resolution must be 256–2048; position epsilon must be (0, 0.001]")
    if not 0 < args.uv_epsilon <= 1e-3:
        parser.error("UV epsilon must be (0, 0.001]")
    if args.rerender and (not args.resume or args.skip_render):
        parser.error("--rerender requires --resume and cannot be combined with --skip-render")
    return args


def launch_blender(args, phase):
    settings = args.output / ".blender-user"
    environment = os.environ.copy()
    for key, relative in (("BLENDER_USER_RESOURCES", ""), ("BLENDER_USER_CONFIG", "config"),
                          ("BLENDER_USER_SCRIPTS", "scripts"), ("BLENDER_USER_EXTENSIONS", "extensions")):
        directory = settings / relative
        directory.mkdir(parents=True, exist_ok=True)
        environment[key] = str(directory)
    command = [str(args.blender.resolve()), "--background", "--factory-startup", "--offline-mode",
               "--threads", "8", "--python-exit-code", "1", "--python", str(Path(__file__).resolve()),
               "--", "--source", str(args.source), "--id", args.id, "--phase", phase,
               "--resolution", str(args.resolution), "--front-yaw", str(args.front_yaw)]
    write_json(args.output / (phase + "-command.json"), command)
    with (args.output / (phase + "-blender.log")).open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, env=environment, stdout=log, stderr=subprocess.STDOUT,
                                   stdin=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        while True:
            try:
                code = process.wait(timeout=30)
                break
            except subprocess.TimeoutExpired:
                print(f"Blender {phase} running; own PID {process.pid}", flush=True)
    if code:
        raise RuntimeError(f"Blender {phase} failed with exit {code}; see {phase}-blender.log")


def foreach(collection, field, dtype, width=1):
    import numpy as np
    data = np.empty(len(collection) * width, dtype=dtype)
    collection.foreach_get(field, data)
    return data.reshape((-1, width)) if width > 1 else data


def bounds(points):
    return {"min": points.min(axis=0).tolist(), "max": points.max(axis=0).tolist(),
            "center": ((points.min(axis=0) + points.max(axis=0)) * 0.5).tolist()}


def setup_scene(args, whole_bounds):
    import bpy
    import numpy as np
    from mathutils import Vector
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = args.resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.5
    scene.world = bpy.data.worlds.new("AUDIT_World")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (.045, .05, .06, 1)
    background.inputs["Strength"].default_value = .35
    camera_data = bpy.data.cameras.new("AUDIT_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("AUDIT_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    extent = float(max(np.array(whole_bounds["max"]) - whole_bounds["min"]))
    center = Vector(whole_bounds["center"])
    camera_data.ortho_scale = extent * 1.15
    camera_data.clip_start = max(extent * .00001, .000001)
    camera_data.clip_end = max(extent * 20, 10)
    for name, offset, power in (("Key", (-2, -3, 3), 220), ("Fill", (2, -2, .8), 80),
                                ("Rim", (1, 2, 2), 140)):
        data = bpy.data.lights.new("AUDIT_" + name, "AREA")
        data.energy, data.size = power * extent * extent, extent * 1.5
        light = bpy.data.objects.new(data.name, data)
        scene.collection.objects.link(light)
        light.location = center + Vector(offset) * extent
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    clay = bpy.data.materials.new("AUDIT_Clay")
    clay.use_fake_user = True
    clay.use_nodes = True
    shader = clay.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (.32, .36, .4, 1)
    shader.inputs["Roughness"].default_value = .8
    scene["audit_extent"] = extent
    scene["audit_center"] = list(center)


def extract(args):
    import bpy
    import numpy as np
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source_hash = sha256(args.source)
    if args.source.suffix.lower() == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(args.source), use_anim=False,
                                 automatic_bone_orientation=False)
    else:
        bpy.ops.import_scene.gltf(filepath=str(args.source), merge_vertices=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("No mesh objects imported")
    data_dir = args.output / "analysis-data"
    data_dir.mkdir(parents=True, exist_ok=True)
    records, minima, maxima = [], [], []
    for index, obj in enumerate(meshes):
        mesh = obj.data
        points = foreach(mesh.vertices, "co", np.float64, 3)
        matrix = np.array(obj.matrix_world, dtype=np.float64)
        points = points @ matrix[:3, :3].T + matrix[:3, 3]
        if not np.isfinite(points).all() or not len(points):
            raise RuntimeError(f"Mesh {obj.name} has empty or nonfinite vertex positions")
        arrays = {"positions": points, "edges": foreach(mesh.edges, "vertices", np.int32, 2),
                  "loop_vertex": foreach(mesh.loops, "vertex_index", np.int32),
                  "loop_edge": foreach(mesh.loops, "edge_index", np.int32),
                  "face_start": foreach(mesh.polygons, "loop_start", np.int32),
                  "face_size": foreach(mesh.polygons, "loop_total", np.int32),
                  "face_material": foreach(mesh.polygons, "material_index", np.int32)}
        mesh.calc_loop_triangles()  # Cache only: polygons/quads remain unchanged.
        triangle_faces = foreach(mesh.loop_triangles, "polygon_index", np.int32)
        arrays["face_triangles"] = np.bincount(triangle_faces, minlength=len(mesh.polygons)).astype(np.int32)
        uv_layers = []
        for uv_index, layer in enumerate(mesh.uv_layers):
            arrays[f"uv_{uv_index}"] = foreach(layer.data, "uv", np.float64, 2)
            uv_layers.append({"name": layer.name, "array": f"uv_{uv_index}"})
        relative = f"analysis-data/mesh-{index:03d}.npz"
        np.savez(args.output / relative, **arrays)
        local_bounds = bounds(points)
        minima.append(local_bounds["min"]); maxima.append(local_bounds["max"])
        record = {"index": index, "object": obj.name, "mesh_datablock": mesh.name,
                  "array_file": relative, "vertices": len(mesh.vertices), "edges": len(mesh.edges),
                  "faces": len(mesh.polygons), "triangles": len(mesh.loop_triangles),
                  "face_types": {str(int(n)): int(c) for n, c in zip(*np.unique(arrays["face_size"], return_counts=True))},
                  "bounds_world": local_bounds, "matrix_world": matrix.tolist(),
                  "materials": [m.name if m else None for m in mesh.materials], "uv_layers": uv_layers,
                  "shape_keys": [key.name for key in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
                  "modifiers": [{"name": mod.name, "type": mod.type} for mod in obj.modifiers]}
        records.append(record)
        obj["audit_mesh_index"] = index
        print(f"EXTRACTED {obj.name}: {record['faces']} faces / {record['triangles']} triangles", flush=True)
    materials = []
    for material in bpy.data.materials:
        textures = []
        if material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    im = node.image
                    textures.append({"image": im.name, "size": list(im.size), "packed": bool(im.packed_file),
                                     "filepath": bpy.path.abspath(im.filepath),
                                     "color_space": im.colorspace_settings.name})
        materials.append({"name": material.name, "uses_nodes": material.use_nodes, "images": textures})
    whole = bounds(np.array(minima + maxima))
    metadata = {"source": str(args.source), "source_sha256": source_hash,
                "source_bytes": args.source.stat().st_size, "blender_version": bpy.app.version_string,
                "format": args.source.suffix.lower(), "objects": records, "materials": materials,
                "bounds_world": whole, "vertex_policy": "Unmodified imported base mesh; modifiers not applied",
                "glb_note": "GLB triangle primitives and seam-duplicated vertices do not prove the provider's native FBX topology."}
    write_json(args.output / "import-metadata.json", metadata)
    setup_scene(args, whole)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output / "audit-scene.blend"))
    if sha256(args.source) != source_hash:
        raise RuntimeError("Source hash changed during import")


def graph_labels(node_count, edges):
    import numpy as np
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    if not node_count:
        return 0, np.empty(0, dtype=np.int32)
    graph = coo_matrix((np.ones(len(edges), dtype=np.uint8), (edges[:, 0], edges[:, 1])),
                       shape=(node_count, node_count)).tocsr()
    return connected_components(graph, directed=False)


def sorted_components(labels, points, face_labels, face_triangles, face_material):
    import numpy as np
    count = int(labels.max()) + 1 if len(labels) else 0
    vertices = np.bincount(labels, minlength=count)
    faces = np.bincount(face_labels, minlength=count)
    triangles = np.bincount(face_labels, weights=face_triangles, minlength=count)
    minimum = np.full((count, 3), np.inf); maximum = np.full((count, 3), -np.inf)
    for axis in range(3):
        np.minimum.at(minimum[:, axis], labels, points[:, axis])
        np.maximum.at(maximum[:, axis], labels, points[:, axis])
    rows = []
    order = np.argsort(-faces, kind="stable")
    rank = np.empty(count, dtype=np.int32); rank[order] = np.arange(count)
    material_counts = {}
    if len(face_labels):
        pairs, amounts = np.unique(np.stack([face_labels, face_material], axis=1), axis=0, return_counts=True)
        for (component, material), amount in zip(pairs, amounts):
            material_counts.setdefault(int(component), {})[str(int(material))] = int(amount)
    for position, component in enumerate(order):
        rows.append({"rank": position, "graph_id": int(component), "vertices": int(vertices[component]),
                     "faces": int(faces[component]), "triangles": int(triangles[component]),
                     "min": minimum[component].tolist(), "max": maximum[component].tolist(),
                     "material_face_counts": material_counts.get(int(component), {})})
    return rows, rank[labels]


def save_component_csv(path, rows):
    with path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream)
        writer.writerow(["rank", "graph_id", "vertices", "faces", "triangles", "min_x", "min_y", "min_z",
                         "max_x", "max_y", "max_z", "material_face_counts"])
        for row in rows:
            writer.writerow([row["rank"], row["graph_id"], row["vertices"], row["faces"], row["triangles"],
                             *row["min"], *row["max"], json.dumps(row["material_face_counts"], sort_keys=True)])


def face_next_loops(starts, sizes, loop_count):
    import numpy as np
    following = np.arange(loop_count, dtype=np.int32) + 1
    following[starts + sizes - 1] = starts
    return following


def uv_audit(data, layer, vertex_position_ids, epsilon, output, prefix):
    import numpy as np
    uv = data[layer["array"]]
    if not np.isfinite(uv).all():
        return {"name": layer["name"], "nonfinite_corners": int((~np.isfinite(uv)).any(axis=1).sum()),
                "islands": None, "note": "Invalid UV coordinates prevent reliable island analysis"}
    starts, sizes = data["face_start"], data["face_size"]
    if not len(starts):
        return {"name": layer["name"], "islands": 0}
    following = face_next_loops(starts, sizes, len(uv))
    # UV corners are identified by position coincidence AND UV coincidence.
    keys = np.column_stack((vertex_position_ids[data["loop_vertex"]], np.rint(uv / epsilon).astype(np.int64)))
    _, corner_node = np.unique(keys, axis=0, return_inverse=True)
    edge_nodes = np.sort(np.column_stack((corner_node, corner_node[following])), axis=1)
    _, edge_id, multiplicity = np.unique(edge_nodes, axis=0, return_inverse=True, return_counts=True)
    face_id = np.repeat(np.arange(len(starts), dtype=np.int32), sizes)
    order = np.argsort(edge_id, kind="stable")
    same_edge = edge_id[order[1:]] == edge_id[order[:-1]]
    face_pairs = np.column_stack((face_id[order[:-1][same_edge]], face_id[order[1:][same_edge]]))
    count, labels = graph_labels(len(starts), face_pairs)
    island_faces = np.bincount(labels, minlength=count)
    loop_labels = labels[face_id]
    minimum = np.full((count, 2), np.inf); maximum = np.full((count, 2), -np.inf)
    for axis in range(2):
        np.minimum.at(minimum[:, axis], loop_labels, uv[:, axis])
        np.maximum.at(maximum[:, axis], loop_labels, uv[:, axis])
    signed_cross = uv[:, 0] * uv[following, 1] - uv[following, 0] * uv[:, 1]
    areas = np.abs(np.add.reduceat(signed_cross, starts)) * .5
    island_area = np.bincount(labels, weights=areas, minlength=count)
    ordered = np.argsort(-island_faces, kind="stable")
    filename = f"{prefix}-uv-{layer['array']}-islands.csv"
    with (output / filename).open("w", newline="", encoding="utf-8") as stream:
        writer = csv.writer(stream); writer.writerow(["rank", "island_id", "faces", "summed_uv_area", "min_u", "min_v", "max_u", "max_v"])
        for rank, island in enumerate(ordered):
            writer.writerow([rank, int(island), int(island_faces[island]), float(island_area[island]),
                             *minimum[island], *maximum[island]])
    return {"name": layer["name"], "islands": int(count), "island_table": filename,
            "single_face_islands": int((island_faces == 1).sum()),
            "largest_island_faces": int(island_faces.max()),
            "largest_island_face_fraction": float(island_faces.max() / len(starts)),
            "boundary_uv_edges": int((multiplicity == 1).sum()),
            "nonmanifold_uv_edges": int((multiplicity > 2).sum()),
            "degenerate_uv_faces": int((areas <= 1e-12).sum()),
            "degenerate_uv_area_threshold": 1e-12,
            "corners_outside_unit_square": int(((uv < -epsilon) | (uv > 1 + epsilon)).any(axis=1).sum()),
            "summed_absolute_face_uv_area": float(areas.sum()),
            "note": "Edge-connected islands; area is not overlap-free coverage; no overlap/distortion acceptance implied"}


def analyze(args):
    import numpy as np
    metadata = json.loads((args.output / "import-metadata.json").read_text(encoding="utf-8"))
    extent = max(np.array(metadata["bounds_world"]["max"]) - metadata["bounds_world"]["min"])
    epsilon = max(float(extent * args.position_epsilon), 1e-12)
    results = []
    for mesh in metadata["objects"]:
        print(f"Analyzing {mesh['object']}: {mesh['faces']} faces", flush=True)
        with np.load(args.output / mesh["array_file"]) as archive:
            data = {key: archive[key] for key in archive.files}
        points, edges = data["positions"], data["edges"]
        starts, sizes, loops = data["face_start"], data["face_size"], data["loop_vertex"]
        prefix = f"mesh-{mesh['index']:03d}"
        raw_count, raw_labels = graph_labels(len(points), edges)
        raw_rows, _ = sorted_components(raw_labels, points, raw_labels[loops[starts]],
                                        data["face_triangles"], data["face_material"])
        save_component_csv(args.output / (prefix + "-imported-components.csv"), raw_rows)
        _, vertex_position_ids = np.unique(np.rint(points / epsilon).astype(np.int64), axis=0, return_inverse=True)
        position_count = int(vertex_position_ids.max()) + 1
        welded_count, position_labels = graph_labels(position_count, vertex_position_ids[edges])
        welded_labels = position_labels[vertex_position_ids]
        welded_rows, ranked_vertex_labels = sorted_components(welded_labels, points, welded_labels[loops[starts]],
                                                               data["face_triangles"], data["face_material"])
        save_component_csv(args.output / (prefix + "-coincident-components.csv"), welded_rows)
        np.savez_compressed(args.output / "analysis-data" / (prefix + "-component-labels.npz"),
                            vertex_component=ranked_vertex_labels.astype(np.int32))
        incidence = np.bincount(data["loop_edge"], minlength=len(edges))
        following = face_next_loops(starts, sizes, len(loops)) if len(starts) else np.empty(0, dtype=np.int32)
        welded_edges = np.sort(np.column_stack((vertex_position_ids[loops], vertex_position_ids[loops[following]])), axis=1)
        nonzero = welded_edges[:, 0] != welded_edges[:, 1]
        _, welded_incidence = np.unique(welded_edges[nonzero], axis=0, return_counts=True)
        result = {**mesh, "imported_components": int(raw_count), "coincident_position_components": int(welded_count),
                  "coincident_unique_positions": position_count, "position_grid_world_units": epsilon,
                  "imported_top_components": raw_rows[:24], "coincident_top_components": welded_rows[:24],
                  "imported_components_table": prefix + "-imported-components.csv",
                  "coincident_components_table": prefix + "-coincident-components.csv",
                  "imported_edges": {"boundary": int((incidence == 1).sum()), "nonmanifold": int((incidence > 2).sum()),
                                     "loose": int((incidence == 0).sum())},
                  "coincident_surface_edges": {"boundary": int((welded_incidence == 1).sum()),
                                                "nonmanifold": int((welded_incidence > 2).sum()),
                                                "collapsed_corners": int((~nonzero).sum())},
                  "uv_analysis": [uv_audit(data, layer, vertex_position_ids, args.uv_epsilon, args.output, prefix)
                                  for layer in mesh["uv_layers"]]}
        results.append(result)
        print(f"COMPONENTS {mesh['object']}: imported={raw_count}, position-coincident={welded_count}", flush=True)
    report = {"schema": "lucid-loop/ren-parts-audit/v1", "source": metadata["source"],
              "source_sha256": metadata["source_sha256"], "source_bytes": metadata["source_bytes"],
              "blender_version": metadata["blender_version"], "source_format": metadata["format"],
              "objects": results, "materials": metadata["materials"], "bounds_world": metadata["bounds_world"],
              "totals": {field: sum(obj[field] for obj in results) for field in
                         ("vertices", "edges", "faces", "triangles", "imported_components", "coincident_position_components")},
              "camera": {"up": "+Z", "front_at_zero": "-Y", "front_yaw_degrees": args.front_yaw,
                         "views": VIEWS, "resolution": args.resolution},
              "limitations": ["Counts are imported base topology per object instance; modifiers and rigs are not applied.",
                              "Position coincidence is a quantized analysis graph, not a geometry edit or proof of semantic parts.",
                              "Objects are analyzed independently; touching parts may coincide and separate surfaces may intersect.",
                              "Component colors show coincidence connectivity, not automatically identified head/hair/eyes.",
                              "Quad percentage alone does not establish eyelid/mouth loops, deformation quality, or mobile readiness.",
                              "GLB triangle topology cannot certify native FBX quads; compare the actual FBX import.",
                              "UV counts do not prove absence of overlap or acceptable distortion/texel density."]}
    write_json(args.output / "report.json", report)
    return report


def render(args):
    import bpy
    import numpy as np
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(args.output / "audit-scene.blend"))
    metadata = json.loads((args.output / "import-metadata.json").read_text(encoding="utf-8"))
    scene = bpy.context.scene
    camera = scene.camera
    scene.render.resolution_x = scene.render.resolution_y = args.resolution
    center, extent = Vector(scene["audit_center"]), float(scene["audit_extent"])
    # Older extraction checkpoints can lack an unassigned material: Blender
    # drops datablocks with no users when the isolated scene is saved/reopened.
    clay = bpy.data.materials.get("AUDIT_Clay")
    if clay is None:
        clay = bpy.data.materials.new("AUDIT_Clay")
        clay.use_nodes = True
        shader = clay.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (.32, .36, .4, 1)
        shader.inputs["Roughness"].default_value = .8
    components = bpy.data.materials.new("AUDIT_ComponentColors")
    components.use_nodes = True
    shader = components.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .8
    attr = components.node_tree.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "AUDIT_COMPONENT_COLOR"
    components.node_tree.links.new(attr.outputs["Color"], shader.inputs["Base Color"])
    for record in metadata["objects"]:
        obj = bpy.data.objects[record["object"]]
        with np.load(args.output / "analysis-data" / f"mesh-{record['index']:03d}-component-labels.npz") as labels:
            ids = labels["vertex_component"]
        palette = np.array([(*colorsys.hsv_to_rgb((.58 + (rank + record['index'] * 17) * .618034) % 1, .62, .66), 1)
                            for rank in range(int(ids.max()) + 1)], dtype=np.float32)
        color = obj.data.color_attributes.new(name="AUDIT_COMPONENT_COLOR", type="FLOAT_COLOR", domain="POINT")
        color.data.foreach_set("color", palette[ids].ravel())
    renders = []
    for mode, override in (("neutral", None), ("clay", clay),
                           ("components", components)):
        scene.view_layers[0].material_override = override
        for view, offset in VIEWS.items():
            angle = math.radians(args.front_yaw + offset)
            camera.location = center + Vector((math.sin(angle), -math.cos(angle), 0)) * extent * 3
            camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
            bpy.context.view_layer.update()
            path = args.output / f"{mode}-{view}.png"
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            renders.append({"path": path.name, "sha256": sha256(path), "mode": mode, "view": view})
            print("RENDERED", path.name, flush=True)
    scene.view_layers[0].material_override = None
    write_json(args.output / "renders.json", renders)


def write_readme(args, report):
    lines = [f"# Ren parts audit: {args.id}", "", f"Source: `{report['source']}`", "",
             f"SHA-256: `{report['source_sha256']}`", "",
             f"Blender {report['blender_version']}; original source hash checked before and after.", "",
             "| Object | Vertices | Faces | Triangles | Face types (corners: count) | Imported components | Coincident components |",
             "| --- | ---: | ---: | ---: | --- | ---: | ---: |"]
    for obj in report["objects"]:
        lines.append(f"| {obj['object']} | {obj['vertices']:,} | {obj['faces']:,} | {obj['triangles']:,} | "
                     f"{json.dumps(obj['face_types'])} | {obj['imported_components']:,} | {obj['coincident_position_components']:,} |")
    lines.extend(["", "The source mesh was not welded, decimated, fitted, or rebuilt. The second component count joins only "
                  "quantized coincident positions in an analysis graph, accounting for many GLB UV-seam duplicates. "
                  "It can also join touching parts; it does not identify anatomical regions.", "",
                  "Neutral renders use imported materials under a fixed light setup. Clay removes material appearance. "
                  "Component colors show position-coincidence connectivity; different colors do not automatically mean separate anatomical parts.", ""])
    if report.get("renders"):
        lines.extend(["| Pass | Front | Three-quarter | Profile |", "| --- | --- | --- | --- |"])
        for mode in ("neutral", "clay", "components"):
            lines.append(f"| {mode} | [front]({mode}-front.png) | [three-quarter]({mode}-three-quarter.png) | [profile]({mode}-profile.png) |")
    else:
        lines.append("Rendering was skipped for this audit.")
    lines.extend(["", "| Object / UV layer | UV islands | Largest island (% of faces) | Near-zero UV faces |",
                  "| --- | ---: | ---: | ---: |"])
    for obj in report["objects"]:
        for layer in obj["uv_analysis"]:
            if layer.get("islands") is not None and "largest_island_face_fraction" in layer:
                lines.append(f"| {obj['object']} / {layer['name']} | {layer['islands']:,} | "
                             f"{100 * layer['largest_island_face_fraction']:.2f}% | {layer['degenerate_uv_faces']:,} |")
    lines.extend(["", "Near-zero UV faces have absolute polygon UV area <= 1e-12. Summed UV areas are not "
                  "overlap-free atlas coverage; island counts do not establish projection quality."])
    lines.extend(["", "Open `audit-scene.blend` for the isolated imported scene. `report.json` holds full statistics and limitations; "
                  "CSV tables enumerate component bounds/counts and UV islands. `analysis-data/` contains read-only extracted arrays and labels "
                  "for reproducible graph analysis. These files are audit copies, not edited replacement assets.", "", "## Limits", ""])
    lines.extend("- " + item for item in report["limitations"])
    (args.output / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    args = parse_args()
    if args.phase:
        extract(args) if args.phase == "extract" else render(args)
        return
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args.output.mkdir(parents=True, exist_ok=True)
    source_hash = sha256(args.source)
    run_path = args.output / "run.json"
    if run_path.exists():
        saved = json.loads(run_path.read_text(encoding="utf-8"))
        if not args.resume or saved["source_sha256"] != source_hash:
            raise RuntimeError("Existing audit requires --resume with the same source hash; use a new id for another source")
        if saved.get("position_epsilon") != args.position_epsilon or saved.get("uv_epsilon") != args.uv_epsilon:
            raise RuntimeError("Analysis tolerances differ from existing audit; use a new id")
        if (saved.get("front_yaw") != args.front_yaw or saved.get("resolution") != args.resolution) and not args.rerender:
            raise RuntimeError("Camera settings differ from existing audit; use a new id")
    else:
        write_json(run_path, {"source": str(args.source), "source_sha256": source_hash, "id": args.id,
                             "position_epsilon": args.position_epsilon, "uv_epsilon": args.uv_epsilon,
                             "resolution": args.resolution, "front_yaw": args.front_yaw})
    if not (args.output / "import-metadata.json").is_file() or not (args.output / "audit-scene.blend").is_file():
        launch_blender(args, "extract")
    report = analyze(args) if not (args.output / "report.json").is_file() else json.loads((args.output / "report.json").read_text())
    if args.rerender and (args.output / "renders.json").is_file():
        archive = args.output / "previous-renders" / str(time.time_ns())
        archive.mkdir(parents=True)
        old_renders = json.loads((args.output / "renders.json").read_text())
        for item in old_renders:
            previous = (args.output / item["path"]).resolve()
            if not previous.is_relative_to(args.output):
                raise RuntimeError("Recorded render path escapes audit directory")
            if previous.is_file():
                shutil.copy2(previous, archive / previous.name)
        shutil.copy2(args.output / "renders.json", archive / "renders.json")
        write_json(archive / "camera.json", report["camera"])
    if not args.skip_render and (args.rerender or not (args.output / "renders.json").is_file()):
        launch_blender(args, "render")
    if sha256(args.source) != source_hash:
        raise RuntimeError("Original source hash changed during audit")
    report["source_hash_verified_unchanged"] = True
    if args.rerender and not args.skip_render:
        report.setdefault("previous_camera_settings", []).append(report["camera"])
        report["camera"] = {"up": "+Z", "front_at_zero": "-Y", "front_yaw_degrees": args.front_yaw,
                            "views": VIEWS, "resolution": args.resolution}
        saved = json.loads(run_path.read_text())
        saved.update(resolution=args.resolution, front_yaw=args.front_yaw)
        write_json(run_path, saved)
    report["renders"] = json.loads((args.output / "renders.json").read_text()) if (args.output / "renders.json").is_file() else []
    write_json(args.output / "report.json", report)
    write_readme(args, report)
    print("REN_PARTS_AUDIT_COMPLETE", str(args.output / "report.json"), flush=True)


if __name__ == "__main__":
    main()
