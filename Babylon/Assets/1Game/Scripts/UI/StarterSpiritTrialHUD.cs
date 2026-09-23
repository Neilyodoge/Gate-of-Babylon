using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>初契赋予教学的选择、对比、验证与自由试玩面板。</summary>
    public sealed class StarterSpiritTrialHUD : MonoBehaviour
    {
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _hint;
        private Button[] _speciesButtons;
        private TextMeshProUGUI[] _speciesLabels;
        private Button[] _carrierButtons;
        private TextMeshProUGUI[] _carrierLabels;
        private Button _assignmentButton;
        private TextMeshProUGUI _assignmentLabel;
        private Button _commitButton;
        private TextMeshProUGUI _commitLabel;
        private GameObject _controls;
        private Image _guidanceDim;

        public event Action<StableConfigId> SpeciesRequested;
        public event Action<CarrierSlot> CarrierRequested;
        public event Action AssignmentRequested;
        public event Action CommitRequested;

        public static StarterSpiritTrialHUD Create()
        {
            GameObject root = new(
                "StarterSpiritTrialHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 126;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            UGuiKit.EnsureEventSystem();
            StarterSpiritTrialHUD hud =
                root.AddComponent<StarterSpiritTrialHUD>();
            hud.Build();
            return hud;
        }

        public void ShowFirstAssignment(
            StableConfigId species,
            CarrierSlot? carrier)
        {
            _guidanceDim.gameObject.SetActive(true);
            _controls.SetActive(true);
            _assignmentButton.gameObject.SetActive(true);
            _commitButton.gameObject.SetActive(false);
            _title.text = "赋予教学 · 跟着亮处点击";
            _body.text =
                species.IsEmpty
                    ? "第1步：点击一只发亮的灵宠。"
                    : !carrier.HasValue
                        ? $"已选择 <color=#F2C66D>{NameOf(species)}</color>\n" +
                          "第2步：点击一个发亮的动作。"
                        : $"<color=#F2C66D>{NameOf(species)}</color> " +
                          $"将附着到 {CarrierName(carrier.Value)}\n" +
                          Effect(species, carrier.Value);
            _hint.text = !carrier.HasValue
                ? "只需要点击亮起来的位置"
                : "第3步：点击下方亮起的“应用组合”";
            bool choosingSpecies = species.IsEmpty;
            bool choosingCarrier =
                !choosingSpecies && !carrier.HasValue;
            ConfigureControls(
                species,
                carrier,
                _ => choosingSpecies,
                _ => choosingCarrier);
            ConfigureAssignment(
                species.IsEmpty || !carrier.HasValue
                    ? "先选择灵宠和动作"
                    : "应用这个组合",
                !species.IsEmpty && carrier.HasValue);
            ConfigureGuidance(
                choosingSpecies,
                choosingCarrier,
                !species.IsEmpty && carrier.HasValue);
        }

        public void ShowDifferentCarrier(
            StableConfigId currentSpecies,
            CarrierSlot currentCarrier,
            StableConfigId pendingSpecies,
            CarrierSlot? pendingCarrier)
        {
            _guidanceDim.gameObject.SetActive(false);
            _controls.SetActive(true);
            _assignmentButton.gameObject.SetActive(true);
            _commitButton.gameObject.SetActive(false);
            _title.text = "赋予教学 2 / 3 · 同宠换动作";
            bool valid =
                pendingSpecies == currentSpecies &&
                pendingCarrier.HasValue &&
                pendingCarrier.Value != currentCarrier;
            _body.text = !pendingCarrier.HasValue ||
                         pendingSpecies.IsEmpty
                ? "选择任意灵宠和动作可预览组合。"
                : $"刚才｜{CarrierName(currentCarrier)}\n" +
                  $"{NameOf(currentSpecies)}：" +
                  $"{Effect(currentSpecies, currentCarrier)}\n\n" +
                  $"预览｜{NameOf(pendingSpecies)}＋" +
                  $"{CarrierName(pendingCarrier.Value)}\n" +
                  $"{Effect(pendingSpecies, pendingCarrier.Value)}";
            _hint.text = valid
                ? "符合本步目标：保持灵宠，只改变动作"
                : $"可以浏览全部组合；确认时保持{NameOf(currentSpecies)}，" +
                  $"并选择不同于{CarrierName(currentCarrier)}的动作";
            ConfigureControls(
                pendingSpecies,
                pendingCarrier,
                _ => true,
                _ => true);
            ConfigureAssignment(
                valid
                    ? $"改附到 {CarrierName(pendingCarrier.Value)}"
                    : "预览中 · 按本步目标选择后可确认",
                valid);
        }

        public void ShowDifferentSpecies(
            StableConfigId currentSpecies,
            CarrierSlot currentCarrier,
            StableConfigId pendingSpecies,
            CarrierSlot pendingCarrier)
        {
            _guidanceDim.gameObject.SetActive(false);
            _controls.SetActive(true);
            _assignmentButton.gameObject.SetActive(true);
            _commitButton.gameObject.SetActive(false);
            _title.text = "赋予教学 3 / 3 · 同动作换宠";
            bool valid =
                pendingSpecies != currentSpecies &&
                pendingCarrier == currentCarrier;
            _body.text = pendingSpecies.IsEmpty
                ? "选择任意灵宠和动作可预览组合。"
                : $"刚才｜{NameOf(currentSpecies)}\n" +
                  $"{CarrierName(currentCarrier)}：" +
                  $"{Effect(currentSpecies, currentCarrier)}\n\n" +
                  $"预览｜{NameOf(pendingSpecies)}＋" +
                  $"{CarrierName(pendingCarrier)}\n" +
                  $"{Effect(pendingSpecies, pendingCarrier)}";
            _hint.text = valid
                ? "符合本步目标：保持动作，只改变灵宠"
                : $"可以浏览全部组合；确认时保持" +
                  $"{CarrierName(currentCarrier)}并选择另一只灵宠";
            ConfigureControls(
                pendingSpecies,
                pendingCarrier,
                _ => true,
                _ => true);
            ConfigureAssignment(
                valid
                    ? $"替换为 {NameOf(pendingSpecies)}"
                    : "预览中 · 按本步目标选择后可确认",
                valid);
        }

        public void ShowVerification(
            int step,
            StableConfigId species,
            CarrierSlot carrier,
            int progress,
            int hintLevel)
        {
            _guidanceDim.gameObject.SetActive(false);
            _controls.SetActive(false);
            _assignmentButton.gameObject.SetActive(false);
            _commitButton.gameObject.SetActive(false);
            _title.text =
                $"赋予教学 {step} / 3 · 触发显化";
            _body.text =
                $"<color=#F2C66D>{NameOf(species)}</color> 已附着到 " +
                $"{CarrierName(carrier)}\n" +
                $"{Effect(species, carrier)}" +
                (carrier == CarrierSlot.Weapon
                    ? $"\n\n当前命中进度　{Mathf.Min(progress, 3)} / 3"
                    : string.Empty);
            _hint.text = VerificationHint(carrier, hintLevel);
        }

        public void ShowGuidedSuccess(
            int step,
            StableConfigId previousSpecies,
            CarrierSlot previousCarrier,
            StableConfigId species,
            CarrierSlot carrier)
        {
            _guidanceDim.gameObject.SetActive(false);
            _controls.SetActive(false);
            _assignmentButton.gameObject.SetActive(false);
            _commitButton.gameObject.SetActive(false);
            _title.text = $"赋予教学 {step} / 3 · 显化成功";
            if (step == 1)
            {
                _body.text =
                    $"{NameOf(species)}已经能借" +
                    $"{CarrierName(carrier)}显化。\n" +
                    Effect(species, carrier);
                _hint.text = "灵宠可以附着在已有动作上";
                return;
            }

            _body.text =
                $"刚才｜{NameOf(previousSpecies)}＋" +
                $"{CarrierName(previousCarrier)}\n" +
                $"{Effect(previousSpecies, previousCarrier)}\n\n" +
                $"现在｜{NameOf(species)}＋{CarrierName(carrier)}\n" +
                Effect(species, carrier);
            _hint.text = step == 2
                ? "同一只灵宠附着不同动作，效果会改变"
                : "同一个动作附着不同灵宠，效果也会改变";
        }

        public void ShowFree(
            StableConfigId currentSpecies,
            CarrierSlot currentCarrier,
            IReadOnlyCollection<string> completed,
            int requiredCount,
            bool confirming,
            string comparisonSuggestion = "")
        {
            _guidanceDim.gameObject.SetActive(false);
            _controls.SetActive(true);
            _assignmentButton.gameObject.SetActive(false);
            _commitButton.gameObject.SetActive(true);
            int completedCount = Mathf.Min(
                completed?.Count ?? 0,
                requiredCount);
            bool objectiveComplete = completedCount >= requiredCount;
            _title.text =
                $"赋予试玩 · 使用不同组合 {completedCount} / " +
                $"{requiredCount}";
            _body.text = confirming
                ? $"确认结契｜" +
                  $"<color=#F2C66D>{NameOf(currentSpecies)}</color>\n" +
                  $"保留赋予｜{CarrierName(currentCarrier)}\n" +
                  $"{Effect(currentSpecies, currentCarrier)}"
                : $"当前生效｜" +
                  $"<color=#F2C66D>{NameOf(currentSpecies)}</color> " +
                  $"＋ {CarrierName(currentCarrier)}\n" +
                  $"{Effect(currentSpecies, currentCarrier)}";
            _hint.text = confirming
                ? "确认后，这只灵宠会成为当前伙伴；仍可点击卡片取消"
                : objectiveComplete
                    ? "点击灵宠或动作立即切换；也可以随时结契"
                    : (string.IsNullOrWhiteSpace(comparisonSuggestion)
                        ? string.Empty
                        : comparisonSuggestion + "\n") +
                      Instruction(currentCarrier);
            ConfigureControls(
                currentSpecies,
                currentCarrier,
                _ => true,
                _ => true,
                completed);
            ConfigureGuidance(false, false, false);
            _commitLabel.text = confirming
                ? "再次点击确认"
                : objectiveComplete
                    ? "选好了"
                    : $"再使用{requiredCount - completedCount}种组合";
            _commitButton.interactable = objectiveComplete;
            SetButtonColor(
                _commitButton,
                !objectiveComplete
                    ? new Color(0.11f, 0.14f, 0.13f)
                    : confirming
                    ? new Color(0.66f, 0.34f, 0.12f)
                    : new Color(0.20f, 0.43f, 0.29f));
        }

        private void ConfigureControls(
            StableConfigId species,
            CarrierSlot? carrier,
            Func<StableConfigId, bool> speciesEnabled,
            Func<CarrierSlot, bool> carrierEnabled,
            IReadOnlyCollection<string> completed = null)
        {
            for (int i = 0; i < _speciesButtons.Length; i++)
            {
                StarterSpiritChoiceProfile profile =
                    StarterSpiritChoicePresentation.Profiles[i];
                bool selected = profile.SpeciesId == species;
                bool anyDone = completed != null &&
                    (completed.Contains(
                         Key(profile.SpeciesId, CarrierSlot.Weapon)) ||
                     completed.Contains(
                         Key(profile.SpeciesId, CarrierSlot.TechniqueQ)) ||
                     completed.Contains(
                         Key(profile.SpeciesId, CarrierSlot.Mobility)));
                _speciesLabels[i].text =
                    $"{(anyDone ? "✓ " : string.Empty)}" +
                    profile.DisplayName;
                _speciesButtons[i].interactable =
                    speciesEnabled(profile.SpeciesId);
                SetButtonColor(
                    _speciesButtons[i],
                    selected
                        ? new Color(0.48f, 0.34f, 0.15f)
                        : new Color(0.16f, 0.22f, 0.20f));
            }
            CarrierSlot[] carriers =
            {
                CarrierSlot.Weapon,
                CarrierSlot.TechniqueQ,
                CarrierSlot.Mobility
            };
            for (int i = 0; i < carriers.Length; i++)
            {
                bool selected =
                    carrier.HasValue && carriers[i] == carrier.Value;
                bool done = completed != null &&
                    !species.IsEmpty &&
                    completed.Contains(Key(species, carriers[i]));
                _carrierLabels[i].text =
                    $"{(done ? "✓ " : string.Empty)}" +
                    CarrierName(carriers[i]);
                _carrierButtons[i].interactable =
                    carrierEnabled(carriers[i]);
                SetButtonColor(
                    _carrierButtons[i],
                    selected
                        ? new Color(0.42f, 0.29f, 0.13f)
                        : new Color(0.14f, 0.19f, 0.18f));
            }
        }

        private void ConfigureAssignment(string label, bool interactable)
        {
            _assignmentLabel.text = label;
            _assignmentButton.interactable = interactable;
            SetButtonColor(
                _assignmentButton,
                interactable
                    ? new Color(0.42f, 0.29f, 0.13f)
                    : new Color(0.11f, 0.14f, 0.13f));
        }

        private void ConfigureGuidance(
            bool speciesStep,
            bool carrierStep,
            bool assignmentStep)
        {
            foreach (Button button in _speciesButtons)
                SetGuideButton(button, speciesStep);
            foreach (Button button in _carrierButtons)
                SetGuideButton(button, carrierStep);
            SetGuideButton(_assignmentButton, assignmentStep);
        }

        private static void SetGuideButton(Button button, bool highlighted)
        {
            if (button == null)
                return;
            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = highlighted;
                outline.effectColor =
                    new Color(1f, 0.76f, 0.22f, 0.95f);
                outline.effectDistance = new Vector2(4f, -4f);
            }
            if (highlighted)
            {
                SetButtonColor(
                    button,
                    new Color(0.72f, 0.45f, 0.10f, 1f));
            }
        }

        public void ShowError(string message)
        {
            _hint.text = $"<color=#F28B72>{message}</color>";
        }

        public static string Key(
            StableConfigId species,
            CarrierSlot carrier)
            => $"{species.Value}:{(int)carrier}";

        private void Build()
        {
            GameObject dim = new(
                "GuidanceDim",
                typeof(RectTransform),
                typeof(Image));
            dim.transform.SetParent(transform, false);
            RectTransform dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            _guidanceDim = dim.GetComponent<Image>();
            _guidanceDim.color = new Color(0f, 0f, 0f, 0.62f);
            _guidanceDim.raycastTarget = false;

            GameObject panel = new(
                "TrialPanel",
                typeof(RectTransform),
                typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.55f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-28f, 0f);
            rect.sizeDelta = new Vector2(500f, 540f);
            panel.GetComponent<Image>().color =
                new Color(0.045f, 0.065f, 0.06f, 0.95f);

            _title = CreateText(
                panel.transform,
                "Title",
                new Vector2(24f, 478f),
                new Vector2(-24f, -18f),
                25f,
                FontStyles.Bold);
            _title.color = new Color(0.96f, 0.76f, 0.36f);
            _body = CreateText(
                panel.transform,
                "Body",
                new Vector2(24f, 300f),
                new Vector2(-24f, -72f),
                19f,
                FontStyles.Normal);
            _body.lineSpacing = 9f;
            _hint = CreateText(
                panel.transform,
                "Hint",
                new Vector2(24f, 256f),
                new Vector2(-24f, -244f),
                14f,
                FontStyles.Normal);
            _hint.color = new Color(0.67f, 0.79f, 0.72f);

            _controls = new GameObject(
                "TrialControls",
                typeof(RectTransform));
            _controls.transform.SetParent(panel.transform, false);
            RectTransform controls =
                _controls.GetComponent<RectTransform>();
            controls.anchorMin = Vector2.zero;
            controls.anchorMax = Vector2.one;
            controls.offsetMin = new Vector2(20f, 132f);
            controls.offsetMax = new Vector2(-20f, -280f);

            _speciesButtons = new Button[3];
            _speciesLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                StarterSpiritChoiceProfile profile =
                    StarterSpiritChoicePresentation.Profiles[i];
                _speciesButtons[i] = CreateButton(
                    _controls.transform,
                    $"Species_{i}",
                    profile.DisplayName,
                    new Vector2(i / 3f, 0.55f),
                    new Vector2((i + 1f) / 3f, 1f),
                    () => SpeciesRequested?.Invoke(
                        StarterSpiritChoicePresentation
                            .Profiles[captured].SpeciesId));
                _speciesLabels[i] =
                    _speciesButtons[i]
                        .GetComponentInChildren<TextMeshProUGUI>();
            }

            CarrierSlot[] carriers =
            {
                CarrierSlot.Weapon,
                CarrierSlot.TechniqueQ,
                CarrierSlot.Mobility
            };
            _carrierButtons = new Button[3];
            _carrierLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < carriers.Length; i++)
            {
                CarrierSlot captured = carriers[i];
                _carrierButtons[i] = CreateButton(
                    _controls.transform,
                    $"Carrier_{i}",
                    CarrierName(captured),
                    new Vector2(i / 3f, 0.08f),
                    new Vector2((i + 1f) / 3f, 0.50f),
                    () => CarrierRequested?.Invoke(captured));
                _carrierLabels[i] =
                    _carrierButtons[i]
                        .GetComponentInChildren<TextMeshProUGUI>();
            }

            _assignmentButton = CreateButton(
                panel.transform,
                "Assignment",
                "确认附着",
                new Vector2(0.12f, 0f),
                new Vector2(0.88f, 0f),
                () => AssignmentRequested?.Invoke());
            RectTransform assignmentRect =
                _assignmentButton.GetComponent<RectTransform>();
            assignmentRect.pivot = new Vector2(0.5f, 0f);
            assignmentRect.anchoredPosition = new Vector2(0f, 76f);
            assignmentRect.sizeDelta = new Vector2(0f, 48f);
            _assignmentLabel =
                _assignmentButton.GetComponentInChildren<TextMeshProUGUI>();

            _commitButton = CreateButton(
                panel.transform,
                "Commit",
                "结契",
                new Vector2(0.20f, 0f),
                new Vector2(0.80f, 0f),
                () => CommitRequested?.Invoke());
            RectTransform commitRect =
                _commitButton.GetComponent<RectTransform>();
            commitRect.pivot = new Vector2(0.5f, 0f);
            commitRect.anchoredPosition = new Vector2(0f, 18f);
            commitRect.sizeDelta = new Vector2(0f, 50f);
            _commitLabel =
                _commitButton.GetComponentInChildren<TextMeshProUGUI>();
            _controls.SetActive(false);
            _assignmentButton.gameObject.SetActive(false);
            _commitButton.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            Vector2 offsetMin,
            Vector2 offsetMax,
            float size,
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
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Action callback)
        {
            GameObject go = new(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            Button button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => callback());
            go.GetComponent<Outline>().enabled = false;
            SetButtonColor(
                button,
                new Color(0.15f, 0.21f, 0.19f));
            TextMeshProUGUI text = CreateText(
                go.transform,
                "Label",
                new Vector2(6f, 6f),
                new Vector2(-6f, -6f),
                16f,
                FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            return button;
        }

        private static void SetButtonColor(
            Button button,
            Color color)
        {
            if (button != null)
                button.GetComponent<Image>().color = color;
        }

        public static string NameOf(StableConfigId species)
        {
            return StarterSpiritChoicePresentation.TryGetProfile(
                species,
                out StarterSpiritChoiceProfile profile)
                ? profile.DisplayName
                : "灵宠";
        }

        public static string CarrierName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.TechniqueQ => "Q 灵息弹",
                CarrierSlot.Mobility => "SPACE 身法",
                _ => "LMB 普攻"
            };
        }

        private static string WeaponEffect(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "连续命中3次，第3击产生一团火花";
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return "连续命中3次，延迟复响刚才的普攻";
            return "连续命中3次，攻击弹向附近第二个目标";
        }

        private static string Effect(
            StableConfigId species,
            CarrierSlot carrier)
        {
            if (carrier == CarrierSlot.Weapon)
                return WeaponEffect(species);
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return carrier == CarrierSlot.TechniqueQ
                    ? "灵息弹命中时产生一颗更明显的火花"
                    : "闪避终点留下短暂范围火花";
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return carrier == CarrierSlot.TechniqueQ
                    ? "灵息弹命中后，延迟复响一发弱化灵息弹"
                    : "闪避起点留下回声，随后产生范围脉冲";
            return carrier == CarrierSlot.TechniqueQ
                ? "灵息弹命中后额外弹向第二个目标"
                : "闪避终点产生弹力冲击，推开附近目标";
        }

        private static string Instruction(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.TechniqueQ =>
                    "瞄准木靶按Q；观察命中后的追加显化",
                CarrierSlot.Mobility =>
                    "靠近木靶按SPACE；观察身法起点或终点",
                _ => "靠近木靶连续点击LMB；第3次命中触发"
            };
        }

        private static string VerificationHint(
            CarrierSlot carrier,
            int hintLevel)
        {
            if (hintLevel >= 4)
            {
                return "<color=#F2C66D>辅助已开启：</color>" +
                       "下一次有效动作必定触发教学显化";
            }
            if (hintLevel >= 3)
            {
                return carrier switch
                {
                    CarrierSlot.TechniqueQ =>
                        "让灵息弹直接命中发光木靶",
                    CarrierSlot.Mobility =>
                        "靠近木靶后按SPACE完成一次身法",
                    _ => "对主木靶连续完成3次有效普攻命中"
                };
            }
            if (hintLevel >= 2)
                return $"请按 {InputName(carrier)}，发光目标会提示有效范围";
            if (hintLevel >= 1)
                return Instruction(carrier);
            return Instruction(carrier);
        }

        private static string InputName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.TechniqueQ => "Q",
                CarrierSlot.Mobility => "SPACE",
                _ => "LMB"
            };
        }
    }
}
