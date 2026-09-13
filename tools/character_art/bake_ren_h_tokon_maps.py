"""Bake semantic native-H NPR controls and paired shadow albedo; no source edits."""

from pathlib import Path
import hashlib
import json
import sys
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
OUT = BASE / "h-anime-paint-v1/tokon-study-v1/maps"
SOURCE = (
    BASE / "h-anime-paint-v1/closed-lip-corrective-v1/Ren_H_ClosedLip_AtlasBlend.blend"
)
EXPECTED = "b34530413f525f4f011f9857a809cb0068aaf4e92fbd880ccaffb42297c68c1c"
sys.path.insert(0, str(Path(__file__).parent))
from finalize_ren_h_material import signature  # noqa: E402
from assemble_ren_h_complete_head import assembly_pose  # noqa: E402


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def smooth(lo, hi, x):
    t = np.clip((x - lo) / (hi - lo), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    assert sha(SOURCE) == EXPECTED
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    assembly_pose()
    head = bpy.data.objects["Ren_H_Head"]
    before = signature(head)
    mesh = head.data
    # Accepted source-H skin object and explicit source IDs, never RGB classification.
    pos = np.array([v.co[:] for v in mesh.vertices])
    ids = np.array([x.value for x in mesh.attributes["source_h_vertex_id"].data])
    face = smooth(0.355, 0.425, pos[:, 2]) * (
        1 - smooth(0.155, 0.205, np.abs(pos[:, 0]))
    )
    face *= 1 - smooth(-0.03, 0.055, pos[:, 1])
    records = []
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 1
    srcmats = list(mesh.materials)
    for slot, srcmat in enumerate(srcmats):
        faces = [p for p in mesh.polygons if p.material_index == slot]
        verts = sorted({v for p in faces for v in p.vertices})
        index = {v: i for i, v in enumerate(verts)}
        temp = bpy.data.meshes.new("NPRBakeOnly")
        temp.from_pydata(
            [mesh.vertices[v].co[:] for v in verts],
            [],
            [[index[v] for v in p.vertices] for p in faces],
        )
        temp.update()
        uv = temp.uv_layers.new(name="HNativeUV")
        for d, s in zip(temp.polygons, faces):
            for dl, sl in zip(d.loop_indices, s.loop_indices):
                uv.data[dl].uv = mesh.uv_layers["HNativeUV"].data[sl].uv
        controls = temp.color_attributes.new(
            name="RenNprSemanticControls", type="FLOAT_COLOR", domain="POINT"
        )
        vals = np.c_[
            face[verts], np.zeros(len(verts)), face[verts] * 0.95, np.ones(len(verts))
        ].astype(np.float32)
        controls.data.foreach_set("color", vals.ravel())
        obj = bpy.data.objects.new("NPRBakeOnly", temp)
        bpy.context.collection.objects.link(obj)
        obj.matrix_world = head.matrix_world
        mat = bpy.data.materials.new("RenNprBakeOnly")
        mat.use_nodes = True
        temp.materials.append(mat)
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        nodes.clear()
        out = nodes.new("ShaderNodeOutputMaterial")
        emit = nodes.new("ShaderNodeEmission")
        links.new(emit.outputs[0], out.inputs["Surface"])
        attr = nodes.new("ShaderNodeVertexColor")
        attr.layer_name = controls.name
        targetnode = nodes.new("ShaderNodeTexImage")
        nodes.active = targetnode
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

        def bake(name, size, socket, space):
            image = bpy.data.images.new(name, width=size, height=size, alpha=False)
            image.colorspace_settings.name = space
            targetnode.image = image
            nodes.active = targetnode
            links.new(socket, emit.inputs[0])
            bpy.ops.object.bake(type="EMIT", margin=8, use_clear=True)
            path = OUT / (name + ".png")
            image.filepath_raw = str(path)
            image.file_format = "PNG"
            image.save()
            return image, dict(
                path=str(path.relative_to(ROOT)),
                sha256=sha(path),
                resolution=size,
                colorSpace=space,
            )

        control, cr = bake(
            f"Ren_H_Skin{slot}_Controls",
            2048 if slot == 0 else 512,
            attr.outputs["Color"],
            "Non-Color",
        )
        tex = nodes.new("ShaderNodeTexImage")
        uvnode = nodes.new("ShaderNodeUVMap")
        uvnode.uv_map = "HNativeUV"
        links.new(uvnode.outputs[0], tex.inputs[0])
        mul = nodes.new("ShaderNodeMixRGB")
        mul.blend_type = "MULTIPLY"
        mul.inputs[0].default_value = 1
        mul.inputs[2].default_value = (0.72, 0.53, 0.50, 1)
        links.new(tex.outputs["Color"], mul.inputs[1])
        if slot == 0:
            paths = [
                (
                    "Open",
                    ROOT
                    / "art/generated/characters/ren/bust-comparison-v1/tripo/studio-h3.1-a-open/embedded-original-textures/image-0.jpg",
                ),
                (
                    "Closed",
                    BASE
                    / "h-anime-paint-v1/closed-lip-corrective-v1/Ren_H_ClosedLip_BaseColor.png",
                ),
            ]
        else:
            image = next(
                n.image for n in srcmat.node_tree.nodes if n.type == "TEX_IMAGE"
            )
            paths = [("Open", Path(bpy.path.abspath(image.filepath)))]
        shadows = []
        for label, path in paths:
            tex.image = bpy.data.images.load(str(path), check_existing=True)
            image, rec = bake(
                f"Ren_H_Skin{slot}_{label}Shadow",
                4096 if slot == 0 else 512,
                mul.outputs[0],
                "sRGB",
            )
            rec.update(endpoint=label, baseTexture=str(path), baseSha256=sha(path))
            shadows.append(rec)
        records.append(
            dict(
                materialSlot=slot,
                sourceMaterial=srcmat.name,
                uvLayer="HNativeUV",
                controls=cr,
                shadows=shadows,
                faceSourceIds=sorted(
                    int(ids[v]) for v in verts if ids[v] >= 0 and face[v] > 0.01
                ),
                generatedVertexCount=int(sum(ids[v] < 0 for v in verts)),
            )
        )
        bpy.data.objects.remove(obj, do_unlink=True)
    assert signature(head) == before and sha(SOURCE) == EXPECTED
    (OUT / "map-contract.json").write_text(
        json.dumps(
            dict(
                sourceSha256=EXPECTED,
                geometryUVShapeMatrixUnchanged=True,
                classification="Accepted head skin source IDs plus explicit native-H front-face spatial taper; no pigment color classification",
                faceFormula="smoothstep(.355,.425,z)*(1-smoothstep(.155,.205,abs(x)))*(1-smoothstep(-.03,.055,y))",
                channels="linear R face, G spec=0, B internal-outline suppression=.95R, A skin=1",
                shadowPaletteLinearMultiplier=[0.72, 0.53, 0.50],
                authorship="Provisional derived warm skin shadow palette; not hand-painted shadow artwork",
                materials=records,
            ),
            indent=2,
        )
        + "\n"
    )
    print("TOKON_MAPS_READY", flush=True)


if __name__ == "__main__":
    main()
