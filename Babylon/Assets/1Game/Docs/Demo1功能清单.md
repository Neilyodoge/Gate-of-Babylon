# Demo1 功能清单

> 本文档记录 Demo1（《仙途秘境》Roguelike 原型）的功能完成状态。
> 最后更新：2026-04-15

---

## 一、已完成功能 ✅

### 1.1 玩家系统

| 功能 | 文件 | 说明 |
|------|------|------|
| WASD 移动 + 鼠标朝向 | `PlayerController.cs` | 俯视角移动，鼠标控制朝向 |
| 近战三段连招 | `PlayerCombat.cs` + `PlayerAnimator.cs` | 鼠标左键三段连招，带扇形判定、伤害倍率、刀光/打击特效 |
| 闪避（2层充能） | `PlayerController.cs` | Space 闪避，2层充能（可连续闪避两次），每层独立恢复CD，带无敌帧、输入优先级 |
| 动画优先级系统 | `PlayerAnimator.cs` | Idle < Attack < Skill < Hit < Evade，高优先级可打断低优先级 |
| 受伤闪烁 | `PlayerController.cs` | 受击时模型闪白反馈 |
| 受击后处理脉冲 | `PlayerController.cs` + `PostProcessSetup.cs` | 受击时屏幕边缘变红（Vignette 脉冲） |

### 1.2 技能系统

| 功能 | 文件 | 说明 |
|------|------|------|
| Q/E/R 三技能槽位 | `PlayerCombat.cs` | Q=落石术（范围伤害），E=金钟罩（增益Buff），R=天雷引（范围伤害） |
| 技能 CD 系统 | `PlayerCombat.cs` + `GameHUD.cs` | 每个技能独立 CD，UI 实时显示剩余时间 |
| 技能槽位管理 | `PlayerCombat.cs` | 装备/卸下/交换技能槽位，查找空闲槽位 |
| 技能释放速度配置 | `SkillData.cs` + `GameConfig.cs` | 两级配置：全局默认 + 单技能覆盖（详见 `技能播放速度配置方案.md`） |
| 技能 Debug 可视化 | `PlayerCombat.cs` | 无 VFX 时自动创建 Debug 视觉效果（落石=Cube下落+范围圈，金钟罩=金色球体护盾） |
| 近战攻击范围 Debug 绘制 | `PlayerCombat.cs` | Debug.DrawLine 绘制扇形攻击范围 + Editor Gizmos |
| 运行时技能兜底 | `Demo1Setup.cs` | Inspector 未配置技能时，自动创建默认落石术和金钟罩 |

### 1.3 战斗系统

| 功能 | 文件 | 说明 |
|------|------|------|
| 受击顿帧（HitStop） | `HitStop.cs` | 普通命中/重击/击杀三级顿帧，通过 `Time.timeScale` 短暂降低 |
| 伤害飘字 | `DamagePopup.cs` | 世界坐标伤害数字，暴击放大显示 |
| 燃烧效果 | `BurnEffect.cs` | 持续伤害 DOT |
| 投射物系统 | `Projectile.cs` + `EnemyProjectile.cs` | 玩家/敌人投射物 |
| 对象池 | `ObjectPool.cs` | 通用对象池，特效/投射物复用 |

### 1.4 敌人 AI

| 功能 | 文件 | 说明 |
|------|------|------|
| 基础近战敌人 | `EnemyBase.cs` | 追踪 + 近战攻击，受击闪白、击退、硬直、死亡缩小 |
| 远程弓箭手 | `EnemyRanged.cs` | 第2层开始出现，远程投射物攻击 |
| 冲锋型敌人 | `EnemyCharger.cs` | 第3层开始出现，蓄力冲锋 |
| AOE 法师 | `EnemyMage.cs` | 第4层开始出现，范围魔法攻击 |
| Boss | `EnemyBoss.cs` | 最后一层 Boss 房间，多阶段行为模式 |
| 精英怪 | `EnemyElite.cs` | 第2层起30%概率出现，3倍血量1.5倍伤害，金色外观，随机2个词缀（狂暴/铁壁/分裂/雷电/吸血/冰霜） |
| 攻击预警 | `EnemyBase.cs` | 攻击前显示红色范围指示器（渐变圆柱） |
| 敌人受击硬直 | `EnemyBase.cs` | 被攻击时短暂停止行动（`_stunTimer = 0.3f`） |
| 敌人血条 | `EnemyHealthBar.cs` | 头顶血条 UI |

