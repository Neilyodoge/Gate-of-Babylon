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

            StartNewRun();
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

            // 生成整局的房间布局
            GenerateLevelLayout();

            // 初始化小地图
            if (_minimap != null)
                _minimap.Initialize(_levelLayout);

            SpawnCurrentRoom();
        }

        /// <summary>生成整局的房间布局（每层2~3个房间）</summary>
        private void GenerateLevelLayout()
        {
            _levelRooms = new List<List<Minimap.RoomType>>();
            _levelLayout = new List<Minimap.RoomType>(); // 扁平化，兼容小地图

            for (int i = 0; i < _realmNames.Length; i++)
            {
                var rooms = new List<Minimap.RoomType>();

                if (i == _realmNames.Length - 1)
                {
                    // 最后一层：1个战斗 + Boss
                    rooms.Add(Minimap.RoomType.Battle);
                    rooms.Add(Minimap.RoomType.Boss);
                }
                else if (i == 0)
                {
                    // 第一层：2个战斗房间（教学/热身）
                    rooms.Add(Minimap.RoomType.Battle);
                    rooms.Add(Minimap.RoomType.Battle);
                }
                else
                {
                    // 中间层：2~3个房间，第一个固定战斗，后面随机
                    rooms.Add(Minimap.RoomType.Battle);

                    // 第二个房间随机
                    float roll = Random.value;
                    if (roll < 0.35f)
                        rooms.Add(Minimap.RoomType.Battle);
                    else if (roll < 0.55f)
                        rooms.Add(Minimap.RoomType.Shop);
                    else if (roll < 0.75f)
                        rooms.Add(Minimap.RoomType.Treasure);
                    else
                        rooms.Add(Minimap.RoomType.Rest);

                    // 50%概率有第三个房间（战斗）
                    if (Random.value < 0.5f)
                        rooms.Add(Minimap.RoomType.Battle);
                }

                _levelRooms.Add(rooms);
                _levelLayout.AddRange(rooms);
            }

            // 确保整局至少有一个商店和一个休息房间
            bool hasShop = false, hasRest = false;
            foreach (var rooms in _levelRooms)
                foreach (var rt in rooms)
                {
                    if (rt == Minimap.RoomType.Shop) hasShop = true;
                    if (rt == Minimap.RoomType.Rest) hasRest = true;
                }

            // 如果缺少商店，在第2层第2个房间插入
            if (!hasShop && _levelRooms.Count > 2 && _levelRooms[2].Count > 1)
                _levelRooms[2][1] = Minimap.RoomType.Shop;
            // 如果缺少休息，在第3层第2个房间插入
            if (!hasRest && _levelRooms.Count > 3 && _levelRooms[3].Count > 1)
                _levelRooms[3][1] = Minimap.RoomType.Rest;

            // 重建扁平化布局
            _levelLayout.Clear();
            foreach (var rooms in _levelRooms)
                _levelLayout.AddRange(rooms);

            // 打印布局
            string layoutStr = "";
            for (int i = 0; i < _levelRooms.Count; i++)
            {
                layoutStr += $"[{_realmNames[i]}:";
                foreach (var rt in _levelRooms[i])
                    layoutStr += $" {rt}";
                layoutStr += "] → ";
            }
            Debug.Log($"<color=cyan>房间布局：{layoutStr}</color>");
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
                // 当前层所有房间通关，进入下一层
                _currentLevel++;
                _currentRoomInLevel = 0;

                if (_currentLevel >= _realmNames.Length)
                {
                    // 通关！
                    Debug.Log("<color=yellow>✨✨✨ 渡劫成功！飞升成仙！✨✨✨</color>");
                    _gameOver = true;
                    GameEvents.Publish(new GameEvents.GameWon());
                    return;
                }

                Debug.Log($"<color=magenta>═══ 进入下一层：{CurrentRealmName} ═══</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>进入下一个房间：{CurrentRealmName} 房间 {_currentRoomInLevel + 1}/{_levelRooms[_currentLevel].Count}</color>");
            }

            // 在房间北侧生成传送门
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

            return 10f;
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            _gameOver = true;
            Debug.Log("<color=red>梦境破碎... 惊醒回到现实</color>");
        }

        /// <summary>敌人被击杀时奖励灵力碎片</summary>
        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (PlayerResources.Instance == null) return;

            // 基础奖励：每个敌人给2-5碎片，随层数增加
            int baseShards = Random.Range(2, 6);
            int levelBonus = _currentLevel; // 每层+1
            int totalShards = baseShards + levelBonus;

            PlayerResources.Instance.AddShards(totalShards);
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
