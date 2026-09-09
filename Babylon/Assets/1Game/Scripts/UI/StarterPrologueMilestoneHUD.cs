using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 序章关键结果的短暂非阻塞反馈。
    /// 只做信息确认，不暂停时间、不接受输入。
    /// </summary>
    public sealed class StarterPrologueMilestoneHUD : MonoBehaviour
    {
        private const float VisibleSeconds = 2.4f;
        private static StarterPrologueMilestoneHUD _instance;

        private RectTransform _panel;
        private CanvasGroup _group;
        private Image _accent;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _subtitle;
        private float _remaining;

        public static void Show(
            string title,
            string subtitle,
            Color accent)
        {
            EnsureInstance();
            _instance.ShowInternal(title, subtitle, accent);
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
                return;

            GameObject root = new(
                "StarterPrologueMilestoneHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 132;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _instance = root.AddComponent<
                StarterPrologueMilestoneHUD>();
            _instance.Build();
        }

        private void Build()
        {
            GameObject panelObject = new(
                "MilestonePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax =
                new Vector2(0.5f, 0.66f);
            _panel.sizeDelta = new Vector2(520f, 104f);
            Image background = panelObject.GetComponent<Image>();
            background.color =
                new Color(0.055f, 0.07f, 0.075f, 0.94f);
            background.raycastTarget = false;
            _group = panelObject.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            GameObject accentObject = new(
                "Accent",
                typeof(RectTransform),
                typeof(Image));
            accentObject.transform.SetParent(_panel, false);
            RectTransform accentRect =
                accentObject.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(7f, 0f);
            _accent = accentObject.GetComponent<Image>();
            _accent.raycastTarget = false;

            _title = CreateText(
                _panel,
                "Title",
                new Vector2(30f, 48f),
                new Vector2(-24f, -12f),
                27f,
                FontStyles.Bold);
            _subtitle = CreateText(
                _panel,
                "Subtitle",
                new Vector2(30f, 14f),
                new Vector2(-24f, -54f),
                16f,
                FontStyles.Normal);
            _subtitle.color =
                new Color(0.82f, 0.84f, 0.80f, 1f);
            _panel.gameObject.SetActive(false);
        }

        private void ShowInternal(
            string title,
            string subtitle,
            Color accent)
        {
            _title.text = title ?? string.Empty;
            _subtitle.text = subtitle ?? string.Empty;
            _title.color = accent;
            _accent.color = accent;
            _remaining = VisibleSeconds;
            _panel.localScale = Vector3.one * 0.96f;
            _group.alpha = 0f;
            _panel.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_remaining <= 0f || !_panel.gameObject.activeSelf)
                return;

            _remaining -= Time.unscaledDeltaTime;
            float elapsed = VisibleSeconds - _remaining;
            float fadeIn = Mathf.Clamp01(elapsed / 0.18f);
            float fadeOut = Mathf.Clamp01(_remaining / 0.35f);
            _group.alpha = Mathf.Min(fadeIn, fadeOut);
            _panel.localScale =
                Vector3.one * Mathf.Lerp(
                    0.96f,
                    1f,
                    Mathf.Clamp01(elapsed / 0.22f));
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
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                text.font = UGuiKit.CjkFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.MidlineLeft;
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
