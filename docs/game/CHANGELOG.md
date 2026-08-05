# 📝 《秘境探索》修改记录（CHANGELOG）

> **用途**：按时间/版本记录**已做的改动**（设计决策 + 代码落地）。最新在上。
> **配套**：未来要做的看 [开发待办](design/开发待办.md)；设计权威看 [GDD](design/GDD_秘境探索.md)。
> **维护约定**：每完成一波改动，在顶部按日期/版本追加一节；细节可链接到 GDD 对应章节。

---

## 关卡 A · Edgar PRO Grid3D 实体地牢桥接原型（2026-08-04）

- 接入 Meryuhi URPFog `urp14`：源码作为 MIT 嵌入式 UPM 包放入 `Packages/moe.meryuhi.effects.fog/`，`N_RenderData` 已挂 Full Screen Fog Renderer Feature，`N_VolumeProfile` 已添加 Height 模式 Override；初始强度为 0，等待场景美术调参。
- `Game_TripPBR.shader` 的 Mask 贴图通道调整为 SMAE：R 光滑度 / G 金属度 / B AO / A 自发光 Mask。
- 修复主菜单“进入基地”槽位按钮：`SaveSlotSelectUI` 在设置尺寸前为按钮补齐 `LayoutElement`，避免空引用并确保横向布局具有可点击尺寸；同时强化 schema v3 迁移，旧存档缺失的成长、机缘与精通集合会自动补齐并写回。
- GDD §7.2 冻结关卡 A 首版产能：每局 14 个主体房（普通 5 / 精英 2 / 事件 2 / 商店 1 / 休息 1 / Boss 2 / 军械库 1），正式模板库为 12 个主体房 + 4 个连接模板；逐项列明分区主题、用途、Doorway 与内容槽要求。
- 确认本地 `Assets/1Game/1Pack/ScriptPackage/Edgar/` 为完整 PRO 包，包含 `DungeonGeneratorGrid3D`、Grid3D 房间模板、门与官方 3D 示例；导入后 Unity 编译 0 error。
- 新增游戏侧 `Resources/LevelDesign/EdgarGrid3D/` 原型资源：14 房 LevelGraph、3 个房间模板、2 个走廊模板与独立 GeneratorSettings，不在运行时直接依赖官方示例图资产。
- 新增 `EdgarDungeonRuntime`：按 Seed 生成实体地牢、提取房间落点/尺寸、战斗开始锁门、清场后解锁。
- 根据首轮反馈将生成根整体放大为 **5 倍**；生成时机从“进入准备区前”延迟到“正式进入首个地牢房间”，避免完整 Edgar 地牢与村庄/准备房重叠。
- Edgar 战斗房关闭旧 `RoomBuilder` 房间几何、陷阱和可破坏物注入，修复两套默认场景内容叠在一起。
- 修复 Edgar Basics 占位材质粉色：复制 Floor/Wall/Door 到游戏侧 `EdgarGrid3D/Materials`，转换为 URP Lit 并重映射 5 个游戏侧房间/走廊模板，不修改第三方包。
- 新增 14 个实体房间触发区；玩家步行进入未清房时激活遭遇，清场解锁门，已清房可回头通过且不重复刷新。
- 移除 Edgar 流程的逐房传送门：普通房清场只改变房间状态；原型区域配置两个 Boss 房，两个 Boss 均击败后才生成进入下一层的出口。
- 移除过关三选一调度并重新启用世界掉落；战斗房清场调用技能/模块掉落，敌人原有技能与材料掉落同步恢复，掉落物在常驻地牢换房时不再被清理。
- 修复运行时首次添加 `DungeonGeneratorGrid3D` 时其 `Awake` 抢跑生成的问题：配置注入前临时停用宿主，防止 null FixedLevelGraphConfig。
- 新增默认 `EdgarMapProvider` 并替换 `GameManager` / `MapProviders.Current` 的 silverua 默认注入；Edgar 实体门廊承担导航，不再弹 STS 全图。
- `RoomSpawnContext` 增加外部几何开关；战斗房落到 Edgar 房间时跳过旧 `RoomBuilder`，保留现有敌人、奖励、Boss 与数值流程。
- 使用 Fantastic Dungeon Pack 的 URP 模块直接拼装首批正式 Edgar 模板：6 个房间、2 个走廊及门连接/封堵件；`PrototypeLevelGraph` 已切换引用这些模板。
- 翻转 8 个模板内全部 150 个 `COMP/P_MOD_Wall_01_O_straight_med` 墙体组合，使装饰面统一朝向房间内部；翻转后模板诊断仍为 8/8 通过。
- 修正 Grid3D 门边界坐标：East/South 门按 Edgar 格子边语义重新对齐；8 个模板全部通过 `RoomTemplateDiagnosticsGrid3D`。
- Edgar 战斗房改为读取实体模板的 `EnemySpawn` / `BossSpawn` 内容插槽；取消在房间 Bounds 内随机坐标刷怪，Boss 不再使用固定 `+Z` 偏移。
- 每个 `BattleRoom` 独立持有本房敌人集合，清场判断不再扫描全场景 `Enemy` 标签；远程、冲锋、法师、精英和 Boss 均纳入本房计数。
- 当敌人总量超过可用插槽时自动分波生成，每波优先排除玩家 5m 内的入口插槽；精英房保底生成精英，同一区域敌群组合改用真实房间序号推进。
- PlayMode 冒烟测试：固定 Seed 生成 14 房；目标房识别 6 个敌人插槽、1 个 Boss 插槽，7 敌配置首波正确生成 6 个，Unity 编译 0 error。
- 删除 Edgar 生成失败时回退旧 `RoomBuilder` 的路径；模板或配置异常现在直接抛出带 Realm/Room/Seed 上下文的错误，避免用旧房间掩盖故障。
- 固定 Seed `20260804` PlayMode 冒烟测试通过：生成 14 个 Fantastic Dungeon 实体房间，Unity 编译 0 error。
- PlayMode 验证：14 个房间触发器、2 个 Boss 房、世界掉落开启；普通房与首个 Boss 清场均无出口，第二个 Boss 清场后出现出口；`RewardPickUI` 未显示；运行场景无不支持 Shader。
- silverua/StsMap 暂未物理删除，待 Edgar 区域主流程 Playtest 通过后再清理。

---

## 关卡 A · 生成器依赖清理，等待 Edgar PRO（2026-08-04）

- GitHub 免费 Edgar `v2.1.0` 经实际包内容确认仅包含 `Grid2D`，没有目标所需的 `DungeonGeneratorGrid3D / RoomTemplateSettingsGrid3D / DoorGrid3D`；已移除本地嵌入 Package、`manifest.json` 与 `packages-lock.json` 记录。
- 物理删除 `Assets/1Game/1Pack/ScriptPackage/DunGen/` 全套代码、编辑器、集成、样例与文档；游戏脚本没有 DunGen 外部引用。
- 生成器方向确定为 **Edgar PRO Grid3D**。等待用户导入 PRO 包后制作最小实体地牢；在替代流程验证通过前保留 silverua STS 过渡地图，避免当前游戏主流程中断。
- Unity 重新解析 Packages 后编译 0 error。

---

## 关卡 A · 区域式地牢目标写入 GDD（2026-08-03）

- GDD §7 将后续关卡目标从固定 6 层 / STS 单 Boss 路线调整为《遗迹 2》式区域 Roguelite；现有 silverua 地图明确为过渡实现，不伪记为已完成。
- 冻结关卡 A「地牢」的宏观分区：外环牢区、连接区、内环祭殿；外环与内环各有一个独立 Boss，所属分区固定、具体候选房随机，暂定双 Boss 均击败后开启出口。
- 明确随机边界：随机降落、Boss/军械库候选位置、连接、捷径、事件与敌群；区域语义、双 Boss 分区和装备 A 的内环军械库来源固定。
- 将布局模板与故事模板拆分：布局负责空间拓扑与内容槽，多组故事模板负责双 Boss、事件、对白、门状态及奖励主题。
- 材料直接刷新在普通战斗房，不新增材料房；材料局外用途、死亡损失和基地建设规则暂缓。
- 写入生成顺序、可达性约束、地图数据目标及当前实现差距，并在 §11.2 记录已确认结论 Q-015、在 §11.4.9 登记后续实现顺序。

---

## V0.4.1 · 局内解锁与三槽存档方案修订（2026-08-03）

- **Build 不再带出**：死亡、主动退出或通关后清空本局技能、模块和增强链，不生成 Build 快照，也不允许在局外装备后带入下一局。
- **获取即永久解锁**：技能/模块在局内第一次实际获取时，立即登记到当前玩家进度存档，不以通关为前提。
- **取消大秘境**：物理删除 `Scripts/Rift/`、`BuildSnapshot`、`BuildBackpackUI`，移除村庄入口、`GameManager.InRift` 分支与 Build 保存调用；普通秘境准备区返回基地功能保留。
- **保留装备反馈**：Build 不带出不影响局内交互；任何技能/模块装备或替换完成后，下方技能栏仍必须立即同步变化。
- **三槽纵向存档**：保留 3 个独立玩家进度槽，存档选择界面改为从上到下展示；已有槽直接读取，删除/覆盖使用独立按钮并二次确认。
- **存档 schema v3**：新增模块永久解锁与累计游玩时间；旧 Build/遗产模块在加载时迁移为发现记录并清空，不恢复为可装备 Build。
- **解锁触发落地**：初始技能、战后奖励、世界拾取、商店、模块入包及自动装配均接入幂等解锁与即时保存；图鉴按当前槽显示已发现/未发现状态。
- **运行态隔离**：回基地及新开局清空技能、模块背包和增强链；模块链变化统一刷新技能栏。
- 已同步 GDD §11.4、开发待办与程序架构说明。

**验证**：Unity 2022.3 强制刷新后 C# 编译 0 error / 0 warning；内存构造 v2 JSON 的迁移烟测通过（技能去重、链模块与遗产模块转入解锁列表、旧 Build 清空、schema 升至 v3）。未进入 Play Mode，避免影响当前未保存的 Lookdev 场景。

---

## V0.4.2 · PC 高品质 Bloom（2026-07-31）

- URP Bloom 新增 `PC` 模式，移植并适配 DanbaidongRP 的抗闪烁预过滤、固定三层分离高斯模糊和多尺度权重合成。
- 扩展原始 `Bloom.shader` Pass 4–10 与 `UberPost.shader` 的 `_BLOOM_PC` 合成变体；保留 Default、nBloom 两条现有路径及其序列化枚举值。
- Volume Inspector 在 PC 模式下仅显示专用参数：亮度范围压缩、预过滤增益、PC Tint、四层合成权重。
- 补齐 UberPost Shader variant stripping；PC 模式复用现有 Bloom Material 与 RTHandle 金字塔，不新增后处理 Shader 资源槽。

**验证**：Unity C# 编译 0 error；强制重导入 Bloom/UberPost Shader 均为 0 编译消息。

---

## V0.4.1 · §11.4.7 补充反馈（4 项 · 2026-07-30）

1. **初始技能后移到正式入口**：`PrepRoom` 进入时不再自动弹三选一；玩家触发“秘境入口”后才打开 `SkillSelectUI`，选择完成立即进入 STS 地图首节点。
2. **两个准备区可返回基地**：新增通用 `PreparationGate`。普通秘境准备区与大秘境缓冲区各有“返回基地”门；正式秘境房间和大秘境挑战间不生成返回门。
3. **每境总节点 24~26，拆为三路线**：`MapConfig` 新增路线预算模式；`MapGenerator` 将总节点（含共享 Boss）分配到 3 条长短不同路线，不再把 24 层错误解释为“每条路线 24 间”。`SilveruaMapProvider` 改为 1 个初始占位 + 根据当前地图节点 `outgoing` 动态扩展，Boss 也由地图共享收束节点驱动。
4. **修复局内只出技能、不出模组**：
   - 新增 `ModuleCatalog` 与 `Resources/ModuleCatalog.asset`，显式收录 `Data/Modules` 下 59 个 `ModuleDef`，修复 `Resources.LoadAll` 无法读取非 Resources 目录、编辑器注入掩盖打包空池的问题。
   - 混合奖励从每次独立 50% 随机改为“首战模块、之后技能/模块交替”，消除连续只出技能的随机长尾。
   - `InitModuleSystem` 保证玩家持有 `ModuleInventory`；奖励或商店自动装配失败时改为放入背包，不再出现“提示已获得但实际丢失”。

**验证**：Unity 编译 0 error；离线生成 8 张地图均为 24~26 节点、3 个起点、3 路汇入共享 Boss；`ModulePoolLoader` 从 Catalog 读回 59 个模块。未启动 Play，避免覆盖当前未保存的 `CombatArena_Dungeon_Test` 场景改动。

---

## V0.4.2 · 三向 UE 风格 PBR Shader（2026-07-30）

- 新增 `Assets/1Game/Shader/Game_TripPBR.shader`：世界空间三向采样 Base/Normal/Mask，修正平坦法线贴图改变几何法线的问题。
- 新增可复用 `Game_Lighitng.hlsl`：独立封装 UE 风格 Disney Diffuse、GGX NDF、Smith Joint Visibility、Schlick Fresnel 与 Epic 环境 BRDF 近似。
- 材质通道收敛为三张贴图：Base、Normal、Mask；Mask 固定为 `R=Metallic / G=AO / B=Emission Mask / A=Smoothness`。
- UE 漫反射公式适配 Unity 光强标定：保留 Disney Diffuse 曲线，但不重复乘 `1/π`，修复同光照下比 URP Lit 暗约 68% 的问题。
- 旧 `TriplanarHalfLambertPBR.shader` 已删除；测试材质迁移并重命名为 `Game_TripPBR.mat`，保留原 Base/Normal/打包贴图引用。
- Unity Shader 导入验证通过，0 编译消息。

---

