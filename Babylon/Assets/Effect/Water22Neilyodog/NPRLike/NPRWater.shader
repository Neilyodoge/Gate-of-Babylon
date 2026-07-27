Shader "Neilyodog/NPRWater"
{
    // NPRWater - 简化版水面
    // 基于 WaterNPR 精简: 去反射/去扭曲/去法线/去Blinn-Phong高光
    // 深浅水颜色由 LUT 控制 (配合 NPRWaterGUI 的 Gradient 编辑器)
    Properties
    {
        [Header(Water Depth Color)]
        _WaterDepthLUT ("深浅水LUT", 2D) = "white" {}
        _WaterAlpha ("水透明度", Range(0, 1)) = 0.5
        _DepthIntensity ("深度强度", Range(0, 10)) = 1

        [Space(10)]
        [Header(Toon HighLight)]
        [HDR]_CartoonSpecular ("Toon高光颜色", Color) = (1, 1, 1, 1)
        _ToonSpecMin ("Toon高光范围Min", Range(0, 1)) = 0.55
        _ToonSpecMax ("Toon高光范围Max", Range(0, 1)) = 0.65
        _ToonNoiseTex ("高光噪声贴图", 2D) = "white" {}
        _ToonNoiseSpeed ("XY第一层速度 ZW第二层速度", Vector) = (0.1, 0.1, -0.05, 0.08)

        [Space(10)]
        [Header(Fresnel)]
        _fresnelScale ("菲尼尔指数", Float) = 5
        _fresnelColor ("菲尼尔颜色", Color) = (1, 1, 1, 1)

        [Space(10)]
        [Header(Distortion)]
        _DistortionTex ("扭曲贴图(RG)", 2D) = "gray" {}
        _DistortionIntensity ("扭曲强度", Range(0, 0.3)) = 0.05
        _DistortionSpeed ("扭曲速度XY", Vector) = (0.05, 0.03, 0, 0)

        [Space(10)]
        [Header(Caustic)]
        _CausticTex ("焦散贴图", 2D) = "white" {}
        _CausticIntensity ("焦散强度", Float) = 1
        _CausticScale ("焦散范围", Float) = 1
        _CausticFacade ("焦散立面", Range(0, 0.5)) = 0.15
        _CausticSpeed ("焦散速度", Vector) = (0.1, 0.1, 0, 0)

        [Space(10)]
        [Header(Foam)]
        _FoamSDF ("泡沫SDF", 2D) = "black" {}
        [HideInInspector]_SDFBoundsMin ("", Vector) = (0, 0, 0, 0)
        [HideInInspector]_SDFBoundsSize ("", Vector) = (1, 0, 1, 10)
        [HDR]_FoamTint ("泡沫颜色", Color) = (1, 1, 1, 1)
        _FoamEdgeWidth ("岸边白边宽度", Float) = 0.3
        _FoamScope ("条纹扩散范围", Float) = 5
        _FoamInterval ("条纹密度", Float) = 3
        _FoamAnimSpeed ("扩散速度", Float) = 0.5
        _FoamNoiseTex ("碎裂噪声图", 2D) = "white" {}
        _FoamNoiseAmp ("噪声扰动SDF幅度", Range(0, 2)) = 0.3
        _FoamFadePower ("条纹消散曲线", Range(0.3, 4)) = 1.5
        _FoamNoiseSpeed ("噪声流动速度", Vector) = (0.05, 0.03, 0, 0)

        [Space(10)]
        [Header(Debug)]
        [Toggle(_DEBUG_SDF)] _DebugSDF ("Debug SDF", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _DEBUG_SDF

            #include "NPRWaterPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "NPRWaterGUI"
}
