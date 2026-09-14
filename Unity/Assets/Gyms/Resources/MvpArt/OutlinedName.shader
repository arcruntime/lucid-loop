Shader "BTD/OutlinedName" {
Properties {_MainTex("Font",2D)="white"{} }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent"}
Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _MainTex_TexelSize;
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 c:COLOR;};
V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;o.c=i.c;return o;}
half4 frag(V i):SV_Target{
 float fill=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;float edge=fill;
 for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)edge=max(edge,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*1.3).a);
 return half4(i.c.rgb*fill/max(edge,.001),edge*i.c.a);
}
ENDHLSL
} } }
