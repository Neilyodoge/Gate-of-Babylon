# 烘焙光照接缝与漏光：先沿缺陷找到边界

**制作决定**：接缝沿同一个物体的光照 UV 岛边时，先试缝合和岛间距；沿不同模块的对象边界时，缝合开关跨不过对象；亮线贴着穿进地面的道具时，先查几何相交。提高全场光照贴图分辨率会同时改变纹素密度和间距，留到定位原因以后再评估。

## 原始证据

核对日期：2026-11-23。

- **同一物体的 UV 岛边**：[Kristijonas Jalnionis，Lightmap UV seams，图 20](https://jalnionis.net/post/783809677267976192/chapter-10-lightmap-uv-seams)。左侧手背到手指上方有一条明显的明暗边，右侧过渡更连贯。作者说明右图开启 `Stitch Seams` 后重新烘焙；该选项只处理同一 GameObject 中可匹配的共边，不能当作跨对象缝合。
- **模块交界**：同一文章图 21 的左图顶面和绿墙上可见随板件排列的细线，右图连续得多。作者把右图标为在建模工具中合并网格再导出，同时说明高采样且关闭过滤也可能达到类似外观。图中没有单独控制这两个因素的逐项对照，不能据此量化合并或关闭过滤的独立贡献。作者还区分用于隔开模块光照 UV 区块的 `Lightmap Padding` 与同一网格内部的 UV 岛间距；具体参数名称和行为按所用版本确认。
- **几何相交与过滤**：[Unity 6000.7 Beta，Troubleshooting light leaking → Adjust scene geometry](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/baking-before-runtime/troubleshooting/troubleshooting-lightmapping-leaking#adjust-scene-geometry)。三联图左侧道具底缘有亮白线和零散亮点，中间亮点少了但底缘仍有残留，右侧异常亮线不再明显。官方图注说明左图有较强高斯过滤和不良道具摆位，中间换 Open Image Denoiser 只减轻漏光，右图移动道具 Y 坐标避开相交才消除该问题。这个例子支持先排查相交，不能推出所有漏光都靠移动道具解决。
- **近景正确也可能在远景串色**：[同页 Adjust lightmap UVs and resolution](https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/baking-before-runtime/troubleshooting/troubleshooting-lightmapping-leaking#adjust-lightmap-uvs-and-resolution)指出，镜头拉远采样到较低 mip 时仍可能漏光，建议检查 `UV Overlap`、岛间纹素距离与光照贴图分辨率；自动生成 UV 时可调整 `Min Lightmap Resolution` 和 `Min Object Scale`，或对照手动 `Pack Margin`。这是文档给出的诊断途径，未在项目验证对应数值。

## 最小对照

| 缺陷位置 | 先做的对照 | 下一步 |
| --- | --- | --- |
| 同一物体上，线跟着光照 UV 岛边 | 固定光照、相机和烘焙设置，只切 `Stitch Seams`。查 `UV Overlap` 或烘焙图；若像相邻岛串色，固定分辨率单测 UV 岛间距，近看和拉远各验一次。 | 记录真正起效的设置和 UV 条件；若仍缺纹素覆盖，再单测提高该物体的光照贴图尺寸。 |
| 线沿多个模块的接缝 | 保留原模块做基线。只对问题区域做一个焊点、重新展开光照 UV 的合并版本；另在原模块版单测过滤/去噪改动。关过滤后重新提高采样是下一组对照。 | 合并前看模块改动成本、剔除粒度和光照图集使用；比较残余接缝、噪点和烘焙时间。 |
| 亮边沿道具底部、墙的穿插处 | 检查几何相交和墙面厚度；保留烘焙设置只修相交，再保留原几何只换去噪或过滤。 | 如果只有换过滤能减淡亮线，先定位污染来源再决定是否保留该设置。 |

**未验证**：未取得作者工程，没有在本地烘焙，未测运行时各 mip、图集增量或目标设备成本。
