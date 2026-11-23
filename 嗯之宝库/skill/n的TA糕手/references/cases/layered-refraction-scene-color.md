# TA-50 · 两层折射叠起来：先查颜色拷贝，再查深度和混合

**制作决定**：后层水幕单独可见、重叠才消失时，先确认后层有通过深度测试、也确实被写入前层读取的颜色纹理。若前层仍读透明绘制前的快照，调排序只能改谁覆盖谁；若两层必须依次扭曲，再考虑在两组之间更新颜色。只在前景轮廓附近出现拖出的颜色，则单独查偏移点的深度和颜色采样足迹。

## 公开证据

- **Unity URP `6000.0/staging`。** [兼容路径的拷贝与透明事件](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs#L338-L347)里，`CopyColorPass` 在天空盒后，默认透明绘制在其后。[Render Graph 对应路径](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererRenderGraph.cs#L1375-L1412)也可在天空盒后将 `activeColor` 拷至 `cameraOpaqueTexture`，再绘制默认透明物。两个默认透明物读同一张颜色快照时，后画的不会自动采到先画的；自定义渲染事件、深度拷贝时机以及下采样设置按实际抓帧核对。
- **制作经验 · Tiago Sousa（Crytek），[GPU Gems 2 第 19 章](https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-19-generic-refraction-simulation) §19.2、§19.4。** 单张背景跳过折射物，作者明确指出不同颜色的折射面重叠会失真；从后到前逐次更新折射图可以让后层参与前层的输入，仅排序配 Alpha 混合是较粗的近似。§19.2 的屏幕 Alpha 遮罩是在偏移落出有效折射区域时回用原坐标，和直接比较线性深度是不同实现；两者都拿不到被遮住、未进入快照的颜色。
- **原图 A/B · Catlike Coding，Unity 2017.4.4f1，[Only Refract Underwater](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/#3.3)。** [直接偏移](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/fake-refraction/refracted-foreground.jpg)时，黄色杆边和左侧红球边缘被拉出明显的锯齿色带；[测试深度并回退](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/fake-refraction/filtered-offset.jpg)后大块错色退掉，黄杆旁仍可见细线。作者用偏移位置的背景线性深度减去表面深度，小于零则回用原 UV；雾计算需要在回退后更新深度。文中还区分颜色过滤混入边缘：点采样或对齐纹素中心可做诊断，其 Built-in 示例也有 MSAA 与目标尺寸限制，不能照搬为 URP 的通用开关。

## 算例：为什么看见后层颜色不等于正确叠折射

用线性背景 `S(x)`，忽略着色、遮挡与边界。后层偏移 +2 px，近层偏移 +3 px。近层直接用同一份快照替换时输出 `S(x+3)`；若近层读的是后层画完后的拷贝，才会在本例中得到 `S(x+5)`。普通 Alpha 混合可保留当前像素的后层颜色，但不会把该层按近层偏移再取样。这个一维算例只解释输入差异，不能算真实光路或跨屏效果。

## 最小对照

1. 固定相机、曝光、分辨率、材质偏移和排序，放互相重叠的远近两片水幕，远层叠一条亮色标记，背景用棋盘，前景用一根窄柱。抓透明前的源颜色、远层绘制后目标、近层实际绑定的源，以及最终颜色。远层根本没画出来就查队列、深度写入与测试；画出来但没进近层的源，就对照混合近似与「先绘远层 → 拷到独立颜色纹理 → 再绘近层」。不要边写同一个目标边直接读它；分组只有在目标镜头的前后关系稳定时才好用，交叉折射面不能靠一个对象顺序排对所有像素。
2. 只留一层，增加偏移让它擦过窄柱。对照无深度拒绝、拒绝后回原 UV，以及拒绝加纹素对齐或缩小采样范围，逐项开关。用同一台相机同一帧的可比较深度，检查偏移 UV 是否出有效视口、颜色和深度是否同尺寸／同位置；降低分辨率或开启 MSAA 后再验细边。只在过滤后还有前景颜色时，优先查颜色过滤足迹，点深度的判断通过仍可能混到邻近前景。
3. 两层重叠少、只需大致看见后层标记时，先评估单次背景拷贝和从后到前混合。重叠是关键画面才测试分层拷贝，分开量颜色拷贝、透明 pass、总 GPU 帧时和边缘误采；再比较全尺寸、区域或较低分辨率拷贝，不能用拷贝次数直接算收益。

**状态**：核对 URP 两条路径的源码、Crytek 原文及 Catlike 两张静态原图；叠层算例与 A/B 为推导和待做方案，未在本地或真机验证。
