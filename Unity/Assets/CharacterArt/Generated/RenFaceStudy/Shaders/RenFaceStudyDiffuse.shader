Shader "LucidLoop/RenFaceStudy/Diffuse"
{
    Properties
    {
        _LinearBaseColor("Linear review color", Vector) = (0.5,0.5,0.5,1)
        _UseVertexColor("Replace with linear vertex color", Float) = 0
        _VertexColorSrgb("Decode source vertex color from sRGB", Float) = 0
        _BaseMap("Source base color", 2D) = "white" {}
        _UseBaseMap("Use source base color", Float) = 0
        _BumpMap("Optional source normal", 2D) = "bump" {}
        _NormalScale("Source normal strength; zero disables", Float) = 0
        _Unlit("Unlit diagnostic", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Diffuse review"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _LinearBaseColor;
                float _UseVertexColor;
                float _VertexColorSrgb;
                float _Unlit;
                float _UseBaseMap;
                float _NormalScale;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 tangentOS : TANGENT;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                // Blender's IrisColor drives Base Color directly. FBX stores these values as LINEAR.
                half3 sourceColor = _VertexColorSrgb > 0.5 ? SRGBToLinear(input.color.rgb) : input.color.rgb;
                half3 albedo = lerp(_LinearBaseColor.rgb, sourceColor, saturate(_UseVertexColor));
                if (_UseBaseMap > 0.5) albedo *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                if (_Unlit > 0.5) return half4(albedo, 1);
                half3 normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                if (_NormalScale > 0.0001)
                {
                    half3 tangentWS = normalize(input.tangentWS.xyz);
                    half3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _NormalScale);
                    normalWS = normalize(tangentWS * normalTS.x + bitangentWS * normalTS.y + normalWS * normalTS.z);
                }
                half3 lightSum = max(half3(0,0,0), SampleSH(normalWS));
                Light key = GetMainLight();
                lightSum += key.color * saturate(dot(normalWS, key.direction)) * key.distanceAttenuation;
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light additional = GetAdditionalLight(lightIndex, input.positionWS);
                    lightSum += additional.color * saturate(dot(normalWS, additional.direction)) * additional.distanceAttenuation;
                LIGHT_LOOP_END
                #endif
                return half4(albedo * lightSum, 1);
            }
            ENDHLSL
        }
    }
}
