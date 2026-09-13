"""Prepare and audit a STATIC Tripo texture upload copy; no API calls."""

from pathlib import Path
import hashlib
import json
import struct
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
SOURCE = BASE / "face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend"
SOURCE_SHA = "6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959"
OUT = BASE / "tripo-texture-v1/input"
AXIS = np.array([[1, 0, 0], [0, 0, 1], [0, -1, 0]], dtype=float)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def capture_mesh(obj, index):
    mesh = obj.data
    mesh.calc_loop_triangles()
    local = np.array([v.co[:] for v in mesh.vertices], dtype=np.float64)
    world = np.array([obj.matrix_world @ v.co for v in mesh.vertices], dtype=np.float64)
    loops = np.array([loop.vertex_index for loop in mesh.loops], dtype=np.int32)
    uvs = {
        f"uv_{i}": np.array([d.uv[:] for d in layer.data])
        for i, layer in enumerate(mesh.uv_layers)
    }
    arrays = dict(
        local_positions=local,
        world_positions=world,
        loop_vertex=loops,
        triangles=np.array(
            [t.vertices[:] for t in mesh.loop_triangles], dtype=np.int32
        ),
        triangle_loops=np.array(
            [t.loops[:] for t in mesh.loop_triangles], dtype=np.int32
        ),
        face_start=np.array([p.loop_start for p in mesh.polygons], dtype=np.int32),
        face_size=np.array([p.loop_total for p in mesh.polygons], dtype=np.int32),
        material_indices=np.array(
            [p.material_index for p in mesh.polygons], dtype=np.int32
        ),
        **uvs,
    )
    file = OUT / f"mesh-{index:02d}.npz"
    np.savez_compressed(file, **arrays)
    record = dict(
        object=obj.name,
        vertices=len(local),
        faces=len(mesh.polygons),
        triangles=len(mesh.loop_triangles),
        matrix_world=[list(row) for row in obj.matrix_world],
        uv_layers=[layer.name for layer in mesh.uv_layers],
        materials=[m.name if m else None for m in mesh.materials],
        archive=file.name,
        archive_sha256=sha(file),
        world_positions_sha256=hashlib.sha256(world.tobytes()).hexdigest(),
        uv_sha256={
            name: hashlib.sha256(value.tobytes()).hexdigest()
            for name, value in uvs.items()
        },
        world_bounds=[world.min(axis=0).tolist(), world.max(axis=0).tolist()],
    )
    return record, arrays


