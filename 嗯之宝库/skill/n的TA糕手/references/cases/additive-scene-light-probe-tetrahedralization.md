# TA-148 · 叠加场景加载后动态角色的烘焙探针光照没更新

**制作决定。** 经典 Light Probe Group 下，新场景的静态光照贴图正常而角色仍取错光时，把场景加载完成、探针数据到达、四面体网格重建完成分开查。合并一批真正改变探针数据的增删场景，等数据就绪后重建一次，最后用固定世界点和角色画面双重验收。

## 公开证据

- **探针数据和插值拓扑不同步。** Unity 6000.0 [Load Light Probes in multiple scenes](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/direct-and-indirect-lighting/light-probes/and-scene-loading) 说明：场景增删会更新当前探针的位置和系数；Single 加载会随载入更新四面体，而 Additive 加载或 `UnloadSceneAsync` 不会自动重算。旧四面体不能反映新增或移除的探针，直接调用插值 API 也可能取到不符合预期的结果。CPU 开销随探针数增加，不宜对一批场景逐个重建。
- **真正的准备信号。** [needsRetetrahedralization](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/lightprobes/needsretetrahedralization) 说明探针是异步加载的外部场景数据，有时晚于 `sceneLoaded`。官方例子是叠加载入五个都带新探针数据的场景，等五次对应事件再重建；不能按所有场景数等待，纯 UI 等未改变探针数据的场景不应算作必到的一次。[TetrahedralizeAsync](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/lightprobes/tetrahedralizeasync) 重建当前全部已加载经典探针的位置；[tetrahedralizationCompleted](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/lightprobes/tetrahedralizationcompleted) 在计算完成后通知。用 [GetInterpolatedProbe](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/lightprobes/getinterpolatedprobe) 做固定位置诊断时，文档说 Renderer 参数用于缓存上帧的四面体索引以加快查找。
- **两种容易误判的情况。** [Unity 的布点排查原图](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/direct-and-indirect-lighting/light-probes/troubleshooting#troubleshooting-light-probe-placement) 中，救护车在较暗的街道中段仍亮，黄色插值四面体穿过了该区域；补暗区探针后，同处的车辆变暗。若场景早就稳定，重建完成后仍取错光，先查锚点与探针分布；[TA-30](light-probe-indoor-outdoor-transition.md) 有对照方式。启用 APV 时，URP 6000.0 的 [ProbeVolumePerSceneData](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeVolumePerSceneData.cs) 按 scene GUID 和 Baking Set 往 `ProbeReferenceVolume` 排队装卸；经典探针的重建不是这条路径的就绪信号。静态贴图的索引另见 [TA-91](additive-scene-lightmap-index-and-atlas-region.md)。

## 最小算例与验收

设旧位置为 `(0,0,0)、(4,0,0)、(0,4,0)、(0,0,4)`，固定测点 `(1,1,1)` 的四个插值权重都为 0.25。若四个旧探针某一标量系数都是 0.2，旧网格就给出 0.2。新场景恰在测点新增系数 0.8 的探针，理想重建后测点落在新顶点，权重为 1，得到 0.8。这个几何算例只说明位置或探针数已变仍不足以验收，同步中的 Unity 原生返回值需要实测。

1. 用经典探针建立 A、B 两区，烘成易分辨的间接色；留一个探针数据为空的场景作对照。固定曝光、直射光与 Renderer 锚点，比较加载 B 后不重建、仅在 `sceneLoaded` 发起、在本批探针数据就绪后发起三组；稳定基准组在数据稳定后采同一世界点。分别记加载完成、探针事件、重建完成和实际画面帧，记录探针数和固定方向的采样值。再做卸 B 与反序加载。
2. 多场景流送中集中管理重建：在已知批次的数据变更事件到齐后发起一次；异步重建期间新数据到来时保留脏标记，在当前轮完成后按最新快照排下一轮，别把旧完成通知当成新批次验证通过。若流送持续，按实测决定最多可容忍的旧数据窗口并检查切换尖峰。比较重建次数、CPU 峰值和从本批探针到达到角色得到正确光的时长。

**验证状态。** 已核对 Unity 6000.0 手册、API、APV 源码和暗街布点前后原图，并复算固定点权重。未跑叠加场景时序、重建耗时或真机对照。
