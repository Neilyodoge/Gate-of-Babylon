# TA-35 · 发片各向异性高光：走向、位置和宽度分开查

**制作决定**：转镜头时如果整片的亮带走向跨错发束，先可视化发丝切向并核对头尾、UV 和镜像约定。方向对了再分开试高光偏移和宽度。能用统一 UV 表达根尖方向的短发片先用该方向；贴图里的弯曲发丝与几何切向不一致时，再在需要的发片上试流向贴图。

## 公开案例和实现

- **资产做法 · Epic UE4.27**：[Photorealistic Character · Tangent](https://dev.epicgames.com/documentation/unreal-engine/photorealistic-character?application_version=4.27#tangent) 的短发角色把发根放到贴图上方、发梢在下方，运行时自动取沿发丝的切向。文档描述其 Tangent 指向发根，并用单根发丝的 ID 给切向 Z 分量加固定随机变化。这是该示例里的切向变化，不是下面 HDRP 两道高光各自的 Shift。Epic 建议在同图集有不同发丝朝向、或贴图卷曲超出几何能表达的情况，使用切线空间的流向贴图。
- **静态原图 · Sparrow**：[Flow Maps for Tangency 并排滑块](https://dev.epicgames.com/documentation/unreal-engine/photorealistic-character?application_version=4.27#flowmapsfortangency) 左侧最终效果可见头顶和辫结的亮带，右侧可见沿曲发片变化的浅蓝紫流向图。文档说明 Flow Map 只用于一部分发片。这两张是最终渲染与输入可视化，不能从图中量化开关贴图的差异，也未展示连续转镜头。
- **可定位实现 · Unity 2022.3 HDRP Kajiya–Kay**：[Hair.hlsl 的材质参数转换](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Hair/Hair.hlsl#L258-L276) 把 `hairStrandDirectionWS` 注释为根到梢；主、次高光各取偏移值，粗糙度另转成各自的高光指数。[直接光的 BSDF](https://github.com/Unity-Technologies/Graphics/blob/2022.3/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Hair/Hair.hlsl#L627-L661) 用同一个发丝方向和表面法线计算两个偏移后的切向，再分别用各自的指数评估高光。换引擎时按当前材质代码验向量方向和空间；Epic 示例的向根约定不能直接照搬到 HDRP。这段源码也不代表 HDRP 的其他毛发模型或环境光路径。

## 最小对照：先看方向，再看亮带和覆盖

1. 固定一盏直射光、曝光、镜头轨迹与材质。准备同一发片的正常 UV、镜像 UV、贴图内有弯曲的区域，显示 Shader 最后实际使用的发丝方向；用带箭头的线或带符号的颜色检查根尖、U/V、镜像和双面。并排看几何固定切向与接入流向贴图，先用近景判断亮带是否跨错发束。若只有镜像片出错，先查切线基和镜像符号。
2. 暂时只留一道直接高光并固定宽度。先固定偏移，再对照原偏移；项目若将贴图接到 Shift，额外对照常数输入，检查局部条纹是否跟着偏移纹理走。方向和位置正确后才单独加宽高光，对比峰值、亮带宽度和镜头慢转时的稳定性。最终恢复两道高光，确认变化不是两道亮带互相覆盖造成的。
3. 单片稳定、叠片或远景才跳时，先看关闭高光后覆盖率是否也在跳。用单片固定遮罩与原来的叠层/抖动路径做对照，再根据结果查方向贴图的 mip、高光欠采样或遮罩覆盖。[Epic 的发片透明度段](https://dev.epicgames.com/documentation/unreal-engine/photorealistic-character?application_version=4.27#hairandtransparency)另描述了 Masked 配合时间抖动的做法；不能仅凭亮带抖就断定切向错。要推广到移动端，记录实际流向/偏移增加的采样和目标设备的 GPU 耗时。

**状态**：已核对 Epic 的资产说明与静态原图、HDRP 的直接光源码；未查得同发片流向开关的动态 A/B，未在本地或目标设备运行、计时。