def audit_glb(path, expected):
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", data)
    assert magic == 0x46546C67 and version == 2 and length == len(data)
    pos = 12
    document = None
    binary = None
    while pos < len(data):
        size, kind = struct.unpack_from("<II", data, pos)
        chunk = data[pos + 8 : pos + 8 + size]
        pos += 8 + size
        if kind == 0x4E4F534A:
            document = json.loads(chunk)
        elif kind == 0x004E4942:
            binary = chunk
    if document.get("skins") or document.get("animations"):
        raise AssertionError("Upload is not static")

    def accessor(index):
        a = document["accessors"][index]
        view = document["bufferViews"][a["bufferView"]]
        dtype = {5126: "<f4", 5125: "<u4", 5123: "<u2", 5121: "u1"}[a["componentType"]]
        count = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        item = np.dtype(dtype).itemsize
        stride = view.get("byteStride", item * count)
        offset = view.get("byteOffset", 0) + a.get("byteOffset", 0)
        return np.ndarray(
            (a["count"], count),
            dtype=dtype,
            buffer=binary,
            offset=offset,
            strides=(stride, item),
        ).copy()

    worlds = {}

    def walk(index, parent):
        n = document["nodes"][index]
        if "matrix" in n:
            local = np.array(n["matrix"]).reshape(4, 4, order="F")
        else:
            q = n.get("rotation", [0, 0, 0, 1])
            rot = Quaternion((q[3], *q[:3])).to_matrix().to_4x4()
            local = np.array(
                Matrix.Translation(Vector(n.get("translation", [0, 0, 0])))
                @ rot
                @ Matrix.Diagonal((*n.get("scale", [1, 1, 1]), 1))
            )
        worlds[index] = parent @ local
        for child in n.get("children", []):
            walk(child, worlds[index])

    for index in document["scenes"][document.get("scene", 0)]["nodes"]:
        walk(index, np.eye(4))
    results = []
    for index, node in enumerate(document["nodes"]):
        if "mesh" not in node:
            continue
        name = node["name"]
        source = expected[name]
        points = source["world_positions"][source["loop_vertex"]] @ AXIS.T
        uv = source.get("uv_0")
        tree = KDTree(len(points))
        for i, p in enumerate(points):
            tree.insert(p, i)
        tree.balance()
        max_position = 0
        max_uv = 0
        exported = 0
        triangles = 0
        for primitive in document["meshes"][node["mesh"]]["primitives"]:
            if primitive.get("targets"):
                raise AssertionError("Unexpected morph targets")
            xyz = accessor(primitive["attributes"]["POSITION"])
            xyz = (np.c_[xyz, np.ones(len(xyz))] @ worlds[index].T)[:, :3]
            out_uv = (
                accessor(primitive["attributes"]["TEXCOORD_0"])
                if "TEXCOORD_0" in primitive["attributes"]
                else None
            )
            if uv is not None and out_uv is None:
                raise AssertionError(f"UVs lost: {name}")
            for i, p in enumerate(xyz):
                matches = tree.find_range(Vector(p), 1e-6)
                if not matches:
                    raise AssertionError(f"Export changed position: {name}")
                max_position = max(max_position, min(m[2] for m in matches))
                if uv is not None:
                    original = np.array([uv[m[1]] for m in matches])
                    original[:, 1] = 1 - original[:, 1]
                    error = float(np.linalg.norm(original - out_uv[i], axis=1).min())
                    max_uv = max(max_uv, error)
                    if error > 1e-6:
                        raise AssertionError(f"Export changed UV corner: {name}")
            exported += len(xyz)
            triangles += len(accessor(primitive["indices"])) // 3
        if triangles != len(source["triangles"]):
            raise AssertionError(f"Triangle count changed: {name}")
        results.append(
            dict(
                object=name,
                exported_split_vertices=exported,
                triangles=triangles,
                max_world_position_error=max_position,
                max_uv_error=max_uv,
                gltf_world_matrix=worlds[index].tolist(),
            )
        )
    if {r["object"] for r in results} != set(expected):
        raise AssertionError("Export object set changed")
    write(OUT / "glb-structure.json", document)
    return dict(
        objects=results,
        axis_map_blender_to_gltf=AXIS.tolist(),
        front="Blender +X remains raw glTF +X",
        up="Blender +Z maps raw glTF +Y",
        uv_convention="glTF TEXCOORD_0 is Blender selected UV with V flipped",
        coordinate_tolerance=1e-6,
        static=True,
        materials=document.get("materials", []),
    )


