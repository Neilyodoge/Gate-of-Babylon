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
            DestroyIfExists("PlayerInfoPanel");
            DestroyIfExists("CodexUITK");
            DestroyIfExists("SettingsUI");
            DestroyIfExists("BuffBarUITK");
            DestroyIfExists("CaveEconomy");
            DestroyIfExists("SaveSlotSelectUI");
            DestroyIfExists("RewardPickUI");
            DestroyIfExists("BuildBackpackUI");
            DestroyIfExists("RiftManager");
            DestroyIfExists("RiftEquipUI");
            DestroyIfExists("RiftRewardUI");
        }

        private static void DestroyIfExists(string goName)
        {
            var go = GameObject.Find(goName);
            if (go != null) Destroy(go);
        }

        /// <summary>V0.4.1：继续游戏——加载最近使用的存档槽位</summary>
        private static void ContinueGame()
        {
            if (!SaveSystem.Instance.HasActiveSlot)
            {
                int last = PlayerPrefs.GetInt("GoB.LastSaveSlot", -1);
                if (last >= 0 && last < SaveSystem.MaxSlots && SaveSystem.Instance.SlotExists(last))
                    SaveSystem.Instance.LoadSlot(last);
            }
            Hide();
        }

        private static bool HasSave()
        {
            for (int i = 0; i < SaveSystem.MaxSlots; i++)
                if (SaveSystem.Instance.SlotExists(i)) return true;
            return false;
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
            Wire(root, "continue", ContinueGame);
            Wire(root, "info", () => PlayerInfoPanel.Show());
            Wire(root, "codex", () => CodexUITK.Show());
            Wire(root, "settings", () => SettingsUI.Show());
            Wire(root, "quit", QuitGame);

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>V0.4.1：点击「开始游戏」弹出存档选择面板。</summary>
        private static void StartWithTemplate()
        {
            SaveSlotSelectUI.Show(() =>
            {
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
                _continueBtn.text = hasSave ? "继续冒险" : "继续冒险（无存档）";
            }
            if (_saveInfo != null)
            {
                if (hasSave && SaveSystem.Instance.HasActiveSlot)
                {
                    var data = SaveSystem.Instance.Data;
                    int builds = data.buildBackpack?.Count ?? 0;
                    _saveInfo.text = $"通关 {data.totalRunsCompleted}　·　阵亡 {data.totalDeaths}　·　Build×{builds}";
                }
                else if (hasSave)
                {
                    _saveInfo.text = "已有存档 — 点击「开始游戏」选择存档";
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
