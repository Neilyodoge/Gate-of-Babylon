# TA-255 · 分辨率降档后，旧的渲染纹理为什么还在

**制作决定。** 降档后先查当前 Pass 绑定的纹理实例、真实尺寸和视口。仍绑定同一张大图，先查 RTHandle 的最大参考尺寸；已换小图且旧实例短期还在，先查 URP 重分配池；过了清退点或在关闭暂存的对照里仍有独立旧实例，才去查其他持有者、渲染图资源和延迟释放。重分配函数返回 `true` 不能当作新纹理分配计数。

## 公开实现 · Unity Graphics `6000.0/staging`

1. **当前视口缩小，自动缩放纹理没缩容。** [Core 使用说明](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Documentation~/rthandle-system-using.md#updating-the-rthandle-system)和 [RTHandleSystem](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/Textures/RTHandleSystem.cs#L238-L303)把 `currentViewportSize` 与 `currentRenderTargetSize` 分开。普通 `SetReferenceSize` 更新视口，超过最大值才扩容；URP [在渲染时用相机目标描述符更新参考尺寸](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipeline.cs#L825-L830)。`ResetReferenceSize` 可以显式重置最大尺寸，但 [Resize 会对该系统登记的自动缩放目标调用 Release／Create](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/Textures/RTHandleSystem.cs#L477-L514)，同一个 Unity 对象 ID 仍可能重建底层资源。源码还有仅编辑器的极端尺寸自动重置分支，别拿编辑器长驻结果推断 Player。
2. **显式描述符换了，小图已在用，大图进池。** 例如 Compatibility 路径的 [`_CameraOpaqueTexture`](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L1311-L1321) 由相机描述符与降采样设置得出显式尺寸。[RenderingUtils](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RenderingUtils.cs#L612-L640) 在显式路径比较宽高；[`ReAllocateHandleIfNeeded`](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RenderingUtils.cs#L825-L877) 先处理旧句柄，再按新描述符取池，命中也返回 `true`。池的键包含[显式尺寸等描述符字段](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/RenderGraphResourceTexture.cs#L395-L403)及[目标名称](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RTHandleUtils.cs#L165-L173)。测试高低切换时保持名字、格式及采样设置一致，只改显式宽高。
3. **池暂存有明确清退时机。** [RTHandleUtils](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RTHandleUtils.cs#L21-L135) 默认最多暂存 32 个，按对象数计；URP [正常渲染帧末](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipeline.cs#L490-L495)清理。若旧图在帧 F 归还、未再取出，且后续持续渲染，条件 `F + 3 < currentFrameIndex` 最早在 F+4 帧末满足。[设置池容量](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RenderingUtils.cs#L1008-L1033)须等 URP 初始化；值改变时清现有池，容量为 0 时拒绝旧资源入池，入口随即释放。该设置只适合跑独立诊断或事先设定预算；不能当成每次切档的清理函数。Render Graph 临时纹理及外部导入的历史资源有各自的生命周期，不能套用这里的 3 帧规则，见 [TA-140](render-graph-history-texture-lifetime.md)。

## 算例与最小对照

一个全分辨率 RGBA16F 颜色目标，单采样、无 mip、单层，不计深度、对齐、暂缓释放和驱动保留。1920×1080 为 15.82 MiB，1280×720 为 7.03 MiB。自动缩放路径在较小视口下可能仍只有一张 15.82 MiB 的大图；显式尺寸路径在归还的高档图和当前低档图并存时，两个对象的理论载荷为 22.85 MiB。两个数字都不是实测峰值。

- 固定一个相机和一个同名测试目标，先跑高→低→高两次：低档一次停两帧，一次停至少六帧。记录视口、目标描述符、实际 `rt.width/height`、纹理实例 ID、归还帧／取回帧和实际创建次数。池命中可在短停留组看到旧高档 ID 回到活跃绑定；长停留组要核对 F+4 帧末以后是否已清退。实例 ID 不变时仍检查尺寸或原生分配事件，以排除 Core 在同一对象上重建。
- 另起同配置的 Player，确认 URP 初始化后把池容量设为 0，重跑相同切档。对比峰值、稳定低档的存活纹理、从低到高的创建次数及切档帧耗时；常规档位来回快、缓存确实命中才有复用收益。单独核对 Memory Profiler 快照和耗时，避免拍快照干扰计时。
- 若禁池后大图仍是当前绑定，回到参考尺寸分支；若另一个旧 ID 持续存在，查 Pass 字段、相机／历史缓冲、自建全局纹理和销毁路径。按各自的拥有者和有效时间判断是否重复，而不是按同名或进程总显存判断泄漏；若对象已清退但总显存不降，再查图形驱动和分配器的保留。持续低档确实需要回收时，优先在受控系统生命周期切换，核实缩容会重建哪些目标及升档成本；短时动态分辨率先保留正常复用。

**验证状态。** 已核对 Unity 公开文档、Core 与 URP 源码并复算载荷；未运行 Player、抓帧、拍内存快照或测真机。
