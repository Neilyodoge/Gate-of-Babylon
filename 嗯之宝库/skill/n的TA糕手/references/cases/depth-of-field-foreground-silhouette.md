# TA-65 · 屏幕空间景深的前景轮廓

**制作决定。** 前景内部已经糊、轮廓还是硬的，先抓弥散圆（CoC）和前景覆盖，确认模糊有没有扩到原轮廓外。覆盖已扩出但焦内背景有脏边时，再查颜色下采样、遮挡补洞与合成；不要用更大半径同时追这两个症状。

## 证据

- **制作原图：Infinity Ward《使命召唤 4》。** Earl Hammon Jr. 在 [GPU Gems 3 第 28 章 §28.4](https://developer.nvidia.com/gpugems/gpugems3/part-iv-image-effects/chapter-28-practical-post-process-depth-field)的[图 28-2](https://developer.download.nvidia.com/books/gpugems3/28fig02.jpg)对比同一角色。右上脸部已糊，头盔的外缘仍贴着清晰版的剪影；作者说这档用了 33 个采样点和每像素 66 次纹理读取。左下增加样点后，头盔与衣领的边界仍偏硬；右下把前景 CoC 向边界外扩，头盔缘和肩部的过渡才变软。做法用每边四分之一分辨率的原始和模糊 CoC（在直边附近估更大的邻域 CoC），再小幅平滑拐角。代价见 §28.7 [图 28-8](https://developer.download.nvidia.com/books/gpugems3/28fig08.jpg)：右格的半径是中格两倍，靠近前景轮廓的背景石纹也被抹开。
- **现代公开实现。** [FidelityFX DoF 1.1](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/depth-of-field/)在半分辨率模糊前做 CoC 双边下采样，并[扩张瓦片范围](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/depth-of-field/#tile-map-dilation)，判断哪些邻块的前景能覆盖当前位置；近景输出颜色和覆盖率后再合成。[Hole filling](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/depth-of-field/#hole-filling)从周围背景估算被前景藏住的部分。单张相机颜色图没有这些被遮挡像素，估算会受背景纹理和薄物体尺寸限制，不能把这种漏色一概归为采样不够多。
- **Unity 6000.0 URP 的模式差异。** [GaussianDepthOfField.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/GaussianDepthOfField.shader#L64-L73)由 Far Start/End 算 CoC，近于开始距离的值被截到零。[BokehDepthOfField.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/BokehDepthOfField.shader)的 CoC 有近、远两侧；近景按邻样本的 CoC 与距离加权并累计覆盖。URP 的 Gaussian 档若只做远景，调远景半径不会得到前景扩散；需要近景虚化才对照 Bokeh 或单独一层前景。

## 用法与验证

1. **先比覆盖。** 用红色近景挡板与蓝色焦内背景，固定相机、曝光和分辨率。例：红色样本在全分辨率 `x = −1`、半径 4 px；蓝色输出像素在 `x = 2`、自身半径 0。两者相差 3 px，红色本可覆盖蓝色；只按输出像素自身半径取样就永远找不到红色。布置真实一根细枝、一个厚挡板和一条斜边，记录原始 CoC、扩张后的范围、轮廓外的近景覆盖；先看遮罩，再看颜色。
2. **再比漏色。** 固定相同的全分辨率等效半径，只切近远层的取样或降采样合成（半分辨率相邻样点在原图相隔 2 px）。检查轮廓外的颜色过渡、应清晰的背景纹理和相机缓移时边缘爬动。把同一形状换成透明材质时，先确认它是否出现在 DoF 读取的相机深度。若遮住的背景细节对画面很重要，先限制最大近景半径、调整构图，或评估把前景和背景单独渲染再合成的成本。
3. 低档只需远景虚化时先保留远景路径。做近远两层时，把下采样、模糊与合成分别计时，并检查开启前景后画面净收益。Infinity Ward 的 1–1.5 ms 是其 1024×768、Radeon X1900 / GeForce 7900 的历史记录，不能用于估算当前移动端。

**验证状态。** 已目视 GPU Gems 图 28-2 和 28-8，核对 FidelityFX、URP 6000.0 实现并做像素距离算例。未做本地单变量、运动或真机对照。
