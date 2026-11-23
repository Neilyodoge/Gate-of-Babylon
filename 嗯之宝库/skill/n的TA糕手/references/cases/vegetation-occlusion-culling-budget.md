# 植被遮挡剔除：少画之后，先把建深度层级的成本算回来

**一句话结论**：高墙、山体或地形转折遮住成片植被时，可以试 GPU 遮挡剔除；开阔俯视也要作为对照。逐项计开关前后的几何绘制、深度层级和剔除耗时，再用完整帧时决定。绘制阶段更快也可能收不回新增开销。

## Steam Deck 的反例：几何少了 0.50 ms，计时小计却多了 0.247 ms

[Ferri de Lange，《Two Pass HZB Occlusion Culling》，2024-12 至 2025-01，Profiling results](https://ferri.dev/project/two-pass-hzb-occlusion-culling/#profiling-results)。作者用 Vulkan 渲染器在 Steam Deck 对比三个镜头；场景总计有 8000 个绘制项，无 CPU 剔除。第一阶段画通过视锥检查的历史可见项，用所得深度建 HZB；第二阶段再测之前未画的项，补画现在可见的几何。以下是作者公布的阶段耗时，单位为 ms。

| 镜头原图 | 关闭时小计 | 开启时几何（两阶段合计） | 开启时建 HZB | 开启时小计 | 小计变化 |
| --- | ---: | ---: | ---: | ---: | ---: |
| [1 · 近处外立面](https://ferri.dev/images/scene-1.webp) | 3.457 | 2.170 | 0.789 | 2.996 | −0.461 |
| [2 · 室内长廊](https://ferri.dev/images/scene-2.webp) | 5.083 | 2.940 | 0.757 | 3.737 | −1.346 |
| [3 · 较开阔的室外镜头](https://ferri.dev/images/scene-3.webp) | 3.754 | 3.240 | 0.725 | 4.001 | +0.247 |

小计含生成绘制命令的成本。镜头 3 的 GBuffer 为 **3.74 → 3.24 ms**，节省 0.50 ms，但新增 HZB 就花了 **0.725 ms**。作者的三组数据里第二阶段 GBuffer 均记作 0 ms；这些数不能代表移动镜头的补画成本。原图只用于识别镜头内容；作者没有列每组实际绘制数或遮挡比例、GPU 完整帧时或样本分布。这是建筑场景的 Steam Deck 数据，不能外推成植被或手机的收益数值。

## 在植被测试里怎么用

- [Unity 6000.0 URP GPU Occlusion Culling](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/gpu-culling#how-gpu-occlusion-culling-works) 配合 GPU Resident Drawer，使用当前帧和前一帧的深度，并按投影包围球大小选降采样层；任一帧未被遮挡就保留。官方也明确，如果剔除掉的工作很少，额外设置工作可能让渲染更慢。Unity 时序和开销不应用 Steam Deck 案例的数字代替。
- 和[实例化植被与剔除粒度](vegetation-instancing-cull-granularity.md)的视锥剔除分开测：树在屏内但被大型遮挡物挡住，才有额外收益。整个块的保守边界只要露出一部分就可能被保留；减小块或改用逐实例判断要同时计剔除、提交与缓冲开销。Alpha 裁剪的叶片能否当遮挡者取决于实际深度覆盖；用实体包围盒替代镂空叶片写深度可能错剔后方物体。
- 在同一目标设备上，固定材质、LOD、分块、阴影、分辨率、视锥剔除和 GPU Resident Drawer，只切 GPU 遮挡剔除。选山体背后的树林、开阔俯视、快速绕出墙后三段；每组记候选／实际绘制实例、额外深度生成或复用开销、HZB、可见性测试、命令生成、受影响几何 Pass、总 GPU 和实际帧时。若深度已经供其他效果使用，只计因开关增加的部分；看最坏镜头是否多画、漏画或延迟露出。单独确认阴影，因为相机遮挡不能说明光源视角也遮挡。

**状态**：已查看作者在 Steam Deck 上的三个阶段计时与对应原图，并核对 Unity 文档；本地未复现，缺同一植被场景和目标设备的完整帧时 A/B。
