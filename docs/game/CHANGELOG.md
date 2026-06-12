# 📝 《仙途秘境》修改记录（CHANGELOG）

> **用途**：按时间/版本记录**已做的改动**（设计决策 + 代码落地）。最新在上。
> **配套**：未来要做的看 [开发待办](design/开发待办.md)；设计权威看 [GDD](design/GDD_仙途梦境.md)。
> **维护约定**：每完成一波改动，在顶部按日期/版本追加一节；细节可链接到 GDD 对应章节。

---

## v0.6.3 · GDD 逻辑迭代（2026-06-12）

GDD 对照审计后的一轮系统实装，覆盖 7 项 gap。

### 三选一灵物卡牌（GDD §5.6）
- 新增 `BattleRewardUI`（UITK）：战斗房通关后弹出 3 张灵物卡片供选择（或跳过），取代旧的地面直接掉落。
- `BattleRoom.OnRoomCleared()` 改为先 `RollRewardCandidates(3)` → 弹 UI → 选完才发布 `RoomCleared` 开传送门。
- 独立 roll 逻辑：不重复、按品阶权重、受 `FeatureFlags.EnableSpiritItems` 和通关掉落概率控制。

### 化身配表 runtime（GDD §4.9）
- `ItemData` 新增 `configId` 字段（对接 `Item_InRun_Config`，与 `SkillData.configId` 对齐）。
- 新增 `AvatarRestriction.cs`（静态工具类）：
  - `GrantDefaultItem(pool)`：入秘境时按 `Avatar_Base_Config.DefaultItem_ID` 匹配 configId 赋初始灵物。
  - `IsAllowed(item)` / `FilterPool(pool)`：黑名单过滤（`Restriction` 字段），已接入 `BattleRoom.RollRewardCandidates` 和 `ShopRoom.GenerateShopItems`。

### 天赋树渐进解锁（GDD §9.1.7）
- `SaveData` 新增 `unlockedGrowthBranches` 列表。
- `GrowthUITK`：首次打开自动解锁每化身第一条分支；第二条分支显示 🔒 提示（"需机缘/成就解锁"）。
- 外部 API：`GrowthUITK.UnlockBranch(avatar, branchLabel)` 供机缘/成就回调。

### 御物傀儡技能同步（GDD §4.3.5）
- `SpiritRootEarthController` 订阅 `SkillCastStarted` → 玩家释放 Q/E/R 时所有活跃土傀立即跟随开火。
- `EarthPuppetTurret` 新增 `SyncFire(damage)` 方法（伤害 = 玩家攻击 × 10% × 坐镇倍率）。

### 枯荣逆旅被动补全（GDD §4.3.2 注）
- `SpiritRootWoodController` 三项补全：
  - 全局种子计数器 `_totalSeedCount`，上限 99（`globalSeedCap`）。
  - 技能命中不再消耗种子（改为"共鸣"额外伤害），仅主动 `DetonateSeeds()` 消耗。
  - 常驻被动 `KuRong_Passive` StatusEffect：每层种子 +0.1% 攻击（`passiveDmgPerSeed`），实时刷新。

### §6.5 槽位修饰扩充
- 8 个技能 SO 新增 `modifierDefs`（MCP 批量设置）：
  - 烈焰斩（焚天/寒霜）、寒冰封印（永冻/雷冰）、烈焰掌（焰灭/冰火）、御剑术（雷剑/焰剑）
  - 影步（风刃/霜步）、混沌吞噬（焚烧/雷霆）、轮回一击（焚天/雷裂）、水镜术（寒流/沸腾）
- 总覆盖 14/30 技能（剩余为 Buff/Heal/AvatarSpecial 类型）。

---

## v0.6.2 · 八条反馈处理（2026-06-10）

一轮玩家反馈修复 + 系统增强，覆盖 UI/掉落/洞府/撤离。

