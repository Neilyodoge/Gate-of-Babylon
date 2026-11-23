# 同 Shader 的对象参数：用 Frame Debugger 区分 SRP Batch 和实例化

**制作决定**：颜色或少量标量只有几个固定档位时，先复用少量兼容材质，检查它们是否仍用同一 Shader 变体。大量同网格对象各自需要颜色时，再给这组对象试共享材质和实例属性。只在 Frame Debugger 证实它们进入期望的绘制路径、Profiler 证实时间收益后推广。

## 同一场景的原始对照

核对日期：2026-11-23。

[Jasper Flick，Catlike Coding《Draw Calls》§2.1–2.3](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/#2.1)。这是 Custom SRP 的不透明球体样例，文章最初使用 Unity 2019.2.9f1，后更新至 2022.3.5f1；没有为每张图单独标注 Editor 版本和设备。

| 做法 | 作者 Frame Debugger 原图 | 怎么读 |
| --- | --- | --- |
| 76 个球使用同一兼容 Shader 的 4 个颜色材质 | [一个 SRP Batch](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/srp-batch.png)；原图中还有清屏与天空盒。 | 4 个材质仍可以走同一 SRP 批。这个条目是按此路径优化的一串 draw，不等于把球体变成一次实例化 draw。 |
| 给其中 24 个球应用颜色 `MaterialPropertyBlock`，未使它们走实例化 | [24 条普通绘制](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/24-non-batched.png)夹在两段 SRP Batch 之间。 | 这组对象离开 SRP Batcher；示例里其余对象的批也被分成两段。不能把行数的增加直接换算成 CPU 毫秒。 |
| 把颜色声明为实例属性并启用实例化，继续用 4 个材质 | [四条实例化绘制](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/instanced-draw-calls.png)，旁边仍有 SRP Batch。 | MPB 可以给实例化读取的属性提供值；相同 Shader 与网格还不够，示例仍按材质拆分。 |
| 给这 24 个球共用一个材质，保留各自的实例颜色 | [一条实例化绘制](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/one-instanced-material.png)，其余对象仍走 SRP。 | 只给需要的对象换路径即可。平台、每实例数据量、排序或 pass 都可能增加实际分组，不能承诺任意数量一条 draw。 |

**官方边界**：[Unity 6000.0 URP 的 SRP Batcher 说明](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher)说明它通过让材质数据驻留 GPU，降低使用同一 Shader 变体的绘制准备和提交成本。[移除兼容性](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-incompatible)说明，对特定 Renderer 加 MPB 会移出 SRP Batcher；大量相同网格和材质可另试 `Graphics.RenderMeshInstanced`，或仅对候选 Renderer 移除兼容并启用材质实例化。两条路用 Profiler 比较。示例中的绘制事件名称以其 Custom SRP 为准。

## 让结果能指导选型

1. 固定对象数、网格、颜色分布、相机和 pass。先比较复用少量材质与 Renderer 逐对象覆盖颜色；分开看 SRP 批条目、每批实际 draw 和普通 Draw Mesh。若批被切，核对相邻事件的 Shader 变体、关键字、材质与 pass，确认 MPB 是否真的应用到了 Renderer。
2. 再让候选对象用同一材质和声明过的实例属性，确认颜色确实逐实例生效；保留它们的实际剔除和排序需求做对照，别拿承受不同可见物数的两组直接比。
3. 在目标设备分别测一组参数固定和一组按实际频率更新的场景。记录主线程、渲染线程准备和提交时间、GPU 时间、材质数量及逐帧更新开销。对少量例外也保留最简单的方案作基线，避免为批条目多一行而改整套管线。

**未验证**：原图证实了示例的绘制路径和事件数量；没有同条件的 CPU 毫秒、GPU 时间或内存数据。未在本地 Unity 或目标设备复现。
