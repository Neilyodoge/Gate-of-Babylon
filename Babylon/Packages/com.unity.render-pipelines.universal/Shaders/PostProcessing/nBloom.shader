Shader "Hidden/nBloom"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Cull Off
        ZWrite Off
        ZTest Always
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        
        #pragma multi_compile_local _ _KILL_FIREFLY
        
        // Blitter会自动将源纹理绑定到_BlitTexture，由Blit.hlsl声明
        // _BlitTexture 和 sampler_LinearClamp 由 Blit.hlsl 提供
        // Vert 顶点着色器也由 Blit.hlsl 提供
        
        float4 _BlitTexture_TexelSize;
        
        TEXTURE2D_X(_BloomTex);
        // _BloomTex使用sampler_LinearClamp（由Blit.hlsl提供），确保双线性过滤
        
        // Bloom 参数
        float _Threshold;
        float _ThresholdKnee;
        float _Intensity;
        float _Scatter;
        float _Clamp;
        
        // Karis Average 滤波强度（值越小压制越强，1.0为默认）
        static const half FILTER_STRENGTH = 1.0;
        
        // 亮度计算（sRGB 亮度）
        half luminance_sRGB(half3 color)
        {
            return dot(color, half3(0.2126, 0.7152, 0.0722));
        }
        
        // 二次阈值函数
        half3 QuadraticThreshold(half3 color, float threshold, float knee)
        {
            half brightness = luminance_sRGB(color);
            
            half softThreshold = knee;
            half softThresholdBrightness = threshold - softThreshold;
            half kneeOffset = softThreshold * 2.0;
            half kneeScale = 0.25 / max(softThreshold, 1e-5);
            
            half soft = brightness - softThresholdBrightness;
            soft = clamp(soft, 0.0, kneeOffset);
            soft = soft * soft * kneeScale;
            
            half contribution = max(soft, brightness - threshold);
            contribution /= max(brightness, 1e-5);
            
            return color * contribution;
        }
        
        // 4-tap box 下采样（含 Kill Fireflies 加权平均）
        half4 Downsample4Tap(float2 uv, float2 texelSize)
        {
            float4 d = texelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
            
            half4 s0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xy);
            half4 s1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zy);
            half4 s2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xw);
            half4 s3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zw);
            
            half4 result;
            
            #if _KILL_FIREFLY
            // Karis Average：亮度倒数加权平均，自然压制高亮像素
            // 原理：w = 1 / (strength + luminance)，亮度越高权重越低
            half w0 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s0.rgb));
            half w1 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s1.rgb));
            half w2 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s2.rgb));
            half w3 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s3.rgb));
            half w = w0 + w1 + w2 + w3;
            result = (s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3) / w;
            #else
            // 简单均匀平均
            result = (s0 + s1 + s2 + s3) * 0.25;
            #endif
            
            return result;
        }
        
        // Kawase 上采样滤波 - 从_BloomTex采样（低层bloom数据）
        half4 UpsampleKawase(float2 uv, float2 texelSize, float sampleScale)
        {
            float2 sampleOffset = texelSize * sampleScale;
            
            half4 sum = SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv + sampleOffset * float2(-1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv + sampleOffset * float2(1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv + sampleOffset * float2(-1.0, 1.0));
            sum += SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv + sampleOffset * float2(1.0, 1.0));
            
            return sum * 0.25;
        }
        
        ENDHLSL
        
        // Pass 0: 预过滤 - 提取高亮区域
        Pass
        {
            Name "Prefilter"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            
            half4 FragPrefilter(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                
                // 应用阈值
                half3 bloomColor = QuadraticThreshold(color.rgb, _Threshold, _ThresholdKnee);
                
                // 钳制防止极端值
                bloomColor = min(bloomColor, _Clamp);
                
                return half4(bloomColor, 1.0);
            }
            ENDHLSL
        }
        
        // Pass 1: 下采样
        Pass
        {
            Name "Downsample"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            
            half4 FragDownsample(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                return Downsample4Tap(uv, _BlitTexture_TexelSize.xy);
            }
            ENDHLSL
        }
        
        // Pass 2: 上采样
        Pass
        {
            Name "Upsample"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            
            half4 FragUpsample(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                
                half4 baseColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 bloomColor = UpsampleKawase(uv, _BlitTexture_TexelSize.xy, _Scatter);
                
                return baseColor + bloomColor;
            }
            ENDHLSL
        }
        
        // Pass 3: 最终合成
        Pass
        {
            Name "Combine"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCombine
            
            half4 FragCombine(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 bloomColor = SAMPLE_TEXTURE2D_X(_BloomTex, sampler_LinearClamp, uv);
                
                // 应用强度并合成
                half3 result = sourceColor.rgb + bloomColor.rgb * _Intensity;
                
                return half4(result, sourceColor.a);
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}