- **问题1 化身卡片标签**：删除仅首张卡片的「★ 本命」badge，改为所有化身卡片均显示角色定位标签（近战·御金/续航·御木/机动·御水/爆发·御火/召唤·御土）。`SpiritRootDef` 新增 `roleTag` 字段。
- **问题2 Buff 悬停说明**：`BuffBarUITK` chip 从 `PickingMode.Ignore` 改为 `Position`；新增 UITK tooltip（PointerEnter/Leave），展示 displayName/description/剩余时间/属性修正明细。
- **问题3 灵物掉落开启**：`GameConfig.启用灵物系统` 默认值改 `true`（代码 + SO asset），恢复全套灵物掉落/拾取/协同/商店。
- **问题4 撤离后洞府残留**：`EnterVillageHub()` 新增清理逻辑——销毁遗留的 `ExtractPoint`、`LevelTransition` portal、`InnerDemonCatalyst`，并调用 `CleanupLeftoverPickups()`。
- **问题7 闭关成色加成**：`CultivationSuppression` 新增 `JingjieQuality` buff——读取 `GetRealmQuality()` + 独立 `QualityBonus[]` 数组，按成色给全属性加成（瑕 0%/凡 +2%/上 +5%/完美 +8% 攻击/血量/减伤），让凝实有实际意义。
- **问题8 撤离倍率+成果面板**：新增 `ExtractResultPanel`（UITK）—— 撤离成功弹出面板展示灵力/历练/素材明细 + 层深倍率（每层 +15%）。`InsightSystem.CommitOnExtract` 和 `CultivationSystem.CommitOnExtract` 均支持 multiplier 参数。

---

## v0.6.1 · 旧技能 SO 重映射（2026-06-09）

将 9 个 `configId=0` 的遗留技能 SO（御剑术/影步/烈焰掌/寒冰诀/天雷引/回春术/雷锁链/镜花水月/傀儡术）接入数据驱动层：

- **`Skill_Base_Config.csv`** 追加 ID 21-29，含名称/描述/品阶/类型/冷却/伤害倍率。
- **CSV→JSON 重导**：`Skill_Base_Config.json` 现含 29 行。
- **SO configId 赋值**：通过 Unity MCP 批量设置 9 个 SO 的 `configId`（21-29），运行时 `SkillTuning` 自动查表覆盖 CD/伤害。
- **BaseDamageRatio = 10000**（100%）：向下兼容 SO 原有 baseDamage，后续可在表里直接调数值。

---

## v0.6 · 体验打磨小批次（2026-06-09）

一轮非阻塞的体验改善，覆盖掉落/分类/视觉/UI 安全。

- **保底奖励重抽**：新增 `SkillPickup.PickValid` — Fisher-Yates 洗牌后跳过其他化身专属，6 处掉落点（BattleRoom + 5 种敌人）统一调用。不再出现"通关了但什么都没掉"（原 ~13% 概率）。
- **丹药归类 + 协同重挂**：回灵丹/灵藤草 → 护体，聚气丹 → 异变（SO + Demo1DataCreator）；`SynergySystem` 11 条 Pill 组合全改 Defense/Anomaly（去重微调阈值）；"丹元归元"→"归元护体"。`Pill` 枚举保留防序列化错位。
- **化身专属门控 UI**：`SkillPickup.BuildPromptData` 专属技能标注 `[专属]`，非本化身显示"化身不符，无法装备"红字并拒绝 `TryPickup`。类型标签增加 `AvatarSpecial → "化身专属"`。
- **持续状态光环**：`HeavenEarthShift`（天地大挪移·绿环脉冲）、`SpiritRootWaterController`（息影瞬步·蓝环脉冲）、`LethalGuard`（金蝉脱壳·蓝环脉冲）——buff 期间脚下常驻 LineRenderer 圆环，结束时销毁。
- **灵脉道具差异化掉落**：深层(≥3层)宝藏房 40%「地脉精华」+100；渡劫突破奖励「洞天残核」+200（`CultivationSystem.Breakthrough` 掉落）。

## v0.6 · 秘境异象联动道心/因果/机缘（2026-06-09）

深化秘境异象——从纯数值调整升级到与道心、因果、机缘系统交互，让异象真正改写玩法节奏。

