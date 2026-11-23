# 透明粒子排序：先分清粒子、整批和逐像素穿插

**一句话结论**：同一系统里的烟片互盖出错，先对照系统内排序；两团各自正常、相遇才跳层，查系统之间的绘制顺序。两张柔边透明面相交时，如果左右像素的前后关系相反，一个整批顺序最多能修好一侧。

## 公开证据

- **原图观察**：[Catlike Coding《Particles》§1.3](https://catlikecoding.com/unity/tutorials/custom-srp/particles/#1.3)（示例从 Unity 2019.4.14f1 升至 2022.3.5f1）。[未按距离排序](https://catlikecoding.com/unity/tutorials/custom-srp/particles/unlit-particles/vertex-colors-without-sorting.png)和[按距离排序](https://catlikecoding.com/unity/tutorials/custom-srp/particles/unlit-particles/vertex-colors-with-sorting.png)中，黑白粒子在重叠位置的叠盖关系发生变化。作者还提醒视点变化可能导致顺序突换；已看的两张静态图不能验证突换发生在哪一帧。
- **版本文档**：[Unity 6.6 Manual，Particle System Renderer](https://docs.unity3d.com/Manual/PartSysRendererModule.html)。`Sort Mode` 只在系统内排粒子；`By Distance` 看相机距离，相机位置不变、原地旋转时不会因旋转本身改序；`By Depth` 看相机近平面深度，原地旋转也可能改序。`Sorting Fudge` 只偏置整个系统与其他透明物体（包括其他粒子系统）的相对排序，较低值更倾向于显示在它们前面，不能用它改内部顺序。在目标 Unity 版本及实际 Shader 的渲染队列内确认。
- **原始反例**：[Khronos OpenGL Wiki，Depth Sorting](https://wikis.khronos.org/opengl/Transparency_Sorting#Depth_Sorting)的[三面循环遮挡图](https://wikis.khronos.org/opengl/File:Triple_overlap.png)可见红盖绿、绿盖蓝、蓝盖红；调三个面的中心距离或固定优先级，无法同时满足三个重叠处。同一问题也可由两片交叉面构造：黑底上两片 50% Alpha 的红、蓝面，按标准 source-over 和线性 RGB 计算，红在前得 `(0.50,0,0.25)`，蓝在前得 `(0.25,0,0.50)`。若左侧红近而右侧蓝近，固定顺序必有一侧不对。此处是算例，未做渲染采样。

## 排查与选型

1. 冻结发射和随机种子，放两张不同色的柔边粒子，保持位置、材质和相机一致。放在同一系统时逐组对照 `None`、`By Distance`、`By Depth`，只改 `Sort Mode`；分别跑原地旋转和移动相机，记录相邻帧重叠点的颜色与跳层角度。随后拆成两个系统、每个只留一张粒子，再只改 `Sorting Fudge`，用 Frame Debugger 追对应绘制事件，定位是否是整批顺序。
2. 若仅有固定分层诉求（例如烟雾始终盖住某一层），在代表性机位验固定优先级。若一对穿插面左、右的前后关系相反，先试收窄粒子、改变布置或拆开持续穿插的层，再验双侧重叠。纯加法混合可作为光点、火星的候选：在理想加法下先后相加相同，但不会给烟雾提供吸收背景的遮盖感。穿进不透明场景时可另查[软粒子](soft-particles-scene-depth.md)；常规软粒子读场景深度时不能解决不写入那张深度的两张烟片之间的排序。
3. 画面通过后，在相同覆盖、层数、分辨率和相机轨迹下再测排序相关 CPU 工作与透明绘制 GPU 时间。若改布局也减少了屏幕覆盖，把覆盖变化单独报告，避免把片元开销下降归给排序。

**状态**：已查看公开静态对照图和循环遮挡原图，核对 Unity 文档并列出两面穿插算例；未核验公开转镜头动态帧，未在本地 Unity 或目标设备验证，也没有排序开销数据。
