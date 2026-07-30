using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 状态栏（buff / debuff）· uGUI+TMP（V0.4.6）——顶部居中，把玩家身上所有具名 StatusEffect
    /// 显示为 chip（名称 / 层数 / 倒计时 + 底部时间条；buff 绿边、debuff 红边）。
    /// 每帧增量对账（reconcile）已有 chip，避免整条重建。类名保留 BuffBarUITK 兼容既有调用。
    /// </summary>
    public class BuffBarUITK : MonoBehaviour
    {
        private static BuffBarUITK _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("BuffBarUITK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BuffBarUITK>();
        }

        private GameObject _root;
        private RectTransform _row;
        private GameObject _tooltip;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;

        private const float ChipWidth = 100f;
        private const float ChipHeight = 46f;

        private class Chip
        {
            public RectTransform root;
            public CanvasGroup group;
            public RectTransform fill;
            public Image fillImg;
            public Outline outline;
            public TextMeshProUGUI name;
            public TextMeshProUGUI time;
            public StatusEffect effect;
        }

        private readonly Dictionary<string, Chip> _chips = new();
        private readonly List<string> _toRemove = new();

        private void Awake()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("BuffBarUITK", 50, transform);
            _root = canvas.gameObject;
            var ray = _root.GetComponent<GraphicRaycaster>();
            // 保留 raycaster 以支持 chip 悬停 tooltip

            _row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            _row.SetParent(_root.transform, false);
            _row.anchorMin = new Vector2(0.5f, 1f); _row.anchorMax = new Vector2(0.5f, 1f);
            _row.pivot = new Vector2(0.5f, 1f);
            _row.anchoredPosition = new Vector2(0f, -12f);
            var hl = _row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f; hl.childAlignment = TextAnchor.UpperCenter;
            hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            var fit = _row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildTooltip();
            _root.SetActive(true);
        }

        private void BuildTooltip()
        {
            _tooltip = new GameObject("Tooltip", typeof(RectTransform), typeof(Image)).GetComponent<Image>().gameObject;
            var trt = (RectTransform)_tooltip.transform;
            trt.SetParent(_root.transform, false);
            trt.pivot = new Vector2(0f, 1f);
            trt.sizeDelta = new Vector2(280f, 120f);
            _tooltip.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
            _tooltip.GetComponent<Image>().raycastTarget = false;
            var tv = _tooltip.AddComponent<VerticalLayoutGroup>();
            tv.padding = new RectOffset(12, 12, 8, 8); tv.spacing = 4f;
            tv.childControlWidth = true; tv.childForceExpandWidth = true; tv.childControlHeight = true; tv.childForceExpandHeight = false;
            var fit = _tooltip.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tooltipTitle = UGuiKit.CreateText(trt, "", 15, UGuiKit.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(_tooltipTitle, 20f);
            _tooltipBody = UGuiKit.CreateText(trt, "", 12, new Color(0.8f, 0.82f, 0.88f), TextAlignmentOptions.TopLeft);
            _tooltipBody.enableWordWrapping = true;
            var ble = _tooltipBody.gameObject.AddComponent<LayoutElement>(); ble.minHeight = 40f;
            _tooltip.SetActive(false);
        }

        private void Update()
        {
            if (_root == null || _row == null) return;

            var player = PlayerController.Instance;
            var status = player != null ? player.GetComponent<StatusEffectController>() : null;
            bool show = !MainMenu.IsVisible && status != null;
            _row.gameObject.SetActive(show);
            if (!show) { if (_tooltip != null) _tooltip.SetActive(false); return; }

            _toRemove.Clear();
            foreach (var k in _chips.Keys) _toRemove.Add(k);

            foreach (var kv in status.Effects)
            {
                var eff = kv.Value;
                if (eff == null || string.IsNullOrEmpty(eff.displayName)) continue;
                string key = kv.Key;
                _toRemove.Remove(key);

                if (!_chips.TryGetValue(key, out var chip))
                {
                    chip = CreateChip();
                    _chips[key] = chip;
                }
                UpdateChip(chip, eff);
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                if (_chips.TryGetValue(_toRemove[i], out var c))
                {
                    if (c.root != null) Destroy(c.root.gameObject);
                    _chips.Remove(_toRemove[i]);
                }
            }
        }

        private Chip CreateChip()
        {
            var rootGo = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(_row, false);
            root.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            rootGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
            var le = rootGo.GetComponent<LayoutElement>(); le.preferredWidth = ChipWidth; le.preferredHeight = ChipHeight;
            var group = rootGo.GetComponent<CanvasGroup>();
            var outline = rootGo.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2f, 2f);

            var name = UGuiKit.CreateText(root, "", 14, UGuiKit.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
            var nrt = (RectTransform)name.transform; nrt.anchorMin = new Vector2(0f, 0.42f); nrt.anchorMax = new Vector2(1f, 1f); nrt.offsetMin = new Vector2(4f, 0f); nrt.offsetMax = new Vector2(-4f, 0f);
            var time = UGuiKit.CreateText(root, "", 11, UGuiKit.TextDim, TextAlignmentOptions.Center);
            var trt = (RectTransform)time.transform; trt.anchorMin = new Vector2(0f, 0.14f); trt.anchorMax = new Vector2(1f, 0.42f); trt.offsetMin = new Vector2(4f, 0f); trt.offsetMax = new Vector2(-4f, 0f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fill = (RectTransform)fillGo.transform; fill.SetParent(root, false);
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 0f); fill.pivot = new Vector2(0f, 0f);
            fill.anchoredPosition = new Vector2(3f, 3f);
            fill.sizeDelta = new Vector2(ChipWidth - 6f, 4f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.raycastTarget = false;

            var chip = new Chip { root = root, group = group, fill = fill, fillImg = fillImg, outline = outline, name = name, time = time };

            var etrig = rootGo.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowTooltip(chip));
            etrig.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideTooltip());
            etrig.triggers.Add(exit);
            return chip;
        }

        private void UpdateChip(Chip chip, StatusEffect eff)
        {
            chip.effect = eff;
            chip.name.text = eff.maxStacks > 1 ? $"{eff.displayName} ×{eff.stacks}" : eff.displayName;
            chip.outline.effectColor = eff.isBuff ? new Color(0.35f, 0.85f, 0.45f, 0.9f) : new Color(0.9f, 0.35f, 0.35f, 0.9f);

            var fc = eff.uiColor;
            if (eff.IsPermanent)
            {
                chip.time.text = "";
                chip.fill.sizeDelta = new Vector2(ChipWidth - 6f, 4f);
                chip.fillImg.color = new Color(fc.r, fc.g, fc.b, 0.35f);
                chip.group.alpha = 1f;
            }
            else
            {
                chip.time.text = $"{Mathf.CeilToInt(Mathf.Max(0f, eff.duration))}s";
                float denom = eff.defaultDuration > 0.01f ? eff.defaultDuration : eff.duration;
                float ratio = denom > 0.01f ? Mathf.Clamp01(eff.duration / denom) : 1f;
                chip.fill.sizeDelta = new Vector2((ChipWidth - 6f) * ratio, 4f);
                chip.fillImg.color = new Color(fc.r, fc.g, fc.b, 1f);

                if (eff.duration <= 3f)
                    chip.group.alpha = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                else
                    chip.group.alpha = 1f;
            }
        }

        private void ShowTooltip(Chip chip)
        {
            if (_tooltip == null || chip.effect == null) return;
            var eff = chip.effect;
            _tooltipTitle.text = eff.isBuff ? $"[增益] {eff.displayName}" : $"[减益] {eff.displayName}";

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(eff.description))
                sb.AppendLine(eff.description);
            if (eff.IsPermanent)
                sb.AppendLine("持续：永久");
            else if (eff.duration > 0f)
                sb.AppendLine($"剩余：{eff.duration:F1}s");
            if (eff.maxStacks > 1)
                sb.AppendLine($"层数：{eff.stacks}/{eff.maxStacks}");
            if (eff.modifiers != null && eff.modifiers.Count > 0)
            {
                foreach (var m in eff.modifiers)
                {
                    string sign = m.value >= 0 ? "+" : "";
                    if (m.isPercent)
                        sb.AppendLine($"  {m.type}: {sign}{m.value * 100f:F0}%");
                    else
                        sb.AppendLine($"  {m.type}: {sign}{m.value:F0}");
                }
            }
            _tooltipBody.text = sb.ToString().TrimEnd();

            // 定位到 chip 下方（overlay canvas：世界坐标即屏幕像素）
            var trt = (RectTransform)_tooltip.transform;
            trt.position = chip.root.position + new Vector3(-ChipWidth * 0.5f, -ChipHeight * 0.5f - 4f, 0f);
            _tooltip.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.SetActive(false);
        }
    }
}
