# 📝 《仙途秘境》修改记录（CHANGELOG）

> **用途**：按时间/版本记录**已做的改动**（设计决策 + 代码落地）。最新在上。
> **配套**：未来要做的看 [开发待办](design/开发待办.md)；设计权威看 [GDD](design/GDD_仙途梦境.md)。
> **维护约定**：每完成一波改动，在顶部按日期/版本追加一节；细节可链接到 GDD 对应章节。

---

## v0.5.7 · 技能机制实现（起步：增益类）（2026-06-05）

- **取消"表→SO 自动同步"流程**：按用户决定删除 `SkillConfigSync` 编辑器工具。今后新增技能/功能由用户明确提出，再手动落地（SO 创建 + Inspector 池接入属 Unity 侧手动步骤），不再自动化建 SO。
- **增益(Buff)类机制落地（修复死分支）**：`PlayerCombat` 技能调度此前**漏接 `SkillType.Buff`**——增益技能（含金钟罩）按下毫无效果。现已补上 `case SkillType.Buff`。
- **`CastBuffSkill` 泛化**：不再写死"减伤+50%"。`SkillData` 新增增益字段（`buffDuration` / `buffAttackSpeedPct` / `buffMoveSpeedPct` / `buffAttackPct` / `buffDamageReduction`），由这些字段组装 `StatusEffect` 经 `StatusEffectController` 应用（自动计时回收、可显示在状态 HUD）；字段全空时兜底减伤+50%（保旧金钟罩行为）。已确认 controller 会把攻速/移速聚合进实时 `CombatStats`。
- **效果**：金钟罩(现有 SO)现真正生效；攻速/移速/攻击增益框架就绪，新增对应 buff SO 即可用（御风诀/御风术 等）。

**③ 区域类机制落地**（通用组件，一次解 4 技能）
- 新增 `ActiveSkillZone`（`Combat/`）：支持 周期伤害 / 减速 / **黑洞吸引** / 灼烧 / **随玩家移动**；`SkillData` 新增 `SkillType.Zone` + 区域字段（`zoneDuration`/`zoneRadius`/`zoneTickInterval`/`zoneDamagePerTick`/`zoneSlowPct`/`zonePullSpeed`/`zoneFollowPlayer`/`zoneBurnDPS`）。`PlayerCombat.CastZoneSkill` + 调度接入（落点/随身二选一）。
- **敌人真减速**：`EnemyBase` 新增 `ApplySlow(duration, slowPct)` + `MoveSpeed` 有效移速（区别于 `ApplyFreeze` 全停）；移动三处改用有效移速。
- 覆盖 混沌吞噬(吸引+DoT) / 天罡北斗阵(随身+减速) / 九天玄火阵(大范围+灼烧) / 冥河召唤(满屏+大幅减速) —— 仅需建对应 Zone SO。

**④ 新奇单体（部分）**
- 寒冰封印：`AreaDamage` 新增 `freezeOnHitChance`/`freezeOnHitDuration`，命中按概率冻结。
- 土遁术：`Dash` 新增 `dashInvulnerable`/`dashInvulnDuration`；`PlayerController.SetInvincible(duration)`（复用无敌计时器）→ 钻地无敌。
- 轮回一击：新增 `RunCombatStats`（`EnemyBase.OnDamage` 累计玩家本局总伤害，`GameManager.StartNewRun` 清零）；`SkillData.damageFromRunTotal`+`runTotalDamageRatio`，`CastAreaSkill` 按"累计伤害×比例"结算。
- **待做**：水镜术(分身嘲讽，需敌人重定向)、金蝉脱壳(受致命伤拦截，需被动技能+玩家死亡钩子)、天地大挪移(投射物反射+攻击转治疗，需投射物系统钩子) —— 均需新子系统，后续推进。

