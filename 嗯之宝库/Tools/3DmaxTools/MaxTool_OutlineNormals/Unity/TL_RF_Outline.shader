// 反向挤出描边（Render Feature 专用）
// 由 RenderObjects 特性作为 override 材质，按 Layer 统一绘制（形式参照 TL_RF_PlannerShadow）。
// Cull Front + 第三套 UV.xy 切线空间八面体编码法线，兼容第二套 UV.xyz 旧 SmoothN。
// 在视空间沿平滑法线外扩；基准宽度 × near→far 距离自适应倍率 × 顶点色 .a。
// 优先读取第三套 UV（TEXCOORD2）；无新编码时读取第二套旧 SmoothN，无烘焙法线时回退原始法线。
Shader "TL/RenderFeature/Outline"
{
    Properties
    {
        [Header(Outline)]
        _OutlineColor ("描边颜色", Color)       = (0.337255, 0.113725, 0.027451, 1)
        _OutlineWidth ("描边宽度", Range(0, 0.1)) = 0.015

        [Header(Distance Adaptive)]
        // 距离自适应：基准宽度乘以 near→far 之间的倍率（与 TLRoleBase 一致）
        [ToggleUI] _OutlineDistAdaptive ("距离自适应", Float)      = 1
        _OutlineMinDist   ("最近距离",  Float)        = 1
        _OutlineMaxDist   ("最远距离",  Float)        = 10
        _OutlineNearScale ("近处倍率",  Range(0.1, 3)) = 0.5
        _OutlineFarScale  ("远处倍率",  Range(0.1, 5)) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "Outline"

            // 反向挤出：剔除正面、只留背面外壳
            Cull   Front
            ZWrite On
            ZTest  LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #pragma prefer_hlslcc gles

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineDistAdaptive;
                float  _OutlineMinDist;
                float  _OutlineMaxDist;
                float  _OutlineNearScale;
                float  _OutlineFarScale;
            CBUFFER_END

            // 新版 Max：第三套 UV.xy 二维编码，统一使用 U+2 标记切线空间。
            // 旧模型：第二套 UV.xyz 切线空间平滑法线（SmoothNormalBaker 写入）。
            // 无有效烘焙数据时回退到原始法线。
            struct Attributes
            {
                float4 positionOS     : POSITION;
                half3  normalOS       : NORMAL;
                float4 tangentOS      : TANGENT;     // 原切线，随蒙皮/合批变换
                float3 smoothNormalTS : TEXCOORD1;   // 第二套 UV 中的旧 SmoothN
                float2 outlineUV3     : TEXCOORD2;   // 固定第三套 UV（Max Channel 3）
                half4  color          : COLOR;       // .a 逐顶点调制描边宽度
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            bool IsMaxOutlineUV(float2 uv)
            {
                return uv.x >= 1.9999 && uv.x <= 3.0001 && uv.y >= -0.0001 && uv.y <= 1.0001;
            }

            float3 DecodeMaxUV(float2 encoded)
            {
                float2 f = (encoded - float2(2.0, 0.0)) * 2.0 - 1.0;
                float3 n = float3(f, 1.0 - abs(f.x) - abs(f.y));
                float fold = saturate(-n.z);
                n.xy += float2(n.x >= 0.0 ? -fold : fold, n.y >= 0.0 ? -fold : fold);
                return normalize(n);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // ── 解码用于外扩的对象空间法线 ──
                // 第三套 UV 解码为切线空间方向，再使用动画/合批后的 TBN 还原。
                // 无新编码时，按旧逻辑从第二套 UV.xyz 还原 SmoothN。
                // 无平滑法线时回退原始法线（可能在硬边裂开，但仍描边）。
                // 注：最终会对 normalVS.xy 归一化，故这里 nOS 只需方向正确、无需再 normalize
                float3 nOS = normalize(input.normalOS);
                float3 tangent = input.tangentOS.xyz - nOS * dot(nOS, input.tangentOS.xyz);
                if (IsMaxOutlineUV(input.outlineUV3))
                {
                    // 有新标记的模型不读取第二套光照 UV；切线无效时保持原始法线。
                    if (dot(tangent, tangent) > 1e-8 && abs(input.tangentOS.w) > 0.5)
                    {
                        float3 nTS = DecodeMaxUV(input.outlineUV3);
                        float3 T = normalize(tangent);
                        float3 B = cross(nOS, T) * input.tangentOS.w;
                        nOS = normalize(nTS.x * T + nTS.y * B + nTS.z * nOS);
                    }
                }
                else if (dot(input.smoothNormalTS, input.smoothNormalTS) > 1e-3 &&
                         dot(tangent, tangent) > 1e-8 && abs(input.tangentOS.w) > 0.5)
                {
                    // 沿用旧版非零检测；第二套须存 SmoothN，普通 UV 无法仅靠此判断可靠区分。
                    float3 T = normalize(input.tangentOS.xyz);
                    float3 B = cross(nOS, T) * input.tangentOS.w;
                    float3 snTS = input.smoothNormalTS;
                    nOS = snTS.x * T + snTS.y * B + snTS.z * nOS;
                }

                // 一次 MV 变换同时得到视空间位置与法线
                float3 posVS    = mul(UNITY_MATRIX_MV, float4(input.positionOS.xyz, 1.0)).xyz;
                float3 normalVS = mul((float3x3)UNITY_MATRIX_MV, nOS);

                // 基准宽度 × 距离自适应倍率 × 顶点色 .a（与 TLRoleBase 一致）
                float width = _OutlineWidth;
                float dist  = -posVS.z;
                float t     = saturate((dist - _OutlineMinDist) / max(_OutlineMaxDist - _OutlineMinDist, 1e-3));
                float scale = lerp(_OutlineNearScale, _OutlineFarScale, t);
                width *= lerp(1.0, scale, _OutlineDistAdaptive);
                width *= input.color.a;

                posVS.xy += normalize(normalVS.xy) * width;
                output.positionCS = mul(UNITY_MATRIX_P, float4(posVS, 1.0));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