### 1.5 灵物系统（Roguelike 核心）

| 功能 | 文件 | 说明 |
|------|------|------|
| 灵物数据定义 | `ItemData.cs` | 5种品阶（凡品→仙品），5种分类（攻击/防御/速度/暴击/生命），功法关联 |
| 灵物拾取 | `ItemPickup.cs` | 按F拾取，长按F分解；提示UI缩小（1/4尺寸）、BillboardUI部分朝向相机（lerpFactor=0.5） |
| 功法拾取 | `SkillPickup.cs` | 功法掉落地上，靠近显示提示，按F装备到技能槽位，长按F分解；提示UI缩小（1/4尺寸）、BillboardUI部分朝向相机 |
| 灵物背包 | `ItemInventory.cs` | 持有灵物管理，属性加成计算 |
| 灵物槽位 | `SpiritSlotSystem.cs` | 技能栏下方3个灵物槽，部分针对技能生效，部分全局生效 |
| 质变效果 | `ItemInventory.cs` | 同类灵物达到阈值触发质变（3个=小质变，5个=大质变），每种分类有独特效果 |
| 灵物组合（Synergy） | `SynergySystem.cs` | 特定组合触发额外效果（风火轮/金刚不坏/天人合一/嗜血狂魔） |
| 灵物背包 UI | `InventoryUI.cs` + `Demo1Setup.cs` | Tab 键打开/关闭，显示持有灵物、Synergy 状态、属性总览 |

### 1.6 房间与关卡

| 功能 | 文件 | 说明 |
|------|------|------|
| 动态房间生成 | `RoomBuilder.cs` | 动态生成地面/墙壁/障碍物，仙侠风格配色 |
| 战斗房间 | `BattleRoom.cs` | 清理所有敌人后通关 |
| 商店房间 | `ShopRoom.cs` | 散修商人 + 灵物展示 |
| 休息房间 | `RestRoom.cs` | 灵泉恢复 50% 生命 |
| 宝箱房间 | `TreasureRoom.cs` | 开箱动画 + 品阶提升 |
| 可破坏物 | `Destructible.cs` | 木箱/石柱/灵石堆，受伤→碎裂动画→40%概率掉灵力碎片 |
| 房间内陷阱 | `RoomBuilder.cs` | 地刺 + 火焰陷阱 |
| 6层关卡推进 | `GameManager.cs` | 练气→化神，难度曲线递增 |
| 层间传送门过渡 | `LevelTransition.cs` | 旋转光柱 + 淡入淡出动画 |

### 1.7 UI 系统

| 功能 | 文件 | 说明 |
|------|------|------|
| 玩家血条（带延迟条） | `GameHUD.cs` | 左上角，受伤时延迟条缓慢追赶 |
| 技能 CD 显示 | `GameHUD.cs` | Q/E/R 三个技能槽位 + 闪避槽位 |
| 连招指示器 | `GameHUD.cs` | 显示当前连招段数 |
| 敌人计数 | `GameHUD.cs` | 右上角显示剩余敌人数量 |
| 境界信息 | `GameHUD.cs` | 顶部中央显示当前层数/境界名称 |
| 小地图 | `Minimap.cs` | 显示房间图标 + 玩家位置 |
| 死亡/通关面板 | `GameHUD.cs` + `Demo1Setup.cs` | 死亡重试 / 渡劫成功 · 飞升成仙 |
| 操作提示 | `Demo1Setup.cs` | 底部显示按键说明 |

### 1.8 视觉与后处理

| 功能 | 文件 | 说明 |
|------|------|------|
| 后处理效果 | `PostProcessSetup.cs` | Bloom / Vignette / Color Grading |
| 环境氛围 | `PostProcessSetup.cs` | 雾效 + 光照随层数变化（越深越暗/越红） |
| 受击屏幕脉冲 | `PostProcessSetup.cs` | 受击时 Vignette 变红脉冲 |
| 俯视角相机 | `TopDownCamera.cs` | 平滑跟随玩家 |

### 1.9 基础架构

