# TA-147 · 大三角面和密网格的距离雾在接缝处对不上

**制作决定。** 同材质的粗网格和密网格只在开雾时出现接缝，先输出雾输入与最终雾权重，确定是度量不同，还是在顶点先算了非线性结果再插值。若是后者，优先插值深度或位置，把非线性曲线留到片元；只有确认该路径在目标机上太贵，才考虑给少数长边加分段或降低雾曲线变化率。

## 公开证据

- **深度定义自己就能制造接缝。** Jasper Flick 的 [Rendering 14 · Forward Fog，第 1.5–1.6 节](https://catlikecoding.com/unity/tutorials/rendering/part-14/#1.6)（Unity 5.5.0f3 Built-in）同镜头原图的白地面上，一侧雾界是曲线，另一侧近乎水平直线，二者交接错开。自定义侧每片元计算相机到表面的距离，标准侧用深度坐标；把雾起止压得很近只是放大差异。这个原图证实换度量就会错边，不能当成同材质疏密网格的实测。
- **看插值量，别只看哪里写了 Fog。** 微软 [Direct3D 9 Vertex Fog](https://learn.microsoft.com/en-us/windows/win32/direct3d9/vertex-fog) 记录了逐顶点算完雾、光栅化插值结果的旧路径。与此不同，three.js r180 的 [fog_vertex](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/fog_vertex.glsl.js) 只写 `-mvPosition.z`，[fog_fragment](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/fog_fragment.glsl.js) 再做指数平方或 `smoothstep`。两者都经过顶点阶段，只有第一种在光栅化时插值最终雾权重。
- **URP 6000.0 的实际分支。** [ShaderVariablesFunctions.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl) 强制定义 `_FOG_FRAGMENT`，`InitializeInputDataFog` 用视图 Z 减近裁面后下限夹为零，[LitForwardPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl) 调它并用 `MixFog`。即使自定义路径沿用旧式顶点 `ComputeFogFactor`，EXP／EXP2 在这个助手里传的是 `density × z` 一类中间项，`ComputeFogIntensity` 才取指数；线性模式则会在生成因子时夹值。先查看项目变体和混合输入，不要凭函数名断定是顶点插值最终指数。

## 中点算例和验收

固定正交相机，让两片共面的网格沿同一边界相接，边界两端的雾深度为 0 和 40 m。粗边只有端点，密边有 20 m 中点。以物体保留比例 `T = exp(−0.05d)` 表示雾；T 越低，雾越浓。

| 同一个 20 m 点 | 计算方式 | T |
| --- | --- | ---: |
| 粗网格 | 先在两端算 T，再各取一半 | 0.5677 |
| 密网格的中点顶点，或任一网格逐片元算雾 | 先取得 20 m，再算 T | 0.3679 |

两侧可差约 0.200 的物体权重。即使叫线性雾，若在顶点就夹到 0～1，跨过起止区间也会破坏线性：起止 10／50 m 时，端点权重 1 和 0.25 的平均是 0.625，20 m 真实权重为 0.75。若两端都在同一未夹值的线性段且深度定义一致，这一类顺序误差才消失。

1. 同一纯色材质、相机、分辨率和曝光，先关雾排几何或光照接缝；再分别可视化 `d` 和最终 `T`。对照切换粗／密边、片元权重、雾函数及视图 Z／径向距离，每次只改一项。只换网格就改变 T，优先查插值；换视角时整条等雾线一起改形，先核两侧是不是取了不同的距离。后处理从深度图取值时先按投影和 API 还原深度，并单验不写入该深度的透明层。
2. 用实际机位和雾变化最陡处检查缝的宽度与像素权重。对逐片元方案测覆盖量大的镜头；若改网格分段就另外量顶点量、LOD 切换和接缝稳定性。

**验证状态。** 已查看 Catlike 的距离定义对照原图、核对 Direct3D／URP／three.js 实现，复算指数及夹值线性中点。未做同材质疏密网格的引擎 A/B 或真机计时。
