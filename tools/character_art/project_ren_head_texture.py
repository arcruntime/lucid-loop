"""Deterministic, geometry-preserving multi-view projection inside Blender.

Run with Blender --background --threads 2 --python THIS_FILE -- --help.
No generation service, UV editing, geometry editing, or Unity integration.
"""

import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def mesh_data(obj):
    """Read the original mesh. Unsupported evaluated geometry is rejected."""
    if obj.type != "MESH" or not obj.data.uv_layers.active:
        raise ValueError("A mesh with an existing active UV layer is required")
    if any(m.show_viewport or m.show_render for m in obj.modifiers):
        raise ValueError(
            "Enabled modifiers are unsupported; supply a reviewed static head"
        )
    if obj.data.shape_keys and any(
        abs(k.value) > 1e-8 for k in obj.data.shape_keys.key_blocks[1:]
    ):
        raise ValueError("Non-neutral shape keys are unsupported")
    mesh = obj.data
    mesh.calc_loop_triangles()
    local = np.array([v.co[:] for v in mesh.vertices], dtype=np.float64)
    matrix = np.array(obj.matrix_world, dtype=np.float64)
    world = (np.c_[local, np.ones(len(local))] @ matrix.T)[:, :3]
    triangles = np.array([t.vertices[:] for t in mesh.loop_triangles], dtype=np.int32)
    loops = np.array([t.loops[:] for t in mesh.loop_triangles], dtype=np.int32)
    uv = np.array([v.uv[:] for v in mesh.uv_layers.active.data], dtype=np.float64)
    if not np.isfinite(world).all() or not np.isfinite(uv).all():
        raise ValueError("Nonfinite positions or UVs")
    if np.any(uv < 0) or np.any(uv > 1):
        raise ValueError("Only one 0..1 UV tile is supported; no wrapping or UDIM")
    geometry = hashlib.sha256()
    geometry.update(local.astype("<f8").tobytes())
    geometry.update(
        np.array([loop.vertex_index for loop in mesh.loops], dtype="<i4").tobytes()
    )
    geometry.update(
        np.array(
            [(p.loop_start, p.loop_total) for p in mesh.polygons], dtype="<i4"
        ).tobytes()
    )
    uv_hash = hashlib.sha256(uv.astype("<f8").tobytes()).hexdigest()
    signature = dict(
        geometry_sha256=geometry.hexdigest(),
        uv_sha256=uv_hash,
        world_matrix=matrix.tolist(),
        uv_layer=mesh.uv_layers.active.name,
    )
    cross = np.cross(
        world[triangles[:, 1]] - world[triangles[:, 0]],
        world[triangles[:, 2]] - world[triangles[:, 0]],
    )
    length = np.linalg.norm(cross, axis=1)
    if np.any(length < 1e-12):
        raise ValueError("Degenerate geometric triangles are unsupported")
    tri_uv = uv[loops]
    uv_area = (tri_uv[:, 1, 0] - tri_uv[:, 0, 0]) * (
        tri_uv[:, 2, 1] - tri_uv[:, 0, 1]
    ) - (tri_uv[:, 1, 1] - tri_uv[:, 0, 1]) * (tri_uv[:, 2, 0] - tri_uv[:, 0, 0])
    if np.any(np.abs(uv_area) < 1e-12):
        raise ValueError("Degenerate UV triangles are unsupported")
    return world, triangles, uv[loops], cross / length[:, None], signature


def camera(spec):
    origin = np.array(spec["position"], dtype=float)
    forward = np.array(spec["target"], dtype=float) - origin
    forward /= np.linalg.norm(forward)
    right = np.cross(forward, spec["up"])
    if np.linalg.norm(right) < 1e-8:
        raise ValueError("Camera up is parallel to its view direction")
    right /= np.linalg.norm(right)
    up = np.cross(right, forward)
    width, height = int(spec["width"]), int(spec["height"])
    scale = float(spec["ortho_scale"])  # Horizontal world-space extent.
    if width < 2 or height < 2 or scale <= 0 or not np.isfinite(forward).all():
        raise ValueError("Invalid fixed orthographic camera")
    axes = np.stack([right, up, forward])
    matrix = np.eye(4)
    matrix[:3, :3] = np.stack([right, up, -forward])
    matrix[:3, 3] = -matrix[:3, :3] @ origin
    metadata = dict(
        spec,
        projection="orthographic",
        pixel_origin="bottom-left",
        sample_location="pixel center",
        depth_units="world units along camera forward; +infinity is background",
        normal_space="world; geometric triangle normal",
        world_to_blender_camera=matrix.tolist(),
    )
    return origin, axes, width, height, scale, metadata


