using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 技能数值的"表覆盖层"（v0.5.5 · B 方案：表作数据层）。
    ///
    /// 机制仍在 SkillData / PlayerCombat 代码里；本工具只在**读取数值时**用
    /// Skill_Base_Config 覆盖（按 <see cref="SkillData.configId"/> 对接）。
    /// - 不写回 SO（避免与 UpgradeRoom 的就地升级冲突），按读即查、可热改表。
    /// - 表里没接（configId=0）或查不到 → 回退 SO 自身数值。
    /// </summary>
    public static class SkillTuning
    {
        /// <summary>有效冷却（秒）：表 BaseCooldown 覆盖 SO.cooldown；未接表则用 SO。</summary>
        public static float EffectiveCooldown(SkillData skill)
        {
            if (skill == null) return 0f;
            if (skill.configId > 0)
            {
                var row = ConfigDatabase.Instance.GetSkillBase(skill.configId);
                if (row != null && row.BaseCooldown > 0f)
                    return row.BaseCooldown;
            }
            return skill.cooldown;
        }

        /// <summary>
        /// 有效基础伤害：表 BaseDamageRatio 作为**对 SO 基础伤害的百分比乘区**（10000=100%）。
        /// 即 effective = SO.baseDamage ×(ratio/10000)。这样与 UpgradeRoom 就地升级（抬高 SO.baseDamage）**共存**——
        /// 升级抬基数，表按百分比微调，互不覆盖；不写回 SO。未接表/ratio≤0 → 用 SO 原值。
        /// </summary>
        public static float EffectiveBaseDamage(SkillData skill)
        {
            if (skill == null) return 0f;
            if (skill.configId > 0)
            {
                var row = ConfigDatabase.Instance.GetSkillBase(skill.configId);
                if (row != null && row.BaseDamageRatio > 0)
                    return skill.baseDamage * (row.BaseDamageRatio / 10000f);
            }
            return skill.baseDamage;
        }
    }
}
