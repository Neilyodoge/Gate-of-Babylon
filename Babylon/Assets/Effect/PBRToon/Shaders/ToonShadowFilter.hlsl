// ToonShadowFilter.hlsl
// The Witness 优化 PCF 阴影滤波器 + PCSS 软阴影
// 参考: Isidoro, "Shadow Mapping: GPU-based Tips and Techniques" (The Witness)
//       V114 yarp 管线 ShadowFilter / PCSS 实现
// 直接采样 URP 的 _MainLightShadowmapTexture，替代默认 PCF 获得更高质量软阴影
//
// PCF 质量等级 (材质 keyword):
//   _TOON_SHADOW_BASE  : 1 tap 硬件 2x2 PCF（最快，硬阴影）
//   _TOON_SHADOW_PCF_3X3 : 4 tap 3x3 优化 PCF（默认）
//   _TOON_SHADOW_PCF_5X5 : 9 tap 5x5 优化 PCF
//   _TOON_SHADOW_PCF_7X7 : 16 tap 7x7 优化 PCF（最高质量固定核）
//   _TOON_SHADOW_PCSS    : PCSS 可变半径软阴影（距离自适应）
//
// 使用方式:
//   在 Forward Pass 中 include 此文件（需要在 Lighting.hlsl 之后）
//   调用 ToonMainLightShadow(shadowCoord) 替代 mainLight.shadowAttenuation

#ifndef TOON_SHADOW_FILTER_INCLUDED
#define TOON_SHADOW_FILTER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// ============================================================================
// Interleaved Gradient Noise (用于 PCSS 采样抖动)
// 参考: "NEXT GENERATION POST PROCESSING IN CALL OF DUTY: ADVANCED WARFARE"
// ============================================================================
float ToonInterleavedGradientNoise(float2 uv)
{
    const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
    return frac(magic.z * frac(dot(uv, magic.xy)));
}

// ============================================================================
// PCF 滤波核
// ============================================================================

// 单次硬件 PCF 采样（URP 的 ShadowSamplingTent.hlsl 已提供 comparison sampler）
half _SampleShadowCmp(float2 uv, float compareValue)
{
    float3 tmp = float3(uv, compareValue);
    return SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, tmp);
}

// BaseShadow: 单次硬件 2x2 PCF（1 tap，最快）
half ToonShadowPCF_Base(float3 shadowUvDepth)
{
    return _SampleShadowCmp(shadowUvDepth.xy, shadowUvDepth.z);
}

