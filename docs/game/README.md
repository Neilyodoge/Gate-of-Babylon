# ProjectR 项目驾驶舱

> Unity 3D URP Roguelite ARPG  
> 目标：专业单人4–6个月垂直切片  
> 代码：`Babylon/Assets/1Game/`  
> 文档重置：2026-09-02

ProjectR 已全面取代旧《仙途秘境／秘境探索》产品方向。旧产品策划、备份和阶段记录已从工作区清除，需要时通过版本控制追溯。

## 从这里开始

| 要回答的问题 | 权威入口 |
|---|---|
| 做什么、为什么做、规则是什么 | [ProjectR GDD](design/GDD_秘境探索.md) |
| 各系统怎样工作 | [系统规格](design/systems/) |
| 第一次进入游戏怎样教学 | [新手关：初契序章](design/levels/新手关_初契序章.md) |
| UI采用什么布局与视觉风格 | [战斗HUD视觉基准](design/ui/战斗HUD视觉基准.md) |
| 技术边界怎样划分 | [技术架构与ADR](architecture/README.md) |
| 程序模块怎样拆、改动影响怎样查 | [程序模块规划](architecture/程序模块规划.md)与[变更影响检查](architecture/变更影响检查.md) |
| 4–6个月交付什么 | [垂直切片](delivery/垂直切片计划.md) |
| 下一步做什么 | [开发待办](design/开发待办.md) |
| 已经改了什么 | [CHANGELOG](CHANGELOG.md) |
| 怎样验证体验 | [游戏测试清单](playtest/游戏测试清单.md) |

## 文档分层

```text
docs/game/design/GDD_秘境探索.md
  ProjectR产品愿景、设计支柱、可执行总纲与实现现状

docs/game/design/systems/
  单系统规则、范围、验收与开放问题

docs/game/architecture/ + decisions/
  技术边界与不可随意推翻的决策

docs/game/delivery/ + playtest/
  交付里程碑与验证方法

docs/game/CHANGELOG.md + design/开发待办.md
  已做与未做
```

## 当前系统规格

- [ProjectR 世界设定](design/设定_ProjectR世界与主角.md)（主角待重新设计）
- [灵宠与结契](design/systems/器灵与结契.md)
- [灵宠天赋树](design/systems/灵宠天赋树.md)
- [动作与回路](design/systems/载体与回路.md)
- [秘境区域与灾相](design/systems/蜂巢秘境与灾相.md)
- [家园与局外成长](design/systems/洞府与局外成长.md)
- [战斗HUD视觉基准](design/ui/战斗HUD视觉基准.md)
- [新手关：初契序章](design/levels/新手关_初契序章.md)

## 当前阶段

第一目标不是继续扩展旧Demo，而是验证最小ProjectR闭环：

1. 一只灵宠能在武器、术法和身法上显化。
2. 三只灵宠能形成可追溯、可预览的联动。
3. 三种附着结构各有独立价值。
4. 秘境区域中相邻地点的状态能够传播和复现。
5. 新结契灵宠只有撤离后永久获得。
6. 一个25–35分钟单局可以完成探索、结契、构筑、灾相与撤离。

详细里程碑见[垂直切片](delivery/垂直切片计划.md)。

## 当前开放决策

- **首区程序暂缓**：风痕林谷策划已经冻结，但按当前优先级暂不制作白盒；先完成[新手初契序章](design/levels/新手关_初契序章.md)具体内容对账。
- 迁灵仅限脱战／灵龛，还是允许战斗中付费迁灵。
- 火花狸、响响鸮、弹弹胶三只工作名灵宠的性格、外形和最终命名。
- 死亡时普通材料保留比例。

开放问题不能被实现细节默认拍板；确定后同步GDD、对应系统规格和CHANGELOG。

## 技术边界

- 保留当前URP工程、Edgar、基础战斗、存档和编辑器工具。
- 旧 `Trigger / Effect / Modifier` 只作为回路实现经验，不再作为玩家可见主系统。
- 玩家侧只使用武器／术法／身法动作与灵宠直接附着；`PlayerCombat` 仍按输入协调、内部载体执行和表现层拆分。
- `GameEvents` 扩展为带来源、标签、因果链和预算的回路事件。
- The Archer主角仅评估骨骼／Avatar与通用动画复用，不作为正式外观；普通怪物和选定Boss的美术与招式逻辑仍按个体提取并适配现有敌人框架。
- 旧功能只在ProjectR替代路径通过切换闸门后删除。

代码图审计显示，战斗模块链、存档主链和地图Provider都是约36～45个额外文件的三跳高爆炸半径区域。因此迁移采用新模块旁路、Legacy适配和G1～G7闸门，不直接替换中心类。

详见[技术架构](architecture/README.md)、[程序模块规划](architecture/程序模块规划.md)、[变更影响检查](architecture/变更影响检查.md)和[ADR-003](architecture/decisions/ADR-003-TheArcher选择性迁移.md)。

## 旧文档状态

旧产品策划、重复备份、ideas、阶段复查和旧系统深档不再留在当前工作区。Git／版本控制是唯一历史来源，现行规则只看本导航列出的ProjectR文档。

## 程序与资源入口

- [随代码维护的工程文档](../../Babylon/Assets/1Game/Docs/README.md)
- [程序架构说明](../../Babylon/Assets/1Game/Docs/程序_架构说明.md)
- [ProjectR程序模块规划](architecture/程序模块规划.md)
- [code-review-graph变更影响检查](architecture/变更影响检查.md)
- 游戏代码：`Babylon/Assets/1Game/Scripts/`
- 游戏资源：`Babylon/Assets/1Game/`
- The Archer迁移目标：`Babylon/Assets/1Game/ArtRes/Package/Character/TheArcherRog/`

## 文档维护规则

- 愿景变更：更新GDD产品定义与设计支柱。
- 系统规则变更：更新对应系统规格和GDD“📊 实现现状”。
- 技术取舍：新增或更新ADR。
- 有意义的已完成改动：记录CHANGELOG。
- 剩余任务与验证：更新开发待办。
- 历史材料：只写入统一归档或原历史文件，不重新成为现行规则。
- 不重命名或移动既有文档；需要重组时通过索引和状态声明完成。

