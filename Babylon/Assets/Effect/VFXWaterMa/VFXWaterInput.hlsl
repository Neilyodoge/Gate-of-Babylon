#ifndef VFXWATER_INPUT_INCLUDED
#define VFXWATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ============================================================================
// CBUFFER - 材质属性
// ============================================================================
CBUFFER_START(UnityPerMaterial)
    // Diffuse
    half4 _AlbedoColor;
    float4 _AlbedoMap_ST;
    float4 _NormalMap_ST;

    // WaveNormal
    half _NormalScaleA;
    half _NormalScaleB;
    float4 _WaveParamsB; // xy=UV缩放, zw=UV滚动速度

    // Mask
    float4 _WaveMaskMap_ST;

    // SSS
    half _DiffuseLerp;
    half _DiffuseLerp1;
    half _DiffuseLerp2;

    // Matcap
    half4 _EnvColor;

    // 整体透明度
    half _Opacity;

CBUFFER_END

// ============================================================================
// 纹理 & 采样器
// ============================================================================
TEXTURE2D(_AlbedoMap);          SAMPLER(sampler_AlbedoMap);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
TEXTURE2D(_WaveNormalMap);      SAMPLER(sampler_WaveNormalMap);
TEXTURE2D(_WaveMaskMap);        SAMPLER(sampler_WaveMaskMap);
TEXTURE2D(_EnvCapTex);          SAMPLER(sampler_EnvCapTex);



#endif
