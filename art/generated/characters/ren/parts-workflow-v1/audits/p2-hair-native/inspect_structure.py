"""Read-only supplemental checks of the imported P2 hair and its OBJ/GLB exports."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct
import numpy as np

audit = Path(__file__).resolve().parent
derived = audit.parents[1] / "derived/hair"
report = json.loads((audit / "report.json").read_text())
source = Path(report["source"])
assert hashlib.sha256(source.read_bytes()).hexdigest() == report["source_sha256"]
with np.load(audit / "analysis-data/mesh-000.npz") as data:
    starts, sizes, loops = data["face_start"], data["face_size"], data["loop_vertex"]
    faces = [tuple(map(int, loops[start:start+size])) for start, size in zip(starts, sizes)]
    keys = [tuple(sorted(face)) for face in faces]
    occurrences = Counter(keys)
    duplicate_groups = {face: count for face, count in occurrences.items() if count > 1}
    duplicate_extra_faces = sum(count - 1 for count in duplicate_groups.values())
    repeated_corner_faces = sum(len(set(face)) != len(face) for face in faces)
    edge_incidence = np.bincount(data["loop_edge"], minlength=len(data["edges"]))
    uv = data["uv_0"]
    duplicated_with_distinct_uv = 0
    seen = {}
    for face, key, start, size in zip(faces, keys, starts, sizes):
        if key not in duplicate_groups:
            continue
        mapping = tuple(sorted((vertex, *uv[start+i]) for i, vertex in enumerate(face)))
        if key in seen and seen[key] != mapping:
            duplicated_with_distinct_uv += 1
        seen[key] = mapping
obj_file = derived / "ren-p2-hair-native-quads.obj"
obj_sizes = Counter()
obj_vertices = 0
for line in obj_file.read_text().splitlines():
    if line.startswith("f "):
        obj_sizes[len(line.split())-1] += 1
    elif line.startswith("v "):
        obj_vertices += 1
glb_file = derived / "ren-p2-hair-triangulated.glb"
glb_bytes = glb_file.read_bytes()
length, kind = struct.unpack_from("<II", glb_bytes, 12)
assert kind == 0x4e4f534a
glb = json.loads(glb_bytes[20:20+length])
primitives = [primitive for mesh in glb["meshes"] for primitive in mesh["primitives"]]
assert all(primitive.get("mode", 4) == 4 for primitive in primitives)
glb_triangles = sum(glb["accessors"][p["indices"]]["count"] // 3 for p in primitives)
assert obj_vertices == report["totals"]["vertices"]
assert dict(obj_sizes) == {int(size): count for size, count in report["objects"][0]["face_types"].items()}
assert glb_triangles == report["totals"]["triangles"]
assert report["camera"]["front_yaw_degrees"] == 90
inspection = json.loads((audit / "inspection-renders.json").read_text())
assert len(report["renders"]) == 9
for record in report["renders"] + inspection:
    assert hashlib.sha256((audit / record["path"]).read_bytes()).hexdigest() == record["sha256"]
exports = json.loads((derived / "provenance.json").read_text())
for record in exports["exports"]:
    assert hashlib.sha256((derived / record["path"]).read_bytes()).hexdigest() == record["sha256"]
result = {"source_sha256": report["source_sha256"], "source_verified_unchanged": True,
          "same_vertex_set_duplicate_face_groups": len(duplicate_groups),
          "same_vertex_set_duplicate_extra_faces": duplicate_extra_faces,
          "duplicate_extras_with_different_uv_mapping": duplicated_with_distinct_uv,
          "faces_with_repeated_vertex_corners": repeated_corner_faces,
          "imported_edge_face_incidence_histogram": dict(sorted(Counter(map(int, edge_incidence)).items())),
          "obj_native_face_sizes_verified": dict(obj_sizes), "obj_vertices": obj_vertices,
          "glb_triangles_verified": glb_triangles,
          "front_camera_yaw_degrees_verified": 90,
          "render_hashes_verified": len(report["renders"]) + len(inspection),
          "derivative_hashes_verified": len(exports["exports"]),
          "note": "Duplicate detection compares unordered vertex sets, not an intersection test. No faces were removed or repaired."}
(audit / "supplemental-structure.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(result, indent=2))
