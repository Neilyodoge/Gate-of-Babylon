// Made with Amplify Shader Editor v1.9.6.3
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SkyCloudsEdge"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_BaseColor("Base Color", Color) = (1,1,1,1)
		_TranslucencyColor("Translucency Color", Color) = (1,1,1,0)
		_EmmissiveColor("Emmissive Color", Color) = (0.5490196,0.627451,0.7294118,0)
		_Scale("Scale", Float) = 1
		_WindDirection("WindDirection", Vector) = (-1,-0.3,-0.3,0)
		_WindSpeed("WindSpeed", Float) = 1
		_Mask0("Mask0", Vector) = (1.985208,0.440832,0.2055923,1.2)
		_Mask1("Mask1", Vector) = (1.985208,0.440832,0.2055923,1.2)
		_DepthDistance("Depth Distance", Float) = 0.4
		_NoiseColorStrength("Noise Color Strength", Range( 0 , 1)) = 0.5565214
		_EmmissiveNoiseColor("Emmissive Noise Color", Range( 0 , 1)) = 0.2000003
		_NoiseSpeed("Noise Speed", Float) = 0.2
		_VorSpeed("Vor Speed", Float) = 0.05
		_AdditionalNoiseDir("Additional Noise Dir", Vector) = (0,0,0,0)
		_AdditionalVoronoiDir("AdditionalVoronoiDir", Vector) = (0.7,-0.3,-0.3,0)
		_VoronoiScale("Voronoi Scale", Float) = 1.4
		_NoiseScale("Noise Scale", Float) = 20
		_VoronoiStrength("Voronoi Strength", Float) = 0.9
		_NoiseStrength("Noise Strength", Float) = 0.03
		_NoiseDisplacement("Noise Displacement", Float) = 0.5
		_NoiseScale3D("Noise Scale 3D", Vector) = (1,0.2,0.6,0)
		_EmmissiveDepthFade("Emmissive Depth Fade", Range( 0 , 1)) = 0.2086951
		_NoiseColorMix("NoiseColorMix", Range( 0 , 1)) = 0.96
		_VertexColorStrength("Vertex Color Strength", Range( 0 , 1)) = 0.7030082


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		_TransStrength( "Strength", Range( 0, 50 ) ) = 1
		_TransNormal( "Normal Distortion", Range( 0, 1 ) ) = 0.5
		_TransScattering( "Scattering", Range( 1, 50 ) ) = 2
		_TransDirect( "Direct", Range( 0, 1 ) ) = 0.9
		_TransAmbient( "Ambient", Range( 0, 1 ) ) = 0.1
		_TransShadow( "Shadow", Range( 0, 1 ) ) = 0.5
		_TessPhongStrength( "Phong Tess Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		_TessEdgeLength ( "Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1
		[HideInInspector][ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1
		[HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Lit" }

		Cull Back
		ZWrite Off
		ZTest LEqual
		Offset 0 , 0
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForwardOnly" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			
            #define _NORMAL_DROPOFF_TS 1
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #define ASE_FOG 1
            #define ASE_PHONG_TESSELLATION
            #define ASE_DEPTH_WRITE_ON
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TESSELLATION 1
            #pragma require tessellation tessHW
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #define ASE_TRANSLUCENCY 1
            #define ASE_LENGTH_TESSELLATION
            #define _EMISSION
            #define _ALPHATEST_ON 1
            #define ASE_SRP_VERSION 140004
            #define REQUIRE_DEPTH_TEXTURE 1


			
            #pragma multi_compile _ DOTS_INSTANCING_ON
		

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

			

			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

			
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
           

			

			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _FORWARD_PLUS

			
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
		

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			

			

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR
			#define ASE_NEEDS_FRAG_WORLD_NORMAL


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float4 lightmapUVOrVertexSH : TEXCOORD1;
				half4 fogFactorAndVertexLight : TEXCOORD2;
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					float4 shadowCoord : TEXCOORD6;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD7;
				#endif
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord8 = screenPos38;
				
				o.ase_texcoord9 = v.positionOS;
				o.ase_normal = v.normalOS;
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif
				v.normalOS = v.normalOS;
				v.tangentOS = v.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( v.normalOS, v.tangentOS );

				o.tSpace0 = float4( normalInput.normalWS, vertexInput.positionWS.x );
				o.tSpace1 = float4( normalInput.tangentWS, vertexInput.positionWS.y );
				o.tSpace2 = float4( normalInput.bitangentWS, vertexInput.positionWS.z );

				#if defined(LIGHTMAP_ON)
					OUTPUT_LIGHTMAP_UV( v.texcoord1, unity_LightmapST, o.lightmapUVOrVertexSH.xy );
				#endif

				#if !defined(LIGHTMAP_ON)
					OUTPUT_SH( normalInput.normalWS.xyz, o.lightmapUVOrVertexSH.xyz );
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					o.dynamicLightmapUV.xy = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					o.lightmapUVOrVertexSH.zw = v.texcoord.xy;
					o.lightmapUVOrVertexSH.xy = v.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );

				#ifdef ASE_FOG
					half fogFactor = ComputeFogFactor( vertexInput.positionCS.z );
				#else
					half fogFactor = 0;
				#endif

				o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.positionCS = vertexInput.positionCS;
				o.clipPosV = vertexInput.positionCS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.tangentOS = v.tangentOS;
				o.texcoord = v.texcoord;
				o.texcoord1 = v.texcoord1;
				o.texcoord2 = v.texcoord2;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				o.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				o.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				o.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag ( VertexOutput IN
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( IN.positionCS );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (IN.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( IN.tSpace0.xyz );
					float3 WorldTangent = IN.tSpace1.xyz;
					float3 WorldBiTangent = IN.tSpace2.xyz;
				#endif

				float3 WorldPosition = float3(IN.tSpace0.w,IN.tSpace1.w,IN.tSpace2.w);
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				float4 ClipPos = IN.clipPosV;
				float4 ScreenPos = ComputeScreenPos( IN.clipPosV );

				float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = IN.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif

				WorldViewDirection = SafeNormalize( WorldViewDirection );

				float4 screenPos38 = IN.ase_texcoord8;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),IN.ase_texcoord9);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float NoiseColor3D151 = temp_output_118_0;
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( IN.ase_normal, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float NoiseColorVoronoi152 = temp_output_190_0;
				float lerpResult157 = lerp( NoiseColor3D151 , NoiseColorVoronoi152 , _NoiseColorMix);
				float NoiseColorMix158 = lerpResult157;
				float3 temp_cast_3 = (NoiseColorMix158).xxx;
				float temp_output_2_0_g158 = _NoiseColorStrength;
				float temp_output_3_0_g158 = ( 1.0 - temp_output_2_0_g158 );
				float3 appendResult7_g158 = (float3(temp_output_3_0_g158 , temp_output_3_0_g158 , temp_output_3_0_g158));
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 Albedo30 = saturate( ( float4( ( DepthFade43 * ( ( temp_cast_3 * temp_output_2_0_g158 ) + appendResult7_g158 ) ) , 0.0 ) * lerpResult161 ) );
				
				float3 temp_cast_6 = (NoiseColorMix158).xxx;
				float temp_output_2_0_g156 = _EmmissiveNoiseColor;
				float temp_output_3_0_g156 = ( 1.0 - temp_output_2_0_g156 );
				float3 appendResult7_g156 = (float3(temp_output_3_0_g156 , temp_output_3_0_g156 , temp_output_3_0_g156));
				float3 temp_cast_8 = (DepthFade43).xxx;
				float temp_output_2_0_g157 = _EmmissiveDepthFade;
				float temp_output_3_0_g157 = ( 1.0 - temp_output_2_0_g157 );
				float3 appendResult7_g157 = (float3(temp_output_3_0_g157 , temp_output_3_0_g157 , temp_output_3_0_g157));
				
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				
				float fresnelNdotV54 = dot( WorldNormal, WorldViewDirection );
				float fresnelNode54 = ( 0.0 + 1.0 * pow( 1.0 - fresnelNdotV54, 5.0 ) );
				

				float3 BaseColor = Albedo30.rgb;
				float3 Normal = float3(0, 0, 1);
				float3 Emission = ( ( _EmmissiveColor * float4( ( ( temp_cast_6 * temp_output_2_0_g156 ) + appendResult7_g156 ) , 0.0 ) ) * float4( ( ( temp_cast_8 * temp_output_2_0_g157 ) + appendResult7_g157 ) , 0.0 ) ).rgb;
				float3 Specular = 0.5;
				float Metallic = 0.0;
				float Smoothness = 0.0;
				float Occlusion = 1;
				float Alpha = ( DepthFade43 * BaseColorAlpha162 );
				float AlphaClipThreshold = 0.0;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = ( ( ( fresnelNode54 + 0.5 ) * _TranslucencyColor ) * DepthFade43 ).rgb;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = IN.positionCS.z;
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.viewDirectionWS = WorldViewDirection;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent, WorldBiTangent, WorldNormal));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = WorldNormal;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					inputData.shadowCoord = ShadowCoords;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
				#else
					inputData.shadowCoord = float4(0, 0, 0, 0);
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = IN.fogFactorAndVertexLight.x;
				#endif
					inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = IN.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(IN.lightmapUVOrVertexSH.xy, IN.dynamicLightmapUV.xy, SH, inputData.normalWS);
				#else
					inputData.bakedGI = SAMPLE_GI(IN.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;
				inputData.shadowMask = SAMPLE_SHADOWMASK(IN.lightmapUVOrVertexSH.xy);

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = IN.dynamicLightmapUV.xy;
					#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = IN.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#ifdef _DBUFFER
					ApplyDecalToSurfaceData(IN.positionCS, surfaceData, inputData);
				#endif

				#ifdef _ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( WorldNormal,0 ) ).xyz * ( 1.0 - dot( WorldNormal, WorldViewDirection ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3( 0, 0, 0 ), IN.fogFactorAndVertexLight.x );
					#else
						color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
					#endif
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				return color;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask R
			AlphaToMask Off

			HLSLPROGRAM

			
            #define _NORMAL_DROPOFF_TS 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #define ASE_FOG 1
            #define ASE_PHONG_TESSELLATION
            #define ASE_DEPTH_WRITE_ON
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TESSELLATION 1
            #pragma require tessellation tessHW
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #define ASE_TRANSLUCENCY 1
            #define ASE_LENGTH_TESSELLATION
            #define _EMISSION
            #define _ALPHATEST_ON 1
            #define ASE_SRP_VERSION 140004
            #define REQUIRE_DEPTH_TEXTURE 1


			
            #pragma multi_compile _ DOTS_INSTANCING_ON
		

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 positionWS : TEXCOORD1;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD2;
				#endif
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord3 = screenPos38;
				
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					o.positionWS = vertexInput.positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.positionCS = vertexInput.positionCS;
				o.clipPosV = vertexInput.positionCS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(	VertexOutput IN
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = IN.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float4 ClipPos = IN.clipPosV;
				float4 ScreenPos = ComputeScreenPos( IN.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float4 screenPos38 = IN.ase_texcoord3;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				float Alpha = ( DepthFade43 * BaseColorAlpha162 );
				float AlphaClipThreshold = 0.0;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = IN.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( IN.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_PHONG_TESSELLATION
			#define ASE_DEPTH_WRITE_ON
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define ASE_TESSELLATION 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_TRANSLUCENCY 1
			#define ASE_LENGTH_TESSELLATION
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define ASE_SRP_VERSION 140004
			#define REQUIRE_DEPTH_TEXTURE 1

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef EDITOR_VISUALIZATION
					float4 VizUV : TEXCOORD2;
					float4 LightCoord : TEXCOORD3;
				#endif
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord4 = screenPos38;
				
				o.ase_texcoord5 = v.positionOS;
				o.ase_normal = v.normalOS;
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				float3 positionWS = TransformObjectToWorld( v.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					o.positionWS = positionWS;
				#endif

				o.positionCS = MetaVertexPosition( v.positionOS, v.texcoord1.xy, v.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );

				#ifdef EDITOR_VISUALIZATION
					float2 VizUV = 0;
					float4 LightCoord = 0;
					UnityEditorVizData(v.positionOS.xyz, v.texcoord0.xy, v.texcoord1.xy, v.texcoord2.xy, VizUV, LightCoord);
					o.VizUV = float4(VizUV, 0, 0);
					o.LightCoord = LightCoord;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = o.positionCS;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.texcoord0 = v.texcoord0;
				o.texcoord1 = v.texcoord1;
				o.texcoord2 = v.texcoord2;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.texcoord0 = patch[0].texcoord0 * bary.x + patch[1].texcoord0 * bary.y + patch[2].texcoord0 * bary.z;
				o.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				o.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = IN.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float4 screenPos38 = IN.ase_texcoord4;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),IN.ase_texcoord5);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float NoiseColor3D151 = temp_output_118_0;
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( IN.ase_normal, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float NoiseColorVoronoi152 = temp_output_190_0;
				float lerpResult157 = lerp( NoiseColor3D151 , NoiseColorVoronoi152 , _NoiseColorMix);
				float NoiseColorMix158 = lerpResult157;
				float3 temp_cast_3 = (NoiseColorMix158).xxx;
				float temp_output_2_0_g158 = _NoiseColorStrength;
				float temp_output_3_0_g158 = ( 1.0 - temp_output_2_0_g158 );
				float3 appendResult7_g158 = (float3(temp_output_3_0_g158 , temp_output_3_0_g158 , temp_output_3_0_g158));
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 Albedo30 = saturate( ( float4( ( DepthFade43 * ( ( temp_cast_3 * temp_output_2_0_g158 ) + appendResult7_g158 ) ) , 0.0 ) * lerpResult161 ) );
				
				float3 temp_cast_6 = (NoiseColorMix158).xxx;
				float temp_output_2_0_g156 = _EmmissiveNoiseColor;
				float temp_output_3_0_g156 = ( 1.0 - temp_output_2_0_g156 );
				float3 appendResult7_g156 = (float3(temp_output_3_0_g156 , temp_output_3_0_g156 , temp_output_3_0_g156));
				float3 temp_cast_8 = (DepthFade43).xxx;
				float temp_output_2_0_g157 = _EmmissiveDepthFade;
				float temp_output_3_0_g157 = ( 1.0 - temp_output_2_0_g157 );
				float3 appendResult7_g157 = (float3(temp_output_3_0_g157 , temp_output_3_0_g157 , temp_output_3_0_g157));
				
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				float3 BaseColor = Albedo30.rgb;
				float3 Emission = ( ( _EmmissiveColor * float4( ( ( temp_cast_6 * temp_output_2_0_g156 ) + appendResult7_g156 ) , 0.0 ) ) * float4( ( ( temp_cast_8 * temp_output_2_0_g157 ) + appendResult7_g157 ) , 0.0 ) ).rgb;
				float Alpha = ( DepthFade43 * BaseColorAlpha162 );
				float AlphaClipThreshold = 0.0;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = BaseColor;
				metaInput.Emission = Emission;
				#ifdef EDITOR_VISUALIZATION
					metaInput.VizUV = IN.VizUV.xy;
					metaInput.LightCoord = IN.LightCoord;
				#endif

				return UnityMetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define ASE_PHONG_TESSELLATION
			#define ASE_DEPTH_WRITE_ON
			#define _SURFACE_TYPE_TRANSPARENT 1
			#define ASE_TESSELLATION 1
			#pragma require tessellation tessHW
			#pragma hull HullFunction
			#pragma domain DomainFunction
			#define ASE_TRANSLUCENCY 1
			#define ASE_LENGTH_TESSELLATION
			#define _EMISSION
			#define _ALPHATEST_ON 1
			#define ASE_SRP_VERSION 140004
			#define REQUIRE_DEPTH_TEXTURE 1


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float3 ase_normal : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord2 = screenPos38;
				
				o.ase_texcoord3 = v.positionOS;
				o.ase_normal = v.normalOS;
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					o.positionWS = vertexInput.positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.positionCS = vertexInput.positionCS;

				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = IN.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float4 screenPos38 = IN.ase_texcoord2;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),IN.ase_texcoord3);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float NoiseColor3D151 = temp_output_118_0;
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( IN.ase_normal, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float NoiseColorVoronoi152 = temp_output_190_0;
				float lerpResult157 = lerp( NoiseColor3D151 , NoiseColorVoronoi152 , _NoiseColorMix);
				float NoiseColorMix158 = lerpResult157;
				float3 temp_cast_3 = (NoiseColorMix158).xxx;
				float temp_output_2_0_g158 = _NoiseColorStrength;
				float temp_output_3_0_g158 = ( 1.0 - temp_output_2_0_g158 );
				float3 appendResult7_g158 = (float3(temp_output_3_0_g158 , temp_output_3_0_g158 , temp_output_3_0_g158));
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 Albedo30 = saturate( ( float4( ( DepthFade43 * ( ( temp_cast_3 * temp_output_2_0_g158 ) + appendResult7_g158 ) ) , 0.0 ) * lerpResult161 ) );
				
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				float3 BaseColor = Albedo30.rgb;
				float Alpha = ( DepthFade43 * BaseColorAlpha162 );
				float AlphaClipThreshold = 0.0;

				half4 color = half4(BaseColor, Alpha );

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				return color;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormalsOnly" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			
            #define _NORMAL_DROPOFF_TS 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #define ASE_FOG 1
            #define ASE_PHONG_TESSELLATION
            #define ASE_DEPTH_WRITE_ON
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TESSELLATION 1
            #pragma require tessellation tessHW
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #define ASE_TRANSLUCENCY 1
            #define ASE_LENGTH_TESSELLATION
            #define _EMISSION
            #define _ALPHATEST_ON 1
            #define ASE_SRP_VERSION 140004
            #define REQUIRE_DEPTH_TEXTURE 1


			
            #pragma multi_compile _ DOTS_INSTANCING_ON
		

			
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
		

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

			

			

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float4 worldTangent : TEXCOORD2;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD3;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD4;
				#endif
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			VertexOutput VertexFunction( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord5 = screenPos38;
				
				o.ase_color = v.ase_color;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;
				v.tangentOS = v.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( v.positionOS.xyz );

				float3 normalWS = TransformObjectToWorldNormal( v.normalOS );
				float4 tangentWS = float4( TransformObjectToWorldDir( v.tangentOS.xyz ), v.tangentOS.w );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					o.positionWS = vertexInput.positionWS;
				#endif

				o.worldNormal = normalWS;
				o.worldTangent = tangentWS;

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				o.positionCS = vertexInput.positionCS;
				o.clipPosV = vertexInput.positionCS;
				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.tangentOS = v.tangentOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			void frag(	VertexOutput IN
						, out half4 outNormalWS : SV_Target0
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = IN.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float3 WorldNormal = IN.worldNormal;
				float4 WorldTangent = IN.worldTangent;

				float4 ClipPos = IN.clipPosV;
				float4 ScreenPos = ComputeScreenPos( IN.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = IN.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float4 screenPos38 = IN.ase_texcoord5;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				float3 Normal = float3(0, 0, 1);
				float Alpha = ( DepthFade43 * BaseColorAlpha162 );
				float AlphaClipThreshold = 0.0;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = IN.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( IN.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(WorldNormal);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float crossSign = (WorldTangent.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
							float3 bitangent = crossSign * cross(WorldNormal.xyz, WorldTangent.xyz);
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent.xyz, bitangent, WorldNormal.xyz));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = WorldNormal;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			
            #define _NORMAL_DROPOFF_TS 1
            #define ASE_FOG 1
            #define ASE_PHONG_TESSELLATION
            #define ASE_DEPTH_WRITE_ON
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TESSELLATION 1
            #pragma require tessellation tessHW
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #define ASE_TRANSLUCENCY 1
            #define ASE_LENGTH_TESSELLATION
            #define _EMISSION
            #define _ALPHATEST_ON 1
            #define ASE_SRP_VERSION 140004
            #define REQUIRE_DEPTH_TEXTURE 1


			
            #pragma multi_compile _ DOTS_INSTANCING_ON
		

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			

			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord = screenPos38;
				
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				float3 positionWS = TransformObjectToWorld( v.positionOS.xyz );

				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float4 screenPos38 = IN.ase_texcoord;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				surfaceDescription.Alpha = ( DepthFade43 * BaseColorAlpha162 );
				surfaceDescription.AlphaClipThreshold = 0.0;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

				return outColor;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			
            #define _NORMAL_DROPOFF_TS 1
            #define ASE_FOG 1
            #define ASE_PHONG_TESSELLATION
            #define ASE_DEPTH_WRITE_ON
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TESSELLATION 1
            #pragma require tessellation tessHW
            #pragma hull HullFunction
            #pragma domain DomainFunction
            #define ASE_TRANSLUCENCY 1
            #define ASE_LENGTH_TESSELLATION
            #define _EMISSION
            #define _ALPHATEST_ON 1
            #define ASE_SRP_VERSION 140004
            #define REQUIRE_DEPTH_TEXTURE 1


			
            #pragma multi_compile _ DOTS_INSTANCING_ON
		

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			

			

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			

			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_VERT_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct VertexInput
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 positionCS : SV_POSITION;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _TranslucencyColor;
			float4 _EmmissiveColor;
			float4 _BaseColor;
			float4 _Mask1;
			float4 _Mask0;
			float3 _NoiseScale3D;
			float3 _WindDirection;
			float3 _AdditionalNoiseDir;
			float3 _AdditionalVoronoiDir;
			float _EmmissiveNoiseColor;
			float _VertexColorStrength;
			float _NoiseColorStrength;
			float _NoiseColorMix;
			float _DepthDistance;
			float _VoronoiStrength;
			float _EmmissiveDepthFade;
			float _VorSpeed;
			float _VoronoiScale;
			float _NoiseStrength;
			float _NoiseScale;
			float _WindSpeed;
			float _NoiseSpeed;
			float _NoiseDisplacement;
			float _Scale;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			float2 voronoihash97( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi97( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash97( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash96( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi96( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash96( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			
			float2 voronoihash89( float2 p )
			{
				
				p = float2( dot( p, float2( 127.1, 311.7 ) ), dot( p, float2( 269.5, 183.3 ) ) );
				return frac( sin( p ) *43758.5453);
			}
			
			float voronoi89( float2 v, float time, inout float2 id, inout float2 mr, float smoothness, inout float2 smoothId )
			{
				float2 n = floor( v );
				float2 f = frac( v );
				float F1 = 8.0;
				float F2 = 8.0; float2 mg = 0;
				for ( int j = -1; j <= 1; j++ )
				{
					for ( int i = -1; i <= 1; i++ )
				 	{
				 		float2 g = float2( i, j );
				 		float2 o = voronoihash89( n + g );
						o = ( sin( time + o * 6.2831 ) * 0.5 + 0.5 ); float2 r = f - g - o;
						float d = 0.5 * dot( r, r );
				 		if( d<F1 ) {
				 			F2 = F1;
				 			F1 = d; mg = g; mr = r; id = o;
				 		} else if( d<F2 ) {
				 			F2 = d;
				
				 		}
				 	}
				}
				return F1;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			VertexOutput VertexFunction(VertexInput v  )
			{
				VertexOutput o;
				ZERO_INITIALIZE(VertexOutput, o);

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				#if ( SHADER_TARGET >= 50 )
				float recip367 = rcp( _Scale );
				#else
				float recip367 = 1.0 / _Scale;
				#endif
				float Scale228 = recip367;
				float4 appendResult329 = (float4(_NoiseScale3D.x , _NoiseScale3D.y , _NoiseScale3D.z , 0.0));
				float3 WindDir317 = ( ( float3( 1, 1, 1 ) * -2 + 1 ) * _WindDirection );
				float3 break332 = WindDir317;
				float temp_output_333_0 = ( abs( break332.x ) + abs( break332.y ) + abs( break332.z ) );
				float4 appendResult340 = (float4(( break332.x / temp_output_333_0 ) , ( break332.y / temp_output_333_0 ) , ( break332.z / temp_output_333_0 ) , 0.0));
				float4 WindDirWeights341 = appendResult340;
				float4 break344 = abs( WindDirWeights341 );
				float4 appendResult330 = (float4(_NoiseScale3D.y , _NoiseScale3D.x , _NoiseScale3D.z , 0.0));
				float4 appendResult331 = (float4(_NoiseScale3D.y , _NoiseScale3D.z , _NoiseScale3D.x , 0.0));
				float4 transform70 = mul(GetObjectToWorldMatrix(),v.positionOS);
				float4 temp_output_71_0 = (transform70).xyzw;
				float simplePerlin3D112 = snoise( ( Scale228 * ( ( ( appendResult329 * break344.x ) + ( appendResult330 * break344.y ) + ( appendResult331 * break344.z ) ) * (temp_output_71_0*1.0 + float4( ( ( WindDir317 + _AdditionalNoiseDir ) * ( _NoiseSpeed * _TimeParameters.x * _WindSpeed ) ) , 0.0 )) ) ).xyz*_NoiseScale );
				simplePerlin3D112 = simplePerlin3D112*0.5 + 0.5;
				float temp_output_118_0 = saturate( (0.0 + (simplePerlin3D112 - 0.0) * (1.0 - 0.0) / (1.0 - 0.0)) );
				float temp_output_198_0 = ( Scale228 * _VoronoiScale );
				float time97 = 0.0;
				float2 voronoiSmoothId97 = 0;
				float4 break84 = (temp_output_71_0*1.0 + float4( ( ( _TimeParameters.x * _VorSpeed * _WindSpeed ) * ( WindDir317 + _AdditionalVoronoiDir ) ) , 0.0 ));
				float2 appendResult85 = (float2(break84.z , break84.y));
				float2 coords97 = appendResult85 * temp_output_198_0;
				float2 id97 = 0;
				float2 uv97 = 0;
				float fade97 = 0.5;
				float voroi97 = 0;
				float rest97 = 0;
				for( int it97 = 0; it97 <3; it97++ ){
				voroi97 += fade97 * voronoi97( coords97, time97, id97, uv97, 0,voronoiSmoothId97 );
				rest97 += fade97;
				coords97 *= 2;
				fade97 *= 0.5;
				}//Voronoi97
				voroi97 /= rest97;
				float3 objToWorldDir91 = mul( GetObjectToWorldMatrix(), float4( v.normalOS, 0 ) ).xyz;
				float3 temp_output_93_0 = pow( abs( objToWorldDir91 ) , 5.0 );
				float dotResult94 = dot( temp_output_93_0 , float3( 1,1,1 ) );
				float3 break104 = ( temp_output_93_0 / dotResult94 );
				float time96 = 217.0;
				float2 voronoiSmoothId96 = 0;
				float2 appendResult86 = (float2(break84.z , break84.x));
				float2 coords96 = appendResult86 * temp_output_198_0;
				float2 id96 = 0;
				float2 uv96 = 0;
				float fade96 = 0.5;
				float voroi96 = 0;
				float rest96 = 0;
				for( int it96 = 0; it96 <3; it96++ ){
				voroi96 += fade96 * voronoi96( coords96, time96, id96, uv96, 0,voronoiSmoothId96 );
				rest96 += fade96;
				coords96 *= 2;
				fade96 *= 0.5;
				}//Voronoi96
				voroi96 /= rest96;
				float time89 = 137.0;
				float2 voronoiSmoothId89 = 0;
				float2 appendResult87 = (float2(break84.x , break84.y));
				float2 coords89 = appendResult87 * temp_output_198_0;
				float2 id89 = 0;
				float2 uv89 = 0;
				float fade89 = 0.5;
				float voroi89 = 0;
				float rest89 = 0;
				for( int it89 = 0; it89 <3; it89++ ){
				voroi89 += fade89 * voronoi89( coords89, time89, id89, uv89, 0,voronoiSmoothId89 );
				rest89 += fade89;
				coords89 *= 2;
				fade89 *= 0.5;
				}//Voronoi89
				voroi89 /= rest89;
				float temp_output_190_0 = saturate( (0.0 + (( 1.0 - ( ( ( voroi97 * break104.x ) + ( voroi96 * break104.y ) ) + ( voroi89 * break104.z ) ) ) - 0.65) * (1.0 - 0.0) / (1.0 - 0.65)) );
				float temp_output_122_0 = saturate( ( ( temp_output_118_0 * _NoiseStrength ) + ( temp_output_190_0 * _VoronoiStrength ) ) );
				float3 ase_objectScale = float3( length( GetObjectToWorldMatrix()[ 0 ].xyz ), length( GetObjectToWorldMatrix()[ 1 ].xyz ), length( GetObjectToWorldMatrix()[ 2 ].xyz ) );
				float3 temp_output_184_0 = ( ( ( temp_output_122_0 - 0.5 ) * v.normalOS * ( _NoiseDisplacement / Scale228 ) ) / ase_objectScale );
				float4 temp_output_2_0_g146 = _Mask0;
				float3 worldToObj306 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g146).xyz, 1 ) ).xyz;
				float3 ase_objectPosition = GetAbsolutePositionWS( UNITY_MATRIX_M._m03_m13_m23 );
				float4 appendResult313 = (float4((temp_output_2_0_g146).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj307 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult313 ).xyz, 1 ) ).xyz;
				float3 temp_output_304_0 = ( temp_output_184_0 + ( min( ( distance( ( temp_output_184_0 + v.positionOS.xyz ) , worldToObj306 ) - length( worldToObj307 ) ) , 0.0 ) * v.normalOS ) );
				float4 temp_output_2_0_g153 = _Mask1;
				float3 worldToObj357 = mul( GetWorldToObjectMatrix(), float4( (temp_output_2_0_g153).xyz, 1 ) ).xyz;
				float4 appendResult354 = (float4((temp_output_2_0_g153).w , 0.0 , 0.0 , 0.0));
				float3 worldToObj358 = mul( GetWorldToObjectMatrix(), float4( ( float4( ase_objectPosition , 0.0 ) + appendResult354 ).xyz, 1 ) ).xyz;
				float3 VertexOffset305 = ( temp_output_304_0 + ( min( ( distance( ( temp_output_304_0 + v.positionOS.xyz ) , worldToObj357 ) - length( worldToObj358 ) ) , 0.0 ) * v.normalOS ) );
				
				float3 vertexPos38 = ( VertexOffset305 + v.positionOS.xyz );
				float4 ase_clipPos38 = TransformObjectToHClip((vertexPos38).xyz);
				float4 screenPos38 = ComputeScreenPos(ase_clipPos38);
				o.ase_texcoord = screenPos38;
				
				o.ase_color = v.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = v.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = VertexOffset305;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif

				v.normalOS = v.normalOS;

				float3 positionWS = TransformObjectToWorld( v.positionOS.xyz );
				o.positionCS = TransformWorldToHClip(positionWS);

				return o;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( VertexInput v )
			{
				VertexControl o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				o.vertex = v.positionOS;
				o.normalOS = v.normalOS;
				o.ase_color = v.ase_color;
				return o;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> v)
			{
				TessellationFactors o;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(v[0].vertex, v[1].vertex, v[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				o.edge[0] = tf.x; o.edge[1] = tf.y; o.edge[2] = tf.z; o.inside = tf.w;
				return o;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			VertexOutput DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				VertexInput o = (VertexInput) 0;
				o.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				o.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				o.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = o.positionOS.xyz - patch[i].normalOS * (dot(o.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				o.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * o.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], o);
				return VertexFunction(o);
			}
			#else
			VertexOutput vert ( VertexInput v )
			{
				return VertexFunction( v );
			}
			#endif

			half4 frag(VertexOutput IN ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				float4 screenPos38 = IN.ase_texcoord;
				float4 ase_screenPosNorm38 = screenPos38 / screenPos38.w;
				ase_screenPosNorm38.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm38.z : ase_screenPosNorm38.z * 0.5 + 0.5;
				float screenDepth38 = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH( ase_screenPosNorm38.xy ),_ZBufferParams);
				float distanceDepth38 = abs( ( screenDepth38 - LinearEyeDepth( ase_screenPosNorm38.z,_ZBufferParams ) ) / ( _DepthDistance ) );
				float temp_output_1_0_g154 = distanceDepth38;
				float DepthFade43 = saturate( ( ( ( pow( temp_output_1_0_g154 , 5.0 ) * 6.0 ) - ( pow( temp_output_1_0_g154 , 4.0 ) * 15.0 ) ) + ( pow( temp_output_1_0_g154 , 3.0 ) * 10.0 ) ) );
				float4 lerpResult161 = lerp( _BaseColor , IN.ase_color , _VertexColorStrength);
				float4 temp_output_2_0_g155 = lerpResult161;
				float BaseColorAlpha162 = (temp_output_2_0_g155).a;
				

				surfaceDescription.Alpha = ( DepthFade43 * BaseColorAlpha162 );
				surfaceDescription.AlphaClipThreshold = 0.0;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
						clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

				return outColor;
			}

			ENDHLSL
		}
		
	}
	
	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19603
Node;AmplifyShaderEditor.Vector3Node;316;-1488,1952;Inherit;False;Property;_WindDirection;WindDirection;4;0;Create;True;0;0;0;False;0;False;-1,-0.3,-0.3;-1,-0.3,-0.3;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.FlipNode;328;-1232,1952;Inherit;False;True;True;True;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;317;-1040,1952;Inherit;False;WindDir;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;326;-1968,1600;Inherit;False;317;WindDir;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;332;-1776,1600;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.AbsOpNode;334;-1616,1568;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;335;-1616,1648;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.AbsOpNode;336;-1616,1728;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;130;-768,1424;Inherit;False;4809.925;1890.271;;80;342;344;343;348;346;347;330;331;143;114;152;151;128;127;239;238;184;183;123;124;232;181;229;125;122;121;115;120;119;116;118;190;191;117;112;192;196;113;111;195;145;109;110;78;89;108;107;77;96;97;87;322;198;74;85;86;320;76;84;88;197;72;228;79;227;83;71;323;91;80;70;134;325;82;90;81;69;329;345;367;Tri Planar Noise;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;333;-1472,1616;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleTimeNode;73;-1008,2240;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;337;-1344,1568;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;338;-1344,1664;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;339;-1344,1760;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;325;-624,2656;Inherit;False;317;WindDir;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;319;-1008,2144;Inherit;False;Property;_WindSpeed;WindSpeed;5;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;81;-672,2544;Inherit;False;Property;_VorSpeed;Vor Speed;12;0;Create;True;0;0;0;False;0;False;0.05;0.05;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;82;-656,2736;Inherit;False;Property;_AdditionalVoronoiDir;AdditionalVoronoiDir;14;0;Create;True;0;0;0;False;0;False;0.7,-0.3,-0.3;0.7,-0.3,-0.3;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.NormalVertexDataNode;90;-400,3088;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.PosVertexDataNode;69;-656,2208;Inherit;False;1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;134;48,2800;Inherit;False;999.4751;434.6895;Smooth Axis Fade ;5;93;92;104;95;94;;1,1,1,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode;340;-1184,1632;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ObjectToWorldTransfNode;70;-448,2208;Inherit;False;1;0;FLOAT4;0,0,0,1;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;80;-448,2448;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TransformDirectionNode;91;-208,3088;Inherit;False;Object;World;False;Fast;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;323;-400,2640;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;227;-464,1632;Inherit;False;Property;_Scale;Scale;3;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;341;-1024,1632;Inherit;False;WindDirWeights;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode;71;-192,2208;Inherit;False;True;True;True;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;83;-224,2448;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.AbsOpNode;92;64,2880;Inherit;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ReciprocalOpNode;367;-304,1632;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;93;272,2928;Inherit;False;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;5;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode;79;144,2368;Inherit;False;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;1;False;2;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;228;-144,1632;Inherit;False;Scale;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;342;112,1808;Inherit;False;341;WindDirWeights;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DotProductOpNode;94;448,3008;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;1,1,1;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;197;464,2096;Inherit;False;228;Scale;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;84;400,2368;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.GetLocalVarNode;320;-608,1824;Inherit;False;317;WindDir;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.AbsOpNode;343;320,1808;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;72;-656,2064;Inherit;False;Property;_NoiseSpeed;Noise Speed;11;0;Create;True;0;0;0;False;0;False;0.2;0.2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;76;-640,1904;Inherit;False;Property;_AdditionalNoiseDir;Additional Noise Dir;13;0;Create;True;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;88;464,2176;Inherit;False;Property;_VoronoiScale;Voronoi Scale;15;0;Create;True;0;0;0;False;0;False;1.4;1.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;143;400,1648;Inherit;False;Property;_NoiseScale3D;Noise Scale 3D;20;0;Create;True;0;0;0;False;0;False;1,0.2,0.6;1,0.2,0.6;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;86;608,2400;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;85;608,2304;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;95;608,2928;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;74;-448,2064;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;198;656,2096;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;322;-368,1936;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;331;624,1808;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;329;624,1504;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;330;624,1648;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.BreakToComponentsNode;344;432,1808;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.DynamicAppendNode;87;608,2496;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.BreakToComponentsNode;104;832,2992;Inherit;True;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.VoronoiNode;97;912,1968;Inherit;True;0;0;1;0;3;False;1;False;False;False;4;0;FLOAT2;0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT2;1;FLOAT2;2
Node;AmplifyShaderEditor.VoronoiNode;96;912,2240;Inherit;True;0;0;1;0;3;False;1;False;False;False;4;0;FLOAT2;0,0;False;1;FLOAT;217;False;2;FLOAT;1;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT2;1;FLOAT2;2
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;77;-224,2000;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;347;784,1808;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;346;784,1648;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;345;784,1504;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;107;1232,2032;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;108;1216,2192;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VoronoiNode;89;912,2512;Inherit;True;0;0;1;0;3;False;1;False;False;False;4;0;FLOAT2;0,0;False;1;FLOAT;137;False;2;FLOAT;1;False;3;FLOAT;0;False;3;FLOAT;0;FLOAT2;1;FLOAT2;2
Node;AmplifyShaderEditor.ScaleAndOffsetNode;78;144,2016;Inherit;False;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;1;False;2;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;348;976,1616;Inherit;False;3;3;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;110;1408,2128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;109;1184,2448;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;145;1328,1712;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;195;1296,1632;Inherit;False;228;Scale;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;111;1568,2256;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;196;1536,1680;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;113;1536,1840;Inherit;False;Property;_NoiseScale;Noise Scale;16;0;Create;True;0;0;0;False;0;False;20;20;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;112;1760,1712;Inherit;True;Simplex3D;True;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;192;1792,2144;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;117;2064,1728;Inherit;True;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;191;1824,2288;Inherit;True;5;0;FLOAT;0;False;1;FLOAT;0.65;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;190;2112,2304;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;118;2352,1728;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;119;2336,1824;Inherit;False;Property;_NoiseStrength;Noise Strength;18;0;Create;True;0;0;0;False;0;False;0.03;0.04;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;116;2272,2192;Inherit;False;Property;_VoronoiStrength;Voronoi Strength;17;0;Create;True;0;0;0;False;0;False;0.9;0.8;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;120;2544,1760;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;115;2544,2000;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;293;4112,1984;Inherit;False;1979.398;736.2354;;10;306;296;294;315;304;303;299;301;298;297;Sphere Mask 0;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;121;2800,1904;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;122;3024,1904;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;229;3056,2272;Inherit;False;228;Scale;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;125;3024,2192;Inherit;False;Property;_NoiseDisplacement;Noise Displacement;19;0;Create;True;0;0;0;False;0;False;0.5;0.5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;294;4160,2224;Inherit;False;Property;_Mask0;Mask0;6;0;Create;True;0;0;0;False;0;False;1.985208,0.440832,0.2055923,1.2;-3.25,0.81,0.08,0.7;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;315;4368,2416;Inherit;False;932;266.2;Radius to object space;5;314;307;312;311;313;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;181;3264,1904;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;232;3264,2192;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;124;3232,2016;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;296;4432,2224;Inherit;False;Alpha Split;-1;;146;07dab7960105b86429ac8eebd729ed6d;0;1;2;FLOAT4;0,0,0,0;False;2;FLOAT3;0;FLOAT;6
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;123;3456,1920;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ObjectScaleNode;183;3472,2096;Inherit;False;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;313;4624,2496;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ObjectPositionNode;311;4416,2464;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleDivideOpNode;184;3648,2000;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PosVertexDataNode;238;3696,2160;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;312;4784,2464;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;239;3920,2112;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformPositionNode;306;4656,2224;Inherit;False;World;Object;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TransformPositionNode;307;4912,2464;Inherit;False;World;Object;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;350;6464,2000;Inherit;False;1780.413;726.1177;;10;365;364;363;362;361;359;357;353;352;351;Sphere Mask 1;1,1,1,1;0;0
Node;AmplifyShaderEditor.DistanceOpNode;297;4880,2128;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LengthOpNode;314;5120,2464;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;298;5168,2112;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;351;6512,2240;Inherit;False;Property;_Mask1;Mask1;7;0;Create;True;0;0;0;False;0;False;1.985208,0.440832,0.2055923,1.2;-1.926,-0.28,-0.223,0.7;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode;352;6720,2432;Inherit;False;932;266.2;Radius to object space;5;360;358;356;355;354;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMinOpNode;301;5328,2112;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;299;5264,2240;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FunctionNode;353;6784,2240;Inherit;False;Alpha Split;-1;;153;07dab7960105b86429ac8eebd729ed6d;0;1;2;FLOAT4;0,0,0,0;False;2;FLOAT3;0;FLOAT;6
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;303;5456,2112;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;-1,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;354;6976,2512;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ObjectPositionNode;355;6768,2480;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleAddOpNode;304;5632,2048;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.PosVertexDataNode;349;6128,2208;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;356;7136,2480;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;366;6336,2144;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TransformPositionNode;357;7008,2240;Inherit;False;World;Object;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TransformPositionNode;358;7264,2480;Inherit;False;World;Object;False;Fast;True;1;0;FLOAT3;0,0,0;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DistanceOpNode;359;7232,2144;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LengthOpNode;360;7472,2480;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;361;7648,2144;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMinOpNode;362;7808,2144;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.NormalVertexDataNode;363;7744,2272;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;364;7936,2144;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;-1,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;365;8112,2080;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;44;880,784;Inherit;False;1119.002;389.2855;;7;43;135;36;38;35;33;34;Depth Fade;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;305;8336,2080;Inherit;False;VertexOffset;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;31;1388.116,-224;Inherit;False;1507.612;770.0389;;15;1;162;13;16;160;161;20;19;146;147;21;30;28;29;163;Albedo;1,1,1,1;0;0
Node;AmplifyShaderEditor.PosVertexDataNode;34;912,928;Inherit;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;33;912,848;Inherit;False;305;VertexOffset;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;35;1136,848;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VertexColorNode;16;1664,288;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;36;912,1088;Inherit;False;Property;_DepthDistance;Depth Distance;8;0;Create;True;0;0;0;False;0;False;0.4;0.4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;13;1440,176;Inherit;False;Property;_BaseColor;Base Color;0;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;160;1872,368;Inherit;False;Property;_VertexColorStrength;Vertex Color Strength;23;0;Create;True;0;0;0;False;0;False;0.7030082;0.7030082;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;38;1296,848;Inherit;False;True;False;True;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;161;2160,240;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;135;1568,848;Inherit;False;SmootherStep;-1;;154;4a9458489eca43748a1c8f13823257ba;0;1;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;163;2325.137,419.9323;Inherit;False;Alpha Split;-1;;155;07dab7960105b86429ac8eebd729ed6d;0;1;2;COLOR;0,0,0,0;False;2;FLOAT3;0;FLOAT;6
Node;AmplifyShaderEditor.RegisterLocalVarNode;43;1776,848;Inherit;False;DepthFade;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;162;2528,448;Inherit;False;BaseColorAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;68;1392,-1504;Inherit;False;1082.072;560.847;;9;150;140;149;141;60;66;62;61;67;Emmission;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;59;1744,-704;Inherit;False;1151.964;403.8841;;7;56;58;57;55;54;136;137;Translucency;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode;164;2608,-976;Inherit;False;162;BaseColorAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;133;2640,-1056;Inherit;False;43;DepthFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;29;2496,224;Inherit;False;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;28;2336,224;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FresnelNode;54;1792,-656;Inherit;False;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;55;2208,-656;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;2432,-656;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;136;2672,-656;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;137;2464,-416;Inherit;False;43;DepthFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;30;2672,224;Inherit;False;Albedo;-1;True;1;0;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;147;2224,80;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;146;2048,-32;Inherit;False;43;DepthFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;157;2608,944;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;158;2768,944;Inherit;True;NoiseColorMix;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;154;2336,720;Inherit;True;151;NoiseColor3D;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;155;2304,912;Inherit;True;152;NoiseColorVoronoi;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;127;3232,1776;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;128;3408,1776;Inherit;False;NoiseColor;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;151;2544,1680;Inherit;False;NoiseColor3D;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;152;2544,2224;Inherit;False;NoiseColorVoronoi;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;114;2272,1952;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;53;2832,-832;Inherit;False;305;VertexOffset;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;156;2272,1104;Inherit;False;Property;_NoiseColorMix;NoiseColorMix;22;0;Create;True;0;0;0;False;0;False;0.96;0.947578;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;67;1904,-1232;Inherit;False;Lerp White To;-1;;156;047d7c189c36a62438973bad9d37b1c2;0;2;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;61;2112,-1328;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;141;2304,-1296;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;1,1,1;False;1;COLOR;0
Node;AmplifyShaderEditor.FunctionNode;149;2112,-1216;Inherit;False;Lerp White To;-1;;157;047d7c189c36a62438973bad9d37b1c2;0;2;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;140;1872,-1120;Inherit;False;43;DepthFade;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;62;1616,-1232;Inherit;False;158;NoiseColorMix;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;21;2000,80;Inherit;False;Lerp White To;-1;;158;047d7c189c36a62438973bad9d37b1c2;0;2;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;19;1760,32;Inherit;False;158;NoiseColorMix;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;20;1696,112;Inherit;False;Property;_NoiseColorStrength;Noise Color Strength;9;0;Create;True;0;0;0;False;0;False;0.5565214;0.7018638;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;56;2032,-576;Inherit;False;Constant;_Float0;Float 0;3;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;66;1552,-1152;Inherit;False;Property;_EmmissiveNoiseColor;Emmissive Noise Color;10;0;Create;True;0;0;0;False;0;False;0.2000003;0.2000003;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;150;1776,-1040;Inherit;False;Property;_EmmissiveDepthFade;Emmissive Depth Fade;21;0;Create;True;0;0;0;False;0;False;0.2086951;0.2086951;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;60;1872,-1440;Inherit;False;Property;_EmmissiveColor;Emmissive Color;2;0;Create;True;0;0;0;False;0;False;0.5490196,0.627451,0.7294118,0;0.5482876,0.6286016,0.7283019,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;58;2192,-528;Inherit;False;Property;_TranslucencyColor;Translucency Color;1;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;131;2880,-1104;Inherit;False;Constant;_MetalSmoothness;MetalSmoothness;14;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;132;2832,-928;Inherit;False;Constant;_AlphaClip;Alpha Clip;18;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;165;2880,-1024;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;32;2912,-1184;Inherit;False;30;Albedo;1;0;OBJECT;;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;1;1472,48;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;3;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;4;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;True;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;5;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;6;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;2;5;False;;10;False;;1;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;7;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormalsOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;8;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;2;5;False;;10;False;;1;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;10;False;;10;False;;10;False;;6;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;9;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;10;48,-176;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;2;3088,-1184;Float;False;True;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;SkyCloudsEdge;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;2;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Transparent=RenderType;Queue=Transparent=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;2;5;False;;10;False;;1;1;False;;10;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;True;True;False;10;False;;10;False;;10;False;;6;False;;3;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForwardOnly;False;False;0;;0;0;Standard;42;Lighting Model;0;0;Workflow;1;638545903288036264;Surface;1;638546043907316520;  Refraction Model;0;0;  Blend;0;0;Two Sided;1;0;Fragment Normal Space,InvertActionOnDeselection;0;0;Forward Only;1;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;1;638555967056712408;  Translucency Strength;1,False,_TransStrength;0;  Normal Distortion;0.5,False,_NormalDistortion;0;  Scattering;2,False,_Scattering;0;  Direct;0.9,False,_Direct;0;  Ambient;0.1,False,_Ambient;0;  Shadow;0.5,False,_Shadow;0;Cast Shadows;0;638545778176623389;  Use Shadow Threshold;0;0;Receive Shadows;1;0;Receive SSAO;0;638545838208863654;GPU Instancing;1;0;LOD CrossFade;1;0;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;1;638546589066032097;  Phong;1;638545776167988407;  Strength;0.5,False,;0;  Type;2;638572589216977204;  Tess;8,False,_TessellationFactor;638555223982842409;  Min;1,False,_TessellationMinDistance;638549165261763217;  Max;20,False,_TessellationMaxDistance;638549165187283418;  Edge Length;7,False,;638546587549290024;  Max Displacement;10,False,;638546587107747633;Write Depth;1;638545930608142192;  Early Z;0;638546026961079552;Vertex Position,InvertActionOnDeselection;1;0;Debug Display;0;0;Clear Coat;0;0;0;10;False;True;False;True;True;True;True;False;True;True;False;;False;0
WireConnection;328;0;316;0
WireConnection;317;0;328;0
WireConnection;332;0;326;0
WireConnection;334;0;332;0
WireConnection;335;0;332;1
WireConnection;336;0;332;2
WireConnection;333;0;334;0
WireConnection;333;1;335;0
WireConnection;333;2;336;0
WireConnection;337;0;332;0
WireConnection;337;1;333;0
WireConnection;338;0;332;1
WireConnection;338;1;333;0
WireConnection;339;0;332;2
WireConnection;339;1;333;0
WireConnection;340;0;337;0
WireConnection;340;1;338;0
WireConnection;340;2;339;0
WireConnection;70;0;69;0
WireConnection;80;0;73;0
WireConnection;80;1;81;0
WireConnection;80;2;319;0
WireConnection;91;0;90;0
WireConnection;323;0;325;0
WireConnection;323;1;82;0
WireConnection;341;0;340;0
WireConnection;71;0;70;0
WireConnection;83;0;80;0
WireConnection;83;1;323;0
WireConnection;92;0;91;0
WireConnection;367;0;227;0
WireConnection;93;0;92;0
WireConnection;79;0;71;0
WireConnection;79;2;83;0
WireConnection;228;0;367;0
WireConnection;94;0;93;0
WireConnection;84;0;79;0
WireConnection;343;0;342;0
WireConnection;86;0;84;2
WireConnection;86;1;84;0
WireConnection;85;0;84;2
WireConnection;85;1;84;1
WireConnection;95;0;93;0
WireConnection;95;1;94;0
WireConnection;74;0;72;0
WireConnection;74;1;73;0
WireConnection;74;2;319;0
WireConnection;198;0;197;0
WireConnection;198;1;88;0
WireConnection;322;0;320;0
WireConnection;322;1;76;0
WireConnection;331;0;143;2
WireConnection;331;1;143;3
WireConnection;331;2;143;1
WireConnection;329;0;143;1
WireConnection;329;1;143;2
WireConnection;329;2;143;3
WireConnection;330;0;143;2
WireConnection;330;1;143;1
WireConnection;330;2;143;3
WireConnection;344;0;343;0
WireConnection;87;0;84;0
WireConnection;87;1;84;1
WireConnection;104;0;95;0
WireConnection;97;0;85;0
WireConnection;97;2;198;0
WireConnection;96;0;86;0
WireConnection;96;2;198;0
WireConnection;77;0;322;0
WireConnection;77;1;74;0
WireConnection;347;0;331;0
WireConnection;347;1;344;2
WireConnection;346;0;330;0
WireConnection;346;1;344;1
WireConnection;345;0;329;0
WireConnection;345;1;344;0
WireConnection;107;0;97;0
WireConnection;107;1;104;0
WireConnection;108;0;96;0
WireConnection;108;1;104;1
WireConnection;89;0;87;0
WireConnection;89;2;198;0
WireConnection;78;0;71;0
WireConnection;78;2;77;0
WireConnection;348;0;345;0
WireConnection;348;1;346;0
WireConnection;348;2;347;0
WireConnection;110;0;107;0
WireConnection;110;1;108;0
WireConnection;109;0;89;0
WireConnection;109;1;104;2
WireConnection;145;0;348;0
WireConnection;145;1;78;0
WireConnection;111;0;110;0
WireConnection;111;1;109;0
WireConnection;196;0;195;0
WireConnection;196;1;145;0
WireConnection;112;0;196;0
WireConnection;112;1;113;0
WireConnection;192;0;111;0
WireConnection;117;0;112;0
WireConnection;191;0;192;0
WireConnection;190;0;191;0
WireConnection;118;0;117;0
WireConnection;120;0;118;0
WireConnection;120;1;119;0
WireConnection;115;0;190;0
WireConnection;115;1;116;0
WireConnection;121;0;120;0
WireConnection;121;1;115;0
WireConnection;122;0;121;0
WireConnection;181;0;122;0
WireConnection;232;0;125;0
WireConnection;232;1;229;0
WireConnection;296;2;294;0
WireConnection;123;0;181;0
WireConnection;123;1;124;0
WireConnection;123;2;232;0
WireConnection;313;0;296;6
WireConnection;184;0;123;0
WireConnection;184;1;183;0
WireConnection;312;0;311;0
WireConnection;312;1;313;0
WireConnection;239;0;184;0
WireConnection;239;1;238;0
WireConnection;306;0;296;0
WireConnection;307;0;312;0
WireConnection;297;0;239;0
WireConnection;297;1;306;0
WireConnection;314;0;307;0
WireConnection;298;0;297;0
WireConnection;298;1;314;0
WireConnection;301;0;298;0
WireConnection;353;2;351;0
WireConnection;303;0;301;0
WireConnection;303;1;299;0
WireConnection;354;0;353;6
WireConnection;304;0;184;0
WireConnection;304;1;303;0
WireConnection;356;0;355;0
WireConnection;356;1;354;0
WireConnection;366;0;304;0
WireConnection;366;1;349;0
WireConnection;357;0;353;0
WireConnection;358;0;356;0
WireConnection;359;0;366;0
WireConnection;359;1;357;0
WireConnection;360;0;358;0
WireConnection;361;0;359;0
WireConnection;361;1;360;0
WireConnection;362;0;361;0
WireConnection;364;0;362;0
WireConnection;364;1;363;0
WireConnection;365;0;304;0
WireConnection;365;1;364;0
WireConnection;305;0;365;0
WireConnection;35;0;33;0
WireConnection;35;1;34;0
WireConnection;38;1;35;0
WireConnection;38;0;36;0
WireConnection;161;0;13;0
WireConnection;161;1;16;0
WireConnection;161;2;160;0
WireConnection;135;1;38;0
WireConnection;163;2;161;0
WireConnection;43;0;135;0
WireConnection;162;0;163;6
WireConnection;29;0;28;0
WireConnection;28;0;147;0
WireConnection;28;1;161;0
WireConnection;55;0;54;0
WireConnection;55;1;56;0
WireConnection;57;0;55;0
WireConnection;57;1;58;0
WireConnection;136;0;57;0
WireConnection;136;1;137;0
WireConnection;30;0;29;0
WireConnection;147;0;146;0
WireConnection;147;1;21;0
WireConnection;157;0;154;0
WireConnection;157;1;155;0
WireConnection;157;2;156;0
WireConnection;158;0;157;0
WireConnection;127;0;122;0
WireConnection;128;0;127;0
WireConnection;151;0;118;0
WireConnection;152;0;190;0
WireConnection;114;0;190;0
WireConnection;67;1;62;0
WireConnection;67;2;66;0
WireConnection;61;0;60;0
WireConnection;61;1;67;0
WireConnection;141;0;61;0
WireConnection;141;1;149;0
WireConnection;149;1;140;0
WireConnection;149;2;150;0
WireConnection;21;1;19;0
WireConnection;21;2;20;0
WireConnection;165;0;133;0
WireConnection;165;1;164;0
WireConnection;2;0;32;0
WireConnection;2;2;141;0
WireConnection;2;3;131;0
WireConnection;2;4;131;0
WireConnection;2;6;165;0
WireConnection;2;7;132;0
WireConnection;2;15;136;0
WireConnection;2;8;53;0
ASEEND*/
//CHKSM=47A5FB06688917647A76034BD1CBF683D5CE0CDD