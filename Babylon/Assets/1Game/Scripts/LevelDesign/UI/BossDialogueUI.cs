using System.Collections;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// Boss 出场对白播报 UI（屏幕下方滑入式古风字幕）。
    /// 与 RealmRewardSelectUI 一致使用 IMGUI 临时实现，后期可替换为 UGUI。
    /// </summary>
    public class BossDialogueUI : MonoBehaviour
    {
        private static BossDialogueUI _instance;

        private string _phaseName;
        private string[] _lines;
        private int _currentLineIdx;
        private float _lineStartTime;
        private float _lineDuration = 3.0f;
        private bool _visible;

        public static void Show(string phaseName, string[] lines, float lineDuration = 3f)
        {
            if (lines == null || lines.Length == 0) return;
            if (_instance == null)
            {
                var go = new GameObject("BossDialogueUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<BossDialogueUI>();
            }
            _instance._phaseName = phaseName;
            _instance._lines = lines;
            _instance._lineDuration = lineDuration;
            _instance._currentLineIdx = 0;
            _instance._lineStartTime = Time.unscaledTime;
            _instance._visible = true;
        }

        private void Update()
        {
            if (!_visible) return;
            if (Time.unscaledTime - _lineStartTime >= _lineDuration)
            {
                _currentLineIdx++;
                if (_currentLineIdx >= _lines.Length)
                {
                    _visible = false;
                    return;
                }
                _lineStartTime = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _lines == null || _currentLineIdx >= _lines.Length) return;

            float w = 800f;
            float h = 110f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 80f;

            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.8f, 0.5f, 0.2f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, w, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), Texture2D.whiteTexture);
            GUI.color = bg;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.7f, 0.3f, 1f) }
            };
            GUI.Label(new Rect(x + 20f, y + 10f, w, 24f), $"【{_phaseName}】", nameStyle);

            var lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x + 20f, y + 38f, w - 40f, h - 50f), _lines[_currentLineIdx], lineStyle);
        }

        public static void HideImmediate()
        {
            if (_instance != null) _instance._visible = false;
        }
    }
}
