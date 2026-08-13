using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 局内 HUD（V0.4.6 改 uGUI+TMP）—— 常驻 UI：顶部经验条 / 等级历练、右上角意志因果寿元、居中拾取飘字。
    /// 拾取/经验时弹一条飘字（监听 InsightChanged）。所有元素逐帧更新文本与可见性。
    /// </summary>
    public class RunHUD : MonoBehaviour
    {
        private static RunHUD _instance;
        public static RunHUD Instance => _instance;

        private struct PickupToast
        {
            public string text;
            public Color color;
            public float remaining;
        }
        private readonly List<PickupToast> _toasts = new();
        private const float ToastDuration = 2.5f;

        // uGUI 元素
        private GameObject _root;
        private GameObject _insightBar;
        private TextMeshProUGUI _insightLabel;
        private TextMeshProUGUI _cultLabel;
        private GameObject _moralPanel;
        private TextMeshProUGUI _daoxinLabel, _karmaLabel, _lifespanLabel;
        private GameObject _phaseRecapPanel;
        private TextMeshProUGUI _phaseRecapLabel;
        private RectTransform _toastRoot;
        private readonly List<TextMeshProUGUI> _toastPool = new();
        private const int ToastPoolMax = 8;

        public static void Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("RunHUD");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RunHUD>();
            }
        }

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.InsightChanged>(OnInsightChanged);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.InsightChanged>(OnInsightChanged);
        }

        // ========== 构建 UI ==========

        private void BuildUI()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("RunHUD", 45, transform);
            _root = canvas.gameObject;
            var ray = _root.GetComponent<GraphicRaycaster>();
            if (ray != null) Destroy(ray); // HUD 不交互

            // 顶部经验条
            _insightBar = new GameObject("InsightBar", typeof(RectTransform), typeof(Image)).GetComponent<Image>().gameObject;
            var ibrt = (RectTransform)_insightBar.transform;
            ibrt.SetParent(_root.transform, false);
            ibrt.anchorMin = new Vector2(0.5f, 1f); ibrt.anchorMax = new Vector2(0.5f, 1f); ibrt.pivot = new Vector2(0.5f, 1f);
            ibrt.anchoredPosition = new Vector2(0f, -12f); ibrt.sizeDelta = new Vector2(280f, 16f);
            _insightBar.GetComponent<Image>().color = new Color(0.78f, 0.68f, 1f, 0.95f);
            _insightBar.GetComponent<Image>().raycastTarget = false;
            _insightLabel = UGuiKit.CreateText(ibrt, "", 12, Color.white, TextAlignmentOptions.Center);
            var ilrt = (RectTransform)_insightLabel.transform; ilrt.anchorMin = Vector2.zero; ilrt.anchorMax = Vector2.one; ilrt.offsetMin = Vector2.zero; ilrt.offsetMax = Vector2.zero;

            // 等级历练
            _cultLabel = UGuiKit.CreateText(_root.transform, "", 12, new Color(0.7f, 0.85f, 1f), TextAlignmentOptions.Center);
            var crt = (RectTransform)_cultLabel.transform;
            crt.anchorMin = new Vector2(0.5f, 1f); crt.anchorMax = new Vector2(0.5f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -34f); crt.sizeDelta = new Vector2(320f, 18f);

            // 右上角道心/因果/寿元
            _moralPanel = new GameObject("MoralPanel", typeof(RectTransform), typeof(Image)).GetComponent<Image>().gameObject;
            var mrt = (RectTransform)_moralPanel.transform;
            mrt.SetParent(_root.transform, false);
            mrt.anchorMin = new Vector2(1f, 1f); mrt.anchorMax = new Vector2(1f, 1f); mrt.pivot = new Vector2(1f, 1f);
            mrt.anchoredPosition = new Vector2(-12f, -115f); mrt.sizeDelta = new Vector2(168f, 74f);
            _moralPanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.78f);
            _moralPanel.GetComponent<Image>().raycastTarget = false;
            var mv = _moralPanel.AddComponent<VerticalLayoutGroup>();
            mv.padding = new RectOffset(8, 8, 5, 5); mv.spacing = 2f;
            mv.childControlWidth = true; mv.childForceExpandWidth = true; mv.childControlHeight = true; mv.childForceExpandHeight = false;
            _daoxinLabel = UGuiKit.CreateText(mrt, "", 12, Color.white, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(_daoxinLabel, 18f);
            _karmaLabel = UGuiKit.CreateText(mrt, "", 12, Color.white, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(_karmaLabel, 18f);
            _lifespanLabel = UGuiKit.CreateText(mrt, "", 12, Color.white, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(_lifespanLabel, 18f);

            // 飘字区
            _toastRoot = new GameObject("Toasts", typeof(RectTransform)).GetComponent<RectTransform>();
            _toastRoot.SetParent(_root.transform, false);
            _toastRoot.anchorMin = new Vector2(0.5f, 1f); _toastRoot.anchorMax = new Vector2(0.5f, 1f); _toastRoot.pivot = new Vector2(0.5f, 1f);
            _toastRoot.anchoredPosition = new Vector2(0f, -80f); _toastRoot.sizeDelta = new Vector2(360f, 200f);
            var tv = _toastRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 0f; tv.childAlignment = TextAnchor.UpperCenter;
            tv.childControlWidth = true; tv.childForceExpandWidth = true; tv.childControlHeight = true; tv.childForceExpandHeight = false;
            for (int i = 0; i < ToastPoolMax; i++)
            {
                var l = UGuiKit.CreateText(_toastRoot, "", 14, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UGuiKit.SetHeight(l, 22f);
                l.gameObject.SetActive(false);
                _toastPool.Add(l);
            }

            _phaseRecapPanel = new GameObject(
                "PhaseRecapPanel",
                typeof(RectTransform),
                typeof(Image));
            var prt = (RectTransform)_phaseRecapPanel.transform;
            prt.SetParent(_root.transform, false);
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.zero;
            prt.pivot = Vector2.zero;
            prt.anchoredPosition = new Vector2(12f, 116f);
            prt.sizeDelta = new Vector2(340f, 84f);
            var recapBackground = _phaseRecapPanel.GetComponent<Image>();
            recapBackground.color = new Color(0.035f, 0.09f, 0.14f, 0.86f);
            recapBackground.raycastTarget = false;
            _phaseRecapLabel = UGuiKit.CreateText(
                prt,
                "",
                14,
                new Color(0.72f, 0.9f, 1f),
                TextAlignmentOptions.TopLeft,
                FontStyles.Normal);
            var recapRt = (RectTransform)_phaseRecapLabel.transform;
            recapRt.anchorMin = Vector2.zero;
            recapRt.anchorMax = Vector2.one;
            recapRt.offsetMin = new Vector2(12f, 8f);
            recapRt.offsetMax = new Vector2(-12f, -8f);
            _phaseRecapLabel.enableWordWrapping = true;
            _phaseRecapPanel.SetActive(false);
        }

        // ========== 经验飘字降噪 ==========

        private int _insightAccumDelta;
        private int _insightAccumCount;
        private int _insightLatestValue;
        private float _insightFlushTimer;
        private const int InsightFlushCount = 5;
        private const float InsightFlushWindow = 1.5f;

        private void OnInsightChanged(GameEvents.InsightChanged evt)
        {
            _insightAccumDelta += evt.Delta;
            _insightAccumCount++;
            _insightLatestValue = evt.NewRunInsight;
            _insightFlushTimer = InsightFlushWindow;

            if (_insightAccumCount >= InsightFlushCount)
                FlushInsightToast();
        }

        private void FlushInsightToast()
        {
            if (_insightAccumCount <= 0) return;
            string text = _insightAccumCount == 1
                ? $"+{_insightAccumDelta} 经验（累计 {_insightLatestValue}）"
                : $"+{_insightAccumDelta} 经验 ×{_insightAccumCount}（累计 {_insightLatestValue}）";
            _toasts.Add(new PickupToast
            {
                text = text,
                color = new Color(0.78f, 0.68f, 1f),
                remaining = ToastDuration * 0.55f
            });
            _insightAccumDelta = 0;
            _insightAccumCount = 0;
            _insightFlushTimer = 0f;
        }

        // ========== 逐帧更新 ==========

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.remaining -= Time.deltaTime;
                if (t.remaining <= 0f) _toasts.RemoveAt(i);
                else _toasts[i] = t;
            }

            if (_insightAccumCount > 0)
            {
                _insightFlushTimer -= Time.deltaTime;
                if (_insightFlushTimer <= 0f) FlushInsightToast();
            }

            bool show = !MainMenu.IsVisible;
            if (_root != null && _root.activeSelf != show) _root.SetActive(show);
            if (!show) return;

            RefreshInsightBar();
            RefreshCultivation();
            RefreshMoral();
            RefreshPhaseRecap();
            RefreshToasts();
        }

        private void RefreshInsightBar()
        {
            var insight = InsightSystem.Instance;
            bool show = insight != null && insight.RunInsight > 0;
            if (_insightBar.activeSelf != show) _insightBar.SetActive(show);
            if (show) _insightLabel.text = $"本局经验 {insight.RunInsight}";
        }

        private void RefreshCultivation()
        {
            bool show = FeatureFlags.EnableCaveMeta && CultivationSystem.Instance != null;
            if (_cultLabel.gameObject.activeSelf != show) _cultLabel.gameObject.SetActive(show);
            if (!show) return;
            var cult = CultivationSystem.Instance;
            int realm = cult.CurrentRealm;
            int quality = cult.GetRealmQuality(realm);
            string qStr = (quality >= 0 && quality < CultivationSystem.QualityNames.Length)
                ? "·" + CultivationSystem.QualityNames[quality] : "";
            _cultLabel.text = $"等级 · {cult.CurrentRealmName}{qStr}　历练 {cult.RunTempering}";
        }

        private void RefreshMoral()
        {
            var h = XianTu.LevelDesign.PlayerStateHooks.Instance;
            bool show = h != null;
            if (_moralPanel.activeSelf != show) _moralPanel.SetActive(show);
            if (!show) return;

            _daoxinLabel.color = h.Daoxin >= 80 ? new Color(0.42f, 0.75f, 1f)
                : h.Daoxin >= 50 ? new Color(0.85f, 0.88f, 0.9f)
                : h.Daoxin >= 20 ? new Color(1f, 0.69f, 0.38f) : new Color(1f, 0.33f, 0.38f);
            _daoxinLabel.text = $"意志 · {h.DaoxinState} {h.Daoxin}";

            bool showKarma = h.KarmaDebt != 0;
            _karmaLabel.gameObject.SetActive(showKarma);
            if (showKarma)
            {
                _karmaLabel.color = h.KarmaDebt > 0 ? new Color(1f, 0.53f, 0.4f) : new Color(0.56f, 0.82f, 0.56f);
                _karmaLabel.text = h.KarmaDebt > 0 ? $"恶业 +{h.KarmaDebt}" : $"善缘 {h.KarmaDebt}";
            }

            bool showLifespan = h.Lifespan != 100;
            _lifespanLabel.gameObject.SetActive(showLifespan);
            if (showLifespan)
            {
                _lifespanLabel.color = h.Lifespan < 30 ? new Color(1f, 0.67f, 0.4f) : new Color(0.78f, 0.81f, 0.85f);
                _lifespanLabel.text = $"寿元 {h.Lifespan} 年";
            }
        }

        private void RefreshToasts()
        {
            for (int i = 0; i < _toastPool.Count; i++)
            {
                if (i < _toasts.Count)
                {
                    var t = _toasts[i];
                    var c = t.color; c.a = Mathf.Clamp01(t.remaining / ToastDuration);
                    _toastPool[i].gameObject.SetActive(true);
                    _toastPool[i].text = t.text;
                    _toastPool[i].color = c;
                }
                else if (_toastPool[i].gameObject.activeSelf)
                {
                    _toastPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void RefreshPhaseRecap()
        {
            bool show = LevelAPhaseRuntime.IsNightMapActive;
            if (_phaseRecapPanel.activeSelf != show)
                _phaseRecapPanel.SetActive(show);
            if (!show)
                return;

            var lines = LevelAPhaseRuntime.GetRecapLines();
            _phaseRecapLabel.text = lines.Count == 0
                ? "<b>上次行动</b>\n未留下可追溯的场景变化"
                : $"<b>上次行动</b>\n· {string.Join("\n· ", lines)}";
        }
    }
}