**📋 各技能 SO 配方**（机制已就位；SO 创建 + 接 `skillPool` 为 Unity 手动步骤）
| 技能 | configId | skillType | 关键字段 |
|---|---|---|---|
| 烈焰斩 | 2 | AreaDamage | elementTag=Fire, aoeRadius~3 |
| 御风诀 | 3 | Buff | buffAttackSpeedPct=0.3, buffDuration~6 |
| 寒冰封印 | 4 | AreaDamage | elementTag=Ice, freezeOnHitChance=1, freezeOnHitDuration~2 |
| 混沌吞噬 | 5 | Zone | zonePullSpeed~3, zoneDamagePerTick~0.12, zoneDuration=5, zoneRadius~5 |
| 土遁术 | 6 | Dash | dashInvulnerable=true, dashInvulnDuration=3 |
| 缩地成寸 | 8 | Dash | leaveTrail=true |
| 御风术 | 10 | Buff | buffMoveSpeedPct=0.5, buffDuration=5 |
| 天罡北斗阵 | 11 | Zone | zoneFollowPlayer=true, zoneSlowPct~0.4, zoneDamagePerTick~0.1, zoneDuration=8 |
| 九天玄火阵 | 12 | Zone | zoneRadius~12(满屏), zoneBurnDPS~5, zoneDamagePerTick~0.08, zoneDuration=5 |
| 冥河召唤 | 13 | Zone | zoneRadius~12, zoneSlowPct~0.6, zoneDamagePerTick~0.1, zoneDuration=5 |
| 轮回一击 | 14 | AreaDamage | damageFromRunTotal=true, runTotalDamageRatio=0.1, canCharge 可选 |

**④ 新奇单体（剩余 3 个补齐）**
- 水镜术：`WaterMirrorDecoy` 嘲讽分身，`EnemyBase` 读 `ActiveTransform` 重定向索敌（缓存 `_playerTarget`）。
- 金蝉脱壳：`LethalGuard`（Buff 武装）；`PlayerController.OnDamage` 致命前拦截 → 免死回 15% + 替身爆炸击退 + 向后瞬移。
- 天地大挪移：`HeavenEarthShift`（10s）；`OnDamage` 顶部受伤反弹给来源 + 自身免疫；普攻命中转为自身治疗。

**SO 实体化（Unity MCP 直接建好）**
- 用 `unity_execute_code` 在 `Assets/1Game/Data/Skills` 创建/更新 **14 个 SkillData SO**（11 + 水镜/金蝉/天地大挪移），含 configId + 全部机制字段，接入 Demo1 `Demo1Setup.skillPool`（12→25）、存盘。可直接进游戏测试。

**⑤ 化身专属 16-20（全部落地）**
- 新增 `SkillType.AvatarSpecial` + `SkillData.avatarSpecial`（`AvatarSpecialKind`）；`PlayerCombat.CastAvatarSpecial` 按种类路由到对应化身控制器，并由各控制器**按当前化身门控**（非该化身不生效）。
- 一念刹那(16) → `SpiritRootGoldController.UnleashOneThought`：强力剑斩，剑心通明时威力翻倍。
- 枯荣逆旅(17) → `SpiritRootWoodController.DetonateSeeds`：引爆周围所有寄生种子（按层数伤害）。99 层/攻击不消耗等被动后补。
- 息影瞬步(18) → `SpiritRootWaterController.EnterShadowStep` + `PlayerController` 闪避无充能消耗 + `OnDodge` 路径伤害（5s）。
- 兵阵合一(19) → `SpiritRootEarthController.TogglePuppetArrayMode` + 新增 `EarthPuppetTurret`：成阵 5 座炮台 AOE 炮击，再次释放撤阵。
- 焚天·业火燎原(20) → `SpiritRootFireController.IgniteInferno`：强化狂火（时长随怒气）+ 结束引爆全场灼烧。

**SO（MCP）**：16-20 五个 AvatarSpecial SO 建好+接池，**Demo1 skillPool 共 30**。

- **全技能机制·分批路线（已全部完成）**：①增益 + ②基础 + ③区域 + ④新奇单体(6) + ⑤化身专属(5)。20 个技能机制 + SO 全部落地、接入 Demo1 池、编译 0 错误。后续为数值打磨与被动补全（枯荣 99 层等）。

## v0.5.6 · 技能配表重做（2026-06-05）

