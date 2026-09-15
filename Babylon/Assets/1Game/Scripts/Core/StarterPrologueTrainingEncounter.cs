using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace XianTu
{
    /// <summary>
    /// 新手序章两阶段遭遇战：先击退两只躁动灵宠，再迫使刃铠灵撤离。
    /// 只统计自己生成的目标，失败重载当前独立场景并按检查点回到遭遇区。
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueTrainingEncounter : MonoBehaviour
    {
        private const float TutorialHpMultiplier = 0.55f;
        private const float TutorialDamageMultiplier = 0.25f;
        private const float BladeBeastHpMultiplier = 0.9f;
        private const float AgitatedSpiritRadius = 0.6f;

        private readonly Dictionary<GameObject, int> _enemyIds = new();
        private readonly StarterPrologueTrainingRuntime _runtime = new();
        [SerializeField] private GameObject agitatedSpiritPrefab;
        [SerializeField] private GameObject possessedHostPrefab;
        private bool _resetting;
        private bool _completing;
        private bool _preparing;
        private StarterPrologueCombatBoundary _boundary;
        private StarterPrologueCombatTutorial _tutorial;

        public bool IsRunning => _runtime.IsRunning;
        public bool IsPreparing => _preparing;
        public int Remaining => _runtime.Remaining;
        public GameObject ThreatVisualPrefab =>
            possessedHostPrefab;

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
            if (_preparing ||
                _runtime.CurrentWave !=
                    StarterPrologueTrainingWave.None ||
                step < StarterPrologueStep.AttachmentChosen ||
                step >= StarterPrologueStep.RescueCompleted)
            {
                return false;
            }

            _preparing = true;
            _tutorial =
                StarterPrologueCombatTutorial.EnsureExists();
            _tutorial.BeginPrimer(BeginCombatAfterPrimer);
            return true;
        }

        private void BeginCombatAfterPrimer()
        {
            _preparing = false;
            if (!StartAgitatedSpiritWave())
            {
                PublishObjective(
                    "战斗目标生成失败，请重新进入战斗区",
                    transform.position);
            }
        }

        private bool StartAgitatedSpiritWave()
        {
            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<StarterPrologueEnemySpawnMarker>();
            if (!HasValidRoster(markers))
            {
                Debug.LogError(
                    "[新手验证战] 需要2个近战和1个远程出生标记。");
                return false;
            }

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
                    TutorialDamageMultiplier,
                    agitatedSpiritPrefab).gameObject;
                enemy.name =
                    $"TutorialAgitatedSpirit_{++agitatedIndex}";
                ConfigureAgitatedSpiritSpacing(
                    enemy,
                    agitatedIndex);
                targets.Add(enemy);
            }

            if (!RegisterWave(targets, false))
                return false;

            BeginCombatBoundary(markers);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            _tutorial?.BeginCarrierPractice();
            PublishEnemyCount();
            return true;
        }

        private static void ConfigureAgitatedSpiritSpacing(
            GameObject enemy,
            int index)
        {
            CharacterController controller =
                enemy.GetComponent<CharacterController>();
            if (controller != null)
                controller.radius = AgitatedSpiritRadius;

            NavMeshAgent agent =
                enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = AgitatedSpiritRadius;
                agent.avoidancePriority =
                    index % 2 == 0 ? 35 : 65;
            }
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
                        "躁动灵宠已退开，注意远处的刃铠灵",
                        evt.Position);
                    StartCoroutine(BeginPossessedHostWave());
                }
                else if (_runtime.CurrentWave ==
                             StarterPrologueTrainingWave.Completed &&
                         !_completing)
                {
                    StartCoroutine(
                        CompleteEncounterSequence(evt.Position));
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
            yield return new WaitForSeconds(2.5f);
            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<StarterPrologueEnemySpawnMarker>();
            Vector3 spawnPosition =
                transform.position + transform.forward * 7f;
            bool foundMarker = false;
            foreach (StarterPrologueEnemySpawnMarker marker in markers)
            {
                if (marker.Role != StarterPrologueEnemyRole.Ranged)
                    continue;
                spawnPosition = marker.transform.position;
                foundMarker = true;
                break;
            }
            if (!foundMarker)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(
                        spawnPosition,
                        out hit,
                        8f,
                        NavMesh.AllAreas))
                {
                    spawnPosition = hit.position;
                }
                Debug.LogWarning(
                    "[新手验证战] 刃铠灵远程出生标记缺失，已使用战斗区导航位置兜底。");
            }

            GameObject enemy =
                StarterProloguePossessedHost.Spawn(
                    spawnPosition,
                    BladeBeastHpMultiplier,
                    TutorialDamageMultiplier,
                    OnPossessedHostResolved,
                    possessedHostPrefab).gameObject;
            var enemies = new List<GameObject> { enemy };
            if (!RegisterWave(enemies, true))
            {
                RecoverFromWaveSpawnFailure();
                yield break;
            }
            // 正式战斗体已在相同出生点生成后再移除预告体，
            // 避免第一波开始时头目从远景中凭空消失。
            StarterPrologueThreatPreview.Remove();
            FocusCamera(enemies[0].transform.position, 0.9f);
            _tutorial?.BeginBossRule();
            PublishEnemyCount();
        }

        private void RecoverFromWaveSpawnFailure()
        {
            Unsubscribe();
            _runtime.Reset();
            _enemyIds.Clear();
            if (_boundary != null)
            {
                _boundary.End();
                _boundary = null;
            }
            PublishObjective(
                "刃铠灵暂未进入战场，请重新靠近战斗区",
                transform.position);
            Debug.LogError(
                "[新手验证战] 刃铠灵波次注册失败，遭遇已复位以避免流程锁死。");
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
                    StarterPrologueTrainingWave.Completed &&
                !_completing)
            {
                StartCoroutine(
                    CompleteEncounterSequence(position));
            }
        }

        private IEnumerator CompleteEncounterSequence(
            Vector3 retreatPosition)
        {
            _completing = true;
            _tutorial?.Complete();
            Unsubscribe();
            if (_boundary != null)
            {
                _boundary.End();
                _boundary = null;
            }

            FocusCamera(retreatPosition, 0.65f);
            yield return new WaitForSeconds(0.45f);

            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null)
            {
                StarterPrologueCaretakerFear fear =
                    caretaker.GetComponent<
                        StarterPrologueCaretakerFear>();
                fear?.Calm();
                FocusCamera(caretaker.transform.position, 1.1f);
                PublishObjective(
                    "听照料员说明情况",
                    caretaker.transform.position);
                StarterPrologueDialogueHUD.Show(
                    "照料员",
                    "它退走了……刚才真是吓死我了。",
                    2.1f);
                yield return new WaitForSeconds(1.75f);
                StarterPrologueDialogueHUD.Show(
                    "照料员",
                    $"多亏你和{ChosenSpiritName()}。这里还不安全，我们先回去。",
                    2.4f);
                yield return new WaitForSeconds(2.45f);
            }

            StarterPrologueAdvanceResult result =
                StarterPrologueProgression.RecordRescueCompleted(
                    SaveSystem.Instance.Data);
            if (result == StarterPrologueAdvanceResult.Success)
                SaveSystem.Instance.Save();
            StarterPrologueMarker homeReturn =
                FindMarker(StarterPrologueMarkerKind.HomeReturn);
            Vector3 returnPosition = homeReturn != null
                ? homeReturn.transform.position
                : retreatPosition;
            PublishObjective("前往回家点", returnPosition);
            StarterPrologueMilestoneHUD.Show(
                "道路安全",
                "凶兽退去，先返回家园",
                new Color(0.94f, 0.62f, 0.24f));
            if (caretaker != null)
                StarterPrologueCaretakerExit.Play();
            Debug.Log(
                "<color=#F2B45E>[新手序章] 刃铠灵撤离与照料员对话完成，等待玩家前往回家点。</color>");
        }

        private static string ChosenSpiritName()
        {
            string chosen =
                SaveSystem.Instance.Data.starterSpiritSpeciesId;
            if (!string.IsNullOrWhiteSpace(chosen) &&
                StarterSpiritChoicePresentation.TryGetProfile(
                    new StableConfigId(chosen),
                    out StarterSpiritChoiceProfile profile))
            {
                return profile.DisplayName;
            }
            return "灵宠";
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
                    "削弱刃铠灵凶势",
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
            _completing = false;
            _preparing = false;
        }
    }
}
