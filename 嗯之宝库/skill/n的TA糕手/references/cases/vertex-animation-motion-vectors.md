# 顶点动画拖影：先确认有速度，再确认速度对得上

**一句话结论**：网格在风中动，速度缓冲里可能仍没有对应像素；有速度也可能用了错误的前帧形变。先看速度覆盖，再看重投影误差，别一上来调 TAA 或升频的历史权重。

## 公开对照：模型在动，速度图却漏掉它

[AMD GPUOpen · FSR 2 UE 集成，Material Instances](https://gpuopen.com/learn/fsr-2-1-unreal-engine-plugin-part2/#material-instances)（2022-10-19 初发，2024-03-11 更新）。作者在 UE 4.27 及更早版本报告：母材质的 `Animate` 默认关，实例覆盖成开。物体会产生 WPO 动画，但旧材质系统按默认值识别这个材质有没有 WPO，导致实例没写速度；把母材质默认值设为开后才有速度。**实际查看**两张原图：橙色桌中央的黑色摆件在两张场景图都在；左下速度小窗中，修改前看不到摆件轮廓，修改后出现对应轮廓。原图验证的是速度覆盖，并没有量化最终拖影、速度幅度或帧时。AMD 明确说 UE5 不需要这个旧材质默认值处理。

同一篇以 *A Boy and His Kite* 的植被说明，旧版地形草 HISM 的 Static 路径还可能漏写速度；UE 5.1 文中改为查 `r.Velocity.EnableLandscapeGrass`，无需照搬旧版强改 Mobility 的方式。另一种症状只在 LOD 渡越带出现：独立速度 Pass 与绘制颜色的 Base Pass 若使用不同抖动覆盖，FSR2 得不到匹配的速度。先定位是否只在渡越发生，必要时对照同 Pass 写速度或临时关闭该材质的抖动过渡；量产仍需对照硬切的跳变及 Pass 成本。

## 有速度但不对：当前参数不能代替上一帧

[Epic TSR 文档 · Parallax Disocclusion / Animation World Position Offset](https://dev.epicgames.com/documentation/unreal-engine/temporal-super-resolution-in-unreal-engine#animationworldpositionoffset) 和 [Previous Frame Switch](https://dev.epicgames.com/documentation/unreal-engine/utility-material-expressions-in-unreal-engine#previous-frame-switch) 指出，WPO 速度需要分别求当前和上一帧位置。纯 Time 的变形可由引擎评估；蓝图或脚本动态改变风强、相位或交互推弯时，还需把上一帧的参与参数传给材料分支，同时确认已启用顶点变形速度输出。若速度独立写在 Base Pass 后，大量 WPO 植被可能多出绘制，不能只验一棵树。

## 最小对照

1. 固定镜头、物体变换、分辨率、LOD 与同一风动时序。先关运动模糊，用速度图确认运动中的枝叶写了速度、静止部分没有误动。完全缺失先查该版本的绘制路径、实例与材质；只有 LOD 渡越缺一部分再查颜色、深度与速度的覆盖。
2. 让运行时风强按同样曲线变化，只切换“前帧用当前参数”与“前帧用记录的上一帧参数”；比较上一帧重投影差、最终时域画面。先开关 TSR/TAA 做定位，再单独验运动模糊；最后加镜头移动和遮挡解除。
3. 两帧速度已正确仍有残影时，再检查遮挡解除、叶片亚像素覆盖或纯材质/亮度变化是否超出几何速度描述的范围，避免用全局速度或质量设置掩盖局部问题。

**状态**：已核验 AMD 同镜头速度缓冲截图与 Epic 正文；未本地复现，未核验同材质前帧参数切换的逐帧 TSR/运动模糊效果，没有真机数据。
