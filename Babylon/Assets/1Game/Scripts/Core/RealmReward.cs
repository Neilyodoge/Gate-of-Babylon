using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 境界突破 3 选 1 奖励数据结构 —— GDD 8.2 落地（v0.4 最小可用版）
    /// </summary>
    public class RealmReward
    {
        public string id;
        public string displayName;
        public string description;
        public RealmRewardCategory category;
        /// <summary>限定化身（None=通用所有玩家可见；其他=仅该化身玩家可见）</summary>
        public SpiritRootType applicableRoot;
        /// <summary>应用奖励的实际逻辑（拿到 PlayerController 上自己写状态）</summary>
        public Action<PlayerController> apply;
        /// <summary>显示给玩家的主题色（数值类=金黄，机制类=蓝色，化身天赋类=对应化身色）</summary>
        public Color displayColor;
    }

    public enum RealmRewardCategory
    {
        Numeric,        // 数值类：直接 +X% 属性
        Mechanic,       // 机制类：灵物槽位 +1 / 闪避充能 +1
        Structural,     // 结构类：商店折扣 / 协同阈值 -1
        Risk,           // 风险类：高风险高回报
        SpiritTalent    // 化身天赋类：解锁当前化身的一个天赋节点
    }

    /// <summary>
    /// 境界突破奖励库 —— v0.4 首批落地（5 个通用 + 5 个化身天赋）。
    /// </summary>
    public static class RealmRewardLibrary
    {
        private static readonly List<RealmReward> _allRewards = new();

        static RealmRewardLibrary()
        {
            // ===== 通用奖励（5 个） =====
            _allRewards.Add(MakeStatReward(
                "Numeric_AtkUp", "剑意初成", "攻击力 +18%（本局永久）",
                RealmRewardCategory.Numeric, SpiritRootType.None,
                new Color(1f, 0.85f, 0.3f),
                new List<StatModifier> { StatModifier.Percent(StatType.AttackDamage, 0.18f) }));

            _allRewards.Add(new RealmReward
            {
                id = "Numeric_HpUp",
                displayName = "体魄筑基",
                description = "最大生命 +25% 并回满（本局永久）",
                category = RealmRewardCategory.Numeric,
                applicableRoot = SpiritRootType.None,
                displayColor = new Color(1f, 0.5f, 0.5f),
                apply = (p) =>
                {
                    ApplyPermanentStatusEffect(p, "Realm_HpUp", "体魄筑基", "最大生命 +25%",
                        new Color(1f, 0.5f, 0.5f),
                        new List<StatModifier> { StatModifier.Percent(StatType.MaxHp, 0.25f) });
                    p.Stats.Heal(p.Stats.maxHp);
                    GameEvents.Publish(new GameEvents.HealthChanged { CurrentHp = p.Stats.currentHp, MaxHp = p.Stats.maxHp });
                }
            });

            _allRewards.Add(MakeStatReward(
                "Numeric_CritUp", "五感凝练", "暴击率 +12%（本局永久）",
                RealmRewardCategory.Numeric, SpiritRootType.None,
                new Color(1f, 0.85f, 0.3f),
                new List<StatModifier> { StatModifier.Flat(StatType.CritRate, 0.12f) }));

            _allRewards.Add(MakeStatReward(
                "Mechanic_MoveSpeedUp", "身轻如燕", "移速 +20%（本局永久）",
                RealmRewardCategory.Mechanic, SpiritRootType.None,
                new Color(0.5f, 0.85f, 1f),
                new List<StatModifier> { StatModifier.Percent(StatType.MoveSpeed, 0.20f) }));

            _allRewards.Add(MakeStatReward(
                "Risk_GlassCannon", "走火入魔", "最大生命 -30%，攻击力 +60%（本局永久）",
                RealmRewardCategory.Risk, SpiritRootType.None,
                new Color(1f, 0.3f, 0.3f),
                new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MaxHp, -0.30f),
                    StatModifier.Percent(StatType.AttackDamage, 0.60f)
                }));

            // ===== 化身天赋类（每根 1 个代表节点）—— 走 StatusEffect 标记，由各化身 Controller 检查 =====

            _allRewards.Add(MakeTalentReward(
                "Talent_Gold_PowerBreak", "金 · 大破", "完美收刀命中后下一次技能伤害 +50%",
                SpiritRootType.Metal, new Color(1f, 0.85f, 0.2f)));

            _allRewards.Add(MakeTalentReward(
                "Talent_Wood_FertileSoil", "木 · 沃土", "寄生种子持续时间 6s → 12s",
                SpiritRootType.Wood, new Color(0.4f, 0.95f, 0.4f)));

            _allRewards.Add(MakeTalentReward(
                "Talent_Water_DoubleShadow", "水 · 双影", "影息斩同时附加 ×0.5 攻击力溅射伤害",
                SpiritRootType.Water, new Color(0.3f, 0.7f, 1f)));

            _allRewards.Add(MakeTalentReward(
                "Talent_Fire_BurningChain", "火 · 灼焰链", "狂火期间普攻 AOE 半径 +50%、伤害 +30%",
                SpiritRootType.Fire, new Color(1f, 0.4f, 0.1f)));

            _allRewards.Add(MakeTalentReward(
                "Talent_Earth_StoneSkin", "土 · 厚壁", "受到伤害减免 +15%（本局永久）",
                SpiritRootType.Earth, new Color(0.85f, 0.7f, 0.4f)));
        }

        // ============= helper =============

        /// <summary>纯数值奖励：用 StatusEffect 挂常驻 BUFF。</summary>
        private static RealmReward MakeStatReward(string id, string name, string desc,
            RealmRewardCategory cat, SpiritRootType root, Color color,
            List<StatModifier> mods)
        {
            return new RealmReward
            {
                id = id, displayName = name, description = desc,
                category = cat, applicableRoot = root, displayColor = color,
                apply = (p) => ApplyPermanentStatusEffect(p, "Realm_" + id, name, desc, color, mods)
            };
        }

        /// <summary>化身天赋奖励：用 StatusEffect 挂常驻"天赋标记"，由各化身 Controller 查询并改变行为。</summary>
        private static RealmReward MakeTalentReward(string id, string name, string desc,
            SpiritRootType root, Color color)
        {
            return new RealmReward
            {
                id = id, displayName = name, description = desc,
                category = RealmRewardCategory.SpiritTalent,
                applicableRoot = root, displayColor = color,
                apply = (p) => ApplyPermanentStatusEffect(p, id, name, desc, color, null)
            };
        }

        private static void ApplyPermanentStatusEffect(PlayerController p, string id, string name, string desc, Color color, List<StatModifier> mods)
        {
            var status = p.GetComponent<StatusEffectController>();
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
                modifiers = mods,
                displayName = name,
                description = desc,
                uiColor = color
            });
        }

        /// <summary>
        /// 从奖励池中筛选可见的奖励（按化身过滤），并随机抽取 3 个。
        /// </summary>
        public static List<RealmReward> Roll3(SpiritRootType currentRoot, HashSet<string> alreadyTakenIds = null)
        {
            // 候选池 = 通用 + 当前化身专属
            var pool = new List<RealmReward>();
            foreach (var r in _allRewards)
            {
                if (alreadyTakenIds != null && alreadyTakenIds.Contains(r.id)) continue;
                if (r.applicableRoot == SpiritRootType.None || r.applicableRoot == currentRoot)
                    pool.Add(r);
            }

            // Fisher-Yates 洗牌 + 取前 3
            var result = new List<RealmReward>();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            for (int i = 0; i < Mathf.Min(3, pool.Count); i++)
                result.Add(pool[i]);
            return result;
        }

        public static int Count => _allRewards.Count;
    }
}
