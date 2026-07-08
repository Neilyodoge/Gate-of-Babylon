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
        public bool enhanceTargetFarthest;    // V0.1.13 增强型让核心投射技自动锁定范围内最远敌（TargetFarthest 改造件）
        public ShapeMode enhanceShape;        // V0.1.13 增强型改造核心投射技发射形态（Shape* 改造件：Wall/Ring/Zone）

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
                enhanceTargetFarthest = false,
                enhanceShape = ShapeMode.None,

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
            ApplyConsumeKindIdentity(ref cfg);
            return cfg;
        }

        // ==================== V0.1.13 consumeKind 联动 ====================
        // 四种消费模型各有身份加成，形成取舍三角（数值集中在此，便于平衡）：
        //   Single（单发）：用完重充，奖励单次爆发 → 增伤 ×1.25
        //   Window（窗口）：择时消费，奖励范围 + 小增伤 → 范围 ×1.20、增伤 ×1.10
        //   Stacks（叠层）：多次消费，收益在层数本身 → 单次中性（×1.0）
        //   Auto（自动）：放弃择时换 hands-free，代价降伤 → 增伤 ×0.80
        // 同时作用于增强字段（enhance*，Enhancement 角色用）与附加字段（damage/radius，Addon 角色用），
        // 每角色只读其一，互不干扰。
        // V0.1.18b 运行时读表：系数改由 ConsumeKind_Bonus_Config（ID=(int)ConsumeKind）提供，
        // 查不到表（未导表 / Resources 缺失）时回退到下列常量，行为与旧版一致（零回归）。
        private const float SingleDamageMul = 1.25f;
        private const float WindowDamageMul = 1.10f;
        private const float WindowRadiusMul = 1.20f;
        private const float AutoDamageMul = 0.80f;

        private static float FallbackDamageMul(ConsumeKind k) => k switch
        {
            ConsumeKind.Single => SingleDamageMul,
            ConsumeKind.Window => WindowDamageMul,
            ConsumeKind.Auto   => AutoDamageMul,
            _ => 1f,
        };

        private static float FallbackRadiusMul(ConsumeKind k) => k switch
        {
            ConsumeKind.Window => WindowRadiusMul,
            _ => 1f,
        };

        /// <summary>获取当前 consumeKind 的增伤系数（供 UI 预览复用）。优先读表，回退常量。</summary>
        public static float ConsumeKindDamageMul(ConsumeKind k)
        {
            var row = LevelDesign.ConfigDatabase.Instance?.GetConsumeKindBonus((int)k);
            return row != null ? row.DamageMul : FallbackDamageMul(k);
        }

        /// <summary>获取当前 consumeKind 的范围系数（供 UI 预览复用）。优先读表，回退常量。</summary>
        public static float ConsumeKindRadiusMul(ConsumeKind k)
        {
            var row = LevelDesign.ConfigDatabase.Instance?.GetConsumeKindBonus((int)k);
            return row != null ? row.RadiusMul : FallbackRadiusMul(k);
        }

        private static void ApplyConsumeKindIdentity(ref ChainConfig cfg)
        {
            float dmg = ConsumeKindDamageMul(cfg.consumeKind);
            float rad = ConsumeKindRadiusMul(cfg.consumeKind);
            if (dmg != 1f)
            {
                cfg.enhanceDamageMult *= dmg; // Enhancement 角色
                cfg.damage *= dmg;            // Addon 角色
            }
            if (rad != 1f)
            {
                cfg.enhanceRadiusMult *= rad; // Enhancement 角色
                cfg.radius *= rad;            // Addon 角色
            }
        }

        private static void ApplyModifier(ref ChainConfig cfg, ModuleDef mod)
        {
            if (mod == null || mod.category != ModuleCategory.Modifier) return;

            switch (mod.modifierType)
            {
                // 形态改造（核心投射技发射几何）——V0.1.13 落地
                case ModifierType.ShapeWall:
                    cfg.enhanceShape = ShapeMode.Wall;
                    break;
                case ModifierType.ShapeRing:
                    cfg.enhanceShape = ShapeMode.Ring;
                    break;
                case ModifierType.ShapeZone:
                    cfg.enhanceShape = ShapeMode.Zone;
                    break;

                // 目标改造·链锁弹射（增强核心投射技命中后反弹）
                case ModifierType.TargetChain:
                    cfg.enhanceChainCount += mod.extraCount > 0 ? mod.extraCount : 2;
                    break;

                // 目标改造·最远（增强核心投射技自动锁定范围内最远敌）
                case ModifierType.TargetFarthest:
                    cfg.enhanceTargetFarthest = true;
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