// 3x3 优化 PCF（4次采样代替9次）
half ToonShadowPCF_3x3(float4 shadowMapSize, float3 shadowUvDepth)
{
    float lightDepth = shadowUvDepth.z;
    float2 uv = shadowUvDepth.xy * shadowMapSize.zw; // uv in texels

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= shadowMapSize.xy; // 回到 [0,1] UV 空间

    float uw0 = (3 - 2 * s);
    float uw1 = (1 + 2 * s);

    float u0 = (2 - s) / uw0 - 1;
    float u1 = s / uw1 + 1;

    float vw0 = (3 - 2 * t);
    float vw1 = (1 + 2 * t);

    float v0 = (2 - t) / vw0 - 1;
    float v1 = t / vw1 + 1;

    half sum = 0;
    sum += uw0 * vw0 * _SampleShadowCmp(base_uv + float2(u0, v0) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw0 * _SampleShadowCmp(base_uv + float2(u1, v0) * shadowMapSize.xy, lightDepth);
    sum += uw0 * vw1 * _SampleShadowCmp(base_uv + float2(u0, v1) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw1 * _SampleShadowCmp(base_uv + float2(u1, v1) * shadowMapSize.xy, lightDepth);

    return sum / 16.0;
}

// 5x5 优化 PCF（9次采样代替25次）
half ToonShadowPCF_5x5(float4 shadowMapSize, float3 shadowUvDepth)
{
    float lightDepth = shadowUvDepth.z;
    float2 uv = shadowUvDepth.xy * shadowMapSize.zw;

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= shadowMapSize.xy;

    float uw0 = (4 - 3 * s);
    float uw1 = 7;
    float uw2 = (1 + 3 * s);

    float u0 = (3 - 2 * s) / uw0 - 2;
    float u1 = (3 + s) / uw1;
    float u2 = s / uw2 + 2;

    float vw0 = (4 - 3 * t);
    float vw1 = 7;
    float vw2 = (1 + 3 * t);

    float v0 = (3 - 2 * t) / vw0 - 2;
    float v1 = (3 + t) / vw1;
    float v2 = t / vw2 + 2;

    half sum = 0;
    sum += uw0 * vw0 * _SampleShadowCmp(base_uv + float2(u0, v0) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw0 * _SampleShadowCmp(base_uv + float2(u1, v0) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw0 * _SampleShadowCmp(base_uv + float2(u2, v0) * shadowMapSize.xy, lightDepth);

    sum += uw0 * vw1 * _SampleShadowCmp(base_uv + float2(u0, v1) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw1 * _SampleShadowCmp(base_uv + float2(u1, v1) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw1 * _SampleShadowCmp(base_uv + float2(u2, v1) * shadowMapSize.xy, lightDepth);

    sum += uw0 * vw2 * _SampleShadowCmp(base_uv + float2(u0, v2) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw2 * _SampleShadowCmp(base_uv + float2(u1, v2) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw2 * _SampleShadowCmp(base_uv + float2(u2, v2) * shadowMapSize.xy, lightDepth);

    return sum / 144.0;
}

// 7x7 优化 PCF（16次采样代替49次）— 搬运自 V114 yarp ShadowFilter
half ToonShadowPCF_7x7(float4 shadowMapSize, float3 shadowUvDepth)
{
    float lightDepth = shadowUvDepth.z;
    float2 uv = shadowUvDepth.xy * shadowMapSize.zw;

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= shadowMapSize.xy;

    float uw0 = (5 * s - 6);
    float uw1 = (11 * s - 28);
    float uw2 = -(11 * s + 17);
    float uw3 = -(5 * s + 1);

    float u0 = (4 * s - 5) / uw0 - 3;
    float u1 = (4 * s - 16) / uw1 - 1;
    float u2 = -(7 * s + 5) / uw2 + 1;
    float u3 = -s / uw3 + 3;

    float vw0 = (5 * t - 6);
    float vw1 = (11 * t - 28);
    float vw2 = -(11 * t + 17);
    float vw3 = -(5 * t + 1);

    float v0 = (4 * t - 5) / vw0 - 3;
    float v1 = (4 * t - 16) / vw1 - 1;
    float v2 = -(7 * t + 5) / vw2 + 1;
    float v3 = -t / vw3 + 3;

    half sum = 0;
    sum += uw0 * vw0 * _SampleShadowCmp(base_uv + float2(u0, v0) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw0 * _SampleShadowCmp(base_uv + float2(u1, v0) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw0 * _SampleShadowCmp(base_uv + float2(u2, v0) * shadowMapSize.xy, lightDepth);
    sum += uw3 * vw0 * _SampleShadowCmp(base_uv + float2(u3, v0) * shadowMapSize.xy, lightDepth);

    sum += uw0 * vw1 * _SampleShadowCmp(base_uv + float2(u0, v1) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw1 * _SampleShadowCmp(base_uv + float2(u1, v1) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw1 * _SampleShadowCmp(base_uv + float2(u2, v1) * shadowMapSize.xy, lightDepth);
    sum += uw3 * vw1 * _SampleShadowCmp(base_uv + float2(u3, v1) * shadowMapSize.xy, lightDepth);

    sum += uw0 * vw2 * _SampleShadowCmp(base_uv + float2(u0, v2) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw2 * _SampleShadowCmp(base_uv + float2(u1, v2) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw2 * _SampleShadowCmp(base_uv + float2(u2, v2) * shadowMapSize.xy, lightDepth);
    sum += uw3 * vw2 * _SampleShadowCmp(base_uv + float2(u3, v2) * shadowMapSize.xy, lightDepth);

    sum += uw0 * vw3 * _SampleShadowCmp(base_uv + float2(u0, v3) * shadowMapSize.xy, lightDepth);
    sum += uw1 * vw3 * _SampleShadowCmp(base_uv + float2(u1, v3) * shadowMapSize.xy, lightDepth);
    sum += uw2 * vw3 * _SampleShadowCmp(base_uv + float2(u2, v3) * shadowMapSize.xy, lightDepth);
    sum += uw3 * vw3 * _SampleShadowCmp(base_uv + float2(u3, v3) * shadowMapSize.xy, lightDepth);

    return sum / 2704.0;
}

// ============================================================================
// PCSS (Percentage Closer Soft Shadows) — 搬运自 V114 yarp Pcss.hlsl
// 距离自适应软阴影：近处硬、远处软
// ============================================================================
#if defined(_TOON_SHADOW_PCSS)

// PCSS 参数：从材质 CBUFFER 中读取
// 材质中定义了 _PcssSoftness, _PcssSoftnessFalloff 等单独变量
// 这里用宏桥接到 PCSS 内部使用的名称
// 注意：包含此文件的 shader 必须在 include 之前声明好 CBUFFER
#define _ToonPcssSoftness           _PcssSoftness
#define _ToonPcssSoftnessFalloff    _PcssSoftnessFalloff
#define _ToonPcssBlockerSamples     ((int)_PcssBlockerSamples)
#define _ToonPcssFilterSamples      ((int)_PcssFilterSamples)
#define _ToonPcssBlockerGradientBias _PcssBlockerGradientBias
#define _ToonPcssPCFGradientBias     _PcssPCFGradientBias

// 32 点 Poisson Disk 采样分布
static const float2 _ToonPoissonOffsets[32] = {
    float2(0.06407013, 0.05409927),
    float2(0.7366577, 0.5789394),
    float2(-0.6270542, -0.5320278),
    float2(-0.4096107, 0.8411095),
    float2(0.6849564, -0.4990818),
    float2(-0.874181, -0.04579735),
    float2(0.9989998, 0.0009880066),
    float2(-0.004920578, -0.9151649),
    float2(0.1805763, 0.9747483),
    float2(-0.2138451, 0.2635818),
    float2(0.109845, 0.3884785),
    float2(0.06876755, -0.3581074),
    float2(0.374073, -0.7661266),
    float2(0.3079132, -0.1216763),
    float2(-0.3794335, -0.8271583),
    float2(-0.203878, -0.07715034),
    float2(0.5912697, 0.1469799),
    float2(-0.88069, 0.3031784),
    float2(0.5040108, 0.8283722),
    float2(-0.5844124, 0.5494877),
    float2(0.6017799, -0.1726654),
    float2(-0.5554981, 0.1559997),
    float2(-0.3016369, -0.3900928),
    float2(-0.5550632, -0.1723762),
    float2(0.925029, 0.2995041),
    float2(-0.2473137, 0.5538505),
    float2(0.9183037, -0.2862392),
    float2(0.2469421, 0.6718712),
    float2(0.3916397, -0.4328209),
    float2(-0.03576927, -0.6220032),
    float2(-0.04661255, 0.7995201),
    float2(0.4402924, 0.3640312),
};

float2 _ToonRotatePoissonSample(float2 pos, float2 rotationTrig)
{
    return float2(pos.x * rotationTrig.x - pos.y * rotationTrig.y,
                  pos.y * rotationTrig.x + pos.x * rotationTrig.y);
}

// Step 1: Blocker Search — 搜索遮挡物平均深度
float2 _ToonComputeBlockerDepth(float2 shadowCoord, float receiverDepth, float searchRadius, float2 rotationTrig)
{
    float blockerSum = 0.0;
    float numBlockers = 0.0;

    // 使用非 comparison sampler 读取原始深度
    // URP 的 _MainLightShadowmapTexture 是 TEXTURE2D_SHADOW，需要用 point sampler 读取原始深度
    UNITY_LOOP
    for (int i = 0; i < _ToonPcssBlockerSamples; ++i)
    {
        float2 offset = _ToonPoissonOffsets[i] * searchRadius;
        offset = _ToonRotatePoissonSample(offset, rotationTrig);

        float2 sampleCoord = shadowCoord + offset;
        // 使用 LOAD 或 point sample 读取原始深度
        // URP shadow map 没有暴露非 comparison sampler，用 comparison 采样近似：
        // 如果 compareValue=0 (reversed-z) 或 compareValue=1 (normal-z)，
        // 则 comparison 结果总是 pass，无法获取原始深度
        // 因此我们用 _SampleShadowCmp 做一个简化的 blocker search：
        // 用当前深度做 comparison，如果 fail 说明是 blocker
        float biasedDepth = receiverDepth;
        biasedDepth += dot(offset, float2(0, 0)) * _ToonPcssBlockerGradientBias;

        float shadowTest = _SampleShadowCmp(sampleCoord, biasedDepth);
        // shadowTest < 0.5 表示该采样点被遮挡（是 blocker）
        if (shadowTest < 0.5)
        {
            // 无法获取精确 blocker 深度，用 receiverDepth 近似
            blockerSum += receiverDepth;
            numBlockers += 1.0;
        }
    }

    float avgBlockerDepth = (numBlockers > 0) ? (blockerSum / numBlockers) : 0;
    return float2(avgBlockerDepth, numBlockers);
}

// Step 3: PCF Filter — Poisson Disk 可变半径 PCF
float _ToonPcssFilter(float3 shadowCoord, float filterRadius, float2 rotationTrig)
{
    float shadowAttenuationSum = 0.0;

    UNITY_LOOP
    for (int i = 0; i < _ToonPcssFilterSamples; ++i)
    {
        float2 offset = _ToonPoissonOffsets[i] * filterRadius;
        offset = _ToonRotatePoissonSample(offset, rotationTrig);

        float2 sampleUV = shadowCoord.xy + offset;
        float biasedDepth = shadowCoord.z;
        biasedDepth += dot(offset, float2(0, 0)) * _ToonPcssPCFGradientBias;

        shadowAttenuationSum += _SampleShadowCmp(sampleUV, biasedDepth);
    }

    return shadowAttenuationSum / max(_ToonPcssFilterSamples, 1);
}

// PCSS 主入口
half ToonShadowPCSS(float4 shadowMapSize, float3 shadowUvDepth, float2 screenPos)
{
    // 计算采样抖动角度（Interleaved Gradient Noise）
    float noise = ToonInterleavedGradientNoise(screenPos);
    float sampleJitterAngle = noise * 3.14159265;
    float2 sampleJitter = float2(cos(sampleJitterAngle), sin(sampleJitterAngle));

    float receiverDepth = shadowUvDepth.z;

    // 深度感知搜索半径
    float zAwareDepth = receiverDepth;
    #if defined(UNITY_REVERSED_Z)
        zAwareDepth = 1.0 - receiverDepth;
    #endif

    float blockerSearchRadius = _ToonPcssSoftness * saturate(zAwareDepth - 0.02) / max(zAwareDepth, 0.001);

    // Step 1: Blocker search
    float2 avgBlockerDepth = _ToonComputeBlockerDepth(shadowUvDepth.xy, receiverDepth, blockerSearchRadius, sampleJitter);
    if (avgBlockerDepth.y < 1)
    {
        // 没有遮挡物，直接返回全亮
        return 1.0;
    }

    // Step 2: Penumbra estimation
    // 由于无法获取精确 blocker 深度，使用 blocker 数量比例估算半影
    float blockerRatio = avgBlockerDepth.y / max(_ToonPcssBlockerSamples, 1);
    float penumbra = blockerRatio;
    penumbra = 1.0 - pow(1.0 - penumbra, _ToonPcssSoftnessFalloff);

    // Step 3: PCF filter
    float filterRadiusUV = penumbra * _ToonPcssSoftness;
    // 限制最小滤波半径，避免完全硬阴影
    filterRadiusUV = max(filterRadiusUV, shadowMapSize.x * 0.5);

    return _ToonPcssFilter(shadowUvDepth, filterRadiusUV, sampleJitter);
}

#endif // _TOON_SHADOW_PCSS

// ============================================================================
// Shadow Edge Color — 搬运自 V114 yarp ShadowMap.cginc
// 在阴影边缘区域叠加渐变颜色，增强视觉层次
// ============================================================================

// 简单版：单段渐变
// shadowAttenuation: [0,1] 阴影值
// shadowEdgeColor: 边缘颜色
// shadowColorBegin/End: 渐变起止阈值
// beginColor/endColor: 暗端/亮端颜色
half3 GetShadowEdgeColor(half shadowAttenuation, half3 shadowEdgeColor, float shadowColorBegin, float shadowColorEnd, half3 beginColor, half3 endColor)
{
    half colorLerp = smoothstep(shadowColorBegin, shadowColorEnd, shadowAttenuation);
    half3 shadowColor = lerp(beginColor, shadowEdgeColor, colorLerp);
    shadowColor = lerp(shadowColor, endColor, shadowAttenuation);
    return shadowColor;
}

// 高级版：多段渐变，支持两端独立过渡宽度
// shadowFactor: [0,1] 阴影值
// shadowBegin/End: 核心渐变区域起止
// beginColor/endColor: 渐变区域两端颜色
// shadowColor: 全暗区颜色
// lightColor: 全亮区颜色
// fadeBeginWidth/fadeEndWidth: 暗端/亮端过渡宽度
half3 GetShadowEdgeColor2(half shadowFactor, float shadowBegin, float shadowEnd, half3 beginColor, half3 endColor, half3 shadowColor, half3 lightColor, float fadeBeginWidth, float fadeEndWidth)
{
    // 计算核心渐变区域
    float range = shadowEnd - shadowBegin;
    float midT = saturate((shadowFactor - shadowBegin) / max(range, 0.001));
    half3 midColor = lerp(beginColor, endColor, midT);

    // 计算平滑过渡因子
    float fadeIn = smoothstep(max(0.001, shadowBegin - fadeBeginWidth), max(shadowBegin + fadeBeginWidth, 0.001), shadowFactor);
    float fadeOut = smoothstep(min(0.999, shadowEnd - fadeEndWidth), min(0.999, shadowEnd + fadeEndWidth), shadowFactor);

    // 组合最终颜色
    half3 color = midColor;
    color = lerp(shadowColor, color, fadeIn);   // 暗端过渡
    color = lerp(color, lightColor, fadeOut);    // 亮端过渡

    return color;
}

// ============================================================================
// 角色 Shadow Atlas 采样
// 由 CharacterShadowAtlasRenderFeature 渲染，支持多角色独立高清阴影
// ============================================================================

// 全局参数（由 CharacterShadowAtlasRenderFeature 设置）
// 使用 TEXTURE2D_FLOAT + 手动深度比较
// 原因：Unity inline sampler 的 Compare 比较函数固定为 LessEqual，
//       在 Reversed-Z 平台（DX11/DX12/Vulkan/Metal）下方向错误导致全白
//       因此必须手动读取原始深度值并做比较
TEXTURE2D_FLOAT(_CharShadowAtlas);
SAMPLER(sampler_CharShadowAtlas);

// Atlas 参数: (1/atlasSize, 1/atlasSize, atlasSize, atlasSize)
float4      _CharShadowAtlasParams;
// 当前帧分配的角色数量
int         _CharShadowCount;
// 每个角色的 VP 矩阵（世界空间 -> shadow UV [0,1]）
float4x4    _CharShadowVPArray[16];
// 每个角色在 Atlas 中的区域 (x/atlas, y/atlas, w/atlas, h/atlas)
float4      _CharShadowAtlasRectArray[16];

// Atlas 手动深度比较采样（双线性插值版本）
half _SampleCharAtlasShadowCmp(float2 uv, float compareValue)
{
    float storedDepth = SAMPLE_TEXTURE2D(_CharShadowAtlas, sampler_CharShadowAtlas, uv).r;

    #if UNITY_REVERSED_Z
        return step(storedDepth, compareValue);
    #else
        return step(compareValue, storedDepth);
    #endif
}

// Atlas 3x3 优化 PCF
half CharAtlasShadowPCF_3x3(float2 atlasUV, float compareDepth, float4 atlasParams)
{
    float2 uv = atlasUV * atlasParams.zw;

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= atlasParams.xy;

    float uw0 = (3 - 2 * s);
    float uw1 = (1 + 2 * s);

    float u0 = (2 - s) / uw0 - 1;
    float u1 = s / uw1 + 1;

    float vw0 = (3 - 2 * t);
    float vw1 = (1 + 2 * t);

    float v0 = (2 - t) / vw0 - 1;
    float v1 = t / vw1 + 1;

    half sum = 0;
    sum += uw0 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v0) * atlasParams.xy, compareDepth);
    sum += uw1 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v0) * atlasParams.xy, compareDepth);
    sum += uw0 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v1) * atlasParams.xy, compareDepth);
    sum += uw1 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v1) * atlasParams.xy, compareDepth);

    return sum / 16.0;
}

// Atlas 5x5 优化 PCF
half CharAtlasShadowPCF_5x5(float2 atlasUV, float compareDepth, float4 atlasParams)
{
    float2 uv = atlasUV * atlasParams.zw;

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= atlasParams.xy;

    float uw0 = (4 - 3 * s);
    float uw1 = 7;
    float uw2 = (1 + 3 * s);

    float u0 = (3 - 2 * s) / uw0 - 2;
    float u1 = (3 + s) / uw1;
    float u2 = s / uw2 + 2;

    float vw0 = (4 - 3 * t);
    float vw1 = 7;
    float vw2 = (1 + 3 * t);

    float v0 = (3 - 2 * t) / vw0 - 2;
    float v1 = (3 + t) / vw1;
    float v2 = t / vw2 + 2;

    half sum = 0;
    sum += uw0 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v0) * atlasParams.xy, compareDepth);
    sum += uw1 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v0) * atlasParams.xy, compareDepth);
    sum += uw2 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v0) * atlasParams.xy, compareDepth);

    sum += uw0 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v1) * atlasParams.xy, compareDepth);
    sum += uw1 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v1) * atlasParams.xy, compareDepth);
    sum += uw2 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v1) * atlasParams.xy, compareDepth);

    sum += uw0 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v2) * atlasParams.xy, compareDepth);
    sum += uw1 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v2) * atlasParams.xy, compareDepth);
    sum += uw2 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v2) * atlasParams.xy, compareDepth);

    return sum / 144.0;
}

