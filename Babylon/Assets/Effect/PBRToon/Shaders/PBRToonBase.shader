// PBRToonBase.shader
// 从 DanbaidongRP/PBRToon/Base 移植到 URP
// 仅保留 Forward 渲染，不含 GBuffer 和光追

Shader "Universal Render Pipeline/PBRToon/Base"
{
    Properties
    {
        // ====== 纹理 ======
        [Header(Textures)]
        _BaseColor                              ("BaseColor", Color) = (1,1,1,1)
        _BaseMap                                ("BaseMap(diff alpha)", 2D) = "white" {}
        [NoScaleOffset]_PBRMask                 ("PBRMask(metal smooth ao emiss)", 2D) = "white" {}
        [NoScaleOffset]_NormalMap               ("NormalMap", 2D) = "bump" {}
        _NormalScale                            ("NormalScale", Range(0, 1)) = 1

        // ====== PBR 属性 ======
        [Header(PBR Properties)]
        _Metallic                               ("Metallic", Range(0, 1)) = 0.5
        _Smoothness                             ("Smoothness", Range(0, 1)) = 0.5
        _Occlusion                              ("Occlusion", Range(0, 1)) = 1

        // ====== 直接光照 ======
        [Header(Direct Light)]
        [HDR]_SelfLight                         ("SelfLight", Color) = (1,1,1,1)
        _MainLightColorLerp                     ("Unity Light or SelfLight", Range(0, 1)) = 0.5
        _DirectOcclusion                        ("DirectOcclusion", Range(0, 1)) = 0.1

        [Header(Shadow)]
        _ShadowColor                            ("ShadowColor", Color) = (0,0,0,1)
        _ShadowOffset                           ("ShadowOffset", Range(-1, 1)) = 0
        _ShadowSharpness                        ("ShadowSharpness", Range(1, 100)) = 10
        _ShadowSmoothScene                      ("ShadowSmoothScene", Range(0, 1)) = 0.1
        _ShadowStrength                         ("ShadowStrength", Range(0, 1)) = 1.0

        // ====== Shadow Ramp ======
        [Header(Shadow Ramp)]
        [Toggle(_SHADOW_RAMP)]_EnableShadowRamp ("Enable Shadow Ramp", Float) = 0
        _ShadowRampTex                          ("ShadowRampTex", 2D) = "white" {}

        // ====== 间接光照 ======
        [Header(Indirect Light Diffuse)]
        [HDR]_SelfEnvColor                      ("SelfEnvColor", Color) = (0.5,0.5,0.5,0.5)
        _EnvColorLerp                           ("Unity SH or SelfEnv", Range(0, 1)) = 0.5
        _IndirDiffUpDirSH                       ("IndirDiffUpDirSH", Range(0, 1)) = 0.0
        _IndirDiffIntensity                     ("IndirDiffIntensity", Range(0, 1)) = 1.0

        [Header(Indirect Light Specular)]
        [Toggle(_INDIR_CUBEMAP)]_EnableIndirCubemap ("Enable Custom Cubemap", Float) = 0
        [NoScaleOffset]_IndirSpecCubemap        ("SpecCube", Cube) = "black" {}
        _IndirSpecCubeWeight                    ("SpecCubeWeight", Range(0, 1)) = 0.5
        _IndirSpecIntensity                     ("IndirSpecIntensity", Range(0.01, 5)) = 1.0

        // ====== 自发光 & 边缘光 ======
        [Header(Emission)]
        [NoScaleOffset]_EmissionMap             ("EmissionMap", 2D) = "white" {}
        [HDR]_EmissionCol                       ("EmissionCol", Color) = (0,0,0,1)

        [Header(RimLight)]
        [HDR]_DirectRimFrontCol                 ("DirectRimFrontCol", Color) = (1,1,1,0.5)
        [HDR]_DirectRimBackCol                  ("DirectRimBackCol", Color) = (0.2,0.2,0.2,0.5)
        _DirectRimWidth                         ("DirectRimWidth", Range(0, 10)) = 2.5
        _PunctualRimWidth                       ("PunctualRimWidth", Range(0, 10)) = 2.75

        // ====== 描边 ======
        [Header(Outline)]
        [Toggle(_OUTLINE_ON)]_EnableOutline      ("Enable Outline", Float) = 1
        _OutlineWidth                           ("OutlineWidth", Range(0, 10)) = 1.0
        _OutlineColor                           ("OutlineColor", Color) = (0.5,0.5,0.5,1)
        _OutlineDepthOldRange                   ("OutlineDepthOldRange", Vector) = (0.01, 2.00, 6.00, 0)
        _OutlineDepthNewRange                   ("OutlineDepthNewRange", Vector) = (0.105, 0.245, 0.60, 0)
        _OutlineNormalScale                     ("OutlineNormalScale", Range(0, 100)) = 1

        // ====== 自定义 PCF 阴影 ======
        [Header(Shadow PCF)]
        [KeywordEnum(Base, PCF_2x2, PCF_3x3, PCF_5x5, PCF_7x7, PCSS)]
        _ToonShadow                             ("Shadow Quality", Float) = 2

        // ====== PCSS 参数 ======
        [Header(PCSS Params)]
        _PcssSoftness                           ("PCSS Softness", Range(0.001, 0.1)) = 0.02
        _PcssSoftnessFalloff                    ("PCSS Softness Falloff", Range(0.1, 5)) = 1.5
        _PcssBlockerSamples                     ("PCSS Blocker Samples", Range(4, 32)) = 16
        _PcssFilterSamples                      ("PCSS Filter Samples", Range(4, 32)) = 16
        _PcssBlockerGradientBias                ("PCSS Blocker Gradient Bias", Range(0, 0.01)) = 0.0
        _PcssPCFGradientBias                    ("PCSS PCF Gradient Bias", Range(0, 0.01)) = 0.0

        // ====== Shadow Edge Color ======
        [Header(Shadow Edge Color)]
        [Toggle(_SHADOW_EDGE_COLOR)]_EnableShadowEdgeColor ("Enable Shadow Edge Color", Float) = 0
        _ShadowEdgeBegin                        ("Shadow Edge Begin", Range(0, 1)) = 0.2
        _ShadowEdgeEnd                          ("Shadow Edge End", Range(0, 1)) = 0.8
        _ShadowEdgeBeginColor                   ("Shadow Edge Begin Color", Color) = (0.3, 0.1, 0.1, 1)
        _ShadowEdgeEndColor                     ("Shadow Edge End Color", Color) = (1, 0.9, 0.8, 1)
        _ShadowEdgeDarkColor                    ("Shadow Edge Dark Color", Color) = (0, 0, 0, 1)
        _ShadowEdgeLightColor                   ("Shadow Edge Light Color", Color) = (1, 1, 1, 1)
        _ShadowEdgeFadeBeginWidth               ("Shadow Edge Fade Begin Width", Range(0, 0.5)) = 0.1
        _ShadowEdgeFadeEndWidth                 ("Shadow Edge Fade End Width", Range(0, 0.5)) = 0.1

        // ====== Debug ======
        [Header(Debug)]
        [Toggle(_DEBUG_SHADOW)]_DebugShadow     ("Debug Shadow", Float) = 0
        [Enum(Shadow,0, Ramp,1)]_DebugShadowMode ("Debug Shadow Mode", Float) = 0

        // ====== 其他设置 ======
        [Header(Other Settings)]
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull                                   ("Cull Mode", Float) = 2
        [Toggle(_ALPHATEST_ON)]_AlphaClip       ("Alpha Clip", Float) = 0
        _Cutoff                                 ("Cutoff", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ====================================================================
        // Forward Pass: PBR Toon 前向渲染
        // ====================================================================
        Pass
        {
            Name "PBRToonForward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0

            // Shader Stages
            #pragma vertex PBRToonBaseVert
            #pragma fragment PBRToonBaseFrag

            // Material Keywords
            #pragma shader_feature_local _SHADOW_RAMP
            #pragma shader_feature_local _INDIR_CUBEMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _ _TOON_SHADOW_BASE _TOON_SHADOW_PCF_2X2 _TOON_SHADOW_PCF_3X3 _TOON_SHADOW_PCF_5X5 _TOON_SHADOW_PCF_7X7 _TOON_SHADOW_PCSS
            #pragma shader_feature_local _SHADOW_EDGE_COLOR
            #pragma shader_feature_local _DEBUG_SHADOW

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CHAR_SHADOW_ATLAS_ON
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // 以下 multi_compile 已精简：卡通角色不需要反射探针混合/盒投影、
            // Light Cookie、Light Layers、Forward+、Lightmap(动态物体)、LOD Crossfade

            // GPU Instancing
            #pragma multi_compile_instancing

            // ====== Includes ======
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #include "PBRToonCommon.hlsl"

            // ====== CBUFFER ======
            CBUFFER_START(UnityPerMaterial)
            float3  _BaseColor;
            float4  _BaseMap_ST;
            float   _NormalScale;

            // PBR Properties
            float   _Metallic;
            float   _Smoothness;
            float   _Occlusion;

            // Direct Light
            float4  _SelfLight;
            float   _MainLightColorLerp;
            float   _DirectOcclusion;

            // Shadow
            float4  _ShadowColor;
            float   _ShadowOffset;
            float   _ShadowSharpness;
            float   _ShadowSmoothScene;
            float   _ShadowStrength;

            // Shadow Edge Color
            float   _ShadowEdgeBegin;
            float   _ShadowEdgeEnd;
            float4  _ShadowEdgeBeginColor;
            float4  _ShadowEdgeEndColor;
            float4  _ShadowEdgeDarkColor;
            float4  _ShadowEdgeLightColor;
            float   _ShadowEdgeFadeBeginWidth;
            float   _ShadowEdgeFadeEndWidth;

            // Indirect
            float4  _SelfEnvColor;
            float   _EnvColorLerp;
            float   _IndirDiffUpDirSH;
            float   _IndirDiffIntensity;
            float   _IndirSpecCubeWeight;
            float   _IndirSpecIntensity;

            // Emission
            float4  _EmissionCol;
            // RimLight
            float4  _DirectRimFrontCol;
            float4  _DirectRimBackCol;
            float   _DirectRimWidth;
            float   _PunctualRimWidth;

            // Alpha Test
            float   _Cutoff;
            // Outline
            float   _OutlineWidth;
            float4  _OutlineColor;
            float4  _OutlineDepthOldRange;
            float4  _OutlineDepthNewRange;
            float   _OutlineNormalScale;
            // PCSS
            float   _PcssSoftness;
            float   _PcssSoftnessFalloff;
            float   _PcssBlockerSamples;
            float   _PcssFilterSamples;
            float   _PcssBlockerGradientBias;
            float   _PcssPCFGradientBias;
            // Debug
            float   _DebugShadowMode;
            CBUFFER_END

            // ToonShadowFilter 必须在 CBUFFER 之后 include，
            // 因为 PCSS 模式需要读取 CBUFFER 中的 _PcssBlockerSamples 等参数
            #include "ToonShadowFilter.hlsl"

            // ====== 纹理声明 ======
            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_PBRMask);            SAMPLER(sampler_PBRMask);
            TEXTURE2D(_NormalMap);           SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap);         SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_ShadowRampTex);      SAMPLER(sampler_ShadowRampTex);
            TEXTURECUBE(_IndirSpecCubemap);

            // ====== 结构体 ======
            struct Attributes
            {
                float4 vertex       : POSITION;
                float3 normal       : NORMAL;
                float4 tangent      : TANGENT;
                float4 color        : COLOR;
                float2 uv0          : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS      : SV_POSITION;
                float3 positionWS       : TEXCOORD0;
                float3 normalWS         : TEXCOORD1;
                float3 tangentWS        : TEXCOORD2;
                float3 biTangentWS      : TEXCOORD3;
                float4 color            : TEXCOORD4;
                float4 uv              : TEXCOORD5; // xy:uv0 zw:uv1

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ====== Vertex Shader ======
            Varyings PBRToonBaseVert(Attributes v)
            {
                Varyings o;
                ZERO_INITIALIZE(Varyings, o);

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionHCS = TransformObjectToHClip(v.vertex.xyz);
                o.positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
                o.biTangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w * GetOddNegativeScale();
                o.color = v.color;
                o.uv.xy = TRANSFORM_TEX(v.uv0.xy, _BaseMap);
                o.uv.zw = v.uv1.xy;

                return o;
            }

            // ====== Fragment Shader ======
            float4 PBRToonBaseFrag(Varyings i) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float  depth = i.positionHCS.z;
                float2 UV = i.uv.xy;
                float2 UV1 = i.uv.zw;
                float3 positionWS = i.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionHCS.xy);

                // ===== 纹理采样 =====
                float4 mainTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, UV);
                float4 pbrMask = SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, UV);
                float3 bumpTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, UV), _NormalScale);

                // ===== 材质属性 =====
                float emission               = 1 - pbrMask.a;
                float metallic               = lerp(0, _Metallic, pbrMask.r);
                float smoothness             = lerp(0, _Smoothness, pbrMask.g);
                float occlusion              = lerp(1 - _Occlusion, 1, pbrMask.b);
                float directOcclusion        = lerp(1 - _DirectOcclusion, 1, pbrMask.b);
                float3 albedo                = mainTex.rgb * _BaseColor.rgb;

                #ifdef _ALPHATEST_ON
                    clip(mainTex.a - _Cutoff);
                #endif

                float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
                float roughness           = PerceptualRoughnessToRoughness(perceptualRoughness);
                float roughnessSquare     = max(roughness * roughness, FLT_MIN);

                // ===== 法线计算 =====
                float3 normalWS = SafeNormalize(i.normalWS);
                float3x3 TBN = float3x3(i.tangentWS, i.biTangentWS, i.normalWS);
                float3 bumpWS = TransformTangentToWorld(bumpTS, TBN);
                normalWS = SafeNormalize(bumpWS);

                // Rim Light 用的视图空间法线
                float3 normalVS = TransformWorldToViewNormal(normalWS);
                normalVS = SafeNormalize(normalVS);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                float NdotV = dot(normalWS, viewDirWS);
                float clampedNdotV = ClampNdotV(NdotV);

                // ===== 初始化光照累加器 =====
                ToonDirectLighting directLighting;
                ToonIndirectLighting indirectLighting;
                ZERO_INITIALIZE(ToonDirectLighting, directLighting);
                ZERO_INITIALIZE(ToonIndirectLighting, indirectLighting);
                float3 rimColor = 0;

                // ===== PBR 参数准备 =====
                float3 diffuseColor = ComputeDiffuseColor(albedo, metallic);
                float3 fresnel0 = ComputeFresnel0(albedo, metallic, DEFAULT_SPECULAR_VALUE);

                // 简化的预积分 FGD (无需 LUT 贴图)
                float3 specularFGD;
                float  diffuseFGD;
                float  reflectivity;
                GetApproxPreIntegratedFGD(clampedNdotV, perceptualRoughness, fresnel0, specularFGD, diffuseFGD, reflectivity);
                float energyCompensation = 1.0 / reflectivity - 1.0;

                float directRimArea = GetCharacterDirectRimLightArea(normalVS, screenUV, depth, _DirectRimWidth);

                // ===== Debug 阴影变量 =====
                float3 _dbg_shadow = 1;    // 实时阴影（shadowScene，shadowMap 采样结果）
                float3 _dbg_rampTex = 1;   // 明暗交界线（shadowNdotL，NdotL 控制的 ramp）

                // ===== 主光源 =====
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                {
                    float3 lightColor = lerp(mainLight.color, _SelfLight.rgb, _MainLightColorLerp);
                    float3 lightDirWS = mainLight.direction;
                    float NdotL = dot(normalWS, lightDirWS);

                    float clampedNdotL = saturate(NdotL);
                    float halfLambert = NdotL * 0.5 + 0.5;
                    float clampedRoughness = max(roughness, 0.002);

                    float LdotV, NdotH, LdotH, invLenLV;
                    GetBSDFAngle(viewDirWS, lightDirWS, NdotL, NdotV, LdotV, NdotH, LdotH, invLenLV);
                    float3 lightDirVS = TransformWorldToViewDir(lightDirWS);
                    lightDirVS = SafeNormalize(lightDirVS);

                    // 阴影：CSM 使用 URP 默认采样（含 _SHADOWS_SOFT 软阴影）
                    // Atlas 阴影使用自定义 PCF/PCSS
                    float shadowAttenuation = ToonMainLightShadowWithCharacterAtlas(shadowCoord, positionWS, NdotL, i.positionHCS.xy);

                    // 明暗交界线: saturate((NdotL - 位置) * 软硬度)
                    float shadowNdotL = saturate((NdotL - _ShadowOffset) * _ShadowSharpness);
                    float shadowScene = SigmoidSharp(shadowAttenuation, 0.5, _ShadowSmoothScene * 5);
                    float shadowArea = min(shadowNdotL, shadowScene);
                    shadowArea = lerp(1, shadowArea, _ShadowStrength);

                    // 保存 debug 变量：shadow = 实时阴影，ramp = 明暗交界线
                    _dbg_shadow = shadowScene.xxx;
                    _dbg_rampTex = shadowNdotL.xxx;

                    float3 shadowRamp = lerp(_ShadowColor.rgb, float3(1, 1, 1), shadowArea);
                    #ifdef _SHADOW_RAMP
                    shadowRamp = SampleDirectShadowRamp(TEXTURE2D_ARGS(_ShadowRampTex, sampler_ShadowRampTex), NdotL, 0.125).xyz;
                    #endif

                    // Shadow Edge Color: 在阴影边缘区域叠加渐变颜色
                    #ifdef _SHADOW_EDGE_COLOR
                    shadowRamp = GetShadowEdgeColor2(
                        shadowArea,
                        _ShadowEdgeBegin, _ShadowEdgeEnd,
                        _ShadowEdgeBeginColor.rgb, _ShadowEdgeEndColor.rgb,
                        _ShadowEdgeDarkColor.rgb, _ShadowEdgeLightColor.rgb,
                        _ShadowEdgeFadeBeginWidth, _ShadowEdgeFadeEndWidth);
                    #endif

                    // BRDF
                    float3 F = F_Schlick(fresnel0, LdotH);
                    float DV = DV_SmithJointGGX(NdotH, abs(NdotL), clampedNdotV, clampedRoughness);
                    float3 specTerm = F * DV;
                    float diffTerm = Lambert();

                    #ifdef _SHADOW_RAMP
                    float specRange = saturate(DV);
                    float3 specRampCol = SampleDirectSpecularRamp(TEXTURE2D_ARGS(_ShadowRampTex, sampler_ShadowRampTex), specRange, 0.375).xyz;
                    specTerm = F * clamp(specRampCol.rgb + DV, 0, 10);
                    #endif

                    // 主光源 Rim Light
                    float3 frontRimCol = lerp(_DirectRimFrontCol.rgb, _DirectRimFrontCol.rgb * lightColor, _DirectRimFrontCol.a);
                    float3 backRimCol = lerp(_DirectRimBackCol.rgb, _DirectRimBackCol.rgb * lightColor, _DirectRimBackCol.a);
                    float3 directRim = GetRimColor(directRimArea, diffuseColor, normalVS, lightDirVS, shadowArea, frontRimCol, backRimCol);

                    // 累加
                    directLighting.diffuse += diffuseColor * diffTerm * shadowRamp * lightColor * directOcclusion;
                    directLighting.specular += specTerm * clampedNdotL * shadowScene * lightColor * directOcclusion;
                    rimColor += directRim;
                }

                // ===== 附加光源 =====
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, positionWS);

                    {
                        float3 lightDirWS = addLight.direction;
                        float NdotL = dot(normalWS, lightDirWS);
                        float clampedNdotL = saturate(NdotL);
                        float clampedRoughness = max(roughness, 0.002);
                        float attenuation = addLight.distanceAttenuation * addLight.shadowAttenuation;

                        float LdotV, NdotH, LdotH, invLenLV;
                        GetBSDFAngle(viewDirWS, lightDirWS, NdotL, NdotV, LdotV, NdotH, LdotH, invLenLV);

                        float3 F = F_Schlick(fresnel0, LdotH);
                        float DV = DV_SmithJointGGX(NdotH, abs(NdotL), clampedNdotV, clampedRoughness);
                        float3 specTerm = F * DV;
                        float diffTerm = Lambert();

                        diffTerm *= clampedNdotL;
                        specTerm *= clampedNdotL;

                        // 附加光 Rim Light
                        float3 lightDirVS = TransformWorldToViewDir(lightDirWS);
                        lightDirVS = SafeNormalize(lightDirVS);
                        float punctualRimArea = GetCharacterPunctualRimLightArea(lightDirVS, screenUV, depth, _PunctualRimWidth);
                        float3 punctualRim = GetRimColor(punctualRimArea, diffuseColor, normalVS, lightDirVS, 1, addLight.color, float3(0,0,0));

                        directLighting.diffuse += diffuseColor * diffTerm * addLight.color * attenuation;
                        directLighting.specular += specTerm * addLight.color * attenuation;
                        rimColor += punctualRim * attenuation;
                    }
                LIGHT_LOOP_END
                #endif

                // ===== 间接漫反射 =====
                EvaluateToonIndirectDiffuse(indirectLighting, diffuseColor, normalWS, _IndirDiffUpDirSH, _SelfEnvColor, _EnvColorLerp);

                // ===== 间接镜面反射 =====
                float3 reflectDirWS = reflect(-viewDirWS, normalWS);
                float reflectionHierarchyWeight = 0.0;

                #if defined(_INDIR_CUBEMAP)
                {
                    float cubeWeight = _IndirSpecCubeWeight;
                    reflectionHierarchyWeight += cubeWeight;
                    EvaluateToonIndirectSpecular_Cubemap(indirectLighting, TEXTURECUBE_ARGS(_IndirSpecCubemap, sampler_LinearRepeat),
                                                        reflectDirWS, perceptualRoughness, specularFGD, cubeWeight);
                }
                #endif

                {
                    float skyWeight = saturate(1.0 - reflectionHierarchyWeight);
                    EvaluateToonIndirectSpecular_Sky(indirectLighting, reflectDirWS, perceptualRoughness, specularFGD, skyWeight);
                }

                // ===== 自发光 =====
                float3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, UV).rgb;
                float3 emissResult = emission * emissionTex * lerp(_EmissionCol.rgb, _EmissionCol.rgb * albedo.rgb, _EmissionCol.a);

                // ===== 后处理合并 =====
                float3 resultColor = ToonPostEvaluate(directLighting, indirectLighting, occlusion, fresnel0, energyCompensation, _IndirDiffIntensity, _IndirSpecIntensity);
                resultColor += emissResult + rimColor;

                #ifdef _DEBUG_SHADOW
                {
                    int debugMode = (int)_DebugShadowMode;
                    if (debugMode == 1)
                        return float4(_dbg_rampTex, 1);   // Ramp: 明暗交界线（shadowNdotL）
                    else
                        return float4(_dbg_shadow, 1);    // Shadow: 实时阴影（shadowScene）
                }
                #endif

                return float4(resultColor, 1);
            }
            ENDHLSL
        }

        // ====================================================================
        // ShadowCaster Pass
        // ====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // 简化的 ShadowCaster
            float3  _BaseColor;
            float4  _BaseMap_ST;
            float   _Cutoff;

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct ShadowAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(col.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }

        // ====================================================================
        // DepthOnly Pass
        // ====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3  _BaseColor;
            float4  _BaseMap_ST;
            float   _Cutoff;

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct DepthAttributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthOnlyVertex(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(DepthVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(col.a - _Cutoff);
                #endif

                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ====================================================================
        // DepthNormals Pass
        // ====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3  _BaseColor;
            float4  _BaseMap_ST;
            float   _NormalScale;
            float   _Cutoff;

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);

            struct DNAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD2;
                float3 bitangentWS  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DepthNormalsVertex(DNAttributes input)
            {
                DNVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w * GetOddNegativeScale();
                return output;
            }

            half4 DepthNormalsFragment(DNVaryings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(col.a - _Cutoff);
                #endif

                float3 normalWS = normalize(input.normalWS);
                return half4(normalWS, 0);
            }
            ENDHLSL
        }

        // ====================================================================
        // Outline Pass: 原神风格背面法线外扩描边
        // 使用平滑法线 (从 UV2/3/4 解码) + 视距自适应描边宽度
        // ====================================================================
        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode" = "Outline"
            }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex ToonOutlineVert
            #pragma fragment ToonOutlineFrag

            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _ALPHATEST_ON

            #pragma multi_compile_instancing

            #include "PBRToonOutline.hlsl"

            // ====== CBUFFER ======
            CBUFFER_START(UnityPerMaterial)
            float3  _BaseColor;
            float4  _BaseMap_ST;
            float   _NormalScale;
            float   _Metallic;
            float   _Smoothness;
            float   _Occlusion;
            float4  _SelfLight;
            float   _MainLightColorLerp;
            float   _DirectOcclusion;
            float4  _ShadowColor;
            float   _ShadowOffset;
            float   _ShadowSharpness;
            float   _ShadowSmoothScene;
            float   _ShadowStrength;
            // Shadow Edge Color
            float   _ShadowEdgeBegin;
            float   _ShadowEdgeEnd;
            float4  _ShadowEdgeBeginColor;
            float4  _ShadowEdgeEndColor;
            float4  _ShadowEdgeDarkColor;
            float4  _ShadowEdgeLightColor;
            float   _ShadowEdgeFadeBeginWidth;
            float   _ShadowEdgeFadeEndWidth;
            float4  _SelfEnvColor;
            float   _EnvColorLerp;
            float   _IndirDiffUpDirSH;
            float   _IndirDiffIntensity;
            float   _IndirSpecCubeWeight;
            float   _IndirSpecIntensity;
            float4  _EmissionCol;
            float4  _DirectRimFrontCol;
            float4  _DirectRimBackCol;
            float   _DirectRimWidth;
            float   _PunctualRimWidth;
            float   _Cutoff;
            // Outline
            float   _OutlineWidth;
            float4  _OutlineColor;
            float4  _OutlineDepthOldRange;
            float4  _OutlineDepthNewRange;
            float   _OutlineNormalScale;
            // PCSS
            float   _PcssSoftness;
            float   _PcssSoftnessFalloff;
            float   _PcssBlockerSamples;
            float   _PcssFilterSamples;
            float   _PcssBlockerGradientBias;
            float   _PcssPCFGradientBias;
            // Debug
            float   _DebugShadowMode;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct OutlineAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
                float4 uv3          : TEXCOORD3; // 平滑法线固定存储在 UV3 (TEXCOORD3)，切线空间 xyz
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineVaryings ToonOutlineVert(OutlineAttributes input)
            {
                OutlineVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

                #ifdef _OUTLINE_ON
                    // 从 UV3 (TEXCOORD3).xyz 解码切线空间平滑法线（3通道编码）
                    // UV 通道分配: UV2=BentNormal, UV3=平滑法线（切线空间）
                    float3 snTS = input.uv3.xyz;

                    // 如果 xyz 都为 0，说明没有烘焙平滑法线，回退到原始法线
                    float3 smoothNormal;
                    if (dot(snTS, snTS) > 0.001)
                    {
                        // 构建 TBN 矩阵，将切线空间法线还原到对象空间
                        float3 T = normalize(input.tangentOS.xyz);
                        float3 N = normalize(input.normalOS);
                        float3 B = normalize(cross(N, T) * input.tangentOS.w);
                        // smoothNormalOS = T * snTS.x + B * snTS.y + N * snTS.z
                        smoothNormal = normalize(T * snTS.x + B * snTS.y + N * snTS.z);
                    }
                    else
                    {
                        smoothNormal = input.normalOS;
                    }

                    output.positionCS = ToonOutlineVertex(
                        input.positionOS,
                        smoothNormal,
                        input.color,
                        _OutlineWidth,
                        _OutlineDepthOldRange.xyz,
                        _OutlineDepthNewRange.xyz,
                        _OutlineNormalScale
                    );
                #else
                    // 描边关闭时，将顶点退化到原点避免渲染
                    output.positionCS = float4(0, 0, 0, 1);
                #endif

                return output;
            }

            half4 ToonOutlineFrag(OutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef _ALPHATEST_ON
                    half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    clip(col.a - _Cutoff);
                #endif

                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.PBRToonBaseShaderGUI"
}
