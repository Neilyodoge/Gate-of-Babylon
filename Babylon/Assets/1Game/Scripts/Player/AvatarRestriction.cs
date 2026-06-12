using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// GDD §4.9：化身配表 runtime — DefaultItem_ID 初始装备 + Restriction 黑名单。
    /// 静态工具类，在入秘境时给初始灵物、在掉落/商店时过滤受限灵物。
    /// </summary>
    public static class AvatarRestriction
    {
        /// <summary>入秘境时调用：若当前化身有 DefaultItem_ID，从 itemPool 中找到并赋予。</summary>
        public static void GrantDefaultItem(ItemData[] itemPool)
        {
            var ctrl = PlayerController.Instance?.GetComponent<SpiritRootController>();
            if (ctrl == null || ctrl.CurrentRoot == SpiritRootType.None) return;

            var db = ConfigDatabase.Instance;
            if (db == null || !db.Loaded) return;

            var row = db.GetAvatar((int)ctrl.CurrentRoot);
            if (row == null || row.DefaultItem_ID <= 0) return;
            if (itemPool == null) return;

            ItemData defaultItem = null;
            foreach (var item in itemPool)
            {
                if (item != null && item.configId == row.DefaultItem_ID)
                {
                    defaultItem = item;
                    break;
                }
            }

            if (defaultItem == null)
            {
                Debug.LogWarning($"[AvatarRestriction] DefaultItem_ID={row.DefaultItem_ID} 在灵物池中未找到匹配 configId 的 SO");
                return;
            }

            var player = PlayerController.Instance;
            player.Inventory.AddItem(defaultItem);

            var spiritSlots = player.GetComponent<SpiritSlotSystem>();
            if (spiritSlots != null)
            {
                int empty = spiritSlots.FindEmptySlot();
                if (empty >= 0) spiritSlots.SetSlot(empty, defaultItem);
            }

            if (defaultItem.linkedSkill != null)
            {
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    int slot = combat.FindEmptySlot();
                    if (slot >= 0) combat.EquipSkillToSlot(defaultItem.linkedSkill, slot);
                }
            }

            Debug.Log($"<color=cyan>[AvatarRestriction] 初始灵物已赋予：{defaultItem.itemName}</color>");
        }

        /// <summary>判断某灵物是否对当前化身受限（黑名单）。返回 true = 可用。</summary>
        public static bool IsAllowed(ItemData item)
        {
            if (item == null || item.configId <= 0) return true;

            var ctrl = PlayerController.Instance?.GetComponent<SpiritRootController>();
            if (ctrl == null || ctrl.CurrentRoot == SpiritRootType.None) return true;

            var db = ConfigDatabase.Instance;
            if (db == null || !db.Loaded) return true;

            var row = db.GetAvatar((int)ctrl.CurrentRoot);
            if (row == null || row.Restriction == null || row.Restriction.Length == 0) return true;

            foreach (int restrictedId in row.Restriction)
            {
                if (restrictedId == item.configId) return false;
            }
            return true;
        }

        /// <summary>从候选池中过滤掉受限灵物，返回新数组。</summary>
        public static ItemData[] FilterPool(ItemData[] pool)
        {
            if (pool == null) return pool;
            var filtered = new System.Collections.Generic.List<ItemData>(pool.Length);
            foreach (var item in pool)
            {
                if (item != null && IsAllowed(item))
                    filtered.Add(item);
            }
            return filtered.ToArray();
        }
    }
}
