"""Audit actual returned Tripo texture output and export an honest static review."""

from pathlib import Path
from collections import Counter, defaultdict
import hashlib
import itertools
import json
import struct
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1/tripo-texture-v1"
SOURCE = BASE / "input/Ren_P2_Texture_ClosedRest.glb"
RETURNED = BASE / "originals/model.glb"
REVIEW = BASE / "review"
EXPORT = BASE / "unity-export"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, data):
    Path(path).write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


class Glb:
    def __init__(self, path):
        self.path = Path(path)
        raw = self.path.read_bytes()
        magic, version, length = struct.unpack_from("<III", raw)
        if (magic, version, length) != (0x46546C67, 2, len(raw)):
            raise ValueError("Expected ordinary GLB2")
        pos = 12
        while pos < len(raw):
            size, kind = struct.unpack_from("<II", raw, pos)
            chunk = raw[pos + 8 : pos + 8 + size]
            pos += size + 8
            if kind == 0x4E4F534A:
                self.json = json.loads(chunk)
            elif kind == 0x004E4942:
                self.binary = chunk
        self.world = {}
        for i in self.json["scenes"][self.json.get("scene", 0)]["nodes"]:
            self.walk(i, np.eye(4))

    def walk(self, i, parent):
        node = self.json["nodes"][i]
        if "matrix" in node:
            m = np.array(node["matrix"]).reshape(4, 4, order="F")
        else:
            q = node.get("rotation", [0, 0, 0, 1])
            m = np.array(
                Matrix.Translation(Vector(node.get("translation", [0, 0, 0])))
                @ Quaternion((q[3], *q[:3])).to_matrix().to_4x4()
                @ Matrix.Diagonal((*node.get("scale", [1, 1, 1]), 1))
            )
        self.world[i] = parent @ m
        for child in node.get("children", []):
            self.walk(child, self.world[i])

    def accessor(self, i):
        a = self.json["accessors"][i]
        v = self.json["bufferViews"][a["bufferView"]]
        if "sparse" in a:
            raise ValueError("Sparse accessor unsupported")
        dtype = {5126: "<f4", 5125: "<u4", 5123: "<u2", 5121: "u1"}[a["componentType"]]
        width = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        item = np.dtype(dtype).itemsize
        return np.ndarray(
            (a["count"], width),
            dtype=dtype,
            buffer=self.binary,
            offset=v.get("byteOffset", 0) + a.get("byteOffset", 0),
            strides=(v.get("byteStride", width * item), item),
        ).copy()

    def meshes(self):
        result = {}
        for i, node in enumerate(self.json["nodes"]):
            if "mesh" not in node:
                continue
            positions = []
            triangles = []
            uv = []
            offset = 0
            for p in self.json["meshes"][node["mesh"]]["primitives"]:
                if p.get("mode", 4) != 4:
                    raise ValueError("Non-triangle primitive")
                xyz = self.accessor(p["attributes"]["POSITION"])
                xyz = (np.c_[xyz, np.ones(len(xyz))] @ self.world[i].T)[:, :3]
                idx = self.accessor(p["indices"]).reshape(-1, 3).astype(int)
                positions.append(xyz)
                triangles.append(idx + offset)
                offset += len(xyz)
                uv.append(
                    self.accessor(p["attributes"]["TEXCOORD_0"])
                    if "TEXCOORD_0" in p["attributes"]
                    else np.full((len(xyz), 2), np.nan)
                )
            result[node.get("name", f"node-{i}")] = dict(
                positions=np.concatenate(positions),
                triangles=np.concatenate(triangles),
                uv=np.concatenate(uv),
            )
        return result


def tree(points):
    result = KDTree(len(points))
    for i, p in enumerate(points):
        result.insert(p, i)
    result.balance()
    return result


