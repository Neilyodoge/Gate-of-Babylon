// VFXWater - 特效水体着色器
// 移植自参考工程 VFXWater，适配 URP 2022
// 功能：双层法线混合、水晶通透SSS、Matcap反射
Shader "Effect/VFXWater"
{
    Properties
    {
        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [Toggle] _ZWrite("ZWrite", Float) = 0
        _Opacity("整体透明度", Range(0, 1)) = 1

        [Header(Diffuse)]
        _AlbedoColor("物体颜色", Color) = (1.0, 1.0, 1.0, 1)
        _AlbedoMap("物体颜色图", 2D) = "white"{}
        _NormalMap("物体法线", 2D) = "bump"{}
        _NormalScaleA("物体法线强度", Range(0, 1)) = 1.0

        [Header(Mask)]
        _WaveMaskMap("遮罩图 (R-厚度图 G-浪尖范围 B-泡沫渐变)", 2D) = "gray" {}

        [Header(SSS)]
        _DiffuseLerp("水晶通透度", Range(0, 1)) = 0
        _DiffuseLerp1("水晶透明开始", Range(0, 2)) = 0
        _DiffuseLerp2("水晶透明结束", Range(0, 2)) = 0

        [Header(Matcap)]
        [NoScaleOffset] _EnvCapTex("环境光 Matcap", 2D) = "white" {}
        [HDR] _EnvColor("环境光颜色", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(WaveNormal)]
        [NoScaleOffset] _WaveNormalMap("海浪法线 (RGB A)", 2D) = "bump" {}
        _WaveParamsB("法线运动参数 (UV缩放xy 速度zw)", Vector) = (1, 1, 0, 0)
        _NormalScaleB("海浪法线强度", Range(0, 1)) = 1



    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 100

        // =================================================================
        // Forward Pass
        // =================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0

            // URP 关键字
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #pragma vertex VFXWaterVert
            #pragma fragment VFXWaterFrag

            #include "VFXWaterForwardPass.hlsl"
            ENDHLSL
        }

        // =================================================================
        // ShadowCaster Pass
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex VFXWaterShadowVert
            #pragma fragment VFXWaterShadowFrag

            #include "VFXWaterForwardPass.hlsl"
            ENDHLSL
        }

        // =================================================================
        // DepthOnly Pass
        // =================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex VFXWaterDepthVert
            #pragma fragment VFXWaterDepthFrag

            #include "VFXWaterForwardPass.hlsl"
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
