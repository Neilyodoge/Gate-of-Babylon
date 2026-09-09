using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>序章专用的紧凑目标提示，不占用战斗技能栏。</summary>
    public sealed class StarterPrologueObjectiveHUD : MonoBehaviour
    {
        private TextMeshProUGUI _phase;
        private TextMeshProUGUI _objective;
        private TextMeshProUGUI _distance;
        private GameObject _panel;
        private CanvasGroup _panelGroup;
        private Vector3 _worldPosition;
        private bool _hasWorldPosition;
        private float _revealRemaining;
        private string _lastObjective;

        private void OnEnable()
        {
            GameEvents.Subscribe<
                GameEvents.StarterPrologueObjectiveChanged>(
                OnObjectiveChanged);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<
                GameEvents.StarterPrologueObjectiveChanged>(
                OnObjectiveChanged);
        }

        private void Update()
        {
            if (!_hasWorldPosition ||
                PlayerController.Instance == null)
            {
                _distance.text = string.Empty;
            }
            else
            {
                float distance = Vector3.Distance(
                    PlayerController.Instance.transform.position,
                    _worldPosition);
                _distance.text =
                    $"◆  {Mathf.CeilToInt(distance)}m";
            }

            if (_revealRemaining <= 0f || _panel == null)
                return;
            _revealRemaining -= Time.unscaledDeltaTime;
            float progress = 1f -
                Mathf.Clamp01(_revealRemaining / 0.22f);
            _panel.transform.localScale =
                Vector3.one * Mathf.Lerp(0.97f, 1f, progress);
            if (_panelGroup != null)
                _panelGroup.alpha = Mathf.Lerp(0.35f, 1f, progress);
        }

        private void OnObjectiveChanged(
            GameEvents.StarterPrologueObjectiveChanged evt)
        {
            _objective.text = evt.Text ?? string.Empty;
            _phase.text = PhaseLabelFor(
                StarterPrologueProgression.GetStep(
                    SaveSystem.Instance.Data),
                evt.Text);
            _worldPosition = evt.WorldPosition;
            _hasWorldPosition = evt.HasWorldPosition;
            bool show = !string.IsNullOrWhiteSpace(evt.Text);
            _panel.SetActive(show);
            if (show && !string.Equals(
                    _lastObjective,
                    evt.Text,
                    System.StringComparison.Ordinal))
            {
                _revealRemaining = 0.22f;
                _panel.transform.localScale =
                    Vector3.one * 0.97f;
                if (_panelGroup != null)
                    _panelGroup.alpha = 0.35f;
            }
            _lastObjective = evt.Text;
        }

        public static string PhaseLabelFor(
            StarterPrologueStep step,
            string objective)
        {
            if (step >= StarterPrologueStep.Completed)
                return "序章完成";
            if (step == StarterPrologueStep.RescueCompleted)
                return "转场 · 家园";
            if (step == StarterPrologueStep.StarterChosen)
                return "初契 · 选择附着";
            if (step == StarterPrologueStep.AttachmentChosen)
            {
                if (!string.IsNullOrWhiteSpace(objective) &&
                    (objective.Contains("稳定") ||
                     objective.Contains("更强") ||
                     objective.Contains("失控宿主")))
                {
                    return "救援 · 失控宿主";
                }
                if (!string.IsNullOrWhiteSpace(objective) &&
                    objective.Contains("躁动"))
                {
                    return "救援 · 躁动灵宠";
                }
                return "救援";
            }
            return "序章 · 遇险";
        }

        public static StarterPrologueObjectiveHUD EnsureExists()
        {
            StarterPrologueObjectiveHUD existing =
                FindObjectOfType<StarterPrologueObjectiveHUD>();
            if (existing != null)
                return existing;

            GameObject root = new(
                "StarterPrologueObjectiveHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform panel = CreatePanel(root.transform);
            StarterPrologueObjectiveHUD hud =
                root.AddComponent<StarterPrologueObjectiveHUD>();
            hud._panel = panel.gameObject;
            hud._panelGroup = panel.GetComponent<CanvasGroup>();
            hud._phase = CreateText(
                panel,
                "Phase",
                new Vector2(20f, 38f),
                new Vector2(-20f, -8f),
                13f,
                TextAlignmentOptions.MidlineLeft);
            hud._phase.color =
                new Color(0.95f, 0.73f, 0.35f);
            hud._phase.fontStyle = FontStyles.Bold;
            hud._objective = CreateText(
                panel,
                "Objective",
                new Vector2(20f, 8f),
                new Vector2(-100f, -30f),
                22f,
                TextAlignmentOptions.MidlineLeft);
            hud._distance = CreateText(
                panel,
                "Distance",
                new Vector2(420f, 8f),
                new Vector2(-18f, -30f),
                17f,
                TextAlignmentOptions.MidlineRight);
            hud._distance.color =
                new Color(0.56f, 0.85f, 1f);
            return hud;
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            GameObject go = new(
                "ObjectivePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -22f);
            rect.sizeDelta = new Vector2(540f, 70f);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.06f, 0.08f, 0.09f, 0.88f);
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject go = new(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                text.font = UGuiKit.CjkFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.96f, 0.95f, 0.9f);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }
    }
}
