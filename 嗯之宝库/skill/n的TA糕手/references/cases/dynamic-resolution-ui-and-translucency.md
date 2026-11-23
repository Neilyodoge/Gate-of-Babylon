# 动态分辨率：HUD、场景 UI 和透明特效分开验

**一句话结论**：降场景渲染比例后，先抓帧看模糊元素画到哪张图、发生在升采样前还是后。独立 Overlay HUD 可以保留输出分辨率；需要场景遮挡的名牌和透明特效则要检查场景采样、深度及运动历史。

## 公开实现和对照

- **URP 6000.0 Render Graph**：[UniversalRendererRenderGraph.cs](https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs)中，`m_RenderTransparentForwardPass.Render` 在相机的场景绘制阶段；当 `rendersOverlayUI` 为真且是最后一台 Base Camera，普通输出在最终解析之后调用 `m_DrawOverlayUIPass.RenderOverlay`，目标是 backbuffer。HDR 输出路径先把 Overlay UI 写到独立 `overlayUITexture`，在最终输出时合入；[DrawScreenSpaceUIPass.cs](https://raw.githubusercontent.com/Unity-Technologies/Graphics/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Passes/DrawScreenSpaceUIPass.cs)的 `RenderOffscreen` 用相机未缩放的像素宽高配置该纹理。此处说的是默认代码路径，自定义 Renderer Feature、相机栈、离屏输出和 XR 另抓帧确认。
- **画在哪，比跟不跟随物体更关键**：[Epic UE5.9 · Widget Components](https://dev.epicgames.com/documentation/en-us/unreal-engine/widget-components-in-unreal-engine)把 World widget 当场景网格绘制、可以被遮挡，Screen widget 则画在世界外、不会被场景几何遮挡。世界位置驱动一个 Overlay 标签和直接渲染世界网格两者看起来都能跟随目标；若为了清晰把场景网格标签改为 Overlay，先决定是否要补遮挡和深度排序。
- **透明模糊不只由比例决定**：[UE5.9 TSR · Outputting Translucency Velocities in Materials](https://dev.epicgames.com/documentation/en-us/unreal-engine/temporal-super-resolution-in-unreal-engine#outputtingtranslucencyvelocitiesinmaterials)同场景截图中，右图开启了 `Output Depth and Velocity`，太阳贴片的眼、嘴比左图清楚。原文说明深度和速度不会像颜色那样混合，只在满足透明度阈值时覆写；全开可能误导后面物体的重投影，特别是低透明度雾边。打开前后检查速度、深度可视化与背景拖影，不把这张图当成普遍开启的依据。
- **成本边界**：[UE5.9 TSR · Upscaling GPU Cost](https://dev.epicgames.com/documentation/en-us/unreal-engine/temporal-super-resolution-in-unreal-engine#upscalinggpucost)给出的 Valley of the Ancient 样例约为 100% 时 0.79 ms、50% 时 0.43 ms，指标是样例 TSR 开销。[Hidden GPU Costs](https://dev.epicgames.com/documentation/en-us/unreal-engine/temporal-super-resolution-in-unreal-engine#hiddengpucostsoftemporalupscaling)指出，默认无次级升采样时，TSR 后的色调映射和运动模糊等仍在显示分辨率运行。1080p 输出按轴缩到 50% 约为 960×540，场景栅格像素只有 25%；这不等于整帧、透明历史或 HUD 成本只剩 25%。

## 怎么验收

1. 固定输出分辨率、曝光、相机和素材；同时放同样的细字 HUD、要穿过遮挡物的场景名牌和一条带柔边的移动粒子。以 100% 和 50% 各抓一帧，标出场景颜色、透明绘制、升采样、HUD 与最终合成的目标尺寸，HDR 再记独立 UI 纹理。若 HUD 已在最终目标画仍模糊，单测 UI 自身的源纹理/字体图集、离屏目标和布局缩放。
2. 先分别让两档比例稳定，再重复 100%→50%→100% 的切换；录相同镜头轨迹中的静止图案、横移、遮挡显露和投影标签跨像素。固定比例也抖时，先隔离粒子自身边缘、采样或素材；只有切换时变糊或留历史，逐项对照空间/时域升采样、动矢量和深度。小 UI 若必须被墙挡住，可保留世界路径并调字号与边缘宽度；允许只贴屏时才试 HUD 路径。透明核心必要时单测写速度，不能用强写整片透明背景换一处锐度。
3. 录场景、透明、升采样、输出后处理和 UI 各段的 GPU 时间，再看整帧。分开看稳定档位、比例变化的峰值和成像恢复帧数；若为了 UI 或特效额外引入一张输出尺寸 RT，记分配、合成和带宽，别把变化都归到分辨率比例上。

**状态**：已核对 URP 源码、Epic 透明贴片静态原图与官方样例计时；未做同场景 HUD/名牌降比例 A/B、动态切换序列或真机复现。