def screen(points, cam):
    origin, axes, width, height, scale, _ = cam
    xyz = (points - origin) @ axes.T
    return np.stack(
        [
            (xyz[..., 0] / scale + 0.5) * width,
            (xyz[..., 1] / (scale * height / width) + 0.5) * height,
            xyz[..., 2],
        ],
        axis=-1,
    )


def triangle_pixels(points, width, height):
    """Raster pixel centers with barycentrics, including boundary pixels."""
    a, b, c = points
    determinant = (b - a)[0] * (c - a)[1] - (b - a)[1] * (c - a)[0]
    if abs(determinant) < 1e-12:
        return None
    lo = np.maximum(np.ceil(points.min(axis=0) - 0.5).astype(int), 0)
    hi = np.minimum(
        np.floor(points.max(axis=0) - 0.5).astype(int), [width - 1, height - 1]
    )
    if np.any(hi < lo):
        return None
    yy, xx = np.mgrid[lo[1] : hi[1] + 1, lo[0] : hi[0] + 1]
    q = np.stack([xx + 0.5, yy + 0.5], axis=-1) - a
    v = (q[..., 0] * (c - a)[1] - q[..., 1] * (c - a)[0]) / determinant
    w = ((b - a)[0] * q[..., 1] - (b - a)[1] * q[..., 0]) / determinant
    weights = np.stack([1 - v - w, v, w], axis=-1)
    valid = np.all(weights >= -1e-9, axis=-1)
    return yy[valid], xx[valid], weights[valid]


def raster_view(world, triangles, normals, cam):
    _, _, width, height, _, _ = cam
    depth = np.full((height, width), np.inf)
    ids = np.full((height, width), -1, dtype=np.int32)
    projected = screen(world, cam)
    for index, tri in enumerate(triangles):
        points = projected[tri]
        if np.any(points[:, 2] <= 0):
            raise ValueError("Geometry intersects or lies behind a capture camera")
        pixels = triangle_pixels(points[:, :2], width, height)
        if pixels is None:
            continue
        y, x, bary = pixels
        z = bary @ points[:, 2]
        keep = z < depth[y, x]
        y, x = y[keep], x[keep]
        depth[y, x] = z[keep]
        ids[y, x] = index
    valid = ids >= 0
    normal = np.zeros((height, width, 3))
    normal[valid] = normals[ids[valid]]
    return depth, ids, normal


def save_image(path, pixels, data=False):
    import bpy

    pixels = np.asarray(pixels, dtype=np.float32)
    if pixels.ndim == 2:
        pixels = np.repeat(pixels[..., None], 3, axis=-1)
    if pixels.shape[2] == 3:
        pixels = np.concatenate([pixels, np.ones((*pixels.shape[:2], 1))], axis=-1)
    height, width, _ = pixels.shape
    image = bpy.data.images.new(Path(path).stem, width, height, alpha=True)
    image.colorspace_settings.name = "Non-Color" if data else "sRGB"
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw = str(Path(path).resolve())
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def load_image(path, data=False):
    import bpy

    image = bpy.data.images.load(str(Path(path).resolve()), check_existing=False)
    image.colorspace_settings.name = "Non-Color" if data else "sRGB"
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    return pixels.reshape(height, width, 4)


def capture(obj, specs, output):
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    world, triangles, uv, normals, signature = mesh_data(obj)
    cameras = []
    names = set()
    for spec in specs:
        name = spec["name"]
        if name in names or not name.replace("_", "").replace("-", "").isalnum():
            raise ValueError("Camera names must be unique simple identifiers")
        names.add(name)
        cam = camera(spec)
        depth, ids, normal = raster_view(world, triangles, normals, cam)
        visible = ids >= 0
        # Deterministic clay diagnostic, not a beauty render or baked lighting.
        light = -cam[1][2] + cam[1][1] * 0.5 - cam[1][0] * 0.3
        light /= np.linalg.norm(light)
        shade = 0.25 + 0.65 * np.maximum(0, normal @ light)
        clay = np.repeat(shade[..., None], 4, axis=-1)
        clay[..., 3] = visible.astype(float)
        clay[~visible, :3] = 0
        save_image(output / f"{name}-clay.png", clay)
        save_image(output / f"{name}-normal.png", normal * 0.5 + 0.5, data=True)
        save_image(output / f"{name}-visibility.png", visible.astype(float), data=True)
        np.savez_compressed(
            output / f"{name}-geometry.npz",
            depth=depth,
            triangle_id=ids,
            world_geometric_normal=normal,
        )
        finite = depth[visible]
        display = np.zeros(depth.shape)
        if len(finite):
            display[visible] = 1 - (finite - finite.min()) / max(np.ptp(finite), 1e-9)
        save_image(output / f"{name}-depth-preview.png", display, data=True)
        cameras.append(
            dict(
                cam[5],
                geometry_file=f"{name}-geometry.npz",
                geometry_file_sha256=digest(output / f"{name}-geometry.npz"),
            )
        )
    if mesh_data(obj)[4] != signature:
        raise AssertionError("Capture changed mesh or UVs")
    result = dict(
        version=1,
        object=obj.name,
        signature=signature,
        cameras=cameras,
        renderer="deterministic orthographic triangle rasterizer; no antialiasing",
        source_scope="selected static head only; other objects are not occluders",
    )
    write_json(output / "capture.json", result)
    return result


