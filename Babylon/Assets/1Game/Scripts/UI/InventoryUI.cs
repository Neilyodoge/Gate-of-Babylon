using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace XianTu
{
    /// <summary>
    /// 灵物背包 UI —— Tab 键打开/关闭
    /// 显示所有持有灵物及其效果、数量、Synergy 状态
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform itemListContent;
        [SerializeField] private Transform synergyListContent;
        [SerializeField] private Text titleText;

        private bool _isOpen;
        private readonly List<GameObject> _itemEntries = new();
        private readonly List<GameObject> _synergyEntries = new();

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

            var items = PlayerController.Instance.Inventory.GetAllItems();

            if (titleText != null)
                titleText.text = $"灵物背包 ({items.Count})";

            // 灵物列表
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

            // Synergy 列表
            var allSynergies = SynergySystem.GetAllSynergies();
            var activeSynergies = SynergySystem.GetActiveSynergies();

            foreach (var synergy in allSynergies)
            {
                bool active = activeSynergies.Contains(synergy.name);
                var entry = CreateSynergyEntry(synergy, active);
                _synergyEntries.Add(entry);
            }
        }

        private GameObject CreateItemEntry(ItemData item, int count)
        {
            var go = new GameObject($"Item_{item.itemName}");
            go.transform.SetParent(itemListContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 40);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(8, 8, 4, 4);
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
            nameText.fontSize = 15;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = item.GetRarityColor();
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 100;

            // 数量
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(go.transform, false);
            var countText = countGo.AddComponent<Text>();
            countText.text = $"x{count}";
            countText.fontSize = 14;
            countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.color = new Color(0.8f, 0.8f, 0.8f);
            var countLE = countGo.AddComponent<LayoutElement>();
            countLE.preferredWidth = 40;

            // 描述
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descText = descGo.AddComponent<Text>();
            descText.text = GetItemEffectText(item);
            descText.fontSize = 12;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            var descLE = descGo.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1;

            return go;
        }

        private string GetItemEffectText(ItemData item)
        {
            var parts = new List<string>();
            if (item.attackBonus > 0) parts.Add($"攻击+{item.attackBonus}");
            if (item.attackBonusPercent > 0) parts.Add($"攻击+{item.attackBonusPercent * 100}%");
            if (item.maxHpBonus > 0) parts.Add($"生命+{item.maxHpBonus}");
            if (item.maxHpBonusPercent > 0) parts.Add($"生命+{item.maxHpBonusPercent * 100}%");
            if (item.moveSpeedBonusPercent > 0) parts.Add($"移速+{item.moveSpeedBonusPercent * 100}%");
            if (item.damageReductionBonus > 0) parts.Add($"减伤+{item.damageReductionBonus * 100}%");
            if (item.critRateBonus > 0) parts.Add($"暴击+{item.critRateBonus * 100}%");
            if (item.healOnKill > 0) parts.Add($"击杀回复{item.healOnKill}");
            if (item.burnDamagePerSecond > 0) parts.Add($"灼烧{item.burnDamagePerSecond}/s");
            return parts.Count > 0 ? string.Join("  ", parts) : item.description;
        }

        private GameObject CreateSynergyEntry(SynergySystem.SynergyDef synergy, bool active)
        {
            var go = new GameObject($"Synergy_{synergy.name}");
            go.transform.SetParent(synergyListContent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30);

            var bg = go.AddComponent<Image>();
            bg.color = active
                ? new Color(synergy.displayColor.r * 0.3f, synergy.displayColor.g * 0.3f, synergy.displayColor.b * 0.3f, 0.6f)
                : new Color(0.1f, 0.1f, 0.1f, 0.4f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // 状态标记
            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(go.transform, false);
            var statusText = statusGo.AddComponent<Text>();
            statusText.text = active ? "✦" : "○";
            statusText.fontSize = 16;
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.color = active ? synergy.displayColor : new Color(0.4f, 0.4f, 0.4f);
            var statusLE = statusGo.AddComponent<LayoutElement>();
            statusLE.preferredWidth = 20;

            // 名称
            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(go.transform, false);
            var nameText = nameGo.AddComponent<Text>();
            nameText.text = synergy.name;
            nameText.fontSize = 14;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.color = active ? synergy.displayColor : new Color(0.5f, 0.5f, 0.5f);
            nameText.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.preferredWidth = 80;

            // 描述
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(go.transform, false);
            var descText = descGo.AddComponent<Text>();
            descText.text = synergy.description;
            descText.fontSize = 12;
            descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            descText.color = active ? new Color(0.9f, 0.9f, 0.9f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            var descLE = descGo.AddComponent<LayoutElement>();
            descLE.flexibleWidth = 1;

            return go;
        }

        private GameObject CreateTextEntry(Transform parent, string text, int fontSize, Color color)
        {
            var go = new GameObject("EmptyText");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 30);
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
