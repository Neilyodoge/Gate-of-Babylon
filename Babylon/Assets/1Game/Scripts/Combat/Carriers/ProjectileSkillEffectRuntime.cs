using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>投射物规则需要的瞄准、伤害和实例副作用边界。</summary>
    public interface IProjectileSkillEffectHost
    {
        Vector3 ProjectileOrigin { get; }
        Vector3 AimDirection { get; }
        bool EnhancementActive { get; }
        bool TargetFarthest { get; }
        bool SurroundPattern { get; }
        bool RingPattern { get; }
        bool WallPattern { get; }
        bool ImpactZone { get; }
        float ProjectileCountMultiplier { get; }
        int ExtraProjectiles { get; }
        bool TryGetFarthestTargetDirection(
            Vector3 origin,
            float maxRange,
            out Vector3 direction);
        float CalculateProjectileDamage(SkillData skill);
        ElementTag ResolveProjectileElement(SkillData skill);
        void SpawnProjectile(
            SkillData skill,
            Vector3 position,
            Vector3 direction,
            float damage,
            ElementTag element,
            bool applyEnhancement,
            bool impactZone);
    }

    /// <summary>
    /// 投射物术法规则执行体。目标覆盖、数量、散射、环绕与墙形排布在此确定，
    /// Prefab、对象池、Projectile组件和Debug实例由宿主处理。
    /// </summary>
    public sealed class ProjectileSkillEffectRuntime
    {
        private const float WallSpacing = 1.1f;
        private readonly IProjectileSkillEffectHost _host;

        public ProjectileSkillEffectRuntime(IProjectileSkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public int Cast(SkillData skill, float damageMultiplier)
        {
            Vector3 spawnPosition = _host.ProjectileOrigin;
            Vector3 direction = _host.AimDirection;
            if (_host.EnhancementActive &&
                _host.TargetFarthest &&
                !_host.SurroundPattern &&
                _host.TryGetFarthestTargetDirection(
                    spawnPosition,
                    22f,
                    out Vector3 farthestDirection))
            {
                direction = farthestDirection;
            }

            float damage =
                _host.CalculateProjectileDamage(skill) * damageMultiplier;
            int count = Mathf.Max(1, skill.projectileCount);
            float spreadAngle = skill.spreadAngle;
            bool ring = _host.EnhancementActive && _host.RingPattern;
            bool wall = _host.EnhancementActive && _host.WallPattern;
            bool impactZone = _host.EnhancementActive && _host.ImpactZone;
            bool surround =
                (_host.EnhancementActive && _host.SurroundPattern) || ring;

            if (_host.EnhancementActive)
            {
                count = Mathf.Max(
                    1,
                    Mathf.RoundToInt(count * _host.ProjectileCountMultiplier) +
                    _host.ExtraProjectiles);
                if (surround)
                    count = Mathf.Max(count, 8);
                if (wall)
                    count = Mathf.Max(count, 5);
                if (count > 1 && spreadAngle < 1f && !wall)
                    spreadAngle = 20f;
            }

            float halfSpread = spreadAngle * 0.5f;
            Vector3 perpendicular =
                Vector3.Cross(Vector3.up, direction).normalized;
            ElementTag element = _host.ResolveProjectileElement(skill);

            for (int index = 0; index < count; index++)
            {
                Vector3 shotDirection = direction;
                Vector3 shotPosition = spawnPosition;
                if (surround)
                {
                    shotDirection =
                        Quaternion.Euler(0f, index * (360f / count), 0f) *
                        direction;
                }
                else if (wall)
                {
                    float offset =
                        (index - (count - 1) * 0.5f) * WallSpacing;
                    shotPosition = spawnPosition + perpendicular * offset;
                }
                else if (count > 1)
                {
                    float angle = Mathf.Lerp(
                        -halfSpread,
                        halfSpread,
                        (float)index / (count - 1));
                    shotDirection =
                        Quaternion.Euler(0f, angle, 0f) * direction;
                }

                _host.SpawnProjectile(
                    skill,
                    shotPosition,
                    shotDirection,
                    damage,
                    element,
                    _host.EnhancementActive,
                    impactZone);
            }

            return count;
        }
    }
}
