using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12 v3：房间之间的"下一间去哪？"3 选 1 选择 UI。
    ///
    /// 设计思路：v0.5 每境只有 2~3 个房间，全图 TreeMap 显得啰嗦。
    /// 改用《杀戮尖塔》/《哈迪斯》风格的"卡片式" 选项：清场后弹出 2~3 张候选卡片，
    /// 玩家点击 → 决定下一间的类型。
    /// 战略层面的全图概览仍由 F8 TreeMapUI（§12.2.1）承载。
    /// </summary>
    public class RoomChoiceUI : MonoBehaviour
    {
        private static RoomChoiceUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        public struct Candidate
        {
            public Minimap.RoomType type;
            public string title;
            public string tooltip;
        }

        private bool _visible;
        private Candidate[] _candidates;
        private Action<Minimap.RoomType> _onSelected;
        private CursorLockMode _prevLock;
        private bool _prevVisible;

        public static void Show(Candidate[] candidates, Action<Minimap.RoomType> onSelected)
        {
            if (candidates == null || candidates.Length == 0)
            {
                onSelected?.Invoke(Minimap.RoomType.Battle);
                return;
            }

            if (_instance == null)
            {
                var go = new GameObject("RoomChoiceUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RoomChoiceUI>();
            }
            _instance._candidates = candidates;
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

        private void Update()
        {
            if (!_visible || _candidates == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            // 数字键 1~9 快捷选择
            int n = Mathf.Min(_candidates.Length, 9);
            for (int i = 0; i < n; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Pick(_candidates[i].type);
                    return;
                }
            }
        }

        private void Pick(Minimap.RoomType t)
        {
            _visible = false;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            _onSelected?.Invoke(t);
        }

        private void OnGUI()
        {
            if (!_visible || _candidates == null) return;

            // 半透明遮罩
            var bg = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = bg;

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.45f, 1f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 50f), "· 下一步去哪？·", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.18f + 50f, Screen.width, 20f),
                      "点击卡片或按数字键 1/2/3 选择路径（选择不可撤销）", subStyle);

            // 卡片布局
            float cardW = 200f;
            float cardH = 260f;
            float gap = 30f;
            int n = _candidates.Length;
            float totalW = n * cardW + (n - 1) * gap;
            float startX = (Screen.width - totalW) * 0.5f;
            float startY = (Screen.height - cardH) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var c = _candidates[i];
                var rect = new Rect(startX + i * (cardW + gap), startY, cardW, cardH);
                DrawCard(rect, c, i + 1);
            }
        }

        private void DrawCard(Rect rect, Candidate c, int hotkey)
        {
            // 卡片背景
            var bg = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // 顶部色带（按房间类型）
            GUI.color = TypeColor(c.type);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 6f), Texture2D.whiteTexture);
            GUI.color = bg;

            // 大图标（用文字代替）
            var iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = TypeColor(c.type) }
            };
            GUI.Label(new Rect(rect.x, rect.y + 30f, rect.width, 100f), TypeIcon(c.type), iconStyle);

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(rect.x, rect.y + 130f, rect.width, 36f), c.title, titleStyle);

            // 描述
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.82f, 0.78f, 1f) }
            };
            GUI.Label(new Rect(rect.x + 10f, rect.y + 170f, rect.width - 20f, 60f), c.tooltip, descStyle);

            // 热键提示
            var hotStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.9f, 0.4f, 1f) }
            };
            GUI.Label(new Rect(rect.x, rect.y + rect.height - 30f, rect.width, 24f), $"[{hotkey}]", hotStyle);

            // 整张卡可点击
            if (GUI.Button(rect, "", GUIStyle.none))
            {
                Pick(c.type);
            }
        }

        private static string TypeIcon(Minimap.RoomType t)
        {
            return t switch
            {
                Minimap.RoomType.Battle => "⚔",
                Minimap.RoomType.Shop => "$",
                Minimap.RoomType.Rest => "♨",
                Minimap.RoomType.Treasure => "宝",
                Minimap.RoomType.Boss => "王",
                Minimap.RoomType.Upgrade => "✦",
                _ => "?"
            };
        }

        private static Color TypeColor(Minimap.RoomType t)
        {
            return t switch
            {
                Minimap.RoomType.Battle => new Color(0.85f, 0.3f, 0.3f),
                Minimap.RoomType.Shop => new Color(1f, 0.85f, 0.3f),
                Minimap.RoomType.Rest => new Color(0.4f, 0.85f, 0.95f),
                Minimap.RoomType.Treasure => new Color(0.95f, 0.7f, 0.2f),
                Minimap.RoomType.Boss => new Color(0.7f, 0.2f, 0.7f),
                Minimap.RoomType.Upgrade => new Color(0.5f, 0.95f, 0.5f),
                _ => Color.gray
            };
        }
    }
}
