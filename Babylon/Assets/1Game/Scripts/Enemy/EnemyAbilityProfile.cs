using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public enum EnemyAbilityAction
    {
        Melee,
        Charge,
        AreaAttack,
        Leap,
        Shockwave,
        Summon,
        Dodge,
        Custom
    }

    public enum EnemyAbilityPhaseScope
    {
        Any,
        Day,
        Night
    }

    [Serializable]
    public sealed class EnemyAbilityRule
    {
        [Tooltip("稳定 ID，用于冷却和使用次数记录；同一配置内不可重复。")]
        public string ID = "ability";
        public EnemyAbilityAction Action;
        [Tooltip("Custom 行为由具体 Boss/精英解释此键。")]
        public string CustomActionKey;

        [Header("决策")]
        [Min(0f)] public float Priority = 10f;
        [Range(0f, 1f)] public float TriggerChance = 1f;
        [Min(0f)] public float InitialDelay;
        [Min(0f)] public float Cooldown = 5f;
        [Tooltip("-1 表示不限次数。")]
        public int MaxUses = -1;

        [Header("距离")]
        [Min(0f)] public float MinDistance;
        [Min(0f)] public float MaxDistance = 20f;

        [Header("自身条件")]
        [Range(0f, 1f)] public float MinHpRatio;
        [Range(0f, 1f)] public float MaxHpRatio = 1f;
        [Tooltip("0 表示任意阶段；1/2/... 表示仅指定战斗阶段。")]
        [Min(0)] public int RequiredCombatPhase;
        public bool DayOnly;
        public bool NightOnly;

        [Header("场上条件")]
        [Tooltip("-1 表示不限制。")]
        public int MinNearbyAllies = -1;
        [Tooltip("-1 表示不限制。")]
        public int MaxNearbyAllies = -1;
        [Tooltip("例如 summon_array_destroyed=1；留空表示不检查。")]
        public string RequiredBossFlag;
        public string BlockedBossFlag;
        [Tooltip("限制上述 Flag 只在哪个昼夜阶段参与判断。")]
        public EnemyAbilityPhaseScope BossFlagScope = EnemyAbilityPhaseScope.Any;
    }

    /// <summary>
    /// Boss/精英共用的技能决策配置。这里只描述“何时可释放”，具体动作由宿主执行。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAbilityProfile", menuName = "仙途秘境/敌人/技能决策配置")]
    public sealed class EnemyAbilityProfile : ScriptableObject
    {
        [Min(0.05f)] public float DecisionInterval = 0.2f;
        public List<EnemyAbilityRule> Abilities = new();
    }

    public readonly struct EnemyAbilityContext
    {
        public readonly float Distance;
        public readonly float HpRatio;
        public readonly int CombatPhase;
        public readonly int NearbyAllies;
        public readonly bool IsNight;

        public EnemyAbilityContext(
            float distance,
            float hpRatio,
            int combatPhase,
            int nearbyAllies,
            bool isNight)
        {
            Distance = distance;
            HpRatio = hpRatio;
            CombatPhase = combatPhase;
            NearbyAllies = nearbyAllies;
            IsNight = isNight;
        }
    }

    public interface IEnemyAbilityExecutor
    {
        bool IsAbilityLocked { get; }
        bool TryExecuteAbility(EnemyAbilityRule rule);
    }
}