def uv_samples(world, triangles, uvs, size):
    owner = np.full((size, size), -1, dtype=np.int32)
    positions = np.zeros((size, size, 3))
    interior = np.zeros((size, size), dtype=bool)
    for i, (tri, uv) in enumerate(zip(triangles, uvs)):
        pixels = triangle_pixels(uv * size, size, size)
        if pixels is None:
            continue
        y, x, bary = pixels
        inside = bary.min(axis=1) > 1e-8
        if np.any((owner[y, x] >= 0) & interior[y, x] & inside):
            raise ValueError(
                "Overlapping UV interiors at output texel centers are unsupported"
            )
        empty = owner[y, x] < 0
        y, x, bary, inside = y[empty], x[empty], bary[empty], inside[empty]
        owner[y, x] = i
        positions[y, x] = bary @ world[tri]
        interior[y, x] = inside
    return owner, positions


def project(obj, captures, reviewed, output, size, min_facing=0.15):
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree

    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    captures = Path(captures)
    if reviewed.get("capture_sha256") != digest(captures / "capture.json"):
        raise ValueError("Review must identify the exact capture.json SHA256")
    metadata = json.loads((captures / "capture.json").read_text(encoding="utf-8"))
    world, triangles, uvs, normals, signature = mesh_data(obj)
    if signature != metadata["signature"]:
        raise ValueError(
            "Input geometry, UVs or world transform differs from captured source"
        )
    if size < 2 or size > 4096 or not 0 <= min_facing < 1:
        raise ValueError("Size must be 2..4096 and min-facing must be 0..<1")
    owner, positions = uv_samples(world, triangles, uvs, size)
    y, x = np.where(owner >= 0)
    points = positions[y, x]
    point_normals = normals[owner[y, x]]
    total = np.zeros(len(points))
    colors = np.zeros((len(points), 3))
    bvh = BVHTree.FromPolygons(world.tolist(), triangles.tolist(), all_triangles=True)
    scale = np.linalg.norm(np.ptp(world, axis=0))
    ray_tolerance = max(scale * 1e-6, 1e-8)
    view_reports = []
    by_name = {c["name"]: c for c in metadata["cameras"]}
    if not reviewed["views"]:
        raise ValueError("At least one explicitly reviewed view is required")
    used = set()
    for review in reviewed["views"]:
        name = review["camera"]
        if name in used:
            raise ValueError("Duplicate reviewed camera")
        used.add(name)
        image_path = Path(review["image"]).resolve()
        if image_path == (output / "projected-color.png").resolve():
            raise ValueError("Output may not overwrite a reviewed painting")
        if review.get("approved") is not True or digest(image_path) != review.get(
            "sha256"
        ):
            raise ValueError(
                "Every painted view must have approved=true and its exact SHA256"
            )
        cam = camera(by_name[name])
        geometry_path = captures / by_name[name]["geometry_file"]
        if digest(geometry_path) != by_name[name]["geometry_file_sha256"]:
            raise ValueError("Captured depth/visibility archive changed")
        with np.load(geometry_path) as archive:
            depth = archive["depth"]
        paint = load_image(image_path)
        if paint.shape[:2] != depth.shape:
            raise ValueError("Reviewed painting dimensions differ from fixed camera")
        projected = screen(points, cam)
        px, py = (
            np.floor(projected[:, 0]).astype(int),
            np.floor(projected[:, 1]).astype(int),
        )
        facing = point_normals @ -cam[1][2]
        valid = (
            (px >= 0)
            & (px < cam[2])
            & (py >= 0)
            & (py < cam[3])
            & (projected[:, 2] > 0)
            & (facing > min_facing)
        )
        candidates = np.flatnonzero(valid)
        accepted = 0
        depth_tolerance = cam[4] / cam[2] * 2
        for index in candidates:
            cx, cy = px[index], py[index]
            difference = abs(depth[cy, cx] - projected[index, 2])
            if not np.isfinite(difference) or difference > depth_tolerance:
                continue
            rgba = paint[cy, cx]
            if rgba[3] <= 0:
                continue
            # Exact geometric visibility in addition to raster depth agreement.
            # Start on the camera plane on the ray through this UV texel.
            start = points[index] - cam[1][2] * projected[index, 2]
            hit, _, _, distance = bvh.ray_cast(
                Vector(start), Vector(cam[1][2]), projected[index, 2] + ray_tolerance
            )
            if hit is None or abs(distance - projected[index, 2]) > ray_tolerance:
                continue
            weight = (
                facing[index] ** 2
                * np.exp(-((difference / depth_tolerance) ** 2))
                * rgba[3]
            )
            colors[index] += rgba[:3] * weight
            total[index] += weight
            accepted += 1
        view_reports.append(
            dict(
                camera=name,
                accepted_texels=accepted,
                image=str(image_path),
                sha256=review["sha256"],
            )
        )
    texture = np.zeros((size, size, 4), dtype=np.float32)
    covered = total > 0
    texture[y[covered], x[covered], :3] = colors[covered] / total[covered, None]
    texture[y[covered], x[covered], 3] = 1
    save_image(output / "projected-color.png", texture)
    save_image(output / "coverage.png", texture[..., 3], data=True)
    # New material on a derived saved file only; source file is never overwritten.
    material = bpy.data.materials.new("ReviewedProjectedHead")
    material.use_nodes = True
    image = bpy.data.images.load(
        str((output / "projected-color.png").resolve()), check_existing=False
    )
    image.colorspace_settings.name = "sRGB"
    image.pack()
    node = material.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = image
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    material.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.8
    # Keep material indices and geometry intact by replacing each existing slot.
    if len(obj.data.materials):
        for i in range(len(obj.data.materials)):
            obj.data.materials[i] = material
    else:
        obj.data.materials.append(material)
    if mesh_data(obj)[4] != signature:
        raise AssertionError("Projection changed mesh or UVs")
    bpy.ops.wm.save_as_mainfile(
        filepath=str((output / "projected-head.blend").resolve())
    )
    report = dict(
        signature_before=signature,
        signature_after=mesh_data(obj)[4],
        uv_texels=len(points),
        covered_texels=int(covered.sum()),
        coverage_fraction=float(covered.mean()) if len(covered) else 0,
        views=view_reports,
        unknown_texels="transparent black; no extrapolation",
        preview_material="opaque: uncovered areas appear black; consult coverage.png",
        sampling="nearest painted pixel; weighted linear color; no seam dilation",
    )
    write_json(output / "projection-report.json", report)
    return report