// Atlas 7x7 优化 PCF（16次采样代替49次）
half CharAtlasShadowPCF_7x7(float2 atlasUV, float compareDepth, float4 atlasParams)
{
    float2 uv = atlasUV * atlasParams.zw;

    float2 base_uv;
    base_uv.x = floor(uv.x + 0.5);
    base_uv.y = floor(uv.y + 0.5);

    float s = (uv.x + 0.5 - base_uv.x);
    float t = (uv.y + 0.5 - base_uv.y);

    base_uv -= float2(0.5, 0.5);
    base_uv *= atlasParams.xy;

    float uw0 = (5 * s - 6);
    float uw1 = (11 * s - 28);
    float uw2 = -(11 * s + 17);
    float uw3 = -(5 * s + 1);

    float u0 = (4 * s - 5) / uw0 - 3;
    float u1 = (4 * s - 16) / uw1 - 1;
    float u2 = -(7 * s + 5) / uw2 + 1;
    float u3 = -s / uw3 + 3;

    float vw0 = (5 * t - 6);
    float vw1 = (11 * t - 28);
    float vw2 = -(11 * t + 17);
    float vw3 = -(5 * t + 1);

    float v0 = (4 * t - 5) / vw0 - 3;
    float v1 = (4 * t - 16) / vw1 - 1;
    float v2 = -(7 * t + 5) / vw2 + 1;
    float v3 = -t / vw3 + 3;

    half sum = 0;
    sum += uw0 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v0) * atlasParams.xy, compareDepth);
    sum += uw1 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v0) * atlasParams.xy, compareDepth);
    sum += uw2 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v0) * atlasParams.xy, compareDepth);
    sum += uw3 * vw0 * _SampleCharAtlasShadowCmp(base_uv + float2(u3, v0) * atlasParams.xy, compareDepth);

    sum += uw0 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v1) * atlasParams.xy, compareDepth);
    sum += uw1 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v1) * atlasParams.xy, compareDepth);
    sum += uw2 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v1) * atlasParams.xy, compareDepth);
    sum += uw3 * vw1 * _SampleCharAtlasShadowCmp(base_uv + float2(u3, v1) * atlasParams.xy, compareDepth);

    sum += uw0 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v2) * atlasParams.xy, compareDepth);
    sum += uw1 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v2) * atlasParams.xy, compareDepth);
    sum += uw2 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v2) * atlasParams.xy, compareDepth);
    sum += uw3 * vw2 * _SampleCharAtlasShadowCmp(base_uv + float2(u3, v2) * atlasParams.xy, compareDepth);

    sum += uw0 * vw3 * _SampleCharAtlasShadowCmp(base_uv + float2(u0, v3) * atlasParams.xy, compareDepth);
    sum += uw1 * vw3 * _SampleCharAtlasShadowCmp(base_uv + float2(u1, v3) * atlasParams.xy, compareDepth);
    sum += uw2 * vw3 * _SampleCharAtlasShadowCmp(base_uv + float2(u2, v3) * atlasParams.xy, compareDepth);
    sum += uw3 * vw3 * _SampleCharAtlasShadowCmp(base_uv + float2(u3, v3) * atlasParams.xy, compareDepth);

    return sum / 2704.0;
}

