using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 状态效果控制器 —— 挂在玩家 / 敌人身上，统一管理身上所有 StatusEffect。
    ///
    /// 核心职责：
    /// 1. 添加 / 刷新 / 移除 StatusEffect（按 id）
    /// 2. 每帧推进 duration / tickTimer，到期触发 onExpire
    /// 3. 把所有 StatusEffect 的 modifiers 聚合，作为 ItemInventory.RecalculateStats 末尾的一步
    /// 4. 元素反应（5.6）的入口：检查命中元素与已有元素状态的反应表
    ///
    /// 与现有系统的边界：
    /// - 对玩家：和 ItemInventory / SpiritSlotSystem / SynergySystem 平行，由 ItemInventory.RecalculateStats 主动 pull modifiers
    /// - 对敌人：单独跑 Update 推进 duration，到期清状态；不参与玩家属性聚合
    /// </summary>
    public class StatusEffectController : MonoBehaviour
    {
        private readonly Dictionary<string, StatusEffect> _effects = new();

        public IReadOnlyDictionary<string, StatusEffect> Effects => _effects;

        // 状态变化对外通知（HUD 用）
        public event System.Action<StatusEffect> OnEffectAdded;
        public event System.Action<StatusEffect> OnEffectChanged;
        public event System.Action<StatusEffect> OnEffectRemoved;

        // 通过有无 ItemInventory 判断是否挂在玩家身上：玩家上每次 modifiers 变化要触发属性重算

        /// <summary>
        /// 添加一个状态。同 id 已存在时叠层 + 刷新持续时间。
        /// </summary>
        public StatusEffect Apply(StatusEffect template)
        {
            if (template == null) return null;

            if (_effects.TryGetValue(template.id, out var existing))
            {
                int newStacks = Mathf.Min(existing.maxStacks > 0 ? existing.maxStacks : int.MaxValue,
                                          existing.stacks + Mathf.Max(1, template.stacks));
                existing.stacks = newStacks;
                if (!existing.IsPermanent && template.defaultDuration > 0f)
                    existing.duration = Mathf.Max(existing.duration, template.defaultDuration);
                OnEffectChanged?.Invoke(existing);
                NotifyPlayerStatsDirty();
                return existing;
            }

            var inst = template.Clone();
            inst.duration = inst.defaultDuration;
            _effects[inst.id] = inst;
            inst.onApply?.Invoke(inst, gameObject);
            OnEffectAdded?.Invoke(inst);
            NotifyPlayerStatsDirty();
            return inst;
        }

        /// <summary>
        /// 移除一层；移除后层数归零则彻底清除。
        /// </summary>
        public void Consume(string id, int amount = 1)
        {
            if (!_effects.TryGetValue(id, out var eff)) return;
            eff.stacks -= amount;
            if (eff.stacks <= 0)
            {
                Remove(id);
                return;
            }
            OnEffectChanged?.Invoke(eff);
            NotifyPlayerStatsDirty();
        }

        public void Remove(string id)
        {
            if (!_effects.TryGetValue(id, out var eff)) return;
            _effects.Remove(id);
            eff.onExpire?.Invoke(eff, gameObject);
            OnEffectRemoved?.Invoke(eff);
            NotifyPlayerStatsDirty();
        }

        public bool Has(string id) => _effects.ContainsKey(id);

        public StatusEffect Get(string id) => _effects.TryGetValue(id, out var e) ? e : null;

        public void Clear()
        {
            var ids = new List<string>(_effects.Keys);
            foreach (var id in ids) Remove(id);
        }

        private void Update()
        {
            if (_effects.Count == 0) return;

            List<string> expired = null;
            foreach (var kv in _effects)
            {
                var eff = kv.Value;

                if (eff.tickInterval > 0f)
                {
                    eff.tickTimer += Time.deltaTime;
                    while (eff.tickTimer >= eff.tickInterval)
                    {
                        eff.tickTimer -= eff.tickInterval;
                        eff.onTick?.Invoke(eff, gameObject, eff.tickInterval);
                    }
                }

                if (!eff.IsPermanent)
                {
                    eff.duration -= Time.deltaTime;
                    if (eff.duration <= 0f)
                    {
                        (expired ??= new List<string>()).Add(eff.id);
                    }
                }
            }

            if (expired != null)
                foreach (var id in expired) Remove(id);
        }

        /// <summary>
        /// 把所有状态的 modifiers 聚合到给定 CombatStats 上。
        /// 由 <see cref="ItemInventory.RecalculateStats"/> 末尾调用，以确保属性单一写入入口。
        /// </summary>
        public void ApplyModifiersTo(CombatStats stats)
        {
            if (stats == null) return;

            float atkFlat = 0f, atkPct = 0f;
            float hpFlat = 0f, hpPct = 0f;
            float msPct = 0f, asPct = 0f;
            float drFlat = 0f;
            float crFlat = 0f, cdFlat = 0f;
            int pierceFlat = 0;
            float psPct = 0f;
            float defFlat = 0f, avatarCoeffFlat = 0f, dmgBonusPct = 0f, armorPenFlat = 0f, skillDmgPct = 0f;

            foreach (var kv in _effects)
            {
                var eff = kv.Value;
                if (eff.modifiers == null) continue;
                int s = Mathf.Max(1, eff.stacks);

                foreach (var m in eff.modifiers)
                {
                    float v = m.value * s;
                    switch (m.type)
                    {
                        case StatType.AttackDamage:
                            if (m.isPercent) atkPct += v; else atkFlat += v;
                            break;
                        case StatType.AttackSpeed:
                            asPct += v; break;
                        case StatType.MaxHp:
                            if (m.isPercent) hpPct += v; else hpFlat += v;
                            break;
                        case StatType.MoveSpeed:
                            msPct += v; break;
                        case StatType.DamageReduction:
                            drFlat += v; break;
                        case StatType.CritRate:
                            crFlat += v; break;
                        case StatType.CritDamage:
                            cdFlat += v; break;
                        case StatType.PierceCount:
                            pierceFlat += Mathf.RoundToInt(v); break;
                        case StatType.ProjectileSpeed:
                            psPct += v; break;
                        case StatType.Defense:
                            defFlat += v; break;
                        case StatType.AvatarCoefficient:
                            avatarCoeffFlat += v; break;
                        case StatType.DamageBonusPercent:
                            dmgBonusPct += v; break;
                        case StatType.ArmorPenPercent:
                            armorPenFlat += v; break;
                        case StatType.SkillDamagePercent:
                            skillDmgPct += v; break;
                    }
                }
            }

            // 与 ItemInventory 中相同的"先叠 flat 再乘 pct"风格
            stats.attackDamage = (stats.attackDamage + atkFlat) * (1f + atkPct);
            stats.maxHp = (stats.maxHp + hpFlat) * (1f + hpPct);
            stats.moveSpeed *= (1f + msPct);
            stats.attackSpeed *= (1f + asPct);
            stats.damageReduction = Mathf.Clamp01(stats.damageReduction + drFlat);
            stats.critRate = Mathf.Clamp01(stats.critRate + crFlat);
            stats.critDamage += cdFlat;
            stats.pierceCount += pierceFlat;
            stats.projectileSpeed *= (1f + psPct);

            stats.defense += defFlat;
            stats.avatarCoefficient += avatarCoeffFlat;
            stats.damageBonusPercent += dmgBonusPct;
            stats.armorPenPercent = Mathf.Clamp01(stats.armorPenPercent + armorPenFlat);
            stats.skillDamagePercent += skillDmgPct;
        }

        private void NotifyPlayerStatsDirty()
        {
            var inv = GetComponent<ItemInventory>();
            if (inv != null) inv.RecalculatePlayerStats();
        }
    }
}
