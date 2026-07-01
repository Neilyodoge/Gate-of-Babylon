using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 模块链的编译产物——包含执行所需的全部参数，
    /// 由 Trigger + Effect + Modifier 合成后缓存。
    /// V.08：链是核心技能的增强器，ChainConfig 描述"消费时给核心技能附加什么"。
    /// </summary>
    public struct ChainConfig
    {
        // meta
        public ExecutionMode executionMode;   // 保留以兼容旧 UI；V.08 不再驱动逻辑
        public ConsumeKind consumeKind;       // V.08 消费模型
        public float windowSeconds;           // Window 模式就绪窗口
        public int maxStacks;                 // Stacks 模式最大层数
        public EffectRole effectRole;         // V.08 效果器角色
        public float enhanceDamageMult;       // V.08 增强型对核心技能的伤害倍率（来自改造件，base 1.0）
        public float enhanceRadiusMult;       // V.08 增强型对核心技能范围的倍率（RadiusScale 改造件，base 1.0）
        public float enhanceProjectileMult;   // V.08 增强型对核心技能投射物数量的倍率（CountScale 改造件，base 1.0）
        public int enhanceExtraProjectiles;   // V.08 增强型对核心技能额外投射物数（ExtraProjectile 改造件）
        public int enhanceChainCount;         // V.08 增强型对核心技能投射物的链锁弹射次数（TargetChain 改造件）
        public bool enhanceSurround;          // V.08 增强型让核心投射技 360° 环绕发射（TargetSurround 改造件）
        public bool enhanceSustained;         // V.08 增强型让核心范围技留下持续地带（Sustained 改造件）
        public bool enhanceDelayedBlast;      // V.08 增强型让核心范围技追加延迟重爆（DelayedBlast 改造件）

        // trigger
        public TriggerType triggerType;
        public int triggerThreshold;
        public float triggerCooldown;
        public float triggerInterval;
        public float moveDistanceThreshold;
        public float healthThreshold;

        // effect
        public EffectType effectType;
        public float damage;
        public float damageScaling;
        public float radius;
        public float healAmount;
        public float healScaling;
        public float shieldAmount;
        public float buffDuration;
        public float buffDamageReduction;
        public int projectileCount;
        public float projectileSpeed;
        public float spreadAngle;
        public float slowPercent;
        public float stunDuration;
        public float knockbackForce;
        public float dashDistance;
        public float pullRadius;
        public float dotDPS;
        public float dotDuration;
        public float invincibleDuration;
        public float summonDuration;
        public float summonDamage;
        public float trapDuration;
        public float vulnerableMultiplier;
        public float vulnerableDuration;
        public ElementTag elementTag;
        public GameObject vfxPrefab;

        // modifier-applied flags
        public bool addBurn;
        public float burnDPS;
        public float burnDuration;
        public bool addFreeze;
        public float freezeDuration;
        public bool addLightning;
        public float lightningDamage;
        public bool addPoison;
        public float poisonDPS;
        public float poisonDuration;
        public bool addKnockback;
        public bool addVulnerable;

        // cost modifiers
        public float costHPPercent;
        public float costDamageBonus;
    }

    /// <summary>
    /// 运行时模块链容器：1 Trigger + 1 Effect + 0–2 Modifier。
    /// Compile() 将 modifier 参数叠加到 effect 参数上，生成可直接执行的 ChainConfig。
    /// 支持万能件：触发器槽/效果器槽可放入万能件，按槽位决定职责。
    /// </summary>
    [System.Serializable]
    public class ModuleChain
    {
        public ModuleDef trigger;
        public ModuleDef effect;
        public ModuleDef modifier0;
        public ModuleDef modifier1;

        public bool IsValid
        {
            get
            {
                if (trigger == null || effect == null) return false;
                bool triggerOk = trigger.category == ModuleCategory.Trigger
                              || trigger.category == ModuleCategory.Universal;
                bool effectOk = effect.category == ModuleCategory.Effect
                             || effect.category == ModuleCategory.Universal;
                return triggerOk && effectOk;
            }
        }

        public ExecutionMode ResolvedExecutionMode
        {
            get
            {
                if (effect != null) return effect.executionMode;
                if (trigger != null) return trigger.executionMode;
                return ExecutionMode.Passive;
            }
        }

        public string DisplayName
        {
            get
            {
                if (!IsValid) return "空链";
                string tName = trigger.category == ModuleCategory.Universal
                    ? $"☆{trigger.displayName}" : trigger.displayName;
                string eName = effect.category == ModuleCategory.Universal
                    ? $"☆{effect.displayName}" : effect.displayName;
                string name = $"{tName}→{eName}";
                if (modifier0 != null) name += $"+{modifier0.displayName}";
                if (modifier1 != null) name += $"+{modifier1.displayName}";
                return name;
            }
        }

        public ChainConfig Compile()
        {
            var tType = trigger.GetTriggerTypeForSlot();
            var eType = effect.GetEffectTypeForSlot();

            var cfg = new ChainConfig
            {
                executionMode = ResolvedExecutionMode,
                consumeKind = trigger.GetConsumeKindForSlot(),
                windowSeconds = trigger.windowSeconds,
                maxStacks = trigger.maxStacks,
                effectRole = effect.GetEffectRoleForSlot(),
                enhanceDamageMult = 1f,
                enhanceRadiusMult = 1f,
                enhanceProjectileMult = 1f,
                enhanceExtraProjectiles = 0,
                enhanceChainCount = 0,
                enhanceSurround = false,
                enhanceSustained = false,
                enhanceDelayedBlast = false,

                triggerType = tType,
                triggerThreshold = trigger.category == ModuleCategory.Universal
                    ? trigger.universalTriggerThreshold : trigger.triggerThreshold,
                triggerCooldown = trigger.category == ModuleCategory.Universal
                    ? trigger.universalTriggerCooldown : trigger.triggerCooldown,
                triggerInterval = trigger.triggerInterval,
                moveDistanceThreshold = trigger.moveDistanceThreshold,
                healthThreshold = trigger.healthThreshold,

                effectType = eType,
                damage = effect.baseDamage,
                damageScaling = effect.damageScaling,
                radius = effect.aoeRadius,
                healAmount = effect.healAmount,
                healScaling = effect.healScaling,
                shieldAmount = effect.shieldAmount,
                buffDuration = effect.buffDuration,
                buffDamageReduction = effect.buffDamageReduction,
                projectileCount = effect.projectileCount,
                projectileSpeed = effect.projectileSpeed,
                spreadAngle = effect.spreadAngle,
                slowPercent = effect.slowPercent,
                stunDuration = effect.stunDuration,
                knockbackForce = effect.knockbackForce,
                dashDistance = effect.dashDistance,
                pullRadius = effect.pullRadius,
                dotDPS = effect.dotDPS,
                dotDuration = effect.dotDuration,
                invincibleDuration = effect.invincibleDuration,
                summonDuration = effect.summonDuration,
                summonDamage = effect.summonDamage,
                trapDuration = effect.trapDuration,
                vulnerableMultiplier = effect.vulnerableMultiplier,
                vulnerableDuration = effect.vulnerableDuration,
                elementTag = effect.elementTag,
                vfxPrefab = effect.vfxPrefab
            };

            ApplyModifier(ref cfg, modifier0);
            ApplyModifier(ref cfg, modifier1);
            return cfg;
        }

        private static void ApplyModifier(ref ChainConfig cfg, ModuleDef mod)
        {
            if (mod == null || mod.category != ModuleCategory.Modifier) return;

            switch (mod.modifierType)
            {
                // 形态改造
                case ModifierType.ShapeWall:
                case ModifierType.ShapeRing:
                case ModifierType.ShapeZone:
                    // 形态变换在效果执行时通过 modifierType 判断
                    break;

                // 目标改造·链锁弹射（增强核心投射技命中后反弹）
                case ModifierType.TargetChain:
                    cfg.enhanceChainCount += mod.extraCount > 0 ? mod.extraCount : 2;
                    break;

                // 目标改造·环绕（增强核心投射技 360° 均分发射）
                case ModifierType.TargetSurround:
                    cfg.enhanceSurround = true;
                    if (mod.extraCount > 0) cfg.enhanceExtraProjectiles += mod.extraCount;
                    break;

                // 节奏改造·持续（增强让核心范围技留下持续地带）
                case ModifierType.Sustained:
                    cfg.enhanceSustained = true;
                    break;

                // 节奏改造·延迟爆炸（增强让核心范围技追加延迟重爆）
                case ModifierType.DelayedBlast:
                    cfg.enhanceDelayedBlast = true;
                    break;

                // 数量改造
                case ModifierType.RadiusScale:
                    cfg.radius *= mod.modifierValue;
                    cfg.enhanceRadiusMult *= mod.modifierValue;
                    break;
                case ModifierType.CountScale:
                    cfg.projectileCount = Mathf.Max(1, Mathf.RoundToInt(cfg.projectileCount * mod.modifierValue));
                    if (cfg.spreadAngle < 1f && cfg.projectileCount > 1)
                        cfg.spreadAngle = 15f;
                    cfg.enhanceProjectileMult *= mod.modifierValue;
                    break;
                case ModifierType.DurationScale:
                    cfg.buffDuration *= mod.modifierValue;
                    cfg.burnDuration *= mod.modifierValue;
                    cfg.freezeDuration *= mod.modifierValue;
                    cfg.dotDuration *= mod.modifierValue;
                    cfg.summonDuration *= mod.modifierValue;
                    break;
                case ModifierType.DamageScale:
                    cfg.damage *= mod.modifierValue;
                    cfg.enhanceDamageMult *= mod.modifierValue;
                    break;
                case ModifierType.ExtraCount:
                    cfg.projectileCount += mod.extraCount;
                    cfg.enhanceExtraProjectiles += mod.extraCount;
                    break;
                case ModifierType.ExtraProjectile:
                    cfg.projectileCount += mod.extraCount;
                    if (cfg.spreadAngle < 1f && cfg.projectileCount > 1)
                        cfg.spreadAngle = 15f;
                    cfg.enhanceExtraProjectiles += mod.extraCount;
                    break;

                // 状态改造
                case ModifierType.AddBurn:
                    cfg.addBurn = true;
                    cfg.burnDPS = mod.burnDPS;
                    cfg.burnDuration = mod.burnDuration;
                    break;
                case ModifierType.AddFreeze:
                    cfg.addFreeze = true;
                    cfg.freezeDuration = mod.freezeDuration;
                    break;
                case ModifierType.AddLightning:
                    cfg.addLightning = true;
                    cfg.lightningDamage = mod.lightningDamage;
                    break;
                case ModifierType.AddPoison:
                    cfg.addPoison = true;
                    cfg.poisonDPS = mod.poisonDPS;
                    cfg.poisonDuration = mod.poisonDuration;
                    break;
                case ModifierType.AddKnockback:
                    cfg.addKnockback = true;
                    break;
                case ModifierType.AddVulnerable:
                    cfg.addVulnerable = true;
                    break;

                // 代价改造
                case ModifierType.CostHP:
                    cfg.costHPPercent = mod.costHPPercent;
                    cfg.costDamageBonus = mod.costDamageBonus;
                    cfg.damage *= (1f + mod.costDamageBonus);
                    cfg.enhanceDamageMult *= (1f + mod.costDamageBonus);
                    break;
                case ModifierType.CostCooldown:
                    cfg.triggerCooldown *= mod.modifierValue;
                    cfg.radius *= 1.5f;
                    break;
            }
        }
    }
}
