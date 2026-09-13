#ifndef LUCID_LOOP_REN_TOKON_NPR_INCLUDED
#define LUCID_LOOP_REN_TOKON_NPR_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

TEXTURE2D(_ShadowMap); SAMPLER(sampler_ShadowMap);
TEXTURE2D(_ControlMap); SAMPLER(sampler_ControlMap);
TEXTURE2D(_ClosedBaseMap); SAMPLER(sampler_ClosedBaseMap);
TEXTURE2D(_ClosedShadowMap); SAMPLER(sampler_ClosedShadowMap);
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
// All passes share exactly this material buffer. Frame updates use cloned
// material instances, never MaterialPropertyBlocks, to retain SRP Batcher use.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _ControlFallback;
    half _UseShadowMap, _UseControlMap, _ClosedWeight;
    half _Threshold, _BoundarySoftness, _FaceCastShadowStrength;
    half4 _BaseColor, _ShadeColor, _HighlightColor, _RimColor;
    float4 _FaceForwardWS, _FaceRightWS, _FaceUpWS, _FaceCenterWS;
    float _FaceWidth;
    half _UseBaseMap, _UseVertexColor, _VertexColorSrgb, _Unlit;
    half _MainLightStrength, _AdditionalLightStrength, _SceneColorStrength, _ShadowStrength;
    half _MaxAdditionalLights, _HighlightStrength, _HighlightThreshold;
    half _RimStrength, _FaceMode;
    half _Cull, _AlphaClip, _Cutoff;
CBUFFER_END

