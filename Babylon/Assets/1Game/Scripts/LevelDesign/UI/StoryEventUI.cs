using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.3 事件 UI（v0.6 改 UI Toolkit）。
    /// 使用：StoryEventUI.Show(row, opt =&gt; { ... }); 玩家点选项 → 回调 → 自动关闭。
    /// 结构 Resources/UI/StoryEventUI.uxml，样式同名 uss。对外保持 Show/HideImmediate/IsVisible。
    /// </summary>
    public class StoryEventUI : MonoBehaviour
    {
        private static StoryEventUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private StoryEventRow _row;
        private Action<EventOption> _onSelected;
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _title;
        private Label _body;
        private VisualElement _options;

        public static void Show(StoryEventRow row, Action<EventOption> onSelected)
        {
            if (row == null || row.Options == null || row.Options.Length == 0)
            {
                onSelected?.Invoke(null);
                return;
            }

            EnsureInstance();
            if (_instance == null)
            {
                onSelected?.Invoke(null);
                return;
            }

            _instance._row = row;
            _instance._onSelected = onSelected;
            _instance._visible = true;
            _instance._prevLock = UnityEngine.Cursor.lockState;
            _instance._prevVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _instance.Rebuild();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("StoryEventUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoryEventUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/StoryEventUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;
            XianTu.ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _title = root.Q<Label>("title");
            _body = root.Q<Label>("body");
            _options = root.Q<VisualElement>("options");
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (_row == null) return;
            if (_title != null) _title.text = $"· 奇遇 · {_row.Name_CN} ·";
            if (_body != null) _body.text = _row.Text_CN;

            if (_options != null)
            {
                _options.Clear();
                foreach (var opt in _row.Options)
                {
                    if (opt == null || string.IsNullOrEmpty(opt.Text)) continue;
                    var captured = opt;
                    var b = new Button(() => OnPick(captured)) { text = BuildOptionLabel(opt) };
                    b.AddToClassList("se-opt");
                    b.enableRichText = true;
                    _options.Add(b);
                }
            }
        }

        private void OnPick(EventOption opt)
        {
            _visible = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = _prevLock;
            UnityEngine.Cursor.visible = _prevVisible;
            _onSelected?.Invoke(opt);
        }

        private string BuildOptionLabel(EventOption opt)
        {
            var tags = new System.Collections.Generic.List<string>();
            if (opt.KarmaChange > 0) tags.Add($"<color=#e87f5b>因果 +{opt.KarmaChange}</color>");
            else if (opt.KarmaChange < 0) tags.Add($"<color=#9ed18c>因果 {opt.KarmaChange}</color>");
            if (opt.DaoxinChange > 0) tags.Add($"<color=#9ed18c>道心 +{opt.DaoxinChange}</color>");
            else if (opt.DaoxinChange < 0) tags.Add($"<color=#e87f5b>道心 {opt.DaoxinChange}</color>");
            if (opt.LifespanChange != 0) tags.Add($"<color=#c89cd8>寿元 {opt.LifespanChange}</color>");
            if (opt.RewardID > 0) tags.Add("<color=#7fb8ff>有奖励</color>");
            if (opt.CostID > 0) tags.Add("<color=#e87f5b>有代价</color>");

            string suffix = tags.Count > 0 ? "    " + string.Join("  ·  ", tags) : "";
            return $"▸ {opt.Text}{suffix}";
        }
    }
}
