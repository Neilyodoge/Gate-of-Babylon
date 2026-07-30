using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.3 事件 UI（V0.4.6 改 uGUI+TMP）。
    /// 使用：StoryEventUI.Show(row, opt =&gt; { ... }); 玩家点选项 → 回调 → 自动关闭。
    /// 对外保持 Show/HideImmediate/IsVisible。
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

        private GameObject _root;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private RectTransform _options;

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
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._root != null) _instance._root.SetActive(false);
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
            var canvas = XianTu.UGuiKit.CreateOverlayCanvas("StoryEventUI", 124, transform);
            _root = canvas.gameObject;
            XianTu.UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.02f, 0.04f, 0.9f));

            var panel = XianTu.UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(720f, 10f), XianTu.UGuiKit.Panel);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            XianTu.UGuiKit.AddVLayout(panel, 14f, new RectOffset(32, 32, 26, 26), TextAnchor.UpperCenter);

            _title = XianTu.UGuiKit.CreateText(panel, "", 26, XianTu.UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            XianTu.UGuiKit.SetHeight(_title, 36f);

            _body = XianTu.UGuiKit.CreateText(panel, "", 16, new Color(0.82f, 0.84f, 0.9f), TextAlignmentOptions.TopLeft);
            _body.enableWordWrapping = true;
            var ble = _body.gameObject.AddComponent<LayoutElement>(); ble.minHeight = 80f; ble.preferredHeight = 120f;

            _options = new GameObject("Options", typeof(RectTransform)).GetComponent<RectTransform>();
            _options.SetParent(panel, false);
            var ov = _options.gameObject.AddComponent<VerticalLayoutGroup>();
            ov.spacing = 8f; ov.childControlWidth = true; ov.childForceExpandWidth = true; ov.childControlHeight = true; ov.childForceExpandHeight = false;

            _root.SetActive(false);
        }

        private void Rebuild()
        {
            if (_row == null) return;
            if (_title != null) _title.text = $"· 奇遇 · {_row.Name_CN} ·";
            if (_body != null) _body.text = _row.Text_CN;

            if (_options != null)
            {
                for (int i = _options.childCount - 1; i >= 0; i--) Destroy(_options.GetChild(i).gameObject);
                foreach (var opt in _row.Options)
                {
                    if (opt == null || string.IsNullOrEmpty(opt.Text)) continue;
                    var captured = opt;
                    var btn = XianTu.UGuiKit.CreateButton(_options, BuildOptionLabel(opt), () => OnPick(captured), out var lbl, XianTu.UGuiKit.BtnNormal, 16, new Vector2(640f, 48f));
                    lbl.alignment = TextAlignmentOptions.Left;
                    XianTu.UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 48f);
                }
            }
        }

        private void OnPick(EventOption opt)
        {
            _visible = false;
            if (_root != null) _root.SetActive(false);
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
