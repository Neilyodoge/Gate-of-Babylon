using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 序章短对白字幕。非阻塞、不接收输入，不承担正式对话树功能。
    /// </summary>
    public sealed class StarterPrologueDialogueHUD : MonoBehaviour
    {
        private static StarterPrologueDialogueHUD _instance;

        private RectTransform _panel;
        private CanvasGroup _group;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _line;
        private float _visibleSeconds;
        private float _remaining;

        public static void Show(
            string speaker,
            string line,
            float visibleSeconds = 2.2f)
        {
            EnsureInstance();
            _instance.ShowInternal(
                speaker,
                line,
                visibleSeconds);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            GameObject root = new(
                "StarterPrologueDialogueHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 131;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = root.AddComponent<
                StarterPrologueDialogueHUD>();
            _instance.Build();
        }

        private void Build()
        {
            GameObject panelObject = new(
                "DialoguePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax =
                new Vector2(0.5f, 0.18f);
            _panel.sizeDelta = new Vector2(700f, 82f);

            Image background = panelObject.GetComponent<Image>();
            background.color =
                new Color(0.045f, 0.055f, 0.05f, 0.88f);
            background.raycastTarget = false;
            _group = panelObject.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _speaker = CreateText(
                _panel,
                "Speaker",
                new Vector2(24f, 42f),
                new Vector2(-24f, -8f),
                15f,
                FontStyles.Bold);
            _speaker.color =
                new Color(0.96f, 0.76f, 0.38f);
            _line = CreateText(
                _panel,
                "Line",
                new Vector2(24f, 8f),
                new Vector2(-24f, -42f),
                21f,
                FontStyles.Normal);
            _line.color =
                new Color(0.95f, 0.96f, 0.91f);
            _panel.gameObject.SetActive(false);
        }

        private void ShowInternal(
            string speaker,
            string line,
            float visibleSeconds)
        {
            _speaker.text = speaker ?? string.Empty;
            _line.text = line ?? string.Empty;
            _visibleSeconds = Mathf.Max(0.5f, visibleSeconds);
            _remaining = _visibleSeconds;
            _group.alpha = 0f;
            _panel.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_remaining <= 0f ||
                !_panel.gameObject.activeSelf)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;
            float elapsed = _visibleSeconds - _remaining;
            float fadeIn = Mathf.Clamp01(elapsed / 0.15f);
            float fadeOut = Mathf.Clamp01(_remaining / 0.25f);
            _group.alpha = Mathf.Min(fadeIn, fadeOut);
            if (_remaining <= 0f)
                _panel.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float fontSize,
            FontStyles style)
        {
            GameObject go = new(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect =
                go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text =
                go.GetComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                text.font = UGuiKit.CjkFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment =
                TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
