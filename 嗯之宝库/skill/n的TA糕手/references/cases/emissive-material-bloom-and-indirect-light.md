# TA-125 · 发光材质运行调亮后旁边墙面不变

**制作决定。** 材质调亮、光晕变大时，先锁曝光并关 Bloom，量不发光的漫反射墙面。若墙上光照预烘焙，运行时只调材质不会更新已有光照贴图；若要求可控的实时照墙，先确认目标平台有合适的实时 GI，或把发光外观和实际补光做成两个可一起驱动的控制项。

## 公开证据

- [Unity 6000.0 · Meta Pass](https://docs.unity3d.com/6000.0/Documentation/Manual/MetaPass.html) 说明光照系统可用不同于运行时表面的颜色和发光输入。[URP LitMetaPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl) 把 `surfaceData.emission` 写入 `metaInput.Emission`，供 Meta Pass 使用。[URP Lit 文档](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lit-shader.html) 将 Baked 限于预烘焙光照贴图，Realtime 则明确依赖启用 Enlighten 实时 GI；不能只看到材质选项就判定项目会更新间接光。自定义 Shader 的运行时发亮也不能证明它给烘焙输出了相同的发光值。
- [Epic UE 5.5 · Using the Emissive Material Input](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-the-emissive-material-input-in-unreal-engine?application_version=5.5) 的 CPU Lightmass 同场景三图：[关静态发光](https://dev.epicgames.com/community/api/documentation/image/12dd8443-ae12-4785-ba6b-2a66c49e6eed?resizing_type=fit&width=1920&height=1080)时箱体橙色条带可见，周围基本全黑；[开静态发光](https://dev.epicgames.com/community/api/documentation/image/c574d445-36c0-43d9-9bc4-28b58410de53?resizing_type=fit&width=1920&height=1080)后地面出现暗红受光；[Emissive Boost 为 2](https://dev.epicgames.com/community/api/documentation/image/8c887447-564d-401a-9226-75708d3abc72?resizing_type=fit&width=1920&height=1080)时橙红地面和右侧砖墙更清楚。图不是线性照度测量，不能从 Boost 2 推出屏幕像素两倍亮。原文也区分 GPU Lightmass：在该路径里无需同一个 CPU 烘焙开关。
- 同一 Epic 文档说明 Lumen 可让发光材质参与实时照明，小而过亮的发光体可能产生噪声。[UE 5.5 · Lumen Technical Details](https://dev.epicgames.com/documentation/en-us/unreal-engine/lumen-technical-details-in-unreal-engine?application_version=5.5) 的 “Small Emissive Meshes Do Not Light the Scene Consistently” 指出，小物件被 Lumen Scene 剔除时可能只剩屏幕追踪提取它们；针对这类不一致，可对照实例上的 `Emissive Light Source`。该选项处理场景纳入问题，不能保证消除所有噪声或更新滞后。

## 用法与最小对照

1. **先验墙上是否真的有光。** 新建一个有色发光面和一块哑光墙，关闭自动曝光、Bloom 和会混淆读数的反射，选墙上远离源轮廓的固定世界区域。它应在镜头内，且自身不发光。保持材质、相机和其他灯不变，对照发光强度两档；能抓到后处理前的颜色时读同一块的线性亮度和色偏。再单独开关 Bloom；如果只有合成后源边缘扩亮，不能以此宣称墙受到了间接光。
2. **区分烘焙与运行时控制。** 烘焙型分两组：同一份光照贴图下运行时切发光；再分别以低、高发光烘焙并比较墙面。只有单独烘焙才变，是选择固定烘焙或独立实时补光的依据。若高档烘焙也不变，检查发光面能否进入烘焙、材质的 GI 设置与 Meta Pass 发光输出，再检查接收墙和背面遮挡。
3. **验证实时路径的边界。** 让小发光体出画但保留同一块受光墙可见，或用工具切换 GI 的 Screen Traces，并留数帧观察稳定结果；若墙的照明只在源可见时出现，先核 Lumen Scene 是否包含发光体。可分别对照放大发光面积并下调亮度、启用实例的发光源设置、加受控的实时灯。受控灯更适合固定触发节奏或低端档位，但需看目标机照明预算、阴影形状及是否与已有 GI 重复叠加。把灯、材质自发光和 Bloom 的强度分别验收。

**验证状态。** 已读公共源码与官方文档，并看过 Epic 三张静态原图。未在本地烘焙或抓帧，未复测发光体出画时的 GI、回稳时间或真机成本。
