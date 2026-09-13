"""Blender shader bake of semantic hair/cap shadow palettes; no bitmap repaint."""

from pathlib import Path
import bpy
import json
import hashlib

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
SRC = BASE / "h-complete-head-v1"
OUT = BASE / "h-anime-paint-v1/tokon-study-v1/maps"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_plane_add()
obj = bpy.context.object
mat = bpy.data.materials.new("SemanticPaletteBake")
mat.use_nodes = True
obj.data.materials.append(mat)
n = mat.node_tree.nodes
n.clear()
links = mat.node_tree.links
o = n.new("ShaderNodeOutputMaterial")
e = n.new("ShaderNodeEmission")
links.new(e.outputs[0], o.inputs[0])
tex = n.new("ShaderNodeTexImage")
mul = n.new("ShaderNodeMixRGB")
mul.blend_type = "MULTIPLY"
mul.inputs[0].default_value = 1
links.new(tex.outputs[0], mul.inputs[1])
target = n.new("ShaderNodeTexImage")
rgb = n.new("ShaderNodeRGB")
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 1
records = []
manifest = json.loads((SRC / "material-contract.json").read_text())
for sourceName, tint, spec in [
    ("Ren_H_Hair_Painted_PaleBlond", (0.56, 0.48, 0.40, 1), 0.35),
    ("Ren_Cap_V2_Cloth", (0.35, 0.36, 0.43, 1), 0.12),
]:
    row = next(x for x in manifest["materials"] if x["sourceName"] == sourceName)
    path = SRC / row["baseColorFile"]
    assert hashlib.sha256(path.read_bytes()).hexdigest() == row["baseColorSha256"]
    tex.image = bpy.data.images.load(str(path))
    mul.inputs[2].default_value = tint
    files = {}
    for label, space, socket in [
        ("Shadow", "sRGB", mul.outputs[0]),
        ("Controls", "Non-Color", rgb.outputs[0]),
    ]:
        rgb.outputs[0].default_value = (0, spec, 0.3, 0)
        image = bpy.data.images.new(
            sourceName + "_" + label, width=2048, height=2048, alpha=True
        )
        image.colorspace_settings.name = space
        target.image = image
        n.active = target
        links.new(socket, e.inputs[0])
        bpy.ops.object.bake(type="EMIT", margin=0, use_clear=True)
        # Skin A=0 is explicit for these semantic accessory materials.
        if label == "Controls":
            image = bpy.data.images.new(
                sourceName + "_ControlsRGBA", width=16, height=16, alpha=True
            )
            image.colorspace_settings.name = "Non-Color"
            image.generated_color = (0, spec, 0.3, 0)
        out = OUT / (sourceName + "_" + label + ".png")
        image.filepath_raw = str(out)
        image.file_format = "PNG"
        image.save()
        files[label] = dict(
            path=str(out.relative_to(ROOT)),
            sha256=hashlib.sha256(out.read_bytes()).hexdigest(),
            colorSpace=space,
        )
    records.append(
        dict(
            sourceName=sourceName,
            sourceUVLayer=row["sourceUVLayer"],
            sourceTextureSha256=row["baseColorSha256"],
            shadowMultiplierLinear=tint,
            files=files,
            authorship="Provisional material-semantic derived palette and constant controls; not artist-painted masks",
        )
    )
(OUT / "accessory-map-contract.json").write_text(json.dumps(records, indent=2) + "\n")
print("ACCESSORY_MAPS_READY", flush=True)
