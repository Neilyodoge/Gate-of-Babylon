# TA-52 · 相邻地形块的 LOD 裂缝：分开验边缘闭合与切换跳变

**制作决定**：先锁定相邻块的 LOD 并用深度／纯色检查是否露底。固定粗细级也有缝，先核对世界位置和高度是否一致，再用边缘拼接或裙边。两边固定时无缝、只有切换时漏光或跳动，先核对两侧是否用同一条共同边作为形变终点；延长各自的渐变时间无法解决边界不同步。

## 公开证据

- **原图与实现 · GPU Gems 2，Asirvatham 与 Hoppe，[Geometry Clipmaps](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-2-terrain-rendering-using-gpu-based-geometry) §2.3.2、§2.3.5。** [图 2-3](https://developer.download.nvidia.com/books/gpugems2/02_clipmaps_03part1.jpg)的 (a) 标出紫色过渡带，(b) 的红色地表可见斜向细线，(c) 的接界弱了。相邻级共享嵌套网格，过渡权重在细级外边到 1；细级的边中点取粗级边两端高度的线性插值。外缘另用退化三角形处理 T 接口，[图 2-6](https://developer.download.nvidia.com/books/gpugems2/02_clipmaps_06part1_new.jpg)展示不同颜色的分区。作者实现同时处理拓扑连接、位置形变和法线渐变；两张静态图无法说明切换持续几帧。
- **CesiumJS `main` · 流送地形的裙边。** [CesiumTerrainProvider](https://github.com/CesiumGS/cesium/blob/main/packages/engine/Source/Core/CesiumTerrainProvider.js#L795-L833)在该量化网格路径用层级最大几何误差乘 5 得到这一块的裙边深度，传给四边；[网格 Worker](https://github.com/CesiumGS/cesium/blob/main/packages/engine/Source/Workers/createVerticesFromQuantizedTerrainMesh.js#L313-L406)把裙边追加并保留不含裙边的索引数。[GlobeSurfaceTileProvider 的条件](https://github.com/CesiumGS/cesium/blob/main/packages/engine/Source/Scene/GlobeSurfaceTileProvider.js#L2765-L2773)在相机处于地下或地球半透明时不画裙边，此时[绘制数量](https://github.com/CesiumGS/cesium/blob/main/packages/engine/Source/Scene/GlobeSurfaceTileProvider.js#L3279-L3286)截到原网格。这说明裙边是独立的遮挡几何，适合从上方看且不要求接面真正对齐的地块；误差乘 5 是该实现的选值，不能当成通用规则。

## 剖面算例

相邻两块边的共同端点高度都是 0 m。粗边只连端点；细边在同一水平位置的中点高度是 0.40 m。即使两个端点对齐，中点仍差 0.40 m；把细边中点形变到粗边插值的 0 m，剖面才重合。若从高的那个边只垂下 0.25 m 的裙边，这个剖面仍差 0.15 m。实际裙边还要核对横向偏差、观察角度和阴影，不用示例数字做默认深度。

## 最小对照

1. 放两块共享高度数据的地形，让接缝跨过一处凸起或沟。固定相机和材质，分别锁同级、相邻差一级以及可出现的最大级差。输出块 ID、LOD、线框和深度，测共同端点和中点的世界空间高度差。若同级都离开，先查坐标原点、边缘采样、独立量化或解码；若只在粗细组合有缝，再核对细边是否引用了粗边上不存在的顶点。
2. 固定不同级对照裸边、裙边和按邻级拼接。裙边适合先保住不透明远景覆盖，额外验低视角、露出的侧面与阴影；如果需要沿边连续且会近看，试共享边规则：2∶1 网格用对应粗级边的索引模板或把细边顶点对齐粗级边，再处理拐角、T 接口和更大级差。独立地块要限制邻级差或为实际组合准备拼接；碰撞、高度查询和导航另查实际数据。
3. 沿接缝慢移并来回跨阈值，再单独模拟一块子地形晚到。逐帧记录两块 LOD、共享边目标和形变权重，以及露底像素和边缘跳动。若仅过渡时张开，让共享边在整个切换内遵守同一邻级／同一目标，必要时保留旧覆盖直到新块可用；然后再调内部渐变。深度已闭合但仍有线时，单独检查法线、材质与阴影，别继续加裙边。

**状态**：已核对 GPU Gems 2 静态原图和顶点逻辑，以及 CesiumJS 的裙边生成与绘制条件；0.40 m 为剖面算例，未做本地速度切换、缺块测试或真机计时。
