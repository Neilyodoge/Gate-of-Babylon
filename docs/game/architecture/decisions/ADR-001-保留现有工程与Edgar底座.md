# ADR-001：保留现有工程与 Edgar 底座

- 状态：已接受
- 日期：2026-09-02

## 背景

ProjectR 全面替代旧产品方向。候选方案是以 The Archer 模板重建，或保留当前 URP 工程并重构。

## 决策

保留当前工程作为宿主，继续使用 URP、Edgar Grid3D、房间生命周期、Seed、NavMesh、存档和现有战斗基础。ProjectR 在这些能力上逐步替换领域模型。

## 原因

- 当前项目已完成 URP、区域地牢和运行时寻路集成。
- The Archer 使用 Built-in Render Pipeline，战斗和线性房间结构与 ProjectR 不一致。
- 全量替换会重新承担已解决的生成、存档、工具和兼容问题。
- ProjectR 的风险在器灵回路和生态，而不是基础模板搭建。

## 后果

- 旧代码在切换闸门通过前继续存在。
- 需要隔离旧领域命名并逐步拆分 `PlayerCombat`。
- Edgar 只承担空间布局，生态与玩法通过 Provider 能力层实现。
- 任何生成器替换都不能破坏 Seed、Socket、房间状态和 NavMesh 合同。

