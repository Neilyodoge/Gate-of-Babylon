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
    // -------- Scene Depth & Color --------
    float2 screenUV   = i.positionHCS.xy / _ScreenParams.xy;
    half   depthRaw   = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
    half   depthScene = LinearEyeDepth(depthRaw, _ZBufferParams);
    half   depthWater = depthScene + i.positionVS.z;
    half   depthNorm  = saturate(abs(depthWater) * _DepthIntensity);
    half4  camColor   = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV);

    // -------- Water Color (LUT) --------
    half4 waterColor = SAMPLE_TEXTURE2D(_WaterDepthLUT, sampler_WaterDepthLUT, float2(depthNorm, 0.5));

    // -------- Caustic --------
    half4 depthVS = 1;
    depthVS.xy = i.positionVS.xy * depthScene / -i.positionVS.z;
    depthVS.z  = depthScene;
    half4 depthWS = mul(unity_CameraToWorld, depthVS);

    float2 causticBaseUV = depthWS.xz * _CausticTex_ST.xy + _CausticTex_ST.zw
                         + depthWS.y * _CausticFacade;
    float2 causticUV1 = frac(_CausticSpeed.xy * _Time.y) + causticBaseUV;
    float2 causticUV2 = frac(_CausticSpeed.xy * float2(1, -1) * _Time.y * 0.5) + causticBaseUV;
    half4  causticA   = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, causticUV1);
    half4  causticB   = SAMPLE_TEXTURE2D(_CausticTex, sampler_CausticTex, causticUV2.yx);
    half3  caustic    = min(causticA, causticB).rgb
                      * saturate(1 - pow(depthNorm, _CausticScale))
                      * _CausticIntensity;

    // -------- Toon Specular (noise * noise pattern) --------
    half3 N = normalize(i.normalWS);
    half3 V = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);

    float2 toonBaseUV = i.positionWS.xz * _ToonNoiseTex_ST.xy + _ToonNoiseTex_ST.zw;
    half   toon1 = SAMPLE_TEXTURE2D(_ToonNoiseTex, sampler_ToonNoiseTex,
                                    toonBaseUV       + _Time.y * _ToonNoiseSpeed.xy).x;
    half   toon2 = SAMPLE_TEXTURE2D(_ToonNoiseTex, sampler_ToonNoiseTex,
                                    toonBaseUV.yx * 1.3 + _Time.y * _ToonNoiseSpeed.zw).x;
    half   toonPattern = toon1 * toon2;
    half   toonMask    = smoothstep(_ToonSpecMin, _ToonSpecMax, toonPattern);
    half3  specular    = toonMask * _CartoonSpecular.rgb;

    // -------- Fresnel --------
    half  NoV     = saturate(dot(N, V));
    half  fresnel = pow(1 - NoV, _fresnelScale);
    half3 SH      = SampleSH(N);

    // -------- Foam (SDF) --------
    float2 sdfUV  = (i.positionWS.xz - _SDFBoundsMin.xy) / _SDFBoundsSize.xy;
    half   dRaw   = SAMPLE_TEXTURE2D(_FoamSDF, sampler_FoamSDF, sdfUV).r * _SDFBoundsSize.z;

    float2 foamNoiseUV = i.positionWS.xz * _FoamNoiseTex_ST.xy + _FoamNoiseTex_ST.zw
                       + _Time.y * _FoamNoiseSpeed.xy;
    half   foamNoise   = SAMPLE_TEXTURE2D(_FoamNoiseTex, sampler_FoamNoiseTex, foamNoiseUV).r;
    half   d           = dRaw + (foamNoise * 2 - 1) * _FoamNoiseAmp;

    #ifdef _DEBUG_SDF
        return half4(saturate(d / _SDFBoundsSize.z).rrr, 1);
    #endif

    half dNorm     = saturate(d / _FoamScope);
    half inScope   = step(d, _FoamScope);
    half edge      = step(d, _FoamEdgeWidth);
    half breakMask = step(pow(dNorm, _FoamFadePower), foamNoise);
    half stripe    = step(0.6, sin(d * _FoamInterval - _Time.y * _FoamAnimSpeed))
                   * inScope * breakMask;
    half foam      = saturate(edge + stripe + toonMask * dNorm);

    // -------- Compose --------
    half3 col = lerp(camColor.rgb, waterColor.rgb, _WaterAlpha);
    col = lerp(col, _FoamTint.rgb, foam * _FoamTint.a);
    col *= 1 + specular;
    col += caustic;
    col += fresnel * _fresnelColor.rgb * _fresnelColor.a * SH;
    col  = MixFog(col, i.fogCoord);

    return half4(saturate(col), waterColor.a);
}

#endif
