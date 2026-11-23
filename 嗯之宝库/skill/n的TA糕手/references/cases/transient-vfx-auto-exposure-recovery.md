# TA-45 · 闪光过后画面仍暗：先看测光目标回没回去

**制作决定**：爆闪结束后，固定灰卡仍比平时暗，先抓同帧的目标 EV 和实际 EV。目标仍偏离基线，就查仍进入测光的像素、分位数、遮罩及场景是否真的变亮；目标已回稳而实际值还滞后，再单调亮场回暗场的适应速度。两个数都恢复时，继续查后处理输出或其他光源，不把它们算作自动曝光的尾巴。

## 原始对照与实现

- **Epic 作者原图，4.25 的中心加权例子。** John Hable 的[技术博客「New Exposure Metering Mask」](https://www.unrealengine.com/tech-blog/how-epic-games-is-handling-auto-exposure-in-4-25?lang=en-US)展示同构图走廊的[全屏测光](https://cdn2.unrealengine.com/Unreal+Engine%2Ftech-blog%2Fhow-epic-games-is-handling-auto-exposure-in-4-25%2F10-1498x925-3482d79a40e307b643a44a8feb7167fac18d3104.png)与[中心加权](https://cdn2.unrealengine.com/Unreal+Engine%2Ftech-blog%2Fhow-epic-games-is-handling-auto-exposure-in-4-25%2F11-1498x925-0d1e9e77a37a5fab812c63003d354bdaced6c746.png)。第二张的两边石墙和前景地砖明显更暗，走廊里的火盆仍很亮。这一静态对照说明改权重也会改变平时的画面亮度；作者用它说明边缘测光权重可降低，不代表所有镜头加遮罩都会变亮，也不能据这两张图断言某次闪光恢复多快。
- **Epic 当前调试口径。** [HDR Eye Adaptation 图](https://dev.epicgames.com/documentation/unreal-engine/auto-exposure-in-unreal-engine#histogramchart)蓝线是目标 EV100，紫线是尚在适应的实际值，白线包含曝光补偿。[适应速度](https://dev.epicgames.com/documentation/unreal-engine/auto-exposure-in-unreal-engine#usingspeedupandspeeddownparameters)以档／秒表达，暗场遇到高亮用 Speed Up，闪光退回原暗场的方向是 Speed Down。Epic 的方案离目标远时线性移动，靠近目标才切指数，因此不能用全程档数除速度报固定恢复秒数。
- **可定位实现 · Unity HDRP 6000.0。** [HistogramExposure.compute 收集阶段](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/HistogramExposure.compute#L43-L62)按亮度入桶并累计测光权重；[归约阶段](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/HistogramExposure.compute#L149-L186)以总权重计算上下分位，再算平均 EV，按配置做曲线和补偿，之后才做历史适应与限幅；输出调试时也单独保存适应前的目标值。[ExposureCommon.hlsl](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/ExposureCommon.hlsl#L100-L120)里，测光时将源色除以上一帧的曝光倍率，渐进适应才用上一帧 EV 和时间步追目标。抓取含预曝光的原始 HDR 值时先核对其口径，别把缓冲里的倍率误认成实际灯光变化。这里是 HDRP 的结构，不是声称它使用 Epic 的适应曲线。
- **覆盖率算例。** 把场景简化为 0 EV 的背景与 10 EV 的闪光，只保留等权样本的 P10–P90。闪光占 2% 时，落在截去的亮端尾部；如果占 20%，仍有 10 个百分点落在保留区间，简化的平均目标由 0 升到 1.25 EV（10 × 10／80）。这是忽略量化、采样、曝光限幅和补偿的算例；大面积或高权重的闪光可以进入保留范围，单砍亮端未必解决。

## 最小复核

1. 固定镜头、曝光补偿、渲染分辨率和效果随机种子，放一块不会被特效直接照亮的灰卡，对同一片闪光采集前、峰值、关闭和恢复四段的目标 EV、实际 EV、灰卡输出亮度。必要时先做固定曝光对照；若问题仍在，单查真实光照、长尾粒子、Bloom 或其他后处理。
2. 目标偏移时，分别把同样覆盖与亮度的特效放在中央和边缘：只调遮罩，或只改上下分位，再核对正常光照和出入室内。遮罩是屏幕权重，不识别“粒子”这个身份；大面积中央爆闪反而有很高权重。直方图亮度范围控制桶覆盖，分位控制保留样本，EV 上下限约束结果；分开调，便于解释效果。
3. 目标已经回稳时，单独比较恢复方向的速度，按实际 EV 回到基线某个容差（如 ±0.1 EV）的时间和灰卡最低亮度验收。快恢复能缩短发暗，但也可能让连续闪光之间的曝光往复更明显；再用短间隔连发和明暗区域过渡回归。

**状态**：已目视 Epic 两张同构图原图，核对公开文档与 Unity HDRP 源码，覆盖率为算例。未录制本地闪光序列，也没有真机测量。
