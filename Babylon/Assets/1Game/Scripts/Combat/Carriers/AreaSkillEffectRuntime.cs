using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public readonly struct AreaSkillTarget
    {
        public object Handle { get; }
        public Vector3 Position { get; }
        public float Defense { get; }
        public bool IsDamageable { get; }

        public AreaSkillTarget(
            object handle,
            Vector3 position,
            float defense,
            bool isDamageable = true)
        {
            Handle = handle;
            Position = position;
            Defense = defense;
            IsDamageable = isDamageable;
        }
    }

    /// <summary>范围伤害术法需要的输入、战斗和场景副作用边界。</summary>
    public interface IAreaSkillEffectHost
    {
        bool EnhancementActive { get; }
        float EnhancementRadiusMultiplier { get; }
        bool HasSustainedEnhancement { get; }
        bool HasDelayedBlastEnhancement { get; }
        float TotalPlayerDamage { get; }
        bool TryGetAreaTarget(out Vector3 worldPosition);
        ElementTag ResolveAreaElement(SkillData skill);
        void PlayAreaVisual(
            SkillData skill,
            Vector3 position,
            float visualScale,
            float actualRadius,
            ElementTag element);
        IReadOnlyList<AreaSkillTarget> FindAreaTargets(Vector3 position, float radius);
        float CalculateAreaDamage(SkillData skill, float targetDefense);
        float CalculateAreaBaseMultiplier(SkillData skill);
        void ApplyAreaDamage(AreaSkillTarget target, float damage);
        void TrackAreaEnhancementTarget(AreaSkillTarget target);
        bool RollAreaChance(float probability);
        void ApplyAreaFreeze(AreaSkillTarget target, float duration);
        void PublishAreaHit(SkillData skill, int slotIndex, AreaSkillTarget target);
        void ApplyAreaElementImpact(
            ElementTag element,
            Vector3 position,
            IReadOnlyList<AreaSkillTarget> targets);
        void ApplyAreaSlotModifiers(
            SkillData skill,
            int slotIndex,
            Vector3 position,
            float radius,
            IReadOnlyList<AreaSkillTarget> targets);
        void SpawnSustainedArea(
            Vector3 position,
            float radius,
            float damageMultiplier,
            ElementTag element);
        void SpawnDelayedArea(
            Vector3 position,
            float radius,
            float damageMultiplier,
            ElementTag element);
    }

    /// <summary>
    /// 范围伤害术法规则执行体。半径、伤害来源、冻结、命中事件及增强分支在此编排，
    /// 物理查询、实际扣血、元素效果与世界实例由宿主执行。
    /// </summary>
    public sealed class AreaSkillEffectRuntime
    {
        private readonly IAreaSkillEffectHost _host;

        public AreaSkillEffectRuntime(IAreaSkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public bool Cast(
            SkillData skill,
            float damageMultiplier,
            float radiusMultiplier,
            int slotIndex)
        {
            if (!_host.TryGetAreaTarget(out Vector3 targetPosition))
                return false;

            float actualRadius = skill.aoeRadius * radiusMultiplier;
            if (_host.EnhancementActive)
                actualRadius *= _host.EnhancementRadiusMultiplier;

            ElementTag element = _host.ResolveAreaElement(skill);
            _host.PlayAreaVisual(
                skill,
                targetPosition,
                radiusMultiplier,
                actualRadius,
                element);

            IReadOnlyList<AreaSkillTarget> targets =
                _host.FindAreaTargets(targetPosition, actualRadius);
            AreaSkillTarget? firstHit = null;
            foreach (AreaSkillTarget target in targets)
            {
                if (!target.IsDamageable)
                    continue;

                float damage = skill.damageFromRunTotal
                    ? _host.TotalPlayerDamage * skill.runTotalDamageRatio * damageMultiplier
                    : _host.CalculateAreaDamage(skill, target.Defense) * damageMultiplier;
                _host.ApplyAreaDamage(target, damage);
                firstHit ??= target;

                if (_host.EnhancementActive)
                    _host.TrackAreaEnhancementTarget(target);

                if (skill.freezeOnHitChance > 0f &&
                    _host.RollAreaChance(skill.freezeOnHitChance))
                {
                    _host.ApplyAreaFreeze(target, skill.freezeOnHitDuration);
                }
            }

            if (firstHit.HasValue)
                _host.PublishAreaHit(skill, slotIndex, firstHit.Value);

            if (element != ElementTag.None)
                _host.ApplyAreaElementImpact(element, targetPosition, targets);

            if (slotIndex >= 0)
            {
                _host.ApplyAreaSlotModifiers(
                    skill,
                    slotIndex,
                    targetPosition,
                    actualRadius,
                    targets);
            }

            if (_host.EnhancementActive &&
                (_host.HasSustainedEnhancement ||
                 _host.HasDelayedBlastEnhancement))
            {
                float baseMultiplier = _host.CalculateAreaBaseMultiplier(skill);
                if (_host.HasSustainedEnhancement)
                {
                    _host.SpawnSustainedArea(
                        targetPosition,
                        actualRadius,
                        baseMultiplier * 0.35f * damageMultiplier,
                        element);
                }

                if (_host.HasDelayedBlastEnhancement)
                {
                    _host.SpawnDelayedArea(
                        targetPosition,
                        actualRadius * 1.15f,
                        baseMultiplier * 1.5f * damageMultiplier,
                        element);
                }
            }

            return true;
        }
    }
}
