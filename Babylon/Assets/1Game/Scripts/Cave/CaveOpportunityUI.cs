using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 洞府机缘事件 UI（v0.6 改 UI Toolkit）。
    /// 玩家点选项 → 触发该选项 effect + 显示结果文本 → 「离去[Enter]」关闭。
    /// 结构 Resources/UI/CaveOpportunityUI.uxml，样式同名 uss（UXML &lt;Style src&gt; 引用）。
    /// 对外保持 Show(opp) / IsVisible。
    /// </summary>
    public class CaveOpportunityUI : MonoBehaviour
    {
        private static CaveOpportunityUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private CaveOpportunitySystem.Opportunity _opp;
        private bool _showingResult;

        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _title;
        private Label _body;
        private VisualElement _options;
        private VisualElement _result;
        private Label _resultText;

        public static void Show(CaveOpportunitySystem.Opportunity opp)
        {
            if (opp == null || opp.options == null || opp.options.Count == 0) return;
            EnsureInstance();
            if (_instance == null) return;

            _instance._opp = opp;
            _instance._showingResult = false;
            _instance._visible = true;
            _instance.Rebuild();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("CaveOpportunityUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CaveOpportunityUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/CaveOpportunityUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _title = root.Q<Label>("title");
            _body = root.Q<Label>("body");
            _options = root.Q<VisualElement>("options");
            _result = root.Q<VisualElement>("result");
            _resultText = root.Q<Label>("result-text");
            var leave = root.Q<Button>("leave");
            if (leave != null) leave.clicked += Close;

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (_opp == null) return;
            if (_title != null) _title.text = $"机缘 · {_opp.title}";
            if (_body != null) _body.text = _opp.text;

            if (_options != null)
            {
                _options.Clear();
                foreach (var opt in _opp.options)
                {
                    var captured = opt;
                    var b = new Button(() => OnPick(captured)) { text = captured.label };
                    b.AddToClassList("op-opt");
                    _options.Add(b);
                }
            }
            SetResultMode(false);
        }

        private void OnPick(CaveOpportunitySystem.Option opt)
        {
            try { opt.effect?.Invoke(); }
            catch (System.Exception e) { Debug.LogError($"[机缘] 选项 effect 失败：{e.Message}"); }

            if (_resultText != null) _resultText.text = opt.resultText;
            SetResultMode(true);
        }

        private void SetResultMode(bool showResult)
        {
            _showingResult = showResult;
            if (_body != null) _body.style.display = showResult ? DisplayStyle.None : DisplayStyle.Flex;
            if (_options != null) _options.style.display = showResult ? DisplayStyle.None : DisplayStyle.Flex;
            if (_result != null) _result.style.display = showResult ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Update()
        {
            if (!_visible || !_showingResult) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                Close();
        }

        private void Close()
        {
            _visible = false;
            _opp = null;
            _showingResult = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }
    }
}
