# 三平面材质：分开验纹理接缝、法线和采样

**一句话结论**：没有好 UV 的静态岩石，可先把可平铺的底色改成同一坐标系的三平面投影，观察原 UV 缝和值得保留的岩纹。若缝只在打光后出现，先查投影法线的方向和混合方式；移动端再按实际纹理组决定哪些数据值得投影。

## 公开对照

- **投影拉伸与重叠**：[Jasper Flick / Catlike Coding《Triplanar Mapping》§2.1–2.4](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/#2.1)（2018，Unity 2017.4.1f1）。同一组测试几何里，[单平面 XY](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/texturing-with-three-planes/xy.jpg)把左盒的顶面和侧面拉成色条；[按法线权重混合三轴](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/texturing-with-three-planes/blending.jpg)后这些面可读到格子，但斜盒和球上能看到多个数字叠在一起。这里对比的是单平面与三平面，未拿同一岩石的手工 UV 做 A/B。用几何法线的各轴绝对值做权重并令总和为 1；提高同一指数可以收窄过渡，但当三轴绝对值相同，归一化后仍各占 1/3。
- **法线与底色要分开验**：[同文 §3.2–3.3](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/#3.2)的[错误法线混合](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/other-surface-properties/normal-blend-incorrect.jpg)和[修正混合](https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/other-surface-properties/normal-blend-correct.jpg)使用法线调试色，右盒近侧面从粉色变成黄褐色。实现先让每轴的贴图法线与对应的投影基匹配，镜像 UV 的同时修正该轴的切向分量；通过 whiteout 混合在该轴投影空间保留几何朝向，再转回共同空间、加权、归一化。原例的负轴错误来自在 whiteout 的 Z 乘法中同时翻了两边的符号，造成朝向翻回；不能把三个切线空间解码结果当成同一空间直接平均。
- **少取一轴的代价**：[Inigo Quilez《Biplanar Mapping》](https://iquilezles.org/articles/biplanar/)（2020）只取最对齐的两轴，在选轴前求位置导数，再向纹理采样提供各轴对应的显式梯度来规避选轴突变产生的 mip 线。实际看了象模型的[双平面重映射](https://iquilezles.org/articles/biplanar/gfx04.jpg)与[三平面 k=8](https://iquilezles.org/articles/biplanar/gfx05.jpg)：象头红、绿、蓝汇合处，双平面仍有更尖的切换。作者用颜色展示权重，没有给出完整 PBR 材质或目标设备计时；显式梯度处理的是 mip 跳变，并不能消除权重选轴本身的边界。

## 怎么选和怎么测

1. 用同一岩石、同一贴图比例、同一镜头对照原 UV、三平面底色和完整三平面。先看底色、再看世界空间法线，最后用低粗糙度和掠射光看高光；分别检查旧 UV 岛边、近对角朝向的三轴交界和真实几何硬边。平法线输入应在正负各轴和斜面都还原几何法线，这是独立于材质纹样的验收。静态模块用同一世界坐标、贴图、尺度和混合参数，有机会跨模型延续纹样；接边顶点法线不连续时权重也会断。世界投影在移动件上可能显得贴图滑动，可再验物体空间版本；已有针对 UV 烘焙的法线或 AO 仍需保留原映射。
2. 按有效采样组数先算：底色、法线、合包的线性遮罩若都从普通 UV 各取一次，是 **3 次**；全部三平面是 **9 次**；只把底色改三平面，保留另外两组 UV，则是 **5 次**。最后一组只适合已有可用 UV，且底色不需要和其他图的具体纹样对齐的材质。三个投影可读取同一张纹理，取样次数增加不等于三份资源。看实际编译和抓帧：无条件采样时，把两轴权重压到零仍不会自动删掉读图；带宽、mip 与 GPU 时间也不能由采样数直接相乘。
3. 在实际机位让岩石从占画面很小到占据大半，以相同分辨率、光照、材质用途和覆盖对照已通过画质的候选。先试减少投影的纹理组、合并可共用坐标和 mip 的遮罩，再考虑换双平面；若用后者，重点复查斜面和远景的细线。记录不透明材质通道与整帧 GPU 时间，几何、光照及分辨率变化单列。

**状态**：已看公开静态原图并核对两个公开实现；3/5/9 是材质采样算例，尚无同岩石本地复现、真机 GPU 计时或带宽测量。
