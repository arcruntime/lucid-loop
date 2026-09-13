Shader "BTD/PaintedBackdrop" {
Properties { _MainTex("Paint",2D)="white"{} }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" } Pass {
Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
half4 frag(V i):SV_Target{return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);}
ENDHLSL
} } }
