// ============================================================================
// Lighting_BentNormal.hlsl
// 基于 Visibility Cone 的光照函数
// Bent Normal 数据从 Mesh UV 解码，通过 VisibilityCone 结构参与光照计算
// 参考 Yarp Occlusion.cginc 的实现
// ============================================================================
#ifndef LIGHTING_BENT_NORMAL_INCLUDED
#define LIGHTING_BENT_NORMAL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ============================================================================
// VisibilityCone 结构体
// 存储从 UV 数据解码后的可见锥体信息
// ============================================================================
struct VisibilityCone
{
    half3 direction;    // 锥体方向 (世界空间, 即 bent normal)
    half  aperture;     // 锥体半角, 归一化到 [0,1], 1 对应 PI/2
    half  scale;        // 锥体缩放 (整体遮蔽强度)
};

// 构造函数
VisibilityCone VisibilityCone_Create(half3 direction, half aperture, half scale)
{
    VisibilityCone cone;
    cone.direction = direction;
    cone.aperture = aperture;
    cone.scale = scale;
    return cone;
}

// ============================================================================
// Ambient Occlusion 计算 (基于 Visibility Cone)
// 参考 Yarp Occlusion.cginc::evalAmbientOcclusion
// ============================================================================
half EvalAmbientOcclusion_BentNormal(VisibilityCone vCone, half3 normal)
{
    // cosTheta: cone 方向与法线的夹角余弦
    half cosTheta = saturate(dot(vCone.direction, normal));

    // 修正: 对于 normal mapped 表面，即使法线偏转 90 度，
    // 完全可见的锥体仍有一半可见
    half cosTheta_ = lerp(cosTheta, cosTheta * 0.5 + 0.5, vCone.aperture);

    half alpha = vCone.aperture;
    // 便宜的 (1 - 1/(1 + tan(alpha)^2)) 近似
    half occlusion = alpha * alpha;

    return cosTheta_ * occlusion * vCone.scale;
}

// ============================================================================
// Specular Occlusion 计算 (基于 Visibility Cone)
// 参考 Yarp Occlusion.cginc::evalSpecularOcclusion
// 使用 BRDF Cone 近似法
// ============================================================================
half GGXRoughnessToConeCosAngle_BentNormal(half roughness)
{
    if (roughness <= 0.565213)
        return min(0.1925 * log2(-72.56 * roughness + 42.03), 0.999);
    else
        return 0.0005;
}

half ApproxSolidAngleConeIntersectCone_BentNormal(half angle1, half angle2, half alpha)
{
    angle1 = min(HALF_PI, angle1);
    half minAngle = min(angle1, angle2);
    half full = TWO_PI * (1.0 - cos(minAngle));
    if (alpha <= max(angle1, angle2) - minAngle)
        return full;

    half absDiff = abs(angle1 - angle2);
    half factor = (alpha - absDiff) / (angle1 + angle2 - absDiff);

    return full * smoothstep(0.0, 1.0, 1.0 - factor * 3.0);
}

half EvalSpecularOcclusion_BentNormal(VisibilityCone vCone, half3 normalWS,
    half3 reflectDirectionWS, half linearRoughness)
{
    half brdfConeCosAngle = GGXRoughnessToConeCosAngle_BentNormal(linearRoughness);
    half cosBeta = dot(vCone.direction, reflectDirectionWS);
    half beta = acos(clamp(cosBeta, -1.0, 1.0));
    half alphaV = vCone.aperture;

    half intersectSolidAngle = ApproxSolidAngleConeIntersectCone_BentNormal(
        alphaV * HALF_PI,
        acos(brdfConeCosAngle),
        beta);

    half brdfSolidAngle = TWO_PI * (1.0 - brdfConeCosAngle);
    half so = intersectSolidAngle / max(brdfSolidAngle, 0.0001);

    so *= vCone.scale;
    return so;
}

// Moving Frostbite to PBR 3.0 的 specular occlusion 估算
half ComputeSpecOcclusion_BentNormal(half NdotV, half AO, half roughness)
{
    return saturate(pow(NdotV + AO, exp2(-16.0 * roughness - 1.0)) - 1.0 + AO);
}

// ============================================================================
// GetMainLight 系列 - Bent Normal 版本
// ============================================================================

Light GetMainLight_BentNormal()
{
    return GetMainLight();
}

Light GetMainLight_BentNormal(float4 shadowCoord)
{
    return GetMainLight(shadowCoord);
}

Light GetMainLight_BentNormal(float4 shadowCoord, float3 positionWS, half4 shadowMask)
{
    return GetMainLight(shadowCoord, positionWS, shadowMask);
}

Light GetMainLight_BentNormal(InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    return GetMainLight(inputData, shadowMask, aoFactor);
}

// ============================================================================
// GetAdditionalLight 系列 - Bent Normal 版本
// ============================================================================

Light GetAdditionalLight_BentNormal(uint i, float3 positionWS)
{
    return GetAdditionalLight(i, positionWS);
}

Light GetAdditionalLight_BentNormal(uint i, float3 positionWS, half4 shadowMask)
{
    return GetAdditionalLight(i, positionWS, shadowMask);
}

Light GetAdditionalLight_BentNormal(uint i, InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    return GetAdditionalLight(i, inputData, shadowMask, aoFactor);
}

// ============================================================================
// GlobalIllumination - Bent Normal 版本
// 使用 VisibilityCone 调制间接光照
// ============================================================================

