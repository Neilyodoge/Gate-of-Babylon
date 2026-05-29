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
        [SerializeField] private ItemData[] rewardPool;
        [SerializeField] private SkillData[] skillRewardPool;
        [SerializeField] private int rewardCount = 1;

        [Header("难度缩放")]
        [SerializeField] private float hpMultiplier = 1f;
        [SerializeField] private float dmgMultiplier = 1f;

        private List<EnemyBase> _enemies = new();
        private int _totalEnemyCount; // 包含所有类型敌人的总数
        private bool _cleared;
        private int _roomIndex;
        private GameObject _enemyHitVFXPrefab;
        private GameObject _roomVisuals;

        public bool IsCleared => _cleared;
        public float RoomWidth => roomWidth;
        public float RoomDepth => roomDepth;

        /// <summary>
        /// 初始化房间
        /// </summary>
        public void Initialize(int roomIndex, int enemyCount, float hpMul, float dmgMul, ItemData[] rewards,
            float width = 35f, float depth = 35f)
        {
            _roomIndex = roomIndex;
            this.enemyCount = enemyCount;

            // v0.5.4：灵气浓度系统移除，敌人难度直接用关卡层数 scaling
            hpMultiplier = hpMul;
            dmgMultiplier = dmgMul;
            rewardPool = rewards;
            roomWidth = width;
            roomDepth = depth;

            // 根据房间大小调整生成半径（留出墙边距）
            spawnRadius = Mathf.Min(width, depth) / 2f - 4f;

            // 构建房间视觉和碰撞体
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
            _enemies.Clear();
            _totalEnemyCount = enemyCount;

            // 根据层数决定敌人类型分配
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

            // 生成普通近战敌人
            for (int i = 0; i < normalCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                var enemy = EnemyBase.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
                if (_enemyHitVFXPrefab != null) enemy.SetHitVFXPrefab(_enemyHitVFXPrefab);
                if (skillRewardPool != null) enemy.SetSkillDrops(skillRewardPool);
                _enemies.Add(enemy);
            }

            // 生成远程弓箍手
            for (int i = 0; i < rangedCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                var ranged = EnemyRanged.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
                if (skillRewardPool != null) ranged.SetSkillDrops(skillRewardPool);
            }

            // 生成冲锋型
            for (int i = 0; i < chargerCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                var charger = EnemyCharger.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
                if (skillRewardPool != null) charger.SetSkillDrops(skillRewardPool);
            }

            // 生成AOE法师
            for (int i = 0; i < mageCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                var mage = EnemyMage.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
                if (skillRewardPool != null) mage.SetSkillDrops(skillRewardPool);
            }
            // 生成陷阱
            int trapCount = Mathf.Min(_roomIndex, 3);
            RoomBuilder.BuildTraps(transform, roomWidth, roomDepth, trapCount);

            // 生成可破坏物
            var config2 = GameConfig.Instance;
            int destructibleCount = config2 != null ? config2.可破坏物数量 : 3;
            for (int i = 0; i < destructibleCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                Destructible.Spawn(spawnPos, rewardPool);
            }

            // 生成精英怪（满足层数条件且概率判定通过）
            var eliteConfig = GameConfig.Instance;
            if (eliteConfig != null && _roomIndex >= eliteConfig.精英怪最低层数)
            {
                if (Random.value < eliteConfig.精英怪出现概率)
                {
                    Vector3 elitePos = GetRandomSpawnPosition();
                    elitePos.y = 0;
                    var elite = EnemyElite.Spawn(elitePos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool, skillRewardPool);
                    _totalEnemyCount++; // 精英怪额外计入总数
                }
            }

            // 监听敌人死亡
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            Debug.Log($"<color=orange>第 {_roomIndex + 1} 层开始！敌人数量：{enemyCount}</color>");

            // 通知UI初始敌人计数
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = enemyCount,
                TotalCount = enemyCount
            });
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            // 移除已死亡的敌人（EnemyBase列表）
            _enemies.RemoveAll(e => e == null || e.gameObject == evt.Enemy);

            // 统计场景中所有存活的Enemy标签对象
            var allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
            int remaining = 0;
            foreach (var e in allEnemies)
            {
                if (e != null && e != evt.Enemy) remaining++;
            }

            // 通知UI更新敌人计数
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = remaining,
                TotalCount = _totalEnemyCount
            });

            if (remaining == 0 && !_cleared)
            {
                OnRoomCleared();
            }
        }

        private void OnRoomCleared()
        {
            _cleared = true;
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            Debug.Log($"<color=green>房间清理完成！</color>");

            // 掉落奖励灵物
            SpawnRewards();

            // 掉落奖励功法
            SpawnSkillReward();

            // 发布事件
            GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
        }

        private void SpawnRewards()
        {
            if (rewardPool == null || rewardPool.Length == 0) return;

            // 过滤 null 元素
            var validPool = new System.Collections.Generic.List<ItemData>();
            foreach (var d in rewardPool) if (d != null) validPool.Add(d);
            if (validPool.Count == 0) return;

            // 通关额外奖励：先判定概率，再掉落
            var config = GameConfig.Instance;
            int count = config != null ? config.通关额外掉落数 : rewardCount;

            // 通关掉落概率判定（debug爆率拉满时跳过判定）
            if (config != null && !config.debugMaxItemDropRate)
            {
                if (Random.value > config.通关掉落概率) return; // 未通过概率判定，不掉落
            }

            for (int i = 0; i < count; i++)
            {
                ItemData item;
                if (config != null)
                {
                    // 按品阶权重选择（层数越高，高品质比重越大）
                    ItemRarity targetRarity = config.RollRarity(_roomIndex);
                    var candidates = new System.Collections.Generic.List<ItemData>();
                    foreach (var d in validPool)
                    {
                        if (d.rarity == targetRarity)
                            candidates.Add(d);
                    }
                    item = candidates.Count > 0
                        ? candidates[Random.Range(0, candidates.Count)]
                        : validPool[Random.Range(0, validPool.Count)];
                }
                else
                {
                    item = validPool[Random.Range(0, validPool.Count)];
                }

                if (item != null)
                {
                    // 在玩家附近掉落（而非房间中心）
                    Vector3 playerPos = PlayerController.Instance != null
                        ? PlayerController.Instance.transform.position
                        : transform.position;
                    Vector3 pos = playerPos + new Vector3(
                        Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                    ItemPickup.Spawn(item, pos);
                }
            }
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

            // 随机选择一个功法
            var skill = skillRewardPool[Random.Range(0, skillRewardPool.Length)];
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
