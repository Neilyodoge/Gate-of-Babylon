using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;

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
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("境界信息")]
        [SerializeField] private TextMeshProUGUI realmText;
        [SerializeField] private TextMeshProUGUI levelText;

        [Header("技能CD")]
        [SerializeField] private Image skillQCooldownFill;
        [SerializeField] private TextMeshProUGUI skillQCooldownText;
        [SerializeField] private Image skillQIcon;

        [SerializeField] private Image skillECooldownFill;
        [SerializeField] private TextMeshProUGUI skillECooldownText;
        [SerializeField] private Image skillEIcon;

        [SerializeField] private Image skillRCooldownFill;
        [SerializeField] private TextMeshProUGUI skillRCooldownText;
        [SerializeField] private Image skillRIcon;

        [Header("闪避CD")]
        [SerializeField] private Image dashCooldownFill;
        [SerializeField] private TextMeshProUGUI dashCooldownText;

        [Header("连招指示器")]
        [SerializeField] private Image[] comboIndicators;  // 3个点

        [Header("敌人计数")]
        [SerializeField] private TextMeshProUGUI enemyCountText;
        [SerializeField] private Image enemyCountIcon;

        [Header("资源显示")]
        [SerializeField] private TextMeshProUGUI shardCountText;

        [Header("提示信息")]
        [SerializeField] private TextMeshProUGUI messageText;
        private float _messageTimer;

        [Header("死亡面板")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private TextMeshProUGUI deathTitleText;
        [SerializeField] private TextMeshProUGUI deathSubText;
        [SerializeField] private Button restartButton;

        [Header("通关面板")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TextMeshProUGUI winTitleText;
        [SerializeField] private TextMeshProUGUI winSubText;
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
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Subscribe<GameEvents.EnemyCountChanged>(OnEnemyCountChanged);
            GameEvents.Subscribe<GameEvents.ComboStepChanged>(OnComboStepChanged);
            GameEvents.Subscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Subscribe<GameEvents.ResourceChanged>(OnResourceChanged);

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
                    float alpha = Mathf.Clamp01(_messageTimer / 1f);
                    var c = messageText.color;
                    c.a = alpha;
                    messageText.color = c;
                }
            }

            // 血条受伤延迟条动画
            UpdateDamageBar(dt);
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

            if (hpSlider != null && ratio < hpSlider.value)
            {
                _damageBarDelay = DAMAGE_BAR_DELAY;
                _damageBarRatio = hpSlider.value;
            }

            if (hpSlider != null)
                hpSlider.value = ratio;

            if (hpText != null)
                hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            if (hpFillImage != null)
            {
                if (ratio > 0.6f)
                    hpFillImage.color = new Color(0.2f, 0.85f, 0.35f);
                else if (ratio > 0.3f)
                    hpFillImage.color = new Color(1f, 0.75f, 0.15f);
                else
                    hpFillImage.color = new Color(0.9f, 0.2f, 0.2f);
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
            // realmText = 境名（第一层/第二层/第三层）；levelText = 境内进度（第 X/N 关）。
            // 三关统一用同一套文案，避免「只提示第几层、不提示第几关」的困惑。
            if (realmText != null)
                realmText.text = evt.RealmName;
            if (levelText != null)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                    levelText.text = $"第 {gm.CurrentRoomInLevel + 1}/{gm.TotalRoomsInLevel} 关";
                else
                    levelText.text = "";
            }

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
                {
                    Color readyColor = GetSkillSlotIconColor(0);
                    skillQIcon.color = evt.RemainingTime > 0 ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : readyColor;
                }
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
                {
                    Color readyColor = GetSkillSlotIconColor(1);
                    skillEIcon.color = evt.RemainingTime > 0 ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : readyColor;
                }
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
                {
                    Color readyColor = GetSkillSlotIconColor(2);
                    skillRIcon.color = evt.RemainingTime > 0 ? new Color(0.3f, 0.3f, 0.3f, 0.5f) : readyColor;
                }
            }
        }

        private Color GetSkillSlotIconColor(int slotIndex)
        {
            var combat = PlayerController.Instance?.GetComponent<PlayerCombat>();
            if (combat == null) return new Color(0.3f, 0.3f, 0.3f, 0.05f);
            var skill = combat.GetSkillInSlot(slotIndex);
            if (skill == null) return new Color(0.3f, 0.3f, 0.3f, 0.05f);
            Color c = skill.rarity switch
            {
                ItemRarity.Fan => new Color(0.7f, 0.7f, 0.7f),
                ItemRarity.Ling => new Color(0.3f, 0.85f, 0.3f),
                ItemRarity.Xuan => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Di => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Tian => new Color(1f, 0.85f, 0f),
                _ => Color.white
            };
            return new Color(c.r, c.g, c.b, 0.25f);
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

        // ==================== 资源 ====================

        private void OnResourceChanged(GameEvents.ResourceChanged evt)
        {
            if (shardCountText != null)
                shardCountText.text = $"{evt.SpiritShards}";

            if (evt.Delta > 0)
                ShowMessage($"<color=#88CCFF>获得碎片 +{evt.Delta}</color>");
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
                    deathTitleText.text = "探索失败";
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
                    winTitleText.text = "✨ 通关成功 ✨";
                if (winSubText != null)
                    winSubText.text = "秘境圆满";
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
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
            GameEvents.Unsubscribe<GameEvents.EnemyCountChanged>(OnEnemyCountChanged);
            GameEvents.Unsubscribe<GameEvents.ComboStepChanged>(OnComboStepChanged);
            GameEvents.Unsubscribe<GameEvents.GameWon>(OnGameWon);
            GameEvents.Unsubscribe<GameEvents.ResourceChanged>(OnResourceChanged);
        }
    }
}
