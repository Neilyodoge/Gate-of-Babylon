using System.Collections;
using UnityEngine;

namespace XianTu
{
    /// <summary>进入初契小坛时打开三宠选择；已有初契结果时只恢复实体状态。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StarterPrologueChoiceTrigger : MonoBehaviour
    {
        public const string CaretakerWarning =
            "别过来！那只大家伙会冲过来！";
        public const string ContainerResponse =
            "等等……它们在回应你。";

        [SerializeField]
        private Transform revealFocus;
        [SerializeField]
        private Transform containerFocus;

        private bool _opened;
        private bool _opening;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private IEnumerator Start()
        {
            if (StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) <
                StarterPrologueStep.StarterChosen)
            {
                HideCandidatesForStory();
            }
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
                yield return StarterPrologueDialogueSystem.PlayNode(
                    "CaretakerWarning");
            }

            Vector3 containers = ResolveContainerFocus();
            FocusCamera(containers, 0.72f);
            RevealCandidates();
            yield return StarterPrologueDialogueSystem.PlayNode(
                "CandidatesRespond");

            StarterPrologueThreatPreview preview =
                FindObjectOfType<StarterPrologueThreatPreview>();
            if (preview != null)
            {
                FocusCamera(preview.transform.position, 0.7f);
                yield return new WaitForSecondsRealtime(0.72f);
            }
            FocusCamera(transform.position, 1.1f);
            _opened = StarterSpiritTrialController.Begin(
                transform,
                OnChosen);
            if (!_opened)
                _opened = StarterSpiritChoiceUI.ShowForScene(OnChosen);
            _opening = false;
        }

        private Vector3 ResolveContainerFocus()
        {
            if (containerFocus != null)
                return containerFocus.position;

            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsOfType<StarterSpiritChoiceWorldEntity>(
                    true);
            if (entities.Length == 0)
                return transform.position;
            Vector3 center = Vector3.zero;
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
                center += entity.transform.position;
            return center / entities.Length;
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
            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsOfType<StarterSpiritChoiceWorldEntity>(
                    true);
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
            {
                if (entity.SpeciesId == species)
                    entity.RevealChosenAndHide();
                else
                    entity.HideImmediately();
            }
            StarterSpiritCarrierController controller =
                FindObjectOfType<StarterSpiritCarrierController>();
            controller?.TryConfigure(SaveSystem.Instance.Data);
            bool requiresAttachment =
                StarterPrologueProgression.RequiresFirstAttachment(
                    SaveSystem.Instance.Data);
            GameEvents.Publish(
                new GameEvents.StarterPrologueObjectiveChanged
                {
                    Text = requiresAttachment
                        ? "将灵宠附着到一个动作"
                        : "赶往前方击退凶性灵宠",
                    HasWorldPosition = false
                });
            if (requiresAttachment)
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
            bool rescueCompleted =
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data) >=
                StarterPrologueStep.RescueCompleted;
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
                entity.HideImmediately();
            GameObject caretaker =
                GameObject.Find("RescueCaretaker_Whitebox");
            if (caretaker != null)
                caretaker.SetActive(!rescueCompleted);
        }

        private static void HideCandidatesForStory()
        {
            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsByType<StarterSpiritChoiceWorldEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (StarterSpiritChoiceWorldEntity entity in entities)
                entity.HideForStory();
        }

        private static void RevealCandidates()
        {
            StarterSpiritChoiceWorldEntity[] entities =
                FindObjectsByType<StarterSpiritChoiceWorldEntity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            float delay = 0f;
            foreach (StarterSpiritChoiceProfile profile in
                     StarterSpiritChoicePresentation.Profiles)
            {
                foreach (StarterSpiritChoiceWorldEntity entity in entities)
                {
                    if (entity.SpeciesId != profile.SpeciesId)
                        continue;
                    entity.RevealForChoice(delay);
                    delay += 0.16f;
                    break;
                }
            }
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
