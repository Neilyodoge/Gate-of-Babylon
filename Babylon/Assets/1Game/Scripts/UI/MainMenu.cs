using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 主菜单（v0.6 改 UI Toolkit）—— 启动入口 + 返回主菜单。
    ///
    /// 结构 Resources/UI/MainMenu.uxml，样式 MainMenu.uss，复用 AvatarSelectPanelSettings
    /// （UIDocument.sortingOrder=0，弹层 10~14 在其上方）。
    /// 启动流程见 Demo1Setup / GameManager：boot 时 ShowOnBoot 显示并暂停。
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private static MainMenu _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private Button _continueBtn;
        private Label _saveInfo;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MainMenu");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainMenu>();
        }

        public static void ShowOnBoot()
        {
            Ensure();
            Show();
        }

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

            _instance.RefreshDynamic();
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

        public static void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            CleanupSingletons();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void CleanupSingletons()
        {
            DestroyIfExists("CaveInventory");
            DestroyIfExists("InsightSystem");
            DestroyIfExists("GameTime");
            DestroyIfExists("RunHUD");
            DestroyIfExists("PauseMenu");
            DestroyIfExists("MainMenu");
            DestroyIfExists("CodexUITK");
            DestroyIfExists("SettingsUI");
            DestroyIfExists("BuffBarUITK");
            DestroyIfExists("CaveEconomy");

        }

        private static void DestroyIfExists(string goName)
        {
            var go = GameObject.Find(goName);
            if (go != null) Destroy(go);
        }

        private static bool HasSave()
        {
            var data = SaveSystem.Instance.Data;
            return data != null && (data.totalRunsCompleted > 0 || data.totalDeaths > 0 || data.caveQi > 0
                                    || data.unlockedTalentIds.Count > 0
                                    || data.unlockedSkillIds.Count > 0);
        }

        // ========== UITK ==========

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/MainMenu");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 0f;   // 主菜单在最底层，弹层在其上
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);
            // 样式经 UXML <Style src> 加载（避免 Resources 空规则缓存坑）

            _overlay = root.Q<VisualElement>("overlay");
            _continueBtn = root.Q<Button>("continue");
            _saveInfo = root.Q<Label>("saveinfo");

            Wire(root, "start", StartWithTemplate);
            Wire(root, "continue", Hide);
            Wire(root, "codex", () => CodexUITK.Show());
            Wire(root, "settings", () => SettingsUI.Show());
            Wire(root, "quit", QuitGame);

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>V0.1.13：开始前先弹起始模板选择，选中后应用模板并进入游戏。</summary>
        private static void StartWithTemplate()
        {
            StartTemplateSelectUI.Show(tpl =>
            {
                if (tpl != null) tpl.ApplyToPlayer();
                Hide();
            });
        }

        private static void Wire(VisualElement root, string name, System.Action action)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += action;
        }

        private void RefreshDynamic()
        {
            bool hasSave = HasSave();
            if (_continueBtn != null)
            {
                _continueBtn.SetEnabled(hasSave);
                _continueBtn.text = hasSave ? "继续修行" : "继续修行（无存档）";
            }
            if (_saveInfo != null)
            {
                if (hasSave)
                {
                    var data = SaveSystem.Instance.Data;
                    _saveInfo.text = $"灵气 {data.caveQi}　·　通关 {data.totalRunsCompleted}　·　陨落 {data.totalDeaths}　·　天赋 {data.unlockedTalentIds.Count}";
                }
                else _saveInfo.text = "";
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
