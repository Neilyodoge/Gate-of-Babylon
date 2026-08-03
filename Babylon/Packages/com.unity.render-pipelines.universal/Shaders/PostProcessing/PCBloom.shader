Shader "Hidden/Universal Render Pipeline/PCBloom"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles
        #pragma multi_compile_local _ _KILL_FIREFLY

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _BlitTexture_TexelSize;
        float4 _PCBloomDownsampleParams; // x: radius, y: sigma, z: threshold, w: clamp
        float4 _PCBloomUpsampleParams;   // x: radius, y: sigma
        float4 _PCBloomPrefilterParams;  // x: luminance compression, y: post-threshold gain
        float2 _PCBloomCombineParams;    // x: current mip weight, y: accumulated low-mip weight

        TEXTURE2D_X(_PCBloomLowMip);
        float4 _PCBloomLowMip_TexelSize;

        float GaussianWeight(int2 offset, float sigma)
        {
            float2 p = offset;
            return exp(-dot(p, p) / max(2.0 * sigma * sigma, 1e-4));
        }

        half3 ThresholdColor(half3 color)
        {
            color = min(color, _PCBloomDownsampleParams.w);
            color *= rcp(1.0 + _PCBloomPrefilterParams.x * Luminance(color));
            color = max(color - _PCBloomDownsampleParams.z, 0.0);
            return color * _PCBloomPrefilterParams.y;
        }

        half3 GaussianPrefilter(float2 uv)
        {
            int radius = (int)_PCBloomDownsampleParams.x;
            float sigma = _PCBloomDownsampleParams.y;
            half3 result = 0.0;
            float weightSum = 0.0;

            [loop]
            for (int y = -7; y <= 7; y++)
            {
                [loop]
                for (int x = -7; x <= 7; x++)
                {
                    if (abs(x) > radius || abs(y) > radius)
                        continue;

                    int2 offset = int2(x, y);
                    half3 color = ThresholdColor(SAMPLE_TEXTURE2D_X(
                        _BlitTexture, sampler_LinearClamp,
                        uv + float2(offset) * _BlitTexture_TexelSize.xy).rgb);
                    float weight = GaussianWeight(offset, sigma);

                #if _KILL_FIREFLY
                    weight *= rcp(1.0 + Luminance(color));
                #endif

                    result += color * weight;
                    weightSum += weight;
                }
            }

            return result / max(weightSum, 1e-4);
        }

        void GetPacked5x5Weights(float sigma, out float offset, out float centerWeight, out float pairWeight, out float normalization)
        {
            float sigma2 = max(sigma * sigma, 1e-4);
            centerWeight = 1.0;
            float weight1 = exp(-1.0 / (2.0 * sigma2));
            float weight2 = exp(-4.0 / (2.0 * sigma2));
            pairWeight = weight1 + weight2;
            offset = (weight1 + 2.0 * weight2) / max(pairWeight, 1e-4);
            float oneDimensionalSum = centerWeight + 2.0 * pairWeight;
            normalization = oneDimensionalSum * oneDimensionalSum;
        }

        half3 GaussianSource5x5(float2 uv, float sigma)
        {
            float offset = 0.0;
            float centerWeight = 0.0;
            float pairWeight = 0.0;
            float normalization = 1.0;
            GetPacked5x5Weights(sigma, offset, centerWeight, pairWeight, normalization);

            float2 d = _BlitTexture_TexelSize.xy * offset;
            half3 result = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * (centerWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x, 0.0)).rgb * (pairWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x, 0.0)).rgb * (pairWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -d.y)).rgb * (centerWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0,  d.y)).rgb * (centerWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x, -d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x, -d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x,  d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x,  d.y)).rgb * (pairWeight * pairWeight);
            return result / max(normalization, 1e-4);
        }

        half3 GaussianSource(float2 uv, int radius, float sigma)
        {
            if (radius == 2)
                return GaussianSource5x5(uv, sigma);

            half3 result = 0.0;
            float weightSum = 0.0;

            [loop]
            for (int y = -7; y <= 7; y++)
            {
                [loop]
                for (int x = -7; x <= 7; x++)
                {
                    if (abs(x) > radius || abs(y) > radius)
                        continue;

                    int2 offset = int2(x, y);
                    float weight = GaussianWeight(offset, sigma);
                    result += SAMPLE_TEXTURE2D_X(
                        _BlitTexture, sampler_LinearClamp,
                        uv + float2(offset) * _BlitTexture_TexelSize.xy).rgb * weight;
                    weightSum += weight;
                }
            }

            return result / max(weightSum, 1e-4);
        }

        half3 GaussianLowMip5x5(float2 uv, float sigma)
        {
            float offset = 0.0;
            float centerWeight = 0.0;
            float pairWeight = 0.0;
            float normalization = 1.0;
            GetPacked5x5Weights(sigma, offset, centerWeight, pairWeight, normalization);

            float2 d = _PCBloomLowMip_TexelSize.xy * offset;
            half3 result = SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv).rgb * (centerWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2(-d.x, 0.0)).rgb * (pairWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2( d.x, 0.0)).rgb * (pairWeight * centerWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2(0.0, -d.y)).rgb * (centerWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2(0.0,  d.y)).rgb * (centerWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2(-d.x, -d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2( d.x, -d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2(-d.x,  d.y)).rgb * (pairWeight * pairWeight);
            result += SAMPLE_TEXTURE2D_X(_PCBloomLowMip, sampler_LinearClamp, uv + float2( d.x,  d.y)).rgb * (pairWeight * pairWeight);
            return result / max(normalization, 1e-4);
        }

        half3 GaussianLowMip(float2 uv, int radius, float sigma)
        {
            if (radius == 2)
                return GaussianLowMip5x5(uv, sigma);

            half3 result = 0.0;
            float weightSum = 0.0;

            [loop]
            for (int y = -7; y <= 7; y++)
            {
                [loop]
                for (int x = -7; x <= 7; x++)
                {
                    if (abs(x) > radius || abs(y) > radius)
                        continue;

                    int2 offset = int2(x, y);
                    float weight = GaussianWeight(offset, sigma);
                    result += SAMPLE_TEXTURE2D_X(
                        _PCBloomLowMip, sampler_LinearClamp,
                        uv + float2(offset) * _PCBloomLowMip_TexelSize.xy).rgb * weight;
                    weightSum += weight;
                }
            }

            return result / max(weightSum, 1e-4);
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            return half4(GaussianPrefilter(uv), 1.0);
        }

        half4 FragDownsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            int radius = (int)_PCBloomDownsampleParams.x;
            return half4(GaussianSource(uv, radius, _PCBloomDownsampleParams.y), 1.0);
        }

        half4 FragUpsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            int radius = (int)_PCBloomUpsampleParams.x;
            float sigma = _PCBloomUpsampleParams.y;
            half3 currentMip = GaussianSource(uv, radius, sigma);
            half3 lowMip = GaussianLowMip(uv, radius, sigma);
            return half4(
                currentMip * _PCBloomCombineParams.x +
                lowMip * _PCBloomCombineParams.y,
                1.0);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PC Bloom Prefilter"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "PC Bloom Downsample"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragDownsample
            ENDHLSL
        }

        Pass
        {
            Name "PC Bloom Upsample"
            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragUpsample
            ENDHLSL
        }
    }

    Fallback Off
}
