using System.Collections;
using System.Collections.Generic;
using Edgar.Unity;
using UnityEngine;
using XianTu.LevelDesign;

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
        private readonly Queue<List<EnemyArchetype>> _pendingWaves = new();
        private readonly List<Transform> _enemySpawnSockets = new();
        private readonly List<Transform> _bossSpawnSockets = new();
        private readonly List<DungeonEnemySpawnArea> _enemySpawnAreas = new();
        private readonly List<DoorHandlerGrid3D> _doorHandlers = new();
        private readonly Dictionary<DungeonEnemySpawnArea, int> _spawnAreaUseCounts = new();
        private readonly List<GameObject> _dormantEnemies = new();
        private int _totalEnemyCount;
        private bool _cleared;
        private bool _spawningNextWave;
        private int _roomIndex;
        private GameObject _enemyHitVFXPrefab;
        private GameObject _roomVisuals;
        private bool _isEliteRoom;
        private bool _buildRoomGeometry = true;
        private EncounterRow _encounter;
        private int _encounterSeed;
        private int _pendingEnemyCount;
        private int _activeWaveSize;
        private bool _encounterPrepared;
        private District _district;
        private int _reinforceAtPct;
        private float _reinforceDelaySec;
        private System.Random _spawnRandom;
        private Transform _contentRoot;

        public bool IsCleared => _cleared;
        public float RoomWidth => roomWidth;
        public float RoomDepth => roomDepth;
        public float HpMultiplier => hpMultiplier;
        public float DmgMultiplier => dmgMultiplier;
        public event System.Action Cleared;

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
            if (_encounter == null)
                throw new System.InvalidOperationException(
                    $"战斗房缺少 Encounter 配置：Room={_roomIndex}, Seed={_encounterSeed}。");
            if (!_encounterPrepared)
                PrepareEncounter();

            _cleared = false;
            _spawningNextWave = false;
            bool hasDormantEnemies = _encounter.SpawnModeEnum == SpawnMode.PreplacedDormant
                                    && _dormantEnemies.Count > 0;
            if (!hasDormantEnemies)
                _encounterEnemies.Clear();

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

            // 监听敌人死亡
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            _totalEnemyCount = _pendingEnemyCount;
            if (hasDormantEnemies)
            {
                _totalEnemyCount = _encounterEnemies.Count;
                foreach (var enemy in _dormantEnemies)
                    if (enemy != null)
                        enemy.SetActive(true);
                _dormantEnemies.Clear();
            }
            else if (_encounter.SpawnModeEnum == SpawnMode.AmbushSpawn)
                StartCoroutine(SpawnAmbushDelayed());
            else if (_encounter.SpawnModeEnum != SpawnMode.ScriptedBoss)
                SpawnNextWave();

            Debug.Log(
                $"<color=orange>房间 {_roomIndex + 1} 战斗开始！Encounter={_encounter.ID} " +
                $"SpawnMode={_encounter.SpawnModeEnum} Seed={_encounterSeed} 敌人总数={_totalEnemyCount}</color>");

            // 通知UI初始敌人计数
            PublishEnemyCount();
        }

        public void ConfigureEncounter(EncounterRow encounter, int seed, District district)
        {
            _encounter = encounter ?? throw new System.ArgumentNullException(nameof(encounter));
            _encounterSeed = seed;
            _district = district;
            _reinforceAtPct = encounter.ReinforceAtPct;
            _reinforceDelaySec = encounter.ReinforceDelaySec;
            _spawnRandom = new System.Random(unchecked(seed * 397) ^ 0x4f1bbcdc);
            _encounterPrepared = false;
        }

        public void PrepareEncounter()
        {
            if (_encounter == null)
                throw new System.InvalidOperationException(
                    $"无法预备战斗：Room={_roomIndex}, Seed={_encounterSeed} 缺少 Encounter。");

            _pendingWaves.Clear();
            _pendingEnemyCount = 0;
            if (_encounter.SpawnModeEnum == SpawnMode.ScriptedBoss)
            {
                _encounterPrepared = true;
                return;
            }

            var authoringConfig = DungeonLevelAuthoringConfig.Instance;
            if (authoringConfig == null)
                throw new System.InvalidOperationException(
                    $"缺少关卡生成配置，Room={_roomIndex}，District={_district}，Seed={_encounterSeed}。");
            BuildAutomaticWaves(authoringConfig.BuildPopulationPlan(_district, _encounterSeed));
        }

        private void BuildAutomaticWaves(EnemyPopulationPlan plan)
        {
            if (plan == null || plan.Waves.Count == 0 || plan.TotalCount == 0)
                throw new System.InvalidOperationException(
                    $"自动小怪方案为空，Room={_roomIndex}，District={_district}，Seed={_encounterSeed}。");

            _reinforceAtPct = plan.ReinforceAtPct;
            _reinforceDelaySec = plan.ReinforceDelaySec;
            List<EnemyArchetype> firstWave = null;
            foreach (var sourceWave in plan.Waves)
            {
                var wave = new List<EnemyArchetype>(sourceWave.Count);
                foreach (var enemy in sourceWave)
                    wave.Add(enemy switch
                    {
                        EnemySpawnKind.Melee => EnemyArchetype.Melee,
                        EnemySpawnKind.Ranged => EnemyArchetype.Ranged,
                        EnemySpawnKind.Charger => EnemyArchetype.Charger,
                        EnemySpawnKind.Mage => EnemyArchetype.Mage,
                        _ => EnemyArchetype.Melee
                    });
                _pendingWaves.Enqueue(wave);
                firstWave ??= wave;
                _pendingEnemyCount += wave.Count;
            }

            if (_isEliteRoom && firstWave != null)
            {
                firstWave.Insert(0, EnemyArchetype.Elite);
                _pendingEnemyCount++;
            }

            enemyCount = _pendingEnemyCount;
            _encounterPrepared = true;
        }

        public void PreSpawnDormant()
        {
            if (_encounter == null
                || _encounter.SpawnModeEnum != SpawnMode.PreplacedDormant)
                return;
            if (!_encounterPrepared)
                PrepareEncounter();

            _encounterEnemies.Clear();
            SpawnNextWave();
            _dormantEnemies.Clear();
            foreach (var enemy in _encounterEnemies)
            {
                if (enemy == null) continue;
                enemy.SetActive(false);
                _dormantEnemies.Add(enemy);
            }
        }

        private IEnumerator SpawnAmbushDelayed()
        {
            Debug.Log(
                $"<color=#ff8844>[Ambush] 房间 {_roomIndex} 伏击预警，Encounter={_encounter.ID}。</color>");
            yield return new WaitForSeconds(Mathf.Max(0.1f, _reinforceDelaySec));
            SpawnNextWave();
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (evt.Enemy == null || !_encounterEnemies.Remove(evt.Enemy))
                return;

            PublishEnemyCount();

            bool reinforcementReady = _encounterEnemies.Count == 0;
            if (!reinforcementReady
                && _reinforceAtPct > 0
                && _activeWaveSize > 0)
            {
                reinforcementReady = _encounterEnemies.Count * 100
                                     <= _activeWaveSize * _reinforceAtPct;
            }

            if (reinforcementReady && _pendingWaves.Count > 0)
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
            {
                Vector3 candidate = _bossSpawnSockets[0].position;
                if (DungeonSpawnSafety.TryFindGroundedPoint(
                        _contentRoot,
                        candidate,
                        0.7f,
                        2.2f,
                        0.1f,
                        out Vector3 grounded))
                    return grounded;

                throw new System.InvalidOperationException(
                    $"Edgar Boss 房 {_roomIndex} 的 BossSpawn 下方没有安全地板。");
            }

            if (!_buildRoomGeometry)
                throw new System.InvalidOperationException(
                    $"Edgar 战斗房 {_roomIndex} 缺少 {DungeonContentSocketType.BossSpawn} 插槽。");

            return transform.position + new Vector3(0f, 0f, 8f);
        }

        private IEnumerator SpawnNextWaveDelayed()
        {
            _spawningNextWave = true;
            yield return new WaitForSeconds(_reinforceDelaySec);
            SpawnNextWave();
            _spawningNextWave = false;
        }

        private void SpawnNextWave()
        {
            if (_pendingWaves.Count == 0)
                return;

            var wave = _pendingWaves.Dequeue();
            int waveSize = wave.Count;
            _activeWaveSize = waveSize;
            _spawnAreaUseCounts.Clear();
            var usedPositions = new List<Vector3>(waveSize);
            for (int i = 0; i < waveSize; i++)
            {
                Vector3 position = GetSpawnPosition(wave[i], usedPositions);
                usedPositions.Add(position);
                SpawnEnemy(wave[i], position);
            }
            _pendingEnemyCount -= waveSize;

            PublishEnemyCount();
        }

        private Vector3 GetSpawnPosition(
            EnemyArchetype archetype,
            IReadOnlyList<Vector3> usedPositions)
        {
            EnemyCombatCategory category = archetype switch
            {
                EnemyArchetype.Ranged => EnemyCombatCategory.Ranged,
                EnemyArchetype.Mage => EnemyCombatCategory.Magic,
                _ => EnemyCombatCategory.Melee
            };
            var player = PlayerController.Instance;

            var candidates = new List<DungeonEnemySpawnArea>();
            foreach (var area in _enemySpawnAreas)
            {
                if (area == null || area.AreaCollider == null || !area.Allows(category))
                    continue;
                _spawnAreaUseCounts.TryGetValue(area, out int used);
                if (area.MaxSpawnCount > 0 && used >= area.MaxSpawnCount)
                    continue;
                candidates.Add(area);
            }

            for (int attempt = 0; attempt < 24 && candidates.Count > 0; attempt++)
            {
                var area = RollSpawnArea(candidates);
                if (!area.TryGetRandomPoint(_spawnRandom, out Vector3 point))
                {
                    candidates.Remove(area);
                    continue;
                }
                if (player != null
                    && Vector3.Distance(point, player.transform.position)
                    < Mathf.Max(5f, area.MinPlayerDistance))
                    continue;
                if (IsInsideDoorClearance(point))
                    continue;

                bool overlaps = false;
                for (int i = 0; i < usedPositions.Count; i++)
                {
                    if (Vector3.Distance(point, usedPositions[i]) < area.MinSeparation)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) continue;

                _spawnAreaUseCounts.TryGetValue(area, out int used);
                _spawnAreaUseCounts[area] = used + 1;
                return point;
            }

            var socketPositions = new List<Vector3>(_enemySpawnSockets.Count);
            foreach (var socket in _enemySpawnSockets)
            {
                if (socket == null) continue;
                if (_contentRoot == null
                    || !DungeonSpawnSafety.TryFindGroundedPoint(
                        _contentRoot,
                        socket.position,
                        0.45f,
                        1.8f,
                        0.1f,
                        out Vector3 grounded))
                    continue;
                if (player != null
                    && Vector3.Distance(grounded, player.transform.position) < 5f)
                    continue;
                if (IsInsideDoorClearance(grounded))
                    continue;
                socketPositions.Add(grounded);
            }

            if (socketPositions.Count > 0)
                return socketPositions[_spawnRandom.Next(socketPositions.Count)];
            if (_contentRoot != null)
            {
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    if (!DungeonSpawnSafety.TryFindRandomGroundedPoint(
                            _contentRoot,
                            _spawnRandom,
                            0.45f,
                            1.8f,
                            0.1f,
                            out Vector3 fallback))
                        break;
                    if (player != null
                        && Vector3.Distance(fallback, player.transform.position) < 5f)
                        continue;
                    if (IsInsideDoorClearance(fallback))
                        continue;

                    bool overlaps = false;
                    for (int i = 0; i < usedPositions.Count; i++)
                    {
                        if (Vector3.Distance(fallback, usedPositions[i]) < 1.5f)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                    if (!overlaps)
                        return fallback;
                }
            }
            if (_buildRoomGeometry)
                return GetRandomSpawnPosition();

            throw new System.InvalidOperationException(
                $"Edgar 战斗房 {_roomIndex} 没有满足地板、胶囊净空、玩家距离、" +
                $"怪物间距与门道避让的刷新点。");
        }

        private DungeonEnemySpawnArea RollSpawnArea(List<DungeonEnemySpawnArea> candidates)
        {
            int total = 0;
            foreach (var area in candidates)
                total += area.Weight;
            int roll = _spawnRandom.Next(Mathf.Max(1, total));
            foreach (var area in candidates)
            {
                roll -= area.Weight;
                if (roll < 0) return area;
            }
            return candidates[candidates.Count - 1];
        }

        private bool IsInsideDoorClearance(Vector3 point)
        {
            if (_contentRoot == null || _doorHandlers.Count == 0)
                return false;

            foreach (var door in _doorHandlers)
            {
                if (door == null) continue;
                Vector3 inward = _contentRoot.TransformDirection((Vector3)door.DirectionVector);
                inward.y = 0f;
                if (inward.sqrMagnitude < 0.001f) continue;
                inward.Normalize();

                Vector3 delta = point - door.transform.position;
                delta.y = 0f;
                float along = Vector3.Dot(delta, inward);
                if (along < -2f || along > 10f)
                    continue;

                float lateral = Mathf.Abs(Vector3.Dot(
                    delta,
                    new Vector3(-inward.z, 0f, inward.x)));
                float scale = Mathf.Max(
                    Mathf.Abs(_contentRoot.lossyScale.x),
                    Mathf.Abs(_contentRoot.lossyScale.z));
                float cellWidth = door.GeneratorSettings != null
                    ? door.GeneratorSettings.CellSize.x
                    : 1f;
                float halfWidth = door.Width * cellWidth * scale * 0.5f + 1.5f;
                if (lateral <= halfWidth)
                    return true;
            }

            return false;
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
                RemainingCount = _encounterEnemies.Count + _pendingEnemyCount,
                TotalCount = _totalEnemyCount
            });
        }

        private void CollectContentSockets(Transform contentRoot)
        {
            _enemySpawnSockets.Clear();
            _bossSpawnSockets.Clear();
            _enemySpawnAreas.Clear();
            _doorHandlers.Clear();
            _contentRoot = contentRoot;
            if (contentRoot == null) return;

            _enemySpawnAreas.AddRange(
                contentRoot.GetComponentsInChildren<DungeonEnemySpawnArea>(true));
            _doorHandlers.AddRange(
                contentRoot.GetComponentsInChildren<DoorHandlerGrid3D>(true));
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
            Cleared?.Invoke();

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
