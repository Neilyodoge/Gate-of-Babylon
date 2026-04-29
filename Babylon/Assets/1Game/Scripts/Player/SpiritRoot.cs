using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵根类型 —— GDD 4.2 的 5 个基础灵根。
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
    /// 灵根的纯数据描述（无 MonoBehaviour）。
    /// 真实的"行为驱动"由 <see cref="SpiritRootController"/> 负责。
    /// </summary>
    public class SpiritRootDef
    {
        public SpiritRootType type;
        public string name;
        public string passive;
        public string starterItemHint;
        public Color displayColor;

        /// <summary>开局加在 _baseStats 上的固定修正（永久 BUFF）</summary>
        public List<StatModifier> baseModifiers;

        /// <summary>用于 HUD tooltip 的二段说明</summary>
        public string tooltip;

        /// <summary>起手携带的灵物 itemName（在 ItemInventory 初始化后由控制器查找并加入背包）</summary>
        public string starterItemName;
    }

    /// <summary>
    /// 5 个基础灵根的内置注册表（数据驱动；后续可改为 ScriptableObject）。
    /// </summary>
    public static class SpiritRootRegistry
    {
        private static readonly List<SpiritRootDef> _defs = new();

        static SpiritRootRegistry()
        {
            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Metal,
                name = "金灵根",
                passive = "锐金之体：所有攻击附带穿透 +1，对穿透的后排目标 50% 伤害",
                starterItemHint = "起手携带：锈铁飞剑",
                displayColor = new Color(1f, 0.85f, 0.2f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.PierceCount, 1)
                },
                tooltip = "适合喜欢直觉式强化的玩家。穿透系灵物（飞剑等）会获得额外加成。",
                starterItemName = "锈铁飞剑"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Wood,
                name = "木灵根",
                passive = "生生不息：每清完一个房间回复 8% 生命；最大生命 -20%",
                starterItemHint = "起手携带：聚灵草",
                displayColor = new Color(0.4f, 0.9f, 0.4f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MaxHp, -0.2f)
                },
                tooltip = "鼓励快速清怪，高风险高回报。配合「灵藤草」「血珊瑚」会形成稳定回血循环。",
                starterItemName = "灵藤草"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Water,
                name = "水灵根",
                passive = "上善若水：受到伤害时，伤害的 25% 反弹给攻击者",
                starterItemHint = "起手携带：水盾符",
                displayColor = new Color(0.3f, 0.7f, 1f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Flat(StatType.DamageReduction, 0.05f)
                },
                tooltip = "防御反击型玩法。配合「玉佩」「龙鳞甲」等可触发更强反击。",
                starterItemName = "玉佩"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Fire,
                name = "火灵根",
                passive = "燎原之火：击杀敌人后 4 秒内攻击 +12%，最多 3 层（连杀 BUFF）",
                starterItemHint = "起手携带：火灵珠",
                displayColor = new Color(1f, 0.4f, 0.1f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.AttackDamage, 0.05f) // 基础 +5% atk 作为开局加成
                },
                tooltip = "鼓励连续击杀，雪球流。配合元素反应（火+冰=蒸汽爆炸）极强。",
                starterItemName = "火灵珠"
            });

            _defs.Add(new SpiritRootDef
            {
                type = SpiritRootType.Earth,
                name = "土灵根",
                passive = "厚德载物：每持有 5 件灵物，获得一层「地脉护盾」（吸收一次伤害）",
                starterItemHint = "起手携带：岩甲符",
                displayColor = new Color(0.85f, 0.7f, 0.4f),
                baseModifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatType.MaxHp, 0.1f)
                },
                tooltip = "鼓励大量收集道具的「囤」流。配合「混沌珠」（每 5 件 +5% 全属性）形成滚雪球。",
                starterItemName = "龙鳞甲"
            });
        }

        public static IReadOnlyList<SpiritRootDef> All => _defs;

        public static SpiritRootDef Get(SpiritRootType t)
        {
            foreach (var d in _defs)
                if (d.type == t) return d;
            return null;
        }
    }
}
