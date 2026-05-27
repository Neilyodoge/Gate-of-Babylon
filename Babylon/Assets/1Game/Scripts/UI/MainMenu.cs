using UnityEngine;
using UnityEngine.SceneManagement;

namespace XianTu
{
    /// <summary>
    /// 主菜单（v0.5 Week 9）—— 启动入口 + 返回主菜单。
    ///
    /// 启动流程：
    /// 1. Demo1Setup.Awake 创建场景对象（包括 PauseMenu / MainMenu 单例）
    /// 2. GameManager.Awake 后立刻调用 <see cref="ShowOnBoot"/> 显示主菜单 + Time.timeScale=0
    /// 3. 玩家点"开始新局" → <see cref="StartNewRunPressed"/> 隐藏菜单 + 恢复时间
    /// 4. 暂停菜单选"返回主菜单" → <see cref="ReturnToMainMenu"/> 重载当前场景
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private static MainMenu _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _maskStyle;
        private Texture2D _maskTex;
        private bool _stylesReady;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("MainMenu");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainMenu>();
        }

        /// <summary>启动时调用：第一次显示主菜单（暂停游戏）</summary>
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

        /// <summary>返回主菜单：重启当前场景（销毁所有运行时对象，状态清零）</summary>
        public static void ReturnToMainMenu()
        {
            // 恢复时间，避免新场景加载时被锁在 0
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 清掉 DontDestroyOnLoad 上的所有玩法单例，避免新一局复用旧状态
            CleanupSingletons();

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void CleanupSingletons()
        {
            // 这些单例都是用 DontDestroyOnLoad 创建在独立 GO 上的，重载场景时不会自动销毁
            // 必须显式清理，否则会出现"主菜单后开始新局，CaveInventory 还有上局缓冲"等奇怪状态
            DestroyIfExists("CaveInventory");
            DestroyIfExists("InsightSystem");
            DestroyIfExists("GameTime");
            DestroyIfExists("RunHUD");
            DestroyIfExists("PauseMenu");
            DestroyIfExists("MainMenu");
            DestroyIfExists("CodexUI");
            DestroyIfExists("SettingsUI");
            DestroyIfExists("PillCarryUI");
            DestroyIfExists("SpiritRootSelectUI");
            DestroyIfExists("StatusEffectHUD");
            DestroyIfExists("SpiritRootMechanicHUD");
            DestroyIfExists("RealmRewardSelectUI");
            DestroyIfExists("CaveEconomy");

            // 清掉 InsightSystem / PendingPillCarry 等运行时状态
            PendingPillCarry.ClearPending();
            PendingPillCarry.ClearActive();
            SynergySystem.Clear();
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
                                    || data.unlockedSkillIds.Count > 0
                                    || data.unlockedItemIds.Count > 0);
        }

        // ========== IMGUI ==========

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _maskTex = new Texture2D(1, 1);
            _maskTex.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.08f, 0.96f));
            _maskTex.Apply();
            _maskStyle = new GUIStyle();
            _maskStyle.normal.background = _maskTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(1f, 0.92f, 0.65f);

            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f, 0.85f);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter
            };
            _footerStyle.normal.textColor = new Color(0.55f, 0.58f, 0.65f, 0.7f);

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            // 不透明黑底
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _maskStyle);

            // 标题
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 100f), "仙途梦境", _titleStyle);
            GUI.Label(new Rect(0, Screen.height * 0.18f + 96f, Screen.width, 30f),
                "梦中修仙 · 搜打撤 · 洞府种田", _subtitleStyle);

            // 按钮区
            const float BtnW = 320f, BtnH = 58f, BtnGap = 16f;
            float btnX = (Screen.width - BtnW) * 0.5f;
            float btnY = Screen.height * 0.45f;

            bool hasSave = HasSave();

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "▶  开始入梦", _btnStyle))
            {
                Hide();
            }
            btnY += BtnH + BtnGap;

            // 继续游戏：有存档时启用
            GUI.enabled = hasSave;
            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH),
                hasSave ? "♻  继续修行" : "♻  继续修行（无存档）", _btnStyle))
            {
                Hide();
            }
            GUI.enabled = true;
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "📜  仙物图鉴", _btnStyle))
            {
                CodexUI.Show();
            }
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "⚙  设置", _btnStyle))
            {
                SettingsUI.Show();
            }
            btnY += BtnH + BtnGap;

            if (GUI.Button(new Rect(btnX, btnY, BtnW, BtnH), "✕  退出游戏", _btnStyle))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // 存档信息（左下）
            if (hasSave)
            {
                var data = SaveSystem.Instance.Data;
                string info = $"灵气 {data.caveQi}  ·  通关 {data.totalRunsCompleted}  ·  入魔 {data.totalDeaths}  ·  天赋 {data.unlockedTalentIds.Count}";
                GUI.Label(new Rect(0, Screen.height - 60f, Screen.width, 20f), info, _footerStyle);
            }

            // 版本号（右下）
            GUI.Label(new Rect(0, Screen.height - 30f, Screen.width, 20f),
                "v0.5 Demo2 · 2026-05", _footerStyle);
        }
    }
}