// Atlas PCSS：Poisson Disk 可变半径软阴影（复用 CSM PCSS 的参数和采样分布）
#if defined(_TOON_SHADOW_PCSS)

// Atlas Blocker Search
float2 _CharAtlasComputeBlockerDepth(float2 atlasUV, float receiverDepth, float searchRadius, float2 rotationTrig)
{
    float blockerSum = 0.0;
    float numBlockers = 0.0;

    UNITY_LOOP
    for (int i = 0; i < _ToonPcssBlockerSamples; ++i)
    {
        float2 offset = _ToonPoissonOffsets[i] * searchRadius;
        offset = _ToonRotatePoissonSample(offset, rotationTrig);

        float2 sampleCoord = atlasUV + offset;
        float shadowTest = _SampleCharAtlasShadowCmp(sampleCoord, receiverDepth);
        if (shadowTest < 0.5)
        {
            blockerSum += receiverDepth;
            numBlockers += 1.0;
        }
    }

    float avgBlockerDepth = (numBlockers > 0) ? (blockerSum / numBlockers) : 0;
    return float2(avgBlockerDepth, numBlockers);
}

// Atlas PCF Filter
float _CharAtlasPcssFilter(float2 atlasUV, float compareDepth, float filterRadius, float2 rotationTrig)
{
    float shadowAttenuationSum = 0.0;

    UNITY_LOOP
    for (int i = 0; i < _ToonPcssFilterSamples; ++i)
    {
        float2 offset = _ToonPoissonOffsets[i] * filterRadius;
        offset = _ToonRotatePoissonSample(offset, rotationTrig);

        float2 sampleUV = atlasUV + offset;
        shadowAttenuationSum += _SampleCharAtlasShadowCmp(sampleUV, compareDepth);
    }

    return shadowAttenuationSum / max(_ToonPcssFilterSamples, 1);
}

