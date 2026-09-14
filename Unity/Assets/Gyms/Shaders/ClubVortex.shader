Shader "LucidLoop/Environment/ClubVortex"
{
    Properties
    {
        _BaseColor ("Violet", Color) = (0.24,0.015,0.6,1)
        _AccentColor ("Rose", Color) = (1,0.015,0.35,1)
        _Energy ("Energy", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _AccentColor;
            half _Energy;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=(i.uv-.5)*float2(1.75,1);
                float r=length(p), a=atan2(p.y,p.x);
                float phase=a*4+r*22-_Time.y*.42;
                half ribbon=pow(saturate(.5+.5*sin(phase+sin(r*24-a*2)*.45)),4);
                half fine=pow(saturate(.5+.5*sin(phase*2.5+r*10)),18);
                half green=pow(saturate(.5+.5*sin(a*7+r*39+1.2)),34)*smoothstep(.15,.5,r);
                half core=exp(-r*19);
                half scan=.94+.06*sin(i.uv.y*1050);
                half3 c=lerp(_BaseColor.rgb*.7,_AccentColor.rgb*1.9,ribbon);
                c+=fine*half3(.65,.25,.8)+green*half3(.4,.55,.07)+core*half3(2,1.3,1.7);
                return half4(c*scan*_Energy,1);
            }
            ENDHLSL
        }
    }
}
