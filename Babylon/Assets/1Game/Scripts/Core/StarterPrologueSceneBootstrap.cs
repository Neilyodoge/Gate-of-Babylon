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
            gameplay.BuildPlayer(
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            PlacePlayerAtMarker();
            hud.BuildHud();
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

        public static StarterPrologueMarkerKind ResumeMarkerFor(
            SaveDataV1 save)
        {
            return StarterPrologueProgression.GetStep(save) switch
            {
                StarterPrologueStep.StarterChosen =>
                    StarterPrologueMarkerKind.BondingAltar,
                StarterPrologueStep.AttachmentChosen =>
                    StarterPrologueMarkerKind.TrainingArena,
                StarterPrologueStep.Completed =>
                    StarterPrologueMarkerKind.CaveGate,
                _ => StarterPrologueMarkerKind.PlayerSpawn
            };
        }
    }
}
