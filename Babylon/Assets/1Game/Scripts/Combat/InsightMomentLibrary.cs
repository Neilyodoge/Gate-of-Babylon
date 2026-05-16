using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 顿悟时刻奖励库（v0.5 修仙独有战斗机制 #2 配套）。
    ///
    /// 跟 <see cref="RealmRewardLibrary"/> 不同：
    /// - 不需要通关境界，达到悟性阈值就触发
    /// - 强度更小（数值奖励约 RealmReward 的 50~70%）
    /// - 不消耗悟性 —— 悟性继续积累用于撤离时转入永久（去悟道蒲团解锁天赋）
    ///
    /// 复用 <see cref="RealmReward"/> 结构 + <see cref="RealmRewardSelectUI"/> 显示。
    /// </summary>
    public static class InsightMomentLibrary
    {
        private static readonly List<RealmReward> _all = new();

        static InsightMomentLibrary()
        {
            // 8 个轻量奖励 —— 比 RealmReward 数值低、机制小巧
            _all.Add(MakeStat(
                "Insight_AtkUp", "灵识初窥", "攻击力 +10%（本局永久）",
                new Color(1f, 0.85f, 0.4f),
                StatModifier.Percent(StatType.AttackDamage, 0.10f)));

            _all.Add(MakeStat(
                "Insight_CritUp", "心若止水", "暴击率 +8%",
                new Color(1f, 0.85f, 0.4f),
                StatModifier.Flat(StatType.CritRate, 0.08f)));

            _all.Add(MakeStat(
                "Insight_MoveUp", "御风之意", "移速 +12%",
                new Color(0.5f, 0.85f, 1f),
                StatModifier.Percent(StatType.MoveSpeed, 0.12f)));

            _all.Add(MakeStat(
                "Insight_HpUp", "气海凝实", "最大生命 +15% 并回 30%",
                new Color(1f, 0.55f, 0.55f),
                StatModifier.Percent(StatType.MaxHp, 0.15f),
                healPercent: 0.30f));

            _all.Add(MakeStat(
                "Insight_AspdUp", "心手相应", "攻速 +15%",
                new Color(1f, 0.7f, 0.4f),
                StatModifier.Percent(StatType.AttackSpeed, 0.15f)));

            _all.Add(MakeStat(
                "Insight_ReduceUp", "玉体不坏", "减伤 +8%",
                new Color(0.85f, 0.7f, 0.4f),
                StatModifier.Flat(StatType.DamageReduction, 0.08f)));

            // 机制类：1 个补充闪避 / 1 个补充技能 CD
            _all.Add(new RealmReward
            {
                id = "Insight_DashCharge",
                displayName = "燕回意",
                description = "闪避充能上限 +1（本局永久）",
                category = RealmRewardCategory.Mechanic,
                applicableRoot = SpiritRootType.None,
                displayColor = new Color(0.5f, 0.85f, 1f),
                apply = (p) =>
                {
                    if (p == null) return;
                    p.SetMaxDashCharges(p.MaxDashCharges + 1);
                    p.RestoreDashCharge();
                }
            });

            _all.Add(new RealmReward
            {
                id = "Insight_SkillRefresh",
                displayName = "灵机一动",
                description = "立即刷新所有技能 CD",
                category = RealmRewardCategory.Mechanic,
                applicableRoot = SpiritRootType.None,
                displayColor = new Color(0.85f, 0.7f, 1f),
                apply = (p) =>
                {
                    var combat = p != null ? p.GetComponent<PlayerCombat>() : null;
                    if (combat != null) combat.ResetAllCooldowns();
                }
            });
        }

        // ========== helpers ==========

        private static RealmReward MakeStat(string id, string name, string desc, Color color,
            StatModifier mod, float healPercent = 0f)
        {
            return new RealmReward
            {
                id = id,
                displayName = name,
                description = desc,
                category = RealmRewardCategory.Numeric,
                applicableRoot = SpiritRootType.None,
                displayColor = color,
                apply = (p) =>
                {
                    var status = p != null ? p.GetComponent<StatusEffectController>() : null;
                    if (status == null) return;
                    status.Apply(new StatusEffect
                    {
                        id = id,
                        isBuff = true,
                        elementTag = ElementTag.None,
                        stacks = 1,
                        maxStacks = 1,
                        defaultDuration = -1f,
                        duration = -1f,
                        modifiers = new List<StatModifier> { mod },
                        displayName = name,
                        description = desc,
                        uiColor = color
                    });
                    if (healPercent > 0f)
                    {
                        p.Stats.Heal(p.Stats.maxHp * healPercent);
                        GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = p.Stats.currentHp, MaxHp = p.Stats.maxHp });
                    }
                }
            };
        }

        private static RealmReward MakeStat(string id, string name, string desc, Color color, StatModifier mod)
            => MakeStat(id, name, desc, color, mod, 0f);

        /// <summary>从池中随机抽 3 个（带去重）</summary>
        public static List<RealmReward> Roll3(HashSet<string> alreadyTaken = null)
        {
            var pool = new List<RealmReward>();
            foreach (var r in _all)
            {
                if (alreadyTaken != null && alreadyTaken.Contains(r.id)) continue;
                pool.Add(r);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            var result = new List<RealmReward>();
            for (int i = 0; i < Mathf.Min(3, pool.Count); i++) result.Add(pool[i]);
            return result;
        }
    }
}
