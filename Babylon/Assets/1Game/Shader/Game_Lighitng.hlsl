#ifndef XIANTU_GAME_LIGHTING_INCLUDED
#define XIANTU_GAME_LIGHTING_INCLUDED

// UE-style default-lit BRDF:
// Disney diffuse + GGX NDF + Smith joint visibility + Schlick Fresnel.

#define GAME_PI 3.14159265359h

struct GameBRDFData
{
    half3 diffuseColor;
    half3 specularColor;
    half perceptualRoughness;
    half roughness;
    half roughness2;
};

half GamePow5(half value)
{
    half value2 = value * value;
    return value2 * value2 * value;
}

GameBRDFData GameInitializeBRDF(half3 baseColor, half metallic, half smoothness)
{
    GameBRDFData data;

    metallic = saturate(metallic);
    smoothness = saturate(smoothness);

    data.diffuseColor = baseColor * (1.0h - metallic);
    data.specularColor = lerp(kDieletricSpec.rgb, baseColor, metallic);
    data.perceptualRoughness = 1.0h - smoothness;
    data.roughness = max(data.perceptualRoughness * data.perceptualRoughness, 0.002h);
    data.roughness2 = data.roughness * data.roughness;

    return data;
}

half GameD_GGX(half roughness2, half NoH)
{
    half d = (NoH * roughness2 - NoH) * NoH + 1.0h;
    return roughness2 / max(GAME_PI * d * d, 1e-5h);
}

half GameVis_SmithJointApprox(half roughness, half NoV, half NoL)
{
    half visV = NoL * (NoV * (1.0h - roughness) + roughness);
    half visL = NoV * (NoL * (1.0h - roughness) + roughness);
    return 0.5h / max(visV + visL, 1e-4h);
}

half3 GameF_Schlick(half3 specularColor, half VoH)
{
    half Fc = GamePow5(1.0h - VoH);
    return saturate(50.0h * specularColor.g) * Fc + (1.0h - Fc) * specularColor;
}

half GameDisneyDiffuse(half roughness, half NoV, half NoL, half VoH)
{
    half energyBias = lerp(0.0h, 0.5h, roughness);
    half energyFactor = lerp(1.0h, 1.0h / 1.51h, roughness);
    half fd90 = energyBias + 2.0h * VoH * VoH * roughness;
    half lightScatter = 1.0h + (fd90 - 1.0h) * GamePow5(1.0h - NoL);
    half viewScatter = 1.0h + (fd90 - 1.0h) * GamePow5(1.0h - NoV);
    return lightScatter * viewScatter * energyFactor;
}

half3 GameEvaluateDirectBRDF(
    GameBRDFData data,
    half3 normalWS,
    half3 viewDirectionWS,
    half3 lightDirectionWS)
{
    half3 halfDirection = SafeNormalize(lightDirectionWS + viewDirectionWS);

    half NoV = saturate(abs(dot(normalWS, viewDirectionWS)) + 1e-5h);
    half NoL = saturate(dot(normalWS, lightDirectionWS));
    half NoH = saturate(dot(normalWS, halfDirection));
    half VoH = saturate(dot(viewDirectionWS, halfDirection));

    half diffuseTerm = GameDisneyDiffuse(
        data.perceptualRoughness,
        NoV,
        NoL,
        VoH);

    half D = GameD_GGX(data.roughness2, NoH);
    half Vis = GameVis_SmithJointApprox(data.roughness, NoV, NoL);
    half3 F = GameF_Schlick(data.specularColor, VoH);

    // UE's analytical BRDF includes 1/PI here. URP's real-time light units
    // already follow Unity's PI-compensated convention, so applying it again
    // makes this material roughly 68% darker than URP Lit under the same light.
    half3 diffuse = data.diffuseColor * diffuseTerm;
    half3 specular = D * Vis * F;
    return (diffuse + specular) * NoL;
}

// Epic's split-sum environment BRDF approximation.
half3 GameEnvironmentBRDFApprox(
    half3 specularColor,
    half perceptualRoughness,
    half NoV)
{
    const half4 c0 = half4(-1.0h, -0.0275h, -0.572h, 0.022h);
    const half4 c1 = half4(1.0h, 0.0425h, 1.04h, -0.04h);

    half4 r = perceptualRoughness * c0 + c1;
    half a004 = min(r.x * r.x, exp2(-9.28h * NoV)) * r.x + r.y;
    half2 AB = half2(-1.04h, 1.04h) * a004 + r.zw;
    return specularColor * AB.x + AB.y;
}

half3 GameEvaluateIndirectBRDF(
    GameBRDFData data,
    half3 diffuseGI,
    half3 specularGI,
    half NoV)
{
    half3 diffuse = diffuseGI * data.diffuseColor;
    half3 specular = specularGI * GameEnvironmentBRDFApprox(
        data.specularColor,
        data.perceptualRoughness,
        NoV);
    return diffuse + specular;
}

#endif
