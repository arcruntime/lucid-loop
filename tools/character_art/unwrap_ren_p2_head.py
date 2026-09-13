"""UV-only native Ren P2 study; Blender 5.1+ background script.

Original UVs, positions and topology are retained. apply_mapping() transfers the
new layer by native source_vertex_id corner correspondence, including reordered
meshes. No new mouth geometry is invented or mapped implicitly.
"""

from pathlib import Path
import hashlib
import json
import sys
from collections import defaultdict
import argparse

import bpy
import numpy as np
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
AUDIT = BASE / "audits/p2-head-native"
OUT = BASE / "face-uv-v1"
LAYER = "FaceUV_v1"


def write(path, data):
    Path(path).write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def geometry_hash(mesh):
    h = hashlib.sha256()
    h.update(np.array([v.co[:] for v in mesh.vertices], dtype="<f8").tobytes())
    h.update(
        np.array([loop.vertex_index for loop in mesh.loops], dtype="<i4").tobytes()
    )
    h.update(
        np.array(
            [(p.loop_start, p.loop_total) for p in mesh.polygons], dtype="<i4"
        ).tobytes()
    )
    return h.hexdigest()


def apply_mapping(obj, mapping, allow_unmapped=False):
    """UV edits only. Returns unmapped polygon IDs; default rejects them."""
    mesh = obj.data
    attr = mesh.attributes.get("source_vertex_id")
    if attr is None:
        raise ValueError("Explicit native source_vertex_id attribute is required")
    before = geometry_hash(mesh)
    lookup = {tuple(sorted(row["source_vertex_ids"])): row for row in mapping["faces"]}
    if len(lookup) != len(mapping["faces"]):
        raise ValueError("Ambiguous duplicate source polygons")
    planned, unmapped = [], []
    for face in mesh.polygons:
        ids = [attr.data[i].value for i in face.vertices]
        row = lookup.get(tuple(sorted(ids))) if all(i >= 0 for i in ids) else None
        if row is None:
            unmapped.append(face.index)
            continue
        corners = dict(zip(row["source_vertex_ids"], row["uv"]))
        planned.extend(
            (loop, corners[native]) for loop, native in zip(face.loop_indices, ids)
        )
    if unmapped and not allow_unmapped:
        raise ValueError(
            f"{len(unmapped)} new/changed polygons require explicit UV authoring"
        )
    layer = mesh.uv_layers.get(LAYER) or mesh.uv_layers.new(name=LAYER)
    for loop, uv in planned:
        layer.data[loop].uv = uv
    mesh.uv_layers.active = layer
    assert geometry_hash(mesh) == before
    return {
        "mapped_polygons": len(mesh.polygons) - len(unmapped),
        "unmapped_polygons": unmapped,
        "geometry_sha256": before,
    }


def components(mesh, groups):
    adjacency = defaultdict(list)
    edge_faces = defaultdict(list)
    for face in mesh.polygons:
        for loop in face.loop_indices:
            edge_faces[mesh.loops[loop].edge_index].append(face.index)
    for edge in mesh.edges:
        fs = edge_faces[edge.index]
        edge.use_seam = len(fs) != 2 or groups[fs[0]] != groups[fs[1]]
        if not edge.use_seam:
            adjacency[fs[0]].append(fs[1])
            adjacency[fs[1]].append(fs[0])
    unseen = set(range(len(mesh.polygons)))
    result = []
    while unseen:
        seed = min(unseen)
        stack = [seed]
        part = []
        unseen.remove(seed)
        while stack:
            f = stack.pop()
            part.append(f)
            for other in adjacency[f]:
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
        result.append(part)
    return result


