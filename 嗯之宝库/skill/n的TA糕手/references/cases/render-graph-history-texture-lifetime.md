# TA-140 · 临时 Render Graph 贴图跨帧读取异常

**制作决定。** 需要上一帧时，保留拥有实际纹理的 `RTHandle`，在每一次 Render Graph 录制时重新导入。`TextureHandle` 只标识本次图中的资源，缓存它不能延长纹理寿命。同一相机本帧要读上一帧并写新历史时，用两个缓冲，再把“句柄可用”“上一份内容已写好”“内容来自同一相机与描述符”分别检查。

## 公开证据

- [Unity Graphics 6000.0 · TextureHandle](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/RenderGraph/RenderGraphResourceTexture.cs#L27-L45) 写明句柄只属于一次 record＋execute，执行后全部失效；仅被剔除 Pass 引用的临时纹理甚至可能没分配。即使由 `ImportTexture` 获得的 `TextureHandle` 也不能留到下一次；跨帧活着的是外部资源。按 [Unity · Render Graph 导入纹理](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-import-a-texture.html) 用外部 `RTHandle` 再取新的图内句柄，释放责任也在生命周期所有者。
- [Unity 6000.0 · URP 读取往帧](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-get-previous-frames.html) 的路径是 `RequestAccess<RawColorHistory>`、`GetHistoryForRead`、`GetPreviousTexture`、`ImportTexture`。[UniversalCameraHistory](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalCameraHistory.cs#L73-L89) 在历史项未处于有效写入版本时返回空。[URP 的写入处](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L715-L741) 仅在请求存在且相机颜色有效时复制到导入的历史目标，注释指原始颜色历史在 TAA、后处理和 Overlay UI 之前；要记录特定后处理的输出，需要选择对应的历史生产者和注入点。
- [RawColorHistory](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/History/RawColorHistory.cs#L66-L120) 分配两个历史 RT，在派生描述符改变时重建，且把历史 MSAA 设为 1；XR multipass 可另分配第二眼的双缓冲。[相机渲染更新](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L345-L363) 收集请求、清理未用历史并交换 RTHandle。双缓冲是在不同帧读旧、写新；只有自定义效果实际提交写入后，下一次才能消费其内容。

## 算例与最小对照

按同一相机的连续渲染次数计数：第 0 次没有历史，取当前画面并写 A；第 1 次读 A、写 B；第 2 次读 B、写 A。若第 3 次变更描述符且不做旧历史重采样，则忽略旧内容，重新从当前画面建立历史。另一相机即使同一 Unity 帧号也用自己的版本和缓冲。1920×1080 的 RGBA16F 颜色，每像素 8 字节，CPU 复算两个单采样、非 mip 目标约为 31.64 MiB；两台同尺寸相机各用一套约 63.28 MiB。未计对齐、平台分配或 XR。

1. 让生产 Pass 向历史写入相机 ID、该相机渲染序号和尺寸标记。把上一帧的标记显示在同一视图，检查第一个有效历史、双相机交替、停更后恢复和 resize。没有历史时测试明确的退化路径，例如使用当前帧；新分配非空也不代表已经写过。
2. 每次注册实际历史读和写依赖：读上一份用 `UseTexture(..., AccessFlags.Read)`，新目标用符合访问方式的写声明；捕获验证生产 Pass 确实执行、读取的对象是上一次提交的目标。禁用剔除可以帮助找没执行的 Pass，但不会延长临时句柄的有效期。若自定义队列或外部命令绕过图依赖，另验栅栏和实际写入完成时机。
3. 在释放资源、相机销毁或描述符变化时检查外部所有者的清理。在记忆两帧已经够用的情况下，不保留每一帧拷贝；额外相机、眼位和高分辨率历史按真实分配核算内存。

**验证状态。** 已核对 Unity 6000.0 文档、Core 和 URP 源码，并复算双缓冲字节；未在 Unity 运行跨帧或双相机抓帧，未验证真机内存与同步行为。