def geometry_audit(source, result):
    source_meshes = source.meshes()
    returned_meshes = result.meshes()
    source_points = np.concatenate([m["positions"] for m in source_meshes.values()])
    returned_points = np.concatenate([m["positions"] for m in returned_meshes.values()])
    source_tree = tree(source_points)
    source_center = (source_points.min(0) + source_points.max(0)) / 2
    target_center = (returned_points.min(0) + returned_points.max(0)) / 2
    sample = returned_points[
        np.linspace(
            0, len(returned_points) - 1, min(1000, len(returned_points))
        ).astype(int)
    ]
    choices = []
    for order in itertools.permutations(range(3)):
        for signs in itertools.product([-1, 1], repeat=3):
            rotation = np.eye(3)[list(order)] * np.array(signs)[:, None]
            if np.linalg.det(rotation) < 0.5:
                continue
            translation = source_center - rotation @ target_center
            errors = [
                source_tree.find(Vector(p))[2]
                for p in sample @ rotation.T + translation
            ]
            choices.append(
                (float(np.mean(errors)), float(np.max(errors)), rotation, translation)
            )
    _, _, rotation, translation = min(choices, key=lambda row: (row[0], row[1]))
    aligned = returned_points @ rotation.T + translation
    aligned_tree = tree(aligned)
    forward = np.array([source_tree.find(Vector(p))[2] for p in aligned])
    reverse = np.array([aligned_tree.find(Vector(p))[2] for p in source_points])
    # Canonical geometric vertex identities allow split/reordered export vertices.
    canonical, ids = np.unique(np.round(source_points, 6), axis=0, return_inverse=True)
    src_lookup = defaultdict(list)
    offset = 0
    for name, m in source_meshes.items():
        local_ids = ids[offset : offset + len(m["positions"])]
        offset += len(m["positions"])
        for tri in m["triangles"]:
            key = tuple(sorted(local_ids[tri]))
            order = np.argsort(local_ids[tri], kind="stable")
            src_lookup[key].append(m["uv"][tri][order])
    target_counts = Counter()
    uv_matched = 0
    uv_changed = 0
    uv_added = 0
    missing_geometry = 0
    per_object = []
    for name, m in returned_meshes.items():
        xyz = m["positions"] @ rotation.T + translation
        nearest = [source_tree.find(Vector(p)) for p in xyz]
        local_ids = np.array([ids[row[1]] for row in nearest])
        for tri in m["triangles"]:
            key = tuple(sorted(local_ids[tri]))
            target_counts[key] += 1
            candidates = src_lookup.get(key, [])
            order = np.argsort(local_ids[tri], kind="stable")
            uv = m["uv"][tri][order]
            if not candidates:
                missing_geometry += 1
                continue
            finite = [c for c in candidates if np.isfinite(c).all()]
            if not finite:
                uv_added += 1
            elif any(np.max(np.abs(c - uv)) <= 1e-6 for c in finite):
                uv_matched += 1
            else:
                uv_changed += 1
        per_object.append(
            dict(
                name=name,
                vertices=len(xyz),
                triangles=len(m["triangles"]),
                max_nearest_source_error=max(row[2] for row in nearest),
            )
        )
    source_counts = Counter({k: len(v) for k, v in src_lookup.items()})
    same_triangles = source_counts == target_counts
    return dict(
        source_sha256=sha(source.path),
        returned_sha256=sha(result.path),
        source_objects=len(source_meshes),
        returned_objects=len(returned_meshes),
        source_triangles=sum(source_counts.values()),
        returned_triangles=sum(target_counts.values()),
        alignment_rotation=rotation.tolist(),
        alignment_translation=translation.tolist(),
        alignment="Only a proper signed-axis permutation plus bbox centering; no scaling or deformation applied",
        returned_to_source_error_p50_p95_max=np.percentile(
            forward, [50, 95, 100]
        ).tolist(),
        source_to_returned_error_p50_p95_max=np.percentile(
            reverse, [50, 95, 100]
        ).tolist(),
        geometry_equal_within_1e6=bool(max(forward.max(), reverse.max()) <= 1e-6),
        geometric_triangle_multiset_equal=same_triangles,
        source_only_triangle_instances=sum((source_counts - target_counts).values()),
        returned_only_triangle_instances=sum((target_counts - source_counts).values()),
        uv_corner_triangles_matching=uv_matched,
        uv_corner_triangles_changed=uv_changed,
        uv_added_to_previously_unmapped_triangles=uv_added,
        unmatched_geometry_triangles=missing_geometry,
        geometric_identity_resolution=1e-6,
        topology_limit="Geometric triangle multiplicities checked; coincident vertex welding/partition changes are not inferred as identical editable topology",
        returned_objects_detail=per_object,
    )


