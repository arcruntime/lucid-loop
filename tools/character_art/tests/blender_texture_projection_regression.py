"""Blender-only projection regression; no Ren assets or paid services.

Run from any directory:
  blender --background --threads 2 --factory-startup --offline-mode \
    --python-exit-code 1 --python /path/to/this_script.py

Evidence is retained in a unique project-relative .local/ren-texture-projection/
regression-* directory. Normal pytest collection must not execute Blender code.
"""

from pathlib import Path
import sys
import tempfile


def main():
    import bpy
    import numpy as np

    project = Path(__file__).resolve().parents[3]
    sys.path.insert(0, str(project / "tools" / "character_art"))
    import project_ren_head_texture as p

    scratch = project / ".local" / "ren-texture-projection"
    scratch.mkdir(parents=True, exist_ok=True)
    root = Path(tempfile.mkdtemp(prefix="regression-", dir=scratch))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh = bpy.data.meshes.new("OcclusionFixture")
    mesh.from_pydata(
        [
            (-1, 0, -1),
            (1, 0, -1),
            (1, 0, 1),
            (-1, 0, 1),
            (-1, 1, -1),
            (1, 1, -1),
            (1, 1, 1),
            (-1, 1, 1),
        ],
        [],
        [(0, 1, 2, 3), (4, 5, 6, 7)],
    )
    mesh.update()
    uv = mesh.uv_layers.new(name="AcceptedUV")
    coords = [
        (0.05, 0.05),
        (0.45, 0.05),
        (0.45, 0.95),
        (0.05, 0.95),
        (0.55, 0.05),
        (0.95, 0.05),
        (0.95, 0.95),
        (0.55, 0.95),
    ]
    for loop, value in zip(uv.data, coords):
        loop.uv = value
    obj = bpy.data.objects.new("HeadFixture", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.wm.save_as_mainfile(filepath=str(root / "source.blend"))
    source_hash = p.digest(root / "source.blend")
    specs = [
        dict(
            name=name,
            position=position,
            target=[0, 0, 0],
            up=[0, 0, 1],
            width=64,
            height=64,
            ortho_scale=4,
        )
        for name, position in [
            ("front", [0, -5, 0]),
            ("quarter", [3.535, -3.535, 0]),
            ("profile", [5, 0, 0]),
        ]
    ]
    p.write_json(root / "cameras.json", dict(cameras=specs))
    meta = p.capture(obj, specs, root / "capture")
    a = np.load(root / "capture/front-geometry.npz")
    assert abs(a["depth"][32, 32] - 5) < 1e-10
    assert np.allclose(a["world_geometric_normal"][32, 32], [0, -1, 0])
    # Red/green halves prove UV direction; rear plane has SAME normal as front,
    # so only visibility prevents projection there.
    paint = np.zeros((64, 64, 4), np.float32)
    paint[:, :32] = [1, 0, 0, 1]
    paint[:, 32:] = [0, 1, 0, 1]
    p.save_image(root / "front-reviewed.png", paint)
    review = dict(
        capture_sha256=p.digest(root / "capture/capture.json"),
        views=[
            dict(
                camera="front",
                image=str(root / "front-reviewed.png"),
                approved=True,
                sha256=p.digest(root / "front-reviewed.png"),
            )
        ],
    )
    p.write_json(root / "review.json", review)
    report = p.project(obj, root / "capture", review, root / "derived", 64)
    result = p.load_image(root / "derived/projected-color.png")
    assert result[32, 10, 0] > 0.99 and result[32, 10, 1] < 0.01, result[32, 10]
    assert result[32, 23, 1] > 0.99 and result[32, 23, 0] < 0.01, result[32, 23]
    assert np.all(result[:, 36:60, 3] == 0), "Hidden rear plane was painted"
    assert p.digest(root / "source.blend") == source_hash
    bpy.ops.wm.open_mainfile(filepath=str(root / "derived/projected-head.blend"))
    obj = bpy.data.objects["HeadFixture"]
    assert p.mesh_data(obj)[4] == meta["signature"]
    failures = []
    for label, mutate, restore in [
        (
            "unapproved",
            lambda: review["views"][0].update(approved=False),
            lambda: review["views"][0].update(approved=True),
        ),
        (
            "capture_hash",
            lambda: review.update(capture_sha256="wrong"),
            lambda: review.update(
                capture_sha256=p.digest(root / "capture/capture.json")
            ),
        ),
        (
            "geometry_mismatch",
            lambda: setattr(obj.data.vertices[0].co, "x", -0.9),
            lambda: setattr(obj.data.vertices[0].co, "x", -1),
        ),
        (
            "uv_mismatch",
            lambda: setattr(obj.data.uv_layers.active.data[0].uv, "x", 0.1),
            lambda: setattr(obj.data.uv_layers.active.data[0].uv, "x", coords[0][0]),
        ),
        (
            "image_hash",
            lambda: review["views"][0].update(sha256="wrong"),
            lambda: review["views"][0].update(
                sha256=p.digest(root / "front-reviewed.png")
            ),
        ),
        (
            "transform_mismatch",
            lambda: setattr(obj, "location", (0, 0, 0.1)),
            lambda: setattr(obj, "location", (0, 0, 0)),
        ),
    ]:
        mutate()
        bpy.context.view_layer.update()
        try:
            p.project(obj, root / "capture", review, root / label, 16)
        except ValueError:
            failures.append(label)
        else:
            raise AssertionError("Did not reject " + label)
        restore()
        bpy.context.view_layer.update()
    # Explicit overlapped UV interior rejection.
    world, triangles, uvs, normals, sig = p.mesh_data(obj)
    try:
        p.uv_samples(world, triangles, np.tile(uvs[:2], (2, 1, 1)), 32)
    except ValueError:
        failures.append("overlapping_uv")
    else:
        raise AssertionError("Did not reject overlapping UV")
    # Linear image color round-trip.
    mid = np.full((8, 8, 4), 0.25, np.float32)
    mid[:, :, 3] = 1
    p.save_image(root / "linear-quarter.png", mid)
    assert (
        np.max(np.abs(p.load_image(root / "linear-quarter.png")[:, :, :3] - 0.25))
        < 0.005
    )
    # Force raster depths to the rear plane; BVH must still reject the hidden rear.
    archive_path = root / "capture/front-geometry.npz"
    with np.load(archive_path) as old:
        arrays = {k: old[k].copy() for k in old.files}
    arrays["depth"][np.isfinite(arrays["depth"])] += 1
    np.savez_compressed(archive_path, **arrays)
    meta["cameras"][0]["geometry_file_sha256"] = p.digest(archive_path)
    p.write_json(root / "capture/capture.json", meta)
    review["capture_sha256"] = p.digest(root / "capture/capture.json")
    bvh_report = p.project(obj, root / "capture", review, root / "bvh-only", 64)
    assert bvh_report["covered_texels"] == 0, (
        "BVH failed to reject hidden rear with misleading raster depth"
    )
    failures.extend(["linear_color_roundtrip", "bvh_independent_hidden_rejection"])
    p.write_json(
        root / "fixture-report.json",
        dict(
            status="passed",
            blender_version=bpy.app.version_string,
            blender_binary=bpy.app.binary_path,
            tool_sha256=p.digest(Path(p.__file__)),
            source_file_sha256=source_hash,
            checks=[
                "front_depth_world_units",
                "world_geometric_normal",
                "uv_left_red_right_green",
                "hidden_same_normal_rear_unpainted",
                "saved_geometry_uv_hash_equal",
                "source_file_hash_equal",
            ]
            + failures,
            projection=report,
        ),
    )
    print("PROJECTION_FIXTURE_PASS", str(root / "fixture-report.json"), flush=True)


if __name__ == "__main__":
    main()
