using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LucidLoop.Gyms.Editor
{
    /// <summary>One feature set for both club moods. Never rewrites character shaders.</summary>
    public static class IosRenderingPolicy
    {
        public const string PipelinePath = "Assets/Gyms/Generated/GymPipeline.asset";

        // These serialized properties have no public setters in the installed URP 17.3.
        static readonly string[] DisabledFeatures = {
            "m_SupportsTerrainHoles", "m_EnableLODCrossFade", "m_MixedLightingSupported",
            "m_SupportsLightCookies", "m_SupportsLightLayers", "m_SupportDataDrivenLensFlare",
            "m_SupportScreenSpaceLensFlare", "m_AdditionalLightShadowsSupported", "m_SoftShadowsSupported"
        };

        public static void Apply(UniversalRenderPipelineAsset pipeline)
        {
            if (AssetDatabase.GetAssetPath(pipeline) != PipelinePath)
                throw new BuildFailedException("The iOS policy can only modify the game GymPipeline asset.");
            pipeline.supportsHDR = true; // Retain emissive bloom in both moods.
            pipeline.msaaSampleCount = 2;
            pipeline.renderScale = 1;
            pipeline.mainLightShadowmapResolution = 1024;
            pipeline.shadowCascadeCount = 1;
            pipeline.shadowDistance = 40;
            pipeline.maxAdditionalLightsCount = 4;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.useSRPBatcher = true;
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_AnyShadowsSupported").boolValue = true;
            foreach (string name in DisabledFeatures)
            {
                var property = serialized.FindProperty(name);
                if (property == null) throw new BuildFailedException("URP policy property missing: " + name);
                property.boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        public static void ValidateForIosBuild()
        {
            var expected = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            Require(expected && GraphicsSettings.defaultRenderPipeline == expected,
                "Select GymPipeline as the default pipeline for the iOS game build.");
            foreach (int index in QualitySettings.GetActiveQualityLevelsForPlatform("iPhone"))
            {
                var actual = QualitySettings.GetRenderPipelineAssetAt(index);
                Require(!actual || actual == expected,
                    "iOS quality level uses a second pipeline: " + QualitySettings.names[index]);
            }
            Require(expected.additionalLightsRenderingMode == LightRenderingMode.PerPixel &&
                expected.maxAdditionalLightsCount <= 4 && !expected.supportsAdditionalLightShadows &&
                !expected.supportsSoftShadows && expected.shadowCascadeCount == 1 && expected.useSRPBatcher &&
                expected.mainLightRenderingMode == LightRenderingMode.PerPixel && expected.supportsMainLightShadows &&
                expected.mainLightShadowmapResolution <= 1024 && expected.shadowDistance <= 40,
                "GymPipeline lighting/batching settings violate the iOS rendering budget.");
            var serialized = new SerializedObject(expected);
            foreach (string name in DisabledFeatures)
                Require(serialized.FindProperty(name) != null && !serialized.FindProperty(name).boolValue,
                    "Unexpected enabled iOS shader feature: " + name);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Gyms/Generated/GymRenderer.asset");
            var renderers = serialized.FindProperty("m_RendererDataList");
            Require(renderers != null && renderers.arraySize == 1 &&
                renderers.GetArrayElementAtIndex(0).objectReferenceValue == renderer,
                "GymPipeline must reference only the game Forward renderer.");
            Require(renderer && new SerializedObject(renderer).FindProperty("m_RenderingMode").intValue == 0 &&
                renderer.rendererFeatures.Count == 0, "iOS game expects Forward rendering with no renderer features.");
            var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
            Require(stripping != null && stripping.stripUnusedVariants,
                "Enable URP Strip Unused Variants for the iOS build.");
        }

        public static void EnableBuildReporting()
        {
            var reporting = GraphicsSettings.GetRenderPipelineSettings<ShaderStrippingSetting>();
            if (reporting == null) throw new BuildFailedException("URP shader reporting settings are missing.");
            reporting.exportShaderVariants = true;
            reporting.shaderVariantLogLevel = UnityEngine.Rendering.ShaderVariantLogLevel.AllShaders;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
        }
    }
}
