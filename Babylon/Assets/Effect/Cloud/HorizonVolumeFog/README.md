# Local Volumetric Cloud

URP 14 的局部 Ray March 体积云核心实现，不依赖 Houdini。`LocalVolumetricCloud` 会在 Unity 内生成 Perlin-Worley 基础体积、Worley 细节体积和 Weather Map，也可以分别指定外部纹理。

## 文件结构

- `LocalVolumetricCloud.cs`：组件参数、材质属性、程序化噪声生成和交互目标。
- `LocalVolumetricCloud.shader`：射线求交、密度采样、体积光照与前向合成。
- `Editor/LocalVolumetricCloudEditor.cs`：中文 Inspector、云团代理与 SDF 代理转换工具。
- `Editor/MeshSdfBaker.cs`：闭合静态网格的 SDF 烘焙器。
- `Generated/`：编辑器生成的 SDF Texture3D 资产。

## 渲染流程

### 1. 代理盒与观察射线

实际渲染对象是一个标准 Cube。Cube 不代表云的最终表面，只负责限定需要执行体积计算的屏幕区域。

Shader 绘制 Cube 背面（`Cull Front`），然后：

1. 由相机位置和当前片元位置构造世界空间观察射线。
2. 将射线变换到 Cube 局部空间。
3. 使用 slab ray-box 算法计算射线进入和离开局部 `[-0.5, 0.5]` 包围盒的距离。
4. 采样相机深度纹理，把射线终点截断在最近的不透明场景表面。
5. 在有效区间内进行 Ray March。

每个坐标轴分别计算近、远平面距离：

```text
t0 = (boxMin - rayOrigin) / rayDirection
t1 = (boxMax - rayOrigin) / rayDirection
entry = max(min(t0, t1))
exit  = min(max(t0, t1))
```

当 `exit <= entry` 时，该像素不需要计算体积。

### 2. 程序化密度场

密度场由三类纹理组成：

- **Shape Texture3D**
  - R：Perlin-Worley 基础形状。
  - GBA：低、中、高频 Worley，用于 FBM 重映射。
- **Detail Texture3D**
  - RGB：三档 Worley 频率，用于侵蚀云块边缘。
- **Weather Map**
  - R：大范围云量覆盖。
  - GB：由二维 Value Noise 有限差分得到的 Curl 扰动方向。
  - A：预留的大范围变化数据。

纹理由 C# 在编辑器或运行时首次启用时生成。噪声哈希和采样周期均可平铺，不依赖 Houdini 或外部 Texture3D 制作流程。

基础形状大致按以下方式组合：

```text
worleyFbm = low * 0.625 + mid * 0.25 + high * 0.125
shape = remap(perlinWorley, -(1 - worleyFbm), 1)
shape = remap(shape, coverageThreshold, 1)
```

Detail Worley 只在基础形状大于零时采样，用于减少无效纹理读取。Weather Map 负责比 3D 噪声更大的分布尺度，避免整个云体积呈现明显重复。

### 3. 密度与边界

最终采样密度近似为：

```text
density = shapeNoise * boundaryMask * volumeDensity
```

- **云量**：移动噪声覆盖阈值，改变云块数量和空隙。
- **体积浓度**：改变已有云块的光学厚度，不直接改变分布。
- **边界遮罩**：根据盒体、云团或网格 SDF 模式限制密度。

支持三种边界：

- **盒体**：根据采样点到 Cube 六个面的最小距离做边缘羽化。
- **云团**：使用解析椭球距离作为基础轮廓，再用低频噪声扰动边界。
- **网格 SDF**：采样烘焙后的带符号距离场；正值表示内部，负值表示外部。

### 4. 自适应 Ray March

观察射线使用世界单位步长，并由最大步数限制最坏成本：

```text
stepLength = max(configuredStepLength, rayLength / maxSteps)
```

密度大于零时使用精细步长；空区域会使用更大的步长。扩大步长前会在中点执行一次低成本探针采样，避免直接跨过薄云层。

体积浓度的积分距离会转换到代理局部空间，因此把 Cube 从 10 米放大到 50 米不会让相同浓度立即饱和。世界空间步长仍用于决定画面采样质量。

### 5. Mipmap 与自动 LOD

Shape、Detail 和 Weather 纹理均生成 Mipmap。Shader 根据以下信息估算采样足迹：

- 当前观察距离对应的屏幕像素世界尺寸。
- Ray March 步长。
- 世界空间或局部空间噪声尺度。
- Texture3D 实际分辨率。

近似 LOD：

