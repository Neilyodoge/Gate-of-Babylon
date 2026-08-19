using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 通用技能选择器。负责条件、优先级、冷却与次数，不关心技能如何表现。
    /// </summary>
    public sealed class EnemyAbilityPlanner
    {
        private readonly EnemyAbilityProfile _profile;
        private readonly IEnemyAbilityExecutor _executor;
        private readonly Dictionary<string, float> _readyAt = new();
        private readonly Dictionary<string, int> _useCounts = new();
        private readonly Dictionary<string, float> _cooldownOverrides = new();
        private readonly float _startedAt;
        private float _nextDecisionAt;

        public EnemyAbilityPlanner(EnemyAbilityProfile profile, IEnemyAbilityExecutor executor)
        {
            _profile = profile;
            _executor = executor;
            _startedAt = Time.time;
        }

        public bool TryDecide(in EnemyAbilityContext context)
        {
            if (_profile == null || _executor == null || _executor.IsAbilityLocked)
                return false;
            if (Time.time < _nextDecisionAt)
                return false;

            _nextDecisionAt = Time.time + Mathf.Max(0.05f, _profile.DecisionInterval);
            var candidates = new List<(EnemyAbilityRule Rule, float Score)>();

            foreach (EnemyAbilityRule rule in _profile.Abilities)
            {
                if (!IsEligible(rule, context))
                    continue;

                float distanceSpan = Mathf.Max(0.01f, rule.MaxDistance - rule.MinDistance);
                float distanceCenter = (rule.MinDistance + rule.MaxDistance) * 0.5f;
                float distanceFitness = 1f - Mathf.Clamp01(
                    Mathf.Abs(context.Distance - distanceCenter) / distanceSpan);
                float score = rule.Priority + distanceFitness + Random.Range(0f, 0.1f);
                candidates.Add((rule, score));
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            foreach ((EnemyAbilityRule rule, _) in candidates)
            {
                if (!_executor.TryExecuteAbility(rule))
                    continue;

                string id = ResolveID(rule);
                float cooldown = _cooldownOverrides.TryGetValue(id, out float overrideValue)
                    ? overrideValue
                    : rule.Cooldown;
                _readyAt[id] = Time.time + Mathf.Max(0f, cooldown);
                _useCounts[id] = GetUseCount(id) + 1;
                return true;
            }
            return false;
        }

        public void ResetCooldown(string abilityID)
        {
            if (!string.IsNullOrWhiteSpace(abilityID))
                _readyAt.Remove(abilityID);
        }

        public void SetCooldownOverride(string abilityID, float cooldown)
        {
            if (!string.IsNullOrWhiteSpace(abilityID))
                _cooldownOverrides[abilityID] = Mathf.Max(0f, cooldown);
        }

        private bool IsEligible(EnemyAbilityRule rule, in EnemyAbilityContext context)
        {
            if (rule == null)
                return false;

            string id = ResolveID(rule);
            if (Time.time - _startedAt < rule.InitialDelay)
                return false;
            if (_readyAt.TryGetValue(id, out float readyAt) && Time.time < readyAt)
                return false;
            if (rule.MaxUses >= 0 && GetUseCount(id) >= rule.MaxUses)
                return false;
            if (context.Distance < rule.MinDistance || context.Distance > rule.MaxDistance)
                return false;
            if (context.HpRatio < rule.MinHpRatio || context.HpRatio > rule.MaxHpRatio)
                return false;
            if (rule.RequiredCombatPhase > 0 && context.CombatPhase != rule.RequiredCombatPhase)
                return false;
            if (rule.DayOnly && context.IsNight)
                return false;
            if (rule.NightOnly && !context.IsNight)
                return false;
            if (rule.MinNearbyAllies >= 0 && context.NearbyAllies < rule.MinNearbyAllies)
                return false;
            if (rule.MaxNearbyAllies >= 0 && context.NearbyAllies > rule.MaxNearbyAllies)
                return false;
            bool evaluateBossFlags = rule.BossFlagScope == EnemyAbilityPhaseScope.Any
                                     || (rule.BossFlagScope == EnemyAbilityPhaseScope.Day
                                         && !context.IsNight)
                                     || (rule.BossFlagScope == EnemyAbilityPhaseScope.Night
                                         && context.IsNight);
            if (evaluateBossFlags)
            {
                if (!EvaluateFlag(rule.RequiredBossFlag, true))
                    return false;
                if (!EvaluateFlag(rule.BlockedBossFlag, false))
                    return false;
            }
            return rule.TriggerChance >= 1f || Random.value <= Mathf.Clamp01(rule.TriggerChance);
        }

        private static bool EvaluateFlag(string expression, bool required)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            bool matched = LevelDesign.BossFlagSet.Instance.Evaluate(expression);
            return required ? matched : !matched;
        }

        private int GetUseCount(string id)
        {
            return _useCounts.TryGetValue(id, out int count) ? count : 0;
        }

        private static string ResolveID(EnemyAbilityRule rule)
        {
            if (!string.IsNullOrWhiteSpace(rule.ID))
                return rule.ID;
            if (rule.Action == EnemyAbilityAction.Custom)
                return $"custom:{rule.CustomActionKey}";
            return rule.Action.ToString();
        }
    }
}
