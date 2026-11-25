# TA-164 · 同一粗糙度换自定义反射探针后高光突然变糊

**制作决定。** 先固定反射方向与整数 LOD，对比两张探针中同一级的亮斑。若同一级已经宽了，先修 Cubemap 的来源、导入或预滤波；若各级都一致、只在真实材质上出现差别，才检查粗糙度到 LOD 的映射、实际绑定和采样范围。生产上应让探针的生成与读取遵守同一套约定，避免用一个材质的 Smoothness 补偿资源不匹配。

## 公开实现

- **Unity 6000.0 · [自定义探针输入](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/reflectionprobe/custombakedtexture) 与 [Specular 卷积](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/textureimportercubemapconvolution/specular)。** 前者用来指定自定义反射贴图，不能凭赋值就认为纹理里的 mip 内容正确。后者是导入器公开的一种卷积方式，文档描述每级对入射光按不同的 Phong 反射波瓣积分，细 mip 较锐、粗 mip 较糊。普通缩图 mip 和镜面卷积 mip 都有层级，但角域权重和每级应代表的反射宽度不同。该说明只证明此导入选项，不等于各管线的实时或烘焙探针都用 Phong。
- **URP 6000.0 的读取端 · [BRDF.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl)、[CommonMaterial.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl)、[ImageBasedLighting.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl)。** 材质光滑度先换成感知粗糙度 `p=1−smoothness`，与 BRDF 中再平方得到的粗糙度分开。相关重载请求 `LOD=p × (1.7−0.7p) × lastMip`，未覆写时 `lastMip=6`，是最后使用的索引，不是纹理宽度或层数。普通 Cubemap 分支在 [GlobalIllumination.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl) 用 `SAMPLE_TEXTURECUBE_LOD` 读取 `unity_SpecCube0` 并解码 HDR；Forward+ 探针图集路径用探针元数据额外取 `min(mip,maxMip)`。因此要分清 Shader 请求值和该探针实际可取的级别。
- **互换资源的反例 · [three.js r180 PMREM 生成器](https://github.com/mrdoob/three.js/blob/r180/src/extras/PMREMGenerator.js) 和 [CubeUV 采样](https://github.com/mrdoob/three.js/blob/r180/src/renderers/shaders/ShaderChunk/cube_uv_reflection_fragment.glsl.js)。** 生成器在 CubeUV 二维图集里存反射级别，单面降到 16×16 后，还在相同尺寸追加更糊的几级。着色器按自己的分段粗糙度映射定位图集块，再在相邻级插值；它们不是普通 Cubemap 的原生 mip。即使两个工具都标为“预滤波环境图”，也要核对级别与布局后才能互换。

## 数值锚点与验收

设 URP 感知粗糙度为 0.5、最后一级为 6，着色器请求 LOD 4.05；若自行写成 `0.5×6`，会请求 LOD 3。在实际读值体现何种模糊，还取决于 mip 内容、采样器和可用级别。4.05 是运行时映射的算例，不是某张探针亮斑宽度的测量。

1. 取同一份线性 HDR 环境，只留一个远离立方体面边界的小亮点。锁方向、探针权重、曝光和解码；同尺寸、同格式做普通缩图和镜面卷积两份 Cubemap，先分别抓整数 mip 0 到 6。比较角域亮斑宽度与线性峰值，避免压缩、曝光或色调映射把亮度差伪装成模糊。mip 0 已不同就先检查输入、朝向、导入分辨率和解码；只有粗层不同才查该层如何生成。若只在跨面时有硬缝，另见 [TA-139](cubemap-rough-mip-face-seams.md)。
2. 再在非金属和金属球上扫 Smoothness，关闭法线贴图、直射灯、额外反射层和探针混合，先单独验环境反射。记录实际绑定资源、着色变体、请求 LOD、级别夹取和采样器，再与显式 LOD 图比较。只在探针切换或盒投影路径变宽时，另查反射方向或多个探针的混合，避免错归因给 mips。
3. 能控制导入时优先用与目标读取路径配套的卷积产物。不兼容的外部图集先从原始 HDR 环境重做，或显式配好它自己的采样分支；已经按正确约定预滤过的输入，先确认导入有没有覆盖其级别，再决定是否重新卷积。最终要在目标格式和平台上复核分级、亮度、成本及粗糙度过渡。

**验证状态。** 已核对 Unity 6000.0 API、URP/Core 源码与 three.js r180；LOD 是算例，未生成同源 Cubemap 对照、引擎抓帧或真机验证。
