using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 暂停菜单（v0.6 改 UI Toolkit）—— ESC 打开 / 关闭。
    ///
    /// 结构 Resources/UI/PauseMenu.uxml，样式 PauseMenu.uss，复用 AvatarSelectPanelSettings。
    /// 按 ESC 时检查是否有其他 UI 在前台（选化身 / 机缘 / 图鉴 / 设置），有则不响应。
    /// 暂停时 Time.timeScale=0；确认对话框（返回主菜单 / 退出）也走 UITK。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _confirm;
        private Label _confirmMsg;
        private System.Action _confirmAction;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PauseMenu>();
        }

        // ========== 显示 / 隐藏 ==========

        public static void Show()
        {
            Ensure();
            if (_instance._visible) return;
            _instance._visible = true;

            if (HitStop.Instance != null) HitStop.Instance.ForceClear();

            _instance._previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _instance._previousCursorLock = UnityEngine.Cursor.lockState;
            _instance._previousCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _instance.HideConfirm();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;

            float prev = _instance._previousTimeScale;
            Time.timeScale = prev >= 0.1f ? prev : 1f;

            UnityEngine.Cursor.lockState = _instance._previousCursorLock;
            UnityEngine.Cursor.visible = _instance._previousCursorVisible;

            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        public static void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        // ========== UITK 构建 ==========

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/PauseMenu");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 10f;
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);
            // 样式经 UXML <Style src> 加载（避免 Resources 空规则缓存坑）

            _overlay = root.Q<VisualElement>("overlay");
            _confirm = root.Q<VisualElement>("confirm");
            _confirmMsg = root.Q<Label>("confirm-msg");

            Wire(root, "resume", Hide);
            Wire(root, "codex", () => { Hide(); CodexUITK.Show(); });
            Wire(root, "settings", () => SettingsUI.Show());
            Wire(root, "tomain", () => AskConfirm("返回主菜单将丢失本局进度，确定吗？",
                () => { Hide(); MainMenu.ReturnToMainMenu(); }));
            Wire(root, "quit", () => AskConfirm("确定要退出游戏吗？", QuitGame));

            var ok = root.Q<Button>("confirm-ok");
            if (ok != null) ok.clicked += () => { var a = _confirmAction; HideConfirm(); a?.Invoke(); };
            var cancel = root.Q<Button>("confirm-cancel");
            if (cancel != null) cancel.clicked += HideConfirm;

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            HideConfirm();
        }

        private static void Wire(VisualElement root, string name, System.Action action)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += action;
        }

        private void AskConfirm(string message, System.Action onConfirm)
        {
            _confirmAction = onConfirm;
            if (_confirmMsg != null) _confirmMsg.text = message;
            if (_confirm != null) _confirm.style.display = DisplayStyle.Flex;
        }

        private void HideConfirm()
        {
            _confirmAction = null;
            if (_confirm != null) _confirm.style.display = DisplayStyle.None;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ========== 输入捕获 ==========

        private void Update()
        {
            if (GameManager.Instance == null) return;

            var kb = Keyboard.current;
            bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;

            // 确认框打开时，ESC 先关确认框
            if (_visible && _confirm != null && _confirm.style.display == DisplayStyle.Flex)
            {
                if (esc) HideConfirm();
                return;
            }

            if (IsBlockedByOtherUI()) return;
            if (esc) Toggle();
        }

        private static bool IsBlockedByOtherUI()
        {
            if (ModuleAssemblyUI.IsVisible) return true;
            if (CodexUITK.IsVisible) return true;
            if (SettingsUI.IsVisible) return true;
            return false;
        }
    }
}
