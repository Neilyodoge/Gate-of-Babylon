using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 化身类型 —— GDD 4.2 的 5 个基础化身。
    /// </summary>
    public enum SpiritRootType
    {
        None = 0,
        Metal,   // 金 —— 锐金之体（穿透）
        Wood,    // 木 —— 生生不息（清房回血，最大血 -20%）
        Water,   // 水 —— 上善若水（受击反伤）
        Fire,    // 火 —— 燎原之火（连杀叠攻）
        Earth    // 土 —— 厚德载物（每 5 件灵物给 1 层护盾）
    }

    /// <summary>
    /// 化身的纯数据描述（无 MonoBehaviour）。
    /// 真实的"行为驱动"由 <see cref="SpiritRootController"/> 负责。
    /// v0.3.2 起，每个化身分为「副词条层（数值/被动）」+「核心机制层（机制版）」+「天赋树」三层。
    /// </summary>
    public class SpiritRootDef
    {
        public SpiritRootType type;
        public string name;
        public string passive;
        public string mechanicTitle;   // v0.3.2 新增：核心机制名（如"完美收刀 / 持续寄生 / 影息斩 ..."）
        public string starterItemHint;
        public Color displayColor;

        /// <summary>开局加在 _baseStats 上的固定修正（永久 BUFF）</summary>
        public List<StatModifier> baseModifiers;

        /// <summary>用于 HUD tooltip 的二段说明</summary>
        public string tooltip;

        /// <summary>起手携带的灵物 itemName（在 ItemInventory 初始化后由控制器查找并加入背包）</summary>
        public string starterItemName;

        /// <summary>v0.3.2 核心机制是否已落地（false = 仅副词条层在生效）</summary>
        public bool mechanicEnabled;

        /// <summary>卡片右上角角色定位标签（如"近战 · 御金"）</summary>
        public string roleTag;
    }

    /// <summary>
    /// 5 个基础化身的内置注册表（数据驱动；后续可改为 ScriptableObject）。
    /// </summary>
    public static class SpiritRootRegistry
    {
        private static readonly List<SpiritRootDef> _defs = new();

        static SpiritRootRegistry()
        {
            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Metal,
                name = "剑魄",
                mechanicTitle = "御金 · 飞剑环绕 / 完美收刀",
                passive = "副词条：所有攻击附带穿透 +1。御金底子：常驻 3 把自律飞剑环绕，周期突刺最近的敌人。核心机制：普攻 / 技能 / 闪避后开「灵压窗口」，窗口内按普攻触发灵压爆发。",
                starterItemHint = "起手携带：锈铁飞剑",
                displayColor = new Color(1f, 0.85f, 0.2f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.PierceCount, 1),
                    StatModifier.Flat(StatType.AvatarCoefficient, 0.10f)
                },
                tooltip = "选剑魄 = 选「御金时机流」。飞剑自律补刀打底，窗口内每次完美收刀 ×1.5 爆发，3 次连续完美进入剑心通明。",
                starterItemName = "锈铁飞剑",
                mechanicEnabled = true,   // v0.3.3 已落地
                roleTag = "近战 · 御金"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Wood,
                name = "青囊",
                mechanicTitle = "持续寄生 / 播种收割",
                passive = "副词条：每清完一个房间回复 3% 生命（v0.3 版 8% → 减半，去掉 -20% 最大生命惩罚）。核心机制：普攻种【寄生种子】，技能引爆所有种子 ×0.5/颗 AOE。",
                starterItemHint = "起手携带：聚灵草",
                displayColor = new Color(0.4f, 0.9f, 0.4f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.AvatarCoefficient, 0.05f)
                },
                tooltip = "选青囊 = 选「普攻 ↔ 技能强耦合循环」。普攻铺 5 颗种子 → 技能一波收割 → 普攻继续铺。",
                starterItemName = "灵藤草",
                mechanicEnabled = true,   // v0.3.3 已落地
                roleTag = "续航 · 御木"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Water,
                name = "影刃",
                mechanicTitle = "影息斩 / 位移即输出",
                passive = "副词条：受到伤害时，10% 反弹给攻击者（v0.3 版 25% → 减半）。核心机制：闪避后 0.4s 内攻击触发影息斩 ×2 + 前冲 + 水痕印，技能命中带水痕 ×1.5。",
                starterItemHint = "起手携带：玉佩",
                displayColor = new Color(0.3f, 0.7f, 1f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.DamageReduction, 0.03f),
                    StatModifier.Flat(StatType.AvatarCoefficient, 0.08f)
                },
                tooltip = "选影刃 = 选「闪避变输出」。战斗循环：闪避→影息斩标记→技能爆破→再闪避换位。",
                starterItemName = "玉佩",
                mechanicEnabled = true,   // v0.4 已落地
                roleTag = "机动 · 御水"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Fire,
                name = "业火",
                mechanicTitle = "魔焰献祭 · 越残越猛",
                passive = "副词条：击杀敌人后 4 秒内攻击 +7% × 3 层。核心机制：残血增伤（生命越低、伤害越高）；按 V 入狂火换攻速移速 + 普攻 AOE，狂火期间持续燃血、并积攒心魔。",
                starterItemHint = "起手携带：火灵珠",
                displayColor = new Color(1f, 0.4f, 0.1f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, 0.03f),
                    StatModifier.Flat(StatType.AvatarCoefficient, 0.12f)
                },
                tooltip = "选业火 = 选「燃血豪赌」。越残血越猛，主动入狂火爆发但燃血涨心魔——高风险高回报。",
                starterItemName = "火灵珠",
                mechanicEnabled = true,   // v0.4 已落地
                roleTag = "爆发 · 御火"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Earth,
                name = "御物",
                mechanicTitle = "召物斗法 · 自律土傀",
                passive = "副词条：每持有 5 件灵物，获得一层「地脉护盾」（吸收一次伤害）。核心机制：附近有敌时常驻最多 2 个自律土傀替你作战；大招「兵阵合一」可一次召出整片傀儡阵。",
                starterItemHint = "起手携带：龙鳞甲",
                displayColor = new Color(0.85f, 0.7f, 0.4f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MaxHp, 0.05f),
                    StatModifier.Flat(StatType.AvatarCoefficient, 0.03f)
                },
                tooltip = "选御物 = 选「召物成军」。自律土傀打底，兵阵合一成阵爆发——指挥傀儡群压制战场。",
                starterItemName = "龙鳞甲",
                mechanicEnabled = true,   // v0.6 召物重心已落地
                roleTag = "召唤 · 御土"
            });
        }

        public static IReadOnlyList<SpiritRootDef> All { get { EnsureConfigOverrides(); return _defs; } }

        public static SpiritRootDef Get(SpiritRootType t)
        {
            EnsureConfigOverrides();
            foreach (var d in _defs)
                if (d.type == t) return d;
            return null;
        }

        // ── v0.5.5：表作数据层（B 方案）——
        // 化身的「机制」仍在代码，但显示名可由 Avatar_Base_Config 覆盖（表 ID = (int)SpiritRootType）。
        // 表里没有/没填 → 回退代码默认值。首次访问时惰性应用一次。
        private static bool _overridesApplied;
        private static void EnsureConfigOverrides()
        {
            if (_overridesApplied) return;
            _overridesApplied = true;   // 即使失败也只尝试一次，避免每帧 IO

            try
            {
                var db = XianTu.LevelDesign.ConfigDatabase.Instance;
                foreach (var d in _defs)
                {
                    var row = db.GetAvatar((int)d.type);
                    if (row != null && !string.IsNullOrWhiteSpace(row.Name_CN))
                        d.name = row.Name_CN;   // 仅覆盖显示名；passive/tooltip/机制保持代码（表 Desc 太短不宜覆盖富文本）
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpiritRootRegistry] 应用 Avatar_Base_Config 覆盖失败（回退默认）：{ex.Message}");
            }
        }
    }
}
