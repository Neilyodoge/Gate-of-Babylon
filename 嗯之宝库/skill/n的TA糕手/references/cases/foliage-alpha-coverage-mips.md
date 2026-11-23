# 蒙版叶片的 mip：保覆盖能救密度，细轮廓还要验

**一句话结论**：远景整片树冠变稀时，先用与材质一致的裁剪阈值试保留 Alpha 覆盖。如果细叶、花茎或叶间空隙决定了剪影，按轮廓另验；全图通过裁剪的比例接近时，细节仍可能消失，局部也可能变成更厚的斑块。

## 两组原图

- **制作案例，树冠**：[Ignacio Castaño，The Witness，《Computing Alpha Mipmaps》，2010-09](https://www.ludicon.com/castano/blog/articles/computing-alpha-mipmaps/)。在[普通 mip 远景](https://the-witness.net/news/wp-content/uploads/2010/09/far_nocoverage.png)中，画面中央树冠透出大量天空；[保覆盖远景](https://the-witness.net/news/wp-content/uploads/2010/09/far_coverage.png)中，同处树冠读成更连续的绿色，仍能辨认枝条。两张图视角接近但不是像素对齐的测量；作者注明这些截图使用 Alpha-to-Coverage。它们支持“远景树冠更完整”，没有控制严格裁剪或给出抗闪烁量化。
- **固定阈值的反例，草／蕨／花**：[Lisyarus《Exploring ways to mipmap alpha-tested textures》，2025-01-05，The testbed](https://lisyarus.github.io/blog/posts/exploring-ways-to-mipmap-alpha-tested-textures.html#section-the-testbed)和 [Method 6](https://lisyarus.github.io/blog/posts/exploring-ways-to-mipmap-alpha-tested-textures.html#section-method-6)。三张 128 × 128 图，均用双线性采样、裁剪阈值 0.5。对照[平均生成的 mip](https://lisyarus.github.io/blog/media/mipmap-alpha-tested-textures/grass_fern_flower_mask_mipmaps.png)与[覆盖重映射后的 mip](https://lisyarus.github.io/blog/media/mipmap-alpha-tested-textures/method6_all.png)：最右一列，普通 mip 的花完全不见，重映射后有一个斑块，但原始细茎和花瓣依然不可辨；草尖、蕨叶锯齿也没有在粗 mip 中恢复。作者指出，一些空隙被填实，同时细弱部分仍被吃掉。这是贴图图集的测试，边缘还可能受相邻格串色影响。

## 这会怎样改变选型

- Castaño 的做法以 mip 0 在目标阈值下通过裁剪的 texel 比例为基准，对粗 mip 重缩放 Alpha，尽量让比例接近。Lisyarus 的实现逐级以来自上一级的覆盖为基准；不能把其花的形状误差当作所有导入器都会产生的结果。全图比例是统计量，不能规定像素应留在哪里；粗 mip 的 texel 少且可能同值，也不总有恰好匹配的解。
- [Unity 6000.0 的 mipMapsPreserveCoverage](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/textureimporter/mipmapspreservecoverage) 会重缩放计算出的 mip Alpha，[alphaTestReferenceValue](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/textureimporter/alphatestreferencevalue) 是导入时的参考值，官方要求与 Alpha Test 的裁剪值匹配。确认材质实际裁的是这份贴图的 Alpha；如同图供多个阈值或 Alpha 还乘了其他值，先核对实际条件再决定是否共用一份保覆盖纹理。
- 降低全局材质裁剪阈值会影响包括 mip 0 在内的采样，近景也可能变厚；适合作为对照。若主要看树冠大轮廓，保覆盖通常值得先试；若单株需要读出细茎、齿或明显空隙，优先试为远档单独制作更可读的蒙版或几何。两条都是待验证的制作选择。

## 同资产验证

固定同一贴图、网格、LOD、相机路线、灯光与分辨率。先用无压缩贴图：A 普通 mip + 原裁剪值，B 保覆盖 + 同一个裁剪值，C 普通 mip + 较低裁剪值。逐档检查整冠通过裁剪的面积、关键叶尖、花茎和空隙，再跑一段后退镜头看闪烁、突然消失和剪影变粗。确认颜色、深度与阴影通道采用的 Alpha 和阈值；然后切到目标平台压缩复查。若材质用 Alpha-to-Coverage，固定 MSAA 档单独对照，别与严格裁剪合并统计。

**状态**：已查看两组公开原图和 Unity 导入文档；没有本地导入、运动 A/B、设备测量或量化变粗幅度。
