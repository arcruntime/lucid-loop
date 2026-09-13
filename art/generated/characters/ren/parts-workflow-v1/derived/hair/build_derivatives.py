"""Create unfitted source-equivalent hair derivatives and extra inspection views."""
import hashlib
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

directory = Path(__file__).resolve().parent
workflow = directory.parents[1]
workspace = workflow.parents[4]
source = workspace / "art/generated/characters/ren/parts-generation-v1/tripo/hair/originals/model.fbx"
audit = workflow / "audits/p2-hair-native"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


source_hash = digest(source)
assert source_hash == "9f8c2cb79c763bb048b4c0b62c058e2357802e86012badf0ce0a15f240a083c9"
bpy.ops.wm.open_mainfile(filepath=str(audit / "audit-scene.blend"))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
before = [(obj.name, len(obj.data.vertices), len(obj.data.polygons)) for obj in meshes]
bpy.ops.object.select_all(action="DESELECT")
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
obj_path = directory / "ren-p2-hair-native-quads.obj"
glb_path = directory / "ren-p2-hair-triangulated.glb"
bpy.ops.wm.obj_export(filepath=str(obj_path), export_selected_objects=True,
                      apply_modifiers=False, export_triangulated_mesh=False,
                      export_uv=True, export_normals=True, export_materials=True,
                      forward_axis="NEGATIVE_Z", up_axis="Y")
bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB",
                          use_selection=True, export_apply=False,
                          export_yup=True, export_animations=False)
after = [(obj.name, len(obj.data.vertices), len(obj.data.polygons)) for obj in meshes]
assert before == after
assert digest(source) == source_hash
exports = [path for path in (obj_path, obj_path.with_suffix(".mtl"), glb_path) if path.is_file()]
provenance = {"source": str(source), "source_sha256": source_hash,
              "blender_version": bpy.app.version_string,
              "source_unchanged": True, "imported_counts_unchanged_by_export": before == after,
              "geometry_changes": "None: no fitting, scale changes, retopology, decimation or weld",
              "coordinate_policy": "Imported Blender world transformed only into standard Y-up export coordinates; no artistic orientation change",
              "obj_note": "Original imported polygon sizes preserved, including native quads and triangles; geometry-only source has no texture images",
              "glb_note": "Local GLB export triangulates polygon primitives; UV/normal discontinuities may duplicate render vertices",
              "exports": [{"path": path.name, "bytes": path.stat().st_size, "sha256": digest(path)} for path in exports]}
(directory / "provenance.json").write_text(json.dumps(provenance, indent=2) + "\n")
scene = bpy.context.scene
clay = bpy.data.materials.get("AUDIT_Clay")
assert clay is not None
scene.view_layers[0].material_override = clay
scene.render.resolution_x = scene.render.resolution_y = 768
center, extent = Vector(scene["audit_center"]), float(scene["audit_extent"])
views = [("yaw-90", -90, 0), ("yaw90", 90, 0), ("yaw180", 180, 0),
         ("underside", -90, -72), ("front-underside", 90, -55)]
records = []
for label, yaw, elevation in views:
    azimuth, altitude = math.radians(yaw), math.radians(elevation)
    direction = Vector((math.sin(azimuth) * math.cos(altitude),
                        -math.cos(azimuth) * math.cos(altitude), math.sin(altitude)))
    scene.camera.location = center + direction * extent * 3
    scene.camera.rotation_euler = (center - scene.camera.location).to_track_quat("-Z", "Y").to_euler()
    path = audit / f"inspection-{label}.png"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    records.append({"path": path.name, "yaw_degrees": yaw, "elevation_degrees": elevation,
                    "sha256": digest(path), "pass": "clay", "source_sha256": source_hash})
    print("INSPECTION_RENDER", path.name, flush=True)
(audit / "inspection-renders.json").write_text(json.dumps(records, indent=2) + "\n")
assert digest(source) == source_hash
print("HAIR_DERIVATIVES_AND_INSPECTION_COMPLETE")
