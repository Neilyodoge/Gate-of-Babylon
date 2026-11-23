# 移动端蒙版植被：先确认深度预通道真的变了

**一句话结论**：做植被 A/B 时先捕获实际帧，确认是哪一个 Pass 在写深度、叶片是否参与、颜色 Pass 是否使用这些深度；然后检查镂空和风动一致，最后看完整 GPU 帧时。仅切 Depth Priming 不能证明开关组分别有、无深度预通道。

## 能从原始证据确认什么

- **Unity URP 6000.0/staging 源码**：[UniversalRenderer 初始化](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L269-L273)把 Android、iOS、tvOS 和 Embedded Linux 的 Depth Priming 推荐值设为 false，所以该版本这些平台的 `Auto` 不会启用它。[实际判定](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L571-L602)表明 `Forced` 仍需通过 Forward/Forward+、初次写入深度的相机（Base 或清深度）、没有 MSAA 等条件。[同文件的预通道判定](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L829-L837)还接受其他深度需求；[DrawObjectsPass](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/DrawObjectsPass.cs#L213-L228)在满足条件的 Priming 不透明绘制中关闭深度写入并用 `Equal`。这些是该版本源码行为，测试时还要看项目所用包和渲染路径。
- **裁剪与顶点路径**：URP Lit 的 [DepthOnly 变体](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader#L310-L350)启用 `_ALPHATEST_ON` 时，[DepthOnlyPass](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl#L26-L53)用同一张 Base Map、Base Color 和 Cutoff 做 Alpha 裁剪，也支持 LOD 淡化；[颜色 Pass 的 LitInput](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl#L250-L255)使用相同的 Alpha 输入。标准 DepthOnly 直接变换输入位置；自定义植被若只在颜色 Pass 加风动，深度和颜色的位置会不匹配。[MJP 的强制提前写深度对照原图](https://therealmjp.github.io/images/early-z/forceearlyz-discard.png)另示范了把深度写在 Alpha discard 之前会错误挡住叶片空洞后方；它是正确性反例，不是 URP 采样或移动端计时。
- **公开设备资料的局限**：[Arm 的 Immortalis-G925 数据](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/immortalis-g925-the-fragment-prepass)在内部定频平台开关的是硬件 Fragment Pre-pass，Fortnite 山区或城镇镜头的总体 GPU cycles 减少 5.4%；作者还说明 Late-Z 蒙版的覆盖判断可能重复执行。[Procedural Pixels 的 Mali 捕获](https://www.proceduralpixels.com/blog/how-to-properly-profile-mobile-gpu-using-arm-streamline#frame-passes)把应用的深度预通道与前向颜色合计估为 18.6 ms，片元工作在 tile 上合并，未给关闭组。两条数据都不能当作引擎植被预通道的净收益。

## 怎么测才知道该不该开

1. **验开关**：同机、同图形 API、同渲染器、同镜头；固定 MSAA、画质、分辨率、LOD、阴影、风动相位和摄像机设置。用帧捕获核对两组实际出现的 DepthOnly、DepthNormals 或 Copy Depth，不透明颜色绘制是否改用 `Equal`，以及目标叶片是否被画进预通道。两组都有预通道时，只能把结果报告为「Depth Priming 开/关」；要回答「是否值得加一次深度绘制」，另做对照并保持各组消费者所需深度等价。
2. **验画面**：选含前后交叠叶片和明显空洞的镜头，检查预通道深度及最终颜色；再在风动顶点变化大、LOD 交叉淡化时逐帧对照。核对预通道和颜色 Pass 的 Alpha 贴图与 UV、Cutoff、LOD 淡化、剔除和顶点位置。空洞被堵或 `Equal` 导致有色像素被剔掉时，先修覆盖，不比较性能。
3. **验净收益**：各测一段密草近景和一段稀疏远景，热稳定后交替采样，记录 GPU 帧时中位数和高分位、受影响 Pass 计时和可用计数器（顶点或图元工作、片元工作、外部带宽）。预通道新增绘制、顶点及 Alpha 判断，颜色 Pass 才可能因遮挡少做昂贵片元工作。若 tile 把片元计时合并，报告可见的小计和完整 GPU 帧时，不从合计反推单个 Pass 的耗时。只有画面等价、目标设备在关键镜头有稳定净收益才考虑开启。

**状态**：已查源码、公开设备数据及植被正确性反例；未做本地复现或真机测试，未找到相同植被场景在移动设备上开关应用层预通道的原始计时。后续拿到可信 A/B 再补设备、管线和场景条件。
