Shader "Game/TripPBR"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)

        [Normal][NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0,2)) = 1.0

        [NoScaleOffset] _MaskMap("Mask Map (R Metallic, G AO, B Emission, A Smoothness)", 2D) = "white" {}
        _MetallicScale("Metallic Multiplier", Range(0,1)) = 0.0
        _OcclusionStrength("AO Strength", Range(0,1)) = 1.0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _SmoothnessScale("Smoothness Multiplier", Range(0,1)) = 0.5

        _TriplanarScale("Triplanar UV Scale", Range(0.01,2)) = 0.25
        _TriplanarBlendSharpness("Triplanar Blend Sharpness", Range(1,16)) = 4.0

        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _RECEIVE_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Assets/1Game/Shader/Game_Lighitng.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _NormalScale;
                half _MetallicScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
                half _SmoothnessScale;
                float _TriplanarScale;
                float _TriplanarBlendSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirectionWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;

                #if defined(_MAIN_LIGHT_SHADOWS) && defined(_RECEIVE_SHADOWS)
                    float4 shadowCoord : TEXCOORD4;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 GameTriplanarBlend(float3 normalWS)
            {
                float3 blend = pow(
                    max(abs(normalWS), 0.0001),
                    _TriplanarBlendSharpness);
                return blend / max(blend.x + blend.y + blend.z, 0.0001);
            }

            half4 GameSampleTriplanar(
                Texture2D textureToSample,
                SamplerState textureSampler,
                float3 positionWS,
                float3 blend)
            {
                float2 uvX = positionWS.zy * _TriplanarScale;
                float2 uvY = positionWS.xz * _TriplanarScale;
                float2 uvZ = positionWS.xy * _TriplanarScale;

                half4 sampleX = SAMPLE_TEXTURE2D(textureToSample, textureSampler, uvX);
                half4 sampleY = SAMPLE_TEXTURE2D(textureToSample, textureSampler, uvY);
                half4 sampleZ = SAMPLE_TEXTURE2D(textureToSample, textureSampler, uvZ);

                return sampleX * blend.x + sampleY * blend.y + sampleZ * blend.z;
            }

            half3 GameSampleTriplanarNormal(
                float3 positionWS,
                half3 geometricNormalWS,
                float3 blend)
            {
                float2 uvX = positionWS.zy * _TriplanarScale;
                float2 uvY = positionWS.xz * _TriplanarScale;
                float2 uvZ = positionWS.xy * _TriplanarScale;

                half3 tangentNormalX = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvX),
                    _NormalScale);
                half3 tangentNormalY = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvY),
                    _NormalScale);
                half3 tangentNormalZ = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvZ),
                    _NormalScale);

                float3 axisSign = lerp(
                    -1.0,
                    1.0,
                    step(0.0, geometricNormalWS));

                float3 perturbX =
                    tangentNormalX.x * float3(0.0, 0.0, axisSign.x) +
                    tangentNormalX.y * float3(0.0, -axisSign.x, 0.0);
                float3 perturbY =
                    tangentNormalY.x * float3(axisSign.y, 0.0, 0.0) +
                    tangentNormalY.y * float3(0.0, 0.0, -axisSign.y);
                float3 perturbZ =
                    tangentNormalZ.x * float3(axisSign.z, 0.0, 0.0) +
                    tangentNormalZ.y * float3(0.0, axisSign.z, 0.0);

                float3 perturbation =
                    perturbX * blend.x +
                    perturbY * blend.y +
                    perturbZ * blend.z;

                return normalize(geometricNormalWS + perturbation);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.fogFactor =
                    ComputeFogFactor(positionInputs.positionCS.z);

                #if defined(_MAIN_LIGHT_SHADOWS) && defined(_RECEIVE_SHADOWS)
                    output.shadowCoord =
                        GetShadowCoord(positionInputs);
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 geometricNormalWS = normalize(input.normalWS);
                half3 viewDirectionWS = normalize(input.viewDirectionWS);
                float3 blend = GameTriplanarBlend(geometricNormalWS);

                half4 baseSample = GameSampleTriplanar(
                    _BaseMap,
                    sampler_BaseMap,
                    input.positionWS,
                    blend);
                half3 baseColor = baseSample.rgb * _BaseColor.rgb;

                half4 mask = GameSampleTriplanar(
                    _MaskMap,
                    sampler_MaskMap,
                    input.positionWS,
                    blend);
                half metallic = saturate(mask.r * _MetallicScale);
                half occlusion = lerp(
                    1.0h,
                    mask.g,
                    _OcclusionStrength);
                half emissionMask = mask.b;
                half smoothness = saturate(
                    mask.a * _SmoothnessScale);

                half3 normalWS = GameSampleTriplanarNormal(
                    input.positionWS,
                    geometricNormalWS,
                    blend);

                GameBRDFData brdf = GameInitializeBRDF(
                    baseColor,
                    metallic,
                    smoothness);

                #if defined(_MAIN_LIGHT_SHADOWS) && defined(_RECEIVE_SHADOWS)
                    Light mainLight = GetMainLight(input.shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif

                half mainAttenuation =
                    mainLight.distanceAttenuation *
                    mainLight.shadowAttenuation;
                half3 directLighting =
                    GameEvaluateDirectBRDF(
                        brdf,
                        normalWS,
                        viewDirectionWS,
                        mainLight.direction) *
                    mainLight.color *
                    mainAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount =
                        GetAdditionalLightsCount();

                    LIGHT_LOOP_BEGIN(additionalLightCount)
                        Light light = GetAdditionalLight(
                            lightIndex,
                            input.positionWS);
                        half attenuation =
                            light.distanceAttenuation *
                            light.shadowAttenuation;

                        directLighting +=
                            GameEvaluateDirectBRDF(
                                brdf,
                                normalWS,
                                viewDirectionWS,
                                light.direction) *
                            light.color *
                            attenuation;
                    LIGHT_LOOP_END
                #endif

                half3 diffuseGI = SampleSH(normalWS);
                half3 reflectDirectionWS =
                    reflect(-viewDirectionWS, normalWS);
                half3 specularGI = GlossyEnvironmentReflection(
                    reflectDirectionWS,
                    input.positionWS,
                    brdf.perceptualRoughness,
                    1.0h);
                half NoV = saturate(
                    abs(dot(normalWS, viewDirectionWS)) + 1e-5h);
                half3 indirectLighting = GameEvaluateIndirectBRDF(
                    brdf,
                    diffuseGI,
                    specularGI,
                    NoV);

                half3 emission =
                    _EmissionColor.rgb * emissionMask;
                half3 color =
                    (directLighting + indirectLighting) *
                    occlusion +
                    emission;

                color = MixFog(color, input.fogFactor);
                return half4(color, baseSample.a * _BaseColor.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
