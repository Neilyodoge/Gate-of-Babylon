// ============================================================================
// Lit_BentNormalForwardPass.hlsl
// 基于 URP LitForwardPass 的 Bent Normal 版本
// Bent Normal 数据存储在 Mesh UV2 中，在顶点着色器中解码
// 
// UV2 数据格式: float4(relativeB, theta, aperture, scale)
// 解码方式:
//   tangentLength = sqrt(1 - relativeB * relativeB)
//   coneVisDir = relativeB * bitangent 
//              + (cos(theta) * normal + sin(theta) * orthoTangent) * tangentLength
//   coneAperture = aperture / HALF_PI  (归一化到 [0,1])
//   coneScale = scale
// ============================================================================
#ifndef LIT_BENT_NORMAL_FORWARD_PASS_INCLUDED
#define LIT_BENT_NORMAL_FORWARD_PASS_INCLUDED

#include "Lighting_BentNormal.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// _VISIBILITY_ON 也需要切线空间 (用于解码 bent normal)
#if defined(_VISIBILITY_ON)
#ifndef REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif
#endif

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    // UV2: bent normal 数据 (relativeB, theta, aperture, scale)
    float4 texcoord2    : TEXCOORD2;
    float2 dynamicLightmapUV  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    half4 normalWS_vConeOcclusion    : TEXCOORD2; // xyz: normalWS, w: vCone scale (1.0 if no visibility)
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5;
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9;
#endif

    // Visibility Cone 数据 (从 VS 传递到 FS)
    // xyz: 锥体方向 (世界空间), w: 锥体半角 (归一化到 [0,1])
#if defined(_VISIBILITY_ON)
    half4 visibilityCone            : TEXCOORD10;
#endif

    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ============================================================================
// Varyings 辅助函数 - 获取/设置 Visibility Cone 数据
// ============================================================================
half3 GetNormalWS(Varyings v) { return v.normalWS_vConeOcclusion.xyz; }
half  GetVConeScale(Varyings v) { return v.normalWS_vConeOcclusion.w; }

#if defined(_VISIBILITY_ON)
half3 GetVisibilityConeDirection(Varyings v) { return v.visibilityCone.xyz; }
half  GetVisibilityConeAperture(Varyings v)  { return v.visibilityCone.w; }
#endif

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(GetNormalWS(input), input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, GetNormalWS(input));

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = GetNormalWS(input);
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);

#if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
#endif
}

// ============================================================================
// 构建 VisibilityCone (在 Fragment Shader 中使用)
// ============================================================================
VisibilityCone BuildVisibilityCone(Varyings input, half3 normalWS)
{
#if defined(_VISIBILITY_ON)
    VisibilityCone vCone;
    vCone.direction = normalize(GetVisibilityConeDirection(input));
    vCone.aperture = saturate(GetVisibilityConeAperture(input));
    vCone.scale = saturate(GetVConeScale(input));

    // 应用遮蔽缩放 (_OcclusionScale 作为全局强度控制)
    vCone.direction = normalize(lerp(normalWS, vCone.direction, _OcclusionScale));
    vCone.aperture = lerp(1.0, vCone.aperture, _OcclusionScale);
    vCone.scale = lerp(1.0, vCone.scale, _OcclusionScale);

    return vCone;
#else
    // 无 visibility 数据时返回默认值 (无遮蔽)
    return VisibilityCone_Create(normalWS, 1.0, 1.0);
#endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

Varyings LitBentNormalPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    output.normalWS_vConeOcclusion = half4(normalInput.normalWS, 1.0);
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, half3(GetNormalWS(output)), viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH(GetNormalWS(output), output.vertexSH);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    // ===== 解码 UV2 中的 Visibility Cone 数据 =====
#if defined(_VISIBILITY_ON)
    float4 visibilityCone = input.texcoord2;
    // 构建副切线
    float3 bitangent = cross(GetNormalWS(output), output.tangentWS.xyz) * output.tangentWS.w;
    float3 orthoTangent = cross(bitangent, GetNormalWS(output));

    // 解码 bent normal 方向:
    // relativeB = visibilityCone.x (副切线分量)
    // theta = visibilityCone.y (法线-切线平面内的角度)
    float tangentAngle = visibilityCone.y;
    float tangentLength = sqrt(max(1.0 - visibilityCone.x * visibilityCone.x, 0.0));
    float3 coneVisDir = visibilityCone.x * bitangent
                      + (cos(tangentAngle) * GetNormalWS(output) + sin(tangentAngle) * orthoTangent) * tangentLength;

    output.visibilityCone.xyz = coneVisDir;
    // aperture: 从弧度归一化到 [0,1], 其中 1 对应 PI/2
    output.visibilityCone.w = saturate(visibilityCone.z / HALF_PI);
    // scale 存储在 normalWS 的 w 分量
    output.normalWS_vConeOcclusion.w = visibilityCone.w;
#endif

    return output;
}

void LitBentNormalPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, half3(GetNormalWS(input)), viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);

#ifdef _DBUFFER
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    // 构建 VisibilityCone
    VisibilityCone vCone = BuildVisibilityCone(input, inputData.normalWS);

    // 使用 Visibility Cone 版本的 PBR 片元着色
    half4 color = UniversalFragmentPBR_BentNormal(inputData, surfaceData, vCone);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}

#endif
