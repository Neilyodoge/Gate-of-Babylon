using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12 v3：房间之间的"下一间去哪？"3 选 1 选择 UI（v0.6 改 UI Toolkit）。
    /// 卡片式选项：清场后弹出 2~3 张候选卡片，点击或数字键 1/2/3 → 决定下一间类型。
    /// 结构 Resources/UI/RoomChoiceUI.uxml，样式同名 uss。对外保持 Show/HideImmediate/IsVisible。
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

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _cards;

        public static void Show(Candidate[] candidates, Action<Minimap.RoomType> onSelected)
        {
            if (candidates == null || candidates.Length == 0)
            {
                onSelected?.Invoke(Minimap.RoomType.Battle);
                return;
            }

            EnsureInstance();
            if (_instance == null)
            {
                onSelected?.Invoke(candidates[0].type);
                return;
            }

            _instance._candidates = candidates;
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
            var go = new GameObject("RoomChoiceUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RoomChoiceUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/RoomChoiceUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;
            XianTu.ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _cards = root.Q<VisualElement>("cards");
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (_cards == null || _candidates == null) return;
            _cards.Clear();
            for (int i = 0; i < _candidates.Length; i++)
            {
                _cards.Add(MakeCard(_candidates[i], i + 1));
            }
        }

        private VisualElement MakeCard(Candidate c, int hotkey)
        {
            var card = new VisualElement();
            card.AddToClassList("rc-card");

            var accent = new VisualElement();
            accent.AddToClassList("rc-accent");
            accent.style.backgroundColor = TypeColor(c.type);
            card.Add(accent);

            var icon = new Label(TypeIcon(c.type));
            icon.AddToClassList("rc-icon");
            icon.style.color = TypeColor(c.type);
            card.Add(icon);

            var title = new Label(c.title);
            title.AddToClassList("rc-card-title");
            card.Add(title);

            var tip = new Label(c.tooltip);
            tip.AddToClassList("rc-tip");
            card.Add(tip);

            var hot = new Label($"[{hotkey}]");
            hot.AddToClassList("rc-hot");
            card.Add(hot);

            var captured = c.type;
            card.RegisterCallback<ClickEvent>(_ => Pick(captured));
            return card;
        }

        private void Update()
        {
            if (!_visible || _candidates == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
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
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = _prevLock;
            UnityEngine.Cursor.visible = _prevVisible;
            _onSelected?.Invoke(t);
        }

        private static string TypeIcon(Minimap.RoomType t)
        {
            // 用汉字单字图标，避免默认字体缺失 ⚔/✦ 等字形显示空框
            return t switch
            {
                Minimap.RoomType.Battle => "战",
                Minimap.RoomType.Shop => "市",
                Minimap.RoomType.Rest => "憩",
                Minimap.RoomType.Treasure => "宝",
                Minimap.RoomType.Boss => "王",
                Minimap.RoomType.Upgrade => "升",
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
