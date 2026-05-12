Shader "Neilyodog/Checkerboard"
{
    Properties
    {
        _MainTex("MainTex", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 0

        [Space(20)]
        [Toggle] _UsingCheckerboard("Using Checkerboard?", Float) = 0
        _Repeat("Repeat", Float) = 5

        [Space(20)]
        [Toggle(_PANARREF_ON)] _PanarRef_ON("Use PlanarReflection", Float) = 0
        _RefIntensity("反射强度", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // =====================================================================
        // Forward Pass
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull   [_Cull]
            ZWrite On
            ZTest  LEqual

            HLSLPROGRAM
            #pragma target 3.0

            #pragma multi_compile_fog
            #pragma shader_feature_local _PANARREF_ON

            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _MainTex_ST;
                half   _Repeat;
                half   _UsingCheckerboard;
                float4 _PlanarReflectionTexture_TexelSize;
                float  _RefIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);                SAMPLER(sampler_MainTex);
            TEXTURE2D(_PlanarReflectionTexture); SAMPLER(sampler_PlanarReflectionTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS  = v.positionOS.xyz;
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogCoord    = ComputeFogFactor(o.positionHCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 棋盘格
                float2 cuv    = floor(i.uv * 2.0) * 0.5 * _Repeat;
                float  check  = frac(cuv.x + cuv.y) * 2.0;
                half   mask   = i.positionOS.y + 0.55;
                half4  c      = half4(check * mask, check * mask, check * mask, 1.0);
                c            *= _Color;

                // 不用棋盘格时用贴图
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                c = lerp(mainTex, c, _UsingCheckerboard);

                // 平面反射
                #if defined(_PANARREF_ON)
                float2 scrPos = i.positionHCS.xy / _ScreenParams.xy;
                half4  refTex = SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, scrPos);
                c.rgb = lerp(c.rgb, refTex.rgb, _RefIntensity);
                #endif

                c.rgb = MixFog(c.rgb, i.fogCoord);
                return c;
            }
            ENDHLSL
        }

        // =====================================================================
        // ShadowCaster Pass
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull      [_Cull]
            ZWrite    On
            ZTest     LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _MainTex_ST;
                half   _Repeat;
                half   _UsingCheckerboard;
                float4 _PlanarReflectionTexture_TexelSize;
                float  _RefIntensity;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // =====================================================================
        // DepthOnly Pass
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull      [_Cull]
            ZWrite    On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _MainTex_ST;
                half   _Repeat;
                half   _UsingCheckerboard;
                float4 _PlanarReflectionTexture_TexelSize;
                float  _RefIntensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
