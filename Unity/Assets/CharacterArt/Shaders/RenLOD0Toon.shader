Shader "LucidLoop/CharacterArt/Ren LOD0 Toon"
{
 Properties
 {
  _BaseMap("Painted albedo",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _AlbedoGain("Linear albedo gain",Vector)=(1,1,1,1)
  _ClosedEyeMap("Closed eyelid albedo",2D)="white"{}
  _ClosedEyeMask("Left/right eyelid mask RG",2D)="black"{}
  _ClosedEyeBlendL("Left eyelid correction",Range(0,1))=0
  _ClosedEyeBlendR("Right eyelid correction",Range(0,1))=0
  _ShadowColor("Shadow palette",Color)=(.55,.43,.58,1)
  _Threshold("Band threshold",Range(-1,1))=.08
  _Softness("Band softness",Range(.001,.2))=.025
  _FaceLighting("Broad face planes",Range(0,1))=0
  _FaceForward("Face forward object space",Vector)=(0,0,-1,0)
  _FaceForwardWorld("Animated face forward",Vector)=(0,0,-1,0)
  _UseWorldFace("Use animated face direction",Float)=0
  _FaceRightWorld("Animated face right",Vector)=(1,0,0,0)
  _AccentStrength("Local light strength",Range(0,1))=.28
  _AccentCap("Combined local light peak",Range(0,2))=.65
  _Diagnostic("0 shaded, 1 normals, 2 head frame",Range(0,2))=0
  _Cull("Cull mode",Float)=2
  _Ambient("Ambient floor",Range(0,1))=.35
  _Unlit("Ink / oral lighting override",Range(0,1))=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Name "ToonForward"
   Cull [_Cull]
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma shader_feature_local_fragment _CLOSED_EYE_CORRECTION
   #pragma target 3.5
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_ClosedEyeMap); SAMPLER(sampler_ClosedEyeMap);
   TEXTURE2D(_ClosedEyeMask); SAMPLER(sampler_ClosedEyeMask);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor,_ShadowColor,_FaceForward,_FaceForwardWorld,_AlbedoGain,_FaceRightWorld;
    float _Threshold,_Softness,_FaceLighting,_Ambient,_Unlit,_UseWorldFace;
    float _ClosedEyeBlendL,_ClosedEyeBlendR,_AccentStrength,_AccentCap,_Diagnostic,_Cull;
   CBUFFER_END
   struct A { float4 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
   struct V { float4 p:SV_POSITION; float3 world:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float3 face:TEXCOORD3; };
   V Vert(A a){ V o; o.p=TransformObjectToHClip(a.p.xyz);o.world=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.face=lerp(TransformObjectToWorldDir(_FaceForward.xyz),_FaceForwardWorld.xyz,_UseWorldFace);return o; }
   float Band(float3 n,float3 face,float3 direction){float d=dot(n,direction);d=lerp(d,dot(face,direction)*.7+dot(n,SafeNormalize(_FaceRightWorld.xyz))*dot(SafeNormalize(_FaceRightWorld.xyz),direction)*.3,_FaceLighting);float edge=max(_Softness,fwidth(d));return smoothstep(_Threshold-edge,_Threshold+edge,d);}
   half3 Accent(Light light,float3 n,float3 face)
   {
    // Broad facial field applies to both key and local lights. Accumulate all
    // candidates, then bound total energy: no unstable first-N truncation.
    return light.color*light.distanceAttenuation*light.shadowAttenuation
      *Band(n,face,light.direction)*_AccentStrength;
   }
   half4 Frag(V i):SV_Target
   {
    half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    #if defined(_CLOSED_EYE_CORRECTION)
    half2 mask=SAMPLE_TEXTURE2D(_ClosedEyeMask,sampler_ClosedEyeMask,i.uv).rg;
    half blend=saturate(mask.r*_ClosedEyeBlendL+mask.g*_ClosedEyeBlendR);
    albedo=lerp(albedo,SAMPLE_TEXTURE2D(_ClosedEyeMap,sampler_ClosedEyeMap,i.uv).rgb,blend);
    #endif
    half3 base=albedo*_BaseColor.rgb*_AlbedoGain.rgb;
    float3 n=SafeNormalize(i.n),face=SafeNormalize(i.face);
    if(_Diagnostic>1.5)return half4(face*.5+.5,1);
    if(_Diagnostic>.5)return half4(n*.5+.5,1);
    // Face review uses authored shadow bands; no camera screen-shadow dependency.
    Light main=GetMainLight();
    half band=Band(n,face,main.direction)*lerp(main.shadowAttenuation,1,_FaceLighting*.75);
    half3 color=base*lerp(_ShadowColor.rgb,1,band)*max(main.color,half3(_Ambient,_Ambient,_Ambient));
    #if defined(_ADDITIONAL_LIGHTS)
    InputData inputData=(InputData)0;
    inputData.positionWS=i.world;
    inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
    half3 accents=0;
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
     CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
     accents+=Accent(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,face);
    }
    #endif
    uint count=GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
     accents+=Accent(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,face);
    LIGHT_LOOP_END
    half peak=max(accents.r,max(accents.g,accents.b));
    accents*=min(1,_AccentCap/max(peak,.0001));
    color+=base*accents;
    #endif
    color=all(isfinite(color))?color:base;
    return half4(lerp(color,base,_Unlit),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
