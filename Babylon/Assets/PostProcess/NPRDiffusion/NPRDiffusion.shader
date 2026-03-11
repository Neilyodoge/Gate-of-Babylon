Shader "Hidden/NPRDiffusion"
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
        
        TEXTURE2D_X(_DiffusionTex);
        
        // Diffusion 参数
        float _Intensity;
        float _Threshold;
        float _ThresholdKnee;
        
        // 亮度计算
        half Luminance_sRGB(half3 color)
        {
            return dot(color, half3(0.2126, 0.7152, 0.0722));
        }
        
        // 软阈值函数 - 提取超过阈值的高亮部分，带软过渡
        half3 SoftThreshold(half3 color, float threshold, float knee)
        {
            half brightness = Luminance_sRGB(color);
            
            half softEdge = brightness - (threshold - knee);
            softEdge = clamp(softEdge, 0.0, 2.0 * knee);
            softEdge = softEdge * softEdge / (4.0 * knee + 1e-5);
            
            half contribution = max(softEdge, brightness - threshold);
            contribution /= max(brightness, 1e-5);
            
            return color * contribution;
        }
        
        // ================================================================
        // Dual Kawase Blur
        // 参考: "Bandwidth-Efficient Rendering" (SIGGRAPH 2015, ARM)
        // 利用硬件双线性过滤，以极少的采样次数实现大范围模糊
        // 降采样: 5-tap (中心 + 4对角) -> 自然半分辨率
        // 上采样: 8-tap (4对角 + 4轴向) -> 恢复上层分辨率
        // ================================================================
        
        // Dual Kawase 降采样核 - 5次采样实现高质量降采样模糊
        half4 DualKawaseDown(float2 uv, float2 texelSize)
        {
            half4 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 4.0;
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2(-1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2(-1.0,  1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 1.0,  1.0));
            return sum / 8.0;
        }
        
        // Dual Kawase 上采样核 - 8次采样实现高质量上采样模糊
        half4 DualKawaseUp(float2 uv, float2 texelSize)
        {
            half4 sum = 0;
            // 4个对角采样 (权重各1)
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2(-1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 1.0, -1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2(-1.0,  1.0));
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 1.0,  1.0));
            // 4个轴向采样 (权重各2)
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2(-2.0,  0.0)) * 2.0;
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 2.0,  0.0)) * 2.0;
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 0.0, -2.0)) * 2.0;
            sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + texelSize * float2( 0.0,  2.0)) * 2.0;
            return sum / 12.0;
        }
        
        ENDHLSL
        
        // Pass 0: 预过滤 - 提取高亮区域 + 首次降采样
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
                
                // 4-tap降采样
                float4 d = _BlitTexture_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
                half4 s0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xy);
                half4 s1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zy);
                half4 s2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.xw);
                half4 s3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d.zw);
                half4 color = (s0 + s1 + s2 + s3) * 0.25;
                
                // 应用亮度阈值提取
                color.rgb = SoftThreshold(color.rgb, _Threshold, _ThresholdKnee);
                
                return color;
            }
            ENDHLSL
        }
        
        // Pass 1: Dual Kawase 降采样
        Pass
        {
            Name "DualKawaseDown"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDown
            
            half4 FragDown(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                return DualKawaseDown(uv, _BlitTexture_TexelSize.xy);
            }
            ENDHLSL
        }
        
        // Pass 2: Dual Kawase 上采样
        Pass
        {
            Name "DualKawaseUp"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUp
            
            half4 FragUp(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                return DualKawaseUp(uv, _BlitTexture_TexelSize.xy);
            }
            ENDHLSL
        }
        
        // Pass 3: 最终合成 - 使用"变亮"(Lighten/Max)方式叠加
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
                
                // 原始SceneColor
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // 模糊后的Diffusion纹理
                half4 diffusionColor = SAMPLE_TEXTURE2D_X(_DiffusionTex, sampler_LinearClamp, uv);
                
                // "变亮" (Lighten) 混合模式：取每个通道的最大值
                // 通过 _Intensity 控制 diffusion 的强度
                half3 diffusionScaled = diffusionColor.rgb * _Intensity;
                half3 result = max(sourceColor.rgb, diffusionScaled);
                
                return half4(result, sourceColor.a);
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}
