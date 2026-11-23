# 屏幕空间反射的边缘缺失：分开验命中、历史和回退

**一句话结论**：反射物出了主相机画面、或被前景藏住时，增加 SSR 步数也拿不到缺失的颜色。环境反射可以填上亮度和大致色调；如果泳池、镜面必须保住具体物体的轮廓和动态倒影，则对比额外渲染的平面反射。

## 公开对照和边界

- **同镜头原图**：[Epic《Planar Reflections》开头的泳池滑动对照](https://dev.epicgames.com/documentation/en-us/unreal-engine/planar-reflections-in-unreal-engine)。[SSR 图](https://dev.epicgames.com/community/api/documentation/image/73f75890-43ad-4609-9126-eb1dcb205f09?resizing_type=fit)中，泳池右半部能见波纹和泛亮，但大块结构倒影向近处和侧边淡掉；[平面反射图](https://dev.epicgames.com/community/api/documentation/image/c7b8a9f0-ec76-4094-8c91-6bef9bc3dd96?resizing_type=fit)里这些部位重新出现大片清楚的倒影。文章说明平面方案从反射视点再渲一次场景；它能反射主视角以外的内容，但适用于平面，捕获本身仍有视野、可见集合和分辨率的限制。
- **失效与回退的具体实现**：[AMD FidelityFX SSSR 1.5，Intersection pass](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/stochastic-screen-space-reflections/#intersection-pass)沿主相机深度层级找交点，判定命中置信度后在射线结果与环境贴图之间混合，以缓和边缘；太粗糙的像素也可在分类时直接用环境贴图。该文档的 [motion vectors](https://gpuopen.com/manuals/fidelityfx_sdk/techniques/stochastic-screen-space-reflections/#providing-motion-vectors)还把当前帧射线遍历与后续时域去噪分开：动矢量给后者做重投影，不能让本帧射线看见屏幕外的东西。
- **平面反射的成本受镜中场景控制**：Epic 的 [Kite GPU 截图](https://dev.epicgames.com/community/api/documentation/image/1b806660-1952-4453-bd69-578d0e168a1f?resizing_type=fit)显示开启后 Scene 29.19 ms、独立 PlanarReflection 23.07 ms，正文给出关闭约 31 ms、开启合计约 52 ms。[Elven Ruins GPU 截图](https://dev.epicgames.com/community/api/documentation/image/84cbe10f-f2f6-41e3-80af-a8fcf3c0e149?resizing_type=fit)是 10.86 ms 与 1.67 ms，对应正文总量约 11 → 13 ms。是两个不同内容的例子，不能用两次计时相除估同场景收益。后者内容按移动端制作，不等于截图是在移动设备跑的。降低反射分辨率也不会同比减少捕获的绘制数和三角形工作。

## 验收时怎么区分三类缺失

1. 选一块反光地面，放一个颜色醒目的标记物。让它从画面中移出，但在平面镜像基准里仍可见；控制粗糙度、曝光、视距和镜头路径，抓 SSR 原始命中、回退权重与合成。标记已不在主相机颜色／深度里时，接受屏幕空间结果缺失并测试回退；调步数、厚度或边缘衰减不能找回标记。
2. 若标记还在屏内、看得到且应在镜里出现，先看是否实际发射了射线、命中点或置信度是否异常。逐项试粗糙度阈值、射线距离与层级、深度厚度，不在一次测试里都加大；把细物漏掉与厚度过大导致的假命中分别记。若原始命中正常、合成后只在动镜头出现残影或慢半拍，则停用或重置历史对照，查动矢量、深度一致性和显露区域的历史拒绝。
3. 对比 SSR 加场景探针与近平面反射。粗糙地面或一般金属先验探针的亮度、色调和边缘过渡；实时角色或近距离反射标记必须对上时，静态探针只能近似，可在关键平面测试反射捕获。记录捕获开启前后总 GPU 帧时以及反射视点的可见集合、阴影和几何量；分辨率与场景开销分开试。镜头应包含物体跨屏边、急转和静止三段。

**状态**：已看 Epic 的同镜头对照和两张 GPU 原图，核对 AMD 实现文档；未在本地或真机复现，未获得目标设备的 SSR、探针和平面反射的同场景测量。
