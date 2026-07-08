using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// V0.1.18b 运行时读表：把 Module_*_Param_Config 参数仓库表的数值覆盖到已加载的
    /// <see cref="ModuleDef"/> 实例上，使策划改 CSV（→ 导表 → JSON）即可影响运行时数值，
    /// 无需逐个手改 SO。
    ///
    /// 安全约束：
    /// - 仅在 <see cref="Application.isPlaying"/> 时执行。Edit 模式下 Resources.LoadAll 返回的是
    ///   真实资产实例，覆盖它会弄脏源盘；Play 模式的内存改动不落盘（下次域重载自动还原），零风险。
    /// - 查不到对应行则保留该模块的 SO 原值（缺表 / 缺行均回退），零回归。
    /// - 当前表由 SO 真实导出，值与 SO 一致，本版覆盖为 1:1（无行为变化）；价值在于后续 CSV 迭代。
    /// </summary>
    public static class ModuleTableApplier
    {
        private static bool _applied;

        public static void ApplyAll(ModuleDef[] pool)
        {
            if (_applied || pool == null) return;
            if (!Application.isPlaying) return;

            var db = ConfigDatabase.Instance;
            if (db == null) return;

            int n = 0;
            foreach (var m in pool)
            {
                if (m == null || string.IsNullOrEmpty(m.moduleId)) continue;
                bool hit = m.category switch
                {
                    ModuleCategory.Trigger => ApplyTrigger(m, db.GetModuleTriggerParam(m.moduleId)),
                    ModuleCategory.Effect => ApplyEffect(m, db.GetModuleEffectParam(m.moduleId)),
                    ModuleCategory.Modifier => ApplyModifier(m, db.GetModuleModifierParam(m.moduleId)),
                    ModuleCategory.Universal => ApplyUniversal(m, db.GetModuleUniversalParam(m.moduleId)),
                    _ => false,
                };
                if (hit) n++;
            }

            _applied = true;
            Debug.Log($"<color=cyan>[ModuleTableApplier] 参数表覆盖 {n}/{pool.Length} 个模块</color>");
        }

        private static bool ApplyTrigger(ModuleDef m, ModuleTriggerParamRow r)
        {
            if (r == null) return false;
            m.triggerType = (TriggerType)r.TriggerType;
            m.triggerThreshold = r.Threshold;
            m.triggerCooldown = r.Cooldown;
            m.triggerInterval = r.Interval;
            m.consumeStacks = r.ConsumeStacks != 0;
            m.moveDistanceThreshold = r.MoveDistanceThreshold;
            m.healthThreshold = r.HealthThreshold;
            m.consumeKind = (ConsumeKind)r.ConsumeKind;
            m.windowSeconds = r.WindowSeconds;
            m.maxStacks = r.MaxStacks;
            return true;
        }

        private static bool ApplyEffect(ModuleDef m, ModuleEffectParamRow r)
        {
            if (r == null) return false;
            m.effectType = (EffectType)r.EffectType;
            m.effectRole = (EffectRole)r.EffectRole;
            m.baseDamage = r.BaseDamage;
            m.damageScaling = r.DamageScaling;
            m.aoeRadius = r.AoeRadius;
            m.elementTag = (ElementTag)r.Element;
            m.healAmount = r.HealAmount;
            m.healScaling = r.HealScaling;
            m.shieldAmount = r.ShieldAmount;
            m.buffDuration = r.BuffDuration;
            m.buffDamageReduction = r.BuffDamageReduction;
            m.projectileSpeed = r.ProjectileSpeed;
            m.projectileCount = r.ProjectileCount;
            m.spreadAngle = r.SpreadAngle;
            m.slowPercent = r.SlowPercent;
            m.stunDuration = r.StunDuration;
            m.knockbackForce = r.KnockbackForce;
            m.dashDistance = r.DashDistance;
            m.pullRadius = r.PullRadius;
            m.dotDPS = r.DotDPS;
            m.dotDuration = r.DotDuration;
            m.invincibleDuration = r.InvincibleDuration;
            m.summonDuration = r.SummonDuration;
            m.summonDamage = r.SummonDamage;
            m.trapDuration = r.TrapDuration;
            m.vulnerableMultiplier = r.VulnerableMultiplier;
            m.vulnerableDuration = r.VulnerableDuration;
            return true;
        }

        private static bool ApplyModifier(ModuleDef m, ModuleModifierParamRow r)
        {
            if (r == null) return false;
            m.modifierType = (ModifierType)r.ModifierType;
            m.modifierValue = r.ModifierValue;
            m.burnDPS = r.BurnDPS;
            m.burnDuration = r.BurnDuration;
            m.freezeDuration = r.FreezeDuration;
            m.lightningDamage = r.LightningDamage;
            m.poisonDPS = r.PoisonDPS;
            m.poisonDuration = r.PoisonDuration;
            m.extraCount = r.ExtraCount;
            m.costHPPercent = r.CostHPPercent;
            m.costDamageBonus = r.CostDamageBonus;
            return true;
        }

        private static bool ApplyUniversal(ModuleDef m, ModuleUniversalParamRow r)
        {
            if (r == null) return false;
            m.universalTriggerType = (TriggerType)r.UniTriggerType;
            m.universalTriggerThreshold = r.UniTriggerThreshold;
            m.universalTriggerCooldown = r.UniTriggerCooldown;
            m.universalEffectType = (EffectType)r.UniEffectType;
            m.universalEffectRole = (EffectRole)r.UniEffectRole;
            m.universalConsumeKind = (ConsumeKind)r.UniConsumeKind;
            if (!string.IsNullOrEmpty(r.TriggerDesc)) m.universalTriggerDesc = r.TriggerDesc;
            if (!string.IsNullOrEmpty(r.EffectDesc)) m.universalEffectDesc = r.EffectDesc;
            return true;
        }
    }
}
