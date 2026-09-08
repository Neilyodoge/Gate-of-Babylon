using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XianTu
{
    /// <summary>
    /// 新手训练场两阶段验证战：三个静止机制靶，然后两个近战、一个远程。
    /// 只统计自己生成的敌人，失败重载当前独立场景并按检查点回到训练场。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueTrainingEncounter : MonoBehaviour
    {
        private const float TutorialHpMultiplier = 0.55f;
        private const float TutorialDamageMultiplier = 0.25f;

        private readonly Dictionary<GameObject, int> _enemyIds = new();
        private readonly StarterPrologueTrainingRuntime _runtime = new();
        private bool _resetting;

        public bool IsRunning => _runtime.IsRunning;
        public int Remaining => _runtime.Remaining;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;
            BeginEncounter();
        }

        public bool BeginEncounter()
        {
            StarterPrologueStep step =
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data);
            if (_runtime.CurrentWave !=
                    StarterPrologueTrainingWave.None ||
                step < StarterPrologueStep.AttachmentChosen ||
                step >= StarterPrologueStep.Completed)
            {
                return false;
            }

            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<StarterPrologueEnemySpawnMarker>();
            if (!HasValidRoster(markers))
            {
                Debug.LogError(
                    "[新手验证战] 需要2个近战和1个远程出生标记。");
                return false;
            }

            var targets = new List<GameObject>(
                StarterPrologueTrainingRuntime.RequiredEnemyCount);
            for (int i = 0; i < markers.Length; i++)
            {
                targets.Add(
                    StarterPrologueTrainingDummy.Spawn(
                        markers[i].transform.position,
                        i).gameObject);
            }

            if (!RegisterWave(targets, false))
                return false;

            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            PublishEnemyCount();
            PublishWaveHint(
                "先试试灵宠附着后的变化",
                markers[1].transform.position);
            return true;
        }

        private bool RegisterWave(
            IReadOnlyList<GameObject> enemies,
            bool validationWave)
        {
            var ids = new List<int>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
                ids.Add(enemies[i].GetInstanceID());

            StarterPrologueTrainingStartResult result =
                validationWave
                    ? _runtime.BeginValidation(ids)
                    : _runtime.Begin(ids);
            if (result !=
                StarterPrologueTrainingStartResult.Success)
            {
                foreach (GameObject enemy in enemies)
                    Destroy(enemy);
                return false;
            }

            _enemyIds.Clear();
            foreach (GameObject enemy in enemies)
                _enemyIds[enemy] = enemy.GetInstanceID();
            return true;
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (!_runtime.IsRunning ||
                evt.Enemy == null ||
                !_enemyIds.TryGetValue(
                    evt.Enemy,
                    out int enemyId))
            {
                return;
            }
            _enemyIds.Remove(evt.Enemy);

            StarterPrologueTrainingStep result =
                _runtime.RegisterDefeated(enemyId);
            PublishEnemyCount();
            if (result.Completed)
            {
                if (_runtime.CurrentWave ==
                    StarterPrologueTrainingWave.MechanismTargets)
                {
                    StartCoroutine(BeginValidationWave());
                }
                else if (_runtime.CurrentWave ==
                         StarterPrologueTrainingWave.Completed)
                {
                    CompleteEncounter();
                }
            }
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            if (_runtime.CurrentWave ==
                    StarterPrologueTrainingWave.None ||
                _runtime.CurrentWave ==
                    StarterPrologueTrainingWave.Completed ||
                _resetting)
                return;
            _resetting = true;
            StartCoroutine(ReloadAfterFailure());
        }

        private IEnumerator ReloadAfterFailure()
        {
            Time.timeScale = 1f;
            yield return null;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.path);
        }

        private IEnumerator BeginValidationWave()
        {
            yield return new WaitForSeconds(0.7f);
            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<StarterPrologueEnemySpawnMarker>();
            if (!HasValidRoster(markers))
            {
                Debug.LogError(
                    "[新手验证战] 第二阶段缺少2近战和1远程出生标记。");
                yield break;
            }

            var enemies = new List<GameObject>(
                StarterPrologueTrainingRuntime.RequiredEnemyCount);
            foreach (StarterPrologueEnemySpawnMarker marker in markers)
            {
                GameObject enemy = marker.Role ==
                                   StarterPrologueEnemyRole.Ranged
                    ? EnemyRanged.Spawn(
                        marker.transform.position,
                        TutorialHpMultiplier,
                        TutorialDamageMultiplier).gameObject
                    : EnemyBase.Spawn(
                        marker.transform.position,
                        TutorialHpMultiplier,
                        TutorialDamageMultiplier).gameObject;
                enemy.name = marker.Role ==
                             StarterPrologueEnemyRole.Ranged
                    ? "TutorialEnemy_Ranged"
                    : "TutorialEnemy_Melee";
                enemies.Add(enemy);
            }

            if (!RegisterWave(enemies, true))
                yield break;
            PublishEnemyCount();
            PublishWaveHint(
                "操作验证：2近战 + 1远程",
                markers[1].transform.position);
        }

        private void CompleteEncounter()
        {
            Unsubscribe();
            StarterPrologueAdvanceResult result =
                StarterPrologueProgression.RecordTrialCompleted(
                    SaveSystem.Instance.Data);
            if (result == StarterPrologueAdvanceResult.Success)
                SaveSystem.Instance.Save();
            Debug.Log(
                "<color=#F2B45E>[新手序章] 验证战完成，洞府门已开放。</color>");
        }

        private void PublishEnemyCount()
        {
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = _runtime.Remaining,
                TotalCount =
                    StarterPrologueTrainingRuntime.RequiredEnemyCount
            });
        }

        private static void PublishWaveHint(
            string text,
            Vector3 position)
        {
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = position + Vector3.up * 2.6f,
                Damage = 0f,
                SpecialTag = text
            });
        }

        private static bool HasValidRoster(
            IReadOnlyList<StarterPrologueEnemySpawnMarker> markers)
        {
            if (markers == null ||
                markers.Count !=
                StarterPrologueTrainingRuntime.RequiredEnemyCount)
            {
                return false;
            }

            int melee = 0;
            int ranged = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Role ==
                    StarterPrologueEnemyRole.Ranged)
                {
                    ranged++;
                }
                else
                {
                    melee++;
                }
            }
            return melee == 2 && ranged == 1;
        }

        private void Unsubscribe()
        {
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _runtime.Reset();
            _enemyIds.Clear();
        }
    }
}
