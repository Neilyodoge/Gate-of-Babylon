# VFX-02 · SmokePortal：让动烟保留稳定的入口

**问题与结论**：场景入口要有翻涌的烟，又要让玩家一眼知道往哪走。先在玩法机位保持门芯和到达路线的位置关系，再让外围烟和碎屑自由变化。密烟挡住入口时，优先限制遮挡范围，再考虑增加亮度。

## 来源与查看范围

- Unity Technologies，[Visual Effect Graph Samples，2022.3 Release 1](https://github.com/Unity-Technologies/VisualEffectGraph-Samples#20223-release-1--1408-rev1-2023-11-10)（2023-11-10，HDRP／VFX Graph 14.0.8）。阅读该版说明，目视 SmokePortal 原发 GIF（480×269）的持续状态多帧。未见启动或熄灭，也未测读图时间。
- **可见画面**：正对入口的展示镜头里，前景石桥指向台阶，台阶两边各有一簇暖橙火光。烟团在上方和两侧改变形状，取样各帧仍能在台阶中轴上找到小块冷蓝亮芯；暗屑掠过亮区。火光面积有时较大，但分别落在入口两侧。
- **查到的实现证据**：[原项目场景与资产](https://github.com/Unity-Technologies/VisualEffectGraph-Samples/tree/master/Assets/Samples/SmokePortal)及 [VFXGraphs 目录](https://github.com/Unity-Technologies/VisualEffectGraph-Samples/tree/master/Assets/Samples/SmokePortal/VFXGraphs)列出场景、VFX_Portal、VFX_PortalSmoke、VFX_PortalFloorDrag、VFX_BraziersFlames 和 VFX_Distortion 等独立资产。目录证实可分别追查这些部分；本轮未查图内节点、绑定和渲染顺序。

## 美术判断与候选做法

按「n的美术祖谱」的场景引导与 VFX 方法，石桥给方向，两簇暖光给落点的两侧边界，冷蓝门芯给终点。持续变化集中在外围，观看焦点仍能留在入口。小屏上如果先认出火把却找不到门，先回查冷芯的可见面积和可达路线，别用更亮的整团烟压过石桥。

**TA 候选**：让门芯和外围烟可分开控制，先用软遮罩限制前层烟在核心所占的覆盖，同时让外圈继续翻涌。若入口还有不可通行、激活等状态，门芯或轮廓应随功能状态有清楚差异；动图没有验证交互状态。透明烟的覆盖和 GPU 成本另测，见[透明特效覆盖与叠层](transparent-vfx-overdraw.md)。

## 最小对照

固定镜头、曝光、门芯亮度、火光和同一烟粒种子。A 让前层烟自由掠过门芯；B 只限制核心区域的烟覆盖。播完整循环，记录门芯被遮住的最长连续时段，并让未看过效果的人在目标视距指出入口。另查斜视、缩小及亮背景。未做对照、未运行样例、未在目标设备验证。