- **Skill_Base_Config 重做**：策划提供的新 19 技能 + 新增 `Rarity` 列；我据"业火"机制设计并补入**专属技能 #20 焚天·业火燎原**（怒气驱动·天品·特殊）。
- **Avatar `Exclusive` 列**：按策划说明新增（引用 Skill_Base ID 标记化身专属技能）；据描述绑定 剑魄→16 / 青囊→17 / 影刃→18 / 御物→19 / 业火→20（`ConfigTables.AvatarBaseRow.Exclusive` + 导入器支持）。
- ~~**表 → SO 自动同步工具**（`SkillConfigSync`）~~：v0.5.7 已按用户决定**删除**，改为手动按需落地（见 v0.5.7）。
- **configId 重绑**：旧 12 SO 中仅「落石术(1)/土遁术(6)」与新表同名→保留绑定；其余 10 个旧 SO（雷锁链/寒冰诀/天雷引/金钟罩/镜花水月/影步/烈焰掌/傀儡术/回春术/御剑术）名不在新表→ `configId=0`（用自身值、不被错套），属**遗留技能**：功能正常但未进当前表，待设计者决定重映射/退役（见 GDD §6.9 注）。
- **新表 16 个新技能**（ID 2-5,7-20）SO 由同步工具生成：基础类型(伤害/位移/增益)可用；**复杂/特殊类（黑洞/冥河/轮回一击/天地大挪移/Type=4 专属等）为数据壳，机制待代码实现**。

## v0.5.5 · 修仙原生系统落地 + 秘境异象（2026-06-01 ~ 06-02）

**范围决策（V.03）**
- 灵物整套**屏蔽**（`FeatureFlags.EnableSpiritItems=false`，Q8）；洞府 meta（闭关/灵脉/机缘）先暂缓、后「常规打开」（`EnableCaveMeta=true`）。开关位 `GameConfig` + DebugConsole「V.03 范围开关」。

**本体境界线落地**（原标"待实现"全部实现）
- `CultivationSystem`（历练值→修为→本体境界 6 阶 + 成色）、`CultivationSuppression`（境界压制）、`TribulationTrial`（渡劫战：天劫+心魔镜像，成色判定）、闭关石室 `MeditationChamber`、身死道消·转世。

**秘境异象**（替代隐藏命格 · GDD §8.4）
- `RealmAnomalySystem`：每局明牌随机异象 + 坍缩式叠加；6 异象（灵潮/雷泽/血月/心魔滋生/万灵复苏/寂灭）；HUD 异象条 + 飘字。命格战力被动退役（`RollFate` 仅保留写 BossFlag）。

**修仙原生四维**
- 道心/因果阈值效果 + 寿元衰朽（`MoralEffects`）；道心挂钩渡劫成色（`TribulationTrial`）；心魔值抉择驱动（`InnerDemonMeter`）；修仙状态 HUD（`RunHUD.DrawMoralStatus`：道心/因果/寿元）。

**洞府闭环增强**
- 链式机缘（跨局回访：故人来谢 / 剑灵认主，`CaveOpportunitySystem`）；灵脉道具入掉落池（`SpiritVeinPickup`：Boss/宝箱掉落→灵脉经验）；灵脉 `ModuleEfficiency` 接灵田生长。

**体验 / 工程**
- 掉落物提示**乱码修复**（编码事故，品阶/效果/按键提示曾全是 `?`）+ **朝向/缩放修复**（提示 Canvas 不再挂拾取物）。
- 掉落物拾取**重构**：抽 `PickupBase` + `WorldPromptPanel`（`BillboardUI` 并入），`ItemPickup`/`SkillPickup` 改为子类，消 ~90% 重复。
- 编辑器菜单旧名 `仙途梦境/` → `仙途秘境/`（全代码 + 文档一致）；玩家可见旧术语（入梦/魂伤/梦境）统一为（入秘境/道伤/秘境）。
- **战斗配表新增**：`Avatar_Base_Config` / `Skill_Base_Config` / `Skill_Effect_Config` 三张 CSV + 接入 `CsvToJsonImporter`（GDD §4.9/6.9/6.9-2）。
- **战斗配表接运行时**：`ConfigDatabase` 加载这 3 张表 + 查表访问器（`GetAvatar`/`GetSkillBase`/`GetSkillEffect`）；DebugConsole「📋 配表自检」验证全链路。
- **B 方案·表作数据层（2026-06-03）**：化身显示名由 `Avatar_Base_Config` 覆盖（`SpiritRootRegistry` 惰性应用，CSV 已对齐 5 行+真实控制器类名）；技能 **CD + 伤害** 由 `Skill_Base_Config` 覆盖（`SkillData.configId` + `SkillTuning.EffectiveCooldown`/`EffectiveBaseDamage`；`PlayerCombat` CD 三处 + 伤害三处接入）。**12 个技能 SO 已批量填 configId(1~12)** + CSV 对齐真实 12 技能（CD=现值、伤害%=10000 中性，不改平衡）。伤害规则=对 SO 基础伤害的百分比乘区，与 UpgradeRoom 升级共存、不写回 SO；全带回退。

