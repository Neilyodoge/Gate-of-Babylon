using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public readonly struct BasicAttackTarget
    {
        public object Handle { get; }
        public object EventTarget { get; }
        public Vector3 Position { get; }
        public Vector3 HitPoint { get; }
        public float Defense { get; }
        public bool IsDamageable { get; }

        public BasicAttackTarget(
            object handle,
            object eventTarget,
            Vector3 position,
            Vector3 hitPoint,
            float defense,
            bool isDamageable = true)
        {
            Handle = handle;
            EventTarget = eventTarget;
            Position = position;
            HitPoint = hitPoint;
            Defense = defense;
            IsDamageable = isDamageable;
        }
    }

    /// <summary>近战伤害与远程普攻实例所需的副作用边界。</summary>
    public interface IBasicAttackEffectHost
    {
        Vector3 MeleeOrigin { get; }
        Vector3 AimDirection { get; }
        Vector3 ForwardFallback { get; }
        float MeleeRange { get; }
        float MeleeHalfAngle { get; }
        bool ConvertMeleeDamageToHealing { get; }
        Vector3 RangedProjectileOrigin { get; }
        float RangedProjectileSpeed { get; }
        float RangedDamageMultiplier { get; }
        ElementTag RangedElement { get; }
        IReadOnlyList<BasicAttackTarget> FindBasicAttackTargets(
            Vector3 origin,
            float range);
        float GetBasicComboMultiplier(int comboStep);
        float CalculateBasicDamage(float targetDefense);
        void ApplyBasicDamage(BasicAttackTarget target, float damage);
        void ApplyConvertedBasicHealing(float healAmount, Vector3 hitPoint);
        void PlayBasicHitVisual(Vector3 hitPoint);
        void SpawnBasicProjectile(
            Vector3 position,
            Vector3 direction,
            float speed,
            float damage,
            ElementTag element);
        void ReduceBasicCooldownOnFinisher();
    }

    /// <summary>
    /// 近战扇形伤害、伤害转治疗和远程普攻投射参数执行体。
    /// 物理查询、生命修改、实际扣血、事件、Prefab和VFX由宿主处理。
    /// </summary>
    public sealed class BasicAttackEffectRuntime
    {
        private readonly IBasicAttackEffectHost _host;

        public BasicAttackEffectRuntime(IBasicAttackEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public MeleeHitResult ResolveMeleeHits(int comboStep)
        {
            Vector3 origin = _host.MeleeOrigin;
            Vector3 forward = _host.AimDirection;
            IReadOnlyList<BasicAttackTarget> targets =
                _host.FindBasicAttackTargets(origin, _host.MeleeRange);
            bool hitAny = false;
            object firstTarget = null;
            Vector3 firstHitPoint = origin;
            float comboMultiplier =
                _host.GetBasicComboMultiplier(comboStep);

            foreach (BasicAttackTarget target in targets)
            {
                if (!target.IsDamageable)
                    continue;

                Vector3 directionToTarget =
                    (target.Position - origin).normalized;
                directionToTarget.y = 0f;
                float angle = Vector3.Angle(forward, directionToTarget);
                if (angle > _host.MeleeHalfAngle)
                    continue;

                float damage =
                    _host.CalculateBasicDamage(target.Defense) *
                    comboMultiplier;
                if (_host.ConvertMeleeDamageToHealing)
                {
                    _host.ApplyConvertedBasicHealing(
                        damage * 0.5f,
                        target.HitPoint);
                    _host.PlayBasicHitVisual(target.HitPoint);
                    hitAny = true;
                    continue;
                }

                _host.ApplyBasicDamage(target, damage);
                _host.PlayBasicHitVisual(target.HitPoint);
                hitAny = true;
                if (firstTarget == null)
                {
                    firstTarget = target.EventTarget;
                    firstHitPoint = target.HitPoint;
                }
            }

            return new MeleeHitResult(
                hitAny,
                firstTarget,
                firstHitPoint);
        }

        public float FireRangedBasic(int comboStep)
        {
            Vector3 direction = _host.AimDirection;
            if (direction.sqrMagnitude < 0.0001f)
                direction = _host.ForwardFallback;

            float damage =
                _host.CalculateBasicDamage(0f) *
                _host.GetBasicComboMultiplier(comboStep) *
                _host.RangedDamageMultiplier;
            _host.SpawnBasicProjectile(
                _host.RangedProjectileOrigin,
                direction,
                _host.RangedProjectileSpeed,
                damage,
                _host.RangedElement);
            if (comboStep == 2)
                _host.ReduceBasicCooldownOnFinisher();
            return damage;
        }
    }
}
