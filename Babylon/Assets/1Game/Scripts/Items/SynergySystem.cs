using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物组合（Synergy）系统
    /// 持有特定组合的灵物时触发额外效果
    /// </summary>
    public static class SynergySystem
    {
        /// <summary>Synergy 定义</summary>
        public class SynergyDef
        {
            public string name;
            public string description;
            public ItemCategory[] requiredCategories; // 需要的灵物分类
            public int[] requiredCounts;              // 每个分类需要的数量
            public System.Action<CombatStats> applyEffect;
            public System.Action<CombatStats> removeEffect;
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
            // 风火轮：2个攻伐 + 1个身法 → 攻击力+30%，移速+20%
            _synergies.Add(new SynergyDef
            {
                name = "风火轮",
                description = "攻伐x2 + 身法x1 → 攻击力+30%，移速+20%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Movement },
                requiredCounts = new[] { 2, 1 },
                applyEffect = stats =>
                {
                    stats.attackDamage *= 1.3f;
                    stats.moveSpeed *= 1.2f;
                },
                removeEffect = stats =>
                {
                    stats.attackDamage /= 1.3f;
                    stats.moveSpeed /= 1.2f;
                },
                displayColor = new Color(1f, 0.5f, 0.1f)
            });

            // 金刚不坏：3个护体 → 减伤+25%，最大生命+20%
            _synergies.Add(new SynergyDef
            {
                name = "金刚不坏",
                description = "护体x3 → 减伤+25%，最大生命+20%",
                requiredCategories = new[] { ItemCategory.Defense },
                requiredCounts = new[] { 3 },
                applyEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.25f);
                    stats.maxHp *= 1.2f;
                    stats.currentHp *= 1.2f;
                },
                removeEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction - 0.25f);
                    stats.maxHp /= 1.2f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                },
                displayColor = new Color(1f, 0.85f, 0.2f)
            });

            // 天人合一：5种不同分类各1个 → 全属性+15%
            _synergies.Add(new SynergyDef
            {
                name = "天人合一",
                description = "集齐5种分类 → 全属性+15%",
                requiredCategories = new[] {
                    ItemCategory.Attack, ItemCategory.Defense,
                    ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Pill
                },
                requiredCounts = new[] { 1, 1, 1, 1, 1 },
                applyEffect = stats =>
                {
                    stats.attackDamage *= 1.15f;
                    stats.maxHp *= 1.15f;
                    stats.currentHp *= 1.15f;
                    stats.moveSpeed *= 1.15f;
                    stats.critRate = Mathf.Clamp01(stats.critRate + 0.1f);
                },
                removeEffect = stats =>
                {
                    stats.attackDamage /= 1.15f;
                    stats.maxHp /= 1.15f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                    stats.moveSpeed /= 1.15f;
                    stats.critRate = Mathf.Clamp01(stats.critRate - 0.1f);
                },
                displayColor = new Color(1f, 0.95f, 0.5f)
            });

            // 嗜血狂魔：2个攻伐 + 1个丹药 → 暴击率+15%，击杀回复+5
            _synergies.Add(new SynergyDef
            {
                name = "嗜血狂魔",
                description = "攻伐x2 + 丹药x1 → 暴击率+15%，击杀回复+5",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Pill },
                requiredCounts = new[] { 2, 1 },
                applyEffect = stats =>
                {
                    stats.critRate = Mathf.Clamp01(stats.critRate + 0.15f);
                },
                removeEffect = stats =>
                {
                    stats.critRate = Mathf.Clamp01(stats.critRate - 0.15f);
                },
                displayColor = new Color(0.9f, 0.1f, 0.2f)
            });
        }

        /// <summary>
        /// 检查并更新所有 Synergy 状态
        /// </summary>
        public static void CheckSynergies(IReadOnlyDictionary<ItemData, int> items, CombatStats stats)
        {
            // 统计每个分类的灵物数量
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
                    // 新激活
                    _activeSynergies.Add(synergy.name);
                    synergy.applyEffect?.Invoke(stats);
                    Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(synergy.displayColor)}>✨ Synergy 激活：{synergy.name} — {synergy.description}</color>");

                    GameEvents.Publish(new GameEvents.SynergyActivated
                    {
                        SynergyName = synergy.name,
                        Description = synergy.description
                    });
                }
                else if (!satisfied && wasActive)
                {
                    // 失效
                    _activeSynergies.Remove(synergy.name);
                    synergy.removeEffect?.Invoke(stats);
                    Debug.Log($"<color=gray>Synergy 失效：{synergy.name}</color>");
                }
            }
        }

        /// <summary>获取所有已激活的 Synergy 名称</summary>
        public static IReadOnlyCollection<string> GetActiveSynergies() => _activeSynergies;

        /// <summary>获取所有 Synergy 定义</summary>
        public static IReadOnlyList<SynergyDef> GetAllSynergies() => _synergies;

        /// <summary>清空激活状态（新一局开始时）</summary>
        public static void Clear()
        {
            _activeSynergies.Clear();
        }
    }
}