```text
texelFootprint = normalizedFootprint * textureResolution
lod = max(log2(texelFootprint) - 0.5, 0)
```

这样可以减少远处高频噪声造成的闪烁、断层和摩尔纹。

## 体积光照

### Beer-Lambert 消光

每一步根据密度和步长计算吸收：

```text
sampleAlpha = 1 - exp(-density * stepLength * extinction)
```

然后按从前向后的方式累计预乘颜色和透射率：

```text
color += transmittance * sampleAlpha * lighting
transmittance *= 1 - sampleAlpha
```

材质使用 `Blend One OneMinusSrcAlpha`，与上述预乘结果对应。

### 方向光

每个有效观察采样点会沿主方向光方向再次步进，累计光线路径上的光学深度：

```text
lightTransmittance = exp(-lightOpticalDepth * extinction)
```

这部分形成云内部的自阴影。`光照步进次数`越高，明暗层次越稳定，但它会乘在所有有效观察采样上，是主要性能成本之一。

### 散射相位

使用 Henyey-Greenstein 相位函数近似云滴的方向性散射：

```text
phase(cosTheta, g) =
    (1 - g²) /
    (4π * (1 + g² - 2g*cosTheta)^(3/2))
```

`前向散射`对应参数 `g`。正值会增强朝向光源观察时的亮边，零接近各向同性。

### 点光源与聚光灯

Shader 接入 URP Additional Lights。每个有效体积采样点读取：

- 光源颜色。
- 采样点到光源的方向。
- 距离与聚光角衰减。
- URP 实时阴影衰减。

局部光源同样经过相位函数后加入散射颜色。为控制成本，目前局部光源不再执行一条额外的云内自阴影 Ray March；`最大局部光源数`只限制单个云体积每个采样点计算的灯数。

## 网格 SDF 烘焙算法

SDF 模式面向闭合静态 `MeshFilter`：

1. 通过 `Mesh.AcquireReadOnlyMeshData` 读取模型，因此不要求 FBX 开启 Read/Write。
2. 将来源网格归一化到带少量 padding 的单位包围盒。
3. 为三角形建立 BVH，加速每个体素到最近三角形的距离查询。
4. 沿 X 轴对每条体素扫描线收集三角形交点。
5. 使用奇偶规则判断体素位于网格内部还是外部。
6. 将内部距离写为正值、外部距离写为负值，保存为 `RHalf Texture3D`。

运行时只采样 SDF 纹理，不会再遍历来源网格。边缘扰动直接加在带符号距离上：

```text
distortedDistance = signedDistance + edgeNoise * edgeVariation
mask = smoothstep(-softness, softness, distortedDistance)
```

因此“边缘变化”为零时保留原始 SDF 轮廓，提高后只扰动表面附近。

## 交互

组件目前提供一个球形密度清除交互。每个采样点计算到 `Interaction Target` 的世界空间距离，并在半径内降低密度：

```text
clearMask = 1 - smoothstep(innerRadius, outerRadius, distance)
density *= 1 - clearMask * interactionAmount
```

可用于角色、飞行物或法术推开云。当前为单目标方案；大量交互体需要改为 StructuredBuffer、体积 Mask 或独立 Compute Texture。

## 使用

1. 在场景中创建标准 Unity Cube。
2. 添加 `ProjectR/Effects/Local Volumetric Cloud` 组件。
3. 缩放 Cube，使其覆盖需要的云体积；组件会自动创建临时材质。
4. 如需角色推开云，将角色 Transform 指定给 `Interaction Target`，再调整 Radius、Softness 和 Amount。

默认使用代理盒局部空间采样，缩放 Cube 会连同云纹理一起缩放。启用“使用世界空间采样”后，缩放只改变云的覆盖范围，不会拉伸云纹理；移动 Cube 时云纹理会保持锁定在世界坐标。

“云量”和“体积浓度”承担不同职责：

- **云量**改变噪声覆盖阈值，控制云块疏密与空隙。
- **体积浓度**只改变已有云块的透光和厚重程度。光学积分按代理局部尺度归一化，因此同一浓度不会因为 Cube 放大而立即饱和。
- **云块阈值（高级）**定义云量为一时的基础形状，一般先调整云量。

## 独立局部云团

截图式的独立蓬松云应选择“边界模式 → 云团”。该模式先生成连续椭球体，再只对外轮廓施加低频噪声，不会沿用天空云的大范围覆盖逻辑挖空整个体积。