**文档**
- GDD 升 v0.5.5；实现状态总表/§8.1 现状表校准（待实现→已实现）；秘术 §9.2 标注「Demo3+ 暂缓 · 效果待重评」。
- **文档体系整理（4 类）**：导航(README) / 策划(GDD) / 修改记录(本 CHANGELOG) / TODO(开发待办)；两棵文档树（`docs/` + `1Game/Docs/`）互链、分工（设计 vs 工程）。
- **去重合并（2026-06-03）**：架构 `tech/架构总览` → 并入 `1Game/Docs/程序_架构说明`（独有的模块图/命名/层级/文件警告/复查链接折入第九节），原文改指针；`策划_灵物与功法设计框架` → 并入 GDD 第5/6/7章，改指针（其旧「数值平衡基线」「单次强化」已过时/被 6.5 槽位修饰取代，弃）；战斗/灵物机制深档（独有）留 `tech/` 并登记进程序文档主页。

> 详细设计见 GDD 对应章节；剩余任务见 [开发待办](design/开发待办.md)。

---

## v0.5.4 · 去"梦境"框架 + 改名《仙途秘境》（2026-05-28）

- 项目原名《仙途梦境》→《仙途秘境》：彻底移除"凡人入梦"叙事，回归纯修仙（修仙者出洞府闯秘境）。
- 术语替换：入梦→入秘境 / 魂伤→道伤 / 残魂→残念 / 梦境→秘境。
- 死代码清理：境界突破 3 选 1 / 顿悟系统 / 灵气浓度 / 丹药 meta（炼丹房/携丹/服丹）/ 法宝（并入机缘事件）。
- 境界系统重构为两条轴（本体境界纵向 + 秘境层深度横向）+ 境界压制 + 身死道消转世（设计定稿，v0.5.5 落地）。

## v0.5.1 ~ v0.5.3 · 设计调整（2026-05）

- 法宝并入"机缘事件产物"，不再作为独立灵物分类。
- 丹药从 meta 移除（炼丹房等）。
- 灵脉 / 机缘 / 闭关修炼 等洞府 meta 重设计（设计定稿）。

## v0.5 · 修仙搜打撤循环 + 洞府种田 meta（2026-05-16）

- 深度重构：搜打撤循环（灵物分两类 + 撤离 + 死亡惩罚）+ 洞府模块 + 4 大修仙战斗系统（后精简为 2 大：渡劫 + 心魔）。

---

## Demo1 · 已实现功能清单（历史归档）

> Demo1 阶段核心战斗 / 化身 / 灵物 / 协同 / 6 层境界推进已完成，保留作历史记录。

| 功能 | 说明 |
|------|------|
| 玩家移动（WASD + 鼠标瞄准）/ 闪避（无敌帧） | Top-down 3D |
| 三段连招近战 / 功法技能（Q/E/R 纯 CD） | 范围/投射/位移/增益 |
| 灵物拾取 & 自动生效 / 属性叠加 / 槽位系统 | 数据驱动 SO |
| 质变阈值 / Synergy 隐藏组合 | 框架已实现，效果待扩展 |
| 敌人 AI：近战 / 远程 / 法师 / 冲锋 / Boss | 多阶段 Boss |
| 房间：战斗 / 商店 / 休息 / 宝箱 / 过渡 | 波次 + 奖励 + 传送门 |
| 6 层境界推进（练气→渡劫） | 难度递增 |
| HUD / 技能栏 / 背包 / 小地图 / 伤害飘字 / 敌人血条 | 事件驱动 |
| 灼烧 DoT / 穿透 / 击杀回血 / 顿帧 / Debug 控制台 | — |
