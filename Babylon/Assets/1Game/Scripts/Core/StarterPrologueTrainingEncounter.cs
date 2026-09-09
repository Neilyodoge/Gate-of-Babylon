using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XianTu
{
    /// <summary>
    /// 新手序章两阶段救援战：先击退两只躁动灵宠，再打断一名失控附身者。
    /// 只统计自己生成的目标，失败重载当前独立场景并按检查点回到遭遇区。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueTrainingEncounter : MonoBehaviour
    {
        private const float TutorialHpMultiplier = 0.55f;
        private const float TutorialDamageMultiplier = 0.25f;
        private const float PossessedHpMultiplier = 0.9f;

        private readonly Dictionary<GameObject, int> _enemyIds = new();
        private readonly StarterPrologueTrainingRuntime _runtime = new();
        private bool _resetting;
        private StarterPrologueCombatBoundary _boundary;

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
                step >= StarterPrologueStep.RescueCompleted)
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

            StarterPrologueThreatPreview.Remove();
            var targets = new List<GameObject>(
                StarterPrologueTrainingRuntime
                    .RequiredAgitatedSpiritCount);
            int agitatedIndex = 0;
            for (int i = 0; i < markers.Length; i++)
            {
                StarterPrologueEnemySpawnMarker marker = markers[i];
                if (marker.Role != StarterPrologueEnemyRole.Melee)
                    continue;

                GameObject enemy = EnemyBase.Spawn(
                    marker.transform.position,
                    TutorialHpMultiplier,
                    TutorialDamageMultiplier).gameObject;
                enemy.name =
                    $"TutorialAgitatedSpirit_{++agitatedIndex}";
                targets.Add(enemy);
            }

            if (!RegisterWave(targets, false))
                return false;

            BeginCombatBoundary(markers);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            PublishEnemyCount();
            return true;
        }

        private bool RegisterWave(
            IReadOnlyList<GameObject> enemies,
            bool possessedHostWave)
        {
            var ids = new List<int>(enemies.Count);
            for (int i = 0; i < enemies.Count; i++)
                ids.Add(enemies[i].GetInstanceID());

            StarterPrologueTrainingStartResult result =
                possessedHostWave
                    ? _runtime.BeginPossessedHost(ids)
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
                    StarterPrologueTrainingWave.AgitatedSpirits)
                {
                    PublishObjective(
                        "躁动已平息，注意更强的能量",
                        evt.Position);
                    StartCoroutine(BeginPossessedHostWave());
                }
                else if (_runtime.CurrentWave ==
                         StarterPrologueTrainingWave.Completed)
                {
                    CompleteEncounter(evt.Position);
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
            PublishObjective(
                "救援失败，正在返回检查点",
                Vector3.zero,
                false);
            StartCoroutine(ReloadAfterFailure());
        }

        private IEnumerator ReloadAfterFailure()
        {
            Time.timeScale = 1f;
            yield return null;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.path);
        }

        private IEnumerator BeginPossessedHostWave()
        {
            yield return new WaitForSeconds(0.7f);
            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<StarterPrologueEnemySpawnMarker>();
            if (!HasValidRoster(markers))
            {
                Debug.LogError(
                    "[新手验证战] 失控者阶段缺少远程出生标记。");
                yield break;
            }

            var enemies = new List<GameObject>(
                StarterPrologueTrainingRuntime
                    .RequiredPossessedHostCount);
            foreach (StarterPrologueEnemySpawnMarker marker in markers)
            {
                if (marker.Role != StarterPrologueEnemyRole.Ranged)
                    continue;

                GameObject enemy =
                    StarterProloguePossessedHost.Spawn(
                    marker.transform.position,
                    PossessedHpMultiplier,
                    TutorialDamageMultiplier,
                    OnPossessedHostResolved).gameObject;
                enemies.Add(enemy);
            }

            if (!RegisterWave(enemies, true))
                yield break;
            FocusCamera(enemies[0].transform.position, 0.9f);
            PublishEnemyCount();
        }

        private void OnPossessedHostResolved(
            GameObject host,
            Vector3 position)
        {
            if (!_runtime.IsRunning ||
                host == null ||
                !_enemyIds.TryGetValue(host, out int hostId) ||
                _runtime.CurrentWave !=
                    StarterPrologueTrainingWave.PossessedHost)
            {
                return;
            }

            _enemyIds.Remove(host);
            StarterPrologueTrainingStep result =
                _runtime.RegisterDefeated(hostId);
            PublishEnemyCount();
            if (result.Completed &&
                _runtime.CurrentWave ==
                    StarterPrologueTrainingWave.Completed)
            {
                CompleteEncounter(position);
            }
        }

        private void CompleteEncounter(Vector3 separationPosition)
        {
            Unsubscribe();
            if (_boundary != null)
            {
                _boundary.End();
                _boundary = null;
            }
            SpawnUnconsciousPair(separationPosition);
            FocusCamera(separationPosition, 1.2f);
            StarterPrologueAdvanceResult result =
                StarterPrologueProgression.RecordRescueCompleted(
                    SaveSystem.Instance.Data);
            if (result == StarterPrologueAdvanceResult.Success)
                SaveSystem.Instance.Save();
            PublishObjective(
                "救援完成 · 准备返回家园",
                separationPosition,
                false);
            StarterPrologueMilestoneHUD.Show(
                "失控解除",
                "人和灵宠都已脱离危险",
                new Color(0.42f, 0.86f, 0.92f));
            StarterPrologueHomeTransition.Begin(2.4f);
            Debug.Log(
                "<color=#F2B45E>[新手序章] 救援完成，等待转场家园。</color>");
        }

        private void PublishEnemyCount()
        {
            int remaining = _runtime.Remaining;
            GameEvents.Publish(new GameEvents.EnemyCountChanged
            {
                RemainingCount = remaining,
                TotalCount = _runtime.CurrentWave ==
                             StarterPrologueTrainingWave.PossessedHost
                    ? StarterPrologueTrainingRuntime
                        .RequiredPossessedHostCount
                    : StarterPrologueTrainingRuntime
                        .RequiredAgitatedSpiritCount
            });
            if (_runtime.CurrentWave ==
                StarterPrologueTrainingWave.PossessedHost)
            {
                GameObject host = FirstTrackedTarget();
                PublishObjective(
                    "稳定失控宿主",
                    host != null
                        ? host.transform.position
                        : transform.position);
            }
            else if (_runtime.CurrentWave ==
                     StarterPrologueTrainingWave.AgitatedSpirits &&
                     remaining > 0)
            {
                GameObject target = FirstTrackedTarget();
                PublishObjective(
                    $"击退躁动灵宠 · {remaining}/2",
                    target != null
                        ? target.transform.position
                        : transform.position);
            }
        }

        public static void SpawnUnconsciousPair(Vector3 position)
        {
            GameObject host = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            host.name = "Unconscious_StarTeamMember";
            host.transform.SetPositionAndRotation(
                position + Vector3.left * 0.65f,
                Quaternion.Euler(0f, 0f, 90f));
            host.transform.localScale =
                new Vector3(0.48f, 0.78f, 0.48f);
            DisableCollider(host);
            SetColor(host, new Color(0.31f, 0.37f, 0.5f));

            GameObject spirit = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            spirit.name = "Unconscious_SeparatedSpirit";
            spirit.transform.position =
                position + Vector3.right * 0.75f +
                Vector3.up * 0.22f;
            spirit.transform.localScale = Vector3.one * 0.58f;
            DisableCollider(spirit);
            SetColor(spirit, new Color(0.25f, 0.66f, 0.92f));
        }

        private static void DisableCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static void SetColor(
            GameObject target,
            Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        private void BeginCombatBoundary(
            IReadOnlyList<StarterPrologueEnemySpawnMarker> markers)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < markers.Count; i++)
                center += markers[i].transform.position;
            center /= markers.Count;

            float farthest = 0f;
            for (int i = 0; i < markers.Count; i++)
            {
                Vector3 offset =
                    markers[i].transform.position - center;
                offset.y = 0f;
                farthest = Mathf.Max(farthest, offset.magnitude);
            }
            _boundary = StarterPrologueCombatBoundary.Begin(
                center,
                Mathf.Max(11f, farthest + 6f));
        }

        private GameObject FirstTrackedTarget()
        {
            foreach (GameObject target in _enemyIds.Keys)
            {
                if (target != null)
                    return target;
            }
            return null;
        }

        private static StarterPrologueMarker FindMarker(
            StarterPrologueMarkerKind kind)
        {
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind == kind)
                    return marker;
            }
            return null;
        }

        private static void PublishObjective(
            string text,
            Vector3 position,
            bool hasPosition = true)
        {
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = text,
                    WorldPosition = position,
                    HasWorldPosition = hasPosition
                });
        }

        private static void FocusCamera(
            Vector3 position,
            float duration)
        {
            TopDownCamera camera =
                FindObjectOfType<TopDownCamera>();
            camera?.FocusOn(position, duration);
        }

        private static bool HasValidRoster(
            IReadOnlyList<StarterPrologueEnemySpawnMarker> markers)
        {
            if (markers == null ||
                markers.Count !=
                StarterPrologueTrainingRuntime
                    .RequiredAgitatedSpiritCount +
                StarterPrologueTrainingRuntime
                    .RequiredPossessedHostCount)
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
            if (_boundary != null)
                _boundary.End();
            _runtime.Reset();
            _enemyIds.Clear();
        }
    }
}
