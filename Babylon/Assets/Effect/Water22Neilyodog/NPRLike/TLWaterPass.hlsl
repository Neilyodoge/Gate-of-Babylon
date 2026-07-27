#ifndef TLWATERPASS
#define TLWATERPASS

#include "TLWaterInput.hlsl"

Varyings vert(Attributes v)
{
    Varyings o = (Varyings)0;
    o.positionWS  = TransformObjectToWorld(v.positionOS.xyz);
    o.positionVS  = TransformWorldToView(o.positionWS);
    o.positionHCS = TransformWorldToHClip(o.positionWS);
    o.normalWS    = TransformObjectToWorldNormal(v.normalOS);
    o.fogCoord    = ComputeFogFactor(o.positionHCS.z);
    return o;
}

half4 frag(Varyings i) : SV_Target
{
    float2 posXZ    = i.positionWS.xz;
    float2 screenUV = i.positionHCS.xy / _ScreenParams.xy;

    // -------- Distortion (offset screen UV by noise) --------
    float2 distortionUV = posXZ * _DistortionTex_ST.xy + _DistortionTex_ST.zw
                        + frac(_DistortionSpeed.xy * _Time.y);
    half2  distortion   = (SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV).xy * 2 - 1)
                        * _DistortionIntensity;
    float2 sceneUV      = screenUV + distortion;

    // -------- Scene Depth & Underwater Color --------
    half  depthScene = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, sceneUV).r, _ZBufferParams);
    half  depthWater = depthScene + i.positionVS.z;
    // 扭曲采样到水面之上的物体时回退到原 UV，避免穿模
    if (depthWater < 0)
    {
        sceneUV    = screenUV;
        depthScene = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, sceneUV).r, _ZBufferParams);
        depthWater = depthScene + i.positionVS.z;
    }
    half  depthNorm  = saturate(abs(depthWater) * _DepthIntensity);
    half3 camColor   = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, sceneUV).rgb;
    half4 waterColor = SAMPLE_TEXTURE2D(_WaterDepthLUT, sampler_WaterDepthLUT, float2(depthNorm, 0.5));

    // -------- Caustic (project scene point to world) --------
    float3 sceneVS    = float3(i.positionVS.xy * depthScene / -i.positionVS.z, depthScene);
    float3 sceneWS    = mul(unity_CameraToWorld, float4(sceneVS, 1)).xyz;
    float2 causticBase = sceneWS.xz * _CausticTex_ST.xy + _CausticTex_ST.zw + sceneWS.y * _CausticFacade;
    half4  causticA   = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, causticBase + frac(_CausticSpeed.xy * _Time.y));
    half4  causticB   = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, (causticBase + frac(_CausticSpeed.xy * float2(0.5, -0.5) * _Time.y)).yx);
    half3  caustic    = min(causticA, causticB).rgb * saturate(1 - pow(depthNorm, _CausticScale)) * _CausticIntensity;

    // -------- Toon Specular (two-layer noise multiply) --------
    float2 toonUV = posXZ * _ToonNoiseTex_ST.xy + _ToonNoiseTex_ST.zw;
    half   toon1  = SAMPLE_TEXTURE2D(_ToonNoiseTex, sampler_ToonNoiseTex, toonUV       + _Time.y * _ToonNoiseSpeed.xy).x;
    half   toon2  = SAMPLE_TEXTURE2D(_ToonNoiseTex, sampler_ToonNoiseTex, toonUV.yx * 1.3 + _Time.y * _ToonNoiseSpeed.zw).x;
    half   toonPattern = toon1 * toon2;
    half   toonMask    = smoothstep(_ToonSpecMin, _ToonSpecMax, toonPattern);

    // -------- Fresnel --------
    half3 N       = normalize(i.normalWS);
    half3 V       = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
    half  fresnel = pow(1 - saturate(dot(N, V)), _fresnelScale);
    half3 SH      = SampleSH(N);

    // -------- Foam (SDF distance + noise distortion) --------
    float2 sdfUV       = (posXZ - _SDFBoundsMin.xy) / _SDFBoundsSize.xy;
    float2 foamNoiseUV = posXZ * _FoamNoiseTex_ST.xy + _FoamNoiseTex_ST.zw + _Time.y * _FoamNoiseSpeed.xy;
    half   foamNoise   = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, foamNoiseUV).r;
    half   d           = SAMPLE_TEXTURE2D(_FoamSDF, sampler_FoamSDF, sdfUV).r * _SDFBoundsSize.z
                       + (foamNoise * 2 - 1) * _FoamNoiseAmp;

    #ifdef _DEBUG_SDF
        return half4(saturate(d / _SDFBoundsSize.z).rrr, 1);
    #endif

    half dNorm  = saturate(d / _FoamScope);
    half edge   = step(d, _FoamEdgeWidth);
    half stripe = step(0.6, sin(d * _FoamInterval - _Time.y * _FoamAnimSpeed))
                * step(pow(dNorm, _FoamFadePower), foamNoise)
                * step(d, _FoamScope);
    half foam   = saturate(edge + stripe + toonMask * dNorm);

    // -------- Compose --------
    half3 col = lerp(camColor, waterColor.rgb, _WaterAlpha);
    col = lerp(col, _FoamTint.rgb, foam * _FoamTint.a);
    col *= 1 + toonMask * _CartoonSpecular.rgb;
    col += caustic;
    col += fresnel * _fresnelColor.rgb * _fresnelColor.a * SH;
    col  = MixFog(col, i.fogCoord);

    return half4(saturate(col), waterColor.a);
}

#endif
