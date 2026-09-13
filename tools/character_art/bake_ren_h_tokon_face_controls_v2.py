"""Bake isolated face-lighting control revision; preserve palettes and source geometry."""

from pathlib import Path
import hashlib
import json
import sys
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
OUT = BASE / "h-anime-paint-v1/tokon-study-v1/maps-face-v2"
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
    face = smooth(0.245, 0.315, pos[:, 2]) * (1 - smooth(0.16, 0.23, np.abs(pos[:, 0])))
    face *= 1 - smooth(-0.015, 0.07, pos[:, 1])
    lower_front = 1 - smooth(-0.12, -0.04, pos[:, 1])
    face *= lower_front + (1 - lower_front) * smooth(0.34, 0.43, pos[:, 2])
    old_face = (
        smooth(0.355, 0.425, pos[:, 2])
        * (1 - smooth(0.155, 0.205, np.abs(pos[:, 0])))
        * (1 - smooth(-0.03, 0.055, pos[:, 1]))
    )
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
            face[verts],
            np.zeros(len(verts)),
            old_face[verts] * 0.95,
            np.ones(len(verts)),
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
        shadows = []  # Reuse frozen v1 shadow palettes; this task changes controls only.
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
                revisedFaceMode=0.9,
                lowerFrontGate="lerp(1-smoothstep(-.12,-.04,y),1,smoothstep(.34,.43,z))",
                geometryUVShapeMatrixUnchanged=True,
                classification="Accepted head skin source IDs plus explicit native-H front-face spatial taper; no pigment color classification",
                faceFormula="smoothstep(.245,.315,z)*(1-smoothstep(.16,.23,abs(x)))*(1-smoothstep(-.015,.07,y))",
                channels="linear R face, G spec=0, B previous-v1 outline suppression unchanged, A skin=1",
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