### 新增 3 种联动异象
- **道心试炼**（`DaoHeartTrial`）☯：道心变动幅度×2；入定(≥80)额外攻击+10%，入魔(<20)额外减伤-15%。修心者得利、堕者速亡。
- **因果轮回**（`KarmaEcho`）⚖：每清完一个房间结算一次——因果债>0 受反噬伤害（债×2% 最大HP），善缘<0 获治愈回馈（1.5%/点）。
- **机缘频现**（`OpportunityRush`）✨：洞府机缘触发率×2、灵力/悟性获取+20%，但敌人攻击+15%。高收益高风险。

### 现有异象增加联动
- **血月** 🩸：击杀时自动积因果+1（杀戮积业），与因果轮回叠加时后果严重。
- **心魔滋生** 😈：每过一层（Realm Breakthrough）道心-5（魔气侵蚀），与道心试炼叠加时入魔更快。

### 集成点
- `PlayerStateHooks.ChangeDaoxin`：乘 `DaoxinDeltaMul`（道心试炼时 ×2）。
- `MoralEffects.ResolveDaoHeart`：入定/入魔档加挂 `DaoTrialAtkBonus` / `DaoTrialDmgRedPenalty`。
- `InsightSystem.AddRunInsight`：乘 `InsightGainMul`（机缘频现时 ×1.2）。
- `CaveOpportunitySystem.OnReturnToCave`：机缘概率乘 `OpportunityMul`（机缘频现时 ×2）。
- `EnemyDamageMul` 改为组合计算（血月 + 机缘频现可叠加）。
- `DebugConsole`：新增 3 个异象调试按钮。

## v0.6 · 成长树做厚：系精通根基分两支（2026-06-09）

回应"分支多点、自由 bd"。原系精通=每化身**单条 3 节点线性链**（变化少）→ 改为**根基 + 两条分支**的小树。

- `SystemMasteryRegistry` 重构：`MasteryNode` 加 `tier`(0根基/1分支/2质变) + `branchLabel`；`MakeNode` 支持**多 `StatModifier`**。每化身本命系 = **1 根基 → 2 分支 ×（分支节点 + 质变节点）= 5 节点**。
  - 价格 灵力 40/60/110；亲和★ 门控 1/2/3；前置 根基→分支→质变。点完根基后两分支起点**同时开放**，自选深入方向（机会成本）。
  - 分支主题：业火 焚势/燎原 · 剑魄 锐金/御金 · 青囊 藤蔓/养元 · 影刃 遁影/虚空 · 御物 造傀/坐镇。效果为常驻 `StatModifier`（攻/暴击/暴伤/攻速/血/减伤/穿透/移速），入秘境 `SystemMasterySystem.Apply` 挂常驻 buff，直接影响战斗。
- `GrowthUITK`：系精通段按 `branchLabel` 分组（◈ 分支头）+ 按 tier 缩进（●/├/└），未解锁显示"需先点前置"。
- play 验证：剑魄树渲染根基「淬锋」+ 两分支「锐金·破阵 / 御金·铁壁」；点亮根基→两分支起点解锁、质变按前置解锁、灵力扣减/状态刷新均正常。

## v0.6 · 重站点 UITK：升级台 + 商店（uGUI→UITK）（2026-06-09）

收尾最后两个重 uGUI 站点，全屏/覆盖层 UI 全面 UITK 化（世界空间元素仍 uGUI）。**保留全部业务逻辑，仅重搭视图**。

