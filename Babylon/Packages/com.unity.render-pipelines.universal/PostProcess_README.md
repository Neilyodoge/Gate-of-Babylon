# Post-Processing 自定义修改说明

本文档记录了对 URP 后处理模块的自定义扩展和修改。

---

# Bloom 模块

在 URP 原始 Bloom 基础上扩展了 **BloomMode** 切换，支持两种 Bloom 算法：

| 特性 | Default（URP 内置） | n（自定义 nBloom） |
|------|---------------------|---------------------|
| 模糊方式 | 逐步高斯模糊（9-tap / 双线性） | **Kawase 模糊**（4-tap box 下采样 + Kawase 上采样） |
| 阈值处理 | 线性 soft-knee 阈值 | **二次阈值函数**（QuadraticThreshold），过渡更平滑 |
| 防闪烁 | 无 | **Kill Fireflies**（Karis Average 加权平均），抑制极亮像素造成的闪烁 |
| 上采样 | 可选双三次插值（High Quality Filtering） | Kawase 滤波上采样，天然平滑 |
| 编码方式 | RGBM 编码（移动端兼容） | 直接 HDR，不使用 RGBM 编码 |
| 性能特征 | 标准开销，适合通用场景 | 采样次数更少，适合需要高质量 Bloom 且对性能敏感的场景 |

---

## 算法对比

### Default 模式（URP 内置 Bloom）

URP 内置 Bloom 采用经典的 **逐级高斯模糊 + Mip 金字塔** 方案：

1. **预过滤（Prefilter）**：对源图像应用亮度阈值，提取高亮区域
2. **下采样金字塔**：逐级半分辨率下采样，每级使用 9-tap 高斯滤波核
3. **上采样合并**：从最低 Mip 逐级上采样回高分辨率，使用双线性或双三次插值
4. **最终合成**：将 Bloom 结果叠加回原图，支持 Lens Dirt

该方案兼容性好，是 URP 的标准实现。

### n 模式（自定义 nBloom）

nBloom 基于 **Kawase Blur** 方案，参考了以下文章的思路：