- **边缘变化**：轮廓向内、向外起伏的幅度。
- **边缘尺度**：低值产生少量大鼓包，高值产生更多细碎起伏。
- **边缘羽化**：轮廓透明过渡宽度。
- **内部变化**：内部受原有云噪声影响的程度；需要饱满团块时保持较低。

直接缩放标准 Cube 可控制云团的球形或椭球形比例。如果组件已经挂在猴头等造型网格上，可点击“转换为云团盒代理”，编辑器会建立匹配其包围盒的 Cube 子对象并关闭来源 Renderer；来源网格仅作为尺寸参考。

## 任意闭合网格边界

要把云限制在猴头、雕像等独立静态网格内部：

1. 将组件临时添加到来源网格，选择“边界模式 → 网格 SDF”。
2. 点击“转换为 SDF 盒代理”。
3. 编辑器会烘焙一张带符号距离场 Texture3D，创建匹配来源包围盒的 Cube 子对象，并关闭来源 Renderer。
4. 后续修改来源网格或 SDF 分辨率后，在代理组件上点击“烘焙 / 更新 SDF”。

来源网格只参与编辑器烘焙；运行时仍由 Cube 代理执行一次 Ray March，避免复杂网格的多个背面片元重复积分。SDF 大于零的区域为网格内部，“网格表面羽化”控制云从表面向内渐入的宽度。

需要保留原始造型但让边缘更像云时，使用“网格边缘变化 / 网格边缘尺度”。它们只推拉 SDF 表面附近，不替换网格的大轮廓；“网格内部变化”越低，原形体越完整，越高则内部空隙越多。将“网格边缘变化”设为 `0` 可恢复严格 SDF 轮廓。

局部造型物建议关闭“使用世界空间采样”，把“天气图影响”降至 `0`，再用“噪声缩放 / 形状阈值”控制内部块面；天空型大范围分布才适合较强的 Weather Map。

抗重复由三层共同完成：

- 基础形状：Perlin-Worley 与三个频率的 Worley FBM。
- 边缘细节：独立的低分辨率 Worley Texture3D，并由 Curl 场扰动。
- 大型分布：世界空间 Weather Map 控制覆盖率，避免整块 3D 纹理的轮廓重复。

世界空间模式建议让“基础噪声世界尺寸”接近或大于云盒主要尺寸，“天气图世界尺寸”再大 3–6 倍。基础形状重复不明显后，再用“细节频率”和“卷曲扰动”补边缘。

Shader 使用 Cube 的局部坐标 `[-0.5, 0.5]` 计算射线出入口，因此最终渲染代理必须是标准 Cube。任意造型通过烘焙后的网格 SDF 限制密度，不直接使用造型网格执行 Ray March。它不是任意开放 Plane 的表面 Shader。

## 性能调整顺序

总体成本近似与以下项目成正比：

```text
屏幕覆盖像素 × 有效观察步数 ×
(基础密度采样 + 方向光步数 + 局部光源数)
```

出现性能问题时建议依次调整：

1. 增大“精细步进距离”。
2. 降低“最大视线步数”。
3. 降低“光照步进次数”。
4. 降低“最大局部光源数”。
5. 缩小体积的屏幕覆盖面积。
6. 最后再降低 Shape / Detail Texture3D 分辨率。

纹理分辨率主要影响显存、生成时间和边缘细节，并不直接等价于每帧 Ray March 步数。

## 注意

- 当前工程主 URP Asset 已启用 Depth Texture；其他质量档若关闭 Depth Texture，云与不透明物体的交界会不正确。
- 透明物体不写入相机深度，因此与云交叉时仍可能出现透明排序问题。
- 支持 URP Additional Lights：点光源和聚光灯会按颜色、距离衰减及实时阴影参与散射。“最大局部光源数”会线性影响有效体积采样的开销；局部光源暂不额外执行云内自阴影步进。
- 生成的 Shape、Detail 与 Weather 纹理均带 Mipmap；Shader 会根据步进距离与屏幕像素足迹自动选择 LOD，减轻远处碎边和闪烁。
- 观察射线使用世界单位精细步长，并在零密度区域扩大步长。性能首先由“精细步进距离、最大视线步数、光照步进次数、屏幕覆盖面积”决定。
- “生成噪声分辨率”只影响生成时的体积纹理精度，不会每帧重新生成。
- SDF 来源必须是闭合三角网格；开口、自交或重叠壳体可能导致内外符号判断错误。
- SDF 当前面向静态 `MeshFilter`。蒙皮动画不会自动更新距离场；动态变形需要另做 GPU SDF 或每帧体素化方案。
