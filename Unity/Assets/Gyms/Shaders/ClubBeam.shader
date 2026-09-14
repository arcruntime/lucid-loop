Shader "LucidLoop/Environment/Beam"
{
    Properties { _BaseColor("Color",Color)=(1,.03,.4,1) _Opacity("Haze",Range(0,.2))=.045 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Opacity;
            CBUFFER_END
            struct A { float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2; };
            V Vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                half face=abs(dot(normalize(i.n),GetWorldSpaceNormalizeViewDir(i.w)));
                half fade=smoothstep(0,.07,i.uv.y)*(1-i.uv.y)*saturate(face*3);
                return half4(_BaseColor.rgb*2,_Opacity*fade);
            }
            ENDHLSL
        }
    }
}
