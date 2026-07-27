#ifndef NPRWATERINPUT
#define NPRWATERINPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float  fogCoord    : TEXCOORD0;
    float3 positionVS  : TEXCOORD1;
    float3 positionWS  : TEXCOORD2;
    float3 normalWS    : TEXCOORD3;
};

CBUFFER_START(UnityPerMaterial)
half  _WaterAlpha;
half  _DepthIntensity;
half4 _CartoonSpecular;
half  _ToonSpecMin;
half  _ToonSpecMax;
half4 _ToonNoiseTex_ST;
half4 _ToonNoiseSpeed;
half  _fresnelScale;
half4 _fresnelColor;
half4 _DistortionTex_ST;
half  _DistortionIntensity;
half4 _DistortionSpeed;
half4 _CausticTex_ST;
half  _CausticIntensity;
half  _CausticScale;
half  _CausticFacade;
half4 _CausticSpeed;
half4 _FoamTint;
half  _FoamEdgeWidth;
half  _FoamScope;
half  _FoamInterval;
half  _FoamAnimSpeed;
half4 _FoamNoiseTex_ST;
half  _FoamNoiseAmp;
half  _FoamFadePower;
half4 _FoamNoiseSpeed;
half4 _SDFBoundsMin;
half4 _SDFBoundsSize;
CBUFFER_END

TEXTURE2D(_WaterDepthLUT);       SAMPLER(sampler_WaterDepthLUT);
TEXTURE2D(_ToonNoiseTex);        SAMPLER(sampler_ToonNoiseTex);
TEXTURE2D(_FoamSDF);             SAMPLER(sampler_FoamSDF);
TEXTURE2D(_FoamNoiseTex);        SAMPLER(sampler_FoamNoiseTex);
TEXTURE2D(_CameraDepthTexture);  SAMPLER(sampler_CameraDepthTexture);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
TEXTURE2D(_DistortionTex);       SAMPLER(sampler_DistortionTex);
TEXTURE2D(_CausticTex);          SAMPLER(sampler_CausticTex);

#endif
