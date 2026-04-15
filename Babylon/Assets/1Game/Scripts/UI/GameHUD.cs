using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 游戏 HUD —— 完整的战斗界面
    /// 包含：血条（带动画）、技能CD、闪避CD、连招指示器、敌人计数、境界信息、
    ///       消息提示、死亡/通关面板
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("血条")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Image hpFillImage;       // 血条填充（用于变色）
        [SerializeField] private Image hpDamageFill;      // 受伤延迟条（红色）
        [SerializeField] private Text hpText;

        [Header("境界信息")]
        [SerializeField] private Text realmText;
        [SerializeField] private Text levelText;

        [Header("技能CD")]
        [SerializeField] private Image skillQCooldownFill;
        [SerializeField] private Text skillQCooldownText;
        [SerializeField] private Image skillQIcon;

        [SerializeField] private Image skillECooldownFill;
        [SerializeField] private Text skillECooldownText;
        [SerializeField] private Image skillEIcon;

        [SerializeField] private Image skillRCooldownFill;
        [SerializeField] private Text skillRCooldownText;
        [SerializeField] private Image skillRIcon;

        [Header("闪避CD")]
        [SerializeField] private Image dashCooldownFill;
        [SerializeField] private Text dashCooldownText;

        [Header("连招指示器")]
        [SerializeField] private Image[] comboIndicators;  // 3个点

        [Header("敌人计数")]
        [SerializeField] private Text enemyCountText;
        [SerializeField] private Image enemyCountIcon;

        [Header("灵物计数")]
        [SerializeField] private Text itemCountText;

        [Header("灵物质变进度")]
        [SerializeField] private RectTransform itemProgressPanel; // 灵物进度区域父节点
        private readonly List<ItemProgressSlot> _itemProgressSlots = new();
        private const int MAX_ITEM_PROGRESS_SLOTS = 8; // 最多显示8种灵物的进度

        [Header("质变触发提示")]
        [SerializeField] private Text qualitativeText; // 质变触发时的大字提示
        private float _qualitativeTimer;

        // 灵物进度悬浮提示
        private GameObject _progressTooltipPanel;
        private Text _progressTooltipTitle;
        private Text _progressTooltipDesc;
        private Text _progressTooltipEffect;
        private RectTransform _progressTooltipRT;
        private int _hoverProgressSlot = -1;
        private Canvas _hudCanvas;

        [Header("资源显示")]
        [SerializeField] private Text shardCountText;

        [Header("提示信息")]
        [SerializeField] private Text messageText;
        private float _messageTimer;

        [Header("死亡面板")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Text deathTitleText;
        [SerializeField] private Text deathSubText;
        [SerializeField] private Button restartButton;

        [Header("通关面板")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private Text winTitleText;
        [SerializeField] private Text winSubText;
        [SerializeField] private Button winRestartButton;

        // 血条动画
        private float _targetHpRatio = 1f;
        private float _damageBarRatio = 1f;
        private float _damageBarDelay;
        private const float DAMAGE_BAR_DELAY = 0.5f;
        private const float DAMAGE_BAR_SPEED = 2f;
        private bool _initialized;

        // 连招颜色
        private readonly Color _comboInactiveColor = new(0.3f, 0.3f, 0.3f, 0.5f);
        private readonly Color[] _comboActiveColors = {
            new(0.4f, 0.8f, 1f, 1f),   // 第1段：青色
            new(0.3f, 1f, 0.5f, 1f),    // 第2段：绿色
            new(1f, 0.85f, 0.2f, 1f)    // 第3段：金色
        };

        private void Start()
        {
            // 订阅事件
            GameEvents.Subscribe<GameEvents.HealthChanged>(OnHealthChanged);
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.SkillCooldownUpdate>(OnSkillCooldownUpdate);
            GameEvents.Subscribe<GameEvents.DashCooldownUpdate>(OnDashCooldownUpdate);
            GameEvents.Subscribe<GameEvents.DashChargeUpdate>(OnDashChargeUpdate);
            GameEvents.Subscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Subscribe<GameEvents.EnemyCountChanged>(OnEnemyCountChanged);
            GameEvents.Subscribe<GameEvents.ComboStepChanged>(OnComboStepChanged);
            GameEvents.Subscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            GameEvents.Subscribe<GameEvents.QualitativeTriggered>(OnQualitativeTriggered);
            GameEvents.Subscribe<GameEvents.SynergyActivated>(OnSynergyActivated);

            // 获取Canvas引用
            _hudCanvas = GetComponentInParent<Canvas>();

            // 初始化显示
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                UpdateHpDisplay(stats.currentHp, stats.maxHp);
                _initialized = true;
            }

            // 隐藏面板
            if (deathPanel != null) deathPanel.SetActive(false);
            if (winPanel != null) winPanel.SetActive(false);

            // 初始化连招指示器
            ResetComboIndicators();

            // 初始化灵物进度槽位
            InitItemProgressSlots();

            // 创建灵物进度悬浮提示
            CreateProgressTooltip();

            // 隐藏质变提示
            if (qualitativeText != null)
                qualitativeText.gameObject.SetActive(false);

            // 绑定按钮
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
            if (winRestartButton != null)
                winRestartButton.onClick.AddListener(OnRestartClicked);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 延迟初始化：确保 PlayerController 准备好后立即初始化血条
            if (!_initialized && PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                UpdateHpDisplay(stats.currentHp, stats.maxHp);
                _initialized = true;
            }

            // 消息淡出
            if (_messageTimer > 0)
            {
                _messageTimer -= dt;
                if (_messageTimer <= 0 && messageText != null)
                    messageText.text = "";
                else if (messageText != null)
                {
                    // 最后1秒淡出
                    float alpha = Mathf.Clamp01(_messageTimer / 1f);
                    var c = messageText.color;
                    c.a = alpha;
                    messageText.color = c;
                }
            }

            // 血条受伤延迟条动画
            UpdateDamageBar(dt);

            // 质变提示淡出
            if (_qualitativeTimer > 0)
            {
                _qualitativeTimer -= dt;
                if (_qualitativeTimer <= 0 && qualitativeText != null)
                    qualitativeText.gameObject.SetActive(false);
                else if (qualitativeText != null)
                {
                    // 最后1秒淡出
                    float alpha = Mathf.Clamp01(_qualitativeTimer / 1f);
                    var c = qualitativeText.color;
                    c.a = alpha;
                    qualitativeText.color = c;
                }
            }

            // 灵物进度槽位发光动画
            UpdateItemProgressGlow();

            // 灵物进度悬浮提示检测
            HandleProgressHover();
        }

        // ==================== 血条 ====================

        private void OnHealthChanged(GameEvents.HealthChanged evt)
        {
            UpdateHpDisplay(evt.CurrentHp, evt.MaxHp);
        }

        private void UpdateHpDisplay(float current, float max)
        {
            float ratio = max > 0 ? current / max : 0;
            _targetHpRatio = ratio;

            // 如果是受伤（新值比当前低），触发延迟条
            if (hpSlider != null && ratio < hpSlider.value)
            {
                _damageBarDelay = DAMAGE_BAR_DELAY;
                _damageBarRatio = hpSlider.value;
            }

            if (hpSlider != null)
                hpSlider.value = ratio;

            if (hpText != null)
                hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            // 血条颜色根据血量变化
            if (hpFillImage != null)
            {
                if (ratio > 0.6f)
                    hpFillImage.color = new Color(0.2f, 0.85f, 0.35f); // 绿色
                else if (ratio > 0.3f)
                    hpFillImage.color = new Color(1f, 0.75f, 0.15f);   // 黄色
                else
                    hpFillImage.color = new Color(0.9f, 0.2f, 0.2f);   // 红色
            }
        }

        private void UpdateDamageBar(float dt)
        {
            if (hpDamageFill == null) return;

            if (_damageBarDelay > 0)
            {
                _damageBarDelay -= dt;
            }
            else
            {
                _damageBarRatio = Mathf.MoveTowards(_damageBarRatio, _targetHpRatio, DAMAGE_BAR_SPEED * dt);
            }

            hpDamageFill.fillAmount = _damageBarRatio;
        }

        // ==================== 境界 ====================

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            if (realmText != null)
                realmText.text = evt.RealmName;
            if (levelText != null)
                levelText.text = $"第 {evt.NewRealmLevel + 1} 层";

            ShowMessage($"<color=#FFD700>— {evt.RealmName} —</color>");
        }

        // ==================== 技能CD ====================

        private void OnSkillCooldownUpdate(GameEvents.SkillCooldownUpdate evt)
        {
            if (evt.SlotIndex == 0)
            {
                if (skillQCooldownFill != null)
                    skillQCooldownFill.fillAmount = evt.TotalCooldown > 0 ? evt.RemainingTime / evt.TotalCooldown : 0;
                if (skillQCooldownText != null)
                {
                    if (evt.RemainingTime > 0)
                        skillQCooldownText.text = $"{evt.RemainingTime:F1}";
                    else
                        skillQCooldownText.text = "Q";
                }
                if (skillQIcon != null)
                    skillQIcon.color = evt.RemainingTime > 0 ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            }
            else if (evt.SlotIndex == 1)
            {
                if (skillECooldownFill != null)
                    skillECooldownFill.fillAmount = evt.TotalCooldown > 0 ? evt.RemainingTime / evt.TotalCooldown : 0;
                if (skillECooldownText != null)
                {
                    if (evt.RemainingTime > 0)
                        skillECooldownText.text = $"{evt.RemainingTime:F1}";
                    else
                        skillECooldownText.text = "E";
                }
                if (skillEIcon != null)
                    skillEIcon.color = evt.RemainingTime > 0 ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            }
            else if (evt.SlotIndex == 2)
            {
                if (skillRCooldownFill != null)
                    skillRCooldownFill.fillAmount = evt.TotalCooldown > 0 ? evt.RemainingTime / evt.TotalCooldown : 0;
                if (skillRCooldownText != null)
                {
                    if (evt.RemainingTime > 0)
                        skillRCooldownText.text = $"{evt.RemainingTime:F1}";
                    else
                        skillRCooldownText.text = "R";
                }
                if (skillRIcon != null)
                    skillRIcon.color = evt.RemainingTime > 0 ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            }
        }

        // ==================== 闪避CD ====================

        private void OnDashCooldownUpdate(GameEvents.DashCooldownUpdate evt)
        {
            if (dashCooldownFill != null)
                dashCooldownFill.fillAmount = evt.TotalCooldown > 0 ? evt.RemainingTime / evt.TotalCooldown : 0;
            if (dashCooldownText != null)
            {
                if (evt.RemainingTime > 0.05f)
                    dashCooldownText.text = $"{evt.RemainingTime:F1}";
                else
                    dashCooldownText.text = "闪避";
            }
        }

        private void OnDashChargeUpdate(GameEvents.DashChargeUpdate evt)
        {
            // 充能式闪避显示：层数 + 恢复进度
            if (dashCooldownFill != null)
            {
                if (evt.CurrentCharges < evt.MaxCharges)
                    dashCooldownFill.fillAmount = evt.RechargeProgress;
                else
                    dashCooldownFill.fillAmount = 1f;
            }
            if (dashCooldownText != null)
            {
                if (evt.MaxCharges > 1)
                    dashCooldownText.text = $"闪避 {evt.CurrentCharges}/{evt.MaxCharges}";
                else
                    dashCooldownText.text = evt.CurrentCharges > 0 ? "闪避" : "充能中";
            }
        }

        // ==================== 连招指示器 ====================

        private void OnComboStepChanged(GameEvents.ComboStepChanged evt)
        {
            if (comboIndicators == null) return;

            if (!evt.IsAttacking)
            {
                ResetComboIndicators();
                return;
            }

            for (int i = 0; i < comboIndicators.Length; i++)
            {
                if (comboIndicators[i] == null) continue;

                if (i <= evt.ComboStep)
                {
                    comboIndicators[i].color = _comboActiveColors[Mathf.Min(i, _comboActiveColors.Length - 1)];
                    // 当前段缩放动画
                    if (i == evt.ComboStep)
                        StartCoroutine(PunchScale(comboIndicators[i].rectTransform, 1.4f, 0.15f));
                }
                else
                {
                    comboIndicators[i].color = _comboInactiveColor;
                }
            }
        }

        private void ResetComboIndicators()
        {
            if (comboIndicators == null) return;
            for (int i = 0; i < comboIndicators.Length; i++)
            {
                if (comboIndicators[i] != null)
                    comboIndicators[i].color = _comboInactiveColor;
            }
        }

        private IEnumerator PunchScale(RectTransform rt, float punchScale, float duration)
        {
            float timer = 0;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = timer / duration;
                float scale = Mathf.Lerp(punchScale, 1f, t);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ==================== 敌人计数 ====================

        private void OnEnemyCountChanged(GameEvents.EnemyCountChanged evt)
        {
            if (enemyCountText != null)
                enemyCountText.text = $"{evt.RemainingCount} / {evt.TotalCount}";
        }

        // ==================== 灵物 ====================

        private void OnItemPickedUp(GameEvents.ItemPickedUp evt)
        {
            ShowMessage($"获得灵物：<color=#{ColorUtility.ToHtmlStringRGB(evt.Item.GetRarityColor())}>{evt.Item.itemName}</color>");
            UpdateItemCount();
            UpdateItemProgress();
        }

        private void UpdateItemCount()
        {
            if (itemCountText == null || PlayerController.Instance == null) return;
            var items = PlayerController.Instance.Inventory.GetAllItems();
            itemCountText.text = $"{items.Count}";
        }

        // ==================== 灵物质变进度 ====================

        /// <summary>灵物进度槽位数据</summary>
        private class ItemProgressSlot
        {
            public GameObject Root;
            public Text NameText;
            public Image ProgressFill;
            public Image GlowImage;
            public Text CountText;
            public Image[] ThresholdDots; // 质变阈值小圆点
            public ItemData BoundItem;
            public int NextThreshold;
            public float GlowPhase; // 发光动画相位
        }

        private void InitItemProgressSlots()
        {
            if (itemProgressPanel == null) return;

            for (int i = 0; i < MAX_ITEM_PROGRESS_SLOTS; i++)
            {
                var slot = CreateItemProgressSlot(i);
                slot.Root.SetActive(false);
                _itemProgressSlots.Add(slot);
            }
        }

        private ItemProgressSlot CreateItemProgressSlot(int index)
        {
            float slotHeight = 28f;
            float slotWidth = 180f;
            float yOffset = -index * (slotHeight + 4f);

            // 根节点
            var root = new GameObject($"ItemProgress_{index}");
            root.transform.SetParent(itemProgressPanel, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0, 1);
            rootRt.anchorMax = new Vector2(0, 1);
            rootRt.pivot = new Vector2(0, 1);
            rootRt.anchoredPosition = new Vector2(0, yOffset);
            rootRt.sizeDelta = new Vector2(slotWidth, slotHeight);

            // 背景条
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);

            // 进度填充条
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0, 1); // 初始宽度为0
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 0.9f, 0.6f);

            // 发光覆盖层（越接近质变越亮）
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root.transform, false);
            var glowRt = glowGo.AddComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.sizeDelta = Vector2.zero;
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.color = new Color(1f, 1f, 1f, 0f);

            // 灵物名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(root.transform, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0);
            nameRt.anchorMax = new Vector2(0.55f, 1);
            nameRt.sizeDelta = Vector2.zero;
            nameRt.offsetMin = new Vector2(4, 0);
            var nameText = nameGo.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 14;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = Color.white;
            nameText.raycastTarget = false;

            // 计数文字
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(root.transform, false);
            var countRt = countGo.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0.6f, 0);
            countRt.anchorMax = new Vector2(1f, 1);
            countRt.sizeDelta = Vector2.zero;
            var countText = countGo.AddComponent<Text>();
            countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.fontSize = 14;
            countText.alignment = TextAnchor.MiddleRight;
            countText.color = new Color(0.8f, 0.8f, 0.8f);
            countText.raycastTarget = false;

            // 描边
            var outline = nameGo.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1, -1);
            var outline2 = countGo.AddComponent<Outline>();
            outline2.effectColor = new Color(0, 0, 0, 0.8f);
            outline2.effectDistance = new Vector2(1, -1);

            return new ItemProgressSlot
            {
                Root = root,
                NameText = nameText,
                ProgressFill = fillImg,
                GlowImage = glowImg,
                CountText = countText,
                GlowPhase = Random.Range(0f, Mathf.PI * 2f)
            };
        }

        /// <summary>更新灵物进度显示</summary>
        private void UpdateItemProgress()
        {
            if (itemProgressPanel == null || PlayerController.Instance == null) return;

            var allItems = PlayerController.Instance.Inventory.GetAllItems();

            // 只显示有质变阈值的灵物
            int slotIndex = 0;
            foreach (var (item, count) in allItems)
            {
                if (slotIndex >= MAX_ITEM_PROGRESS_SLOTS) break;
                if (item.qualitativeThresholds == null || item.qualitativeThresholds.Length == 0) continue;

                // 找到下一个未触发的质变阈值
                int nextThreshold = 0;
                int prevThreshold = 0;
                foreach (int t in item.qualitativeThresholds)
                {
                    if (count < t)
                    {
                        nextThreshold = t;
                        break;
                    }
                    prevThreshold = t;
                }

                var slot = _itemProgressSlots[slotIndex];
                slot.Root.SetActive(true);
                slot.BoundItem = item;
                slot.NextThreshold = nextThreshold;

                // 名称颜色按品阶
                slot.NameText.text = item.itemName;
                slot.NameText.color = item.GetRarityColor();

                if (nextThreshold > 0)
                {
                    // 未全部触发：显示进度
                    float progress = (float)(count - prevThreshold) / (nextThreshold - prevThreshold);
                    progress = Mathf.Clamp01(progress);

                    // 进度条填充
                    var fillRt = slot.ProgressFill.rectTransform;
                    fillRt.anchorMax = new Vector2(progress, 1);

                    // 进度条颜色：越接近越亮
                    Color fillColor;
                    if (progress < 0.5f)
                        fillColor = Color.Lerp(new Color(0.3f, 0.4f, 0.5f, 0.5f), new Color(0.4f, 0.7f, 1f, 0.7f), progress * 2f);
                    else
                        fillColor = Color.Lerp(new Color(0.4f, 0.7f, 1f, 0.7f), new Color(1f, 0.85f, 0.3f, 0.9f), (progress - 0.5f) * 2f);
                    slot.ProgressFill.color = fillColor;

                    slot.CountText.text = $"{count}/{nextThreshold}";
                    slot.CountText.color = progress >= 0.8f ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.8f, 0.8f);
                }
                else
                {
                    // 全部触发：满进度 + 金色
                    var fillRt = slot.ProgressFill.rectTransform;
                    fillRt.anchorMax = new Vector2(1, 1);
                    slot.ProgressFill.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                    slot.CountText.text = $"✔ {count}";
                    slot.CountText.color = new Color(1f, 0.85f, 0.2f);
                }

                slotIndex++;
            }

            // 隐藏多余槽位
            for (int i = slotIndex; i < MAX_ITEM_PROGRESS_SLOTS; i++)
            {
                _itemProgressSlots[i].Root.SetActive(false);
                _itemProgressSlots[i].BoundItem = null;
            }
        }

        /// <summary>灵物进度槽位发光动画（越接近质变越亮）</summary>
        private void UpdateItemProgressGlow()
        {
            if (PlayerController.Instance == null) return;

            foreach (var slot in _itemProgressSlots)
            {
                if (!slot.Root.activeSelf || slot.BoundItem == null || slot.GlowImage == null) continue;

                if (slot.NextThreshold <= 0)
                {
                    // 已全部触发：稳定金色微光
                    float glow = 0.15f + Mathf.Sin(Time.time * 2f + slot.GlowPhase) * 0.05f;
                    slot.GlowImage.color = new Color(1f, 0.85f, 0.2f, glow);
                }
                else
                {
                    int count = PlayerController.Instance.Inventory.GetItemCount(slot.BoundItem);
                    float progress = (float)count / slot.NextThreshold;

                    if (progress >= 0.6f)
                    {
                        // 接近质变：脉冲发光，越接近越快越亮
                        float speed = Mathf.Lerp(2f, 6f, (progress - 0.6f) / 0.4f);
                        float intensity = Mathf.Lerp(0.05f, 0.3f, (progress - 0.6f) / 0.4f);
                        float glow = intensity * (0.5f + 0.5f * Mathf.Sin(Time.time * speed + slot.GlowPhase));
                        Color glowColor = Color.Lerp(new Color(0.5f, 0.7f, 1f), new Color(1f, 0.85f, 0.2f), (progress - 0.6f) / 0.4f);
                        slot.GlowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, glow);
                    }
                    else
                    {
                        slot.GlowImage.color = new Color(1f, 1f, 1f, 0f);
                    }
                }
            }
        }

        // ==================== 灵物进度悬浮提示 ====================

        /// <summary>创建灵物进度悬浮提示面板</summary>
        private void CreateProgressTooltip()
        {
            var canvasRoot = _hudCanvas != null ? _hudCanvas.transform : transform;

            _progressTooltipPanel = new GameObject("ProgressTooltip");
            _progressTooltipPanel.transform.SetParent(canvasRoot, false);
            _progressTooltipRT = _progressTooltipPanel.AddComponent<RectTransform>();
            _progressTooltipRT.sizeDelta = new Vector2(300, 160);
            _progressTooltipRT.pivot = new Vector2(0, 0.5f);

            var bg = _progressTooltipPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.95f);
            bg.raycastTarget = false;

            // 边框
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(_progressTooltipPanel.transform, false);
            var borderRT = borderGo.AddComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-1, -1);
            borderRT.offsetMax = new Vector2(1, 1);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
            borderImg.raycastTarget = false;
            borderGo.transform.SetAsFirstSibling();

            // 标题（灵物名称 + 品阶）
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_progressTooltipPanel.transform, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 0.7f);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.offsetMin = new Vector2(8, 0);
            titleRT.offsetMax = new Vector2(-8, -4);
            _progressTooltipTitle = titleGo.AddComponent<Text>();
            _progressTooltipTitle.fontSize = 18;
            _progressTooltipTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _progressTooltipTitle.alignment = TextAnchor.MiddleCenter;
            _progressTooltipTitle.fontStyle = FontStyle.Bold;
            _progressTooltipTitle.raycastTarget = false;
            _progressTooltipTitle.supportRichText = true;
            var titleOutline = titleGo.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0, 0, 0, 0.8f);
            titleOutline.effectDistance = new Vector2(1, -1);

            // 描述（灵物描述 + 效果）
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(_progressTooltipPanel.transform, false);
            var descRT = descGo.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0.35f);
            descRT.anchorMax = new Vector2(1, 0.7f);
            descRT.offsetMin = new Vector2(8, 0);
            descRT.offsetMax = new Vector2(-8, 0);
            _progressTooltipDesc = descGo.AddComponent<Text>();
            _progressTooltipDesc.fontSize = 13;
            _progressTooltipDesc.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _progressTooltipDesc.alignment = TextAnchor.MiddleCenter;
            _progressTooltipDesc.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            _progressTooltipDesc.raycastTarget = false;
            _progressTooltipDesc.supportRichText = true;

            // 质变进度信息
            var effectGo = new GameObject("Effect");
            effectGo.transform.SetParent(_progressTooltipPanel.transform, false);
            var effectRT = effectGo.AddComponent<RectTransform>();
            effectRT.anchorMin = new Vector2(0, 0);
            effectRT.anchorMax = new Vector2(1, 0.35f);
            effectRT.offsetMin = new Vector2(8, 4);
            effectRT.offsetMax = new Vector2(-8, 0);
            _progressTooltipEffect = effectGo.AddComponent<Text>();
            _progressTooltipEffect.fontSize = 12;
            _progressTooltipEffect.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _progressTooltipEffect.alignment = TextAnchor.MiddleCenter;
            _progressTooltipEffect.color = new Color(0.6f, 0.9f, 0.6f, 0.8f);
            _progressTooltipEffect.raycastTarget = false;
            _progressTooltipEffect.supportRichText = true;

            _progressTooltipPanel.SetActive(false);
        }

        /// <summary>处理灵物进度面板的鼠标悬浮</summary>
        private void HandleProgressHover()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mousePos = mouse.position.ReadValue();
            int newHover = -1;

            // 检测鼠标是否在某个进度槽位上
            for (int i = 0; i < _itemProgressSlots.Count; i++)
            {
                var slot = _itemProgressSlots[i];
                if (!slot.Root.activeSelf || slot.BoundItem == null) continue;

                var rt = slot.Root.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverProgressSlot)
            {
                _hoverProgressSlot = newHover;
                if (newHover >= 0)
                    ShowProgressTooltip(newHover, mousePos);
                else
                    HideProgressTooltip();
            }

            // 跟随鼠标位置
            if (_hoverProgressSlot >= 0 && _progressTooltipPanel != null && _progressTooltipPanel.activeSelf)
                PositionProgressTooltip(mousePos);
        }

        /// <summary>显示灵物进度悬浮提示</summary>
        private void ShowProgressTooltip(int slotIndex, Vector2 mousePos)
        {
            if (_progressTooltipPanel == null) return;
            if (slotIndex < 0 || slotIndex >= _itemProgressSlots.Count) return;

            var slot = _itemProgressSlots[slotIndex];
            var item = slot.BoundItem;
            if (item == null) return;

            int count = PlayerController.Instance != null
                ? PlayerController.Instance.Inventory.GetItemCount(item) : 0;

            // 品阶名称
            string rarityName = item.rarity switch
            {
                ItemRarity.Fan => "凡品",
                ItemRarity.Ling => "灵品",
                ItemRarity.Xuan => "玄品",
                ItemRarity.Di => "地品",
                ItemRarity.Tian => "天品",
                _ => "凡品"
            };

            _progressTooltipTitle.text = $"{item.itemName}（{rarityName}）x{count}";
            _progressTooltipTitle.color = item.GetRarityColor();

            // 描述 + 属性效果
            var effectParts = new List<string>();
            if (item.attackBonus > 0) effectParts.Add($"攻击+{item.attackBonus}");
            if (item.attackBonusPercent > 0) effectParts.Add($"攻击+{item.attackBonusPercent * 100}%");
            if (item.maxHpBonus > 0) effectParts.Add($"生命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) effectParts.Add($"生命+{item.maxHpBonusPercent * 100}%");
            if (item.moveSpeedBonusPercent > 0) effectParts.Add($"移速+{item.moveSpeedBonusPercent * 100}%");
            if (item.damageReductionBonus > 0) effectParts.Add($"减伤+{item.damageReductionBonus * 100}%");
            if (item.critRateBonus > 0) effectParts.Add($"暴击+{item.critRateBonus * 100}%");
            if (item.healOnKill > 0) effectParts.Add($"击杀回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) effectParts.Add($"灼烧{item.burnDamagePerSecond}/s");
            if (item.freezeChance > 0) effectParts.Add($"冻结{item.freezeChance * 100}%");

            string effectStr = effectParts.Count > 0 ? string.Join("  ", effectParts) : "";
            _progressTooltipDesc.text = item.description + (effectStr.Length > 0 ? $"\n<color=#AFA>{effectStr}</color>" : "");

            // 质变进度信息
            string qualInfo = "";
            if (item.qualitativeThresholds != null && item.qualitativeThresholds.Length > 0)
            {
                qualInfo = "<color=#FFD700>质变阈值：</color>";
                foreach (int t in item.qualitativeThresholds)
                {
                    bool reached = count >= t;
                    qualInfo += reached
                        ? $"<color=#FFD700>✔{t}</color>  "
                        : $"<color=#888>{t}</color>  ";
                }

                // 显示下一个质变的效果预览
                string nextEffect = GetQualitativePreview(item, count);
                if (!string.IsNullOrEmpty(nextEffect))
                    qualInfo += $"\n{nextEffect}";
            }
            _progressTooltipEffect.text = qualInfo;

            _progressTooltipPanel.SetActive(true);
            _progressTooltipPanel.transform.SetAsLastSibling();
            PositionProgressTooltip(mousePos);
        }

        /// <summary>获取下一个质变效果的预览文字</summary>
        private string GetQualitativePreview(ItemData item, int currentCount)
        {
            if (item.qualitativeThresholds == null) return "";

            // 找到下一个未触发的阈值
            int nextThreshold = 0;
            foreach (int t in item.qualitativeThresholds)
            {
                if (currentCount < t)
                {
                    nextThreshold = t;
                    break;
                }
            }

            if (nextThreshold == 0)
                return "<color=#FFD700>✨ 已全部质变完成</color>";

            // 根据灵物名称预览效果
            string preview = item.itemName switch
            {
                "火灵珠" when nextThreshold == 5 => "焚天！每5次攻击释放火焰冲击波",
                "火灵珠" when nextThreshold == 8 => "焚天大成！攻击力+50%，暴击伤害+50%",
                "玉佩" when nextThreshold == 5 => "玉碎！致命伤害免疫（CD 60秒）",
                "风灵珠" when nextThreshold == 5 => "御风！闪避后留下攻击残影",
                "锈铁飞剑" when nextThreshold == 5 => "剑阵！飞剑环绕护体",
                "锈铁飞剑" when nextThreshold == 8 => "万剑归宗！攻击力+30%，穿透+3",
                "回灵丹" when nextThreshold == 5 => "涅槃！死亡时原地复活",
                _ => "灵力共鸣，属性提升"
            };

            return $"<color=#AAF>下一阶（x{nextThreshold}）：{preview}</color>";
        }

        private void HideProgressTooltip()
        {
            if (_progressTooltipPanel != null)
                _progressTooltipPanel.SetActive(false);
        }

        private void PositionProgressTooltip(Vector2 screenPos)
        {
            if (_progressTooltipRT == null || _hudCanvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_hudCanvas.transform, screenPos, null, out Vector2 localPos);

            // 在鼠标右侧显示
            Vector2 offset = new Vector2(20, 0);
            Vector2 pos = localPos + offset;

            // 确保不超出屏幕边界
            var canvasRT = (RectTransform)_hudCanvas.transform;
            float tooltipW = _progressTooltipRT.sizeDelta.x;
            float tooltipH = _progressTooltipRT.sizeDelta.y;
            float canvasHalfW = canvasRT.sizeDelta.x / 2f;
            float canvasHalfH = canvasRT.sizeDelta.y / 2f;

            // 右边界溢出则改为左侧显示
            if (pos.x + tooltipW > canvasHalfW)
                pos.x = localPos.x - tooltipW - 10;
            // 上下边界
            pos.y = Mathf.Clamp(pos.y, -canvasHalfH + tooltipH / 2f + 5, canvasHalfH - tooltipH / 2f - 5);

            _progressTooltipRT.anchoredPosition = pos;
        }

        // ==================== Synergy 组合激活提示 ====================

        private void OnSynergyActivated(GameEvents.SynergyActivated evt)
        {
            ShowMessage($"<color=#FFD700>✨ 组合激活：{evt.SynergyName} — {evt.Description}</color>");
        }

        // ==================== 质变触发提示 ====================

        private void OnQualitativeTriggered(GameEvents.QualitativeTriggered evt)
        {
            // 大字提示
            if (qualitativeText != null)
            {
                qualitativeText.gameObject.SetActive(true);
                qualitativeText.text = $"✨ {evt.EffectDescription}";
                qualitativeText.color = new Color(1f, 0.85f, 0.2f, 1f);
                _qualitativeTimer = 4f;

                // 缩放动画
                StartCoroutine(PunchScale(qualitativeText.rectTransform, 1.3f, 0.3f));
            }

            // 同时显示在消息栏
            ShowMessage($"<color=#FFD700>✨ {evt.Item.itemName} x{evt.Count} — {evt.EffectDescription}</color>");

            // 更新进度显示
            UpdateItemProgress();
        }

        // ==================== 资源 ====================

        private void OnResourceChanged(GameEvents.ResourceChanged evt)
        {
            if (shardCountText != null)
                shardCountText.text = $"{evt.SpiritShards}";

            // 获得碎片时显示提示
            if (evt.Delta > 0)
                ShowMessage($"<color=#88CCFF>获得灵力碎片 +{evt.Delta}</color>");
        }

        // ==================== 房间清理 ====================

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            ShowMessage("房间清理完成！寻找传送门继续前进...");
        }

        // ==================== 死亡/通关 ====================

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            if (deathPanel != null)
            {
                deathPanel.SetActive(true);
                if (deathTitleText != null)
                    deathTitleText.text = "梦境破碎";
                if (deathSubText != null)
                {
                    string realm = GameManager.Instance != null ? GameManager.Instance.CurrentRealmName : "未知";
                    int level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel + 1 : 0;
                    deathSubText.text = $"止步于 {realm} · 第 {level} 层";
                }
            }
        }

        private void OnGameWon(GameEvents.GameWon evt)
        {
            if (winPanel != null)
            {
                winPanel.SetActive(true);
                if (winTitleText != null)
                    winTitleText.text = "✨ 渡劫成功 ✨";
                if (winSubText != null)
                    winSubText.text = "飞升成仙，梦境圆满";
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Restart();
        }

        // ==================== 消息提示 ====================

        private void ShowMessage(string msg)
        {
            if (messageText != null)
            {
                messageText.text = msg;
                var c = messageText.color;
                c.a = 1f;
                messageText.color = c;
                _messageTimer = 3f;
            }
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.HealthChanged>(OnHealthChanged);
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.SkillCooldownUpdate>(OnSkillCooldownUpdate);
            GameEvents.Unsubscribe<GameEvents.DashCooldownUpdate>(OnDashCooldownUpdate);
            GameEvents.Unsubscribe<GameEvents.DashChargeUpdate>(OnDashChargeUpdate);
            GameEvents.Unsubscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Unsubscribe<GameEvents.EnemyCountChanged>(OnEnemyCountChanged);
            GameEvents.Unsubscribe<GameEvents.ComboStepChanged>(OnComboStepChanged);
            GameEvents.Unsubscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
            GameEvents.Unsubscribe<GameEvents.QualitativeTriggered>(OnQualitativeTriggered);
            GameEvents.Unsubscribe<GameEvents.SynergyActivated>(OnSynergyActivated);
        }
    }
}
