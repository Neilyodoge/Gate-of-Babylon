using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 战斗属性数据，所有战斗实体（玩家/敌人）共用
    /// 灵物效果通过修改这些属性来生效
    /// </summary>
    [System.Serializable]
    public class CombatStats
    {
        [Header("生命")]
        public float maxHp = 100f;
        public float currentHp = 100f;

        [Header("攻击")]
        public float attackDamage = 10f;
        /// <summary>攻击速度倍率（1.0 = 基础速度）</summary>
        public float attackSpeed = 1f;
        /// <summary>暴击率 0~1</summary>
        public float critRate = 0.05f;
        /// <summary>暴击伤害倍率</summary>
        public float critDamage = 1.5f;

        [Header("防御")]
        /// <summary>减伤比例 0~1（旧系统兼容）</summary>
        public float damageReduction = 0f;
        /// <summary>防御力（GDD §13 平坦值，用于新伤害公式）</summary>
        public float defense = 0f;

        [Header("GDD §13 新增乘区")]
        /// <summary>化身系数（per-avatar，如金 0.05 / 木 0.03 等）</summary>
        public float avatarCoefficient = 0f;
        /// <summary>增伤百分比（通用乘区，来自灵物/buff 等）</summary>
        public float damageBonusPercent = 0f;
        /// <summary>减防百分比（穿甲，降低目标防御的有效值）</summary>
        public float armorPenPercent = 0f;
        /// <summary>技能伤害加成（技能专用乘区）</summary>
        public float skillDamagePercent = 0f;

        [Header("移动")]
        public float moveSpeed = 6f;
        /// <summary>闪避冷却时间</summary>
        public float dashCooldown = 1.5f;

        [Header("投射物")]
        public float projectileSpeed = 15f;
        /// <summary>穿透次数（0=不穿透）</summary>
        public int pierceCount = 0;

        /// <summary>是否存活</summary>
        public bool IsAlive => currentHp > 0;

        // ======================== GDD §13 伤害公式 ========================
        //
        // 基础伤害 = (base × (1 + avatarCoeff [+ dmgBonus%]) - targetDef × (1 - armorPen%)) × [skillDmg] × critDmg?
        //
        // 普攻：skillDmg = 1（不参与）
        // 技能：skillDmg = SkillData.baseDamage（或 skillDamagePercent 加成后的值）
        // critDmg 仅暴击时乘入
        // ===================================================================

        /// <summary>
        /// GDD §13 普攻伤害（不含技能乘区）。
        /// targetDefense = 目标 CombatStats.defense。
        /// </summary>
        public (float damage, bool isCrit) CalcMeleeDamage(float targetDefense)
        {
            float raw = attackDamage * (1f + avatarCoefficient + damageBonusPercent)
                      - targetDefense * (1f - Mathf.Clamp01(armorPenPercent));
            raw = Mathf.Max(1f, raw);

            bool crit = Random.value < critRate;
            if (crit) raw *= critDamage;
            return (raw, crit);
        }

        /// <summary>
        /// GDD §13 技能伤害（含技能乘区）。
        /// skillMul 来自 SkillData 的伤害倍率（1.0 = 100%）。
        /// </summary>
        public (float damage, bool isCrit) CalcSkillDamage(float targetDefense, float skillMul)
        {
            float raw = attackDamage * (1f + avatarCoefficient + damageBonusPercent)
                      - targetDefense * (1f - Mathf.Clamp01(armorPenPercent));
            raw = Mathf.Max(1f, raw);
            raw *= Mathf.Max(0.01f, skillMul * (1f + skillDamagePercent));

            bool crit = Random.value < critRate;
            if (crit) raw *= critDamage;
            return (raw, crit);
        }

        /// <summary>
        /// 向后兼容：旧版 CalculateDamage()，不走新防御公式（targetDefense=0）。
        /// 调用方如不传入目标防御，退化为旧行为。
        /// </summary>
        public float CalculateDamage()
        {
            var (dmg, _) = CalcMeleeDamage(0f);
            return dmg;
        }

        /// <summary>
        /// 召唤物 / 衍生伤害（GDD 6.7.2 兼容入口）。
        /// </summary>
        public (float damage, bool isCrit) BuildSummonDamage(float baseRatio, float flatBonus = 0f, bool inheritCrit = true)
        {
            float dmg = flatBonus + attackDamage * baseRatio * (1f + avatarCoefficient + damageBonusPercent);
            bool isCrit = false;
            if (inheritCrit && Random.value < critRate)
            {
                dmg *= critDamage;
                isCrit = true;
            }
            return (dmg, isCrit);
        }

        /// <summary>
        /// GDD §13 受伤：先走 defense 新公式减伤，再叠旧 damageReduction。
        /// attackerArmorPen 由攻击方提供（通常已在 CalcMeleeDamage/CalcSkillDamage 中处理，
        /// 此处 rawDamage 已经是减防后的值；damageReduction 作为额外层保留兼容）。
        /// </summary>
        public float TakeDamage(float rawDamage)
        {
            float actualDamage = rawDamage * (1f - Mathf.Clamp01(damageReduction));
            actualDamage = Mathf.Max(0f, actualDamage);
            currentHp = Mathf.Max(0, currentHp - actualDamage);
            return actualDamage;
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public void Heal(float amount)
        {
            currentHp = Mathf.Min(maxHp, currentHp + amount);
        }

        /// <summary>
        /// 重置为满血
        /// </summary>
        public void ResetHp()
        {
            currentHp = maxHp;
        }

        /// <summary>
        /// 复制属性（用于创建基础属性副本）
        /// </summary>
        public CombatStats Clone()
        {
            return (CombatStats)MemberwiseClone();
        }
    }
}