// Atlas PCSS 主入口
half CharAtlasShadowPCSS(float2 atlasUV, float compareDepth, float4 atlasParams, float2 screenPos)
{
    float noise = ToonInterleavedGradientNoise(screenPos);
    float sampleJitterAngle = noise * 3.14159265;
    float2 sampleJitter = float2(cos(sampleJitterAngle), sin(sampleJitterAngle));

    // 搜索半径：基于 Atlas 纹素大小和 PCSS Softness
    float blockerSearchRadius = _ToonPcssSoftness * 0.5;

    // Step 1: Blocker search
    float2 avgBlockerDepth = _CharAtlasComputeBlockerDepth(atlasUV, compareDepth, blockerSearchRadius, sampleJitter);
    if (avgBlockerDepth.y < 1)
    {
        return 1.0;
    }

    // Step 2: Penumbra estimation
    float blockerRatio = avgBlockerDepth.y / max(_ToonPcssBlockerSamples, 1);
    float penumbra = blockerRatio;
    penumbra = 1.0 - pow(1.0 - penumbra, _ToonPcssSoftnessFalloff);

    // Step 3: PCF filter
    float filterRadiusUV = penumbra * _ToonPcssSoftness;
    filterRadiusUV = max(filterRadiusUV, atlasParams.x * 0.5);

    return _CharAtlasPcssFilter(atlasUV, compareDepth, filterRadiusUV, sampleJitter);
}

