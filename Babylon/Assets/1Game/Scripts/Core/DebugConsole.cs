using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 运行时 Debug 控制台
    /// 按 Tab 键或点击左上角小按钮打开/关闭
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        public static DebugConsole Instance { get; private set; }

        private bool _isOpen;
        private GameObject _panelGo;
        private RectTransform _panelRT;
        private Canvas _canvas;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _stopwatchButtonText;
        private GameObject _guidePanelGo;
        private RectTransform _guideFlowContent;
        private ScrollRect _scrollRect;
        private RectTransform _contentRT;
        private GameObject _toggleBtnGo;  // 屏幕角落的开关按钮
        private Canvas _toggleCanvas;     // 开关按钮的独立Canvas

        // Debug 状态
        private bool _godMode;          // 无敌模式
        private bool _lockHp;           // 锁血模式
        private float _lockedHpValue;   // 锁定的血量值
        private bool _oneHitKill;       // 一击必杀
        private float _originalAttack;  // 原始攻击力（用于恢复）
        private bool _speedBoost;       // 加速模式
        private float _originalSpeed;   // 原始移速
        private static bool _stopwatchRunning;
        private static double _stopwatchAccumulated;
        private static double _stopwatchStartedAt;
        private double _nextStopwatchRefreshAt;
        // 日志
        private List<string> _logMessages = new();
        private TextMeshProUGUI _logText;
        private const int MAX_LOG_LINES = 200;

        // 打包可见日志面板（捕获 Application.logMessageReceived）
        private GameObject _logPanelGo;
        private TextMeshProUGUI _logPanelText;
        private bool _logPanelOpen;

        private void Awake()
        {
            Instance = this;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStopwatchSession()
        {
            _stopwatchRunning = false;
            _stopwatchAccumulated = 0d;
            _stopwatchStartedAt = 0d;
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleUnityLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleUnityLog;
        }

        private void Start()
        {
            CreateToggleButton();
        }

        private void Update()
        {
            // Tab 键切换 Debug 面板
            var kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                TogglePanel();

            if (_isOpen &&
                _stopwatchRunning &&
                Time.realtimeSinceStartupAsDouble >=
                _nextStopwatchRefreshAt)
            {
                _nextStopwatchRefreshAt =
                    Time.realtimeSinceStartupAsDouble + 0.1d;
                RefreshStatus();
            }

            // 锁血模式：每帧恢复到锁定值
            if (_lockHp && PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                if (stats.currentHp != _lockedHpValue)
                {
                    stats.currentHp = _lockedHpValue;
                    GameEvents.Publish(new GameEvents.HealthChanged
                    {
                        CurrentHp = stats.currentHp,
                        MaxHp = stats.maxHp
                    });
                }
            }
        }

        private void TogglePanel()
        {
            _isOpen = !_isOpen;
            if (_isOpen && _panelGo == null)
                CreateUI();
            if (_panelGo != null)
                _panelGo.SetActive(_isOpen);
            if (_isOpen)
            {
                RefreshStatus();
                // 打开时解锁鼠标
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                SetGuideVisible(false);
                if (_logPanelGo != null)
                {
                    // 关闭控制台时一并收起日志面板（否则没有按钮可关）
                    _logPanelOpen = false;
                    _logPanelGo.SetActive(false);
                }
            }
        }

        public void OpenPanel()
        {
            if (!_isOpen)
                TogglePanel();
            else
                RefreshStatus();
        }

        public void OpenLevelGuide()
        {
            OpenPanel();
            SetGuideVisible(true);
        }

        private void ToggleLevelGuide()
        {
            SetGuideVisible(_guidePanelGo == null || !_guidePanelGo.activeSelf);
        }

        private void SetGuideVisible(bool visible)
        {
            if (_guidePanelGo != null)
                _guidePanelGo.SetActive(visible);
            if (visible)
            {
                RefreshStatus();
                RebuildEventFlow();
                Canvas.ForceUpdateCanvases();
            }
        }

        // ==================== Debug 功能 ====================

        /// <summary>无敌模式（不受伤害）</summary>
        private void ToggleGodMode()
        {
            _godMode = !_godMode;
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                stats.damageReduction = _godMode ? 1f : (GameConfig.Instance != null ? GameConfig.Instance.玩家减伤比例 : 0f);
            }
            AddLog(_godMode ? "<color=yellow>[无敌] 开启</color>" : "<color=gray>[无敌] 关闭</color>");
            RefreshStatus();
        }

        /// <summary>锁血模式（血量不变）</summary>
        private void ToggleLockHp()
        {
            _lockHp = !_lockHp;
            if (_lockHp && PlayerController.Instance != null)
                _lockedHpValue = PlayerController.Instance.Stats.currentHp;
            AddLog(_lockHp ? $"<color=yellow>[锁血] 开启（锁定在 {_lockedHpValue:F0}）</color>" : "<color=gray>[锁血] 关闭</color>");
            RefreshStatus();
        }

        /// <summary>满血</summary>
        private void FullHeal()
        {
            if (PlayerController.Instance == null) return;
            var stats = PlayerController.Instance.Stats;
            stats.currentHp = stats.maxHp;
            if (_lockHp) _lockedHpValue = stats.maxHp;
            GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = stats.currentHp, MaxHp = stats.maxHp });
            AddLog("<color=green>♥ 已满血</color>");
        }

        /// <summary>一击必杀模式</summary>
        private void ToggleOneHitKill()
        {
            _oneHitKill = !_oneHitKill;
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                if (_oneHitKill)
                {
                    _originalAttack = stats.attackDamage;
                    stats.attackDamage = 99999f;
                }
                else
                {
                    stats.attackDamage = _originalAttack;
                }
            }
            AddLog(_oneHitKill ? "<color=red>[一击必杀] 开启</color>" : "<color=gray>[一击必杀] 关闭</color>");
            RefreshStatus();
        }

        /// <summary>加速模式（3倍移速）</summary>
        private void ToggleSpeedBoost()
        {
            _speedBoost = !_speedBoost;
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                if (_speedBoost)
                {
                    _originalSpeed = stats.moveSpeed;
                    stats.moveSpeed = _originalSpeed * 3f;
                }
                else
                {
                    stats.moveSpeed = _originalSpeed;
                }
            }
            AddLog(_speedBoost ? "<color=cyan>👟 加速模式 开启（3倍）</color>" : "<color=gray>👟 加速模式 关闭</color>");
            RefreshStatus();
        }

        /// <summary>清除所有敌人</summary>
        private void KillAllEnemies()
        {
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            Debug.Log($"[DebugConsole] KillAllEnemies: 找到 {enemies.Length} 个Enemy标签对象");
            int count = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                var damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Debug.Log($"[DebugConsole] 击杀: {enemy.name}, IsAlive={damageable.Stats?.IsAlive}");
                    damageable.OnDamage(999999f, enemy.transform.position, gameObject);
                    count++;
                }
            }
            AddLog($"<color=red>[清场] 已击杀 {count} 个敌人</color>");
        }

        /// <summary>跳转到商店房间</summary>
        private void GotoShopRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(RoomType.Shop);
            AddLog("<color=yellow>$ 跳转到商店房间</color>");
        }

        /// <summary>跳转到战斗房间</summary>
        private void GotoBattleRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(RoomType.Battle);
            AddLog("<color=orange>[战斗] 跳转到战斗房间</color>");
        }

        /// <summary>跳转到Boss房间</summary>
        private void GotoBossRoom()
        {
            if (GameManager.Instance == null) return;
            if (MapProviders.Current is EdgarMapProvider)
            {
                bool success = GameManager.Instance.DebugGotoEdgarRoom(RoomType.Boss);
                AddLog(success
                    ? "<color=red>[Boss] 已直达本局真实 Boss 房</color>"
                    : "<color=red>× 未找到本局 Boss 节点</color>");
                if (success)
                    TogglePanel();
                return;
            }
            GameManager.Instance.DebugGotoRoom(RoomType.Boss);
            AddLog("<color=red>[Boss] 跳转到Boss房间</color>");
        }

        /// <summary>跳转到休息房间</summary>
        private void GotoRestRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(RoomType.Rest);
            AddLog("<color=cyan>♥ 跳转到休息房间</color>");
        }

        /// <summary>跳转到宝箱房间</summary>
        private void GotoTreasureRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(RoomType.Treasure);
            AddLog("<color=orange>★ 跳转到宝箱房间</color>");
        }

        /// <summary>跳转到升级房间</summary>
        private void GotoUpgradeRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(RoomType.Upgrade);
            AddLog("<color=green>↑ 跳转到升级房间</color>");
        }

        private void GotoEdgarNode(string nodeName)
        {
            if (GameManager.Instance == null)
                return;

            bool success = GameManager.Instance.DebugGotoEdgarRoom(nodeName);
            AddLog(success
                ? $"<color=#66ccff>◎ 已直达节点 {nodeName}</color>"
                : $"<color=red>× 无法直达节点 {nodeName}，请先进入 Edgar 秘境</color>");
            if (success)
                TogglePanel();
        }

        private void CompleteLayoutEvent()
        {
            if (MapProviders.Current is not EdgarMapProvider edgar)
            {
                AddLog("<color=red>× 当前不是 Edgar 秘境，无法完成路线事件</color>");
                return;
            }

            bool success = edgar.DebugCompleteLayoutEvent(out string message);
            AddLog(success
                ? $"<color=#80e09b>✓ {message}</color>"
                : $"<color=#ff9a76>× {message}</color>");
            RefreshStatus();
            if (_guidePanelGo != null && _guidePanelGo.activeSelf)
                RebuildEventFlow();
        }

        /// <summary>大量增加灵力碎片</summary>
        private void AddShardsLarge()
        {
            if (PlayerResources.Instance == null) return;
            PlayerResources.Instance.AddShards(5000);
            AddLog("<color=#88CCFF>[资源] +5000 回路碎片</color>");
        }

        /// <summary>强制通关当前房间</summary>
        private void ClearCurrentRoom()
        {
            if (GameManager.Instance == null) return;
            // 先杀所有敌人
            KillAllEnemies();
            // 然后发布通关事件
            GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = GameManager.Instance.CurrentLevel });
            AddLog("<color=green>✓ 强制通关当前房间</color>");
        }

        /// <summary>提升攻击力</summary>
        private void BoostAttack()
        {
            if (PlayerController.Instance == null) return;
            PlayerController.Instance.Stats.attackDamage += 50f;
            AddLog($"<color=red>[攻击] +50（当前：{PlayerController.Instance.Stats.attackDamage:F0}）</color>");
            RefreshStatus();
        }

        /// <summary>提升最大生命</summary>
        private void BoostMaxHp()
        {
            if (PlayerController.Instance == null) return;
            var stats = PlayerController.Instance.Stats;
            stats.maxHp += 100f;
            stats.currentHp += 100f;
            if (_lockHp) _lockedHpValue = stats.currentHp;
            GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = stats.currentHp, MaxHp = stats.maxHp });
            AddLog($"<color=green>♥ 最大生命 +100（当前：{stats.maxHp:F0}）</color>");
            RefreshStatus();
        }

        /// <summary>切换游戏时间缩放</summary>
        private float _timeScaleIndex = 1;
        private readonly float[] _timeScales = { 0.25f, 0.5f, 1f, 2f, 4f };
        private void CycleTimeScale()
        {
            _timeScaleIndex = (_timeScaleIndex + 1) % _timeScales.Length;
            Time.timeScale = _timeScales[(int)_timeScaleIndex];
            AddLog($"<color=cyan>⏱ 时间缩放：{Time.timeScale}x</color>");
            RefreshStatus();
        }

        private void ToggleStopwatch()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_stopwatchRunning)
            {
                _stopwatchAccumulated +=
                    now - _stopwatchStartedAt;
                _stopwatchRunning = false;
                AddLog(
                    $"<color=#FFD66B>⏸ 计时暂停：{FormatStopwatch(StopwatchElapsed)}</color>");
                return;
            }

            _stopwatchStartedAt = now;
            _stopwatchRunning = true;
            AddLog(
                $"<color=#80E09B>▶ 计时开始：{FormatStopwatch(StopwatchElapsed)}</color>");
        }

        private void ResetStopwatch()
        {
            _stopwatchRunning = false;
            _stopwatchAccumulated = 0d;
            _stopwatchStartedAt =
                Time.realtimeSinceStartupAsDouble;
            AddLog("<color=#AAB2C0>↺ 计时器已归零</color>");
        }

        private static double StopwatchElapsed =>
            _stopwatchAccumulated +
            (_stopwatchRunning
                ? Time.realtimeSinceStartupAsDouble -
                  _stopwatchStartedAt
                : 0d);

        public static string FormatStopwatch(double seconds)
        {
            seconds = System.Math.Max(0d, seconds);
            int totalMinutes = (int)(seconds / 60d);
            double remainingSeconds =
                seconds - totalMinutes * 60d;
            return
                $"{totalMinutes:00}:{remainingSeconds:00.0}";
        }

        /// <summary>重新开始</summary>
        private void RestartGame()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Restart();
            AddLog("<color=magenta>↺ 重新开始</color>");
        }

        // ==================== 模块链调试 ====================

        /// <summary>确保玩家身上有模块背包 + 槽位管理器（村庄 Hub 里这俩可能还没创建）。</summary>
        private ModuleInventory EnsureModuleComponents()
        {
            var player = PlayerController.Instance;
            if (player == null) return null;
            var inv = player.GetComponent<ModuleInventory>();
            if (inv == null) inv = player.gameObject.AddComponent<ModuleInventory>();
            if (player.GetComponent<ModuleSlotManager>() == null)
                player.gameObject.AddComponent<ModuleSlotManager>();
            return inv;
        }

        private void GrantAllModules()
        {
            var inv = EnsureModuleComponents();
            if (inv == null) { AddLog("<color=red>玩家不存在</color>"); return; }

            var pool = GetModulePool();
            if (pool == null || pool.Length == 0) { AddLog("<color=red>模块池为空</color>"); return; }

            foreach (var m in pool)
                if (m != null) inv.Add(m);

            AddLog($"<color=#00ffcc>📦 已发放 {pool.Length} 个模块到背包</color>");
            OpenAssemblyUI();
        }

        private void GrantAllModulesX3()
        {
            var inv = EnsureModuleComponents();
            if (inv == null) { AddLog("<color=red>玩家不存在</color>"); return; }

            var pool = GetModulePool();
            if (pool == null || pool.Length == 0) { AddLog("<color=red>模块池为空</color>"); return; }

            for (int r = 0; r < 3; r++)
                foreach (var m in pool)
                    if (m != null) inv.Add(m);

            AddLog($"<color=#00ffcc>📦📦 已发放 {pool.Length * 3} 个模块到背包（每种 x3）</color>");
            OpenAssemblyUI();
        }

        /// <summary>打开模块装配界面（无视战斗状态），让玩家手动配链。</summary>
        private void OpenAssemblyUI()
        {
            if (ModuleAssemblyUI.Instance == null)
            {
                AddLog("<color=yellow>装配界面未初始化（ModuleAssemblyUI 不存在）</color>");
                return;
            }
            ModuleAssemblyUI.Instance.ForceOpen();
            // 关掉 Debug 面板，避免遮挡装配界面
            _isOpen = false;
            if (_panelGo != null) _panelGo.SetActive(false);
            AddLog("<color=#9be0c0>🔧 已打开回路装配界面 · 手动接通Q/E/R载体</color>");
        }

        private void AutoAssembleQChain()
        {
            if (PlayerController.Instance == null) return;
            var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
            var slots = PlayerController.Instance.GetComponent<ModuleSlotManager>();
            if (inv == null || slots == null) { AddLog("<color=red>模块系统组件未找到</color>"); return; }

            var triggers = inv.GetForSlot(0);
            var effects = inv.GetForSlot(1);
            var modifiers = inv.GetByCategory(ModuleCategory.Modifier);

            if (triggers.Count == 0 || effects.Count == 0)
            {
                AddLog("<color=yellow>背包中缺少可放入触发器/效果器槽位的模块</color>");
                return;
            }

            var chain = new ModuleChain
            {
                trigger = triggers[0],
                effect = effects[0],
                modifier0 = modifiers.Count > 0 ? modifiers[0] : null
            };
            slots.EquipChain(0, chain);
            AddLog($"<color=#00ffcc>[回路] Q槽已装配：{chain.DisplayName}</color>");
        }

        private void AutoAssembleAllChains()
        {
            if (PlayerController.Instance == null) return;
            var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
            var slots = PlayerController.Instance.GetComponent<ModuleSlotManager>();
            if (inv == null || slots == null) { AddLog("<color=red>模块系统组件未找到</color>"); return; }

            var triggers = inv.GetForSlot(0);
            var effects = inv.GetForSlot(1);
            var modifiers = inv.GetByCategory(ModuleCategory.Modifier);

            int assembled = 0;
            var usedT = new System.Collections.Generic.HashSet<ModuleDef>();
            var usedE = new System.Collections.Generic.HashSet<ModuleDef>();

            for (int s = 0; s < 3; s++)
            {
                ModuleDef t = null, e = null, m = null;
                foreach (var tr in triggers) { if (!usedT.Contains(tr)) { t = tr; break; } }
                foreach (var ef in effects) { if (!usedE.Contains(ef)) { e = ef; break; } }
                if (t == null || e == null) break;

                usedT.Add(t);
                usedE.Add(e);
                if (modifiers.Count > s) m = modifiers[s];

                var chain = new ModuleChain { trigger = t, effect = e, modifier0 = m };
                slots.EquipChain(s, chain);
                assembled++;
                AddLog($"<color=#00ffcc>[回路] {(s == 0 ? "Q" : s == 1 ? "E" : "R")}槽：{chain.DisplayName}</color>");
            }
            AddLog($"<color=cyan>共装配 {assembled} 条链</color>");
        }

        private void ClearAllModules()
        {
            if (PlayerController.Instance == null) return;
            var inv = PlayerController.Instance.GetComponent<ModuleInventory>();
            var slots = PlayerController.Instance.GetComponent<ModuleSlotManager>();
            if (inv != null) inv.Clear();
            if (slots != null) slots.ClearAll();
            AddLog("<color=gray>🗑 模块背包 + 链槽位已清空</color>");
        }

        private ModuleDef[] GetModulePool()
        {
            if (GameManager.Instance != null)
            {
                var field = typeof(GameManager).GetField("modulePool",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var pool = field.GetValue(GameManager.Instance) as ModuleDef[];
                    if (pool != null && pool.Length > 0) return pool;
                }
            }
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ModuleDef", new[] { "Assets/1Game/Data/Modules" });
            var list = new System.Collections.Generic.List<ModuleDef>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var m = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleDef>(path);
                if (m != null) list.Add(m);
            }
            return list.ToArray();
