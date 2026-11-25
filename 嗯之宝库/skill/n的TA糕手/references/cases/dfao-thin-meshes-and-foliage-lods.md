# TA-80 · 距离场 AO 的薄面漏遮挡与边缘偏暗

**制作决定。** 薄板漏遮挡，先用同机位查网格距离场是否表示了板和关键镂空，再分开试补厚、提高单资产分辨率及改变 AO 最大距离。远景树只在 LOD 切换时变暗，则先对照 LOD 深度和原距离场；优先修资产的形状或深度，不先调全场景 AO。

## 证据

- **同模型的分辨率对照。** Epic UE 4.27 [Mesh Distance Fields：Quality](https://dev.epicgames.com/documentation/unreal-engine/mesh-distance-fields?application_version=4.27&lang=en-US#quality) 的三联图中，低分辨率仍看得到中间横杆，两端支架的精细外形却变钝；高分辨率图里，支架上缘的小凸起和下方转折更清楚。官方正文说明，有符号体素至少要有一个落在薄几何内部的负值样本，才能表示这个过零表面；调高分辨率会增加体积纹理内存。三联图是网格距离场可视化，没有给出薄板漏遮挡的 AO 对照，远离表面的大范围软遮挡也可能不受小误差影响。
- **同一场景的两级距离场。** [Distance Field Ambient Occlusion：Global Distance Fields](https://dev.epicgames.com/documentation/unreal-engine/distance-field-ambient-occlusion?application_version=4.27&lang=en-US#globaldistancefields) 的同机位滑块里，两图都能辨认建筑的大轮廓；全局图的窗框与画面中央支架比逐物体图粗，细轮廓更少。UE 4.27 文档说明靠近锥体起点用逐物体场，远段用随相机移动的低分辨率全局场。在同文 [Scene Representation](https://dev.epicgames.com/documentation/unreal-engine/distance-field-ambient-occlusion?application_version=4.27&lang=en-US#scenerepresentation)，官方说隔离 DFAO 的视图只响应 Sky Light 的 `Occlusion Max Distance`；不要用两张距离场图的灰度判断 AO 强度，也不要用亮度下限解释字段里消失的几何。
- **植被 LOD 的反例。** 同文 [Foliage](https://dev.epicgames.com/documentation/unreal-engine/distance-field-ambient-occlusion?application_version=4.27&lang=en-US#foliage) 的 No Pixel Depth Offset / Pixel Depth Offset 滑块里，无偏移时下方几棵树冠暗成块，有偏移时同一批树冠能看见更多枝叶明暗。作者解释，低模轮廓收进原网格距离场会造成过遮挡；A Boy and His Kite 的远景卡片用像素深度偏移来恢复较接近原几何的深度。同一节建议对需要的植被试用双面距离场，避免把有大量镂空的叶片束当成实心。该文的 SSAO-only / DFAO + 双面图同时改变两项，不能单独证明双面的收益；[网格距离场：Two-Sided Distance Field](https://dev.epicgames.com/documentation/unreal-engine/mesh-distance-fields?application_version=4.27&lang=en-US#two-sideddistancefield) 另明确给出双面追踪成本更高的取舍。

## 最小对照

1. **几何或体素。** 在同一静止机位，让一薄板遮住同一地面，固定光、曝光、缩放和遮挡距离，分别抓网格距离场、全局距离场和隔离 DFAO。只提高该资产的 `Distance Field Resolution Scale` 重建一组；另组保留原分辨率，只给薄板加封闭厚度。观察场里板和开孔、接收面实际遮挡及资产体积内存。厚度和高分辨率都不挽回的薄部件，先核对它是否参与距离场照明。
2. **遮挡范围。** 如果局部场已表示几何，保留同一资产，只改 `Occlusion Max Distance`，分别看近接触和较远的遮挡带；再按固定路径移动相机，对照全局图里细结构随相机距离是否先丢。范围改变只验证遮挡能走多远，不能补上没采到的薄面；全局图丢细节也不自动等于最终 AO 漏遮挡。
3. **接收面与 LOD。** 若只有低模或树卡变暗，锁机位单独强制高低 LOD，对照深度、距场轮廓和 AO；再试修低模外形，或仅在远景卡试深度偏移并复查边缘、交叉处和阴影。提高 Sky Light 的最低亮度可压住黑块，仍要检查其他物体是否一同变平。需要双面植被时，固定相同实例数和视野再量 GPU 成本。

**验证状态。** 已目视 UE 4.27 同模型分辨率、同场景两级距离场与树卡深度偏移三组公开对照；它们不是同一资产的一组完整三视图。没有本地薄板、动态相机、UE5 新管线或目标设备测试；以上排查步骤是待验证方案。距离场粒子碰撞见 [TA-56](gpu-particle-offscreen-collision.md)。
