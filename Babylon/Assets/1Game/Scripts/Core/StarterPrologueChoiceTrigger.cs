using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>进入初契小坛时打开三宠选择；已有初契结果时只恢复实体状态。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueChoiceTrigger : MonoBehaviour
    {
        public const string CaretakerWarning =
            "先别过来！它们被失控的能量惊到了！";

        [SerializeField]
        private Transform revealFocus;

        private bool _opened;
        private bool _opening;

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
            if (_opened || _opening || !other.CompareTag("Player"))
                return;
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.StarterChosen)
            {
                ApplyChosenEntityState();
                return;
            }

            StartCoroutine(OpenChoiceAfterThreatReveal());
        }

        private IEnumerator OpenChoiceAfterThreatReveal()
        {
            _opening = true;
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = string.Empty,
                    HasWorldPosition = false
                });

            Transform caretaker = ResolveRevealFocus();
            if (caretaker != null)
            {
                FocusCamera(caretaker.position, 1.05f);
                StarterPrologueDialogueHUD.Show(
                    "照料员",
                    CaretakerWarning,
                    1.8f);
                yield return new WaitForSecondsRealtime(1.08f);
            }

            StarterPrologueThreatPreview preview =
                FindObjectOfType<StarterPrologueThreatPreview>();
            if (preview != null)
            {
                FocusCamera(preview.transform.position, 0.7f);
                yield return new WaitForSecondsRealtime(0.72f);
            }
            FocusCamera(transform.position, 1.1f);
            _opened = StarterSpiritChoiceUI.ShowForScene(OnChosen);
            _opening = false;
        }

        private Transform ResolveRevealFocus()
        {
            if (revealFocus != null)
                return revealFocus;

            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker == null)
                caretaker = GameObject.Find("MARKER_Caretaker");
            return caretaker != null
                ? caretaker.transform
                : null;
        }

        private void OnChosen(
            StableConfigId species,
            SpiritInstanceState spirit)
        {
            _opened = false;
            StarterPrologueCaretakerExit.Play(species);
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            controller?.TryConfigure(SaveSystem.Instance.Data);
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = "将灵宠附着到一个动作",
                    HasWorldPosition = false
                });
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

            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsByType<StarterSpiritChoiceWorldEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
            {
                entity.gameObject.SetActive(false);
            }
            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null)
                caretaker.SetActive(false);
        }

        private static void FocusCamera(
            Vector3 position,
            float duration)
        {
            TopDownCamera camera =
                FindObjectOfType<TopDownCamera>();
            camera?.FocusOn(position, duration);
        }
    }
}