> **参考文献**：[高质量泛光Bloom改进以及高斯核采样的优化 - 知乎 Zhihu](https://zhuanlan.zhihu.com/p/630726865)

#### 核心流程

```
源图像
  │
  ▼
[Pass 0] 预过滤（QuadraticThreshold + Clamp）
  │
  ▼
[Pass 1] 下采样金字塔（4-tap Box Filter + Kill Fireflies / Karis Average）
  │  ½ → ¼ → ⅛ → ... → 1/2^N
  ▼
[Pass 2] Kawase 上采样合并（逐级混合）
  │  1/2^N → ... → ⅛ → ¼ → ½
  ▼
[UberPost] 最终合成（Intensity × Tint + Lens Dirt）
```

#### 关键技术点

**1. 二次阈值函数（Quadratic Threshold）**

不同于 URP 默认的线性 soft-knee，nBloom 使用二次曲线实现阈值过渡：

```hlsl
half soft = brightness - softThresholdBrightness;
soft = clamp(soft, 0.0, kneeOffset);
soft = soft * soft * kneeScale;  // 二次过渡
half contribution = max(soft, brightness - threshold);
```

这使得 Bloom 边缘过渡更加柔和自然，避免硬切割感。

**2. Kill Fireflies（抑制萤火虫 - Karis Average）**

在下采样阶段使用 **Karis Average（亮度倒数加权平均）** 算法，自然地压制极端亮度的像素，防止单个极亮像素在 Bloom 中产生闪烁。

通过 `#pragma multi_compile_local _ _KILL_FIREFLY` 编译期关键字控制，关闭时零性能开销。

```hlsl
// Karis Average：w = 1 / (strength + luminance)，亮度越高权重越低
half w0 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s0.rgb));
half w1 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s1.rgb));
half w2 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s2.rgb));
half w3 = 1.0 / (FILTER_STRENGTH + luminance_sRGB(s3.rgb));
half w = w0 + w1 + w2 + w3;
result = (s0 * w0 + s1 * w1 + s2 * w2 + s3 * w3) / w;
```

**原理**：计算每个采样点的亮度，取其倒数作为混合权重。亮度越高的像素权重越低，从而被自然压制；亮度正常的像素权重保持较高，几乎不受影响。这种方式避免了硬阈值判断带来的视觉断层，过渡完全平滑连续。

其中 `FILTER_STRENGTH = 1.0` 控制压制力度（值越小压制越激进），`1.0` 为业界常用默认值。

**3. Kawase 模糊上采样**

上采样阶段使用 Kawase 滤波核（4 个对角采样点），相比标准双线性插值能以更少的采样次数获得更大的模糊范围：

```hlsl
half4 sum = SAMPLE(uv + offset * float2(-1, -1));
sum += SAMPLE(uv + offset * float2( 1, -1));
sum += SAMPLE(uv + offset * float2(-1,  1));
sum += SAMPLE(uv + offset * float2( 1,  1));
return sum * 0.25;
```

---

## 当前实现 vs 文章中的 Dual Kawase Blur

参考文章中介绍了多种模糊算法，其中与 nBloom 最相关的是 **Dual Kawase Blur**（双重 Kawase 模糊）。当前 nBloom 采用的是 **简化版 Kawase**，与文章中的标准 Dual Kawase 存在以下差异：

### 采样方式对比

| 阶段 | 文章 Dual Kawase Blur | 当前 nBloom 实现 |
|------|----------------------|------------------|
| 下采样 | **5-tap**：中心点 ×4 + 4 对角点 ×1（加权平均，中心权重更高） | **4-tap Box Filter**：4 对角点等权平均（无中心采样） |
| 上采样 | **8-tap**：4 对角点 ×2 + 4 正交（上下左右）点 ×1（共 12 权重） | **4-tap Kawase**：仅 4 对角点等权平均 |

### 具体差异分析

**1. 下采样差异**

文章中的 Dual Kawase 下采样：
```hlsl
// 5-tap: 中心点权重 4/8，四个对角各 1/8
half4 sum = tex(uv) * 4.0;       // 中心采样，权重 ×4
sum += tex(uv + d.xy);            // 左下
sum += tex(uv + d.zy);            // 右下
sum += tex(uv + d.xw);            // 左上
sum += tex(uv + d.zw);            // 右上
return sum * (1.0 / 8.0);
```

当前 nBloom 下采样：
```hlsl
// 4-tap: 四个对角各 1/4，无中心采样
half4 s1 = tex(uv + d.xy);
half4 s2 = tex(uv + d.zy);
half4 s3 = tex(uv + d.xw);
half4 s4 = tex(uv + d.zw);
return (s1 + s2 + s3 + s4) * 0.25;
```

**差异**：文章方案通过中心点的高权重保留了更多高频细节，而当前方案是纯均匀采样，模糊更均匀但可能丢失少量细节。

**2. 上采样差异**

文章中的 Dual Kawase 上采样：
```hlsl
// 8-tap: 4 对角 ×2 + 4 正交 ×1，共 12 权重
half4 sum = tex(uv + float2(-d, -d)) * 2.0; // 左下 ×2
sum += tex(uv + float2( d, -d)) * 2.0;      // 右下 ×2
sum += tex(uv + float2(-d,  d)) * 2.0;      // 左上 ×2
sum += tex(uv + float2( d,  d)) * 2.0;      // 右上 ×2
sum += tex(uv + float2(-d * 2, 0));          // 左
sum += tex(uv + float2( d * 2, 0));          // 右
sum += tex(uv + float2(0, -d * 2));          // 下
sum += tex(uv + float2(0,  d * 2));          // 上
return sum * (1.0 / 12.0);
```

当前 nBloom 上采样：
```hlsl
// 4-tap: 仅 4 对角，等权
half4 sum = tex(uv + offset * float2(-1, -1));
sum += tex(uv + offset * float2( 1, -1));
sum += tex(uv + offset * float2(-1,  1));
sum += tex(uv + offset * float2( 1,  1));
return sum * 0.25;
```

**差异**：文章方案有 8 个采样点（4 对角 + 4 正交），覆盖范围更大、各向同性更好；当前方案仅 4 个对角采样，覆盖方向有限。

### 优劣总结

| 维度 | 文章 Dual Kawase | 当前 nBloom | 说明 |
|------|-----------------|-------------|------|
| **模糊质量** | ⭐⭐⭐ 更好 | ⭐⭐ 良好 | Dual Kawase 各向同性更好，边缘更柔和 |
| **采样性能** | 下采样 5-tap + 上采样 8-tap = 13 次/级 | 下采样 4-tap + 上采样 4-tap = 8 次/级 | 当前方案每级少 5 次采样，性能更优 |
| **细节保留** | 中心加权保留更多高频信息 | 均匀采样，高频信息衰减更快 | Dual Kawase 在保留 Bloom 源细节上更好 |
| **实现复杂度** | 中等 | 简单 | 当前方案代码更简洁 |
| **视觉差异** | 更圆润、更自然的光晕 | 可能呈轻微十字/方形倾向 | 实际差异在多级金字塔叠加后不太明显 |

> **结论**：当前 nBloom 的实现是 Kawase Blur 的简化版本，以牺牲少量模糊质量换取更好的性能。在大多数游戏场景下视觉差异不大，若追求更高品质可考虑升级为文章中的标准 Dual Kawase 方案。

---

## 使用方式

1. 在 Volume 组件中添加 **Bloom** Override
2. 在 Inspector 顶部的 **Bloom Mode** 下拉框中选择模式：
   - `Default`：使用 URP 内置 Bloom
   - `n`：使用自定义 nBloom 算法
3. 当选择 `n` 模式时，会显示额外的 **nBloom Mode Settings**：
   - **Threshold Knee**：阈值过渡柔和度（0~1）
   - **Kill Fireflies**：是否开启萤火虫抑制（Karis Average 加权平均，编译期关键字，关闭时无性能开销）

两种模式共享以下通用参数：Threshold、Intensity、Scatter、Tint、Clamp、High Quality Filtering、Downscale、Max Iterations、Lens Dirt。

---

## Bloom 涉及文件

| 文件 | 说明 |
|------|------|
| `Runtime/Overrides/Bloom.cs` | Bloom Volume 组件定义，包含 BloomMode 枚举和所有参数 |
| `Editor/Overrides/BloomEditor.cs` | Bloom Inspector 编辑器，根据模式显示/隐藏参数 |
| `Runtime/Passes/PostProcessPass.cs` | 后处理渲染 Pass，包含 `SetupBloom`（Default）和 `SetupnBloom`（n）两套渲染流程 |
| `Shaders/PostProcessing/nBloom.shader` | nBloom 专用 Shader（Prefilter / Downsample / Upsample / Combine） |
| `Runtime/Data/PostProcessData.cs` | 后处理资源数据，引用 nBloom Shader |
| `Runtime/Data/PostProcessData.asset` | 后处理资源配置 |

---

# Tonemapping 模块

在 URP 原始 Tonemapping 基础上扩展了四种额外的 Tonemapping 算法：**GT**（Gran Turismo Tonemapping）、**ACESSimple**（简化版 ACES）、**UE4**（Unreal Engine 4 Film Tonemapper）和 **PBRNeutral**（Khronos PBR Neutral），加上 URP 内置的 **Neutral** 和 **ACES**，共提供六种可选的色调映射模式。

## 模式总览

| 模式 | 来源 | 色彩空间 | 性能 | 适用场景 |
|------|------|---------|------|---------|
| **None** | — | — | — | 不应用色调映射 |
| **Neutral** | URP 内置 | sRGB | ⭐⭐⭐ 低 | 通用场景，对色相/饱和度影响最小，适合作为深度调色的起点 |
| **ACES** | URP 内置 | ACEScg/ACES | ⭐⭐ 中 | 影视级品质，完整的 ACES RRT+ODT 近似，色彩准确但计算量较大 |
| **GT** | Hajime Uchimura (GDC 2017) | sRGB | ⭐⭐⭐ 低 | 写实风格，暗部细节保留好，高光压缩自然 |
| **ACESSimple** | Krzysztof Narkowicz (2016) | sRGB | ⭐⭐⭐⭐ 极低 | 移动端/性能敏感场景，视觉接近 ACES 但仅需一个有理函数 |
| **UE4** | Unreal Engine 4/5 | ACEScg/ACES | ⭐⭐ 中 | 完整的 UE 原生 Film Tonemapper，包含 Glow/Red Modifier/Pre-Post Desaturation |
| **PBRNeutral** | Khronos (glTF) | sRGB | ⭐⭐⭐ 低 | 等比缩放高光保色相、仅高光受控去饱和，暗部可下压；忠实还原材质颜色，适合 PBR/写实 |

## 各算法详细说明

### Neutral（URP 内置）

URP 默认的 Neutral Tonemapper，特点是对色相和饱和度影响最小，仅做范围重映射。适合需要大量后期调色的工作流，因为它不会引入额外的色彩偏移。

### ACES（URP 内置）

完整的 ACES（Academy Color Encoding System）色调映射近似。整个 Color Grading 流程都在 ACES 色彩空间中进行，包括：
- 对比度调整在 ACEScc 空间中完成
- Tonemapping 使用 `ACEScg → ACES(AP0) → AcesTonemap()` 的标准流程
- 亮度计算使用 `AcesLuminance()` 而非标准 `Luminance()`

视觉效果更具电影感，对比度更强，但会影响色相和饱和度。

### GT（Gran Turismo Tonemapping）

> **参考文献**：Hajime Uchimura, "HDR Theory and practice", GDC 2017 / CEDEC 2017

该算法使用一条分段 S 曲线，分为暗部（Toe）、线性段（Linear）、高光（Shoulder）三部分。

#### 核心公式

```
分段函数 f(x):
  暗部 (x < m):     T = m * pow(x/m, c) + b
  线性段 (m ≤ x ≤ m+l):  L = m + a * (x - m)
  高光 (x > m+l):    S = P - (P - S1) * exp(CP * (x - S0))
```

#### 默认参数

| 参数 | 值 | 说明 |
|------|-----|------|
| P | 1.0 | 最大亮度 (Max Brightness) |
| a | 1.0 | 对比度 (Contrast) |
| m | 0.22 | 线性段起始点 (Linear Section Start) |
| l | 0.4 | 线性段长度 (Linear Section Length) |
| c | 1.33 | 暗部曲线形状 (Black Tightness Shape) |
| b | 0.0 | 暗部提升偏移 (Pedestal) |

#### 特点

- **暗部细节保留好**：通过 `c`（暗部曲线形状）参数控制暗部压缩程度，`c > 1` 时暗部提升更平缓
- **高光压缩自然**：使用指数衰减（`exp`）实现高光到最大亮度的平滑过渡
- **色彩准确**：在 sRGB 空间中逐通道操作，不引入色彩空间变换带来的偏移
- **性能优秀**：无需复杂的色彩空间转换，计算量与 Neutral 相当

### ACESSimple（简化版 ACES）

> **参考文献**：[ACES Filmic Tone Mapping Curve - Krzysztof Narkowicz](https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/)
> **可视化**：[Desmos 曲线](https://www.desmos.com/calculator/zygyam5cg3?lang=zh-CN)

使用单个有理函数拟合 ACES 曲线，代码极为简洁：

```hlsl
f(x) = (x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)
```

#### ACES vs ACESSimple 对比

| 特性 | ACES（完整版） | ACESSimple（简化版） |
|------|--------------|---------------------|
| 色彩空间 | 在 ACEScg 色彩空间中操作 | 直接在线性 sRGB 中操作 |
| Color Grading | 对比度在 ACEScc 中调整，亮度用 ACES Luminance | 标准 sRGB 流程，标准 Luminance |
| 指令数 | 较多（Glow、Red Modifier、全局去饱和等） | 仅一个有理函数，约 5 条指令 |
| 精度 | 高，忠实还原 ACES RRT+ODT 参考 | 近似拟合，暗部/亮部有轻微偏差 |
| 适用场景 | 影视级品质渲染 | 移动端、性能敏感场景 |

### UE4（Unreal Engine 4 Film Tonemapper）

> **参考来源**：Unreal Engine 4/5 `TonemapCommon.ush` 中的 `FilmToneMap` 函数

这是 UE4/UE5 默认使用的 Film Tonemapper，完整移植自参考工程的 `TonemapCommon.cginc`。与 URP 内置 ACES 不同，UE4 版本使用的是基于 **log10 的参数化三段 S 曲线**（Toe/Straight/Shoulder），而非 URP ACES 的 RRT+ODT 近似。

#### 处理流程

```
sRGB (D65)
  │
  ▼
[D65→D60 色度适应] sRGB → XYZ → D65_2_D60_CAT → AP0 (ACES2065-1)
  │
  ▼
[Glow 模块] 低亮度微光增益（RRT_GLOW_GAIN = 0.05）
  │  基于场景平均亮度，在暗部添加微弱的整体亮度提升
  ▼
[Red Modifier] 红色色相校正（RRT_RED_SCALE = 0.82）
  │  压缩过饱和红色，使其向橙色偏移，避免红色过于刺眼
  ▼
[AP0 → AP1] 转入 ACEScg 工作空间
  │
  ▼
[Pre Desaturation] 全局去饱和 0.96
  │  Tonemap 前轻微降低饱和度，防止高饱和色彩在映射时溢色
  ▼
[Film Tone Curve] 参数化 S 曲线（log10 域）
  │  三段式：Toe（暗部）/ Straight（线性段）/ Shoulder（高光）
  │  使用 smoothstep 三次 Hermite 插值实现平滑过渡
  ▼
[Post Desaturation] 全局去饱和 0.93
  │  Tonemap 后进一步降低饱和度，确保输出色彩在 sRGB 色域内
  ▼
[D60→D65 色度适应] AP1 → XYZ → D60_2_D65_CAT → sRGB
  │
  ▼
sRGB 输出
```

#### Film Tone Curve 核心算法

在 log10 域中构建分段 S 曲线，三段各用二次函数（smoothstep）拟合：

```hlsl
// Toe (暗部): log10 空间中的二次曲线
real ToeVal = (-ToeSlope * ToeMatch + ToeOffset) * smoothstep(0, 1, (ToeNew - x) / (ToeNew - InBlack));

// Shoulder (高光): log10 空间中的二次曲线
real ShoulderVal = (ShoulderSlope * ShoulderMatch + ShoulderOffset) * smoothstep(0, 1, (x - ShoulderNew) / (InWhite - ShoulderNew));

// 最终组合
real f = x + ToeVal + ShoulderVal;
return exp2(f) - FilmBlackClip;  // 从 log2 域回到线性
```

#### 默认参数（ACES 预设）

| 参数 | 值 | 说明 |
|------|-----|------|
| `FilmSlope` | 0.91 | 曲线中间段斜率，控制整体对比度 |
| `FilmToe` | 0.53 | 暗部曲线强度，值越大暗部越暗 |
| `FilmShoulder` | 0.23 | 高光压缩强度，值越大高光越压缩 |
| `FilmBlackClip` | 0.0 | 黑色裁切，控制最暗处截断 |
| `FilmWhiteClip` | 0.035 | 白色裁切，控制最亮处过曝余量 |

#### UE4 vs URP ACES 对比

| 特性 | URP ACES（内置） | UE4 Film Tonemapper |
|------|-----------------|---------------------|
| Tonemap 曲线 | RRT+ODT 近似（分段有理函数） | log10 参数化三段 S 曲线 |
| Glow 模块 | ✅ | ✅ |
| Red Modifier | ✅ | ✅ |
| Pre Desaturation | ✅ (0.96) | ✅ (0.96) |
| Post Desaturation | ✅ (0.93) | ✅ (0.93) |
| 曲线可调参数 | 无（固定 RRT+ODT 拟合） | 5 个参数（Slope/Toe/Shoulder/BlackClip/WhiteClip） |
| Color Grading 空间 | ACEScc（D60 白点） | 标准 sRGB/LogC（D65 白点） |
| 色度适应 | D60→D65（仅输出时） | D65→D60→D65（往返） |
| 视觉风格 | 更高对比度，更强烈的电影感 | 更接近 UE4/UE5 的默认渲染效果 |

### PBRNeutral（Khronos PBR Neutral）

> **参考来源**：Khronos Group glTF PBR Neutral Tonemapper（https://github.com/KhronosGroup/ToneMapping）
> 本工程移植自 topheroes 版本，额外带一个 `darken` 黑点下压幅度参数，**已在 Tonemapping Volume 上暴露为可调滑条**（`Pbr Neutral Darken`，范围 0~1，默认 1.0 = 标准 Khronos PBR Neutral）。
> 该值由 C# 每帧写入 uniform `_PBRNeutralDarken`：HDR 路径经 `ColorGradingLutPass` 设到 LutBuilderHdr 材质并烘入 LUT，LDR 路径经 `PostProcessPass` 设到 UberPost 材质。

该算法专为「忠实还原材质基础色」设计，核心思路：

1. **黑点偏移（暗部下压）**：对最小通道做二次偏移 `offset`，`darken` 控制幅度（0 关闭，1 标准）。
2. **等比缩放保色相**：当峰值超过压缩起点（`0.8 - 0.04`）时，对 RGB 三通道**等比例**缩放到新峰值，因此不改变色相。
3. **仅高光去饱和**：只在高光区做受控去饱和（`desaturation = 0.15`），中低亮度颜色几乎不动。

#### 核心公式

```hlsl
const float startCompression = 0.8 - 0.04;
const float desaturation     = 0.15;
float x = min3(color.rgb);
float offset = (x < 0.08 ? x - 6.25 * x * x : 0.04) * darken;
color -= offset;
float peak = max3(color.rgb);
if (peak < startCompression) return color;      // 中低亮度直接返回
float d = 1.0 - startCompression;
float newPeak = 1.0 - d * d / (peak + d - startCompression);
color *= newPeak / peak;                        // 等比缩放 → 保色相
float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
color = lerp(color, newPeak.xxx, g);            // 仅高光受控去饱和
```

#### 特点

- **保色相最好**：等比缩放使得高光不偏色（相比 Neutral 的 soft-knee 更稳），是它相对 URP Neutral 的主要优势。
- **暗部干净**：黑点偏移把接近 0 的噪声压下去，暗部更沉。
- **性能低**：纯 sRGB 逐通道运算，无色彩空间转换，开销与 Neutral / GT 相当。

---

## 色温白点（White Point）参考

各 Tonemapping 模式在内部处理和输出时使用不同的色温白点基准：

| Tonemapping | 工作白点 | 输出白点 | 色度适应 (CAT) | 说明 |
|-------------|---------|---------|---------------|------|
| **ACES** | D60 | D65 | D60→D65（输出时） | ACES 标准白点为 D60 (CIE xy = 0.32168, 0.33767)，完整 Color Grading 流程（ACEScc 对比度、ACES Luminance）都在 D60 色彩空间中进行 |
| **UE4** | D60 | D65 | D65→D60→D65（往返） | 输入时先做 D65→D60 色度适应进入 ACES 域处理，处理完后再做 D60→D65 转回 sRGB |
| **Neutral** | D65 | D65 | 无 | 直接在 sRGB (D65) 线性空间中逐通道操作 |
| **GT** | D65 | D65 | 无 | 直接在 sRGB (D65) 线性空间中逐通道操作 |
| **ACESSimple** | D65 | D65 | 无 | 尽管名为"ACES Simple"，但不做任何色彩空间转换，直接在 sRGB 中操作 |
| **PBRNeutral** | D65 | D65 | 无 | 直接在 sRGB (D65) 线性空间中逐通道操作（等比缩放 + 高光去饱和） |

**核心区别**：ACES 和 UE4 在 Tonemap 前会把颜色从 sRGB (D65) 转到 ACES 域 (D60)，在高饱和度区域的色彩处理更准确（因 D60 和 D65 对 R/G/B 通道的增益不同）。而 Neutral/GT/ACESSimple 始终在 D65 sRGB 色彩空间中直接操作。

**Color Grading 阶段**：
- **ACES 模式**：对比度调整在 ACEScc 空间中完成（D60 白点），亮度计算使用 `AcesLuminance()`（基于 AP1 三刺激值 `AP1_RGB2Y`）
- **其他所有模式**（Neutral/GT/ACESSimple/UE4）：对比度在 LogC 空间中完成（D65 白点），亮度计算使用标准 `Luminance()`（基于 sRGB 系数）

**白平衡**：所有模式共享相同的白平衡实现（`LinearToLMS` → `_ColorBalance` → `LMSToLinear`），在 LMS 色彩空间中操作。LMS 转换（Hunt-Pointer-Estevez 变换）是一个感知模型空间，不依赖 D60/D65 的选择，因此白平衡调整对所有模式行为一致。

---

## Tonemapping 集成架构

### 数据流

```
Volume (Tonemapping.cs)
  │  TonemappingMode 枚举
  ▼
C# Pass (ColorGradingLutPass / PostProcessPass)
  │  根据 mode 启用对应 Shader 关键字
  │  _TONEMAP_NEUTRAL / _TONEMAP_ACES / _TONEMAP_GT / _TONEMAP_ACES_SIMPLE / _TONEMAP_UE4 / _TONEMAP_PBRNEUTRAL
  ▼
Shader (LutBuilderHdr / UberPost)
  │  multi_compile 分支选择
  ▼
Tonemap 函数 (LutBuilderHdr.shader → Tonemap())
  │  HDR Color Grading 路径：在 LUT 构建时应用
  │
ApplyTonemap 函数 (Common.hlsl)
  │  LDR 路径：在 UberPost 中直接应用
  ▼
色调映射算法实现 (Color.hlsl)
  NeutralTonemap() / AcesTonemap() / GTTonemap() / ACESSimpleTonemap() / UE4FilmTonemap() / PBRNeutralTonemap()
```

### HDR vs LDR 两条路径

- **HDR Color Grading**（`_HDR_GRADING` 启用时）：Tonemapping 在 `LutBuilderHdr.shader` 的 `Tonemap()` 函数中执行，结果烘入 3D LUT，后续通过 LUT 查表应用
- **LDR Color Grading**：Tonemapping 在 `Common.hlsl` 的 `ApplyTonemap()` 中直接执行，在 `UberPost.shader` 中调用

### Shader 关键字与 Variant Stripping

每种 Tonemapping 模式对应一个 shader 关键字，通过 `multi_compile_local` 声明：

```hlsl
#pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL _TONEMAP_GT _TONEMAP_ACES_SIMPLE _TONEMAP_UE4 _TONEMAP_PBRNEUTRAL
```

`ShaderScriptableStripper.cs` 会在构建时剥离未使用的 Tonemapping variant，减小包体。

---

## 使用方式

1. 在 Volume 组件中添加 **Tonemapping** Override
2. 在 **Mode** 下拉框中选择算法：
   - `None`：不应用色调映射
   - `Neutral`：URP 默认，对色彩影响最小
   - `ACES`：完整 ACES 近似，电影感色调
   - `GT`：Gran Turismo Tonemapping，写实风格
   - `ACESSimple`：简化 ACES，性能优先
   - `UE4`：Unreal Engine 4 Film Tonemapper
   - `PBRNeutral`：Khronos PBR Neutral，保色相最好、忠实还原材质色
3. 选择 **ACES** 或 **Neutral** 时，若启用 HDR Output 还可配置额外参数（Range Reduction Mode、Paper White 等）

> **注意**：GT、ACESSimple 和 UE4 不使用 ACES 色彩空间进行 Color Grading，走标准 sRGB/LogC 路径（与 Neutral 一致）。UE4 模式在 Tonemap 阶段自行进行 sRGB↔ACES 的色彩空间往返转换。在 HDR Output 场景下，它们会走通用的 Rec2020 转换路径。

---

## Tonemapping 涉及文件

| 文件 | 说明 |
|------|------|
| `Runtime/Overrides/Tonemapping.cs` | Tonemapping Volume 组件定义，包含 `TonemappingMode` 枚举、`pbrNeutralDarken` 可调参数 |
| `Editor/Overrides/TonemappingEditor.cs` | Tonemapping Inspector：PBRNeutral 模式下显示 `Pbr Neutral Darken` 滑条 |
| `Runtime/UniversalRenderPipelineCore.cs` | Shader 关键字字符串定义（`_TONEMAP_GT`、`_TONEMAP_ACES_SIMPLE`、`_TONEMAP_UE4`、`_TONEMAP_PBRNEUTRAL`） |
| `Runtime/Passes/ColorGradingLutPass.cs` | HDR LUT 构建 Pass，根据 mode 启用对应关键字 |
| `Runtime/Passes/PostProcessPass.cs` | 后处理渲染 Pass，LDR 路径中根据 mode 启用对应关键字 |
| `Shaders/PostProcessing/LutBuilderHdr.shader` | HDR LUT 构建 Shader，包含 `Tonemap()` 函数的所有分支 |
| `Shaders/PostProcessing/UberPost.shader` | UberPost Shader，声明 tonemapping multi_compile |
| `Shaders/PostProcessing/Common.hlsl` | 后处理公共函数，包含 `ApplyTonemap()` 的所有分支 |
| `com.unity.render-pipelines.core/../Color.hlsl` | 核心色彩库，包含 `GTTonemap()`、`ACESSimpleTonemap()`、`UE4FilmTonemap()` 和 `PBRNeutralTonemap()` 的算法实现 |
| `com.unity.render-pipelines.core/../ACES.hlsl` | ACES 色彩科学库，包含色彩空间矩阵和转换函数（D60↔D65 CAT、AP0/AP1/sRGB 转换等） |
| `Editor/ShaderScriptableStripper.cs` | Shader variant stripping，构建时剥离未使用的 tonemapping variant |
