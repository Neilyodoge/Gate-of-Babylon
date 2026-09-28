// Handwritten URP 14 implementation replacing the Amplify-generated source.
// Existing property names are retained so shipped materials keep their values.
// The mobile, 2D and "_n" shader variants remain separate assets.
Shader "SkyClouds"
{
    Properties
    {
        [HideInInspector] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _TranslucencyColor ("Translucency Color", Color) = (1, 1, 1, 0)
        _EmmissiveColor ("Emmissive Color", Color) = (0.5490196, 0.627451, 0.7294118, 0)
        _Scale ("Scale", Float) = 1
        _WindDirection ("WindDirection", Vector) = (-1, -0.3, -0.3, 0)
        _WindSpeed ("WindSpeed", Float) = 1
        _Mask0 ("Mask0", Vector) = (1.985208, 0.440832, 0.2055923, 1.2)
        _Mask1 ("Mask1", Vector) = (1.985208, 0.440832, 0.2055923, 1.2)
        _DepthDistance ("Depth Distance", Float) = 0.4
        _NoiseColorStrength ("Noise Color Strength", Range(0, 1)) = 0.5565214
        _EmmissiveNoiseColor ("Emmissive Noise Color", Range(0, 1)) = 0.2000003
        _NoiseSpeed ("Noise Speed", Float) = 0.2
        _VorSpeed ("Vor Speed", Float) = 0.05
        _AdditionalNoiseDir ("Additional Noise Dir", Vector) = (0, 0, 0, 0)
        _AdditionalVoronoiDir ("AdditionalVoronoiDir", Vector) = (0.7, -0.3, -0.3, 0)
        _VoronoiScale ("Voronoi Scale", Float) = 1.4
        _NoiseScale ("Noise Scale", Float) = 20
        _VoronoiStrength ("Voronoi Strength", Float) = 0.9
        _NoiseStrength ("Noise Strength", Float) = 0.03
        _NoiseDisplacement ("Noise Displacement", Float) = 0.5
        _NoiseScale3D ("Noise Scale 3D", Vector) = (1, 0.2, 0.6, 0)
        _EmmissiveDepthFade ("Emmissive Depth Fade", Range(0, 1)) = 0.2086951
        _NoiseColorMix ("NoiseColorMix", Range(0, 1)) = 0.96
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 0.7030082
        _TransStrength ("Strength", Range(0, 50)) = 1
        _TransNormal ("Normal Distortion", Range(0, 1)) = 0.5
        _TransScattering ("Scattering", Range(1, 50)) = 2
        _TransDirect ("Direct", Range(0, 1)) = 0.9
        _TransAmbient ("Ambient", Range(0, 1)) = 0.1
        _TransShadow ("Shadow", Range(0, 1)) = 0.5
        _FakeVolume ("假体积-内部体积", Range(0, 1)) = 0
        _FakeVolumeDepth ("假体积-穿透深度", Range(0, 2)) = 0.35
        _DisplaceSmoothing ("法线外扩平滑", Range(0, 1)) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("深度写入", Float) = 1
        _ShadowOpacity ("Cast Shadow Opacity", Range(0, 1)) = 0.35
        _TessPhongStrength ("Phong Tess Strength", Range(0, 1)) = 0.5
        _TessEdgeLength ("Edge length", Range(2, 50)) = 16
        [HideInInspector][ToggleOff] _SpecularHighlights ("Specular Highlights", Float) = 1
        [HideInInspector][ToggleOff] _EnvironmentReflections ("Environment Reflections", Float) = 1
        [HideInInspector][ToggleOff] _ReceiveShadows ("Receive Shadows", Float) = 1
        [HideInInspector] _QueueOffset ("Queue Offset", Float) = 0
        [HideInInspector] _QueueControl ("Queue Control", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TranslucencyColor;
            half4 _EmmissiveColor;
            float _Scale;
            float4 _WindDirection;
            float _WindSpeed;
            float4 _AdditionalNoiseDir;
            float4 _AdditionalVoronoiDir;
            float _NoiseScale;
            float4 _NoiseScale3D;
            float _NoiseStrength;
            float _NoiseSpeed;
            float _NoiseDisplacement;
            float _VoronoiScale;
            float _VoronoiStrength;
            float _VorSpeed;
            float _NoiseColorStrength;
            float _NoiseColorMix;
            float _EmmissiveNoiseColor;
            float _EmmissiveDepthFade;
            float _VertexColorStrength;
            float _DepthDistance;
            float _TransStrength;
            float _TransNormal;
            float _TransScattering;
            float _TransDirect;
            float _TransAmbient;
            float _TransShadow;
            float _FakeVolume;
            float _FakeVolumeDepth;
            float _DisplaceSmoothing;
            float _ShadowOpacity;
            float _TessPhongStrength;
            float _TessEdgeLength;
            float4 _Mask0;
            float4 _Mask1;
            float _AlphaCutoff;
            float _TessMaxDisp;
            float _QueueOffset;
            float _QueueControl;
        CBUFFER_END

        // Filled by URP's ShadowUtils for each shadow-casting light.
        float3 _LightDirection;
        float3 _LightPosition;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TessControlPoint
        {
            float4 positionOS : INTERNALTESSPOS;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct TessellationFactors
        {
            float edge[3] : SV_TessFactor;
            float inside : SV_InsideTessFactor;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 color : TEXCOORD2;
            float4 screenPosition : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct ShadowVaryings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
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
                    Permute(lattice.z + float4(0.0, i1.z, i2.z, 1.0)) +
                    lattice.y + float4(0.0, i1.y, i2.y, 1.0)) +
                lattice.x + float4(0.0, i1.x, i2.x, 1.0));
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
            float4 gradients = float4(
                dot(x0, g0),
                dot(x1, g1),
                dot(x2, g2),
                dot(x3, g3));
            return 42.0 * dot(attenuation, gradients);
        }

        float2 VoronoiHash(float2 value)
        {
            value = float2(
                dot(value, float2(127.1, 311.7)),
                dot(value, float2(269.5, 183.3)));
            return frac(sin(value) * 43758.5453);
        }

        float VoronoiF1(float2 coordinates, float phase)
        {
            float2 cell = floor(coordinates);
            float2 local = frac(coordinates);
            float nearest = 8.0;

            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                [unroll]
                for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 feature = VoronoiHash(cell + offset);
                    feature =
                        sin(phase + feature * 6.2831) * 0.5 + 0.5;
                    float2 delta = offset + feature - local;
                    nearest = min(nearest, 0.5 * dot(delta, delta));
                }
            }
            return nearest;
        }

        float VoronoiFbm(float2 coordinates, float phase)
        {
            float result = 0.0;
            float weight = 0.5;
            float totalWeight = 0.0;

            [unroll]
            for (int octave = 0; octave < 3; octave++)
            {
                result += VoronoiF1(coordinates, phase) * weight;
                totalWeight += weight;
                coordinates *= 2.0;
                weight *= 0.5;
            }
            return result / max(totalWeight, 1e-4);
        }

        float2 EvaluateCloudNoise(float3 positionWS, float3 normalWS)
        {
            float scale = max(abs(_Scale), 1e-4);
            float3 windDirection = -_WindDirection.xyz;
            float windWeightSum =
                abs(windDirection.x) +
                abs(windDirection.y) +
                abs(windDirection.z);
            float3 windWeights = windWeightSum > 1e-5
                ? abs(windDirection / windWeightSum)
                : float3(1.0, 0.0, 0.0);
            float3 scaleXYZ = _NoiseScale3D.xyz;
            float3 scaleYXZ = _NoiseScale3D.yxz;
            float3 scaleYZX = _NoiseScale3D.yzx;
            float3 directionalNoiseScale =
                scaleXYZ * windWeights.x +
                scaleYXZ * windWeights.y +
                scaleYZX * windWeights.z;
            float3 noiseOffset =
                (windDirection + _AdditionalNoiseDir.xyz) *
                (_Time.y * _NoiseSpeed * _WindSpeed);
            float3 noiseCoordinates =
                directionalNoiseScale *
                (positionWS + noiseOffset) *
                (_NoiseScale / scale);
            float perlin = saturate(SimplexNoise3D(noiseCoordinates) * 0.5 + 0.5);

            float3 voronoiOffset =
                (windDirection + _AdditionalVoronoiDir.xyz) *
                (_Time.y * _VorSpeed * _WindSpeed);
            float3 voronoiPosition = positionWS + voronoiOffset;
            float voronoiScale = _VoronoiScale / scale;
            float3 weights = pow(abs(normalWS), 5.0);
            weights /= max(weights.x + weights.y + weights.z, 1e-4);

            float yz = VoronoiFbm(voronoiPosition.zy * voronoiScale, 0.0);
            float zx = VoronoiFbm(voronoiPosition.zx * voronoiScale, 217.0);
            float xy = VoronoiFbm(voronoiPosition.xy * voronoiScale, 137.0);
            float triplanarVoronoi = yz * weights.x + zx * weights.y + xy * weights.z;
            float voronoi = saturate(((1.0 - triplanarVoronoi) - 0.65) / 0.35);
            return float2(perlin, voronoi);
        }

        float3 ApplySphereMask(float3 positionWS, float3 normalWS, float4 mask)
        {
            float penetration = max(mask.w - distance(positionWS, mask.xyz), 0.0);
            return positionWS - normalWS * penetration;
        }

        float3 DisplacePosition(float3 positionOS, float3 normalOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 normalWS = TransformObjectToWorldNormal(normalOS);
            float3 objectCenterWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float3 radialWS = SafeNormalize(positionWS - objectCenterWS);
            float3 displaceDirectionWS =
                SafeNormalize(lerp(normalWS, radialWS, _DisplaceSmoothing));
            float2 noise = EvaluateCloudNoise(positionWS, normalWS);
            float displacementNoise = saturate(
                noise.x * _NoiseStrength +
                noise.y * _VoronoiStrength);
            positionWS += displaceDirectionWS *
                ((displacementNoise - 0.5) * _NoiseDisplacement * max(abs(_Scale), 1e-4));
            positionWS = ApplySphereMask(positionWS, normalWS, _Mask0);
            positionWS = ApplySphereMask(positionWS, normalWS, _Mask1);
            return TransformWorldToObject(positionWS);
        }

        TessControlPoint TessellationVertex(Attributes input)
        {
            TessControlPoint output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionOS = input.positionOS;
            output.normalOS = input.normalOS;
            output.color = input.color;
            return output;
        }

        float CalculateEdgeTessellation(float3 firstWS, float3 secondWS)
        {
            float distanceToCamera = max(
                distance((firstWS + secondWS) * 0.5, GetCameraPositionWS()),
                1e-3);
            float edgeLength = distance(firstWS, secondWS);
            return clamp(
                edgeLength * _ScreenParams.y /
                (max(_TessEdgeLength, 1.0) * distanceToCamera),
                1.0,
                32.0);
        }

        TessellationFactors PatchConstantFunction(
            InputPatch<TessControlPoint, 3> patch)
        {
            UNITY_SETUP_INSTANCE_ID(patch[0]);
            float3 p0 = TransformObjectToWorld(patch[0].positionOS.xyz);
            float3 p1 = TransformObjectToWorld(patch[1].positionOS.xyz);
            float3 p2 = TransformObjectToWorld(patch[2].positionOS.xyz);

            TessellationFactors factors;
            factors.edge[0] = CalculateEdgeTessellation(p1, p2);
            factors.edge[1] = CalculateEdgeTessellation(p2, p0);
            factors.edge[2] = CalculateEdgeTessellation(p0, p1);
            factors.inside =
                (factors.edge[0] + factors.edge[1] + factors.edge[2]) / 3.0;
            return factors;
        }

        [domain("tri")]
        [partitioning("fractional_odd")]
        [outputtopology("triangle_cw")]
        [patchconstantfunc("PatchConstantFunction")]
        [outputcontrolpoints(3)]
        TessControlPoint HullFunction(
            InputPatch<TessControlPoint, 3> patch,
            uint controlPointId : SV_OutputControlPointID)
        {
            return patch[controlPointId];
        }

        void InterpolatePatch(
            OutputPatch<TessControlPoint, 3> patch,
            float3 barycentric,
            out float3 positionOS,
            out float3 normalOS,
            out float4 color)
        {
            float3 linearPosition =
                patch[0].positionOS.xyz * barycentric.x +
                patch[1].positionOS.xyz * barycentric.y +
                patch[2].positionOS.xyz * barycentric.z;
            normalOS = normalize(
                patch[0].normalOS * barycentric.x +
                patch[1].normalOS * barycentric.y +
                patch[2].normalOS * barycentric.z);
            color =
                patch[0].color * barycentric.x +
                patch[1].color * barycentric.y +
                patch[2].color * barycentric.z;

            float3 projected0 =
                linearPosition -
                dot(linearPosition - patch[0].positionOS.xyz, patch[0].normalOS) *
                patch[0].normalOS;
            float3 projected1 =
                linearPosition -
                dot(linearPosition - patch[1].positionOS.xyz, patch[1].normalOS) *
                patch[1].normalOS;
            float3 projected2 =
                linearPosition -
                dot(linearPosition - patch[2].positionOS.xyz, patch[2].normalOS) *
                patch[2].normalOS;
            float3 phongPosition =
                projected0 * barycentric.x +
                projected1 * barycentric.y +
                projected2 * barycentric.z;
            positionOS = lerp(
                linearPosition,
                phongPosition,
                saturate(_TessPhongStrength));
        }

        Varyings BuildVaryings(
            float3 positionOS,
            float3 normalOS,
            float4 color,
            TessControlPoint source)
        {
            Varyings output;
            UNITY_TRANSFER_INSTANCE_ID(source, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 displacedPositionOS = DisplacePosition(positionOS, normalOS);
            VertexPositionInputs positionInputs =
                GetVertexPositionInputs(displacedPositionOS);
            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = TransformObjectToWorldNormal(normalOS);
            output.color = color;
            output.screenPosition = ComputeScreenPos(positionInputs.positionCS);
            output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            return output;
        }

        [domain("tri")]
        Varyings DomainFunction(
            TessellationFactors factors,
            OutputPatch<TessControlPoint, 3> patch,
            float3 barycentric : SV_DomainLocation)
        {
            float3 positionOS;
            float3 normalOS;
            float4 color;
            InterpolatePatch(
                patch,
                barycentric,
                positionOS,
                normalOS,
                color);
            return BuildVaryings(positionOS, normalOS, color, patch[0]);
        }

        ShadowVaryings BuildShadowVaryings(
            float3 positionOS,
            float3 normalOS,
            TessControlPoint source)
        {
            ShadowVaryings output;
            UNITY_TRANSFER_INSTANCE_ID(source, output);

            float3 displacedPositionOS =
                DisplacePosition(positionOS, normalOS);
            float3 positionWS =
                TransformObjectToWorld(displacedPositionOS);
            float3 normalWS =
                TransformObjectToWorldNormal(normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS =
                    normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

            output.positionCS = TransformWorldToHClip(
                ApplyShadowBias(
                    positionWS,
                    normalWS,
                    lightDirectionWS));

            #if UNITY_REVERSED_Z
                output.positionCS.z = min(
                    output.positionCS.z,
                    UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(
                    output.positionCS.z,
                    UNITY_NEAR_CLIP_VALUE);
            #endif
            return output;
        }

        [domain("tri")]
        ShadowVaryings ShadowDomainFunction(
            TessellationFactors factors,
            OutputPatch<TessControlPoint, 3> patch,
            float3 barycentric : SV_DomainLocation)
        {
            float3 positionOS;
            float3 normalOS;
            float4 color;
            InterpolatePatch(
                patch,
                barycentric,
                positionOS,
                normalOS,
                color);
            return BuildShadowVaryings(
                positionOS,
                normalOS,
                patch[0]);
        }

        float CalculateDepthFade(Varyings input)
        {
            float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
            float sceneDepth = LinearEyeDepth(
                SampleSceneDepth(screenUV),
                _ZBufferParams);
            float surfaceDepth = LinearEyeDepth(
                input.screenPosition.z / input.screenPosition.w,
                _ZBufferParams);
            float fade = saturate(
                abs(sceneDepth - surfaceDepth) /
                max(_DepthDistance, 1e-4));
            return fade * fade * fade *
                (fade * (fade * 6.0 - 15.0) + 10.0);
        }

        half3 EvaluateTranslucency(
            Light light,
            half3 normalWS,
            half3 viewDirectionWS,
            half3 baseColor,
            half3 bakedGI)
        {
            half shadowAttenuation = lerp(
                1.0h,
                light.shadowAttenuation,
                _TransShadow);
            half3 attenuatedColor =
                light.color *
                light.distanceAttenuation *
                shadowAttenuation;
            half3 distortedLightDirection =
                light.direction + normalWS * _TransNormal;
            half viewLight =
                pow(
                    saturate(dot(viewDirectionWS, -distortedLightDirection)),
                    _TransScattering);
            half3 scattering =
                attenuatedColor *
                (viewLight * _TransDirect + bakedGI * _TransAmbient) *
                _TranslucencyColor.rgb;
            return baseColor * scattering * _TransStrength;
        }

        half4 ForwardFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            float3 shadingRadialWS = SafeNormalize(
                input.positionWS -
                TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
            normalWS = SafeNormalize(
                lerp(normalWS, (half3)shadingRadialWS, (half)_DisplaceSmoothing));
            half3 viewDirectionWS =
                SafeNormalize(GetCameraPositionWS() - input.positionWS);
            float depthFade = CalculateDepthFade(input);
            float2 noise = EvaluateCloudNoise(input.positionWS, normalWS);
            float noiseColor = lerp(noise.x, noise.y, _NoiseColorMix);

            half3 vertexTint = lerp(
                _BaseColor.rgb,
                input.color.rgb,
                _VertexColorStrength);
            half3 baseColor =
                vertexTint *
                lerp(1.0h, noiseColor.xxx, _NoiseColorStrength);

            // 假体积：沿视线向内多采噪声，制造内部起伏与遮蔽，
            // 让位移表面读起来有厚度而不是硬壳。默认强度为零。
            if (_FakeVolume > 0.0h)
            {
                float interiorOcclusion = 0.0;
                [unroll]
                for (int interiorStep = 1; interiorStep <= 3; interiorStep++)
                {
                    float3 interiorWS =
                        input.positionWS -
                        viewDirectionWS *
                        (_FakeVolumeDepth * max(abs(_Scale), 1e-4) *
                         0.25 * interiorStep);
                    float2 interiorNoise =
                        EvaluateCloudNoise(interiorWS, normalWS);
                    interiorOcclusion +=
                        lerp(interiorNoise.x, interiorNoise.y, _NoiseColorMix);
                }
                interiorOcclusion /= 3.0;
                half volumeShade = lerp(
                    1.0h,
                    saturate(interiorOcclusion + 0.3),
                    _FakeVolume);
                baseColor *= volumeShade;
            }

            half3 bakedGI = SampleSH(normalWS);
            float4 shadowCoordinates =
                TransformWorldToShadowCoord(input.positionWS);
            Light mainLight = GetMainLight(shadowCoordinates);
            half mainAttenuation =
                mainLight.distanceAttenuation *
                lerp(
                    1.0h,
                    mainLight.shadowAttenuation,
                    saturate(_ShadowOpacity));
            half3 lighting =
                baseColor *
                (bakedGI +
                 mainLight.color *
                 mainAttenuation *
                 saturate(dot(normalWS, mainLight.direction)));
            lighting += EvaluateTranslucency(
                mainLight,
                normalWS,
                viewDirectionWS,
                baseColor,
                bakedGI);

            #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light additionalLight =
                        GetAdditionalLight(lightIndex, input.positionWS);
                    half attenuation =
                        additionalLight.distanceAttenuation *
                        lerp(
                            1.0h,
                            additionalLight.shadowAttenuation,
                            saturate(_ShadowOpacity));
                    lighting +=
                        baseColor *
                        additionalLight.color *
                        attenuation *
                        saturate(dot(normalWS, additionalLight.direction));
                    lighting += EvaluateTranslucency(
                        additionalLight,
                        normalWS,
                        viewDirectionWS,
                        baseColor,
                        bakedGI);
                LIGHT_LOOP_END
            #endif

            half emissionNoise =
                lerp(1.0h, noiseColor, _EmmissiveNoiseColor);
            half emissionDepth =
                lerp(1.0h, depthFade, _EmmissiveDepthFade);
            lighting *= depthFade;
            half3 emission =
                _EmmissiveColor.rgb *
                emissionNoise *
                emissionDepth;
            lighting += emission;
            lighting = MixFog(lighting, input.fogFactor);

            half vertexAlpha = lerp(
                _BaseColor.a,
                input.color.a,
                _VertexColorStrength);
            half alpha = saturate(vertexAlpha * depthFade);
            return half4(lighting, alpha);
        }

        half4 DepthOnlyFragment(Varyings input) : SV_Target
        {
            return 0;
        }

        half4 DepthNormalsFragment(Varyings input) : SV_Target
        {
            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            return half4(normalWS * 0.5h + 0.5h, 0.0h);
        }

        float InterleavedGradientNoise(float2 pixelPosition)
        {
            return frac(
                52.9829189 *
                frac(dot(
                    floor(pixelPosition),
                    float2(0.06711056, 0.00583715))));
        }

        half4 ShadowFragment(ShadowVaryings input) : SV_Target
        {
            float dither =
                InterleavedGradientNoise(input.positionCS.xy);
            clip(saturate(_ShadowOpacity) - dither);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex TessellationVertex
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #pragma fragment ForwardFragment
            #pragma require tessellation tessHW
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex TessellationVertex
            #pragma hull HullFunction
            #pragma domain ShadowDomainFunction
            #pragma fragment ShadowFragment
            #pragma require tessellation tessHW
            #pragma multi_compile_instancing
            // The light-position branch runs in the tessellation domain stage.
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex TessellationVertex
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #pragma fragment DepthOnlyFragment
            #pragma require tessellation tessHW
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex TessellationVertex
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #pragma fragment DepthNormalsFragment
            #pragma require tessellation tessHW
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    FallBack Off
}
