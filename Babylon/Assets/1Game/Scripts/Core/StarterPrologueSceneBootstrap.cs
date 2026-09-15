using UnityEngine;
using UnityEngine.AI;

namespace XianTu
{
    /// <summary>
    /// 独立新手场景的最小运行时装配器。
    /// 不启动Demo1地牢和GameManager，只复用玩家、HUD与基础公共系统。
    /// </summary>
    public sealed class StarterPrologueSceneBootstrap : MonoBehaviour
    {
        private bool _previousCircuitRuntime;

        private void Awake()
        {
            _previousCircuitRuntime =
                FeatureFlags.EnableCircuitRuntime;
            FeatureFlags.EnableCircuitRuntime = true;

            SystemsBuilder systems =
                FindObjectOfType<SystemsBuilder>();
            if (systems == null)
            {
                systems = new GameObject("Systems")
                    .AddComponent<SystemsBuilder>();
            }

            GameplayBuilder gameplay =
                FindObjectOfType<GameplayBuilder>();
            if (gameplay == null)
            {
                gameplay = new GameObject("Gameplay")
                    .AddComponent<GameplayBuilder>();
            }

            HudBuilder hud = FindObjectOfType<HudBuilder>();
            if (hud == null)
            {
                hud = new GameObject("UI")
                    .AddComponent<HudBuilder>();
            }

            systems.BuildObjectPool();
            BuildNavigation();
            gameplay.BuildPlayer(
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            PlacePlayerAtMarker();
            SetupCaretakerPresentation();
            hud.BuildHud();
            FindObjectOfType<GameHUD>()
                ?.ConfigureForStarterPrologue();
            if (FindObjectOfType<DebugConsole>() == null)
            {
                new GameObject("StarterPrologueDebugConsole")
                    .AddComponent<DebugConsole>();
            }
            StarterPrologueObjectiveHUD.EnsureExists();
            StarterPrologueDialogueSystem.EnsureExists();
            StarterPrologueCombatTutorial.EnsureExists();
            StarterPrologueTimingTracker.EnsureExists();
            SetupPathGuide();
            SetupThreatPreview();
            SetupHomeReturnTrigger();
            PublishInitialObjective();
            StarterPrologueOpeningBeat.EnsureExists();
            systems.BuildHitStop();
            systems.BuildEventSystem();
            systems.BuildAudioManager();
        }

        private void OnDestroy()
        {
            FeatureFlags.EnableCircuitRuntime =
                _previousCircuitRuntime;
        }

        private static void PlacePlayerAtMarker()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null)
                return;

            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            StarterPrologueMarkerKind desired =
                ResumeMarkerFor(SaveSystem.Instance.Data);
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind != desired)
                {
                    continue;
                }

