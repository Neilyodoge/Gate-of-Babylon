# 大量同类特效：先拆活动系统数、粒子数和上传量

**制作决定**：如果大量短促的命中火花或爆炸卡在 CPU，先在同粒子数、同覆盖率下测减少活动系统数。把一次性发射与持续跟随分开：Unity 的 Galaxy Sample 证明可以按效果类型合并发射，但持续跟随仍可能每次更新上传整个状态表。

## 原始证据

核对日期：2026-11-23。

- **制作约束**：[Micuda1，Unity Discussions #7](https://discussions.unity.com/t/particles-in-dots-vfx-graph-vs-particlesystem/1641626/7)报告 ECS 射击原型使用数千个活动 Shuriken 系统时明显掉性能，因为现成素材仍先选 Shuriken。未报设备或分项时间，不能把掉帧归给模拟或渲染。池化和 Burst Job 是作者拟议的优化。[LifeFX 作者 #2](https://discussions.unity.com/t/particles-in-dots-vfx-graph-vs-particlesystem/1641626/2)建议共享 Graph 并点出上传效率；帖中毫秒数只是估计。
- **公开实现：一次性发射**。[Unity ECS Galaxy Sample](https://github.com/Unity-Technologies/ECSGalaxySample)的 [`VFXSystem.cs`，`VFXManager<T>`](https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/Assets/Scripts/VFXSystem.cs#L100-L130)把命中火花、爆炸各交给对应的 Graph。一类效果先攒有上限的 CPU 请求，`SetData` 只传本次请求数，再发一个 `SpawnBatch` 事件。队列满时不追加请求。减少的是逐事件使用独立系统的需要；源码不能证明整类效果只有一个 draw。
- **反例：持续跟随**。推进器的 [`VFXManagerParented<T>`](https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/Assets/Scripts/VFXSystem.cs#L191-L236)用可复用索引对应位置、方向、颜色、尺寸和长度。发射请求仍按数量上传，但有效缓冲下每次更新另外执行 `SetData(Datas)`，上传全容量的状态表；[`OnUpdate`](https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/Assets/Scripts/VFXSystem.cs#L324-L330)也先在主线程等待三个类型相关的 Job 完成。[容量设置](https://github.com/Unity-Technologies/ECSGalaxySample/blob/main/Assets/Scripts/VFXSystem.cs#L277-L285)把 Android/iOS 的推进器表设为 50,000 项，其他为 100,000 项。按源码结构 3 个三分量向量和 2 个 float、每分量 4 B 估算，若实测 stride 是 44 B，两档每次调用分别向 API 提交约 2.2 MB、4.4 MB 状态数据，不含发射请求。这是逻辑字节数，不是实测总线流量、驱动拷贝、GPU 时间或同时可见粒子数。
- **渲染实例化的边界**：[Unity 6000.0 Built-in 粒子 GPU Instancing](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/built-in/shader-built-in-birp/shader-standard-particle-shaders/particle-system-optimization/part-sys-instancing)限定 Mesh 渲染模式，要求兼容材质和平台。它是 Renderer 的选项，不会把现有 Particle System 的模拟迁到 GPU；也不能将 Built-in 的配置直接外推到其他管线。

## 怎么选、怎么验

| 看到的瓶颈 | 做一组对照 | 观察什么 |
| --- | --- | --- |
| 同类短效果很多，CPU 更新或提交吃紧 | 固定逐帧可见粒子数和发射轨迹，先比多个活动系统与更少共享系统；单独测池化是否只降了创建销毁成本。能保留同样网格外观时，再单独切 Shuriken Mesh 实例化。 | 活动系统数、主线程/渲染线程耗时、模拟耗时、draw 和漏发请求。 |
| 模拟本身贵，或持续跟随数据多 | 再试批量提交给 GPU 模拟。测不同比例的活跃记录，先证实全表上传或 Job 等待有影响，再考虑压缩活跃记录或分区，并核对可见性和事件延迟。 | `SetData` 的范围与调用耗时、CPU 等待、GPU 模拟耗时，以及目标图形 API 和设备是否支持所需功能。 |
| 行为可预制，或卡在透明绘制 | 前者可以把同一形状做成简单的网格动画候选；需要逐粒碰撞或独立运动时另算。若 GPU 主要卡在透明 pass，按[透明特效覆盖](transparent-vfx-overdraw.md)先改覆盖和叠层。 | 画面、顶点和贴图成本、排序变化、透明 pass 耗时。 |

保持目标分辨率、相机轨迹、发射种子、屏幕覆盖、混合和排序可比；一次只换系统数量、渲染路径或模拟路径之一，记录峰值时段和 P95。**未验证**：未找到同一效果的 Shuriken、网格动画、共享 Graph 真机 A/B；未运行样例，不给迁移阈值或帧率承诺。
