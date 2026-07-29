// UnityChan Toon Shader —— URP 版本（衣服 / 头发主体）
// 原 built-in ForwardBase 版改写：falloff 阴影 + 高光 + 环境反射 + 法线贴图 + 边缘光 + 主光阴影接收
// 用法：在 .shader 中 #define ENABLE_NORMAL_MAP 后再 #include 本文件

#ifndef UNITYCHAN_CHARA_MAIN_URP_INCLUDED
#define UNITYCHAN_CHARA_MAIN_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Float types（保持原 shader 精度别名）
#define float_t    half
#define float2_t   half2
#define float3_t   half3
#define float4_t   half4
#define float3x3_t half3x3

// 材质参数（SRP Batcher 兼容）
CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _ShadowColor;
    float  _SpecularPower;
    float  _EdgeThickness;
    float  _DepthBias;
    float4 _MainTex_ST;
CBUFFER_END

TEXTURE2D(_MainTex);                   SAMPLER(sampler_MainTex);
TEXTURE2D(_FalloffSampler);            SAMPLER(sampler_FalloffSampler);
TEXTURE2D(_RimLightSampler);           SAMPLER(sampler_RimLightSampler);
TEXTURE2D(_SpecularReflectionSampler); SAMPLER(sampler_SpecularReflectionSampler);
TEXTURE2D(_EnvMapSampler);             SAMPLER(sampler_EnvMapSampler);
TEXTURE2D(_NormalMapSampler);          SAMPLER(sampler_NormalMapSampler);

#define FALLOFF_POWER 0.3

struct appdata_uchan
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float4 tangent  : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos      : SV_POSITION;
    float2 uv       : TEXCOORD0;
    float3 eyeDir   : TEXCOORD1;
    float3 lightDir : TEXCOORD2;
    float3 normal   : TEXCOORD3;
#ifdef ENABLE_NORMAL_MAP
    float3 tangent  : TEXCOORD4;
    float3 binormal : TEXCOORD5;
#endif
    float3 worldPos   : TEXCOORD6;
    float4 shadowCoord: TEXCOORD7;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Vertex shader
v2f vert( appdata_uchan v )
{
    v2f o = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(v);

    float3 worldPos = TransformObjectToWorld( v.vertex.xyz );
    o.pos     = TransformWorldToHClip( worldPos );
    o.worldPos = worldPos;
    o.uv.xy   = TRANSFORM_TEX( v.texcoord.xy, _MainTex );
    o.normal  = normalize( TransformObjectToWorldNormal( v.normal ) );

    // 视线方向
    o.eyeDir  = normalize( _WorldSpaceCameraPos.xyz - worldPos );
    // 主平行光方向（URP：方向光的方向存在 _MainLightPosition.xyz）
    o.lightDir = normalize( _MainLightPosition.xyz );

#ifdef ENABLE_NORMAL_MAP
    o.tangent  = normalize( TransformObjectToWorldDir( v.tangent.xyz ) );
    o.binormal = normalize( cross( o.normal, o.tangent ) * v.tangent.w );
#endif

    o.shadowCoord = TransformWorldToShadowCoord( worldPos );
    return o;
}

// Overlay blend
inline float3_t GetOverlayColor( float3_t inUpper, float3_t inLower )
{
    float3_t oneMinusLower = float3_t( 1.0, 1.0, 1.0 ) - inLower;
    float3_t valUnit = 2.0 * oneMinusLower;
    float3_t minValue = 2.0 * inLower - float3_t( 1.0, 1.0, 1.0 );
    float3_t greaterResult = inUpper * valUnit + minValue;

    float3_t lowerResult = 2.0 * inLower * inUpper;

    half3 lerpVals = round(inLower);
    return lerp(lowerResult, greaterResult, lerpVals);
}

