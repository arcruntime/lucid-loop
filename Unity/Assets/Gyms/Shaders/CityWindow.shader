Shader "LucidLoop/Environment/CityWindow"
{
    Properties
    {
        _BaseMap("City panorama",2D)="black"{}
        _Exposure("Exposure",Range(0,3))=.8
    }
    SubShader
    {
        Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;float _Exposure;
            CBUFFER_END
            struct Attributes{float4 positionOS:POSITION;};
            struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            Varyings Vert(Attributes i)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                // Exterior glazing is cut away with the roof in the gameplay overview.
                if(unity_OrthoParams.w>.5)clip(-1);
                // An infinite panorama through the window, with consistent camera parallax.
                float3 ray=normalize(i.positionWS-GetCameraPositionWS());
                float2 uv=float2(atan2(ray.z,ray.x)/6.2831853+.5,asin(clamp(ray.y,-1,1))/3.14159265+.5);
                half3 city=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb;
                return half4(city*_Exposure,1);
            }
            ENDHLSL
        }
    }
}
