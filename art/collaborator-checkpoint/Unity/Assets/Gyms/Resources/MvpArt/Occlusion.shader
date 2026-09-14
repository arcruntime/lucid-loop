Shader "BTD/PaintedOcclusion" {
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" } Pass {
ColorMask 0 ZWrite On
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 vert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
half4 frag():SV_Target{return 0;}
ENDHLSL
} } }