#ifdef ENABLE_NORMAL_MAP
    // 由法线贴图求世界法线（保持原实现：raw*2-1，不走 UnpackNormal）
    inline float3_t GetNormalFromMap( v2f input )
    {
        float3_t normalVec = SAMPLE_TEXTURE2D( _NormalMapSampler, sampler_NormalMapSampler, input.uv ).xyz * 2 - 1;

        float3_t xBasis = float3_t( input.tangent.x, input.binormal.x, input.normal.x );
        float3_t yBasis = float3_t( input.tangent.y, input.binormal.y, input.normal.y );
        float3_t zBasis = float3_t( input.tangent.z, input.binormal.z, input.normal.z );

        normalVec = float3_t(
            dot( normalVec, xBasis ),
            dot( normalVec, yBasis ),
            dot( normalVec, zBasis )
        );
        normalVec = normalize( normalVec );
        return normalVec;
    }
#endif

// Fragment shader
half4 frag( v2f i ) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);

    float4_t diffSamplerColor = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, i.uv.xy );

#ifdef ENABLE_NORMAL_MAP
    float3_t normalVec = GetNormalFromMap( i );
#else
    float3_t normalVec = i.normal;
#endif

    // Falloff：法线与视线夹角 → 渐变查表
    float_t normalDotEye = dot( normalVec, i.eyeDir.xyz );
    float_t falloffU = clamp( 1.0 - abs( normalDotEye ), 0.02, 0.98 );
    float4_t falloffSamplerColor = FALLOFF_POWER * SAMPLE_TEXTURE2D( _FalloffSampler, sampler_FalloffSampler, float2( falloffU, 0.25f ) );
    float3_t shadowColor = diffSamplerColor.rgb * diffSamplerColor.rgb;
    float3_t combinedColor = lerp( diffSamplerColor.rgb, shadowColor, falloffSamplerColor.r );
    combinedColor *= ( 1.0 + falloffSamplerColor.rgb * falloffSamplerColor.a );

    // Specular（以视线当作光向；等价原 lit().z）
    float4_t reflectionMaskColor = SAMPLE_TEXTURE2D( _SpecularReflectionSampler, sampler_SpecularReflectionSampler, i.uv.xy );
    float_t specularDot = dot( normalVec, i.eyeDir.xyz );
    float_t specTerm = ( normalDotEye > 0.0 ) ? pow( max( specularDot, 0.0 ), _SpecularPower ) : 0.0;
    float3_t specularColor = saturate( specTerm ) * reflectionMaskColor.rgb * diffSamplerColor.rgb;
    combinedColor += specularColor;

    // Reflection（球面环境贴图）
    float3_t reflectVector = reflect( -i.eyeDir.xyz, normalVec ).xzy;
    float2_t sphereMapCoords = 0.5 * ( float2_t( 1.0, 1.0 ) + reflectVector.xy );
    float3_t reflectColor = SAMPLE_TEXTURE2D( _EnvMapSampler, sampler_EnvMapSampler, sphereMapCoords ).rgb;
    reflectColor = GetOverlayColor( reflectColor, combinedColor );

    combinedColor = lerp( combinedColor, reflectColor, reflectionMaskColor.a );
    combinedColor *= _Color.rgb * _MainLightColor.rgb;
    float opacity = diffSamplerColor.a * _Color.a * _MainLightColor.a;

    // 接收主光阴影（等价原 LIGHT_ATTENUATION）
    Light mainLight = GetMainLight( i.shadowCoord );
    float_t attenuation = saturate( 2.0 * mainLight.shadowAttenuation - 1.0 );
    shadowColor = _ShadowColor.rgb * combinedColor;
    combinedColor = lerp( shadowColor, combinedColor, attenuation );

    // Rimlight
    float_t rimlightDot = saturate( 0.5 * ( dot( normalVec, i.lightDir ) + 1.0 ) );
    falloffU = saturate( rimlightDot * falloffU );
    falloffU = SAMPLE_TEXTURE2D( _RimLightSampler, sampler_RimLightSampler, float2( falloffU, 0.25f ) ).r;
    float3_t lightColor = diffSamplerColor.rgb;
    combinedColor += falloffU * lightColor;

    return float4( combinedColor, opacity );
}

#endif // UNITYCHAN_CHARA_MAIN_URP_INCLUDED
