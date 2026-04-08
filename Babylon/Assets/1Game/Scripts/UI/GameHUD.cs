using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 游戏 HUD —— 显示血条、境界、技能CD、灵物列表
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("血条")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text hpText;

        [Header("境界信息")]
        [SerializeField] private Text realmText;
        [SerializeField] private Text levelText;

        [Header("技能CD")]
        [SerializeField] private Image skillQCooldownFill;
        [SerializeField] private Text skillQCooldownText;

        [Header("灵物计数")]
        [SerializeField] private Text itemCountText;

        [Header("提示信息")]
        [SerializeField] private Text messageText;
        private float _messageTimer;

        private void Start()
        {
            // 订阅事件
            GameEvents.Subscribe<GameEvents.HealthChanged>(OnHealthChanged);
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.SkillCooldownUpdate>(OnSkillCooldownUpdate);
            GameEvents.Subscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);

            // 初始化显示
            if (PlayerController.Instance != null)
            {
                var stats = PlayerController.Instance.Stats;
                UpdateHpDisplay(stats.currentHp, stats.maxHp);
            }
        }

        private void Update()
        {
            // 消息淡出
            if (_messageTimer > 0)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0 && messageText != null)
                    messageText.text = "";
            }
        }

        private void OnHealthChanged(GameEvents.HealthChanged evt)
        {
            UpdateHpDisplay(evt.CurrentHp, evt.MaxHp);
        }

        private void UpdateHpDisplay(float current, float max)
        {
            if (hpSlider != null)
                hpSlider.value = max > 0 ? current / max : 0;
            if (hpText != null)
                hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            if (realmText != null)
                realmText.text = evt.RealmName;
            if (levelText != null)
                levelText.text = $"第 {evt.NewRealmLevel + 1} 层";

            ShowMessage($"进入 {evt.RealmName}");
        }

        private void OnSkillCooldownUpdate(GameEvents.SkillCooldownUpdate evt)
        {
            if (evt.SlotIndex != 0) return;

            if (skillQCooldownFill != null)
                skillQCooldownFill.fillAmount = evt.TotalCooldown > 0 ? evt.RemainingTime / evt.TotalCooldown : 0;
            if (skillQCooldownText != null)
            {
                if (evt.RemainingTime > 0)
                    skillQCooldownText.text = $"{evt.RemainingTime:F1}s";
                else
                    skillQCooldownText.text = "Q";
            }
        }

        private void OnItemPickedUp(GameEvents.ItemPickedUp evt)
        {
            ShowMessage($"获得灵物：{evt.Item.itemName} x{evt.CurrentCount}");
            UpdateItemCount();
        }

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            ShowMessage("房间清理完成！准备进入下一层...");
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            ShowMessage("梦境破碎... 惊醒回到现实");
        }

        private void ShowMessage(string msg)
        {
            if (messageText != null)
            {
                messageText.text = msg;
                _messageTimer = 3f;
            }
        }

        private void UpdateItemCount()
        {
            if (itemCountText == null || PlayerController.Instance == null) return;
            var items = PlayerController.Instance.Inventory.GetAllItems();
            itemCountText.text = $"灵物：{items.Count} 种";
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.HealthChanged>(OnHealthChanged);
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.SkillCooldownUpdate>(OnSkillCooldownUpdate);
            GameEvents.Unsubscribe<GameEvents.ItemPickedUp>(OnItemPickedUp);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
        }
    }
}
