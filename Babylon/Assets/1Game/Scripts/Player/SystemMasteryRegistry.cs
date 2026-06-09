using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>御灵五系（设定_御灵五系 §5）。灵 → 御灵系 + 生灵/空间/神识/召物。</summary>
    public enum SystemKind { Yuling, Life, Space, Mind, Summon }

    /// <summary>
    /// 系精通节点（化身×系 · 局外成长，二值解锁）。apply 在入秘境时挂常驻 StatusEffect buff。
    /// </summary>
    public class MasteryNode
    {
        public string id;
        public SpiritRootType avatar;
        public SystemKind system;
        public string displayName;
        public string description;
        public int cost;                 // 灵力
        public string prereqId;          // 前置节点（null=无）
        public int affinityStarReq;      // 需要的亲和谱★（铺垫1/关键2/质变3）
        public int tier;                 // 0=根基 / 1=分支 / 2=质变（用于 UI 缩进）
        public string branchLabel;       // 分支主题（""=根基）
        public Action<PlayerController> apply;
    }

    /// <summary>
    /// 系精通注册表（阶段C MVP）：5 化身亲和谱 + 各化身"本命系"3 节点链（铺垫→关键→质变）。
    /// 节点效果先以通用属性 buff 落地（复用永久天赋的 StatusEffect 模式）；
    /// 文档"局内叠系 tag 定阈值/合技"语义待"御灵之路 tag 系统"实现后升级。
    /// </summary>
    public static class SystemMasteryRegistry
    {
        public static readonly SystemKind[] AllSystems =
            { SystemKind.Yuling, SystemKind.Life, SystemKind.Space, SystemKind.Mind, SystemKind.Summon };

        public static string SystemName(SystemKind s) => s switch
        {
            SystemKind.Yuling => "御灵",
            SystemKind.Life => "生灵",
            SystemKind.Space => "空间",
            SystemKind.Mind => "神识",
            SystemKind.Summon => "召物",
            _ => "?"
        };

        // ── 亲和谱（每化身对各系 ★ 上限 0~4）——设定_御灵五系 §4 ──
        // 顺序：御灵 / 生灵 / 空间 / 神识 / 召物
        private static readonly Dictionary<SpiritRootType, int[]> _affinity = new()
        {
            { SpiritRootType.Fire,  new[] { 4, 3, 1, 0, 2 } }, // 业火 · 本命御灵(火)
            { SpiritRootType.Metal, new[] { 4, 1, 2, 2, 3 } }, // 剑魄 · 本命御灵(金)
            { SpiritRootType.Wood,  new[] { 2, 4, 2, 1, 3 } }, // 青囊 · 本命生灵
            { SpiritRootType.Water, new[] { 2, 1, 4, 3, 2 } }, // 影刃 · 本命空间
            { SpiritRootType.Earth, new[] { 2, 2, 1, 0, 4 } }, // 御物 · 本命召物
        };

        /// <summary>取化身对某系的亲和★上限（0~4）。</summary>
        public static int AffinityStars(SpiritRootType avatar, SystemKind system)
        {
            if (_affinity.TryGetValue(avatar, out var arr))
            {
                int i = (int)system;
                if (i >= 0 && i < arr.Length) return arr[i];
            }
            return 0;
        }

        /// <summary>化身的本命系（亲和★ 最高者）。</summary>
        public static SystemKind BodySystem(SpiritRootType avatar)
        {
            int best = -1; SystemKind bestSys = SystemKind.Yuling;
            foreach (var s in AllSystems)
            {
                int v = AffinityStars(avatar, s);
                if (v > best) { best = v; bestSys = s; }
            }
            return bestSys;
        }

        // ── 节点表 ──
        private static List<MasteryNode> _all;
        public static IReadOnlyList<MasteryNode> AllNodes { get { EnsureBuilt(); return _all; } }

        public static MasteryNode Get(string id)
        {
            EnsureBuilt();
            foreach (var n in _all) if (n.id == id) return n;
            return null;
        }

        /// <summary>某化身可见的系精通节点（MVP：本命系 3 节点链）。</summary>
        public static IEnumerable<MasteryNode> NodesFor(SpiritRootType avatar)
        {
            EnsureBuilt();
            foreach (var n in _all) if (n.avatar == avatar) yield return n;
        }

        // 价格（灵力）：根基 40 / 分支 60 / 质变 110
        private const int CostRoot = 40, CostBranch = 60, CostApex = 110;

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _all = new List<MasteryNode>();

            // ── 业火 · 御灵(火)：根基 → 焚势(输出) / 燎原(持续) ──
            Root(SpiritRootType.Fire, SystemKind.Yuling, "fire",
                "引焰", "攻击 +8%", P(StatType.AttackDamage, 0.08f));
            Branch("fire", "焚势·爆发",
                "炽刃", "攻击 +12%", new[] { P(StatType.AttackDamage, 0.12f) },
                "焚天之势", "暴击伤害 +30%", new[] { F(StatType.CritDamage, 0.30f) });
            Branch("fire", "燎原·灼烧",
                "炎息", "攻速 +15%", new[] { P(StatType.AttackSpeed, 0.15f) },
                "业火燎原", "攻击 +15% · 暴击率 +8%", new[] { P(StatType.AttackDamage, 0.15f), F(StatType.CritRate, 0.08f) });

            // ── 剑魄 · 御灵(金)：根基 → 锐金(穿透) / 御金(铁壁) ──
            Root(SpiritRootType.Metal, SystemKind.Yuling, "metal",
                "淬锋", "攻击 +8%", P(StatType.AttackDamage, 0.08f));
            Branch("metal", "锐金·破阵",
                "破甲", "穿透 +1", new[] { F(StatType.PierceCount, 1) },
                "剑心圆满", "暴击率 +12% · 暴击伤害 +20%", new[] { F(StatType.CritRate, 0.12f), F(StatType.CritDamage, 0.20f) });
            Branch("metal", "御金·铁壁",
                "金身", "减伤 +8%", new[] { F(StatType.DamageReduction, 0.08f) },
                "金刚不坏", "最大生命 +15% · 减伤 +5%", new[] { P(StatType.MaxHp, 0.15f), F(StatType.DamageReduction, 0.05f) });

            // ── 青囊 · 生灵：根基 → 藤蔓(攻势) / 养元(续航) ──
            Root(SpiritRootType.Wood, SystemKind.Life, "wood",
                "生养", "最大生命 +10%", P(StatType.MaxHp, 0.10f));
            Branch("wood", "藤蔓·攻势",
                "春生", "攻击 +10%", new[] { P(StatType.AttackDamage, 0.10f) },
                "枯荣", "攻击 +12% · 攻速 +10%", new[] { P(StatType.AttackDamage, 0.12f), P(StatType.AttackSpeed, 0.10f) });
            Branch("wood", "养元·厚生",
                "厚土", "最大生命 +12%", new[] { P(StatType.MaxHp, 0.12f) },
                "生生不息", "最大生命 +10% · 减伤 +10%", new[] { P(StatType.MaxHp, 0.10f), F(StatType.DamageReduction, 0.10f) });

            // ── 影刃 · 空间：根基 → 遁影(疾袭) / 虚空(缥缈) ──
            Root(SpiritRootType.Water, SystemKind.Space, "water",
                "身法", "移速 +12%", P(StatType.MoveSpeed, 0.12f));
            Branch("water", "遁影·疾袭",
                "疾风", "攻速 +12%", new[] { P(StatType.AttackSpeed, 0.12f) },
                "瞬影", "攻击 +15% · 暴击率 +10%", new[] { P(StatType.AttackDamage, 0.15f), F(StatType.CritRate, 0.10f) });
            Branch("water", "虚空·缥缈",
                "缥缈", "移速 +10% · 减伤 +6%", new[] { P(StatType.MoveSpeed, 0.10f), F(StatType.DamageReduction, 0.06f) },
                "空蝉", "暴击率 +12% · 暴击伤害 +25%", new[] { F(StatType.CritRate, 0.12f), F(StatType.CritDamage, 0.25f) });

            // ── 御物 · 召物：根基 → 造傀(成军) / 坐镇(镇岳) ──
            Root(SpiritRootType.Earth, SystemKind.Summon, "earth",
                "固元", "最大生命 +10%", P(StatType.MaxHp, 0.10f));
            Branch("earth", "造傀·成军",
                "精铁傀儡", "攻击 +10%", new[] { P(StatType.AttackDamage, 0.10f) },
                "成军", "攻击 +15% · 攻速 +10%", new[] { P(StatType.AttackDamage, 0.15f), P(StatType.AttackSpeed, 0.10f) });
            Branch("earth", "坐镇·镇岳",
                "磐石", "减伤 +10%", new[] { F(StatType.DamageReduction, 0.10f) },
                "金身镇岳", "最大生命 +15% · 减伤 +5%", new[] { P(StatType.MaxHp, 0.15f), F(StatType.DamageReduction, 0.05f) });
        }

        private static StatModifier P(StatType t, float v) => StatModifier.Percent(t, v);
        private static StatModifier F(StatType t, float v) => StatModifier.Flat(t, v);

        // 记住每个 prefix 当前根 id（供 Branch 接前置）
        private static readonly Dictionary<string, (SpiritRootType avatar, SystemKind sys, string rootId)> _roots = new();

        /// <summary>建根基节点（tier0，affReq1，无前置）。</summary>
        private static void Root(SpiritRootType avatar, SystemKind system, string prefix,
            string name, string desc, StatModifier mod)
        {
            string id = $"{prefix}_root";
            _all.Add(MakeNode(id, avatar, system, name, desc, CostRoot, null, 1, 0, "根基", mod));
            _roots[prefix] = (avatar, system, id);
        }

        /// <summary>在 prefix 根基上接一条分支：分支节点(tier1,affReq2) → 质变节点(tier2,affReq3)。</summary>
        private static void Branch(string prefix, string branchLabel,
            string n1, string d1, StatModifier[] m1,
            string n2, string d2, StatModifier[] m2)
        {
            var (avatar, sys, rootId) = _roots[prefix];
            int bi = 0;
            foreach (var n in _all) if (n.avatar == avatar && n.tier == 1) bi++; // 该化身已存在的分支节点计数 → 唯一 id
            string id1 = $"{prefix}_b{bi}_1", id2 = $"{prefix}_b{bi}_2";
            _all.Add(MakeNode(id1, avatar, sys, n1, d1, CostBranch, rootId, 2, 1, branchLabel, m1));
            _all.Add(MakeNode(id2, avatar, sys, n2, d2, CostApex, id1, 3, 2, branchLabel, m2));
        }

        private static MasteryNode MakeNode(string id, SpiritRootType avatar, SystemKind system,
            string name, string desc, int cost, string prereq, int affReq, int tier, string branchLabel,
            params StatModifier[] mods)
        {
            var node = new MasteryNode
            {
                id = id, avatar = avatar, system = system,
                displayName = name, description = desc,
                cost = cost, prereqId = prereq, affinityStarReq = affReq,
                tier = tier, branchLabel = branchLabel
            };
            var modList = new List<StatModifier>(mods);
            node.apply = player =>
            {
                if (player == null) return;
                var status = player.GetComponent<StatusEffectController>();
                if (status == null) return;
                status.Apply(new StatusEffect
                {
                    id = "Mastery_" + id,
                    isBuff = true,
                    elementTag = ElementTag.None,
                    stacks = 1,
                    maxStacks = 1,
                    defaultDuration = -1f,
                    duration = -1f,
                    modifiers = modList,
                    displayName = "精通·" + name,
                    description = desc,
                    uiColor = new Color(0.7f, 0.85f, 1f)
                });
            };
            return node;
        }
    }
}
