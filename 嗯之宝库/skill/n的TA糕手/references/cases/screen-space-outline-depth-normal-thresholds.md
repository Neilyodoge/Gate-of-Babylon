# TA-39 · 屏幕空间描边：远景缺线与斜面误描分开查

**制作决定**：先输出深度和法线的两张边缘遮罩。远景中两个表面有前后关系却缺分界，先核深度编码和随距离阈值；台阶折角的深度差小但法线会变，留给法线遮罩补线；同一块斜平面被画成白块时，检查视角造成的连续深度梯度，优先只提高掠射位置的深度阈值。

## 同一构图的公开对照

- **距离修正会带来新的误描。** [Roystan《Unity Outline Shader》，Unity 2018.3，§1–3](https://roystan.net/articles/outline-shader/)用四个对角样本做 Roberts Cross。作者直接采非线性深度，先用常量判断。在[固定阈值](https://roystan.net/media/tutorials/outline-threshold.png)原图，右上两远方块粘成一条外轮廓；[按该项目深度缩放阈值](https://roystan.net/media/tutorials/outline-depth-threshold.png)后两块分界出现，但盒子顶面和几段楼梯亮面更宽。原文的缩放适用于这个深度编码，不直接复制旧的参数到线性深度、其他投影或渲染管线。
- **法线折角和掠射抑制解决不同的线。** [仅法线](https://roystan.net/media/tutorials/outline-normals.png)里楼梯有更多台阶轮廓，但部分线仍断续；作者取两张二值遮罩的最大值，[混合图](https://roystan.net/media/tutorials/outline-combined.png)仍可见整块发白的盒子顶面。文章报告全局抬高深度阈值会吞掉壶口和部分台阶，于是改为按表面法线与观察方向的角度，只在掠射面提高深度阈值。[改完原图](https://roystan.net/media/tutorials/outline-artifact-removed.png)里前景顶面的白块退成边线，楼梯仍大多有线。这是同一静态构图的对照，未量化薄轮廓或镜头运动时的代价。

## 当前管线要另验的边界

- **深度单位改变阈值意义。** 常规透视投影下，设备深度可写成常量加 `b/z`；同为 1 m 的深度落差，2→3 m 与 50→51 m 相比，前者的设备深度差约为后者的 **425 倍**。这是固定投影的算例，不包含量化和景深；倒转 Z 会换符号，不改变该差值之比。若以眼空间正深度重写，先以米为单位做深度差，再按需求加入距离和角度阈值；斜面在眼空间依然可能有较大的邻像素深度差。新实现同一坐标空间归一化法线和观察方向，正交相机另验观察方向与深度转换。
- **合成顺序不等于输入覆盖。** [URP 6000.0 全屏 Pass 文档](https://docs.unity.com/en-us/engine/6000.0/manual/post-processing-and-full-screen-effects/urp/custom-post-processing/renderer-feature-full-screen-pass)允许在透明之前、透明之后且后处理之前，或后处理之后执行。[FullScreenPassRendererFeature.cs](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs#L264-L297)按需求声明相机深度/法线纹理；[Render Graph 预通道判定](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L1701-L1714)将法线需求作为预通道条件之一。即使交换描边和透明的画色次序，缺在相机深度或法线里的透明对象也不会自动成为输入。特定透明物必须有线时，单测掩码或单独的深度/轮廓路径。

## 同一镜头怎样验收

1. 放一对前后错开的远方块、一个斜平面、几个台阶和一个细环。固定分辨率与镜头，先看深度差、法线差和各自二值结果，再看组合；只在斜平面误描时，试掠射位置的阈值，同时检查细环和近景剪影没有被抹掉。共面且同法线的对象分界不在这两种信号里；需要分开时，给目标对象独立 ID/遮罩，别缩小到全屏误描的阈值。
2. 固定最终输出，比较高低 Render Scale，用被读取的纹理尺寸换算采样跨度。若深度缓冲随内部比例缩小，每个内部采样步在输出中覆盖的范围会变；0.5 倍比例时一个内部像素在几何上相当于两个输出像素，最终线宽还受重建与滤波影响。锁定机位先测宽度，再转动镜头看断续；同时标记描边在透明、色调/Bloom 与升采样的前后。
3. 以同样输入测纯描边阶段成本；另做从只读深度到同时请求法线的对照，抓帧确认实际法线生产、颜色拷贝和合成事件。只记录剥离后真正新增的 GPU 阶段和整帧时，不把所有前置成本都算成几次边缘采样。

**状态**：已核对 Roystan 五张静态原图与原文、Unity 6000.0 的全屏 Feature 和 Render Graph 源码；深度差和采样跨度是算例，未在本地或真机复现、计时。
