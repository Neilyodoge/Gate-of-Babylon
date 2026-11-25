# TA-134 · 表情变形后高光仍像原形

**制作决定。** BlendShape 改了轮廓却没有正确移动高光时，固定同一姿势，先用世界法线可视化和不带法线贴图的斜光材质验法线，再启用切线空间法线贴图。前者就不对，先比形变前后的法线数据；只有后者不对，再追切线、UV 与实际着色分支。文件声明支持一种增量，不等于导入和绘制使用了它。

## 公开证据

- [Khronos glTF 2.0 · Morph Targets](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#morph-targets) 把性状存成基准属性的增量；基础网格已有显式法线且目标省掉 `NORMAL` 时，该目标不改变这些法线。切线增量为 XYZ，不能通过形变改 W 手性。若实现从位置、法线和 UV 重算切线，规范另有对形变切线的计算规则，不能把“未存切线”直接等同于“运行时冻结切线”。
- **格式与实现的反例。** [three.js r180 的 GLTFLoader.addMorphTargets](https://github.com/mrdoob/three.js/blob/r180/examples/jsm/loaders/GLTFLoader.js#L2409-L2486) 只建立 Morph 的 position、normal、color 属性，没有 `TANGENT` 目标分支；[Morph 法线顶点代码](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/morphnormal_vertex.glsl.js#L1-L15) 在启用时累加权重法线增量。[WebGLPrograms](https://github.com/mrdoob/three.js/blob/r180/src/renderers/webgl/WebGLPrograms.js#L292-L315) 在带切线且使用法线贴图或各向异性时启用顶点切线；[beginnormal_vertex](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/beginnormal_vertex.glsl.js#L1-L10) 从基础网格取切线，[normal_fragment_begin](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/normal_fragment_begin.glsl.js#L20-L46) 则在有切线与基于导数重建之间分支。这里说明的是该版本的加载与 WebGL 路径。比较有无基础切线可作诊断；导数基底在 UV 接缝、退化或与烘焙基底不一致时仍需验收。
- **Unity FBX 的入口。** [Unity 6000.0 模型导入器](https://docs.unity3d.com/6000.0/Documentation/Manual/FBXImporter-Model.html) 说明 `Blend Shape Normals` 的 `None` 不施加形变法线，`Calculate` 根据平滑设置计算，`Import` 缺源法线时可能由 FBX SDK 自行计算，结果与 `Calculate` 可不同。手册还要求导入 BlendShape 法线时 FBX 有平滑组。可以从可读 Mesh 用 [GetBlendShapeFrameVertices](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.GetBlendShapeFrameVertices.html) 分别读取位置、法线、切线增量，核对已导入的网格，而不只看导出设置。glTF 的语义不能用来预测 Unity FBX 导入器算出的具体方向。

## 算例与最小对照

两组 CPU 算例用同一方向光 `L=(1,0,0)`，用 `N·L` 检查方向响应。数值是诊断量，不是引擎高光强度。

| 对照 | 观察点 | CPU 结果 |
| --- | --- | --- |
| 无法线贴图，面片绕 Y 从 `(0,0,1)` 的法线抬起 45° | 只改位置，渲染仍用原 N；加法线增量后 N 为 `(0.707,0,0.707)` | `N·L` 从 0 到约 0.707 |
| 面片绕 Z 转 90°，法线仍为 `(0,0,1)` | 切线空间采样法线取 `(0.707,0,0.707)`；应把切线从 `+X` 变为 `+Y` | 继续用旧切线时 `N·L≈0.707`，新切线时为 0；关贴图时都为 0 |

1. 固定 LOD、骨姿、灯、相机和其它目标，单调一个表情的 0%、50%、100%。记录轮廓、见光错误处的实际世界法线和对应顶点增量；挑参考变形确实应该转动法线的点，避免把正常的零增量当成丢失。对照同拓扑目标网格或 DCC，不能只凭高光猜法线。
2. 无贴图方向对了再加切线空间法线贴图。先用支持方向判断的固定偏斜法线当测试信号，随后回到真实烘焙图，再核 UV 镜像、TBN 和接缝。只在这一关异常才追切线导出、导入及着色分支；记录哪个路径实际重算或读取切线。只有换 LOD 后整套表情才消失时，先查目标与动画权重，见 [TA-64](morph-target-data-and-curve-lod.md)。

**验证状态。** 核对 glTF 规范、three.js r180 和 Unity 6000.0 的公开路径，执行了两个方向算例；未在 Unity、three.js 做同素材对照，也未真机验证。