| 功能 | 文件 | 说明 |
|------|------|------|
| 事件系统 | `GameEvents.cs` | 轻量级发布/订阅解耦 |
| 配置系统 | `GameConfig.cs` | ScriptableObject 集中管理所有数值 |
| 场景自动搭建 | `Demo1Setup.cs` | 运行时动态创建所有 GameObject、UI、Animator 等 |
| 编辑器工具 | `Demo1DataCreator.cs` | 菜单命令一键创建测试数据 / 配置场景 |
| Debug控制台 | `DebugConsole.cs` | Tab键打开，无敌/锁血/一击必杀/爆率拉满/房间跳转/时间缩放等 |
| 材质辅助工具 | `MaterialHelper.cs` | 自动处理URP/Standard/Legacy Shader兼容 |
| 怪物模型配置 | `MonsterPrefabs.cs` | 5种敌人类型的Prefab引用（为空用胶囊体） |
| 自定义 Bloom | URP Package 修改 | nBloom（Kawase 模糊）+ Kill Fireflies（详见 `PostProcess_README.md`） |
| 自定义 Tonemapping | URP Package 修改 | GT / ACESSimple / UE4 三种额外算法（详见 `PostProcess_README.md`） |
| 音效配置 | `AudioConfig.cs` | ScriptableObject 集中管理所有音效资源引用，按分类配置槽位 |
| 音效管理器 | `AudioManager.cs` | 全局单例，SFX 对象池（16路），事件自动播放，防重叠，BGM 管理 |

---

## 二、明确排除的功能 ❌

以下功能在讨论中明确决定**不做**：

| 功能 | 原因 |
|------|------|
| 攻击前摇取消 | 暂不需要 |
| 攻击拖尾 | 暂不需要 |
| ~~音效系统~~ | ✅ 已实现框架（AudioConfig + AudioManager + 事件自动播放），待导入音频资源 |
| 视觉模型替换 | 后续手动导入美术资源替换 |
| 震屏（Camera Shake） | 暂不需要 |
| 通关后灵物 3 选 1 | 暂不需要（Roguelike 经典设计，后续可加） |

---

## 三、待办 TODO 📋

### 3.1 美术资源替换（手动）

- [ ] 角色模型替换（当前为 Capsule，项目中已有 Frank_Katana 资产）
- [ ] 敌人模型替换（当前为 Capsule）
- [ ] 技能 VFX Prefab 制作（替换当前的 Debug Cube/Sphere 可视化）
- [ ] 环境美术（替换当前的纯色 Cube 墙壁/地面）

### 3.2 可选的后续功能

- [x] 音效系统框架（AudioConfig 配置 + AudioManager 单例 + SFX 对象池 + 事件自动播放）
- [ ] 导入音频资源并配置到 AudioConfig 对应槽位
- [ ] BGM 淡入淡出过渡
- [ ] 震屏（Camera Shake / Cinemachine Impulse）
- [ ] 攻击前摇取消（闪避可取消攻击前摇）
- [ ] 攻击拖尾（武器运动模糊/拖尾效果）
- [ ] 通关后灵物 3 选 1（Roguelike 核心循环）
- [x] R 键技能配置（天雷引，范围伤害）
- [ ] 技能槽位选择UI（替换技能时让玩家选择替换哪个槽位）
- [x] 分解系统（分解获得灵力碎片）
- [x] 灵力碎片资源UI（左下角显示）
- [x] 房间F键防护（防止连续按F跳层通关）
- [x] 商店购买系统（用灵力碎片购买灵物）

### 3.3 技能系统演进

详见 [技能播放速度配置方案.md](技能播放速度配置方案.md)：

- [ ] 中期：Animator 参数 `speedMultiplier` 控制（多 Layer 不互相干扰）
- [ ] 后期：Timeline + Prefab 方案（动画/特效/音效精确同步）

---

## 四、项目文件结构

