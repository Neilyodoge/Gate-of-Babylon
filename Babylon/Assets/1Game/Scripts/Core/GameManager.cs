using UnityEngine;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 游戏管理器 —— 控制整局游戏流程
    /// 线性推进 6 层（练气→筑基→金丹→元婴→化神→渡劫）
    /// 每层包含多个房间（2~3个），通关所有房间后进入下一层
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("境界配置")]
        private readonly string[] _realmNames = { "练气期", "筑基期", "金丹期", "元婴期", "化神期", "渡劫期" };

        [Header("难度曲线")]
        [SerializeField] private int baseEnemyCount = 3;
        [SerializeField] private int enemyCountPerLevel = 2;
        [SerializeField] private float hpScalePerLevel = 0.3f;
        [SerializeField] private float dmgScalePerLevel = 0.2f;

        [Header("房间尺寸")]
        [SerializeField] private float roomSize = 35f;

        [Header("灵物池（在 Inspector 中配置）")]
        [SerializeField] private ItemData[] itemPool;

        // v0.5 Week 4：保留 Inspector 原始 pool，每局重置后再叠加已解锁的炼器灵物
        private ItemData[] _basePool;

        [Header("功法池（在 Inspector 中配置）")]
        [SerializeField] private SkillData[] skillPool;

        [Header("敌人受击特效")]
        [SerializeField] private GameObject enemyHitVFXPrefab;

        [Header("引用")]
        [SerializeField] private Transform roomSpawnPoint;

        // 游戏状态
        private int _currentLevel; // 当前层数（0-5）
        private int _currentRoomInLevel; // 当前层内的房间索引
        private GameObject _currentRoomGo; // 当前房间的 GameObject
        private bool _gameOver;

        // 房间布局（二维：每层包含多个房间）
        private List<List<Minimap.RoomType>> _levelRooms; // [层][房间索引]
        private List<Minimap.RoomType> _levelLayout; // 扁平化布局（兼容小地图）
        private Minimap _minimap;
        private int _flatRoomIndex; // 扁平化的房间索引（用于小地图）

        public int CurrentLevel => _currentLevel;
        public int CurrentRoomInLevel => _currentRoomInLevel;
        public int TotalRoomsInLevel => _levelRooms != null && _currentLevel < _levelRooms.Count ? _levelRooms[_currentLevel].Count : 1;
        public string CurrentRealmName => _currentLevel < _realmNames.Length ? _realmNames[_currentLevel] : "飞升";

        /// <summary>按 itemName 在 itemPool 中查找灵物（化身起手灵物 / 调试用）</summary>
        public ItemData FindItemByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || itemPool == null) return null;
            foreach (var it in itemPool)
                if (it != null && it.itemName == itemName) return it;
            return null;
        }

        /// <summary>
        /// v0.5 Week 4：把炼器房已解锁的灵物注入 itemPool（每局开始时调用）。
        /// 第一次调用时备份 Inspector 原始 pool；之后每次都从备份+解锁集重建，
        /// 避免上一局的运行时 SO 被反复追加。
        /// </summary>
        private void AugmentItemPoolFromForge()
        {
            if (_basePool == null) _basePool = itemPool;

            // v0.5 Week 6：基础池如果空 / 全 null，从 ItemPool 自动注册器（Resources/Items）兜底加载
            bool baseEmpty = _basePool == null || _basePool.Length == 0;
            if (!baseEmpty)
            {
                bool allNull = true;
                foreach (var x in _basePool) if (x != null) { allNull = false; break; }
                if (allNull) baseEmpty = true;
            }
            if (baseEmpty)
            {
                var auto = ItemPool.All;
                if (auto != null && auto.Length > 0)
                {
                    Debug.Log($"<color=cyan>[GameManager] Inspector itemPool 为空 → 自动从 ItemPool 注册 {auto.Length} 件灵物</color>");
                    _basePool = auto;
                }
            }

            itemPool = UnlockedItemPoolLoader.Augment(_basePool);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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

            // 启动 → 进入村庄 Hub。玩家在村里：
            //   1. 默认已选好金化身（不去 NPC 也能直接玩）
            //   2. 想换化身 → 走司命使按 F
            //   3. 出发 → 走山门按 F → StartNewRun()
            EnterVillageHub();
            StatusEffectHUD.EnsureExists();
            SpiritRootMechanicHUD.EnsureExists();
            RunHUD.Ensure();
        }

        /// <summary>
        /// 生成村庄 Hub，把玩家放到中央，并自动激活默认化身（金）。
        /// 山门触发后会调 <see cref="StartNewRun"/>。
        /// </summary>
        private void EnterVillageHub()
        {
            if (_currentRoomGo != null)
                Destroy(_currentRoomGo);

            // 防御：上一局如果在面板打开时被外部强制重启（场景重载、Debug Restart…），
            // 这里把它关掉并把 timeScale 恢复到 1，否则玩家进村会卡在 0 速度。
            SpiritRootSelectUI.Hide();
            if (Time.timeScale < 0.9f) Time.timeScale = 1f;

            Vector3 spawnPos = roomSpawnPoint != null ? roomSpawnPoint.position : Vector3.zero;

            _currentRoomGo = new GameObject("VillageHub");
            _currentRoomGo.transform.position = spawnPos;
            var hub = _currentRoomGo.AddComponent<VillageHub>();
            hub.Initialize(onPortalEntered: StartNewRun);

            // 默认化身：金。玩家可以走到司命使那里重选。
            ApplyDefaultSpiritRootIfNone();

            TeleportPlayer(spawnPos);

            Debug.Log("<color=magenta>═══ 入梦之村 · 选择化身后从山门入梦 ═══</color>");
        }

        /// <summary>
        /// 玩家此前没选过化身 → 自动应用默认（金）；已经选过则跳过，避免重置玩家手选的化身。
        /// </summary>
        private void ApplyDefaultSpiritRootIfNone()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            var ctrl = player.GetComponent<SpiritRootController>();
            if (ctrl == null) return;
            if (ctrl.CurrentRoot != SpiritRootType.None) return;

            ctrl.Select(SpiritRootType.Metal, player.Stats);
        }

        /// <summary>开始新的一局</summary>
        public void StartNewRun()
        {
            _currentLevel = 0;
            _currentRoomInLevel = 0;
            _flatRoomIndex = 0;
            _gameOver = false;

            Debug.Log("<color=magenta>═══════════════════════════</color>");
            Debug.Log("<color=magenta>  入梦... 仙途梦境开始</color>");
            Debug.Log("<color=magenta>═══════════════════════════</color>");

            // v0.5：自动应用所有跨局已解锁的化身天赋
            if (PlayerController.Instance != null)
            {
                PermanentTalentLoader.Apply(PlayerController.Instance);
                // v0.5 Week 4：起手功法 / 阵法台增益 / 灵兽伙伴 —— 三个一次性 / 持久效果
                StartSkillLoader.Apply(PlayerController.Instance);
                FormationBuffApplier.Apply(PlayerController.Instance);
                SpiritBeastLoader.Apply(PlayerController.Instance);
            }

            // v0.5 Week 4：把已解锁的炼器灵物注入本局梦境掉落池
            AugmentItemPoolFromForge();

            // 生成整局的房间布局
            GenerateLevelLayout();

            // 初始化小地图
            if (_minimap != null)
                _minimap.Initialize(_levelLayout);

            SpawnCurrentRoom();
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
            // 0 练气期：战 → 商店（早期见到商店）
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Shop },
            // 1 筑基期：战 → 战 → 宝箱
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Battle, Minimap.RoomType.Treasure },
            // 2 金丹期：战 → 升级 → 休息
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Upgrade, Minimap.RoomType.Rest },
            // 3 元婴期：战 → 商店 → 战
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Shop, Minimap.RoomType.Battle },
            // 4 化神期：战 → 宝箱 → 升级
            new[] { Minimap.RoomType.Battle, Minimap.RoomType.Treasure, Minimap.RoomType.Upgrade },
            // 5 渡劫期：战 → Boss
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

            // 发布境界信息
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

            int enemyCount = baseEnemyCount + _currentLevel * enemyCountPerLevel;
            float hpMul = 1f + _currentLevel * hpScalePerLevel;
            float dmgMul = 1f + _currentLevel * dmgScalePerLevel;
            room.Initialize(_currentLevel, enemyCount, hpMul, dmgMul, itemPool, roomSize, roomSize);
            room.SetSkillPool(skillPool);

            if (enemyHitVFXPrefab != null)
                room.SetEnemyHitVFX(enemyHitVFXPrefab);

            Debug.Log($"<color=yellow>【{CurrentRealmName}】战斗房间 | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1}</color>");
            room.StartBattle();
        }

        private void SpawnBossRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"BossRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<BattleRoom>();

            float hpMul = 1f + _currentLevel * hpScalePerLevel;
            float dmgMul = 1f + _currentLevel * dmgScalePerLevel;
            // Boss房间：少量普通敌人 + 1个Boss
            int normalEnemyCount = 2;
            room.Initialize(_currentLevel, normalEnemyCount, hpMul, dmgMul, itemPool, roomSize, roomSize);
            room.SetSkillPool(skillPool);

            if (enemyHitVFXPrefab != null)
                room.SetEnemyHitVFX(enemyHitVFXPrefab);

            Debug.Log($"<color=red>【{CurrentRealmName}】★ Boss 房间 ★</color>");
            room.StartBattle();

            // 额外生成Boss
            Vector3 bossPos = spawnPos + new Vector3(0, 0, 8f);
            EnemyBoss.Spawn(bossPos, hpMul, dmgMul, _currentLevel, itemPool);
        }

        private void SpawnShopRoom(Vector3 spawnPos)
        {
            _currentRoomGo = new GameObject($"ShopRoom_Lv{_currentLevel}_{CurrentRealmName}");
            _currentRoomGo.transform.position = spawnPos;
            var room = _currentRoomGo.AddComponent<ShopRoom>();
            room.Initialize(_currentLevel, itemPool, skillPool);
            Debug.Log($"<color=yellow>【{CurrentRealmName}】商店房间 — 按F离开</color>");
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
            room.Initialize(_currentLevel, itemPool);
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

            _currentRoomInLevel++;
            _flatRoomIndex++;

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

                LevelTransition.Instance.SpawnPortal(portalPos, () => SpawnCurrentRoom());
            }
            else
            {
                Invoke(nameof(SpawnCurrentRoom), 2f);
            }
        }

        /// <summary>
        /// v0.5 搜打撤：每境界结束时同时生成【出梦点】（撤离）和【下一境界传送门】（继续），让玩家做决策。
        /// </summary>
        private void SpawnExtractPointAndPortal()
        {
            Vector3 roomCenter = _currentRoomGo != null ? _currentRoomGo.transform.position : Vector3.zero;
            float roomHalfDepth = GetCurrentRoomHalfDepth();

            // 检查是否已通关最后一层
            bool isLastRealm = _currentLevel >= _realmNames.Length - 1;

            // 渡劫台：放在房间正北（玩家可选，不强制）
            var tribGo = new GameObject($"HeavenTribulation_Level{_currentLevel}");
            tribGo.transform.position = roomCenter + new Vector3(0, 0, 6f);
            var trib = tribGo.AddComponent<HeavenTribulation>();
            trib.Build(() => { });

            // v0.5 Week 4 心魔劫：化神期（idx=4）/ 渡劫期（idx=5）每境界结束后必出心魔台，
            // 与渡劫台 + 出梦点 + 下一境界传送门 共存，让玩家在四选一中决定。
            // 放在 NE 角，与渡劫台（正北）的 2.5m 交互触发器留足距离，避免互抢路由。
            if (_currentLevel >= 4)
            {
                var demonGo = new GameObject($"InnerDemonCatalyst_Level{_currentLevel}");
                demonGo.transform.position = roomCenter + new Vector3(6f, 0, 8f);
                var demon = demonGo.AddComponent<InnerDemonCatalyst>();
                demon.Build();
            }

            // 出梦点：放在房间西侧（左边）
            var extractGo = new GameObject($"ExtractPoint_Level{_currentLevel}");
            extractGo.transform.position = roomCenter + new Vector3(-6f, 0, 0);
            var ep = extractGo.AddComponent<ExtractPoint>();
            ep.Build(() =>
            {
                // 撤离成功：提交洞府素材 → 50% 悟性转永久 → 清丹药 → 回收灵兽 → 回 VillageHub
                CaveInventory.Instance.CommitCurrentRun();
                InsightSystem.Instance.CommitOnExtract();
                PendingPillCarry.ClearActive();
                SpiritBeastLoader.Despawn();
                EnterVillageHub();
                _transitioning = false;
                _gameOver = false;
                Debug.Log($"<color=#88ff88>[GameManager] 撤离成功 · 回到洞府</color>");
            });

            if (isLastRealm)
            {
                // 最后一层：直接通关
                _currentLevel++;
                _currentRoomInLevel = 0;
                Debug.Log("<color=yellow>✨✨✨ 渡劫成功！飞升成仙！✨✨✨</color>");
                _gameOver = true;
                GameEvents.Publish(new GameEvents.GameWon());
                return;
            }

            // 下一境界传送门：放在房间东侧（右边）
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

            // v0.5 搜打撤：失去本局所有洞府素材，按 10% 折算为灵气补偿；丹药 + 悟性也消失；灵兽伙伴一并销毁
            int qiCompensation = CaveInventory.Instance.AbandonCurrentRun(0.10f);
            PendingPillCarry.ClearActive();
            InsightSystem.Instance.AbandonOnDeath();
            SpiritBeastLoader.Despawn();

            // 增加死亡统计
            SaveSystem.Instance.Data.totalDeaths++;

            // 挂"魂伤" debuff（跨局，洞府里慢慢消除）
            var saveData = SaveSystem.Instance.Data;
            saveData.soulHurtRemainingSec = 3f * 3600f;  // 3 游戏内小时
            SaveSystem.Instance.Save();

            Debug.Log($"<color=red>梦境破碎... 惊醒回到现实（残魂转化 {qiCompensation} 灵气 + 3 小时魂伤）</color>");
        }

        /// <summary>敌人被击杀时奖励灵力碎片</summary>
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

            // v0.5 顿悟系统：按怪物类型加悟性
            int insightAmount = 1;
            if (evt.Enemy != null)
            {
                string n = evt.Enemy.name;
                if (n.Contains("Boss")) insightAmount = 10;
                else if (n.Contains("Elite")) insightAmount = 3;
            }
            InsightSystem.Instance.AddRunInsight(insightAmount, "击杀");
        }

        /// <summary>清理场景中残留的掉落物（上一关未拾取的灵物和功法）</summary>
        private void CleanupLeftoverPickups()
        {
            // 清理灵物拾取物
            var itemPickups = Object.FindObjectsOfType<ItemPickup>();
            foreach (var pickup in itemPickups)
            {
                if (pickup != null)
                    Destroy(pickup.gameObject);
            }

            // 清理功法拾取物
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
                PlayerController.Instance.Inventory.Clear();
                PlayerController.Instance.SpiritSlots.Clear();
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
