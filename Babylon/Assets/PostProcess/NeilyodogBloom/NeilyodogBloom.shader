Shader "Hidden/NeilyodogBloom"
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
        int _KillFireflies;
        
        // 亮度计算
half Luminance_NeilyodogBloom(half3 color)
        {
            return dot(color, half3(0.299, 0.587, 0.114));
        }
        
        // 二次阈值函数
        half3 QuadraticThreshold(half3 color, float threshold, float knee)
        {
            half brightness = Luminance_NeilyodogBloom(color);
            
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
        
        // 4-tap box 下采样
        half4 Downsample4Tap(float2 uv, float2 texelSize)
        {
            float4 d = texelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
            
            half4 s1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xy);
            half4 s2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zy);
            half4 s3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xw);
            half4 s4 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zw);
            
            half4 result = (s1 + s2 + s3 + s4) * 0.25;
            
            // Kill fireflies
            if (_KillFireflies)
            {
                half l1 = Luminance_NeilyodogBloom(s1.rgb);
                half l2 = Luminance_NeilyodogBloom(s2.rgb);
                half l3 = Luminance_NeilyodogBloom(s3.rgb);
                half l4 = Luminance_NeilyodogBloom(s4.rgb);
                
                half avgL = (l1 + l2 + l3 + l4) * 0.25;
                half maxL = max(max(l1, l2), max(l3, l4));
                
                if (maxL > avgL * 8.0)
                {
                    result = half4(avgL, avgL, avgL, 1.0);
                }
            }
            
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