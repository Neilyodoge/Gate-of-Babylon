#ifndef WATERPASS
#define WATERPASS

#include "WaterInput.hlsl"

struct Attributes
{
    float4 PositionOS: POSITION;
    float2 uv: TEXCOORD0;
    float3 normalOS: NORMAL;
    float4 tangentOS: TANGENT;
    float4 Color : COLOR;
};
struct Varyings
{
    float4 PositionHCS: SV_POSITION;
    float4 Color : COLOR;
    float2 uv: TEXCOORD0;
    float fogCoord: TEXCOORD1;
    float3 PositionVS: TEXCOORD2;  // view Space
    float3 PositionWS: TEXCOORD3;
    float3 tangentWS: TEXCOORD4;
    float3 bitangentWS: TEXCOORD5;
    float3 normalWS: TEXCOORD6;
    float vecterAnim : TEXCOORD7;
    
};

Varyings vert(Attributes v)
{
    Varyings o = (Varyings)0;
    o.Color = v.Color;
    VertexNormalInputs normal = GetVertexNormalInputs(v.normalOS, v.tangentOS);
    o.tangentWS = normal.tangentWS;
    o.bitangentWS = normal.bitangentWS;
    o.normalWS = normal.normalWS;
    float2 ScreenUV = TransformObjectToHClip(v.PositionOS.xyz).xy / _ScreenParams.xy;
    half depthTex = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, ScreenUV,0).r;
    half depthScene = LinearEyeDepth(depthTex, _ZBufferParams);
    half depthWater = depthScene + TransformWorldToView(v.PositionOS.xyz).z;

    o.PositionWS = TransformObjectToWorld(v.PositionOS.xyz);
    float2 animUV = float2(frac(_VertexAnimSpeed.x * _Time.y), frac(_VertexAnimSpeed.y * _Time.y))        
                    + (o.PositionWS.xz * _VertexAnim_ST.xy + _VertexAnim_ST.zw);
    half animTex = SAMPLE_TEXTURE2D_LOD(_VertexAnim, sampler_VertexAnim,animUV,0).r;
    float3 VertexAnim = GerstnerWave(_WaveA, o.PositionWS, o.tangentWS, o.bitangentWS);
    VertexAnim += GerstnerWave(_WaveB, o.PositionWS, o.tangentWS, o.bitangentWS);
    VertexAnim = (VertexAnim + pow(animTex,6) * _VertexIntensity)/2;
    o.PositionWS += VertexAnim * v.Color.r;
    o.vecterAnim = VertexAnim;

    o.PositionVS = TransformWorldToView(o.PositionWS);
    o.PositionHCS = TransformWorldToHClip(o.PositionWS);

    o.uv = o.PositionWS.xz;
    o.fogCoord = ComputeFogFactor(o.PositionHCS.z);
    return o;
}

