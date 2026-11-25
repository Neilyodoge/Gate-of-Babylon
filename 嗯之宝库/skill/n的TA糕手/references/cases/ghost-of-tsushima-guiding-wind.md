# VFX-17 · 《对马岛之魂》：用跨景风纹交代目标方位

**问题与结论。** 探索镜头里，玩家要知道该朝哪边转。用能横跨近景与远景的清楚风纹交代大方向，披风和草叶留给人物身边的风感；不要让疏草、暗草或植被降档决定提示还能不能看见。风纹告诉玩家目标方位，可走的路线还得靠地形或另一个提示。

## 画面与公开说明

- **画面 · PlayStation India，[Ghost of Tsushima | Guiding Wind](https://www.facebook.com/SonyPlaystationIndia/videos/ghost-of-tsushima-guiding-wind/276239760413502/)，2020-05-29。** 约 4.12 秒的 PS4 展示短片，在后台静音、正常速播放时取离散样帧。约 1.38–1.68 秒，顶左可见 Golden Temple 目标标识，仁站在草坡上，披风朝一侧扬起。几缕浅色长纹横跨人物近旁和岩石上方，在深绿植被前清楚可辨。
- 约 3.31–3.52 秒及结束停帧，镜头能见到水岸，近草仍占着画面下方，几道较细的浅色线仍在远侧暗底上可辨。披风的形变化留在人物附近，线条则跨过没有草叶可用作提示的画面区域。不同角度的样帧显示了线条的指向轴；未从离散图追出一条粒子的完整运动，未看到手柄触发，也未验更换目标后的响应。
- **功能说明 · [PlayStation 新手指南「Follow the wind」](https://www.playstation.com/en-us/editorial/ghost-of-tsushima-beginners-guide/)。** 指南称引路风指向当前目标，也可以跟随它找地图上选的点。指南配的神社短片取样只看见花丛、鸟居与小路，作为环境参考，不能独立证明召风信号。公开资料未说明此片段内草、布和风纹的坐标或参数如何共用。

## 候选做法与最小对照

让导航目的地提供独立的世界方位，在近处和中远处各留少量可分辨的细长风纹；草叶和披风仍按环境风做局部响应。转镜头时检查风纹是否仍指向原目标，靠近视线方向时另查缩短到难以辨认的情况。裁减粒子和远景草是两个独立的画质旋钮，白岩、天空和遮挡场景里的线条对比还需核验。

同一目标、固定草风、披风、曝光和粒子种子，A 隐藏长纹而保留局部动势，B 打开长纹；分别在草坡和无草岩地用相同机位转镜。记录第一次转身是否朝目标，以及多久才辨明方向。未做本地 A/B、可读性测量或目标设备验证。

## 发射器转身时的模拟空间

**制作决定。** 方向风纹如果已经留在场景里，优先用 World 模拟，并把目标速度明确作为世界向量接入。新粒子的出生点仍可取发射器的局部位置。只修正出生时的北向，无法阻止 Local 模拟里的旧风纹被重新投到别处。

**公开证据 · Unity 6000.0／VFX Graph 17.0.4。** [Systems](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Documentation~/Systems.md#L15-L32) 分别定义 Local、World 模拟；实现中，[VFXCommon.hlsl](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Shaders/VFXCommon.hlsl#L97-L113) 的 World 输出直接使用模拟位置，Local 输出以当前物体到世界矩阵变换位置（齐次分量 1）与向量（齐次分量 0）。[Properties](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Documentation~/Properties.md#L48-L59) 的 L/W 标志属于值本身，图会按需要转换，它不切换系统模拟空间。[Change Space](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Documentation~/Operator-ChangeSpace.md#L3-L37) 可显式转换；[Direction 输入](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Editor/Models/Slots/Implementations/VFXSlotDirection.cs#L26-L45) 在转换路径上会归一化，[Vector 输入](https://github.com/Unity-Technologies/Graphics/blob/52dcb5726277b769559de8bf0155f7b021bea913/Packages/com.unity.visualeffectgraph/Editor/Models/Slots/Implementations/VFXSlotVector.cs#L10-L19) 不做这一步。速度已含大小时用带空间的 Vector；如果用 Direction，另乘所需速率。

**90° 算例。** 固定发射器原点、单位缩放，初始朝向不旋转。旧粒子已在 (3, 0, 0)，世界目标速度始终是 (2, 0, 0)。暂停位移积分，只让发射器绕 +Y 转 90°，于是局部 +X 指向世界 −Z：

| 配置 | 转身后旧粒子的世界位置 | 转身后新发粒子的世界速度 |
| --- | --- | --- |
| Local 模拟，正确换算世界速度 | (0, 0, −3) | (2, 0, 0)，进入模拟时用 (0, 0, 2) |
| World 模拟，世界速度输入 | (3, 0, 0) | (2, 0, 0) |
| World 模拟，速度误设为局部 (2, 0, 0) | (3, 0, 0) | (0, 0, −2) |

**本地对照计划。** 固定相机与世界目标，用颜色区分旧、新两批；先冻结积分转 90° 查旧粒子位置，再让一批新粒子跑一小段查速度的方位与大小。父级有缩放时单独检验速度长度。本轮为源码和旋转算例，未运行 Unity、实机或确认《对马岛之魂》的实现。
