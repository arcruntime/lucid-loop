using System;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace LucidLoop.Gyms.Editor
{
    public static class GymMaterialEmission
    {
        public static void Configure(Material material, Color emission)
        {
            if (!material || !material.shader || material.shader.name != "Universal Render Pipeline/Lit")
                throw new ArgumentException("Gym emission configuration requires a URP Lit material.");
            material.SetColor("_EmissionColor", emission);
            // URP reconstructs _EMISSION from AnyEmissive during import. A keyword
            // alone is not durable when the material still says EmissiveIsBlack.
            material.globalIlluminationFlags = emission.maxColorComponent > 0
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
        }
    }
}
