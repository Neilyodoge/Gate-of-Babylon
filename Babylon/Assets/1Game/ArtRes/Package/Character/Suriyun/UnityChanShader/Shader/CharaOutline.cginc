// UnityChan Toon Shader —— URP 版本（反向外扩描边 Outline）
// 顶点沿裁剪空间法线外扩，Cull Front 渲染背面形成描边

#ifndef UNITYCHAN_CHARA_OUTLINE_URP_INCLUDED
#define UNITYCHAN_CHARA_OUTLINE_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define float_t  half
#define float2_t half2
#define float3_t half3
#define float4_t half4

// CBUFFER 与 CharaMain 保持一致布局（便于 SRP Batcher）
CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _ShadowColor;
    float  _SpecularPower;
    float  _EdgeThickness;
    float  _DepthBias;
    float4 _MainTex_ST;
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

struct appdata_uchan
{
    float4 vertex   : POSITION;
    float3 normal   : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 UV  : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// 描边参数
#define OUTLINE_DISTANCE_SCALE   (0.0016)
#define OUTLINE_NORMAL_SCALE_MIN (0.003)
#define OUTLINE_NORMAL_SCALE_MAX (0.030)

v2f vert( appdata_uchan v )
{
    v2f o = (v2f)0;
    UNITY_SETUP_INSTANCE_ID(v);

    float4 projPos    = TransformObjectToHClip( v.vertex.xyz );
    // 等价原 UnityObjectToClipPos(float4(normal,0))：法线经 M*VP 变换到裁剪空间
    float4 projNormal = normalize( mul( GetWorldToHClipMatrix(),
                                        mul( GetObjectToWorldMatrix(), float4( v.normal, 0.0 ) ) ) );

    float distanceToCamera = OUTLINE_DISTANCE_SCALE * projPos.z;
    float normalScale = _EdgeThickness *
        lerp( OUTLINE_NORMAL_SCALE_MIN, OUTLINE_NORMAL_SCALE_MAX, distanceToCamera );

    o.pos = projPos + normalScale * projNormal;
    #ifdef UNITY_REVERSED_Z
        o.pos.z -= _DepthBias;
    #else
        o.pos.z += _DepthBias;
    #endif
    o.UV = v.texcoord.xy;
    return o;
}

inline float_t GetMaxComponent( float3_t inColor )
{
    return max( max( inColor.r, inColor.g ), inColor.b );
}

// 伪饱和度调整（非真实 HSL）
inline float3_t SetSaturation( float3_t inColor, float_t inSaturation )
{
    float_t maxComponent = GetMaxComponent( inColor ) - 0.0001;
    float3_t saturatedColor = step( maxComponent.rrr, inColor ) * inColor;
    return lerp( inColor, saturatedColor, inSaturation );
}

#define SATURATION_FACTOR 0.6
#define BRIGHTNESS_FACTOR 0.8

half4 frag( v2f i ) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);

    float4_t mainMapColor = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, i.UV );

    float3_t outlineColor = BRIGHTNESS_FACTOR
        * SetSaturation( mainMapColor.rgb, SATURATION_FACTOR )
        * mainMapColor.rgb;

    return float4_t( outlineColor, mainMapColor.a ) * _Color * _MainLightColor;
}

#endif // UNITYCHAN_CHARA_OUTLINE_URP_INCLUDED
