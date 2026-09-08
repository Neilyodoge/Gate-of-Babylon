using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>持续区域与召唤术法需要的场景副作用边界。</summary>
    public interface IWorldSkillEffectHost
    {
        Vector3 Origin { get; }
        Vector3 AimDirection { get; }
        bool TryGetGroundPointer(out Vector3 worldPosition);
        void SpawnZone(SkillData skill, Vector3 position, float damageMultiplier);
        void SpawnDecoy(Vector3 position, float duration);
        float BuildSummonDamage(float attackRatio, float flatDamage);
        void SpawnSummon(SkillData skill, Vector3 position, float damage, float duration);
        void LogWorldSkill(string message);
    }

    /// <summary>
    /// 持续区域与召唤术法的规则执行体。目标位置和召唤参数在此确定，
    /// 场景查询、实例化、协程及增强注入由宿主完成。
    /// </summary>
    public sealed class WorldSkillEffectRuntime
    {
        private readonly IWorldSkillEffectHost _host;

        public WorldSkillEffectRuntime(IWorldSkillEffectHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public Vector3 CastZone(SkillData skill, float damageMultiplier)
        {
            Vector3 spawnPosition = _host.Origin;
            if (!skill.zoneFollowPlayer &&
                _host.TryGetGroundPointer(out Vector3 pointerPosition))
            {
                spawnPosition = pointerPosition;
            }

            _host.SpawnZone(skill, spawnPosition, damageMultiplier);
            _host.LogWorldSkill(
                $"<color=cyan>{skill.skillName} 召唤持续区域（{(skill.zoneFollowPlayer ? "随身" : "落点")}）</color>");
            return spawnPosition;
        }

        public void CastSummon(SkillData skill)
        {
            if (skill.summonIsDecoy)
            {
                float decoyDuration = skill.summonDuration > 0f
                    ? skill.summonDuration
                    : 3f;
                _host.SpawnDecoy(_host.Origin, decoyDuration);
                _host.LogWorldSkill(
                    $"<color=cyan>{skill.skillName}！水镜分身吸引敌人 {decoyDuration:F0} 秒</color>");
                return;
            }

            Vector3 spawnPosition = _host.Origin + _host.AimDirection * 2f;
            float damage = _host.BuildSummonDamage(skill.damageScaling, skill.summonDamage);
            float duration = skill.summonDuration;
            _host.SpawnSummon(skill, spawnPosition, damage, duration);
            _host.LogWorldSkill(
                $"<color=cyan>召唤！持续 {duration:F0} 秒，每次攻击 {damage:F0} 伤害</color>");
        }
    }
}
