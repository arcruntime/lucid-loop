Shader "LucidLoop/Characters/RenTokonInk"
{
    Properties { _ControlMap("Linear R face B suppression",2D)="black" {} _UseControlMap("Use control map",Float)=0 _ControlFallback("R face B suppression",Vector)=(0,0,0,0) _Ink("Ink",Color)=(0.025,0.019,0.037,1) _Width("Width (world meters)",Range(0,0.01))=0.0015 _MaxPixels("Maximum width at 932px height",Range(0.25,3))=0.85 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ControlMap); SAMPLER(sampler_ControlMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _Ink, _ControlFallback; float _Width, _MaxPixels, _UseControlMap; float4 _ControlMap_ST;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; half face:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                half4 controls=_ControlFallback;
                if(_UseControlMap>0.5) controls=SAMPLE_TEXTURE2D_LOD(_ControlMap,sampler_ControlMap,TRANSFORM_TEX(v.uv,_ControlMap),0);
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                float4 original=TransformWorldToHClip(p);
                float3 expanded=p+TransformObjectToWorldNormal(v.normalOS)*_Width*(1-controls.b*0.85);
                float4 clip=TransformWorldToHClip(expanded);
                float2 delta=clip.xy/clip.w-original.xy/original.w;
                float pixels=length(delta*_ScreenParams.xy*0.5);
                float limit=_MaxPixels*(_ScreenParams.y/932.0)*(1-controls.b*0.85);
                clip.xy=(original.xy/original.w+delta*min(1.0,limit/max(pixels,0.0001)))*clip.w;
                Varyings o; o.positionCS=clip; o.positionWS=p;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.face=controls.r;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Facial ink is painted in the albedo. Keep the outer contour,
                // but reject internal hull strokes around the nose and eyelids.
                half facing=abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
                clip(max(0.5-i.face,0.35-facing));
                return _Ink;
            }
            ENDHLSL
        }
    }
}
