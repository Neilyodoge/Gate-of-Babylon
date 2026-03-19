// PBRToonCommon.hlsl
// 从 DanbaidongRP/PBRToon 移植并适配 URP 的公共工具库
// 包含: Toon 光照辅助函数、结构体、RimLight、间接光照评估等

#ifndef PBR_TOON_COMMON_INCLUDED
#define PBR_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/BSDF.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

// ============================================================================
// 工具函数
// ============================================================================

// Sigmoid 锐化函数，用于将 [0,1] 渐变映射为可控过渡的阶梯函数
// x: 输入值, center: 中心点, sharp: 锐度 (越大越锐利)
float SigmoidSharp(float x, float center, float sharp)
{
    float s = saturate((x - center) * sharp + 0.5);
    return s;
}

// ============================================================================
// 光照数据结构
// ============================================================================

struct ToonDirectLighting
{
    float3 diffuse;
    float3 specular;
};

struct ToonIndirectLighting
{
    float3 diffuse;
    float3 specular;
};

// ============================================================================
// RimLight 相关函数 (屏幕空间深度边缘光)
// 需要 _CameraDepthTexture，仅在 include 了 DeclareDepthTexture.hlsl 的 Pass 中可用
// ============================================================================
#ifdef UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED

// 主光源方向 Rim Light 区域检测
// normalVS: 视图空间法线, screenUV: 屏幕 UV, d: 深度, rimWidth: 边缘宽度
float GetCharacterDirectRimLightArea(float3 normalVS, float2 screenUV, float d, float rimWidth)
{
    float normalExtendLeftOffset = normalVS.x > 0 ? 1.0 : -1.0;
    normalExtendLeftOffset *= rimWidth * 0.0044;

    float eyeDepth = LinearEyeDepth(d, _ZBufferParams);

    float2 extendUV = screenUV;
    extendUV.x += normalExtendLeftOffset / (eyeDepth + 3.0);

    float extendedRawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_LinearClamp, extendUV, 0).x;
    float extendedEyeDepth = LinearEyeDepth(extendedRawDepth, _ZBufferParams);

    float depthOffset = extendedEyeDepth - eyeDepth;
    float rimArea = saturate(depthOffset * 4);

    return rimArea;
}

// 点光源/聚光灯 Rim Light 区域检测
float GetCharacterPunctualRimLightArea(float3 lightDirVS, float2 screenUV, float d, float rimWidth)
{
    float2 normalExtendDirVS = normalize(lightDirVS.xy);
    normalExtendDirVS *= rimWidth * 0.0044;

    float eyeDepth = LinearEyeDepth(d, _ZBufferParams);

    float2 extendUV = screenUV;
    extendUV.xy += normalExtendDirVS.xy / (eyeDepth + 3.0);

    float extendedRawDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_LinearClamp, extendUV, 0).x;
    float extendedEyeDepth = LinearEyeDepth(extendedRawDepth, _ZBufferParams);

    float depthOffset = extendedEyeDepth - eyeDepth;
    float rimArea = saturate(depthOffset * 1);

    return rimArea;
}

// 计算 Rim 颜色
float3 GetRimColor(float rimArea, float3 albedo, float3 normalVS, float3 lightDirVS, float shadow, float3 frontColor, float3 backColor)
{
    float NdotLVS = dot(normalVS, lightDirVS);

    float frontRim = max(NdotLVS, 0);
    float backRim = max(-NdotLVS, 0);

    float3 frontRimColor = frontRim * frontColor;
    float3 backRimColor = backRim * backColor;
    float3 albedoRimColor = saturate(albedo + 0.3);

    float3 rimColor = (frontRimColor + backRimColor) * albedoRimColor * saturate(shadow + 0.2);
    return rimColor * rimArea;
}

#endif // UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED

// ============================================================================
// ShadowRamp 采样
// ============================================================================

float4 SampleDirectShadowRamp(TEXTURE2D_PARAM(RampTex, RampSampler), float lightRange, float rampY)
{
    return SAMPLE_TEXTURE2D(RampTex, RampSampler, float2(lightRange, rampY));
}

float4 SampleDirectSpecularRamp(TEXTURE2D_PARAM(RampTex, RampSampler), float specRange, float rampY)
{
    return SAMPLE_TEXTURE2D(RampTex, RampSampler, float2(specRange, rampY));
}

