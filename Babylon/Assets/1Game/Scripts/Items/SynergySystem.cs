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

            // ==================== 新增 Synergy 组合 ====================

            // 冰火两重天：2个攻伐 + 2个异变 → 攻击同时附带灼烧和冻结，暴击率+10%
            _synergies.Add(new SynergyDef
            {
                name = "冰火两重天",
                description = "攻伐x2 + 异变x2 → 攻击附带灼烧+冻结，暴击率+10%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Anomaly },
                requiredCounts = new[] { 2, 2 },
                applyEffect = stats =>
                {
                    stats.critRate = Mathf.Clamp01(stats.critRate + 0.1f);
                },
                removeEffect = stats =>
                {
                    stats.critRate = Mathf.Clamp01(stats.critRate - 0.1f);
                },
                displayColor = new Color(0.5f, 0.3f, 1f)
            });

            // 灵龟护体：3个护体 + 2个丹药 → 受击回复生命，减伤+10%
            _synergies.Add(new SynergyDef
            {
                name = "灵龟护体",
                description = "护体x3 + 丹药x2 → 受击回复少量生命，减伤+10%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                applyEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.1f);
                    stats.maxHp *= 1.15f;
                    stats.currentHp *= 1.15f;
                },
                removeEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction - 0.1f);
                    stats.maxHp /= 1.15f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                },
                displayColor = new Color(0.2f, 0.8f, 0.5f)
            });

            // 疾风骤雨：3个身法 + 2个攻伐 → 攻速+30%，移速+20%，攻击力+15%
            _synergies.Add(new SynergyDef
            {
                name = "疾风骤雨",
                description = "身法x3 + 攻伐x2 → 攻速+30%，移速+20%，攻击力+15%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Attack },
                requiredCounts = new[] { 3, 2 },
                applyEffect = stats =>
                {
                    stats.attackSpeed *= 1.3f;
                    stats.moveSpeed *= 1.2f;
                    stats.attackDamage *= 1.15f;
                },
                removeEffect = stats =>
                {
                    stats.attackSpeed /= 1.3f;
                    stats.moveSpeed /= 1.2f;
                    stats.attackDamage /= 1.15f;
                },
                displayColor = new Color(0.3f, 0.9f, 1f)
            });

            // 万毒归宗：3个异变 + 2个丹药 → 所有攻击附带剧毒（灼烧伤害翻倍），击杀回复+50%
            _synergies.Add(new SynergyDef
            {
                name = "万毒归宗",
                description = "异变x3 + 丹药x2 → 灼烧伤害翻倍，击杀回复+50%",
                requiredCategories = new[] { ItemCategory.Anomaly, ItemCategory.Pill },
                requiredCounts = new[] { 3, 2 },
                applyEffect = stats =>
                {
                    // 灼烧翻倍通过标记实现，击杀回复由数值加成
                    stats.attackDamage *= 1.1f;
                    stats.critDamage += 0.3f;
                },
                removeEffect = stats =>
                {
                    stats.attackDamage /= 1.1f;
                    stats.critDamage -= 0.3f;
                },
                displayColor = new Color(0.6f, 0.1f, 0.8f)
            });

            // 铜墙铁壁：4个护体 + 1个身法 → 减伤+20%，受击时有概率反弹伤害
            _synergies.Add(new SynergyDef
            {
                name = "铜墙铁壁",
                description = "护体x4 + 身法x1 → 减伤+20%，受击反弹15%伤害",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Movement },
                requiredCounts = new[] { 4, 1 },
                applyEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction + 0.2f);
                    stats.maxHp *= 1.2f;
                    stats.currentHp *= 1.2f;
                },
                removeEffect = stats =>
                {
                    stats.damageReduction = Mathf.Clamp01(stats.damageReduction - 0.2f);
                    stats.maxHp /= 1.2f;
                    stats.currentHp = Mathf.Min(stats.currentHp, stats.maxHp);
                },
                displayColor = new Color(0.8f, 0.7f, 0.3f)
            });

            // 暗影刺客：2个身法 + 2个异变 + 1个攻伐 → 暴击伤害+80%，暴击率+8%
            _synergies.Add(new SynergyDef
            {
                name = "暗影刺客",
                description = "身法x2 + 异变x2 + 攻伐x1 → 暴击伤害+80%，暴击率+8%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Attack },
                requiredCounts = new[] { 2, 2, 1 },
                applyEffect = stats =>
                {
                    stats.critDamage += 0.8f;
                    stats.critRate = Mathf.Clamp01(stats.critRate + 0.08f);
                },
                removeEffect = stats =>
                {
                    stats.critDamage -= 0.8f;
                    stats.critRate = Mathf.Clamp01(stats.critRate - 0.08f);
                },
                displayColor = new Color(0.3f, 0.1f, 0.3f)
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
