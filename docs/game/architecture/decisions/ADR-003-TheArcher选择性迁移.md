# ADR-003：The Archer 只做选择性迁移

- 状态：已接受
- 日期：2026-09-02
- 来源工程：`D:\Project\Aracher`

## 决策

The Archer 不作为 ProjectR 主工程，也不搬运其完整战斗、关卡、存档、Ability 枚举、服务定位器或对象池。迁移主角、普通怪物和选定Boss的美术资源，并迁移选定Boss所需的独立战斗逻辑。

## 允许迁移

目标美术目录：

`Babylon/Assets/1Game/ArtRes/Package/Character/TheArcherRog/`

确定内容：

- 主角模型、骨骼、Avatar、动画、材质与纹理
- 垂直切片采用的普通怪物模型、动画、材质、纹理及配套表现
- 选定Boss的模型、动画、材质、VFX、音效、阶段和招式逻辑
- Boss招式依赖的投射物、预警、命中、受击和死亡反馈

可选内容：

- 主角或通用战斗表现需要的瞄准指示、箭矢命中和反馈逻辑

逻辑迁移按选定Boss逐个提取依赖闭包，改为 `XianTu` 命名空间，移除静态服务定位、关卡推进和硬编码Ability枚举耦合，并接入现有 `EnemyAbilityProfile / Planner`、伤害、对象池和事件系统。可保留招式状态机、阶段切换、施法时序和预警逻辑，但不能让The Archer框架接管ProjectR战斗生命周期。

关卡策划冻结期间，Boss迁移仅验证独立战斗，不决定Boss在关卡中的位置、是否强制或如何被探索削弱。

## 禁止迁移

- `StageController` 静态服务定位器。
- 79项硬编码 `AbilityType`。
- `BinaryFormatter` 存档。
- 线性逐房关卡流程。
- `PoolsManager`。
- 整套百分比被动叠加。
- 依赖 Built-in RP 的材质和Shader。

## 流程

1. 建立资产清单和许可证来源记录。
2. 第一批建立主角、普通怪物和候选Boss的资源清单及依赖闭包。
3. 小批导入模型、动画、VFX和音频，在隔离测试场景完成URP材质转换。
4. 先验证主角和普通怪物美术替换，再逐个迁移选定Boss的独立战斗逻辑。
5. Boss逻辑通过适配层接入现有敌人框架，不接入The Archer关卡和成长流程。
6. 通过编译、Prefab引用、动画、材质、招式时序和运行时验证。

## 约束

Asset Store 资源只可作为游戏组成部分使用，不能作为源资产重新分发。任何第三方依赖和许可证信息必须随迁移清单记录。

