#ifndef WATERINPUT
#define WATERINPUT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
float _DissolveSpeedX, _DissolveSpeedY, _MaskSpeedX, _MaskSpeedY, _CustomVertexAnimOffset;
float _FresnelWidth, _FresnelSideScale, _VertexAnimScale, _VertexAnimWidth, _UseInRole, _CloseVertexColor;
float _Cutoff;
float _DepthOffset;
half4 _UVRotMatrix;
int4 _CopyColorBlend;
int _DissolveType, _DistortionScreenUV;
int _InvertFresnel, _FresnelA;
int _VATexG;
int _Surface;
// Toggle
int _NoLOn,_DissolveOSUV;
int _UseCopyColorBlend;
int _SoftParticle, _Distortion, _Fresnel, _VertexAnim, _PolarCoordinates,_UseMaskR,_UseDissolveA;
half4 _VATint1, _VATint2, _VATint3, _VertexAnimTiling;
half4 _BaseColor;
half4 _FresnelColor;
half4 _DissolveEdgeColor;
// ST
half4 _BaseMap_ST;
half4 _MaskTex_ST;
half4 _DissolveTex_ST;
half4 _DistortionTex_ST;
half4 _FresnelTex_ST;
half4 _VertexAnimTex_ST;
half4 _NoLTint;

half _NoLpos,_DissolveOSUVSmooth1;
half _SoftValue;
half _OffsetSpeedX, _OffsetSpeedY;
half _SoftParticleFadeParamsNear, _SoftParticleFadeParamsFar, _SoftParticleFadeHeightMapIntensity, _SoftParticleFadeHeightMapScale;
half _DissolveIntensity,_DissolveIntensity100, _DissolveEdgeWidth, _DissolveEdgeWidthSoft;
half _DistortionIntensity, _DistortionOpaque, _DistortionTransparents, _MainTexCustomDataON, _MaskTexCustomDataON, _DissolveCustomData;
half _DistortionSpeedX, _DistortionSpeedY;
half _FresnelIntensity, _FresnelOffsetX, _FresnelOffsetY;
half _VertexAnimSpeedX, _VertexAnimSpeedY, _VertexAnimIntensity, _VertexAnimTint, _VertexAnimCustomData;
// 其他
half _PreAlphaMul;
CBUFFER_END


TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);
TEXTURE2D(_DissolveTex);
SAMPLER(sampler_DissolveTex);
// TEXTURE2D(_CameraDepthTexture);
// SAMPLER(sampler_CameraDepthTexture);
TEXTURE2D(_DistortionTex);
SAMPLER(sampler_DistortionTex);
TEXTURE2D(_FresnelTex);
SAMPLER(sampler_FresnelTex);
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
#if !defined(SHADER_API_MOBILE)
    TEXTURE2D(_CameraTransparentsTexture);
    SAMPLER(sampler_CameraTransparentsTexture);
#endif
TEXTURE2D(_VertexAnimTex);
SAMPLER(sampler_VertexAnimTex);


#if defined(_SOFTPARTICLES_ON)  //软粒子
    // height : 用maintex作为高度图去丰富软粒子
    float SoftParticles(float near, float far, float4 projection, float height)
    {
        float fade = 1;
        // 代替 if 了
        near = max(0.0001, near);
        far = max(0.0001, far);
        float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, projection.xy / projection.w), _ZBufferParams);
#if defined( SHADER_API_OPENGL ) || defined( SHADER_API_GLES ) || defined( SHADER_API_GLES3 ) || defined( SHADER_API_GLCORE )
        float thisZ = LinearEyeDepth(projection.z / projection.w * 0.5 + 0.5, _ZBufferParams);//YJY
#else 
        float thisZ = LinearEyeDepth(projection.z / projection.w, _ZBufferParams);
#endif
        //#if UNITY_REVERSED_Z
        fade = saturate(far * ((sceneZ - near) - thisZ));
        // #else
        //     fade = saturate(far * ((sceneZ - near) + thisZ));
        // #endif
        float HeighFade = saturate(far * ((sceneZ - _SoftParticleFadeHeightMapScale) - thisZ));
        float HeighScale = (1 - HeighFade) * fade;
        HeighScale *= HeighScale;
        float heightMap = 1 - pow(height, _SoftParticleFadeHeightMapIntensity);
        return saturate(sceneZ- thisZ);//saturate(fade * (1-HeighScale *heightMap));

    }
#endif
#if defined(_POLARUV)   // 极坐标函数
    float2 RectToPolar(float2 uv, float2 centerUV)
    {
        uv = uv - centerUV;
        float theta = atan2(uv.y, uv.x);    // atan()值域[-π/2, π/2]一般不用; atan2()值域[-π, π]
        float r = length(uv);
        return float2(theta, r);
    }
#endif

half4 Triplanar(float3 posOS,half3 normal,float smooth,TEXTURE2D_PARAM(tex, smp),float4 ST)
{
    half3 normalws = normalize(normal);
    half3 weight = pow(abs(normalize(normalws)), smooth);
    half3 uvweight = weight /(weight.x+weight.y+weight.z);
    half4 col0 = SAMPLE_TEXTURE2D(tex,smp, posOS.xy * ST.xy + ST.zw)*uvweight.z;
    half4 col1 = SAMPLE_TEXTURE2D(tex,smp,posOS.xz * ST.xy + ST.zw)*uvweight .y;
    half4 col2 = SAMPLE_TEXTURE2D(tex,smp , posOS.zy * ST.xy + ST.zw)*uvweight .x;
    return col0+col1+col2;
}
float2 rotUV(float2 uv)
{
    // _UVRotMatrix = (cos, -sin, sin, cos), precomputed by GUI
    uv = mul(uv - 0.5, float2x2(_UVRotMatrix.x, _UVRotMatrix.y,
                                 _UVRotMatrix.z, _UVRotMatrix.w)) + 0.5;
    return uv;
}

struct appdata
{
    float4 PositionOS: POSITION;
    float3 normalOS: NORMAL;

    float4 uv: TEXCOORD0;
    float4 vertexColor: COLOR;
    float4 CustomData1: TEXCOORD1;
    float4 CustomData2: TEXCOORD2;
};
struct v2f
{
    float4 uv: TEXCOORD0;
    float3 normalWS: NORMAL;
    float4 PositionCS: SV_POSITION;
    float4 vertexColor: COLOR;
    float4 CustomData1: TEXCOORD1;
    float4 CustomData2: TEXCOORD2;
    #if defined(_SOFTPARTICLES_ON)  // 软粒子
        float4 ScreenPos: TEXCOORD3;
    #endif
    #if defined(_FRESNEL_ON) 
        float3 viewDirWS: TEXCOORD4;
    #endif

    float4 PositionOS : TEXCOORD7;
};
#endif