def unwrap(obj, labels, interior):
    mesh = obj.data
    world = np.array([obj.matrix_world @ v.co for v in mesh.vertices])
    groups = []
    # Native pinched outer eyelid/commissure polygons produced verified positive
    # UV-area intersections in the otherwise contiguous ABF face island.
    # Put these tiny peripheral details across explicit local UV seams.
    corner_faces = {
        1258,
        1262,
        1263,
        1264,
        3267,
        3268,
        3314,
        3316,
        6032,
        6038,
        8133,
        8134,
        8170,
        8172,
    }
    for face in mesh.polygons:
        center = world[list(face.vertices)].mean(axis=0)
        x, y, z = center
        if face.index in corner_faces:
            group = "corner-detail-left" if y >= 0 else "corner-detail-right"
        elif face.index in interior:
            group = "old-interior"
        elif labels[face.vertices[0]] != 0:
            group = f"component-{labels[face.vertices[0]]}"
        elif z < -0.27:
            group = "neck-left" if y >= 0 else "neck-right"
        elif abs(y) > 0.235 and x < 0.14:
            group = "ear-left" if y >= 0 else "ear-right"
        elif x > 0.015:
            group = "face"
        else:
            group = "rear-left" if y >= 0 else "rear-right"
        groups.append(group)
    parts = components(mesh, groups)
    layer = mesh.uv_layers.new(name=LAYER)
    mesh.uv_layers.active = layer
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.unwrap(method="ANGLE_BASED", margin=0.001)
    bpy.ops.object.mode_set(mode="OBJECT")
    # Edit-mode rebuild invalidates cached UV RNA data references.
    layer = mesh.uv_layers[LAYER]
    face_parts = [p for p in parts if groups[p[0]] == "face"]
    main = max(face_parts, key=len)
    ordered = [main] + sorted(
        (p for p in parts if p is not main), key=lambda p: -len(p)
    )
    # The face owns a large contiguous rectangle. Secondary islands occupy a
    # separate right strip, independently scaled; no automatic face fragmentation.
    large = [
        p
        for p in ordered[1:]
        if len(p) >= 30 or groups[p[0]].startswith("corner-detail")
    ]
    small = [p for p in ordered[1:] if p not in large]
    ordered = [main] + large + small
    records = []
    for index, part in enumerate(ordered):
        loops = np.array([loop for f in part for loop in mesh.polygons[f].loop_indices])
        values = np.array([layer.data[i].uv[:] for i in loops])
        if not np.isfinite(values).all():
            raise RuntimeError("Unwrap produced nonfinite UVs")
        lo, hi = values.min(axis=0), values.max(axis=0)
        if index == 0:
            target_lo = np.array([0.02, 0.02])
            target_hi = np.array([0.78, 0.98])
        else:
            is_large = index <= len(large)
            offset = index - 1 if is_large else index - 1 - len(large)
            count = len(large) if is_large else len(small)
            rows = int(np.ceil(count / 2))
            col = offset % 2
            row = offset // 2
            bottom, height = (0.21, 0.78) if is_large else (0.01, 0.18)
            gap = 0.002 if is_large else 0.0005
            target_lo = np.array(
                [0.80 + col * 0.10 + 0.005, bottom + row * height / rows + gap]
            )
            target_hi = np.array(
                [
                    0.80 + (col + 1) * 0.10 - 0.005,
                    bottom + (row + 1) * height / rows - gap,
                ]
            )
        factor = np.min((target_hi - target_lo) / np.maximum(hi - lo, 1e-12))
        values = (values - (lo + hi) / 2) * factor + (target_lo + target_hi) / 2
        for i, uv in zip(loops, values):
            layer.data[i].uv = uv
        records.append(
            dict(
                island=index,
                region=groups[part[0]],
                faces=len(part),
                source_face_ids=part,
                uv_bounds=[values.min(axis=0).tolist(), values.max(axis=0).tolist()],
            )
        )
    return records, groups


