Shader "ProjectR/Effects/Local Volumetric Cloud"
{
    Properties
    {
        [NoScaleOffset] _ShapeNoiseTex ("基础形状纹理", 3D) = "white" {}
        [NoScaleOffset] _DetailNoiseTex ("细节纹理", 3D) = "white" {}
        [NoScaleOffset] _WeatherMap ("天气图", 2D) = "white" {}
        [NoScaleOffset] _MeshSDFTex ("网格 SDF", 3D) = "white" {}
        [Toggle] _UseMeshSDF ("使用网格 SDF 边界", Float) = 0
        _MeshSurfaceFade ("网格表面羽化", Range(0.001, 0.25)) = 0.035
        _MeshEdgeVariation ("网格边缘变化", Range(0, 0.15)) = 0.02
        _MeshEdgeFrequency ("网格边缘尺度", Range(0.2, 4)) = 0.8
        _MeshInteriorVariation ("网格内部变化", Range(0, 1)) = 0.2
        [Toggle] _UseCloudBlob ("使用云团边界", Float) = 0
        _BlobEdgeVariation ("云团边缘变化", Range(0, 0.5)) = 0.18
        _BlobEdgeFrequency ("云团边缘尺度", Range(0.2, 4)) = 0.65
        _BlobEdgeSoftness ("云团边缘羽化", Range(0.005, 0.3)) = 0.07
        _BlobInteriorVariation ("云团内部变化", Range(0, 1)) = 0.25
        [HDR] _CloudColor ("云颜色", Color) = (1, 1, 1, 1)
        [HDR] _ShadowColor ("阴影颜色", Color) = (0.18, 0.22, 0.3, 1)
        _CloudAmount ("云量", Range(0, 1)) = 1
        _Density ("密度", Range(0, 8)) = 1.2
        _Threshold ("形状阈值", Range(0, 0.95)) = 0.48
        _NoiseScale ("噪声缩放", Range(0.25, 8)) = 1
        [Toggle] _WorldSpaceSampling ("使用世界空间采样", Float) = 0
        _BaseNoiseWorldSize ("基础噪声世界尺寸", Float) = 60
        _DetailFrequency ("细节频率", Range(1, 16)) = 5
        _DetailStrength ("细节侵蚀强度", Range(0, 1)) = 0.25
        _WeatherInfluence ("天气图影响", Range(0, 1)) = 0.45
        _WeatherWorldSize ("天气图世界尺寸", Float) = 180
        _CurlStrength ("卷曲扰动", Range(0, 2)) = 0.18
        _EdgeFade ("边缘羽化", Range(0.001, 0.5)) = 0.12
        _Extinction ("消光强度", Range(0.01, 8)) = 0.9
        _Ambient ("环境光强度", Range(0, 1)) = 0.1
        _Anisotropy ("前向散射", Range(-0.8, 0.8)) = 0.3
        [Toggle] _EnableAdditionalLights ("启用局部光源", Float) = 1
        _AdditionalLightStrength ("局部光源强度", Range(0, 4)) = 1
        _MaxAdditionalLights ("最大局部光源数", Range(0, 8)) = 4
        _ViewStepLength ("精细步进距离", Float) = 0.35
        _StepCount ("最大视线步数", Range(16, 256)) = 192
        _EmptyStepMultiplier ("空区域步进倍率", Range(1, 8)) = 2
        _LightStepCount ("光照步进次数", Range(1, 16)) = 4
        _Jitter ("步进抖动", Range(0, 1)) = 0.1
        _WindDirection ("风向（局部空间）", Vector) = (1, 0, 0, 0)
        _WindSpeed ("风速", Float) = 0.01

        [HideInInspector] _InteractionCenter ("Interaction Center", Vector) = (0, -100000, 0, 0)
        [HideInInspector] _InteractionRadius ("Interaction Radius", Float) = 0
        [HideInInspector] _InteractionSoftness ("Interaction Softness", Float) = 0.5
        [HideInInspector] _InteractionAmount ("Interaction Amount", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "LocalVolumetricCloud"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE3D(_ShapeNoiseTex);
            SAMPLER(sampler_ShapeNoiseTex);
            TEXTURE3D(_DetailNoiseTex);
            SAMPLER(sampler_DetailNoiseTex);
            TEXTURE2D(_WeatherMap);
            SAMPLER(sampler_WeatherMap);
            TEXTURE3D(_MeshSDFTex);
            SAMPLER(sampler_MeshSDFTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColor;
                half4 _ShadowColor;
                float _CloudAmount;
                float _Density;
                float _Threshold;
                float _NoiseScale;
                float _WorldSpaceSampling;
                float _BaseNoiseWorldSize;
                float _DetailFrequency;
                float _DetailStrength;
                float _WeatherInfluence;
                float _WeatherWorldSize;
                float _CurlStrength;
                float _EdgeFade;
                float _Extinction;
                float _Ambient;
                float _Anisotropy;
                float _EnableAdditionalLights;
                float _AdditionalLightStrength;
                int _MaxAdditionalLights;
                float _ViewStepLength;
                int _StepCount;
                float _EmptyStepMultiplier;
                int _LightStepCount;
                float _Jitter;
                float4 _WindDirection;
                float _WindSpeed;
                float4 _InteractionCenter;
                float _InteractionRadius;
                float _InteractionSoftness;
                float _InteractionAmount;
                float4 _NoiseResolution;
                float _UseMeshSDF;
                float _MeshSurfaceFade;
                float _MeshEdgeVariation;
                float _MeshEdgeFrequency;
                float _MeshInteriorVariation;
                float _UseCloudBlob;
                float _BlobEdgeVariation;
                float _BlobEdgeFrequency;
                float _BlobEdgeSoftness;
                float _BlobInteriorVariation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float SafeDirection(float value)
            {
                return value >= 0.0 ? max(value, 1e-5) : min(value, -1e-5);
            }

            // The proxy is a standard Unity cube with local bounds [-0.5, 0.5].
            float2 RayBoxDistance(float3 rayOriginOS, float3 rayDirectionOS)
            {
                float3 safeDirection = float3(
                    SafeDirection(rayDirectionOS.x),
                    SafeDirection(rayDirectionOS.y),
                    SafeDirection(rayDirectionOS.z));
                float3 inverseDirection = rcp(safeDirection);
                float3 nearPlane = (-0.5 - rayOriginOS) * inverseDirection;
                float3 farPlane = (0.5 - rayOriginOS) * inverseDirection;
                float3 tMin = min(nearPlane, farPlane);
                float3 tMax = max(nearPlane, farPlane);
                float entry = max(max(tMin.x, tMin.y), tMin.z);
                float exit = min(min(tMax.x, tMax.y), tMax.z);
                return float2(max(entry, 0.0), max(exit, 0.0));
            }

            float EdgeMask(float3 positionOS)
            {
                float3 edgeDistance = 0.5 - abs(positionOS);
                return smoothstep(0.0, max(_EdgeFade, 1e-4), min(edgeDistance.x, min(edgeDistance.y, edgeDistance.z)));
            }

            float BoundaryMask(float3 positionOS, float boundaryNoise)
            {
                if (_UseMeshSDF > 0.5)
                {
                    float3 sdfCoordinates = saturate(positionOS + 0.5);
                    float signedDistance = SAMPLE_TEXTURE3D_LOD(
                        _MeshSDFTex, sampler_MeshSDFTex, sdfCoordinates, 0).r;
                    float edgeDisplacement =
                        (boundaryNoise * 2.0 - 1.0) * _MeshEdgeVariation;
                    float softness = max(_MeshSurfaceFade, 1e-4);
                    return smoothstep(
                        -softness,
                        softness,
                        signedDistance + edgeDisplacement);
                }

                if (_UseCloudBlob > 0.5)
                {
                    float ellipsoidDistance = 1.0 - length(positionOS * 2.0);
                    float edgeDisplacement =
                        (boundaryNoise * 2.0 - 1.0) * _BlobEdgeVariation;
                    float softness = max(_BlobEdgeSoftness, 1e-4);
                    return smoothstep(-softness, softness, ellipsoidDistance + edgeDisplacement);
                }

                return EdgeMask(positionOS);
            }

            float CloudRemap01(float value, float sourceMinimum, float sourceMaximum)
            {
                return saturate((value - sourceMinimum) / max(sourceMaximum - sourceMinimum, 1e-4));
            }

            float CalculateNoiseLod(float normalizedFootprint, float resolution)
            {
                float texelFootprint = max(normalizedFootprint * resolution, 1.0);
                return clamp(log2(texelFootprint) - 0.5, 0.0, log2(max(resolution, 1.0)));
            }

            float SampleDensity(
                float3 positionWS,
                float3 positionOS,
                bool cheapSample,
                float sampleFootprintWS,
                float3 sampleDirectionWS)
            {
                float3 windOffset = _WindDirection.xyz * (_Time.y * _WindSpeed);
                float3 localSamplePosition = positionOS + 0.5;
                float useWorldSpace = saturate(_WorldSpaceSampling);
                // Zoom around the texture center so values below one do not collapse
                // the whole proxy into an arbitrary corner of the volume texture.
                float3 localCoordinates = (localSamplePosition - 0.5) * _NoiseScale + 0.5;
                float3 worldCoordinates = positionWS / max(_BaseNoiseWorldSize, 0.1);
                float3 baseCoordinates = lerp(localCoordinates, worldCoordinates, useWorldSpace) + windOffset;

                float3 footprintOS = mul(
                    (float3x3)unity_WorldToObject,
                    normalize(sampleDirectionWS) * sampleFootprintWS);
                float localBaseFootprint = length(footprintOS) * _NoiseScale;
                float worldBaseFootprint = sampleFootprintWS / max(_BaseNoiseWorldSize, 0.1);
                float baseFootprint = lerp(localBaseFootprint, worldBaseFootprint, useWorldSpace);
                float baseLod = CalculateNoiseLod(baseFootprint, _NoiseResolution.x);

                float2 localWeatherUV = localSamplePosition.xz;
                float2 worldWeatherUV = positionWS.xz / max(_WeatherWorldSize, 1.0);
                float2 weatherUV = lerp(localWeatherUV, worldWeatherUV, useWorldSpace) + windOffset.xz * 0.15;
                float localWeatherFootprint = length(footprintOS.xz);
                float worldWeatherFootprint =
                    length(normalize(sampleDirectionWS).xz) * sampleFootprintWS / max(_WeatherWorldSize, 1.0);
                float weatherFootprint = lerp(localWeatherFootprint, worldWeatherFootprint, useWorldSpace);
                float weatherLod = CalculateNoiseLod(weatherFootprint, _NoiseResolution.z);
                float4 weather = SAMPLE_TEXTURE2D_LOD(
                    _WeatherMap, sampler_WeatherMap, frac(weatherUV), weatherLod);

                float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(
                    _ShapeNoiseTex, sampler_ShapeNoiseTex, frac(baseCoordinates), baseLod);
                float lowFrequencyFbm = dot(shapeNoise.gba, float3(0.625, 0.25, 0.125));
                float shape = CloudRemap01(shapeNoise.r, -(1.0 - lowFrequencyFbm), 1.0);
                float boundaryNoise = shapeNoise.r;
                if (_UseCloudBlob > 0.5 || _UseMeshSDF > 0.5)
                {
                    float boundaryFrequency =
                        _UseMeshSDF > 0.5 ? _MeshEdgeFrequency : _BlobEdgeFrequency;
                    float3 boundaryCoordinates =
                        (localSamplePosition - 0.5) * boundaryFrequency + 0.5 +
                        windOffset * 0.25;
                    float boundaryLod = CalculateNoiseLod(
                        localBaseFootprint * boundaryFrequency, _NoiseResolution.x);
                    boundaryNoise = SAMPLE_TEXTURE3D_LOD(
                        _ShapeNoiseTex,
                        sampler_ShapeNoiseTex,
                        frac(boundaryCoordinates),
                        boundaryLod).r;
                }

                float coverage = lerp(1.0, weather.r, _WeatherInfluence);
                // Weather should vary the macro coverage, but must not erase an
                // entire local proxy just because its sampled patch is dark.
                float missingAmount = 1.0 - saturate(_CloudAmount);
                float coverageThreshold =
                    _Threshold +
                    missingAmount * missingAmount * (1.05 - _Threshold) +
                    (1.0 - coverage) * 0.25;
                shape = CloudRemap01(shape, coverageThreshold, 1.0);

                if (!cheapSample && shape > 0.0 && _DetailStrength > 0.0)
                {
                    float2 curl = weather.gb * 2.0 - 1.0;
                    float3 detailCoordinates = baseCoordinates * _DetailFrequency + windOffset * 0.7;
                    detailCoordinates.xz += curl * _CurlStrength;
                    float detailLod = CalculateNoiseLod(
                        baseFootprint * _DetailFrequency, _NoiseResolution.y);
                    float3 detailNoise = SAMPLE_TEXTURE3D_LOD(
                        _DetailNoiseTex, sampler_DetailNoiseTex, frac(detailCoordinates), detailLod).rgb;
                    float detailFbm = dot(detailNoise, float3(0.625, 0.25, 0.125));
                    float erosion = (1.0 - detailFbm) * _DetailStrength;
                    shape = CloudRemap01(shape, erosion, 1.0);
                }

                if (_UseCloudBlob > 0.5)
                {
                    shape = lerp(1.0, shape, _BlobInteriorVariation);
                }
                else if (_UseMeshSDF > 0.5)
                {
                    shape = lerp(1.0, shape, _MeshInteriorVariation);
                }

                float density = shape * BoundaryMask(positionOS, boundaryNoise) * _Density;
                if (_InteractionAmount > 0.0 && _InteractionRadius > 0.0)
                {
                    float distanceToInteraction = distance(positionWS, _InteractionCenter.xyz);
                    float innerRadius = max(0.0, _InteractionRadius - _InteractionSoftness);
                    float clearing = 1.0 - smoothstep(innerRadius, _InteractionRadius, distanceToInteraction);
                    density *= 1.0 - saturate(clearing * _InteractionAmount);
                }
                return density;
            }

            float HenyeyGreenstein(float cosAngle, float anisotropy)
            {
                float g2 = anisotropy * anisotropy;
                float denominatorBase = max(1.0 + g2 - 2.0 * anisotropy * cosAngle, 1e-4);
                return (1.0 - g2) / max(4.0 * PI * pow(denominatorBase, 1.5), 1e-4);
            }

            float Hash12(float2 value)
            {
                float3 p3 = frac(value.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float LightTransmittance(float3 positionWS, Light mainLight)
            {
                int lightSteps = clamp(_LightStepCount, 1, 16);
                float3 lightDirectionWS = normalize(mainLight.direction);
                float3 positionOS = TransformWorldToObject(positionWS);
                float3 lightDirectionOS = mul((float3x3)unity_WorldToObject, lightDirectionWS);
                float distanceToExit = RayBoxDistance(positionOS, lightDirectionOS).y;
                float stepLength = distanceToExit / lightSteps;
                float normalizedStepLength = length(lightDirectionOS * stepLength);
                float opticalDepth = 0.0;

                [loop]
                for (int index = 0; index < 16; index++)
                {
                    if (index >= lightSteps)
                        break;

                    float sampleDistance = (index + 0.5) * stepLength;
                    float3 sampleWS = positionWS + lightDirectionWS * sampleDistance;
                    float3 sampleOS = TransformWorldToObject(sampleWS);
                    opticalDepth += SampleDensity(
                        sampleWS,
                        sampleOS,
                        true,
                        stepLength,
                        lightDirectionWS) * normalizedStepLength;
                }
                return exp(-opticalDepth * _Extinction);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 cameraWS = GetCameraPositionWS();
                float3 rayDirectionWS = normalize(input.positionWS - cameraWS);
                float3 rayOriginOS = TransformWorldToObject(cameraWS);
                float3 rayDirectionOS = mul((float3x3)unity_WorldToObject, rayDirectionWS);
                float2 boxDistance = RayBoxDistance(rayOriginOS, rayDirectionOS);

                float rawDepth = SampleSceneDepth(screenUV);
                float3 opaquePositionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float opaqueDistance = dot(opaquePositionWS - cameraWS, rayDirectionWS);
                float rayEnd = min(boxDistance.y, max(opaqueDistance, 0.0));
                float rayLength = rayEnd - boxDistance.x;
                if (rayLength <= 0.0)
                    return 0;

                int maximumViewSteps = clamp(_StepCount, 16, 256);
                float stepLength = max(_ViewStepLength, rayLength / maximumViewSteps);
                float emptyStepLength = stepLength * max(_EmptyStepMultiplier, 1.0);
                float normalizedStepLength = length(rayDirectionOS * stepLength);
                float jitter = lerp(0.5, Hash12(input.positionCS.xy), _Jitter);
                float travel = boxDistance.x + jitter * stepLength;

                Light mainLight = GetMainLight();
                float phase = HenyeyGreenstein(dot(rayDirectionWS, mainLight.direction), _Anisotropy);
                float phaseScale = phase * 4.0 * PI;
                float transmittance = 1.0;
                float3 accumulatedColor = 0.0;

                [loop]
                for (int index = 0; index < 256; index++)
                {
                    if (index >= maximumViewSteps || travel >= rayEnd || transmittance < 0.01)
                        break;

                    float3 sampleWS = cameraWS + rayDirectionWS * travel;
                    float3 sampleOS = TransformWorldToObject(sampleWS);
                    float pixelFootprintWS =
                        2.0 * max(travel, 0.0) /
                        max(_ScaledScreenParams.y * abs(UNITY_MATRIX_P._m11), 1.0);
                    float sampleFootprintWS = max(stepLength, pixelFootprintWS);
                    float density = SampleDensity(
                        sampleWS, sampleOS, false, sampleFootprintWS, rayDirectionWS);
                    if (density > 1e-4)
                    {
                        float sunVisibility = LightTransmittance(sampleWS, mainLight);
                        float3 directLight = lerp(_ShadowColor.rgb, mainLight.color, sunVisibility);
                        float3 lighting = _Ambient * _CloudColor.rgb + directLight * phaseScale * _CloudColor.rgb;

                        #if defined(_ADDITIONAL_LIGHTS)
                            if (_EnableAdditionalLights > 0.5 && _MaxAdditionalLights > 0)
                            {
                                uint additionalLightCount = min(
                                    GetAdditionalLightsCount(),
                                    (uint)_MaxAdditionalLights);

                                LIGHT_LOOP_BEGIN(additionalLightCount)
                                    Light additionalLight = GetAdditionalLight(lightIndex, sampleWS);
                                    float attenuation =
                                        additionalLight.distanceAttenuation *
                                        additionalLight.shadowAttenuation;
                                    float additionalPhase = HenyeyGreenstein(
                                        dot(rayDirectionWS, additionalLight.direction),
                                        _Anisotropy) * 4.0 * PI;
                                    lighting +=
                                        additionalLight.color *
                                        attenuation *
                                        additionalPhase *
                                        _AdditionalLightStrength *
                                        _CloudColor.rgb;
                                LIGHT_LOOP_END
                            }
                        #endif

                        float sampleAlpha =
                            1.0 - exp(-density * normalizedStepLength * _Extinction);
                        accumulatedColor += transmittance * sampleAlpha * lighting;
                        transmittance *= 1.0 - sampleAlpha;
                        travel += stepLength;
                    }
                    else
                    {
                        float probeTravel = min(travel + emptyStepLength * 0.5, rayEnd);
                        float3 probeWS = cameraWS + rayDirectionWS * probeTravel;
                        float3 probeOS = TransformWorldToObject(probeWS);
                        float probePixelFootprintWS =
                            2.0 * max(probeTravel, 0.0) /
                            max(_ScaledScreenParams.y * abs(UNITY_MATRIX_P._m11), 1.0);
                        float probeDensity = SampleDensity(
                            probeWS,
                            probeOS,
                            true,
                            max(stepLength, probePixelFootprintWS),
                            rayDirectionWS);
                        travel += probeDensity > 1e-4 ? stepLength : emptyStepLength;
                    }
                }

                return half4(accumulatedColor, 1.0 - transmittance);
            }
            ENDHLSL
        }
    }
}
