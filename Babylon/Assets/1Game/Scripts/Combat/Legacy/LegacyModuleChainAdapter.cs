using System;

namespace XianTu
{
    /// <summary>
    /// 将现有模块链翻译为载体可读取的只读增强快照。
    /// P0不改变模块链执行路径；后续载体接管时通过本适配器保持可回滚。
    /// </summary>
    public static class LegacyModuleChainAdapter
    {
        public static bool IsValid(ModuleChain chain) => chain != null && chain.IsValid;

        public static ChainConfig Compile(ModuleChain chain)
        {
            if (!IsValid(chain))
                throw new ArgumentException("A valid legacy module chain is required.", nameof(chain));

            return chain.Compile();
        }

        public static CombatEnhancementSnapshot ToEnhancementSnapshot(in ChainConfig config)
        {
            return new CombatEnhancementSnapshot(
                config.effectRole switch
                {
                    EffectRole.Enhancement => CarrierEnhancementRole.Enhancement,
                    EffectRole.Addon => CarrierEnhancementRole.Addon,
                    _ => throw new ArgumentOutOfRangeException(nameof(config), config.effectRole, "Unsupported legacy effect role."),
                },
                config.elementTag,
                config.enhanceDamageMult,
                config.enhanceRadiusMult,
                config.enhanceProjectileMult,
                config.enhanceExtraProjectiles,
                config.enhanceChainCount,
                config.enhanceSurround,
                config.enhanceSustained,
                config.enhanceDelayedBlast,
                config.enhanceTargetFarthest,
                config.enhanceShape switch
                {
                    ShapeMode.None => CarrierShapeMode.None,
                    ShapeMode.Wall => CarrierShapeMode.Wall,
                    ShapeMode.Ring => CarrierShapeMode.Ring,
                    ShapeMode.Zone => CarrierShapeMode.Zone,
                    _ => throw new ArgumentOutOfRangeException(nameof(config), config.enhanceShape, "Unsupported legacy shape."),
                });
        }

        public static CarrierSlot SlotIndexToCarrier(int slotIndex)
        {
            return CarrierSlotMapping.TechniqueFromIndex(slotIndex);
        }
    }

    /// <summary>只读包装当前模块槽；未接入PlayerCombat，不改变运行时行为。</summary>
    public sealed class LegacyModuleEnhancementProvider : ICombatEnhancementProvider
    {
        private readonly ModuleSlotManager _slots;

        public LegacyModuleEnhancementProvider(ModuleSlotManager slots)
        {
            _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        }

        public bool TryGetSnapshot(CarrierSlot slot, out CombatEnhancementSnapshot snapshot)
        {
            int legacySlot = slot switch
            {
                CarrierSlot.TechniqueQ => 0,
                CarrierSlot.TechniqueE => 1,
                CarrierSlot.TechniqueR => 2,
                _ => -1,
            };

            if (legacySlot < 0 || !_slots.HasChain(legacySlot) || !_slots.IsProc(legacySlot))
            {
                snapshot = default;
                return false;
            }

            snapshot = LegacyModuleChainAdapter.ToEnhancementSnapshot(_slots.GetConfig(legacySlot));
            return true;
        }
    }
}
