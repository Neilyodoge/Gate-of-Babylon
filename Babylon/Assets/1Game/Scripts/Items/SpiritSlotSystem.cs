using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物槽位系统 —— 梦之形风格
    /// 每个技能（Q/E/R）下方2个灵物槽位，共6个
    /// 灵物拾取后放入槽中，可长按拖拽交换位置
    /// </summary>
    public class SpiritSlotSystem : MonoBehaviour
    {
        /// <summary>灵物槽位数据</summary>
        public class SpiritSlot
        {
            public ItemData item;       // 槽位中的灵物（null=空）
            public int linkedSkillSlot; // 对应的技能槽位索引（0=Q, 1=E, 2=R）

            public bool IsEmpty => item == null;
        }

        /// <summary>6个灵物槽位，每个技能下方2个（Q:0-1, E:2-3, R:4-5）</summary>
        private readonly SpiritSlot[] _slots = new SpiritSlot[6];

        /// <summary>每个技能下方的灵物槽位数</summary>
        public const int SLOTS_PER_SKILL = 2;

        public IReadOnlyList<SpiritSlot> Slots => _slots;

        private ItemInventory _inventory;

        private void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new SpiritSlot
                {
                    item = null,
                    linkedSkillSlot = i / SLOTS_PER_SKILL // 0,0,1,1,2,2
                };
            }
            _inventory = GetComponent<ItemInventory>();
        }

        /// <summary>初始化（保留签名以兼容 PlayerController；全局属性由背包统一重算）</summary>
        public void Initialize(CombatStats baseStats, CombatStats playerStats)
        {
            if (_inventory == null) _inventory = GetComponent<ItemInventory>();
        }

        /// <summary>
        /// 将灵物放入指定槽位（返回被替换的旧灵物，可能为null）
        /// </summary>
        public ItemData SetSlot(int slotIndex, ItemData item)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return null;

            // V.03（Q8）：整套灵物屏蔽时，不接受装备灵物（不产生槽位词条 / 效果）。
            // 返回 null 表示"未放入、无被替换物"，避免调用方把灵物当作旧物重新掉落。
            if (!FeatureFlags.EnableSpiritItems && item != null) return null;

            ItemData old = _slots[slotIndex].item;

            // 移除旧灵物效果
            if (old != null)
                RemoveItemEffect(old, slotIndex);

            _slots[slotIndex].item = item;

            // 应用新灵物效果
            if (item != null)
                ApplyItemEffect(item, slotIndex);

            // 槽位变化 → 通知背包重算（灵物离开槽位则其全局词条立即失效）
            if (_inventory != null)
                _inventory.RecalculatePlayerStats();

            // 发布事件
            GameEvents.Publish(new GameEvents.SpiritSlotChanged
            {
                SlotIndex = slotIndex,
                NewItem = item,
                OldItem = old
            });

            string slotName = GetSlotKeyName(slotIndex);
            if (item != null)
                Debug.Log($"<color=green>灵物 {item.itemName} 放入 {slotName} 槽位</color>");
            else if (old != null)
                Debug.Log($"<color=gray>灵物 {old.itemName} 从 {slotName} 槽位移除</color>");

            return old;
        }

        /// <summary>获取指定槽位的灵物</summary>
        public ItemData GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return null;
            return _slots[slotIndex].item;
        }

        /// <summary>找到第一个空闲灵物槽位（-1表示没有空位）</summary>
        public int FindEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty) return i;
            }
            return -1;
        }

        /// <summary>交换两个灵物槽位</summary>
        public void SwapSlots(int slotA, int slotB)
        {
            if (slotA == slotB) return;
            if (slotA < 0 || slotA >= _slots.Length) return;
            if (slotB < 0 || slotB >= _slots.Length) return;

            // 先移除两个槽位的效果
            if (_slots[slotA].item != null)
                RemoveItemEffect(_slots[slotA].item, slotA);
            if (_slots[slotB].item != null)
                RemoveItemEffect(_slots[slotB].item, slotB);

            // 交换
            (_slots[slotA].item, _slots[slotB].item) = (_slots[slotB].item, _slots[slotA].item);

            // 重新应用效果
            if (_slots[slotA].item != null)
                ApplyItemEffect(_slots[slotA].item, slotA);
            if (_slots[slotB].item != null)
                ApplyItemEffect(_slots[slotB].item, slotB);

            // 槽位灵物未变，但绑定技能可能不同，仍触发一次重算保持一致
            if (_inventory != null)
                _inventory.RecalculatePlayerStats();

            Debug.Log($"<color=cyan>灵物槽位交换：{GetSlotKeyName(slotA)} ↔ {GetSlotKeyName(slotB)}</color>");
        }

        /// <summary>移除指定槽位的灵物（返回被移除的灵物）</summary>
        public ItemData RemoveFromSlot(int slotIndex)
        {
            return SetSlot(slotIndex, null);
        }

        /// <summary>清空所有灵物槽位</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                SetSlot(i, null);
            }
        }

        // ==================== 效果应用 ====================

        /// <summary>
        /// 槽位效果：仅处理技能充能层数等槽位专属逻辑。
        /// 灵物全局数值已由 <see cref="ItemInventory.RecalculateStats"/> 按背包持有数统一聚合，避免与背包重算互相覆盖。
        /// </summary>
        private void ApplyItemEffect(ItemData item, int slotIndex)
        {
            if (item == null) return;

            if (item.skillChargeBonus > 0)
            {
                int skillIdx = slotIndex / SLOTS_PER_SKILL;
                var combat = GetComponent<PlayerCombat>();
                if (combat != null)
                    combat.AddChargeBonus(skillIdx, item.skillChargeBonus);
            }
        }

        private void RemoveItemEffect(ItemData item, int slotIndex)
        {
            if (item == null) return;

            if (item.skillChargeBonus > 0)
            {
                int skillIdx = slotIndex / SLOTS_PER_SKILL;
                var combat = GetComponent<PlayerCombat>();
                if (combat != null)
                    combat.RemoveChargeBonus(skillIdx, item.skillChargeBonus);
            }
        }

        /// <summary>
        /// 获取指定槽位灵物的灼烧DPS（针对该槽位对应的技能）
        /// </summary>
        public float GetSlotBurnDPS(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return 0f;
            var item = _slots[slotIndex].item;
            return item != null ? item.burnDamagePerSecond : 0f;
        }

        /// <summary>获取所有槽位灵物的总灼烧DPS</summary>
        public float GetTotalBurnDPS()
        {
            float total = 0f;
            foreach (var slot in _slots)
            {
                if (slot.item != null)
                    total += slot.item.burnDamagePerSecond;
            }
            return total;
        }

        /// <summary>获取指定槽位灵物的冻结概率</summary>
        public float GetSlotFreezeChance(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return 0f;
            var item = _slots[slotIndex].item;
            return item != null ? item.freezeChance : 0f;
        }

        /// <summary>获取指定技能槽位的蓄力速度加成（来自该技能下方的灵物）</summary>
        public float GetSkillChargeSpeedBonus(int skillSlotIndex)
        {
            float total = 0f;
            int startSlot = skillSlotIndex * SLOTS_PER_SKILL;
            for (int i = startSlot; i < startSlot + SLOTS_PER_SKILL && i < _slots.Length; i++)
            {
                if (_slots[i].item != null)
                    total += _slots[i].item.chargeSpeedBonusPercent;
            }
            return total;
        }

        /// <summary>获取指定技能槽位的蓄力伤害加成（来自该技能下方的灵物）</summary>
        public float GetSkillChargeDamageBonus(int skillSlotIndex)
        {
            float total = 0f;
            int startSlot = skillSlotIndex * SLOTS_PER_SKILL;
            for (int i = startSlot; i < startSlot + SLOTS_PER_SKILL && i < _slots.Length; i++)
            {
                if (_slots[i].item != null)
                    total += _slots[i].item.chargeDamageBonusPercent;
            }
            return total;
        }

        /// <summary>获取指定技能槽位的CD缩减加成（来自该技能下方的灵物）</summary>
        public float GetSkillCooldownReduction(int skillSlotIndex)
        {
            float total = 0f;
            int startSlot = skillSlotIndex * SLOTS_PER_SKILL;
            for (int i = startSlot; i < startSlot + SLOTS_PER_SKILL && i < _slots.Length; i++)
            {
                if (_slots[i].item != null)
                    total += _slots[i].item.cooldownReductionPercent;
            }
            return Mathf.Clamp01(total); // 最高100%
        }

        /// <summary>
        /// 获取某个技能槽下方的所有灵物（去掉空槽，返回 ItemData 列表）。
        /// 用于 GDD 6.5 技能修饰匹配（按 modTag）。
        /// </summary>
        public System.Collections.Generic.List<ItemData> GetItemsInSkillSlot(int skillSlotIndex)
        {
            var list = new System.Collections.Generic.List<ItemData>();
            int startSlot = skillSlotIndex * SLOTS_PER_SKILL;
            for (int i = startSlot; i < startSlot + SLOTS_PER_SKILL && i < _slots.Length; i++)
            {
                if (_slots[i].item != null) list.Add(_slots[i].item);
            }
            return list;
        }

        private string GetSlotKeyName(int slotIndex)
        {
            int skillIdx = slotIndex / SLOTS_PER_SKILL;
            int subIdx = slotIndex % SLOTS_PER_SKILL;
            string skillName = skillIdx switch
            {
                0 => "Q",
                1 => "E",
                2 => "R",
                _ => "?"
            };
            return $"{skillName}-{subIdx + 1}";
        }
    }
}
