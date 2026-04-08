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
        [SerializeField] private Vector3 roomSize = new(20f, 0, 20f);
        [SerializeField] private int enemyCount = 5;
        [SerializeField] private float spawnRadius = 8f;

        [Header("掉落奖励")]
        [SerializeField] private ItemData[] rewardPool;
        [SerializeField] private int rewardCount = 1;

        [Header("难度缩放")]
        [SerializeField] private float hpMultiplier = 1f;
        [SerializeField] private float dmgMultiplier = 1f;

        private List<EnemyBase> _enemies = new();
        private bool _cleared;
        private int _roomIndex;
        private GameObject _enemyHitVFXPrefab;

        public bool IsCleared => _cleared;

        /// <summary>
        /// 初始化房间
        /// </summary>
        public void Initialize(int roomIndex, int enemyCount, float hpMul, float dmgMul, ItemData[] rewards)
        {
            _roomIndex = roomIndex;
            this.enemyCount = enemyCount;
            hpMultiplier = hpMul;
            dmgMultiplier = dmgMul;
            rewardPool = rewards;
        }

        /// <summary>
        /// 开始战斗（生成敌人）
        /// </summary>
        public void StartBattle()
        {
            _cleared = false;
            _enemies.Clear();

            for (int i = 0; i < enemyCount; i++)
            {
                Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
                spawnPos.y = 0;

                var enemy = EnemyBase.Spawn(spawnPos, hpMultiplier, dmgMultiplier);

                // 设置受击特效
                if (_enemyHitVFXPrefab != null)
                    enemy.SetHitVFXPrefab(_enemyHitVFXPrefab);

                _enemies.Add(enemy);
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
            // 移除已死亡的敌人
            _enemies.RemoveAll(e => e == null || e.gameObject == evt.Enemy);

            // 通知UI更新敌人计数
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = _enemies.Count,
                TotalCount = enemyCount
            });

            if (_enemies.Count == 0 && !_cleared)
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

            for (int i = 0; i < rewardCount; i++)
            {
                var item = rewardPool[Random.Range(0, rewardPool.Length)];
                if (item != null)
                {
                    Vector3 pos = transform.position + new Vector3(
                        Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
                    ItemPickup.Spawn(item, pos);
                }
            }
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
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
            Gizmos.DrawWireCube(transform.position, roomSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}