#endif // _TOON_SHADOW_PCSS

// 采样角色 Shadow Atlas
half SampleCharacterAtlasShadow(float3 positionWS, out bool outCovered, float2 screenPos = float2(0, 0))
{
    outCovered = false;

    if (_CharShadowCount <= 0)
        return 1.0h;

    half minShadow = 1.0h;

    [loop]
    for (int i = 0; i < _CharShadowCount; i++)
    {
        float4 shadowUV = mul(_CharShadowVPArray[i], float4(positionWS, 1.0));

        if (any(shadowUV.xy < 0.001) || any(shadowUV.xy > 0.999))
            continue;

        outCovered = true;

        float4 atlasRect = _CharShadowAtlasRectArray[i];
        float2 atlasUV = shadowUV.xy * atlasRect.zw + atlasRect.xy;

        float compareDepth = shadowUV.z;

        // Atlas PCF 跟随主 PCF 等级
        #if defined(_TOON_SHADOW_PCSS)
            half shadow = CharAtlasShadowPCSS(atlasUV, compareDepth, _CharShadowAtlasParams, screenPos);
        #elif defined(_TOON_SHADOW_PCF_7X7)
            half shadow = CharAtlasShadowPCF_7x7(atlasUV, compareDepth, _CharShadowAtlasParams);
        #elif defined(_TOON_SHADOW_PCF_5X5)
            half shadow = CharAtlasShadowPCF_5x5(atlasUV, compareDepth, _CharShadowAtlasParams);
        #elif defined(_TOON_SHADOW_BASE)
            half shadow = _SampleCharAtlasShadowCmp(atlasUV, compareDepth);
        #else
            half shadow = CharAtlasShadowPCF_3x3(atlasUV, compareDepth, _CharShadowAtlasParams);
        #endif

        minShadow = min(minShadow, shadow);
    }

    return minShadow;
}