- **✅ 升级台 `UpgradeRoom` 改 UITK**（`Resources/UI/UpgradeRoom.uxml/.uss`）：3 槽（Q/E/R）×3 升级（伤害+15% / CD-10% / 充能+1层）卡片，价格递增、充能上限 3、灵力碎片不足置灰——逻辑（`GetUpgradePrice`/`OnUpgrade*`/`TrySpend`）原样保留，视图改 `UIDocument`+卡片构建。绿色主题。play 截图验证（焰石术卡显示伤害/CD/充能/已升级，按钮按碎片余额置灰）。
- **✅ 商店 `ShopRoom` 改 UITK**（`Resources/UI/ShopRoom.uxml/.uss`）：5 商品卡（灵物/功法混排，沿用 `GenerateShopItems` 滚动 + `CalculatePrice`/`CalculateSkillPrice` + `OnBuyClicked` 购买/装备/灵物入槽逻辑）；色条/图标/名称/副标/简效/价格/购买按钮；**悬停 tooltip 简化为面板底部固定栏**（左侧金色描边，显示名称+描述+数值+价格），替代旧鼠标跟随 IMGUI tooltip；买不起 chip 走 `shop-buy--poor` 红态。紫金商人主题。play 截图验证（5 功法卡 + 底部 tooltip 显示「金钟罩（玄品·功法）」详情）。
- 两站点均：`<Style src>` 内联样式表（避开 `Resources.Load<StyleSheet>` 旧缓存坑）、共用 `AvatarSelectPanelSettings`、`sortingOrder=10`、面板随房间销毁。删除两文件中的旧 `CreateText`/uGUI 构建代码与 `EventTrigger` 悬停。
- 至此重站点 IMGUI/uGUI 覆盖层基本清零（剩余仅 `SettingsUI` 等轻量项，可后续按需）。

## v0.6 · 设计收敛：砍系叠层 + 局外一棵树 + 货币统一「灵力」（2026-06-09）

策划讨论后的减法（详见 [设定_御灵五系.md](design/设定_御灵五系.md) §3/§5/§7 与 v0.6 取舍记录）。

**设计决策**
- **砍掉"系叠层 / 御灵之路 in-run 系 tag 构筑"**：与现有"灵物分类协同(`SynergySystem` 30 条)+功法"重复、与局外成长树冲突、非必需。局内构筑沿用现有系统。合技 / `ElementTag`升格 一并取消。
- **局外两棵树合并为一棵"化身成长树"**：原"系精通节点树" + "天赋树"合并（避免重复，正是之前察觉的冲突）。"系"仅作节点分类主题。
- **货币统一为「灵力」**（原"悟性"改名）：局外成长唯一货币。

**代码**
- `CultivationSystem`：境界突破改为发**灵力**（`accumulatedInsight += 60/阶`，里程碑守卫防重领），不再发精通点。
- `SystemMasterySystem`：系精通加点改为**消耗灵力**（`InsightSystem.SpendPermanentInsight`）；`SystemMasteryRegistry` 节点成本改灵力量级（铺垫40/关键70/质变110）。
- `GrowthUITK`：头部只显示「灵力」；系精通 + 化身天赋两段统一花灵力（同一棵树、同一货币）。play 验证。
- 全局"悟性"显示改名「灵力」：`RunHUD`(灵力条/飘字)、`CodexUITK`(天赋成本)、`CaveOpportunitySystem`(机缘奖励)、`WuDaoCushion`(模块定位)。`accumulatedInsight` 字段名内部保留。
- 注：`SaveData.masteryPoints`/`talentPoints` 字段保留但不再使用（旧档兼容）。

## v0.6 · 专门 buff 栏（StatusEffectHUD → BuffBarUITK）（2026-06-09）

反馈："buff 需要一个专门显示的栏位"。旧 `StatusEffectHUD`（顶部居中 IMGUI 条）灰盒、跟新 UITK 风格不统一、且与顶部境界条挤在一起。
- 新增 **`BuffBarUITK`**（UI Toolkit 状态栏）：把玩家所有具名 `StatusEffect` 显示为 chip——名称 / ×层数 / 倒计时 + 底部时间条；**buff 绿边、debuff 红边**；每帧增量对账（不整条重建）。
- 位置改为**左上角血条下方**（避开顶部居中的境界 IMGUI——UITK 在 IMGUI 之下会被遮挡）；`pickingMode=Ignore` 不挡输入；主菜单时自动隐藏；`sortingOrder=5`。
- 删除旧 `StatusEffectHUD.cs`，`GameManager`/`MainMenu` 引用改为 `BuffBarUITK`。play 验证 buff/debuff/常驻 chip 显示正常。
- **即将到期闪烁**：状态剩余 ≤3s 时 chip 脉冲闪烁（`Time.unscaledTime` 正弦），提醒玩家 buff 快没了。

## v0.6 · 召物攻击视觉反馈补强（2026-06-09）

