using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// Boss 出场对白播报 UI（v0.6 改 UI Toolkit）——屏幕下方滑入式古风字幕，自动逐行播放。
    /// 结构 Resources/UI/BossDialogueUI.uxml，样式同名 uss。横幅不阻挡输入（pickingMode=Ignore）。
    /// 对外保持 Show(phaseName, lines, dur) / HideImmediate。
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

        private UIDocument _doc;
        private VisualElement _overlay;
        private Label _speaker;
        private Label _line;

        public static void Show(string phaseName, string[] lines, float lineDuration = 3f)
        {
            if (lines == null || lines.Length == 0) return;
            EnsureInstance();
            if (_instance == null) return;

            _instance._phaseName = phaseName;
            _instance._lines = lines;
            _instance._lineDuration = lineDuration;
            _instance._currentLineIdx = 0;
            _instance._lineStartTime = Time.unscaledTime;
            _instance._visible = true;
            _instance.RefreshLine();
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
            var go = new GameObject("BossDialogueUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BossDialogueUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/BossDialogueUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;
            XianTu.ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _speaker = root.Q<Label>("speaker");
            _line = root.Q<Label>("line");
            if (_overlay != null)
            {
                _overlay.pickingMode = PickingMode.Ignore;
                _overlay.style.display = DisplayStyle.None;
            }
        }

        private void Update()
        {
            if (!_visible || _lines == null) return;
            if (Time.unscaledTime - _lineStartTime >= _lineDuration)
            {
                _currentLineIdx++;
                if (_currentLineIdx >= _lines.Length)
                {
                    HideImmediate();
                    return;
                }
                _lineStartTime = Time.unscaledTime;
                RefreshLine();
            }
        }

        private void RefreshLine()
        {
            if (_lines == null || _currentLineIdx < 0 || _currentLineIdx >= _lines.Length) return;
            if (_speaker != null) _speaker.text = $"【{_phaseName}】";
            if (_line != null) _line.text = _lines[_currentLineIdx];
        }
    }
}
