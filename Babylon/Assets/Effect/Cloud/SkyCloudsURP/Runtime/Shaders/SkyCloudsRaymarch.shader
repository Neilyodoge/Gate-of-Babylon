Shader "ProjectR/Effects/SkyClouds Raymarch"
{
    Properties
    {
        [HDR] _CloudColor ("云颜色", Color) = (0.92, 0.96, 1, 1)
        [HDR] _AmbientColor ("环境散射", Color) = (0.06, 0.075, 0.1, 1)
        _Density ("体积浓度", Range(0, 4)) = 1.8
        _CloudAmount ("云量", Range(0, 1)) = 0.8
        _Extinction ("消光强度", Range(0.1, 8)) = 1.7

        [Toggle] _UseMeshSDF ("使用网格 SDF", Float) = 0
        _MeshSDFTex ("网格 SDF", 3D) = "white" {}
        _SdfSurfaceFade ("SDF 表面羽化", Range(0.001, 0.15)) = 0.02
        _SdfEdgeVariation ("SDF 边缘变化", Range(0, 0.15)) = 0.015
        _SdfEdgeFrequency ("SDF 边缘尺度", Range(0.2, 8)) = 1.2

        _ShapeRadius ("云核半径", Range(0.2, 0.45)) = 0.41
        _ShapeNoiseScale ("轮廓噪声尺度", Range(0.1, 6)) = 2.1
        _ShapeVariation ("轮廓变化", Range(0, 0.6)) = 0.35
        _EdgeSoftness ("边缘羽化", Range(0.005, 0.3)) = 0.025
        _InteriorVariation ("内部空隙", Range(0, 1)) = 0.3
        _DetailNoiseScale ("细节噪声尺度", Range(0.5, 16)) = 4.2
        _DetailStrength ("细节侵蚀", Range(0, 1)) = 0.25

        _WindDirection ("风向", Vector) = (0.7, 0.15, 0.35, 0)
        _WindSpeed ("风速", Range(0, 2)) = 0.08
        [Toggle] _WorldSpaceSampling ("世界空间采样", Float) = 0
        _WorldNoiseScale ("世界噪声缩放", Range(0.01, 2)) = 0.2

        _Anisotropy ("前向散射", Range(-0.8, 0.8)) = 0.28
        _MainLightStrength ("主光强度", Range(0, 4)) = 0.85
        _AdditionalLightStrength ("附加光强度", Range(0, 4)) = 0.8
        _SelfShadowStrength ("自阴影强度", Range(0, 1)) = 0.75
        _ShadowDensity ("自阴影浓度", Range(0, 8)) = 2.5
        _MaxAdditionalLights ("最大附加光数量", Range(0, 8)) = 4

        _StepCount ("视线步数", Range(8, 128)) = 96
        _LightStepCount ("光照步数", Range(0, 12)) = 6
        _JitterStrength ("步进抖动", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "RaymarchForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE3D(_MeshSDFTex);
            SAMPLER(sampler_MeshSDFTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColor;
                half4 _AmbientColor;
                float _Density;
                float _CloudAmount;
                float _Extinction;
                float _UseMeshSDF;
                float _SdfSurfaceFade;
                float _SdfEdgeVariation;
                float _SdfEdgeFrequency;
                float _ShapeRadius;
                float _ShapeNoiseScale;
                float _ShapeVariation;
                float _EdgeSoftness;
                float _InteriorVariation;
                float _DetailNoiseScale;
                float _DetailStrength;
                float4 _WindDirection;
                float _WindSpeed;
                float _WorldSpaceSampling;
                float _WorldNoiseScale;
                float _Anisotropy;
                float _MainLightStrength;
                float _AdditionalLightStrength;
                float _SelfShadowStrength;
                float _ShadowDensity;
                float _MaxAdditionalLights;
                float _StepCount;
                float _LightStepCount;
                float _JitterStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Mod289(float3 value)
            {
                return value - floor(value / 289.0) * 289.0;
            }

            float4 Mod289(float4 value)
            {
                return value - floor(value / 289.0) * 289.0;
            }

            float4 Permute(float4 value)
            {
                return Mod289((value * 34.0 + 1.0) * value);
            }

            float4 TaylorInverseSqrt(float4 value)
            {
                return 1.79284291400159 - value * 0.85373472095314;
            }

            float SimplexNoise3D(float3 value)
            {
                const float2 constants = float2(1.0 / 6.0, 1.0 / 3.0);
                float3 lattice = floor(value + dot(value, constants.yyy));
                float3 x0 = value - lattice + dot(lattice, constants.xxx);
                float3 comparison = step(x0.yzx, x0.xyz);
                float3 inverseComparison = 1.0 - comparison;
                float3 i1 = min(comparison.xyz, inverseComparison.zxy);
                float3 i2 = max(comparison.xyz, inverseComparison.zxy);
                float3 x1 = x0 - i1 + constants.xxx;
                float3 x2 = x0 - i2 + constants.yyy;
                float3 x3 = x0 - 0.5;
                lattice = Mod289(lattice);

                float4 permutation = Permute(
                    Permute(
                        Permute(lattice.z + float4(0, i1.z, i2.z, 1)) +
                        lattice.y + float4(0, i1.y, i2.y, 1)) +
                    lattice.x + float4(0, i1.x, i2.x, 1));
                float4 j = permutation - 49.0 * floor(permutation / 49.0);
                float4 xIndex = floor(j / 7.0);
                float4 yIndex = floor(j - 7.0 * xIndex);
                float4 x = (xIndex * 2.0 + 0.5) / 7.0 - 1.0;
                float4 y = (yIndex * 2.0 + 0.5) / 7.0 - 1.0;
                float4 h = 1.0 - abs(x) - abs(y);
                float4 b0 = float4(x.xy, y.xy);
                float4 b1 = float4(x.zw, y.zw);
                float4 s0 = floor(b0) * 2.0 + 1.0;
                float4 s1 = floor(b1) * 2.0 + 1.0;
                float4 sh = -step(h, 0.0);
                float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
                float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
                float3 g0 = float3(a0.xy, h.x);
                float3 g1 = float3(a0.zw, h.y);
                float3 g2 = float3(a1.xy, h.z);
                float3 g3 = float3(a1.zw, h.w);
                float4 normalization = TaylorInverseSqrt(float4(
                    dot(g0, g0),
                    dot(g1, g1),
                    dot(g2, g2),
                    dot(g3, g3)));
                g0 *= normalization.x;
                g1 *= normalization.y;
                g2 *= normalization.z;
                g3 *= normalization.w;

                float4 attenuation = max(
                    0.6 - float4(
                        dot(x0, x0),
                        dot(x1, x1),
                        dot(x2, x2),
                        dot(x3, x3)),
                    0.0);
                attenuation *= attenuation;
                attenuation *= attenuation;
                return 42.0 * dot(
                    attenuation,
                    float4(
                        dot(x0, g0),
                        dot(x1, g1),
                        dot(x2, g2),
                        dot(x3, g3)));
            }

            float DetailNoise(float3 position)
            {
                float noise = 0.0;
                float amplitude = 0.5714286;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    noise +=
                        (SimplexNoise3D(position) * 0.5 + 0.5) *
                        amplitude;
                    position =
                        position * 2.03 +
                        float3(17.17, 31.73, 11.91);
                    amplitude *= 0.5;
                }
                return saturate(noise);
            }

            bool IntersectProxySphere(
                float3 rayOriginOS,
                float3 rayDirectionOS,
                out float nearDistance,
                out float farDistance)
            {
                const float radius = 0.5;
                float a = dot(rayDirectionOS, rayDirectionOS);
                float b = 2.0 * dot(rayOriginOS, rayDirectionOS);
                float c = dot(rayOriginOS, rayOriginOS) - radius * radius;
                float discriminant = b * b - 4.0 * a * c;
                if (discriminant < 0.0 || a < 1e-8)
                {
                    nearDistance = 0.0;
                    farDistance = 0.0;
                    return false;
                }

                float root = sqrt(discriminant);
                float inverseDenominator = 0.5 / a;
                nearDistance = (-b - root) * inverseDenominator;
                farDistance = (-b + root) * inverseDenominator;
                return farDistance > max(nearDistance, 0.0);
            }

            bool IntersectProxyBox(
                float3 rayOriginOS,
                float3 rayDirectionOS,
                out float nearDistance,
                out float farDistance)
            {
                float3 directionSign =
                    step(0.0, rayDirectionOS) * 2.0 - 1.0;
                float3 safeDirection =
                    directionSign *
                    max(abs(rayDirectionOS), 1e-6);
                float3 first =
                    (-0.5 - rayOriginOS) / safeDirection;
                float3 second =
                    (0.5 - rayOriginOS) / safeDirection;
                float3 axisNear = min(first, second);
                float3 axisFar = max(first, second);
                nearDistance =
                    max(axisNear.x, max(axisNear.y, axisNear.z));
                farDistance =
                    min(axisFar.x, min(axisFar.y, axisFar.z));
                return farDistance > max(nearDistance, 0.0);
            }

            bool IntersectProxy(
                float3 rayOriginOS,
                float3 rayDirectionOS,
                out float nearDistance,
                out float farDistance)
            {
                if (_UseMeshSDF > 0.5)
                {
                    return IntersectProxyBox(
                        rayOriginOS,
                        rayDirectionOS,
                        nearDistance,
                        farDistance);
                }
                return IntersectProxySphere(
                    rayOriginOS,
                    rayDirectionOS,
                    nearDistance,
                    farDistance);
            }

            float3 GetNoisePosition(
                float3 positionOS,
                float3 positionWS)
            {
                float3 localPosition = positionOS * 2.0;
                float3 worldPosition =
                    positionWS * max(_WorldNoiseScale, 1e-4);
                return lerp(
                    localPosition,
                    worldPosition,
                    step(0.5, _WorldSpaceSampling));
            }

            float SampleDensity(
                float3 positionOS,
                float3 positionWS)
            {
                float3 wind =
                    _WindDirection.xyz *
                    (_Time.y * _WindSpeed);
                float3 noisePosition =
                    GetNoisePosition(positionOS, positionWS) + wind;
                float shapeNoise =
                    SimplexNoise3D(
                        noisePosition * _ShapeNoiseScale) *
                    0.5 + 0.5;
                float detailNoise =
                    DetailNoise(
                        noisePosition * _DetailNoiseScale +
                        float3(7.1, 19.7, 3.4));

                float boundary;
                if (_UseMeshSDF > 0.5)
                {
                    float3 sdfCoordinates = positionOS + 0.5;
                    if (any(sdfCoordinates < 0.0) ||
                        any(sdfCoordinates > 1.0))
                    {
                        return 0.0;
                    }

                    float signedDistance =
                        SAMPLE_TEXTURE3D_LOD(
                            _MeshSDFTex,
                            sampler_MeshSDFTex,
                            sdfCoordinates,
                            0).r;
                    float edgeNoise =
                        SimplexNoise3D(
                            noisePosition *
                            _SdfEdgeFrequency);
                    float displacedDistance =
                        signedDistance +
                        edgeNoise *
                        _SdfEdgeVariation;
                    float softness =
                        max(_SdfSurfaceFade, 1e-4);
                    boundary =
                        smoothstep(
                            -softness,
                            softness,
                            displacedDistance);
                }
                else
                {
                    float3 normalizedPosition =
                        positionOS /
                        max(_ShapeRadius, 1e-3);
                    float radialField =
                        1.0 - length(normalizedPosition);
                    if (radialField <
                        -_ShapeVariation - _EdgeSoftness)
                    {
                        return 0.0;
                    }

                    float displacedField =
                        radialField +
                        (shapeNoise - 0.5) *
                        _ShapeVariation;
                    boundary =
                        smoothstep(
                            -_EdgeSoftness,
                            _EdgeSoftness,
                            displacedField);
                }

                float distribution =
                    shapeNoise * 0.68 +
                    detailNoise * 0.32;
                float threshold = lerp(0.78, 0.18, _CloudAmount);
                float interiorNoise =
                    smoothstep(
                        threshold,
                        min(threshold + 0.28, 1.0),
                        distribution);
                float interior =
                    lerp(
                        1.0,
                        interiorNoise,
                        _InteriorVariation);
                float erosion =
                    lerp(
                        1.0,
                        smoothstep(0.22, 0.78, detailNoise),
                        _DetailStrength);

                return
                    boundary *
                    interior *
                    erosion *
                    _Density;
            }

            float HenyeyGreenstein(float cosineAngle, float anisotropy)
            {
                float g = clamp(anisotropy, -0.8, 0.8);
                float gSquared = g * g;
                float denominator = max(
                    1.0 + gSquared - 2.0 * g * cosineAngle,
                    1e-3);
                return
                    (1.0 - gSquared) /
                    (4.0 * PI * pow(denominator, 1.5));
            }

            float MarchLightTransmittance(
                float3 positionOS,
                float3 positionWS,
                float3 lightDirectionWS)
            {
                int lightSteps =
                    clamp((int)_LightStepCount, 0, 12);
                if (lightSteps <= 0 || _SelfShadowStrength <= 0.001)
                {
                    return 1.0;
                }

                float3 directionWS = SafeNormalize(lightDirectionWS);
                float3 directionOS =
                    mul((float3x3)unity_WorldToObject, directionWS);
                float nearDistance;
                float farDistance;
                if (!IntersectProxy(
                    positionOS + directionOS * 1e-3,
                    directionOS,
                    nearDistance,
                    farDistance))
                {
                    return 1.0;
                }

                float stepLength =
                    max(farDistance, 0.0) / lightSteps;
                float opticalDepth = 0.0;
                [loop]
                for (int stepIndex = 0;
                     stepIndex < lightSteps;
                     stepIndex++)
                {
                    float distanceAlongLight =
                        (stepIndex + 0.5) * stepLength;
                    float3 sampleOS =
                        positionOS +
                        directionOS * distanceAlongLight;
                    float3 sampleWS =
                        positionWS +
                        directionWS * distanceAlongLight;
                    opticalDepth +=
                        SampleDensity(sampleOS, sampleWS) *
                        stepLength *
                        _ShadowDensity;
                }

                float transmittance = exp(-opticalDepth);
                return lerp(
                    1.0,
                    transmittance,
                    _SelfShadowStrength);
            }

            float Hash12(float2 value)
            {
                float3 p3 = frac(value.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 cameraPositionWS = GetCameraPositionWS();
                float3 rayDirectionWS =
                    SafeNormalize(input.positionWS - cameraPositionWS);
                float3 rayOriginOS =
                    TransformWorldToObject(cameraPositionWS);
                float3 rayDirectionOS =
                    mul(
                        (float3x3)unity_WorldToObject,
                        rayDirectionWS);

                float nearDistance;
                float farDistance;
                if (!IntersectProxy(
                    rayOriginOS,
                    rayDirectionOS,
                    nearDistance,
                    farDistance))
                {
                    discard;
                }
                nearDistance = max(nearDistance, 0.0);

                float2 screenUV =
                    GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    bool hasSceneDepth = sceneDepth > 1e-5;
                #else
                    bool hasSceneDepth = sceneDepth < 0.99999;
                #endif
                if (hasSceneDepth)
                {
                    float3 scenePositionWS =
                        ComputeWorldSpacePosition(
                            screenUV,
                            sceneDepth,
                            UNITY_MATRIX_I_VP);
                    float distanceToScene =
                        dot(
                            scenePositionWS - cameraPositionWS,
                            rayDirectionWS);
                    farDistance = min(
                        farDistance,
                        distanceToScene);
                }

                if (farDistance <= nearDistance)
                {
                    discard;
                }

                int stepCount =
                    clamp((int)_StepCount, 8, 128);
                float stepLength =
                    (farDistance - nearDistance) / stepCount;
                float jitter =
                    Hash12(input.positionCS.xy) *
                    saturate(_JitterStrength);
                float currentDistance =
                    nearDistance +
                    stepLength * jitter;

                half3 accumulatedColor = 0.0h;
                float transmittance = 1.0;

                [loop]
                for (int stepIndex = 0;
                     stepIndex < stepCount;
                     stepIndex++)
                {
                    if (currentDistance >= farDistance ||
                        transmittance < 0.01)
                    {
                        break;
                    }

                    float3 sampleWS =
                        cameraPositionWS +
                        rayDirectionWS * currentDistance;
                    float3 sampleOS =
                        rayOriginOS +
                        rayDirectionOS * currentDistance;
                    float density =
                        SampleDensity(sampleOS, sampleWS);

                    if (density > 1e-4)
                    {
                        half3 lighting = _AmbientColor.rgb;
                        Light mainLight =
                            GetMainLight(
                                TransformWorldToShadowCoord(sampleWS));
                        float mainSelfShadow =
                            MarchLightTransmittance(
                                sampleOS,
                                sampleWS,
                                mainLight.direction);
                        float mainPhase =
                            HenyeyGreenstein(
                                dot(
                                    -rayDirectionWS,
                                    mainLight.direction),
                                _Anisotropy) *
                            (4.0 * PI);
                        lighting +=
                            mainLight.color *
                            mainLight.distanceAttenuation *
                            mainLight.shadowAttenuation *
                            mainSelfShadow *
                            mainPhase *
                            _MainLightStrength;

                        #if defined(_ADDITIONAL_LIGHTS)
                            uint lightCount = min(
                                GetAdditionalLightsCount(),
                                (uint)max(
                                    _MaxAdditionalLights,
                                    0));
                            LIGHT_LOOP_BEGIN(lightCount)
                                Light additionalLight =
                                    GetAdditionalLight(
                                        lightIndex,
                                        sampleWS);
                                float additionalSelfShadow =
                                    MarchLightTransmittance(
                                        sampleOS,
                                        sampleWS,
                                        additionalLight.direction);
                                float additionalPhase =
                                    HenyeyGreenstein(
                                        dot(
                                            -rayDirectionWS,
                                            additionalLight.direction),
                                        _Anisotropy) *
                                    (4.0 * PI);
                                lighting +=
                                    additionalLight.color *
                                    additionalLight.distanceAttenuation *
                                    additionalLight.shadowAttenuation *
                                    additionalSelfShadow *
                                    additionalPhase *
                                    _AdditionalLightStrength;
                            LIGHT_LOOP_END
                        #endif

                        float sampleOpacity =
                            1.0 -
                            exp(
                                -density *
                                stepLength *
                                _Extinction);
                        accumulatedColor +=
                            transmittance *
                            sampleOpacity *
                            lighting *
                            _CloudColor.rgb;
                        transmittance *=
                            1.0 - sampleOpacity;
                    }

                    currentDistance += stepLength;
                }

                half alpha =
                    saturate(1.0 - transmittance);
                clip(alpha - 1e-4);
                return half4(accumulatedColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
