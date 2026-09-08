using System;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 将现有Unity结算点转换为结构化Legacy诊断结果。
    /// 仅旁路记录，不参与目标筛选、伤害或资源计算。
    /// </summary>
    public static class LegacyCombatResultRecorder
    {
        public static void RecordPlayerDamage(
            GameObject attacker,
            GameObject target,
            float requestedAmount,
            float appliedAmount,
            bool countTowardRunTotal = false)
        {
            if (attacker == null ||
                target == null ||
                PlayerController.Instance == null)
            {
                return;
            }

            bool isDirectPlayer =
                attacker == PlayerController.Instance.gameObject;
            Projectile projectile = attacker.GetComponent<Projectile>();
            bool isPlayerProjectile =
                projectile != null &&
                projectile.OwnerPlayer == PlayerController.Instance;
            if (!isDirectPlayer && !isPlayerProjectile)
                return;
            if (appliedAmount <= 0f ||
                float.IsNaN(appliedAmount) ||
                float.IsInfinity(appliedAmount))
            {
                return;
            }

            CircuitEntityRef targetRef = BuildTarget(target);
            if (countTowardRunTotal)
            {
                RunCombatStats.AddPlayerDamage(
                    requestedAmount,
                    appliedAmount,
                    targetRef);
            }
            else
            {
                RunCombatStats.RecordPlayerDamage(
                    requestedAmount,
                    appliedAmount,
                    targetRef);
            }

            GameEvents.Publish(new GameEvents.PlayerDamageResolved
            {
                Attacker = attacker,
                Target = target,
                RequestedAmount = requestedAmount,
                AppliedAmount = appliedAmount,
                TargetRef = targetRef,
                IsPlayerOwnedDamage = true
            });
        }

        public static CircuitEntityRef BuildTarget(GameObject target)
        {
            if (target == null)
                return default;

            string category = target.GetComponent<Destructible>() != null
                ? "destructible"
                : "enemy";
            IDamageable damageable = target.GetComponent<IDamageable>();
            string targetType = damageable != null
                ? damageable.GetType().Name
                : target.GetType().Name;
            return new CircuitEntityRef(
                target.GetInstanceID(),
                null,
                Guid.Empty,
                new StableConfigId(
                    $"legacy.{category}.{targetType}"),
                default);
        }

        public static CircuitEntityRef BuildPlayerTarget(
            GameObject fallback = null)
        {
            GameObject player = PlayerController.Instance != null
                ? PlayerController.Instance.gameObject
                : fallback;
            if (player == null)
                return default;

            return new CircuitEntityRef(
                player.GetInstanceID(),
                null,
                Guid.Empty,
                new StableConfigId("legacy.player"),
                default);
        }
    }
}
