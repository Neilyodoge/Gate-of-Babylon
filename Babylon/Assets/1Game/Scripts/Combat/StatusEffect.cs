using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 单个属性修正（作用到 CombatStats 上）。
    /// 与灵物原生词条平行，但通过 StatusEffectController 累加进玩家 _playerStats。
    /// </summary>
    [System.Serializable]
    public struct StatModifier
    {
        public StatType type;
        /// <summary>是百分比（true）还是平加值（false）</summary>
        public bool isPercent;
        public float value;

        public static StatModifier Flat(StatType t, float v) => new StatModifier { type = t, isPercent = false, value = v };
        public static StatModifier Percent(StatType t, float v) => new StatModifier { type = t, isPercent = true, value = v };
    }

    /// <summary>
    /// 受 StatModifier 影响的属性枚举，参考 CombatStats 字段。
    /// </summary>
    public enum StatType
    {
        AttackDamage,
        AttackSpeed,
        MaxHp,
        MoveSpeed,
        DamageReduction,
        CritRate,
        CritDamage,
        PierceCount,
        ProjectileSpeed,

        // === GDD §13 新增（v0.7 数值公式） ===
        Defense,              // 防御力（平坦值，敌人专用）
        LegacyCoeff,          // 【墓碑】原化身系数乘区，已废弃 — 保序占位，勿删/勿移动
        DamageBonusPercent,   // 增伤百分比（通用乘区）
        ArmorPenPercent,      // 减防百分比（穿甲）
        SkillDamagePercent    // 技能伤害加成（技能专用乘区）
    }

    /// <summary>
    /// StatusEffect —— BUFF / DEBUFF 通用容器。
    /// 设计要点：
    /// 1. 可叠加层数；可刷新持续时间
    /// 2. 携带 elementTag 用于元素反应
    /// 3. 携带 modifiers 用于属性聚合（与协同 / 质变 / 灵物并列）
    /// 4. 通过 onTick / onExpire 钩子做周期掉血、解除时清理 VFX 等
    /// </summary>
    public class StatusEffect
    {
        public string id;
        public bool isBuff;
        public ElementTag elementTag;
        public int stacks;
        public int maxStacks;
        public float duration;          // 剩余秒数；< 0 表示常驻
        public float defaultDuration;   // 用于刷新
        public float tickInterval;      // 0 表示无周期
        public List<StatModifier> modifiers;
        public string displayName;
        public string description;
        public Color uiColor;

        // 钩子
        public System.Action<StatusEffect, GameObject> onApply;
        public System.Action<StatusEffect, GameObject, float> onTick;
        public System.Action<StatusEffect, GameObject> onExpire;

        // 运行时
        public float tickTimer;
        public object source;

        public bool IsExpired => duration >= 0f && duration <= 0f;
        public bool IsPermanent => duration < 0f;

        public StatusEffect Clone()
        {
            var s = (StatusEffect)MemberwiseClone();
            s.modifiers = modifiers == null ? null : new List<StatModifier>(modifiers);
            s.tickTimer = 0f;
            return s;
        }
    }
}
