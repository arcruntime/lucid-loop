"""Place the native Ren P2 head and hair without changing their mesh data.

Run in isolated Blender background mode. Placement is a review experiment;
neither this script nor a successful export establishes character acceptance.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "art/generated/characters/ren/parts-generation-v1/tripo"
OUTPUT = ROOT / "art/generated/characters/ren/parts-workflow-v1/assembly"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def mesh_digest(objects):
    state = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        state.append({"vertices": [list(v.co) for v in mesh.vertices],
                      "polygons": [list(p.vertices) for p in mesh.polygons],
                      "uv": [[list(v.uv) for v in layer.data] for layer in mesh.uv_layers]})
    return hashlib.sha256(json.dumps(state, separators=(",", ":")).encode()).hexdigest()


def material(name, color):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    bsdf = result.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = .85
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--id", default="placement-v1")
    parser.add_argument("--hair-scale", type=float, default=.85)
    parser.add_argument("--hair-position", type=float, nargs=3, default=(0, 0, .115))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if not args.id.replace("-", "").isalnum() or not .1 < args.hair_scale < 3:
        raise ValueError("Invalid placement identity or scale")
    output = OUTPUT / args.id
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    records, groups = {}, {}
    for role in ("head", "hair"):
        source = SOURCES / role / "originals/model.fbx"
        source_hash = digest(source)
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(source), use_anim=False, automatic_bone_orientation=False)
        imported = [obj for obj in bpy.data.objects if obj not in before]
        root = bpy.data.objects.new("Ren_" + role + "_placement", None)
        bpy.context.scene.collection.objects.link(root)
        for obj in imported:
            if obj.parent not in imported:
                obj.parent = root
        groups[role] = imported
        records[role] = {"source": source.relative_to(ROOT).as_posix(), "sha256": source_hash,
                         "imported_mesh_geometry_uv_sha256": mesh_digest(imported)}
        if role == "hair":
            root.scale = (args.hair_scale,) * 3
            root.location = args.hair_position
        records[role]["placement"] = {"position": list(root.location), "scale": list(root.scale),
                                       "euler_radians": list(root.rotation_euler)}
        clay = material("Study_" + role, (.4, .44, .49) if role == "head" else (.2, .23, .27))
        for obj in imported:
            if obj.type == "MESH":
                obj.data.materials.clear()
                obj.data.materials.append(clay)
                for face in obj.data.polygons:
                    face.material_index = 0

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("StudyWorld")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs[0].default_value = (.04, .045, .055, 1)
    scene.world.node_tree.nodes.get("Background").inputs[1].default_value = .5
    scene.view_settings.view_transform = "AgX"
    camera = bpy.data.objects.new("StudyCamera", bpy.data.cameras.new("StudyCamera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.55
    camera.data.clip_start = .001
    camera.data.clip_end = 20
    target = Vector((0, 0, .10))
    for name, position, power in (("Key", (2, -2, 3), 230), ("Fill", (2, 2, 1), 120), ("Rim", (-2, 1, 2), 180)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size = power, 2
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = position
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
    renders = []
    for stage in ("head", "assembled"):
        for obj in groups["hair"]:
            obj.hide_render = stage == "head"
        for label, degrees in (("front", 0), ("three-quarter", -35), ("profile", -90)):
            angle = math.radians(degrees)
            camera.location = target + Vector((3 * math.cos(angle), 3 * math.sin(angle), 0))
            camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
            path = output / (stage + "-" + label + ".png")
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            renders.append({"file": path.name, "sha256": digest(path)})
    for role in records:
        if mesh_digest(groups[role]) != records[role]["imported_mesh_geometry_uv_sha256"]:
            raise RuntimeError("Geometry or UV changed during placement")
        if digest(ROOT / records[role]["source"]) != records[role]["sha256"]:
            raise RuntimeError("Native source changed")
    scene["construction_status"] = "Rigid source placement study; untextured, unrigged, open-mouth source. No likeness approval."
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "ren-p2-parts-placement.blend"))
    evidence = {"status": "UNREVIEWED_PLACEMENT", "blender_version": bpy.app.version_string,
                "coordinates": "Blender Z up, source front +X; head source frame unchanged",
                "parts": records, "renders": renders,
                "camera": {"projection": "orthographic", "scale": 1.55, "target": list(target), "distance": 3},
                "method": "Only separate part object-parent transforms and clay material bindings. Mesh coordinates, polygons and UVs preserved exactly."}
    (output / "placement.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    print("REN_PARTS_PLACEMENT_READY " + str(output), flush=True)


if __name__ == "__main__":
    main()
