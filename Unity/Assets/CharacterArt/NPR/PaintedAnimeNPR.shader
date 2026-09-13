Shader "LucidLoop/Characters/PaintedAnimeNPR"
{
    Properties
    {
        [MainTexture] _BaseMap("Artist base paint", 2D) = "white" {}
        _BaseColor("Linear base color / texture tint", Vector) = (1,1,1,1)
        _UseBaseMap("Use base map", Float) = 1
        _UseVertexColor("Vertex color replaces base tint", Float) = 0
        _VertexColorSrgb("Decode sRGB vertex color", Float) = 0
        _Unlit("Exact source-color diagnostic", Float) = 0
        _ShadeColor("Artist shade pigment multiplier", Color) = (0.78,0.76,0.86,1)
        _DeepShadeColor("Artist deep shade pigment multiplier", Color) = (0.53,0.51,0.68,1)
        _ShadeThreshold("Shade to base threshold", Range(0,1)) = 0.55
        _ShadeSoftness("Shade edge softness", Range(0.01,0.5)) = 0.16
        _DeepThreshold("Deep shade to shade threshold", Range(0,1)) = 0.22
        _DeepSoftness("Deep shade edge softness", Range(0.01,0.5)) = 0.12
        [Toggle(_PAINT_REGIONS)] _UsePaintRegions("Use authored region map", Float) = 0
        _PaintRegionMap("R: light bias G: deep bias B: highlight permission", 2D) = "gray" {}
        _PaintRegionStrength("Authored region bias", Range(0,0.5)) = 0
        _AmbientFloor("Neutral tone floor", Range(0,1)) = 0.22
        _ProbeStrength("Probe contribution to tone", Range(0,1)) = 0.10
        _MainLightStrength("Key contribution to tone", Range(0,2)) = 0.65
        _AdditionalLightStrength("Additional contribution to tone", Range(0,1)) = 0.25
        _SceneColorStrength("Scene hue influence", Range(0,1)) = 0.30
        _ShadowStrength("Received shadow influence", Range(0,1)) = 0.65
        _MaxAdditionalLights("Maximum additional lights evaluated", Range(0,8)) = 4
        _HighlightColor("Broad painted highlight pigment", Color) = (0.91,0.86,0.78,1)
        _HighlightStrength("Broad highlight strength; skin zero", Range(0,1)) = 0
        _HighlightThreshold("Broad highlight threshold", Range(0,1)) = 0.82
        _HighlightSoftness("Broad highlight softness", Range(0.01,0.5)) = 0.13
        _RimColor("Edge-light pigment", Color) = (0.70,0.76,0.89,1)
        _RimStrength("Restrained edge-light strength", Range(0,1)) = 0.06
        _RimPower("Broad edge-light falloff", Range(1,8)) = 3
        _FaceMode("Stable analytic face lighting", Range(0,1)) = 0
        _FaceForwardWS("Face forward world direction", Vector) = (0,0,1,0)
        _FaceRightWS("Face right world direction", Vector) = (1,0,0,0)
        _FaceUpWS("Face up world direction", Vector) = (0,1,0,0)
        _FaceCenterWS("Face center world position", Vector) = (0,0,0,1)
        _FaceWidth("Face half width in world units", Float) = 0.3
        _FaceCurvature("Broad face side-plane curvature", Range(0,2)) = 0.85
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Optional cutout", Float) = 0
        _Cutoff("Cutout threshold", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        Blend One Zero
        HLSLINCLUDE
        #include "PaintedAnimeNPR.hlsl"
        ENDHLSL
        Pass
        {
            Name "PaintedForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NprVertex
            #pragma fragment NprFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _PAINT_REGIONS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NprVertex
            #pragma fragment NprDepth
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NprVertex
            #pragma fragment NprDepthNormals
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NprShadowVertex
            #pragma fragment NprDepth
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    Fallback Off
}
