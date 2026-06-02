# 🎮 《仙途秘境》项目文档

> **v0.5.5（2026-06-02）**：修仙原生系统落地 + **秘境异象**（每局变量·替代命格）。本体境界/境界压制/渡劫/转世/灵脉/机缘 全部实现；道心/因果/寿元 有了局内效果与 HUD；详见 GDD 变更摘要。
> **v0.5.4（2026-05-28）改名说明**：项目原名《仙途梦境》于 2026-05-28 改名为《仙途秘境》——彻底去除"凡人入梦"叙事框架，回归纯修仙叙事（修仙者出洞府闯秘境）。Unity 菜单路径已于 2026-06-01 统一改为 `仙途秘境/`；文档文件名仍沿用旧名（有意保留防断链）。

> **项目代号**：Solo
> **类型**：Roguelike Top-down 3D ARPG
> **引擎**：Unity 3D URP
> **代码目录**：`Assets/1Game/`

---

## 📁 代码结构

```
Assets/1Game/Scripts/
├── Core/                          # 核心系统
│   ├── Demo1Setup.cs              #   场景初始化（纯代码生成所有对象）
│   ├── GameManager.cs             #   游戏流程管理（房间推进/境界/胜负）
│   ├── GameConfig.cs              #   全局配置 ScriptableObject
│   ├── GameEvents.cs              #   事件总线（发布/订阅）
│   ├── MaterialHelper.cs          #   材质创建工具（Shader fallback）
│   ├── MonsterPrefabs.cs          #   怪物预制体管理
│   ├── ObjectPool.cs              #   对象池
│   ├── PlayerResources.cs         #   玩家资源（灵力碎片）
│   ├── PostProcessSetup.cs        #   后处理氛围控制
│   ├── TopDownCamera.cs           #   俯视角相机
│   └── DebugConsole.cs            #   运行时 Debug 控制台
├── Player/                        # 玩家系统
│   ├── PlayerController.cs        #   移动/闪避/输入处理
│   ├── PlayerCombat.cs            #   三段连招/技能释放/伤害判定
│   ├── PlayerAnimator.cs          #   动画状态机/Blend Tree
│   └── AnimationEventRelay.cs     #   动画事件中继
├── Enemy/                         # 敌人 AI
│   ├── EnemyBase.cs               #   基础近战敌人
│   ├── EnemyRanged.cs             #   远程射击敌人（预警线）
│   ├── EnemyMage.cs               #   法师敌人（魔法弹+爆炸）
│   ├── EnemyCharger.cs            #   冲锋敌人（蓄力冲撞）
│   ├── EnemyBoss.cs               #   Boss（多阶段/特殊技能）
│   └── EnemyProjectile.cs         #   敌人投射物
├── Combat/                        # 战斗数据
│   ├── CombatStats.cs             #   属性结构体（HP/ATK/SPD/暴击等）
│   ├── IDamageable.cs             #   伤害接口
│   ├── SkillData.cs               #   功法技能 ScriptableObject
│   ├── Projectile.cs              #   玩家投射物
│   ├── BurnEffect.cs              #   灼烧 DoT 效果
│   └── HitStop.cs                 #   顿帧反馈
├── Items/                         # 灵物 & 技能
│   ├── ItemData.cs                #   灵物数据 ScriptableObject
│   ├── ItemInventory.cs           #   灵物背包（叠加计算）
│   ├── ItemPickup.cs              #   灵物拾取交互（世界空间提示）
│   ├── SkillPickup.cs             #   功法拾取交互
│   ├── SpiritSlotSystem.cs        #   灵物槽位系统
│   └── SynergySystem.cs           #   隐藏组合（Synergy）系统
├── Room/                          # 房间系统
│   ├── RoomBuilder.cs             #   房间构建器（地板/墙壁/装饰）
│   ├── BattleRoom.cs              #   战斗房间（波次/奖励）
│   ├── ShopRoom.cs                #   商店房间（NPC/购买/出售）
│   ├── RestRoom.cs                #   休息房间（回血泉水）
│   ├── TreasureRoom.cs            #   宝箱房间
│   ├── LevelTransition.cs         #   关卡过渡（传送门）
│   └── RoomExitTrigger.cs         #   房间出口触发器
├── UI/                            # UI 系统
│   ├── GameHUD.cs                 #   主 HUD（血条/境界/CD/消息）
│   ├── SkillBarUI.cs              #   技能栏 + 灵物槽位（拖拽/悬停提示）
│   ├── InventoryUI.cs             #   背包面板
│   ├── Minimap.cs                 #   小地图
│   ├── DamagePopup.cs             #   伤害飘字
│   └── EnemyHealthBar.cs          #   敌人血条
└── Editor/                        # Editor 工具
    ├── Demo1DataCreator.cs        #   一键创建灵物/功法/配置数据
    ├── GameConfigEditor.cs        #   GameConfig 自定义 Inspector
    └── ToolSearchWindow.cs        #   工具搜索窗口
```

