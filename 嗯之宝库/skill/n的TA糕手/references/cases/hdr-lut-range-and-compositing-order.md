# TA-41 · LUT 调色：高光收白先定位夹值，UI 偏色另查合成阶段

**制作决定**：HDR 画面在开启用户 LUT 后，彩色高光突然趋白，先在固定曝光下对比 LUT 权重为 0 和刚大于 0 的两帧。Unity 6 URP 的 HDR 调色路径有一个随用户 LUT 启用而执行的范围限制，降低权重仍可能跨入这个分支。UI 偏色则先确定 UI 绘制在调色之前还是之后，再决定改 LUT 或改合成位置。

## 已核对的证据

- **公开源码 · Unity URP 6000.0。** [Common.hlsl：ApplyColorGrading](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl#L101-L155) 先乘后曝光。在 HDR 调色分支中，输入转为 LogC，再查内部 LUT；若用户 LUT 权重大于 0，则在用户 LUT 的 sRGB 查表和混合之前，把此时的颜色按通道限制到 0–1。LDR 调色分支先做色调映射并限制范围，然后才走用户 LUT 和内部 LUT。开启 HDR 调色不等于让额外挂载的用户 LUT 处理未限制的场景亮度。如果内部 LUT 的输出早已全在 0–1，这处限制本身不会再丢高光。
- **公开源码 · 输出顺序。** [LutBuilderHdr.shader：生成内部 LUT](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/LutBuilderHdr.shader#L206-L240) 将 Alexa LogC 查表坐标解回线性，先做调色，再走对应的 SDR 色调映射或 HDR 输出处理；实现注释给出的输入上界约为 58.86 线性单位，它不是屏幕亮度，也不是任意导入 LUT 的范围。[UberPost.shader：调色和 UI 合成](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/UberPost.shader#L242-L284) 先处理场景颜色，之后在条件成立的 HDR 输出分支把已单独绘制的 Overlay UI 按纸白亮度和峰值亮度合成。这只能说明该路径，不能推出所有 Canvas 或世界空间 UI 都绕过调色。
- **公开文档 · 可迁移边界。** [Epic LUT 文档](https://dev.epicgames.com/documentation/unreal-engine/using-lookup-tables-for-color-grading-in-unreal-engine) 将传统用户 LUT 描述为面向显示的 LDR 工具，另有色调映射之前、面向场景的调色。给 SDR 交付做出的 LUT 要拿到 HDR 输出使用时，先定位它依赖的色彩空间和处理阶段。
- **夹值算例。** 假设到达夹值点的两个线性 RGB 为 (4, 2, 1) 和 (8, 4, 2)，逐通道限制到 0–1 后都变成 (1, 1, 1)。即使后面的 LUT 是恒等表，也恢复不了两档亮度与原有的通道比例。这里演示的是独立的数学操作，未声称这些值会出现在某个默认预设的内部 LUT 输出。

## 最小复核

1. 固定相机、曝光、色调映射和输出设置，放灰阶及带颜色的 HDR 亮度阶梯。用恒等用户 LUT 对比权重 0、很小正值和 1，抓取后曝光、内部 LUT 输出、用户 LUT 后的 RGB 和最终输出。若断层只在权重从 0 变正时出现，核对内部 LUT 输出是否有通道大于 1；若关闭用户 LUT 也出现，先看内部 LUT 映射和输出转换。
2. 单独验证 UI：用同样颜色做一块场景色卡和一块走 HDR Overlay 合成的 UI 色卡，固定纸白、峰值亮度和显示模式，只切调色配置。检查色卡在合成前后的数值；需要独立于场景调色的 UI，先确认实际抓帧中确实在调色后合成。
3. [Catlike Coding 调色教程](https://catlikecoding.com/unity/tutorials/custom-srp/color-grading/) 给出了同用 ACES 色调映射、改变调色工作空间的组图入口。这可用于看工作空间差异；不能代替权重为 0／正值的夹值对照。

**状态**：已核对公开源码分支和文档，算例为计算；作者组图未完成目视复核，尚无本地编辑器、HDR 显示器或真机对照。
