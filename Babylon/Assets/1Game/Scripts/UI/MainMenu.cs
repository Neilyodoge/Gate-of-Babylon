using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 主菜单（V0.4.6 改 uGUI+TMP）—— 启动入口 + 返回主菜单。
    ///
    /// UI 用 UGuiKit 代码化构建：屏幕空间 Overlay Canvas（sortingOrder=100），
    /// 全屏遮罩 + 居中标题/副标题/按钮列 + 底部存档信息/版本号。
    /// 中文由动态 TMP 字体 Resources/Fonts/"NotoSansSC SDF" 支撑。
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

        private GameObject _root;          // Canvas 根，整体显隐
        private Button _continueBtn;
        private TextMeshProUGUI _continueLabel;
        private TextMeshProUGUI _saveInfo;

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

        // ========== uGUI 构建 ==========

        private void Awake()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("MainMenuCanvas", 100, transform);
            _root = canvas.gameObject;

            // 全屏遮罩
            UGuiKit.CreateScrim(_root.transform);

            // 居中内容列
            var center = UGuiKit.CreatePanel(_root.transform, "Center", new Vector2(480f, 10f), new Color(0, 0, 0, 0));
            var vfit = center.gameObject.AddComponent<ContentSizeFitter>();
            vfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(center, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(center, "仙 途 秘 境", 64, UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            SetPreferredHeight(title, 80f);

            var subtitle = UGuiKit.CreateText(center, "闯秘境修仙 · 搜打撤 · 洞府养成", 22, UGuiKit.TextDim);
            SetPreferredHeight(subtitle, 34f);

            AddSpacer(center, 12f);

            MakeMenuButton(center, "进入基地", StartWithTemplate, UGuiKit.BtnPrimary);
            _continueBtn = MakeMenuButton(center, "继续修行", ContinueGame, UGuiKit.BtnNormal, out _continueLabel);
            MakeMenuButton(center, "角色信息", () => PlayerInfoPanel.Show(), UGuiKit.BtnNormal);
            MakeMenuButton(center, "图鉴", () => CodexUITK.Show(), UGuiKit.BtnNormal);
            MakeMenuButton(center, "设置", () => SettingsUI.Show(), UGuiKit.BtnNormal);
            MakeMenuButton(center, "退出游戏", QuitGame, UGuiKit.BtnWarn);

            // 底部存档信息
            _saveInfo = UGuiKit.CreateText(_root.transform, "", 20, UGuiKit.TextDim);
            var srt = (RectTransform)_saveInfo.transform;
            srt.anchorMin = new Vector2(0.5f, 0f); srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 64f);
            srt.sizeDelta = new Vector2(900f, 30f);

            // 版本号
            var version = UGuiKit.CreateText(_root.transform, "V0.4.6 · 2026-07", 18, new Color(0.5f, 0.53f, 0.6f, 1f), TextAlignmentOptions.BottomRight);
            var vrt = (RectTransform)version.transform;
            vrt.anchorMin = new Vector2(1f, 0f); vrt.anchorMax = new Vector2(1f, 0f);
            vrt.pivot = new Vector2(1f, 0f);
            vrt.anchoredPosition = new Vector2(-24f, 20f);
            vrt.sizeDelta = new Vector2(320f, 26f);

            _root.SetActive(false);
        }

        private Button MakeMenuButton(RectTransform parent, string text, UnityEngine.Events.UnityAction onClick, Color color)
            => MakeMenuButton(parent, text, onClick, color, out _);

        private Button MakeMenuButton(RectTransform parent, string text, UnityEngine.Events.UnityAction onClick, Color color, out TextMeshProUGUI label)
        {
            var btn = UGuiKit.CreateButton(parent.transform, text, onClick, out label, color, 30, new Vector2(440f, 56f));
            SetPreferredHeight(btn.GetComponent<RectTransform>(), 56f);
            return btn;
        }

        private static void SetPreferredHeight(Component c, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        private static void SetPreferredHeight(RectTransform rt, float h) => SetPreferredHeight((Component)rt, h);

        private static void AddSpacer(RectTransform parent, float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = h;
        }

        /// <summary>V0.4.1：点击「开始游戏」弹出存档选择面板。</summary>
        private static void StartWithTemplate()
        {
            SaveSlotSelectUI.Show(() =>
            {
                Hide();
            });
        }

        private void RefreshDynamic()
        {
            bool hasSave = HasSave();
            if (_continueBtn != null)
                UGuiKit.SetButtonEnabled(_continueBtn, hasSave, UGuiKit.BtnNormal);
            if (_continueLabel != null)
                _continueLabel.text = hasSave ? "继续冒险" : "继续冒险（无存档）";

            if (_saveInfo != null)
            {
                if (hasSave && SaveSystem.Instance.HasActiveSlot)
                {
                    var data = SaveSystem.Instance.Data;
                    int skills = data.unlockedSkillIds?.Count ?? 0;
                    int modules = data.unlockedModuleIds?.Count ?? 0;
                    _saveInfo.text = $"通关 {data.totalRunsCompleted}　·　阵亡 {data.totalDeaths}　·　解锁技能 {skills} / 模块 {modules}";
                }
                else if (hasSave)
                {
                    _saveInfo.text = "已有存档 — 点击「进入基地」选择存档";
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
