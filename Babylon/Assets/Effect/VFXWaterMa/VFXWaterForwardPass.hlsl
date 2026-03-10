#ifndef VFXWATER_FORWARD_PASS_INCLUDED
#define VFXWATER_FORWARD_PASS_INCLUDED

#include "VFXWaterInput.hlsl"

// ============================================================================
// 结构体
// ============================================================================
struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord0    : TEXCOORD0;
    float2 texcoord1    : TEXCOORD1;
    half4  color        : COLOR;
};

struct Varyings
{
    float4 positionCS       : SV_POSITION;
    float4 uv01             : TEXCOORD0; // xy=uv0, zw=uv1
    float3 positionWS       : TEXCOORD1;
    half3  normalWS         : TEXCOORD2;
    half4  tangentWS        : TEXCOORD3; // xyz=tangent, w=sign
    half3  viewDirWS        : TEXCOORD4;
    half   fogFactor        : TEXCOORD5;
    half4  vertexColor      : TEXCOORD6;
};

// ============================================================================
// Matcap UV 计算（参考工程原始算法，带视角旋转补偿）
// ============================================================================
half2 ComputeMatCapUV(half3 normalWS, float3 positionWS)
{
    // 将法线转到视图空间
    float3 viewNorm = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
    // 计算视图空间位置并归一化为视线方向
    float3 viewPos = mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).xyz;
    float3 viewDir = normalize(viewPos);
    // 用 cross 做旋转补偿，防止相机旋转时 Matcap 偏移
    float3 viewCross = cross(viewDir, viewNorm);
    viewNorm = float3(-viewCross.y, viewCross.x, 0.0);
    return viewNorm.xy * 0.5 + 0.5;
}

// ============================================================================
// 法线混合（UDN 方式）
// ============================================================================
half3 BlendNormalsUDN(half3 n1, half3 n2)
{
    half3 r;
    r.xy = n1.xy + n2.xy;
    r.z = n1.z;
    return normalize(r);
}

// ============================================================================
// 顶点着色器
// ============================================================================
Varyings VFXWaterVert(Attributes input)
{
    Varyings output = (Varyings)0;

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = normalWS;

    // 切线空间
    half sign = input.tangentOS.w * GetOddNegativeScale();
    half3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
    output.tangentWS = half4(tangentWS, sign);

    output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    output.uv01 = float4(input.texcoord0, input.texcoord1);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    output.vertexColor = input.color;

    return output;
}

// ============================================================================
// 片元着色器
// ============================================================================
half4 VFXWaterFrag(Varyings input) : SV_Target
{
    float2 uv0 = input.uv01.xy;
    float2 uv1 = input.uv01.zw;

    // ------------------------------------------------------------------
    // 1. 采样 Albedo
    // ------------------------------------------------------------------
    half4 albedo = SAMPLE_TEXTURE2D(_AlbedoMap, sampler_AlbedoMap, TRANSFORM_TEX(uv0, _AlbedoMap));
    // Gamma 校正（Gamma 工作空间下需要转 Linear）
    #if defined(UNITY_COLORSPACE_GAMMA)
        albedo.rgb = SRGBToLinear(albedo.rgb);
    #endif
    albedo.rgb *= _AlbedoColor.rgb;
    half opacity = albedo.a * _AlbedoColor.a;

    // ------------------------------------------------------------------
    // 2. 采样遮罩图 (R-厚度, G-浪尖范围, B-泡沫渐变)
    // ------------------------------------------------------------------
    half4 waveMask = SAMPLE_TEXTURE2D(_WaveMaskMap, sampler_WaveMaskMap, TRANSFORM_TEX(uv0, _WaveMaskMap));

    // ------------------------------------------------------------------
    // 3. 双层法线混合
    // ------------------------------------------------------------------
    // 物体法线
    half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv0, _NormalMap));
    half3 normalTS = UnpackNormal(normalSample);
    normalTS = lerp(half3(0, 0, 1), normalTS, _NormalScaleA);

    // 海浪法线 (UV动画)
    half2 waveUV = uv1 * _WaveParamsB.xy + frac(_WaveParamsB.zw * _Time.x);
    half4 waveNormalSample = SAMPLE_TEXTURE2D(_WaveNormalMap, sampler_WaveNormalMap, waveUV);
    half3 waveNormalTS = UnpackNormal(waveNormalSample);
    waveNormalTS = lerp(half3(0, 0, 1), waveNormalTS, _NormalScaleB);

    // 混合
    half3 finalNormalTS = normalize(normalTS + waveNormalTS);

    // 转换到世界空间
    half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    half3x3 TBN = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
    half3 normalWS = normalize(mul(finalNormalTS, TBN));

    // ------------------------------------------------------------------
    // 4. SSS 水晶通透效果
    // ------------------------------------------------------------------
    half diffuseLerp = smoothstep(_DiffuseLerp1, _DiffuseLerp2, saturate(waveMask.r)) * _DiffuseLerp * (1.0 - waveMask.g);
    half3 diffuseColor = albedo.rgb;

    // ------------------------------------------------------------------
    // 5. SSS 通透直接影响透明度
    // ------------------------------------------------------------------
    opacity *= (1.0 - diffuseLerp);

    // ------------------------------------------------------------------
    // 7. Matcap 环境反射
    // ------------------------------------------------------------------
    half2 matCapUV = ComputeMatCapUV(normalWS, input.positionWS);
    half3 specularLobe = SAMPLE_TEXTURE2D(_EnvCapTex, sampler_EnvCapTex, matCapUV).rgb;
    // 确保线性空间
    #if defined(UNITY_COLORSPACE_GAMMA)
        specularLobe = SRGBToLinear(specularLobe);
    #endif
    specularLobe *= _EnvColor.rgb;

    diffuseColor += specularLobe * (1.0 - waveMask.g);

    // Matcap 亮度补偿透明度，防止高光区域因透明度低而消失
    half a = max(0, dot(specularLobe, half3(0.22, 0.707, 0.071)));
    opacity = saturate(opacity + a);

    // ------------------------------------------------------------------
    // 9. 最终颜色（参考工程不做传统光照，直接输出混合后的颜色）
    // ------------------------------------------------------------------
    half3 finalColor = diffuseColor;

    // 应用整体透明度
    opacity *= _Opacity;

    half4 outputColor = half4(finalColor, opacity);

    // ------------------------------------------------------------------
    // 10. 雾效
    // ------------------------------------------------------------------
    outputColor.rgb = MixFog(outputColor.rgb, input.fogFactor);

    return outputColor;
}

// ============================================================================
// ShadowCaster 顶点/片元
// ============================================================================
struct ShadowAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord0    : TEXCOORD0;
};

struct ShadowVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
};

float3 _LightDirection;

ShadowVaryings VFXWaterShadowVert(ShadowAttributes input)
{
    ShadowVaryings output = (ShadowVaryings)0;

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // Shadow bias
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif

    output.positionCS = positionCS;
    output.uv = input.texcoord0;
    return output;
}

half4 VFXWaterShadowFrag(ShadowVaryings input) : SV_Target
{
    return 0;
}

// ============================================================================
// DepthOnly 顶点/片元
// ============================================================================
struct DepthAttributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord0    : TEXCOORD0;
};

struct DepthVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv           : TEXCOORD0;
};

DepthVaryings VFXWaterDepthVert(DepthAttributes input)
{
    DepthVaryings output = (DepthVaryings)0;

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = input.texcoord0;
    return output;
}

half4 VFXWaterDepthFrag(DepthVaryings input) : SV_Target
{
    return 0;
}

#endif
