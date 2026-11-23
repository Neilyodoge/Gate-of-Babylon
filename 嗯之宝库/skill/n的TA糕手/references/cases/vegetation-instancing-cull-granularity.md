# 实例化植被：分别看提交次数和绘制实例数

**一句话结论**：草地若横跨多个镜头区域，整片共用一个包围盒会让屏外实例跟着可见区一起提交绘制。先对照按空间分块；若分块产生的 CPU 提交开销明显，再试间接绘制中逐实例剔除。决定用哪条路径，要看实例数和帧时各自怎么变。

## 官方边界与草地证据

- **Unity 6000.0 API**：[RenderMeshInstanced，Remarks](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/graphics/rendermeshinstanced#remarks) 按一次调用的整体包围盒剔除、排序；默认自动计算，也可用 `RenderParams.worldBounds` 覆盖。单次调用的容量与实例数据有关，文档给出的默认双矩阵情况为 511 个。此处是直接调用的行为，不能外推到所有实例化路径。
- **Unity 6000.0 API**：[RenderMeshIndirect，Remarks](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/graphics/rendermeshindirect#remarks) 也以 `worldBounds` 作调用整体的剔除依据。但绘制参数可以由 GPU 生成，单次 API 调用还能包含多个绘制命令。用间接 API 不会自动获得逐实例剔除：要另算可见列表和绘制参数。
- **作者实现与原图**：[Cyanilux《GPU Instanced Grass Breakdown》，2025-10-05 更新，Adding GPU Frustum Culling](https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/#frustum-culling)。计算着色器把实例位置转到裁剪空间，对通过视锥及距离检查的实例追加索引，再用 `GraphicsBuffer.CopyCount` 更新间接绘制的 `instanceCount`。作者给出 256 × 256 区域中的 **50 万个候选实例，约 8.9 万进入绘制**（FOV 60°，远裁面 100）。[原图](https://www.cyanilux.com/tutorials/gpu-instanced-grass-breakdown/FrustumCulling.png) 中草主要留在相机白色视锥内；这是一张开启状态的画面，数值来自作者说明。作者称开关两种状态都在 500 FPS 以上，仅说明空场景使用一块高端 PC GPU；未公布两组独立帧时、CPU 提交或 Pass 耗时，也没有空间块大小 A/B。实例数下降不能直接折算为帧时收益。

## 如何改分组

假设同网格同材质有 480 株分布在 12 个区，每区 40 株，主相机恰好只覆盖其中 3 区，剔除只测包围盒，且不考虑其他 Pass。整片作为一批可能把 **480** 个实例留给绘制；分区后，其余 9 个盒可整批淘汰，主相机只剩 **120** 个实例。如果脚本仍逐区调用 API，CPU 仍有 12 次调用，由引擎拒绝 9 批；若脚本先用空间结构筛块，则只调用 3 次，但需计入 CPU 筛选。这个算例的实例数指 draw 的输入，视锥外几何仍可被 GPU 裁掉，不等于有屏外像素着色，也不等于顶点着色器调用数。

优先把近邻放一块，避免为了凑满一批把距离很远的草混在同一盒里。开阔镜头本来就看见大多数区时，分小块可能只加 CPU 工作；狭长走廊或相机侧掠视锥边缘时，先试更小的分块。间接方案再用逐实例剔除对照：作者样例用实例位置加边界余量，树冠或大幅风摆要检查包含实际顶点变形的保守边界，否则画面边缘会提前消失。主相机列表也不能直接当阴影列表；屏外树仍可能向屏内投影。

## 最小验证

1. 锁相机、草的随机种子、LOD、材质、风状态和分辨率，选开阔俯视与窄视锥边界两组代表镜头。先关阴影和遮挡剔除，比较整片、空间块（至少两档大小）、同块加逐实例筛选；保持可见叶片一致。
2. 核对主线程调用数、渲染线程提交时间、实际绘制命令及各命令的实例数；GPU 计时把剔除 Compute 和草地绘制分开，再看总帧时。打开真实阴影和风后，再抓影子 Pass、玩法镜头中的弹出及峰值。CPU 和 GPU 孰为瓶颈，以目标设备结果决定。

**状态**：已核对官方边界、作者源码与原图；480→120 为受控算例。没有本地复现、真机数据或定量 CPU／GPU 净收益。
