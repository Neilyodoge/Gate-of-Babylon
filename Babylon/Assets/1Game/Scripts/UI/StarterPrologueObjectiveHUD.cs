using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>序章专用的紧凑目标提示，不占用战斗技能栏。</summary>
    public sealed class StarterPrologueObjectiveHUD : MonoBehaviour
    {
        private TextMeshProUGUI _objective;
        private TextMeshProUGUI _distance;
        private GameObject _panel;
        private Vector3 _worldPosition;
        private bool _hasWorldPosition;

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
                return;
            }
            float distance = Vector3.Distance(
                PlayerController.Instance.transform.position,
                _worldPosition);
            _distance.text = $"{Mathf.CeilToInt(distance)}m";
        }

        private void OnObjectiveChanged(
            GameEvents.StarterPrologueObjectiveChanged evt)
        {
            _objective.text = evt.Text ?? string.Empty;
            _worldPosition = evt.WorldPosition;
            _hasWorldPosition = evt.HasWorldPosition;
            _panel.SetActive(!string.IsNullOrWhiteSpace(evt.Text));
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
            hud._objective = CreateText(
                panel,
                "Objective",
                new Vector2(20f, 10f),
                new Vector2(-74f, -10f),
                25f,
                TextAlignmentOptions.MidlineLeft);
            hud._distance = CreateText(
                panel,
                "Distance",
                new Vector2(420f, 10f),
                new Vector2(-18f, -10f),
                20f,
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
                typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -82f);
            rect.sizeDelta = new Vector2(500f, 58f);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.06f, 0.09f, 0.12f, 0.82f);
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
