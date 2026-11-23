# 透明特效：先量覆盖和叠层，再找 GPU 瓶颈

**问题**：大面积烟雾、云层或半透明粒子变贵时，先缩遮罩和层数、简化 Shader，还是改混合方案？先用相同 Shader 测屏幕覆盖与叠层的敏感度，再用计数器和单变量试验区分片元计算、纹理访问、深度测试与写回的影响。

## 来源明确

核对日期：2026-11-23。

- [Arm Mali G2 Performance Counter Reference Guide · Overdraw](https://arm-software.github.io/libGPUCounters/mali_g2_counter_reference.html#g_overdraw)：`MaliFragOverdraw` 是每个输出像素平均着色的片元数。成本会随参与的层数累积；指南建议减少混合透明层，在高分辨率下尤其值得查。该值是平均量，不能定位哪一个粒子或屏幕区域，也不直接给出毫秒数。
- [同一指南 · Render passes and tile-based rendering](https://arm-software.github.io/libGPUCounters/mali_g2_counter_reference.html) 及 [External Bus Bandwidth](https://arm-software.github.io/libGPUCounters/mali_g2_counter_reference.html#g_externalbusbandwidth)：Mali 按 tile 处理，帧缓冲的工作集可留在片上，最终状态再写回。因此混合在逻辑上需要读取目标颜色，不等于每层都会向外部内存做一次颜色读写。外部流量计数反映 GPU 总线边界；要结合其他指标归因。
- [Unity 6000.0 LTS · ShaderLab Blend](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-blend)：混合由源值和目标值按因子与运算组合；启用混合会使一些硬件优化失效，文档举例是隐藏面剔除与 Early‑Z。实际仍能否及何时通过深度拒绝，要结合设备、着色器行为与绘制状态判断。

## 怎么用

- 把“画面占了多大”和“这些像素反复处理几次”分开。透明四边形即使贴图大部分看不见，也先按栅格化覆盖估算；Alpha 为零不是 Shader 没跑的证据。可优先试紧贴可见轮廓的网格或降低密集处层数，核对近景软边、动画范围和粒子形态有无受损。
- 先估算 `Σ 每层实际屏幕覆盖像素` 做取舍，不能当作片元执行数。提早剔除、部分覆盖的片元组、MSAA、渲染目标大小和平台实现都可能影响实测。落在同一区域的高峰和全屏平均也不是一回事。
- 仅凭过绘倍数无法判定是算术、贴图、混合还是带宽受限。如果各层 Shader 很复杂，再做简化片元工作量的对照；若想把软透明改成裁剪效果，先验证能否保留边缘和排序，再看 Early/Late Z。Arm G2 指南明确指出，`discard` 或 Alpha-to-Coverage 可能阻止 Early ZS 更新，不能保证裁剪一定更快。

## 最小对照：目标设备

用一个固定相机的合成场景，关闭粒子模拟和随机性；保持同样的透明 Shader、采样模式、深度和混合状态，以及分辨率、目标格式、MSAA、质量档与图形 API。预热后每组采同样长的稳定区间，记录 GPU 帧时间与透明 pass 时间（工具支持时），保留中位数、波动范围和设备/图形 API。

| 对照 | 只主动改什么 | 看什么 |
| --- | --- | --- |
| 覆盖 | 固定透明层数与网格拓扑，缩放屏幕投影面积。合成测试可固定纹理 mip，以减少距离带来的采样差异。 | GPU 时间和着色片元指标是否随面积上升；还要看边缘变化。 |
| 叠层 | 同一区域画 1、2、4 层；可用同一道实例化 draw 控制提交数量，额外记录几何工作仍随实例数改变。 | 结合片元计数、binning/main 时间（若可用）看变化。如果要区分总覆盖量与局部重叠，可再比较相同总覆盖的分散布局与叠层布局。 |
| 追因 | 固定几何与层数，另做片元精简或关闭混合的对照，每次只改一类。 | 结合纹理/算术计数、Early/Late Z、外部流量和帧耗判断；关混合会改变渲染语义及优化条件，差值不能直接叫作“混合单元耗时”。 |

**状态**：已核对厂商文档，验证表是候选流程。未运行 Unity 场景，未取得 GPU 捕获或任何设备实测；不判断哪个成本占主导。
