// UnityChan Toon Shader —— URP 版本（皮肤 / 眼睛 / 睫毛 / 腮红 精简版）
// falloff 阴影 + 边缘光 + 主光阴影接收（无高光/反射/法线贴图）

#ifndef UNITYCHAN_CHARA_SKIN_URP_INCLUDED
#define UNITYCHAN_CHARA_SKIN_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

#define float_t    half
#define float2_t   half2
#define float3_t   half3
#define float4_t   half4

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _ShadowColor;
    float  _EdgeThickness;
    float  _DepthBias;
    float4 _MainTex_ST;
CBUFFER_END

TEXTURE2D(_MainTex);         SAMPLER(sampler_MainTex);
TEXTURE2D(_FalloffSampler);  SAMPLER(sampler_FalloffSampler);
TEXTURE2D(_RimLightSampler); SAMPLER(sampler_RimLightSampler);

#define FALLOFF_POWER 1.0

struct appdata_uchan
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos        : SV_POSITION;
    float3 normal     : TEXCOORD0;
    float2 uv         : TEXCOORD1;
    float3 eyeDir     : TEXCOORD2;
    float3 lightDir   : TEXCOORD3;
    float3 worldPos   : TEXCOORD4;
    float4 shadowCoord: TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

v2f vert( appdata_uchan v )
{
    v2f o = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(v);

    float3 worldPos = TransformObjectToWorld( v.vertex.xyz );
    o.pos      = TransformWorldToHClip( worldPos );
    o.worldPos = worldPos;
    o.uv       = TRANSFORM_TEX( v.texcoord.xy, _MainTex );
    o.normal   = normalize( TransformObjectToWorldNormal( v.normal ) );
    o.eyeDir   = normalize( _WorldSpaceCameraPos.xyz - worldPos );
    o.lightDir = normalize( _MainLightPosition.xyz );
    o.shadowCoord = TransformWorldToShadowCoord( worldPos );
    return o;
}

half4 frag( v2f i ) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);

    float4_t diffSamplerColor = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, i.uv );

    // Falloff
    float_t normalDotEye = dot( i.normal, i.eyeDir );
    float_t falloffU = clamp( 1 - abs( normalDotEye ), 0.02, 0.98 );
    float4_t falloffSamplerColor = FALLOFF_POWER * SAMPLE_TEXTURE2D( _FalloffSampler, sampler_FalloffSampler, float2( falloffU, 0.25f ) );
    float3_t combinedColor = lerp( diffSamplerColor.rgb, falloffSamplerColor.rgb * diffSamplerColor.rgb, falloffSamplerColor.a );

    // Rimlight
    float_t rimlightDot = saturate( 0.5 * ( dot( i.normal, i.lightDir ) + 1.0 ) );
    falloffU = saturate( rimlightDot * falloffU );
    falloffU = SAMPLE_TEXTURE2D( _RimLightSampler, sampler_RimLightSampler, float2( falloffU, 0.25f ) ).r;
    float3_t lightColor = diffSamplerColor.rgb * 0.5;
    combinedColor += falloffU * lightColor;

    // 接收主光阴影
    Light mainLight = GetMainLight( i.shadowCoord );
    float3_t shadowColor = _ShadowColor.rgb * combinedColor;
    float_t attenuation = saturate( 2.0 * mainLight.shadowAttenuation - 1.0 );
    combinedColor = lerp( shadowColor, combinedColor, attenuation );

    return float4_t( combinedColor, diffSamplerColor.a ) * _Color * _MainLightColor;
}

#endif // UNITYCHAN_CHARA_SKIN_URP_INCLUDED
