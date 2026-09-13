#ifndef LUCID_LOOP_PAINTED_ANIME_NPR_INCLUDED
#define LUCID_LOOP_PAINTED_ANIME_NPR_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_PaintRegionMap); SAMPLER(sampler_PaintRegionMap);
// All passes share exactly this material buffer. Frame updates use cloned
// material instances, never MaterialPropertyBlocks, to retain SRP Batcher use.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor, _ShadeColor, _DeepShadeColor, _HighlightColor, _RimColor;
    float4 _FaceForwardWS, _FaceRightWS, _FaceUpWS, _FaceCenterWS;
    float _FaceWidth;
    half _UseBaseMap, _UseVertexColor, _VertexColorSrgb, _Unlit;
    half _ShadeThreshold, _ShadeSoftness, _DeepThreshold, _DeepSoftness;
    half _UsePaintRegions, _PaintRegionStrength, _AmbientFloor, _ProbeStrength;
    half _MainLightStrength, _AdditionalLightStrength, _SceneColorStrength, _ShadowStrength;
    half _MaxAdditionalLights, _HighlightStrength, _HighlightThreshold, _HighlightSoftness;
    half _RimStrength, _RimPower, _FaceMode, _FaceCurvature;
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
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        output.fogAndVertexLight.yzw = VertexLighting(p.positionWS, output.normalWS);
    #endif
    return output;
}
half4 NprSource(NprVaryings input)
{
    half3 vertexColor = _VertexColorSrgb > 0.5h ? SRGBToLinear(input.color.rgb) : input.color.rgb;
    half4 color = half4(lerp(_BaseColor.rgb, vertexColor, saturate(_UseVertexColor)), _BaseColor.a);
    if (_UseBaseMap > 0.5h)
        color *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    #if defined(_ALPHATEST_ON)
        clip(color.a - _Cutoff);
    #endif
    return color;
}
half NprPeak(half3 color) { return max(color.r, max(color.g, color.b)); }
half NprBand(half value, half threshold, half softness)
{
    half width = max(softness, 0.01h);
    return smoothstep(threshold - width, threshold + width, value);
}
half3 NprLightingNormal(float3 positionWS, half3 geometryNormal)
{
    // A broad face-plane field, not an SDF likeness model. No UV dependence:
    // transferable to the selected final head; cheeks/nose mesh noise cannot
    // create extra diffuse terminators. Frame must follow the animated head.
    float3 relative = (positionWS - _FaceCenterWS.xyz) / max(_FaceWidth, 0.001);
    half side = clamp(dot(relative, _FaceRightWS.xyz), -1.0, 1.0);
    half height = clamp(dot(relative, _FaceUpWS.xyz), -1.0, 1.0);
    half3 broad = SafeNormalize(_FaceForwardWS.xyz + _FaceRightWS.xyz * side * _FaceCurvature
                              + _FaceUpWS.xyz * height * 0.12h);
    return SafeNormalize(lerp(geometryNormal, broad, saturate(_FaceMode)));
}
struct NprLightSum
{
    half tone;
    half energy;
    half3 hue;
};
void NprAccumulate(Light light, half3 normalWS, half strength, inout NprLightSum sum)
{
    #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer())) return;
    #endif
    half peak = NprPeak(light.color);
    // URP retains distance, spot cone and shadow attenuation. Bounding each
    // contribution protects painterly colors from near-light blowouts.
    half energy = min(peak * light.distanceAttenuation, 2.0h) * strength;
    half receivedShadow = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
    half broadLight = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
    half weight = energy * broadLight * receivedShadow;
    sum.tone += weight;
    sum.energy += weight;
    sum.hue += light.color / max(peak, 0.0001h) * weight;
}
half4 NprFragment(NprVaryings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half3 baseColor = NprSource(input).rgb;
    if (_Unlit > 0.5h) return half4(baseColor, 1);
    half3 geometricNormal = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(frontFace, 1, -1);
    half3 normalWS = NprLightingNormal(input.positionWS, geometricNormal);
    half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #endif
    Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
    NprLightSum sum = (NprLightSum)0;
    NprAccumulate(mainLight, normalWS, _MainLightStrength, sum);
    #if defined(_ADDITIONAL_LIGHTS)
        uint additionalUsed = 0;
        uint additionalBudget = (uint)clamp(_MaxAdditionalLights, 0.0h, 8.0h);
        #if USE_CLUSTER_LIGHT_LOOP
            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
            {
                CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                if (additionalUsed < additionalBudget)
                {
                    NprAccumulate(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)),
                                  normalWS, _AdditionalLightStrength, sum);
                    ++additionalUsed;
                }
            }
        #endif
        uint pixelLightCount = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(pixelLightCount)
            if (additionalUsed < additionalBudget)
            {
                NprAccumulate(GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1)),
                              normalWS, _AdditionalLightStrength, sum);
                ++additionalUsed;
            }
        LIGHT_LOOP_END
    #endif
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
        // Compatibility fallback. Review/phone preset uses per-pixel lights so
        // face-plane normals and additional shadow maps are evaluated correctly.
        half3 vertexLight = input.fogAndVertexLight.yzw;
        half energy = min(NprPeak(vertexLight), 2.0h) * _AdditionalLightStrength;
        sum.tone += energy; sum.energy += energy;
        sum.hue += vertexLight / max(NprPeak(vertexLight), 0.0001h) * energy;
    #endif
    half3 regions = half3(0.5h,0.5h,1.0h);
    #if defined(_PAINT_REGIONS)
        regions = SAMPLE_TEXTURE2D(_PaintRegionMap, sampler_PaintRegionMap, input.uv).rgb;
    #endif
    half probe = min(NprPeak(max(SampleSH(normalWS), 0.0h)), 1.0h) * _ProbeStrength;
    half tone = saturate(_AmbientFloor + probe + sum.tone + (regions.r - 0.5h) * _PaintRegionStrength);
    half baseBand = NprBand(tone, _ShadeThreshold, _ShadeSoftness);
    half deepBand = NprBand(tone + (regions.g - 0.5h) * _PaintRegionStrength, _DeepThreshold, _DeepSoftness);
    half3 color = lerp(baseColor * _DeepShadeColor.rgb, baseColor * _ShadeColor.rgb, deepBand);
    color = lerp(color, baseColor, baseBand);
    half3 sceneHue = sum.energy > 0.0001h ? sum.hue / sum.energy : half3(1,1,1);
    color *= lerp(half3(1,1,1), sceneHue, saturate(_SceneColorStrength * sum.energy));
    // Broad pigment accents, not a microfacet/specular BRDF. Skin preset sets
    // strength exactly zero. No normal map, reflection probes, or per-light spec.
    half mainEnergy = saturate(NprPeak(mainLight.color) * mainLight.distanceAttenuation)
                      * lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
    #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(mainLight.layerMask, GetMeshRenderingLayer())) mainEnergy = 0;
    #endif
    half3 halfDirection = SafeNormalize(mainLight.direction + viewWS);
    half highlight = NprBand(saturate(dot(normalWS, halfDirection)), _HighlightThreshold, _HighlightSoftness)
                     * _HighlightStrength * mainEnergy * regions.b;
    color = lerp(color, max(color, _HighlightColor.rgb * lerp(half3(1,1,1), sceneHue, _SceneColorStrength)), saturate(highlight));
    half rim = pow(saturate(1.0h - dot(geometricNormal, viewWS)), _RimPower)
               * saturate(dot(geometricNormal, mainLight.direction) * 0.5h + 0.5h)
               * _RimStrength * mainEnergy;
    color = lerp(color, max(color, _RimColor.rgb * lerp(half3(1,1,1), sceneHue, _SceneColorStrength)), saturate(rim));
    return half4(MixFog(color, input.fogAndVertexLight.x), 1);
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