def main():
    import bpy

    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ["capture", "project"]:
        p = sub.add_parser(name)
        p.add_argument("--input", required=True, type=Path)
        p.add_argument("--object", required=True)
        p.add_argument("--output", required=True, type=Path)
    sub.choices["capture"].add_argument("--cameras", required=True, type=Path)
    sub.choices["project"].add_argument("--capture", required=True, type=Path)
    sub.choices["project"].add_argument("--review", required=True, type=Path)
    sub.choices["project"].add_argument("--size", type=int, default=1024)
    sub.choices["project"].add_argument("--min-facing", type=float, default=0.15)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :])
    if args.input.resolve() == (args.output / "projected-head.blend").resolve():
        raise ValueError("Output may not overwrite the source blend")
    before = digest(args.input)
    bpy.ops.wm.open_mainfile(filepath=str(args.input.resolve()))
    obj = bpy.data.objects[args.object]
    if args.command == "capture":
        capture(
            obj,
            json.loads(args.cameras.read_text(encoding="utf-8"))["cameras"],
            args.output,
        )
    else:
        reviewed = json.loads(args.review.read_text(encoding="utf-8"))
        for view in reviewed["views"]:
            view["image"] = str((args.review.parent / view["image"]).resolve())
        project(obj, args.capture, reviewed, args.output, args.size, args.min_facing)
    if before != digest(args.input):
        raise AssertionError("Source file changed on disk")
    print("TEXTURE_PROJECTION_OK", args.command, flush=True)


if __name__ == "__main__":
    main()
