# TA-66 · 胶囊阴影在脚边与墙角的覆盖

**制作决定。** 后墙出现跟角色移动的软黑影时，先只关闭间接胶囊阴影，确认它是否和脚底阴影同源。位置合理但太黑，先调最低可见度；投得太远，查最大投射距离与间接光方向；紧贴墙也有异常黑块，则对照专用阴影胶囊的形状和位置。

## 证据

- **同一镜头原图。** [UE 5.5 Capsule Shadows Overview](https://dev.epicgames.com/documentation/unreal-engine/capsule-shadows-overview-in-unreal-engine?application_version=5.5#capsule-indirect-shadows)的[开启](https://dev.epicgames.com/community/api/documentation/image/ef1726fe-d6e6-45dd-baa5-d89c0cbc4e3b?resizing_type=fit&width=1920&height=1080)／[关闭](https://dev.epicgames.com/community/api/documentation/image/e8e89c72-73f2-45f2-a7f9-cff059acd5b6?resizing_type=fit&width=1920&height=1080)两图中，开启后两脚旁的影沿地面伸到墙根，后方墙上也多了一条软暗影；关闭后二者一同消失。墙面变暗本身可能就是预期的间接遮蔽，原图没有展示隔墙穿透。Epic 文档说明，间接路径用骨骼上的简化胶囊表示遮蔽物，由 Lightmass 的体积光照样本给软影方向；门洞两侧可随样本插值改变强度和方向。
- **强度、半影和距离是独立的控制。** 官方 [Capsule Indirect Shadow Min Visibility](https://dev.epicgames.com/documentation/unreal-engine/capsule-shadows-overview-in-unreal-engine?application_version=5.5#indirect-minimum-shadow-visibility)限制间接软影最暗能到什么程度，可以减轻自阴影；提高它不会缩短计算的投射距离。UE 5.5 [控制台变量参考](https://dev.epicgames.com/documentation/unreal-engine/unreal-engine-console-variables-reference?application_version=5.5)把 `r.CapsuleMaxIndirectOcclusionDistance`（表中为 200）定义为间接最大投射距离，另有独立的直接阴影距离；`r.CapsuleMinSkyAngle` 是使用预计算无遮挡天空向量时的源角下限，不是距离。[PixelAnt 的制作说明](https://pixelantgames.com/blog/unreal-engine-optimization-capsule-shadows/)也将软度、间接投射距离和最低可见度分开调，建议独立记录 Capsule Shadows 的 GPU 时间。文章给的人形距离只是试调起点，不应当成通用值。
- **成本与适用范围。** Epic 的 [Performance／Limitations](https://dev.epicgames.com/documentation/unreal-engine/capsule-shadows-overview-in-unreal-engine?application_version=5.5#performance)说明，半分辨率计算后做深度感知上采样，并按屏幕瓦片剔除，其公开旧设备例子为 1080p、Radeon 7870、每角色 10 个胶囊，首个角色 0.29 ms，额外角色约 0.05 ms／个。该页也列出形状近似导致自阴影、极软时硬边，以及与 Virtual Shadow Maps 的兼容限制。不要根据旧计时或直接阴影示例推算当前项目的兼容性和帧时；先验证实际启用的渲染路径。

## 用法与验证

1. 在有预计算间接光的目标场景，固定角色姿势、曝光和相机，分别关直接胶囊阴影、关间接胶囊阴影并抓帧，记录脚底、墙根和角色自身的颜色变化；确认同画面的常规投影或屏幕空间接触阴影没有一起变化。然后单独比较不同离墙距离，并走过门口重复一次，检查光照采样方向变化是否和墙影一起移动。
2. 先保持阴影胶囊和光照不变，只缩短间接距离，观察远墙的范围和脚底是否还能站住。近墙和脚底若都在同一有效范围内，缩全局距离可能同时伤到二者；回到专用 Shadow Physics Asset，逐组对照腿、脚、躯干胶囊，再看近墙的暗影是否合理。位置正确但太黑时，用最低可见度做下一组对照；不要拿变淡的影证明覆盖范围已经缩小。
3. 用真实人数和镜头量覆盖面积、胶囊数量以及半分辨率上采样的边缘稳定性。保留接地需要的形状；对不适用的路径，按项目已用的直接阴影、接触阴影或贴地假影另做同构图比较。

**验证状态。** 已目视 Epic 同镜头开关图，核对 UE 5.5 文档及 PixelAnt 原文。没有墙面误影的本地单变量、运动或真机对照。
