# ProjectR 技术架构

> 状态：迁移设计  
> 代码根目录：`Babylon/Assets/1Game/Scripts/`

本目录记录 ProjectR 的稳定技术边界和架构决策。随代码变化的类级说明仍以 [`Assets/1Game/Docs`](../../../Babylon/Assets/1Game/Docs/README.md) 为准。

## 实施入口

- [程序模块规划](程序模块规划.md)：模块ID、依赖方向、当前到目标映射、P0～P5迁移路线和G1～G7闸门。
- [变更影响检查](变更影响检查.md)：code-review-graph改前／改后流程、Unity引用风险和检查单模板。
- [垂直切片计划](../delivery/垂直切片计划.md)：M0～M5交付目标。

所有ProjectR程序任务应标记一个主模块和受影响模块。旧 `Modules/` 玩家路径与无暮王城专用流程统一归入 `M-LEGACY`，只能适配或修复，不能继续扩展。

## 目标分层

```text
Input
  → Carrier Runtime（武器／术法／身法动作）
  → Spirit Runtime（器灵实例、附着、悟法）
  → Circuit Runtime（源／应／化事件与因果链）
  → Combat Runtime（伤害、状态、投射物、召唤）

Dungeon Provider
  → Spatial Layout（Edgar／手工图）
  → Ecology Runtime（区域状态与邻接传播）
  → Encounter Runtime（房间生命周期、敌人、灾相）

Persistent Save
  ← Run State（可丢弃）
  ← Extraction Commit（唯一提交边界）
```

## 保留

- URP项目与现有游戏工程。
- `GameEvents` 类型安全事件总线思想。
- `ObjectPool`，修复后继续使用。
- `SkillData`、伤害、状态、投射物和基础敌人能力。
- Edgar Grid3D、Seed、房间状态、Socket和NavMesh。
- 三槽存档、基地Prefab插槽与配置导入工具。

## 重构

- `PlayerCombat`：拆为输入协调、载体执行和表现层。
- `ModuleChain`：从玩家可见模块链转为内部回路编译与执行参考。
- `ModuleSlotManager`：改为 N器灵 × M载体附着模型。
- `GameEvents`：增加来源、标签、因果链、深度和防递归预算。
- 关卡 Provider：增加区域状态、邻接传播和能力查询。
- Boss：从阶段门改为可拆解的灾相回路。

## 新增领域对象

- `SpiritDefinition`：种族核心机制和可用显化。
- `SpiritInstance`：永久 GUID、性格、关系、悟法池。
- `CarrierDefinition`：动作与插槽能力。
- `AttachmentLoadout`：器灵到载体的多对一附着。
- `CircuitEvent`：来源、标签、数值、因果链和预算。
- `RunBuildState`：本局载体、悟法、铭刻和联契。
- `RegionState`：区域生态状态。
- `EcologyRule`：邻接传播规则。
- `ExtractionPayload`：撤离时允许提交的差异数据。

## 切换闸门

- G1：载体运行时替代旧核心技能执行。
- G2：回路运行时替代旧模块链玩家路径。
- G3：蜂巢状态替代旧双阶段关卡。
- G4：契匣与撤离提交替代旧发现即永久。
- G5：新HUD替代旧模块装配UI。
- G6：完整25–35分钟垂直切片通过。
- G7：G1～G6全部通过后，才允许物理删除对应Legacy路径。

## 架构决策

- [ADR-001：保留现有工程与Edgar底座](decisions/ADR-001-保留现有工程与Edgar底座.md)
- [ADR-002：器灵回路采用事件与因果链模型](decisions/ADR-002-器灵回路事件模型.md)
- [ADR-003：The Archer只做选择性迁移](decisions/ADR-003-TheArcher选择性迁移.md)

