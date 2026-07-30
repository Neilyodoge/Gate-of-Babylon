using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 暂停菜单（V0.4.6 改 uGUI+TMP）—— ESC 打开 / 关闭。
    ///
    /// UI 用 UGuiKit 代码化构建（Canvas sortingOrder=110）。
    /// 按 ESC 时检查是否有其他 UI 在前台（角色信息 / 图鉴 / 设置 / 模块装配），有则不响应。
    /// 暂停时 Time.timeScale=0；确认对话框（返回主菜单 / 退出）为内嵌子层。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private GameObject _root;
        private GameObject _confirm;
        private TextMeshProUGUI _confirmMsg;
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
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;

            float prev = _instance._previousTimeScale;
            Time.timeScale = prev >= 0.1f ? prev : 1f;

            UnityEngine.Cursor.lockState = _instance._previousCursorLock;
            UnityEngine.Cursor.visible = _instance._previousCursorVisible;

            if (_instance._root != null) _instance._root.SetActive(false);
        }

        public static void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        // ========== uGUI 构建 ==========

        private void Awake()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("PauseMenuCanvas", 110, transform);
            _root = canvas.gameObject;

            UGuiKit.CreateScrim(_root.transform);

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(460f, 10f), UGuiKit.Panel);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(panel, 12f, new RectOffset(28, 28, 28, 28), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(panel, "已 暂 停", 40, UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 56f);

            MakeBtn(panel, "继续游戏", Hide, UGuiKit.BtnPrimary);
            MakeBtn(panel, "角色信息", () => { Hide(); PlayerInfoPanel.Show(); }, UGuiKit.BtnNormal);
            MakeBtn(panel, "图鉴", () => { Hide(); CodexUITK.Show(); }, UGuiKit.BtnNormal);
            MakeBtn(panel, "设置", () => SettingsUI.Show(), UGuiKit.BtnNormal);
            MakeBtn(panel, "返回主菜单", () => AskConfirm("返回主菜单将丢失本局进度，确定吗？",
                () => { Hide(); MainMenu.ReturnToMainMenu(); }), UGuiKit.BtnNormal);
            MakeBtn(panel, "退出游戏", () => AskConfirm("确定要退出游戏吗？", QuitGame), UGuiKit.BtnWarn);

            BuildConfirm();

            _root.SetActive(false);
        }

        private void MakeBtn(RectTransform parent, string text, UnityEngine.Events.UnityAction onClick, Color color)
        {
            var btn = UGuiKit.CreateButton(parent.transform, text, onClick, color, 28, new Vector2(404f, 52f));
            UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 52f);
        }

        private void BuildConfirm()
        {
            _confirm = UGuiKit.CreateStretch(_root.transform, "Confirm").gameObject;
            UGuiKit.CreateScrim(_confirm.transform, new Color(0f, 0f, 0f, 0.6f));

            var panel = UGuiKit.CreatePanel(_confirm.transform, "ConfirmPanel", new Vector2(480f, 220f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 18f, new RectOffset(28, 28, 28, 28), TextAnchor.MiddleCenter);

            _confirmMsg = UGuiKit.CreateText(panel, "", 24, UGuiKit.TextMain, TextAlignmentOptions.Center);
            _confirmMsg.enableWordWrapping = true;
            UGuiKit.SetHeight(_confirmMsg, 90f);

            var row = UGuiKit.CreateRow(panel, 20f, 52f);
            row.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var ok = UGuiKit.CreateButton(row.transform, "确定", () => { var a = _confirmAction; HideConfirm(); a?.Invoke(); }, UGuiKit.BtnWarn, 26, new Vector2(180f, 52f));
            UGuiKit.SetHeight(ok.GetComponent<RectTransform>(), 52f); ok.GetComponent<LayoutElement>().preferredWidth = 180f;
            var cancel = UGuiKit.CreateButton(row.transform, "取消", HideConfirm, UGuiKit.BtnNormal, 26, new Vector2(180f, 52f));
            UGuiKit.SetHeight(cancel.GetComponent<RectTransform>(), 52f); cancel.GetComponent<LayoutElement>().preferredWidth = 180f;

            _confirm.SetActive(false);
        }

        private void AskConfirm(string message, System.Action onConfirm)
        {
            _confirmAction = onConfirm;
            if (_confirmMsg != null) _confirmMsg.text = message;
            if (_confirm != null) _confirm.SetActive(true);
        }

        private void HideConfirm()
        {
            _confirmAction = null;
            if (_confirm != null) _confirm.SetActive(false);
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
            if (_visible && _confirm != null && _confirm.activeSelf)
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
            if (PlayerInfoPanel.IsVisible) return true;
            if (CodexUITK.IsVisible) return true;
            if (SettingsUI.IsVisible) return true;
            return false;
        }
    }
}