def extract_images(glb):
    folder = EXPORT / "Textures"
    folder.mkdir(parents=True, exist_ok=True)
    images = []
    for i, record in enumerate(glb.json.get("images", [])):
        if "bufferView" not in record:
            raise ValueError(
                "Only original embedded-image bytes supported for this trial"
            )
        view = glb.json["bufferViews"][record["bufferView"]]
        offset = view.get("byteOffset", 0)
        payload = glb.binary[offset : offset + view["byteLength"]]
        extension = {"image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp"}[
            record["mimeType"]
        ]
        name = "".join(
            c if c.isalnum() or c in "_-" else "_"
            for c in record.get("name", f"image_{i}")
        )
        file = folder / f"{i:02d}_{name}{extension}"
        file.write_bytes(payload)
        image = bpy.data.images.load(str(file), check_existing=False)
        size = list(image.size)
        bpy.data.images.remove(image)
        images.append(
            dict(
                index=i,
                name=record.get("name"),
                file=str(file),
                sha256=sha(file),
                bytes=len(payload),
                mime_type=record["mimeType"],
                resolution=size,
            )
        )
    return images


def material_manifest(glb, images):
    def slot(value, space, channels):
        if value is None:
            return None
        image = images[glb.json["textures"][value["index"]]["source"]]
        return dict(
            **image,
            color_space=space,
            channels=channels,
            texcoord=value.get("texCoord", 0),
            texture_transform=value.get("extensions", {}).get("KHR_texture_transform"),
        )

    records = []
    for i, material in enumerate(glb.json.get("materials", [])):
        pbr = material.get("pbrMetallicRoughness", {})
        records.append(
            dict(
                index=i,
                sourceName=material.get("name", f"Material_{i}"),
                baseColor=slot(pbr.get("baseColorTexture"), "sRGB", "RGBA"),
                normal=slot(
                    material.get("normalTexture"), "linear", "OpenGL tangent normal RGB"
                ),
                metallicRoughness=slot(
                    pbr.get("metallicRoughnessTexture"),
                    "linear",
                    "roughness G; metallic B",
                ),
                baseColorFactor=pbr.get("baseColorFactor", [1, 1, 1, 1]),
                metallicFactor=pbr.get("metallicFactor", 1),
                roughnessFactor=pbr.get("roughnessFactor", 1),
                normalScale=material.get("normalTexture", {}).get("scale", 1),
                doubleSided=material.get("doubleSided", False),
                alphaMode=material.get("alphaMode", "OPAQUE"),
            )
        )
    return records


