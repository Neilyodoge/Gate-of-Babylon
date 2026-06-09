using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 状态栏（buff / debuff）· UI Toolkit（v0.6）——顶部居中，把玩家身上所有具名 StatusEffect
    /// 显示为 chip（名称 / 层数 / 倒计时 + 底部时间条；buff 绿边、debuff 红边）。取代旧 IMGUI 版 StatusEffectHUD。
    /// 每帧增量对账（reconcile）已有 chip，避免整条重建。
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

        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _avatar;
        private VisualElement _row;

        private class Chip
        {
            public VisualElement root;
            public VisualElement fill;
            public Label name;
            public Label time;
        }

        private readonly Dictionary<string, Chip> _chips = new();
        private readonly List<string> _toRemove = new();

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/BuffBarUITK");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 5f;   // HUD 层

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _avatar = root.Q<Label>("avatar");
            _row = root.Q<VisualElement>("row");
            if (_overlay != null) _overlay.pickingMode = PickingMode.Ignore;
        }

        private void Update()
        {
            if (_overlay == null || _row == null) return;

            var player = PlayerController.Instance;
            var status = player != null ? player.GetComponent<StatusEffectController>() : null;
            bool show = !MainMenu.IsVisible && status != null;
            _overlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            // 化身标签
            var rootCtrl = player.GetComponent<SpiritRootController>();
            if (_avatar != null)
            {
                if (rootCtrl != null && rootCtrl.CurrentDef != null)
                {
                    _avatar.text = $"化身 · {rootCtrl.CurrentDef.name}";
                    _avatar.style.color = rootCtrl.CurrentDef.displayColor;
                    _avatar.style.display = DisplayStyle.Flex;
                }
                else _avatar.style.display = DisplayStyle.None;
            }

            // 对账 chip
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
                    _row.Add(chip.root);
                }
                UpdateChip(chip, eff);
            }

            for (int i = 0; i < _toRemove.Count; i++)
            {
                if (_chips.TryGetValue(_toRemove[i], out var c))
                {
                    c.root.RemoveFromHierarchy();
                    _chips.Remove(_toRemove[i]);
                }
            }
        }

        private Chip CreateChip()
        {
            var root = new VisualElement();
            root.AddToClassList("bb-chip");
            root.pickingMode = PickingMode.Ignore;
            var name = new Label();
            name.AddToClassList("bb-chip__name");
            root.Add(name);
            var time = new Label();
            time.AddToClassList("bb-chip__time");
            root.Add(time);
            var fill = new VisualElement();
            fill.AddToClassList("bb-chip__fill");
            root.Add(fill);
            return new Chip { root = root, name = name, time = time, fill = fill };
        }

        private void UpdateChip(Chip chip, StatusEffect eff)
        {
            chip.name.text = eff.maxStacks > 1 ? $"{eff.displayName} ×{eff.stacks}" : eff.displayName;
            chip.root.EnableInClassList("bb-chip--debuff", !eff.isBuff);
            chip.fill.style.backgroundColor = eff.uiColor;

            if (eff.IsPermanent)
            {
                chip.time.text = "";
                chip.fill.style.width = Length.Percent(100f);
                chip.fill.style.opacity = 0.35f;
                chip.root.style.opacity = 1f;
            }
            else
            {
                chip.time.text = $"{Mathf.CeilToInt(Mathf.Max(0f, eff.duration))}s";
                float denom = eff.defaultDuration > 0.01f ? eff.defaultDuration : eff.duration;
                float ratio = denom > 0.01f ? Mathf.Clamp01(eff.duration / denom) : 1f;
                chip.fill.style.width = Length.Percent(ratio * 100f);
                chip.fill.style.opacity = 1f;

                // 即将到期（≤3s）脉冲闪烁提醒
                if (eff.duration <= 3f)
                    chip.root.style.opacity = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                else
                    chip.root.style.opacity = 1f;
            }
        }
    }
}
