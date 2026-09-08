using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public enum ImmediateSkillVisual
    {
        Buff,
        Heal
    }

    /// <summary>Buff与治疗术法需要的Unity副作用边界。</summary>
    public interface IImmediateSkillEffectHost
    {
        float AttackDamage { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }
        void SetCurrentHealth(float value);
        void RecordHealing(float requestedHeal, float actualHeal);
        void ArmLethalGuard(float duration);
        void ActivateHeavenEarthShift(float duration);
        void ApplyBuffStatus(StatusEffect effect);
        void ApplyBuffSlotModifiers(SkillData skill, int slotIndex);
        void PlayImmediateVisual(SkillData skill, ImmediateSkillVisual visual, Color fallbackColor);
        void PublishHealthChanged();
        void PublishHealNumber(float actualHeal);
        void LogImmediateSkill(string message);
    }

    /// <summary>
    /// 不持有MonoBehaviour的立即生效术法执行体。
    /// 规则计算在此完成，组件、事件和VFX操作经宿主边界执行。
    /// </summary>
    public sealed class ImmediateSkillEffectRuntime
    {
        private readonly IImmediateSkillEffectHost _host;

        public ImmediateSkillEffectRuntime(IImmediateSkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public void CastBuff(SkillData skill, int slotIndex)
        {
            if (skill.armLethalGuard)
            {
                float duration = skill.lethalGuardDuration > 0f
                    ? skill.lethalGuardDuration
                    : skill.cooldown;
                _host.ArmLethalGuard(duration);
                _host.LogImmediateSkill(
                    $"<color=cyan>{skill.skillName} 武装！受致命伤将自动脱身</color>");
                return;
            }

            if (skill.heavenEarthShift)
            {
                float duration = skill.buffDuration > 0f ? skill.buffDuration : 10f;
                _host.ActivateHeavenEarthShift(duration);
                _host.LogImmediateSkill(
                    $"<color=cyan>{skill.skillName}！乾坤倒转：伤害反弹，攻击转治疗</color>");
                return;
            }

            float buffDuration = skill.buffDuration > 0f
                ? skill.buffDuration
                : (skill.vfxDuration > 0f ? skill.vfxDuration : 5f);
            var modifiers = BuildBuffModifiers(skill);
            _host.ApplyBuffStatus(new StatusEffect
            {
                id = $"skill_buff_{skill.configId}_{skill.skillName}",
                isBuff = true,
                elementTag = skill.elementTag,
                stacks = 1,
                maxStacks = 1,
                defaultDuration = buffDuration,
                duration = buffDuration,
                modifiers = modifiers,
                displayName = skill.skillName,
                description = skill.description,
                uiColor = SkillModifierApplier.ColorOf(skill.elementTag)
            });

            if (slotIndex >= 0 && skill.modifierDefs != null && skill.modifierDefs.Length > 0)
                _host.ApplyBuffSlotModifiers(skill, slotIndex);

            Color shieldColor = skill.elementTag != ElementTag.None
                ? SkillModifierApplier.ColorOf(skill.elementTag)
                : new Color(1f, 0.85f, 0.1f, 0.35f);
            shieldColor.a = 0.35f;
            _host.PlayImmediateVisual(skill, ImmediateSkillVisual.Buff, shieldColor);
            _host.LogImmediateSkill(
                $"<color=cyan>{skill.skillName} 增益生效（由 StatusEffect 计时），持续 {buffDuration}秒</color>");
        }

        public float CastHeal(SkillData skill)
        {
            float requestedHeal = skill.healAmount + _host.AttackDamage * skill.healScaling;
            float oldHealth = _host.CurrentHealth;
            float newHealth = Mathf.Min(oldHealth + requestedHeal, _host.MaxHealth);
            _host.SetCurrentHealth(newHealth);
            float actualHeal = newHealth - oldHealth;
            _host.RecordHealing(requestedHeal, actualHeal);

            _host.PublishHealthChanged();
            _host.PublishHealNumber(actualHeal);
            _host.PlayImmediateVisual(skill, ImmediateSkillVisual.Heal, Color.green);
            _host.LogImmediateSkill(
                $"<color=green>回春术！恢复 {actualHeal:F0} 生命值</color>");
            return actualHeal;
        }

        private static List<StatModifier> BuildBuffModifiers(SkillData skill)
        {
            var modifiers = new List<StatModifier>();
            if (skill.buffAttackSpeedPct != 0f)
                modifiers.Add(StatModifier.Percent(StatType.AttackSpeed, skill.buffAttackSpeedPct));
            if (skill.buffMoveSpeedPct != 0f)
                modifiers.Add(StatModifier.Percent(StatType.MoveSpeed, skill.buffMoveSpeedPct));
            if (skill.buffAttackPct != 0f)
                modifiers.Add(StatModifier.Percent(StatType.AttackDamage, skill.buffAttackPct));
            if (skill.buffDamageReduction != 0f)
                modifiers.Add(StatModifier.Flat(StatType.DamageReduction, skill.buffDamageReduction));
            if (modifiers.Count == 0)
                modifiers.Add(StatModifier.Flat(StatType.DamageReduction, 0.5f));
            return modifiers;
        }
    }
}
