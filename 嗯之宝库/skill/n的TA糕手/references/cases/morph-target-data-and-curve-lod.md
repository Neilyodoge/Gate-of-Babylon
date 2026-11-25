# TA-64 · 表情变形在 LOD 切换时丢失

**制作决定。** 身体还在动、嘴眼却在换 LOD 时回到中性，先选中出问题的 LOD，手动把单一 Morph 权重从 0 拉到 1。无变形先查该级是否有目标数据，变形拉错位置先查重映射；手动正常、播放时才丢，再看曲线的 Max LOD、关联骨和实际提交的权重。

## 证据

- **同角色的公开对照。** [Simplygon 10.2.8400.0 的 LOD Recipe](https://documentation.simplygon.com/SimplygonSDK_10.2.8400.0/ue5/howtos/lodrecipe/lodrecipemorphtargets.html#_4-result)给出了逐 LOD、逐目标保留选项。两张原图都在骨骼网格编辑器将该目标设为 1.0：[图 5 Joy](https://documentation.simplygon.com/SimplygonSDK_10.2.8400.0/assets/morphtargetsJoy.ba1fea58.png)到 LOD2 还有上扬嘴角，到 LOD3 消失；[图 6 Sadness](https://documentation.simplygon.com/SimplygonSDK_10.2.8400.0/assets/morphtargetsSadness.d536fd63.png)抬起的内眉到 LOD2 就不再出现。此处 LOD0 是原网格，生成级中 Sadness 只保留 LOD1，Joy 保留 LOD1 和 2。两张图说明同一级可只缺部分动作；它们是编辑器静态预览，未测运行时插值和帧时。
- **数据路径和运行时路径是两道门。** [UE 5.5 骨骼网格 LOD 文档](https://dev.epicgames.com/documentation/unreal-engine/skeletal-mesh-lods-in-unreal-engine?application_version=5.5#skeletalmeshreductiontool-2)的 Remap Morph Targets 是从基础级向指定的减面级重映射目标；修改设置后按对应级重建。可用 [`UMorphTarget::HasDataForLOD`](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/Animation/UMorphTarget/HasDataForLOD?application_version=5.5)检查该级有无数据，但不能用它证明轮廓正确。[动画曲线文档](https://dev.epicgames.com/documentation/unreal-engine/animation-curves-in-unreal-engine?application_version=5.5)定义 Max LOD 为最后求值级，例如设为 1，LOD2 起停用；Connected Bones 还可让曲线随关联骨的活跃状态变化。
- **顶点变形出错的反例。** Epic [UE-30955](https://issues.unrealengine.com/issue/UE-30955)记录：导入自带的 LOD1，切过去手动推 Morph，角色会斜着拉长或异常缩放；LOD0 正常。它是 UE 4.12 已修复的回归，原 FBX 链接已撤，不能推断 UE 5.5 有同一缺陷。它能提醒排查：有数据、有权重，只能证明两道门都开了，还要看变形落在哪些顶点上。

## 用法与验证

1. 强制同一角色的相邻 LOD，固定相机位置、基础骨姿和材质，一次只拉一个可见目标（闭眼、张嘴、口型或眉形），分别保存 0、0.5、1 的静帧。记录该级有无目标数据、嘴缝/眼缘开合和是否拉动不该动的顶点。对照网格编辑器和运行时；记下实际使用的头部网格 LOD，而不只记录身体档位。
2. 手动权重正常时，以同一动画采样时间对比切换前后曲线和生效权重。对照只改 Max LOD 与只改关联骨，并检查是否有自定义蓝图在覆盖权重。若预览时就拉歪，先试重映射或为该级独立导入目标并检查目标拓扑，调曲线不会修复顶点错位；若只是小幅细节被压没，再独立比较 LOD 的 Morph 位置误差容限。
3. 以实际显示像素验收。对话和玩法仍能读出的闭眼、张口或口型留到需要的级；细微脸颊或皱纹可以更早停。若要主动去掉可见表情，比较在切换前把权重逐步降下与切级硬停。群体场景分别测资源内存、动画曲线成本与蒙皮成本，再定保留清单。

**验证状态。** 已目视 Simplygon 两组原图，核对 UE 5.5 文档及历史复现；未做本地运行时 A/B 或真机验证。
