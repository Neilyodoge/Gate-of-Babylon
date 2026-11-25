# URP Render Graph 同帧全局遮罩为空：发布与依赖要分别声明

**TA-212 · 制作决定。** 切换到 Render Graph 后自定义遮罩读空，先按同一次图录制检查生产 Pass、发布顺序和消费声明。写入遮罩的 Pass 用正确附件或资源写声明，之后调用 `SetGlobalTextureAfterPass`；需要在后续自定义 Pass 的 Shader 里用这个槽位时，在消费 Pass 用 `UseGlobalTexture(id, AccessFlags.Read)`。若只是两个自定义 Pass 要交接纹理，不需要材质通过全局名字访问，可用同图帧数据传递 `TextureHandle` 并用 `UseTexture` 声明。这样依赖可以精确到一张图。

## 公开证据：三个时点

- **录制**：[Unity 6000.0 · Create a texture as a global texture](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph/working-with-textures/pass-textures-between-passes/create-global-texture) 给出发布和读取的两侧示例，提醒不要用 Unsafe Pass 的 `CommandBuffer.SetGlobal` 代替。[RenderGraphBuilders.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/RenderGraphBuilders.cs) 在构建器结束时把发布记录加入图内槽位；`UseGlobalTexture` 解析当时的槽位并调用 `UseTexture`，没有前面的发布会抛 `ArgumentException`。
- **执行**：[IRenderGraphBuilder.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/IRenderGraphBuilder.cs) 明确全局值到 Pass 结束才更新；同一 Pass 访问那个全局槽仍看旧值。发布调用本身不构成纹理使用依赖；只有后续 Pass 声明读取，图才知道哪张纹理确实要被消费。只写一张没有显式读者的临时纹理时，查图上的实际剔除与生命周期。局部试用 `AllowPassCulling(false)` 可定位生产 Pass 是否被剔除，不能补缺少的读依赖；`AllowGlobalStateModification(true)` 还会禁用剔除并引入图的同步点。
- **图边界**：[RenderGraph.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/RenderGraph.cs) 的 `ClearGlobalBindings` 在该分支把已注册全局纹理复绑到默认黑纹理，代码同时指出同一帧可能有多次图录制与执行。跨执行要在消费图重新导入由外部持有的纹理，并重新声明依赖；参见 [历史贴图的寿命](render-graph-history-texture-lifetime.md)。[URP DrawObjectsPass.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/DrawObjectsPass.cs) 自己调用 `UseAllGlobalTextures(true)`，因此同一材质挂在场景物件上能读到，换进自定义全屏 Pass 仍可能缺依赖。只需一张图时不要用读全部来兜底。

## 两个 Pass 的最小对照

固定单台相机和注入顺序。A 写临时单通道遮罩，分别按帧输出 0.25、0.75；B 用它输出相机颜色。先检查 B 本身与相机输出有依赖，再按表处理全局槽。测试图没有其他读者，没有覆盖同名槽位或跨相机复用。

| 单独改变的条件 | 预期和排查点 |
| --- | --- |
| A 不发布，B 显式 `UseGlobalTexture` | 首次录制缺槽位就报错，先查相机过滤与 Pass 顺序 |
| A 发布，B 的 Shader 采样但未声明 | 图看不见这条边；在 Render Graph Viewer 查 A 是否被剔除，查实际读到的是旧值、默认值还是临时碰巧一致 |
| A 发布，B 显式读取并输出 | 图中应有 A→B 依赖；用 Frame Debugger 确认 A 执行后绑定，B 每次读到对应帧的值 |

同图通过后才加入第二台相机，确认是真正的跨执行还是仅在一个图里换了 Pass；不要凭相同 Unity 帧号当它们共享绑定。诊断时同时记 Pass 是否执行、消费时实际句柄和取样值。用固定灰阶定位管线后，再换回真实罩图查格式、UV 和相机尺寸。

**状态**：核对了 Unity 6000.0 手册、Core Render Graph 和 URP 源码。两 Pass 结果是按源码写的对照计划，未在 Unity 录图、抓帧或真机验证。