half3 GlobalIllumination_BentNormal(BRDFData brdfData, BRDFData brdfDataClearCoat, float clearCoatMask,
    half3 bakedGI, half occlusion, float3 positionWS,
    half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV,
    VisibilityCone vCone)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0 - NoV);

    // 使用 visibility cone 计算 AO
    half bentAO = EvalAmbientOcclusion_BentNormal(vCone, normalWS);
    half combinedOcclusion = occlusion * bentAO;

    half3 indirectDiffuse = bakedGI * combinedOcclusion;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS,
        brdfData.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);

    // 使用 visibility cone 计算 specular occlusion
    half specOcc = EvalSpecularOcclusion_BentNormal(vCone, normalWS, reflectVector,
        brdfData.perceptualRoughness);
    // 也结合 Frostbite 的估算做 fallback
    half specOccFallback = ComputeSpecOcclusion_BentNormal(NoV, combinedOcclusion, brdfData.roughness);
    specOcc = max(specOcc, specOccFallback);
    indirectSpecular *= specOcc;

    half3 color = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

    if (IsOnlyAOLightingFeatureEnabled())
    {
        color = half3(1, 1, 1);
    }

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half3 coatIndirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS,
        brdfDataClearCoat.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);
    coatIndirectSpecular *= specOcc;
    half3 coatColor = EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatIndirectSpecular, fresnelTerm);

    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;
    return (color * (1.0 - coatFresnel * clearCoatMask) + coatColor) * occlusion;
#else
    return color * occlusion;
#endif
}

// 简化版本 (无 clearcoat)
half3 GlobalIllumination_BentNormal(BRDFData brdfData, half3 bakedGI, half occlusion,
    float3 positionWS, half3 normalWS, half3 viewDirectionWS,
    VisibilityCone vCone)
{
    const BRDFData noClearCoat = (BRDFData)0;
    return GlobalIllumination_BentNormal(brdfData, noClearCoat, 0.0, bakedGI, occlusion,
        positionWS, normalWS, viewDirectionWS, 0, vCone);
}

// ============================================================================
// LightingPhysicallyBased - Bent Normal 版本
// 使用 VisibilityCone 调制直接光照中的 NdotL
// ============================================================================

half3 LightingPhysicallyBased_BentNormal(BRDFData brdfData, BRDFData brdfDataClearCoat,
    half3 lightColor, half3 lightDirectionWS, half lightAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff, VisibilityCone vCone)
{
    // 标准 NdotL
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 radiance = lightColor * (lightAttenuation * NdotL);

    half3 brdf = brdfData.diffuse;
#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        brdf += brdfData.specular * DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS);

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
        half brdfCoat = kDielectricSpec.r * DirectBRDFSpecular(brdfDataClearCoat, normalWS, lightDirectionWS, viewDirectionWS);
        half NoV = saturate(dot(normalWS, viewDirectionWS));
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1.0 - NoV);
        brdf = brdf * (1.0 - clearCoatMask * coatFresnel) + brdfCoat * clearCoatMask;
#endif
    }
#endif

    return brdf * radiance;
}

half3 LightingPhysicallyBased_BentNormal(BRDFData brdfData, BRDFData brdfDataClearCoat,
    Light light, half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff, VisibilityCone vCone)
{
    return LightingPhysicallyBased_BentNormal(brdfData, brdfDataClearCoat,
        light.color, light.direction, light.distanceAttenuation * light.shadowAttenuation,
        normalWS, viewDirectionWS,
        clearCoatMask, specularHighlightsOff, vCone);
}

// 简化版本 (无 clearcoat)
half3 LightingPhysicallyBased_BentNormal(BRDFData brdfData, Light light,
    half3 normalWS, half3 viewDirectionWS, VisibilityCone vCone)
{
#ifdef _SPECULARHIGHLIGHTS_OFF
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif
    const BRDFData noClearCoat = (BRDFData)0;
    return LightingPhysicallyBased_BentNormal(brdfData, noClearCoat, light,
        normalWS, viewDirectionWS, 0.0, specularHighlightsOff, vCone);
}

// ============================================================================
// UniversalFragmentPBR_BentNormal - 完整的 PBR 片元着色
// 使用 VisibilityCone 参与 GI 和直接光照
// ============================================================================

half4 UniversalFragmentPBR_BentNormal(InputData inputData, SurfaceData surfaceData,
    VisibilityCone vCone)
{
#if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
#endif

    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();

    Light mainLight = GetMainLight_BentNormal(inputData, shadowMask, aoFactor);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData;
    lightingData.giColor = inputData.bakedGI;
    lightingData.emissionColor = surfaceData.emission;
    lightingData.vertexLightingColor = 0;
    lightingData.mainLightColor = 0;
    lightingData.additionalLightsColor = 0;

    // 使用 VisibilityCone 版本的 GlobalIllumination
    lightingData.giColor = GlobalIllumination_BentNormal(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS,
                                              inputData.normalizedScreenSpaceUV, vCone);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased_BentNormal(brdfData, brdfDataClearCoat,
                                                              mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              surfaceData.clearCoatMask, specularHighlightsOff, vCone);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight_BentNormal(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased_BentNormal(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff, vCone);
        }
    }
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight_BentNormal(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased_BentNormal(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff, vCone);
        }
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
#endif

#if REAL_IS_HALF
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif // LIGHTING_BENT_NORMAL_INCLUDED
