// PBRToonOutline.hlsl
// 原神风格描边算法 (背面法线外扩 + 视距自适应宽度)
// 从 PBRToonCommon.hlsl 拆分而来，便于单独维护描边逻辑
//
// 用法：在 Outline Pass 内 #include 本文件，调用 ToonOutlineVertex 即可。
// 顶点输入约定：
//   positionOS    - 对象空间顶点位置
//   smoothNormalOS- 对象空间平滑法线 (一般由 UV3 切线空间法线 + TBN 解码而来)
//   vertexColor.a - 逐顶点描边宽度缩放 (默认 0.5)
// 材质参数约定 (在 Shader 的 CBUFFER 中声明)：
//   _OutlineWidth          float        全局描边宽度缩放
//   _OutlineDepthOldRange  float3/float4 (0.01, 2.00, 6.00) x:最细距离 y:近远分界 z:最粗距离
//   _OutlineDepthNewRange  float3/float4 (0.105, 0.245, 0.60) x:近处宽度 y:分界宽度 z:远处宽度
//   _OutlineNormalScale    float        法线 XY 缩放，默认 1.0

#ifndef PBR_TOON_OUTLINE_INCLUDED
#define PBR_TOON_OUTLINE_INCLUDED

// 仅依赖 URP Core (TransformObjectToWorld / TransformWorldToView / UNITY_MATRIX_*)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// ============================================================================
// 视距 Remap：将 EyeDepth 在旧范围内线性映射到新范围
// 魔改 Remap：与标准 Remap 区别是限定分母最小值不为 0 (max(..., 0.001))
// ============================================================================
float OutlineEyeDepthRemap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + saturate((In - InMinMax.x) / max(InMinMax.y - InMinMax.x, 0.001)) * (OutMinMax.y - OutMinMax.x);
}

// ============================================================================
// 原神风格描边顶点着色器
// positionOS:      对象空间顶点位置
// smoothNormalOS:  对象空间平滑法线 (从 UV3/TEXCOORD3.xyz 解码切线空间法线，用 TBN 矩阵还原到对象空间)
//                  UV 通道分配: UV2=BentNormal, UV3=平滑法线（切线空间 xyz）
// vertexColor:     顶点色 (A 通道 = 顶点描边宽度缩放，默认 0.5)
// 描边参数由 CBUFFER 中的 uniform 变量提供
// ============================================================================
float4 ToonOutlineVertex(
    float4 positionOS,
    float3 smoothNormalOS,
    float4 vertexColor,
    // 以下为材质参数
    float  outlineWidth,             // _OutlineWidth: 全局描边宽度缩放
    float3 eyeDepthRemapOldRanges,   // _OutlineDepthOldRange: 默认 (0.01, 2.0, 6.0)  x:最细距离 y:近远分界 z:最粗距离
    float3 eyeDepthRemapNewRanges,   // _OutlineDepthNewRange: 默认 (0.105, 0.245, 0.60) x:近处最细宽度 y:宽度 z:远处宽度
    float  normalizeViewNormalXYScale // _OutlineNormalScale: 法线 XY 缩放 默认 1.0
)
{
    // ====== 视图空间位置 ======
    // 这里算的是世界空间的米，1 米 = Transform 的 1
    float3 positionVS = TransformWorldToView(TransformObjectToWorld(positionOS.xyz));

    // ====== 法线变换到视图空间 ======
    float3 normalWS = mul((float3x3)unity_ObjectToWorld, smoothNormalOS);
    float3 normalVS = mul((float3x3)unity_MatrixV, normalWS);

    // 描边宽度方向：压扁 Z 后归一化取 XY
    // 这边算宽度相当于变化率也一起跟着算了，最后算宽度是不计算变化率的
    float2 normalVSxy = normalize(float3(normalVS.xy, 0.01)).xy * normalizeViewNormalXYScale;

    // ====== FOV 自适应 ======
    // 透视投影矩阵 unity_CameraProjection._m11 = cot(FOV/2)
    // cot(0.5*45°) = 2.414，即 FOV=45° 时 fov45AdaptScale=1，可看做缩放系数
    // FOV 越小，cot(0.5*FOV) 越大，2.414 / _m11 越小
    // FOV 越小，人物越大，描边在 3D 空间上变小，最终屏幕上粗度相应保持不变
    float fov45AdaptScale = 2.414 / unity_CameraProjection._m11;

    // ====== 视距分段 Remap ======
    // _OutlineDepthOldRange: x:最细距离 y:近远分界距离 z:最粗距离
    // _OutlineDepthNewRange: x:近处最细宽度 y:分界处宽度 z:远处宽度
    // positionVS.z 用负数是因为摄像机朝向 -z 轴向
    float fovFactor = -positionVS.z * fov45AdaptScale;
    bool isNear = fovFactor < eyeDepthRemapOldRanges.y;
    float2 widthOldRange = isNear ? eyeDepthRemapOldRanges.xy : eyeDepthRemapOldRanges.yz;
    float2 widthNewRange = isNear ? eyeDepthRemapNewRanges.xy : eyeDepthRemapNewRanges.yz;
    float eyeDepthInNewRange = OutlineEyeDepthRemap(fovFactor, widthOldRange, widthNewRange);

    // ====== 描边缩放因子 ======
    // 原神逐顶点宽度控制放在 UV 中 (uv2.y 默认 0.5)，我们改用顶点色 A 通道替代
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

#endif // PBR_TOON_OUTLINE_INCLUDED