half4 frag(Varyings i): SV_Target
{
    // Normal
    float3x3 T2W = float3x3(i.tangentWS.xyz, i.bitangentWS.xyz, i.normalWS.xyz);
    float2 normalUV = float2(frac(_NormalSpeed.x * _Time.y), frac(_NormalSpeed.y * _Time.y))
                    + (i.uv * _BumpTex_ST.xy + _BumpTex_ST.zw);
    float4 normalTex = SAMPLE_TEXTURE2D(_BumpTex, sampler_BumpTex, normalUV);
    float2 normalUV2 = float2(frac(_NormalSpeed.z * _Time.y), frac(_NormalSpeed.w * _Time.y))
                    + (float2(i.uv.x,-i.uv.y) * _DetailBumpTex_ST.xy + _DetailBumpTex_ST.zw);
    float4 detailnormalTex = SAMPLE_TEXTURE2D(_DetailBumpTex, sampler_DetailBumpTex, normalUV2);
    float3 bumpWS = TransformTangentToWorldNormal(T2W, normalTex,_WaterBumpScale);
    float3 detailbumpWS = TransformTangentToWorldNormal(T2W, detailnormalTex,_DetailBumpScale);
    bumpWS = UNDNormal(bumpWS,detailbumpWS);
    float flatNormalDistance = 1-i.PositionHCS.w * _flatNormal;
    bumpWS = lerp(float3(0,1,0),bumpWS,flatNormalDistance);
    
    // water depth
    float2 ScreenUV = i.PositionHCS.xy / _ScreenParams.xy ;
    half depthTex = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, ScreenUV).r;
    half depthScene = LinearEyeDepth(depthTex, _ZBufferParams);
    half depthWater = depthScene + i.PositionVS.z;
    
    // Distortion
    float2 distortionUV = float2(frac(_DistortionSpeed.x * _Time.y), frac(_DistortionSpeed.y * _Time.y))
    + (i.uv * _DistortionTex_ST.xy + _DistortionTex_ST.zw);
    half2 distortionTex = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV).xy * i.Color.r;
    float2 opaqueUV = ScreenUV + _DistortionIntensity * distortionTex;
    half depthDistortionTex = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, opaqueUV).r;
    half depthDistortionScene = LinearEyeDepth(depthDistortionTex, _ZBufferParams);
    half depthDistortionWater = depthDistortionScene + i.PositionVS.z;
    if (depthDistortionWater < 0)
    {
        opaqueUV = ScreenUV;
        depthDistortionWater = depthWater;
    }
    half4 camColorTex = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, opaqueUV);
    
    // Caustic
    half4 depthVS = 1; 
    depthVS.xy = i.PositionVS.xy * depthDistortionScene / - i.PositionVS.z;
    depthVS.z = depthDistortionScene;
    half4 depthWS = mul(unity_CameraToWorld, depthVS);
    float2 causticUV = float2(frac(_FoamSpeed.z * _Time.y), frac(_FoamSpeed.w * _Time.y))
                    + (depthWS.xz * _CausticTex_ST.xy + _CausticTex_ST.zw) + (depthWS.y * _CausticFacade);  
    half4 causticTex = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, causticUV);
    float2 causticUV2 = float2(frac(_FoamSpeed.z * _Time.y * 0.5), frac(-_FoamSpeed.w * _Time.y * 0.5))
                    + (depthWS.xz * _CausticTex_ST.xy + _CausticTex_ST.zw) + (depthWS.y * _CausticFacade);
    half4 causticTex2 = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, causticUV2.yx);
    half4 finalCaustic = min(causticTex, causticTex2);
    finalCaustic.rgb = finalCaustic.rgb * saturate(1 - pow(depthDistortionWater, _CausticScale)) * _CausticIntensity;

    // Specular & Toon Specular
    float2 distortionUV2 = float2(_DistortionSpeed.z * _Time.y, _DistortionSpeed.w * _Time.y)
                            + (i.uv * _DistortionTex_ST.xy + _DistortionTex_ST.zw);
    half distortionTex2 = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex,distortionUV2);

    // Blinn-Phong
    float3 shadowDistortion = i.PositionWS;
    shadowDistortion.xz = shadowDistortion.xz + _DistortionIntensity * distortionTex;
    float4 shadowCoord = TransformWorldToShadowCoord(shadowDistortion);
    Light light = GetMainLight(shadowCoord);
    float3 N = bumpWS;
    float3 vertexN = normalize(i.normalWS);
    #if defined(_CUSTOM_LIGHT_DIR)
        float shadowPart = 1;
        float3 L = normalize((float3)_TLEnvLightDir.xyz);
    #else
        float shadowPart = saturate(light.shadowAttenuation);
        float3 L = light.direction;
    #endif
    float3 V = normalize(_WorldSpaceCameraPos.xyz - i.PositionWS);
    float3 H = normalize(V + L);
    float NoH = saturate(dot(N, H));
    float LoH = saturate(dot(L, H));
    float NoL = saturate(dot(N, L));
    float3 SH = SampleSH(N);
    // fresnel
    float NoV = saturate(dot(N, V)) * _fresnelScale;
    half fresnelPart = 1 - NoV * NoV * NoV;
    fresnelPart = saturate(fresnelPart-(1-shadowPart) * (1-_ShadowColor.a));
    half4 specular = _SpecularColor * pow(max(0,NoH), _HeightScale);
    // Toon Specular: smoothstep on distortion texture pattern
    half waterNormal = min(distortionTex.x, distortionTex2);
    half4 ToonSpecular = smoothstep(0.55, 0.65, waterNormal) * _CartoonSpecular;
    ToonSpecular = ToonSpecular * max(i.vecterAnim, 0.1);
    specular = max(ToonSpecular, specular);

    // Sparkle
    float2 sparkleBaseUV = i.uv * _SparkleTex_ST.xy + _SparkleTex_ST.zw;
    float SparkleTex1 = SAMPLE_TEXTURE2D(_SparkleTex,sampler_SparkleTex, sparkleBaseUV + frac(_Time.y * _SparkleSpeed.r)).r;
    float SparkleTex2 = SAMPLE_TEXTURE2D(_SparkleTex,sampler_SparkleTex, sparkleBaseUV + frac(_Time.y * _SparkleSpeed.g)).r;
    float SparklePart = saturate(pow(SparkleTex1 * SparkleTex2,_SparkleIntensity));

    // foam
    float2 foamUV = float2(_FoamSpeed.x * _Time.y, _FoamSpeed.x * _Time.y) + (i.PositionWS.xz * _FoamTex_ST.xy + _FoamTex_ST.zw);
    half foamTex = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex,foamUV);
    half foam = smoothstep(0, foamTex.r * _FoamRange, depthWater);
    // waterSide
    float waterSide = depthWater * ((i.PositionWS.y - _FoamHeight) * sin((_Time.y * _FoamSpeed.y) * 10)/10+1);
    waterSide = smoothstep(0,_FoamSide,waterSide) * max((1-i.vecterAnim),depthDistortionWater);

    // color blend
    half4 Tint = half4(0,0,0,1);
    Tint.rgb = lerp(_WaterSideColor.rgb, _WaterColor.rgb, saturate(depthDistortionWater));
    Tint.rgb = lerp(Tint.rgb,_WaterDepthWSColor, saturate(depthDistortionWater + _DepthForCol) * _WaterDepthWSColor.a);
    half3 NoLColorBlend = lerp(_WaterDepthWSColor.rgb * _WaterDepthWSColor.a,_WaterSideColor, NoL);
    #if defined(_CUSTOM_LIGHT_DIR)
        half3 _customLightMul = _CustomLightColor.rgb * _CustomLightIntensity;
        NoLColorBlend *= _customLightMul;
        specular.rgb *= _customLightMul;
    #endif
    float3 outputBlend = (Tint.rgb+NoLColorBlend)/2;
    Tint.rgb = lerp(Tint.rgb,outputBlend,_UseBlend);
    Tint.rgb = lerp(camColorTex, Tint, _WaterAlpha);
    Tint.rgb = lerp(Tint.rgb, _FoamTint.rgb, (1 - foam) * _FoamTint.a);
    Tint.rgb += SparklePart * _SparkleTint.rgb;                                                             // 先叠闪点
    Tint.rgb += lerp(specular * _ShadowColor.a, specular, shadowPart);                                      // 再叠高光
    Tint.rgb += lerp(finalCaustic.rgb * _ShadowColor.a,finalCaustic.rgb,shadowPart);
    Tint.rgb = lerp(_ShadowColor.rgb * Tint.rgb, Tint.rgb, shadowPart);
    Tint.rgb = lerp(Tint.rgb,max(Tint.rgb, _fresnelColor.rgb * SH * _fresnelColor.a), fresnelPart);
    Tint.rgb *= lerp(1,SH,_SHIntensity);
    Tint.rgb = MixFog(Tint.rgb, i.fogCoord);
    #if _WATERSIDE
        Tint.rgb = lerp(camColorTex * _WaterSideTint.rgb,Tint,saturate(smoothstep(0,2,waterSide)));
        float damp = smoothstep(0,2-1.9,waterSide) * (1-smoothstep(0,2,waterSide));
        Tint.rgb = lerp(Tint.rgb,Tint.rgb*_WaterSideTint,damp);
        Tint.a *= smoothstep(0,_DampSide,waterSide);
    #endif 

    #if _DEBUGMODE
        switch(_Debug)
        {
            case 0:
            Tint.rgb = damp;
            break;
            case 1:
            Tint.rgb = saturate(depthDistortionWater + _DepthForCol);
            break;
            case 2:
            Tint.rgb = SH;
            break;
            case 3:
            Tint.rgb = fresnelPart;
            break;
            case 4:
            Tint.rgb = shadowPart;
            break;
            case 5:
            Tint.rgb = i.vecterAnim;
            break;
            case 6:
            Tint.rgb = specular;
            break;
            case 7:
            Tint.rgb = 1-i.PositionHCS.w * _flatNormal;
            break;
            case 8:
            Tint.rgb = i.Color.rgb;
            break;
            case 9:
            Tint.rgb = SparklePart * _SparkleTint.rgb;
            break;
        }
    #endif

    return saturate(Tint);
}

#endif
