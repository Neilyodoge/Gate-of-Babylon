using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>位移术法需要的物理和场景副作用边界。</summary>
    public interface IDashSkillEffectHost
    {
        Vector3 Origin { get; }
        Vector3 AimDirection { get; }
        Vector3 ResolveDashDestination(Vector3 start, Vector3 direction, float distance);
        void ShowDashTrail(SkillData skill, Vector3 start, Vector3 destination);
        void MoveTo(Vector3 destination);
        void SetInvincible(float duration);
        void ApplyDashTrailDamage(SkillData skill, Vector3 start, Vector3 destination);
        void PlayDashVisual(SkillData skill, Vector3 destination);
        void LogDash(string message);
    }

    /// <summary>
    /// 位移术法规则执行体。距离回退、无敌和路径伤害分支在此决定，
    /// 碰撞检测、角色移动、伤害和VFX由宿主执行。
    /// </summary>
    public sealed class DashSkillEffectRuntime
    {
        private readonly IDashSkillEffectHost _host;

        public DashSkillEffectRuntime(IDashSkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public float Cast(SkillData skill)
        {
            Vector3 direction = _host.AimDirection;
            float distance = skill.dashDistance > 0f ? skill.dashDistance : 8f;
            Vector3 start = _host.Origin;
            Vector3 destination =
                _host.ResolveDashDestination(start, direction, distance);

            _host.ShowDashTrail(skill, start, destination);
            _host.MoveTo(destination);

            if (skill.dashInvulnerable && skill.dashInvulnDuration > 0f)
                _host.SetInvincible(skill.dashInvulnDuration);

            if (skill.leaveTrail)
                _host.ApplyDashTrailDamage(skill, start, destination);

            _host.PlayDashVisual(skill, destination);
            float traveledDistance = Vector3.Distance(start, destination);
            _host.LogDash(
                $"<color=cyan>{skill.skillName}！位移 {traveledDistance:F1} 米</color>");
            return traveledDistance;
        }
    }
}