反馈："玩御物只看见怪挨打、看不到攻击"。根因：召物攻击只在敌人身上爆一下、缺"从召物射出"的过程。
- **御物土傀**（`EarthPuppetTurret`，被动土傀 + 兵阵合一共用）：开炮时加 **炮口闪光** + 一道 **土黄冲击束（傀儡→敌人）** + 命中爆，攻击来源/过程可见。
- **御金飞剑**（`FlyingSwordSwarm`）：突刺改为 **从最近飞剑到敌人的全程束线**（原为固定短线、够不到远敌）+ 命中火花。
- 复用 `FxFactory.SpawnSliceLine` / `SpawnElementBurst`。

**手感一致性补齐**（`EnemyBase.OnDamage`）
- **兜底打击火花**：未配 `hitVFXPrefab` 的敌人原来命中只闪白、无火花；现兜底 `SpawnElementBurst`，保证每次命中都有打击点。
- **暴击轻微震屏**：暴击额外 `CameraShake.TriggerLight()`。
- 注：核对发现飘字(`DamagePopup` 池化/分类/暴击缩放)、受击闪白、顿帧(`HitStop`)、击退、玩家受击(`DamageFlash`+边缘红 `PulseVignette`) 等反馈本就齐全；本轮主要补召物攻击来源与命中火花一致性。

## v0.6 · 阶段C 核心：系精通 + 境界重定位 + 成长页（2026-06-09）

局外成长地基落地（[设定_御灵五系.md](design/设定_御灵五系.md) §5/§7）。

**关键决策**
- 精通点由「境界突破」发放（里程碑制）；**天赋仍花悟性**（保留现有系统、让悟性有意义），精通点专用于系精通。
- 本体境界改为**终身保留**（§7「累积只增」）：死亡只丢"本局未撤离历练"，境界/精通/已点系精通/银行历练值均保留（避免重练重领点数的漏洞）。

**数据层**：`SaveData` v2 新增 `masteryPoints` / `talentPoints`(预留) / `masteryNodeIds` / `realmMilestonesGranted`（JsonUtility 加性字段，旧档自动兼容）。

**境界**：`CultivationSystem.Breakthrough` 成功后 `GrantBreakthroughPoints()` 按里程碑补发精通点（`realmMilestonesGranted` 守卫防重领，每阶 +2）；`ReincarnateOnDeath` 不再归零境界/修为/成色。闭关→修为→渡劫突破链路（`MeditationChamber`）保持不变。

**系精通**：新增 `SystemMasteryRegistry`（5 化身×5 系亲和谱★上限 + 各化身本命系 3 节点链 铺垫→关键→质变，节点 `apply` 为常驻 `StatusEffect` 属性 buff）+ `SystemMasterySystem`（加点校验：未点/点数/前置/亲和★；`Allocate` 扣点持久化；`Apply` 入秘境挂当前化身已点节点）。`GameManager.StartNewRun` 接入。

**成长页**：新增 `GrowthUITK`（UITK，复用 `<Style src>` + PanelSettings + sortingOrder12）——头部 境界/悟性/精通点；系精通本命系加点（前置门控）；化身天赋参悟（花悟性）。**悟道蒲团**入口改为打开此页（旧 IMGUI 面板移除）。play 验证：突破发点、加点链前置解锁、天赋参悟、UI 实时刷新均正常。

**不在本批**：御灵之路 in-run 系 tag 构筑系统（节点"阈值/合技"语义依赖它）、`ElementTag`→系标签升格、五系全节点图（先本命主线 MVP）。

## v0.6 · UITK 修复：面板缩放 + 样式缓存坑根治（2026-06-09）

修复"ESC 暂停 / 选化身页等 UITK 面板在实际分辨率下大小/位置奇怪"。
- **缩放**：`AvatarSelectPanelSettings.m_Match` 0→0.5（ScaleWithScreenSize 宽高均衡匹配，UITK 推荐默认）；10 个面板共用一处改全局生效。
- **样式缓存坑根治**：选化身/图鉴/暂停/主菜单 4 个面板原用代码 `Resources.Load<StyleSheet>` 加载 uss，偶发拿到**空规则缓存**导致整页裸奔（全宽堆叠）。统一改为 UXML `<Style src="X.uss">` 随 VisualTreeAsset 引用加载（与已修的 SettingsUI 一致），移除代码加载。play 验证选化身 + 暂停恢复正常居中/样式。