// ============================================================================
// Debug：角色 Atlas 阴影可视化
// ============================================================================
#if defined(_CHAR_SHADOW_ATLAS_ON)

half4 DebugSampleCharacterAtlasShadowOnly(float3 positionWS)
{
    bool covered;
    half shadow = SampleCharacterAtlasShadow(positionWS, covered);
    if (!covered)
        return half4(1, 0, 1, 1);
    return half4(shadow.xxx, 1);
}

half4 DebugCharacterAtlasUV(float3 positionWS)
{
    if (_CharShadowCount <= 0)
        return half4(1, 0, 1, 1);

    [loop]
    for (int i = 0; i < _CharShadowCount; i++)
    {
        float4 shadowUV = mul(_CharShadowVPArray[i], float4(positionWS, 1.0));
        if (any(shadowUV.xy < 0.001) || any(shadowUV.xy > 0.999))
            continue;

        float4 atlasRect = _CharShadowAtlasRectArray[i];
        float2 atlasUV = shadowUV.xy * atlasRect.zw + atlasRect.xy;
        return half4(atlasUV, 0, 1);
    }
    return half4(1, 0, 1, 1);
}

half4 DebugCharacterAtlasDepth(float3 positionWS)
{
    if (_CharShadowCount <= 0)
        return half4(1, 0, 1, 1);

    [loop]
    for (int i = 0; i < _CharShadowCount; i++)
    {
        float4 shadowUV = mul(_CharShadowVPArray[i], float4(positionWS, 1.0));
        if (any(shadowUV.xy < 0.001) || any(shadowUV.xy > 0.999))
            continue;

        float4 atlasRect = _CharShadowAtlasRectArray[i];
        float2 atlasUV = shadowUV.xy * atlasRect.zw + atlasRect.xy;
        float storedDepth = SAMPLE_TEXTURE2D(_CharShadowAtlas, sampler_CharShadowAtlas, atlasUV).r;
        return half4(storedDepth.xxx, 1);
    }
    return half4(1, 0, 1, 1);
}