// ============================================================================
// 间接光照评估 (适配 URP)
// 需要 URP Lighting.hlsl，仅在 Forward Pass 中可用
// ============================================================================
#ifdef UNIVERSAL_LIGHTING_INCLUDED

// 间接漫反射：基于 SH 球谐函数
void EvaluateToonIndirectDiffuse(inout ToonIndirectLighting lighting, float3 diffuseColor, float3 normalWS, float upDirScale, float4 selfEnvColor, float envColorLerp)
{
    float3 SHNormal = lerp(normalWS, float3(0,1,0), upDirScale);
    // 使用 URP 的 SH 采样
    float3 SHColor = SampleSH(SHNormal);
    SHColor = lerp(SHColor, selfEnvColor.rgb, envColorLerp);
    lighting.diffuse += SHColor * diffuseColor;
}

// 间接镜面反射：从 Cubemap 采样
void EvaluateToonIndirectSpecular_Cubemap(inout ToonIndirectLighting lighting, TEXTURECUBE_PARAM(textureName, samplerName), float3 reflectDirWS, float perceptualRoughness, float3 specularFGD, float weight)
{
    float mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
    float3 cubeReflection = SAMPLE_TEXTURECUBE_LOD(textureName, samplerName, reflectDirWS, mip).xyz;
    lighting.specular += specularFGD * cubeReflection * weight;
}

// 间接镜面反射：从天空盒 (ReflectionProbe/SkyBox) 采样
void EvaluateToonIndirectSpecular_Sky(inout ToonIndirectLighting lighting, float3 reflectDirWS, float perceptualRoughness, float3 specularFGD, float weight)
{
    // URP 使用 GlossyEnvironmentReflection 获取环境反射
    float3 skyReflection = GlossyEnvironmentReflection(reflectDirWS, perceptualRoughness, 1.0);
    lighting.specular += specularFGD * skyReflection * weight;
}

// 后处理：合并直接光与间接光，应用 AO 和能量补偿
float3 ToonPostEvaluate(in ToonDirectLighting dirLighting, in ToonIndirectLighting indirLighting, float occlusion, float3 fresnel0, float energyCompensation, float indirDiffInten, float indirSpecInten)
{
    // 对间接光应用 AO
    float3 indirDiff = indirLighting.diffuse * occlusion;
    float3 indirSpec = indirLighting.specular * occlusion;

    return dirLighting.diffuse + indirDiff * indirDiffInten
        + (dirLighting.specular + indirSpec * indirSpecInten) * (1.0 + fresnel0 * energyCompensation);
}

#endif // UNIVERSAL_LIGHTING_INCLUDED

// ============================================================================
// 简化版 PreIntegrated FGD// 使用 Schlick 近似代替预积分 FGD 查表
// ============================================================================

void GetApproxPreIntegratedFGD(float NdotV, float perceptualRoughness, float3 fresnel0, out float3 specularFGD, out float diffuseFGD, out float reflectivity)
{
    // 使用 URP EnvironmentBRDF 的近似方式
    // Schlick 近似: F = F0 + (1-F0) * (1 - NdotV)^5
    float x = 1.0 - NdotV;
    float x2 = x * x;
    float x5 = x2 * x2 * x;

    // 基于粗糙度调整
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

    // 简化 GGX FGD 近似
    // 参考: Brian Karis, "Real Shading in Unreal Engine 4"
    float2 AB = float2(-1.04, 1.04) * float2(roughness, roughness) + float2(1.0, -0.5) * float2(1.0 - roughness, 1.0 - roughness);
    // 更精确的近似
    float bias = AB.y * x5 + AB.x * x;
    float scale = 1.0 - bias;

    specularFGD = fresnel0 * scale + bias;
    reflectivity = scale;

    // Disney Diffuse 近似 (简化为 1.0)
    diffuseFGD = 1.0;
}

// ============================================================================
// Outline 描边公共函数 (原神风格描边算法)
// ============================================================================

