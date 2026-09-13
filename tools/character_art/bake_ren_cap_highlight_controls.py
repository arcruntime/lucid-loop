"""Bake cap specular-permission data aligned to existing seam/brim UVs."""

from pathlib import Path
import hashlib
import json
import bpy
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "art/generated/characters/ren/parts-workflow-v1"
OUT = BASE / "h-anime-paint-v1/tokon-study-v1/cap-controls-v1"
OUT.mkdir(exist_ok=True)
ORIGINAL = BASE / "h-anime-paint-v1/tokon-study-v1/maps/Ren_Cap_V2_Cloth_Controls.png"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_plane_add()
obj = bpy.context.object
m = bpy.data.materials.new("CapHighlightPlacementData")
m.use_nodes = True
obj.data.materials.append(m)
n = m.node_tree.nodes
n.clear()
links = m.node_tree.links
uv = n.new("ShaderNodeTexCoord")
sep = n.new("ShaderNodeSeparateXYZ")
links.new(uv.outputs["UV"], sep.inputs[0])
u = sep.outputs["X"]
v = sep.outputs["Y"]


def math(op, a, b=0):
    node = n.new("ShaderNodeMath")
    node.operation = op
    for i, x in enumerate([a, b]):
        if isinstance(x, (float, int)):
            node.inputs[i].default_value = x
        else:
            links.new(x, node.inputs[i])
    return node.outputs[0]


def ramp(x, a, b):
    node = n.new("ShaderNodeMapRange")
    node.clamp = True
    node.interpolation_type = "SMOOTHSTEP"
    links.new(x, node.inputs["Value"])
    node.inputs["From Min"].default_value = a
    node.inputs["From Max"].default_value = b
    return node.outputs[0]


# Permission is restricted to existing drawn seam neighborhoods, not circular lobes.
seam = math(
    "DIVIDE",
    math(
        "ABSOLUTE",
        math("SUBTRACT", math("FRACT", math("ADD", math("MULTIPLY", u, 6), 0.5)), 0.5),
    ),
    6,
)
seam_band = math("SUBTRACT", 1, ramp(seam, 0.0015, 0.006))
crown = math("MULTIPLY", ramp(v, 0.20, 0.27), math("SUBTRACT", 1, ramp(v, 0.61, 0.69)))
crown = math("MULTIPLY", crown, seam_band)
brim = math(
    "MULTIPLY", ramp(v, 0.937, 0.944), math("SUBTRACT", 1, ramp(v, 0.950, 0.958))
)
g = math("MULTIPLY", math("MAXIMUM", crown, brim), 31 / 255)
combine = n.new("ShaderNodeCombineXYZ")
links.new(g, combine.inputs["Y"])
combine.inputs["Z"].default_value = 77 / 255
emit = n.new("ShaderNodeEmission")
links.new(combine.outputs[0], emit.inputs[0])
out = n.new("ShaderNodeOutputMaterial")
links.new(emit.outputs[0], out.inputs["Surface"])
image = bpy.data.images.new(
    "Ren_Cap_SeamHighlightControls", width=1024, height=1024, alpha=True
)
image.colorspace_settings.name = "Non-Color"
target = n.new("ShaderNodeTexImage")
target.image = image
n.active = target
s = bpy.context.scene
s.render.engine = "CYCLES"
s.cycles.samples = 1
bpy.ops.object.bake(type="EMIT", margin=0, use_clear=True)
# Pack the semantic A=0 skin flag; this is control data, not pigment painting.
data = np.empty(len(image.pixels), np.float32)
image.pixels.foreach_get(data)
data = data.reshape(-1, 4)
data[:, 0] = 0
data[:, 2] = 77 / 255
data[:, 3] = 0
image.pixels.foreach_set(data.ravel())
path = OUT / "Ren_Cap_SeamHighlightControls.png"
image.filepath_raw = str(path)
image.file_format = "PNG"
image.save()
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

contract = dict(
    sourceControlSha256=sha(ORIGINAL),
    outputTexture=str(path.relative_to(ROOT)),
    outputSha256=sha(path),
    colorSpace="linear / Non-Color",
    sourceName="Ren_Cap_V2_Cloth",
    sourceUVLayer="AccessoryUV",
    baseMapScaleOffset=[1, 1, 0, 0],
    changedChannel="G only; max31/255 unchanged",
    preservedChannels=dict(R=0, B=77 / 255, A=0),
    placement="Six existing crown seam columns: u=k/6, width .0015-.006, v .20-.69 smooth taper; outer brim strip v .937-.958",
    artistReference="art/characters/ren-model-sheet.png",
    constructionReference="tools/character_art/revise_ren_cap_fit.py crown/bill UV construction",
    authorship="Artist-guided procedural control placement; no base or shadow painting; no geometry changes",
    otherMaterialPropertiesUnchanged=True,
    review="Pending actual matched Unity front/quarter/head-turn comparison",
)
(OUT / "cap-control-contract.json").write_text(json.dumps(contract, indent=2) + "\n")
print("CAP_CONTROL_READY", flush=True)
