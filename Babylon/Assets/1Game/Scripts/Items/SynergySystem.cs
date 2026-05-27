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

            // =====================================================
            // v0.5 Week 8 内容扩充：新增 20 个协同（共 30 个）
            // 数值加成统一在 ApplyActiveSynergyStatModifiers 末尾的新 case 中实现
            // =====================================================

            // ---- 单类高数量（5 个）----
            _synergies.Add(new SynergyDef
            {
                name = "剑势如山", description = "攻伐x6 → 攻击力+30%、暴击伤害+25%",
                requiredCategories = new[] { ItemCategory.Attack }, requiredCounts = new[] { 6 },
                displayColor = new Color(1f, 0.7f, 0.2f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "玄龟铁衣", description = "护体x4 → 减伤+18%、最大生命+15%",
                requiredCategories = new[] { ItemCategory.Defense }, requiredCounts = new[] { 4 },
                displayColor = new Color(0.5f, 0.65f, 0.85f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "风行天下", description = "身法x4 → 移速+25%、攻速+20%",
                requiredCategories = new[] { ItemCategory.Movement }, requiredCounts = new[] { 4 },
                displayColor = new Color(0.5f, 1f, 0.85f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "诡道", description = "异变x4 → 暴击率+18%、暴击伤害+50%",
                requiredCategories = new[] { ItemCategory.Anomaly }, requiredCounts = new[] { 4 },
                displayColor = new Color(0.7f, 0.3f, 0.95f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "丹元归元", description = "丹药x3 → 最大生命+25%、减伤+8%",
                requiredCategories = new[] { ItemCategory.Pill }, requiredCounts = new[] { 3 },
                displayColor = new Color(1f, 0.55f, 0.7f)
            });

            // ---- 双类（10 个）----
            _synergies.Add(new SynergyDef
            {
                name = "山岳镇魂", description = "攻伐x3 + 护体x2 → 攻击+15%、最大生命+20%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Defense },
                requiredCounts = new[] { 3, 2 },
                displayColor = new Color(0.85f, 0.65f, 0.35f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "剑光如电", description = "攻伐x2 + 身法x2 → 攻击+12%、攻速+25%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Movement },
                requiredCounts = new[] { 2, 2 },
                displayColor = new Color(1f, 0.85f, 0.4f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "杀阵噬血", description = "攻伐x3 + 异变x1 → 攻击+18%、暴击率+8%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Anomaly },
                requiredCounts = new[] { 3, 1 },
                displayColor = new Color(0.95f, 0.3f, 0.45f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "御体长生", description = "护体x3 + 丹药x1 → 最大生命+22%、减伤+8%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Pill },
                requiredCounts = new[] { 3, 1 },
                displayColor = new Color(0.55f, 0.9f, 0.7f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "霜雪重身", description = "护体x2 + 异变x2 → 最大生命+15%、减伤+12%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Anomaly },
                requiredCounts = new[] { 2, 2 },
                displayColor = new Color(0.45f, 0.7f, 1f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "御风踏雪", description = "身法x2 + 护体x2 → 移速+18%、减伤+10%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Defense },
                requiredCounts = new[] { 2, 2 },
                displayColor = new Color(0.7f, 0.95f, 1f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "千里追风", description = "身法x3 + 丹药x1 → 移速+18%、攻速+15%、最大生命+8%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Pill },
                requiredCounts = new[] { 3, 1 },
                displayColor = new Color(0.85f, 1f, 0.7f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "邪锋", description = "身法x2 + 异变x3 → 暴击伤害+60%、暴击率+12%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Anomaly },
                requiredCounts = new[] { 2, 3 },
                displayColor = new Color(0.65f, 0.35f, 0.95f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "丹魂凝结", description = "异变x2 + 丹药x2 → 暴击率+10%、攻击+15%",
                requiredCategories = new[] { ItemCategory.Anomaly, ItemCategory.Pill },
                requiredCounts = new[] { 2, 2 },
                displayColor = new Color(0.85f, 0.45f, 0.85f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "铁壁长虹", description = "护体x4 + 异变x1 → 减伤+18%、最大生命+20%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Anomaly },
                requiredCounts = new[] { 4, 1 },
                displayColor = new Color(0.7f, 0.75f, 0.55f)
            });

            // ---- 三类（5 个）----
            _synergies.Add(new SynergyDef
            {
                name = "三才阵", description = "攻伐x2 + 护体x2 + 身法x1 → 全能：攻击+10%、减伤+10%、移速+10%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Defense, ItemCategory.Movement },
                requiredCounts = new[] { 2, 2, 1 },
                displayColor = new Color(1f, 0.95f, 0.55f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "狂剑诀", description = "攻伐x3 + 护体x1 + 异变x1 → 攻击+22%、暴击率+10%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Defense, ItemCategory.Anomaly },
                requiredCounts = new[] { 3, 1, 1 },
                displayColor = new Color(1f, 0.5f, 0.35f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "御灵护身", description = "护体x2 + 身法x2 + 丹药x1 → 减伤+10%、移速+12%、最大生命+12%",
                requiredCategories = new[] { ItemCategory.Defense, ItemCategory.Movement, ItemCategory.Pill },
                requiredCounts = new[] { 2, 2, 1 },
                displayColor = new Color(0.55f, 0.95f, 0.85f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "杀生丹诀", description = "攻伐x2 + 异变x2 + 丹药x1 → 暴击伤害+65%、攻击+10%",
                requiredCategories = new[] { ItemCategory.Attack, ItemCategory.Anomaly, ItemCategory.Pill },
                requiredCounts = new[] { 2, 2, 1 },
                displayColor = new Color(0.95f, 0.35f, 0.55f)
            });
            _synergies.Add(new SynergyDef
            {
                name = "风雷诀", description = "身法x2 + 异变x2 + 丹药x1 → 攻速+22%、暴击率+10%",
                requiredCategories = new[] { ItemCategory.Movement, ItemCategory.Anomaly, ItemCategory.Pill },
                requiredCounts = new[] { 2, 2, 1 },
                displayColor = new Color(0.85f, 0.75f, 1f)
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

                    // ===== v0.5 Week 8 新增 20 个协同 =====

                    // 单类高数量
                    case "剑势如山":
                        st.attackDamage *= 1.30f; st.critDamage += 0.25f; break;
                    case "玄龟铁衣":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.18f);
                        st.maxHp *= 1.15f; break;
                    case "风行天下":
                        st.moveSpeed *= 1.25f; st.attackSpeed *= 1.20f; break;
                    case "诡道":
                        st.critRate = Mathf.Clamp01(st.critRate + 0.18f);
                        st.critDamage += 0.50f; break;
                    case "丹元归元":
                        st.maxHp *= 1.25f;
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.08f); break;

                    // 双类
                    case "山岳镇魂":
                        st.attackDamage *= 1.15f; st.maxHp *= 1.20f; break;
                    case "剑光如电":
                        st.attackDamage *= 1.12f; st.attackSpeed *= 1.25f; break;
                    case "杀阵噬血":
                        st.attackDamage *= 1.18f;
                        st.critRate = Mathf.Clamp01(st.critRate + 0.08f); break;
                    case "御体长生":
                        st.maxHp *= 1.22f;
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.08f); break;
                    case "霜雪重身":
                        st.maxHp *= 1.15f;
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.12f); break;
                    case "御风踏雪":
                        st.moveSpeed *= 1.18f;
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.10f); break;
                    case "千里追风":
                        st.moveSpeed *= 1.18f; st.attackSpeed *= 1.15f; st.maxHp *= 1.08f; break;
                    case "邪锋":
                        st.critDamage += 0.60f;
                        st.critRate = Mathf.Clamp01(st.critRate + 0.12f); break;
                    case "丹魂凝结":
                        st.critRate = Mathf.Clamp01(st.critRate + 0.10f);
                        st.attackDamage *= 1.15f; break;
                    case "铁壁长虹":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.18f);
                        st.maxHp *= 1.20f; break;

                    // 三类
                    case "三才阵":
                        st.attackDamage *= 1.10f;
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.10f);
                        st.moveSpeed *= 1.10f; break;
                    case "狂剑诀":
                        st.attackDamage *= 1.22f;
                        st.critRate = Mathf.Clamp01(st.critRate + 0.10f); break;
                    case "御灵护身":
                        st.damageReduction = Mathf.Clamp01(st.damageReduction + 0.10f);
                        st.moveSpeed *= 1.12f; st.maxHp *= 1.12f; break;
                    case "杀生丹诀":
                        st.critDamage += 0.65f; st.attackDamage *= 1.10f; break;
                    case "风雷诀":
                        st.attackSpeed *= 1.22f;
                        st.critRate = Mathf.Clamp01(st.critRate + 0.10f); break;
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