struct NprAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct NprVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half4 fogAndVertexLight : TEXCOORD3;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
NprVaryings NprVertex(NprAttributes input)
{
    NprVaryings output = (NprVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = p.positionCS;
    output.positionWS = p.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.color = input.color;
    output.fogAndVertexLight.x = ComputeFogFactor(p.positionCS.z);
    return output;
}
half4 NprSource(NprVaryings input)
{
    half3 vertexColor = _VertexColorSrgb > 0.5h ? SRGBToLinear(input.color.rgb) : input.color.rgb;
    half4 color = half4(lerp(_BaseColor.rgb, vertexColor, saturate(_UseVertexColor)), _BaseColor.a);
    if (_UseBaseMap > 0.5h)
    {
        half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
        if (_ClosedWeight > 0.0h)
            baseTex = lerp(baseTex, SAMPLE_TEXTURE2D(_ClosedBaseMap, sampler_ClosedBaseMap, input.uv), saturate(_ClosedWeight));
        color *= baseTex;
    }
    #if defined(_ALPHATEST_ON)
        clip(color.a - _Cutoff);
    #endif
    return color;
}
half NprPeak(half3 color) { return max(color.r, max(color.g, color.b)); }
// First-N light evaluation is a bounded study policy, not stable priority selection.
void TokonAdditional(Light light, half3 normalWS, half face, inout half3 accent)
{
    #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer())) return;
    #endif
    half energy = min(NprPeak(light.color) * light.distanceAttenuation, 1.0h);
    half3 forward = SafeNormalize(_FaceForwardWS.xyz);
    half3 right = SafeNormalize(_FaceRightWS.xyz);
    half diffuse = lerp(dot(normalWS, light.direction), dot(forward, light.direction)*0.7h + dot(normalWS, right)*dot(right, light.direction)*0.3h, face);
    half edge = max(_BoundarySoftness, fwidth(diffuse)*0.75h);
    half response = smoothstep(_Threshold - edge, _Threshold + edge, diffuse);
    accent += light.color / max(NprPeak(light.color), 0.0001h) * energy * response * _AdditionalLightStrength;
}
half4 NprFragment(NprVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half3 base = NprSource(input).rgb;
    if (_Unlit > 0.5h) return half4(base, 1);
    half4 controls = _ControlFallback;
    if (_UseControlMap > 0.5h) controls = SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, input.uv);
    half face = saturate(controls.r * _FaceMode);
    half skin = saturate(controls.a);
    half3 n = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(frontFace, 1, -1);
    half3 forward = SafeNormalize(_FaceForwardWS.xyz);
    half3 right = SafeNormalize(_FaceRightWS.xyz);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 sc = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
        float4 sc = TransformWorldToShadowCoord(input.positionWS);
    #endif
    Light key = GetMainLight(sc, input.positionWS, half4(1,1,1,1));
    half diffuse = lerp(dot(n, key.direction), dot(forward,key.direction)*0.7h + dot(n,right)*dot(right,key.direction)*0.3h, face);
    half edge = max(_BoundarySoftness, fwidth(diffuse)*0.75h);
    half band = smoothstep(_Threshold-edge, _Threshold+edge, diffuse);
    // Shadow region is classification, independent of normal-stabilization strength.
    half castStrength = lerp(_ShadowStrength, _FaceCastShadowStrength, saturate(controls.r));
    band *= lerp(1.0h, smoothstep(0.35h,0.55h,key.shadowAttenuation), castStrength);
    half3 shadow = base * _ShadeColor.rgb;
    if (_UseShadowMap > 0.5h)
    {
        shadow = SAMPLE_TEXTURE2D(_ShadowMap,sampler_ShadowMap,input.uv).rgb;
        if (_ClosedWeight > 0.0h) shadow = lerp(shadow,SAMPLE_TEXTURE2D(_ClosedShadowMap,sampler_ClosedShadowMap,input.uv).rgb,saturate(_ClosedWeight));
        // Shadow map is complete albedo, never multiplied by base albedo.
        shadow *= _BaseColor.rgb;
    }
    half keyEnergy = saturate(NprPeak(key.color)*key.distanceAttenuation*_MainLightStrength);
    #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(key.layerMask, GetMeshRenderingLayer())) keyEnergy=0;
    #endif
    band *= keyEnergy;
    half3 keyHue = key.color/max(NprPeak(key.color),0.0001h);
    half3 color = lerp(shadow,base,band) * lerp(half3(1,1,1),keyHue,saturate(_SceneColorStrength*keyEnergy));
    half3 accents=0;
    InputData inputData=(InputData)0;
    inputData.positionWS=input.positionWS;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
    #if defined(_ADDITIONAL_LIGHTS)
        uint used=0; uint budget=(uint)clamp(_MaxAdditionalLights,0.0h,8.0h);
        #if USE_CLUSTER_LIGHT_LOOP
            [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                if(used<budget) { TokonAdditional(GetAdditionalLight(lightIndex,input.positionWS),n,face,accents); ++used; }
            }
        #endif
        uint pixelLightCount=GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            if(used<budget) { TokonAdditional(GetAdditionalLight(lightIndex,input.positionWS),n,face,accents); ++used; }
        LIGHT_LOOP_END
    #endif
    // Additional lights are explicitly unshadowed and their combined lift is capped.
    color += base * min(accents,half3(0.35h,0.35h,0.35h));
    half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
    half h=dot(n,SafeNormalize(key.direction+view));
    half specEdge=max(0.001h,fwidth(h));
    half spec=smoothstep(_HighlightThreshold-specEdge,_HighlightThreshold+specEdge,h)*controls.g*(1-skin)*(1-face)*band;
    color += _HighlightColor.rgb*spec*_HighlightStrength;
    half grazing=1-saturate(dot(n,view));
    half rimEdge=max(0.005h,fwidth(grazing));
    half rim=smoothstep(0.78h-rimEdge,0.78h+rimEdge,grazing)*saturate(diffuse)*(1-skin)*(1-face)*keyEnergy;
    color += _RimColor.rgb*rim*_RimStrength;
    return half4(MixFog(color,input.fogAndVertexLight.x),1);
}
half4 NprDepth(NprVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    NprSource(input);
    return 0;
}
half4 NprDepthNormals(NprVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    NprSource(input);
    half3 normal = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(frontFace, 1, -1);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 octNormal = PackNormalOctQuadEncode(normal);
        return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
    #endif
    return half4(normal, 0);
}
float3 _LightDirection;
float3 _LightPosition;
NprVaryings NprShadowVertex(NprAttributes input)
{
    NprVaryings output = NprVertex(input);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDirection = normalize(_LightPosition - output.positionWS);
    #else
        float3 lightDirection = _LightDirection;
    #endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
        ApplyShadowBias(output.positionWS, output.normalWS, lightDirection)));
    return output;
}
#endif
