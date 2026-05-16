using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物背包系统 —— 仅记录"当前持有数量"。
    /// 全局战斗数值由 <see cref="RecalculateStats"/> 唯一写入，且**只取当前在 <see cref="SpiritSlotSystem"/> 槽位中的灵物**为来源；
    /// 不在槽位的灵物（未来用于特殊道具）不参与全局数值聚合，仅保留计数。
    /// </summary>
    public class ItemInventory : MonoBehaviour
    {
        private readonly Dictionary<ItemData, int> _items = new();
        private static readonly HashSet<string> SlotManagedQualitativeEffects = new()
        {
            "焚天",
            "玉碎",
            "御风",
            "剑阵",
            "涅槃"
        };

        private CombatStats _baseStats;
        private CombatStats _playerStats;
        private SpiritSlotSystem _spiritSlots;

        /// <summary>质变触发时一次性满血（灵藤草 x4、丹药共鸣 x5 等）</summary>
        private bool _pendingFullHealFromQualitative;

        public IReadOnlyDictionary<ItemData, int> Items => _items;

        private void Awake()
        {
            _spiritSlots = GetComponent<SpiritSlotSystem>();
        }

        public void Initialize(CombatStats baseStats, CombatStats playerStats)
        {
            _baseStats = baseStats.Clone();
            _playerStats = playerStats;
            if (_spiritSlots == null)
                _spiritSlots = GetComponent<SpiritSlotSystem>();
        }

        /// <summary>
        /// 遍历"当前在灵物槽位中"的灵物，附带其在背包中的持有数量；同一灵物只产出一次（去重）。
        /// </summary>
        private IEnumerable<(ItemData item, int count)> GetSlotItemsWithCount()
        {
            if (_spiritSlots == null) yield break;
            var seen = new HashSet<ItemData>();
            foreach (var slot in _spiritSlots.Slots)
            {
                if (slot == null || slot.item == null) continue;
                if (!seen.Add(slot.item)) continue;
                if (!_items.TryGetValue(slot.item, out var c) || c <= 0) continue;
                yield return (slot.item, c);
            }
        }

        public void AddItem(ItemData item)
        {
            if (item == null) return;

            if (_items.ContainsKey(item))
                _items[item]++;
            else
                _items[item] = 1;

            int count = _items[item];

            if (item.qualitativeThresholds != null)
            {
                foreach (int threshold in item.qualitativeThresholds)
                {
                    if (count == threshold)
                    {
                        string effectDesc = ApplyQualitativeEffect(item, count);
                        Debug.Log($"<color=yellow>✨ 质变触发！{item.itemName} x{count} — {effectDesc}</color>");

                        GameEvents.Publish(new GameEvents.QualitativeTriggered
                        {
                            Item = item,
                            Count = count,
                            EffectDescription = effectDesc
                        });
                    }
                }
            }

            SynergySystem.CheckSynergies(BuildSlotItemDict());
            RecalculateStats();

            if (_pendingFullHealFromQualitative)
            {
                _playerStats.currentHp = _playerStats.maxHp;
                _pendingFullHealFromQualitative = false;
                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = _playerStats.currentHp,
                    MaxHp = _playerStats.maxHp
                });
            }

            GameEvents.Publish(new GameEvents.ItemPickedUp
            {
                Item = item,
                CurrentCount = count
            });

            Debug.Log($"<color=green>拾取灵物：{item.itemName}（{item.rarity}）x{count}</color>");
        }

        public int GetItemCount(ItemData item)
        {
            return _items.TryGetValue(item, out int count) ? count : 0;
        }

        public void RemoveItem(ItemData item, int amount = 1)
        {
            if (item == null || !_items.ContainsKey(item)) return;

            _items[item] -= amount;
            if (_items[item] <= 0)
                _items.Remove(item);

            SynergySystem.CheckSynergies(BuildSlotItemDict());
            RecalculateStats();

            Debug.Log($"<color=gray>移除灵物：{item.itemName} x{amount}</color>");
        }

        /// <summary>
        /// 外部系统（如灵物槽位变更）请求重新聚合属性，勿直接改 <see cref="CombatStats"/>。
        /// </summary>
        public void RecalculatePlayerStats()
        {
            SynergySystem.CheckSynergies(BuildSlotItemDict());
            RecalculateStats();
        }

        /// <summary>构造"槽位灵物 → 持有数量"字典，供 SynergySystem 检查协同。</summary>
        private Dictionary<ItemData, int> BuildSlotItemDict()
        {
            var dict = new Dictionary<ItemData, int>();
            foreach (var (item, cnt) in GetSlotItemsWithCount())
                dict[item] = cnt;
            return dict;
        }

        private void RecalculateStats()
        {
            float hpRatio = _playerStats.maxHp > 0 ? _playerStats.currentHp / _playerStats.maxHp : 1f;

            _playerStats.attackDamage = _baseStats.attackDamage;
            _playerStats.attackSpeed = _baseStats.attackSpeed;
            _playerStats.maxHp = _baseStats.maxHp;
            _playerStats.moveSpeed = _baseStats.moveSpeed;
            _playerStats.damageReduction = _baseStats.damageReduction;
            _playerStats.critRate = _baseStats.critRate;
            _playerStats.critDamage = _baseStats.critDamage;
            _playerStats.pierceCount = _baseStats.pierceCount;
            _playerStats.projectileSpeed = _baseStats.projectileSpeed;
            _playerStats.dashCooldown = _baseStats.dashCooldown;

            float attackFlatBonus = 0f;
            float attackPercentBonus = 0f;
            float hpFlatBonus = 0f;
            float hpPercentBonus = 0f;
            float moveSpeedPercentBonus = 0f;
            float damageReductionBonus = 0f;
            float critRateBonus = 0f;
            float attackSpeedPercentBonus = 0f;
            int pierceBonus = 0;
            float projectileSpeedPercentBonus = 0f;

            // 仅"槽位中"的灵物贡献全局词条；同一灵物的持有数量从背包查
            foreach (var (item, cnt) in GetSlotItemsWithCount())
            {
                attackFlatBonus += item.attackBonus * cnt;
                attackPercentBonus += item.attackBonusPercent * cnt;
                hpFlatBonus += item.maxHpBonus * cnt;
                hpPercentBonus += item.maxHpBonusPercent * cnt;
                moveSpeedPercentBonus += item.moveSpeedBonusPercent * cnt;
                damageReductionBonus += item.damageReductionBonus * cnt;
                critRateBonus += item.critRateBonus * cnt;
                attackSpeedPercentBonus += item.attackSpeedBonusPercent * cnt;
                pierceBonus += item.pierceBonus * cnt;
                projectileSpeedPercentBonus += item.projectileSpeedBonusPercent * cnt;
            }

            _playerStats.attackDamage = (_baseStats.attackDamage + attackFlatBonus) * (1f + attackPercentBonus);
            _playerStats.maxHp = (_baseStats.maxHp + hpFlatBonus) * (1f + hpPercentBonus);
            _playerStats.moveSpeed = _baseStats.moveSpeed * (1f + moveSpeedPercentBonus);
            _playerStats.damageReduction = Mathf.Clamp01(_baseStats.damageReduction + damageReductionBonus);
            _playerStats.critRate = Mathf.Clamp01(_baseStats.critRate + critRateBonus);
            _playerStats.attackSpeed = _baseStats.attackSpeed * (1f + attackSpeedPercentBonus);
            _playerStats.pierceCount = _baseStats.pierceCount + pierceBonus;
            _playerStats.projectileSpeed = _baseStats.projectileSpeed * (1f + projectileSpeedPercentBonus);

            ApplyQualitativeStatModifiers();
            SyncQualitativeMechanisms();

            SynergySystem.ApplyActiveSynergyStatModifiers(_playerStats);

            // StatusEffect 框架（BUFF/DEBUFF）—— 化身连杀、临时增益等都走这里
            var statusCtrl = GetComponent<StatusEffectController>();
            if (statusCtrl != null) statusCtrl.ApplyModifiersTo(_playerStats);

            _playerStats.damageReduction = Mathf.Clamp01(_playerStats.damageReduction);
            _playerStats.critRate = Mathf.Clamp01(_playerStats.critRate);

            _playerStats.currentHp = _playerStats.maxHp * hpRatio;
            if (_playerStats.currentHp > _playerStats.maxHp)
                _playerStats.currentHp = _playerStats.maxHp;

            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _playerStats.currentHp,
                MaxHp = _playerStats.maxHp
            });
        }

        /// <summary>
        /// 质变带来的数值修正：按"槽位中存在该灵物 + 背包数量达标"两个条件叠加。
        /// 一旦灵物离开槽位，其质变数值立即失效（重算时不再产出该项）。
        /// </summary>
        private void ApplyQualitativeStatModifiers()
        {
            foreach (var (item, c) in GetSlotItemsWithCount())
            {
                switch (item.itemName)
                {
                    case "火灵珠":
                        if (c >= 8)
                        {
                            _playerStats.attackDamage *= 1.5f;
                            _playerStats.critDamage += 0.5f;
                        }
                        break;
                    case "锈铁飞剑":
                        if (c >= 8)
                        {
                            _playerStats.attackDamage *= 1.3f;
                            _playerStats.pierceCount += 3;
                        }
                        break;
                    case "破镜碎片":
                        if (c >= 3)
                            _playerStats.critDamage += 0.5f;
                        break;
                    case "灵藤草":
                        if (c >= 4)
                            _playerStats.maxHp *= 1.2f;
                        break;
                    case "星辰尘":
                        if (c >= 2)
                        {
                            _playerStats.moveSpeed *= 1.15f;
                            _playerStats.attackSpeed *= 1.15f;
                        }
                        break;
                    case "引魂灯":
                        if (c >= 2)
                        {
                            _playerStats.critRate = Mathf.Clamp01(_playerStats.critRate + 0.15f);
                            _playerStats.attackDamage *= 1.2f;
                        }
                        break;
                }

                switch (item.category)
                {
                    case ItemCategory.Anomaly:
                        if (c >= 5)
                            _playerStats.critRate = Mathf.Clamp01(_playerStats.critRate + 0.15f);
                        break;
                    case ItemCategory.Pill:
                        // 回灵丹 x5 在名称分支已触发「涅槃」并 return，不再叠加丹药共鸣（与旧版一致）
                        if (c >= 5 && item.itemName != "回灵丹")
                            _playerStats.maxHp *= 1.3f;
                        break;
                }
            }
        }

        public bool HasItem(ItemData item, int minCount = 1)
        {
            return GetItemCount(item) >= minCount;
        }

        public List<(ItemData item, int count)> GetAllItems()
        {
            return _items.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        public float GetTotalBurnDPS()
        {
            float total = 0f;
            foreach (var (item, cnt) in GetSlotItemsWithCount())
            {
                if (item.burnDamagePerSecond > 0)
                    total += item.burnDamagePerSecond * cnt;
            }
            return total;
        }

        public float GetFreezeChance()
        {
            float max = 0f;
            foreach (var (item, cnt) in GetSlotItemsWithCount())
            {
                if (item.freezeChance > 0)
                    max = Mathf.Max(max, item.freezeChance * cnt);
            }
            return Mathf.Clamp01(max);
        }

        public float GetHealOnKill()
        {
            float total = 0f;
            foreach (var (item, cnt) in GetSlotItemsWithCount())
            {
                if (item.healOnKill > 0)
                    total += item.healOnKill * cnt;
            }
            return total;
        }

        /// <summary>
        /// 根据"槽位中存在该灵物 + 背包数量达标"同步机制性质变。
        /// 灵物离开槽位或数量不达标时，对应机制立即停用。
        /// </summary>
        private void SyncQualitativeMechanisms()
        {
            var runner = QualitativeEffectRunner.Instance;
            if (runner == null) return;

            var desired = new HashSet<string>();
            foreach (var (item, count) in GetSlotItemsWithCount())
                AddDesiredQualitativeEffect(item, count, desired);

            foreach (string effectId in SlotManagedQualitativeEffects)
            {
                bool shouldBeActive = desired.Contains(effectId);
                bool isActive = runner.ActiveEffects.Contains(effectId);

                if (shouldBeActive && !isActive)
                    runner.ActivateEffect(effectId);
                else if (!shouldBeActive && isActive)
                    runner.DeactivateEffect(effectId);
            }
        }

        private static void AddDesiredQualitativeEffect(ItemData item, int count, HashSet<string> desired)
        {
            switch (item.itemName)
            {
                case "火灵珠":
                    if (count >= 5) desired.Add("焚天");
                    break;
                case "玉佩":
                    if (count >= 5) desired.Add("玉碎");
                    break;
                case "风灵珠":
                    if (count >= 5) desired.Add("御风");
                    break;
                case "锈铁飞剑":
                    if (count >= 5) desired.Add("剑阵");
                    break;
                case "回灵丹":
                    if (count >= 5) desired.Add("涅槃");
                    break;
            }
        }

        /// <summary>
        /// 质变提示与一次性满血标记；不直接激活机制或写持久属性。
        /// 机制是否生效由 <see cref="SyncQualitativeMechanisms"/> 按槽位状态统一同步。
        /// </summary>
        private string ApplyQualitativeEffect(ItemData item, int count)
        {
            switch (item.itemName)
            {
                case "火灵珠":
                    if (count == 5)
                        return "焚天！每5次攻击释放火焰冲击波，灼烧周围敌人";
                    if (count == 8)
                        return "焚天大成！攻击力+50%，暴击伤害+50%，冲击波伤害翻倍";
                    break;

                case "玉佩":
                    if (count == 5)
                        return "玉碎！受到致命伤害时碎裂免疫，击退周围敌人（CD 60秒）";
                    break;

                case "风灵珠":
                    if (count == 5)
                        return "御风！闪避后留下风之残影，残影自动攻击附近敌人";
                    break;

                case "锈铁飞剑":
                    if (count == 5)
                        return "剑阵！飞剑环绕护体，自动攻击靠近的敌人";
                    if (count == 8)
                        return "万剑归宗！攻击力+30%，穿透+3，剑阵伤害翻倍";
                    break;

                case "回灵丹":
                    if (count == 5)
                        return "涅槃！死亡时消耗回灵丹原地复活，复活后3秒无敌";
                    break;

                case "寒冰玉髓":
                    if (count == 4)
                        return "冰封领域！攻击冻结概率翻倍，被冻结的敌人受到额外50%伤害";
                    break;

                case "血珊瑚":
                    if (count == 5)
                        return "血祭！击杀敌人时回复10%最大生命值，灼烧伤害翻倍";
                    break;

                case "破镜碎片":
                    if (count == 3)
                        return "照妖真眼！暴击伤害+50%，暴击时对周围敌人造成溅射伤害";
                    break;

                case "灵藤草":
                    if (count == 4)
                    {
                        _pendingFullHealFromQualitative = true;
                        return "灵藤缠身！生命+20%并全满，击杀回复量翻倍";
                    }
                    break;

                case "星辰尘":
                    if (count == 2)
                        return "星移斗转！移速和攻速额外+15%，闪避距离增加";
                    break;

                case "引魂灯":
                    if (count == 2)
                        return "摄魂夺魄！暴击率+15%，攻击力+20%，击杀回复翻倍";
                    break;
            }

            switch (item.category)
            {
                case ItemCategory.Attack:
                    if (count == 5)
                        return "攻伐灵力共鸣，攻击附带灵力余波";
                    break;
                case ItemCategory.Defense:
                    if (count == 5)
                        return "护体灵力共鸣，受击时有概率格挡";
                    break;
                case ItemCategory.Movement:
                    if (count == 5)
                        return "身法灵力共鸣，移动留下残影";
                    break;
                case ItemCategory.Anomaly:
                    if (count == 5)
                        return "异变灵力共鸣！暴击率+15%，攻击附带灵力爆发";
                    break;
                case ItemCategory.Pill:
                    if (count == 5)
                    {
                        _pendingFullHealFromQualitative = true;
                        return "丹药灵力共鸣！生命+30%并全满！";
                    }
                    break;
            }

            return "灵力共鸣，属性提升";
        }

        public void Clear()
        {
            _items.Clear();
            _pendingFullHealFromQualitative = false;
            SynergySystem.Clear();
            if (QualitativeEffectRunner.Instance != null)
                QualitativeEffectRunner.Instance.Clear();
            RecalculateStats();
        }
    }
}
