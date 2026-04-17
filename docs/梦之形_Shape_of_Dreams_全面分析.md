# 🌙 梦之形 (Shape of Dreams) 源文件全面分析报告

> **游戏版本**: r.1.2.1.7_s  
> **开发商**: Lizard Smoothie  
> **引擎**: Unity (URP 渲染管线)  
> **网络框架**: Mirror (P2P 多人联机)  
> **平台**: StandaloneWindows64 (Steam)  
> **分析日期**: 2026-04-17  

---

## 目录

1. [项目总览与技术架构](#1-项目总览与技术架构)
2. [程序逻辑分析](#2-程序逻辑分析)
3. [策划系统分析](#3-策划系统分析)
4. [角色（旅行者）系统](#4-角色旅行者系统)
5. [技能（记忆）系统](#5-技能记忆系统)
6. [精华（宝石）系统](#6-精华宝石系统)
7. [星辰（天赋树）系统](#7-星辰天赋树系统)
8. [元素系统](#8-元素系统)
9. [怪物与Boss系统](#9-怪物与boss系统)
10. [地图与关卡系统](#10-地图与关卡系统)
11. [神殿与交互系统](#11-神殿与交互系统)
12. [诅咒系统](#12-诅咒系统)
13. [清醒梦系统](#13-清醒梦系统)
14. [宝箱与拾取物系统](#14-宝箱与拾取物系统)
15. [Mod系统](#15-mod系统)
16. [数值体系深度分析](#16-数值体系深度分析)
17. [本地化系统](#17-本地化系统)
18. [渲染与美术技术](#18-渲染与美术技术)

---

## 1. 项目总览与技术架构

### 1.1 目录结构

```
Shape of Dreams/
├── Shape of Dreams.exe          # 游戏主程序
├── UnityPlayer.dll              # Unity 运行时
├── UnityCrashHandler64.exe      # 崩溃处理器
├── version.txt                  # 版本号 r.1.2.1.7_s
├── D3D12/                       # D3D12 渲染支持
│   └── D3D12Core.dll
├── Mods/                        # Mod 系统（官方支持）
│   ├── Documentation.txt        # → GitHub 文档链接
│   └── ModTemplate/             # Mod 开发模板
├── MonoBleedingEdge/            # Mono 运行时
├── RawData/                     # 开放的游戏原始数据（JSON）
│   ├── !Images/                 # 所有图标资源
│   ├── !Sprites/                # 精灵图资源
│   ├── !ModResources/           # Mod 可覆盖的配置数据
│   ├── gradients.json           # UI 渐变色配置
│   ├── zh-CN/ en-US/ ...        # 多语言本地化数据
│   └── Readme.txt               # 数据使用条款
├── Attributions/                # 第三方资源授权声明
└── Shape of Dreams_Data/        # Unity 构建数据
    ├── app.info                 # 应用信息
    ├── boot.config              # 启动配置
    ├── level0 ~ level196        # 197 个场景文件
    ├── Managed/                 # C# 程序集（DLL）
    ├── Plugins/                 # 原生插件
    ├── Resources/               # Unity 内置资源
    └── StreamingAssets/         # Addressables 资源包
```

### 1.2 核心技术栈

| 技术领域 | 使用方案 |
|---------|---------|
| **渲染管线** | URP (Universal Render Pipeline) |
| **网络同步** | Mirror + FizzySteamworks (Steam P2P) |
| **资源管理** | Addressables 2.7.3 |
| **异步框架** | UniTask |
| **动画补间** | DOTween / DOTweenPro |
| **UI 框架** | Unity UI + TextMeshPro + Febucci TextAnimator |
| **序列化** | Newtonsoft.Json + Odin Inspector (Sirenix) |
| **物理布料** | Magica Cloth V2 |
| **GPU 实例化** | GPU Instancer Pro |
| **地形** | MicroSplat + TerrainToMesh |
| **后处理** | SC Post Effects + VolFx + FlatKit |
| **粒子特效** | VFX Graph + Particle System |
| **Steam 集成** | Steamworks.NET + EOS SDK |
| **Mod 支持** | HarmonyX (0Harmony.dll) |
| **节点编辑器** | XNode |
| **数学库** | Unity.Mathematics + Burst 编译 |
| **Lobby 服务** | Unity Services Lobbies |
| **输入系统** | New Input System |
| **Cinemachine** | 相机管理 |
| **NavMesh** | AI 导航 |

### 1.3 核心程序集架构

**Dew 命名空间** 是游戏的核心框架，分为四个主要模块：
- **Dew.Core** — 核心游戏逻辑（实体系统、状态机、战斗系统、网络同步）
- **Dew.Contents** — 游戏内容定义（技能、怪物、物品、关卡）
- **Dew.UI** — UI 系统（HUD、菜单、弹窗）
- **Dew.External** — 外部服务接口（Steam、在线服务）

### 1.4 启动配置

```ini
gfx-threading-mode=6        # 多线程渲染模式
wait-for-native-debugger=0  # 不等待调试器
hdr-display-enabled=0       # HDR 显示关闭
gc-max-time-slice=3         # GC 最大时间片 3ms（增量GC）
single-instance=            # 单实例模式
```

> **注意**: `gc-max-time-slice=3` 表示使用了增量式 GC，每帧最多花 3ms 做垃圾回收，这对于动作游戏的帧率稳定性非常重要。

---

## 2. 程序逻辑分析

### 2.1 实体系统 (Entity System)

游戏采用了组件化的实体系统，从 Mod 配置数据可以推断出核心组件：

```
Entity
├── EntityAI          — AI 行为控制
│   ├── disableAI                    # 是否禁用 AI
│   └── excludeFromAutoTargeting     # 是否排除自动瞄准
├── EntityStatus      — 属性状态
│   ├── baseStats                    # 基础属性
│   ├── scalingStats                 # 成长属性
│   └── manaTypeKey                  # 法力类型
└── EntityControl     — 移动控制
    ├── obstacleAvoidance            # 障碍物回避
    ├── baseAgentSpeed               # 基础移动速度
    ├── rotationSmoothTime           # 旋转平滑时间
    ├── normalizedAcceleration       # 标准化加速度
    └── rotateSpeed                  # 旋转速度
```

### 2.2 技能系统架构

技能系统分为两层配置：

**St_ (Skill Template)** — 技能模板层：
```json
{
  "isLevelUpEnabled": true,
  "configs.0._cooldownTime": "16",
  "configs.0._maxCharges": "1",
  "configs.0._addedCharges": "1",
  "configs.0._manaCost": "0",
  "configs.0.castMethod._range": "10",
  "configs.0.castMethod._radius": "6",
  "configs.0.faceForward": true,
  "configs.0.overrideRotation": true
}
```

**Ai_ (Ability Instance)** — 技能实例层（具体数值）：
```json
{
  "firstDamage.apFactor": "4",
  "chainDamage.apFactor": "1.85",
  "maxChainCount": "5",
  "chainDelay": "0.05",
  "firstProcCoefficient": "1",
  "chainProcCoefficient": "0.5"
}
```

### 2.3 伤害计算公式

```
最终伤害 = (baseValue + AD × adFactor + AP × apFactor + Lvl × lvlFactor 
           + Armor × armorFactor + AddedHP × addedHpFactor 
           + CritPercentage × critPercentageFactor) 
           × scalingMultiplier × leveling
```

**伤害缩放类型 (scalingType)**：
- `basic` — 基础缩放：`basicConstant + AP×basicAP + AD×basicAD + Lvl×basicLvl`，每级额外乘以 `basicAddedMultiplierPerLevel`
- `star` — 星辰缩放：固定数值数组，按星辰等级索引
- `unknown` — 复合计算（如 `damagePerTick/damageInterval`）

### 2.4 网络同步架构

- 使用 **Mirror** 作为网络框架，Host-Client 模式
- 通过 **FizzySteamworks** 实现 Steam P2P 传输
- 支持 **kcp2k** 和 **Telepathy** 作为备选传输层
- 使用 **Unity Lobbies** 服务进行房间匹配

### 2.5 状态效果系统 (Status Effect)

状态效果 (Se_) 是游戏的核心机制之一，通用配置结构：

| 参数 | 说明 |
|------|------|
| maxStack | 最大叠加层数 |
| killOnZeroStack | 层数归零时移除 |
| autoDecay | 自动衰减 |
| decayTime | 衰减间隔（秒） |
| decayAllAtOnce | 一次性衰减所有层 |
| resetTimerOnStackChange | 层数变化时重置计时器 |
| isCleansable | 是否可净化 |
| isBeneficialBuff | 是否为增益效果 |
| isKilledByCrowdControlImmunity | 控制免疫是否移除 |
| scaleDurationByTenacity | 持续时间是否受韧性影响 |

---

## 3. 策划系统分析

### 3.1 游戏核心循环

选择旅行者 → 进入梦境冒险 → 探索房间（战斗/神殿/商人/Boss）→ 获取装备和经验 → 击败Boss进入下一世界 → 到达纯白之梦通关 → 获取梦之尘 → 升级星辰天赋 → 循环

### 3.2 世界（区域）结构

| 世界 | 英文名 | 主题 | Boss | 特色怪物 |
|------|--------|------|------|---------|
| 🌲 森林 | Forest | 自然/精灵 | 森林恶魔 (BossDemon) | 蜘蛛、树人、猎犬、圣甲虫 |
| ❄️ 雪山 | SnowMountain | 冰雪/维京 | 斯库尔 (BossSkoll) | 冰元素、雪狼、维京战士、拾荒者 |
| 🔥 熔岩之地 | LavaLand | 火焰/地狱 | 炎魔 (BossInfernus) | 火元素、岩浆兽、炽热狼、地狱蜘蛛 |
| 🌑 暗洞 | DarkCave | 黑暗/洞穴 | 探索者 (BossSeeker) | 蝙蝠、洞穴蜘蛛、暗元素、蝾螈 |
| 🎨 墨境 | Ink | 东方水墨 | 暗月/白夜 (BossDarkMoon/WhiteNight) | 弓手、鬼刃、鬼枪、神兽 |
| ☁️ 天空 | Sky | 天空/星辰 | 尼克斯 (BossNyx) | 巴姆系列怪物、星种 |
| 😱 绝望 | Despair | 恐怖/扭曲 | 阿祖拉克 (BossAzurak) | 位移者、恐虫、麻痹蝇、不稳定鼠 |
| ⭐ 特殊 | Special | 隐藏Boss | 厄瑞玻斯/光元素/深渊之口/遗忘者 | 隐藏挑战 |
| 🍕 Primus | Primus | 特殊区域 | 普里姆斯·艾伦 | 特殊事件 |

### 3.3 稀有度体系

| 稀有度 | 英文 | 前缀 | 适用范围 |
|--------|------|------|---------|
| 普通 | Common | C | 记忆、精华 |
| 稀有 | Rare | R | 记忆、精华 |
| 史诗 | Epic | E | 记忆、精华 |
| 传说 | Legendary | L | 记忆、精华 |
| 独特 | Unique | U | Boss 掉落 |

---

## 4. 角色（旅行者）系统

### 4.1 角色一览

| 角色 | 定位 | 基础AD | 基础AP | 基础HP | 护甲 | 法力 |
|------|------|--------|--------|--------|------|------|
| **Aurena** 奥蕾娜 | 战斗治疗者 | 28 | 36 | 220 | 3 | 100 |
| **Bismuth** 比斯穆特 | 远程持续输出 | 26 | 41 | 170 | 0 | 0 |
| **Shell** 空壳 | 刺客 | 64 | 32 | 250 | 2 | 100 |
| **Lacerta** 拉塞尔塔 | 远程狙击手 | 43 | 41 | 220 | 0 | 100 |
| **Mist** 米斯特 | 决斗家 | 51 | 33 | 250 | 2 | 100 |
| **Nachia** 娜琪亚 | 召唤法师 | 33 | 46 | 190 | 0 | 100 |
| **Vesper** 维斯珀 | 近战坦克 | 54 | 33 | 280 | 3 | 100 |
| **Yubar** 尤巴尔 | 远程法师 | 32 | 43 | 180 | 0 | 0 |

### 4.2 角色详细分析

#### 🌟 Aurena（奥蕾娜）— 被驱逐的阿凯纳月之学会贤者
- **背景**: 因钻研禁忌知识被学会除名的贤者，能分解自身生命力进行治疗
- **Q**: Golden Burst（金色爆发）/ Reduction（削减）
- **R**: Dangerous Theory（危险理论）/ Chain Reaction（连锁反应）
- **特质**: Disintegrating Claw / Beautiful Threat
- **移动**: Feathery Dash（羽翼冲刺）
- **成长**: AD+2, AP+2, HP+20%, 护甲+1/级

#### 📖 Bismuth（比斯穆特）— 双魂共体
- **背景**: 无名魔法书和普通盲女融合而成，快速移动持续远程输出
- **Q/R 共享技能池**: Innocence / Infernal Tales / Valiant Heart / Distorted Mind
- **特质**: Prismatic Eyes（棱镜之眼）
- **移动**: Sprint（冲刺）
- **成长**: AD+1, AP+1, HP+20%/级（低成长但多形态）

#### 🗡️ Shell（空壳）— 人为制造的刺客
- **背景**: 无法感受情感与痛苦的完美杀戮工具，灵魂附体的木偶
- **Q**: Laceration（撕裂）
- **R**: Annihilation Stance（歼灭姿态）
- **特质**: The Killing Flow（杀戮之流）
- **移动**: Flash Step（闪步）
- **成长**: AD+4, HP+25%, 护甲+1/级（纯AD，最高AD成长）

#### 🔫 Lacerta（拉塞尔塔）— 前狙击手猎人蜥蜴
- **背景**: 前皇家卫队狙击手兼猎人，精于枪与火药之道
- **Q**: Hand Cannon / Incendiary Rounds
- **R**: Quick Trigger / Precision Shot
- **特质**: Salamander Powder / Double Tap
- **移动**: Nimble Dodge
- **成长**: AD+1, AP+1, HP+20%/级

#### ⚔️ Mist（米斯特）— 贝尔德西尔弗家族的决斗家
- **背景**: 出身贵族的勇敢决斗家，敏捷战斗技巧闪避致命攻击
- **Q**: Lunge（突刺）/ Fleche（飞刺）
- **R**: Unbreakable Determination / Parry（格挡）
- **特质**: Astrid's Masterpiece En Garde / Priorite
- **移动**: Fast Feet（快步）
- **成长**: AD+2, AP+2, HP+25%, 护甲+1/级

#### 🐺 Nachia（娜琪亚）— 梦幻世界的唤灵师
- **背景**: 梦幻世界的唤灵师，梦幻之森的林中守卫
- **Q**: Sylvan Call / Moonlight Pact
- **R**: Nature's Whisper / Serpentine Blessing
- **特质**: Heart of the Pack / Circle of Life
- **移动**: Dreamy Waltz
- **成长**: AP+2, HP+20%/级（纯AP成长）

#### 🔨 Vesper（维斯珀）— 太阳烈焰骑士团的残酷审判者
- **背景**: 骑士团团长、冷酷审判者，近战阻挡并造成高额伤害
- **Q**: Cruel Sun / Discipline
- **R**: Sanctuary of El / Baptism of Sun
- **特质**: Resolve / Mercy of El
- **移动**: Charge（冲锋）
- **成长**: AD+2, AP+1, HP+25%, 护甲+2/级（最高HP和护甲）

#### ⭐ Yubar（尤巴尔）— 梦之生、星星之神
- **背景**: 诞生于梦境的童话存在，掌管星辰创造与毁灭的神明
- **Q**: Ethereal Influence / Super Nova
- **R**: Cataclysm / Tranquility
- **特质**: Exotic Matter / Convergence Point
- **移动**: Flicker（闪烁）
- **成长**: AP+3, HP+20%/级（最高AP成长）
- **星辰树**: 毁灭(默认3/最大7), 生命(1/2), 想象(3/6), 灵活(2/3)
- **移速**: 5.1

---

## 5. 技能（记忆）系统

### 5.1 技能分类

| 前缀 | 类别 | 数量 |
|------|------|------|
| C_ | Common 普通 | 20 |
| R_ | Rare 稀有 | 22 |
| E_ | Epic 史诗 | 20+ |
| L_ | Legendary 传说 | 8 |
| U_ | Unique 独特 | 6 |

### 5.2 通用技能完整列表

**普通(C)**: BackStep, BeamOfLight, CorrosiveTrails, DarkBolt, DarkSpear, FlashFreeze, GlacialStomp, Hemorrhage, IceBlock, IceClaw, MagicSword, MassProtection, Pew, PressurePoint, Purgatory, Sneeze, SparklingWaterGun, Starfall, SwiftSlash, Whirlwind

**稀有(R)**: BlackArbalest, BoneCrusher, Chomp, DancingBlades, FlamingWhip, Frostbite, GlacialHammer, GreatFrostSword, Ignite, Immolation, Inspire, LightningDance, OrbOfLight, PhaseShift, PillarOfFlame, RepulsiveShield, Scattershot, ShadowOverdrive, ShadowWalk, Smite, StaticDischarge

**史诗(E)**: AntiGravity, Blink, ChainLightning, ClutchesOfMalice, CrimsonLance, DoomsdayMeteor, FinalExplosion, FlameJet, Harvest, JusticeGuillotine, LizardlyBlessing, MassCleanse, MysticDagger, Permafrost, Rewind, SearingCharge, ShadowVolley, SliceThroat, StygianRush, UmbralEdge, VileStrike, WinterDive

**传说(L)**: Blizzard, ButchersStrike, CoinExplosion, LightExplosion, MentalCorruption, Multishot, PyranasFireball, SpectreBullet

**独特(U)**: BeamOfBalance, Burrow, HerWorld, Hysteria, ShoutOfOblivion, WorldCracker

### 5.3 技能配置详解（以链式闪电为例）

```
Chain Lightning（链式闪电）— 史诗技能
├── 冷却时间: 16秒
├── 充能数: 1 (+1 额外)
├── 施法范围: 10, 效果半径: 6
├── 首次伤害: 400% AP
├── 链式伤害: 185% AP
├── 最大链式次数: 5
├── 链式延迟: 0.05秒
├── 首次触发系数: 1.0
├── 链式触发系数: 0.5
├── 弹道速度: 140
└── 可链式到自身: 是
```

### 5.4 独特技能详解

**World Cracker（世界破碎者）**:
- CD: 13秒, 充能: 1 (+1)
- 效果: 持续光属性射线，575% AP/秒，对所有接触的敌人造成伤害
- 特殊: 施法期间无法移动，但可无限维持直到取消
- 背景: 曾经最强大存在的力量碎片，能一击将整个世界化为灰烬

---

## 6. 精华（宝石）系统

### 6.1 精华分类

| 稀有度 | 数量 | 代表精华 |
|--------|------|---------|
| Common (C) | 16 | Charcoal, Confidence, Efficiency, Guidance, Lethality, Love, Quicksilver, Regeneration, Responsibility, Sharp, Shatter, Sulfur, Talc, Vengeance, Void, Wind |
| Rare (R) | 24 | Abyss, Accuracy, Adventure, Blade, Bleak, Blood, Celestial, Composure, Contempt, Dusk, Epiphany, Flow, Frost, Glaciate, Insatiable, Momentum, Mortality, NightSky, Panic, Rejuvenation, Ricochet, Rigidity, Scorched, Shock, Snow, Spiral, Stillness, Wealth, Wound |
| Epic (E) | 22 | Aftershock, Apathy, Blossom, Clemency, Crimson, Direness, Domination, Fangs, Fever, Flexibility, Insensitivity, Insight, Inversion, Metal, Might, Obsidian, Omega, Opportunity, Overload, Pain, Predation, Protection, Reflex, Thunder, Twilight, Umbra, Virtuousness |
| Legendary (L) | 9 | ChaosApple, DivineFaith, Embertail, HeartOfGold, Paranoia, Perfect, PureWhite, SolarEye, SuppressedArcanum |
| Unique (U) | 4 | EternalFlame, GlacialCore, LastStarlight, SoulPrison |

### 6.2 精华效果示例

**Gem_R_Mortality（死亡精华）** — 稀有:
> 命中时有 1.2% 几率直接处决非Boss敌人（基础0.6%，每级+1%）

**Gem_U_SoulPrison（灵魂牢笼）** — 独特:
> 受致命伤害时忽略并获得3秒无敌，然后摧毁此精华。每1%品质恢复3HP，多余治疗转护盾。

---

## 7. 星辰（天赋树）系统

### 7.1 四大类别

| 类别 | 英文 | 功能 |
|------|------|------|
| 🔴 毁灭 | Destruction | 攻击/伤害增强 |
| 🟢 生命 | Life | 防御/生存增强 |
| 🟡 想象 | Imagination | 经济/探索增强 |
| 🔵 灵活 | Flexible | 技能特化/改造 |

### 7.2 星辰缩放方式

- **Multiply** — 乘法缩放（数值越大越好）
- **Divide** — 除法缩放（数值越小越好，如CD惩罚递减）
- **ZeroToOneSmallerBetter** — 0~1范围，越小越好

### 7.3 Yubar 星辰详解

#### 毁灭类
| 星辰 | 效果 | 等级 | 价格 | 解锁 |
|------|------|------|------|------|
| Starry Sky Tapestry | +AP (5/7/9/11) | 4 | 30/30/30/40 | Lv0 |
| Dazzling Meteor | 每5级+AP (3/4/5/6) | 4 | 30/30/40/40 | Lv5 |
| Stellar Flow | +记忆急速 (5/8/11/15) | 4 | 30/30/40/40 | Lv5 |
| Starlit Gaze | 每个光属性记忆+AP (2/2.5/3/3.5) | 4 | 40×4 | Lv10 |
| Eclipse | AD的25%/35%/45%转化为AP | 3 | 50/40/50 | Lv20 |
| Starlit Eyes | 对5层光标记+10%/15%/20%伤害 | 3 | 60/40/50 | Lv25 |
| Glass Cannon | +AD(5/7/9)+AP(7/10/13)，-15%HP | 3 | 60/40/50 | Lv30 |
| AP on Dodge | 闪避后+12%/14%/16%AP，2秒 | 3 | - | Lv15 |

#### 灵活类（技能改造）
| 星辰 | 效果 | 价格 | 解锁 |
|------|------|------|------|
| 流逆 | 以太影响不再定身但爆炸+50%伤害 | 80 | Lv5 |
| 爆炸信心 | 以太影响爆炸半径+25% | 100 | Lv15 |
| 不稳定以太 | 爆炸-60%伤害，命中自动爆炸 | 120 | Lv25 |
| 迅捷施法 | 超新星重置闪避CD | 80 | Lv5 |
| 星辰牧羊 | 超新星满蓄力自动爆炸 | 100 | Lv15 |
| 迫击炮 | 超新星距离+40%，半径+30% | 120 | Lv25 |
| 力量聚焦 | 大灾变持续时间+50% | 80 | Lv5 |
| 燃烧流星 | 落点2秒内造成100%流星伤害 | 100 | Lv15 |
| 选择暴力 | 大灾变改为增加AD和攻速 | 120 | Lv25 |
| 银河流星雨 | 每个光属性记忆+1颗流星 | 150 | Lv35 |
| 灵活思维 | 宁静CD-2秒，无敌改为100%HP护盾 | 80 | Lv5 |
| 星之歌 | 宁静CD+2秒，也减少精华CD | 100 | Lv15 |
| 轻松之心 | 宁静CD-4秒，无敌时间-40% | 120 | Lv25 |

#### 想象类
| 星辰 | 效果 |
|------|------|
| Dream-born | 拆解时额外获得50%/100%/150%金币等值梦之尘 |
| God in a Fairy Tale | -10%HP，+10%/15%/20%梦之尘获取 |
| Cookie Baker | 击杀有0.5%/0.8%/1.2%几率掉落星星饼干 |

#### 生命类
| 星辰 | 效果 |
|------|------|
| Stellar Trace | +1闪避充能，CD+2/1.5/1秒 |
| Peek-a-boo! | 闪避CD-0.8秒，距离-40%/30%/20% |
| Now You See Me | 闪避距离+33% |

---

## 8. 元素系统

### 8.1 四大元素

| 元素 | 最大层数 | 衰减时间 | 衰减方式 | 特殊机制 |
|------|---------|---------|---------|---------|
| 🔥 **火 Fire** | 1000 | 5秒 | 全部一次性 | 持续灼烧，对英雄×0.25，对Boss×0.2 |
| ❄️ **冰 Cold** | 1 | 5秒 | 全部一次性 | 减速/冻结，对英雄CC×0.6，对Boss CC×0.35 |
| ✨ **光 Light** | 5 | 10秒 | 全部一次性 | 叠加标记，满层触发额外效果 |
| 🌑 **暗 Dark** | 5 | 10秒 | 全部一次性 | 叠加标记，满层触发额外效果 |

### 8.2 元素共通特性
- 所有元素**不可净化**
- 都**不是增益效果**
- 都**不受控制免疫影响**
- 都**不受韧性缩放**
- 层数变化时重置衰减计时器

---

## 9. 怪物与Boss系统

### 9.1 Boss 数值（森林恶魔示例）

| 属性 | 值 |
|------|-----|
| AD | 40 |
| AP | 40 |
| HP | 8000 |
| 护甲 | 0 |
| 起始技能CD | 8秒 |
| 延迟攻击几率 | 35% |
| 追踪随机性 | 0.5 |
| 追踪预测性 | 0.5 |
| 移动速度 | 4.4 |

### 9.2 精英怪 (MiniBoss) 机制

| 精英怪 | 机制 |
|--------|------|
| **BloodThorn** 血刺 | 主刺+子刺+弹射物，造成流血 |
| **IceAura** 冰光环 | 范围冰冻减速 |
| **OrbSpitter** 球吐者 | 发射追踪球体 |
| **SpinningArrow** 旋转箭 | 旋转弹幕 |
| **UnstableExplosive** 不稳定炸弹 | 自爆机制 |

### 9.3 幻影皮肤 (Mirage Skin) 系统

为怪物添加额外能力：
- **Delusion** 幻觉 — 发射追踪导弹，造成幻觉状态
- **Oblivion** 遗忘 — 发射球体，造成沉默
- **Oppression** 压迫 — 范围爆炸
- **Pulverization** 粉碎 — 践踏攻击
- **Sanctification** 圣化 — 保护光环
- **Armor** 护甲 — 额外护甲

---

## 10. 地图与关卡系统

### 10.1 场景规模

游戏包含 **197 个场景文件** (level0 ~ level196)

### 10.2 房间修饰器 (Room Mods) — 共 58 种

**战斗增强**: AcceleratedTime, Ambush, HarderFightBetterReward, GravityTraining, Hunted

**环境效果**: ArcticTerritory, EngulfedInFlame, ToxicArea, DarkCondensationZone, BlackRain

**奖励增强**: GoldEverywhere, StardustEverywhere, MeteoricLife

**特殊事件**: DreamTeller, LeafPuppies, StarCookie, SymbioteHabitat

**神殿生成**: SpawnGuidance, SpawnDisintegration, SpawnWell, SpawnMiniBoss, SpawnHiddenStash

**危险**: RiskOfMeteors, UnstableRatSwarm, WarpingField, InkStrikeWarning

**特殊**: PureDream, Limbo, TheConsortOfNight, PlayTutorial

---

## 11. 神殿与交互系统

游戏有 **25 种神殿**：

### 核心神殿
| 神殿 | 功能 |
|------|------|
| **Guidance** 引导 | 选择记忆/精华 |
| **Disintegration** 拆解 | 拆解记忆获取金币 |
| **UpgradeWell** 升级井 | 升级精华品质 |
| **Memory** 记忆 | 记忆相关 |
| **BlessedGuidance** 祝福引导 | 增强版引导 |

### 风险神殿
| 神殿 | 功能 |
|------|------|
| **Chaos** 混沌 | 随机效果 |
| **Hatred** 仇恨 | 仇恨/诅咒 |
| **MawOfDoom** 深渊之口 | 高风险高回报 |
| **Despair** 绝望 | 绝望效果 |

### 特殊神殿
| 神殿 | 功能 |
|------|------|
| **AltarOfCleansing** 净化祭坛 | 净化诅咒 |
| **LoopCat** 循环猫 | 特殊NPC |
| **PyranasLove** 皮拉纳之爱 | 特殊物品 |
| **PrimusDoor** 普里姆斯之门 | 特殊区域入口 |
| **TheThreshold** 门槛 | 世界转换 |
| **Destiny** 命运 | 命运选择 |
| **Luck** 幸运 | 幸运效果 |
| **Paradox** 悖论 | 悖论效果 |
| **Enlightenment** 启蒙 | 启蒙效果 |
| **Entanglement** 纠缠 | 纠缠效果 |
| **Ascension** 飞升 | 飞升效果 |
| **BossSoul** Boss灵魂 | Boss掉落 |
| **Concept** 概念 | 概念效果 |
| **MirrorOfRemorse** 悔恨之镜 | 回溯效果 |
| **Retrospection** 回顾 | 回顾效果 |
| **Stardust** 星尘 | 梦之尘相关 |

---

## 12. 诅咒系统

游戏有 **22 种诅咒**：

| 诅咒 | 推测效果 |
|------|---------|
| AmplifiedPain 放大痛苦 | 受到伤害增加 |
| AnchorOfMind 心灵之锚 | 限制某种能力 |
| BlissOfIgnorance 无知之福 | 隐藏某些信息 |
| BrainFog 脑雾 | 视野/判断受限 |
| DarkUrge 黑暗冲动 | 强制行为 |
| DreamAffliction_Hallucinatory | 幻觉梦魇 |
| DreamAffliction_Inductive | 感应梦魇 |
| DreamAffliction_Paralytic | 麻痹梦魇 |
| Electrified 带电 | 受到电击效果 |
| EternalLoop 永恒循环 | 循环效果 |
| FaintMemory 模糊记忆 | 技能受限 |
| FateOfGreed 贪婪之命 | 经济惩罚 |
| Fragility 脆弱 | 防御降低 |
| FragmentedBeing 碎裂存在 | 属性分裂 |
| IntermittentExplosion 间歇爆炸 | 随机爆炸 |
| Lethargy 嗜睡 | 速度降低 |
| LossOfIdentity 身份丧失 | 能力受限 |
| SilentNight 寂静之夜 | 沉默效果 |
| SoftSkin 柔软皮肤 | 护甲降低 |
| Somnambulism 梦游 | 控制受限 |
| UnstableEnergy 不稳定能量 | 随机效果 |
| VainDream 虚妄之梦 | 梦境干扰 |

---

## 13. 清醒梦系统

**13 种清醒梦** (Lucid Dream)：

| 清醒梦 | 解锁条件 |
|--------|---------|
| **BonVoyage** 一路顺风 | 不杀猎人通关3次 |
| **EmbraceMortality** 拥抱死亡 | 单次被诅咒6次 |
| **FalseLifeline** 虚假生命线 | 低血量进入12个新区域 |
| **FishScales** 鱼鳞 | 不使用护盾通关2次 |
| **GrievousWounds** 重伤 | 噩梦难度通关 |
| **HarmlessWhispers** 无害低语 | 同时拥有4个控制技能 |
| **MadLife** 疯狂人生 | 不受伤通过动荡森林 |
| **MarshOfDestiny** 命运沼泽 | 世界5+被诅咒3次 |
| **Overpopulation** 人口过剩 | 单次技能击杀16+敌人 |
| **PrudentJellyfish** 谨慎水母 | 4个6秒+CD技能通关 |
| **SparklingDreamFlask** 闪光梦瓶 | 不使用引导神殿通关2次 |
| **TheDarkestUrge** 最黑暗冲动 | 恐吓/击败商人20次 |
| **WILD** 狂野 | 单次访问12个猎人房间 |

---

## 14. 宝箱与拾取物系统

### 宝箱物品
BlueElixir, CrimsonElixir, SparklingElixir, Clairvoyance, CloakOfGuidance, DreamingStarfish, FragmentOfDetermination, StarSeekersJournal, SuspiciousHat, TokenOfGuidance, TreasureMap, TotallyGenuineTreasureMap

### 拾取物
HealthOrb, ManaOrb, RegenOrb, Small/Medium/LargeExpOrb, Small/Medium/LargeGoldOrb, DreamDust, AttackDamage, AbilityPower, Protection, SkillHaste, Speed

---

## 15. Mod系统

### 15.1 架构
- **HarmonyX** — 运行时代码注入
- **ModBehaviour** — Mod 基类（继承 MonoBehaviour）
- **JSON Override System** — 数据覆盖系统
- **.NET Standard 2.1** — 编译目标
- **Mod 文档**: https://github.com/LizardSmoothie/ShapeOfDreamsModDocs

### 15.2 Mod 开发模板

```csharp
public class TestMod : ModBehaviour
{
    private void Awake()
    {
        // mod.metadata — 访问 Mod 元数据
        // harmony — 自动创建的 Harmony 实例
        harmony.PatchAll();
    }

    private void OnDestroy()
    {
        // 支持热重载，必须清理
        harmony.UnpatchAll();
    }
}
```

### 15.3 可 Mod 的数据范围

几乎所有游戏数据都可以被 Mod 覆盖：
- ✅ 所有技能参数（伤害、冷却、范围等）
- ✅ 所有角色属性（基础属性、成长属性）
- ✅ 所有怪物属性（HP、伤害、AI行为）
- ✅ 所有精华效果
- ✅ 所有星辰天赋
- ✅ 所有神殿配置
- ✅ 所有诅咒效果
- ✅ 所有清醒梦效果
- ✅ 所有宝箱物品
- ✅ 所有房间修饰器
- ✅ 元素系统参数
- ✅ 拾取物配置
- ✅ 商人配置

---

## 16. 数值体系深度分析

### 16.1 属性系统

#### 基础属性

| 属性 | 英文 | 说明 |
|------|------|------|
| 攻击力 | attackDamage (AD) | 物理伤害基础 |
| 法术强度 | abilityPower (AP) | 魔法伤害基础 |
| 最大生命 | maxHealth (HP) | 生命值上限 |
| 最大法力 | maxMana | 法力值上限 |
| 生命回复 | healthRegen | 每秒回血 |
| 法力回复 | manaRegen | 每秒回蓝 |
| 暴击增幅 | critAmp | 暴击伤害倍率 |
| 暴击几率 | critChance | 暴击概率 |
| 技能急速 | abilityHaste | CDR 机制 |
| 韧性 | tenacity | CC 减免 |
| 火焰增幅 | fireEffectAmp | 火焰效果加成 |
| 冰冻增幅 | coldEffectAmp | 冰冻效果加成 |
| 光明增幅 | lightEffectAmp | 光明效果加成 |
| 黑暗增幅 | darkEffectAmp | 黑暗效果加成 |
| 护甲 | armor | 物理减伤 |

#### 成长属性
每个基础属性都有 **Flat(固定值)** 和 **Percentage(百分比)** 两种成长方式。

### 16.2 角色定位分析

```
纯 AD 型:  Shell (64 AD, +4/级)
AD 偏向:   Vesper (54 AD), Mist (51 AD)
混合型:    Lacerta (43 AD / 41 AP)
AP 偏向:   Yubar (43 AP, +3/级), Nachia (46 AP, +2/级)
治疗型:    Aurena (36 AP, 均衡成长)
特殊型:    Bismuth (41 AP, 低成长但多形态)
```

### 16.3 生存能力排名

1. **Vesper** — 280 HP, 3 护甲, +25% HP/级, +2 护甲/级
2. **Shell/Mist** — 250 HP, 2 护甲, +25% HP/级, +1 护甲/级
3. **Aurena** — 220 HP, 3 护甲, +20% HP/级, +1 护甲/级
4. **Lacerta** — 220 HP, 0 护甲, +20% HP/级
5. **Nachia** — 190 HP, 0 护甲, +20% HP/级
6. **Yubar** — 180 HP, 0 护甲, +20% HP/级
7. **Bismuth** — 170 HP, 0 护甲, +20% HP/级

### 16.4 经济系统

| 货币 | 用途 | 获取方式 |
|------|------|---------|
| **金币 Gold** | 商店购买、神殿使用 | 击杀、拆解、宝箱 |
| **梦之尘 Dream Dust** | 星辰天赋升级（永久） | 通关奖励、特定事件 |

### 16.5 UI 渐变色系统

| 标识 | 颜色 | 含义 |
|------|------|------|
| sv_ad | #FF8A2D 橙 | 攻击力缩放 |
| sv_ap | #16D7FF 青 | 法术强度缩放 |
| sv_ahp | #7CF248 绿 | 额外生命缩放 |
| sv_arm | #E1EBED 银 | 护甲缩放 |
| sv_critp | #FF5C5C 红 | 暴击百分比缩放 |

---

## 17. 本地化系统

### 支持 12 种语言
en-US, zh-CN, zh-TW, ja-JP, ko-KR, de-DE, es-MX, fr-FR, it-IT, pl-PL, pt-BR, ru-RU, tr-TR

### 本地化数据结构
每种语言包含: travelers.json, memories.json, essences.json, stars.json, achievements.json

### 富文本系统
```
<color=yellow>关键词</color>           — 黄色高亮
<sprite=1>                             — 内联属性图标
<sprite=5>                             — 缩放指示器图标
<gradient=sv_ap>575%</gradient>        — 属性缩放渐变色
```

---

## 18. 渲染与美术技术

| 技术 | 用途 |
|------|------|
| **URP** | 主渲染管线 |
| **FlatKit** | 卡通/扁平化渲染风格 |
| **SC Post Effects + VolFx** | 后处理效果 |
| **VFX Graph** | GPU 粒子特效 |
| **Shader Graph** | 可视化着色器 |
| **MicroSplat** | 高级地形着色 |
| **TerrainToMesh** | 地形转网格优化 |
| **GPU Instancer Pro** | GPU 实例化渲染 |
| **Magica Cloth V2** | 布料物理模拟 |
| **Cinemachine** | 电影级相机系统 |
| **Decal System** | 贴花系统 |
| **D3D12** | 独立D3D12支持 |
| **Burst** | 高性能计算编译 |

---

## 附录: 成就系统

### 角色解锁成就

| 角色 | 成就 | 条件 |
|------|------|------|
| Aurena | Horns, Feathers, and Gold | 累计治疗25000HP |
| Bismuth | Once Upon a Time | 保持移速900+持续3秒 |
| Shell | Reactivated | 为木偶注入灵魂 |
| Nachia | The True Ruler of the Forest | 击败森林恶魔12次 |
| Vesper | Boundless Rage | 到达星空之梦 |
| Yubar | Pure Imagination | 累计获得450梦之尘 |

### 通用成就（部分）

| 成就 | 条件 | 解锁 |
|------|------|------|
| Balance Forever Lost | 同时击败守护双子 | St_E_SliceThroat |
| Deja vu | 抚摸猫咪 | St_L_MentalCorruption |
| Electric Type | 单次伤害1500+ | St_E_ChainLightning |
| Heavier than Atlas | 达到3000最大HP | Gem_E_Might |
| Midas Incarnate | 拥有6000金币 | Gem_L_HeartOfGold |
| Iron Body | 获得300%+最大HP护盾 | Gem_E_Protection |
| Transcendent Speed | 达到6攻速/秒 | Gem_R_Wound |
| Ultra Rapid Fire | 8秒内施放12个技能 | Gem_E_Insight |
| M-M-M-MONSTER KILL! | 单次技能击杀16+敌人 | LucidDream_Overpopulation |
| Vivid Dream | 噩梦难度通关 | LucidDream_GrievousWounds |

---

## 总结

《梦之形》是一款技术架构成熟、内容丰富的 Roguelike 动作游戏：

### 技术亮点
- 🏗️ **模块化架构** — Dew 命名空间清晰分层（Core/Contents/UI/External）
- 🌐 **完善的多人联机** — Mirror + Steam P2P，支持合作冒险
- 🔧 **强大的 Mod 支持** — HarmonyX 代码注入 + JSON 数据覆盖
- 📊 **开放的数据系统** — RawData 目录完全开放，方便社区创作
- 🎨 **精致的渲染** — URP + FlatKit 卡通风格 + VFX Graph 粒子

### 策划亮点
- 🎭 **8 位风格迥异的角色** — 从纯 AD 刺客到纯 AP 法师，覆盖多种玩法
- ⚔️ **76+ 通用技能 + 角色专属技能组** — 深度的技能系统
- ⭐ **4 大类别星辰天赋** — 通用 + 角色专属，提供长期成长
- 🌍 **7+ 个主题世界** — 森林、雪山、熔岩、暗洞、墨境、天空、绝望
- 🎲 **高度随机性** — 58 种房间修饰器 + 22 种诅咒 + 13 种清醒梦

### 数值特点
- 📈 **双轨成长** — 基础属性 + 百分比成长，兼顾前期和后期
- 🔥 **四元素系统** — 火/冰/光/暗各有独特机制
- 💎 **品质系统** — 精华品质提供额外维度的数值深度
- 💰 **双货币经济** — 金币（局内）+ 梦之尘（永久），Roguelike 经典设计
