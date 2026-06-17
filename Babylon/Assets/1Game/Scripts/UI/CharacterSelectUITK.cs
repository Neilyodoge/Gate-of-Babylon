using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 主角（职业）选择面板 · UI Toolkit 版。卡片由 <see cref="PlayerCharacterRegistry"/> 数据生成，
    /// 选择后调用 <see cref="PlayerController.ApplyCharacterProfile"/> 热替换模型 + 普攻形态，
    /// 并写入 <see cref="PlayerCharacterRegistry.Selected"/>（跨局存活）。
    ///
    /// 与化身选择（SpiritRootSelectUITK）正交：这里换"外观 + 近战/远程"，化身换"数值 + 机制"。
    /// 面板渲染复用 AvatarSelectPanelSettings（含中文字体），结构在代码里搭建，无需额外 uxml。
    /// </summary>
    public class CharacterSelectUITK : MonoBehaviour
    {
        private static CharacterSelectUITK _instance;

        private bool _visible;
        private float _previousTimeScale = 1f;

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _cards;

        public static bool IsVisible => _instance != null && _instance._visible;

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null || _instance._visible) return;

            if (HitStop.Instance != null) HitStop.Instance.ForceClear();
            _instance._previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _instance._visible = true;
            _instance.BuildCards();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;

            float prev = _instance._previousTimeScale;
            Time.timeScale = prev >= 0.1f ? prev : 1f;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("CharacterSelectUITK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CharacterSelectUITK>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 11f;   // 在主菜单(0)/化身(10)之上
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            BuildShell();
        }

        private void BuildShell()
        {
            var root = _doc.rootVisualElement;
            if (root == null) return;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.92f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;

            var title = new Label("择 道 · 选择主角");
            title.style.fontSize = 38;
            title.style.color = new Color(1f, 0.88f, 0.5f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            _overlay.Add(title);

            var sub = new Label("剑修近身搏杀，法修御灵远击 —— 道途不同，可随时于问道使处重择");
            sub.style.fontSize = 16;
            sub.style.color = new Color(0.75f, 0.75f, 0.85f);
            sub.style.marginBottom = 24;
            _overlay.Add(sub);

            _cards = new VisualElement();
            _cards.style.flexDirection = FlexDirection.Row;
            _cards.style.justifyContent = Justify.Center;
            _cards.style.flexWrap = Wrap.Wrap;
            _overlay.Add(_cards);

            root.Add(_overlay);
        }

        private void BuildCards()
        {
            if (_cards == null) return;
            _cards.Clear();

            var defs = PlayerCharacterRegistry.All;
            if (defs == null || defs.Count == 0)
            {
                var empty = new Label("未找到主角档案，请先在 Unity 菜单运行『仙途秘境/⑦ 配置法系主角 Mori』");
                empty.style.color = new Color(1f, 0.5f, 0.5f);
                _cards.Add(empty);
                return;
            }

            foreach (var def in defs)
                _cards.Add(MakeCard(def));
        }

        private VisualElement MakeCard(PlayerCharacterProfile def)
        {
            var card = new VisualElement();
            card.style.width = 260;
            card.style.marginLeft = 12; card.style.marginRight = 12;
            card.style.marginTop = 8; card.style.marginBottom = 8;
            card.style.paddingLeft = 18; card.style.paddingRight = 18;
            card.style.paddingTop = 18; card.style.paddingBottom = 18;
            card.style.backgroundColor = new Color(0.10f, 0.11f, 0.16f, 0.95f);
            card.style.borderTopLeftRadius = 10; card.style.borderTopRightRadius = 10;
            card.style.borderBottomLeftRadius = 10; card.style.borderBottomRightRadius = 10;
            SetBorder(card, new Color(def.themeColor.r, def.themeColor.g, def.themeColor.b, 0.6f), 2);

            var accent = new VisualElement();
            accent.style.height = 6;
            accent.style.backgroundColor = def.themeColor;
            accent.style.borderTopLeftRadius = 4; accent.style.borderTopRightRadius = 4;
            accent.style.marginBottom = 12;
            card.Add(accent);

            var name = new Label(def.displayName);
            name.style.fontSize = 26;
            name.style.color = def.themeColor;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(name);

            var role = new Label(string.IsNullOrEmpty(def.roleTag) ? "—" : def.roleTag);
            role.style.fontSize = 15;
            role.style.color = new Color(0.8f, 0.82f, 0.9f);
            role.style.marginBottom = 10;
            card.Add(role);

            var body = new Label(string.IsNullOrEmpty(def.description) ? "" : def.description);
            body.style.fontSize = 14;
            body.style.color = new Color(0.7f, 0.72f, 0.8f);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginBottom = 16;
            body.style.minHeight = 64;
            card.Add(body);

            var captured = def;
            var pick = new Button(() => Pick(captured)) { text = "选 择" };
            pick.style.height = 40;
            pick.style.fontSize = 18;
            pick.style.color = Color.white;
            pick.style.backgroundColor = new Color(def.themeColor.r * 0.5f, def.themeColor.g * 0.5f, def.themeColor.b * 0.5f, 0.9f);
            card.Add(pick);

            return card;
        }

        private static void SetBorder(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Hide();   // ESC 保留当前主角
        }

        private void Pick(PlayerCharacterProfile def)
        {
            if (def == null) { Hide(); return; }

            PlayerCharacterRegistry.Selected = def;
            var player = PlayerController.Instance;
            if (player != null)
                player.ApplyCharacterProfile(def);

            Hide();
        }
    }
}
