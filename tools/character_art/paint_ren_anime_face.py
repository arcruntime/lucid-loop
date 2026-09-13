"""Blender-native projected face paint; source geometry and UVs remain immutable.

Run Blender --background --python this_file -- --preview, then --finish.
This authors shader masks and bakes them through Blender; it does not edit raster
images or reuse the provider's changed geometry. Preview is deliberately separate
so art direction can be reviewed before baking and pose captures.
"""

from pathlib import Path
import argparse
import hashlib
import json
import sys

import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
SOURCE = BASE / "face-uv-v1/eye-integration-v2/Ren_P2_Face_EyeUV.blend"
SOURCE_SHA = "6f388b7c0ec135c77288a39d329e9e459c51f4121adb5629270ab42f90fa6959"
OUT = BASE / "anime-paint-v2"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_ren_p2_mouth import setup_review  # noqa: E402
from unwrap_ren_p2_head import mesh_preservation_signature  # noqa: E402


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def write(p, v):
    p.write_text(json.dumps(v, indent=2) + "\n", encoding="utf-8")


def linear(c):
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


class Paint:
    def __init__(self):
        self.mat = bpy.data.materials.new("Ren_AnimePaint_v2_GraphicSkin")
        self.mat.use_nodes = True
        self.t = self.mat.node_tree
        self.t.nodes.clear()
        tex = self.t.nodes.new("ShaderNodeTexCoord")
        xyz = self.t.nodes.new("ShaderNodeSeparateXYZ")
        self.t.links.new(tex.outputs["Object"], xyz.inputs[0])
        self.x, self.y, self.z = xyz.outputs
        self.a = self.math("ABSOLUTE", self.y)
        self.front = self.ramp(self.x, 0.12, 0.20)
        self.color = (*linear((0.82, 0.68, 0.62)), 1)

    def put(self, socket, value):
        if hasattr(value, "is_output"):
            self.t.links.new(value, socket)
        else:
            socket.default_value = value

    def math(self, operation, *values):
        n = self.t.nodes.new("ShaderNodeMath")
        n.operation = operation
        for s, v in zip(n.inputs, values):
            self.put(s, v)
        return n.outputs[0]

    def ramp(self, value, low, high):
        n = self.t.nodes.new("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHERSTEP"
        for s, v in zip(n.inputs[:5], [value, low, high, 0.0, 1.0]):
            self.put(s, v)
        return n.outputs[0]

    def product(self, *values):
        result = values[0]
        for v in values[1:]:
            result = self.math("MULTIPLY", result, v)
        return result

    def donor(self):
        """Sample the unedited reviewed donor through a fitted surface projector."""
        data = json.loads((BASE / "face-paint-v1/front-anchors.json").read_text())[
            "images"
        ]
        names = [
            "nose_tip",
            "nose_wing_left",
            "nose_wing_right",
            "mouth_corner_left",
            "mouth_corner_right",
            "chin",
            "mouth_seam_center",
            "eye_outer_left",
            "eye_inner_left",
            "eye_inner_right",
            "eye_outer_right",
            "crown",
            "ear_outer_left",
            "ear_outer_right",
        ]
        p = np.array([data["guide"]["anchors"][n]["xy_1024"] for n in names]) / 1024
        q = np.array([data["v1"]["anchors"][n]["xy_1024"] for n in names]) / 1024
        # Add explicit upper/lower aperture anchors from the final neutral lid
        # contract, keeping painted sclera inside the real eye opening. Donor
        # bounds are reviewed pixel samples, not an inferred vermilion boundary.
        p = np.r_[p, np.array([[402, 426], [402, 455], [622, 426], [622, 455]]) / 1024]
        q = np.r_[q, np.array([[400, 410], [400, 444], [625, 410], [625, 444]]) / 1024]
        names += [
            "current_upper_lid_left",
            "current_lower_lid_left",
            "current_upper_lid_right",
            "current_lower_lid_right",
        ]
        d = ((p[:, None] - p[None, :]) ** 2).sum(2)
        kernel = d * np.log(np.maximum(d, 1e-12))
        affine = np.c_[np.ones(len(p)), p]
        system = np.block([[kernel, affine], [affine.T, np.zeros((3, 3))]])
        coeff = np.linalg.solve(system, np.r_[q, np.zeros((3, 2))])
        write(
            OUT / "projector-fit.json",
            dict(
                anchor_names=names,
                source_guide_xy=p.tolist(),
                donor_xy=q.tolist(),
                coefficients=coeff.tolist(),
                residual_max=float(
                    np.abs(system @ coeff - np.r_[q, np.zeros((3, 2))]).max()
                ),
                limitation="Guide anchors precede final eye patch. Fit is a measured initialization, not automatic acceptance; inspect current surface renders.",
            ),
        )
        u = self.math("ADD", 0.5, self.math("DIVIDE", self.y, 1.12))
        v = self.math("SUBTRACT", 0.5, self.math("DIVIDE", self.z, 1.12))
        uv = []
        for axis in range(2):
            value = self.math(
                "ADD",
                coeff[-3, axis],
                self.math(
                    "ADD",
                    self.product(u, coeff[-2, axis]),
                    self.product(v, coeff[-1, axis]),
                ),
            )
            for point, weight in zip(p, coeff[: len(p), axis]):
                dx = self.math("SUBTRACT", u, point[0])
                dy = self.math("SUBTRACT", v, point[1])
                radius = self.math("ADD", self.product(dx, dx), self.product(dy, dy))
                log = self.math("LOGARITHM", self.math("MAXIMUM", radius, 1e-12), np.e)
                value = self.math(
                    "ADD", value, self.product(radius, log, float(weight))
                )
            uv.append(value)
        vec = self.t.nodes.new("ShaderNodeCombineXYZ")
        self.put(vec.inputs[0], uv[0])
        self.put(vec.inputs[1], self.math("SUBTRACT", 1, uv[1]))
        tex = self.t.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(
            str(BASE / "face-paint-v1/painted-views/front-paint-v1.png"),
            check_existing=True,
        )
        tex.extension = "EXTEND"
        self.t.links.new(vec.outputs[0], tex.inputs["Vector"])
        # The donor's aperture differs slightly from the real lid. Exclude pale
        # neutral sclera pigment on HEAD skin; retain dark painted liner. Nearby
        # cheek pigment supplies the transition, never the authored eye objects.
        rgb = self.t.nodes.new("ShaderNodeSeparateColor")
        self.t.links.new(tex.outputs["Color"], rgb.inputs[0])
        eye_zone = self.product(
            self.ramp(self.a, 0.052, 0.065),
            self.ramp(self.a, 0.192, 0.180),
            self.ramp(self.z, 0.038, 0.050),
            self.ramp(self.z, 0.113, 0.101),
        )
        sclera = self.product(
            eye_zone,
            self.ramp(rgb.outputs[0], 0.18, 0.28),
            self.ramp(
                self.math("SUBTRACT", rgb.outputs[2], rgb.outputs[0]), -0.07, -0.025
            ),
        )
        cheek_vector = self.t.nodes.new("ShaderNodeVectorMath")
        cheek_vector.operation = "ADD"
        self.t.links.new(vec.outputs[0], cheek_vector.inputs[0])
        cheek_vector.inputs[1].default_value = (0, -0.055, 0)
        cheek = self.t.nodes.new("ShaderNodeTexImage")
        cheek.image = tex.image
        cheek.extension = "EXTEND"
        self.t.links.new(cheek_vector.outputs[0], cheek.inputs["Vector"])
        clean = self.t.nodes.new("ShaderNodeMixRGB")
        self.put(clean.inputs[0], sclera)
        self.t.links.new(tex.outputs["Color"], clean.inputs[1])
        self.t.links.new(cheek.outputs["Color"], clean.inputs[2])
        # Explicit view-limited skin: frontal surface only, no front image on rear
        # or ears. A wide smooth border transitions into an authored neutral base.
        geometry = self.t.nodes.new("ShaderNodeNewGeometry")
        normal = self.t.nodes.new("ShaderNodeVectorTransform")
        normal.vector_type = "NORMAL"
        normal.convert_from = "WORLD"
        normal.convert_to = "OBJECT"
        self.t.links.new(geometry.outputs["Normal"], normal.inputs[0])
        axes = self.t.nodes.new("ShaderNodeSeparateXYZ")
        self.t.links.new(normal.outputs[0], axes.inputs[0])
        lip_contact = self.product(
            self.ramp(self.a, 0.09, 0.078),
            self.ramp(self.z, -0.20, -0.185),
            self.ramp(self.z, -0.10, -0.115),
        )
        facing = self.math(
            "MAXIMUM", self.ramp(axes.outputs["X"], 0.10, 0.55), lip_contact
        )
        coverage = self.product(
            self.ramp(self.x, 0.07, 0.22),
            self.ramp(self.a, 0.275, 0.20),
            facing,
        )
        n = self.t.nodes.new("ShaderNodeMixRGB")
        self.put(n.inputs[0], coverage)
        n.inputs[1].default_value = (*linear((0.80, 0.665, 0.60)), 1)
        self.t.links.new(clean.outputs[0], n.inputs[2])
        self.color = n.outputs[0]
        bsdf = self.t.nodes.new("ShaderNodeBsdfPrincipled")
        self.put(bsdf.inputs["Base Color"], self.color)
        bsdf.inputs["Roughness"].default_value = 1
        bsdf.inputs["Specular IOR Level"].default_value = 0
        emission = self.t.nodes.new("ShaderNodeEmission")
        self.put(emission.inputs["Color"], self.color)
        mix = self.t.nodes.new("ShaderNodeMixShader")
        mix.inputs[0].default_value = 0.65
        self.t.links.new(bsdf.outputs[0], mix.inputs[1])
        self.t.links.new(emission.outputs[0], mix.inputs[2])
        out = self.t.nodes.new("ShaderNodeOutputMaterial")
        self.t.links.new(mix.outputs[0], out.inputs["Surface"])
        self.mat["paint_color_node"] = n.name
        self.mat["paint_coverage_node"] = coverage.node.name
        return self.mat


def signatures():
    return {
        o.name: mesh_preservation_signature(o)
        for o in bpy.data.objects
        if o.type == "MESH"
    }


def pose(**values):
    for o in bpy.data.objects:
        if o.type == "MESH" and o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks:
                if k.name != "Basis":
                    k.value = values.get(k.name, 0)
    bpy.context.view_layer.update()


def render(scene, camera, aim, name, position):
    camera.location = position
    camera.data.ortho_scale = 1.12
    aim(camera, (0, 0, 0))
    scene.render.filepath = str(OUT / (name + ".png"))
    bpy.ops.render.render(write_still=True)


def preview():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    before = signatures()
    write(OUT / "source-signatures.json", before)
    write(
        OUT / "source-uv-signatures.json",
        {o.name: sha_uv(o) for o in bpy.data.objects if o.type == "MESH"},
    )
    head = bpy.data.objects["Ren_Head"]
    mat = Paint().donor()
    # Keep polygon material IDs stable while replacing both old skin/lip slots.
    head.data.materials[0] = mat
    head.data.materials[2] = mat
    pose(mouthSeal=1)
    for o in list(bpy.data.objects):
        if o.type in {"LIGHT", "CAMERA"}:
            bpy.data.objects.remove(o, do_unlink=True)
    scene, camera, aim = setup_review()
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.view_settings.view_transform = "Standard"
    bpy.ops.wm.save_as_mainfile(
        filepath=str(OUT / "Ren_AnimePaint_Procedural_Study.blend")
    )
    render(scene, camera, aim, "preview-front", (3, 0, 0))
    render(scene, camera, aim, "preview-quarter", (2.5, -1.7, 0))
    write(
        OUT / "preview.json",
        dict(
            source_sha256=sha(SOURCE),
            blender_version=bpy.app.version_string,
            method="Reviewed image donor through fitted surface projector, material only; not artist acceptance",
        ),
    )
    print("ANIME_PAINT_PREVIEW_READY", flush=True)


def finish():
    bpy.ops.wm.open_mainfile(
        filepath=str(OUT / "Ren_AnimePaint_Procedural_Study.blend")
    )
    head = bpy.data.objects["Ren_Head"]
    pose(mouthSeal=1)
    before = json.loads((OUT / "source-signatures.json").read_text())
    uv_before = json.loads((OUT / "source-uv-signatures.json").read_text())
    original_slots = list(head.data.materials)
    skin = original_slots[0]
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    scene.cycles.device = "CPU"
    scene.render.bake.margin = 12
    scene.render.bake.margin_type = "EXTEND"
    scene.render.bake.use_clear = True
    head.data.uv_layers.active = head.data.uv_layers["FaceUV_v1"]
    head.data.uv_layers["FaceUV_v1"].active_render = True
    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head
    images = []
    for mode in ["BaseColor", "ProjectionCoverage"]:
        image = bpy.data.images.new(
            "Ren_AnimePaint_v2_" + mode, width=2048, height=2048, alpha=True
        )
        image.colorspace_settings.name = "sRGB" if mode == "BaseColor" else "Non-Color"
        temporary = {}
        for slot, original in enumerate(original_slots):
            if original.name not in temporary:
                mat = original.copy()
                temporary[original.name] = mat
                tree = mat.node_tree
                out = next(n for n in tree.nodes if n.type == "OUTPUT_MATERIAL")
                emission = tree.nodes.new("ShaderNodeEmission")
                if original == skin:
                    node = tree.nodes[
                        skin["paint_color_node"]
                        if mode == "BaseColor"
                        else skin["paint_coverage_node"]
                    ]
                    tree.links.new(node.outputs[0], emission.inputs["Color"])
                elif mode == "BaseColor":
                    bsdf = next(n for n in tree.nodes if n.type == "BSDF_PRINCIPLED")
                    emission.inputs["Color"].default_value = bsdf.inputs[
                        "Base Color"
                    ].default_value
                else:
                    emission.inputs["Color"].default_value = (0, 0, 0, 1)
                tree.links.new(emission.outputs[0], out.inputs["Surface"])
                target = tree.nodes.new("ShaderNodeTexImage")
                target.image = image
                tree.nodes.active = target
            head.data.materials[slot] = temporary[original.name]
        bpy.ops.object.bake(type="EMIT", uv_layer="FaceUV_v1")
        image.filepath_raw = str(OUT / (image.name + ".png"))
        image.file_format = "PNG"
        image.save()
        images.append(image)
        for i, mat in enumerate(original_slots):
            head.data.materials[i] = mat
        for mat in temporary.values():
            bpy.data.materials.remove(mat)
    # The runtime material has one atlas sample. The projector is only an authoring tool.
    baked = bpy.data.materials.new("Ren_AnimePaint_v2_BakedSkin")
    baked.use_nodes = True
    tree = baked.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    out = tree.nodes.get("Material Output")
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "FaceUV_v1"
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = images[0]
    tex.extension = "EXTEND"
    tree.links.new(uv.outputs[0], tex.inputs["Vector"])
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1
    bsdf.inputs["Specular IOR Level"].default_value = 0
    emission = tree.nodes.new("ShaderNodeEmission")
    tree.links.new(tex.outputs["Color"], emission.inputs["Color"])
    mix = tree.nodes.new("ShaderNodeMixShader")
    mix.inputs[0].default_value = 0.65
    tree.links.new(bsdf.outputs[0], mix.inputs[1])
    tree.links.new(emission.outputs[0], mix.inputs[2])
    tree.links.new(mix.outputs[0], out.inputs["Surface"])
    head.data.materials[0] = baked
    head.data.materials[2] = baked
    images[0].pack()
    after = signatures()
    checks = {}
    for name, initial in before.items():
        result = after[name]
        for key in initial:
            if name == "Ren_Head" and key == "materials":
                continue
            if initial[key] != result[key]:
                raise ValueError(f"Unexpected source mutation: {name} {key}")
        if uv_before[name] != sha_uv(bpy.data.objects[name]):
            raise ValueError("UV mutation")
        checks[name] = dict(
            geometry_shapes_attributes_matrix_normals_exact=True,
            uv_hash=uv_before[name],
            materials_changed=name == "Ren_Head",
        )
    scene.render.engine = "BLENDER_EEVEE"
    camera = scene.camera

    def aim(obj, target):
        from mathutils import Vector

        obj.rotation_euler = (
            (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
        )

    target = OUT / "Ren_AnimePaint_v2.blend"
    camera.location = (3, 0, 0)
    camera.data.ortho_scale = 1.12
    aim(camera, (0, 0, 0))
    bpy.ops.wm.save_as_mainfile(filepath=str(target))
    captures = []
    for name, values, position in [
        ("neutral-front", {"mouthSeal": 1}, (3, 0, 0)),
        ("neutral-quarter", {"mouthSeal": 1}, (2.5, -1.7, 0)),
        ("neutral-profile", {"mouthSeal": 1}, (0, -3, 0)),
        ("mouth-open-front", {"jawOpen_A": 1}, (3, 0, 0)),
        ("blink-front", {"mouthSeal": 1, "eyeBlinkL": 1, "eyeBlinkR": 1}, (3, 0, 0)),
        (
            "blink-quarter",
            {"mouthSeal": 1, "eyeBlinkL": 1, "eyeBlinkR": 1},
            (2.5, -1.7, 0),
        ),
    ]:
        pose(**values)
        render(scene, camera, aim, name, position)
        captures.append(
            dict(file=name + ".png", pose=values, sha256=sha(OUT / (name + ".png")))
        )
    pose(mouthSeal=1)
    report = dict(
        status="MATERIAL_ONLY_PAINT_STUDY_NOT_LIKENESS_ACCEPTANCE",
        source=str(SOURCE),
        source_sha256=sha(SOURCE),
        derived_sha256=sha(target),
        blender_version=bpy.app.version_string,
        donor_sha256=sha(BASE / "face-paint-v1/painted-views/front-paint-v1.png"),
        images=[
            dict(
                file=Path(i.filepath_raw).name,
                sha256=sha(i.filepath_raw),
                width=i.size[0],
                height=i.size[1],
            )
            for i in images
        ],
        checks=checks,
        captures=captures,
        limitations=[
            "Single front donor; neutral authored base on rear/sides.",
            "Donor contains painted form shading; not a measured albedo map.",
            "All existing eye assemblies preserved; donor skin pixels deform with existing UVs.",
            "No likeness approval or complete expression repertoire implied.",
        ],
    )
    write(OUT / "paint-report.json", report)
    write_contract()
    print("ANIME_PAINT_BAKE_VERIFIED", target, flush=True)


def write_contract():
    report = json.loads((OUT / "paint-report.json").read_text())
    initial = json.loads((OUT / "source-signatures.json").read_text())["Ren_Head"]
    write(
        OUT / "material-transfer.json",
        dict(
            status="REVIEW_CANDIDATE",
            sourceWorkingBlendSha256=SOURCE_SHA,
            materialBlend=str(OUT / "Ren_AnimePaint_v2.blend"),
            materialBlendSha256=report["derived_sha256"],
            sourceObject="Ren_Head",
            sourceMaterial="Ren_AnimePaint_v2_BakedSkin",
            replaceMaterialSlots=[0, 2],
            preserveMaterialSlots=[1],
            expectedHeadGeometrySha256=initial["geometry"],
            expectedHeadAllUvSha256=report["checks"]["Ren_Head"]["uv_hash"],
            expectedShapeKeys=initial["shapes"],
            uvLayer="FaceUV_v1",
            unityUvChannel=0,
            exportInstruction="Expose FaceUV_v1 as UV0 in the export COPY; do not substitute old native UVs.",
            baseColor=dict(
                file=str(OUT / report["images"][0]["file"]),
                sha256=report["images"][0]["sha256"],
                colorSpace="sRGB",
                resolution=2048,
                alphaUsage="opaque skin; unused atlas texels are transparent",
                filter="bilinear/trilinear with mipmaps",
                paddingPixels=12,
            ),
            coverage=dict(
                file=str(OUT / report["images"][1]["file"]),
                sha256=report["images"][1]["sha256"],
                colorSpace="linear",
                usage="provenance only; front donor weight, not runtime opacity",
            ),
            shader=dict(
                baseColorFactor=[1, 1, 1, 1],
                metallic=0,
                roughness=1,
                specular=0,
                normalMap=None,
                blenderReview="0.65 emission/basecolor + 0.35 diffuse, energy blended, not added",
                unityExpectation="Opaque diffuse with controlled ambient floor, dynamic main/additional lights; no glossy PBR/normal detail.",
            ),
            transferScope="Materials only. Keep gaze/eyes, all positions, triangles, UV corners, shape coordinates and transforms.",
        ),
    )


def sha_uv(obj):
    h = hashlib.sha256()
    for layer in obj.data.uv_layers:
        h.update(layer.name.encode())
        h.update(np.array([v.uv[:] for v in layer.data], dtype="<f8").tobytes())
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", action="store_true")
    parser.add_argument("--finish", action="store_true")
    args = parser.parse_args(
        sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    )
    if sha(SOURCE) != SOURCE_SHA:
        raise ValueError("Frozen working source changed")
    OUT.mkdir(parents=True, exist_ok=True)
    if args.preview:
        preview()
    if args.finish:
        finish()
    if sha(SOURCE) != SOURCE_SHA:
        raise ValueError("Source changed during study")


if __name__ == "__main__":
    main()