## v0.6 · 阶段B 收尾：御物坐镇聚灵 + 御金塑金/磁牵（2026-06-09）

补完阶段 B 剩余玩法，阶段 B（化身重构）✅完成。

**御物：`扎根` → `坐镇聚灵`（重写，贴合召物身份）**
- 站立 1.2s 进入指挥官姿态：自身**减伤 +40%**（经 `PlayerController.OnDamage` 钩子 `ScaleIncomingDamage`）、**土傀增伤 +60%**（`EarthPuppetTurret.GlobalDamageMul`）、**召唤加速**（间隔 4s→2s）+ **上限 +1**、移速 -50%；移动即解除。
- 去掉原"站桩攻击 +25%"通用增益——把强度从自身转移到召物。`地脉护盾`(每5件灵物1层) 保留为防御副词条。

**御金：补全金属控制三件套**
- **塑金形态**（V 键循环 无→刃→甲→无）：`塑金·刃` 攻击 +25%（StatModifier）/ `塑金·甲` 受伤 -35%（`SpiritRootGoldController.ScaleIncomingDamage` 经 `PlayerController.OnDamage` 钩子）。play 模式验证：刃 100→100 / 甲 100→65 / 解 100→100。
- **磁牵**：灵压爆发(`完美收刀`)前先把 4m 内敌人拉向身前聚拢，再扇形爆发一网打尽（控位 + 聚怪 synergy）。
- 仍保留 飞剑环绕 / 剑心通明 / 一念刹那。
- `PlayerController.OnDamage` 在土化身减伤钩子之后新增御金减伤钩子。

## v0.6 · 御灵五系阶段B：化身重构（业火/御物/剑魄）（2026-06-09）

**统一动作：「叠层引爆」收敛为青囊专属**
- 业火 `业焰印`、御物 `地脉烙印` 均移除（与青囊 `寄生种子` 撞车）；青囊独占叠层引爆。

**业火 → 魔焰献祭**
- 去业焰印；新增 `残血增伤`（越残越猛）、`狂火燃血`（狂火期掉血）、入狂火涨心魔。

**御物 → 召物重心**
- 移除地脉烙印；新增**被动自律土傀**（`TickPuppets`：附近有敌时维持 ≤2 个 `EarthPuppetTurret`）；兵阵合一仍为大招；扎根/护盾暂留待后续重写。

**剑魄 → 御金（金属控制）**
- 保留剑心通明/完美收刀/一念刹那；新增**飞剑环绕**（`FlyingSwordSwarm`：常驻 3 把自律飞剑，每 1.2s 突刺最近敌 攻×0.6）→ "御金"底子。塑金/磁牵待补。

**UI 方向（已定 + 试点完成）**
- 现状：玩家向 UI 多为 IMGUI + LegacyRuntime 字体，天花板低。
- 决定：**全屏/覆盖层 UI 走 UI Toolkit（UXML+USS，AI 友好且可复用 HTML 预览那套设计）**；世界空间 3D 元素留 uGUI。
- **✅ 试点：选化身页改 UITK**（`SpiritRootSelectUITK` + `Resources/UI/AvatarSelect.uxml/.uss` + `AvatarSelectPanelSettings`）。
  - 玻璃拟态卡片、按化身色着色的顶条/名字、本命卡金边+★徽章、hover 抬升/缩放过渡、字体清晰。对比旧 IMGUI 灰盒明显提升（已 play 模式截图验证渲染正常）。
  - 调用方（`VillageHub`/`GameManager`/`PauseMenu`）已切到 `SpiritRootSelectUITK`；旧 `SpiritRootSelectUI`(IMGUI) 暂留可回退。
  - ⚠️ 已知：**IMGUI 总绘制在 UITK 运行时面板之上**。选化身页在村中只与边角 RunHUD 共存，中央面板不被遮挡 OK；后续全屏 UITK（暂停/图鉴）若与全屏 IMGUI 共存需注意层级（最终把全屏 IMGUI 一并迁 UITK 即可根除）。
