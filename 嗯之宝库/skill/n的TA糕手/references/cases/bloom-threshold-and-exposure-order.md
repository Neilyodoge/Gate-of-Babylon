# TA-176 · 曝光一变，同一发光面的 Bloom 突然消失

**制作判断。** 先抓 Bloom 的预滤波输入和输出。HDRP 里场景曝光能把发光源推过 Bloom 的软阈值；Color Adjustments 的 Post Exposure 在提取之后，只能改已经合成的显示亮度。如果画面光晕不见了但 Bloom 纹理仍有内容，先查合成、后曝光、色调映射和输出，别先改提取阈值。

## 公开证据：以 HDRP 6000.0 为例

- **发光输入。** [UnlitData.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/UnlitData.hlsl) 在原发光和乘了逆当前曝光的发光之间按 `_EmissiveExposureWeight` 插值；[ShaderPassForwardUnlit.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl) 又把发光乘当前曝光后写入颜色。仅看无雾、不混合的发光分量，正曝光倍率为 `E`、权重为 `w` 时，输出相当于 `原发光 × (1−w+wE)`。`w=1` 完全随场景曝光，`w=0` 的这一路发光可抵消曝光，不能据此推断底色、整个材质或其他管线也一样。
- **Bloom 在哪个域提取。** [HDRenderPipeline.PostProcess.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/HDRenderPipeline.PostProcess.cs) 把当前颜色 `source` 交给 `BloomPass`；`GetBloomThresholdParams` 将面板阈值从 Gamma 转到线性，再取其约一半作软膝宽度。[BloomPrefilter.compute](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/BloomPrefilter.compute) 直接对输入样本应用阈值，未在此把亮度除回场景曝光；启用 Alpha 的路径先乘夹紧后的 Alpha。[BloomCommon.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/BloomCommon.hlsl) 用 RGB 最大值进入软阈值，不能拿显示亮度或像素 nits 直接替代。
- **后曝光是另一个控制。** 同一管线把 `2^PostExposure` 传到 [UberPost.compute](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/UberPost.compute)。常规 LUT 路径先合成 Bloom，才乘 `PostExposure` 并查调色 LUT；源码注释明确是让后曝光微调不改变 Bloom、DoF 等效果的处理。因此同样表现为变暗，两个控制可能在不同阶段产生差别。

## 一个发光面的算例

用足够大的不透明灰发光块隔离采样，假定最大通道原值为 2，发光曝光权重为 1，线性阈值为 1，软膝宽度约 0.5。忽略其他颜色和空间过滤，从 BloomCommon 计算单个样本的亮度：

| 场景曝光倍率 E | 提取前最大通道 | 软阈值后最大通道 |
| ---: | ---: | ---: |
| 1.000 | 2.00 | 1.0000 |
| 0.375 | 0.75 | 0.0313 |
| 0.200 | 0.40 | 0 |

输入 0.75 低于阈值仍有少量输出；0.40 低于软膝起点才归零。若固定场景曝光为 1，单把常规 Color Adjustments 的 Post Exposure 改为 −1 EV，表内提取值不变，进入调色 LUT 前的合成颜色在该步骤乘 0.5；最终显示值仍取决于 LUT 和输出。以上是源码算例，非画面测量。

## 最小验收

1. 固定分辨率、视角、材质、发光体面积和 Bloom 参数，对同一发光块分别扫相机手动曝光、Post Exposure；记录 Bloom 输入、Prefilter 输出、模糊纹理和最终颜色。用大纯色块先排采样平均，随后回到实际小发光体。若只有入口随相机曝光跨软阈值，再测曝光范围、阈值或材质曝光权重；若入口和提取都稳定，转查后合成。
2. 在同一个相机曝光对照只切发光权重 0／1，分别检查前景、合成后与品牌颜色是否合适；不要把灯、间接照明和 Bloom 当作同一强度。[TA-125](emissive-material-bloom-and-indirect-light.md) 专查墙面受光。恢复自动曝光时逐帧记录实际曝光倍率和 Bloom 首次过阈值的时刻；历史重置、抖动和小物件降采样分开做 A/B。

**验证状态。** 已查 HDRP 6000.0 材质、曝光和 Bloom 源码，复算软阈值；尚未在引擎抓取曝光时序、画面或真机测试。