// 视距 Remap：将 EyeDepth 在旧范围内线性映射到新范围
// 魔改 Remap: 与标准 Remap 区别是限定分母最小值不为0 (max(..., 0.001))
float OutlineEyeDepthRemap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + saturate((In - InMinMax.x) / max(InMinMax.y - InMinMax.x, 0.001)) * (OutMinMax.y - OutMinMax.x);
}

// 原神风格描边顶点着色器
// positionOS:      对象空间顶点位置
// smoothNormalOS:  对象空间平滑法线 (从 UV3/TEXCOORD3.xy 解码，z 通过勾股定理重建)
//                  UV 通道分配: UV2=BentNormal, UV3=平滑法线
// vertexColor:     顶点色 (A通道 = 顶点描边宽度缩放，默认0.5)
// 描边参数由 CBUFFER 中的 uniform 变量提供
float4 ToonOutlineVertex(
    float4 positionOS,
    float3 smoothNormalOS,
    float4 vertexColor,
    // 以下为材质参数
    float  outlineWidth,             // _OutlineWidth: 全局描边宽度缩放
    float3 eyeDepthRemapOldRanges,   // _OutlineDepthOldRange: 默认 (0.01, 2.0, 6.0)  x:最细距离 y:近远分界 z:最粗距离
    float3 eyeDepthRemapNewRanges,   // _OutlineDepthNewRange: 默认 (0.105, 0.245, 0.60) x:近处最细宽度 y:宽度 z:远处宽度
    float  normalizeViewNormalXYScale // _OutlineNormalScale: 法线XY缩放 默认 1.0
)
{
    // ====== 视图空间位置 ======
    // 这里算的是世界空间的米，1米 = Transform的1
    float3 positionVS = TransformWorldToView(TransformObjectToWorld(positionOS.xyz));

    // ====== 法线变换到视图空间 ======
    float3 normalWS = mul((float3x3)unity_ObjectToWorld, smoothNormalOS);
    float3 normalVS = mul((float3x3)unity_MatrixV, normalWS);

    // 描边宽度方向：压扁Z后归一化取XY
    // 这边算宽度相当于变化率也一起跟着算了，最后算宽度是不计算变化率的
    float2 normalVSxy = normalize(float3(normalVS.xy, 0.01)).xy * normalizeViewNormalXYScale;

    // ====== FOV 自适应 ======
    // 透视投影矩阵 unity_CameraProjection._m11 = cot(FOV/2)
    // cot(0.5*45°) = 2.414，即FOV=45°时 fov45AdaptScale=1，可看做缩放系数
    // FOV 越小，cot(0.5*FOV) 越大，2.414 / _m11 越小
    // FOV 越小，人物越大，描边在3D空间上变小，最终屏幕上粗度相应保持不变
    float fov45AdaptScale = 2.414 / unity_CameraProjection._m11;

    // ====== 视距分段 Remap ======
    // _Property_EyeDepthRemapOldRanges: x:最细距离 y:近远分界距离 z:最粗距离
    // _Property_EyeDepthRemapNewRanges: x:近处最细宽度 y:分界处宽度 z:远处宽度
    // positionVS.z 用负数是因为摄像机朝向-z轴向
    float fovFactor = -positionVS.z * fov45AdaptScale;
    bool isNear = fovFactor < eyeDepthRemapOldRanges.y;
    float2 widthOldRange = isNear ? eyeDepthRemapOldRanges.xy : eyeDepthRemapOldRanges.yz;
    float2 widthNewRange = isNear ? eyeDepthRemapNewRanges.xy : eyeDepthRemapNewRanges.yz;
    float eyeDepthInNewRange = OutlineEyeDepthRemap(fovFactor, widthOldRange, widthNewRange);

    // ====== 描边缩放因子 ======
    // 原神逐顶点宽度控制放在UV中(uv2.y默认0.5)，我们改用顶点色A通道替代
    float depthScaleW = vertexColor.a;
    float depthScale = eyeDepthInNewRange * 0.02 * depthScaleW;

    // 应用全局宽度缩放
    depthScale *= outlineWidth;

    // ====== 法线方向偏移 + 投影 ======
    float3 biasedPos = positionVS;
    biasedPos.xy += normalVSxy * depthScale;
    float4 positionCS = mul(UNITY_MATRIX_P, float4(biasedPos, 1.0));

    return positionCS;
}

#endif // PBR_TOON_COMMON_INCLUDED