                CharacterController controller =
                    player.GetComponent<CharacterController>();
                if (controller != null)
                    controller.enabled = false;
                player.transform.SetPositionAndRotation(
                    marker.transform.position,
                    marker.transform.rotation);
                if (controller != null)
                    controller.enabled = true;
                return;
            }
        }

        private static void BuildNavigation()
        {
            StarterPrologueMarker marker =
                FindObjectOfType<StarterPrologueMarker>();
            if (marker == null)
                return;

            // 序章是手工场景，NavMesh 由场景上的 NavMeshSurface 预烘并随场景加载。
            // HIGHLANDS 的环境网格不开放读取，运行时烘焙在打包后会丢失这些障碍，
            // 所以只有预烘数据缺失时才退回运行时构建保底。
            if (NavMesh.CalculateTriangulation().vertices.Length > 0)
                return;

            // 救援与战斗已合并为同一块空地，兜底导航覆盖这块空地和通往家园的出口。
            GameObject owner = marker.transform.root.gameObject;
            var clearing = new Bounds(
                new Vector3(6.5f, 8f, -14.5f),
                new Vector3(37f, 12f, 25f));

            DungeonNavMeshRuntime runtime =
                DungeonNavMeshRuntime.BuildFor(owner, clearing);
            if (runtime == null || !runtime.IsBuilt)
            {
                Debug.LogError(
                    "[新手序章] 战斗区运行时NavMesh构建失败。");
            }
        }

        private static void PublishInitialObjective()
        {
            StarterPrologueStep step =
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data);
            StarterPrologueMarkerKind markerKind;
            string text;
            switch (step)
            {
                case StarterPrologueStep.StarterChosen:
                    markerKind =
                        StarterPrologueMarkerKind.RescueClearing;
                    text = "将灵宠附着到一个动作";
                    break;
                case StarterPrologueStep.AttachmentChosen:
                    markerKind =
                        StarterPrologueMarkerKind.PossessionClearing;
                    text = "赶往前方击退凶性灵宠";
                    break;
                case StarterPrologueStep.RescueCompleted:
                    markerKind =
                        StarterPrologueMarkerKind.HomeReturn;
                    text = "前往回家点";
                    break;
                case StarterPrologueStep.Completed:
                    GameEvents.Publish(
                        new GameEvents
                            .StarterPrologueObjectiveChanged
                        {
                            Text = "正在返回家园……",
                            HasWorldPosition = false
                        });
                    return;
                default:
                    markerKind =
                        StarterPrologueMarkerKind.RescueClearing;
                    text =
                        StarterPrologueOpeningBeat
                            .MovementObjective;
                    break;
            }

            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind != markerKind)
                    continue;
                GameEvents.Publish(
                    new GameEvents.StarterPrologueObjectiveChanged
                    {
                        Text = text,
                        WorldPosition = marker.transform.position,
                        HasWorldPosition =
                            step != StarterPrologueStep.Completed
                    });
                return;
            }
        }

        private static void SetupHomeReturnTrigger()
        {
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind !=
                    StarterPrologueMarkerKind.HomeReturn)
                {
                    continue;
                }
                StarterPrologueHomeCompletion.EnsureAt(
                    marker.transform);
                return;
            }
        }

        private static void SetupCaretakerPresentation()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.RescueCompleted)
            {
                return;
            }
            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null &&
                caretaker.GetComponent<
                    StarterPrologueCaretakerFear>() == null)
            {
                caretaker.AddComponent<
                    StarterPrologueCaretakerFear>();
            }
        }

        private static void SetupPathGuide()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.StarterChosen)
            {
                return;
            }

            Transform start = null;
            Transform destination = null;
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind ==
                    StarterPrologueMarkerKind.PlayerSpawn)
                {
                    start = marker.transform;
                }
                else if (marker.Kind ==
                         StarterPrologueMarkerKind.RescueClearing)
                {
                    destination = marker.transform;
                }
            }
            StarterProloguePathGuide.EnsureBetween(
                start,
                destination);
        }

        private static void SetupThreatPreview()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.AttachmentChosen)
            {
                return;
            }
            StarterPrologueEnemySpawnMarker[] markers =
                FindObjectsOfType<
                    StarterPrologueEnemySpawnMarker>();
            foreach (StarterPrologueEnemySpawnMarker marker in markers)
            {
                if (marker.Role != StarterPrologueEnemyRole.Ranged)
                    continue;
                StarterPrologueTrainingEncounter encounter =
                    FindObjectOfType<
                        StarterPrologueTrainingEncounter>();
                StarterPrologueThreatPreview.CreateAt(
                    marker.transform.position,
                    encounter != null
                        ? encounter.ThreatVisualPrefab
                        : null);
                return;
            }
        }

        public static StarterPrologueMarkerKind ResumeMarkerFor(
            SaveDataV1 save)
        {
            return StarterPrologueProgression.GetStep(save) switch
            {
                StarterPrologueStep.StarterChosen =>
                    StarterPrologueMarkerKind.RescueClearing,
                StarterPrologueStep.AttachmentChosen =>
                    StarterPrologueMarkerKind.PossessionClearing,
                StarterPrologueStep.RescueCompleted =>
                    StarterPrologueMarkerKind.RescueResolved,
                StarterPrologueStep.Completed =>
                    StarterPrologueMarkerKind.HomeReturn,
                _ => StarterPrologueMarkerKind.PlayerSpawn
            };
        }

    }
}
