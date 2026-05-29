using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 暂停菜单（v0.5 Week 9）—— ESC 打开 / 关闭。
    ///
    /// 按 ESC 时检查是否有其他 UI 在前台（SpiritRootSelectUI / PillCarryUI / Codex / Settings），
    /// 有则不响应（让前台 UI 自己处理 ESC）。
    ///
    /// 暂停时 Time.timeScale=0 但 RunHUD 等 IMGUI 仍会绘制。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private static PauseMenu _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private GUIStyle _titleStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _maskStyle;
        private Texture2D _maskTex;
        private bool _stylesReady;

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

            // 跟 SpiritRootSelectUI 同样的防御：清顿帧
            if (HitStop.Instance != null) HitStop.Instance.ForceClear();

            _instance._previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _instance._previousCursorLock = Cursor.lockState;
            _instance._previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Hide()
        {
            if (_instance == null || !_instance._visible) return;
            _instance._visible = false;

            float prev = _instance._previousTimeScale;
            Time.timeScale = prev >= 0.1f ? prev : 1f;

            Cursor.lockState = _instance._previousCursorLock;
            Cursor.visible = _instance._previousCursorVisible;
        }

        public static void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        // ========== 输入捕获 ==========

        private void Update()
        {
            // 不在战斗 / 洞府场景时不响应（暂停菜单是游戏中 ESC 才出现）
            if (GameManager.Instance == null) return;

            // 前台有其他 UI 时不响应（让它们自己处理 ESC）
            if (IsBlockedByOtherUI()) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private static bool IsBlockedByOtherUI()
        {
            if (SpiritRootSelectUI.IsVisible) return true;
            if (PillCarryUI.IsVisible) return true;
            if (CodexUI.IsVisible) return true;
            if (SettingsUI.IsVisible) return true;
            return false;
        }

        // ========== IMGUI ==========

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _maskTex = new Texture2D(1, 1);
            _maskTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.70f));
            _maskTex.Apply();
            _maskStyle = new GUIStyle();
            _maskStyle.normal.background = _maskTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.92f, 0.78f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            // 全屏半透明遮罩
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _maskStyle);

            // 居中面板
            const float PanelW = 360f, PanelH = 460f;
            float x = (Screen.width - PanelW) * 0.5f;
            float y = (Screen.height - PanelH) * 0.5f;

            // 标题
            GUI.Label(new Rect(x, y + 20f, PanelW, 60f), "≡ 仙途·暂停 ≡", _titleStyle);

            // 按钮区
            float btnY = y + 110f;
            const float BtnW = 280f, BtnH = 48f, BtnGap = 14f;
            float btnX = x + (PanelW - BtnW) * 0.5f;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "▶  继续修行", _btnStyle))
                Hide();
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "📜  仙物图鉴", _btnStyle))
            {
                Hide();
                CodexUI.Show();
            }
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "⚙  设置", _btnStyle))
            {
                SettingsUI.Show();
            }
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "🏠  返回主菜单", _btnStyle))
            {
                // 防误操作：再点一次确认（用 OnGUI 状态简单实现）
                _confirmReturnMain = true;
            }
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "✕  退出游戏", _btnStyle))
            {
                _confirmExit = true;
            }

            // 确认对话框
            if (_confirmReturnMain) DrawConfirmBox(ref _confirmReturnMain, "返回主菜单将丢失本局进度，确定吗？", () =>
            {
                Hide();
                MainMenu.ReturnToMainMenu();
            });

            if (_confirmExit) DrawConfirmBox(ref _confirmExit, "确定要退出游戏吗？", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private bool _confirmReturnMain;
        private bool _confirmExit;

        private void DrawConfirmBox(ref bool flag, string message, System.Action onConfirm)
        {
            const float W = 460f, H = 180f;
            float x = (Screen.width - W) * 0.5f;
            float y = (Screen.height - H) * 0.5f;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _maskStyle);

            GUI.Box(new Rect(x, y, W, H), "");
            var msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            GUI.Label(new Rect(x + 20f, y + 30f, W - 40f, 60f), message, msgStyle);

            if (GUI.Button(new Rect(x + 50f, y + H - 60f, 150f, 40f), "确定", _btnStyle))
            {
                flag = false;
                onConfirm?.Invoke();
            }
            if (GUI.Button(new Rect(x + W - 200f, y + H - 60f, 150f, 40f), "取消", _btnStyle))
            {
                flag = false;
            }
        }
    }
}
