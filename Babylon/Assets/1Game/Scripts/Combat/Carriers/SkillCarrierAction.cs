using System;

namespace XianTu
{
    /// <summary>
    /// 术法载体的首个运行时适配器。P1.1只统一调用入口，
    /// 具体技能效果仍委托现有PlayerCombat实现，确保开关切换时行为等价。
    /// </summary>
    public sealed class SkillCarrierAction : ICarrierAction
    {
        private readonly Func<SkillData, int, int, bool> _legacyExecutor;

        public SkillCarrierAction(Func<SkillData, int, int, bool> legacyExecutor)
        {
            _legacyExecutor = legacyExecutor ?? throw new ArgumentNullException(nameof(legacyExecutor));
        }

        public CarrierResult Execute(in CarrierContext context)
        {
            if (context.LegacySkill == null)
                return default;

            if (!CarrierSlotMapping.TryToTechniqueIndex(context.Slot, out int slotIndex))
                return default;

            bool castStarted = _legacyExecutor(context.LegacySkill, slotIndex, context.ChargeLevel);
            return new CarrierResult(castStarted, castStarted);
        }
    }
}
