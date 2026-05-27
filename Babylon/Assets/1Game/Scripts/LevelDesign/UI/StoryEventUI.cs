using System;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.3 事件 UI（IMGUI 临时实现，复用 RealmRewardSelectUI 模式）。
    ///
    /// 使用：StoryEventUI.Show(row, opt =&gt; { ... });
    /// 显示后玩家点选项 → 回调被触发 → 自动关闭。
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

        public static void Show(StoryEventRow row, Action<EventOption> onSelected)
        {
            if (row == null || row.Options == null || row.Options.Length == 0)
            {
                onSelected?.Invoke(null);
                return;
            }

            if (_instance == null)
            {
                var go = new GameObject("StoryEventUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<StoryEventUI>();
            }
            _instance._row = row;
            _instance._onSelected = onSelected;
            _instance._visible = true;
            _instance._prevLock = Cursor.lockState;
            _instance._prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void HideImmediate()
        {
            if (_instance != null) _instance._visible = false;
        }

        private void OnGUI()
        {
            if (!_visible || _row == null) return;

            // 半透明遮罩
            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = bg;

            float w = 800f;
            float h = 540f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // 主面板
            GUI.color = new Color(0.07f, 0.05f, 0.04f, 0.96f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.7f, 0.5f, 0.2f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 3f, w, 3f), Texture2D.whiteTexture);
            GUI.color = bg;

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.45f, 1f) }
            };
            GUI.Label(new Rect(x, y + 18f, w, 36f), $"· 奇遇 · {_row.Name_CN} ·", titleStyle);

            // 事件文本
            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.92f, 0.88f, 0.82f, 1f) }
            };
            GUI.Label(new Rect(x + 40f, y + 70f, w - 80f, 180f), _row.Text_CN, textStyle);

            // 选项按钮
            float btnY = y + 270f;
            float btnH = 60f;
            float btnGap = 12f;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 20, 8, 8),
                wordWrap = true
            };

            int optCount = 0;
            foreach (var opt in _row.Options)
            {
                if (opt == null || string.IsNullOrEmpty(opt.Text)) continue;
                if (GUI.Button(new Rect(x + 40f, btnY + optCount * (btnH + btnGap), w - 80f, btnH),
                               BuildOptionLabel(opt), btnStyle))
                {
                    var picked = opt;
                    _visible = false;
                    Cursor.lockState = _prevLock;
                    Cursor.visible = _prevVisible;
                    _onSelected?.Invoke(picked);
                    return;
                }
                optCount++;
            }
        }

        private string BuildOptionLabel(EventOption opt)
        {
            // 在按钮上提示主要后果（玩家可见的代价/收益）
            var tags = new System.Collections.Generic.List<string>();
            if (opt.KarmaChange > 0) tags.Add($"<color=#e87f5b>因果 +{opt.KarmaChange}</color>");
            else if (opt.KarmaChange < 0) tags.Add($"<color=#9ed18c>因果 {opt.KarmaChange}</color>");
            if (opt.DaoxinChange > 0) tags.Add($"<color=#9ed18c>道心 +{opt.DaoxinChange}</color>");
            else if (opt.DaoxinChange < 0) tags.Add($"<color=#e87f5b>道心 {opt.DaoxinChange}</color>");
            if (opt.LifespanChange != 0) tags.Add($"<color=#c89cd8>寿元 {opt.LifespanChange}</color>");
            if (opt.RewardID > 0) tags.Add("<color=#7fb8ff>有奖励</color>");
            if (opt.CostID > 0) tags.Add("<color=#e87f5b>有代价</color>");

            string suffix = tags.Count > 0 ? "  " + string.Join(" · ", tags) : "";
            return $"▸ {opt.Text}{suffix}";
        }
    }
}
