using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace XianTu
{
    /// <summary>
    /// 灵物背包 UI —— Tab 键打开/关闭
    /// 支持局内BD调整：查看/卸下技能、查看/卸下灵物槽位、分解灵物
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemListContent;
        [SerializeField] private Transform synergyListContent;
        [SerializeField] private Transform slotSectionContent; // 技能+灵物槽位区域
        [SerializeField] private Text titleText;

        private bool _isOpen;
        private readonly List<GameObject> _itemEntries = new();
        private readonly List<GameObject> _synergyEntries = new();
        private readonly List<GameObject> _slotEntries = new();

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
                panel.SetActive(_isOpen);
                if (_isOpen) Refresh();
            }
        }

        /// <summary>刷新背包显示</summary>
        public void Refresh()
        {
            if (PlayerController.Instance == null) return;

            // 清空旧条目
            foreach (var entry in _itemEntries) Destroy(entry);
            _itemEntries.Clear();
            foreach (var entry in _synergyEntries) Destroy(entry);
            _synergyEntries.Clear();
            foreach (var entry in _slotEntries) Destroy(entry);
            _slotEntries.Clear();

            var items = PlayerController.Instance.Inventory.GetAllItems();

            if (titleText != null)
                titleText.text = $"灵物背包 ({items.Count})";

            // ===== 灵物列表 =====
            foreach (var (item, count) in items)
            {
                var entry = CreateItemEntry(item, count);
                _itemEntries.Add(entry);
            }

            if (items.Count == 0)
            {
                var emptyText = CreateTextEntry(itemListContent, "尚无灵物", 16, new Color(0.6f, 0.6f, 0.6f, 0.6f));
                _itemEntries.Add(emptyText);
            }

            // ===== 技能+灵物槽位 =====
            if (slotSectionContent != null)
            {
                RefreshSlotSection();
            }

            // ===== Synergy 列表 =====
            var allSynergies = SynergySystem.GetAllSynergies();
            var activeSynergies = SynergySystem.GetActiveSynergies();

            foreach (var synergy in allSynergies)
            {
                bool active = activeSynergies.Contains(synergy.name);
                var entry = CreateSynergyEntry(synergy, active);
                _synergyEntries.Add(entry);
            }
        }

        // ==================== 槽位管理区域 ====================

        private void RefreshSlotSection()
        {
            // --- 技能槽位标题 ---
            var skillTitle = CreateTextEntry(slotSectionContent, "— 技能槽位 —", 16, new Color(0.5f, 0.9f, 1f));
            _slotEntries.Add(skillTitle);

            var combat = PlayerController.Instance.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                string[] slotNames = { "Q", "E", "R" };
                Color[] slotColors = {
                    new Color(0.3f, 0.5f, 1f),
                    new Color(0.8f, 0.4f, 0.2f),
                    new Color(0.6f, 0.3f, 0.8f)
                };

                for (int i = 0; i < 3; i++)
                {
                    var skill = combat.GetSkillInSlot(i);
                    int slotIdx = i; // 闭包捕获
                    var entry = CreateSlotEntry(
                        slotSectionContent,
                        $"[{slotNames[i]}]",
                        skill != null ? skill.skillName : "空",
                        skill != null ? slotColors[i] : new Color(0.4f, 0.4f, 0.4f, 0.5f),
                        skill != null,
                        () =>
                        {
                            // 卸下技能：掉落在玩家脚下
                            var s = combat.UnequipSkill(slotIdx);
                            if (s != null)
                            {
                                Vector3 dropPos = PlayerController.Instance.transform.position +
                                    Random.insideUnitSphere * 1.5f;
                                dropPos.y = PlayerController.Instance.transform.position.y + 0.5f;
                                SkillPickup.Spawn(s, dropPos);
                            }
                            Refresh();
                        }
                    );
                    _slotEntries.Add(entry);
                }
            }

            // --- 灵物槽位标题 ---
            var spiritTitle = CreateTextEntry(slotSectionContent, "— 灵物槽位 —", 16, new Color(1f, 0.85f, 0.5f));
            _slotEntries.Add(spiritTitle);

            var spiritSlots = PlayerController.Instance.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
                string[] slotNames = { "Q", "E", "R" };
                Color[] slotColors = {
                    new Color(0.2f, 0.35f, 0.7f),
                    new Color(0.55f, 0.28f, 0.14f),
                    new Color(0.4f, 0.2f, 0.55f)
                };

                for (int i = 0; i < spiritSlots.Slots.Count; i++)
                {
                    var slot = spiritSlots.Slots[i];
                    int slotIdx = i;
                    var entry = CreateSlotEntry(
                        slotSectionContent,
                        $"[{slotNames[i]}]",
                        slot.item != null ? slot.item.itemName : "空",
                        slot.item != null ? slot.item.GetRarityColor() : new Color(0.4f, 0.4f, 0.4f, 0.5f),
                        slot.item != null,
                        () =>
                        {
                            // 卸下灵物：掉落在玩家脚下
                            var oldItem = spiritSlots.RemoveFromSlot(slotIdx);
                            if (oldItem != null)
                            {
                                Vector3 dropPos = PlayerController.Instance.transform.position +
                                    Random.insideUnitSphere * 1.5f;
                                dropPos.y = PlayerController.Instance.transform.position.y + 0.5f;
                                ItemPickup.Spawn(oldItem, dropPos);
                            }
                            Refresh();
                        }
                    );
                    _slotEntries.Add(entry);
                }
            }
        }

        /// <summary>创建槽位条目（技能/灵物通用）</summary>
        private GameObject CreateSlotEntry(Transform parent, string label, string name, Color color,
            bool hasContent, System.Action onUnequip)
        {
            var go = new GameObject($"Slot_{label}");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(6, 6, 3, 3);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var bg = go.AddComponent<Image>();
            bg.color = hasContent
                ? new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.6f)
                : new Color(0.1f, 0.1f, 0.1f, 0.3f);

            // 槽位标签
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.fontSize = 14;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.color = new Color(0.7f, 0.7f, 0.7f);
            labelText.fontStyle = FontStyle.Bold;
            var labelLE = labelGo.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 30;

            // 名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = name;
            nameText.fontSize = 14;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = hasContent ? color : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // 卸下按钮（只有有内容时才显示）
            if (hasContent)
            {
                var btnGo = new GameObject("UnequipBtn");
                btnGo.transform.SetParent(go.transform, false);
                var btnRt = btnGo.AddComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(40, 28);
                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = new Color(0.6f, 0.25f, 0.2f, 0.8f);
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = btnImg;
                var btnColors = btn.colors;
                btnColors.highlightedColor = new Color(0.8f, 0.35f, 0.3f);
                btnColors.pressedColor = new Color(0.5f, 0.15f, 0.1f);
                btn.colors = btnColors;
                btn.onClick.AddListener(() => onUnequip?.Invoke());
                var btnLE = btnGo.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 40;
                btnLE.preferredHeight = 28;

                var btnTextGo = new GameObject("BtnText");
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRt = btnTextGo.AddComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.offsetMin = Vector2.zero;
                btnTextRt.offsetMax = Vector2.zero;
                var btnText = btnTextGo.AddComponent<Text>();
                btnText.text = "卸下";
                btnText.fontSize = 12;
                btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                btnText.color = Color.white;
                btnText.alignment = TextAnchor.MiddleCenter;
            }

            return go;
        }

        // ==================== 灵物列表 ====================

        private GameObject CreateItemEntry(ItemData item, int count)
        {
            var go = new GameObject($"Item_{item.itemName}");
            go.transform.SetParent(itemListContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // 背景
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.6f);

            // 品阶颜色条
            var colorBar = new GameObject("ColorBar");
            colorBar.transform.SetParent(go.transform, false);
            var barRt = colorBar.AddComponent<RectTransform>();
            barRt.sizeDelta = new Vector2(4, 0);
            var barImg = colorBar.AddComponent<Image>();
            barImg.color = item.GetRarityColor();
            var barLE = colorBar.AddComponent<LayoutElement>();
            barLE.preferredWidth = 4;

            // 名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = item.itemName;
            nameText.fontSize = 14;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = item.GetRarityColor();
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 90;

            // 数量
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(go.transform, false);
            var countText = countGo.AddComponent<Text>();
            countText.text = $"x{count}";
            countText.fontSize = 13;
            countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.color = new Color(0.8f, 0.8f, 0.8f);
            var countLE = countGo.AddComponent<LayoutElement>();
            countLE.preferredWidth = 35;

            // 描述
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descText = descGo.AddComponent<Text>();
            descText.text = GetItemEffectText(item);
            descText.fontSize = 11;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            var descLE = descGo.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1;

            // 分解按钮
            int shards = PlayerResources.GetDecomposeShards(item.rarity);
            var decompBtnGo = new GameObject("DecompBtn");
            decompBtnGo.transform.SetParent(go.transform, false);
            var decompBtnRt = decompBtnGo.AddComponent<RectTransform>();
            decompBtnRt.sizeDelta = new Vector2(55, 28);
            var decompBtnImg = decompBtnGo.AddComponent<Image>();
            decompBtnImg.color = new Color(0.5f, 0.35f, 0.15f, 0.8f);
            var decompBtn = decompBtnGo.AddComponent<Button>();
            decompBtn.targetGraphic = decompBtnImg;
            var decompBtnColors = decompBtn.colors;
            decompBtnColors.highlightedColor = new Color(0.7f, 0.5f, 0.2f);
            decompBtnColors.pressedColor = new Color(0.4f, 0.25f, 0.1f);
            decompBtn.colors = decompBtnColors;
            var capturedItem = item;
            decompBtn.onClick.AddListener(() =>
            {
                DecomposeItem(capturedItem);
            });
            var decompBtnLE = decompBtnGo.AddComponent<LayoutElement>();
            decompBtnLE.preferredWidth = 55;
            decompBtnLE.preferredHeight = 28;

            var decompTextGo = new GameObject("DecompText");
            decompTextGo.transform.SetParent(decompBtnGo.transform, false);
            var decompTextRt = decompTextGo.AddComponent<RectTransform>();
            decompTextRt.anchorMin = Vector2.zero;
            decompTextRt.anchorMax = Vector2.one;
            decompTextRt.offsetMin = Vector2.zero;
            decompTextRt.offsetMax = Vector2.zero;
            var decompText = decompTextGo.AddComponent<Text>();
            decompText.text = $"✦{shards}";
            decompText.fontSize = 12;
            decompText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            decompText.color = new Color(0.5f, 0.8f, 1f);
            decompText.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        /// <summary>从背包中分解一个灵物</summary>
        private void DecomposeItem(ItemData item)
        {
            if (item == null || PlayerController.Instance == null) return;

            var inventory = PlayerController.Instance.Inventory;
            int count = inventory.GetItemCount(item);
            if (count <= 0) return;

            // 分解获得碎片
            int shards = PlayerResources.GetDecomposeShards(item.rarity);
            if (PlayerResources.Instance != null)
                PlayerResources.Instance.AddShards(shards);

            // 从背包移除一个
            inventory.RemoveItem(item, 1);

            Debug.Log($"<color=yellow>背包分解：{item.itemName} → 获得 {shards} 灵力碎片</color>");

            // 刷新UI
            Refresh();
        }

        private string GetItemEffectText(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"攻+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻+{item.attackBonusPercent * 100}%");
            if (item.maxHpBonus > 0) parts.Add($"命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"命+{item.maxHpBonusPercent * 100}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"速+{item.moveSpeedBonusPercent * 100}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤+{item.damageReductionBonus * 100}%");
            if (item.critRateBonus > 0) parts.Add($"暴击+{item.critRateBonus * 100}%");
            if (item.healOnKill > 0) parts.Add($"回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            return parts.Count > 0 ? string.Join(" ", parts) : item.description;
        }

        // ==================== Synergy ====================

        private GameObject CreateSynergyEntry(SynergySystem.SynergyDef synergy, bool active)
        {
            var go = new GameObject($"Synergy_{synergy.name}");
            go.transform.SetParent(synergyListContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 28);

            var bg = go.AddComponent<Image>();
            bg.color = active
                ? new Color(synergy.displayColor.r * 0.3f, synergy.displayColor.g * 0.3f, synergy.displayColor.b * 0.3f, 0.6f)
                : new Color(0.1f, 0.1f, 0.1f, 0.4f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(6, 6, 2, 2);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // 状态标记
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(go.transform, false);
            var statusText = statusGo.AddComponent<Text>();
            statusText.text = active ? "✦" : "○";
            statusText.fontSize = 14;
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.color = active ? synergy.displayColor : new Color(0.4f, 0.4f, 0.4f);
            var statusLE = statusGo.AddComponent<LayoutElement>();
            statusLE.preferredWidth = 18;

            // 名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = synergy.name;
            nameText.fontSize = 13;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = active ? synergy.displayColor : new Color(0.5f, 0.5f, 0.5f);
            nameText.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 70;

            // 描述
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descText = descGo.AddComponent<Text>();
            descText.text = synergy.description;
            descText.fontSize = 11;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.color = active ? new Color(0.9f, 0.9f, 0.9f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            var descLE = descGo.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1;

            return go;
        }

        private GameObject CreateTextEntry(Transform parent, string text, int fontSize, Color color)
        {
            var go = new GameObject("TextEntry");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 26);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            return go;
        }
    }
}
