// UnityChan Toon Shader —— URP ShadowCaster Pass（投射阴影，含自阴影）
// 原 built-in 版靠 FallBack 提供投影 Pass，URP 需显式实现

#ifndef UNITYCHAN_CHARA_SHADOWCASTER_URP_INCLUDED
#define UNITYCHAN_CHARA_SHADOWCASTER_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct appdata_uchan
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

v2f vert( appdata_uchan input )
{
    v2f o = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(input);

    float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
    float3 normalWS   = TransformObjectToWorldNormal( input.normalOS );

    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
        float3 lightDirectionWS = normalize( _LightPosition - positionWS );
    #else
        float3 lightDirectionWS = _LightDirection;
    #endif

    float4 positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, normalWS, lightDirectionWS ) );
    #if UNITY_REVERSED_Z
        positionCS.z = min( positionCS.z, UNITY_NEAR_CLIP_VALUE );
    #else
        positionCS.z = max( positionCS.z, UNITY_NEAR_CLIP_VALUE );
    #endif

    o.positionCS = positionCS;
    return o;
}

half4 frag( v2f input ) : SV_Target
{
    return 0;
}

#endif // UNITYCHAN_CHARA_SHADOWCASTER_URP_INCLUDED
