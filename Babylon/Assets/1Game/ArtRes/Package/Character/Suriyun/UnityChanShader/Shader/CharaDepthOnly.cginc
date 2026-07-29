// UnityChan Toon Shader —— URP DepthOnly Pass（深度预处理 / 深度图）

#ifndef UNITYCHAN_CHARA_DEPTHONLY_URP_INCLUDED
#define UNITYCHAN_CHARA_DEPTHONLY_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct appdata_uchan
{
    float4 positionOS : POSITION;
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
    o.positionCS = TransformObjectToHClip( input.positionOS.xyz );
    return o;
}

half4 frag( v2f input ) : SV_Target
{
    return 0;
}

#endif // UNITYCHAN_CHARA_DEPTHONLY_URP_INCLUDED
