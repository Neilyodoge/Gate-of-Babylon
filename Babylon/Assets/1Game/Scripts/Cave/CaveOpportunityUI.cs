using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府机缘事件 UI（IMGUI 临时实现，模式同 StoryEventUI）。
    /// 玩家点选项 → 触发该选项 effect + 显示结果文本 → 关闭。
    /// </summary>
    public class CaveOpportunityUI : MonoBehaviour
    {
        private static CaveOpportunityUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private CaveOpportunitySystem.Opportunity _opp;
        private string _resultText;   // 选完后显示的结果；null 时显示选项

        public static void Show(CaveOpportunitySystem.Opportunity opp)
        {
            if (opp == null || opp.options == null || opp.options.Count == 0) return;
            if (_instance == null)
            {
                var go = new GameObject("CaveOpportunityUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CaveOpportunityUI>();
            }
            _instance._opp = opp;
            _instance._resultText = null;
            _instance._visible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!_visible || _opp == null) return;

            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = bg;

            const float W = 560f, H = 380f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, rect.height - 36));

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            titleStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
            GUILayout.Label($"✦ 机缘 · {_opp.title}", titleStyle);
            GUILayout.Space(10);

            var textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            textStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f);

            if (_resultText == null)
            {
                GUILayout.Label(_opp.text, textStyle);
                GUILayout.Space(16);
                foreach (var opt in _opp.options)
                {
                    var captured = opt;
                    if (GUILayout.Button(captured.label, GUILayout.Height(38)))
                    {
                        try { captured.effect?.Invoke(); }
                        catch (System.Exception e) { Debug.LogError($"[机缘] 选项 effect 失败：{e.Message}"); }
                        _resultText = captured.resultText;
                    }
                    GUILayout.Space(4);
                }
            }
            else
            {
                GUILayout.Label(_resultText, textStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("离去 [Enter]", GUILayout.Height(36)) ||
                    (Event.current.type == EventType.KeyDown &&
                     (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)))
                {
                    Close();
                }
            }

            GUILayout.EndArea();
        }

        private void Close()
        {
            _visible = false;
            _opp = null;
            _resultText = null;
        }
    }
}
