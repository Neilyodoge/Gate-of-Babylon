using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
        private Canvas _canvas;
        private Text _statusText;
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
        private bool _maxDropRate;      // 爆率拉满模式

        // 日志
        private List<string> _logMessages = new();
        private Text _logText;
        private const int MAX_LOG_LINES = 50;

        private void Awake()
        {
            Instance = this;
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
            AddLog(_godMode ? "<color=yellow>✦ 无敌模式 开启</color>" : "<color=gray>✦ 无敌模式 关闭</color>");
            RefreshStatus();
        }

        /// <summary>锁血模式（血量不变）</summary>
        private void ToggleLockHp()
        {
            _lockHp = !_lockHp;
            if (_lockHp && PlayerController.Instance != null)
                _lockedHpValue = PlayerController.Instance.Stats.currentHp;
            AddLog(_lockHp ? $"<color=yellow>✦ 锁血模式 开启（锁定在 {_lockedHpValue:F0}）</color>" : "<color=gray>✦ 锁血模式 关闭</color>");
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
            AddLog(_oneHitKill ? "<color=red>⚔ 一击必杀 开启</color>" : "<color=gray>⚔ 一击必杀 关闭</color>");
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
            int count = 0;
            foreach (var enemy in enemies)
            {
                var damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.OnDamage(999999f, enemy.transform.position, gameObject);
                    count++;
                }
            }
            AddLog($"<color=red>☠ 已击杀 {count} 个敌人</color>");
        }

        /// <summary>跳转到商店房间</summary>
        private void GotoShopRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Shop);
            AddLog("<color=yellow>$ 跳转到商店房间</color>");
        }

        /// <summary>跳转到战斗房间</summary>
        private void GotoBattleRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Battle);
            AddLog("<color=orange>⚔ 跳转到战斗房间</color>");
        }

        /// <summary>跳转到Boss房间</summary>
        private void GotoBossRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Boss);
            AddLog("<color=red>☠ 跳转到Boss房间</color>");
        }

        /// <summary>跳转到休息房间</summary>
        private void GotoRestRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Rest);
            AddLog("<color=cyan>♥ 跳转到休息房间</color>");
        }

        /// <summary>跳转到宝箱房间</summary>
        private void GotoTreasureRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Treasure);
            AddLog("<color=orange>★ 跳转到宝箱房间</color>");
        }

        /// <summary>增加灵力碎片</summary>
        private void AddShards()
        {
            if (PlayerResources.Instance == null) return;
            PlayerResources.Instance.AddShards(500);
            AddLog("<color=#88CCFF>✦ +500 灵力碎片</color>");
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
            AddLog($"<color=red>⚔ 攻击力 +50（当前：{PlayerController.Instance.Stats.attackDamage:F0}）</color>");
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

        /// <summary>重新开始</summary>
        private void RestartGame()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Restart();
            AddLog("<color=magenta>↺ 重新开始</color>");
        }

        /// <summary>爆率拉满模式（每个敌人必掉灵物）</summary>
        private void ToggleMaxDropRate()
        {
            _maxDropRate = !_maxDropRate;
            var config = GameConfig.Instance;
            if (config != null)
                config.debugMaxDropRate = _maxDropRate;
            AddLog(_maxDropRate ? "<color=yellow>💎 爆率拉满 开启（100%掉落）</color>" : "<color=gray>💎 爆率拉满 关闭</color>");
            RefreshStatus();
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
            var txt = textGo.AddComponent<Text>();
            txt.text = "Debug";
            txt.fontSize = 13;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.7f);
            outline.effectDistance = new Vector2(1, -1);
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

            // 主面板（左侧）
            _panelGo = new GameObject("DebugPanel");
            _panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRT = _panelGo.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0, 0);
            panelRT.anchorMax = new Vector2(0, 1);
            panelRT.pivot = new Vector2(0, 0.5f);
            panelRT.offsetMin = new Vector2(10, 10);
            panelRT.offsetMax = new Vector2(290, -10);
            var panelImg = _panelGo.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);

            // 标题
            CreateLabel(_panelGo.transform, "Title", "═══ Debug 控制台 (Tab) ═══",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -5), new Vector2(0, -30),
                16, new Color(1f, 0.85f, 0.3f), FontStyle.Bold);

            // 状态文本
            var statusGo = CreateLabel(_panelGo.transform, "Status", "",
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(5, -35), new Vector2(-5, -130),
                11, new Color(0.7f, 0.9f, 0.7f), FontStyle.Normal);
            _statusText = statusGo.GetComponent<Text>();
            _statusText.alignment = TextAnchor.UpperLeft;

            // 分隔线
            CreateSeparator(_panelGo.transform, 0.87f);

            // 按钮区域（ScrollView）
            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(_panelGo.transform, false);
            var scrollRT = scrollGo.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0.08f);
            scrollRT.anchorMax = new Vector2(1, 0.87f);
            scrollRT.offsetMin = new Vector2(5, 0);
            scrollRT.offsetMax = new Vector2(-5, 0);
            _scrollRect = scrollGo.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            var scrollImg = scrollGo.AddComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0.01f); // 几乎透明，用于接收滚动
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // 内容容器
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            _contentRT = contentGo.AddComponent<RectTransform>();
            _contentRT.anchorMin = new Vector2(0, 1);
            _contentRT.anchorMax = new Vector2(1, 1);
            _contentRT.pivot = new Vector2(0.5f, 1);
            _contentRT.offsetMin = Vector2.zero;
            _contentRT.offsetMax = Vector2.zero;
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scrollRect.content = _contentRT;

            // ===== 按钮组 =====

            // --- 玩家状态 ---
            CreateSectionHeader(contentGo.transform, "【 玩家状态 】");
            CreateButton(contentGo.transform, "🛡 无敌模式", new Color(0.6f, 0.5f, 0.2f), ToggleGodMode);
            CreateButton(contentGo.transform, "🔒 锁血模式", new Color(0.5f, 0.4f, 0.2f), ToggleLockHp);
            CreateButton(contentGo.transform, "♥ 满血恢复", new Color(0.2f, 0.5f, 0.3f), FullHeal);
            CreateButton(contentGo.transform, "⚔ 一击必杀", new Color(0.6f, 0.2f, 0.2f), ToggleOneHitKill);
            CreateButton(contentGo.transform, "👟 加速模式 (3x)", new Color(0.2f, 0.4f, 0.5f), ToggleSpeedBoost);

            // --- 属性调整 ---
            CreateSectionHeader(contentGo.transform, "【 属性调整 】");
            CreateButton(contentGo.transform, "⚔ 攻击力 +50", new Color(0.5f, 0.25f, 0.2f), BoostAttack);
            CreateButton(contentGo.transform, "♥ 最大生命 +100", new Color(0.2f, 0.45f, 0.25f), BoostMaxHp);
            CreateButton(contentGo.transform, "✦ 灵力碎片 +500", new Color(0.2f, 0.35f, 0.5f), AddShards);
            CreateButton(contentGo.transform, "💎 爆率拉满 (100%)", new Color(0.5f, 0.4f, 0.1f), ToggleMaxDropRate);

            // --- 房间控制 ---
            CreateSectionHeader(contentGo.transform, "【 房间跳转 】");
            CreateButton(contentGo.transform, "☠ 清除所有敌人", new Color(0.5f, 0.15f, 0.15f), KillAllEnemies);
            CreateButton(contentGo.transform, "✓ 强制通关当前房间", new Color(0.3f, 0.5f, 0.2f), ClearCurrentRoom);
            CreateButton(contentGo.transform, "$ 跳转 → 商店", new Color(0.5f, 0.45f, 0.15f), GotoShopRoom);
            CreateButton(contentGo.transform, "⚔ 跳转 → 战斗", new Color(0.5f, 0.3f, 0.15f), GotoBattleRoom);
            CreateButton(contentGo.transform, "☠ 跳转 → Boss", new Color(0.5f, 0.1f, 0.1f), GotoBossRoom);
            CreateButton(contentGo.transform, "♥ 跳转 → 休息", new Color(0.15f, 0.35f, 0.5f), GotoRestRoom);
            CreateButton(contentGo.transform, "★ 跳转 → 宝箱", new Color(0.5f, 0.35f, 0.1f), GotoTreasureRoom);

            // --- 系统 ---
            CreateSectionHeader(contentGo.transform, "【 系统 】");
            CreateButton(contentGo.transform, "⏱ 切换时间缩放", new Color(0.3f, 0.3f, 0.4f), CycleTimeScale);
            CreateButton(contentGo.transform, "↺ 重新开始", new Color(0.4f, 0.2f, 0.3f), RestartGame);

            // 底部日志区域
            CreateSeparator(_panelGo.transform, 0.08f);
            var logGo = CreateLabel(_panelGo.transform, "Log", "",
                new Vector2(0, 0), new Vector2(1, 0.08f), new Vector2(5, 2), new Vector2(-5, -2),
                10, new Color(0.6f, 0.6f, 0.6f, 0.8f), FontStyle.Normal);
            _logText = logGo.GetComponent<Text>();
            _logText.alignment = TextAnchor.LowerLeft;
            _logText.supportRichText = true;

            RefreshStatus();
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
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);
            return go;
        }

        private void CreateButton(Transform parent, string label, Color bgColor, UnityEngine.Events.UnityAction onClick)
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
            var txt = textGo.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 14;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.white;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.7f);
            outline.effectDistance = new Vector2(1, -1);
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var go = new GameObject($"Header_{title}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 22;
            le.minHeight = 22;

            var txt = go.AddComponent<Text>();
            txt.text = title;
            txt.fontSize = 12;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = new Color(0.8f, 0.7f, 0.5f, 0.9f);
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
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

            string status = "";
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                int shards = PlayerResources.Instance != null ? PlayerResources.Instance.SpiritShards : 0;
                int level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0;
                string realm = GameManager.Instance != null ? GameManager.Instance.CurrentRealmName : "?";

                status += $"<color=#AAF>境界：</color>{realm}（第{level + 1}层）\n";
                status += $"<color=#AFA>生命：</color>{stats.currentHp:F0}/{stats.maxHp:F0}\n";
                status += $"<color=#FAA>攻击：</color>{stats.attackDamage:F0}  <color=#AAF>攻速：</color>{stats.attackSpeed:F1}\n";
                status += $"<color=#AFF>移速：</color>{stats.moveSpeed:F1}  <color=#FFA>碎片：</color>{shards}\n";
                status += "\n";
                status += $"无敌：{BoolStr(_godMode)}  锁血：{BoolStr(_lockHp)}\n";
                status += $"秒杀：{BoolStr(_oneHitKill)}  加速：{BoolStr(_speedBoost)}\n";
                status += $"爆率拉满：{BoolStr(_maxDropRate)}\n";
                status += $"时间缩放：{Time.timeScale}x";
            }
            else
            {
                status = "<color=red>玩家未初始化</color>";
            }

            _statusText.text = status;
        }

        private string BoolStr(bool v) => v ? "<color=yellow>ON</color>" : "<color=gray>OFF</color>";

        // ==================== 日志 ====================

        private void AddLog(string msg)
        {
            _logMessages.Add(msg);
            if (_logMessages.Count > MAX_LOG_LINES)
                _logMessages.RemoveAt(0);

            if (_logText != null)
                _logText.text = _logMessages.Count > 0 ? _logMessages[_logMessages.Count - 1] : "";

            RefreshStatus();
            Debug.Log($"[DebugConsole] {msg}");
        }
    }
}
