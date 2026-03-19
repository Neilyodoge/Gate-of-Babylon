// PBRToonFace.shader
// 从 DanbaidongRP/PBRToon/Face 移植到 URP
// 特色: SDF 脸部阴影贴图, 鼻尖高光, 头发阴影检测(简化)

Shader "Universal Render Pipeline/PBRToon/Face"
{
    Properties
    {
        // ====== 纹理 ======
        [Header(Textures)]
        _BaseColor                              ("BaseColor", Color) = (1,1,1,1)
        _BaseMap                                ("BaseMap(diff alpha)", 2D) = "white" {}

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

        [Header(Face Lightmap)]
        [NoScaleOffset]_FaceLightMap            ("FaceLightMap", 2D) = "white" {}

        [Header(Shadow)]
        _ShadowColor                            ("ShadowColor", Color) = (0,0,0,1)
        _ShadowOffset                           ("ShadowOffset", Range(-1, 1)) = 0.5
        _ShadowSmoothNdotL                      ("ShadowSmoothNdotL", Range(0, 1)) = 0.25
        _ShadowSmoothScene                      ("ShadowSmoothScene", Range(0, 1)) = 0.1
        _ShadowStrength                         ("ShadowStrength", Range(0, 1)) = 1.0

        [Header(Nose Specular)]
        [HDR]_NoseSpecColor                     ("NoseSpecColor", Color) = (0,0,0,1)
        _NoseSpecMin                            ("NoseSpecMin", Float) = 0
        _NoseSpecMax                            ("NoseSpecMax", Float) = 0.5

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
            "Queue" = "Geometry-10"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ====================================================================
        // Forward Pass: PBR Toon Face
        // ====================================================================
        Pass
        {
            Name "PBRToonFaceForward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex PBRToonFaceVert
            #pragma fragment PBRToonFaceFrag

            // Material Keywords
            #pragma shader_feature_local _SHADOW_RAMP
            #pragma shader_feature_local _INDIR_CUBEMAP
            #pragma shader_feature_local _ALPHATEST_ON

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

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
            float   _ShadowSmoothNdotL;
            float   _ShadowSmoothScene;
            float   _ShadowStrength;

            // Nose Spec
            float4  _NoseSpecColor;
            float   _NoseSpecMin;
            float   _NoseSpecMax;

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

            // FaceDirection (由 C# 脚本设置)
            float3  _FaceRightDirWS;
            float3  _FaceFrontDirWS;

            // Alpha Test
            float   _Cutoff;
            // Outline
            float   _OutlineWidth;
            float4  _OutlineColor;
            float4  _OutlineDepthOldRange;
            float4  _OutlineDepthNewRange;
            float   _OutlineNormalScale;
            CBUFFER_END

            // ====== 纹理声明 ======
            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_FaceLightMap);       SAMPLER(sampler_FaceLightMap);
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
                float2 faceLightDot     : TEXCOORD6; // x: 左右方向dot, y: SDF采样值
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ====== Vertex Shader ======
            Varyings PBRToonFaceVert(Attributes v)
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

                // Face lightmap dot value
                {
                    Light mainLight = GetMainLight();
                    float3 lightDirWS = mainLight.direction;
                    lightDirWS.xz = normalize(lightDirWS.xz);
                    float3 faceRightWS = _FaceRightDirWS;
                    faceRightWS.xz = normalize(faceRightWS.xz);
                    o.faceLightDot.x = dot(lightDirWS.xz, faceRightWS.xz);
                    o.faceLightDot.y = saturate(dot(-lightDirWS.xz, _FaceFrontDirWS.xz) * 0.5 + _ShadowOffset);
                }

                return o;
            }

            // ====== Fragment Shader ======
            float4 PBRToonFaceFrag(Varyings i) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float  depth = i.positionHCS.z;
                float2 UV = i.uv.xy;
                float2 UV1 = i.uv.zw;
                float3 positionWS = i.positionWS;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionHCS.xy);

                // FaceLightMap UV 计算 (SDF)
                float2 faceLightMapUV = UV1;
                faceLightMapUV.x = 1 - faceLightMapUV.x;
                faceLightMapUV.x = i.faceLightDot.x < 0 ? 1 - faceLightMapUV.x : faceLightMapUV.x;

                // ===== 纹理采样 =====
                float4 mainTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, UV);
                float4 faceLightMap = SAMPLE_TEXTURE2D(_FaceLightMap, sampler_FaceLightMap, faceLightMapUV);

                // ===== 材质属性 =====
                float emission               = 1;
                float metallic               = _Metallic;
                float smoothness             = _Smoothness;
                float occlusion              = _Occlusion;
                float directOcclusion        = 1;
                float3 albedo                = mainTex.rgb * _BaseColor.rgb;

                #ifdef _ALPHATEST_ON
                    clip(mainTex.a - _Cutoff);
                #endif

                float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
                float roughness           = PerceptualRoughnessToRoughness(perceptualRoughness);
                float roughnessSquare     = max(roughness * roughness, FLT_MIN);

                float3 normalWS = SafeNormalize(i.normalWS);

                // Rim Light 用的视图空间法线
                float3 normalVS = TransformWorldToViewNormal(normalWS);
                normalVS = SafeNormalize(normalVS);

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                float NdotV = dot(normalWS, viewDirWS);
                float clampedNdotV = ClampNdotV(NdotV);

                uint meshRenderingLayers = GetMeshRenderingLayer();

                // ===== 初始化光照累加器 =====
                ToonDirectLighting directLighting;
                ToonIndirectLighting indirectLighting;
                ZERO_INITIALIZE(ToonDirectLighting, directLighting);
                ZERO_INITIALIZE(ToonIndirectLighting, indirectLighting);
                float3 rimColor = 0;

                // ===== PBR 参数准备 =====
                float3 diffuseColor = ComputeDiffuseColor(albedo, metallic);
                float3 fresnel0 = ComputeFresnel0(albedo, metallic, DEFAULT_SPECULAR_VALUE);

                float3 specularFGD;
                float  diffuseFGD;
                float  reflectivity;
                GetApproxPreIntegratedFGD(clampedNdotV, perceptualRoughness, fresnel0, specularFGD, diffuseFGD, reflectivity);
                float energyCompensation = 1.0 / reflectivity - 1.0;

                float directRimArea = GetCharacterDirectRimLightArea(normalVS, screenUV, depth, _DirectRimWidth);

                // ===== Face SDF 属性 =====
                float faceSDF = faceLightMap.r;
                float faceShadowArea = faceLightMap.a;

                // ===== 主光源 =====
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
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

                    // 阴影：使用 SDF 脸部阴影贴图
                    float shadowAttenuation = mainLight.shadowAttenuation;

                    float shadowNdotL = SigmoidSharp(halfLambert, _ShadowOffset, _ShadowSmoothNdotL * 5);
                    float faceMapShadow = SigmoidSharp(faceSDF, i.faceLightDot.y, _ShadowSmoothNdotL * 5) * faceShadowArea;
                    float shadowScene = SigmoidSharp(shadowAttenuation, 0.5, _ShadowSmoothScene * 5);
                    float shadowArea = min(faceMapShadow, shadowScene); // Face SDF 控制阴影
                    shadowArea = lerp(1, shadowArea, _ShadowStrength);

                    float3 shadowRamp = lerp(_ShadowColor.rgb, float3(1, 1, 1), shadowArea);
                    #ifdef _SHADOW_RAMP
                    shadowRamp = SampleDirectShadowRamp(TEXTURE2D_ARGS(_ShadowRampTex, sampler_ShadowRampTex), shadowArea, 0.125).xyz;
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

                    // Face Nose Spec (鼻尖高光)
                    float faceSpecStep = clamp(i.faceLightDot.y, 0.001, 0.999);
                    float2 noseSpecUV = faceLightMapUV;
                    noseSpecUV.x = 1 - noseSpecUV.x;
                    float4 noseSpecMap = SAMPLE_TEXTURE2D(_FaceLightMap, sampler_FaceLightMap, noseSpecUV);
                    float noseSpecArea1 = step(faceSpecStep, noseSpecMap.g);
                    float noseSpecArea2 = step(1 - faceSpecStep, noseSpecMap.b);
                    float noseSpecArea = noseSpecArea1 * noseSpecArea2 * smoothstep(_NoseSpecMin, _NoseSpecMax, 1 - i.faceLightDot.y);
                    float3 noseSpecColor = _NoseSpecColor.rgb * _NoseSpecColor.a * noseSpecArea;

                    // 累加
                    directLighting.diffuse += diffuseColor * diffTerm * shadowRamp * lightColor * directOcclusion;
                    directLighting.specular += specTerm * clampedNdotL * shadowScene * lightColor * directOcclusion + noseSpecColor;
                    rimColor += directRim;
                }

                // ===== 附加光源 =====
                #if defined(_ADDITIONAL_LIGHTS)
                // 构造 InputData 供 LIGHT_LOOP_BEGIN (Forward+) 使用
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                    Light addLight = GetAdditionalLight(lightIndex, positionWS);

                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(addLight.layerMask, meshRenderingLayers))
                    #endif
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

                        float3 lightDirVS = TransformWorldToViewDir(lightDirWS);
                        lightDirVS = SafeNormalize(lightDirVS);
                        float punctualRimArea = GetCharacterPunctualRimLightArea(lightDirVS, screenUV, depth, _PunctualRimWidth);
                        float3 punctualRim = GetRimColor(punctualRimArea, diffuseColor, normalVS, lightDirVS, 1, addLight.color, float3(0,0,0));

                        directLighting.diffuse += diffuseColor * diffTerm * addLight.color * attenuation;
                        directLighting.specular += specTerm * addLight.color * attenuation;
                        rimColor += punctualRim * attenuation;
                    }
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, positionWS);

                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(addLight.layerMask, meshRenderingLayers))
                    #endif
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
                float3 emissResult = emission * lerp(_EmissionCol.rgb, _EmissionCol.rgb * albedo.rgb, _EmissionCol.a);

                // ===== 后处理合并 =====
                float3 resultColor = ToonPostEvaluate(directLighting, indirectLighting, occlusion, fresnel0, energyCompensation, _IndirDiffIntensity, _IndirSpecIntensity);
                resultColor += emissResult + rimColor;

                return float4(resultColor, 1);
            }
            ENDHLSL
        }

        // ShadowCaster & DepthOnly 复用 Base
        UsePass "Universal Render Pipeline/PBRToon/Base/SHADOWCASTER"
        UsePass "Universal Render Pipeline/PBRToon/Base/DEPTHONLY"
        UsePass "Universal Render Pipeline/PBRToon/Base/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/PBRToon/Base/OUTLINE"
    }

    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.PBRToonFaceShaderGUI"
}
