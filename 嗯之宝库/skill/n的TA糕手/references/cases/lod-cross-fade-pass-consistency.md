# LOD 交叉淡化：颜色、深度和阴影要一起验收

**制作决定**：先用同一镜头看硬切是否暴露跳变；需要淡化时，对自定义 Shader 分别验收实际使用的颜色、深度和投影通道。只修颜色画面可能留下不匹配的深度或投影。对常停在阈值附近的镜头，追加时间淡化与距离带的对照，比较抖点停留和两个 LOD 同时绘制的代价。

## 原始证据

核对日期：2026-11-23。

- **实际失败与修复**：[Jasper Flick《LOD and Reflections》§1.3–1.4](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/#1.3)，Custom SRP，教程由 Unity 2019.2.21f1 更新到 2022.3.5f1。作者先用粗条纹调试 `ClipLOD`：进入的 LOD 收到负号的淡化因子，最初使用同一减法导致它全部被裁掉。[失败原图](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-half.png)的前景球只剩稀疏的黄条，并露出条纹内的背景；[修复原图](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/striped-lod-complete.png)里，青色一组条纹补齐了前景球体。作者按淡化因子正负对两级取互补阈值，并把裁剪同时放入颜色和 `ShadowCaster`。换成[细抖动后的原图](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/lod-groups/dithered-lod.png)仍能看到密集点纹。是教程中的调试场景，无法据此断言不同形状的 LOD 无轮廓洞。
- **实际查看的运动片段**：[同文 §1.5 Animated Cross-Fading](https://catlikecoding.com/unity/tutorials/custom-srp/lod-and-reflections/#1.5)。静音抽看 0 秒、约 2.41 秒和约 2.55 秒；镜头拉远后，左侧小球从较密的红色点纹变成很淡的点纹。作者用动画淡化避免停在距离带时抖点和不稳的阴影持续出现；启用后按时间跨过，不再用距离淡化带宽度。没有对同一相机轨迹下的硬切做逐帧 A/B。
- **官方实现怎样接通各通道**：[Unity Graphics 仓库的 URP Lit.shader](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader)在读取时的 `master` 为 ForwardLit、GBuffer、ShadowCaster、DepthOnly 和 DepthNormals 等各声明 `LOD_FADE_CROSSFADE` 变体。[DepthOnlyPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl#L66-L81)和 [ShadowCasterPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl#L63-L74)的片元函数均实际调用 `LODFadeCrossFade`。这些是源码事实，项目所用 URP 版本、启用哪些 pass、Player 是否保留相应变体要另验。

## 最小对照

1. 选轮廓差异明显的资产，固定 LOD 阈值、相机轨迹、光源、分辨率和抗锯齿。分别做无淡化、距离带淡化和时间淡化；各拍穿过阈值、停在阈值附近和小幅来回三段。先把不同 LOD 临时着不同颜色，确认相邻两级都参与以及是否露底，再用实际材质看远景点纹和轮廓。
2. 在过渡前、过渡中、结束后各抓 Frame Debugger 或 GPU 捕获。对照相机颜色、项目确实使用的深度相关 pass 和阴影，不要求不同视角的像素掩码一模一样，但要确认都随同一过渡状态变化。若只有颜色淡化，先查缺了哪个 pass 的变体或裁剪；如果只是远端轮廓差别太大，先比较调整网格或复用主体，只渐隐细节。作者的叠层方块例中，同一 Renderer 出现在相邻两级时不会与自身淡化。
3. 两级不同 Renderer 的过渡帧会同时送入绘制；按 pass 记 draws、顶点量、GPU 时间，并统计代表性镜头内有多少资产同时进入过渡。距离带加宽可能延长重复工作，时间淡化在集中跨阈值时也可能有峰值。只把这些写为待测假设；不以 draw 翻倍推定帧耗时翻倍。

**未验证**：未运行 Unity 样例或项目，没有相机相同的硬切 A/B，没有真机的深度、阴影成本及过渡峰值数据。
