// TextureDebug.shader
// 通用贴图调试 Shader，以 Unlit 形式单独查看各通道数据
// 支持：贴图各通道、顶点色 RGBA、法线贴图、模型原始法线、UV3 平滑法线、UV 坐标

Shader "Hidden/Tools/TextureDebug"
{
    Properties
    {
        // ====== Debug 模式选择 ======
        [Header(Debug Mode)]
        [Enum(Tex_RGB,0, Tex_R,1, Tex_G,2, Tex_B,3, Tex_A,4, VertexColor_RGB,5, VertexColor_R,6, VertexColor_G,7, VertexColor_B,8, VertexColor_A,9, NormalMap,10, MeshNormal,11, SmoothNormal_UV3,12, UV0,13, UV1,14)]
        _DebugMode ("Debug Mode", Float) = 0

        // ====== 纹理 ======
        [Header(Texture)]
        _Tex ("Texture", 2D) = "white" {}

        // ====== 法线贴图设置 ======
        [Header(Normal Map)]
        _NormalScale ("Normal Scale", Range(0, 2)) = 1

        // ====== 其他设置 ======
        [Header(Other Settings)]
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull Mode", Float) = 2
        [Toggle]_GammaCorrect ("Gamma 矫正 (线性数据可视化)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "TextureDebug"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DebugVert
            #pragma fragment DebugFrag

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ====== CBUFFER ======
            CBUFFER_START(UnityPerMaterial)
            float4 _Tex_ST;
            float  _NormalScale;
            float  _DebugMode;
            float  _GammaCorrect;
            CBUFFER_END

            // ====== 纹理声明 ======
            TEXTURE2D(_Tex);    SAMPLER(sampler_Tex);

            // ====== 结构体 ======
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float4 color        : COLOR;
                float2 uv0          : TEXCOORD0;
                float2 uv1          : TEXCOORD1;
                float2 uv3          : TEXCOORD3; // 平滑法线存储在 UV3
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float4 color        : TEXCOORD0;
                float4 uv           : TEXCOORD1; // xy:uv0  zw:uv1
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 biTangentWS  : TEXCOORD4;
                float3 smoothNormalOS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ====== Vertex Shader ======
            Varyings DebugVert(Attributes v)
            {
                Varyings o;
                ZERO_INITIALIZE(Varyings, o);

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv.xy = TRANSFORM_TEX(v.uv0, _Tex);
                o.uv.zw = v.uv1.xy;

                // 模型原始法线（世界空间）
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = TransformObjectToWorldDir(v.tangentOS.xyz);
                o.biTangentWS = cross(o.normalWS, o.tangentWS) * v.tangentOS.w * GetOddNegativeScale();

                // 从 UV3.xy 还原平滑法线（对象空间）
                float2 snXY = v.uv3.xy;
                float snLenSq = dot(snXY, snXY);
                if (snLenSq > 0.001)
                {
                    float z = sqrt(max(0, 1.0 - saturate(snLenSq)));
                    o.smoothNormalOS = normalize(float3(snXY, z));
                }
                else
                {
                    o.smoothNormalOS = v.normalOS;
                }

                return o;
            }

            // ====== Fragment Shader ======
            float4 DebugFrag(Varyings i) : SV_Target0
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 UV = i.uv.xy;
                float3 result = float3(1, 0, 1); // 洋红色作为未匹配的默认色

                int mode = (int)_DebugMode;

                // --- Texture 通道 ---
                if (mode <= 4)
                {
                    float4 tex = SAMPLE_TEXTURE2D(_Tex, sampler_Tex, UV);
                    if (mode == 0)       result = tex.rgb;
                    else if (mode == 1)  result = tex.rrr;
                    else if (mode == 2)  result = tex.ggg;
                    else if (mode == 3)  result = tex.bbb;
                    else if (mode == 4)  result = tex.aaa;
                }
                // --- VertexColor ---
                else if (mode <= 9)
                {
                    if (mode == 5)       result = i.color.rgb;
                    else if (mode == 6)  result = i.color.rrr;
                    else if (mode == 7)  result = i.color.ggg;
                    else if (mode == 8)  result = i.color.bbb;
                    else if (mode == 9)  result = i.color.aaa;
                }
                // --- NormalMap（贴图作为法线贴图，切线空间→世界空间可视化） ---
                else if (mode == 10)
                {
                    float4 normalSample = SAMPLE_TEXTURE2D(_Tex, sampler_Tex, UV);
                    float3 bumpTS = UnpackNormalScale(normalSample, _NormalScale);
                    float3x3 TBN = float3x3(i.tangentWS, i.biTangentWS, i.normalWS);
                    float3 bumpWS = normalize(mul(bumpTS, TBN));
                    result = bumpWS * 0.5 + 0.5;
                }
                // --- MeshNormal（模型原始法线） ---
                else if (mode == 11)
                {
                    float3 normalWS = normalize(i.normalWS);
                    result = normalWS * 0.5 + 0.5;
                }
                // --- SmoothNormal_UV3（从 UV3 还原的平滑法线） ---
                else if (mode == 12)
                {
                    float3 smoothNormalWS = TransformObjectToWorldNormal(i.smoothNormalOS);
                    smoothNormalWS = normalize(smoothNormalWS);
                    result = smoothNormalWS * 0.5 + 0.5;
                }
                // --- UV0 可视化 ---
                else if (mode == 13)
                {
                    result = float3(frac(i.uv.xy), 0);
                }
                // --- UV1 可视化 ---
                else if (mode == 14)
                {
                    result = float3(frac(i.uv.zw), 0);
                }

                // Gamma 矫正
                if (_GammaCorrect > 0.5)
                {
                    result = pow(max(result, 0), 1.0 / 2.2);
                }

                return float4(result, 1);
            }
            ENDHLSL
        }

        // ====== DepthOnly Pass ======
        // URP 的深度预渲染需要此 Pass，否则 Forward Pass 的 ZTest Equal 会导致物体不可见
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            float4 DepthOnlyFrag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ====== DepthNormals Pass ======
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 DepthNormalsFrag(Varyings i) : SV_Target
            {
                float3 normalWS = normalize(i.normalWS);
                return float4(normalWS, 0);
            }
            ENDHLSL
        }
    }

    CustomEditor "TextureDebugShaderGUI"
}
