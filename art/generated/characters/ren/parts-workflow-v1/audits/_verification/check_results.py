"""Check actual Blender FBX/GLB imports against the authored synthetic fixture."""
import ast
import hashlib
import json
from pathlib import Path

audits = Path(__file__).resolve().parent.parent
workspace = audits.parents[5]
ast.parse((workspace / "tools/character_art/audit_ren_parts.py").read_text(encoding="utf-8"))
checks = []
for run_id, expected_faces in (("verification-native-fbx", 4), ("verification-triangle-glb", 8)):
    report = json.loads((audits / run_id / "report.json").read_text())
    totals = report["totals"]
    assert totals["vertices"] == 16
    assert totals["faces"] == expected_faces
    assert totals["triangles"] == 8
    assert totals["imported_components"] == 4
    assert totals["coincident_position_components"] == 3
    assert report["source_hash_verified_unchanged"]
    assert hashlib.sha256(Path(report["source"]).read_bytes()).hexdigest() == report["source_sha256"]
    meshes = {mesh["object"]: mesh for mesh in report["objects"]}
    if run_id == "verification-native-fbx":
        assert meshes["SeamedQuads"]["face_types"] == {"4": 2}
        assert meshes["NgonAndTriangle"]["face_types"] == {"3": 1, "5": 1}
    else:
        assert all(mesh["face_types"] == {"3": 4} for mesh in meshes.values())
    for mesh in meshes.values():
        assert mesh["uv_analysis"][0]["islands"] == 2
        assert len(mesh["materials"]) == 1
    checks.append({"run": run_id, "passed": True, "source_sha256": report["source_sha256"]})

baseline = audits / "tripo-h31-closed-baseline"
report = json.loads((baseline / "report.json").read_text())
assert report["source_hash_verified_unchanged"]
assert hashlib.sha256(Path(report["source"]).read_bytes()).hexdigest() == report["source_sha256"]
assert len(report["renders"]) == 9
for render in report["renders"]:
    assert hashlib.sha256((baseline / render["path"]).read_bytes()).hexdigest() == render["sha256"]
checks.append({"run": baseline.name, "passed": True, "nine_render_hashes_valid": True,
               "source_sha256": report["source_sha256"]})
(audits / "_verification/results.json").write_text(json.dumps(checks, indent=2) + "\n")
print(json.dumps(checks, indent=2))