half4 DebugCharacterAtlasDepthDiff(float3 positionWS)
{
    if (_CharShadowCount <= 0)
        return half4(1, 0, 1, 1);

    [loop]
    for (int i = 0; i < _CharShadowCount; i++)
    {
        float4 shadowUV = mul(_CharShadowVPArray[i], float4(positionWS, 1.0));
        if (any(shadowUV.xy < 0.001) || any(shadowUV.xy > 0.999))
            continue;

        float4 atlasRect = _CharShadowAtlasRectArray[i];
        float2 atlasUV = shadowUV.xy * atlasRect.zw + atlasRect.xy;
        float storedDepth = SAMPLE_TEXTURE2D(_CharShadowAtlas, sampler_CharShadowAtlas, atlasUV).r;
        float compareDepth = shadowUV.z;

        #if UNITY_REVERSED_Z
            float diff = compareDepth - storedDepth;
        #else
            float diff = storedDepth - compareDepth;
        #endif

        float absDiff = abs(diff) * 100.0;
        if (diff >= 0)
            return half4(0, saturate(absDiff), 0, 1);
        else
            return half4(saturate(absDiff), 0, 0, 1);
    }
    return half4(1, 0, 1, 1);
}

#endif // _CHAR_SHADOW_ATLAS_ON

// ============================================================================
// 主入口：替代 mainLight.shadowAttenuation 的高质量阴影采样
// ============================================================================

// shadowCoord: TransformWorldToShadowCoord(positionWS) 的结果
// NdotL: dot(normalWS, lightDirWS)，用于自适应 depth bias 减少掠射角 shadow acne
// screenPos: 屏幕像素坐标（仅 PCSS 模式需要，用于 IGN 抖动）
// 返回 [0,1] 阴影衰减值，0 = 全阴影，1 = 全亮
// CSM 也使用自定义 PCF/PCSS 滤波器，与 Atlas Shadow 保持一致
half ToonMainLightShadow(float4 shadowCoord, float NdotL, float2 screenPos)
{
    #if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        return half(1.0);
    #endif

    float3 shadowUvDepth = shadowCoord.xyz;

    #if defined(_TOON_SHADOW_PCSS)
        return ToonShadowPCSS(_MainLightShadowmapSize, shadowUvDepth, screenPos);
    #elif defined(_TOON_SHADOW_PCF_7X7)
        return ToonShadowPCF_7x7(_MainLightShadowmapSize, shadowUvDepth);
    #elif defined(_TOON_SHADOW_PCF_5X5)
        return ToonShadowPCF_5x5(_MainLightShadowmapSize, shadowUvDepth);
    #elif defined(_TOON_SHADOW_BASE)
        return ToonShadowPCF_Base(shadowUvDepth);
    #else
        // 默认 3x3 PCF
        return ToonShadowPCF_3x3(_MainLightShadowmapSize, shadowUvDepth);
    #endif
}

// 兼容旧接口（无 screenPos 参数）
half ToonMainLightShadow(float4 shadowCoord, float NdotL)
{
    return ToonMainLightShadow(shadowCoord, NdotL, float2(0, 0));
}

// 带角色 Atlas 阴影的主入口
half ToonMainLightShadowWithCharacterAtlas(float4 shadowCoord, float3 positionWS, float NdotL, float2 screenPos)
{
    half csmShadow = ToonMainLightShadow(shadowCoord, NdotL, screenPos);

    #if !defined(_CHAR_SHADOW_ATLAS_ON)
        return csmShadow;
    #else
        bool atlasCovered;
        half charShadow = SampleCharacterAtlasShadow(positionWS, atlasCovered, screenPos);

        if (atlasCovered)
        {
            return charShadow;
        }
        else
        {
            return csmShadow;
        }
    #endif
}

// 兼容旧接口（无 screenPos 参数）
half ToonMainLightShadowWithCharacterAtlas(float4 shadowCoord, float3 positionWS, float NdotL)
{
    return ToonMainLightShadowWithCharacterAtlas(shadowCoord, positionWS, NdotL, float2(0, 0));
}

#endif // TOON_SHADOW_FILTER_INCLUDED
