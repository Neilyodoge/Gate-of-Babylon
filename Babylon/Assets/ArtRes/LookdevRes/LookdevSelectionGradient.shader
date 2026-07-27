// Lookdev 选择渐变（URP 14 Unlit）
// 纵向：dark = saturate(worldPos.y * YDarkScale)，Y 越高越黑
// 径向：距世界原点距离渐隐
Shader "Lookdev/SelectionGradient"
{
    Properties
    {
        [Header(Vertical Y Gradient)]
        _YDarkScale       ("Y Dark Scale (worldY * 系数)", Float) = 0.01
        _VerticalGradient ("Vertical Power", Range(0.01, 8))      = 1
        _FarValue         ("Far Value",      Range(0, 1))         = 1

        [Header(Radial Mask)]
        _MaskRadius       ("Mask Radius",    Float)              = 100
        _MaskSoftness     ("Mask Softness",  Range(0.01, 8))     = 1

        [Header(Selection)]
        [HDR] _SelectionColor ("Selection Color", Color) = (1, 0.5, 0, 0)

        [Header(Depth)]
        _PixelDepthOffset ("Pixel Depth Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"            = "Opaque"
            "RenderPipeline"        = "UniversalPipeline"
            "Queue"                 = "Geometry"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _YDarkScale;
                half  _VerticalGradient;
                half  _FarValue;
                half  _MaskRadius;
                half  _MaskSoftness;
                half4 _SelectionColor;
                half  _PixelDepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            half PositiveClampedPow(half baseVal, half expVal)
            {
                return pow(saturate(baseVal), max(expVal, 0.001h));
            }

            half3 EvaluateLookdev(float3 positionWS)
            {
                half3 bright = half3(_FarValue, _FarValue, _FarValue);
                bright = lerp(bright, _SelectionColor.rgb, _SelectionColor.a);

                half dist = length(positionWS);
                half distMask = PositiveClampedPow(dist / max(_MaskRadius, 1e-4h), _MaskSoftness);
                bright *= (1.0h - distMask);

                // worldY * 系数，越大越黑；系数越小需要越高才黑
                half yDark = PositiveClampedPow(saturate(positionWS.y * _YDarkScale), _VerticalGradient);
                return lerp(bright, half3(0, 0, 0), yDark);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = posInputs.positionWS;
                output.positionCS = posInputs.positionCS;

                if (_PixelDepthOffset != 0.0h)
                {
                    half3 viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                    float3 offsetCS = TransformWorldToHClip(posInputs.positionWS + viewDirWS * _PixelDepthOffset);
                    output.positionCS.z = offsetCS.z;
                }

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color = EvaluateLookdev(input.positionWS);

                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    half shadowAtten = MainLightRealtimeShadow(shadowCoord);
                    color *= shadowAtten;
                #endif

                return half4(max(color, 0.001), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
