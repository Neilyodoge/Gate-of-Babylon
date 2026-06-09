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
        private bool _maxItemDropRate;   // 灵物爆率拉满
        private bool _maxSkillDropRate;  // 功法爆率拉满

        // 日志
        private List<string> _logMessages = new();
        private Text _logText;
        private const int MAX_LOG_LINES = 200;

        // 打包可见日志面板（捕获 Application.logMessageReceived）
        private GameObject _logPanelGo;
        private Text _logPanelText;
        private bool _logPanelOpen;

        private void Awake()
        {
            Instance = this;
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

            // 从 GameConfig 同步 debug 状态（场景重新加载后 static 字段可能仍为 true）
            var config = GameConfig.Instance;
            if (config != null)
            {
                _maxItemDropRate = config.debugMaxItemDropRate;
                _maxSkillDropRate = config.debugMaxSkillDropRate;
            }
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
            else if (_logPanelGo != null)
            {
                // 关闭控制台时一并收起日志面板（否则没有按钮可关）
                _logPanelOpen = false;
                _logPanelGo.SetActive(false);
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

        /// <summary>跳转到升级房间</summary>
        private void GotoUpgradeRoom()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.DebugGotoRoom(Minimap.RoomType.Upgrade);
            AddLog("<color=green>↑ 跳转到升级房间</color>");
        }

        /// <summary>大量增加灵力碎片</summary>
        private void AddShardsLarge()
        {
            if (PlayerResources.Instance == null) return;
            PlayerResources.Instance.AddShards(5000);
            AddLog("<color=#88CCFF>✦ +5000 灵力碎片</color>");
        }

        /// <summary>
        /// 把背包里已持有的每种灵物直接补满到其最高质变阈值，
        /// 途径每一个阈值都会正常触发 QualitativeTriggered 事件（走 AddItem 本来的逻辑），
        /// 方便快速测试 5 件 / 8 件质变态。没有阈值配置的灵物按 5 件兜底。
        /// </summary>
        private void MaxOutHeldItems()
        {
            if (PlayerController.Instance == null) return;
            var inventory = PlayerController.Instance.Inventory;
            if (inventory == null) return;

            var items = inventory.GetAllItems();
            if (items.Count == 0)
            {
                AddLog("<color=gray>💎 当前背包无灵物，无法升满</color>");
                return;
            }

            int totalAdded = 0;
            int bumpedKinds = 0;
            foreach (var (item, currentCount) in items)
            {
                int target = 5;
                if (item.qualitativeThresholds != null && item.qualitativeThresholds.Length > 0)
                {
                    target = 0;
                    foreach (int t in item.qualitativeThresholds)
                        if (t > target) target = t;
                }

                int needed = target - currentCount;
                if (needed <= 0) continue;

                for (int i = 0; i < needed; i++)
                    inventory.AddItem(item);

                totalAdded += needed;
                bumpedKinds++;
            }

            if (bumpedKinds == 0)
                AddLog("<color=gray>💎 所有灵物已在最高阈值</color>");
            else
                AddLog($"<color=yellow>💎 灵物一键升满：{bumpedKinds} 种 +{totalAdded} 件</color>");
            RefreshStatus();
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

        /// <summary>调试：打印 ConfigDatabase 已加载的全部表（验证 CSV→JSON→运行时读取链路）。</summary>
        private void DumpConfigTables()
        {
            var db = XianTu.LevelDesign.ConfigDatabase.Instance;
            AddLog($"<color=#c8d0a0>📋 配表：地图{db.MapStructures.Count} 房间{db.RoomSockets.Count} 事件{db.StoryEvents.Count} Boss{db.BossPhases.Count} 道具{db.ItemsInRun.Count} 素材{db.CaveMaterials.Count}</color>");
            AddLog($"<color=#c8d0a0>📋 战斗表：化身{db.Avatars.Count} 主动技能{db.SkillBases.Count} 效果{db.SkillEffects.Count}</color>");
            foreach (var kv in db.Avatars)
                AddLog($"<color=#9cc0ff>· 化身[{kv.Key}] {kv.Value.Name_CN} / {kv.Value.ControllerScript}</color>");
            foreach (var kv in db.SkillBases)
                AddLog($"<color=#9cc0ff>· 技能[{kv.Key}] {kv.Value.Name_CN} (Type{kv.Value.Type} CD{kv.Value.BaseCooldown} 倍率{kv.Value.BaseDamageRatio})</color>");
            if (db.Avatars.Count == 0 && db.SkillBases.Count == 0)
                AddLog("<color=#ffaa66>战斗表为空——请先在 Unity 菜单「修仙图/导表」(Ctrl+Shift+T) 把 CSV 导成 JSON</color>");
        }

        /// <summary>调试：临时开/关灵物系统（V.03 Q8 默认关）。重进秘境后生效。</summary>
        private void ToggleSpiritItemsFlag()
        {
            FeatureFlags.EnableSpiritItems = !FeatureFlags.EnableSpiritItems;
            AddLog($"<color=#c79bff>🔮 灵物系统 → {(FeatureFlags.EnableSpiritItems ? "已启用" : "已屏蔽")}（重进秘境/重开商店后生效）</color>");
        }

        /// <summary>调试：临时开/关洞府 meta（闭关/灵脉/机缘，V.03 Q7 默认关）。回洞府后生效。</summary>
        private void ToggleCaveMetaFlag()
        {
            FeatureFlags.EnableCaveMeta = !FeatureFlags.EnableCaveMeta;
            AddLog($"<color=#9be0c0>🏔 洞府meta → {(FeatureFlags.EnableCaveMeta ? "已启用" : "已暂缓")}（回洞府/重进秘境后生效）</color>");
        }

        /// <summary>调试：+200 修为（历练值→存量→修为 一条龙，方便直接测渡劫战）。</summary>
        private void BoostCultivationExp()
        {
            var cult = CultivationSystem.Instance;
            cult.AddRunTempering(200, "debug");
            cult.CommitOnExtract();      // → 历练值存量
            cult.CultivateToExp(200);    // → 修为
            string canBt = cult.CanBreakthrough ? "（修为已够，可冲击境界）" : "";
            AddLog($"<color=#9cc0ff>🧘 修为 +200 → {cult.CurrentExp}/{cult.NextBreakthroughCost} · 当前 {cult.CurrentRealmName}{canBt}</color>");
        }

        /// <summary>调试：+200 历练值存量（测洞府"修为 vs 灵脉"分配）。</summary>
        private void BoostRunTempering()
        {
            var cult = CultivationSystem.Instance;
            cult.AddRunTempering(200, "debug");
            cult.CommitOnExtract();      // 直接结算进存量
            AddLog($"<color=#9cc0ff>🧘 历练值存量 +200 → {cult.TemperingPool}（去闭关石室/灵脉台分配）</color>");
        }

        /// <summary>调试：+200 灵脉经验。</summary>
        private void BoostSpiritVein()
        {
            SpiritVeinSystem.Instance.InjectExp(200, "调试");
            var v = SpiritVeinSystem.Instance;
            AddLog($"<color=#9be0c0>💎 灵脉经验 +200 → {v.LevelName}（掉率 +{v.DropBonus * 100:F0}%）</color>");
        }

        /// <summary>调试：强制触发一次机缘事件（无视概率，按当前灵脉等级筛池）。</summary>
        private void TriggerOpportunity()
        {
            CaveOpportunitySystem.Instance.ForceTrigger();
            AddLog($"<color=#ffd47a>✦ 触发机缘（灵脉 {SpiritVeinSystem.Instance.LevelName}）—— 灵脉越高，可撞见越高级的机缘</color>");
        }

        /// <summary>调试：模拟一次回府（链式机缘计数 +1），到期则触发回访。先选"赠予灵药/接纳剑灵"埋点，再点几次即可看回访。</summary>
        private void AdvanceOpportunityChain()
        {
            CaveOpportunitySystem.Instance.DebugAdvanceReturn();
            int pending = SaveSystem.Instance.Data.pendingOpportunities.Count;
            AddLog($"<color=#ffd47a>🔗 回府计数 → {SaveSystem.Instance.Data.caveReturnCount}（待回访 {pending} 条）</color>");
        }

        /// <summary>调试：道心 -25（测心摇 / 入魔阈值的局内减益）。</summary>
        private void LowerDaoxin()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeDaoxin(-25);
            var h = XianTu.LevelDesign.PlayerStateHooks.Instance;
            AddLog($"<color=#b0c8ff>🧿 道心 → {h.Daoxin}（{h.DaoxinState}）</color>");
        }

        /// <summary>调试：道心 +25（测入定增益）。</summary>
        private void RaiseDaoxin()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeDaoxin(25);
            var h = XianTu.LevelDesign.PlayerStateHooks.Instance;
            AddLog($"<color=#b0c8ff>🧘 道心 → {h.Daoxin}（{h.DaoxinState}）</color>");
        }

        /// <summary>调试：因果债 +12（测业障受伤增益）。</summary>
        private void AddKarma()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeKarma(12);
            AddLog($"<color=#d8b0a0>☯ 因果债 → {XianTu.LevelDesign.PlayerStateHooks.Instance.KarmaDebt}</color>");
        }

        /// <summary>调试：因果债 -12（测善缘庇佑）。</summary>
        private void ReduceKarma()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeKarma(-12);
            AddLog($"<color=#a0d090>🍀 因果债 → {XianTu.LevelDesign.PlayerStateHooks.Instance.KarmaDebt}</color>");
        }

        /// <summary>调试：寿元 -25（测衰朽 / 油尽灯枯减益）。</summary>
        private void ReduceLifespan()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeLifespan(-25);
            AddLog($"<color=#c0b0a0>⏳ 寿元 → {XianTu.LevelDesign.PlayerStateHooks.Instance.Lifespan} 年</color>");
        }

        /// <summary>调试：寿元 +25。</summary>
        private void AddLifespan()
        {
            XianTu.LevelDesign.PlayerStateHooks.Instance.ChangeLifespan(25);
            AddLog($"<color=#c0b0a0>⏳ 寿元 → {XianTu.LevelDesign.PlayerStateHooks.Instance.Lifespan} 年</color>");
        }

        /// <summary>调试：强制设定本局秘境异象。</summary>
        private void SetAnomaly(RealmAnomaly a)
        {
            RealmAnomalySystem.Instance.DebugSet(a);
            var info = RealmAnomalySystem.Info(a);
            AddLog($"<color=#c8a0ff>{info.icon} 秘境异象 → {info.name}</color>");
        }

        /// <summary>调试：在玩家身边掉一颗灵脉道具（测秘境掉落拾取 → 灵脉经验）。</summary>
        private void DropSpiritVeinItem()
        {
            var p = PlayerController.Instance;
            if (p == null) { AddLog("<color=red>无玩家</color>"); return; }
            var pk = SpiritVeinPickup.Spawn("地脉精华", 200, p.transform.position + p.transform.forward * 2f);
            AddLog(pk != null
                ? "<color=#9be0c0>💎 已掉落「地脉精华」(+200 灵脉) · 走近自动汲取</color>"
                : "<color=#ffaa66>洞府 meta 未启用，灵脉道具不生成</color>");
        }

        /// <summary>调试：+50 心魔值（满 100 且正在打 Boss 时触发乱入）。</summary>
        private void BoostInnerDemon()
        {
            InnerDemonMeter.Instance.DebugAddMeter(50f);
            string hint = EnemyBoss.AliveCount > 0 ? "（有 Boss，满值即乱入）" : "（需在 Boss 战中才会乱入）";
            AddLog($"<color=#ff8899>👹 心魔值 +50 → {Mathf.RoundToInt(InnerDemonMeter.Instance.Meter)}/100 {hint}</color>");
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

        /// <summary>灵物爆率拉满</summary>
        private void ToggleMaxItemDropRate()
        {
            _maxItemDropRate = !_maxItemDropRate;
            var config = GameConfig.Instance;
            if (config != null)
            {
                config.debugMaxItemDropRate = _maxItemDropRate;
                // 验证设置是否生效
                Debug.Log($"[DebugConsole] debugMaxItemDropRate 设置为 {_maxItemDropRate}，验证读取: {config.debugMaxItemDropRate}");

                // 检查灵物池
                if (_maxItemDropRate && GameManager.Instance != null)
                {
                    var poolField = typeof(GameManager).GetField("itemPool",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var pool = poolField?.GetValue(GameManager.Instance) as ItemData[];
                    if (pool == null || pool.Length == 0)
                        Debug.LogError("[DebugConsole] ⚠ GameManager.itemPool 为空！敌人无法掉落灵物！");
                    else
                        Debug.Log($"[DebugConsole] ✓ GameManager.itemPool 有 {pool.Length} 个灵物");
                }
            }
            else
            {
                Debug.LogWarning("[DebugConsole] GameConfig.Instance 为 null！");
            }
            AddLog(_maxItemDropRate ? "<color=yellow>💎 灵物爆率拉满 开启</color>" : "<color=gray>💎 灵物爆率拉满 关闭</color>");
            RefreshStatus();
        }

        /// <summary>功法爆率拉满</summary>
        private void ToggleMaxSkillDropRate()
        {
            _maxSkillDropRate = !_maxSkillDropRate;
            var config = GameConfig.Instance;
            if (config != null)
                config.debugMaxSkillDropRate = _maxSkillDropRate;
            AddLog(_maxSkillDropRate ? "<color=cyan>📜 功法爆率拉满 开启</color>" : "<color=gray>📜 功法爆率拉满 关闭</color>");
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
            CreateButton(contentGo.transform, "✦ 灵力碎片 +5000", new Color(0.25f, 0.4f, 0.55f), AddShardsLarge);
            CreateButton(contentGo.transform, "📜 功法爆率拉满", new Color(0.2f, 0.4f, 0.5f), ToggleMaxSkillDropRate);
            CreateButton(contentGo.transform, "🗡 发当前化身专属技能", new Color(0.45f, 0.3f, 0.5f), GrantAvatarSpecialSkill);

            // --- V.03 范围开关（运行时临时覆盖，便于测试被屏蔽的系统）---
            CreateSectionHeader(contentGo.transform, "【 V.03 范围开关 】");
            CreateButton(contentGo.transform, "🔮 灵物系统：开/关", new Color(0.45f, 0.3f, 0.5f), ToggleSpiritItemsFlag);
            CreateButton(contentGo.transform, "🏔 洞府meta：开/关", new Color(0.3f, 0.45f, 0.45f), ToggleCaveMetaFlag);
            CreateButton(contentGo.transform, "📋 配表自检（打印已加载表）", new Color(0.4f, 0.4f, 0.3f), DumpConfigTables);

            // --- 本体境界（v0.5.4 渡劫战测试）---
            CreateSectionHeader(contentGo.transform, "【 本体境界 】");
            CreateButton(contentGo.transform, "🧘 修为 +200（直给·测渡劫）", new Color(0.35f, 0.45f, 0.6f), BoostCultivationExp);
            CreateButton(contentGo.transform, "🧘 历练值存量 +200（测分配）", new Color(0.3f, 0.4f, 0.55f), BoostRunTempering);
            CreateButton(contentGo.transform, "💎 灵脉经验 +200", new Color(0.3f, 0.55f, 0.45f), BoostSpiritVein);
            CreateButton(contentGo.transform, "✦ 触发机缘事件", new Color(0.45f, 0.4f, 0.2f), TriggerOpportunity);
            CreateButton(contentGo.transform, "👹 心魔值 +50", new Color(0.5f, 0.18f, 0.25f), BoostInnerDemon);

            // --- 道心 / 因果（v0.5.5 抉择后果测试）---
            CreateSectionHeader(contentGo.transform, "【 道心 / 因果 】");
            CreateButton(contentGo.transform, "🧿 道心 -25（测心摇/入魔）", new Color(0.3f, 0.35f, 0.55f), LowerDaoxin);
            CreateButton(contentGo.transform, "🧘 道心 +25（测入定）", new Color(0.35f, 0.5f, 0.6f), RaiseDaoxin);
            CreateButton(contentGo.transform, "☯ 因果 +12（测业障）", new Color(0.5f, 0.35f, 0.3f), AddKarma);
            CreateButton(contentGo.transform, "🍀 因果 -12（测善缘）", new Color(0.35f, 0.5f, 0.35f), ReduceKarma);
            CreateButton(contentGo.transform, "⏳ 寿元 -25（测衰朽）", new Color(0.5f, 0.42f, 0.35f), ReduceLifespan);
            CreateButton(contentGo.transform, "⏳ 寿元 +25", new Color(0.4f, 0.48f, 0.42f), AddLifespan);

            // --- 秘境异象（v0.5.5 每局变量）---
            CreateSectionHeader(contentGo.transform, "【 秘境异象 】");
            CreateButton(contentGo.transform, "⚡ 异象 · 雷泽", new Color(0.35f, 0.4f, 0.55f), () => SetAnomaly(RealmAnomaly.LeiZe));
            CreateButton(contentGo.transform, "🌊 异象 · 灵潮汹涌", new Color(0.25f, 0.45f, 0.5f), () => SetAnomaly(RealmAnomaly.LingChao));
            CreateButton(contentGo.transform, "😈 异象 · 心魔滋生", new Color(0.45f, 0.3f, 0.45f), () => SetAnomaly(RealmAnomaly.DemonGrowth));
            CreateButton(contentGo.transform, "♻ 异象 · 万灵复苏", new Color(0.3f, 0.5f, 0.35f), () => SetAnomaly(RealmAnomaly.Revival));
            CreateButton(contentGo.transform, "🩸 异象 · 血月", new Color(0.5f, 0.28f, 0.28f), () => SetAnomaly(RealmAnomaly.BloodMoon));
            CreateButton(contentGo.transform, "☯ 异象 · 道心试炼", new Color(0.35f, 0.42f, 0.55f), () => SetAnomaly(RealmAnomaly.DaoHeartTrial));
            CreateButton(contentGo.transform, "⚖ 异象 · 因果轮回", new Color(0.5f, 0.44f, 0.28f), () => SetAnomaly(RealmAnomaly.KarmaEcho));
            CreateButton(contentGo.transform, "✨ 异象 · 机缘频现", new Color(0.55f, 0.5f, 0.28f), () => SetAnomaly(RealmAnomaly.OpportunityRush));
            CreateButton(contentGo.transform, "⛰ 异象 · 清除", new Color(0.4f, 0.4f, 0.42f), () => SetAnomaly(RealmAnomaly.None));

            // --- 房间控制 ---
            CreateSectionHeader(contentGo.transform, "【 房间跳转 】");
            CreateButton(contentGo.transform, "☠ 清除所有敌人", new Color(0.5f, 0.15f, 0.15f), KillAllEnemies);
            CreateButton(contentGo.transform, "✓ 强制通关当前房间", new Color(0.3f, 0.5f, 0.2f), ClearCurrentRoom);
            CreateButton(contentGo.transform, "$ 跳转 → 商店", new Color(0.5f, 0.45f, 0.15f), GotoShopRoom);
            CreateButton(contentGo.transform, "⚔ 跳转 → 战斗", new Color(0.5f, 0.3f, 0.15f), GotoBattleRoom);
            CreateButton(contentGo.transform, "☠ 跳转 → Boss", new Color(0.5f, 0.1f, 0.1f), GotoBossRoom);
            CreateButton(contentGo.transform, "♥ 跳转 → 休息", new Color(0.15f, 0.35f, 0.5f), GotoRestRoom);
            CreateButton(contentGo.transform, "★ 跳转 → 宝箱", new Color(0.5f, 0.35f, 0.1f), GotoTreasureRoom);
            CreateButton(contentGo.transform, "↑ 跳转 → 升级", new Color(0.2f, 0.5f, 0.3f), GotoUpgradeRoom);

            // --- 系统 ---
            CreateSectionHeader(contentGo.transform, "【 系统 】");
            CreateButton(contentGo.transform, "📜 日志面板：展开/收起", new Color(0.25f, 0.3f, 0.45f), ToggleLogPanel);
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
                status += $"灵物爆率：{BoolStr(_maxItemDropRate)}  功法爆率：{BoolStr(_maxSkillDropRate)}\n";
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
            var ttxt = titleGo.AddComponent<Text>();
            ttxt.text = "═══ 运行日志（最近 40 行）═══";
            ttxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ttxt.fontSize = 13;
            ttxt.fontStyle = FontStyle.Bold;
            ttxt.color = new Color(1f, 0.85f, 0.3f);
            ttxt.raycastTarget = false;

            var txtGo = new GameObject("LogText");
            txtGo.transform.SetParent(_logPanelGo.transform, false);
            var ltrt = txtGo.AddComponent<RectTransform>();
            ltrt.anchorMin = Vector2.zero;
            ltrt.anchorMax = Vector2.one;
            ltrt.offsetMin = new Vector2(8, 8);
            ltrt.offsetMax = new Vector2(-8, -28);
            _logPanelText = txtGo.AddComponent<Text>();
            _logPanelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _logPanelText.fontSize = 12;
            _logPanelText.color = Color.white;
            _logPanelText.alignment = TextAnchor.LowerLeft;
            _logPanelText.supportRichText = true;
            _logPanelText.raycastTarget = false;
            _logPanelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _logPanelText.verticalOverflow = VerticalWrapMode.Truncate;
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

        /// <summary>在玩家身边生成"当前化身的专属技能"掉落，便于测试 16-20。</summary>
        private void GrantAvatarSpecialSkill()
        {
            var pc = PlayerController.Instance;
            var root = pc != null ? pc.GetComponent<SpiritRootController>() : null;
            if (root == null) { AddLog("发专属失败：无玩家/化身"); return; }

            var cur = root.CurrentRoot;
            SkillData match = null;
            foreach (var s in Resources.FindObjectsOfTypeAll<SkillData>())
            {
                if (s != null && s.skillType == SkillType.AvatarSpecial && s.RequiredRoot == cur)
                {
                    match = s;
                    break;
                }
            }
            if (match == null) { AddLog($"未找到 {cur} 的专属技能 SO"); return; }

            SkillPickup.Spawn(match, pc.transform.position + pc.transform.forward * 1.5f);
            AddLog($"已生成 {cur} 专属：{match.skillName}（走过去拾取）");
        }
    }
}
