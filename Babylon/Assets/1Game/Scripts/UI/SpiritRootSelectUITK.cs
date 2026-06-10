using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 化身选择面板 · UI Toolkit 版（v0.6 UI 试点；已取代旧 IMGUI 版，旧版已删除）。
    /// 结构来自 Resources/UI/AvatarSelect.uxml，样式来自 AvatarSelect.uss，卡片在代码里按
    /// <see cref="SpiritRootRegistry"/> 数据生成。面板渲染走 AvatarSelectPanelSettings（覆盖层置顶）。
    ///
    /// 对外保持与旧版一致的静态 API（Show / Hide / IsVisible），方便调用方平滑切换。
    /// </summary>
    public class SpiritRootSelectUITK : MonoBehaviour
    {
        private static SpiritRootSelectUITK _instance;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private bool _built;

        private UIDocument _doc;
        private VisualElement _overlay;

        public static bool IsVisible => _instance != null && _instance._visible;

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null || _instance._visible) return;

            if (HitStop.Instance != null) HitStop.Instance.ForceClear();
            _instance._previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _instance._visible = true;
            _instance.Build();
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
            var go = new GameObject("SpiritRootSelectUITK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SpiritRootSelectUITK>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/AvatarSelect");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;   // 弹层在主菜单(0)之上

            var root = _doc.rootVisualElement;
            if (root != null)
            {
                if (root.childCount == 0 && tree != null) tree.CloneTree(root);
                // 样式经 UXML <Style src> 随 VisualTreeAsset 引用加载（避免 Resources 空规则缓存坑）
                _overlay = root.Q<VisualElement>("overlay");
                if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            }
        }

        private void Build()
        {
            if (_built) return;
            if (_doc == null || _doc.rootVisualElement == null) return;

            var cards = _doc.rootVisualElement.Q<VisualElement>("cards");
            if (cards == null) return;
            cards.Clear();

            var defs = SpiritRootRegistry.All;
            for (int i = 0; i < defs.Count; i++)
            {
                cards.Add(MakeCard(defs[i], isDefault: i == 0));
            }
            _built = true;
        }

        private VisualElement MakeCard(SpiritRootDef def, bool isDefault)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            if (isDefault) card.AddToClassList("card--default");

            var accent = new VisualElement();
            accent.AddToClassList("card__accent");
            accent.style.backgroundColor = def.displayColor;
            card.Add(accent);

            if (!string.IsNullOrEmpty(def.roleTag))
            {
                var badge = new VisualElement();
                badge.AddToClassList("card__badge");
                badge.style.display = DisplayStyle.Flex;
                var badgeLabel = new Label(def.roleTag);
                badgeLabel.AddToClassList("card__badge-label");
                badge.Add(badgeLabel);
                card.Add(badge);
            }

            var name = new Label(def.name);
            name.AddToClassList("card__name");
            name.style.color = def.displayColor;
            card.Add(name);

            var mech = new Label(string.IsNullOrEmpty(def.mechanicTitle) ? "—" : def.mechanicTitle);
            mech.AddToClassList("card__mech");
            card.Add(mech);

            var body = new Label(string.IsNullOrEmpty(def.tooltip) ? def.passive : def.tooltip);
            body.AddToClassList("card__body");
            card.Add(body);

            var hint = new Label(def.starterItemHint);
            hint.AddToClassList("card__hint");
            card.Add(hint);

            var captured = def;
            var pick = new Button(() => Pick(captured)) { text = "选 择" };
            pick.AddToClassList("card__pick");
            card.Add(pick);

            return card;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                var defs = SpiritRootRegistry.All;
                if (defs.Count > 0) Pick(defs[0]);
            }
        }

        private void Pick(SpiritRootDef def)
        {
            var player = PlayerController.Instance;
            if (player != null)
            {
                var ctrl = player.GetComponent<SpiritRootController>();
                if (ctrl != null) ctrl.Select(def.type, player.Stats);
            }
            Hide();
        }
    }
}
