using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 战斗技能栏上的灵宠挂件与瞬时回路反馈。
    /// 技能槽仍是主体；本组件只读取存档与回路事件。
    /// </summary>
    public sealed class SpiritCircuitHUD : MonoBehaviour
    {
        private readonly List<Image> _circuitLines = new();
        private readonly List<Image> _heatPips = new();
        private readonly Dictionary<Image, Color>
            _selectionBorderColors = new();
        private RectTransform[] _skillSlots;
        private RectTransform _starterBadge;
        private GameObject _attachmentPanel;
        private TextMeshProUGUI _attachmentHint;
        private readonly TextMeshProUGUI[] _attachmentCardLabels =
            new TextMeshProUGUI[3];
        private TextMeshProUGUI _circuitStageHint;
        private Image _stageBorder;
        private Color _stageBorderOriginal;
        private SpiritCircuitPendingStage _pendingStage;
        private CarrierSlot _nextCarrier;
        private float _stageRemaining;
        private int _stageHeat;
        private int _stageHeatRequired;
        private float _flashRemaining;
        private float _attachmentErrorUntil;
        private int _heat;
        private bool _started;
        private bool _attachmentSelecting;
        private bool _attachmentRequired;

        public static bool IsAttachmentSelectionActive { get; private set; }

        public void Configure(RectTransform[] skillSlots)
        {
            _skillSlots = skillSlots;
            if (_started)
                Rebuild();
        }

        public bool TryBeginPrologueAttachmentSelection()
        {
            if (!StarterPrologueProgression.RequiresFirstAttachment(
                    SaveSystem.Instance.Data) ||
                _starterBadge == null)
            {
                return false;
            }
            if (_attachmentSelecting)
                return _attachmentRequired;

            BeginAttachmentSelection(true);
            return true;
        }

        public static int VisualIndexForCarrier(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.TechniqueQ => 0,
                CarrierSlot.TechniqueE => 1,
                CarrierSlot.TechniqueR => 2,
                CarrierSlot.Mobility => 3,
                CarrierSlot.Weapon => 4,
                _ => -1
            };
        }

        public static bool TryCarrierForVisualIndex(
            int visualIndex,
            out CarrierSlot carrier)
        {
            switch (visualIndex)
            {
                case 4:
                    carrier = CarrierSlot.Weapon;
                    return true;
                case 0:
                    carrier = CarrierSlot.TechniqueQ;
                    return true;
                case 3:
                    carrier = CarrierSlot.Mobility;
                    return true;
                default:
                    carrier = default;
                    return false;
            }
        }

        private void Start()
        {
            _started = true;
            GameEvents.Subscribe<GameEvents.FirstPetCircuitAdvanced>(
                OnCircuitAdvanced);
            GameEvents.Subscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnStarterAttachmentChanged);
            GameEvents.Subscribe<GameEvents.SpiritLoadoutChanged>(
                OnSpiritLoadoutChanged);
            GameEvents.Subscribe<GameEvents.StarterSpiritCarrierAdvanced>(
                OnStarterCarrierAdvanced);
            CreateAttachmentHint();
            CreateCircuitStageHint();
            Rebuild();
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.FirstPetCircuitAdvanced>(
                OnCircuitAdvanced);
            GameEvents.Unsubscribe<GameEvents.StarterSpiritAttachmentChanged>(
                OnStarterAttachmentChanged);
            GameEvents.Unsubscribe<GameEvents.SpiritLoadoutChanged>(
                OnSpiritLoadoutChanged);
            GameEvents.Unsubscribe<GameEvents.StarterSpiritCarrierAdvanced>(
                OnStarterCarrierAdvanced);
            EndAttachmentSelection();
        }

        private void Update()
        {
            HandleAttachmentInput();
            UpdateCircuitStageCountdown();
            if (_flashRemaining <= 0f)
                return;

            _flashRemaining -= Time.unscaledDeltaTime;
            float strength = Mathf.Clamp01(_flashRemaining / 0.45f);
            foreach (Image line in _circuitLines)
            {
                if (line == null)
                    continue;
                Color color = line.color;
                color.a = Mathf.Lerp(0.12f, 0.85f, strength);
                line.color = color;
            }
        }

        private void OnCircuitAdvanced(
            GameEvents.FirstPetCircuitAdvanced evt)
        {
            _heat = Mathf.Clamp(
                evt.Heat,
                0,
                FirstSpiritHeatRuntime.DirectHitsPerSpark);
            UpdateHeatPips();
            UpdateCircuitStage(evt);
            if (evt.ChainStarted)
                _flashRemaining = 0.45f;
        }

        private void OnStarterAttachmentChanged(
            GameEvents.StarterSpiritAttachmentChanged evt)
        {
            Rebuild();
        }

        private void OnSpiritLoadoutChanged(
            GameEvents.SpiritLoadoutChanged evt)
        {
            Rebuild();
        }

        private void OnStarterCarrierAdvanced(
            GameEvents.StarterSpiritCarrierAdvanced evt)
        {
            _heat = Mathf.Clamp(
                evt.Progress,
                0,
                FirstSpiritHeatRuntime.DirectHitsPerSpark);
            UpdateHeatPips();
            if (evt.Activated)
                _flashRemaining = 0.45f;
        }

        private void Rebuild()
        {
            if (_skillSlots == null || _skillSlots.Length < 5)
                return;

            EndAttachmentSelection();
            ClearStageBorder();
            ClearVisuals();
            SaveDataV1 save = SaveSystem.Instance.Data;
            if (save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null)
            {
                return;
            }

            var roster = new Dictionary<Guid, SpiritInstanceSave>();
            foreach (SpiritInstanceSave entry in save.spiritRoster)
            {
                if (entry != null &&
                    Guid.TryParse(entry.instanceGuid, out Guid id) &&
                    id != Guid.Empty)
                {
                    roster[id] = entry;
                }
            }

            var attached = new Dictionary<StableConfigId, RectTransform>();
            StarterSpiritCarrierController starterController =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        StarterSpiritCarrierController>()
                    : null;
            FirstSpiritCircuitController circuitController =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        FirstSpiritCircuitController>()
                    : null;
            foreach (string activeGuid
                     in save.activeSpiritInstanceGuids)
            {
                if (!Guid.TryParse(activeGuid, out Guid id) ||
                    !roster.TryGetValue(id, out SpiritInstanceSave entry) ||
                    !SpiritSaveMapper.TryRestore(
                        entry,
                        out SpiritInstanceState spirit))
                {
                    continue;
                }

                CarrierSlot carrier;
                if (starterController?.Spirit != null &&
                    starterController.Spirit.Identity.InstanceId ==
                    spirit.Identity.InstanceId)
                {
                    carrier = starterController.Attachment;
                }
                else if (circuitController?.Loadout != null &&
                         circuitController.Loadout.TryGetCarrier(
                             spirit.Identity.InstanceId,
                             out CarrierSlot runtimeCarrier))
                {
                    carrier = runtimeCarrier;
                }
                else if (!FirstSpiritCircuitLoadoutProvider
                              .TryResolveCarrier(
                                  spirit.Identity.SpeciesConfigId,
                                  out carrier))
                {
                    continue;
                }

                int index = VisualIndexForCarrier(carrier);
                if (index < 0 || index >= _skillSlots.Length)
                    continue;

                RectTransform badge = CreateBadge(
                    _skillSlots[index],
                    spirit.Identity.SpeciesConfigId);
                attached[spirit.Identity.SpeciesConfigId] = badge;
                if (starterController?.Spirit != null &&
                    starterController.Spirit.Identity.InstanceId ==
                    spirit.Identity.InstanceId)
                {
                    _starterBadge = badge;
                }
            }

            if (attached.TryGetValue(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    out RectTransform spark) &&
                attached.TryGetValue(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    out RectTransform echo))
            {
                CreateCircuitLine(spark, echo);
            }
            if (attached.TryGetValue(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    out echo) &&
                attached.TryGetValue(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    out RectTransform bounce))
            {
                CreateCircuitLine(echo, bounce);
            }
            UpdateHeatPips();
            ApplyCircuitStageVisual();
        }

        private void HandleAttachmentInput()
        {
            if (_starterBadge == null)
                return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (_attachmentSelecting &&
                keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_attachmentRequired)
                {
                    if (_attachmentHint != null)
                        _attachmentHint.text =
                            "先选择LMB、Q或SPACE完成首次附着";
                    return;
                }
                EndAttachmentSelection();
                return;
            }
            if (_attachmentSelecting && keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    TrySelectAttachment(CarrierSlot.Weapon);
                    return;
                }
                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    TrySelectAttachment(CarrierSlot.TechniqueQ);
                    return;
                }
                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    TrySelectAttachment(CarrierSlot.Mobility);
                    return;
                }
            }
            if (mouse == null)
                return;

            Vector2 position = mouse.position.ReadValue();
            if (_attachmentSelecting)
                UpdateAttachmentHint(position);
            if (!mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (!_attachmentSelecting)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        _starterBadge,
                        position,
                        null))
                {
                    BeginAttachmentSelection(false);
                }
                return;
            }

            int index = FindAttachmentSlot(position);
            if (!TryCarrierForVisualIndex(
                    index,
                    out CarrierSlot carrier))
            {
                if (!_attachmentRequired)
                    EndAttachmentSelection();
                return;
            }

            TrySelectAttachment(carrier);
        }

        private void TrySelectAttachment(CarrierSlot carrier)
        {
            if (!_attachmentSelecting)
                return;
            StarterSpiritCarrierController controller =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        StarterSpiritCarrierController>()
                    : null;
            StarterSpiritAttachmentResult result =
                controller != null
                    ? controller.TryAttach(carrier)
                    : StarterSpiritAttachmentResult.UnsupportedCarrier;
            if (result == StarterSpiritAttachmentResult.InCombat)
            {
                if (_attachmentHint != null)
                    _attachmentHint.text = "战斗中不能换挂";
                _attachmentErrorUntil =
                    Time.unscaledTime + 1.2f;
                return;
            }
            EndAttachmentSelection();
        }

        private void BeginAttachmentSelection(bool required)
        {
            _attachmentSelecting = true;
            _attachmentRequired = required;
            IsAttachmentSelectionActive = true;
            if (required)
                SetAttachmentCameraFocus(true);
            _selectionBorderColors.Clear();
            int[] indices = { 4, 0, 3 };
            foreach (int index in indices)
            {
                Image border = _skillSlots[index]
                    ?.Find($"SkillBorder_{index}")
                    ?.GetComponent<Image>();
                if (border == null)
                    continue;
                _selectionBorderColors[border] = border.color;
                border.color = new Color(1f, 0.72f, 0.24f, 1f);
            }
            if (_attachmentHint != null)
            {
                _attachmentHint.text =
                    required
                        ? "首次附着 · 选择一个动作"
                        : "重新选择附着动作";
            }
            UpdateAttachmentCards();
            _attachmentPanel?.SetActive(true);
        }

        private void EndAttachmentSelection()
        {
            foreach (KeyValuePair<Image, Color> entry
                     in _selectionBorderColors)
            {
                if (entry.Key != null)
                    entry.Key.color = entry.Value;
            }
            _selectionBorderColors.Clear();
            _attachmentSelecting = false;
            _attachmentRequired = false;
            IsAttachmentSelectionActive = false;
            SetAttachmentCameraFocus(false);
            _attachmentErrorUntil = 0f;
            _attachmentPanel?.SetActive(false);
        }

        private static void SetAttachmentCameraFocus(bool active)
        {
            TopDownCamera camera =
                FindFirstObjectByType<TopDownCamera>();
            camera?.SetInteractionFocus(active);
        }

        private int FindAttachmentSlot(Vector2 screenPosition)
        {
            int[] indices = { 4, 0, 3 };
            foreach (int index in indices)
            {
                RectTransform slot = _skillSlots[index];
                if (slot != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        slot,
                        screenPosition,
                        null))
                {
                    return index;
                }
            }
            return -1;
        }

        private void UpdateAttachmentHint(Vector2 screenPosition)
        {
            if (_attachmentHint == null)
                return;
            if (Time.unscaledTime < _attachmentErrorUntil)
                return;

            int index = FindAttachmentSlot(screenPosition);
            StarterSpiritCarrierController controller =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        StarterSpiritCarrierController>()
                    : null;
            StableConfigId species =
                controller?.Spirit?.Identity.SpeciesConfigId ?? default;
            _attachmentHint.text = AttachmentDescription(
                species,
                index);
        }

        private static string AttachmentDescription(
            StableConfigId species,
            int visualIndex)
        {
            if (visualIndex < 0)
                return "选择下方动作卡，或按1／2／3";

            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                return visualIndex switch
                {
                    4 => "LMB · 连续3次命中产生火花",
                    0 => "Q灵息弹 · 命中产生强化火花",
                    3 => "SPACE · 身法终点产生范围火花",
                    _ => ""
                };
            }
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return visualIndex switch
                {
                    4 => "LMB · 连续3次命中后复响攻击",
                    0 => "Q灵息弹 · 命中后延迟复响",
                    3 => "SPACE · 身法结束后产生回声脉冲",
                    _ => ""
                };
            }
            return visualIndex switch
            {
                4 => "LMB · 连续3次命中后弹向近敌",
                0 => "Q灵息弹 · 命中后额外弹射",
                3 => "SPACE · 身法终点产生弹力脉冲",
                _ => ""
            };
        }

        private void CreateAttachmentHint()
        {
            RectTransform panel = UGuiKit.CreatePanel(
                transform,
                "AttachmentSelectionPanel",
                new Vector2(720f, 142f),
                new Color(0.09f, 0.085f, 0.065f, 0.94f));
            panel.anchorMin = panel.anchorMax =
                new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 122f);
            _attachmentPanel = panel.gameObject;

            _attachmentHint = UGuiKit.CreateText(
                panel,
                "",
                17,
                new Color(1f, 0.84f, 0.55f, 0.98f),
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            RectTransform hintRect = _attachmentHint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -10f);
            hintRect.sizeDelta = new Vector2(-24f, 28f);
            if (UGuiKit.CjkFont != null)
                _attachmentHint.font = UGuiKit.CjkFont;
            _attachmentHint.outlineColor =
                new Color(0.12f, 0.08f, 0.04f, 0.9f);
            _attachmentHint.outlineWidth = 0.15f;
            _attachmentHint.raycastTarget = false;

            CarrierSlot[] carriers =
            {
                CarrierSlot.Weapon,
                CarrierSlot.TechniqueQ,
                CarrierSlot.Mobility
            };
            Color[] colors =
            {
                new(0.42f, 0.27f, 0.14f, 1f),
                new(0.18f, 0.36f, 0.46f, 1f),
                new(0.19f, 0.40f, 0.30f, 1f)
            };
            for (int i = 0; i < carriers.Length; i++)
            {
                CarrierSlot carrier = carriers[i];
                Button button = UGuiKit.CreateButton(
                    panel,
                    "",
                    () => TrySelectAttachment(carrier),
                    colors[i],
                    14,
                    new Vector2(216f, 78f));
                RectTransform buttonRect =
                    button.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax =
                    new Vector2(0.5f, 0f);
                buttonRect.pivot = new Vector2(0.5f, 0f);
                buttonRect.anchoredPosition =
                    new Vector2((i - 1) * 230f, 12f);
                _attachmentCardLabels[i] =
                    button.GetComponentInChildren<TextMeshProUGUI>();
            }
            _attachmentPanel.SetActive(false);
        }

        private void UpdateAttachmentCards()
        {
            StarterSpiritCarrierController controller =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        StarterSpiritCarrierController>()
                    : null;
            StableConfigId species =
                controller?.Spirit?.Identity.SpeciesConfigId ?? default;
            int[] indices = { 4, 0, 3 };
            string[] keys = { "1", "2", "3" };
            for (int i = 0; i < _attachmentCardLabels.Length; i++)
            {
                TextMeshProUGUI label = _attachmentCardLabels[i];
                if (label == null)
                    continue;
                string description =
                    AttachmentDescription(species, indices[i]);
                int separator = description.IndexOf(" · ",
                    StringComparison.Ordinal);
                string action = separator >= 0
                    ? description.Substring(0, separator)
                    : description;
                string effect = separator >= 0
                    ? description.Substring(separator + 3)
                    : string.Empty;
                label.text =
                    $"<b>{keys[i]}  {action}</b>\n" +
                    $"<size=12>{effect}</size>";
            }
        }

        private void CreateCircuitStageHint()
        {
            var go = new GameObject("CircuitStageHint");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 158f);
            rt.sizeDelta = new Vector2(500f, 24f);
            _circuitStageHint = go.AddComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null)
                _circuitStageHint.font = UGuiKit.CjkFont;
            _circuitStageHint.fontSize = 13f;
            _circuitStageHint.alignment =
                TextAlignmentOptions.Center;
            _circuitStageHint.color =
                new Color(0.68f, 0.92f, 0.86f, 0.96f);
            _circuitStageHint.outlineColor =
                new Color(0.05f, 0.12f, 0.10f, 0.9f);
            _circuitStageHint.outlineWidth = 0.18f;
            _circuitStageHint.raycastTarget = false;
            go.SetActive(false);
        }

        private void UpdateCircuitStage(
            GameEvents.FirstPetCircuitAdvanced evt)
        {
            if (evt.HeatRequired <= 0)
                return;
            _pendingStage = evt.PendingStage;
            _nextCarrier = evt.NextCarrier;
            _stageRemaining = Mathf.Max(0f, evt.RemainingSeconds);
            _stageHeat = Mathf.Max(0, evt.Heat);
            _stageHeatRequired = Mathf.Max(1, evt.HeatRequired);
            ApplyCircuitStageVisual();
        }

        private void UpdateCircuitStageCountdown()
        {
            if (_pendingStage !=
                    SpiritCircuitPendingStage.Heating &&
                _stageRemaining > 0f)
            {
                _stageRemaining = Mathf.Max(
                    0f,
                    _stageRemaining - Time.deltaTime);
                UpdateCircuitStageText();
            }
        }

        private void ApplyCircuitStageVisual()
        {
            ClearStageBorder();
            bool show =
                _pendingStage != SpiritCircuitPendingStage.Heating ||
                _stageHeat > 0;
            if (_circuitStageHint != null)
                _circuitStageHint.gameObject.SetActive(show);
            if (!show)
                return;

            int index = VisualIndexForCarrier(_nextCarrier);
            if (!_attachmentSelecting &&
                index >= 0 &&
                index < (_skillSlots?.Length ?? 0))
            {
                _stageBorder = _skillSlots[index]
                    ?.Find($"SkillBorder_{index}")
                    ?.GetComponent<Image>();
                if (_stageBorder != null)
                {
                    _stageBorderOriginal = _stageBorder.color;
                    _stageBorder.color =
                        new Color(0.42f, 0.92f, 0.78f, 1f);
                }
            }
            UpdateCircuitStageText();
        }

        private void UpdateCircuitStageText()
        {
            if (_circuitStageHint == null ||
                !_circuitStageHint.gameObject.activeSelf)
            {
                return;
            }

            string action = CarrierHintName(_nextCarrier);
            _circuitStageHint.text = _pendingStage switch
            {
                SpiritCircuitPendingStage.AwaitingEcho =>
                    $"火花待接 {_stageRemaining:0.0}s　用 {action} 触发响响鸮",
                SpiritCircuitPendingStage.AwaitingGel =>
                    $"复响待改造 {_stageRemaining:0.0}s　用 {action} 接弹弹胶",
                _ =>
                    $"火花蓄势 {_stageHeat}/{_stageHeatRequired}　继续使用 {action}"
            };
        }

        private void ClearStageBorder()
        {
            if (_stageBorder != null)
                _stageBorder.color = _stageBorderOriginal;
            _stageBorder = null;
        }

        private static string CarrierHintName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.Weapon => "LMB",
                CarrierSlot.TechniqueQ => "Q",
                CarrierSlot.TechniqueE => "E",
                CarrierSlot.TechniqueR => "R",
                CarrierSlot.Mobility => "SPACE",
                _ => "对应动作"
            };
        }

        private RectTransform CreateBadge(
            RectTransform slot,
            StableConfigId species)
        {
            var go = new GameObject("SpiritBadge");
            go.transform.SetParent(slot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-2f, 2f);
            rt.sizeDelta = new Vector2(32f, 32f);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            Sprite badgeFrame = ProjectRUITheme.Instance?.SkillFrame;
            image.color = badgeFrame != null
                ? Color.white
                : BadgeColor(species);
            image.sprite = badgeFrame != null
                ? badgeFrame
                : CircleSprite();
            image.preserveAspect = badgeFrame != null;
            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.16f, 0.1f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            Sprite portrait =
                ProjectRUITheme.Instance?.SpiritPortrait(species);
            if (portrait != null)
            {
                var portraitGo = new GameObject("Portrait");
                portraitGo.transform.SetParent(go.transform, false);
                var portraitRt =
                    portraitGo.AddComponent<RectTransform>();
                portraitRt.anchorMin = Vector2.zero;
                portraitRt.anchorMax = Vector2.one;
                portraitRt.offsetMin = new Vector2(1f, 1f);
                portraitRt.offsetMax = new Vector2(-1f, -1f);
                var portraitImage = portraitGo.AddComponent<Image>();
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.raycastTarget = false;
            }
            else
            {
                var textGo = new GameObject("Glyph");
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<TextMeshProUGUI>();
                if (UGuiKit.CjkFont != null)
                    text.font = UGuiKit.CjkFont;
                text.text = BadgeGlyph(species);
                text.fontSize = 10f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.18f, 0.13f, 0.08f);
                text.raycastTarget = false;
            }

            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                CreateHeatPips(rt);
            }
            return rt;
        }

        private void CreateHeatPips(RectTransform badge)
        {
            for (int i = 0; i < 3; i++)
            {
                var pipGo = new GameObject($"Heat_{i}");
                pipGo.transform.SetParent(badge, false);
                var rt = pipGo.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(4f, 4f);
                rt.anchoredPosition =
                    new Vector2((i - 1) * 5f, -5f);
                Image image = pipGo.AddComponent<Image>();
                image.raycastTarget = false;
                image.sprite = CircleSprite();
                _heatPips.Add(image);
            }
        }

        private void CreateCircuitLine(
            RectTransform from,
            RectTransform to)
        {
            Vector2 start = SlotLocalPoint(from);
            Vector2 end = SlotLocalPoint(to);
            Vector2 delta = end - start;
            var go = new GameObject("CircuitLine");
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = start;
            rt.sizeDelta = new Vector2(delta.magnitude, 6f);
            rt.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 0.65f, 0.22f, 0.12f);
            if (ProjectRUITheme.Instance?.CircuitConnector != null)
            {
                image.sprite =
                    ProjectRUITheme.Instance.CircuitConnector;
                image.type = Image.Type.Simple;
            }
            _circuitLines.Add(image);
        }

        private Vector2 SlotLocalPoint(RectTransform badge)
        {
            RectTransform slot = badge.parent as RectTransform;
            return slot != null
                ? slot.anchoredPosition + new Vector2(20f, 20f)
                : Vector2.zero;
        }

        private void UpdateHeatPips()
        {
            for (int i = 0; i < _heatPips.Count; i++)
            {
                _heatPips[i].color = i < _heat
                    ? new Color(1f, 0.58f, 0.12f, 1f)
                    : new Color(0.36f, 0.27f, 0.18f, 0.55f);
            }
        }

        private void ClearVisuals()
        {
            _starterBadge = null;
            _circuitLines.Clear();
            _heatPips.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "CircuitLine")
                    Destroy(child.gameObject);
            }
            foreach (RectTransform slot in _skillSlots)
            {
                if (slot == null)
                    continue;
                for (int i = slot.childCount - 1; i >= 0; i--)
                {
                    Transform child = slot.GetChild(i);
                    if (child.name == "SpiritBadge")
                        Destroy(child.gameObject);
                }
            }
        }

        private static string BadgeGlyph(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "狸";
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return "鸮";
            return "胶";
        }

        private static Color BadgeColor(StableConfigId species)
        {
            if (species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return new Color(1f, 0.55f, 0.18f, 0.96f);
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
                return new Color(0.76f, 0.86f, 0.72f, 0.96f);
            return new Color(0.42f, 0.83f, 0.74f, 0.96f);
        }

        private static Sprite CircleSprite()
        {
            return ProjectRUITheme.Instance?.SkillFrame;
        }
    }
}
