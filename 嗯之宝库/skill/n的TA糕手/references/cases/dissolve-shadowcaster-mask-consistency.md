# TA-145 · 材质溶解时阴影仍是完整轮廓

**制作决定。** 大块区域在主画面已经消失、同一个实时投影物仍在阴影图里写完整轮廓，先查实际执行的 ShadowCaster 是否有溶解变体，并核对遮罩、坐标和阈值。阴影图已经挖孔、接收面却仍近似一整块时，再查阴影分辨率、过滤、偏移和其他投影来源。

## 公开证据

- **原始前后图。** Jasper Flick 的 [Rendering 12 · Cutout Shadows](https://catlikecoding.com/unity/tutorials/rendering/part-12/#1)（2017-01-31，Unity 5.5.0f3 Built-in）第一组方向光原图中，实心红板和已裁成碎片的红板仍留下相同的外框投影；聚光灯原图的碎片也投出实心四边形。[第 1.2 节](https://catlikecoding.com/unity/tutorials/rendering/part-12/#1.2) 为 ShadowCaster 添加 UV、材质 Alpha、裁剪及对应变体后，两种灯的原图里都出现与碎片对应的空隙。这个观察是 Cutout 的前后对照，不是对 URP 或自定义溶解的实测。
- **URP 6000.0：同一裁剪助手不代表每个 Pass 采到同一值。** [Lit.shader](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader) 分别在 ForwardLit 和 ShadowCaster 声明 `_ALPHATEST_ON`；[ShadowCasterPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl) 只在该分支传基础 UV 并以 `_BaseMap.a`、`_BaseColor`、`_Cutoff` 调 `Alpha`。[LitInput.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl) 的表面也用这个助手；但 [LitForwardPass.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl) 在 `_PARALLAXMAP` 下先改 UV。另一个例外是 [SurfaceInput.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl) 在使用基础贴图 Alpha 存平滑度的分支，以颜色 Alpha 代替贴图 Alpha。实际取样和当前材质变体都要查。
- **复用 Pass 的条件。** [Unity 6000.0 的 UsePass 文档](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/reference/sl-reference/sl-sub-shader-object/sl-use-pass) 说明它插入指定 Shader 的命名 Pass；自定义颜色 Pass 对另一张噪声图做的 `clip` 不会因此加进被复用的阴影 Pass。上述 URP 的 `ApplyShadowBias` 则作用于投影位置，不能代替自定义溶解裁剪。

## 最小算例和验收

固定同一表面三处采样，遮罩分别为 `0.20、0.55、0.80`，以 `clip(mask - threshold)` 判断保留。前向阈值 0.60，只留最后一处；阴影没做溶解时写三处，走旧阈值 0.50 时写后两处，匹配 0.60 才留最后一处。这是同坐标算例，不能换算成真实阴影覆盖率或预计 GPU 时间。

1. 用独立的实时灯和易辨识的大洞做对照；先切换该物体 Cast Shadows，确认异常确实来自它。固定灯、镜头、动画参数、材质和阴影档，比较 A 只裁颜色、B 颜色和阴影调用同一裁剪逻辑。抓帧查看实际 ShadowCaster、关键字、纹理绑定和阴影图，再看接收面；若编辑器有洞、Player 没洞，核对 Player 的阴影变体和回退 Pass。另用聚光灯与方向光各验一次。
2. 需要稳定一致时优先共用对象空间坐标或约定好的 UV、遮罩和当帧参数；相机屏幕坐标或依赖视角的偏移不能原样用于光源投影。阴影只计算裁剪用的最小输入，不带溶解描边或发光。大洞先验规则，再用细边对照两档阴影分辨率和硬／软过滤；确认阴影图孔洞存在后才调偏移，分别记录接收面成像和额外 ShadowCaster 时间。

**验证状态。** 已查看 Catlike Coding 前后原图、核对 URP 6000.0 源码与 Unity 文档，并复算阈值算例；未在本地抓帧或在真机测影子细节和 Pass 耗时。
