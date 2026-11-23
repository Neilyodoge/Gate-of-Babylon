# 法线贴图接缝：先看坏在哪些区域，再选修法

**制作决定**：只有镜像区域反光翻面，先对照模型自带切线是否被重算；普通 UV 和镜像 UV 一起表现出错误方向，再单测绿通道。平面出现跟着三角对角线的折影，优先固定烘焙和运行时使用的三角网格。分别验收这些情况，避免通过全局翻绿通道掩盖局部错误。

## 原始图和边界

核对日期：2026-11-23。

- **镜像与 Y 方向不是同一种坏法**：[Ed Mackey / Khronos glTF-Sample-Assets · Normal-Tangent Mirror Test](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/NormalTangentMirrorTest)。样件提供顶点切线，并排放真实几何、普通法线贴图、V Mirror、U Mirror。正常原图里四列反光方向相近。[忽略输入切线](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/NormalTangentMirrorTest#problem-supplied-tangent-vectors-not-used)一组里地平线近似水平但只有镜像两列看成凹面；[错误翻转 Y](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/NormalTangentMirrorTest#problem-flipped-y-axis-or-flipped-green-channel)一组里真实几何列仍正常，三列法线贴图样件里有多个格子的地平线斜或反转。前一组错误图使用了不同渲染器和环境图，因此只比较各图内部的几何对照，不把两个图的亮度差当作证据。此样件检验的是**使用文件自带切线**，自动生成切线应另用项目的 `NormalTangentTest`。glTF 法线贴图自身采用该规范的 OpenGL 方向，渲染器若内部约定不同应在读入时适配。
- **切线基与三角化**：[Joe Wilson，Marmoset《The Toolbag Baking Tutorial》](https://marmoset.co/posts/toolbag-baking-tutorial/)的[切线匹配图](https://marmoset.co/wp-content/uploads/2017/04/tangentmismatch.jpg)里，匹配版本的立方件面光滑，失配版的平面出现大范围波状亮暗。[三角化对照图](https://marmoset.co/wp-content/uploads/2017/04/nmtriagulation.jpg)的两级圆柱在不匹配的对角方向出现沿三角形的尖形明暗，匹配版的平台与顶面平顺。作者建议烘焙前固定三角化，因为四边面、N 边面的内部边可在两端被拆得不同。确认两端的顶点法线、UV 和三角化来自同一份网格再比切线算法。
- **分辨率会暴露硬表面差异**：同文 [Hard Edges and UVs 原图](https://marmoset.co/wp-content/uploads/2017/04/eotech2.jpg)按 1024、512、256 对比。无硬边的下排在 1024 难见问题，降到 256 后大侧面出现沿三角形的亮线；沿 UV 缝设硬边的上排在同尺寸更干净。作者提出，保留硬边的地方应切开 UV，避免两面争用边线纹素；没有要求每条 UV 缝都硬化。这是该件硬表面的图示，不能推广为曲面都应增加硬边，也不是压缩格式的 A/B。
- **Unity 的导入选项**：[Unity 6000.0 · ModelImporterTangents](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/modelimportertangents)把 `Import`（用模型文件的顶点切线）与 `CalculateMikk`（用 MikkTSpace 计算，文档列为默认）列为不同操作。MikkTSpace 一致不能抵消输入网格或法线的变化；Unity 的 glTF 插件也未必走同一 ModelImporter 路径。

## 项目里怎样分开验证

保持同一资产、相机、反光环境和光照；先用正常、U 镜像、V 镜像样件及无贴图几何作对照。暂时禁用压缩和低 mip 作为诊断基线，辨别是粗质量档扩大了误差，还是最高质量下已有结构性错误。

1. 镜像区域独坏：在同一已三角化网格上只切换导入已有切线与重算，检查 UV 镜像的方向符号是否一路保留到 Shader。固定贴图，不同时翻绿通道。
2. 普通区域也凹凸反了：用明确约定的样件只改 Y 解码或贴图导入中的一端，避免素材输出和引擎重复翻转。
3. 明暗沿对角线打折：固定低模三角、顶点法线和 UV，用同份导出烘焙与运行。若硬表面仅在远景或低分辨率出问题，再对照在已有 UV 缝上设合适硬边的重烘焙，并分别恢复目标 mip 与压缩格式。

**未验证**：只核对公开原图和文档；尚未在 Unity 复现、测各平台压缩或确认项目实际使用的导入器。
