using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 战斗房间管理器
    /// Demo1: 简单的波次战斗，清完敌人后掉落灵物并开门
    /// </summary>
    public class BattleRoom : MonoBehaviour
    {
        [Header("房间参数")]
        [SerializeField] private float roomWidth = 35f;
        [SerializeField] private float roomDepth = 35f;
        [SerializeField] private int enemyCount = 5;
        [SerializeField] private float spawnRadius = 8f;

        [Header("掉落奖励")]
        [SerializeField] private SkillData[] skillRewardPool;

        [Header("模块掉落")]
        [SerializeField] private ModuleDef[] moduleRewardPool;

        [Header("难度缩放")]
        [SerializeField] private float hpMultiplier = 1f;
        [SerializeField] private float dmgMultiplier = 1f;

        private enum EnemyArchetype { Melee, Ranged, Charger, Mage, Elite }

        private readonly HashSet<GameObject> _encounterEnemies = new();
        private readonly Queue<EnemyArchetype> _pendingEnemies = new();
        private readonly List<Transform> _enemySpawnSockets = new();
        private readonly List<Transform> _bossSpawnSockets = new();
        private int _totalEnemyCount;
        private bool _cleared;
        private bool _spawningNextWave;
        private int _roomIndex;
        private GameObject _enemyHitVFXPrefab;
        private GameObject _roomVisuals;
        private bool _isEliteRoom;
        private bool _buildRoomGeometry = true;

        public bool IsCleared => _cleared;
        public float RoomWidth => roomWidth;
        public float RoomDepth => roomDepth;

        /// <summary>
        /// 初始化房间
        /// </summary>
        public void Initialize(int roomIndex, int enemyCount, float hpMul, float dmgMul,
            float width = 35f, float depth = 35f, bool buildRoomGeometry = true,
            Transform contentRoot = null)
        {
            _roomIndex = roomIndex;
            this.enemyCount = enemyCount;
            hpMultiplier = hpMul;
            dmgMultiplier = dmgMul;
            roomWidth = width;
            roomDepth = depth;
            _buildRoomGeometry = buildRoomGeometry;
            CollectContentSockets(contentRoot);

            // 根据房间大小调整生成半径（留出墙边距）
            spawnRadius = Mathf.Min(width, depth) / 2f - 4f;

            // Edgar Grid3D 已提供实体房间时，不再叠加旧 RoomBuilder 几何。
            if (_buildRoomGeometry)
                BuildRoom();
        }

        /// <summary>构建房间的地面、墙壁、障碍物</summary>
        private void BuildRoom()
        {
            if (_roomVisuals != null)
                Destroy(_roomVisuals);

            _roomVisuals = RoomBuilder.Build(transform, roomWidth, roomDepth, _roomIndex);
        }

        /// <summary>
        /// 开始战斗（生成多种类型敌人）
        /// </summary>
        public void StartBattle()
        {
            _cleared = false;
            _spawningNextWave = false;
            _encounterEnemies.Clear();
            _pendingEnemies.Clear();

            // 依据区域内房间深度组装敌群；具体落点由 Edgar 内容插槽决定。
            int rangedCount = 0;
            int chargerCount = 0;
            int mageCount = 0;
            int normalCount = enemyCount;

            if (_roomIndex >= 1)
            {
                // 第2层开始出现远程
                rangedCount = Mathf.Min(1 + _roomIndex / 2, enemyCount / 3);
                normalCount -= rangedCount;
            }
            if (_roomIndex >= 2)
            {
                // 第3层开始出现冲锋
                chargerCount = Mathf.Min(1, normalCount / 2);
                normalCount -= chargerCount;
            }
            if (_roomIndex >= 3)
            {
                // 第4层开始出现法师
                mageCount = Mathf.Min(1, normalCount / 2);
                normalCount -= mageCount;
            }

            for (int i = 0; i < normalCount; i++)
                _pendingEnemies.Enqueue(EnemyArchetype.Melee);
            for (int i = 0; i < rangedCount; i++)
                _pendingEnemies.Enqueue(EnemyArchetype.Ranged);
            for (int i = 0; i < chargerCount; i++)
                _pendingEnemies.Enqueue(EnemyArchetype.Charger);
            for (int i = 0; i < mageCount; i++)
                _pendingEnemies.Enqueue(EnemyArchetype.Mage);

            // Edgar 模板自行承载场景内容，避免旧陷阱/可破坏物与实体模板重叠。
            if (_buildRoomGeometry)
            {
                int trapCount = Mathf.Min(_roomIndex, 3);
                RoomBuilder.BuildTraps(transform, roomWidth, roomDepth, trapCount);

                var config2 = GameConfig.Instance;
                int destructibleCount = config2 != null ? config2.可破坏物数量 : 3;
                for (int i = 0; i < destructibleCount; i++)
                {
                    Vector3 spawnPos = GetRandomSpawnPosition();
                    spawnPos.y = 0;
                    Destructible.Spawn(spawnPos);
                }
            }

            // 精英房保底精英；普通房仍沿用配置概率。
            var eliteConfig = GameConfig.Instance;
            bool spawnElite = _isEliteRoom;
            if (!spawnElite
                && eliteConfig != null
                && _roomIndex >= eliteConfig.精英怪最低层数)
            {
                spawnElite = Random.value < eliteConfig.精英怪出现概率;
            }
            if (spawnElite)
                _pendingEnemies.Enqueue(EnemyArchetype.Elite);

            // 监听敌人死亡
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            _totalEnemyCount = _pendingEnemies.Count;
            SpawnNextWave();

            Debug.Log($"<color=orange>房间 {_roomIndex + 1} 战斗开始！敌人总数：{_totalEnemyCount}</color>");

            // 通知UI初始敌人计数
            PublishEnemyCount();
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (evt.Enemy == null || !_encounterEnemies.Remove(evt.Enemy))
                return;

            PublishEnemyCount();

            if (_encounterEnemies.Count == 0 && _pendingEnemies.Count > 0)
            {
                if (!_spawningNextWave)
                    StartCoroutine(SpawnNextWaveDelayed());
            }
            else if (_encounterEnemies.Count == 0 && !_cleared)
                OnRoomCleared();
        }

        /// <summary>登记由房间工厂额外生成的敌人（例如 Boss）。</summary>
        public void RegisterEnemy(GameObject enemy)
        {
            if (enemy == null || !_encounterEnemies.Add(enemy))
                return;

            _totalEnemyCount++;
            PublishEnemyCount();
        }

        public Vector3 GetBossSpawnPosition()
        {
            if (_bossSpawnSockets.Count > 0)
                return _bossSpawnSockets[0].position;

            if (!_buildRoomGeometry)
                throw new System.InvalidOperationException(
                    $"Edgar 战斗房 {_roomIndex} 缺少 {DungeonContentSocketType.BossSpawn} 插槽。");

            return transform.position + new Vector3(0f, 0f, 8f);
        }

        private IEnumerator SpawnNextWaveDelayed()
        {
            _spawningNextWave = true;
            yield return new WaitForSeconds(0.75f);
            SpawnNextWave();
            _spawningNextWave = false;
        }

        private void SpawnNextWave()
        {
            if (_pendingEnemies.Count == 0)
                return;

            var positions = GetAvailableSpawnPositions();
            int waveSize = Mathf.Min(_pendingEnemies.Count, positions.Count);
            for (int i = 0; i < waveSize; i++)
                SpawnEnemy(_pendingEnemies.Dequeue(), positions[i]);

            PublishEnemyCount();
        }

        private List<Vector3> GetAvailableSpawnPositions()
        {
            var positions = new List<Vector3>(_enemySpawnSockets.Count);
            var player = PlayerController.Instance;

            for (int i = 0; i < _enemySpawnSockets.Count; i++)
            {
                var socket = _enemySpawnSockets[i];
                if (socket == null) continue;
                if (player != null && Vector3.Distance(socket.position, player.transform.position) < 5f)
                    continue;
                positions.Add(socket.position);
            }

            // 如果所有点都靠近入口，仍使用全部插槽，避免战斗无法开始。
            if (positions.Count == 0)
            {
                for (int i = 0; i < _enemySpawnSockets.Count; i++)
                    if (_enemySpawnSockets[i] != null)
                        positions.Add(_enemySpawnSockets[i].position);
            }

            if (positions.Count == 0)
            {
                if (!_buildRoomGeometry)
                    throw new System.InvalidOperationException(
                        $"Edgar 战斗房 {_roomIndex} 缺少 {DungeonContentSocketType.EnemySpawn} 插槽。");
                positions.Add(GetRandomSpawnPosition());
            }

            // 每波洗牌，避免敌群总从固定方向出现。
            for (int i = positions.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (positions[i], positions[swapIndex]) = (positions[swapIndex], positions[i]);
            }

            return positions;
        }

        private void SpawnEnemy(EnemyArchetype archetype, Vector3 position)
        {
            GameObject spawned;
            switch (archetype)
            {
                case EnemyArchetype.Ranged:
                {
                    var enemy = EnemyRanged.Spawn(position, hpMultiplier, dmgMultiplier);
                    if (skillRewardPool != null) enemy.SetSkillDrops(skillRewardPool);
                    spawned = enemy.gameObject;
                    break;
                }
                case EnemyArchetype.Charger:
                {
                    var enemy = EnemyCharger.Spawn(position, hpMultiplier, dmgMultiplier);
                    if (skillRewardPool != null) enemy.SetSkillDrops(skillRewardPool);
                    spawned = enemy.gameObject;
                    break;
                }
                case EnemyArchetype.Mage:
                {
                    var enemy = EnemyMage.Spawn(position, hpMultiplier, dmgMultiplier);
                    if (skillRewardPool != null) enemy.SetSkillDrops(skillRewardPool);
                    spawned = enemy.gameObject;
                    break;
                }
                case EnemyArchetype.Elite:
                {
                    spawned = EnemyElite.Spawn(
                        position, hpMultiplier, dmgMultiplier, skillRewardPool).gameObject;
                    break;
                }
                default:
                {
                    var enemy = EnemyBase.Spawn(position, hpMultiplier, dmgMultiplier);
                    if (_enemyHitVFXPrefab != null) enemy.SetHitVFXPrefab(_enemyHitVFXPrefab);
                    if (skillRewardPool != null) enemy.SetSkillDrops(skillRewardPool);
                    spawned = enemy.gameObject;
                    break;
                }
            }

            _encounterEnemies.Add(spawned);
        }

        private void PublishEnemyCount()
        {
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = _encounterEnemies.Count + _pendingEnemies.Count,
                TotalCount = _totalEnemyCount
            });
        }

        private void CollectContentSockets(Transform contentRoot)
        {
            _enemySpawnSockets.Clear();
            _bossSpawnSockets.Clear();
            if (contentRoot == null) return;

            var sockets = contentRoot.GetComponentsInChildren<DungeonContentSocket>(true);
            for (int i = 0; i < sockets.Length; i++)
            {
                switch (sockets[i].SocketType)
                {
                    case DungeonContentSocketType.EnemySpawn:
                        _enemySpawnSockets.Add(sockets[i].transform);
                        break;
                    case DungeonContentSocketType.BossSpawn:
                        _bossSpawnSockets.Add(sockets[i].transform);
                        break;
                }
            }
        }

        private void OnRoomCleared()
        {
            _cleared = true;
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            Debug.Log($"<color=green>房间清理完成！</color>");

            // 当前奖励方案统一为世界掉落；过关后不再弹三选一。
            SpawnSkillReward();
            SpawnModuleReward();

            GameEvents.Publish(new GameEvents.RoomCleared
            {
                RoomIndex = _roomIndex,
                IsElite = _isEliteRoom,
                IsCombatRoom = true
            });
        }

        /// <summary>通关后掉落功法奖励</summary>
        private void SpawnSkillReward()
        {
            if (skillRewardPool == null || skillRewardPool.Length == 0) return;

            var config = GameConfig.Instance;
            if (config == null) return;

            // 功法掉落概率判定
            float chance = config.debugMaxSkillDropRate ? 1f : config.通关功法掉落概率;
            if (Random.value > chance) return;

            // 从池中随机选一个功法（化身门控已随化身系统移除，任何角色都能拾取）
            var skill = SkillPickup.PickValid(skillRewardPool);
            if (skill != null)
            {
                Vector3 playerPos = PlayerController.Instance != null
                    ? PlayerController.Instance.transform.position
                    : transform.position;
                Vector3 pos = playerPos + new Vector3(
                    Random.Range(-2f, 2f), 0, Random.Range(1f, 3f));
                SkillPickup.Spawn(skill, pos);
                Debug.Log($"<color=cyan>功法掉落：{skill.skillName}</color>");
            }
        }

        /// <summary>获取房间内的随机生成位置（避开中心安全区）</summary>
        private Vector3 GetRandomSpawnPosition()
        {
            float safeRadius = 5f; // 玩家出生点附近的安全区
            float margin = 3f;     // 墙壁边距
            float halfW = roomWidth / 2f - margin;
            float halfD = roomDepth / 2f - margin;

            Vector3 pos;
            int attempts = 0;
            do
            {
                pos = transform.position + new Vector3(
                    Random.Range(-halfW, halfW),
                    0,
                    Random.Range(-halfD, halfD));
                attempts++;
            } while (Vector3.Distance(pos, transform.position) < safeRadius && attempts < 30);

            return pos;
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            if (_roomVisuals != null)
                Destroy(_roomVisuals);
        }

        /// <summary>设置功法掉落池</summary>
        public void SetSkillPool(SkillData[] skills)
        {
            skillRewardPool = skills;
        }

        public void SetModulePool(ModuleDef[] modules)
        {
            moduleRewardPool = modules;
        }

        /// <summary>V0.2.1：标记此房间为精英房（掉落数量 +1，且稀有度偏移 +20）</summary>
        public void SetEliteRoom(bool isElite)
        {
            _isEliteRoom = isElite;
        }

        private void SpawnModuleReward()
        {
            if (moduleRewardPool == null || moduleRewardPool.Length == 0) return;

            var config = GameConfig.Instance;
            if (config != null && !_isEliteRoom)
            {
                if (Random.value > config.模块掉落概率) return;
            }

            int dropCount;
            if (_isEliteRoom)
            {
                dropCount = config != null ? config.精英房模块掉落数量 : 3;
            }
            else
            {
                int min = config != null ? config.模块掉落数量最少 : 1;
                int max = config != null ? config.模块掉落数量最多 : 2;
                dropCount = Random.Range(min, max + 1);
            }
            int rarityBias = GetFloorRarityBias() + (_isEliteRoom ? 20 : 0);

            var player = PlayerController.Instance;
            Vector3 basePos = player != null ? player.transform.position : transform.position;

            for (int i = 0; i < dropCount; i++)
            {
                var module = ModuleDropWeighting.PickWeighted(moduleRewardPool, rarityBias);
                if (module == null) continue;
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                ModulePickup.Spawn(module, basePos + offset + Vector3.right * (i * 1.5f));
            }
        }

        private static int GetFloorRarityBias()
        {
            // V0.4.2 解耦：统一走 IMapProvider，不再直接触碰 LevelDesignDirector / ConfigDatabase。
            int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0;
            return MapProviders.Current.GetRarityBias(currentLevel);
        }

        /// <summary>设置敌人受击特效</summary>
        public void SetEnemyHitVFX(GameObject prefab)
        {
            _enemyHitVFXPrefab = prefab;
        }

        /// <summary>
        /// 在 Scene 视图中绘制房间范围
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(roomWidth, 4f, roomDepth));
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}