## V0.4.1（工程规范）· 场景搭建合规化：美术对象场景预置 + Demo1Setup 按类别拆分（2026-07-30）

**目标**：把「一个 950 行 `Demo1Setup` 运行时 new 出所有东西」改成 Unity 标准做法——美术相关对象放场景里直接调参，运行时实例化的对象按类别拆到不同节点/脚本。

**后处理：改走纯 Unity 默认**
- **删除** `PostProcessSetup.cs`（原来用代码拼 `Volume`+`VolumeProfile`、并随层数变暗变红 / 受击暗角脉冲）。同步移除 `GameManager`（2 处 `UpdateAtmosphere`）与 `PlayerController`（`PulseVignette`）调用。
- 新增标准资产 `Assets/1Game/Settings/Demo1PostProcess.asset`（`VolumeProfile`，含 Bloom/Vignette/ColorAdjustments，值沿用旧默认）。
- 场景「Art/Global Volume」= 标准 `Volume`（isGlobal，priority 1）引用该 Profile；美术在 URP 标准 Inspector 调参，不再有自定义参数面板与动态驱动脚本。

**美术对象场景预置（不再运行时实例化）**
- `Main Camera`（+`TopDownCamera`+`AudioListener`+URP CameraData，`renderPostProcessing=on`）、`Directional Light`、`Global Volume` 直接放进 Demo1 场景「Art」节点，Inspector 可调。
- `Demo1Setup` 仅保留相机/平行光的「场景缺失兜底」（后处理不兜底，走场景 Volume）。

**运行时实例化按类别拆分**
- 场景新增类别根节点 `Systems` / `Gameplay` / `UI`，分别挂新脚本 `SystemsBuilder` / `GameplayBuilder` / `HudBuilder`，生成的对象归到各自节点下（Hierarchy 一眼分类）。
    - `SystemsBuilder`：对象池 / GameManager(+技能池/模块池注入+DebugConsole) / 顿帧 / 层间过渡 / EventSystem / 音效。
    - `GameplayBuilder`：临时地面 / 玩家（含主角档案热构建、攻击原点、战斗组件）。
    - `HudBuilder`：GameCanvas + GameHUD（血条/境界/敌人数/技能栏/连招/碎片/消息/死亡&通关面板/小地图）。
- `Demo1Setup` 瘦身为 Bootstrap：持有配置（技能/模型/特效等）+ 按依赖顺序调度三个 Builder；未指定 Builder 时运行时兜底查找/新建对应根节点。

**验证**：编译 0 错；Play 下 `cameras=1 lights=1 volumes=1`，GameManager/Player/Pool/HUD/EventSystem 各存在且唯一，控制台 0 报错，渲染（相机/光照/Bloom/Vignette/HUD）正常。

---

## V0.4.1 · §11.4.6 BUG 及调整整理（9 项 · 2026-07-30）

**目标**：按 GDD §11.4.6 表逐项修复/调整，落地「无技能开局 + 无掉落 + 统一 3 关 + BD 只在大秘境 + 大秘境选层 + 手动存档」。

1. **[BUG] 局外去技能**：`GameManager.EnterVillageHub` 进村时 `ClearPlayerLoadout()` —— 清空 Q/E/R 三槽 + 3 条增强链并刷新技能栏（普攻独立，不受影响）。开局在 `Start` 即进村，故初始也「挂空」。
2. **[BUG] 完全无掉落**：新增总开关 `GameManager.EnableWorldDrops=false`，在 `SkillPickup.Spawn`/`ItemPickup.Spawn`/`ModulePickup.Spawn` 顶部 early-return（`CaveMaterialPool` 经 `ItemPickup.Spawn` 一并生效）。功法/洞府素材/妖丹/模块/宝箱/遗产模块均不再落地；技能与模块改由局内三选一发放，灵力碎片（货币）仍照常直接结算。
3. **[调整] 每关房间数 20~26**：`DefaultMapConfig.layers` 12→**24**（23 房 + 末 Boss），`GetFloors` 按 `LayerCount` 铺脚手架 → 每境 24 房。仅 1 个 MapConfig，三境共用，房间数一致。
4. **[BUG] 统一 3 关表现**：换境后第一间也走 STS 全图择路（`SpawnLevelCompletePortal` 传送门回调 `SpawnCurrentRoom`→`EnterNextRoomWithChoice`），消除「另一种关卡形式」；HUD `levelText` 由「第 X 层」改为「第 X/N 关」（境内进度），`realmText` 保留境名，不再两个控件都说「层」。
5. **[BUG] 装备台刷新技能栏**：`BuildSnapshot.ApplyToPlayer` 装完后广播 3 次 `SkillEquipped` + 兜底 `SkillBarUI.RefreshSkillSlots()`，让玩家直观确认「已替换成功」。
6. **[调整] 大秘境选层**：新增 `RiftTierSelectUI`（uGUI+TMP，`-10/-1/+1/+10` 步进 + `1/25/50/100` 预设，1~100 层），`RiftManager.OnStartChallengeRequested` 装备 Build 后弹出，选定 → `SetTier` → 开始挑战。
7. **[调整] 局外剔除 BD 系统**：`VillageHub` 移除「配置使」`ModuleConfigNPC` 与「Build 管理使」`BuildManagerNPC`，村庄只留大秘境入口 + 山门；BD 装备只在大秘境缓冲区 `RiftEquipStation`。
8. **[调整] 局外不显示地图**：`Minimap.SetVisible(bool)`；进村/进大秘境隐藏，`StartNewRun` 进秘境显示。
9. **[调整] 暂停菜单存档**：`PauseMenu` 加「存档」按钮 → `SaveSystem.Save()`（与自动存档同底层），点击后按钮短暂显示「已存档」（真实时间协程，兼容 `timeScale=0`）。

**验证**：编译 0 错；`DefaultMapConfig` 运行期读回 `layers=24 lastType=Boss`。

---

## V0.4.1（进度重构线）· 接入 silverua 杀戮尖塔全图作为唯一进度驱动（2026-07-30）

