# TA-38 · 纹理数组和图集：纹理绑定、绘制次数与 mip 分开验

**制作决定**：贴图同尺寸、同格式且需要独立 mip 或块内重复时，优先试数组；尺寸相差大、可给各区域做 mip 隔离时，试图集或按尺寸分组。想减少绘制，要另外做到真正共用材质，并把数组切片编号或图集 UV 矩形做成每实例或合并网格的数据。只让材质引用同一纹理资源，无法保证现有的绘制合并。

## 公共实现和可见证据

- **一份数组并不等于每像素只采样一次。** [Jasper Flick《Hex Map 14》§2.3–3](https://catlikecoding.com/unity/tutorials/hex-map/part-14/#2.3) 在旧 Unity 示例中把沙、草、泥、石和雪复制为一张数组；同一地形三角形三个顶点保存相同的三个地表编号，权重另插值，Shader 从 `texcoord2` 读取编号，再做三次纹理采样和加权混合。[作者原图](https://catlikecoding.com/unity/tutorials/hex-map/part-14/selecting-textures/textured-terrain.png)中沙地、草和泥可在相邻的六边格边上渐变。这说明一个资源绑定可承载多个地表及其混合，作者没有对照图集，也没给出地形绘制数或设备采样耗时。
- **同一材质与每实例编号是两层条件。** [Catlike Coding 的实例颜色对照](srp-batcher-per-object-properties.md)中，同网格用四个材质是[四次实例绘制](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/instanced-draw-calls.png)，共享一个材质、让实例数据保留差异后是[一次](https://catlikecoding.com/unity/tutorials/custom-srp/draw-calls/batching/one-instanced-material.png)。这是颜色对照，支持检查材质分组，不能当作数组相较图集的实测。[Unity 6000.0 Built-in 的每实例属性](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/birp/gpu-instancing-shader/per-instance-properties)明确将声明为实例属性的数据从 MPB 汇入实例绘制，非实例属性会阻止这条路径。候选实现是把同一个数组固定在共享材质上、只变数字切片；图集固定绑定、只变 UV 矩形也可以同样做。常量换成每个 Renderer 的新材质或直接给实例覆盖纹理，会重新引入分组变量。
- **资源生成和发布边界。** [Unity 6000.0 Texture2DArray](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/texture2darray)要求切片同宽高、同格式；要核对 sRGB/线性、压缩格式和 mip 链，并检查目标 Player 的 `supports2DArrayTextures` 与 [maxTextureArraySlices](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/systeminfo/maxtexturearrayslices)。文档不支持以 Crunch 格式创建数组。[Graphics.CopyTexture](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/graphics/copytexture)还给出源 Texture2D 和目标数组受不同 mip 限制时的区域复制示例；编辑器中能把 mip 逐级拷过去，不能保证降低画质档后仍拷到预期层。生成后核对实际加载的 mip 尺寸和内容，缺失的高分辨率数据不能靠改坐标补回。
- **数组不混隔壁层；图集要管跨区取样和 mip 生成两步。** 直接用干净原图生成数组时，单次 2D 过滤只在当前切片内进行。从已有串色的 mip 复制到数组不能恢复源内容。图集在远处看到邻块颜色时，用[已有的 0 FPS 原图和诊断顺序](flipbook-atlas-mip-bleeding.md)区分滤波跨格与 mip 已混色，再考虑隔离带和逐块预滤波。重复 UV 的地表不要直接复用图集最外圈的 Clamp 解决局部格边。

## 先估尺寸，再做同材质对照

- 假设有 12 张 256×256 和 4 张 1024×1024，统一到 1024×1024 塞进一张数组有 16,777,216 个基础层纹素；原尺寸合计 4,980,736，约 **3.37 倍**。如果两档都需要保留视觉尺寸，先试按尺寸分数组或打图集。这个比值只比较名义基础层纹素数，没计入图集空隙、隔离带、mip、压缩块或实际 GPU 分配；分数组还要检查是否因资源不同产生额外材质分组。
- 固定同一网格、Shader 主 Pass、相机和材质设置。对照独立材质贴图、共享图集且 UV 矩形随实例、共享数组且切片编号随实例；再故意给数组版本拆多个材质做负对照。抓 Frame Debugger 中的实际 draw、实例数、各次绑定和 Pass；SRP Batch 的条目数不能等同于实例 draw。先验不透明，再单测透明排序、阴影等会拆批的情况。CPU 提交、GPU 阶段耗时和纹理驻留量分开记。
- 用对比色相邻区域，在 mip 0、出错的粗 mip、远处斜视和不同质量档重新采样。验切片编号/UV 是否正确，边缘是否串色，法线及罩贴图导入是否仍是线性数据。比较在**同画质**下的显存、提交和 GPU 时间，避免以糊一级纹理来换用量优势。

**状态**：已核对公开地形实现、实例颜色抓帧和 Unity API；内存比是基础层算例，尚无同一场景数组与图集的本地或真机 A/B。