---

## 📁 文档结构

```
docs/game/
├── README.md                    # 👈 你在这里（项目总览 & 导航）
├── design/                      # 📋 策划文档
│   ├── GDD_仙途梦境.md          # ⭐ 完整 GDD（唯一权威 · v0.5.4 起为《仙途秘境》）
│   ├── GDD_仙途梦境_简明版.md   # GDD 简明版（每模块"意义+方向"，含 changelog）
│   ├── Demo路线图.md            # ⭐ 当前 Demo 阶段规划（v0.5 起，取代旧"开发路线图"）
│   ├── 开发待办.md              # ⭐ 当前待办 Backlog（按 P1/P2/P3 + 技术债）
│   ├── 灵物设计表.md            # 灵物详细数据表（品阶/效果/质变）
│   ├── 功法设计表.md            # 功法技能详细数据表
│   ├── 隐藏组合表.md            # Synergy 组合列表
│   ├── 关卡设计填表指南.md      # 关卡/事件 Excel→JSON 填表指南
│   └── 开发路线图.md            # ⚠️ 已归档（Demo1 期，被 Demo路线图.md 取代）
├── tech/                        # 💻 程序文档
│   ├── 架构总览.md              # 代码架构、模块关系、命名规范
│   ├── 战斗系统.md              # 属性计算、伤害公式、CD 模型
│   └── 灵物系统.md              # 灵物数据驱动、叠加算法、质变机制
└── art/                         # 🎨 美术文档
    └── 美术风格指南.md           # 视觉方向、配色、特效风格（待定）
```

> **看哪份？** 设计权威看 `GDD_仙途梦境.md`；快速理解看 `简明版`；接下来做什么看 `开发待办.md` + `Demo路线图.md`。

---

## 🎯 当前阶段：Demo2（v0.5.x · 修仙原生重构 + 洞府 meta）

> **Demo1 已完成**（核心战斗 / 化身 / 灵物 / 协同 / 6 层境界推进）——下方"Demo1 功能完成状态"表保留作历史记录。
>
> **Demo2 进行中**：搜打撤循环 + 洞府 meta（闭关修炼 / 本体境界 / 灵脉 / 机缘 / 渡劫战 / 心魔 / 转世）。
> 进度与待办看 [Demo路线图](design/Demo路线图.md) 和 [开发待办](design/开发待办.md)；
> 新增系统代码在 `Scripts/Cave/`（洞府模块）、`Scripts/Combat/`（CultivationSystem / SpiritVeinSystem / InnerDemonMeter / TribulationTrial 等）。

### Demo1 功能完成状态（历史）

