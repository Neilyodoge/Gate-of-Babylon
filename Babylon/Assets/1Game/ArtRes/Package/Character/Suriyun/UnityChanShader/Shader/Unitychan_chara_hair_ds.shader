Shader "UnityChan/Hair - Double-sided"
{
	Properties
	{
		_Color ("Main Color", Color) = (1, 1, 1, 1)
		_ShadowColor ("Shadow Color", Color) = (0.8, 0.8, 1, 1)
		_SpecularPower ("Specular Power", Float) = 20
		_EdgeThickness ("Outline Thickness", Float) = 1
		_DepthBias  ("Outline Depth Bias", Float) = 0.00012

		_MainTex ("Diffuse", 2D) = "white" {}
		_FalloffSampler ("Falloff Control", 2D) = "white" {}
		_RimLightSampler ("RimLight Control", 2D) = "white" {}
		_SpecularReflectionSampler ("Specular / Reflection Mask", 2D) = "white" {}
		_EnvMapSampler ("Environment Map", 2D) = "" {}
		_NormalMapSampler ("Normal Map", 2D) = "" {}
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline"="UniversalPipeline"
			"RenderType"="Opaque"
			"Queue"="Geometry"
		}

		// ---- Forward（双面）----
		Pass
		{
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			Cull Off
			ZTest LEqual
			ZWrite On
			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT
			#define ENABLE_NORMAL_MAP
			#include "CharaMain.cginc"
			ENDHLSL
		}

		// ---- Outline ----
		Pass
		{
			Name "Outline"
			Tags { "LightMode"="SRPDefaultUnlit" }
			Cull Front
			ZTest LEqual
			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "CharaOutline.cginc"
			ENDHLSL
		}

		// ---- ShadowCaster ----
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			Cull Off
			ZWrite On
			ZTest LEqual
			ColorMask 0
			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
			#include "CharaShadowCaster.cginc"
			ENDHLSL
		}

		// ---- DepthOnly ----
		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }
			Cull Off
			ZWrite On
			ColorMask R
			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "CharaDepthOnly.cginc"
			ENDHLSL
		}
	}

	FallBack Off
}
