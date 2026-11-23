# TA-53 · 可变速率着色：画质与 GPU 收益

2026-11-23 · 公开案例研读；未在本地引擎或目标设备复现。

## 问题与结论

大面积地形和水面是降着色率的候选，但同一次绘制里仍可能有细浪、岸线或高光。若整批 2×2 会损失这些细节，先试让低对比区域继续用 2×2、只把敏感区域保在 1×1。是否值得开启，最终看实际生效速率、画面和整帧 GPU 时间；三项分别验证。

## 公开证据

- **Firaxis / DirectX 团队，2019-03-18。** [原文 “What does this actually look like in practice?”](https://devblogs.microsoft.com/directx/variable-rate-shading-a-scalpel-in-a-world-of-sledgehammers/)：RTX 2060、4K、同地图同缩放，关闭时约 53 FPS。Tier 1 将地形和水面设为 2×2，车辆、建筑和 UI 保持 1×1，作者报告约 +20% FPS。Tier 2 用边缘检测生成屏幕速率图，作者报告同场景 +14% FPS。这是该镜头的帧率，原文未给逐通道 GPU 耗时。
- **画面观察。** [Tier 1 原图](https://devblogs.microsoft.com/directx/wp-content/uploads/sites/42/2019/03/Tier1-Default-side-by-side-no-labels.png)右侧开启；[Tier 2 原图](https://devblogs.microsoft.com/directx/wp-content/uploads/sites/42/2019/03/Tier2-Default-side-by-side.png)左侧开启。对齐同一画面的右下黄色半岛，在 Tier 1 开启图上，半岛右侧海面的部分浅色细浪比关闭的原图淡，岸边轮廓仍可读。[Tier 2 速率可视化](https://devblogs.microsoft.com/directx/wp-content/uploads/sites/42/2019/03/VRS-Tier-2-ssimage.png)里的红色是设为 1×1 的区域，蓝色是设为 2×2 的区域；开阔海面多蓝，海岸和地格边界多红。该图显示指定速率分布，没有提供逐 shader invocation 的实际速率或连续帧。
- **Chris Anderson，UE 5.4 公开近似测量，2024-05-15。** [“Some Approximate Numbers”](https://blog.rime.red/variable-rate-shading-in-unreal-5/#some-approximate-numbers)：Valley of the Ancients，3840×2160、TSR ScreenPercentage 100%，RTX 3090 Ti 限功耗 225 W。作者表中关闭约 22.8 ms，Conservative CS 约 21.9 ms，全局 2×2 配置约 21.1 ms；保守方案约省 0.9 ms。已查看该镜头的 [Conservative CS 预览](https://blog.rime.red/content/images/size/w2000/format/jpg/2024/05/HighresScreenshot00008.png)，覆盖随地形和边界变化；图片未提供颜色到速率的图例。作者在 “VRS in Unreal” 另报告激进的 Full 模式在 Nanite 几何上有伪影；该描述不当作表中全局 2×2 的逐像素质量对照。计时没有提供逐通道瓶颈或稳定区间。
- **速率与成本边界。** [Microsoft：查询 SV_ShadingRate 和 Depth and stencil](https://learn.microsoft.com/en-us/windows/win32/direct3d12/vrs#querying-shading-rate-by-using-sv_shadingrate)说明 D3D12 Tier 2 可在兼容的像素着色器中读硬件选定速率，深度、模板和覆盖仍按完整采样分辨率计算。 [AMD FidelityFX VS 1.2：Technical details](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/variable-shading/#technical-details)说明硬件粗着色不减少渲染目标写入的像素数，小三角形或填充受限通道的收益可能有限，通常也帮不到无昂贵像素着色器的深度通道。

## 怎么用与怎么验

1. 固定镜头、分辨率、TSR、材质与光源，比较关闭、整批 2×2、按区域保护细节三组。把速率图当作输入；再对照通道绑定和组合器，D3D12 Tier 2 在兼容的目标像素着色器用诊断版显示 SV_ShadingRate；Nanite 软件路径另看其调试视图。这些诊断单独抓图，正式计时关闭调试绘制。
2. 分别看停帧与慢移镜头，重点对齐细浪、贴花、法线高光和遮罩边缘。仅在同一绘制的某些区域出问题时，优先只保护那些区域；平台若只有按绘制设置，改为保护整批或只对可接受的材质开启。
3. 对照目标着色通道、速率图生成和整帧 GPU 时间；条件允许时再看像素着色调用、渲染目标流量及深度、阴影等独立通道。没真的降速先查设置是否生效；降了速而目标通道没明显变快，查目标通道是否受写入或几何限制；目标通道变快但整帧没动，再查速率图开销、其他通道和同步。两份公开案例都没有提供足以判定剩余 GPU 瓶颈的逐通道数据，这三种情况仍待目标设备 A/B 验证。