#else
            return System.Array.Empty<ModuleDef>();
#endif
        }

        // ==================== UI 创建 ====================

        /// <summary>创建屏幕左上角的Debug开关小按钮（始终可见）</summary>
        private void CreateToggleButton()
        {
            var canvasGo = new GameObject("DebugToggleCanvas");
            canvasGo.transform.SetParent(transform);
            _toggleCanvas = canvasGo.AddComponent<Canvas>();
            _toggleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _toggleCanvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _toggleBtnGo = new GameObject("DebugToggleBtn");
            _toggleBtnGo.transform.SetParent(canvasGo.transform, false);
            var btnRT = _toggleBtnGo.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0, 1);
            btnRT.anchorMax = new Vector2(0, 1);
            btnRT.pivot = new Vector2(0, 1);
            btnRT.anchoredPosition = new Vector2(8, -8);
            btnRT.sizeDelta = new Vector2(70, 26);

            var btnImg = _toggleBtnGo.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.1f, 0.18f, 0.6f);

            var btn = _toggleBtnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.2f, 0.35f, 0.9f);
            colors.pressedColor = new Color(0.08f, 0.06f, 0.12f, 0.9f);
            btn.colors = colors;
            btn.onClick.AddListener(TogglePanel);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_toggleBtnGo.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = "Debug";
            txt.fontSize = 13;
            if (UGuiKit.CjkFont != null) txt.font = UGuiKit.CjkFont;
            txt.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.outlineColor = new Color(0, 0, 0, 0.7f);
            txt.outlineWidth = 0.2f;
        }

        private void CreateUI()
        {
            // 屏幕空间 Canvas
            var canvasGo = new GameObject("DebugCanvas");
            canvasGo.transform.SetParent(transform);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999; // 最高层级
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            UGuiKit.EnsureEventSystem();

            // 主面板只保留Playtest计时器。
            _panelGo = new GameObject("DebugPanel");
            _panelGo.transform.SetParent(canvasGo.transform, false);
            _panelRT = _panelGo.AddComponent<RectTransform>();
            _panelRT.anchorMin = _panelRT.anchorMax =
                new Vector2(0f, 1f);
            _panelRT.pivot = new Vector2(0f, 1f);
            _panelRT.anchoredPosition = new Vector2(10f, -42f);
            _panelRT.sizeDelta = new Vector2(330f, 174f);
            var panelImg = _panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            CreateLabel(
                _panelGo.transform,
                "Title",
                "Playtest 计时器　(Tab关闭)",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(10f, -6f),
                new Vector2(-10f, -36f),
                16,
                new Color(1f, 0.85f, 0.3f),
                FontStyle.Bold);

            var statusGo = CreateLabel(
                _panelGo.transform,
                "Status",
                "",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, -42f),
                new Vector2(-12f, -100f),
                30,
                new Color(1f, 0.86f, 0.42f),
                FontStyle.Bold);
            _statusText = statusGo.GetComponent<TextMeshProUGUI>();
            _statusText.alignment = TextAlignmentOptions.Center;

            GameObject controls = new(
                "TimerControls",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            controls.transform.SetParent(_panelGo.transform, false);
            RectTransform controlsRect =
                controls.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 0f);
            controlsRect.anchorMax = new Vector2(1f, 0f);
            controlsRect.pivot = new Vector2(0.5f, 0f);
            controlsRect.offsetMin = new Vector2(12f, 14f);
            controlsRect.offsetMax = new Vector2(-12f, 58f);
            HorizontalLayoutGroup layout =
                controls.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            Button toggle = CreateButton(
                controls.transform,
                "▶ 开始",
                new Color(0.18f, 0.42f, 0.3f),
                ToggleStopwatch);
            _stopwatchButtonText =
                toggle.GetComponentInChildren<TextMeshProUGUI>();
            CreateButton(
                controls.transform,
                "↺ 重置",
                new Color(0.34f, 0.28f, 0.32f),
                ResetStopwatch);
            RefreshStatus();
        }

        private void CreateGuidePanel(Transform parent)
        {
            _guidePanelGo = new GameObject("EventDebugPanel", typeof(RectTransform), typeof(Image));
            _guidePanelGo.transform.SetParent(parent, false);
            var panelRect = _guidePanelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 0.5f);
            panelRect.offsetMin = new Vector2(310f, 10f);
            panelRect.offsetMax = new Vector2(1170f, -10f);

            var image = _guidePanelGo.GetComponent<Image>();
            image.color = new Color(0.07f, 0.11f, 0.16f, 0.96f);
            image.raycastTarget = false;

            CreateLabel(_guidePanelGo.transform, "Title", "═══ 事件 Debug · 关卡提示 ═══",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(8f, -8f), new Vector2(-8f, -42f),
                16, new Color(1f, 0.85f, 0.3f), FontStyle.Bold);

            CreateLabel(_guidePanelGo.transform, "Legend",
                "流程：遇到事件  →  三选一  →  立即生效  →  永夜变化　　<color=#8fe3ff>亮色=可选</color>　<color=#80e09b>绿色=已选</color>　<color=#777777>灰色=不可选</color>",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -42f), new Vector2(-16f, -72f),
                12, new Color(0.76f, 0.8f, 0.86f), FontStyle.Normal);

            var scrollGo = new GameObject("EventFlowScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_guidePanelGo.transform, false);
            var scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(12f, 12f);
            scrollRectTransform.offsetMax = new Vector2(-12f, -78f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var contentGo = new GameObject("EventFlowContent", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _guideFlowContent = contentGo.GetComponent<RectTransform>();
            _guideFlowContent.anchorMin = new Vector2(0f, 1f);
            _guideFlowContent.anchorMax = new Vector2(1f, 1f);
            _guideFlowContent.pivot = new Vector2(0.5f, 1f);
            _guideFlowContent.offsetMin = Vector2.zero;
            _guideFlowContent.offsetMax = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.content = _guideFlowContent;

            _guidePanelGo.SetActive(false);
        }

        private void RebuildEventFlow()
        {
            if (_guideFlowContent == null)
                return;

            for (int i = _guideFlowContent.childCount - 1; i >= 0; i--)
                Destroy(_guideFlowContent.GetChild(i).gameObject);

            CreateFlowText(_guideFlowContent, LevelGuidePresenter.BuildFlowSummary(),
                13, new Color(0.65f, 0.88f, 1f), 28f, FontStyles.Bold);
            foreach (var flowEvent in LevelGuidePresenter.BuildFlowData())
                CreateEventFlowGroup(_guideFlowContent, flowEvent);
        }

        private void CreateEventFlowGroup(
            Transform parent,
            LevelGuidePresenter.FlowEvent flowEvent)
        {
            var group = new GameObject($"Flow_{flowEvent.Name}", typeof(RectTransform), typeof(Image));
            group.transform.SetParent(parent, false);
            var groupLayout = group.AddComponent<LayoutElement>();
            groupLayout.preferredHeight = 218f;
            groupLayout.minHeight = 218f;
            var groupImage = group.GetComponent<Image>();
            groupImage.color = flowEvent.IsAvailable
                ? new Color(0.12f, 0.2f, 0.28f, 0.98f)
                : flowEvent.IsCompleted
                    ? new Color(0.11f, 0.24f, 0.17f, 0.98f)
                    : new Color(0.12f, 0.13f, 0.15f, 0.98f);

            var vertical = group.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 3f;
            vertical.padding = new RectOffset(10, 10, 7, 7);
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            Color headerColor = flowEvent.IsAvailable
                ? new Color(1f, 0.82f, 0.42f)
                : flowEvent.IsCompleted
                    ? new Color(0.5f, 0.9f, 0.62f)
                    : new Color(0.48f, 0.5f, 0.54f);
            CreateFlowText(group.transform,
                $"{flowEvent.Name}　<color=#aab2c0>[{flowEvent.Status}]</color>",
                15, headerColor, 26f, FontStyles.Bold);
            CreateFlowText(group.transform, "↓　选择其一", 12,
                flowEvent.IsAvailable ? new Color(0.55f, 0.85f, 1f) : new Color(0.4f, 0.42f, 0.46f),
                19f, FontStyles.Normal, TextAlignmentOptions.Center);

            var options = new GameObject("Options", typeof(RectTransform));
            options.transform.SetParent(group.transform, false);
            var optionsLayoutElement = options.AddComponent<LayoutElement>();
            optionsLayoutElement.preferredHeight = 150f;
            optionsLayoutElement.minHeight = 150f;
            var horizontal = options.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 8f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;

            for (int i = 0; i < flowEvent.Options.Count; i++)
                CreateOptionCard(options.transform, flowEvent.Options[i], i + 1);
        }

        private void CreateOptionCard(
            Transform parent,
            LevelGuidePresenter.FlowOption option,
            int index)
        {
            var card = new GameObject($"Option_{index}", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var cardLayout = card.AddComponent<LayoutElement>();
            cardLayout.minWidth = 220f;
            cardLayout.flexibleWidth = 1f;
            var image = card.GetComponent<Image>();
            image.color = option.State switch
            {
                LevelGuidePresenter.FlowOptionState.Selected =>
                    new Color(0.12f, 0.38f, 0.22f, 0.98f),
                LevelGuidePresenter.FlowOptionState.Available =>
                    new Color(0.08f, 0.3f, 0.42f, 0.98f),
                _ => new Color(0.16f, 0.17f, 0.19f, 0.98f),
            };

            var vertical = card.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 3f;
            vertical.padding = new RectOffset(9, 9, 7, 7);
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            bool dimmed = option.State == LevelGuidePresenter.FlowOptionState.Unavailable;
            Color titleColor = dimmed
                ? new Color(0.48f, 0.49f, 0.52f)
                : Color.white;
            Color bodyColor = dimmed
                ? new Color(0.4f, 0.41f, 0.44f)
                : new Color(0.82f, 0.86f, 0.92f);
            Color stateColor = option.State switch
            {
                LevelGuidePresenter.FlowOptionState.Selected => new Color(0.55f, 1f, 0.65f),
                LevelGuidePresenter.FlowOptionState.Available => new Color(0.55f, 0.9f, 1f),
                _ => new Color(0.48f, 0.49f, 0.52f),
            };

            CreateFlowText(card.transform, $"{index}. {option.Title}",
                14, titleColor, 30f, FontStyles.Bold);
            CreateFlowText(card.transform, $"现 → {option.Immediate}",
                11, bodyColor, 35f, FontStyles.Normal);
            CreateFlowText(card.transform, $"夜 → {option.Night}",
                11, bodyColor, 35f, FontStyles.Normal);
            CreateFlowText(card.transform, option.StateLabel,
                11, stateColor, 23f, FontStyles.Bold, TextAlignmentOptions.Center);
        }

        private static TextMeshProUGUI CreateFlowText(
            Transform parent,
            string text,
            int fontSize,
            Color color,
            float height,
            FontStyles style,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = UGuiKit.CreateText(parent, text, fontSize, color, alignment, style);
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            return label;
        }

        // ==================== UI 辅助方法 ====================

        private GameObject CreateLabel(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = text;
            txt.fontSize = fontSize;
            if (UGuiKit.CjkFont != null) txt.font = UGuiKit.CjkFont;
            txt.color = color;
            txt.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            txt.alignment = TextAlignmentOptions.Center;
            txt.richText = true;
            txt.raycastTarget = false;
            txt.enableWordWrapping = false;
            txt.overflowMode = TextOverflowModes.Overflow;
            txt.outlineColor = new Color(0, 0, 0, 0.8f);
            txt.outlineWidth = 0.2f;
            return go;
        }

        private Button CreateButton(
            Transform parent,
            string label,
            Color bgColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject($"Btn_{label}");
            btnGo.transform.SetParent(parent, false);
            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = 32;
            le.minHeight = 32;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = bgColor;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            // 按钮高亮色
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.7f;
            btn.colors = colors;

            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8, 0);
            textRT.offsetMax = new Vector2(-8, 0);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 14;
            if (UGuiKit.CjkFont != null) txt.font = UGuiKit.CjkFont;
            txt.color = Color.white;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.outlineColor = new Color(0, 0, 0, 0.7f);
            txt.outlineWidth = 0.2f;
            return btn;
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var go = new GameObject($"Header_{title}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 22;
            le.minHeight = 22;

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = title;
            txt.fontSize = 12;
            if (UGuiKit.CjkFont != null) txt.font = UGuiKit.CjkFont;
            txt.color = new Color(0.8f, 0.7f, 0.5f, 0.9f);
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Left;
            txt.raycastTarget = false;
        }

        private void CreateSeparator(Transform parent, float yAnchor)
        {
            var go = new GameObject("Sep");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.05f, yAnchor);
            rt.anchorMax = new Vector2(0.95f, yAnchor);
            rt.sizeDelta = new Vector2(0, 1);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 0.4f, 0.3f, 0.4f);
            img.raycastTarget = false;
        }

        // ==================== 状态刷新 ====================

        private void RefreshStatus()
        {
            if (_statusText == null) return;

            _statusText.text =
                $"{FormatStopwatch(StopwatchElapsed)}\n" +
                (_stopwatchRunning
                    ? "<color=#80E09B>运行中</color>"
                    : "<color=#AAB2C0>已暂停</color>");
            if (_stopwatchButtonText != null)
            {
                _stopwatchButtonText.text =
                    _stopwatchRunning ? "⏸ 暂停" : "▶ 开始";
            }
        }

        private string BoolStr(bool v) => v ? "<color=yellow>ON</color>" : "<color=gray>OFF</color>";

        // ==================== 日志 ====================

        private void AddLog(string msg)
        {
            // 经 Debug.Log 输出 → 由 HandleUnityLog 统一捕获显示（避免重复入列）
            Debug.Log($"[DebugConsole] {msg}");
            RefreshStatus();
        }

        /// <summary>捕获所有 Unity 日志（含报错/异常），打包版也能在日志面板看到。</summary>
        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            string color =
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) ? "#ff6b6b" :
                (type == LogType.Warning) ? "#ffd24d" : "#cfd2d6";
            string prefix =
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) ? "[ERR] " :
                (type == LogType.Warning) ? "[WARN] " : "";

            string line = $"<color={color}>{prefix}{condition}</color>";

            // 异常/报错附第一行堆栈，便于打包版定位
            if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stackTrace))
            {
                int nl = stackTrace.IndexOf('\n');
                string firstFrame = nl > 0 ? stackTrace.Substring(0, nl) : stackTrace;
                line += $"  <color=#888888>@ {firstFrame}</color>";
            }

            _logMessages.Add(line);
            if (_logMessages.Count > MAX_LOG_LINES)
                _logMessages.RemoveAt(0);

            if (_logText != null)
                _logText.text = _logMessages[_logMessages.Count - 1];
            if (_logPanelOpen && _logPanelText != null)
                RefreshLogPanel();
        }

        // ==================== 打包可见日志面板 ====================

        private void ToggleLogPanel()
        {
            _logPanelOpen = !_logPanelOpen;
            if (_logPanelOpen && _logPanelGo == null)
                CreateLogPanel();
            if (_logPanelGo != null)
                _logPanelGo.SetActive(_logPanelOpen);
            if (_logPanelOpen)
                RefreshLogPanel();
        }

        private void CreateLogPanel()
        {
            Transform parent = _canvas != null ? _canvas.transform : transform;

            _logPanelGo = new GameObject("LogPanel");
            _logPanelGo.transform.SetParent(parent, false);
            var rt = _logPanelGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);
            var img = _logPanelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.82f);
            img.raycastTarget = false;

            // 标题
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_logPanelGo.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 1);
            trt.anchorMax = new Vector2(1, 1);
            trt.offsetMin = new Vector2(8, -24);
            trt.offsetMax = new Vector2(-8, -4);
            var ttxt = titleGo.AddComponent<TextMeshProUGUI>();
            ttxt.text = "═══ 运行日志（最近 40 行）═══";
            if (UGuiKit.CjkFont != null) ttxt.font = UGuiKit.CjkFont;
            ttxt.fontSize = 13;
            ttxt.fontStyle = FontStyles.Bold;
            ttxt.color = new Color(1f, 0.85f, 0.3f);
            ttxt.raycastTarget = false;

            var txtGo = new GameObject("LogText");
            txtGo.transform.SetParent(_logPanelGo.transform, false);
            var ltrt = txtGo.AddComponent<RectTransform>();
            ltrt.anchorMin = Vector2.zero;
            ltrt.anchorMax = Vector2.one;
            ltrt.offsetMin = new Vector2(8, 8);
            ltrt.offsetMax = new Vector2(-8, -28);
            _logPanelText = txtGo.AddComponent<TextMeshProUGUI>();
            if (UGuiKit.CjkFont != null) _logPanelText.font = UGuiKit.CjkFont;
            _logPanelText.fontSize = 12;
            _logPanelText.color = Color.white;
            _logPanelText.alignment = TextAlignmentOptions.BottomLeft;
            _logPanelText.richText = true;
            _logPanelText.raycastTarget = false;
            _logPanelText.enableWordWrapping = true;
            _logPanelText.overflowMode = TextOverflowModes.Truncate;
        }

        private void RefreshLogPanel()
        {
            if (_logPanelText == null) return;
            int start = Mathf.Max(0, _logMessages.Count - 40);
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < _logMessages.Count; i++)
                sb.AppendLine(_logMessages[i]);
            _logPanelText.text = sb.ToString();
        }

    }
}