| 功能 | 状态 | 说明 |
|------|------|------|
| **玩家移动**（WASD + 鼠标瞄准） | ✅ 完成 | Top-down 3D 视角 |
| **闪避**（Space，带无敌帧） | ✅ 完成 | 可被灵物增强 |
| **三段连招近战** | ✅ 完成 | 左键连击，各段不同伤害倍率 |
| **功法技能**（Q 槽位，纯 CD） | ✅ 完成 | 支持范围/投射物/位移/增益类型 |
| **灵物拾取 & 自动生效** | ✅ 完成 | 数据驱动 ScriptableObject |
| **灵物属性叠加** | ✅ 完成 | 绝对值 + 百分比分层计算 |
| **灵物槽位系统** | ✅ 完成 | 技能栏下方显示，拖拽换位 |
| **质变阈值检测** | ✅ 框架 | 检测已实现，具体质变效果待扩展 |
| **Synergy 隐藏组合** | ✅ 框架 | 组合检测已实现，效果待扩展 |
| **基础敌人 AI** | ✅ 完成 | 追踪 + 近战 + 掉落 |
| **远程敌人** | ✅ 完成 | 预警线 + 射击 |
| **法师敌人** | ✅ 完成 | 魔法弹 + 爆炸范围 |
| **冲锋敌人** | ✅ 完成 | 蓄力 + 冲撞 |
| **Boss 敌人** | ✅ 完成 | 多阶段 + 特殊技能 |
| **战斗房间 & 波次** | ✅ 完成 | 清完奖励 + 自动推进 |
| **商店房间** | ✅ 完成 | NPC 商人 + 购买/出售灵物 |
| **休息房间** | ✅ 完成 | 回血泉水 |
| **宝箱房间** | ✅ 完成 | 开箱掉落灵物 |
| **关卡过渡** | ✅ 完成 | 传送门 + 境界推进 |
| **境界推进**（6 层） | ✅ 完成 | 练气→筑基→金丹→元婴→化神→渡劫 |
| **HUD** | ✅ 完成 | 血条/境界/CD/消息，事件驱动 |
| **技能栏 UI** | ✅ 完成 | 技能图标 + 灵物槽位 + 悬停提示 + 拖拽 |
| **背包面板** | ✅ 完成 | 灵物列表 + 详情 |
| **小地图** | ✅ 完成 | 房间类型标记 |
| **伤害飘字** | ✅ 完成 | 暴击/普通不同样式 |
| **敌人血条** | ✅ 完成 | 世界空间跟随 |
| **灼烧 DoT** | ✅ 完成 | 灵物触发 |
| **穿透机制** | ✅ 完成 | 灵物叠加 |
| **击杀回复** | ✅ 完成 | 灵物叠加 |
| **顿帧反馈** | ✅ 完成 | HitStop 打击感 |
| **Debug 控制台** | ✅ 完成 | Tab 键呼出，无敌/锁血/秒杀/房间跳转等 |
| **材质兼容** | ✅ 完成 | MaterialHelper（Shader 查找带 fallback，避免粉色材质） |

### 快速开始

1. 在 Unity 中创建新场景 → 保存到 `Assets/1Game/Scenes/Demo1.unity`
2. 创建空 GameObject → 挂载 `Demo1Setup` 组件
3. （可选）右键 Create → 仙途梦境 → 灵物数据/功法数据，配置后拖入 Inspector
4. 点击 Play 即可运行

### 操作说明

| 按键 | 功能 |
|------|------|
| WASD | 移动 |
| 鼠标 | 瞄准方向 |
| 左键 | 近战攻击（三段连招） |
| Q/E/R | 释放功法技能 |
| Space | 闪避（带无敌帧） |
| Tab | 打开/关闭 Debug 控制台 |
| I | 打开/关闭背包 |

---

## 📋 快速导航

### 策划相关
- [完整 GDD](design/GDD_仙途梦境.md) — ⭐ 游戏核心设计、系统详解（唯一权威）
- [GDD 简明版](design/GDD_仙途梦境_简明版.md) — 每模块"意义+方向+例子"，快速上手
- [Demo 路线图](design/Demo路线图.md) — ⭐ 当前 Demo 阶段规划
- [开发待办](design/开发待办.md) — ⭐ 当前 Backlog（P1/P2/P3 + 技术债 + 待 playtest）
- [灵物设计表](design/灵物设计表.md) — 所有灵物的数据定义
- [功法设计表](design/功法设计表.md) — 所有功法技能的数据定义
- [隐藏组合表](design/隐藏组合表.md) — Synergy 组合触发条件 & 效果
- [关卡设计填表指南](design/关卡设计填表指南.md) — 关卡/事件 Excel→JSON 填表

### 程序相关
- [架构总览](tech/架构总览.md) — 代码结构、模块关系图、命名规范
- [战斗系统](tech/战斗系统.md) — 属性计算公式、伤害流程、CD 模型
- [灵物系统](tech/灵物系统.md) — 数据驱动架构、叠加算法、质变机制

### 美术相关
- [美术风格指南](art/美术风格指南.md) — 视觉方向、配色方案（待定）

---

## 🔗 相关资源

- **代码目录**：`Babylon/Assets/1Game/Scripts/`
- **数据资产**：`Babylon/Assets/1Game/Data/`
- **预制体**：`Babylon/Assets/1Game/Prefabs/`
- **项目根 README**：[../../README.md](../../README.md) — 包含渲染管线、后处理、Editor 工具等完整信息
