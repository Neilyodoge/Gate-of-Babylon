using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物组合（Synergy）系统
    /// 持有特定组合的灵物时触发额外效果
    /// 数值加成统一在 ItemInventory.RecalculateStats 末尾通过 ApplyActiveSynergyStatModifiers 应用，
    /// 避免与背包重算互相覆盖；机制效果仍在本类开关 QualitativeEffectRunner。
    /// </summary>
    public static class SynergySystem
    {
        /// <summary>Synergy 定义（仅用于描述与条件判定；数值由 ApplyActiveSynergyStatModifiers 集中处理）</summary>
        public class SynergyDef
        {
            public string name;
            public string description;
            public ItemCategory[] requiredCategories;
            public int[] requiredCounts;
            public Color displayColor;
        }

        private static readonly List<SynergyDef> _synergies = new();
        private static readonly HashSet<string> _activeSynergies = new();

        static SynergySystem()
        {
            RegisterDefaultSynergies();
        }

        private static void RegisterDefaultSynergies()
        {
            _synergies.Add(new SynergyDef
            {
                name = "风火轮",
                description = "攻伐x3 + 身法x2 → 冲刺留下火墙，灼烧经过的敌人",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Movement },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(1f, 0.5f, 0.1f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "金刚不坏",
                description = "护体x5 → 受击30%概率完全格挡并反弹伤害",
                requiredCategories = new[] { ItemCategory.Defense },
                requiredCounts = new[] { 5 },
                displayColor = new Color(1f, 0.85f, 0.2f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "天人合一",
                description = "5种分类各x2 → 每30秒随机元素爆发（火/冰/风/雷）",
                requiredCategories = new[] {
                    ItemCategory.Attack, ItemCategory.Defense,
                    ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Pill
                },
                requiredCounts = new[] { 2, 2, 2, 2, 2 },
                displayColor = new Color(1f, 0.95f, 0.5f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "嗜血狂魔",
                description = "攻伐x3 + 丹药x2 → 击杀后嗜血5秒：攻速翻倍，持续掉血",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(0.9f, 0.1f, 0.2f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "冰火两重天",
                description = "攻伐x2 + 异变x2 → 攻击附带灼烧+冻结，暴击率+10%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Anomaly },
                requiredCounts = new[] { 2, 2 },
                displayColor = new Color(0.5f, 0.3f, 1f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "灵龟护体",
                description = "护体x3 + 丹药x2 → 受击回复少量生命，减伤+10%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(0.2f, 0.8f, 0.5f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "疾风骤雨",
                description = "身法x3 + 攻伐x2 → 攻速+30%，移速+20%，攻击力+15%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Attack },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(0.3f, 0.9f, 1f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "万毒归宗",
                description = "异变x3 + 丹药x2 → 灼烧伤害翻倍，击杀回复+50%",
                requiredCategories = new[] { ItemCategory.Anomaly, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(0.6f, 0.1f, 0.8f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "铜墙铁壁",
                description = "护体x4 + 身法x1 → 减伤+20%，受击反弹15%伤害",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Movement },
                requiredCounts = new[] { 4, 1 },
                displayColor = new Color(0.8f, 0.7f, 0.3f)
            });

            _synergies.Add(new SynergyDef
            {
                name = "暗影刺客",
                description = "身法x2 + 异变x2 + 攻伐x1 → 暴击伤害+80%，暴击率+8%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Attack },
                requiredCounts = new[] { 2, 2, 1 },
                displayColor = new Color(0.3f, 0.1f, 0.3f)
            });
        }

        /// <summary>
        /// 将当前激活的 Synergy 数值加成应用到 stats（应在背包聚合之后调用）。
        /// </summary>
        public static void ApplyActiveSynergyStatModifiers(CombatStats st)
        {
            foreach (var name in _activeSynergies.OrderBy(x => x))
            {
                switch (name)
                {
                    case "风火轮":
                        st.moveSpeed *= 1.1f;
                        break;
                    case "金刚不坏":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.15f);
                        st.maxHp *= 1.1f;
                        break;
                    case "天人合一":
                        st.attackDamage *= 1.08f;
                        st.maxHp *= 1.08f;
                        st.moveSpeed *= 1.08f;
                        break;
                    case "嗜血狂魔":
                        break;
                    case "冰火两重天":
                        st.critRate = Mathf.Clamp01(st.critRate + 0.1f);
                        break;
                    case "灵龟护体":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.1f);
                        st.maxHp *= 1.15f;
                        break;
                    case "疾风骤雨":
                        st.attackSpeed *= 1.3f;
                        st.moveSpeed *= 1.2f;
                        st.attackDamage *= 1.15f;
                        break;
                    case "万毒归宗":
                        st.attackDamage *= 1.1f;
                        st.critDamage += 0.3f;
                        break;
                    case "铜墙铁壁":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.2f);
                        st.maxHp *= 1.2f;
                        break;
                    case "暗影刺客":
                        st.critDamage += 0.8f;
                        st.critRate = Mathf.Clamp01(st.critRate + 0.08f);
                        break;
                }
            }
        }

        /// <summary>
        /// 检查并更新所有 Synergy 状态（不写 CombatStats，仅开关机制 + 维护激活集合）。
        /// </summary>
        public static void CheckSynergies(IReadOnlyDictionary<ItemData, int> items)
        {
            var categoryCounts = new Dictionary<ItemCategory, int>();
            foreach (var kvp in items)
            {
                var cat = kvp.Key.category;
                if (categoryCounts.ContainsKey(cat))
                    categoryCounts[cat] += kvp.Value;
                else
                    categoryCounts[cat] = kvp.Value;
            }

            foreach (var synergy in _synergies)
            {
                bool satisfied = true;
                for (int i = 0; i < synergy.requiredCategories.Length; i++)
                {
                    int required = synergy.requiredCounts[i];
                    int have = categoryCounts.TryGetValue(synergy.requiredCategories[i], out int c) ? c : 0;
                    if (have < required)
                    {
                        satisfied = false;
                        break;
                    }
                }

                bool wasActive = _activeSynergies.Contains(synergy.name);

                if (satisfied && !wasActive)
                {
                    _activeSynergies.Add(synergy.name);
                    ActivateSynergyMechanism(synergy.name);
                    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(synergy.displayColor)}>✨ Synergy 激活：{synergy.name} — {synergy.description}</color>");

                    GameEvents.Publish(new GameEvents.SynergyActivated
                    {
                        SynergyName = synergy.name,
                        Description = synergy.description
                    });
                }
                else if (!satisfied && wasActive)
                {
                    _activeSynergies.Remove(synergy.name);
                    DeactivateSynergyMechanism(synergy.name);
                    Debug.Log($"<color=gray>Synergy 失效：{synergy.name}</color>");
                }
            }
        }

        private static void ActivateSynergyMechanism(string name)
        {
            var runner = QualitativeEffectRunner.Instance;
            switch (name)
            {
                case "风火轮":
                    if (runner != null) runner.FireTrailSynergyActive = true;
                    break;
                case "天人合一":
                    runner?.ActivateElementBurst();
                    break;
                case "嗜血狂魔":
                    if (runner != null) runner.BloodlustSynergyActive = true;
                    break;
            }
        }

        private static void DeactivateSynergyMechanism(string name)
        {
            var runner = QualitativeEffectRunner.Instance;
            switch (name)
            {
                case "风火轮":
                    if (runner != null) runner.FireTrailSynergyActive = false;
                    break;
                case "天人合一":
                    runner?.DeactivateElementBurst();
                    break;
                case "嗜血狂魔":
                    if (runner != null) runner.BloodlustSynergyActive = false;
                    break;
            }
        }

        public static bool IsVajraActive => _activeSynergies.Contains("金刚不坏");

        public static IReadOnlyCollection<string> GetActiveSynergies() => _activeSynergies;

        public static IReadOnlyList<SynergyDef> GetAllSynergies() => _synergies;

        public static void Clear()
        {
            var runner = QualitativeEffectRunner.Instance;
            if (runner != null)
            {
                runner.FireTrailSynergyActive = false;
                runner.BloodlustSynergyActive = false;
                runner.DeactivateElementBurst();
            }
            _activeSynergies.Clear();
        }
    }
}
