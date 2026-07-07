# 🎮 《秘境探索》项目文档 · 总导航

> **类型**：Roguelite Top-down 3D ARPG（Unity URP）｜**代码目录**：`Babylon/Assets/1Game/`
> **最新版本**：V.06（2026-06-24）—— 核心方向调整为 **长局模块化 Build + Hades 式局外结算**；技能系统重构为 **触发器 + 效果器 + 改造件**；独立职业与天赋树删除，迁移为 **起始模板 + 状态型触发器 + 模块熟练度**。
> **命名说明**：旧文档中出现的“仙途 / 化身 / 灵物 / 功法 / 五系 / 修仙”等术语为历史命名；当前设计统一使用“秘境探索 / 起始模板 / 增强道具 / 技能模块 / 触发器流派”等通用游戏术语。

---

## 🧭 文档分四类，按需取用

| 类别 | 看什么 | 入口 |
|------|--------|------|
| 📐 **策划（设计）** | 游戏怎么设计的（权威） | [GDD](design/GDD_秘境探索.md) ＋ 各设计表 |
| 📝 **修改记录** | 都改了什么（按版本/时间） | [CHANGELOG.md](CHANGELOG.md) |
| ✅ **TODO** | 做了哪些 / 还差哪些 | [开发待办.md](design/开发待办.md) |
| 🗄️ **归档 / 参考** | 历史快照、复查、参考分析、工程文档 | 见下方「归档与参考」 |

> **三句话上手**：设计权威看 **GDD**；想知道最近改了啥看 **CHANGELOG**；接下来做什么看 **开发待办**。

---

## 📐 策划（设计）

**核心**
- [GDD_秘境探索.md](design/GDD_秘境探索.md) — ⭐ 完整 GDD，唯一权威（含各系统实现现状块）
- [GDD_秘境探索_简明版.md](design/GDD_秘境探索_简明版.md) — 每模块"意义 + 方向 + 例子"，快速理解

**数据 / 配表**
- [灵物设计表.md](design/灵物设计表.md) · [功法设计表.md](design/功法设计表.md) · [隐藏组合表.md](design/隐藏组合表.md)
  - 注：文件名暂不改以免断链；现行语义分别为 **增强道具表 / 技能模块表 / 模块协同表**。
- [关卡设计填表指南.md](design/关卡设计填表指南.md) — Excel→JSON 填表（9 张表：6 关卡 + 3 战斗）

**程序 / 美术参考**
- 架构权威 → [`1Game/Docs/程序_架构说明`](../../Babylon/Assets/1Game/Docs/程序_架构说明.md)（随代码；`tech/架构总览` 已并入为指针）
- 战斗 / 灵物机制深档：[tech/战斗系统.md](tech/战斗系统.md) · [tech/灵物系统.md](tech/灵物系统.md)（程序参考，Demo1 期）
- [art/美术风格指南.md](art/美术风格指南.md)

## 📝 修改记录
- [CHANGELOG.md](CHANGELOG.md) — ⭐ 按版本/时间记录已做的改动（设计决策 + 代码落地）

## ✅ TODO（做了哪些 / 还差哪些）
- [开发待办.md](design/开发待办.md) — ⭐ 已完成清单 + 剩余 Backlog（P1/P2/P3 + 技术债 + 待 playtest）
- [Demo路线图.md](design/Demo路线图.md) — 当前 Demo 阶段规划

## 🗄️ 归档与参考
- **历史快照 / 复查**：[docs/reviews/](../reviews/)（按日期归档的进度与复查）
- **已归档路线图**：[_archive/开发路线图.md](design/_archive/开发路线图.md)（Demo1 期，已被 Demo路线图 取代）
- **已归档 GDD 旧版本**：[design/_archive/](design/_archive/)（V.06 及更早的 GDD 备份，V.07 起以 `GDD_秘境探索.md` 为准）
- **参考分析**：[梦之形 Shape of Dreams 全面分析](../梦之形_Shape_of_Dreams_全面分析.md) · [TH 与 URP 对比](../TH与URP对比.md)
- **工程文档（随代码）** → [`Babylon/Assets/1Game/Docs/` 索引](../../Babylon/Assets/1Game/Docs/README.md)：程序架构 / 数据流 / 掉率系统 / Debug 工具 / 配置说明 / 资源(灵物·功法)配置指南 等，偏实现、给程序看（有自己的分类索引）。

---

## 🎯 当前阶段：v0.6 设计重构

> Demo1 的核心战斗骨架仍可复用，但设计方向已从“搜打撤 + 修仙 meta”收束为“长局模块化 Build + 局外经验结算”。
> 当前重点：模块化技能系统、触发器流派 / 起始模板、模块熟练度、统一经验值结算、阶段返回机制。
> 进度看 [开发待办](design/开发待办.md)，阶段规划看 [Demo路线图](design/Demo路线图.md)。

### 操作
| 按键 | 功能 | | 按键 | 功能 |
|---|---|---|---|---|
| WASD | 移动 | | Q/E/R | 释放技能模块 |
| 鼠标 | 瞄准 | | Space | 闪避（无敌帧） |
| 左键 | 近战三连 | | Tab | Debug 控制台 |
| I | 背包 | | F | 拾取 / 交互 |

---

## 📁 代码结构（速览 · 详见 [tech/架构总览](tech/架构总览.md)）

```
Assets/1Game/Scripts/
├── Core/       游戏流程 / 配置 / 事件总线 / 对象池 / Debug 控制台 / 范围开关(FeatureFlags)
├── Player/     移动闪避 / 连招技能 / 动画 / 旧角色控制器 / 状态效果
├── Enemy/      近战 / 远程 / 法师 / 冲锋 / 精英 / Boss
├── Combat/     属性 / 技能 / 投射物 / 旧局外成长系统 / 秘境异象
├── Items/      增强道具 / 背包 / 拾取(PickupBase·ItemPickup·SkillPickup) / 改造件 / 协同
├── Room/       房间构建 / 战斗·商店·休息·宝箱 / 基地 Hub / 关卡过渡
├── Cave/       旧基地模块（v0.6 设计上裁剪，代码可回收）
├── LevelDesign/ 第12章关卡设计（TreeMap / 事件 / Boss形态 / 配表 ConfigDatabase）
├── UI/         HUD / 技能栏 / 背包 / 小地图 / 飘字 / 血条
└── Editor/     数据创建 / 配置面板 / 工具搜索 / CSV→JSON 导表
```

---

## 🔗 相关资源
- 数据资产 `Assets/1Game/Data/`｜关卡表源 `Assets/1Game/RawData/LevelDesign/`｜战斗/模块表源 `Assets/1Game/RawData/Combat/`｜导出 JSON `Assets/1Game/Resources/{LevelDesign,Combat}/`
- 项目根 README：[../../README.md](../../README.md)（渲染管线 / 后处理 / Editor 工具）
