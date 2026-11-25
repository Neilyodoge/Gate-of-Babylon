# TA-91 · 叠加场景的光照贴图索引与区域映射

**制作决定。** 两个 Unity 场景单独正常、叠加载入后某些网格像拿错了烘焙结果时，先查 `LightmapSettings.lightmaps[renderer.lightmapIndex]` 实际指向哪份贴图数据；确认是正确的图，再看 `lightmapScaleOffset` 和网格的光照 UV 取的是哪块。仅凭索引数字变了不能判断错图；同一数字在不同数组里也未必指向同一张图。

## 公开证据与边界

- **两层映射。** Unity 6000.0 的 [LightmapSettings.lightmaps](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/lightmapsettings/lightmaps) 和 [Renderer.lightmapIndex](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/renderer/lightmapindex) 说明索引指向当前光照贴图数组，`-1` 表示未分配；[Renderer.lightmapScaleOffset](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/renderer/lightmapscaleoffset) 定义 xy 为缩放、zw 为偏移。[URP 6000.0/staging 的 Lighting.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl) 在 `OUTPUT_LIGHTMAP_UV` 中直接用 `uv * scale + offset`。这能确认渲染用的区域变换，不能据此推断 Unity 原生加载器会按何种顺序分配槽。
- **共同烘焙仍按场景加载。** Unity 6000.0 [多场景烘焙说明](https://docs.unity.com/en-us/engine/6000.0/manual/working-with-scenes/multi-scene-editing/bakemultiplescenes#bake-lightmaps-with-multiple-scenes) 说阴影和 GI 反弹可跨参与烘焙的场景计算，但光照贴图和实时 GI 数据按场景独立装卸；一起烘焙的光照探针是另一条路径。不要为了修运行时错索引，先把独立烘焙的场景改成一起烘焙。
- **外观相似的反例。** Unity 官方 [UUM-127288](https://issuetracker.unity.com/issues/11724/baked-shadows-are-missing-when-no-scene-contains-both-shadowcasting-mixed-lights-and-baked-lighting-data) 的 Player 复现中，叠加载入后立方体缺阴影；问题条件是没有场景同时包含投影的混合光与烘焙光照数据。报告列出 6000.0.62f1 可复现、6000.0.74f1 含修复。这是阴影路径的反例，页面没有报告读错贴图槽或 UV 数值。

## 最小对照

1. 在固定的引导场景中保留同一台相机、曝光和活动场景，让 A、B 各有一张易区分的烘焙贴图。分别做 A 单载、B 单载、A→B、B→A，再单验卸载和重载。从相同的加载完成点记录 Renderer 对应的数组槽和实际颜色纹理引用、方向纹理与 Shadowmask（若有），同时记 UV 缩放偏移。名称相同也要比较实际引用或资源身份。编辑器与 Development Player 分开验证。
2. 若阵列槽对应错误，先停用自定义的灯光数据注入，对照 Unity 原生加载。简化算例：自定义数组为 `[A,B]` 时，B 的局部索引 0 若直接照抄，会取到 A；若数组为 `[B,A]`，同一错误做法恰好取对 B。维护自定义映射时，用资源身份求当前槽，装卸后一起核对数组和相关 Renderer 的引用；这个数组顺序是为了说明问题设定的，不是 Unity 原生加载行为的实测。
3. 仍取同一张 B 时，用有编号的调试图查它取哪一格。光照 UV `(0.2, 0.4)`、缩放 `(0.5, 0.5)`、偏移 `(0.5, 0)` 得到 `(0.6, 0.2)`；误用零偏移会变成 `(0.1, 0.2)`，跳到另一半图。如果实际图和区域都对、只丢阴影，查混合光与烘焙数据；如果只是 UV 岛边串色且随视距变化，转查 [TA-08 的烘焙接缝排查](lightmap-seams-and-leaks.md)。

**验证状态。** 已核对版本文档、URP 宏及 Unity 官方阴影报告，两个数字例子是构造算例。未找到只换加载顺序且给出槽与区域读数的公开对照，未在本地 Unity 或真机复现。
