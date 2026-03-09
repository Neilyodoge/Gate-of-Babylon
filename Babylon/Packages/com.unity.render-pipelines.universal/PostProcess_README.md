# Post-Processing 自定义修改说明

## Bloom 模块

在 URP 原始 Bloom 基础上扩展了 **BloomMode** 切换，支持两种 Bloom 算法：

| 特性 | Default（URP 内置） | n（自定义 nBloom） |
|------|---------------------|---------------------|
| 模糊方式 | 逐步高斯模糊（9-tap / 双线性） | **Kawase 模糊**（4-tap box 下采样 + Kawase 上采样） |
| 阈值处理 | 线性 soft-knee 阈值 | **二次阈值函数**（QuadraticThreshold），过渡更平滑 |
| 防闪烁 | 无 | **Kill Fireflies**，抑制极亮像素造成的闪烁 |
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
[Pass 1] 下采样金字塔（4-tap Box Filter + Kill Fireflies）
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

**2. Kill Fireflies（抑制萤火虫）**

在下采样阶段检测并抑制极端亮度的像素，防止单个极亮像素在 Bloom 中产生闪烁：

```hlsl
half avgL = (l1 + l2 + l3 + l4) * 0.25;
half maxL = max(max(l1, l2), max(l3, l4));
if (maxL > avgL * 8.0)
{
    result = half4(avgL, avgL, avgL, 1.0);
}
```

当某个采样点亮度超过平均亮度 8 倍时，用平均亮度替代，有效消除萤火虫效应。

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
   - **Kill Fireflies**：是否开启萤火虫抑制

两种模式共享以下通用参数：Threshold、Intensity、Scatter、Tint、Clamp、High Quality Filtering、Downscale、Max Iterations、Lens Dirt。

---

## 涉及文件

| 文件 | 说明 |
|------|------|
| `Runtime/Overrides/Bloom.cs` | Bloom Volume 组件定义，包含 BloomMode 枚举和所有参数 |
| `Editor/Overrides/BloomEditor.cs` | Bloom Inspector 编辑器，根据模式显示/隐藏参数 |
| `Runtime/Passes/PostProcessPass.cs` | 后处理渲染 Pass，包含 `SetupBloom`（Default）和 `SetupnBloom`（n）两套渲染流程 |
| `Shaders/PostProcessing/nBloom.shader` | nBloom 专用 Shader（Prefilter / Downsample / Upsample / Combine） |
| `Runtime/Data/PostProcessData.cs` | 后处理资源数据，引用 nBloom Shader |
| `Runtime/Data/PostProcessData.asset` | 后处理资源配置 |
