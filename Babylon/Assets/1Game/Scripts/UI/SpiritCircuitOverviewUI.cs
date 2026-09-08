using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// ProjectR只读回路总览。复用现有回路预览图和结构化战斗结果，
    /// 不在UI层重新推导战斗规则。
    /// </summary>
    public sealed class SpiritCircuitOverviewUI : MonoBehaviour
    {
        private GameObject _root;
        private TextMeshProUGUI _body;
        private Button _previewButton;
        private Button _statsButton;
        private RectTransform _loadoutControls;
        private TextMeshProUGUI _loadoutStatus;
        private Guid _draggedSpiritId;
        private bool _showStats;

        public static SpiritCircuitOverviewUI Instance { get; private set; }
        public static bool IsVisible =>
            Instance != null && Instance._root != null &&
            Instance._root.activeSelf;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            BuildUI();
            _root.SetActive(false);
            GameEvents.Subscribe<GameEvents.SpiritLoadoutChanged>(
                OnLoadoutChanged);
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.SpiritLoadoutChanged>(
                OnLoadoutChanged);
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.mKey.wasPressedThisFrame)
                Toggle();
            else if (IsVisible && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Toggle()
        {
            if (IsVisible)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (_root == null)
                return;
            _root.SetActive(true);
            Time.timeScale = 0f;
            Refresh();
        }

        public void Close()
        {
            if (_root != null)
                _root.SetActive(false);
            Time.timeScale = 1f;
        }

        private void BuildUI()
        {
            Canvas canvas = UGuiKit.CreateOverlayCanvas(
                "SpiritCircuitOverview",
                124,
                transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(
                _root.transform,
                new Color(0.03f, 0.045f, 0.04f, 0.82f));

            RectTransform panel = UGuiKit.CreatePanel(
                _root.transform,
                "Panel",
                new Vector2(820f, 610f),
                new Color(0.10f, 0.14f, 0.12f, 0.98f));
            UGuiKit.AddVLayout(
                panel,
                12f,
                new RectOffset(30, 30, 24, 24),
                TextAnchor.UpperCenter,
                cChildH: true);

            TextMeshProUGUI title = UGuiKit.CreateText(
                panel,
                "灵宠回路",
                28,
                UGuiKit.Gold,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            UGuiKit.SetHeight(title, 42f);

            RectTransform tabs = UGuiKit.CreateRow(panel, 12f, 44f);
            _previewButton = UGuiKit.CreateButton(
                tabs,
                "回路预览",
                () => SelectTab(false),
                UGuiKit.BtnPrimary,
                18,
                new Vector2(180f, 42f));
            _statsButton = UGuiKit.CreateButton(
                tabs,
                "战斗统计",
                () => SelectTab(true),
                UGuiKit.Panel,
                18,
                new Vector2(180f, 42f));

            _body = UGuiKit.CreateText(
                panel,
                "",
                18,
                UGuiKit.TextMain,
                TextAlignmentOptions.TopLeft);
            _body.enableWordWrapping = true;
            _body.overflowMode = TextOverflowModes.Ellipsis;
            UGuiKit.SetHeight(_body, 180f);

            _loadoutControls = UGuiKit.CreateRow(panel, 16f, 150f);
            _loadoutStatus = UGuiKit.CreateText(
                panel,
                "",
                15,
                new Color(0.76f, 0.82f, 0.72f),
                TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_loadoutStatus, 34f);

            Button close = UGuiKit.CreateButton(
                panel,
                "关闭  [M]",
                Close,
                out _,
                UGuiKit.BtnPrimary,
                18,
                new Vector2(230f, 46f));
            UGuiKit.SetHeight(close, 46f);
        }

        private void SelectTab(bool showStats)
        {
            _showStats = showStats;
            Refresh();
        }

        private void Refresh()
        {
            if (_body == null)
                return;
            _previewButton.image.color =
                _showStats ? UGuiKit.Panel : UGuiKit.BtnPrimary;
            _statsButton.image.color =
                _showStats ? UGuiKit.BtnPrimary : UGuiKit.Panel;
            _body.text = _showStats
                ? BuildStatsText(RunCombatStats.Results)
                : BuildPreviewText();
            _loadoutControls.gameObject.SetActive(!_showStats);
            _loadoutStatus.gameObject.SetActive(!_showStats);
            if (!_showStats)
                RebuildLoadoutControls();
        }

        private void OnLoadoutChanged(GameEvents.SpiritLoadoutChanged evt)
        {
            if (IsVisible)
                Refresh();
        }

        private void RebuildLoadoutControls()
        {
            for (int i = _loadoutControls.childCount - 1; i >= 0; i--)
                Destroy(_loadoutControls.GetChild(i).gameObject);

            FirstSpiritCircuitController circuit =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        FirstSpiritCircuitController>()
                    : null;
            if (circuit?.Loadout == null)
            {
                _loadoutStatus.text =
                    "单宠改附继续使用HUD灵宠挂件。";
                return;
            }

            RectTransform spirits = CreateColumn(
                _loadoutControls,
                "Spirits",
                250f);
            foreach (SpiritInstanceState spirit in
                     circuit.Loadout.ActiveSpirits
                         .OrderBy(item =>
                             item.Identity.SpeciesConfigId.Value))
            {
                circuit.Loadout.TryGetCarrier(
                    spirit.Identity.InstanceId,
                    out CarrierSlot current);
                Image card = UGuiKit.CreateBox(
                    spirits,
                    new Color(0.19f, 0.27f, 0.22f, 0.98f),
                    new Vector2(240f, 36f));
                UGuiKit.SetHeight(card, 36f);
                TextMeshProUGUI label = UGuiKit.CreateText(
                    card.transform,
                    $"{SpeciesName(spirit.Identity.SpeciesConfigId)}" +
                    $"  ·  {CarrierShortName(current)}",
                    15,
                    UGuiKit.TextMain,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);
                Stretch(label.rectTransform);
                SpiritDragHandle drag =
                    card.gameObject.AddComponent<SpiritDragHandle>();
                drag.Configure(this, spirit.Identity.InstanceId);
                Button select = card.gameObject.AddComponent<Button>();
                Guid id = spirit.Identity.InstanceId;
                select.onClick.AddListener(() => SelectSpirit(id));
            }

            RectTransform carriers = CreateColumn(
                _loadoutControls,
                "Carriers",
                480f);
            RectTransform firstRow =
                UGuiKit.CreateRow(carriers, 8f, 48f);
            RectTransform secondRow =
                UGuiKit.CreateRow(carriers, 8f, 48f);
            CreateCarrierTarget(firstRow, CarrierSlot.Weapon, circuit);
            CreateCarrierTarget(
                firstRow,
                CarrierSlot.TechniqueQ,
                circuit);
            CreateCarrierTarget(
                firstRow,
                CarrierSlot.TechniqueE,
                circuit);
            CreateCarrierTarget(
                secondRow,
                CarrierSlot.TechniqueR,
                circuit);
            CreateCarrierTarget(
                secondRow,
                CarrierSlot.Mobility,
                circuit);
            _loadoutStatus.text =
                $"当前结构：{PatternName(circuit.Loadout.Pattern)}" +
                "　拖动灵宠卡到动作槽，或先点灵宠再点动作。";
        }

        private static RectTransform CreateColumn(
            Transform parent,
            string name,
            float width)
        {
            RectTransform column =
                new GameObject(name, typeof(RectTransform))
                    .GetComponent<RectTransform>();
            column.SetParent(parent, false);
            column.sizeDelta = new Vector2(width, 140f);
            UGuiKit.AddVLayout(
                column,
                6f,
                new RectOffset(4, 4, 4, 4));
            LayoutElement layout =
                column.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
            layout.preferredHeight = 140f;
            layout.minHeight = 140f;
            return column;
        }

        private void CreateCarrierTarget(
            Transform parent,
            CarrierSlot carrier,
            FirstSpiritCircuitController circuit)
        {
            int count = circuit.Loadout.GetSpirits(carrier).Count;
            Image box = UGuiKit.CreateBox(
                parent,
                count > 0
                    ? new Color(0.29f, 0.38f, 0.24f, 0.98f)
                    : new Color(0.14f, 0.18f, 0.16f, 0.98f),
                new Vector2(145f, 44f));
            LayoutElement layout =
                box.gameObject.GetComponent<LayoutElement>() ??
                box.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 145f;
            layout.preferredHeight = 44f;
            TextMeshProUGUI label = UGuiKit.CreateText(
                box.transform,
                $"{CarrierShortName(carrier)}  ×{count}",
                14,
                UGuiKit.TextMain,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            Stretch(label.rectTransform);
            SpiritDropTarget drop =
                box.gameObject.AddComponent<SpiritDropTarget>();
            drop.Configure(this, carrier);
            Button button = box.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => DropOn(carrier));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        internal void BeginDrag(Guid spiritInstanceId)
        {
            _draggedSpiritId = spiritInstanceId;
            _loadoutStatus.text = "拖到右侧任意动作槽进行预演。";
        }

        internal void EndDrag()
        {
            _draggedSpiritId = Guid.Empty;
        }

        private void SelectSpirit(Guid spiritInstanceId)
        {
            _draggedSpiritId = spiritInstanceId;
            _loadoutStatus.text = "已选择灵宠，再点击右侧动作槽。";
        }

        internal void PreviewDrop(CarrierSlot carrier)
        {
            if (_draggedSpiritId == Guid.Empty)
                return;
            _loadoutStatus.text =
                $"预演：改附至 {CarrierName(carrier)}" +
                $"  →  {PreviewPattern(_draggedSpiritId, carrier)}";
        }

        internal void DropOn(CarrierSlot carrier)
        {
            if (_draggedSpiritId == Guid.Empty)
            {
                _loadoutStatus.text = "请先选择或拖动一只灵宠。";
                return;
            }

            FirstSpiritCircuitController circuit =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        FirstSpiritCircuitController>()
                    : null;
            SpiritLoadoutChangeResult result =
                circuit != null
                    ? circuit.TryMigrateAttachment(
                        _draggedSpiritId,
                        carrier)
                    : SpiritLoadoutChangeResult.InvalidSpirit;
            _draggedSpiritId = Guid.Empty;
            _loadoutStatus.text = result switch
            {
                SpiritLoadoutChangeResult.Success => "改附完成。",
                SpiritLoadoutChangeResult.NoChange => "灵宠已在该动作上。",
                SpiritLoadoutChangeResult.BlockedInCombat =>
                    "附近仍有敌人，战斗中不能改附。",
                _ => "改附失败，请检查当前编队。"
            };
            Refresh();
        }

        private string PreviewPattern(
            Guid spiritInstanceId,
            CarrierSlot target)
        {
            FirstSpiritCircuitController circuit =
                PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<
                        FirstSpiritCircuitController>()
                    : null;
            if (circuit?.Loadout == null)
                return "不可用";

            var occupied = new Dictionary<CarrierSlot, int>();
            foreach (SpiritInstanceState spirit in
                     circuit.Loadout.ActiveSpirits)
            {
                CarrierSlot carrier = target;
                if (spirit.Identity.InstanceId != spiritInstanceId &&
                    circuit.Loadout.TryGetCarrier(
                        spirit.Identity.InstanceId,
                        out CarrierSlot current))
                {
                    carrier = current;
                }
                occupied.TryGetValue(carrier, out int count);
                occupied[carrier] = count + 1;
            }
            return occupied.Count switch
            {
                1 => "3＋0 集中显化",
                2 => "2＋1 双宠接续",
                3 => "1＋1＋1 长回路",
                _ => "未完成"
            };
        }

        private static string PatternName(SpiritAttachmentPattern pattern)
        {
            return pattern switch
            {
                SpiritAttachmentPattern.OneOneOne => "1＋1＋1 长回路",
                SpiritAttachmentPattern.TwoOne => "2＋1 双宠接续",
                SpiritAttachmentPattern.ThreeZero => "3＋0 集中显化",
                _ => "未完成"
            };
        }

        private static string CarrierShortName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.Weapon => "LMB",
                CarrierSlot.TechniqueQ => "Q",
                CarrierSlot.TechniqueE => "E",
                CarrierSlot.TechniqueR => "R",
                CarrierSlot.Mobility => "SPACE",
                _ => "?"
            };
        }

        private static string BuildPreviewText()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null)
                return "尚未找到玩家与出战灵宠。";

            StarterSpiritCarrierController starter =
                player.GetComponent<StarterSpiritCarrierController>();
            if (starter?.Spirit != null)
            {
                return
                    $"<b>{SpeciesName(starter.Spirit.Identity.SpeciesConfigId)}</b>" +
                    $"  →  {CarrierName(starter.Attachment)}\n\n" +
                    $"{SingleRole(starter.Spirit.Identity.SpeciesConfigId)}\n\n" +
                    "天赋投资跟随灵宠；脱战改附不会清空本局节点。\n" +
                    "点击HUD上的灵宠挂件可改附至LMB、Q或SPACE。";
            }

            FirstSpiritCircuitController circuit =
                player.GetComponent<FirstSpiritCircuitController>();
            if (circuit?.Loadout == null)
                return "当前没有可预览的灵宠回路。";

            var lines = new List<string>();
            foreach (SpiritInstanceState spirit in
                     circuit.Loadout.ActiveSpirits)
            {
                if (!circuit.Loadout.TryGetCarrier(
                        spirit.Identity.InstanceId,
                        out CarrierSlot carrier))
                {
                    continue;
                }
                lines.Add(
                    $"<b>{SpeciesName(spirit.Identity.SpeciesConfigId)}</b>" +
                    $"  →  {CarrierName(carrier)}  ·  " +
                    SingleRole(spirit.Identity.SpeciesConfigId));
            }

            CircuitPreviewGraph graph = circuit.Runtime?.Preview;
            if (graph != null)
            {
                lines.Add("");
                lines.Add(
                    $"连接 {graph.Edges.Count} 条  ·  " +
                    $"断点 {graph.InputBreakpoints.Count + graph.OutputBreakpoints.Count} 个");
                lines.Add(
                    graph.HasCycle
                        ? "<color=#E98B72>检测到循环，请检查触发边界。</color>"
                        : "<color=#8FCB91>回路无递归循环。</color>");
            }
            return string.Join("\n", lines);
        }

        public static string BuildStatsText(
            IReadOnlyList<StructuredCombatResult> results)
        {
            if (results == null || results.Count == 0)
                return "本局尚无可显示的战斗统计。";

            var totals = new Dictionary<
                (StableConfigId metric, CombatOutcomeKind outcome),
                (float amount, int count)>();
            foreach (StructuredCombatResult result in results)
            {
                var key = (result.MetricId, result.Outcome);
                totals.TryGetValue(key, out var value);
                totals[key] = (
                    value.amount + result.AppliedAmount,
                    value.count + 1);
            }

            var lines = new List<string>
            {
                "<b>本局来源汇总</b>",
                "数值按实际生效量统计；回路追加伤害保留独立来源。",
                ""
            };
            foreach (var entry in totals
                         .OrderByDescending(pair => pair.Value.amount)
                         .Take(12))
            {
                lines.Add(
                    $"{MetricName(entry.Key.metric),-18}  " +
                    $"{OutcomeName(entry.Key.outcome)} " +
                    $"{entry.Value.amount:0.#}  ·  " +
                    $"{entry.Value.count}次");
            }
            return string.Join("\n", lines);
        }

        private static string SpeciesName(StableConfigId species)
        {
            if (species == FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "火花狸";
            if (species == FirstSpiritCircuitContent.EchoOwlSpecies)
                return "响响鸮";
            if (species == FirstSpiritCircuitContent.BounceGelSpecies)
                return "弹弹胶";
            return species.ToString();
        }

        private static string SingleRole(StableConfigId species)
        {
            if (species == FirstSpiritCircuitContent.SparkRaccoonSpecies)
                return "产出火花并积蓄热度";
            if (species == FirstSpiritCircuitContent.EchoOwlSpecies)
                return "延后复响并确认目标";
            if (species == FirstSpiritCircuitContent.BounceGelSpecies)
                return "把命中改造成弹射或落点冲击";
            return "灵宠显化";
        }

        private static string CarrierName(CarrierSlot carrier)
        {
            return carrier switch
            {
                CarrierSlot.Weapon => "LMB 武器动作",
                CarrierSlot.TechniqueQ => "Q 术法动作",
                CarrierSlot.TechniqueE => "E 术法动作",
                CarrierSlot.TechniqueR => "R 术法动作",
                CarrierSlot.Mobility => "SPACE 身法动作",
                _ => carrier.ToString()
            };
        }

        private static string OutcomeName(CombatOutcomeKind outcome)
        {
            return outcome switch
            {
                CombatOutcomeKind.Damage => "伤害",
                CombatOutcomeKind.Healing => "治疗",
                CombatOutcomeKind.Defense => "防御",
                CombatOutcomeKind.Resource => "资源",
                _ => outcome.ToString()
            };
        }

        private static string MetricName(StableConfigId metric)
        {
            string value = metric.ToString();
            if (value.Contains("spark-raccoon"))
                return "火花狸";
            if (value.Contains("echo-owl"))
                return "响响鸮";
            if (value.Contains("bounce-gel"))
                return "弹弹胶";
            if (value == "combat.player.damage")
                return "玩家动作";
            if (value == "combat.player.damage-taken")
                return "承受伤害";
            if (value == "combat.player.healing")
                return "恢复";
            if (value == "combat.player.defense")
                return "防御";
            return value;
        }
    }

    internal sealed class SpiritDragHandle :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private SpiritCircuitOverviewUI _owner;
        private Guid _spiritInstanceId;
        private CanvasGroup _canvasGroup;

        public void Configure(
            SpiritCircuitOverviewUI owner,
            Guid spiritInstanceId)
        {
            _owner = owner;
            _spiritInstanceId = spiritInstanceId;
            _canvasGroup =
                gameObject.GetComponent<CanvasGroup>() ??
                gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 0.65f;
            _owner.BeginDrag(_spiritInstanceId);
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
            _owner.EndDrag();
        }
    }

    internal sealed class SpiritDropTarget :
        MonoBehaviour,
        IDropHandler,
        IPointerEnterHandler
    {
        private SpiritCircuitOverviewUI _owner;
        private CarrierSlot _carrier;

        public void Configure(
            SpiritCircuitOverviewUI owner,
            CarrierSlot carrier)
        {
            _owner = owner;
            _carrier = carrier;
        }

        public void OnDrop(PointerEventData eventData)
        {
            _owner.DropOn(_carrier);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner.PreviewDrop(_carrier);
        }
    }
}
