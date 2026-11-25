# 静止物体的发光图案换帧后，在时域超分里留残影

**一句话结论**：先比超分前后的同一帧，确认残留始于历史合成。对已经画入不透明颜色的发光图案，FSR 2.2.1 的透明绘制前后差分辅助函数看不到它的跨帧变化；优先在材质能确定的变化区域试局部遮罩，亮起和熄灭的像素都要覆盖。

## 公开实现：两个遮罩解决不同问题

- AMD [FSR 2.2.1 Reactive mask](https://github.com/GPUOpen-Effects/FidelityFX-FSR2/tree/v2.2.1#reactive-mask) 允许引擎告诉合成阶段哪些位置应更多采纳当前帧。文档建议透明混合可借用混合 Alpha，再限制上限；不透明发光材质的几何覆盖值不能直接当作反应强度。零速度只说明重投影回原位置；FSR 仍会用亮度变化、邻域色域和其他信号消减不可靠的历史。
- 同版本 [Transparency and composition mask](https://github.com/GPUOpen-Effects/FidelityFX-FSR2/tree/v2.2.1#transparency-and-composition-mask) 明列动画贴图为应用之一。它削弱细节锁和亮度不稳定性的保护；Reactive Mask 调的是积累比重。两条输入先各自做 A/B，避免同时开启后无法定位哪一项在起作用。
- 单独的 [`ffxFsr2ContextGenerateReactiveMask` 辅助着色器](https://github.com/GPUOpen-Effects/FidelityFX-FSR2/blob/v2.2.1/src/ffx-fsr2-api/shaders/ffx_fsr2_autogen_reactive_pass.hlsl) 计算同帧 `abs(ColorPostAlpha - ColorPreAlpha)`，再按标记取最大分量或长度，乘比例并可做阈值化。若不透明发光已经包含在两份颜色里，两输入在该像素相同，差值为零；调高比例解决不了这个反例。这只针对该辅助函数；同版[实验性遮罩自动生成](https://github.com/GPUOpen-Effects/FidelityFX-FSR2/tree/v2.2.1#automatically-generating-transparency-and-composition-mask) 是另一入口，文档说明还比较了上一帧。

## 算例与制作取舍

- 若一行发光从 `[1, 0]` 换成 `[0, 1]`，同一帧透明绘制前后都为 `[0, 1]` 时差分无法提示变化。只用本帧发光强度作为遮罩也会漏掉第一个、刚熄灭的位置；它恰好可能留着旧亮点。可试在换帧时写该材质的变化区，或用两帧图案的绝对差给这些像素信号。需要稳住静止细线时，不要把整片材质长期标成高反应。
- [积累源码](https://github.com/GPUOpen-Effects/FidelityFX-FSR2/blob/v2.2.1/src/ffx-fsr2-api/shaders/ffx_fsr2_accumulate.h) 中，`ComputeBaseAccumulationWeight` 的历史基权重乘 `1 - fThisFrameReactiveFactor`，`Accumulate` 用当前采样权重除以两者权重和。假设其他项和当前权重不变：历史基权重 4、当前权重 1，有效反应因子从 0 到 0.5 会把历史基权重降到 2，当前份额从 20% 变成约 33%。实际还会受遮罩扩张、前帧反应、深度、历史校正及当前重建权重等影响，遮罩数值不能直接当成最终混合比。

## 同镜头验证

固定镜头、网格、输入和输出分辨率、抖动序列、曝光与序列起始历史。暂关 Bloom、动态模糊和帧生成；先看超分输入是否已残留，再用一亮一灭的发光图案，分别录稳定图案和换帧后的连续输出。对照默认、自动前后差分、只给局部 Composition Mask、只给局部 Reactive Mask，最后再试组合；抓帧确认实际输入遮罩确实覆盖刚熄灭的位置。记录换帧后旧位置落到预设亮度差阈值所需的帧数，同时观察不变细线的亮度波动和边缘，选能减残影的最小范围与强度。

**状态**：已核对 AMD FSR 2.2.1 文档、遮罩和积累着色器及简化算例；尚未在引擎跑对照、抓帧或实机验证。