def main():
    REVIEW.mkdir(parents=True, exist_ok=True)
    EXPORT.mkdir(parents=True, exist_ok=True)
    frozen = {
        str(p): sha(p)
        for p in [
            SOURCE,
            RETURNED,
            BASE.parent / "face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend",
        ]
    }
    if (
        frozen[str(SOURCE)]
        != "a690b11aa070b846f2aaac3f472dbc4bd5a83132a0efa992fc07f15d7d753e70"
    ):
        raise ValueError("Submitted input changed")
    original = Glb(SOURCE)
    returned = Glb(RETURNED)
    audit = geometry_audit(original, returned)
    write(REVIEW / "geometry-audit.json", audit)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    images = extract_images(returned)
    materials = material_manifest(returned, images)
    write(REVIEW / "textures-materials.json", dict(images=images, materials=materials))
    bpy.ops.import_scene.gltf(filepath=str(RETURNED))
    by_name = {r["name"]: r for r in images}
    for image in bpy.data.images:
        if image.name in by_name:
            image.filepath = by_name[image.name]["file"]
    objects = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    fbx = EXPORT / "RenFaceStudyTripoTexture.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="OFF",
        path_mode="ABSOLUTE",
        embed_textures=False,
        colors_type="SRGB",
    )
    rows = []
    asset_prefix = "Assets/CharacterArt/Generated/RenFaceStudy/TextureTrial/Textures/"
    for m in materials:
        row = dict(
            sourceName=m["sourceName"],
            useVertexColor="Iris" in m["sourceName"],
            baseColorAsset=asset_prefix + Path(m["baseColor"]["file"]).name,
            baseColorFile=m["baseColor"]["file"],
            baseColorSha256=m["baseColor"]["sha256"],
            normalAsset=asset_prefix + Path(m["normal"]["file"]).name,
            normalFile=m["normal"]["file"],
            normalSha256=m["normal"]["sha256"],
            baseColorSrgb=dict(zip(["r", "g", "b", "a"], m["baseColorFactor"])),
            normalScale=m["normalScale"],
            doubleSided=m["doubleSided"],
            metallicRoughness=m["metallicRoughness"],
        )
        rows.append(row)
    manifest = dict(
        sourceFbxAsset="Assets/CharacterArt/Generated/RenFaceStudy/TextureTrial/Sources/RenFaceStudyTripoTexture.fbx",
        sourceFbxFile=str(fbx),
        sourceSha256=sha(fbx),
        returnedGlbSha256=sha(RETURNED),
        reviewNote="Actual provider whole head and eyes; no source eye replacement, no mesh correction",
        position=dict(x=0, y=0, z=0),
        eulerAngles=dict(x=0, y=90, z=0),
        scale=dict(x=1, y=1, z=1),
        sourceFrame="Blender import from raw glTF: +X front, +Z up; FBX forward -Z/up Y. Unity viewer verifies import rotation.",
        objects=[
            dict(
                name=o.name,
                materials=[m.name for m in o.data.materials],
                matrix=[list(row) for row in o.matrix_world],
            )
            for o in objects
        ],
        materials=rows,
        vertexColorEncoding="sRGB",
        allowMissingUv0OnRenderers=[o.name for o in objects if not o.data.uv_layers],
    )
    write(EXPORT / "unity-manifest.json", manifest)
    print("TRIPO_UNITY_EXPORT_READY", fbx, flush=True)
    sys.path.insert(0, str(ROOT / "tools/character_art"))
    from build_ren_p2_mouth import setup_review

    scene, camera, aim = setup_review()
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    bpy.ops.wm.save_as_mainfile(filepath=str(REVIEW / "Actual_Tripo_Output.blend"))
    captures = []
    for mode in ["neutral", "unlit"]:
        if mode == "unlit":
            for material in bpy.data.materials:
                if not material.use_nodes:
                    continue
                tree = material.node_tree
                bsdf = next(
                    (n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None
                )
                output = next(
                    (n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None
                )
                if not bsdf or not output:
                    continue
                emission = tree.nodes.new("ShaderNodeEmission")
                source = bsdf.inputs["Base Color"]
                if source.links:
                    tree.links.new(
                        source.links[0].from_socket, emission.inputs["Color"]
                    )
                else:
                    emission.inputs["Color"].default_value = source.default_value
                tree.links.new(emission.outputs[0], output.inputs["Surface"])
        for name, location in [
            ("front", (3, 0, 0)),
            ("quarter", (2.5, -1.7, 0)),
            ("profile", (0, -3, 0)),
        ]:
            camera.location = location
            aim(camera, (0, 0, 0))
            camera.data.ortho_scale = 1.12
            file = REVIEW / f"{mode}-{name}.png"
            scene.render.filepath = str(file)
            bpy.ops.render.render(write_still=True)
            captures.append(dict(file=file.name, sha256=sha(file)))
    write(REVIEW / "captures.json", captures)
    for path, value in frozen.items():
        assert sha(path) == value
    write(
        REVIEW / "completion.json",
        dict(
            status="AUDIT_AND_EXPORT_COMPLETE",
            blender_version=bpy.app.version_string,
            frozen_input_hashes=frozen,
            fbx_sha256=sha(fbx),
            captures=captures,
        ),
    )
    print("TRIPO_RETURNED_AUDIT_COMPLETE", flush=True)


if __name__ == "__main__":
    main()
