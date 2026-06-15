using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// GDD §5.6 / V.05 §2.3：战斗房通关后弹出三选一灵物卡牌，
    /// 玩家选择一件带走（或跳过）。选择后发布 RoomCleared 事件开启传送门。
    /// </summary>
    public class BattleRewardUI : MonoBehaviour
    {
        private static BattleRewardUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private ItemData[] _candidates;
        private Action<ItemData> _onSelected;
        private CursorLockMode _prevLock;
        private bool _prevCursorVisible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _cards;

        public static void Show(ItemData[] candidates, Action<ItemData> onSelected)
        {
            if (candidates == null || candidates.Length == 0)
            {
                onSelected?.Invoke(null);
                return;
            }

            EnsureInstance();
            if (_instance == null)
            {
                onSelected?.Invoke(null);
                return;
            }

            _instance._candidates = candidates;
            _instance._onSelected = onSelected;
            _instance._visible = true;
            _instance._prevLock = UnityEngine.Cursor.lockState;
            _instance._prevCursorVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            _instance.Rebuild();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void HideImmediate()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("BattleRewardUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BattleRewardUI>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/BattleRewardUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 12f;
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

            _overlay = root.Q<VisualElement>("overlay");
            _cards = root.Q<VisualElement>("cards");
            var skip = root.Q<Button>("skip");
            if (skip != null) skip.clicked += () => Pick(null);

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (_cards == null || _candidates == null) return;
            _cards.Clear();
            for (int i = 0; i < _candidates.Length; i++)
            {
                _cards.Add(MakeCard(_candidates[i], i + 1));
            }
        }

        private VisualElement MakeCard(ItemData item, int hotkey)
        {
            Color rc = item.GetRarityColor();
            var card = new VisualElement();
            card.AddToClassList("br-card");

            var accent = new VisualElement();
            accent.AddToClassList("br-accent");
            accent.style.backgroundColor = rc;
            card.Add(accent);

            var icon = new VisualElement();
            icon.AddToClassList("br-icon");
            icon.style.backgroundColor = rc * 0.4f;
            var iconLabel = new Label(RaritySymbol(item.rarity));
            iconLabel.AddToClassList("br-icon-label");
            iconLabel.style.color = rc;
            icon.Add(iconLabel);
            card.Add(icon);

            var nameLabel = new Label(item.itemName);
            nameLabel.AddToClassList("br-name");
            nameLabel.style.color = rc;
            card.Add(nameLabel);

            var rarityLabel = new Label(RarityName(item.rarity));
            rarityLabel.AddToClassList("br-rarity");
            card.Add(rarityLabel);

            var effect = new Label(GetBriefEffect(item));
            effect.AddToClassList("br-effect");
            card.Add(effect);

            var hot = new Label($"[{hotkey}]");
            hot.AddToClassList("br-hot");
            card.Add(hot);

            var captured = item;
            card.RegisterCallback<ClickEvent>(_ => Pick(captured));
            return card;
        }

        private void Update()
        {
            if (!_visible || _candidates == null) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            int n = Mathf.Min(_candidates.Length, 9);
            for (int i = 0; i < n; i++)
            {
                if (kb[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Pick(_candidates[i]);
                    return;
                }
            }
        }

        private void Pick(ItemData item)
        {
            _visible = false;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            UnityEngine.Cursor.lockState = _prevLock;
            UnityEngine.Cursor.visible = _prevCursorVisible;

            if (item != null) GrantItem(item);
            _onSelected?.Invoke(item);
        }

        private static void GrantItem(ItemData item)
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            player.Inventory.AddItem(item);

            if (item.linkedSkill != null)
            {
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    int empty = combat.FindEmptySlot();
                    if (empty >= 0)
                    {
                        combat.EquipSkillToSlot(item.linkedSkill, empty);
                        GameEvents.Publish(new GameEvents.SkillEquipped
                        {
                            Skill = item.linkedSkill,
                            SlotIndex = empty
                        });
                    }
                }
            }

            var spiritSlots = player.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
                int empty = spiritSlots.FindEmptySlot();
                if (empty >= 0)
                    spiritSlots.SetSlot(empty, item);
            }

            Debug.Log($"<color=cyan>战利品选择：{item.itemName}</color>");
        }

        private static string RaritySymbol(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡", ItemRarity.Ling => "灵", ItemRarity.Xuan => "玄",
            ItemRarity.Di => "地", ItemRarity.Tian => "天", _ => "?"
        };

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡品", ItemRarity.Ling => "灵品", ItemRarity.Xuan => "玄品",
            ItemRarity.Di => "地品", ItemRarity.Tian => "天品", _ => "凡品"
        };

        private static string GetBriefEffect(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"攻击 +{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻击 +{item.attackBonusPercent * 100:0}%");
            if (item.maxHpBonus > 0) parts.Add($"生命 +{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"生命 +{item.maxHpBonusPercent * 100:0}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"移速 +{item.moveSpeedBonusPercent * 100:0}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤 +{item.damageReductionBonus * 100:0}%");
            if (item.critRateBonus > 0) parts.Add($"暴击 +{item.critRateBonus * 100:0}%");
            if (item.healOnKill > 0) parts.Add($"击杀回复 {item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧 {item.burnDamagePerSecond}/s");
            if (item.linkedSkill != null) parts.Add($"功法：{item.linkedSkill.skillName}");
            return parts.Count > 0 ? string.Join("\n", parts) : "基础灵物";
        }
    }
}
