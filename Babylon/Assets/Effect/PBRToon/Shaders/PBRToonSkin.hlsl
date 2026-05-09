// PBRToonSkin.hlsl
// 基于视角 (Fresnel-style) 的轻量化"假 SSS"皮肤效果库
//
// 算法本质：当视线越接近掠射 (NoV → 0)，把 albedo 朝 _SSSColor 拉，
// 用来模拟皮肤在耳廓 / 鼻翼 / 手指边缘的"红透"次表面散射感。
// 不需要 ThicknessMap，几乎零成本，但只能呈现"边缘 SSS"，不替代真正的
// Pre-integrated Skin Shading。
//
// 用法（在 Forward Pass 内）：
//   #include "PBRToonSkin.hlsl"
//   ...
//   #ifdef _SKIN_ON
//       albedo = ApplySkinSSS(albedo, NdotV, _SSSColor.rgb, _SSSArea);
//   #endif
//
// 调用时机：必须在 albedo 计算完成后、ComputeDiffuseColor / ComputeFresnel0
// 之前注入，让后续 PBR 链路使用染色后的 albedo。
//
// 材质参数约定（在 Shader 的 CBUFFER 中声明）：
//   _SSSColor  float4   皮肤次表面颜色（默认偏红肉色 (1.0, 0.55, 0.5)）
//   _SSSArea   float    边缘 SSS 范围/强度（典型 0.5 ~ 1.5）

#ifndef PBR_TOON_SKIN_INCLUDED
#define PBR_TOON_SKIN_INCLUDED

// ============================================================================
// 核心：基于已有 NoV 的 SSS 染色
// NoV : dot(normalWS, viewDirWS)，未 saturate；本函数内部会 saturate
// ============================================================================
float3 ApplySkinSSS(float3 albedo, float NoV, float3 sssColor, float sssArea)
{
    // 把 [0,1] 的 NoV 压到 [0.15, 1.0]
    // 防止背面 (NoV<=0) 直接拉满 SSS 而出现一圈过宽的红边，
    // 同时让正面 (NoV=1) 完全保持原色，过渡更柔和
    float sss_NoV = saturate(NoV);
    sss_NoV = sss_NoV * 0.85 + 0.15;

    // 边缘越掠射，sss_area 越接近 1
    float sss_area = saturate(sssArea * (1.0 - sss_NoV));

    // 在白色和 SSS 颜色之间插值，作为一个染色乘数
    float3 sssColorEffect = lerp(float3(1.0, 1.0, 1.0), sssColor, sss_area);

    return albedo * sssColorEffect;
}

// ============================================================================
// 便利重载：直接传 normalWS / viewDirWS（自动算 NoV）
// 适合那些 fragment 还没算过 NdotV 的调用方
// ============================================================================
float3 ApplySkinSSS(float3 albedo, float3 normalWS, float3 viewDirWS, float3 sssColor, float sssArea)
{
    float NoV = dot(normalWS, viewDirWS);
    return ApplySkinSSS(albedo, NoV, sssColor, sssArea);
}

#endif // PBR_TOON_SKIN_INCLUDED
