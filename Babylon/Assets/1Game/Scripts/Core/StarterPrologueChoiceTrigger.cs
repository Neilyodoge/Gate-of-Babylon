using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>进入初契小坛时打开三宠选择；已有初契结果时只恢复实体状态。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueChoiceTrigger : MonoBehaviour
    {
        private bool _opened;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private IEnumerator Start()
        {
            ApplyChosenEntityState();
            yield return null;
            TryOpenRequiredAttachment();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_opened || !other.CompareTag("Player"))
                return;
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.StarterChosen)
            {
                ApplyChosenEntityState();
                return;
            }

            _opened = StarterSpiritChoiceUI.ShowForScene(OnChosen);
        }

        private void OnChosen(
            StableConfigId species,
            SpiritInstanceState spirit)
        {
            _opened = false;
            ApplyChosenEntityState();
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            controller?.TryConfigure(SaveSystem.Instance.Data);
            TryOpenRequiredAttachment();
        }

        private static bool TryOpenRequiredAttachment()
        {
            if (!StarterPrologueProgression.RequiresFirstAttachment(
                    SaveSystem.Instance.Data))
            {
                return false;
            }
            SpiritCircuitHUD hud =
                FindObjectOfType<SpiritCircuitHUD>();
            return hud != null &&
                   hud.TryBeginPrologueAttachmentSelection();
        }

        private static void ApplyChosenEntityState()
        {
            string chosen =
                SaveSystem.Instance.Data.starterSpiritSpeciesId;
            if (string.IsNullOrWhiteSpace(chosen))
                return;

            var chosenId = new StableConfigId(chosen);
            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsOfType<StarterSpiritChoiceWorldEntity>();
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
            {
                entity.gameObject.SetActive(
                    entity.SpeciesId == chosenId);
            }
        }
    }
}
