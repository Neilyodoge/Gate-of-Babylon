using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物背包系统 —— 管理玩家持有的所有灵物
    /// "捡到就生效，叠加就完事"
    /// </summary>
    public class ItemInventory : MonoBehaviour
    {
        /// <summary>持有的灵物及其数量</summary>
        private readonly Dictionary<ItemData, int> _items = new();

        /// <summary>基础属性（角色初始值）</summary>
        private CombatStats _baseStats;

        /// <summary>玩家战斗属性引用</summary>
        private CombatStats _playerStats;

        public IReadOnlyDictionary<ItemData, int> Items => _items;

        /// <summary>
        /// 初始化，绑定玩家属性
        /// </summary>
        public void Initialize(CombatStats baseStats, CombatStats playerStats)
        {
            _baseStats = baseStats.Clone();
            _playerStats = playerStats;
        }

        /// <summary>
        /// 添加灵物（拾取时调用）
        /// </summary>
        public void AddItem(ItemData item)
        {
            if (item == null) return;

            if (_items.ContainsKey(item))
                _items[item]++;
            else
                _items[item] = 1;

            int count = _items[item];

            // 检查质变
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

            // 重新计算所有属性
            RecalculateStats();

            // 检查 Synergy 组合
            SynergySystem.CheckSynergies(_items, _playerStats);

            // 发布事件
            GameEvents.Publish(new GameEvents.ItemPickedUp
            {
                Item = item,
                CurrentCount = count
            });

            Debug.Log($"<color=green>拾取灵物：{item.itemName}（{item.rarity}）x{count}</color>");
        }

        /// <summary>
        /// 获取某灵物的持有数量
        /// </summary>
        public int GetItemCount(ItemData item)
        {
            return _items.TryGetValue(item, out int count) ? count : 0;
        }

        /// <summary>
        /// 重新计算所有属性（每次灵物变化时调用）
        /// 从基础值开始，叠加所有灵物效果
        /// </summary>
        private void RecalculateStats()
        {
            // 先恢复到基础值
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

            // 先叠加绝对值，再叠加百分比
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

            foreach (var kvp in _items)
            {
                ItemData item = kvp.Key;
                int count = kvp.Value;

                attackFlatBonus += item.attackBonus * count;
                attackPercentBonus += item.attackBonusPercent * count;
                hpFlatBonus += item.maxHpBonus * count;
                hpPercentBonus += item.maxHpBonusPercent * count;
                moveSpeedPercentBonus += item.moveSpeedBonusPercent * count;
                damageReductionBonus += item.damageReductionBonus * count;
                critRateBonus += item.critRateBonus * count;
                attackSpeedPercentBonus += item.attackSpeedBonusPercent * count;
                pierceBonus += item.pierceBonus * count;
                projectileSpeedPercentBonus += item.projectileSpeedBonusPercent * count;
            }

            // 应用：基础值 + 绝对加成，再乘以百分比
            _playerStats.attackDamage = (_baseStats.attackDamage + attackFlatBonus) * (1f + attackPercentBonus);
            _playerStats.maxHp = (_baseStats.maxHp + hpFlatBonus) * (1f + hpPercentBonus);
            _playerStats.moveSpeed = _baseStats.moveSpeed * (1f + moveSpeedPercentBonus);
            _playerStats.damageReduction = Mathf.Clamp01(_baseStats.damageReduction + damageReductionBonus);
            _playerStats.critRate = Mathf.Clamp01(_baseStats.critRate + critRateBonus);
            _playerStats.attackSpeed = _baseStats.attackSpeed * (1f + attackSpeedPercentBonus);
            _playerStats.pierceCount = _baseStats.pierceCount + pierceBonus;
            _playerStats.projectileSpeed = _baseStats.projectileSpeed * (1f + projectileSpeedPercentBonus);

            // 保持血量比例
            _playerStats.currentHp = _playerStats.maxHp * hpRatio;

            // 通知 UI 更新
            GameEvents.Publish(new GameEvents.HealthChanged
            {
                CurrentHp = _playerStats.currentHp,
                MaxHp = _playerStats.maxHp
            });
        }

        /// <summary>
        /// 检查是否持有某灵物（用于 Synergy 判断）
        /// </summary>
        public bool HasItem(ItemData item, int minCount = 1)
        {
            return GetItemCount(item) >= minCount;
        }

        /// <summary>
        /// 获取所有灵物列表（UI 展示用）
        /// </summary>
        public List<(ItemData item, int count)> GetAllItems()
        {
            return _items.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        }

        /// <summary>
        /// 获取灼烧总伤害（所有灼烧灵物叠加）
        /// </summary>
        public float GetTotalBurnDPS()
        {
            float total = 0f;
            foreach (var kvp in _items)
            {
                if (kvp.Key.burnDamagePerSecond > 0)
                    total += kvp.Key.burnDamagePerSecond * kvp.Value;
            }
            return total;
        }

        /// <summary>
        /// 获取冻结概率（取最高值，不叠加）
        /// </summary>
        public float GetFreezeChance()
        {
            float max = 0f;
            foreach (var kvp in _items)
            {
                if (kvp.Key.freezeChance > 0)
                    max = Mathf.Max(max, kvp.Key.freezeChance * kvp.Value);
            }
            return Mathf.Clamp01(max);
        }

        /// <summary>
        /// 获取击杀回复总量
        /// </summary>
        public float GetHealOnKill()
        {
            float total = 0f;
            foreach (var kvp in _items)
            {
                if (kvp.Key.healOnKill > 0)
                    total += kvp.Key.healOnKill * kvp.Value;
            }
            return total;
        }

        /// <summary>
        /// 应用质变效果（根据灵物分类和数量触发特殊效果）
        /// </summary>
        private string ApplyQualitativeEffect(ItemData item, int count)
        {
            switch (item.category)
            {
                case ItemCategory.Attack:
                    if (count == 3)
                    {
                        _playerStats.attackDamage *= 1.25f;
                        _playerStats.critRate = Mathf.Clamp01(_playerStats.critRate + 0.1f);
                        return "攻伐灵力共鸣！攻击力+25%，暴击率+10%";
                    }
                    else if (count == 5)
                    {
                        _playerStats.attackDamage *= 1.5f;
                        _playerStats.critDamage += 0.5f;
                        return "攻伐灵力大成！攻击力+50%，暴击伤害+50%";
                    }
                    break;

                case ItemCategory.Defense:
                    if (count == 3)
                    {
                        _playerStats.damageReduction = Mathf.Clamp01(_playerStats.damageReduction + 0.15f);
                        _playerStats.maxHp *= 1.15f;
                        _playerStats.currentHp *= 1.15f;
                        return "护体灵力共鸣！减伤+15%，生命+15%";
                    }
                    else if (count == 5)
                    {
                        _playerStats.damageReduction = Mathf.Clamp01(_playerStats.damageReduction + 0.25f);
                        return "护体灵力大成！减伤+25%，受击反弹伤害";
                    }
                    break;

                case ItemCategory.Movement:
                    if (count == 3)
                    {
                        _playerStats.moveSpeed *= 1.2f;
                        _playerStats.dashCooldown *= 0.7f;
                        return "身法灵力共鸣！移速+20%，闪避CD-30%";
                    }
                    else if (count == 5)
                    {
                        _playerStats.moveSpeed *= 1.3f;
                        return "身法灵力大成！移速+30%，闪避无敌帧延长";
                    }
                    break;

                case ItemCategory.Anomaly:
                    if (count == 3)
                    {
                        _playerStats.critRate = Mathf.Clamp01(_playerStats.critRate + 0.15f);
                        return "异变灵力共鸣！暴击率+15%，攻击附带灵力爆发";
                    }
                    break;

                case ItemCategory.Pill:
                    if (count == 3)
                    {
                        _playerStats.maxHp *= 1.3f;
                        _playerStats.currentHp = _playerStats.maxHp;
                        return "丹药灵力共鸣！生命+30%并全满！";
                    }
                    break;
            }

            return "灵力共鸣，属性提升";
        }

        /// <summary>
        /// 清空所有灵物（新一局开始时）
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            SynergySystem.Clear();
            RecalculateStats();
        }
    }
}
