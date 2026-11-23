# TA-34 · 体积雾的时间重投影拖影

**一句话结论**：雾里的闪灯或移动光束留下亮尾时，先固定镜头和体素设置，分别对照历史权重和该灯是否参与体积散射。降低历史权重可能减轻亮尾但让噪点变明显；单灯退出体积雾能保留其他灯的历史平滑，但会失去这盏灯在雾中的光束。

## 证据

- **官方文档 · UE**：[Epic · Temporal Reprojection](https://dev.epicgames.com/documentation/en-us/unreal-engine/volumetric-fog-in-unreal-engine#temporalreprojection) 说明低分辨率、跟随视锥的体积纹理通过逐帧抖动和历史重投影平滑；手电和枪口闪光等快速变化的灯会产生光照拖尾。文档建议对不需要照亮雾的快速变化灯，把该灯的 `Volumetric Scattering Intensity` 设为 0。
- **画面观察 · UE 静态图**：同一条街的官方原图，[散射强度 1](https://dev.epicgames.com/community/api/documentation/image/66fccc45-9963-4487-adef-38480f629702?resizing_type=fit) 可见雾中光束，[散射强度 0](https://dev.epicgames.com/community/api/documentation/image/d2e5822a-a15b-4248-90f9-1948b99f53c9?resizing_type=fit) 的光束消失，场景表面仍有可见照明。这组图只验证灯的体积贡献，未切换历史或展示移动、关灯后的连续帧。
- **文档和实现 · Godot 4.4**：[Volumetric fog 文档](https://docs.godotengine.org/en/4.4/tutorials/3d/volumetric_fog.html) 区分历史占比高时的拖影与占比低时可见的抖动。[4.4-stable 的雾处理 GLSL](https://github.com/godotengine/godot/blob/4.4-stable/servers/rendering/renderer_rd/shaders/environment/volumetric_fog_process.glsl#L312-L334) 检查重投影坐标是否在上一帧体积内并取历史样本，[约第 711 行](https://github.com/godotengine/godot/blob/4.4-stable/servers/rendering/renderer_rd/shaders/environment/volumetric_fog_process.glsl#L700-L722) 再以历史权重混合。固定位置、权重 0.9、关灯后当前增量为 0 的理想算例，22 帧后历史增量剩约 9.8%（0.9^22）；它不代表 UE/Godot 的实际输出或推荐参数。
- **性能边界 · UE**：[Epic · Performance](https://dev.epicgames.com/documentation/en-us/unreal-engine/volumetric-fog-in-unreal-engine#performance) 报告开启体积阴影的点光和聚光灯比不投射体积阴影的同类灯约贵 3 倍；这是灯光路径的报告值，不能用来估算关闭历史或调整体素后整帧能省多少。文档中的 PS4 和 GTX 970 例子使用不同的画质档，也不能拿两例相减当体素成本。

## 怎么验证

1. 固定相机、曝光、雾密度、体素网格、阴影设置和相同灯光轨迹。先拍静态灯下的基准，随后在同一帧让灯瞬间熄灭；引擎支持时比较关闭历史、低权重和高权重。固定雾中一个不被物体遮挡的区域，记录亮度相对关灯稳定值降到峰值增量 10% 的帧数，并单独观察静止时的噪点或闪动。
2. 恢复基准历史设置，只改该灯的体积贡献到 0，确认表面照明仍可读，并在实际玩法镜头判断丢失光束是否可接受。必须保留光束时，再试改历史权重或体素尺寸，每次只改一项；跟随镜头、镜头切换等情况另跑一组，避免把显露和光源残影混为一谈。
3. 在同设备同质量档下分别对照体素档、体积光贡献和体积阴影，记录 GPU 整帧、体积雾阶段以及可分辨的灯光注入/历史阶段耗时，同时留截图和连续帧。比较前先达到稳定状态；计时器无法拆开的阶段保留其合计。

**状态**：已核对 Epic 与 Godot 文档、Godot 4.4 源码和 Epic 静态原图；未获得同镜头移动灯历史开关对照，未在本地或目标设备运行、计时。
