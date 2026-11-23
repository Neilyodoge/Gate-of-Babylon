# 方向光级联阴影：分开验切换线、纹素密度和动态爬动

**一句话结论**：暗边只在级联界面拐折，先对照软混合及两级世界空间纹素尺寸；同一级内部也随慢移镜头爬动，则先查投影范围和光空间纹素网格有没有逐帧漂移。混合接缝、稳定投影、增加分辨率解决的是不同问题。

## 公开证据

- **同镜头的级联接缝**：[Microsoft《Cascaded Shadow Maps》图 9](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/cascaded-shadow-maps#blend-between-cascades)的[原图](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/images/cascade-seams.jpg)中，左侧路面和坡上的阴影边突然变宽或错开，右侧加混合带后过渡更连续。DirectX 样例只在过渡带分别算两级的阴影可见度并插值；带外仍用一级。它混合比较后的可见度，不能拿两张深度图的原始数值直接平均。
- **混合代价和失败边界**：[Catlike Coding《Directional Shadows》§4.7](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/#4.7)是 Unity 2019.2 制作、更新到 2022.3 的自定义 SRP。在[硬切原图](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/shadow-quality/hard-cascade-transition.png)里，几个球的地面阴影被弧状切界割开；[软混合原图](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/shadow-quality/soft-cascade-transition.png)弱化了边界；[抖动放大图](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/shadow-quality/dithered-zoomed-in.png)仍能看到离散点。源码中软混合在带内再过滤一级；抖动按屏幕噪声选择其中一级，每个取样点仍只过滤一级，但有分支、图样与镜头运动的取舍。不能由这两种选法的采样路径直接换算 GPU 时间。
- **稳定和清晰度的取舍**：[Microsoft · Fit to Cascade](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/cascaded-shadow-maps#fit-to-cascade)指出，紧贴子视锥能多用纹素，但旋转相机时正交投影尺寸可能改变。较稳定的范围会留出空边，损失一部分纹素密度；[Moving the Light in Texel-Sized Increments](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/common-techniques-to-improve-shadow-depth-maps#moving-the-light-in-texel-sized-increments)再按固定纹素步长对齐光空间 XY。对齐位置不能抵消投影跨度、光方向或 FOV 改动引起的重新采样。这里核对了原文和实现，没有观察连续运动帧。

## 怎样定位和取预算

1. 固定光源、阴影图集、滤波与偏移；显示级联索引，在地面排同样的遮挡物。从同一镜头比较硬切和窄软混合带，再让一个固定世界坐标的接收点跨分界。若断线与调试级联环或分区一起走，检查两级的阴影有效覆盖、滤波和偏移；若某一级本来就缺投影物，应先查级联剔除，混合只能把缺失淡过去。只在最大阴影距离处淡出的情况，另查阴影距离衰减。
2. 先算每级正交宽度 ÷ 有效 tile 宽度。两级都用 1024 纹素，一块覆盖 24 m 得约 2.34 cm/纹素，另一块覆盖 96 m 得约 9.38 cm/纹素。同样按纹素数定义的 PCF 核，在后一块覆盖的世界空间宽度约为四倍。若带外本来就糊，先把分区往镜头真正关心的位置收，或者挪阴影距离与 tile 预算；只加宽混合会把问题摊开。固定图集加级联也可能缩小每块有效 tile，抓一帧核对布局后再比较。
3. 让静态场景的相机慢平移，并选一直处于同一近级联的固定阴影边；再只旋转相机对照。锁定正交范围后只开关纹素对齐，记录同一世界位置的阴影值和投影矩阵。先关闭时序抗锯齿隔离阴影变化，再用实际玩法设置复验抖动噪点和动态效果。世界阴影不动、只有屏幕锯齿位置变时，别把显示采样误判成阴影投影漂移。
4. [Catlike §3.2](https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/#3.2)对每级联有一次 `DrawShadows`，使用各自的剔除数据和图集区域。切换级联数量时分开量绘制阴影图的开销（各级投影物数、三角形、GPU 时间）与场景着色读取的开销（过渡带覆盖、着色和整帧 GPU 时间）。物件可出现在多个级联，也可被剔除；不能直接用级联数倍乘整帧成本。

**状态**：已看微软与 Catlike Coding 的静态对照，核对级联绘制及混合实现；世界纹素数值是算例，未做连续运动、本地工程或真机计时对照。
