# TA-51 · 重定向走路脚滑：先对齐根位移，再处理支撑期锁脚

**制作决定**：同一段走路换到不同腿长的角色后，先量一周期实际走过的距离，再量脚跟或前掌在真正支撑时相对地面的漂移。两只脚都随实际移动速度变化而沿行进方向滑，先核对步幅、播放速率和角色位移。根轨迹合适，只有某只脚在接触期漂移时，再对照重定向姿势和该链的 IK 锁定。先看膝盖和髋还能不能伸到锁定点，否则滑动可能变成跳腿。

## 公开证据

- **Epic · Speed Planting 工作流。** [Fix Foot Sliding with IK Retargeter](https://dev.epicgames.com/documentation/unreal-engine/fix-foot-sliding-with-ik-retargeter-in-unreal-engine#sourceanimationsetup)把腿长与站姿差异列为重定向脚滑的来源。该例从**源动画**的 `ball_l`、`ball_r` 提取 XYZ 平移速度，在低平段识别支撑；目标侧为腿链建立 IK Goals，然后用源曲线和[阈值](https://dev.epicgames.com/documentation/unreal-engine/fix-foot-sliding-with-ik-retargeter-in-unreal-engine#speedplantinginikretargeter)锁定目标脚。文中 30 是这一条曲线的例值；用目标角色已经滑动的速度去判接触可能漏掉应锁的帧。本文核对了官方流程正文，未目视前后步态视频。
- **Epic · 根与 IK 分开。** [双足重定向的 Retarget Root](https://dev.epicgames.com/documentation/unreal-engine/retargeting-bipeds-with-ik-rig-in-unreal-engine#retargetroot)通常选骨盆或髋；[UE 5.5 `TargetRootSettings`](https://dev.epicgames.com/documentation/en-us/unreal-engine/python-api/class/TargetRootSettings?application_version=5.5)分别描述 `scale_horizontal`、`scale_vertical` 和 `affect_ik_horizontal`。后者为 0 时脚部 IK 位置独立于根修改，为 1 时相对修改后的根计算；所以改重定向根的缩放后，还须确认 IK 位置跟着发生了什么。验证时区分重定向根骨、动画实际提取的根运动，以及控制器或胶囊最终应用的世界位移。
- **方法反例 · Kovar、Schreiner、Gleicher，[Footskate Cleanup](https://research.cs.wisc.edu/graphics/Gallery/kovar.vol/Cleanup/)。** 作者的 `blendOff`、`anklePop`、`kneePop`、`rootPop` 实验说明分别列出逐帧硬启停 IK、单脚跟／前掌过渡、腿接近伸直，以及为可达性直接投影根时的跳变。其方案把受约束帧的改动平滑带到相邻帧，平滑根修正后允许小幅腿长变化。伸长属于论文处理，不是 UE 默认行为，也不应未经画面验收加到角色；这里引用原作者实验说明，未看 AVI。

## 最小对照

1. 用直线匀速、平地和单一未混合循环做基准。标记源动作双脚的脚跟着地、全掌支撑、前掌蹬离；对应量目标的关节到地面与支撑点相对地面的位移（移动平台则量平台局部坐标）。记录每步角色实际位移、播放速率、腿链与髋姿势。原地片段的脚会相对角色向后走；要从对应参考系重建世界速度，再用速度判支撑，或用源接触标记。不能把原地片段的相对脚速直接当世界脚速。
2. 只变角色实际水平位移／播放匹配，或在独立的一组只变重定向姿势与链设置，不同时改 IK。直线算例：角色前进 1.5 m/s，支撑点相对角色以 1.2 m/s 向后，地面参考系还剩前向 0.3 m/s，四分之一秒就滑 7.5 cm。锁点可以遮掉一段位移误差，但角色仍会走完原来的距离，松锁时也可能弹回。
3. 根轨迹对齐后，只开关逐脚的支撑锁定。保留脚跟滚到前掌的正常动作，对照渐入、渐出与硬切，量支撑期水平漂移、离地误差、松锁帧的位移跳变和膝角峰值。若单脚往侧面漂或膝突然伸直，先验站姿与腿的可达范围，再决定是否配合少量髋修正。最后恢复转弯、混合、地形或网络控制路径分别复核。

**状态**：已核对 Epic 公开操作说明与 UE 5.5 字段，并核对论文作者的实验说明；7.5 cm 为推导，未目视前后步态视频，未本地或真机测量。
