"""Freeze a Ren neutral geometry snapshot and capture a matched paint guide.

Run inside Blender; source blends and authored shape keys are never overwritten.
This captures guides and geometric evidence only. It never paints or projects.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
sys.path.insert(0, str(Path(__file__).resolve().parent))
import project_ren_head_texture as projection


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def neutral_material():
    material = bpy.data.materials.new("Ren_PaintGuide_UniformSkin")
    material.use_nodes = True
    color = [((v + .055) / 1.055) ** 2.4 for v in (.78, .72, .67)]
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = 1
    bsdf.inputs["Specular IOR Level"].default_value = 0
    material.diffuse_color = (*color, 1)
    return material


def arrays(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    local = np.array([v.co[:] for v in mesh.vertices], dtype=np.float64)
    matrix = np.array(obj.matrix_world, dtype=np.float64)
    world = (np.c_[local, np.ones(len(local))] @ matrix.T)[:, :3]
    tri = np.array([t.vertices[:] for t in mesh.loop_triangles], dtype=np.int32)
    cross = np.cross(world[tri[:, 1]] - world[tri[:, 0]],
                     world[tri[:, 2]] - world[tri[:, 0]])
    length = np.linalg.norm(cross, axis=1)
    valid = length > 0
    return world, tri[valid], cross[valid] / length[valid, None], int((~valid).sum())


def raw_signature(obj):
    """Same unmodified mesh/UV signature as projection helper, without rejecting seals."""
    mesh = obj.data
    geometry = hashlib.sha256()
    geometry.update(np.array([v.co[:] for v in mesh.vertices], dtype="<f8").tobytes())
    geometry.update(np.array([loop.vertex_index for loop in mesh.loops], dtype="<i4").tobytes())
    geometry.update(np.array([(p.loop_start, p.loop_total) for p in mesh.polygons], dtype="<i4").tobytes())
    uv = np.array([v.uv[:] for v in mesh.uv_layers.active.data], dtype="<f8")
    return dict(geometry_sha256=geometry.hexdigest(),
                uv_sha256=hashlib.sha256(uv.tobytes()).hexdigest(),
                world_matrix=np.array(obj.matrix_world).tolist(),
                uv_layer=mesh.uv_layers.active.name)


def degenerates(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    source_faces = mesh.attributes.get("source_face_id")
    source_vertices = mesh.attributes.get("source_vertex_id")
    rows = []
    for triangle in mesh.loop_triangles:
        points = [obj.matrix_world @ mesh.vertices[i].co for i in triangle.vertices]
        twice_area = (points[1] - points[0]).cross(points[2] - points[0]).length
        if twice_area >= 1e-12:
            continue
        polygon = mesh.polygons[triangle.polygon_index]
        face_id = source_faces.data[polygon.index].value if source_faces else None
        vertex_ids = [source_vertices.data[i].value for i in triangle.vertices] if source_vertices else None
        rows.append(dict(loop_triangle_index=triangle.index, polygon_index=polygon.index,
                         source_face_id=face_id, source_vertex_ids=vertex_ids,
                         source_scope="generated" if face_id is not None and face_id < 0 else "native",
                         material=mesh.materials[polygon.material_index].name,
                         twice_area=twice_area, exactly_zero_area=twice_area == 0,
                         local_vertices=[list(mesh.vertices[i].co) for i in triangle.vertices],
                         uv=[list(mesh.uv_layers.active.data[i].uv) for i in triangle.loops]))
    return dict(geometry_signature=raw_signature(obj), near_zero_triangles=rows,
                exactly_zero_count=sum(row["exactly_zero_area"] for row in rows),
                raster_skip_policy="Only exactly zero-area triangles omitted; raw mesh and UV remain unchanged.",
                hidden_region_policy="Zero-area contact strips have no visible coverage in this closed capture; no UV fill or extrapolation performed.")


def capture_static(head, spec, output):
    """Keep collapsed seal triangles in the snapshot; omit only their raster work."""
    try:
        result = projection.capture(head, [spec], output)
        result["strict_projection_compatible"] = True
        return result
    except ValueError as error:
        strict_error = str(error)
    output.mkdir(parents=True, exist_ok=True)
    world, triangles, normals, skipped = arrays(head)
    cam = projection.camera(spec)
    depth, ids, normal = projection.raster_view(world, triangles, normals, cam)
    visible = ids >= 0
    light = -cam[1][2] + cam[1][1] * .5 - cam[1][0] * .3
    light /= np.linalg.norm(light)
    shade = .25 + .65 * np.maximum(0, normal @ light)
    clay = np.repeat(shade[..., None], 4, axis=-1)
    clay[..., 3] = visible
    clay[~visible, :3] = 0
    projection.save_image(output / "front-clay.png", clay)
    projection.save_image(output / "front-normal.png", normal * .5 + .5, data=True)
    projection.save_image(output / "front-visibility.png", visible.astype(float), data=True)
    np.savez_compressed(output / "front-geometry.npz", depth=depth, triangle_id=ids,
                        world_geometric_normal=normal, raster_triangle_vertices=triangles)
    display = np.zeros(depth.shape)
    finite = depth[visible]
    display[visible] = 1 - (finite - finite.min()) / max(np.ptp(finite), 1e-9)
    projection.save_image(output / "front-depth-preview.png", display, data=True)
    result = dict(version=1, object=head.name, signature=raw_signature(head),
                  cameras=[dict(cam[5], geometry_file="front-geometry.npz",
                                geometry_file_sha256=digest(output / "front-geometry.npz"))],
                  renderer="existing projection helper rasterizer; zero-area triangles omitted from raster only",
                  source_scope="selected static head only; other objects are not occluders",
                  strict_projection_compatible=False, strict_capture_error=strict_error,
                  raster_only_excluded_degenerate_triangles=skipped,
                  triangle_id_scope="Index into raster_triangle_vertices stored in NPZ; not raw loop-triangle indices",
                  geometry_modified=False)
    write_json(output / "capture.json", result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path,
                        default=BASE / "face-integration-v1/Ren_P2_Face_Integrated.blend")
    parser.add_argument("--output", type=Path, default=BASE / "face-paint-v1")
    parser.add_argument("--reuse-source-snapshot", action="store_true",
                        help="Resume guide preparation from the existing immutable private snapshot")
    parser.add_argument("--audit-snapshot", action="store_true",
                        help="Audit collapsed contact triangles in the existing static snapshot without rendering")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source = args.input.resolve()
    output = args.output.resolve()
    if args.audit_snapshot:
        bpy.ops.wm.open_mainfile(filepath=str(output / "Ren_P2_PaintNeutral_Snapshot.blend"))
        report = degenerates(bpy.data.objects["Ren_Head_PaintSnapshot"])
        bpy.ops.wm.open_mainfile(filepath=str(output / "Ren_P2_Source_Integrated_Snapshot.blend"))
        original = bpy.data.objects["Ren_Head"]
        for row in report["near_zero_triangles"]:
            polygon = original.data.polygons[row["polygon_index"]]
            row["source_original_material"] = original.data.materials[polygon.material_index].name
        write_json(output / "geometry-capture/degenerate-contact-triangles.json", report)
        print("REN_PAINT_CONTACT_AUDIT", report["exactly_zero_count"], flush=True)
        return
    if source.parent == output or source == output / "Ren_P2_PaintNeutral_Snapshot.blend":
        raise ValueError("Use an isolated output directory distinct from the source")
    output.mkdir(parents=True, exist_ok=True)
    frozen_source = output / "Ren_P2_Source_Integrated_Snapshot.blend"
    if frozen_source.exists() and not args.reuse_source_snapshot:
        raise FileExistsError("Existing snapshot is immutable; choose a new output folder")
    initial_source_hash = digest(frozen_source) if frozen_source.exists() else digest(source)
    if not frozen_source.exists():
        shutil.copy2(source, frozen_source)
    if digest(frozen_source) != initial_source_hash:
        raise RuntimeError("Source changed while taking snapshot")
    bpy.ops.wm.open_mainfile(filepath=str(frozen_source))
    scene = bpy.context.scene
    source_shapes = {}
    for obj in scene.objects:
        if obj.type == "MESH" and obj.data.shape_keys:
            source_shapes[obj.name] = {k.name: k.value for k in obj.data.shape_keys.key_blocks}
            for key in obj.data.shape_keys.key_blocks:
                if key.name != "Basis":
                    key.value = 1.0 if key.name == "mouthSeal" else 0.0
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    static_records = []
    for obj in list(scene.objects):
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        evaluated_coords = np.array([v.co[:] for v in evaluated.data.vertices])
        mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True,
                                               depsgraph=depsgraph)
        mesh.name = obj.name + "_NeutralSnapshotMesh"
        actual = np.array([v.co[:] for v in mesh.vertices])
        if actual.shape != evaluated_coords.shape or not np.array_equal(actual, evaluated_coords):
            raise AssertionError("Static capture differs from evaluated geometry")
        obj.modifiers.clear()
        obj.data = mesh
        if obj.data.shape_keys:
            raise AssertionError("Static evaluated mesh unexpectedly retains shape keys")
        static_records.append(dict(object=obj.name, vertices=len(mesh.vertices),
                                   faces=len(mesh.polygons), evaluated_max_error=0.0))
    head = bpy.data.objects["Ren_Head"]
    head.name = "Ren_Head_PaintSnapshot"
    layer = head.data.uv_layers.get("FaceUV_v1")
    if layer is None:
        raise ValueError("Source has no FaceUV_v1")
    head.data.uv_layers.active = layer
    layer.active_render = True
    before_geometry = raw_signature(head)
    zero_area_report = degenerates(head)
    for row in zero_area_report["near_zero_triangles"]:
        row["source_original_material"] = row["material"]
    skin = neutral_material()
    replaced = []
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        for index, material in enumerate(obj.data.materials):
            if material and ("Draft_Skin" in material.name or "Draft_RoseLips" in material.name):
                replaced.append(dict(object=obj.name, slot=index, previous=material.name))
                obj.data.materials[index] = skin
    spec = dict(name="front", position=[3, 0, 0], target=[0, 0, 0],
                up=[0, 0, 1], width=1024, height=1024, ortho_scale=1.12)
    camera = scene.camera
    camera.location = spec["position"]
    camera.rotation_euler = (Vector(spec["target"]) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = spec["ortho_scale"]
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.55, .55, .55, 1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .8
    for obj in scene.objects:
        if obj.type == "LIGHT":
            obj.data.size = 4
    guides = output / "guides"
    guides.mkdir(exist_ok=True)
    bpy.context.view_layer.update()
    scene.render.filepath = str(guides / "front-neutral-skin-with-eyes.png")
    bpy.ops.render.render(write_still=True)
    capture_dir = output / "geometry-capture"
    capture = capture_static(head, spec, capture_dir)
    write_json(capture_dir / "degenerate-contact-triangles.json", zero_area_report)
    cam = projection.camera(spec)
    with np.load(capture_dir / "front-geometry.npz") as z:
        head_depth = z["depth"].copy()
    eye_depth = np.full(head_depth.shape, np.inf)
    eye_reports = []
    for obj in scene.objects:
        if obj.type == "MESH" and obj.name.startswith("Ren_Eye_") and not obj.hide_render:
            world, triangles, normals, skipped = arrays(obj)
            depth, ids, _ = projection.raster_view(world, triangles, normals, cam)
            eye_depth = np.minimum(eye_depth, depth)
            eye_reports.append(dict(object=obj.name, raster_triangles=len(triangles),
                                    ignored_degenerate_triangles=skipped))
    eye_mask = np.isfinite(eye_depth) & (eye_depth <= head_depth + 1e-6)
    head_visible = np.isfinite(head_depth)
    skin_eligible = head_visible & ~eye_mask
    projection.save_image(capture_dir / "front-eye-exclusion.png", eye_mask.astype(float), data=True)
    projection.save_image(capture_dir / "front-skin-projection-eligible.png", skin_eligible.astype(float), data=True)
    np.savez_compressed(capture_dir / "front-eye-occlusion.npz", eye_depth=eye_depth,
                        visible_eye_assembly=eye_mask, skin_projection_eligible=skin_eligible)
    if raw_signature(head) != before_geometry:
        raise AssertionError("Capture/material preparation changed mesh or UV coordinates")
    scene["paint_trial_status"] = "PRELIMINARY_STATIC_NEUTRAL_CAPTURE_NOT_PROJECTED"
    scene["source_geometry_sha256"] = before_geometry["geometry_sha256"]
    bpy.context.preferences.filepaths.save_version = 0
    derived = output / "Ren_P2_PaintNeutral_Snapshot.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(derived))
    report = dict(status="PRELIMINARY_FRONT_PAINT_GUIDE_CAPTURED_NOT_PROJECTED",
                  source=str(source), source_sha256_at_capture=initial_source_hash,
                  source_sha256_after=digest(source),
                  private_source_snapshot=frozen_source.name,
                  private_source_sha256=digest(frozen_source),
                  derived_static_snapshot=derived.name, derived_static_sha256=digest(derived),
                  blender_version=bpy.app.version_string,
                  pose=dict(mouthSeal=1, other_non_basis_keys=0), source_shape_values=source_shapes,
                  static_objects=static_records, head_object=head.name,
                  geometry_signature=before_geometry, capture_metadata="geometry-capture/capture.json",
                  strict_projection_compatible=capture["strict_projection_compatible"],
                  strict_capture_error=capture.get("strict_capture_error"),
                  raster_only_excluded_degenerate_triangles=capture.get("raster_only_excluded_degenerate_triangles", 0),
                  camera=spec, removed_draft_material_assignments=replaced,
                  guide=dict(file="guides/front-neutral-skin-with-eyes.png",
                             sha256=digest(guides / "front-neutral-skin-with-eyes.png"),
                             width=1024, height=1024),
                  eye_exclusion=dict(objects=eye_reports, visible_pixels=int(eye_mask.sum()),
                                     eligible_head_pixels=int(skin_eligible.sum()),
                                     note="Exclude all visible eye assembly pixels from skin projection; helper's head-only capture does not model other-object occlusion."),
                  limitations=["Exact old geometry snapshot while parent repairs blink topology in parallel.",
                               "Any later topology, neutral pose, UV or camera change requires recapture or explicit verified transfer.",
                               "Neutral guide uses the same skin material over draft lip polygons; their broad material boundary is not a lip-paint target.",
                               "No painting, projection, baking, geometry approval or texture completion performed by this script."])
    write_json(output / "prepare-report.json", report)
    if digest(frozen_source) != initial_source_hash:
        raise AssertionError("Private source snapshot changed")
    print("REN_PAINT_FRONT_GUIDE_READY", output, flush=True)


if __name__ == "__main__":
    main()