**目标**：把地图彻底换成 silverua [slay-the-spire-map-in-unity](https://github.com/silverua/slay-the-spire-map-in-unity) 原生生成 + 渲染，地图=进度真源、每局重生成、动效用 DOTween；移除旧的 `TreeMapGenerator`/`TreeMapUI`/`RoomChoiceUI` 分叉图路径（换 provider 后不再调用）。

**依赖取舍**（承接 Stage 1）
- 已把 silverua `Scripts/Prefabs/Sprites/Materials/Resources/Scriptable Objects` + 自带 DOTween 落入 `1Game/StsMap/`。
- **不持久化地图**（每局重生成）：剔除 `Newtonsoft.Json`（`Map.cs`/`Node.cs`/`MapManager.cs` 去序列化、`MapManager.SaveMap()` 置空、`Start()` 无条件生成）。
- 去掉编辑器 `OneLine` 特性（`MapConfig.cs`/`MapLayer.cs`）。

**桥接（复用现有 `IMapProvider` 接缝，主循环几乎零改动）**
- `MapPlayerTracker.EnterNode` → 触发静态事件 `NodeEntered`；延时进入改用非缩放时间 `SetUpdate(true)`（地图是 UI 覆盖层，可能在 `timeScale=0` 下操作）。
- 新增 `StsMapScreen`（`1Game/Scripts/LevelDesign/StsMap/`）：运行时代码搭 Overlay Canvas + 半透明遮罩 + 标题 + 两个 `ScrollRect`，实例化 `Resources/StsMapObjectsUI`（原 `MapObjectsUI Variant` 预制体）并回填 `MapViewUI` 的 ScrollRect 引用；负责显隐、按境重生成、`NodeEntered`→回调房型后自动隐藏。为此给 `MapViewUI` 加 `SetScrollRects(...)`。
- 新增 `SilveruaMapProvider : IMapProvider`：每境一张分叉图，`GetFloors()` 用「层数=房间数、末间 Boss」脚手架；`TryShowNavigation` 弹 silverua 全图、点节点后 `NodeType→RoomType`（Minor→Battle / Elite→Elite / RestSite→Rest / Treasure→Treasure / Store→Shop / Boss→Boss / Mystery→Event）；数值查询（敌人缩放/稀有度/阶段返回）仍复用 `ConfigDatabase.MapStructures` 配表。

**GameManager 接入**
- `Awake` 无条件 `MapProviders.Current = new SilveruaMapProvider()`（旧 `LevelDesignMapProvider` 不再成为 Current，旧 `TreeMapUI` 随之休眠）。
- 首房也经地图点选进入（silverua 首层即起点选择）：`OnPrepRoomComplete` → `EnterNextRoomWithChoice`；遗产模块改由本局**首个** `SpawnCurrentRoom` 注入（`_pendingLegacyInject`）。
- 新增 `GameManager.RealmCount`。

**验证（Unity 运行）**：地图生成+渲染正常（左起点→右 Boss 完整分叉图、Kenney 背景板、节点/连线）；模拟点选起点 → `NodeEntered` → provider 回调 `NodeType` → 地图自动隐藏，链路通；`timeScale=0` 下亦生效。编译 0 错。

**遗留清理（同版 · 2026-07-30 收尾）**
- **物理删除**旧地图代码：`LevelDesign/Map/TreeMap.cs`（`TreeMap`/`TreeNode`/`TreeMapGenerator`）、`LevelDesign/UI/TreeMapUI.cs`、`Core/Level/LevelDesignMapProvider.cs`（含 .meta；空 `Map/` 目录一并删）。`RoomChoiceUI` 更早已删。
- **`LevelDesignDirector` 解耦地图**：删 `CurrentMap`/`ShowMap`/`CurrentMapNode`/`MarkCurrentNodeCleared`/`IsCurrentNodeBoss`/`TryTriggerRoomEvent`；只留「整局/区域 Flag & 玩家状态重置」+「Boss 形态解析」。
- **重置职责迁移**：整局/换境的 Flag/状态重置从 `LevelDesignBootstrap.RealmBreakthrough` 移到 `SilveruaMapProvider.StartRun/OnEnterRealm`（内部调 `Director.StartNewRun/BeginAct`）；`Bootstrap` 去掉 TreeMap 节点驱动的 mode A，房间剧情事件统一走线性调度表（mode B），仅保留区域通关 meta 标记（回 realm 0 视为新局复位）。
- `MapProviders.Current` 默认值改 `SilveruaMapProvider`。
- **`MapConfig` 按 Act 分化（预留接口）**：`StsMapScreen.SetActConfig(act)` 从 `MapViewUI.allMapConfigs` 按 (act-1) 选配置，provider 在 StartRun/OnEnterRealm 调用；当前列表仅 1 个 `DefaultMapConfig`（各境共用），追加对应 `MapConfig` 即可分化，无需改代码。
- **`LevelRoomType` 保留**（有意）：它是配表 schema 枚举（`RoomSocketRow.TypeEnum` ↔ CSV RoomType 整数列），非死代码；silverua 侧只用 `NodeType→RoomType`，已不再触碰它，故不合并。
- 验证：play-mode smoke（provider 重置→事件→Floors/缩放/稀有度）全绿，编译 0 错。

---

## V0.4.6 · UI 方案统一为 uGUI + TMP（2026-07-29）

**目标**：全项目 UI 从「UITK + uGUI + IMGUI 三套混用」收敛到**单一 uGUI + TextMeshPro**，统一中文字体与视觉，消除 IMGUI 运行期面板与旧 `UnityEngine.UI.Text`（□□□ 中文缺字）。

**技术栈定调（后续锁定）**：UI 方案统一为 **uGUI 全家桶 —— uGUI + TextMeshPro + DOTween（动效）+ 自定义 Shader（视觉）**。不再新增 UITK / IMGUI 面板；新面板一律走 `UGuiKit`。

**基建**
- 新增 `UI/UGuiKit.cs`：代码化构建库（Overlay Canvas / 遮罩 / 面板 / 文本 / 按钮 / 滑条 / 开关 / 滚动 / 卡片 / 网格 / 属性卡 / 分节标题 + 主题色常量）。所有文本统一走动态中文 TMP 字体 `Resources/Fonts/NotoSansSC SDF`（菜单「仙途秘境/UI/生成中文 TMP 字体资产」生成），并导入 TMP Essential Resources 修复 `TMP_Settings.instance` 为 null。

**UITK → uGUI+TMP（面板重写，删除 UIDocument/VisualElement）**
- MainMenu（试点，已验证）、PauseMenu、SettingsUI、RewardPickUI、SkillSelectUI、SaveSlotSelectUI、BuildBackpackUI、PlayerInfoPanel、CodexUITK、ShopRoom、UpgradeRoom、StoryEventUI、BossDialogueUI、ExtractResultPanel、RiftEquipUI、RiftRewardUI、BuffBarUITK、TreeMapUI（连线改用旋转 Image 细条）。

**IMGUI（OnGUI）→ uGUI+TMP**
- RunHUD、RiftChamber、FormationPlatform（阵法台）、ScripturePavilion（藏经阁）。运行期已无 `OnGUI`（仅剩 Editor 窗口 `ToolSearchWindow`/`ConfigDashboard` 保留 IMGUI，合理）。

**旧 `UnityEngine.UI.Text` → TMP（约 20 文件）**
- HUD/世界 UI：GameHUD、Demo1Setup（HUD 构建器）、SkillBarUI（拖拽/悬停提示）、ProcBarsHUD、ModuleChainProcOverlay、ModuleAssemblyUI（装配台）、DamagePopup、EnemyHealthBar、Minimap、NpcHeadCard、WorldPromptPanel、SkillPickup、DebugConsole。
- 房间/交互：VillageHub、PrepRoom、TreasureRoom、RestRoom、RoomExitTrigger。
- 怪物/战斗飘字：EnemyBoss、EnemyElite、PlayerCombat（链触发提示）。
- 统一映射：`Text`→`TextMeshProUGUI`、`TextAnchor`→`TextAlignmentOptions`、`FontStyle`→`FontStyles`、`horizontalOverflow/verticalOverflow`→`enableWordWrapping/overflowMode`、`UnityEngine.UI.Outline`（TMP 不支持）→ TMP 内建 `outlineColor/outlineWidth`、`supportRichText`→`richText`、`Resources.GetBuiltinResource<Font>`/`UIBuiltins.LegacyFont`→`UGuiKit.CjkFont`。装配台/DebugConsole 的 `CreateText` 辅助保留旧 `FontStyle/TextAnchor` 形参、内部映射，避免改动数十处调用点。

**收尾**：Unity 编译 0 错误（`PrepRoom._skillSelected` 为既有无害 warning）。待清理：`Resources/UI/*.uxml/.uss` 与 `AvatarSelectPanelSettings.asset` 等旧 UITK 资产（已无 `.cs` 消费者，安全删除留待下一波）。

---

## V0.4.5 · 地图统一为单一 STS 分叉全图（2026-07-29）

**目标**：接入 silverua [杀戮尖塔地图](https://github.com/silverua/slay-the-spire-map-in-unity) 的生成思路，并修复「第一次进入是全图、之后每间变三选一」的双地图 bug。

**bug 根因**：`Map_Structure_Config.MaxFloor=1` 让旧 `TreeMapGenerator` 退化成「起点→Boss」2 节点图 → 首次导航弹一次全图后 `CurrentNode` 即到 Boss（无后继），此后每间都回退到 `RoomChoiceUI` 三选一；而真正的 12 间房走的是 GameManager 另一套 `_levelRooms`，两套进度完全脱节。

**做法（option A：移植生成算法，不引入 DOTween/uGUI/精灵栈）**：

| 文件 | 变更 |
|------|------|
| `LevelDesign/Map/TreeMap.cs` | 重写 `TreeMapGenerator.Generate`：移植 silverua 的固定列宽 grid + 多条起点→Boss 列随机游走路径 + 分叉汇合；产出仍是现有 `TreeMap/TreeNode`（`Floors`+`Next`）。深度=每境房间数（`MaxNodes`），宽度固定 4 列。新增 `EnsureLayerConnectivity` / `AssignRoomTypes`（保底精英/商店/事件 + 权重） |
| `Core/Level/LevelDesignMapProvider.cs` | `TryShowNavigation` 每境按 `ActID = 当前境+1` 惰性 `BeginAct` 重生成分叉图，使其 `CurrentNode` 推进与 GameManager 房间推进锁步；`GetFloors()` 返回 `null`（分叉图不再充当线性脚手架，避免把 12 层误当 12 个境），线性 12 间脚手架交回 `GameManager.fixedLayout`，房型由玩家在全图上的选择覆盖 |
| `Core/GameManager.cs` | `EnterNextRoomWithChoice` 去掉 `RoomChoiceUI` 三选一回退，统一走单一全图；删除 `TryShowRoomChoice`/`BuildRoomCandidates`/`TypeTitle`/`TypeTooltip` |
| `LevelDesign/UI/RoomChoiceUI.cs` + `Resources/UI/RoomChoiceUI.uxml/.uss` | **删除** |

沿用现有 UITK `TreeMapUI` 表现（左起点→右 Boss、Painter2D 连线、点击/数字键选点）。**零新依赖**，Unity 编译 0 错误。

---

## V0.4.4 · 去修仙化清理 · R4（2026-07-29）

**目标**：GDD 已确认去修仙化，物理删除过期的「职业 / 化身 / 局内灵物」逻辑与资产（承 R1/R2/R3）。经确认：**保留单一主角档案**（不硬编码主角），洞府素材（CaveMaterial）与 `ItemRarity` 共享枚举**不属于灵物范畴，保留**。Unity 编译 0 错误。

**A · 职业 / 起始模板（去多职业，留单一主角档案）**
- 删除脚本：`Core/StartTemplate.cs`（含 `StartTemplateRegistry`）、`UI/StartTemplateSelectUI.cs`、`UI/CharacterSelectUITK.cs`（均无外部调用者，村庄门户早已直接入秘境）。
- 删除资产：`Resources/StartTemplates/`（炼金师/守卫者/刺客/播种者/法修·元素范围/游侠·投射连锁/剑修·近战爆发 等 8 个）、`Resources/CharacterProfiles/法修.asset`。
- 保留：`PlayerCharacterProfile` / `PlayerCharacterRegistry` / `剑修.asset` 作为**单一主角档案**（`Registry.Selected` 取 sortOrder 最小 → 剑修，驱动 Kazuko 模型 + 命中特效，不受影响）。
- `Editor/Demo1DataCreator`：不再生成 `法修` 档案，更新日志文案。

**B · 化身（残留清理）**
- 删除孤儿 UI 资产：`Resources/UI/AvatarSelect.uxml/.uss`、`Resources/UI/GrowthUITK.uxml/.uss`（早无 `.cs` 消费者）。
- 删除战斗残留：`CombatStats.avatarCoefficient`（化身乘区）字段 + 三条伤害公式中的该项；`StatusEffectController` 去其聚合；`StatType.AvatarCoefficient` → 改为 `LegacyCoeff` **墓碑**（保序不移动，避免破坏已序列化数据）。
- 保留：`AvatarSelectPanelSettings.asset`（多 UITK 面板共用的字体/设置，非化身逻辑）。

**C · 局内灵物**
- 删除资产：`Data/Items/`（20 个 SO）、`Resources/Items/`（20 个 SO）、`RawData/LevelDesign/Item_InRun_Config.csv`、`Resources/LevelDesign/Item_InRun_Config.json`、`Resources/UI/BattleRewardUI.uxml/.uss`。
- 删除代码逻辑：`ConfigTables.ItemInRunRow`/`ItemInRunTable`、`ConfigDatabase.ItemsInRun`/`GetItem`/加载/日志、`CsvToJsonImporter` 的 `Item_InRun_Config` 导入与 `ParseItemInRunRow`、`Combat_Table_Index.csv` 灵物行；`StoryEventService` 事件奖励/代价改为按 ID 占位（不再查灵物表）；`Demo1DataCreator` 不再生成/加载灵物 SO；`GameManager` 商店 tooltip「购买灵物/丹药」→「购买技能/模块」。
- 保留：`Items/ItemData.cs`（承载 `ItemRarity`/`ItemCategory` 共享枚举，被 20+ 模块/技能/UI 引用）、拾取基础设施（`PickupBase`/`SkillPickup`/`WorldPromptPanel`）、洞府素材管线（`ItemPickup`/`CaveInventory`/`CaveMaterialPool` + `Resources/CaveMaterials/`）。

**残留（非承重，后续可选清）**：`Demo1DataCreator` 内 `Create*Orb/Bead/...` 等灵物 SO 生成方法体已成孤儿（无调用者，编译无害）；各处 `灵物`/`化身` 字样的历史注释；`1Game/Docs/资源_灵物配置指南.md` 等历史文档保留留痕。

**文档同步**：GDD 版本表 + §10.3 归档表 R4；`1Game/Docs/程序_架构说明.md`（基线快照 + 目录注释 + §4.6 类速查去死条目 + §9.7 数值公式）。

---

## V0.4.3 · 近战平砍特效（2026-07-29）

**目标**：近战主角平砍**去挥击刀光**，改为**命中怪物时**随机播放 hit-line 打击特效（不再是按键挥击就出）。

- `PlayerCombat`：新增 `hitVFXPrefabs` 命中特效随机集合 + `SetHitVFXSet()` + `PickHitVFX()`（过滤空槽后随机）；`SpawnHitVFX`（仅命中敌人时调用）优先从集合随机取一个，空则回退单个 `hitVFXPrefab`。新增 `DisableSlashVFX()` 关闭挥击刀光。`OnSlashVFXRequested`（挥击路径）维持单个 `slashVFXPrefab` 逻辑。
- `PlayerCharacterProfile`：新增 `disableSlashVFX` 开关 + `hitVFXPrefabs` 集合字段（按角色覆盖）。
- `PlayerController.ApplyCharacterProfile`：`disableSlashVFX` → 清挥击特效；`hitVFXPrefabs` 非空 → 应用命中随机集合，否则回退单个。
- 资产：`剑修` 档案 `disableSlashVFX=true`（挥击无特效）+ `hitVFXPrefabs` = [`hit-line-1`, `hit-line-2`]（`ArtRes/Package/VFX/Hit & Slashes Vol.3`）。仅影响近战主角，法修不受影响。

> 修正：本条初版误把 hit-line 放在挥击(swing)路径（按键即出）；按需求改到命中(hit)路径（打到怪才出）。

---

## V0.4.2 · 关卡层解耦（2026-07-29）

**目标**：把 `GameManager` 这个上帝对象拆薄，让「地图拓扑 / 房间生成 / 游戏流程」各自独立、可替换，为后续替换地图系统（如接入杀戮尖塔式地图）铺路。

### 新建领域层 `Core/Level/`
- **`RoomType.cs`**：房间类型**领域枚举**（单一真源），从 `Minimap` 内嵌枚举提升为顶层 `XianTu.RoomType`（成员/整数值保持一致）；`Minimap` 反过来消费它，解除「UI 类持有领域模型」的反向依赖。
- **`IMapProvider.cs`**：地图/拓扑抽象接口 + `MapProviders.Current` 全局可替换入口（`StartRun` / `GetFloors` / `GetEnemyScale` / `GetRarityBias` / `GetHasStageReturn` / `TryTriggerRoomEvent` / `MarkCurrentCleared` / `CurrentNodeHasNext` / `TryShowNavigation` / `CurrentActId`）。
- **`LevelDesignMapProvider.cs`**：默认实现，收口对 `LevelDesignDirector` / `ConfigDatabase` / `TreeMap` 的直接访问，并在边界完成 `LevelRoomType → RoomType` 映射。
- **`RoomFactory.cs`**：`IRoomFactory` + `RoomSpawnContext`，原 `GameManager.Spawn*Room` 全部搬入。

### GameManager 瘦身
- 房间生成大 switch → `_roomFactory.Spawn(type, ctx)`，删除 ~200 行 `Spawn*` 方法。
- 敌人缩放 / Boss ActID / 事件触发 / 节点标记 / 地图导航全部改经 `IMapProvider`，不再直接 `using` LevelDesign。
- `GetFloorRarityBias` 原在 GameManager/BattleRoom/ShopRoom **各抄一份**，统一收口到 `MapProviders.Current.GetRarityBias()`。

### 文档与规则
- 更新 `1Game/Docs/程序_架构说明.md`：修正滞后基线 + 新增 §十「关卡层解耦」。
- 新增 Cursor 规则 `.cursor/rules/架构文档同步.mdc`：改动游戏逻辑后必须同步架构文档。

> 说明：`LevelRoomType` 与 `RoomType` 目前仍双存、在 provider 边界映射（TreeMap 生成内部仍用 `LevelRoomType`）；`IMapView` 表现层接口留待真正换图时再抽。

---

## V0.4.1 Phase3 · 大秘境（2026-07-29）

**目标**：GDD §11.4.1 + Q-008/Q-009——局外大秘境挑战（装备 Build 验证数值），计时清怪 → Boss，奖励搭框架。

### 大秘境入口
- **新建 `RiftEntrance`**（`VillageHub.cs`）：村庄右侧红紫色传送门，按 F 进入大秘境。
- 前置门控：局外 Build 背包为空时提示「需先带出 Build 才能进入」，无法进入。

### 流程管理
- **新建 `RiftManager.cs`**：大秘境流程编排——缓冲区 → 装备 Build → 计时挑战 → 奖励 → 回村。`RiftTier` 每通关 +1 提升难度。
- `GameManager`：新增 `InRift` 标志 + `EnterRift()` / `ExitRiftToVillage()` + `PlacePlayer()`；`OnRoomCleared` / `OnPlayerDied` 在大秘境模式下短路交由 `RiftManager` 处理；进出大秘境满血复活。

### 缓冲区 + Build 装备
- **新建 `RiftBufferRoom.cs`**：缓冲区含装备台 NPC（`RiftEquipStation`）+ 挑战门（`RiftChallengeGate`）。
- **新建 `RiftEquipUI.cs`**：列出背包所有 Build，选一套 `ApplyToPlayer()` 装备到角色；未装备时挑战门自动弹出装备 UI。
- `BuildSnapshot.ApplyToPlayer()`：从 Resources 还原技能（按 `skillName`）+ 模块链（按 `moduleId`）装到玩家。

### 计时挑战间
- **新建 `RiftChamber.cs`**：计时清怪 → Boss（参考暗黑 3 大秘境）。目标击杀数 `20 + (tier-1)×5`，HP/DMG 随层数缩放；达标后清场刷 Boss，击杀 Boss 通关。IMGUI 顶部 HUD 显示计时 + 进度。

### 奖励框架
- **新建 `RiftRewardUI.cs`**：成功/失败结算面板，展示层数 + 用时，奖励区为占位框架（实际产出待策划书面确认，GDD Q-009）。显示时冻结时间。

---

## V0.4.1 Phase2 · 存档系统（2026-07-29）

**目标**：GDD §11.4 V0.4.1 Phase2——进度存档 3 槽 + Build 无限存档 + 局外背包。

### 存档槽位系统
- **重构 `SaveSystem.cs`**：从单文件 `save_v1.json` 改为 3 槽位 `save_slot_{0-2}.json`。
- 兼容旧存档：首次启动自动迁移旧 `save_v1.json` 到槽位 0。
- `PlayerPrefs` 记录最近使用的槽位（`GoB.LastSaveSlot`），支持断点续玩。
- 进入秘境时自动存档（`AutoSave()`）。

### Build 背包
- **新建 `BuildSnapshot.cs`**：`BuildSnapshot` + `ChainSnapshot` 数据结构，序列化 3 技能 + 3 增强链（通过 `skillName` / `moduleId` 引用）。
- `BuildSnapshot.CaptureFromPlayer()`：运行时从 `PlayerCombat` + `ModuleSlotManager` 抓取当前 Build 快照。
- `SaveDataV1`：新增 `buildBackpack: List<BuildSnapshot>` 和 `slotName` / `createdTimestamp` 字段。
- 通关 / 死亡时自动保存 Build（`GameManager` 调用 `SaveSystem.SaveBuildFromCurrentRun()`）。

### 存档选择 UI
- **新建 `SaveSlotSelectUI.cs`**：UITK 程序化面板，3 张卡片展示槽位状态，支持加载 / 新建 / 覆盖确认。
- `MainMenu`：「开始游戏」→ 弹出存档选择面板；「继续游戏」→ 直接加载最近槽位。

### Build 背包 UI
- **新建 `BuildBackpackUI.cs`**：UITK 面板，列表展示所有已保存 Build（名称 / 摘要 / 时间），支持删除。
- **新建 `BuildManagerNPC`**（`VillageHub.cs`）：村庄右侧金色 NPC，按 F 打开 Build 背包。

---

## V0.4.1 Phase1 · 局内调整（2026-07-28）

**目标**：GDD §11.4 V0.4.1 版本计划 Phase1，核心是将游戏从「地面掉落 + 背包」转型为「三选一奖励 + 直接装备」。

### Phase1b: 开局无技能
- `GameManager.InitModuleSystem()`：移除 `ModulePoolLoader.GrantSeedLoadout()` 调用，开局 Q/E/R 全空（保留鼠标左键普攻）。
- `Demo1Setup`：已有 V0.4 改动，开局不赋技能。

### Phase1c: 三选一奖励系统（核心新机制）
- **新建** `RewardPickUI.cs`：UITK 程序化三选一面板——战斗房清场 70% / 精英房 90% / 事件房 100% 触发。
- 三张卡牌同类（全技能 或 全模块），玩家必须选择一张或点「跳过」。
- 技能栏满时自动弹替换确认 UI，展示 3 个槽位可替换，被替换技能按稀有度折算货币（`PlayerResources.GetDecomposeShards`）。
- 模块自动装备到第一个有空位的增强链（`TryAutoEquipModule`），装不下则提示打开装配 UI。
- `BattleRoom.OnRoomCleared()`：移除 `SpawnSkillReward()` / `SpawnModuleReward()` 地面掉落调用。
- `GameEvents.RoomCleared`：新增 `IsElite` / `IsEvent` / `IsCombatRoom` 字段。
- `GameManager.OnRoomCleared()`：战斗类房间清场后先弹 `RewardPickUI.TryShow()`，完成后才走过渡流程。

### Phase1d: 移除局内背包
- `GameManager.InitModuleSystem()`：不再初始化 `ModuleInventory`。
- `ShopRoom` 购买模块：从 `inv.Add()` 改为 `RewardPickUI.TryAutoEquipModule()` 直接装备到链。
- `ModuleInventory` 类保留（`ModuleAssemblyUI` 依赖），但运行时不填充。

### Phase1e: 商店扩展
- 商品数量：2 技能 + 5 模块 = 7 件（原 2+3=5）。
- **新增刷新按钮**：`ShopRoom.uxml` 添加 `refresh` 按钮，消耗基础货币刷新全部商品，每次刷新费用递增（基础 20 × 次数）。
- 技能替换折算：由 `RewardPickUI` 替换确认流程统一处理。

### Phase1a: 关卡结构调整
- 层数从 6 改为 3（`_realmNames`）。
- 固定布局 `_fixedLayout`：每层 12 关（战→战→精英→商店→战→事件→战→商店→战→精英→战→Boss），精英 ≤2，商店 ≤2。
- `Map_Structure_Config.csv`：3 条记录，每条 `MaxFloor=1` / `MinNodes=MaxNodes=12`，敌人缩放 1.0/1.3/1.6。

---

## V0.4.1 · 创意收集机制（2026-07-27）

- 新增 [创意收集箱](design/ideas/创意收集箱.md)，作为未评审玩法点子的分类记录入口，不替代 GDD 或开发排期。
- 首条记录：关卡「时限采集与追猎者」——倒计时内集齐目标材料并穿过传送门；超时未撤离则出现追猎者，特殊关可替换为专属 Boss。

---

## V0.4.0 · 战斗/关卡/系统全面重构（2026-07-15）

**V0.4 全量落地**。按 GDD §11.4 V0.4 版本计划，完成战斗板块（2 项）、关卡板块（4 项）、系统板块（1 项）全部修改，同时完成主题从"修仙"到"通用冒险/奇幻"的文案替换。

### 前置：主题替换 + V0.3 清理
- 全部玩家可见 UI 文案中的修仙术语替换为通用冒险词汇（梦境破碎→探索失败、灵力碎片→碎片、功法→技能、洞府→基地、道心→意志等）。
- 涉及文件：`PlayerInfoPanel`、`GameHUD`、`ExtractResultPanel`、`RunHUD`、`SkillBarUI`、`MainMenu`、`Demo1Setup`、`VillageHub`、`GameManager`、`ShopRoom`。
- GDD 内 V0.3 版本计划 + 版本表记录行已删除（已留档）。

### 战斗1：删除职业选择
- 移除 `StartTemplateSelectUI` 在 `VillagePortal` 中的调用，山门按 F 直接进入秘境。
- `Demo1Setup.CreatePlayer()`：移除 Q/E/R 技能自动分配逻辑（仅保留 Inspector 手动配置用于调试）。
- 移除 `PickStartingSkillFromDisk()` 方法（不再需要从磁盘挑选起始技能）。
- `GameManager.StartNewRun()`：移除 `StartSkillLoader.Apply()` 调用（起手功法系统不再使用）。
- `ModuleDropWeighting`：移除 `StartTemplateRegistry.Selected` 依赖和 `TemplateAffinityBonus` 权重。

### 战斗2：技能三选一
- 新增 `SkillSelectUI.cs`（UITK 程序化构建）：从技能池中筛选最低品质（`ItemRarity.Fan`）技能，随机展示 3 个供玩家选 1 个装备到 Q 槽位。
- 技能卡片展示：类型颜色 + 技能名 + 类型标签 + 描述 + 伤害/冷却数值。
- 玩家初始无任何技能，所有技能变化来自准备房间选择和局内拾取/购买。

### 关卡1：准备房间
- 新增 `PrepRoom.cs` + `PrepRoomExit.cs`：局外→准备房间→技能选择→局内。
- 准备房间独立调色板（冷蓝色调），带告示牌和出口门。
- 选完技能后出口解锁（头顶提示从"先选择一个技能"变为"按 [F] 进入秘境"），按 F 进入第一个战斗房间。
- `GameManager.StartNewRun()` 流程改为：初始化→生成地图→PrepRoom→OnPrepRoomComplete→SpawnCurrentRoom。

### 关卡2：删除撤退系统
- `GameManager`：`SpawnExtractPointAndPortal()` → `SpawnLevelCompletePortal()`，移除 ExtractPoint 生成和撤离选择分支。
- 层间过渡简化为：非最终层→直接传送门进下一层；最终层→通关结算面板。
- `RunHUD`：移除 `CaveMaterialPickedUp`、`ExtractSuccess`、`ExtractInterrupted` 事件订阅和对应 toast。
- `ExtractResultPanel`：简化默认 `Show()` 重载为 `EndType.Death`；保留 `EndType.Extract` 枚举值兼容但不再使用。
- `EnterVillageHub()`：移除 `ExtractPoint` 清理逻辑。

### 关卡3：模块掉落 + 商店购买
- `GameConfig` 新增 4 个字段：`模块掉落概率`(0.75)、`模块掉落数量最少`(1)、`模块掉落数量最多`(2)、`精英房模块掉落数量`(3)。
- `BattleRoom.SpawnModuleReward()`：使用 `GameConfig` 驱动掉落概率和数量（替代原有硬编码 `Random.value < 0.5f ? 2 : 1`）。
- `ShopRoom`：新增 `ModuleDef[]` 参数，商品布局改为 2 技能 + 3 模块。新增 `BuildModuleCard()`、`CalculateModulePrice()`、模块购买逻辑（加入 `ModuleInventory`）、模块悬停提示。
- `GameManager.SpawnShopRoom()`：传入 `modulePool`。

### 关卡4：关卡房间扩充
- `_fixedLayout`：6 层统一为每层 11 个房间（战→战→精英→商店→战→战→事件→战→战→升级→Boss）。
- `Map_Structure_Config.csv` + `.json`：三个 Act 统一为 MaxFloor=4、MinNodes=3、MaxNodes=4、HasStageReturn 全 0（无阶段返回）。

### 系统1：C 键角色信息
- `PlayerInfoPanel`：快捷键从 `Tab` 改为 `C`，解决与 `DebugConsole`（Tab 键）的冲突。
- `Demo1Setup` 底部操作提示同步更新。
- `GameManager` 相关注释同步更新。

---

## V0.3.0–V0.3.4 · 系统交互重构 + UI 优化（2026-07-14）

**V0.3 全量落地**。按 GDD §11.4 V0.3 版本计划，对 UI 交互系统进行一轮重构，解决信息不透明、操作提示缺失、职业选择流程不合理、图鉴空白、HUD 布局重叠等问题。

### V0.3.0 信息面板
- **`PlayerInfoPanel`**（UITK，程序化构建）：Tab 键切换显示/隐藏，主菜单 + 暂停菜单均有入口。
- 展示内容：起始模板信息、基础属性（HP/攻击/暴击/速度/防御等 8 项卡片）、成长信息（本局/永久经验、击杀数、道心、探索时长等）、当前增强链（Q/E/R 三槽核心技能 + 模块链概览）。
- 与 `PauseMenu` / `ModuleAssemblyUI` / `MainMenu` 互斥（防止同时弹出多个面板）。

### V0.3.1 局内装配快捷键提示
- 底部操作提示更新为：`WASD 移动 | 左键挥刀 | Q/E/R 技能 | Space 闪避 | F 拾取 | M 模块装配 | Tab 角色信息 | ESC 暂停`。
- M 键装配台局内外均可使用（已有功能，本次只补齐提示）。

### V0.3.2 职业选择移至山门入口
- **主菜单**：「入秘境」按钮改为「进入基地」，点击后直接进入村庄 Hub（无模板选择），玩家在 Hub 内无战斗技能，可自由走动配置。
- **山门（VillagePortal）**：按 F 时弹出 `StartTemplateSelectUI`，选中模板后 `ApplyToPlayer()` 再调 `StartNewRun()`，流程变为：基地探索→山门选模板→入秘境。
- 解决了"局外就能释放技能"的不合理问题，将职业身份感延迟到进入关卡时刻。

### V0.3.3 图鉴功能
- **`CodexUITK` 重写**：从占位（"图鉴开发中"）升级为完整的模块/技能目录。
- 双 Tab 页：模块（按触发器/效果器/改造件/万能件筛选）、核心技能（全量展示）。
- 模块卡片：大类角标 + 稀有度配色名称 + 描述 + 消费模型标签。
- 技能卡片：类型标签 + 描述 + CD/伤害倍率。
- 主菜单「图鉴」+ 暂停菜单「图鉴」按钮均已接入。

### V0.3.4 HUD 优化
- **Buff 栏下移**：`bb-root margin-top` 46px → 74px，避开左上角 HP 血条区域。
- **道心/因果/寿元面板下移**：RunHUD `DrawMoralStatus` y=12 → y=115，避免与右上角小地图 + 敌人计数重叠。
- **敌人计数下移**：EnemyPanel offset 从 `(-95,-60)` 调至 `(-110,-75)`，为小地图图例腾出空间。
- **小地图图例**：在小地图下方添加一行图例 `⚔战斗 ⚡精英 ?事件 $商店 ♥休息 ☠Boss`，消除图标含义不明问题。
- **小地图标题**：从"仙途"改为"地图"，更直观。

---

## V0.2.5 · 单局节奏压缩 + Event 叙事全覆盖（2026-07-10）

**V0.2.5 落地**。将三 Act 总楼层从 24 层压缩到 12 层（3+4+5），使单局通关时长落入 25-40 分钟目标区间。同时为 Act2（幽冥谷）和 Act3（炼狱峰）各填充 2-4 个叙事事件（含条件分支），Event 房不再空转。

### 单局时长调优

- **Map_Structure_Config 压缩**：Act1 6→3层、Act2 8→4层、Act3 10→5层，总计 12 层 × 2-4 节点 = ~30 房间。
- **EnemyScaleMul / ModuleRarityBias / HasStageReturn** 数组同步裁剪至新楼层数。
- **运行时计时器**：`GameManager.RunElapsedSeconds` 属性，`StartNewRun` 开始计时，死亡/撤离/通关截止。
- **结算面板显示探索时长**：`ExtractResultPanel` 新增"探索时长 MM:SS"行。
- **Debug 日志**：死亡/通关时打印 `[RunTimer] X.X 分钟（目标 25-40min）`，便于快速验证。

### Event 房叙事填充

| Act | EventID | 名称 | 类型 | 核心抉择 |
|-----|---------|------|------|----------|
| 1 | 1001 | 叶修之死 | 无前置 | 取走风灵珠 / 埋葬 / 搜刮 |
| 1 | 1002 | 灵药宝库 | 条件(saved_yeXiu) | 接受赐药 |
| 1 | 1003 | 古修遗宝 | 无前置 | 寿元换 / 放弃 / 强取 |
| 2 | 2001 | 幽魂哀歌 | 无前置 | 读碑 / 驱散 / 绕路 |
| 2 | 2002 | 冥河渡口 | 无前置 | 血祭 / 寿元 / 强渡 |
| 2 | 2003 | 亡者遗言 | 条件(read_ghost_tablet) | 接受传承 / 拒绝 |
| 2 | 2004 | 灵魂交易 | 无前置 | 接受 / 拒绝 / 反噬 |
| 3 | 3001 | 龙骨祭坛 | 无前置 | 肉身献祭 / 心火 / 离去 |
| 3 | 3002 | 天火试炼 | 无前置 | 破解 / 压制 / 绕路 |
| 3 | 3003 | 龙血觉醒 | 条件(dragon_sacrifice) | 觉醒 / 压制 |
| 3 | 3004 | 劫雷降世 | 无前置 | 硬抗 / 分散 / 逃离 |

### Room_Socket_Group_Config 扩展

- Act2 新增 ID 7-13（Battle/Elite/Event×3/Shop/Boss）
- Act3 新增 ID 14-20（Battle/Elite/Event×3/Shop/Boss）
- Map_Structure_Config 的 RoomPoolID 按 Act 指向正确的 Socket 组

---

## V0.2.4 · Boss 动态化 + 通关结算闭环（2026-07-10）

**V0.2.4 落地**。Boss 系统从硬编码 bossID=1 切换为 ActID 驱动，P2 形态切换现读取 `Boss_Phase_Config` 配表；通关（击败最终层 Boss）正式弹出结算面板（EndType.Victory × 2.0），与死亡/撤离共享统一的遗产选择流程。三 Act 各有独立龙形 Boss Prefab 和多形态配表。

### Boss 动态化

- **`EnemyBoss.Spawn(pos, hpMul, dmgMul, bossID)`**：新增第四参数 `bossID`，默认 1。
- **`GameManager.SpawnBossRoom`**：自动从 `LevelDesignDirector.CurrentMap.ActID` 获取 bossID 传入，每 Act 对应独立 Boss 形态配置。
- **P2 配表驱动**：`EnemyBoss._pendingPhase2Row` 在 Spawn 时缓存 `BossPhaseSelector.Select(bossID).Phase2`；50% HP 触发 `CheckPhaseTransition` 时，若有配表 P2 形态则应用 `StatModifier`（ATK/SPD），否则退化为原有 moveSpeed×1.3 硬编码。

### Boss 多形态美术接入

- **`MonsterPrefabs` 扩展**：新增 `Boss_Act2_Prefab`（Dragon Nightfall）和 `Boss_Act3_Prefab`（Dragon Dusk）字段 + `GetBossPrefab(int bossID)` 查询方法。
- **`MonsterPrefabs.asset`**：已绑定 Dragon Nightfall / Dragon Dusk 预制体 GUID（与 Dragon Darkness 同系列，共享动画骨骼）。
- **`EnemyBoss.Spawn`**：现使用 `prefabs.GetBossPrefab(bossID)` 替代原 `Boss敌人Prefab`。

### Boss_Phase_Config 填充 Act2 / Act3

- **BossID=2 「幽冥谷守灵」**（5 形态）：常规 / 亡魂共鸣(kill≥30) / 冥河化身(道心≤15) / 悯生(kill<10) / 仇恨积聚(因果≥5)。
- **BossID=3 「炼狱峰龙魂」**（5 形态）：常规 / 龙怒焚天(道心≤10) / 古龙试炼(无死通关Act2) / 寂灭苏醒(因果≥7) / 怜悯(善举+低杀戮)。
- 每形态含独立对白、数值修正(StatModifier)、召唤配置(SummonSquadID)。

### Flag 系统扩展

- **`PlayerStateHooks.KillCount`**：全局击杀计数，`LevelDesignBootstrap.OnEnemyKilled` 每次击杀 +1 → 写入 `BossFlagSet("kill_count")`。
- **`PlayerStateHooks.MarkActCleared(actID)`**：无死通关某 Act 后写入 `cleared_act{N}_no_death=1`。
- **`PlayerStateHooks.MarkDeath()`**：标记本局死亡（`LevelDesignBootstrap.OnPlayerDied` 触发）。
- **`ResetForNewRun`**：新局清零 KillCount、HasDiedThisRun。

### 通关结算闭环

- **`SpawnExtractPointAndPortal` 中 `isLastRealm=true` 分支重写**：
  - 计算 `victoryMul = 2.0`，调用 `InsightSystem.CommitOnExtract(2.0)` 和 `CultivationSystem.CommitOnExtract(2.0)`。
  - 提交洞府素材。
  - 弹出 `ExtractResultPanel.Show(EndType.Victory, legacyModules, ...)`。
  - 确认后返回洞府（`EnterVillageHub()`）。
- 通关不再是无声无息的 `_gameOver=true`，而是正式走统一结算流程。

### 验证要点

- [ ] 编译 0 error
- [ ] 击败最终层 Boss 后弹出"秘境通关"面板，经验 ×2.0 + 遗产选择
- [ ] 不同 Act 的 Boss 应用不同形态（如果 Boss_Phase_Config 有对应行）
- [ ] 50% HP P2 切换时，若配表有 Phase2 行则应用 StatModifier

---

## V0.2.2 · 统一结算 + 遗产系统（2026-07-09）

**V0.2.2 落地**。统一了所有局终出口（死亡/撤离/通关）的经验结算模型，并实现了「遗产模块」系统——每次局终，玩家从背包中选择 1 个模块带入下一局首战。

### 统一结算模型

- **死亡不再全丢**：`InsightSystem.CommitOnDeath(0.5f)` 保留 50% 经验转入永久（GDD P1 "死亡 0.5x"）。
- **结算倍率体系**：`EndType.Death=0.5x` / `Extract=1.0x` / `Victory=2.0x`，叠加层深倍率（每层 +15%）。
- **死亡弹出结算面板**：不再直接消失，改为暂停游戏 → 展示经验明细 + 遗产选择 → 确认后返回洞府。

### 遗产系统

- **`SaveDataV1.lastRunLegacyModuleId`**：新增跨局字段，存储上局选定的遗产模块 ID。
- **结算面板遗产选择**：`ExtractResultPanel.BuildLegacySection()` 展示玩家背包中所有模块为可点击卡片，选中 1 件高亮为金色边框。
- **遗产注入**：`GameManager.TryInjectLegacyModule()` 在 `StartNewRun()` 末尾检查存档，若有遗产模块则在玩家脚下生成 `ModulePickup`，一次性使用后清空。
- **心理缓解**：死后不再"一无所获"，有遗产带入下局 + 50% 经验保底，减轻 GDD Q2 提到的"重开抵触"。

### ExtractResultPanel 重构

- 新增 `EndType` 枚举（`Death`/`Extract`/`Victory`）。
- 新增 8 参数 `Show` 重载（保留旧 6 参数兼容）。
- 面板标题根据 `EndType` 显示"梦境破碎"/"安全撤离"/"秘境通关"。
- 确认按钮文案动态：有遗产可选时显示"确认遗产 · 返回洞府"。

### 验证要点

- [x] 编译 0 error（排除预存 GameObjectToPng 无关错误）
- [ ] 死亡后弹出结算面板（标题"梦境破碎"，经验显示 ×0.5）
- [ ] 结算面板显示模块卡片网格，可选中 1 件
- [ ] 选中遗产后确认 → `lastRunLegacyModuleId` 写入存档
- [ ] 下一局 StartNewRun → 遗产模块自动掉落在玩家脚下
- [ ] 撤离时面板同样显示遗产选择（EndType=Extract）

---

## V0.2.1 · 房间类型全闭环 + 稀有度联动 + 阶段返回（2026-07-09）

**V0.2.1 落地**。让 TreeMap 生成的 6 种房间类型（Battle/Elite/Event/Shop/Rest/Boss）全部可运行，不再把 Elite 退化为普通战斗、Event 退化为宝箱。同时把 `ModuleRarityBias` 接入掉落权重，让深层楼层产出更多高品阶模块。

### 房间类型扩展

- **`Minimap.RoomType` 枚举**：新增 `Elite`、`Event` 两个值，小地图/UI/调试接口全线支持。
- **`MapLevelRoomToMinimap` 映射更新**：`LevelRoomType.Elite → Minimap.Elite`（不再退化为 Battle），`LevelRoomType.Event → Minimap.Event`（不再退化为 Treasure）。
- **Minimap UI**：Elite 显示 ⚡ 橙色，Event 显示 ? 淡蓝色。

### 精英房（SpawnEliteRoom）

- 敌人数量少于普通战斗（`baseEnemyCount - 1`，最少 2）。
- HP/DMG 乘以 `GameConfig.精英怪血量倍率` × `精英怪伤害倍率` × 层缩放。
- 通关掉落 3 个模块（普通房 1-2 个），且稀有度偏移 +20。
- `BattleRoom.SetEliteRoom(true)` 标记后走独立掉落逻辑。

### 事件房（SpawnEventRoom）

- 构建基础房间视觉（`RoomBuilder.Build`）。
- 自动触发 `LevelDesignDirector.TryTriggerRoomEvent()`，接入 `StoryEventService` 叙事事件系统。
- 事件完成后自动发布 `RoomCleared` 推进主循环。

### 模块稀有度联动

- **`ModuleDropWeighting.PickWeighted(pool, rarityBias)`**：新增 `int rarityBias` 参数。
- `rarityBias > 0` 时，高品阶模块（Ling/Xuan/Di/Tian）权重按 `rarityOrd × rarityBias × 0.01` 递增。
- `BattleRoom.SpawnModuleReward` 现从 `Map_Structure_Config.ModuleRarityBias[floor]` 读取偏移；精英房额外 +20。

### 阶段返回点条件化

- **`GameManager.ShouldShowStageReturn()`**：读取 `HasStageReturn[currentLevel]`。
- 若当前层无阶段返回（配表 = 0），层末不生成出梦点，直接传送门进入下一层。
- 兜底：配表不可用时 → 总是允许撤离（向后兼容旧行为）。

### 验证要点（待 Unity Play 后执行）

- [x] 编译 0 error
- [ ] TreeMap 中 Elite 节点→SpawnEliteRoom 被调用（日志 `★ 精英房 ★`）
- [ ] 精英房掉落 3 件模块，稀有度明显高于普通房
- [ ] Event 节点→SpawnEventRoom 弹出叙事事件
- [ ] 小地图正确显示 ⚡ 和 ? 图标
- [ ] HasStageReturn=0 的层末直接进入下一层（无出梦点）
- [ ] HasStageReturn=1 的层末同时出现出梦点 + 传送门

---

## V0.2.0 · 关卡生成配表 + 程序化地图接入主循环（2026-07-09）

**V0.2 正式启动**。V0.1 战斗配表体系已收官，进入关卡设计版本。本版核心：把已有的 `LevelDesignDirector`/`TreeMapGenerator` 系统从旁路接入 `GameManager` 主循环，让每局不再走硬编码固定布局，而是读 `Map_Structure_Config` 程序化生成树状地图。

### 设计决策（GDD §11.2.2 Q-004/Q-005）

- **Q1 反制系统**：设计合理但时机不对→移至 V0.3。V0.2 难度递增沿用：数量/倍率/新类型/Boss 阶段。
- **Q2 重开抵触**：V0.2 同步控制节奏（25-40min 目标）+ 阶段返回激励 + 遗产系统。

### 配表扩展

- **`Map_Structure_Config.csv` 新增 3 列**：
  - `EnemyScaleMul`：每层敌人数值缩放倍率（分号分隔浮点数组）。Act1 = 1.0/1.3/1.6/2.0/2.5/3.0。
  - `ModuleRarityBias`：每层模块掉落稀有度偏移（百分比）。越深层越偏向高稀有度。
  - `HasStageReturn`：每层结束后是否有阶段返回点（0/1 数组）。Act1 第 2/4 层有返回点。
- **`ConfigTables.MapStructureRow`** 新增字段 + `GetEnemyScale(floor)`/`GetRarityBias(floor)`/`GetHasStageReturn(floor)` 便捷访问器。
- **`CsvToJsonImporter`** 新增 `ParseFloatArray` 方法 + `ParseMapStructureRow` 扩展解析。

### 主循环接入

- **`GameManager.StartNewRun()`**：不再调用 `GenerateLevelLayout()`（固定布局），改为：
  1. `LevelDesignDirector.Instance.StartNewRun()` → 生成 Act1 TreeMap
  2. `GenerateLevelLayoutFromTreeMap(map)` → 把 TreeMap 节点映射为兼容旧 `_levelRooms` 结构的房间序列
  3. 保留旧固定布局作为兜底（TreeMap 生成失败时）
- **`GameManager.SpawnBattleRoom()`**：从旧公式 `1 + level × scale` 改为乘以 `GetCurrentFloorEnemyScale()` 从配表读取的每层缩放倍率。
- **`GameManager.OnRoomCleared()`**：新增 `LevelDesignDirector.MarkCurrentNodeCleared()` 调用，同步标记 TreeMap 节点完成状态。
- **`LevelDesignBootstrap.OnRealmBreakthrough(0)`**：检测 Director 已有地图时跳过重复生成（防止 GameManager 直接调用后 Bootstrap 再次覆盖）。

### 现有系统复用

- `TreeMapGenerator.Generate(actID)` → 从 `Map_Structure_Config` 读参数，按 层→节点→路径 三级程序化生成
- `TreeMapUI` → Slay the Spire 式路径选择界面（已有 UITK 实现）
- `RoomChoiceUI` → 3 选 1 卡片退化方案（TreeMap 无候选节点时）
- `useTreeMapFlow = true`（默认开启，F12 可切换）
- `RoomBuilder` → 程序化房间视觉（墙壁/柱子/陷阱/配色随层变化）

### 验证要点（待 Unity 重开后执行）

- [ ] 编译 0 error
- [ ] 导表生成新 JSON（含 EnemyScaleMul/ModuleRarityBias/HasStageReturn）
- [ ] StartNewRun 后 TreeMap 非空（`LevelDesignDirector.CurrentMap != null`）
- [ ] 房间序列来自 TreeMap（日志显示 `[V0.2] TreeMap 布局：...`）
- [ ] 战斗房敌人缩放倍率正确（第 1 层 ×1.0，第 3 层 ×1.6）
- [ ] 房间清场后弹出 TreeMap UI 路径选择

---

## V0.1.18d · 核心技能表补全（技能参数仓库表）（2026-07-08）

补齐战斗配表最后一环——`Skill_Base_Config` 主表只有 8 列（ID/名称/描述/品阶/类型/CD/伤害/图标），而 `SkillData` SO 有 50+ 字段（充能/蓄力/投射/位移/治疗/召唤/Buff/Zone 等）散落未进表。沿用模块「主表 + 参数仓库表」模式补全。

- **`Skill_Param_Config.csv`（新，24 行，54 列）**：从 25 个 `SkillData` 资产中 configId>0 的 24 个真实导出，主键 `ConfigId`=`Skill_Base_Config.ID`（=`SkillData.configId`）。覆盖 SO 全部数值/开关字段：伤害·缩放·CD·施速/充能(层数·恢复)/蓄力(Lv2·3 时间·伤害·范围·移速)/AoE/投射(速度·数量·散射)/位移(距离·留痕·无敌)/治疗(量·缩放)/召唤(时长·伤害·嘲讽)/Buff(时长·攻速·移速·攻击·减伤)/命中冻结/轮回结算/保命/天地大挪移/Zone(时长·半径·跳率·每跳伤害·减速·吸引·跟随·灼烧)/表现(动作·特效时长)。资产引用类字段（icon/prefab/vfx/audio/modifierDefs）不入表。
- **孤儿说明**：`金钟罩`（configId=0）未进 `Skill_Base_Config`，本表也不含（跳过），后续如需入表再补 ID。表 IDs 16-20（一念刹那/枯荣逆旅等 §特殊型）无对应 SO，同样不在本表。
- **导表管线接入**：`SkillParamRow`（54 字段）+ `SkillParamTable` + `CsvToJsonImporter.ParseSkillParamRow`（Combat 根）+ `ConfigDatabase.SkillParams`（按 `ConfigId`）/`GetSkillParam`。
- **`Combat_Table_Index.csv`**：登记 `Skill_Param_Config`（启用），`Skill_Base_Config` 关联列补 `Skill_Param_Config`。
- **深度**：作表 + 可导 JSON + 可加载（与其余仓库表同档），**运行时仍以 SO 为准**，零回归。
- **验证（已通过）**：编译 0 error；导表生成 JSON；`Reload()` 后 `SkillParams=24`，抽样 `混沌吞噬`(Zone 时长5/吸引3)、`土遁术`(Dash 无敌1/3s)、`御风诀`(Buff 6s/攻速+0.3) 字段正确。

---

## V0.1.18c · 运行时改为读表（ConsumeKind 系数 / 敌人倍率 / 模块数值）（2026-07-08）

把前两版「作表」升级为「运行时真正读表」——运行时数值从 CSV→JSON 表读取，策划改表即可影响游戏；三处均带**安全回退**（缺表/缺行 → 用原硬编码/SO 值），且当前表值由源码/SO 真实导出，本版为 1:1（零行为变化），价值在后续 CSV 迭代。

- **ConsumeKind 系数读表**：`ModuleChain.ConsumeKindDamageMul/RadiusMul` 改由 `ConsumeKind_Bonus_Config`（ID=`(int)ConsumeKind`）提供，查不到回退常量（Single 1.25 / Window 1.10+范围 1.20 / Auto 0.80）。
- **敌人倍率读表**：`EnemyBase/Mage/Ranged/Charger/Boss` 的 `Spawn` 中 HP/伤害/防御倍率改读 `Enemy_Base_Config`（ID 1/2/3/4/6），缺行回退各自原字面量。**精英**沿用 `GameConfig` 精英倍率（Inspector 可编辑单一真源），不改表覆盖以免分叉。
- **模块数值读表**：新增 `ModuleTableApplier`，在 `GameManager.SetupModules` 解析出真实 `modulePool`（Demo1Setup 注入的 `Data/Modules` 59 资产）后、`GrantSeedLoadout` 之前，用 `Module_*_Param_Config` 覆盖每个 `ModuleDef` 全字段。**仅 Play 模式执行**（Edit 模式 SO 是真实资产，覆盖会脏盘——已用 `Application.isPlaying` 拦截；Play 内存改动不落盘，下次域重载还原），缺行保留 SO 原值。
  - 注：初版误挂在 `ModulePoolLoader.LoadAll()`（仅兜底路径，且当前 Resources 无模块→返回 0，会空耗 `_applied` 守卫），已改挂 `GameManager`。
- **回归风险**：ConsumeKind/敌人为纯字面量 1:1 替换，零回归；模块覆盖为 Play 模式内存操作，值与 SO 一致。
- **验证（已通过）**：编译 0 error；表加载 `Modules=59 Trig=13 Eff=20 Mod=21 Uni=5 Enemies=6 CK=4`；ConsumeKind 读表 `Single=1.25 / Window范围=1.2 / Auto=0.8`；敌人行 `Boss=8/3/3 Mage=0.8/1.5/0.6`；Play 中对真实 59 池篡改字段后 `ModuleTableApplier.ApplyAll` 还原为表值（`E_BingZhui`→25/4、`T_低血量`→阈值1/冷却5）。

---

## V0.1.18b · 模块参数仓库表（触发 / 效果 / 改造 / 万能）（2026-07-08）

延续 V0.1.18，把 `Module_Base_Config` 只放身份/标签/关键参数的定位落实到底——按四大类各拆一张「参数仓库表」，承载 `ModuleDef` 里的**完整数值参数**，全部从 59 个真实资产导出（非手写），以 `ModuleId` 与主表关联。

- **`Module_Trigger_Param_Config.csv`（新，13 行）**：触发器全参数——`TriggerType`/阈值/冷却/interval/consumeStacks/moveDistanceThreshold/healthThreshold/consumeKind/windowSeconds/maxStacks。
- **`Module_Effect_Param_Config.csv`（新，20 行）**：效果器全参数——`EffectType`/`EffectRole`/伤害·缩放·AoE·元素/治疗·护盾/buff 时长·减伤/投射速度·数量·散射/减速·眩晕·击退·冲刺·牵引/DoT DPS·时长/无敌·召唤·陷阱/易伤倍率·时长。
- **`Module_Modifier_Param_Config.csv`（新，21 行）**：改造件全参数——`ModifierType`/value/burn(DPS·时长)/freeze/lightning/poison(DPS·时长)/extraCount/costHP·costDamageBonus。
- **`Module_Universal_Param_Config.csv`（新，5 行）**：万能件双面全参数——触发面(type/阈值/冷却) + 效果面(type/role/consumeKind) + 双面 UI 描述。
- **导表管线接入**：`ConfigTables` 4 行结构 + 4 张 `*Table` 包装 + `CsvToJsonImporter` 4 解析（走 `Combat` 根）+ `ConfigDatabase` 4 张 `Dictionary<string,…>`（`ModuleId` 键）+ `GetModuleTriggerParam/EffectParam/ModifierParam/UniversalParam`。
- **`Combat_Table_Index.csv`**：4 表状态由「计划」改「启用」，补齐行数与字段说明。
- **深度**：同 V0.1.18——作表 + 可导 JSON + 可加载，**运行时仍以 SO 为准**，零回归。
- **验证**：编译 0 error；导表生成 4 张 JSON；`Reload()` 后 `Trig=13 / Eff=20 / Mod=21 / Uni=5`（合计 59 = 模块总数），各类计数与主表一致。

---

## V0.1.18 · 战斗配表基础设施（模块 / 敌人 / 消费系数）（GDD §11.3 V0.1.14 计划落地）（2026-07-07）

按 GDD §11.3「添加所有战斗相关表格 + 落实 §5.7 模块配置字段」推进：把只能在 Unity 里逐个改 SO、或散落在脚本/常量里的战斗数据，导出成策划可用 CSV，并接入既有导表管线（CSV → JSON → `ConfigDatabase`）。

- **模块配置主表 `Module_Base_Config.csv`（新）**：从 59 个 `ModuleDef` 资产真实导出（非手写），覆盖 §5.7 字段——ID/名称/大类/子类/稀有度/功能·形态·流派标签/consumeKind/windowSeconds/maxStacks/effectRole/触发阈值·冷却·interval/基础伤害·倍率·范围/元素/改造值/万能双面类型 + 新增策划字段 `DropSource`（掉落来源）/`UnlockCond`（解锁条件）/UI 描述。
- **`Combat_Table_Index.csv`（新）**：战斗相关全部配表的索引表（表名/分类/主键/状态/关联表/用途），登记现有 8 张 + 模块主表 + 枚举图例 + 4 张计划中的模块参数仓库表，后续新增策划表在此登记。
- **`Module_Enum_Legend.csv`（新）**：模块表所有枚举列（Category/Rarity/ConsumeKind/EffectRole/SubType=TriggerType·EffectType·ModifierType/Element/各 Tag）的 int↔名称对照，自枚举自动生成，供策划看懂 CSV 里的数字。
- **`Enemy_Base_Config.csv`（新，GDD §7.3）**：6 个敌人类型（普通/法师/远程/冲锋/精英/Boss）相对 `GameConfig` 敌人基础值的 HP/伤害/防御倍率 + 移速/侦测/攻击距离/攻击间隔 + 类型专属参数；现值**抽取自 `Enemy*` 脚本硬编码**（如 Boss ×8/×3/×3、法师 ×0.8/×1.5/×0.6、冲锋 ×1.5/×2/×1.5），供策划集中查看，运行时暂仍走脚本。
- **`ConsumeKind_Bonus_Config.csv`（新，GDD §5.6）**：消费模型身份三角（Single 增伤 1.25 / Window 1.10+范围 1.20 / Stacks 中性 / Auto 0.80）；现值抽取自 `ModuleChain` 常量。
- **导表管线接入**：`ModuleBaseRow`/`EnemyBaseRow`/`ConsumeKindBonusRow` + 对应 `CsvToJsonImporter` 解析（并入「修仙图/导表」）+ `ConfigDatabase.Modules`（`ModuleId` 字符串键，新增 `LoadTableStr`）/`Enemies`/`ConsumeKindBonuses` + `GetModule/GetEnemy/GetConsumeKindBonus`。
- **深度**：本版为「作表 + 可导 JSON + 可加载」（与 `Skill_Base_Config` 同档），**运行时仍以 SO/脚本/常量为准**，未改运行逻辑，零回归风险。
- **验证**：编译 0 error；导表生成 3 张 JSON；`ConfigDatabase.Reload()` 后 `Modules=59 / Enemies=6 / ConsumeKindBonuses=4`，抽样 `E_HuoYu_Rain`(Effect/AreaDamage/Addon/Fire)、Boss(×8/×3/×3)、法师(×0.8/间隔3.5)、Single(1.25)/Auto(0.80) 字段全部正确。
- **未做（计划）**：4 张模块参数仓库表（触发/效果/改造/万能全参数）已在索引表登记为「计划」；玩家基础数值/难度曲线已由 `GameConfig` SO 承载（Inspector 可编辑，未再镜像 CSV 避免分叉）；运行时改为读表（CSV 覆盖/生成 SO）为后续迁移，本版不动。

---

## V0.1.17 · P2 模块池扩容（效果器 20 / 改造件 21）（2026-07-02）

补齐首批模块池目标数量，只用 PlayerCombat 已实现的 EffectType/ModifierType，保证新模块开箱即用。生成器 `PoolExpansionGenerator`（编辑器菜单「修仙图/P2 — 扩容模块池」，幂等）。

- **效果器 10 → 20**：新增 减速(Slow) / 眩晕(Stun) / 易伤(MarkVulnerable) / 无敌(Invincible·Enhancement) / 净化(Cleanse·Enhancement) / 火雨(AreaDamage·火) / 冰锥(Projectile·冰) / 剧毒(DoT·木) / 震荡波(Knockback) / 落石(AreaDamage·土)。
- **改造件 18 → 21**：新增 附雷(AddLightning) / 破绽(AddVulnerable) / 延冷(CostCooldown)。
- **验证**：编译 0 error；生成 13 个；池计数 触发器 13 / 效果器 20 / 改造件 21 / 万能 5；抽样链编译正确（减速链 Slow=0.5；火雨+附雷链 AreaDamage·Fire·addLightning=True）。
- **未达标**：状态型触发器仍 3 个（目标 4-6）——`傀儡计数` 阻塞于召唤系统，其余需新增状态机制类型（新枚举+逻辑），非纯内容工作。

---

## V0.1.16c · P1 模块掉落软性动态权重（2026-07-02）

收尾 P1 起始模板最后一项——掉落权重按「当前模板 + 本局成型链」动态偏置，但不硬锁池子。

- **`ModuleDropWeighting`（新）**：`PickWeighted(pool)` 按权重抽取。权重 = 基础 1（恒 >0）+ 起始模板风格重叠 ×+1.5 + 半成型链缺件补齐（缺触发器/效果器 → 对应大类 +2）+ 已拥有构筑风格协同 +0.75。读 `StartTemplateRegistry.Selected` + 玩家 `ModuleInventory`/`ModuleSlotManager`。
- **接入**：`BattleRoom.SpawnModuleReward` 由均匀随机改为 `ModuleDropWeighting.PickWeighted`。
- **验证**：编译 0 error；6000 次采样（选中「播种者」，风格 Seed|Poison|Fire）——同风格件 T_种子/T_引爆/E_毒雾/M_毒蚀 ≈4.4–5.1%（均匀 2.2%），中性件 T_低血量/T_每3秒 ≈1.8–2.0%（未被锁池）。

---

## V0.1.16b · P1 起始模板扩容（4 款原型模板）（2026-07-02）

利用刚落地的种子/背击触发器，把起始模板从 3 款扩到 7 款，覆盖更多开局流派。模板仅以 `startingModules` 区分（复用既有角色档案 + 核心技能），体现「起始模板只决定开局模块」的设计。选择 UI（`StartTemplateSelectUI`）动态收录 `StartTemplateRegistry.All`（`flexWrap` 网格），无需改 UI 即渲染全部 7 款。

- **新增 4 款**（`Resources/StartTemplates/`）：
  - **播种者**（法修档案）：`T_种子 + E_毒雾 + T_引爆 + E_范围爆炸 + M_扩散` —— 种下→引爆循环。
  - **刺客**（剑修档案）：`T_背击 + E_突刺 + T_闪避后 + E_飞弹 + M_连锁` —— 背击强化 + 闪避追击。
  - **守卫者**（剑修档案）：`T_受击时 + E_冲击波 + E_护盾 + M_击退` —— 稳守反打。
  - **炼金师**（法修档案）：`T_每3秒 + E_治疗 + T_每5秒 + E_范围爆炸 + M_灼烧` —— 周期节律续航。
- **验证**：4 款均 skills(Q/E/R)+profile 非空、模块引用全部解析成功；`Registry.All` 收录 7 款、排序正确。
- **未做**：`指挥者`（依赖召唤系统，随 `傀儡计数` 延后）；`雷行者`（暂由「游侠·投射连锁」近似覆盖）；掉落权重按模板/成型链动态调整（需改 `BattleRoom.SpawnModuleReward`）。

---

## V0.1.16 · P1 状态型触发器（种子生成/引爆）（2026-07-02）

延续 V0.1.15，落地状态型触发器里体量最大的**种子系统**——一个完整的「种下→引爆」循环。

- **`SeedSystem`（新）**：世界种子状态载体。`Plant(pos, element)` 在命中处放置无伤害标记球（存续 8s / 上限 16 / 近距 1m 合并刷新 / 满则替换最旧）；`DetonateAll(cfg, owner, layer)` 在每颗种子位置 `OverlapSphere` 对敌施加接入效果器的伤害（`cfg.damage + damageScaling×攻击`）+ 元素爆闪/环 + 灼烧/冰冻/毒/DoT 状态，然后清空。按需创建（`Ensure`），随场景卸载自然销毁。
- **`SeedPlant` 触发器**：订阅 `SkillHitConnected`+`MeleeHitConnected`，命中即 `Plant` 并累积本链 Proc（Stacks）。新增 `T_种子`（Trigger · Stacks · styleTag Seed）。
- **`SeedDetonate` 触发器**：`Tick` 轮询 `SeedSystem.ActiveCount > 0` 即 Proc；消费在 `PlayerCombat.EndEnhancement` 前置钩子里调 `DetonateAll` 并 return（不走鼠标落点效果）。新增 `T_引爆`（Trigger · Single · Addon）。需搭配一个伤害效果器（如 `E_范围爆炸`）提供引爆数值。
- **验证**：编译 0 error；两资产导入正确（`SeedPlant/Stacks`、`SeedDetonate/Single`）；`SeedSystem` 种植/合并/引爆/清空编辑器实测（种 4 颗含 1 颗合并→3 颗，引爆返回 3→清 0）；`T_引爆+E_范围爆炸` 编译为 `SeedDetonate/AreaDamage`（dmg 37.5 · r4）确认引爆钩子可达。
- **`傀儡计数` 延后（阻塞）**：当前无持久召唤物可计数——技能召唤仅临时协程，模块链 `SummonPuppet/SummonTurret` 效果器未 spawn，无召唤物注册表。需先建召唤/随从系统再接 `PuppetCount`。

---

## V0.1.15 · P1 状态型触发器（背击标记）+ SkillHitCount 补线（2026-07-02）

按开发待办顺序推进 P1 模块化技能系统的「状态型触发器」缺口。`TriggerType` 枚举早已声明 `SeedPlant/SeedDetonate/BackstabMark/PuppetCount`，但 `TriggerTracker` 从未接线（永不触发）。本次先落地最自洽、无需新世界对象基础设施的 **背击标记**。

- **背击标记（BackstabMark）**：`TriggerTracker` 订阅 `SkillHitConnected` + `MeleeHitConnected`，命中时用 `IsBackstab()` 判定——比较目标前向与「目标→玩家」方向，`dot < 0`（玩家处于目标背弧）即 Proc。新增 `T_背击` 模块（`Trigger` · `ConsumeKind.Single` · cd 4s · styleTag Backstab）。
- **设计注记**：敌人 AI 每帧转身面向玩家，故纯背击窗口主要出现在敌人「攻击前摇（不转身）/ 锁定其他目标」时——定位为高技巧向触发器。
- **附带修复**：`SkillHitCount`（技能命中 N 次）此前声明未接线，补上 `SkillHitConnected` 订阅。
- **验证**：编译 0 error；`T_背击` 资产导入正确（`triggerType=BackstabMark, consumeKind=Single`）；背弧点积符号经编辑器几何测试确认（敌人背向玩家→True，面向玩家→False）。
- **仍待实现**：`种子生成/种子引爆`（需世界种子状态载体 + 可视化 + 引爆效果接线）、`傀儡计数`（需召唤物注册表）。

---

## V0.1.14 · 去修仙重构（移除叙事系统 + 进度线中性化）（2026-07-02）

代码层系统性清除「修仙」残留：局外洞府/局外系统整体移除，事件/惩罚系统删除，纵向进度线中性化为通用等级/经验词汇。分四阶推进，每阶均编译通过；R4 经 Play 模式冒烟测试（0 error / 0 warning）。归档见 [GDD §10.3](design/GDD_秘境探索.md)。

### R1 · 移除洞府/局外系统
- 删除：`SpiritVeinSystem` / `SpiritVeinModule` / `SpiritVeinPickup` / `MeditationChamber` / `SpiritBeastCompanion` / `SpiritBeastGarden` / `CaveOpportunitySystem` / `CaveOpportunityUI`(uxml/uss) / `LingTian` / `ForgeRoom`（含 .meta）。
- 引用清理：`GameManager`（灵兽 spawn / 机缘回洞）、`EnemyBase/Elite/Boss` 与 `TreasureRoom` / `CultivationSystem` 的灵脉掉落、`RunHUD` 灵脉事件、`PauseMenu` 洞府 UI 判定、`GameEvents` 灵脉/灵兽事件结构。

### R2 · 移除事件/惩罚系统
- 删除：`InnerDemonTribulation`（心魔台 + 镜像）/ `InnerDemonMeter`（心魔条）/ `RealmAnomaly`（秘境异象）/ `TribulationTrial`（渡劫战）/ `CultivationSuppression`（境界压制）（含 .meta）。
- 引用清理：`GameEvents` 删 `RealmAnomalyAnnounced`/`Tribulation*`/`InnerDemon*` 事件与 `TribulationOutcome` 枚举；`GameManager` 去心魔重置/异象数值倍率/心魔台生成/渡劫回调/击杀倍率；`RunHUD` 去异象条/渡劫遮罩/心魔条；`EnemyBase/Elite` 去异象掉率与「万灵复苏」复活；`PlayerStateHooks`/`InsightSystem`/`MoralEffects` 去异象修正。
- 保留：`MoralEffects`（道心/因果/寿元，仅切断对已删异象的依赖）。

### R3 · 进度线中性化（保留系统骨架）
- 词汇：境界→**等级/阶**（`一阶`…`六阶`）、秘境层名→**第一层…第六层**、修为→**进阶经验**、悟性/灵力(Insight)→**经验**、成色→**品质**（粗糙/普通/精良/完美）、渡劫/凝实→**晋级/精炼**、渡劫成功→**通关成功**。
- 落点：`CultivationSystem` / `InsightSystem`（文档+日志+数组）、`RunHUD`（等级/历练/经验条）、`GameManager`（层名+胜利文案+日志）、`GameHUD`/`Demo1Setup`（胜利标题）、`GameEvents`/`SaveData`（字段注释）、`ExtractResultPanel`（结算行）、`MainMenu`（入魔→陨落）。系统类名/字段名保留（存档兼容），仅改词汇。
- 未改（留待「主题重命名」独立决策）：主菜单标题「仙途秘境」及 `修仙/洞府/仙物` 等世界观品牌词、`灵气`(shard) 货币名。

### R4 · 验证
- 编译 0 error；Play 模式冒烟：`RealmNames=一阶…六阶`、`QualityNames=粗糙/普通/精良/完美`、`RunInsight`/`RunTempering` 正常累积、`GameManager.CurrentRealmName=第一层`、活动场景 0 missing script、控制台 0 error / 0 warning。

### R5 · 配置层清理（P0 待办）
- **死代码 Avatar 配置链删除**：`AvatarBaseRow/Table`、`ConfigDatabase.Avatars`/`GetAvatar`/加载调用、导表 `ParseAvatarBaseRow`、`Avatar_Base_Config.json`+`.csv`（含 .meta）。`Avatar` 配表仅被加载从未被消费，`StartTemplate` SO 已完全取代化身开局。
- **玩家可见 UI 去已删系统词**：`CharacterSelectUITK` 副标题「法修御灵远击」→「法修远程轰击」；`法修` 角色档案 `roleTag`「远程·御灵」→「远程·法修」、`description` 御灵→远程（含编辑器 `Demo1DataCreator` 同步）。
- **保留**：`ItemInRun`(灵物，事件奖励在用) / `SkillBase`(功法，SkillTuning 在用) 配表及 `灵物/功法` 世界观词、`修仙/秘境` 品牌词——均属活系统或已确认保留，未动；对应字段迁移列为待定（见 [开发待办 P0](design/开发待办.md#p0--文档与配置对齐)）。
- 验证：编译 0 error。

---

## V0.1.13 · 增强系统补全（consumeKind 联动 / 目标·形态改造 / 消费爆发 / 起始模板）（2026-07-02）

补齐 V.08（V0.1.12）遗留的增强改造件与开局差异化，全部经 Play 模式运行时验证。

### 模块占位图标（ModuleAssemblyUI）
- 无 `Sprite icon` 时按**子类单字字形 + 元素/类别配色**生成占位图标（`SubtypeGlyph` / `SubtypeColor`），39 个模块（35 种 `类别:字形` 组合）在装配 UI 均有辨识度，零新美术资源。

### consumeKind 联动（ModuleChain / ModuleAssemblyUI）
- 四种消费模型各带**身份加成**，构成取舍三角（数值集中在 `ModuleChain.ApplyConsumeKindIdentity`）：
  - Single 增伤 ×1.25（单发爆发）｜ Window 增伤 ×1.10 + 范围 ×1.20（择时换范围）｜ Stacks 中性（收益在层数）｜ Auto 增伤 ×0.80（挂机代价）。
  - 同源系数同时作用于增强字段（`enhance*`，Enhancement 角色）与附加字段（`damage`/`radius`，Addon 角色），每角色只读其一。
- 装配 UI 链预览新增「◇ XX 联动：增伤 +X% / 范围 +X%」提示（`ConsumeKindBonusText`，与数值同源）。
- 验证：`M_伤害强化`(×1.4) 下 Single=1.75 / Window=1.54(范围 1.20) / Stacks=1.40 / Auto=1.12。

### TargetFarthest 目标改造（新增 `M_远锁`）
- `ChainConfig.enhanceTargetFarthest` + `PlayerCombat.TryFindFarthestEnemyDir`：挂 TargetFarthest 且非环绕时，核心投射技初始方向从鼠标改为**范围内（22m）最远敌**；否则保持鼠标瞄准（解决瞄准语义冲突）。
- 验证：近敌(+x4)/远敌(+z14) → 锁定远者 dir=(0,0,1)。

### Shape* 形态改造（新增 `ShapeMode` 枚举 + `M_火墙`/`M_火环`/`M_火域`）
- `ChainConfig.enhanceShape`（Wall/Ring/Zone），`CastProjectileSkill` 据此改造核心投射技发射几何：
  - **火墙**：≥5 发平行同向，起点沿垂直方向铺开成墙。
  - **火环**：≥8 发 360° 均分外散（与环绕共用）。
  - **火域（hybrid）**：飞弹照常飞行，命中/寿命结束落点由 `Projectile.SetImpactZone` 生成小型持续区域（`ActiveSkillZone`，每 tick≈本发 30% 伤害）。
- 验证（御剑术基线 3 发）：Wall=5 发/方向数 1、Ring=8 发/8 方向、Zone 3 发均 `_impactZone=true`。

### 消费爆发层（PlayerCombat.PlayConsumeBurst）
- 任意 Proc 被消费瞬间（Enhancement/Addon 皆触发）：角色处**元素色爆闪点光**（intensity 6/range 8，0.14s）+ **元素色特效环** + **按 consumeKind 分级震屏**（Single/Window 中震，Stacks/Auto 轻震），程序化零美术。
- 验证：调用后爆闪光源 + 特效环 + `CameraShakeDriver` 激活。

### 起始模板系统（取代旧「化身开局差异化」）
- 新增 `StartTemplate` SO（3 核心技能 Q/E/R + 角色档案 + 起手模块）+ `StartTemplateRegistry`（`Resources/StartTemplates/`，静态选择跨场景存活）。
- `StartTemplateSelectUI`（UITK 程序化面板，主题色卡片网格）由 `MainMenu`「开始」弹出；选中后 `StartTemplate.ApplyToPlayer()` 重装 3 技能 + 应用档案 + 向 `ModuleInventory` 发起手模块。无模板资产时直接回退默认分配。
- 3 个默认模板：剑修·近战爆发 / 法修·元素范围 / 游侠·投射连锁（各带一条可立即成链的起手模块集）。
- 验证：注册表加载 3 模板；应用「游侠·投射连锁」→ Q/E/R=御剑术/烈焰掌/缩地成寸、背包 4 模块（触发+效果+2 改造）、法修档案（远程普攻）；选择面板截图确认渲染。

---

## V.08 · 模块增强系统落地（核心动作循环 + 增强注入）（2026-06-30）

把 GDD §5（V.08）的「核心技能 × 增强链 · Proc → Consume」从设计落到代码。模块链不再自走，改为挂在核心技能上做**增强器**：触发器决定何时上膛（Proc），玩家按 Q/E/R 释放核心技能时消费增强。

### 数据层（ModuleDef / ModuleChain）
- `ModuleDef` 新增 `ConsumeKind`（Single / Window / Stacks / Auto）与 `EffectRole`（Enhancement / Addon）枚举及字段：`consumeKind` / `windowSeconds` / `maxStacks` / `effectRole`，以及万能件专用的 `universalConsumeKind` / `universalEffectRole`；含 `Sprite icon` 字段。
- `ChainConfig` 扩展 `consumeKind` / `effectRole` / `enhanceDamageMult`（增强型对核心技能的伤害倍率，base 1.0）；`ModuleChain.Compile()` 据 `effectRole` 双轨解释 `damage`，并把改造件 `DamageScale` / `代价·消耗生命` 折算进 `enhanceDamageMult`。

### 触发与槽位（TriggerTracker / ModuleSlotManager）
- `TriggerTracker` 按 `consumeKind` 重写为四套状态机（Single / Window / Stacks / Auto），对外暴露 `ThresholdProgress` / `CurrentStacks` / `WindowRemaining` / `CooldownRemaining` 供 HUD 读取。
- `ModuleSlotManager`：`IsProc` / `ConsumeProc` / `GetConfig` / `HasChain` 按 consumeKind 工作；`Auto` 模式经回调自动释放绑定核心技能。
- **部分链持久化**：`EquipChain` 不再丢弃未成链的半成品（只有触发器或只有效果器也会存进槽位），仅在 `IsValid` 时建 tracker，修复局内逐件装配丢模块 + UI 无法显示「待补」状态的 bug。

### 释放与增强注入（PlayerCombat / SkillModifierApplier / Projectile）
- `HandleSkills` 统一为「按键 → 释放核心技能 → 若链 Proc 则注入增强 + 消费」：`BeginEnhancement`（cast 前设伤害倍率/元素覆盖上下文）→ `UseSkill` → `EndEnhancement`（cast 后施加效果）→ `ConsumeProc`。普通、蓄力、Auto 三条释放路径均接入。
- **增强型（Enhancement）注入核心技能**：
  - 伤害倍率：`enhanceDamageMult`（来自改造件）乘到核心技能伤害（`UseSkill` 的 `chargeDmgMul`）。
  - 元素覆盖：链有元素（效果自带或灼烧→火/冰冻→冰/雷→雷/毒→土）时覆盖核心技能本次命中元素（`CastAreaSkill` / `CastProjectileSkill`）。
  - 即时自益：治疗 / 护盾 / 无敌 / 净化（复用 `ExecuteChainHeal/Shield/Invincible` + 新增 `StatusEffectController.ClearDebuffs`）。
  - 控制 + 附加状态作用到**核心技能实际命中的敌人**：范围技同步捕获命中目标（`_enhHitTargets`）；投射技通过 `Projectile.SetEnhancement(ChainConfig)` 在命中时施加（对象池复用时 `Initialize` 重置 payload）；未命中 / 非范围则回退绕玩家半径。
  - 形态改造（部分）：`enhanceRadiusMult`（RadiusScale/M_扩散）放大核心范围技范围；`enhanceProjectileMult`（CountScale/M_连锁）+ `enhanceExtraProjectiles`（ExtraProjectile/M_额外飞弹）增加核心投射技发数，单发被增强为多发时自动散射；`enhanceChainCount`（TargetChain/M_链锁弹射）让核心投射技命中后自动寻敌反弹（`Projectile.SetChain`：搜索半径 9、伤害衰减 0.8/跳、不重复命中）；`enhanceSurround`（TargetSurround/M_环绕射击）让核心投射技 360° 均分环绕发射（至少 8 发）。Debug 投射物（无 prefab）现也携带增强 payload + 链锁（`CreateDebugProjectile` 返回 `Projectile`）。仅 TargetFarthest、Shape* 仍为 no-op（Sustained/DelayedBlast 见下文节奏改造）。
  - 新增改造件 SO：`M_链锁弹射`（TargetChain，弹射 2 次）、`M_环绕射击`（TargetSurround）。
- **附加型（Addon）**：维持 spawn 独立世界效果（`ExecuteChainEffect`）。
- 抽出共享真源 `SkillModifierApplier.ApplyEnhancementToEnemy()` / `ApplyEnhancementStatus()`，PlayerCombat 与 Projectile 共用，消除重复。

### 表现层（ProcBarsHUD / ModuleAssemblyUI）
- **角色旁三竖条 Proc 指示器**（GDD §5.13）：新增 `ProcBarsHUD`，屏幕空间跟随角色，按 Q/E/R 显示充能进度 / 就绪（元素色 + 呼吸光）/ 层数 / 窗口倒计时 / 冷却 / Auto 脉动，由 `Demo1Setup.CreateHUD` 挂到 GameCanvas。
- **装配 UI 重做**：`ModuleAssemblyUI` 从文字列表改为卡片网格背包 + Q/E/R 三列竖向链布局，含全息预览、人类可读链效果预览、安装 toast（链激活 / 还差 X 成链）、三态配色（已激活 / 待补 / 空）。
- `Demo1Setup` 保证开局 Q/E/R 三个核心技能无空窗（未配置则从 `Data/Skills` 兜底挑选）。

### 节奏改造·持续 / 延迟爆炸（Sustained / DelayedBlast）
- 持续：增强让瞬发范围核心技在落点留下持续地带：`ActiveSkillZone.SpawnCustom(pos, player, mask, radius, life=4s, tick=0.5s, perTickMul, element)`，每 tick 造成技能倍率 35% 的 DoT + 附加状态。`ChainConfig.enhanceSustained` 由 `Sustained` 改造件折算。新增 SO `M_余烬地带`。
- 延迟爆炸：增强让范围核心技在落点追加一次带预警的延迟重爆：新增 `DelayedAreaBlast` 组件（预警 0.8s → 范围伤害 ×1.5 + 元素表现 + 附加状态）。`ChainConfig.enhanceDelayedBlast` 由 `DelayedBlast` 改造件折算。新增 SO `M_延迟轰爆`。

### 持续区域（Zone）增强
- `ActiveSkillZone.SetEnhancement(cfg, elementOverride, radiusMult)`：增强注入区域元素覆盖 + 范围倍率 + 每 tick 附加状态（灼烧/冰冻/毒，复用 `SkillModifierApplier.ApplyEnhancementStatus`）；伤害倍率经 `damageMul` 生效。控制类不逐 tick 施加（避免持续弹飞）。
- PlayerCombat 委托标记 `_enhDelegatedToProjectile` 泛化为 `_enhWorldDelegated`（投射物命中 / 区域 tick 共用），避免与 `EndEnhancement` 绕玩家回退重复施加。

### 运行时验证（Play 模式，2026-07-01）
- 增强链运行时装配正确：`role=Enhancement / consumeKind=Single / enhanceDamageMult=1.4 / enhanceSurround=true`。
- 形态改造·环绕端到端：核心投射技 御剑术 基线 3 发 → 挂环绕增强链后 8 发（走无 prefab 的 Debug 投射物路径）。
- 伤害倍率端到端：投射物伤害 24.0 → 33.6，比值 ×1.40（匹配 `M_伤害强化`），经 `UseSkill` 注入路径。
- 形态改造·链锁端到端：核心投射技挂 `M_链锁弹射` 后，出生投射物 `_chainRemaining=2 / _hasEnh=true`（`Projectile.SetChain` 已注入）。
- 节奏改造端到端（鼠标落点居中后）：`M_余烬地带` → 落点生成 1 个 `ActiveSkillZone_Sustained`；`M_延迟轰爆` → 落点生成 1 个 `DelayedAreaBlast`。四项形态/节奏改造（环绕/链锁/持续/延迟）均已运行时确认生成对应世界对象。

### 旧系统移除
- 「灵物」「御灵 / 化身」系统及相关 UI 已从当前可玩路径移除（历史素材保留在 `_archive` / `设定_*`）。

> 实现状态（已做 / 未做的边界）见 [开发待办 · V.08 实现状态](design/开发待办.md#v08-实现状态2026-06-30)。

---

## V.06 · 文档与设计主轴重构（2026-06-24）

设计层从“搜打撤 + 修仙职业 / 化身成长”收束为 **长局模块化 Build + Hades 式局外结算**。

- 主 GDD 与简明版同步为 `Trigger + Effect + Modifier` 模块化技能系统。
- 独立职业 / 化身系统删除，迁移为 **起始模板 + 状态型触发器**。
- 独立天赋树删除，迁移为 **模块熟练度 / 解锁节点**。
- 独立宝石系统删除，迁移为 **改造件 / 符文**。
- 秘术系统从当前规划删除。
- `开发待办.md`、`Demo路线图.md`、`功法设计表.md`、`灵物设计表.md`、`隐藏组合表.md` 已按 v0.6 方向重写。
- `设定_御灵五系.md`、`设定_化身.md` 改为历史素材库，不再作为现行规则。

---

## V.05 · 数值公式 + 灵物系统重构（2026-06-12）

GDD V.05 版本规划的核心落地：数值公式建立 + 灵物系统全面重构 + 全量接入。

### §13 数值公式重写
- `CombatStats` 新增 5 个 GDD §13 属性：`defense`(防御力)、`avatarCoefficient`(化身系数)、`damageBonusPercent`(增伤百分比)、`armorPenPercent`(减防百分比)、`skillDamagePercent`(技能伤害加成)。
- `StatType` 枚举新增 `Defense` / `AvatarCoefficient` / `DamageBonusPercent` / `ArmorPenPercent` / `SkillDamagePercent`。
- `CombatStats` 新增 `CalcMeleeDamage(targetDef)` / `CalcSkillDamage(targetDef, skillMul)` / `BuildSummonDamage()` 方法，实现 GDD §13 的四条公式：
  - 普攻：`(base × (1 + avatarCoeff + dmgBonus%) - enemyDef × (1 - armorPen%)) × critDmg`
  - 技能：上式 `× skillDmg × (1 + skillDmgPct)`
- `StatusEffectController` / `SpiritRootController` / `ItemInventory.RecalculateStats` 均已支持新 5 属性的 buff 叠加。

### §13 公式全量接入
- **玩家侧**：`PlayerCombat.CheckMeleeHit`（近战连招）、`CastAreaSkill`（范围技能）、`CastProjectileSkill`（投射物技能）、`CastDashSkill`（冲刺留痕）、`CastSummonSkill`（召唤技能）全部切换至 `CalcMeleeDamage` / `CalcSkillDamage` / `BuildSummonDamage`。
- **衍生伤害**：`ActiveSkillZone`（持续区域 tick）、`Projectile`（投射物命中含 armorPen 传递）、`EarthPuppetTurret`（傀儡炮击）全部接入新公式。
- **化身机制**：`SpiritRootWaterController`（影息斩/息影瞬步/水痕收割）、`SpiritRootGoldController`（灵压爆发/大破/剑心大爆发）全部接入。
- **敌人侧**：`EnemyBase` / `EnemyElite` / `EnemyBoss` / `EnemyCharger` / `EnemyMage` / `EnemyRanged` / `EnemyProjectile` / `InnerDemonTribulation` 攻击均改用新公式。
- **敌人防御力**：`GameConfig` 新增 `敌人基础防御力 = 3`；普通×1 / 远程×0.5 / 冲锋×1.5 / 法师×0.6 / 精英×2 / Boss×3。
- **化身系数**：`SpiritRootRegistry` 中每个化身注入 `AvatarCoefficient`：金 0.10 / 木 0.05 / 水 0.08 / 火 0.12 / 土 0.03。
- **ItemPool 同步**：20 个灵物 SO 已同步到 `Resources/Items`，运行时加载就绪。
- 旧 `CalculateDamage()` 调用点已清零（向后兼容方法仍保留但不再有调用方）。

### §5 灵物系统重构
- **分类重写**：`ItemCategory` 从旧 6 类（Attack/Defense/Movement/Anomaly/Skill/洞府材料）改为 GDD §5.2 的 3+1+6 结构：
  - `StatStacking`(数值堆叠) / `MechanicEnhance`(机制增强) / `MechanicModify`(机制修改) + `Skill`(功法) + 洞府素材不变。
  - 旧类型保留为 `[Obsolete]` 别名，序列化兼容。
- **旧灵物全部删除**：`Assets/1Game/Data/Items/` 下 30 个旧 SO 已清除。
- **20 个新灵物 SO 已创建**（按 GDD §5.4 设计 + 自行扩充）：
  - 数值堆叠类 ×10：药蛇之血 / 朱睛冰蟾 / 化界石 / 凝血丹 / 九阳石 / 玄铁护符 / 疾风草 / 天雷印 / 灵龟甲 / 破军符
  - 机制增强类 ×5：岩甲符 / 寒玉髓 / 烈焰精华 / 灵泉石 / 蚀甲虫
  - 机制修改类 ×5：磨剑石 / 因果丝 / 裂空符 / 万灵珠 / 混沌珠
- **配表就绪**：`Item_InRun_Config.csv` 重写（20 行 × 25 列，含 GDD §13 新属性列）；`ConfigTables.ItemInRunRow` 与 `CsvToJsonImporter` 同步更新。
- **SynergySystem** 全部协同条件已迁移至新分类。
- **CodexUITK / ConfigDashboard / ForgeRoom / Demo1DataCreator** 引用全面更新。

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
