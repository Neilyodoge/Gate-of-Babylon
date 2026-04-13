using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵物组合（Synergy）系统
    /// 持有特定组合的灵物时触发额外效果
    /// 改造后：提高触发门槛，效果从纯数值变为机制性玩法
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
            // 风火轮：3个攻伐 + 2个身法 → 冲刺时身后留下火墙，持续灼烧经过的敌人
            _synergies.Add(new SynergyDef
            {
                name = "风火轮",
                description = "攻伐x3 + 身法x2 → 冲刺留下火墙，灼烧经过的敌人",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Movement },
                requiredCounts = new[] { 3, 2 },
                applyEffect = stats =>
                {
                    // 机制性效果：冲刺留火墙（由 QualitativeEffectRunner 处理）
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.FireTrailSynergyActive = true;
                    // 附带少量数值加成
                    stats.moveSpeed *= 1.1f;
                },
                removeEffect = stats =>
                {
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.FireTrailSynergyActive = false;
                    stats.moveSpeed /= 1.1f;
                },
                displayColor = new Color(1f, 0.5f, 0.1f)
            });

            // 金刚不坏：5个护体 → 受击时有30%概率完全格挡，并反弹50%伤害
            _synergies.Add(new SynergyDef
            {
                name = "金刚不坏",
                description = "护体x5 → 受击30%概率完全格挡并反弹伤害",
                requiredCategories = new[] { ItemCategory.Defense },
                requiredCounts = new[] { 5 },
                applyEffect = stats =>
                {
                    // 格挡机制由 PlayerController 检查 Synergy 状态实现
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.15f);
                    stats.maxHp *= 1.1f;
                    stats.currentHp *= 1.1f;
                },
                removeEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction - 0.15f);
                    stats.maxHp /= 1.1f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                },
                displayColor = new Color(1f, 0.85f, 0.2f)
            });

            // 天人合一：5种不同分类各2个 → 每30秒随机触发一个元素爆发（火/冰/风/雷）
            _synergies.Add(new SynergyDef
            {
                name = "天人合一",
                description = "5种分类各x2 → 每30秒随机元素爆发（火/冰/风/雷）",
                requiredCategories = new[] {
                    ItemCategory.Attack, ItemCategory.Defense,
                    ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Pill
                },
                requiredCounts = new[] { 2, 2, 2, 2, 2 },
                applyEffect = stats =>
                {
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.ActivateElementBurst();
                    // 附带少量全属性加成
                    stats.attackDamage *= 1.08f;
                    stats.maxHp *= 1.08f;
                    stats.currentHp *= 1.08f;
                    stats.moveSpeed *= 1.08f;
                },
                removeEffect = stats =>
                {
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.DeactivateElementBurst();
                    stats.attackDamage /= 1.08f;
                    stats.maxHp /= 1.08f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                    stats.moveSpeed /= 1.08f;
                },
                displayColor = new Color(1f, 0.95f, 0.5f)
            });

            // 嗜血狂魔：3个攻伐 + 2个丹药 → 击杀后进入嗜血状态5秒，攻速翻倍但持续掉血
            _synergies.Add(new SynergyDef
            {
                name = "嗜血狂魔",
                description = "攻伐x3 + 丹药x2 → 击杀后嗜血5秒：攻速翻倍，持续掉血",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                applyEffect = stats =>
                {
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.BloodlustSynergyActive = true;
                },
                removeEffect = stats =>
                {
                    var runner = QualitativeEffectRunner.Instance;
                    if (runner != null)
                        runner.BloodlustSynergyActive = false;
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

        /// <summary>检查金刚不坏是否激活（用于格挡判定）</summary>
        public static bool IsVajraActive => _activeSynergies.Contains("金刚不坏");

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
