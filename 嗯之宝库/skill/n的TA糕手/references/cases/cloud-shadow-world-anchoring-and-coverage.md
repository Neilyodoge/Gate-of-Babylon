# TA-94 · 云影的世界坐标与覆盖边界

**制作决定。** 冻结云动画和太阳后平移镜头。覆盖区内同一个地标的暗斑整体跟着镜头走，先核对云的取样坐标、阴影图生成和读取是否用了相同的投影原点与坐标参考；暗斑在区内大体不动、只在远端出现一道跟镜头走的界线，先看覆盖范围和图外回退。

## 公开证据与边界

- **覆盖窗口可以跟着相机。** UE 5.5 的 [Volumetric Cloud 文档](https://dev.epicgames.com/documentation/en-us/unreal-engine/volumetric-cloud-component-in-unreal-engine?application_version=5.5#directionallightinteractionsandshadowing) 说明 `Cloud Shadow Extent` 控制围绕相机的云阴影图半径，缩小范围可让有限分辨率集中在更小区域；`Cloud Shadow Map Resolution Scale` 则改变图的分辨率和开销。两项能做独立对照；此处读了正文，未将开关云影的示意图当作移动或范围的实测。
- **生成和读取使用对应的基底。** Unity HDRP `6000.0/staging` 的 [EvaluateShadowRegionData](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/VolumetricClouds/HDRenderPipeline.VolumetricCloudsShadows.cs) 在主光坐标中包住相机及截到阴影距离的视锥角点。[生成 Compute](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/VolumetricClouds/VolumetricCloudsTraceShadows.compute) 由贴图 UV、原点与两轴还原行星空间的光线起点；[接收端 HLSL](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/VolumetricCloudsShadowSampling.hlsl) 用接收点减原点，投到同一组轴，再除以投影尺寸找回 UV，图外用独立回退值。覆盖窗口重定位不等于云图样跟相机走；这份生成代码另有按相机距离淡化密度的步骤，冻结时间不能保证远处阴影强度完全不变。
- **实际的远景裁切修复。** Unity [提交 5ae6eac](https://github.com/Unity-Technologies/Graphics/commit/5ae6eacde9af3) 记录相机离几何较远时的云影裁切，范围计算改用 `shadowDistance / cos(FOV / 2)` 再限在原视锥角点长度内。只增贴图分辨率不会修正视锥角点覆盖不足；这个补丁也不表示每种远端硬边都能靠加范围解决。

## 最小对照

1. 关云动画、锁太阳、固定分辨率，在地面放几个世界位置不动的高对比地标。只平移相机，先挑始终留在图内的点，分别记录阴影值、捕获原点、范围与时间参数。简化成一维：跨度 100 m，原点从 0 移到 10 m，世界 30 m 对应的 UV 从 0.30 变 0.20；若还用 0.30 去读新图，取到的是世界 40 m。图与矩阵要一起更新，使用相机相对坐标时先核对两端的参照。若只有缓慢的纹素爬动，再检查采样网格、过滤或时序处理，参见 [TA-31](directional-shadow-cascade-transitions.md)。
2. 再让一个地标越过有效边界，画出覆盖轮廓，并排看图内阴影和图外回退。图内稳定但边界跳变时，可先在覆盖内做窄过渡，混到符合远景光照的回退值；远景必须有大形状时再试较粗的世界空间云影。若先改范围，固定分辨率复核清晰度：光空间一轴覆盖 512 m、256 纹素是 2 m/纹素；扩到 1024 m 就是 4 m/纹素，地面的实际投影尺寸还受太阳角度影响。分别记录远端接缝、近景清晰度和生成／采样 GPU 时间。

**验证状态。** 已核对 Epic 正文、HDRP 生成和读取源码与裁切补丁，坐标及纹素值为算例；未取得冻结云动画后移动镜头的公开 A/B，未做本地引擎或真机验证。