- **✅ 图鉴页改 UITK**（`CodexUITK` + `Resources/UI/CodexUI.uxml/.uss`，复用同一 PanelSettings）。
  - 3 标签（灵物/协同/化身天赋）+ 筛选 chip + 滚动列表；行带品阶/系别色左边框与圆点、名字按品阶着色、元素 tag、激活/已悟状态。三标签均 play 模式截图验证。
  - 调用方 `PauseMenu`/`MainMenu` 已切到 `CodexUITK`；`MainMenu.OnGUI` 加守卫——图鉴可见时主菜单 IMGUI 让位（避免 IMGUI 盖住 UITK）。旧 `CodexUI`(IMGUI) 暂留可回退。
- **化身文案清理**：业火/御物/剑魄 在 v0.6 重构后，`SpiritRootRegistry` 的机制名/被动/简介同步更新（业火→魔焰献祭·越残越猛；御物→召物斗法·自律土傀且 `mechanicEnabled=true`；剑魄→御金·飞剑环绕），去掉过时的"待落地/v0.3 减半"等内部备注。
- **✅ 暂停菜单改 UITK**（`PauseMenu` 原地重写为 UITK + `Resources/UI/PauseMenu.uxml/.uss`）。
  - 5 按钮（继续修行/仙物图鉴/设置/返回主菜单/退出游戏）+ UITK 确认对话框（返回主菜单/退出）；保留原静态 API（Ensure/Show/Hide/Toggle/IsVisible）、ESC 输入与 `IsBlockedByOtherUI` 逻辑。已 play 模式截图验证菜单 + 确认框。
  - 暂停菜单不在主菜单出现，无需"让位守卫"；`SettingsUI` 仍 IMGUI（打开时盖在 UITK 暂停层上，可接受，后续可一并迁移）。
  - 注：`PauseMenu.cs` 已无 IMGUI，旧 OnGUI 版被替换（非并存）。
- **✅ 设置页 + 主菜单改 UITK**（`SettingsUI` / `MainMenu` 原地重写）。
  - 设置：3 标签（音频滑条 / 画质 chip+全屏开关+分辨率 / 控制键位）；用 UITK 原生 Slider/Toggle。
  - 主菜单：标题 + 5 按钮（入秘境主按钮 / 继续修行〔无存档置灰〕/ 图鉴 / 设置 / 退出）+ 存档信息/版本号；`UIDocument.sortingOrder=0`。
  - **UITK 层级**：弹层按 `UIDocument.sortingOrder` 分层——主菜单 0 < 选化身/暂停 10 < 图鉴 12 < 设置 14，确保从主菜单/暂停打开的弹层在上方。
  - **去掉 MainMenu 的 IMGUI 让位补丁**（主菜单已是 UITK，不再需要）。
- **游戏内 HUD 守卫**：主菜单改 UITK 后，IMGUI 的 `RunHUD`/`SpiritRootMechanicHUD`/`StatusEffectHUD` 会透到主菜单上方（IMGUI 永在 UITK 之上），已加 `if (MainMenu.IsVisible) return;` 守卫。
  - 修复编译歧义：`Cursor` ↔ `UIElements.Cursor` → 全限定 `UnityEngine.Cursor`。
- **UITK 经验**：`SettingsUI.uss` 经 `Resources.Load<StyleSheet>` 偶发拿到空规则缓存（与 `LoadAssetAtPath` 不一致）。改为在 UXML 用 `<Style src="...uss">` 随 VisualTreeAsset 引用加载即稳定；ScrollView 显式设 `mode=Vertical` + 隐藏水平滚动条。
- 下一步：商店/升级台/机缘等剩余 IMGUI 界面；选化身/图鉴的旧 IMGUI 版确认稳定后删除。

## v0.6 · UITK 第二批：5 个游戏内覆盖层（2026-06-09）

把剩余 5 个游戏内 IMGUI 覆盖层迁到 UI Toolkit，沿用 `<Style src>` 随 VisualTreeAsset 引用样式、复用 `AvatarSelectPanelSettings`、`sortingOrder` 分层。各面板保留原静态 API（`Show/HideImmediate/IsVisible`），调用方不改。全部 play 模式截图验证。

