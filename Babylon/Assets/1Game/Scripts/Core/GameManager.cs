using UnityEngine;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 游戏管理器 —— 控制整局游戏流程
    /// V0.4：线性推进 6 层，每层 10+ 个房间（统一结构），通关所有房间后进入下一层。
    /// 新增准备房间 → 技能三选一 → 战斗开始。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("层数配置")]
        // V0.4.1：3 层结构（每层 12 关 × 3 路线）
        private readonly string[] _realmNames = { "第一层", "第二层", "第三层" };

        [Header("难度曲线")]
        [SerializeField] private int baseEnemyCount = 3;
        [SerializeField] private int enemyCountPerLevel = 2;
        [SerializeField] private float hpScalePerLevel = 0.3f;
        [SerializeField] private float dmgScalePerLevel = 0.2f;

        [Header("房间尺寸")]
        [SerializeField] private float roomSize = 35f;

        [Header("功法池（在 Inspector 中配置）")]
        [SerializeField] private SkillData[] skillPool;

        [Header("模块池（GDD V.07 模块化技能）")]
        [SerializeField] private ModuleDef[] modulePool;

        [Header("敌人受击特效")]
        [SerializeField] private GameObject enemyHitVFXPrefab;

        [Header("引用")]
        [SerializeField] private Transform roomSpawnPoint;

        [Header("v3 第 12 章 · 关卡设计")]
        [Tooltip("启用后，房间清场时弹出 3 选 1 卡片让玩家选下一间类型（参考杀戮尖塔/哈迪斯）。F12 可运行时切换。")]
        [SerializeField] private bool useTreeMapFlow = true;

        // 游戏状态
        private int _currentLevel; // 当前层数（0-5）
        private int _currentRoomInLevel; // 当前层内的房间索引
        private GameObject _currentRoomGo; // 当前房间的 GameObject
        private bool _gameOver;
        private readonly HashSet<int> _clearedEdgarRooms = new();
        private int _activeEdgarRoomIndex = -1;
        private int _defeatedEdgarBosses;
        // V0.2.5：单局时长计时
        private float _runStartTime;
        private float _runElapsedTime;
        /// <summary>当前单局已耗时（秒）</summary>
        public float RunElapsedSeconds => _gameOver ? _runElapsedTime : (Time.time - _runStartTime);

        /// <summary>v3：开关 TreeMap 主循环（可由 Inspector / F12 切换，自动持久化到 PlayerPrefs）</summary>
        public bool UseTreeMapFlow
        {
            get => useTreeMapFlow;
            set
            {
                useTreeMapFlow = value;
                PlayerPrefs.SetInt(PrefKeyTreeMapFlow, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// v3：在 SpawnCurrentRoom 之前强制覆盖下一间房间的类型。
        /// 由 TreeMapUI 分叉图导航调用：玩家在全图上选定下一节点 → 写回 _levelRooms 对应槽位。
        /// </summary>
        public void OverrideNextRoomType(RoomType type)
        {
            if (_levelRooms == null || _currentLevel >= _levelRooms.Count) return;
            var layer = _levelRooms[_currentLevel];
            if (_currentRoomInLevel < 0 || _currentRoomInLevel >= layer.Count) return;
            var old = layer[_currentRoomInLevel];
            if (old == type) return;
            layer[_currentRoomInLevel] = type;
            // 同步小地图（重建扁平化数组）
            if (_minimap != null && _levelLayout != null)
            {
                int flatIdx = 0;
                for (int r = 0; r < _currentLevel; r++) flatIdx += _levelRooms[r].Count;
                flatIdx += _currentRoomInLevel;
                if (flatIdx >= 0 && flatIdx < _levelLayout.Count)
                    _levelLayout[flatIdx] = type;
            }
            Debug.Log($"<color=cyan>[TreeMapFlow] 玩家选择：{old} → {type}</color>");
        }

        // 房间布局（二维：每层包含多个房间）
        private List<List<RoomType>> _levelRooms; // [层][房间索引]
        private List<RoomType> _levelLayout; // 扁平化布局（兼容小地图）
        private Minimap _minimap;
        private int _flatRoomIndex; // 扁平化的房间索引（用于小地图）

        // V0.4.2 解耦：房间生成委托给工厂（IRoomFactory），流程控制不再持有各类 Spawn* 逻辑。
        private readonly IRoomFactory _roomFactory = new RoomFactory();

        public int CurrentLevel => _currentLevel;
        public int CurrentRoomInLevel => _currentRoomInLevel;
        /// <summary>本局境（realm/层）总数，供地图提供者生成对应数量的分叉图脚手架。</summary>
        public int RealmCount => _realmNames.Length;

        // ==================== V0.4.1 掉落物总开关 ====================
        /// <summary>
        /// 世界掉落物总开关。当前奖励方案为全掉落，不再在过关后弹三选一。
        /// 集中一处控制，所有 *.Spawn 工厂在生成前查询它。
        /// </summary>
        public static bool EnableWorldDrops = true;

        /// <summary>
        /// 从普通秘境准备区返回基地。仅准备区出口调用；正式进入地图后不提供此入口。
        /// </summary>
        public void ExitRunPreparationToVillage()
        {
            _transitioning = false;
            _gameOver = false;
            RewardPickUI.ForceHide();
            SkillSelectUI.Hide();
            EnterVillageHub();
        }
        public int TotalRoomsInLevel => _levelRooms != null && _currentLevel < _levelRooms.Count ? _levelRooms[_currentLevel].Count : 1;
        public string CurrentRealmName => _currentLevel < _realmNames.Length ? _realmNames[_currentLevel] : "巅峰";

        private void InitModuleSystem()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            var inventory = player.GetComponent<ModuleInventory>();
            if (inventory == null)
                inventory = player.gameObject.AddComponent<ModuleInventory>();
            inventory.Clear();

            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots == null) slots = player.gameObject.AddComponent<ModuleSlotManager>();
            slots.ClearAll();

            // Auto-load module pool if Inspector field is empty
            if (modulePool == null || modulePool.Length == 0)
                modulePool = ModulePoolLoader.LoadAll();

            // V0.1.18c 运行时读表：用参数仓库表覆盖模块 SO 数值（仅 Play 模式，缺行回退）。
            ModuleTableApplier.ApplyAll(modulePool);

            // 开局不给种子 loadout，Q/E/R 全空（保留普攻）。
            // 玩家通过局内世界掉落逐步获取技能和模块。
        }

        private const string PrefKeyTreeMapFlow = "GoB.UseTreeMapFlow";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // V0.4.1：Edgar Grid3D 生成实体地牢；独立 STS 地图不再驱动进度。
            MapProviders.Current = new EdgarMapProvider();

            // v3：跨 session 持久化 TreeMapFlow 开关。
            // 首次运行（无键）→ 强制开启（让玩家立刻看到 3 选 1 卡片）；
            // 后续运行 → 沿用上次 PlayerPrefs；F12 可切换并立即保存
            if (PlayerPrefs.HasKey(PrefKeyTreeMapFlow))
                useTreeMapFlow = PlayerPrefs.GetInt(PrefKeyTreeMapFlow) == 1;
            else
                useTreeMapFlow = false;
            // Edgar 以实体门和走廊导航，不显示独立地图 UI。
            useTreeMapFlow = false;
            Debug.Log("<color=cyan>[GameManager] Edgar Grid3D 实体地牢已启用</color>");
        }

        private void Start()
        {
            // 从 GameConfig 读取难度曲线参数
            var config = GameConfig.Instance;
            if (config != null)
            {
                baseEnemyCount = config.基础敌人数量;
                enemyCountPerLevel = config.每层增加敌人数;
                hpScalePerLevel = config.每层血量倍率;
                dmgScalePerLevel = config.每层伤害倍率;
                roomSize = config.房间大小;
            }

            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            // 启动 → 进入村庄 Hub。玩家在村里走配置使配模块，走山门出发。
            EnterVillageHub();
            BuffBarUITK.EnsureExists();   // v0.6：UITK 状态栏（取代旧 IMGUI StatusEffectHUD）
            RunHUD.Ensure();
            PauseMenu.Ensure();
            PlayerInfoPanel.Ensure();     // V0.3.0：信息面板（C 键切换，V0.4 解决与 Debug 面板 Tab 键冲突）
            // v0.5 Week 9：启动时显示主菜单
            MainMenu.ShowOnBoot();
        }

        /// <summary>
        /// 生成村庄 Hub，把玩家放到中央。山门触发后会调 <see cref="StartNewRun"/>。
        /// </summary>
        private void EnterVillageHub()
        {
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);
            if (MapProviders.Current is EdgarMapProvider edgar)
                edgar.ClearDungeon();

            // 清理上局残留的 root-level 对象（传送门 / 散落拾取物）
            if (LevelTransition.Instance != null)
                LevelTransition.Instance.DestroyPortal();
            CleanupLeftoverPickups();

            // 防御：上一局如果 timeScale 被改过，进村恢复到 1，否则玩家会卡在 0 速度。
            if (Time.timeScale < 0.9f) Time.timeScale = 1f;

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            _currentRoomGo = new GameObject("VillageHub");
            _currentRoomGo.transform.position = spawnPos;
            var hub = _currentRoomGo.AddComponent<VillageHub>();
            hub.Initialize(onPortalEntered: StartNewRun);

            TeleportPlayer(spawnPos);

            // #1：局外「挂空」——完全去掉 Q/E/R 技能与增强链（仅保留普攻）。
            ClearPlayerLoadout();
            // #8：局外不显示地图（小地图仅在秘境内可见）。
            if (_minimap != null) _minimap.SetVisible(false);

            Debug.Log("<color=magenta>═══ 冒险者基地 · 从秘境之门出发 ═══</color>");
        }

        /// <summary>
        /// #1 局外「挂空」：清空玩家 Q/E/R 技能槽 + 3 条增强链，并刷新技能栏 UI。
        /// 普攻（鼠标左键三段连招）独立于技能槽，不受影响。
        /// </summary>
        private void ClearPlayerLoadout()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.UnequipSkill(0);
                combat.UnequipSkill(1);
                combat.UnequipSkill(2);
            }

            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots != null) slots.ClearAll();

            var inventory = player.GetComponent<ModuleInventory>();
            if (inventory != null) inventory.Clear();

            if (SkillBarUI.Instance != null) SkillBarUI.Instance.RefreshSkillSlots();
        }

        /// <summary>开始新的一局</summary>
        public void StartNewRun()
        {
            _currentLevel = 0;
            _currentRoomInLevel = 0;
            _flatRoomIndex = 0;
            _gameOver = false;
            _clearedEdgarRooms.Clear();
            _activeEdgarRoomIndex = -1;
            _defeatedEdgarBosses = 0;
            _runStartTime = Time.time;
            RewardPickUI.ForceHide();

            // v0.5.7：清零本局累计伤害（轮回一击按此结算）
            RunCombatStats.Reset();

            Debug.Log("<color=magenta>═══════════════════════════</color>");
            Debug.Log("<color=magenta>  进入秘境... 冒险开始</color>");
            Debug.Log("<color=magenta>═══════════════════════════</color>");

            // V0.4.1：进入关卡时自动存档
            SaveSystem.Instance.AutoSave();

            // 阵法台增益保留
            if (PlayerController.Instance != null)
            {
                FormationBuffApplier.Apply(PlayerController.Instance);
            }

            // GDD V.07：初始化模块系统（确保 ModuleInventory + ModuleSlotManager 存在）
            InitModuleSystem();

            // V0.2 / V0.4.2：通过 IMapProvider 生成本局地图（默认 LevelDesign 树状图）
            MapProviders.Current.StartRun();

            // 从地图拓扑生成房间布局（兼容小地图 + 现有 SpawnCurrentRoom）
            GenerateLevelLayoutFromProvider();

            // 初始化小地图（#8：进入秘境才显示）
            if (_minimap != null)
            {
                _minimap.Initialize(_levelLayout);
                _minimap.SetVisible(true);
            }

            // V0.4.1：先进入准备房间；触发正式入口时才选择初始技能，选完进入第一间。
            SpawnPrepRoom();
        }

        /// <summary>生成准备房间：可返回基地；触发正式入口并选择技能后进入第一间。</summary>
        private void SpawnPrepRoom()
        {
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);
            CleanupLeftoverPickups();

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;
            _currentRoomGo = new GameObject("PrepRoom");
            _currentRoomGo.transform.position = spawnPos;
            var prep = _currentRoomGo.AddComponent<PrepRoom>();
            prep.Initialize(skillPool, OnPrepRoomComplete, ExitRunPreparationToVillage);

            TeleportPlayer(spawnPos);
            Debug.Log("<color=#6699ff>═══ 秘境准备区 · 可前往入口或返回基地 ═══</color>");
        }

        private void OnPrepRoomComplete()
        {
            Debug.Log("<color=#66ff99>准备完成 · 择路进入第一处秘境</color>");
            // 首房也经地图点选进入（silverua 全图首层就是起点选择）。
            EnterNextRoomWithChoice();
        }

        /// <summary>
        /// V0.4.2：从 <see cref="IMapProvider"/> 的拓扑生成 _levelRooms 结构的房间布局。
        /// 地图未就绪时回退固定布局。房间类型映射由 provider 在边界完成。
        /// </summary>
        private void GenerateLevelLayoutFromProvider()
        {
            _levelRooms = new List<List<RoomType>>();
            _levelLayout = new List<RoomType>();

            var floors = MapProviders.Current.GetFloors();
            if (floors == null || floors.Count == 0)
            {
                // V0.4.5：LevelDesign provider 现只提供「分叉导航图」（逐间弹全图），
                // 线性房间脚手架统一用 fixedLayout（每境 12 间）；房型由玩家在全图上的选择覆盖。
                GenerateLevelLayout();
                return;
            }

            foreach (var floor in floors)
            {
                var rooms = new List<RoomType>(floor);
                if (rooms.Count == 0) rooms.Add(RoomType.Battle);
                _levelRooms.Add(rooms);
                _levelLayout.AddRange(rooms);
            }

            string layoutStr = "";
            for (int i = 0; i < _levelRooms.Count; i++)
            {
                string name = i < _realmNames.Length ? _realmNames[i] : $"层{i + 1}";
                layoutStr += $"[{name}:";
                foreach (var rt in _levelRooms[i])
                    layoutStr += $" {rt}";
                layoutStr += "] → ";
            }
            Debug.Log($"<color=cyan>[V0.4.2] 地图布局：{layoutStr}</color>");
        }

        /// <summary>
        /// V0.4.1 固定布局回退方案（TreeMap 不可用时）。
        /// 每层 12 关：战→战→精英→商店→战→事件→战→商店→战→精英→战→Boss
        /// 约束：精英 ≤2，商店 ≤4
        /// </summary>
        private static readonly RoomType[][] _fixedLayout =
        {
            new[] { RoomType.Battle, RoomType.Battle, RoomType.Elite,
                    RoomType.Shop, RoomType.Battle, RoomType.Event,
                    RoomType.Battle, RoomType.Shop, RoomType.Battle,
                    RoomType.Elite, RoomType.Battle, RoomType.Boss },
            new[] { RoomType.Battle, RoomType.Battle, RoomType.Elite,
                    RoomType.Shop, RoomType.Battle, RoomType.Event,
                    RoomType.Battle, RoomType.Shop, RoomType.Battle,
                    RoomType.Elite, RoomType.Battle, RoomType.Boss },
            new[] { RoomType.Battle, RoomType.Battle, RoomType.Elite,
                    RoomType.Shop, RoomType.Battle, RoomType.Event,
                    RoomType.Battle, RoomType.Shop, RoomType.Battle,
                    RoomType.Elite, RoomType.Battle, RoomType.Boss },
        };

        /// <summary>使用 <see cref="_fixedLayout"/> 装载本局所有房间</summary>
        private void GenerateLevelLayout()
        {
            _levelRooms = new List<List<RoomType>>();
            _levelLayout = new List<RoomType>();

            for (int i = 0; i < _realmNames.Length; i++)
            {
                var rooms = new List<RoomType>(i < _fixedLayout.Length
                    ? _fixedLayout[i]
                    : new[] { RoomType.Battle });
                _levelRooms.Add(rooms);
                _levelLayout.AddRange(rooms);
            }

            string layoutStr = "";
            for (int i = 0; i < _levelRooms.Count; i++)
            {
                layoutStr += $"[{_realmNames[i]}:";
                foreach (var rt in _levelRooms[i])
                    layoutStr += $" {rt}";
                layoutStr += "] → ";
            }
            Debug.Log($"<color=cyan>房间布局（固定）：{layoutStr}</color>");
        }

        /// <summary>生成当前房间</summary>
        private void SpawnCurrentRoom(bool teleportPlayer = true)
        {
            _transitioning = false; // 重置过渡标记

            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            // Edgar 是同一张常驻地牢，掉落物要留在原房间；旧线性房间才清理残留。
            if (MapProviders.Current is not EdgarMapProvider)
                CleanupLeftoverPickups();

            if (MapProviders.Current is not EdgarMapProvider edgar)
                throw new System.InvalidOperationException(
                    $"当前关卡必须使用 {nameof(EdgarMapProvider)}，实际为 {MapProviders.Current?.GetType().Name ?? "null"}。");

            if (!edgar.TryGetCurrentPlacement(out var placement))
                throw new System.InvalidOperationException(
                    $"Edgar 地牢未能提供房间落点：Realm={_currentLevel}, Room={_currentRoomInLevel}。请先修复生成错误。");

            Vector3 spawnPos = placement.SpawnPosition;
            float activeRoomSize = placement.RoomSize;
            const bool buildRoomGeometry = false;
            _activeEdgarRoomIndex = _currentRoomInLevel;

            // 获取当前层当前房间的类型
            var roomType = _levelRooms[_currentLevel][_currentRoomInLevel];

            // 更新小地图
            if (_minimap != null)
                _minimap.UpdateCurrentRoom(_flatRoomIndex);

            // 发布层级信息
            GameEvents.Publish(new GameEvents.RealmBreakthrough
            {
                NewRealmLevel = _currentLevel,
                RealmName = CurrentRealmName
            });

            Debug.Log($"<color=cyan>【{CurrentRealmName}】房间 {_currentRoomInLevel + 1}/{_levelRooms[_currentLevel].Count} — {roomType}</color>");

            // V0.4.2：房间生成委托给 IRoomFactory
            _currentRoomGo = _roomFactory.Spawn(
                roomType,
                BuildRoomContext(
                    spawnPos,
                    activeRoomSize,
                    buildRoomGeometry,
                    placement.Instance.RoomTemplateInstance.transform));
            _currentRoomGo.GetComponent<RoomRuntimeController>()?.Enter();

            if (teleportPlayer)
                TeleportPlayer(spawnPos);

        }

        /// <summary>
        /// 玩家通过 Edgar 实体门廊进入新房间。已清理房间允许自由回访，不会重复生成遭遇。
        /// </summary>
        public void EnterEdgarRoom(int roomIndex)
        {
            if (MapProviders.Current is not EdgarMapProvider edgar
                || _gameOver
                || roomIndex < 0
                || _levelRooms == null
                || _currentLevel >= _levelRooms.Count
                || roomIndex >= _levelRooms[_currentLevel].Count)
                return;

            if (roomIndex == _activeEdgarRoomIndex || _clearedEdgarRooms.Contains(roomIndex))
                return;

            var activeBattle = _currentRoomGo != null
                ? _currentRoomGo.GetComponent<BattleRoom>()
                : null;
            if (activeBattle != null && !activeBattle.IsCleared)
                return;

            _currentRoomInLevel = roomIndex;
            _flatRoomIndex = roomIndex;
            edgar.SelectRoom(roomIndex);
            SpawnCurrentRoom(teleportPlayer: false);
        }

        /// <summary>V0.4.2：打包当局参数供 <see cref="IRoomFactory"/> 生成房间。</summary>
        private RoomSpawnContext BuildRoomContext(
            Vector3 spawnPos,
            float activeRoomSize,
            bool buildRoomGeometry,
            Transform contentRoot = null)
        {
            var roomAuthoring = contentRoot != null
                ? contentRoot.GetComponent<DungeonRoomAuthoring>()
                : null;
            return new RoomSpawnContext
            {
                level = _currentLevel,
                realmName = CurrentRealmName,
                spawnPos = spawnPos,
                roomSize = activeRoomSize,
                skillPool = skillPool,
                modulePool = modulePool,
                enemyHitVFX = enemyHitVFXPrefab,
                baseEnemyCount = baseEnemyCount,
                enemyCountPerLevel = enemyCountPerLevel,
                hpScalePerLevel = hpScalePerLevel,
                dmgScalePerLevel = dmgScalePerLevel,
                floorScale = GetCurrentFloorEnemyScale(),
                bossActId = MapProviders.Current.CurrentActId,
                roomIndex = _currentRoomInLevel,
                roomCount = _levelRooms != null && _currentLevel < _levelRooms.Count
                    ? _levelRooms[_currentLevel].Count
                    : 1,
                encounterSeed = unchecked(
                    ((_currentLevel + 1) * 73856093)
                    ^ ((_currentRoomInLevel + 1) * 19349663)),
                buildRoomGeometry = buildRoomGeometry,
                contentRoot = contentRoot,
                district = roomAuthoring != null
                    ? roomAuthoring.District
                    : XianTu.LevelDesign.District.Outer,
                hasDistrict = roomAuthoring != null,
            };
        }

        /// <summary>V0.4.2：当前层敌人数值缩放（经 IMapProvider）</summary>
        private float GetCurrentFloorEnemyScale() => MapProviders.Current.GetEnemyScale(_currentLevel);

        /// <summary>V0.4.2：当前层是否显示阶段返回点（经 IMapProvider）</summary>
        private bool ShouldShowStageReturn() => MapProviders.Current.GetHasStageReturn(_currentLevel);

        private void TeleportPlayer(Vector3 pos)
        {
            if (PlayerController.Instance != null)
            {
                var cc = PlayerController.Instance.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                PlayerController.Instance.transform.position = pos + Vector3.up * 0.1f;
                if (cc != null) cc.enabled = true;
            }
        }

        // 防止连续触发RoomCleared导致跳层
        private bool _transitioning;

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            if (_gameOver || _transitioning) return;
            _transitioning = true;
            ContinueAfterRoomCleared(evt);
        }

        private void ContinueAfterRoomCleared(GameEvents.RoomCleared evt)
        {
            // V0.4.2：标记地图当前节点已完成
            MapProviders.Current.MarkCurrentCleared();

            if (MapProviders.Current is EdgarMapProvider)
            {
                int clearedIndex = _currentRoomInLevel;
                _clearedEdgarRooms.Add(clearedIndex);

                bool isBoss = _levelRooms != null
                              && _currentLevel < _levelRooms.Count
                              && clearedIndex >= 0
                              && clearedIndex < _levelRooms[_currentLevel].Count
                              && _levelRooms[_currentLevel][clearedIndex] == RoomType.Boss;
                if (isBoss)
                    _defeatedEdgarBosses++;

                _transitioning = false;
                Debug.Log($"<color=#66ccff>[Edgar] 房间 {clearedIndex + 1} 已清理；Boss {_defeatedEdgarBosses}/{EdgarMapProvider.RequiredBossCount}</color>");

                // 普通清场只解锁实体门，不生成逐房传送门。
                // 本局对侧端点 Boss 击败后，生成进入下一层的出口。
                if (_defeatedEdgarBosses >= EdgarMapProvider.RequiredBossCount)
                    SpawnLevelCompletePortal();
                return;
            }

            _currentRoomInLevel++;
            _flatRoomIndex++;

            // ── 地图动态扩展 ──
            // 固定布局 _fixedLayout 每层只有若干房间，但地图一个 Act 可能有更多节点。
            // 如果地图当前节点还有后续节点（没走到 Boss），就动态往 _levelRooms 里追加槽位，
            // 防止 _currentRoomInLevel >= layer.Count 被误判为"本层通关"。
            if (useTreeMapFlow)
            {
                if (MapProviders.Current.CurrentNodeHasNext && _currentLevel < _levelRooms.Count)
                {
                    var layer = _levelRooms[_currentLevel];
                    while (_currentRoomInLevel >= layer.Count)
                        layer.Add(RoomType.Battle);
                    if (_levelLayout != null)
                        while (_flatRoomIndex >= _levelLayout.Count)
                            _levelLayout.Add(RoomType.Battle);
                    Debug.Log($"<color=cyan>[MapFlow] 动态扩展槽位 → layer.Count={layer.Count}  flatLayout={_levelLayout?.Count}</color>");
                }
            }

            // 检查当前层是否还有下一个房间
            bool levelComplete = _currentRoomInLevel >= _levelRooms[_currentLevel].Count;

            if (levelComplete)
            {
                // V0.4：当前层全部通关 → 生成传送门进入下一层（或通关结算）
                SpawnLevelCompletePortal();
                return;
            }
            else
            {
                Debug.Log($"<color=cyan>进入下一个房间：{CurrentRealmName} 房间 {_currentRoomInLevel + 1}/{_levelRooms[_currentLevel].Count}</color>");
            }

            // 在房间北侧生成传送门（同层内房间过渡）
            if (LevelTransition.Instance != null)
            {
                float roomHalfDepth = GetCurrentRoomHalfDepth();
                Vector3 roomCenter = _currentRoomGo != null ? _currentRoomGo.transform.position : Vector3.zero;
                Vector3 portalPos = roomCenter + new Vector3(0, 0, roomHalfDepth * 0.5f);

                LevelTransition.Instance.SpawnPortal(portalPos, () => EnterNextRoomWithChoice());
            }
            else
            {
                Invoke(nameof(EnterNextRoomWithChoice), 2f);
            }
        }

        /// <summary>
        /// V0.4.5：进入下一房间前的路径决策 —— 统一走单一 STS 全图（TreeMapUI）。
        /// 优先级：TreeMap 分叉图导航（依赖 LevelDesignDirector.CurrentMap）
        ///       → 直接进入（useTreeMapFlow 关闭 或 下一间是 Boss / 无候选节点）。
        /// </summary>
        private void EnterNextRoomWithChoice()
        {
            Debug.Log($"<color=cyan>[TreeMapFlow] EnterNextRoomWithChoice  useTreeMapFlow={useTreeMapFlow}  realm={_currentLevel}  roomIdx={_currentRoomInLevel}</color>");
            // V0.4.5：导航统一为单一 STS 全图（TreeMapUI）。已移除 RoomChoiceUI 三选一退化分支。
            if (useTreeMapFlow && TryShowTreeMapNavigation()) return;
            SpawnCurrentRoom();
        }

        /// <summary>
        /// V0.4.2：弹地图导航 UI 走格子（经 IMapProvider）；返回 false 表示当前情境不适合
        /// （无地图 / Boss 房 / 无候选节点）。玩家选完后 → OverrideNextRoomType + SpawnCurrentRoom。
        /// </summary>
        private bool TryShowTreeMapNavigation()
        {
            // Boss 房保持线性叙事：把"下一槽位是否 Boss"交给 provider 判定
            bool bossNext = false;
            if (_levelRooms != null && _currentLevel < _levelRooms.Count)
            {
                var layer = _levelRooms[_currentLevel];
                bossNext = _currentRoomInLevel >= 0 && _currentRoomInLevel < layer.Count
                           && layer[_currentRoomInLevel] == RoomType.Boss;
            }

            return MapProviders.Current.TryShowNavigation(bossNext, picked =>
            {
                OverrideNextRoomType(picked);
                SpawnCurrentRoom();
            });
        }

        /// <summary>
        /// V0.4：当前层通关后，生成传送门进入下一层；最终层则弹通关结算面板。
        /// 不再有撤离/出梦点选择，标准 roguelike 流程。
        /// </summary>
        private void SpawnLevelCompletePortal()
        {
            Vector3 roomCenter = _currentRoomGo != null ? _currentRoomGo.transform.position : Vector3.zero;
            float roomHalfDepth = GetCurrentRoomHalfDepth();
            bool isLastRealm = _currentLevel >= _realmNames.Length - 1;
            Vector3 portalPos = roomCenter + new Vector3(0f, 0f, roomHalfDepth * 0.5f);
            var runtime = FindFirstObjectByType<EdgarDungeonRuntime>();
            if (MapProviders.Current is EdgarMapProvider
                && runtime != null
                && runtime.TryGetContentSocketPosition(
                    _currentRoomInLevel,
                    DungeonContentSocketType.ExitPortal,
                    out Vector3 socketPosition))
                portalPos = socketPosition;

            void CompleteLevel()
            {
                if (isLastRealm)
                {
                    _gameOver = true;
                    _runElapsedTime = Time.time - _runStartTime;
                    float victoryMul = 2.0f;
                    int insightRaw = InsightSystem.Instance.CommitOnExtract(victoryMul);
                    int temperingRaw = 0;
                    if (FeatureFlags.EnableCaveMeta)
                        temperingRaw = CultivationSystem.Instance.CommitOnExtract(victoryMul);
                    int matCount = CaveInventory.Instance.TotalPendingCount;
                    CaveInventory.Instance.CommitCurrentRun();

                    string realmName = _currentLevel < _realmNames.Length
                        ? _realmNames[_currentLevel]
                        : "巅峰";
                    ExtractResultPanel.Show(
                        _currentLevel,
                        realmName,
                        insightRaw,
                        temperingRaw,
                        matCount,
                        ExtractResultPanel.EndType.Victory,
                        () =>
                        {
                            EnterVillageHub();
                            _transitioning = false;
                            _gameOver = false;
                            Debug.Log("<color=yellow>✨✨✨ 通关成功！返回基地 ✨✨✨</color>");
                        });
                    GameEvents.Publish(new GameEvents.GameWon());
                    Debug.Log(
                        $"<color=lime>[RunTimer] 通关时长：{_runElapsedTime / 60f:F1} 分钟（目标 25-40min）</color>");
                    return;
                }

                _currentLevel++;
                _currentRoomInLevel = 0;
                _clearedEdgarRooms.Clear();
                _activeEdgarRoomIndex = -1;
                _defeatedEdgarBosses = 0;
                MapProviders.Current.OnEnterRealm(_currentLevel);
                Debug.Log($"<color=magenta>═══ 进入下一层：{CurrentRealmName} ═══</color>");
                EnterNextRoomWithChoice();
            }

            if (LevelTransition.Instance != null)
                LevelTransition.Instance.SpawnPortal(portalPos, CompleteLevel);
            else
                CompleteLevel();
        }

        /// <summary>获取当前房间的半深度（用于定位传送门）</summary>
        private float GetCurrentRoomHalfDepth()
        {
            if (_currentRoomGo == null) return 10f;

            var battleRoom = _currentRoomGo.GetComponent<BattleRoom>();
            if (battleRoom != null) return battleRoom.RoomDepth / 2f;

            var shopRoom = _currentRoomGo.GetComponent<ShopRoom>();
            if (shopRoom != null) return shopRoom.RoomDepth / 2f;

            var restRoom = _currentRoomGo.GetComponent<RestRoom>();
            if (restRoom != null) return restRoom.RoomDepth / 2f;

            var treasureRoom = _currentRoomGo.GetComponent<TreasureRoom>();
            if (treasureRoom != null) return treasureRoom.RoomDepth / 2f;

            var upgradeRoom = _currentRoomGo.GetComponent<UpgradeRoom>();
            if (upgradeRoom != null) return upgradeRoom.RoomDepth / 2f;

            return 10f;
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            _gameOver = true;
            _runElapsedTime = Time.time - _runStartTime;
            Debug.Log($"<color=red>[RunTimer] 死亡时长：{_runElapsedTime / 60f:F1} 分钟（目标 25-40min）</color>");

            // V0.2.2：死亡 0.5x 经验转入永久（不再全丢）
            int insightRaw = InsightSystem.Instance.RunInsight;
            int temperingRaw = 0;
            if (FeatureFlags.EnableCaveMeta)
                temperingRaw = CultivationSystem.Instance.RunTempering;

            InsightSystem.Instance.CommitOnDeath(0.5f);
            if (FeatureFlags.EnableCaveMeta)
                CultivationSystem.Instance.ReincarnateOnDeath();

            int matCount = CaveInventory.Instance.TotalPendingCount;
            int qiCompensation = CaveInventory.Instance.AbandonCurrentRun(0.10f);

            SaveSystem.Instance.Data.totalDeaths++;

            SaveSystem.Instance.Save();

            // 弹出结算面板；局内技能、模块和增强链在回基地时统一清空。
            ExtractResultPanel.Show(_currentLevel, CurrentRealmName,
                insightRaw, temperingRaw, matCount,
                ExtractResultPanel.EndType.Death,
                () =>
                {
                    EnterVillageHub();
                    _transitioning = false;
                    _gameOver = false;
                    Debug.Log($"<color=#ff8866>[GameManager] 死亡结算完成 · 回到基地（补偿 {qiCompensation} 资源）</color>");
                });
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (PlayerResources.Instance == null) return;

            // 经济平衡（2026-04 调整）：
            // 旧公式 Random.Range(2, 6) + _currentLevel 在 6 层下整局给 ~300-600 碎片，配合 5 件商品 ≈10-50 单价
            // 出现"钱多到买不完"的体感。这里降到约一半，让商店物价显得有分量。
            int baseShards = Random.Range(1, 3);          // 1-2
            int levelBonus = _currentLevel / 2;           // 0..2
            int totalShards = baseShards + levelBonus;     // 1..4

            PlayerResources.Instance.AddShards(totalShards);

            // 经验系统：按怪物类型加经验
            int insightAmount = 1;
            if (evt.Enemy != null)
            {
                string n = evt.Enemy.name;
                if (n.Contains("Boss")) insightAmount = 10;
                else if (n.Contains("Elite")) insightAmount = 3;
            }
            InsightSystem.Instance.AddRunInsight(insightAmount, "击杀");
            // 击杀同时累积历练（普通 +1 / 精英 +5 / Boss +20）（V.03 Q7：meta 暂缓时不累积）
            if (FeatureFlags.EnableCaveMeta)
            {
                int temperingAmount = insightAmount == 10 ? 20 : (insightAmount == 3 ? 5 : 1);
                CultivationSystem.Instance.AddRunTempering(temperingAmount, "击杀");
            }
        }

        private void CleanupLeftoverPickups()
        {
            var skillPickups = Object.FindObjectsOfType<SkillPickup>();
            foreach (var pickup in skillPickups)
            {
                if (pickup != null)
                    Destroy(pickup.gameObject);
            }

            // 清理残留的敌人（防止上一关的敌人遗留）
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (e != null)
                    Destroy(e);
            }
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }

        /// <summary>设置小地图引用</summary>
        public void SetMinimap(Minimap minimap)
        {
            _minimap = minimap;
        }

        /// <summary>重新开始（UI 按钮调用）</summary>
        public void Restart()
        {
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            if (LevelTransition.Instance != null)
                LevelTransition.Instance.DestroyPortal();

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.Stats.ResetHp();
            }

            if (PlayerResources.Instance != null)
                PlayerResources.Instance.Clear();

            GameEvents.Clear();
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // ==================== Debug 接口 ====================

        /// <summary>Debug：直接跳转到指定类型的房间</summary>
        public void DebugGotoRoom(RoomType roomType)
        {
            if (MapProviders.Current is EdgarMapProvider)
            {
                if (!DebugGotoEdgarRoom(roomType))
                    Debug.LogError($"[Debug] Edgar 地牢中找不到 {roomType} 房间。");
                return;
            }

            _gameOver = false;
            _transitioning = false;

            // 销毁当前房间
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            // 销毁传送门
            if (LevelTransition.Instance != null)
                LevelTransition.Instance.DestroyPortal();

            // 清除所有残留敌人
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
                Destroy(e);

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            // V0.4.2：房间生成委托给 IRoomFactory
            _currentRoomGo = _roomFactory.Spawn(
                roomType,
                BuildRoomContext(spawnPos, roomSize, buildRoomGeometry: true));
            _currentRoomGo.GetComponent<RoomRuntimeController>()?.Enter();

            // 传送玩家
            TeleportPlayer(spawnPos);

            Debug.Log($"<color=magenta>[Debug] 跳转到 {roomType} 房间</color>");
        }

        public bool DebugGotoEdgarRoom(RoomType roomType)
        {
            if (MapProviders.Current is not EdgarMapProvider edgar
                || !edgar.TryFindRoomIndex(roomType, out int roomIndex))
                return false;
            return DebugGotoEdgarRoom(roomIndex, edgar);
        }

        public bool DebugGotoEdgarRoom(string nodeName)
        {
            if (MapProviders.Current is not EdgarMapProvider edgar
                || string.IsNullOrWhiteSpace(nodeName))
                return false;
            if (!edgar.TryGetCurrentPlacement(out _))
                return false;

            var runtime = FindFirstObjectByType<EdgarDungeonRuntime>();
            if (runtime == null)
                return false;
            for (int i = 0; i < runtime.RoomCount; i++)
            {
                if (string.Equals(
                        runtime.GetNodeName(i),
                        nodeName,
                        System.StringComparison.OrdinalIgnoreCase))
                    return DebugGotoEdgarRoom(i, edgar);
            }
            return false;
        }

        private bool DebugGotoEdgarRoom(int roomIndex, EdgarMapProvider edgar)
        {
            if (_levelRooms == null
                || _currentLevel < 0
                || _currentLevel >= _levelRooms.Count
                || roomIndex < 0
                || roomIndex >= _levelRooms[_currentLevel].Count)
                return false;

            _gameOver = false;
            _transitioning = false;
            if (LevelTransition.Instance != null)
                LevelTransition.Instance.DestroyPortal();
            foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
                Destroy(enemy);

            _clearedEdgarRooms.Remove(roomIndex);
            _activeEdgarRoomIndex = -1;
            RoomRuntimeController.DebugResetRoom(roomIndex);
            _currentRoomInLevel = roomIndex;
            _flatRoomIndex = roomIndex;
            edgar.SelectRoom(roomIndex);
            SpawnCurrentRoom(teleportPlayer: true);

            Debug.Log(
                $"<color=magenta>[Debug] 直达 Edgar 节点 " +
                $"{FindFirstObjectByType<EdgarDungeonRuntime>()?.GetNodeName(roomIndex)}，" +
                $"Room={roomIndex}，Type={_levelRooms[_currentLevel][roomIndex]}</color>");
            return true;
        }

        /// <summary>Debug：设置当前层数</summary>
        public void DebugSetLevel(int level)
        {
            _currentLevel = Mathf.Clamp(level, 0, _realmNames.Length - 1);
            _gameOver = false;
            Debug.Log($"<color=magenta>[Debug] 设置层数为 {_currentLevel}（{CurrentRealmName}）</color>");
        }
    }
}
