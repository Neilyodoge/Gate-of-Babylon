using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 序章战斗专用的非阻塞步骤面板。
    /// 只显示当前教学任务，不覆盖顶部目标或底部技能栏。
    /// </summary>
    public sealed class StarterPrologueCombatGuideHUD : MonoBehaviour
    {
        private static StarterPrologueCombatGuideHUD _instance;

        private RectTransform _panel;
        private CanvasGroup _group;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _hint;
        private float _autoHideRemaining = -1f;
        private bool _fadingOut;

        public static StarterPrologueCombatGuideHUD EnsureExists()
        {
            if (_instance != null)
                return _instance;

            GameObject root = new(
                "StarterPrologueCombatGuideHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 124;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance =
                root.AddComponent<StarterPrologueCombatGuideHUD>();
            _instance.Build();
            return _instance;
        }

        public void ShowPrimer(
            bool attackDone,
            bool techniqueDone,
            bool dodgeDone)
        {
            ShowPersistent(
                "基础战斗 · 每项试一次",
                $"{Checklist(attackDone)}  LMB　普攻　近身连续攻击\n" +
                $"{Checklist(techniqueDone)}  Q　灵息弹　中距离直线攻击\n" +
                $"{Checklist(dodgeDone)}  SPACE　闪避　快速拉开距离",
                "可任意顺序；完成一项会立即打勾");
        }

        public void ShowPrimerComplete(bool autoReleased)
        {
            ShowTransient(
                autoReleased ? "继续前进" : "基础动作已掌握",
                autoReleased
                    ? "教学已自动放行，不会阻塞战斗"
                    : "躁动灵宠正在靠近",
                string.Empty,
                1.2f);
        }

        public void ShowCarrierPractice(
            CarrierSlot carrier,
            StableConfigId species,
            int progress)
        {
            string name = SpiritName(species);
            string instruction =
                CarrierInstruction(carrier, species, progress);
            ShowPersistent(
                "让灵宠首次显化",
                $"<color=#F2C66D>{name}</color> · " +
                $"{CarrierName(carrier)}\n{instruction}",
                $"按提示命中即可触发：{SpeciesEffect(species, carrier)}");
        }

        public void ShowCarrierReminder(
            CarrierSlot carrier,
            StableConfigId species)
        {
            ShowPersistent(
                "试试当前附着",
                CarrierReminder(carrier, species),
                "提示只出现一次，不会暂停战斗");
            Pulse();
        }

        public void ShowCarrierComplete(
            StableConfigId species,
            CarrierSlot carrier)
        {
            ShowTransient(
                "灵宠显化成功",
                $"{SpiritName(species)}已响应" +
                $"{CarrierName(carrier)}",
                "继续击退躁动灵宠",
                1.8f);
        }

        public void ShowBossRule()
        {
            ShowTransient(
                "刃铠灵",
                "削弱凶势，迫使它撤退",
                "它不是结契目标，也不需要击杀",
                2.6f);
        }

        public void Hide()
        {
            if (_panel == null || !_panel.gameObject.activeSelf)
                return;
            _autoHideRemaining = -1f;
            _fadingOut = true;
        }

        private void Build()
        {
            GameObject panelObject = new(
                "CombatGuidePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.anchorMin = _panel.anchorMax =
                new Vector2(1f, 0.57f);
            _panel.pivot = new Vector2(1f, 0.5f);
            _panel.anchoredPosition = new Vector2(-28f, 0f);
            _panel.sizeDelta = new Vector2(390f, 222f);
            Image background = panelObject.GetComponent<Image>();
            background.color =
                new Color(0.055f, 0.075f, 0.07f, 0.93f);
            background.raycastTarget = false;
            _group = panelObject.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            Image accent = CreateAccent(_panel);
            accent.color = new Color(0.94f, 0.67f, 0.28f, 1f);

            _title = CreateText(
                _panel,
                "Title",
                new Vector2(30f, 166f),
                new Vector2(-22f, -16f),
                22f,
                FontStyles.Bold);
            _title.color =
                new Color(0.96f, 0.77f, 0.39f, 1f);
            _body = CreateText(
                _panel,
                "Body",
                new Vector2(30f, 54f),
                new Vector2(-22f, -58f),
                18f,
                FontStyles.Normal);
            _body.lineSpacing = 12f;
            _hint = CreateText(
                _panel,
                "Hint",
                new Vector2(30f, 15f),
                new Vector2(-22f, -174f),
                13f,
                FontStyles.Normal);
            _hint.color =
                new Color(0.65f, 0.76f, 0.70f, 1f);
            _panel.gameObject.SetActive(false);
        }

        private void ShowPersistent(
            string title,
            string body,
            string hint)
        {
            SetContent(title, body, hint);
            _autoHideRemaining = -1f;
        }

        private void ShowTransient(
            string title,
            string body,
            string hint,
            float seconds)
        {
            SetContent(title, body, hint);
            _autoHideRemaining = Mathf.Max(0.2f, seconds);
        }

        private void SetContent(
            string title,
            string body,
            string hint)
        {
            _title.text = title ?? string.Empty;
            _body.text = body ?? string.Empty;
            _hint.text = hint ?? string.Empty;
            _fadingOut = false;
            _group.alpha = 1f;
            _panel.localScale = Vector3.one;
            _panel.gameObject.SetActive(true);
        }

        private void Pulse()
        {
            if (_panel != null)
                _panel.localScale = Vector3.one * 1.035f;
        }

        private void Update()
        {
            if (_panel == null || !_panel.gameObject.activeSelf)
                return;

            _panel.localScale = Vector3.Lerp(
                _panel.localScale,
                Vector3.one,
                9f * Time.unscaledDeltaTime);
            if (_autoHideRemaining > 0f)
            {
                _autoHideRemaining -= Time.unscaledDeltaTime;
                if (_autoHideRemaining <= 0f)
                    _fadingOut = true;
            }

            if (!_fadingOut)
                return;
            _group.alpha -= Time.unscaledDeltaTime * 4.5f;
            if (_group.alpha <= 0f)
            {
                _group.alpha = 0f;
                _panel.gameObject.SetActive(false);
                _fadingOut = false;
            }
        }

        private static string Checklist(bool done)
        {
            return done
                ? "<color=#77D6A4>✓</color>"
                : "<color=#718078>○</color>";
        }

        private static string SpiritName(StableConfigId species)
        {
            return StarterSpiritChoicePresentation.TryGetProfile(
                species,
                out StarterSpiritChoiceProfile profile)
                ? profile.DisplayName
                : "灵宠";
        }

        private static string CarrierName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.Weapon => "武器普攻",
                CarrierSlot.TechniqueQ => "Q灵息弹",
                CarrierSlot.Mobility => "SPACE身法",
                _ => "当前动作"
            };
        }

        private static string CarrierInstruction(
            CarrierSlot carrier,
            StableConfigId species,
            int progress)
        {
            if (carrier == CarrierSlot.Weapon)
            {
                int required =
                    StarterSpiritCarrierRuntime
                        .WeaponHitsPerActivation;
                return $"连续命中敌人　{Mathf.Min(progress, required)} / {required}";
            }
            if (carrier == CarrierSlot.TechniqueQ)
                return "用灵息弹命中一只敌人";
            if (carrier == CarrierSlot.Mobility)
                return "在敌人附近完成一次闪避";
            return "使用当前附着动作";
        }

        private static string CarrierReminder(
            CarrierSlot carrier,
            StableConfigId species)
        {
            string effect = SpeciesEffect(species, carrier);
            return carrier switch
            {
                CarrierSlot.Weapon =>
                    $"靠近敌人连续点击LMB\n第3次命中会{effect}",
                CarrierSlot.TechniqueQ =>
                    $"瞄准敌人按Q释放灵息弹\n命中后会{effect}",
                CarrierSlot.Mobility =>
                    $"靠近敌人按SPACE闪避\n身法结束时会{effect}",
                _ => "使用已附着的动作触发灵宠效果"
            };
        }

        private static string SpeciesEffect(
            StableConfigId species,
            CarrierSlot carrier)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                return carrier == CarrierSlot.TechniqueQ
                    ? "产生强化火花"
                    : carrier == CarrierSlot.Mobility
                        ? "产生范围火花"
                        : "产生火花";
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return carrier == CarrierSlot.TechniqueQ
                    ? "延迟复响"
                    : carrier == CarrierSlot.Mobility
                        ? "产生回声脉冲"
                        : "复响攻击";
            }
            return carrier == CarrierSlot.TechniqueQ
                ? "额外弹射"
                : carrier == CarrierSlot.Mobility
                    ? "产生弹力脉冲"
                    : "弹向附近敌人";
        }

        private static Image CreateAccent(Transform parent)
        {
            GameObject go = new(
                "Accent",
                typeof(RectTransform),
                typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(6f, 0f);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
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
            text.color = new Color(0.92f, 0.94f, 0.90f, 1f);
            text.enableWordWrapping = true;
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
