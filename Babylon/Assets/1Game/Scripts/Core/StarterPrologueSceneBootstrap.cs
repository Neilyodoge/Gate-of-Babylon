using UnityEngine;

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
            RestorePostRescueState();
            hud.BuildHud();
            StarterPrologueObjectiveHUD.EnsureExists();
            SetupReturnGuide();
            PublishInitialObjective();
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
            if (marker != null)
                DungeonNavMeshRuntime.BuildFor(
                    marker.transform.root.gameObject);
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
                    text = "赶往失控区救人";
                    break;
                case StarterPrologueStep.RescueCompleted:
                    markerKind =
                        StarterPrologueMarkerKind.HomeReturn;
                    text = "沿小径回家";
                    break;
                case StarterPrologueStep.Completed:
                    markerKind =
                        StarterPrologueMarkerKind.HomeReturn;
                    text = "序章完成 · 已回到家园";
                    break;
                default:
                    markerKind =
                        StarterPrologueMarkerKind.RescueClearing;
                    text = "前往救援空地";
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

        private static void SetupReturnGuide()
        {
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind ==
                    StarterPrologueMarkerKind.HomeReturn)
                {
                    StarterPrologueReturnGuide.EnsureAt(
                        marker.transform);
                    StarterPrologueHomeCompletion.EnsureAt(
                        marker.transform);
                    return;
                }
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

        private static void RestorePostRescueState()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) !=
                StarterPrologueStep.RescueCompleted)
            {
                return;
            }
            StarterPrologueMarker[] markers =
                FindObjectsOfType<StarterPrologueMarker>();
            foreach (StarterPrologueMarker marker in markers)
            {
                if (marker.Kind !=
                    StarterPrologueMarkerKind.RescueResolved)
                {
                    continue;
                }
                StarterPrologueTrainingEncounter
                    .SpawnUnconsciousPair(
                        marker.transform.position +
                        marker.transform.forward * 2.5f);
                return;
            }
        }
    }
}
