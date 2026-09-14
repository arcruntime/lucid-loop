Shader "BTD/ExpandedBackdrop" {
Properties { _MainTex("Paint",2D)="white"{} _Mood("Intimate",Float)=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" } Pass {
Cull Off ZWrite Off ZTest Always
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float _Mood;
struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
half4 frag(V i):SV_Target {
    half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
    float2 floorUV=(i.uv-float2(.477,.50))/float2(.17,.23);
    float pool=1-smoothstep(.3,1.15,length(floorUV));
    float pulse=sin(_Time.y*lerp(3.2,1.0,_Mood)-length(floorUV)*3)*.045*pool;
    c.rgb*=1+pulse;
    c.rgb=lerp(c.rgb,c.rgb*float3(1.08,.91,.91),pool*_Mood*.65);
    return c;
}
ENDHLSL
} } }