- **✅ 机缘 `CaveOpportunityUI`**：标题+正文+选项 → 结果页「离去[Enter]」关闭。`sortingOrder=10`。
- **✅ 房间三选一 `RoomChoiceUI`**：按 RoomType 着色卡片 + 图标（汉字单字，避免默认字体缺 ⚔/✦ 字形）+ 数字键 1/2/3 热键 + 点击 → `onSelected`。
- **✅ 奇遇 `StoryEventUI`**：标题/正文 + 选项按钮（`enableRichText` 保留因果/道心/寿元/奖励/代价彩色标签）→ `onSelected`。
- **✅ Boss 字幕 `BossDialogueUI`**：底部橙边横幅、自动逐行播报（`Update` 计时切行），`pickingMode=Ignore` 不阻挡输入。
- **✅ 仙山舆图 `TreeMapUI`**：节点横向铺开 + **`generateVisualContent`/Painter2D 自绘连线**（当前路径金色、其余灰色）+ 数字键/点击选择 + 图例（道心/因果/寿元）+ readOnly 查看模式。`sortingOrder=11`。
- 本批未含：商店/升级台/炼器（重站点 UI）、`InventoryUI`（已是 uGUI）——留后续。
- **✅ 清理**：删除已被取代的旧 IMGUI `SpiritRootSelectUI.cs` / `CodexUI.cs`（确认无代码引用后删除，doc 注释 `<see cref>` 一并清理），编译 0 错误。剩余 IMGUI 仅商店/升级台/炼器等重站点 UI。

## v0.5.7 · 打包修复 · 道伤可视化 · 专属技能掉落门控 · Debug 工具（2026-06-05）

**打包 bug：山门按 F 无反应（editor 正常）**
- 根因：玩家死亡挂 3 游戏小时**道伤**(`soulHurtRemainingSec`，存档持久化)，`VillagePortal` 在道伤>0 时**静默吞掉 F 并隐藏提示**；editor 测试存档干净（=0）故无感，build 持久化存档（死过一次）即锁死山门。
- **山门不再静默**：道伤未愈时提示直接显示「🩸 道伤未愈 · 剩 xx」，归零后恢复「按 [F] 入秘境」。
- **道伤 debuff 横幅**：`RunHUD.DrawSoulHurtDebuff()`——道伤>0 时屏幕顶部居中红色脉动横幅「🩸 道伤未愈 · 剩 xx · 无法入秘境」（村子/秘境都显示）。

**专属技能掉落门控（修复：掉错化身的专属技能）**
- 现象：选青囊却掉到影刃的息影瞬步，且放了没效果（控制器按当前化身门控→正确空放）。
- 根因：5 个 AvatarSpecial 进了通用 skillPool，对任何化身随机掉。
- 修复：`SkillData.RequiredRoot`（AvatarSpecialKind→灵根映射）；**`SkillPickup.Spawn` 单一出口门控**——化身专属技能仅对对应化身生成，非该化身一律不掉（覆盖杀怪/宝箱/商店/战斗奖励所有来源）。
- `CastAvatarSpecial` 加诊断 log（`[化身专属] 释放 X（需 Y / 当前 Z）`）。
- 注：有保底奖励处约 13% 概率抽到不匹配→那次空奖励（如需保底重抽再补）。

**Debug 工具（打包可调试）**
- **📜 日志面板**：`DebugConsole` 捕获 `Application.logMessageReceived`（含报错/异常+首行堆栈），按钮展开右半屏日志面板（最近 40 行/缓冲 200）。**打包版也能看 log**。
- **🩹 清除道伤**：一键清零道伤存档并保存（解锁山门，便于测试）。
- **🗡 发当前化身专属技能**：按当前化身找到对应 AvatarSpecial SO 在身边生成掉落，免刷直接测 16-20。
- **精简**：移除当前无用按钮（灵物一键升满/灵物爆率拉满〔灵物屏蔽期〕、推进链式机缘、掉落灵脉道具）；对应方法保留无引用。

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