```
Assets/1Game/
├── Scripts/
│   ├── Combat/          # 战斗系统
│   │   ├── BurnEffect.cs
│   │   ├── CombatStats.cs
│   │   ├── HitStop.cs
│   │   ├── IDamageable.cs
│   │   ├── Projectile.cs
│   │   └── SkillData.cs
│   │   ├── DebugConsole.cs
│   │   ├── Demo1Setup.cs
│   │   ├── AudioConfig.cs
│   │   ├── AudioManager.cs
│   │   ├── GameConfig.cs
│   │   ├── GameEvents.cs
│   │   ├── GameManager.cs
│   │   ├── MaterialHelper.cs
│   │   ├── MonsterPrefabs.cs
│   │   ├── ObjectPool.cs
│   │   ├── PlayerResources.cs
│   │   ├── PostProcessSetup.cs
│   │   └── TopDownCamera.cs
│   ├── Editor/          # 编辑器工具
│   │   ├── ConfigDashboard.cs
│   │   ├── Demo1DataCreator.cs
│   │   ├── GameConfigEditor.cs
│   │   └── ToolSearchWindow.cs
│   ├── Enemy/           # 敌人 AI
│   │   ├── EnemyBase.cs
│   │   ├── EnemyBoss.cs
│   │   ├── EnemyCharger.cs
│   │   ├── EnemyElite.cs
│   │   ├── EnemyMage.cs
│   │   ├── EnemyProjectile.cs
│   │   └── EnemyRanged.cs
│   ├── Items/           # 灵物系统
│   │   ├── ItemData.cs
│   │   ├── ItemInventory.cs
│   │   ├── ItemPickup.cs
│   │   ├── SkillPickup.cs       # 也包含 BillboardUI 类
│   │   ├── SpiritSlotSystem.cs
│   │   ├── SynergySystem.cs
│   │   └── QualitativeEffectRunner.cs
│   ├── Player/          # 玩家
│   │   ├── AnimationEventRelay.cs
│   │   ├── PlayerAnimator.cs
│   │   ├── PlayerCombat.cs
│   │   └── PlayerController.cs
│   ├── Room/            # 房间与关卡
│   │   ├── BattleRoom.cs
│   │   ├── Destructible.cs
│   │   ├── LevelTransition.cs
│   │   ├── RestRoom.cs
│   │   ├── RoomBuilder.cs
│   │   ├── RoomExitTrigger.cs
│   │   ├── ShopRoom.cs
│   │   └── TreasureRoom.cs
│   └── UI/              # UI 系统
│       ├── DamagePopup.cs
│       ├── EnemyHealthBar.cs
│       ├── GameHUD.cs
│       ├── InventoryUI.cs
│       ├── Minimap.cs
│       └── SkillBarUI.cs
├── Docs/                # 文档
│   ├── README.md                     ← 文档索引（总入口）
│   ├── Demo1功能清单.md              ← 本文档
│   ├── 工具搜索面板使用说明.md        ← F1 工具搜索面板
│   ├── 技能播放速度配置方案.md        ← 技能速度配置演进
│   ├── 配置_GameConfig说明.md        ← 全局配置字段说明
│   ├── 配置速查表.md                 ← 一页纸配置总览
│   ├── 程序_架构说明.md              ← 代码架构（面向程序/AI）
│   ├── 程序_Debug与工具说明.md       ← Debug控制台+编辑器工具
│   ├── 程序_数据流与生命周期.md      ← 启动/运行/清理流程
│   ├── 资源_灵物配置指南.md          ← 灵物 SO 创建指南
│   ├── 资源_功法配置指南.md          ← 功法 SO 创建指南
│   └── 资源_数据创建工具说明.md      ← 编辑器工具使用说明
└── Data/                # 数据资产（由 Demo1DataCreator 生成）
    ├── Skills/
    ├── Items/
    └── GameConfig.asset
```

---

## 五、游戏流程

```
开始游戏
  │
  ▼
生成房间布局（小地图显示）
  │
  ▼
┌─────────────────────────────────────┐
│  房间类型（随机）                      │
│  ├── 战斗房间 → 多种敌人 + 陷阱       │
│  ├── 商店房间 → 散修商人 + 灵物展示    │
│  ├── 休息房间 → 灵泉恢复 50% 生命     │
│  ├── 宝箱房间 → 开箱获得高品阶灵物     │
│  └── Boss 房间 → 普通敌人 + Boss      │
└─────────────────────────────────────┘
  │
  ▼
房间清理完成 → 传送门出现
  │
  ▼
传送门过渡动画（旋转光柱 + 淡入淡出）
  │
  ▼
下一层（难度递增，新敌人类型解锁）
  │
  ▼
最后一层通关 → 渡劫成功 · 飞升成仙
```

---

## 六、操作说明

| 按键 | 功能 |
|------|------|
| WASD | 移动 |
| 鼠标 | 瞄准方向 |
| 鼠标左键 | 近战攻击（三段连招） |
| Q | 技能1（落石术 — 范围伤害） |
| E | 技能2（金钟罩 — 增益Buff） |
| R | 技能3（天雷引 — 范围伤害） |
| Space | 闪避（2层充能，带无敌帧） |
| F | 拾取/交互（短按拾取，长按分解） |
| Tab | 打开/关闭灵物背包 |