def main():
    if (OUT / "Ren_P2_Texture_ClosedRest.glb").exists():
        raise FileExistsError("Texture-trial input is frozen; do not overwrite a submitted GLB")
    if sha(SOURCE) != SOURCE_SHA:
        raise ValueError("Unexpected corrected head checkpoint")
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene = bpy.context.scene
    objects = [o for o in scene.objects if o.type == "MESH" and not o.hide_render]
    pose = {}
    for obj in objects:
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                if key.name != "Basis":
                    key.value = 1 if key.name == "mouthSeal" else 0
            pose[obj.name] = {k.name: k.value for k in obj.data.shape_keys.key_blocks}
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        evaluated = obj.evaluated_get(graph)
        positions = np.array([v.co[:] for v in evaluated.data.vertices])
        mesh = bpy.data.meshes.new_from_object(
            evaluated, preserve_all_data_layers=True, depsgraph=graph
        )
        if not np.array_equal(positions, np.array([v.co[:] for v in mesh.vertices])):
            raise AssertionError("Static capture changed evaluated positions")
        obj.modifiers.clear()
        obj.data = mesh
        if mesh.shape_keys:
            raise AssertionError("Static upload retains shape keys")
    head = bpy.data.objects["Ren_Head"]
    layer = head.data.uv_layers["FaceUV_v1"]
    expected_uv = np.array([v.uv[:] for v in layer.data])
    for other in list(head.data.uv_layers):
        if other.name != "FaceUV_v1":
            head.data.uv_layers.remove(other)
    head.data.uv_layers.active_index = 0
    head.data.uv_layers[0].active_render = True
    assert np.array_equal(
        expected_uv, np.array([v.uv[:] for v in head.data.uv_layers[0].data])
    )
    sys.path.insert(0, str(ROOT / "tools/character_art"))
    from prepare_ren_paint_views import neutral_material

    neutral = neutral_material()
    replacements = []
    for obj in objects:
        for index, mat in enumerate(obj.data.materials):
            if mat and ("Draft_Skin" in mat.name or "Draft_RoseLips" in mat.name):
                replacements.append(
                    dict(
                        object=obj.name,
                        slot=index,
                        previous=mat.name,
                        replacement=neutral.name,
                    )
                )
                obj.data.materials[index] = neutral
    records = []
    expected = {}
    for index, obj in enumerate(objects):
        record, arrays = capture_mesh(obj, index)
        records.append(record)
        expected[obj.name] = arrays
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    glb = OUT / "Ren_P2_Texture_ClosedRest.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(glb),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_animations=False,
        export_skins=False,
        export_morph=False,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_vertex_color="MATERIAL",
        export_all_vertex_colors=False,
        export_draco_mesh_compression_enable=False,
    )
    if glb.stat().st_size >= 150_000_000:
        raise AssertionError("Upload exceeds 150 MB")
    audit = audit_glb(glb, expected)
    write(OUT / "glb-audit.json", audit)
    snapshot = OUT / "Ren_ClosedRest_Upload.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(snapshot))
    report = dict(
        source_blend=str(SOURCE),
        source_sha256=SOURCE_SHA,
        glb=glb.name,
        glb_sha256=sha(glb),
        glb_bytes=glb.stat().st_size,
        blender_version=bpy.app.version_string,
        objects=records,
        static_pose=pose,
        material_replacements=replacements,
        axis_audit=audit,
        scope="Static evaluated texture-service COPY; no remesh; working head untouched",
    )
    write(OUT / "input-manifest.json", report)
    print("TRIPO_UPLOAD_GLB_VERIFIED", glb, sha(glb), flush=True)
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    renders = []
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
        scene.render.filepath = str(OUT / f"input-{label}.png")
        bpy.ops.render.render(write_still=True)
        renders.append(
            dict(file=f"input-{label}.png", sha256=sha(OUT / f"input-{label}.png"))
        )
    report["renders"] = renders
    # Fresh glTF import verifies the renderer's actual interpretation too.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    reimport = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        original = expected[obj.name]["world_positions"]
        tree = KDTree(len(original))
        for i, p in enumerate(original):
            tree.insert(p, i)
        tree.balance()
        errors = [tree.find(obj.matrix_world @ v.co)[2] for v in obj.data.vertices]
        if max(errors) > 1e-6:
            raise AssertionError("Reimport changed source frame")
        reimport.append(
            dict(
                object=obj.name,
                max_world_position_error=max(errors),
                matrix=[list(row) for row in obj.matrix_world],
            )
        )
    report["reimport"] = reimport
    report["snapshot_sha256"] = sha(snapshot)
    write(OUT / "input-manifest.json", report)
    assert sha(SOURCE) == SOURCE_SHA
    print("TRIPO_INPUT_PREPARATION_COMPLETE", glb, flush=True)


if __name__ == "__main__":
    main()
