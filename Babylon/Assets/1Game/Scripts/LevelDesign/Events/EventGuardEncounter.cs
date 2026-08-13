using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>事件房专用轻量战斗，不发房间结算与掉落，只在全员击败后回调。</summary>
    public sealed class EventGuardEncounter : MonoBehaviour
    {
        private readonly HashSet<GameObject> _enemies = new();
        private Action _onCleared;
        private bool _running;

        public void Begin(
            Transform contentRoot,
            int seed,
            float hpMultiplier,
            float damageMultiplier,
            int meleeCount,
            int rangedCount,
            bool includeElite,
            Action onCleared)
        {
            if (_running)
                return;
            _running = true;
            _onCleared = onCleared;
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);

            int total = Mathf.Max(0, meleeCount)
                        + Mathf.Max(0, rangedCount)
                        + (includeElite ? 1 : 0);
            var positions = ResolveSpawnPositions(contentRoot, seed, total);
            int index = 0;
            for (int i = 0; i < meleeCount; i++)
                Register(EnemyBase.Spawn(positions[index++], hpMultiplier, damageMultiplier).gameObject);
            for (int i = 0; i < rangedCount; i++)
                Register(EnemyRanged.Spawn(positions[index++], hpMultiplier, damageMultiplier).gameObject);
            if (includeElite)
                Register(EnemyElite.Spawn(
                    positions[index],
                    hpMultiplier,
                    damageMultiplier).gameObject);

            Debug.Log($"[事件战斗] 已生成禁卫 {total} 名，Seed={seed}。");
            if (_enemies.Count == 0)
                Complete();
        }

        private void Register(GameObject enemy)
        {
            if (enemy != null)
                _enemies.Add(enemy);
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (!_running || evt.Enemy == null || !_enemies.Remove(evt.Enemy))
                return;
            if (_enemies.Count == 0)
                Complete();
        }

        private void Complete()
        {
            if (!_running)
                return;
            _running = false;
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            var callback = _onCleared;
            _onCleared = null;
            callback?.Invoke();
        }

        private static List<Vector3> ResolveSpawnPositions(
            Transform contentRoot,
            int seed,
            int count)
        {
            var result = new List<Vector3>(count);
            if (contentRoot == null)
                throw new InvalidOperationException("事件战斗缺少实体房根节点。");

            var random = new System.Random(seed);
            var areas = contentRoot.GetComponentsInChildren<DungeonEnemySpawnArea>(true);
            for (int i = 0; i < count; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < 64 && !found; attempt++)
                {
                    Vector3 candidate;
                    if (areas.Length > 0)
                    {
                        var area = areas[random.Next(areas.Length)];
                        if (!area.TryGetRandomPoint(random, out candidate))
                            continue;
                    }
                    else if (!DungeonSpawnSafety.TryFindRandomGroundedPoint(
                                 contentRoot,
                                 random,
                                 0.45f,
                                 1.8f,
                                 0.1f,
                                 out candidate))
                    {
                        continue;
                    }

                    if (PlayerController.Instance != null
                        && Vector3.Distance(
                            candidate,
                            PlayerController.Instance.transform.position) < 5f)
                        continue;

                    bool separated = true;
                    foreach (var existing in result)
                    {
                        if ((existing - candidate).sqrMagnitude < 2.25f)
                        {
                            separated = false;
                            break;
                        }
                    }
                    if (!separated)
                        continue;

                    result.Add(candidate);
                    found = true;
                }

                if (!found)
                    throw new InvalidOperationException(
                        $"事件战斗无法找到第 {i + 1}/{count} 个安全刷新点。");
            }

            return result;
        }

        private void OnDestroy()
        {
            if (_running)
                GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }
    }
}
