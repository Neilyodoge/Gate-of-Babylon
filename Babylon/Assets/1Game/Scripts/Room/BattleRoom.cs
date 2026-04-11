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
                _enemies.Add(enemy);
            }

            // 生成远程弓箭手
            for (int i = 0; i < rangedCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                EnemyRanged.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
            }

            // 生成冲锋型
            for (int i = 0; i < chargerCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                EnemyCharger.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
            }

            // 生成AOE法师
            for (int i = 0; i < mageCount; i++)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                spawnPos.y = 0;
                EnemyMage.Spawn(spawnPos, hpMultiplier, dmgMultiplier, _roomIndex, rewardPool);
            }

            // 生成陷阱
            int trapCount = Mathf.Min(_roomIndex, 3);
            RoomBuilder.BuildTraps(transform, roomWidth, roomDepth, trapCount);

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

            // 发布事件
            GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = _roomIndex });
        }

        private void SpawnRewards()
        {
            if (rewardPool == null || rewardPool.Length == 0) return;

            // 通关额外奖励在房间中心附近掉落
            var config = GameConfig.Instance;
            int count = config != null ? config.通关额外掉落数 : rewardCount;

            for (int i = 0; i < count; i++)
            {
                ItemData item;
                if (config != null)
                {
                    // 按品阶权重选择
                    ItemRarity targetRarity = config.RollRarity();
                    var candidates = new List<ItemData>();
                    foreach (var d in rewardPool)
                    {
                        if (d != null && d.rarity == targetRarity)
                            candidates.Add(d);
                    }
                    item = candidates.Count > 0
                        ? candidates[Random.Range(0, candidates.Count)]
                        : rewardPool[Random.Range(0, rewardPool.Length)];
                }
                else
                {
                    item = rewardPool[Random.Range(0, rewardPool.Length)];
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
