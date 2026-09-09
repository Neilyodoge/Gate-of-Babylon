using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// ProjectR独立家园场景的最小技术落点。
    /// 当前只负责公共系统、玩家出生和序章到达提交，不定义家园玩法布局。
    /// </summary>
    public sealed class ProjectRHomeSceneBootstrap : MonoBehaviour
    {
        public const string PlayerSpawnName =
            "ProjectRHomePlayerSpawn";

        private void Awake()
        {
            SystemsBuilder systems =
                FindFirstObjectByType<SystemsBuilder>();
            if (systems == null)
            {
                systems = new GameObject("Systems")
                    .AddComponent<SystemsBuilder>();
            }

            GameplayBuilder gameplay =
                FindFirstObjectByType<GameplayBuilder>();
            if (gameplay == null)
            {
                gameplay = new GameObject("Gameplay")
                    .AddComponent<GameplayBuilder>();
            }

            systems.BuildObjectPool();
            if (PlayerController.Instance == null)
            {
                gameplay.BuildPlayer(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }
            PlacePlayer();
            systems.BuildHitStop();
            systems.BuildEventSystem();
            systems.BuildAudioManager();

            CommitPrologueArrival();
        }

        private static void PlacePlayer()
        {
            PlayerController player = PlayerController.Instance;
            GameObject marker = GameObject.Find(PlayerSpawnName);
            if (player == null || marker == null)
                return;

            CharacterController controller =
                player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            player.transform.SetPositionAndRotation(
                marker.transform.position,
                marker.transform.rotation);
            if (controller != null)
                controller.enabled = true;
        }

        private static void CommitPrologueArrival()
        {
            StarterPrologueAdvanceResult result =
                TryCommitArrival(SaveSystem.Instance.Data);
            if (result != StarterPrologueAdvanceResult.Success)
                return;

            SaveSystem.Instance.Save();
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = "家园 · 准备下一次探索",
                    HasWorldPosition = false
                });

            string partnerName = "新伙伴";
            string speciesId =
                SaveSystem.Instance.Data.starterSpiritSpeciesId;
            if (!string.IsNullOrWhiteSpace(speciesId) &&
                StarterSpiritChoicePresentation.TryGetProfile(
                    new StableConfigId(speciesId),
                    out StarterSpiritChoiceProfile profile))
            {
                partnerName = profile.DisplayName;
            }

            StarterPrologueMilestoneHUD.Show(
                "初契完成",
                $"{partnerName}已成为你的第一位伙伴",
                new Color(0.96f, 0.76f, 0.38f));
            Debug.Log(
                "<color=#F2B45E>[新手序章] 家园加载完成，序章正式完成。</color>");
        }

        public static StarterPrologueAdvanceResult TryCommitArrival(
            SaveDataV1 save)
        {
            return StarterPrologueProgression
                .RecordPrologueCompleted(save);
        }
    }
}
