# TA-105 · Alembic 网格缓存的拓扑变化与运动向量

**制作决定。** 缓存形体播放正确，只在破碎或重网格的几帧拉出长条时，先检查前后采样点能否代表同一表面点。顶点能对应时可用位置差；对应关系失效时优先从模拟端随当前网格导出显式速度。在没有可信速度的变拓扑帧，先隔离变形向量或做针对性降级，别用相邻数组索引硬配。

## 公开证据

- [UE 5.5 · Alembic File Importer / Import as Geometry Cache](https://dev.epicgames.com/documentation/en-us/unreal-engine/alembic-file-importer-in-unreal-engine?application_version=5.5#importasgeometrycache) 区分三种 Motion Vectors：默认 `No Motion Vectors`，拓扑稳定时 `Calculate Motion Vectors During Import`，顶点数量变化时导出原 DCC 的速度并选 `Import Abc Velocities as Motion Vectors`。后两者会增加缓存磁盘占用。文档用顶点数量举例；即使数量相同，若点顺序改变且没有可用的映射，也不能用同行位置相减。
- [SideFX · Alembic ROP](https://www.sidefx.com/docs/houdini/nodes/out/alembic.html) 把 `v` 列为特殊属性；多边形的顶点域 `v` 会作为点域写出。其 `Use Motion Blur` 开关存的是子帧几何采样，不等于成功写入速度属性。[Alembic 1.8.8 的 OPolyMesh.cpp](https://github.com/alembic/alembic/blob/1.8.8/lib/Alembic/AbcGeom/OPolyMesh.cpp) 在样本有速度时创建标准的 `.velocities` 矢量数组。[SideFX 论坛原帖](https://www.sidefx.com/forum/topic/97923/#post-430357) 的作者报告即使 Houdini 里有顶点 `v` 且 UE 选了导入速度，仍未看到运动模糊。该帖没有核实写入属性或给出最终解，不能据它断言顶点域一定是故障根因；应检查导出的文件和实际向量通道。
- [Blender 4.3 · Mesh Sequence Cache / Velocity](https://docs.blender.org/manual/en/4.3/modeling/modifiers/modify/mesh_sequence_cache.html#velocity) 支持加载变拓扑缓存，默认从 Alembic 的 `.velocities` 取数据；明确区分按帧速度与按秒速度，后者转换时用 `1/FPS`。不要把这条界面的换算直接当成 UE 导入器的固定规则；跨 DCC 要核对数据单位、采样时间和播放速度。

## 最小对照

1. **先证实能不能配点。** 对一段稳定网格和一段破碎帧，比较顶点顺序、稳定 ID（若工作流能导出）与面索引。算例：上一帧按 A、B 存 0、1 m；下一帧两点都向右走 0.1 m，但改按 B、A 存 1.1、0.1 m。按槽位相减得到 +1.1、−0.9 m，按身份对应都是 +0.1 m。固定计数或轮廓连续都不足以证明顺序稳定；对破碎生成的新点也不能假定上一帧就有同 ID。
2. **分别检查导出、导入和屏幕输出。** 用已知速度的单向平移作标尺：2.4 m/s 在 24 FPS 缓存的一样本间隔对应 0.10 m；按实时时间推进、60 FPS 下一个屏幕帧对应的是 0.04 m，不能把每个缓存样本的位移当每个屏幕帧的位移。先检查破碎帧 `.velocities` 是否存在并与该样本的位置配套，再比较源端速度、引擎导入后和实际向量缓冲的方向与量级。单独验坐标轴、长度单位和物体整体变换，避免把整体变换同时算进变形速度和对象运动。
3. 固定相机、帧采样和快门，对照 UE 不导入变形向量、导入时差分、导入显式速度三组。先单看向量通道和破碎前后一帧，再分别开运动模糊与时间抗锯齿；新显露像素没有对应历史，即使速度合理也可能需要历史拒绝。若坏向量只出现在重建的片段，优先测试只抑制这部分变形向量或在该切点重置历史的候选；缓存不存变形向量不等于摄像机和对象整体运动都归零。

**验证状态。** 已核对 Epic、SideFX、Blender 手册、Alembic 写入源码和论坛作者报告，并复算顶点重排与时间单位算例；未本地导入同一破碎缓存，未做向量抓帧、真机或占用测试。
