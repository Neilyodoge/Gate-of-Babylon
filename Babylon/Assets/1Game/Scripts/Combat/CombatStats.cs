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
        /// <summary>减伤比例 0~1</summary>
        public float damageReduction = 0f;

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

        /// <summary>
        /// 计算最终伤害（考虑暴击）
        /// </summary>
        public float CalculateDamage()
        {
            bool isCrit = Random.value < critRate;
            float damage = attackDamage;
            if (isCrit)
                damage *= critDamage;
            return damage;
        }

        /// <summary>
        /// 召唤物 / 衍生伤害的统一构造（GDD 6.7.2）。
        /// 不再让"焚天/剑阵/御风/元素爆发"等 hardcode 调用 attackDamage*ratio，
        /// 而是经过本方法以继承玩家的暴击 / 加成。
        /// </summary>
        /// <param name="baseRatio">该衍生伤害基于玩家攻击的倍率</param>
        /// <param name="flatBonus">附加的固定伤害（例如焚天的 _fireBurstDamage 基础值）</param>
        /// <param name="inheritCrit">是否参与暴击 roll</param>
        /// <returns>(damage, isCrit)</returns>
        public (float damage, bool isCrit) BuildSummonDamage(float baseRatio, float flatBonus = 0f, bool inheritCrit = true)
        {
            float dmg = flatBonus + attackDamage * baseRatio;
            bool isCrit = false;
            if (inheritCrit && Random.value < critRate)
            {
                dmg *= critDamage;
                isCrit = true;
            }
            return (dmg, isCrit);
        }

        /// <summary>
        /// 受到伤害，返回实际伤害值
        /// </summary>
        public float TakeDamage(float rawDamage)
        {
            float actualDamage = rawDamage * (1f - Mathf.Clamp01(damageReduction));
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
