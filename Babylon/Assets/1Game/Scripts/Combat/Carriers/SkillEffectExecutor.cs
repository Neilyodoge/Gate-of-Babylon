using System;

namespace XianTu
{
    /// <summary>
    /// 术法效果编排所需的Unity副作用边界。P1期间由PlayerCombat实现，
    /// 后续具体效果可逐项迁出而不改变载体调用合同。
    /// </summary>
    public interface ISkillEffectHost
    {
        void PublishSkillCastStarted(SkillData skill, int slotIndex);
        bool TryBeginSkillCast(SkillData skill);
        void LogSkillCast(SkillData skill, int chargeLevel);
        void CastArea(SkillData skill, float damageMultiplier, float radiusMultiplier, int slotIndex);
        void CastProjectile(SkillData skill, float damageMultiplier);
        void CastDash(SkillData skill);
        void CastBuff(SkillData skill, int slotIndex);
        void CastZone(SkillData skill, float damageMultiplier);
        void CastHeal(SkillData skill);
        void CastSummon(SkillData skill);
    }

    public sealed class SkillEffectExecutor
    {
        private readonly ISkillEffectHost _host;

        public SkillEffectExecutor(ISkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool Execute(
            SkillData skill,
            int slotIndex,
            int chargeLevel,
            float enhancementDamageMultiplier = 1f)
        {
            if (skill == null)
                return false;

            _host.PublishSkillCastStarted(skill, slotIndex);

            if (skill.skillType == SkillType.Buff)
            {
                _host.LogSkillCast(skill, 1);
                _host.CastBuff(skill, slotIndex);
                return true;
            }

            if (skill.skillType == SkillType.Heal)
            {
                _host.LogSkillCast(skill, 1);
                _host.CastHeal(skill);
                return true;
            }

            if (!_host.TryBeginSkillCast(skill))
                return false;

            float damageMultiplier =
                skill.GetChargeDamageMultiplier(chargeLevel) * enhancementDamageMultiplier;
            float radiusMultiplier = skill.GetChargeRadiusMultiplier(chargeLevel);
            _host.LogSkillCast(skill, chargeLevel);

            switch (skill.skillType)
            {
                case SkillType.AreaDamage:
                    _host.CastArea(skill, damageMultiplier, radiusMultiplier, slotIndex);
                    break;
                case SkillType.Projectile:
                    _host.CastProjectile(skill, damageMultiplier);
                    break;
                case SkillType.Dash:
                    _host.CastDash(skill);
                    break;
                case SkillType.Zone:
                    _host.CastZone(skill, damageMultiplier);
                    break;
                case SkillType.Summon:
                    _host.CastSummon(skill);
                    break;
            }

            return true;
        }
    }
}
