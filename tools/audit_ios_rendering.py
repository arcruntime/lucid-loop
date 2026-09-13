"""Read serialized Unity assets without opening the shared Editor; no compiled-variant claims."""
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "Unity"


def field(text, name):
    match = re.search(r"^  " + re.escape(name) + r": (.*)$", text, re.MULTILINE)
    return match.group(1) if match else None


def inspect():
    scenes = {}
    for path in sorted((UNITY / "Assets/Gyms/Scenes").glob("*.unity")):
        text = path.read_text(encoding="utf-8-sig")
        lights = re.findall(r"^--- !u!108 &.*?\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)
        types = Counter(field(light, "m_Type") for light in lights)
        shadows = sum(bool(re.search(r"  m_Shadows:\n    m_Type: [12]", light)) for light in lights)
        scenes[path.stem] = {
            "serialized_light_components": len(lights),
            "types": { {"0": "spot", "1": "directional", "2": "point"}.get(k, k): v for k, v in types.items()},
            "shadow_requested_components": shadows,
            "note": "Serialized components, not runtime visibility or per-object overlap.",
        }
    materials = list((UNITY / "Assets/Gyms/Generated").glob("*.mat"))
    keywords = Counter()
    shaders = Counter()
    emission_mismatches = []
    for path in materials:
        text = path.read_text(encoding="utf-8-sig")
        match = re.search(r"  m_ValidKeywords:(.*?)(?=\n  [a-zA-Z_])", text, re.S)
        keywords[tuple(sorted(re.findall(r"  - (\S+)", match.group(1) if match else "")))] += 1
        shaders[field(text, "m_Shader")] += 1
        # Serialized positive emission must agree with both the keyword and GI flags.
        rgb = re.search(r"_EmissionColor: \{r: ([^,]+), g: ([^,]+), b: ([^,}]+)", text)
        if rgb and max(map(float, rgb.groups())) > 0:
            flags = int(field(text, "m_LightmapFlags") or 0)
            active = re.findall(r"  - (\S+)", match.group(1) if match else "")
            if "_EMISSION" not in active or not (flags & 3) or (flags & 4):
                emission_mismatches.append(path.name)
    pipeline = (UNITY / "Assets/Gyms/Generated/GymPipeline.asset").read_text()
    names = ["m_AdditionalLightsRenderingMode", "m_AdditionalLightsPerObjectLimit", "m_AdditionalLightShadowsSupported",
             "m_SoftShadowsSupported", "m_ShadowCascadeCount", "m_MainLightShadowmapResolution", "m_ShadowDistance",
             "m_UseSRPBatcher", "m_SupportsHDR", "m_MSAA", "m_SupportsTerrainHoles", "m_EnableLODCrossFade",
             "m_MixedLightingSupported", "m_SupportsLightCookies", "m_SupportsLightLayers",
             "m_SupportDataDrivenLensFlare", "m_SupportScreenSpaceLensFlare"]
    return {"scenes": scenes, "gym_materials": len(materials), "gym_shader_references": dict(shaders),
            "gym_material_keyword_sets": [{"keywords": list(k), "materials": v} for k, v in sorted(keywords.items())],
            "pipeline": {name: field(pipeline, name) for name in names},
            "positive_emission_materials_with_inconsistent_flags_or_keyword": emission_mismatches,
            "compiled_shader_variants": None,
            "compiled_shader_variants_note": "Requires an iOS Unity build; inspect Temp/shader-stripping.json."}


if __name__ == "__main__":
    print(json.dumps(inspect(), indent=2))
