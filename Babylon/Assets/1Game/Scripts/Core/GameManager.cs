using UnityEngine;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 游戏管理器 —— 控制整局游戏流程
    /// 线性推进 6 层（第一层→第六层）
    /// 每层包含多个房间（2~3个），通关所有房间后进入下一层
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("层数配置")]
        private readonly string[] _realmNames = { "第一层", "第二层", "第三层", "第四层", "第五层", "第六层" };

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
        /// 通常由 RoomChoiceUI 调用：玩家选了某张卡 → 写回 _levelRooms 对应槽位。
        /// </summary>
        public void OverrideNextRoomType(Minimap.RoomType type)
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
        private List<List<Minimap.RoomType>> _levelRooms; // [层][房间索引]
        private List<Minimap.RoomType> _levelLayout; // 扁平化布局（兼容小地图）
        private Minimap _minimap;
        private int _flatRoomIndex; // 扁平化的房间索引（用于小地图）

        public int CurrentLevel => _currentLevel;
        public int CurrentRoomInLevel => _currentRoomInLevel;
        public int TotalRoomsInLevel => _levelRooms != null && _currentLevel < _levelRooms.Count ? _levelRooms[_currentLevel].Count : 1;
        public string CurrentRealmName => _currentLevel < _realmNames.Length ? _realmNames[_currentLevel] : "巅峰";

        private void InitModuleSystem()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            var inv = player.GetComponent<ModuleInventory>();
            if (inv == null) inv = player.gameObject.AddComponent<ModuleInventory>();
            inv.Clear();

            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots == null) slots = player.gameObject.AddComponent<ModuleSlotManager>();
            slots.ClearAll();

            // Auto-load module pool if Inspector field is empty
            if (modulePool == null || modulePool.Length == 0)
                modulePool = ModulePoolLoader.LoadAll();

            // V0.1.18c 运行时读表：用参数仓库表覆盖模块 SO 数值（仅 Play 模式，缺行回退）。
            // 在 GrantSeedLoadout 之前执行，保证种子 loadout 也用覆盖后的数值。
            ModuleTableApplier.ApplyAll(modulePool);

            // Grant seed loadout (first T+E+M → auto-assemble on Q)
            if (modulePool != null && modulePool.Length > 0)
                ModulePoolLoader.GrantSeedLoadout(player, modulePool);
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

            // v3：跨 session 持久化 TreeMapFlow 开关。
            // 首次运行（无键）→ 强制开启（让玩家立刻看到 3 选 1 卡片）；
            // 后续运行 → 沿用上次 PlayerPrefs；F12 可切换并立即保存
            if (PlayerPrefs.HasKey(PrefKeyTreeMapFlow))
                useTreeMapFlow = PlayerPrefs.GetInt(PrefKeyTreeMapFlow) == 1;
            else
                useTreeMapFlow = true;
            Debug.Log($"<color=cyan>[GameManager] v3 房间 3 选 1：{(useTreeMapFlow ? "已开启" : "已关闭")}（F12 可切换）</color>");
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
            PlayerInfoPanel.Ensure();     // V0.3.0：信息面板（Tab 键切换）
            // v0.5 Week 9：启动时显示主菜单（暂停游戏直到玩家点"开始入梦"）
            MainMenu.ShowOnBoot();
        }

        /// <summary>
        /// 生成村庄 Hub，把玩家放到中央。山门触发后会调 <see cref="StartNewRun"/>。
        /// </summary>
        private void EnterVillageHub()
        {
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            // 清理上局残留的 root-level 对象（ExtractPoint / 传送门 / 散落拾取物）
            if (LevelTransition.Instance != null)
                LevelTransition.Instance.DestroyPortal();
            foreach (var ep in FindObjectsOfType<ExtractPoint>())
                Destroy(ep.gameObject);
            CleanupLeftoverPickups();

            // 防御：上一局如果 timeScale 被改过，进村恢复到 1，否则玩家会卡在 0 速度。
            if (Time.timeScale < 0.9f) Time.timeScale = 1f;

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            _currentRoomGo = new GameObject("VillageHub");
            _currentRoomGo.transform.position = spawnPos;
            var hub = _currentRoomGo.AddComponent<VillageHub>();
            hub.Initialize(onPortalEntered: StartNewRun);

            TeleportPlayer(spawnPos);

            Debug.Log("<color=magenta>═══ 入梦之村 · 配置模块后从山门入梦 ═══</color>");
        }

        /// <summary>开始新的一局</summary>
        public void StartNewRun()
        {
            _currentLevel = 0;
            _currentRoomInLevel = 0;
            _flatRoomIndex = 0;
            _gameOver = false;
            _runStartTime = Time.time;

            // v0.5.7：清零本局累计伤害（轮回一击按此结算）
            RunCombatStats.Reset();

            Debug.Log("<color=magenta>═══════════════════════════</color>");
            Debug.Log("<color=magenta>  入秘境... 仙途秘境开始</color>");
            Debug.Log("<color=magenta>═══════════════════════════</color>");

            // 起手功法 / 阵法台增益 —— 一次性 / 持久效果
            if (PlayerController.Instance != null)
            {
                StartSkillLoader.Apply(PlayerController.Instance);
                FormationBuffApplier.Apply(PlayerController.Instance);
            }

            // GDD V.07：初始化模块系统（确保 ModuleInventory + ModuleSlotManager 存在）
            InitModuleSystem();

            // V0.2：通过 LevelDesignDirector 生成程序化树状地图
            var dir = LevelDesign.LevelDesignDirector.Instance;
            dir.StartNewRun();

            // 从 TreeMap 生成兼容的房间布局（兼容小地图 + 现有 SpawnCurrentRoom）
            GenerateLevelLayoutFromTreeMap(dir.CurrentMap);

            // 初始化小地图
            if (_minimap != null)
                _minimap.Initialize(_levelLayout);

            SpawnCurrentRoom();

            // V0.2.2：遗产模块注入 — 上局选定的模块在首战掉落
            TryInjectLegacyModule();
        }

        /// <summary>
        /// V0.2.2：遗产系统 — 上一局结束时玩家选择的 1 件遗产模块，
        /// 在本局首战斗房地面自动生成 ModulePickup，作为"前世记忆"。
        /// 注入后清空存档字段（一次性使用）。
        /// </summary>
        private void TryInjectLegacyModule()
        {
            var save = SaveSystem.Instance.Data;
            if (string.IsNullOrEmpty(save.lastRunLegacyModuleId)) return;

            // 从 modulePool 查找对应模块
            ModuleDef legacyMod = null;
            if (modulePool != null)
            {
                foreach (var m in modulePool)
                {
                    if (m != null && m.moduleId == save.lastRunLegacyModuleId)
                    {
                        legacyMod = m;
                        break;
                    }
                }
            }

            if (legacyMod == null)
            {
                Debug.LogWarning($"[遗产] 找不到模块 '{save.lastRunLegacyModuleId}'，跳过注入");
                save.lastRunLegacyModuleId = "";
                SaveSystem.Instance.Save();
                return;
            }

            // 在玩家附近生成遗产拾取物
            Vector3 pos = PlayerController.Instance != null
                ? PlayerController.Instance.transform.position + Vector3.right * 2f
                : Vector3.right * 2f;
            ModulePickup.Spawn(legacyMod, pos);

            Debug.Log($"<color=#ffcc33>[遗产] 前世记忆：{legacyMod.displayName}（{legacyMod.category}）已在脚下显现</color>");

            // 清空存档（一次性使用）
            save.lastRunLegacyModuleId = "";
            SaveSystem.Instance.Save();
        }

        /// <summary>
        /// V0.2：从 TreeMap 生成兼容旧 _levelRooms 结构的房间布局。
        /// 把树状图的每一层映射为 _levelRooms 的一个 layer，保持向后兼容。
        /// </summary>
        private void GenerateLevelLayoutFromTreeMap(LevelDesign.TreeMap map)
        {
            _levelRooms = new List<List<Minimap.RoomType>>();
            _levelLayout = new List<Minimap.RoomType>();

            if (map == null || map.Floors.Count == 0)
            {
                Debug.LogWarning("[GameManager] TreeMap 为空，回退固定布局");
                GenerateLevelLayout();
                return;
            }

            for (int f = 0; f < map.Floors.Count; f++)
            {
                var floor = map.Floors[f];
                var rooms = new List<Minimap.RoomType>();
                foreach (var node in floor)
                {
                    rooms.Add(MapLevelRoomToMinimap(node.RoomType));
                }
                if (rooms.Count == 0) rooms.Add(Minimap.RoomType.Battle);
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
            Debug.Log($"<color=cyan>[V0.2] TreeMap 布局：{layoutStr}</color>");
        }

        /// <summary>
        /// 整局房间布局（写死，不再随机）。设计目标：
        ///   - 每境第一间永远战斗（导入战斗节奏）
        ///   - 每境最后是核心房（商店 / 升级 / 宝箱 / 休息 / Boss）
        ///   - 整局保证 2 家商店、1 间休息、2 个宝箱、2 个升级台，节奏稳定
        ///   - 总长度 16 间房，跑一次约 20~30 分钟
        /// </summary>
        private static readonly Minimap.RoomType[][] _fixedLayout =
        {
            // 0 第一层：战 → 商店（早期见到商店）
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Shop },
            // 1 第二层：战 → 战 → 宝箱
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Battle, Minimap.RoomType.Treasure },
            // 2 第三层：战 → 升级 → 休息
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Upgrade, Minimap.RoomType.Rest },
            // 3 第四层：战 → 商店 → 战
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Shop, Minimap.RoomType.Battle },
            // 4 第五层：战 → 宝箱 → 升级
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Treasure, Minimap.RoomType.Upgrade },
            // 5 第六层：战 → Boss
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Boss }
        };

        /// <summary>使用 <see cref="_fixedLayout"/> 装载本局所有房间</summary>
        private void GenerateLevelLayout()
        {
            _levelRooms = new List<List<Minimap.RoomType>>();
            _levelLayout = new List<Minimap.RoomType>();

            for (int i = 0; i < _realmNames.Length; i++)
            {
                var rooms = new List<Minimap.RoomType>(i < _fixedLayout.Length
                    ? _fixedLayout[i]
                    : new[] { Minimap.RoomType.Battle });
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
        private void SpawnCurrentRoom()
        {
            _transitioning = false; // 重置过渡标记

            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            // 清理上一关残留的掉落物（灵物和功法拾取物）
            CleanupLeftoverPickups();

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            // 获取当前层当前房间的类型
            var roomType = _levelRooms[_currentLevel][_currentRoomInLevel];

            // 更新后处理氛围
            if (PostProcessSetup.Instance != null)
                PostProcessSetup.Instance.UpdateAtmosphere(_currentLevel, _realmNames.Length);

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

            switch (roomType)
            {
                case Minimap.RoomType.Battle:
                    SpawnBattleRoom(spawnPos);
                    break;
                case Minimap.RoomType.Elite:
                    SpawnEliteRoom(spawnPos);
                    break;
                case Minimap.RoomType.Event:
                    SpawnEventRoom(spawnPos);
                    break;
                case Minimap.RoomType.Shop:
                    SpawnShopRoom(spawnPos);
                    break;
                case Minimap.RoomType.Rest:
                    SpawnRestRoom(spawnPos);
                    break;
                case Minimap.RoomType.Treasure:
                    SpawnTreasureRoom(spawnPos);
                    break;
                case Minimap.RoomType.Boss:
                    SpawnBossRoom(spawnPos);
                    break;
                case Minimap.RoomType.Upgrade:
                    SpawnUpgradeRoom(spawnPos);
                    break;
            }

            // 将玩家传送到房间中心
            TeleportPlayer(spawnPos);
        }

        private void SpawnBattleRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"BattleRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<BattleRoom>();

            // V0.2：从 TreeMap 配表读取每层缩放倍率（缺省回退旧公式）
            float floorScale = GetCurrentFloorEnemyScale();
            int enemyCount = baseEnemyCount + _currentLevel * enemyCountPerLevel;
            float hpMul = (1f + _currentLevel * hpScalePerLevel) * floorScale;
            float dmgMul = (1f + _currentLevel * dmgScalePerLevel) * floorScale;
            room.Initialize(_currentLevel, enemyCount, hpMul, dmgMul, roomSize, roomSize);
            room.SetSkillPool(skillPool);
            room.SetModulePool(modulePool);

            if (enemyHitVFXPrefab != null)
                room.SetEnemyHitVFX(enemyHitVFXPrefab);

            Debug.Log($"<color=yellow>【{CurrentRealmName}】战斗房间 | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1} | 层缩放 x{floorScale:F2}</color>");
            room.StartBattle();
        }

        private void SpawnBossRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"BossRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<BattleRoom>();

            float hpMul = 1f + _currentLevel * hpScalePerLevel;
            float dmgMul = 1f + _currentLevel * dmgScalePerLevel;
            int normalEnemyCount = 2;
            room.Initialize(_currentLevel, normalEnemyCount, hpMul, dmgMul, roomSize, roomSize);
            room.SetSkillPool(skillPool);
            room.SetModulePool(modulePool);

            if (enemyHitVFXPrefab != null)
                room.SetEnemyHitVFX(enemyHitVFXPrefab);

            Debug.Log($"<color=red>【{CurrentRealmName}】★ Boss 房间 ★</color>");
            room.StartBattle();

            Vector3 bossPos = spawnPos + new Vector3(0, 0, 8f);
            int bossID = LevelDesign.LevelDesignDirector.Instance?.CurrentMap?.ActID ?? 1;
            EnemyBoss.Spawn(bossPos, hpMul, dmgMul, bossID);
        }

        private void SpawnShopRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"ShopRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<ShopRoom>();
            room.Initialize(_currentLevel, skillPool);
            Debug.Log($"<color=yellow>【{CurrentRealmName}】商店房间 — 按F离开</color>");
        }

        /// <summary>V0.2.1：精英战斗房 — 更少但更强的敌人 + 保底高稀有度模块掉落</summary>
        private void SpawnEliteRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"EliteRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<BattleRoom>();

            var config = GameConfig.Instance;
            float eliteHpMul = config != null ? config.精英怪血量倍率 : 3f;
            float eliteDmgMul = config != null ? config.精英怪伤害倍率 : 1.5f;
            float floorScale = GetCurrentFloorEnemyScale();

            int enemyCount = Mathf.Max(2, baseEnemyCount - 1);
            float hpMul = (1f + _currentLevel * hpScalePerLevel) * floorScale * eliteHpMul;
            float dmgMul = (1f + _currentLevel * dmgScalePerLevel) * floorScale * eliteDmgMul;

            room.Initialize(_currentLevel, enemyCount, hpMul, dmgMul, roomSize, roomSize);
            room.SetSkillPool(skillPool);
            room.SetModulePool(modulePool);
            room.SetEliteRoom(true);

            if (enemyHitVFXPrefab != null)
                room.SetEnemyHitVFX(enemyHitVFXPrefab);

            Debug.Log($"<color=#ff8800>【{CurrentRealmName}】★ 精英房 ★ | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1}</color>");
            room.StartBattle();
        }

        /// <summary>V0.2.1：事件房 — 触发叙事事件（StoryEventService），完成后自动 RoomCleared</summary>
        private void SpawnEventRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"EventRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;

            RoomBuilder.Build(_currentRoomGo.transform, roomSize, roomSize, _currentLevel);

            Debug.Log($"<color=#6677ff>【{CurrentRealmName}】事件房 — 触发叙事事件</color>");

            var dir = LevelDesign.LevelDesignDirector.Instance;
            dir.TryTriggerRoomEvent(() =>
            {
                GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _currentRoomInLevel });
            });
        }

        private void SpawnRestRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"RestRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<RestRoom>();
            room.Initialize(_currentLevel);
            Debug.Log($"<color=cyan>【{CurrentRealmName}】休息房间 — 灵泉恢复生命 — 按F离开</color>");
        }

        private void SpawnTreasureRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"TreasureRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<TreasureRoom>();
            room.Initialize(_currentLevel);
            Debug.Log($"<color=yellow>【{CurrentRealmName}】宝箱房间 — 靠近开启 — 按F离开</color>");
        }

        private void SpawnUpgradeRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"UpgradeRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<UpgradeRoom>();
            room.Initialize(_currentLevel);
            Debug.Log($"<color=green>【{CurrentRealmName}】升级房间 — 靠近功法宗师按F修炼 — 按F离开</color>");
        }

        /// <summary>V0.2：从 LevelDesignDirector.CurrentMap 获取当前层的敌人缩放倍率</summary>
        private float GetCurrentFloorEnemyScale()
        {
            var dir = LevelDesign.LevelDesignDirector.Instance;
            if (dir?.CurrentMap == null) return 1f;
            var db = LevelDesign.ConfigDatabase.Instance;
            if (db == null) return 1f;
            foreach (var kv in db.MapStructures)
            {
                if (kv.Value.ActID == dir.CurrentMap.ActID)
                    return kv.Value.GetEnemyScale(_currentLevel);
            }
            return 1f;
        }

        /// <summary>V0.2.1：从配表检查当前层是否有阶段返回点</summary>
        private bool ShouldShowStageReturn()
        {
            var dir = LevelDesign.LevelDesignDirector.Instance;
            if (dir?.CurrentMap == null) return true;
            var db = LevelDesign.ConfigDatabase.Instance;
            if (db == null) return true;
            foreach (var kv in db.MapStructures)
            {
                if (kv.Value.ActID == dir.CurrentMap.ActID)
                    return kv.Value.GetHasStageReturn(_currentLevel);
            }
            return true;
        }

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

            // V0.2：标记 TreeMap 当前节点已完成
            LevelDesign.LevelDesignDirector.Instance?.MarkCurrentNodeCleared();

            _currentRoomInLevel++;
            _flatRoomIndex++;

            // ── TreeMap 动态扩展 ──
            // 固定布局 _fixedLayout 每层只有 2~3 个房间，但 TreeMap 一个 Act 可能有 5~6 个节点。
            // 如果 TreeMap 当前节点还有后续子节点（没走到 Boss），就动态往 _levelRooms 里追加槽位，
            // 防止 _currentRoomInLevel >= layer.Count 被误判为"本层通关"。
            if (useTreeMapFlow)
            {
                var dir = LevelDesign.LevelDesignDirector.Instance;
                if (dir != null && dir.CurrentMap != null)
                {
                    var curNode = dir.CurrentMapNode;
                    bool treeHasMore = curNode != null && curNode.Next != null && curNode.Next.Count > 0;
                    if (treeHasMore && _currentLevel < _levelRooms.Count)
                    {
                        var layer = _levelRooms[_currentLevel];
                        while (_currentRoomInLevel >= layer.Count)
                            layer.Add(Minimap.RoomType.Battle);
                        if (_levelLayout != null)
                            while (_flatRoomIndex >= _levelLayout.Count)
                                _levelLayout.Add(Minimap.RoomType.Battle);
                        Debug.Log($"<color=cyan>[TreeMapFlow] 动态扩展槽位 → layer.Count={layer.Count}  flatLayout={_levelLayout?.Count}</color>");
                    }
                }
            }

            // 检查当前层是否还有下一个房间
            bool levelComplete = _currentRoomInLevel >= _levelRooms[_currentLevel].Count;

            if (levelComplete)
            {
                // v0.5 搜打撤：当前层全部通关 → 在房间内 spawn【出梦点】，玩家可选择撤离 vs 闯下一层
                SpawnExtractPointAndPortal();
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
        /// v3 第 12 章：进入下一房间前的路径决策。
        /// 优先级：TreeMap UI（走格子，依赖 LevelDesignDirector.CurrentMap）
        ///       → RoomChoiceUI（3 选 1 卡片，TreeMap 没就绪时的退化方案）
        ///       → 直接进入（useTreeMapFlow 关闭 或 下一间是 Boss）。
        /// </summary>
        private void EnterNextRoomWithChoice()
        {
            Debug.Log($"<color=cyan>[TreeMapFlow] EnterNextRoomWithChoice  useTreeMapFlow={useTreeMapFlow}  realm={_currentLevel}  roomIdx={_currentRoomInLevel}</color>");
            if (useTreeMapFlow)
            {
                if (TryShowTreeMapNavigation()) return;
                if (TryShowRoomChoice()) return;
            }
            SpawnCurrentRoom();
        }

        /// <summary>
        /// 弹 TreeMap UI 走格子；返回 false 表示当前情境不适合（无 TreeMap / Boss 房 / 无候选节点）。
        /// 玩家选完后 → 把节点类型映射回 Minimap.RoomType → OverrideNextRoomType + SpawnCurrentRoom。
        /// </summary>
        private bool TryShowTreeMapNavigation()
        {
            var dir = LevelDesign.LevelDesignDirector.Instance;
            if (dir == null || dir.CurrentMap == null) return false;

            var cur = dir.CurrentMap.CurrentNode;
            if (cur == null || cur.Next == null || cur.Next.Count == 0) return false;

            // Boss 房保持线性叙事
            if (_levelRooms != null && _currentLevel < _levelRooms.Count)
            {
                var layer = _levelRooms[_currentLevel];
                if (_currentRoomInLevel >= 0 && _currentRoomInLevel < layer.Count
                    && layer[_currentRoomInLevel] == Minimap.RoomType.Boss)
                    return false;
            }

            dir.ShowMap(node =>
            {
                if (node != null)
                {
                    var mapped = MapLevelRoomToMinimap(node.RoomType);
                    OverrideNextRoomType(mapped);
                }
                SpawnCurrentRoom();
            });
            return true;
        }

        /// <summary>
        /// LevelDesign 系统的 LevelRoomType（Battle/Elite/Shop/Event/Boss）→ Minimap.RoomType。
        /// Event 房映射为 Treasure（最接近"特殊房"），Elite 仍归 Battle。
        /// </summary>
        private static Minimap.RoomType MapLevelRoomToMinimap(LevelDesign.LevelRoomType t)
        {
            return t switch
            {
                LevelDesign.LevelRoomType.Battle => Minimap.RoomType.Battle,
                LevelDesign.LevelRoomType.Elite => Minimap.RoomType.Elite,
                LevelDesign.LevelRoomType.Shop => Minimap.RoomType.Shop,
                LevelDesign.LevelRoomType.Event => Minimap.RoomType.Event,
                LevelDesign.LevelRoomType.Boss => Minimap.RoomType.Boss,
                _ => Minimap.RoomType.Battle
            };
        }

        /// <summary>展示 3 选 1 房间卡片。返回 false 表示当前情境不适合展示（例如下一房间是 Boss）。</summary>
        private bool TryShowRoomChoice()
        {
            if (_levelRooms == null || _currentLevel >= _levelRooms.Count)
            {
                Debug.Log("<color=yellow>[TreeMapFlow] 跳过：_levelRooms 为空或越界</color>");
                return false;
            }
            var layer = _levelRooms[_currentLevel];
            if (_currentRoomInLevel < 0 || _currentRoomInLevel >= layer.Count)
            {
                Debug.Log($"<color=yellow>[TreeMapFlow] 跳过：roomIdx={_currentRoomInLevel} 越出 layer({layer.Count})</color>");
                return false;
            }

            var currentSlot = layer[_currentRoomInLevel];
            if (currentSlot == Minimap.RoomType.Boss)
            {
                Debug.Log("<color=yellow>[TreeMapFlow] 跳过：下一间是 Boss 房</color>");
                return false;
            }

            var candidates = BuildRoomCandidates(currentSlot);
            if (candidates == null || candidates.Length < 2)
            {
                Debug.Log("<color=yellow>[TreeMapFlow] 跳过：候选 < 2</color>");
                return false;
            }

            Debug.Log($"<color=cyan>[TreeMapFlow] ★ 弹出 {candidates.Length} 选 1 房间卡片</color>");
            LevelDesign.RoomChoiceUI.Show(candidates, picked =>
            {
                Debug.Log($"<color=cyan>[TreeMapFlow] 玩家选定：{picked}</color>");
                OverrideNextRoomType(picked);
                SpawnCurrentRoom();
            });
            return true;
        }

        /// <summary>构建 3 张候选卡片：包括"默认"那张 + 2 张异类</summary>
        private LevelDesign.RoomChoiceUI.Candidate[] BuildRoomCandidates(Minimap.RoomType defaultType)
        {
            // 在深层后，候选池可以增加"宝箱 / 升级 / 休息"权重
            var pool = new System.Collections.Generic.List<Minimap.RoomType>
            {
                Minimap.RoomType.Battle,
                Minimap.RoomType.Shop,
                Minimap.RoomType.Treasure,
                Minimap.RoomType.Rest,
                Minimap.RoomType.Upgrade
            };
            // 把默认槽位也加入（这样玩家有"按设计走" 的选项）
            pool.Add(defaultType);

            // 去重 + 洗牌
            var distinct = new System.Collections.Generic.HashSet<Minimap.RoomType>(pool);
            var shuffled = new System.Collections.Generic.List<Minimap.RoomType>(distinct);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            // 取前 3
            int n = Mathf.Min(3, shuffled.Count);
            var arr = new LevelDesign.RoomChoiceUI.Candidate[n];
            for (int i = 0; i < n; i++)
            {
                var t = shuffled[i];
                arr[i] = new LevelDesign.RoomChoiceUI.Candidate
                {
                    type = t,
                    title = TypeTitle(t),
                    tooltip = TypeTooltip(t)
                };
            }
            return arr;
        }

        private static string TypeTitle(Minimap.RoomType t) => t switch
        {
            Minimap.RoomType.Battle => "战斗",
            Minimap.RoomType.Elite => "精英",
            Minimap.RoomType.Event => "事件",
            Minimap.RoomType.Shop => "商店",
            Minimap.RoomType.Rest => "休息",
            Minimap.RoomType.Treasure => "宝藏",
            Minimap.RoomType.Boss => "Boss",
            Minimap.RoomType.Upgrade => "悟道",
            _ => "未知"
        };

        private static string TypeTooltip(Minimap.RoomType t) => t switch
        {
            Minimap.RoomType.Battle => "刷怪 + 拾取局内灵物 / 洞府素材",
            Minimap.RoomType.Elite => "精英怪 — 强化敌人 + 保底稀有模块掉落",
            Minimap.RoomType.Event => "叙事事件 — 选择驱动的随机奖励/代价",
            Minimap.RoomType.Shop => "用本局货币购买灵物 / 丹药",
            Minimap.RoomType.Rest => "灵泉静修，回复生命",
            Minimap.RoomType.Treasure => "开启宝箱，获得稀有奖励",
            Minimap.RoomType.Boss => "层 Boss，挑战极限",
            Minimap.RoomType.Upgrade => "拜访功法宗师，强化已有功法",
            _ => ""
        };

        /// <summary>
        /// v0.5 搜打撤：每层结束时同时生成【出梦点】（撤离）和【下一层传送门】（继续），让玩家做决策。
        /// V0.2.1：仅当 HasStageReturn[currentLevel]=1 时显示出梦点，否则直接进入下一层。
        /// </summary>
        private void SpawnExtractPointAndPortal()
        {
            Vector3 roomCenter = _currentRoomGo != null ? _currentRoomGo.transform.position : Vector3.zero;
            float roomHalfDepth = GetCurrentRoomHalfDepth();

            bool isLastRealm = _currentLevel >= _realmNames.Length - 1;

            // V0.2.1：检查配表是否允许本层阶段返回
            bool hasStageReturn = ShouldShowStageReturn();

            if (!isLastRealm && !hasStageReturn)
            {
                // 无阶段返回点 → 直接传送到下一层
                if (LevelTransition.Instance != null)
                {
                    Vector3 portalPos = roomCenter + new Vector3(0f, 0, roomHalfDepth * 0.5f);
                    LevelTransition.Instance.SpawnPortal(portalPos, () =>
                    {
                        _currentLevel++;
                        _currentRoomInLevel = 0;
                        Debug.Log($"<color=magenta>═══ 闯入下一层：{CurrentRealmName} ═══</color>");
                        SpawnCurrentRoom();
                    });
                }
                else
                {
                    _currentLevel++;
                    _currentRoomInLevel = 0;
                    SpawnCurrentRoom();
                }
                return;
            }

            // 逐层挑战台已移除。
            // 秘境层推进只剩"撤离 vs 继续"+ 等级差压制作为难度门槛。

            // 出梦点：放在房间西侧（左边）
            var extractGo = new GameObject($"ExtractPoint_Level{_currentLevel}");
            extractGo.transform.position = roomCenter + new Vector3(-6f, 0, 0);
            var ep = extractGo.AddComponent<ExtractPoint>();
            int capturedLevel = _currentLevel;
            ep.Build(() =>
            {
                float mul = ExtractResultPanel.LayerMultiplier(capturedLevel);
                string realmName = capturedLevel < _realmNames.Length ? _realmNames[capturedLevel] : "巅峰";

                int matCount = CaveInventory.Instance.TotalPendingCount;
                CaveInventory.Instance.CommitCurrentRun();
                int insightRaw = InsightSystem.Instance.CommitOnExtract(mul);
                int temperingRaw = 0;
                if (FeatureFlags.EnableCaveMeta)
                    temperingRaw = CultivationSystem.Instance.CommitOnExtract(mul);

                // V0.2.2：收集模块背包用于遗产选择
                IReadOnlyList<ModuleDef> legacyModules = null;
                if (PlayerController.Instance != null)
                {
                    var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
                    if (inv != null && inv.Modules.Count > 0)
                        legacyModules = inv.Modules;
                }

                ExtractResultPanel.Show(capturedLevel, realmName,
                    insightRaw, temperingRaw, matCount,
                    ExtractResultPanel.EndType.Extract, legacyModules,
                    () =>
                    {
                        EnterVillageHub();
                        _transitioning = false;
                        _gameOver = false;
                        Debug.Log($"<color=#88ff88>[GameManager] 撤离成功 · 回到洞府（层深倍率 ×{mul:F2}）</color>");
                    });
            });

            if (isLastRealm)
            {
                // V0.2.4：通关 → 弹结算面板（EndType.Victory × 2.0 + 遗产选择）
                _gameOver = true;
                _runElapsedTime = Time.time - _runStartTime;
                float victoryMul = 2.0f;
                int insightRaw = InsightSystem.Instance.CommitOnExtract(victoryMul);
                int temperingRaw = 0;
                if (FeatureFlags.EnableCaveMeta)
                    temperingRaw = CultivationSystem.Instance.CommitOnExtract(victoryMul);
                int matCount = CaveInventory.Instance.TotalPendingCount;
                CaveInventory.Instance.CommitCurrentRun();

                IReadOnlyList<ModuleDef> legacyModules = null;
                if (PlayerController.Instance != null)
                {
                    var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
                    if (inv != null && inv.Modules.Count > 0)
                        legacyModules = inv.Modules;
                }

                string realmName = _currentLevel < _realmNames.Length ? _realmNames[_currentLevel] : "巅峰";
                ExtractResultPanel.Show(_currentLevel, realmName,
                    insightRaw, temperingRaw, matCount,
                    ExtractResultPanel.EndType.Victory, legacyModules,
                    () =>
                    {
                        EnterVillageHub();
                        _transitioning = false;
                        _gameOver = false;
                        Debug.Log("<color=yellow>✨✨✨ 通关成功！返回洞府 ✨✨✨</color>");
                    });
                GameEvents.Publish(new GameEvents.GameWon());
                Debug.Log($"<color=lime>[RunTimer] 通关时长：{_runElapsedTime / 60f:F1} 分钟（目标 25-40min）</color>");
                return;
            }

            // 下一层传送门：放在房间东侧（右边）
            if (LevelTransition.Instance != null)
            {
                Vector3 portalPos = roomCenter + new Vector3(6f, 0, 0);
                LevelTransition.Instance.SpawnPortal(portalPos, () =>
                {
                    _currentLevel++;
                    _currentRoomInLevel = 0;
                    Debug.Log($"<color=magenta>═══ 闯入下一层：{CurrentRealmName} ═══</color>");
                    SpawnCurrentRoom();
                });
            }
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

            // 收集玩家当前模块背包用于遗产选择
            IReadOnlyList<ModuleDef> legacyModules = null;
            if (PlayerController.Instance != null)
            {
                var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
                if (inv != null && inv.Modules.Count > 0)
                    legacyModules = inv.Modules;
            }

            // V0.2.2：弹出结算面板（死亡模式 + 遗产选择）
            ExtractResultPanel.Show(_currentLevel, CurrentRealmName,
                insightRaw, temperingRaw, matCount,
                ExtractResultPanel.EndType.Death, legacyModules,
                () =>
                {
                    EnterVillageHub();
                    _transitioning = false;
                    _gameOver = false;
                    Debug.Log($"<color=#ff8866>[GameManager] 死亡结算完成 · 回到洞府（残魂 {qiCompensation} 灵气）</color>");
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
        public void DebugGotoRoom(Minimap.RoomType roomType)
        {
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

            // 更新后处理氛围
            if (PostProcessSetup.Instance != null)
                PostProcessSetup.Instance.UpdateAtmosphere(_currentLevel, _realmNames.Length);

            switch (roomType)
            {
                case Minimap.RoomType.Battle:
                    SpawnBattleRoom(spawnPos);
                    break;
                case Minimap.RoomType.Elite:
                    SpawnEliteRoom(spawnPos);
                    break;
                case Minimap.RoomType.Event:
                    SpawnEventRoom(spawnPos);
                    break;
                case Minimap.RoomType.Shop:
                    SpawnShopRoom(spawnPos);
                    break;
                case Minimap.RoomType.Rest:
                    SpawnRestRoom(spawnPos);
                    break;
                case Minimap.RoomType.Treasure:
                    SpawnTreasureRoom(spawnPos);
                    break;
                case Minimap.RoomType.Boss:
                    SpawnBossRoom(spawnPos);
                    break;
                case Minimap.RoomType.Upgrade:
                    SpawnUpgradeRoom(spawnPos);
                    break;
            }

            // 传送玩家
            TeleportPlayer(spawnPos);

            Debug.Log($"<color=magenta>[Debug] 跳转到 {roomType} 房间</color>");
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
