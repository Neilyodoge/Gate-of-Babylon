# TA-84 · 布料的碰撞代理与褶皱穿插

**制作决定。** 裙摆穿过腿时，先把身体碰撞体、布料模拟代理和最终裙摆分开看：腿的代理没包住身体，先校正代理；小碰撞体能从布料的顶点碰撞球之间通过，再对照边碰撞或局部加密。只有同一块裙摆的两道褶皱互穿，才用自碰撞做对照。慢速也漏和只在原速漏是两条排查路线，分别检查空间覆盖和时间步。

## 证据

- **身体碰撞：相邻顶点球之间会漏。** Magica Soft 的 Magica Cloth 2 [Collider Collision](https://magicasoft.jp/en/mc2_magicacloth_collidercollision/#Vertex_sphere_mode) 给出两段动图。点模式 `mc2_collisionsetup_G2.gif` 中，小碰撞体从相邻白色顶点球之间经过，代理链仍直；[边模式](https://magicasoft.jp/en/mc2_magicacloth_collidercollision/#Edge_mode) 的 `mc2_collisionsetup_G1.gif` 中，代理链绕开碰撞体。文档说明点模式检查代理顶点球，边模式检查代理边，`Radius` 分别表示顶点球半径和边半径；边模式开销更高。两段图支持先查点间空隙，没有覆盖三角形内部所有漏碰情形，也没有证明边模式能消除高速穿透。
- **布与布：独立的模式和厚度。** [Self-collision setting](https://magicasoft.jp/en/mc2_selfcollision/#Self_Mode) 说明 `Full Mesh` 判断点对三角形、边对边；[Surface Thickness](https://magicasoft.jp/en/mc2_selfcollision/#Surface_Thickness) 用双方厚度之和，身体碰撞用的顶点 `Radius` 不参与。例如双方各设 5 mm，文档算例的分开厚度是 10 mm；这不等于最终渲染网格各处都有 10 mm 间隙。公开棋盘布折叠图 `mc2_self-1.jpg` 是单张示意，缺同一动作的开关对照。作者列出的[限制](https://magicasoft.jp/en/mc2_magicacloth_selfcollision/#Precautions_for_use) 包括高速仍可能缠绕、代理拓扑可能引入抖动，以及较高开销。不同布料组件之间的穿插另查 `Sync Mode` 和碰撞伙伴。
- **空间和时间各有一个调节入口。** [Reduction](https://magicasoft.jp/en/mc2_magicacloth_reduction/#Adjust_while_looking_at_the_scene_view) 说明简化对象是模拟代理；减得过多会减弱碰撞，还可能扩大渲染网格与代理的差异。作者的 `Shape Distance` 不会合并同一表面上不连通的近邻点，可在贴得很近的双层网格先对照代理拓扑。[MagicaSettings](https://magicasoft.jp/en/mc2_settings_component/) 把每秒模拟次数 `Simulation Frequency` 和单渲染帧允许更新的上限 `Max Simulation Count Per Frame` 分开。调高频率后须确认实跑步数及是否撞上限；这两个设置与同一步内求解迭代次数不是一回事。较细的时间步可以用来检验高速漏碰假设，无法填上慢速就存在的几何空隙。

## 最小对照

1. 固定资产、碰撞体列表、相机和初始褶皱，用同一抬腿或急转动作跑慢速和原速。逐帧标出第一次穿入，同时看身体碰撞体、布料代理、最终渲染网格。腿面在身体代理外，先校正代理放置和尺寸；模拟代理没有穿而成品穿，先查代理到渲染网格的映射和两者形差。
2. 身体代理覆盖正确时，保持原代理拓扑和步数，只切 `Point`／`Edge`；再固定碰撞模式，单独改变嫌疑区域的模拟网格密度。记录穿入最深的一帧、连续帧数和剪影是否撑开。只有原速出问题时另做频率对照，记录实际模拟步数与 CPU 时间；提高频率后变好也应保留具体速度和帧时范围。
3. 固定身体碰撞设置，用会折叠但尽量不碰腿的动作，只切自碰撞。确认改善后再单独对照厚度，检查褶皱互穿、被撑开的距离和原地抖动。最后在目标设备用玩法视距和代表性同屏角色数验收，看效果能否抵得过模拟开销。

**验证状态。** 已目视作者的两段碰撞动图和一张折叠布原图，核对代理减面、碰撞模式和模拟步数文档；未在本地运行 A/B，未做目标设备实测。只在离屏回来或根瞬移后出现的问题另见 [TA-46](cloth-offscreen-resume-and-teleport.md)。