def metrics(obj, records):
    mesh = obj.data
    mesh.calc_loop_triangles()
    world = np.array([obj.matrix_world @ v.co for v in mesh.vertices])
    uv = np.array([d.uv[:] for d in mesh.uv_layers[LAYER].data])
    samples = []
    tris = []
    main_faces = set(records[0]["source_face_ids"])
    for tri in mesh.loop_triangles:
        xyz = world[list(tri.vertices)]
        q = uv[list(tri.loops)]
        tris.append(q)
        e1, e2 = xyz[1] - xyz[0], xyz[2] - xyz[0]
        length = np.linalg.norm(e1)
        area2 = np.linalg.norm(np.cross(e1, e2))
        if length < 1e-12 or area2 < 1e-12:
            continue
        basis = np.array([[length, np.dot(e1, e2) / length], [0, area2 / length]])
        jac = (q[1:] - q[0]).T @ np.linalg.inv(basis)
        singular = np.linalg.svd(jac, compute_uv=False)
        density = 1024 * np.sqrt(abs(np.linalg.det(jac)))
        center = xyz.mean(axis=0)
        region = "other"
        if tri.polygon_index in main_faces:
            if (
                center[0] > 0.17
                and 0.02 < center[2] < 0.14
                and 0.05 < abs(center[1]) < 0.19
            ):
                region = "eyes"
            if center[0] > 0.20 and -0.18 < center[2] < -0.05 and abs(center[1]) < 0.1:
                region = "mouth"
        samples.append(
            (region, density, float(singular[0] / max(singular[-1], 1e-15)), area2 / 2)
        )
    result = {}
    for region in ["eyes", "mouth", "other"]:
        a = np.array([[d, s, area] for r, d, s, area in samples if r == region])
        result[region] = (
            dict(
                triangles=len(a),
                texels_per_source_unit_at_1024_median=float(np.median(a[:, 0])),
                anisotropy_p50_p95_max=np.percentile(a[:, 1], [50, 95, 100]).tolist(),
                estimated_texels_at_1024=float(np.sum(a[:, 0] ** 2 * a[:, 2])),
            )
            if len(a)
            else {}
        )
    # Test conflicting interiors at 2048 texel centers; shared boundaries excluded.
    sys.path.insert(0, str(ROOT / "tools/character_art"))
    from project_ren_head_texture import triangle_pixels, save_image

    size = 2048
    owner = np.full((size, size), -1, np.int32)
    overlap = 0
    flipped = 0
    image = np.zeros((size, size, 4), np.float32)
    for index, q in enumerate(tris):
        cross = np.linalg.det((q[1:] - q[0]).T)
        if abs(cross) < 1e-12:
            flipped += 1
        pix = triangle_pixels(q * size, size, size)
        if pix is None:
            continue
        y, x, bary = pix
        inside = bary.min(axis=1) > 1e-7
        overlap += int(np.sum((owner[y[inside], x[inside]] >= 0)))
        owner[y[inside], x[inside]] = index
        checker = ((x // 64 + y // 64) % 2) * 0.45 + 0.30
        image[y, x, :3] = checker[:, None]
        image[y, x, 3] = 1
    save_image(OUT / "atlas-checker.png", image)
    result["overlap_2048_conflicting_interior_samples"] = overlap
    result["degenerate_uv_triangles"] = flipped
    result["islands"] = len(records)
    result["main_face_island_faces"] = records[0]["faces"]
    return result


def exact_overlaps(mesh, epsilon=1e-11):
    """Positive-area triangle intersection, including adjacent folded triangles."""
    mesh.calc_loop_triangles()
    triangles = [
        np.array([mesh.uv_layers[LAYER].data[i].uv[:] for i in t.loops])
        for t in mesh.loop_triangles
    ]
    bounds = [(t.min(axis=0), t.max(axis=0)) for t in triangles]
    buckets = defaultdict(list)
    pairs = set()
    conflicts = []

    def area(poly):
        return 0.5 * sum(
            p[0] * q[1] - q[0] * p[1] for p, q in zip(poly, poly[1:] + poly[:1])
        )

    def intersection(a, b):
        polygon = [tuple(p) for p in a]
        sign = 1 if area([tuple(p) for p in b]) >= 0 else -1
        for u, v in zip(b, np.roll(b, -1, axis=0)):
            if not polygon:
                break
            old = polygon
            polygon = []

            def side(p):
                return sign * (
                    (v[0] - u[0]) * (p[1] - u[1]) - (v[1] - u[1]) * (p[0] - u[0])
                )

            for p, q in zip(old, old[1:] + old[:1]):
                sp, sq = side(p), side(q)
                if sp >= 0:
                    polygon.append(p)
                if (sp >= 0) != (sq >= 0):
                    f = sp / (sp - sq)
                    polygon.append((p[0] + f * (q[0] - p[0]), p[1] + f * (q[1] - p[1])))
        return abs(area(polygon)) if len(polygon) >= 3 else 0

    for index, (lo, hi) in enumerate(bounds):
        for x in range(int(lo[0] * 64), int(hi[0] * 64) + 1):
            for y in range(int(lo[1] * 64), int(hi[1] * 64) + 1):
                bucket = buckets[x, y]
                for other in bucket:
                    pair = (other, index)
                    if pair in pairs:
                        continue
                    pairs.add(pair)
                    olo, ohi = bounds[other]
                    if np.any(np.minimum(hi, ohi) - np.maximum(lo, olo) <= 0):
                        continue
                    overlap = intersection(triangles[index], triangles[other])
                    if overlap > epsilon:
                        conflicts.append(
                            dict(
                                triangles=[other, index],
                                source_faces=[
                                    mesh.loop_triangles[other].polygon_index,
                                    mesh.loop_triangles[index].polygon_index,
                                ],
                                uv_area=overlap,
                            )
                        )
                bucket.append(index)
    return dict(
        positive_area_conflicts=len(conflicts),
        uv_area_epsilon=epsilon,
        summed_overlap_uv_area=sum(row["uv_area"] for row in conflicts),
        conflicts=conflicts,
    )


def assign_cavity_uv(obj, face_indices, rim_source_ids, bounds):
    """Map an explicitly selected N-column, three-ring native oral cup.

    Only UV corners on face_indices change. The radial rings are discovered
    through topology, not assumed to occupy particular vertex indices.
    """
    mesh = obj.data
    provenance = mesh.attributes["source_vertex_id"]
    by_source = {
        item.value: i for i, item in enumerate(provenance.data) if item.value >= 0
    }
    ring = [by_source[i] for i in rim_source_ids]
    count = len(ring)
    if count < 3 or len(set(ring)) != count or len(face_indices) != count * 4:
        raise ValueError("Cavity contract requires N unique rim points and 4N polygons")
    adjacency = defaultdict(set)
    vertices = set()
    for index in face_indices:
        face = mesh.polygons[index]
        ids = list(face.vertices)
        vertices.update(ids)
        for a, b in zip(ids, ids[1:] + ids[:1]):
            adjacency[a].add(b)
            adjacency[b].add(a)
    rings = [ring]
    visited = set(ring)
    for _ in range(3):
        next_ring = []
        for vertex in rings[-1]:
            candidates = adjacency[vertex] - visited
            if len(candidates) != 1:
                raise ValueError("Cavity does not have a unique radial continuation")
            next_ring.append(next(iter(candidates)))
        if len(set(next_ring)) != count:
            raise ValueError("Cavity ring is not bijective")
        rings.append(next_ring)
        visited.update(next_ring)
    remaining = vertices - visited
    if len(remaining) != 1 or len(vertices) != count * 4 + 1:
        raise ValueError("Expected one final cavity fan center")
    center_vertex = next(iter(remaining))
    if any(center_vertex not in adjacency[v] for v in rings[-1]):
        raise ValueError("Cavity rear ring does not meet a single fan center")
    lo, hi = np.array(bounds)
    if np.any(hi <= lo) or np.any(lo < 0) or np.any(hi > 1):
        raise ValueError("Invalid reserved cavity atlas rectangle")
    center = (lo + hi) / 2
    radius = np.min(hi - lo) * 0.46
    points = np.array([obj.matrix_world @ mesh.vertices[v].co for v in ring])
    lengths = np.linalg.norm(np.roll(points, -1, axis=0) - points, axis=1)
    phase = np.r_[0, np.cumsum(lengths[:-1])] / lengths.sum() * 2 * np.pi
    directions = np.stack([np.cos(phase), np.sin(phase)], axis=1)
    values = {center_vertex: center}
    for current, fraction in zip(rings, [1, 0.72, 0.42, 0.16]):
        for vertex, uv in zip(current, center + directions * radius * fraction):
            values[vertex] = uv
    layer = mesh.uv_layers[LAYER]
    for index in face_indices:
        for loop in mesh.polygons[index].loop_indices:
            layer.data[loop].uv = values[mesh.loops[loop].vertex_index]
    return dict(
        faces=len(face_indices),
        vertices=len(vertices),
        rings=[len(r) for r in rings],
        radial_fractions=[1, 0.72, 0.42, 0.16],
        reserved_bounds=[lo.tolist(), hi.tolist()],
        radius=float(radius),
        method="arc-length phases; topology-matched concentric cup rings",
    )


def shape_signature(obj):
    keys = obj.data.shape_keys
    if keys is None:
        return None
    return dict(
        use_relative=keys.use_relative,
        eval_time=keys.eval_time,
        keys=[
            dict(
                name=key.name,
                value=key.value,
                relative_key=key.relative_key.name,
                slider_min=key.slider_min,
                slider_max=key.slider_max,
                mute=key.mute,
                interpolation=key.interpolation,
                coordinates_sha256=hashlib.sha256(
                    np.array([v.co[:] for v in key.data], dtype="<f8").tobytes()
                ).hexdigest(),
            )
            for key in keys.key_blocks
        ],
    )


def integrate_oral(input_path, output):
    output = Path(output).resolve()
    input_path = Path(input_path).resolve()
    target = output / "Ren_P2_Oral_FaceUV.blend"
    if target == input_path:
        raise ValueError("Cannot overwrite the oral source")
    output.mkdir(parents=True, exist_ok=True)
    input_hash = hashlib.sha256(input_path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(input_path))
    meshes = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    before = {
        name: dict(
            geometry=geometry_hash(o.data),
            shapes=shape_signature(o),
            world_matrix=[list(row) for row in o.matrix_world],
        )
        for name, o in meshes.items()
    }
    head = meshes["tripo_node_91cffa7c"]
    old_uv = {
        layer.name: np.array([v.uv[:] for v in layer.data])
        for layer in head.data.uv_layers
    }
    mapping = json.loads((OUT / "source-corner-uv-map.json").read_text())
    records = json.loads((OUT / "uv-report.json").read_text())["island_regions"]
    patch = json.loads((input_path.parent / "source-id-patch.json").read_text())
    if patch.get("derived_blend_sha256") not in (None, input_hash):
        raise ValueError("Oral topology manifest does not match the input blend hash")
    result = apply_mapping(head, mapping, allow_unmapped=True)
    face_ids = head.data.attributes["source_face_id"]
    cavity = [i for i, item in enumerate(face_ids.data) if item.value < 0]
    if set(cavity) != set(result["unmapped_polygons"]):
        raise ValueError("Unmapped faces extend beyond the explicit new oral cavity")
    freed = next(row for row in records if row["region"] == "old-interior")
    remaining = {item.value for item in face_ids.data if item.value >= 0}
    if remaining.intersection(freed["source_face_ids"]):
        raise ValueError("The reserved old-interior atlas region is still occupied")
    cavity_report = assign_cavity_uv(
        head, cavity, patch["aperture_source_vertex_ids"], freed["uv_bounds"]
    )
    overlap = exact_overlaps(head.data)
    write(output / "exact-overlap-report.json", overlap)
    if overlap["positive_area_conflicts"]:
        raise RuntimeError("Integrated head UV intersections")
    for name, o in meshes.items():
        assert before[name]["geometry"] == geometry_hash(o.data)
        assert before[name]["shapes"] == shape_signature(o)
        assert before[name]["world_matrix"] == [list(row) for row in o.matrix_world]
    for name, array in old_uv.items():
        assert np.array_equal(
            array, np.array([v.uv[:] for v in head.data.uv_layers[name].data])
        )
    expected_uv = np.array([v.uv[:] for v in head.data.uv_layers[LAYER].data])
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    bpy.ops.wm.open_mainfile(filepath=str(target))
    for name, sig in before.items():
        obj = bpy.data.objects[name]
        assert sig["geometry"] == geometry_hash(obj.data) and sig[
            "shapes"
        ] == shape_signature(obj)
        assert sig["world_matrix"] == [list(row) for row in obj.matrix_world]
    saved = bpy.data.objects["tripo_node_91cffa7c"].data
    assert np.array_equal(
        expected_uv, np.array([v.uv[:] for v in saved.uv_layers[LAYER].data])
    )
    for name, array in old_uv.items():
        assert np.array_equal(
            array, np.array([v.uv[:] for v in saved.uv_layers[name].data])
        )
    assert hashlib.sha256(input_path.read_bytes()).hexdigest() == input_hash
    report = dict(
        status="UV_ONLY_ORAL_INTEGRATION_VERIFIED",
        source_blend=str(input_path),
        source_blend_sha256=input_hash,
        derived_blend_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
        blender_version=bpy.app.version_string,
        native_mapping=result,
        cavity=cavity_report,
        exact_uv_conflicts=overlap["positive_area_conflicts"],
        all_mesh_and_shape_signatures=before,
        original_uv_layers_exact=True,
        saved_uv_exact=True,
        source_file_unchanged=True,
    )
    write(output / "integration-report.json", report)
    print("REN_ORAL_UV_INTEGRATION_VERIFIED", target, flush=True)


def mesh_preservation_signature(obj):
    """Capture all persisted mesh attributes except the intentionally edited UV."""
    mesh = obj.data
    attributes = {}
    for attr in mesh.attributes:
        if attr.name == LAYER:
            continue
        if not len(attr.data):
            values = []
        else:
            first = attr.data[0]
            prop = next(
                (p for p in ["value", "vector", "color"] if hasattr(first, p)), None
            )
            if prop is None:
                raise ValueError(f"Unsupported attribute storage: {attr.name}")
            values = []
            for item in attr.data:
                v = getattr(item, prop)
                values.append(list(v) if hasattr(v, "__len__") else v)
        attributes[attr.name] = dict(
            domain=attr.domain,
            type=attr.data_type,
            sha256=hashlib.sha256(
                json.dumps(values, separators=(",", ":")).encode()
            ).hexdigest(),
        )
    return dict(
        geometry=geometry_hash(mesh),
        shapes=shape_signature(obj),
        attributes=attributes,
        matrix=[list(row) for row in obj.matrix_world],
        materials=[m.name if m else None for m in mesh.materials],
        face_materials=[p.material_index for p in mesh.polygons],
        smooth=[p.use_smooth for p in mesh.polygons],
        corner_normals_sha256=hashlib.sha256(
            np.array([n.vector[:] for n in mesh.corner_normals], dtype="<f8").tobytes()
        ).hexdigest(),
    )


def extend_eye_chart(obj, part):
    """Interpolate through the exact surviving outer per-corner UV boundary."""
    mesh = obj.data
    layer = mesh.uv_layers[LAYER]
    part_attr = mesh.attributes["face_part"]
    source = mesh.attributes["source_vertex_id"]
    selected = [p.index for p in mesh.polygons if part_attr.data[p.index].value == part]
    vertices = sorted({v for i in selected for v in mesh.polygons[i].vertices})
    outer = [v for v in vertices if source.data[v].value >= 0]
    if len(outer) != 43:
        raise ValueError("Expected 43 shared outer eye anchors")
    selected_set = set(selected)
    known = defaultdict(list)
    for face in mesh.polygons:
        if face.index in selected_set:
            continue
        for loop in face.loop_indices:
            vertex = mesh.loops[loop].vertex_index
            if vertex in outer:
                known[vertex].append(np.array(layer.data[loop].uv[:]))
    target = []
    for vertex in outer:
        values = known[vertex]
        if not values:
            raise ValueError("Outer eye anchor has no surviving UV corner")
        if np.max(np.linalg.norm(np.array(values) - values[0], axis=1)) > 1e-7:
            raise ValueError("Outer eye boundary crosses an existing UV seam")
        target.append(values[0])
    xyz = np.array([obj.matrix_world @ mesh.vertices[v].co for v in vertices])
    index = {v: i for i, v in enumerate(vertices)}
    known_indices = [index[v] for v in outer]
    plane = xyz[:, 1:3]
    center = plane[known_indices].mean(axis=0)
    scale = np.linalg.norm(np.ptp(plane[known_indices], axis=0))
    plane = (plane - center) / scale
    anchors = plane[known_indices]

    def kernel(a, b):
        r2 = np.sum((a[:, None, :] - b[None, :, :]) ** 2, axis=2)
        return 0.5 * r2 * np.log(np.maximum(r2, 1e-30))

    affine = np.c_[np.ones(len(anchors)), anchors]
    system = np.block(
        [[kernel(anchors, anchors), affine], [affine.T, np.zeros((3, 3))]]
    )
    coefficients = np.linalg.solve(system, np.r_[np.array(target), np.zeros((3, 2))])
    values = np.c_[kernel(plane, anchors), np.ones(len(plane)), plane] @ coefficients
    # Assign exact shared float values after interpolation, ensuring welded UVs.
    for i, value in zip(known_indices, target):
        values[i] = value
    for face_index in selected:
        for loop in mesh.polygons[face_index].loop_indices:
            layer.data[loop].uv = values[index[mesh.loops[loop].vertex_index]]
    mesh.calc_loop_triangles()
    uv_area = 0
    anisotropy = []
    min_double_area = float("inf")
    for tri in mesh.loop_triangles:
        if tri.polygon_index not in selected_set:
            continue
        q = values[[index[v] for v in tri.vertices]]
        p = xyz[[index[v] for v in tri.vertices]]
        a, b = p[1] - p[0], p[2] - p[0]
        length = np.linalg.norm(a)
        area2 = np.linalg.norm(np.cross(a, b))
        double_area = abs(np.linalg.det((q[1:] - q[0]).T))
        min_double_area = min(min_double_area, double_area)
        uv_area += double_area / 2
        basis = np.array([[length, np.dot(a, b) / length], [0, area2 / length]])
        singular = np.linalg.svd(
            (q[1:] - q[0]).T @ np.linalg.inv(basis), compute_uv=False
        )
        anisotropy.append(float(singular[0] / singular[-1]))
    if min_double_area <= 1e-12 or not np.isfinite(values).all():
        raise ValueError("Eye UV extension has degenerate or nonfinite triangles")
    return dict(
        face_part=part,
        faces=len(selected),
        vertices=len(vertices),
        outer_anchors=len(outer),
        method="thin-plate UV extension in native YZ from exact surviving boundary corners",
        uv_bounds=[values.min(axis=0).tolist(), values.max(axis=0).tolist()],
        outer_boundary_uv_max_error=0,
        estimated_texels_at_1024=uv_area * 1024**2,
        anisotropy_p50_p95_max=np.percentile(anisotropy, [50, 95, 100]).tolist(),
        minimum_uv_triangle_double_area=min_double_area,
    )


def integrate_eyes(input_path, output):
    input_path = Path(input_path).resolve()
    output = Path(output).resolve()
    target = output / "Ren_P2_Face_EyeUV.blend"
    if input_path == target:
        raise ValueError("Cannot overwrite integrated source")
    output.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(input_path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(input_path))
    before = {
        o.name: mesh_preservation_signature(o)
        for o in bpy.data.objects
        if o.type == "MESH"
    }
    head = bpy.data.objects["Ren_Head"]
    mesh = head.data
    original = np.array([v.uv[:] for v in mesh.uv_layers[LAYER].data])
    parts = mesh.attributes["face_part"]
    protected = [
        i
        for p in mesh.polygons
        if parts.data[p.index].value not in (2, 3)
        for i in p.loop_indices
    ]
    eyes = [extend_eye_chart(head, part) for part in (2, 3)]
    current = np.array([v.uv[:] for v in mesh.uv_layers[LAYER].data])
    assert np.array_equal(original[protected], current[protected])
    overlap = exact_overlaps(mesh)
    write(output / "exact-overlap-report.json", overlap)
    write(output / "eye-uv-attempt.json", dict(eyes=eyes, source_sha256=source_hash))
    if overlap["positive_area_conflicts"]:
        raise RuntimeError("Eye extension has UV overlaps; reject derivative")
    for name, sig in before.items():
        assert mesh_preservation_signature(bpy.data.objects[name]) == sig
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    bpy.ops.wm.open_mainfile(filepath=str(target))
    for name, sig in before.items():
        assert mesh_preservation_signature(bpy.data.objects[name]) == sig
    head = bpy.data.objects["Ren_Head"]
    mesh = head.data
    assert np.array_equal(
        current, np.array([v.uv[:] for v in mesh.uv_layers[LAYER].data])
    )
    report = dict(
        status="UV_ONLY_EYE_INTEGRATION_VERIFIED",
        source_blend=str(input_path),
        source_sha256=source_hash,
        derived_sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
        eyes=eyes,
        exact_uv_conflicts=0,
        geometry_shapes_attributes_materials_matrices_exact=True,
        protected_face_uv_exact=True,
        saved_uv_exact=True,
        blender_version=bpy.app.version_string,
    )
    write(output / "integration-report.json", report)
    write(output / "preservation-signatures.json", before)
    # Checker is transient review setup; the saved derivative retains all materials.
    material = bpy.data.materials.new("EyeUV_Transient_Checker")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    uvnode = nodes.new("ShaderNodeUVMap")
    uvnode.uv_map = LAYER
    checker = nodes.new("ShaderNodeTexChecker")
    checker.inputs["Scale"].default_value = 32
    checker.inputs["Color1"].default_value = (0.12, 0.32, 0.50, 1)
    checker.inputs["Color2"].default_value = (0.8, 0.8, 0.8, 1)
    links.new(uvnode.outputs["UV"], checker.inputs["Vector"])
    links.new(
        checker.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"]
    )
    for i in range(len(mesh.materials)):
        mesh.materials[i] = material
    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 24
    for obj in scene.objects:
        if obj.type == "MESH" and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name != "Basis":
                    key.value = 1 if key.name == "mouthSeal" else 0
    for label, position in [
        ("front", (3, 0, 0)),
        ("quarter", (2.5, -1.7, 0)),
        ("profile", (0, -3, 0)),
    ]:
        scene.camera.location = position
        scene.camera.rotation_euler = (
            (Vector((0, 0, 0)) - scene.camera.location)
            .to_track_quat("-Z", "Y")
            .to_euler()
        )
        scene.camera.data.ortho_scale = 1.12
        scene.render.filepath = str(output / f"checker-{label}.png")
        bpy.ops.render.render(write_still=True)
    for obj in scene.objects:
        if obj.type == "MESH" and obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name.startswith("eyeBlink"):
                    key.value = 1
    for label, position in [("front", (3, 0, 0)), ("quarter", (2.5, -1.7, 0))]:
        scene.camera.location = position
        scene.camera.rotation_euler = (
            (Vector((0, 0, 0)) - scene.camera.location)
            .to_track_quat("-Z", "Y")
            .to_euler()
        )
        scene.render.filepath = str(output / f"checker-blink-{label}.png")
        bpy.ops.render.render(write_still=True)
    assert hashlib.sha256(input_path.read_bytes()).hexdigest() == source_hash
    print("REN_EYE_UV_INTEGRATION_VERIFIED", target, flush=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source = AUDIT / "audit-scene.blend"
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(source))
    name = json.loads((AUDIT / "import-metadata.json").read_text())["objects"][0][
        "object"
    ]
    obj = bpy.data.objects[name]
    mesh = obj.data
    before = geometry_hash(mesh)
    if before != "b172c32bb39b18ed7756df3f5d0d19f4d3c41d56368450f219eceb3950e195bc":
        raise RuntimeError(
            "Unexpected native geometry; local seam IDs require re-review"
        )
    original_uv = {
        layer.name: np.array([v.uv[:] for v in layer.data]) for layer in mesh.uv_layers
    }
    labels = np.load(AUDIT / "analysis-data/mesh-000-component-labels.npz")[
        "vertex_component"
    ]
    interior = set(
        json.loads((BASE / "mouth-v1/aperture-trace.json").read_text())[
            "interior_source_face_ids"
        ]
    )
    records, groups = unwrap(obj, labels, interior)
    attr = mesh.attributes.get("source_vertex_id") or mesh.attributes.new(
        "source_vertex_id", "INT", "POINT"
    )
    attr.data.foreach_set("value", np.arange(len(mesh.vertices), dtype=np.int32))
    mapping = dict(
        version=1,
        native_fbx_sha256="cbb8f2da55edf79d138eafadf1609a89b15792f74937160244f2bd527fda1819",
        source_geometry_sha256=before,
        layer=LAYER,
        faces=[
            dict(
                source_face_id=p.index,
                source_vertex_ids=list(p.vertices),
                region=groups[p.index],
                uv=[list(mesh.uv_layers[LAYER].data[i].uv) for i in p.loop_indices],
            )
            for p in mesh.polygons
        ],
    )
    write(OUT / "source-corner-uv-map.json", mapping)
    report = metrics(obj, records)
    overlap = exact_overlaps(mesh)
    write(OUT / "exact-overlap-report.json", overlap)
    report["exact_positive_area_uv_conflicts"] = overlap["positive_area_conflicts"]
    report["exact_overlap_area_epsilon"] = overlap["uv_area_epsilon"]
    if overlap["positive_area_conflicts"]:
        raise RuntimeError("Positive-area UV intersections remain; reject study")
    assert geometry_hash(mesh) == before
    for name, array in original_uv.items():
        assert np.array_equal(
            array, np.array([v.uv[:] for v in mesh.uv_layers[name].data])
        )
    report.update(
        geometry_before_sha256=before,
        geometry_after_sha256=geometry_hash(mesh),
        original_uv_layers_unchanged=True,
        blender_version=bpy.app.version_string,
        island_regions=records,
    )
    write(OUT / "uv-report.json", report)
    material = bpy.data.materials.new("FaceUV_Checker_24")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    uvnode = nodes.new("ShaderNodeUVMap")
    uvnode.uv_map = LAYER
    checker = nodes.new("ShaderNodeTexChecker")
    checker.inputs["Scale"].default_value = 24
    checker.inputs["Color1"].default_value = (0.12, 0.32, 0.50, 1)
    checker.inputs["Color2"].default_value = (0.8, 0.8, 0.8, 1)
    links.new(uvnode.outputs["UV"], checker.inputs["Vector"])
    links.new(
        checker.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"]
    )
    nodes.get("Principled BSDF").inputs["Roughness"].default_value = 0.85
    for i in range(len(mesh.materials)):
        mesh.materials[i] = material
    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 24
    for label, position in [
        ("front", (3, 0, 0)),
        ("quarter", (2.5, -1.7, 0)),
        ("profile", (0, -3, 0)),
    ]:
        scene.camera.location = position
        scene.camera.rotation_euler = (
            (Vector((0, 0, 0)) - scene.camera.location)
            .to_track_quat("-Z", "Y")
            .to_euler()
        )
        scene.camera.data.ortho_scale = 1.12
        scene.render.filepath = str(OUT / f"checker-{label}.png")
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "Ren_P2_FaceUV_Study.blend"))
    assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
    print(
        "REN_FACE_UV_STUDY_READY",
        report["islands"],
        report["overlap_2048_conflicting_interior_samples"],
        flush=True,
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--oral-input", type=Path)
    parser.add_argument("--eye-input", type=Path)
    parser.add_argument("--output", type=Path, default=OUT / "oral-integration")
    args = parser.parse_args(
        sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    )
    if args.eye_input:
        integrate_eyes(args.eye_input, args.output)
    elif args.oral_input:
        integrate_oral(args.oral_input, args.output)
    else:
        main()
