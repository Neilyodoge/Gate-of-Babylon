// 参考 QianMo X-PostProcessing-Library / GlitchImageBlockV3
//   https://github.com/QianMo/X-PostProcessing-Library
// 在原版基础上：
//   1. _BlockSize 升级为 vec2 → X/Y 独立控制，可拉伸
//   2. UI / VFX 共用一个 shader（UGUI 自动设的 UNITY_UI_CLIP_RECT 处理 RectMask2D）
//   3. URP 12 风格 HLSL（兼容 Built-in，因为 UGUI 默认走 Built-in 约定）
//   4. block 触发时整体水平撕裂 + RGB 色散 + halo ghost 多层叠加
//
// ─── 移动端（东南亚低端机）优化要点 ─────────────────────────────
// - 时间种子 floor(_Time.y * _GlitchSpeed) 整帧只算一次（per-frame 常量）
// - 双 per-pixel hash（block / dir）打包成一次 vec2 sin
// - 屏幕常量 hash 也打包成一次 vec2 sin（编译器通常会 hoist 到帧常量）
// - 强度/颜色用 half (fp16) 精度，UV 保 float (fp32) 防止位移抖动
// - 去掉 UNITY_UI_ALPHACLIP 变体（透明队列 + 渐变 alpha 用不上）
// - 去掉 _MODE_VFX/_MODE_UI 变体（UNITY_UI_CLIP_RECT 自身就只在 UI 上下文出现）
Shader "TH/VFX/GlitchImageBlock"
{
    Properties
    {
        // 不加 [PerRendererData]：UI 的 Image 会自动 override _MainTex 不依赖这个标签，
        // 而 VFX（SpriteRenderer / Quad）需要美术在 Inspector 里能看到并赋值
        _MainTex                        ("主贴图",                2D)               = "white" {}
        [HDR]_Color                    ("染色 (HDR)",            Color)            = (1,1,1,1)

        [Header(Glitch)]
        // 块密度 (X, Y) —— 越小块越大
        //   X 调小 → 块横向变宽（水平拉伸感）
        //   Y 调大 → 块纵向变密（更多横向条带）
        _BlockSize                      ("块密度 (X Y)",          Vector)           = (4, 12, 0, 0)
        _GlitchSpeed                    ("Glitch 速度",           Range(1, 30))    = 5      // 块种子刷新频率（每秒次数）
        _GlitchAmount                   ("Glitch 强度",           Range(0, 4))      = 2       // 整体倍率
        _DisplacePow                    ("Glitch 稀疏度",                Range(1, 16))     = 4       // pow(hash,N)，越大越稀疏，原版 QianMo=11
        _BlockShiftMax                  ("Glitch 位移距离",        Range(0, 0.5))    = 0.1     // block 触发时整体水平位移（撕裂感）

         [Header(RGBShift)]
        _RGBSplitMax                    ("RGB偏移距离",          Range(0, 0.5))    = 0.03    // 色散最大 UV 偏移（block 触发时的峰值）

        // G/B 偏移各自独立的染色：
        //   默认 (0,1,0,1) / (0,0,1,1) → 等效原版绿/蓝色散
        //   想要橙紫青粉等任意撞色组合，直接改这两个色块即可
        //   HDR 支持 >1 的强度（霓虹辉光），调暗到 0 等效"关闭该方向色散"
        [HDR]_GShiftTint               ("G偏移染色 (HDR)",       Color)            = (0, 1, 0, 1)
        [HDR]_BShiftTint               ("B偏移染色 (HDR)",       Color)            = (0, 0, 1, 1)

        // ── UI 系统自动设置（保持 UGUI 兼容）──
        [HideInInspector] _Stencil          ("Stencil ID",          Float) = 0
        [HideInInspector] _StencilOp        ("Stencil Op",          Float) = 0
        [HideInInspector] _StencilComp      ("Stencil Comp",        Float) = 8
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask",   Float) = 255
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask",  Float) = 255
        [HideInInspector] _ColorMask        ("Color Mask",          Float) = 15
        [HideInInspector] _ClipRect         ("Clip Rect",           Vector) = (-32767, -32767, 32767, 32767)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"    = "UniversalPipeline"
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // UI Mask 用，VFX 不影响
        Stencil
        {
            Ref      [_Stencil]
            Comp     [_StencilComp]
            Pass     [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Pass
        {
            Name "GlitchImageBlock"
            Tags { "LightMode" = "UniversalForward" }

            Cull   Off
            Lighting Off
            ZWrite Off
            ZTest  [unity_GUIZTestMode]
            // 标准 alpha blending：兼容 UI 不透明合成 + VFX 半透明特效
            Blend  SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // 移动端目标级别：3.5 = SM 4.0 等价，覆盖 GLES 3.0+ / Vulkan / Metal
            // 东南亚 90%+ 设备至少 GLES 3.0，可放心使用 half 精度和现代采样宏
            #pragma target 3.5

            // GLES 后端使用 HLSLcc → GLSL 转译（产物比默认更紧凑）
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x      // 旧 D3D11 feature level 9.x，已无视
            // #pragma multi_compile_instancing     // UI/VFX 用不上 GPU Instancing

            // UGUI RectMask2D 支持：UGUI 在父级是 RectMask2D 时运行时 EnableKeyword
            //   不加这个 pragma 的话 #if defined(UNITY_UI_CLIP_RECT) 会被永久预处理掉
            //   即使运行时 EnableKeyword 也没用，导致 ScrollRect / 列表里的 glitch 溢出
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ────────────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 worldPos   : TEXCOORD1;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float4 _BlockSize;
                float  _GlitchSpeed;
                half   _GlitchAmount;
                half   _DisplacePow;
                half   _BlockShiftMax;
                half   _RGBSplitMax;
                half4  _GShiftTint;
                half4  _BShiftTint;
                float4 _ClipRect;
            CBUFFER_END

            // ── UI 矩形裁剪（内联自 UnityUI.cginc，避免依赖）──
            inline half UIClip2D(float2 pos, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, pos) * step(pos, clipRect.zw);
                return (half)(inside.x * inside.y);
            }

            // ── 双随机 hash（per-pixel）──
            //   sin() 在 D3D11/GLES 上仅保证 [-100π,100π]≈[-314,314] 内精确
            //   所以必须用 frac() 把 timeSeed 贡献约束到小范围，再乘以适中系数
            //   确保 dot 结果始终在 [-200, 200] 以内
            inline float2 Hash22Time(float2 seed, float timeSeed)
            {
                float2 p = seed * 0.754 + frac(timeSeed * float2(0.1031, 0.1473)) * 4.0 + 1.7;
                return frac(sin(float2(
                    dot(p, float2(5.37, 3.12)),
                    dot(p, float2(2.93, 7.41))
                )) * 43758.5453);
            }

            // ── 屏幕常量双随机：G/B offset 的随机倍率 ──
            inline float2 ScreenHash2(float timeSeed)
            {
                float2 t = frac(float2(0.371, 0.593) * timeSeed) * 6.0 + 1.0;
                return frac(sin(t * 4.73) * 43758.5453);
            }

            // ────────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos   = IN.positionOS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = (half4)IN.color;
                return OUT;
            }

            // ────────────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 uv = IN.uv;

                // ── 0) 整帧时间种子（提出来，避免每次 hash 都重算）──
                //   floor(_Time.y * _GlitchSpeed) → 每 1/_GlitchSpeed 秒整体跳一次
                float timeSeed = floor(_Time.y * _GlitchSpeed);

                // ── 1) 块状随机：1 次 vec2 sin 算出 blockHash + dirHash ──
                //   blockHash → 控制本块是否触发 glitch
                //   dirHash   → 控制本块撕裂方向（左/右）
                float2 blockId = floor(uv * _BlockSize.xy);
                float2 hashes  = Hash22Time(blockId, timeSeed);
                half  blockHash = (half)hashes.x;
                half  dirHash   = (half)hashes.y;

                // ── 2) pow(hash, _DisplacePow) 稀疏化 ──
                //   _DisplacePow = 11（原版 QianMo）：极度稀疏，几秒一次大跳变
                //   _DisplacePow = 4（默认）：每秒都能看到几次 glitch
                //   _DisplacePow = 2：几乎全帧都在抖（连续抖动风）
                half displace = (half)pow((float)blockHash, (float)_DisplacePow) * _GlitchAmount;
                half disp01   = saturate(displace);

                // ── 3) 屏幕常量双随机（G/B 偏移的随机倍率）──
                //   编译器通常 hoist，即便没 hoist 也只是 1 次 vec2 sin
                float2 screenRand = ScreenHash2(timeSeed);
                half  randG = (half)screenRand.x;
                half  randB = (half)screenRand.y;

                // ── 4) 基础块位移：block 触发时整体水平撕裂 ──
                //   不同 block 朝不同方向撕（dirHash * 2 - 1 → [-1, 1]）
                //   displace=0 → baseShift=0；displace=1 → 撕到 _BlockShiftMax
                float baseShift = (float)disp01 * (float)_BlockShiftMax * (dirHash * 2.0h - 1.0h);
                float2 baseUV   = uv + float2(baseShift, 0);

                // ── 5) 三次采样 RGB 拆分（在已撕裂的 baseUV 上再加色散）──
                //   R = baseUV 中心
                //   G = baseUV 向右偏移
                //   B = baseUV 向左偏移
                //   randG/randB 给 G/B 不同的"晃动节奏"，比对称漂亮
                //   色散幅度也由 displace 驱动 → 没 glitch 时无色散
                half  rgbAmt = disp01 * _RGBSplitMax;
                float ofsG   = (float)(rgbAmt * (0.5h + 0.5h * randG));
                float ofsB   = (float)(rgbAmt * (0.5h + 0.5h * randB));

                half4 sR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV);
                half4 sG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2( ofsG, 0));
                half4 sB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + float2(-ofsB, 0));

                // ── 6) 三路独立染色 + 合成 ──
                //
                // 管线：
                //   base   = sR.rgb × _Color × vertexColor  （_Color 只染 base）
                //   gGhost = sG.a × _GShiftTint             （纯 alpha 做亮度 × tint，不受 _Color 影响）
                //   bGhost = sB.a × _BShiftTint             （同上）
                //
                // 合成（alpha-over）：
                //   ghosts 在底层，base 盖在上面
                //   sR.a=1 → 完全显示 base（字心内部）
                //   sR.a=0 → 完全显示 ghost（边缘外延 / 拖尾）
                //   sR.a∈(0,1) → 自然过渡

                // base：原始贴图色 × _Color × vertex color
                half3 baseCol = sR.rgb * _Color.rgb * IN.color.rgb;

                // G/B ghost：alpha 作为亮度遮罩 × 各自 tint（去色、纯色）
                half3 gGhost = sG.a * _GShiftTint.rgb;
                half3 bGhost = sB.a * _BShiftTint.rgb;

                // 合成：ghost 底层，base 盖上
                half3 col = lerp(gGhost + bGhost, baseCol, sR.a);

                // alpha：三路取最大（ghost 区域也可见），再乘总体透明度
                half a = max(sR.a, max(sG.a, sB.a)) * IN.color.a * _Color.a;

                half4 finalColor;
                finalColor.rgb = col;
                finalColor.a   = a;

                // ── 8) UGUI RectMask2D 裁剪（仅 UI 上下文有此 keyword）─
                #if defined(UNITY_UI_CLIP_RECT)
                    finalColor.a *= UIClip2D(IN.worldPos.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
