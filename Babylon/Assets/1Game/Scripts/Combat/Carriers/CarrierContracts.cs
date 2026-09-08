using System;

namespace XianTu
{
    /// <summary>ProjectR动作载体槽位。P0只定义契约，不接管现有输入与技能释放。</summary>
    public enum CarrierSlot
    {
        Weapon = 0,
        TechniqueQ = 1,
        TechniqueE = 2,
        TechniqueR = 3,
        Mobility = 4,
    }

    public static class CarrierSlotMapping
    {
        public static CarrierSlot TechniqueFromIndex(int slotIndex)
        {
            return slotIndex switch
            {
                0 => CarrierSlot.TechniqueQ,
                1 => CarrierSlot.TechniqueE,
                2 => CarrierSlot.TechniqueR,
                _ => throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Technique slot must be Q, E, or R."),
            };
        }

        public static int ToTechniqueIndex(CarrierSlot slot)
        {
            return slot switch
            {
                CarrierSlot.TechniqueQ => 0,
                CarrierSlot.TechniqueE => 1,
                CarrierSlot.TechniqueR => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Carrier is not a technique slot."),
            };
        }

        public static bool TryToTechniqueIndex(CarrierSlot slot, out int slotIndex)
        {
            switch (slot)
            {
                case CarrierSlot.TechniqueQ:
                    slotIndex = 0;
                    return true;
                case CarrierSlot.TechniqueE:
                    slotIndex = 1;
                    return true;
                case CarrierSlot.TechniqueR:
                    slotIndex = 2;
                    return true;
                default:
                    slotIndex = -1;
                    return false;
            }
        }
    }

    /// <summary>一次载体执行所需的最小上下文。</summary>
    public readonly struct CarrierContext
    {
        public CarrierSlot Slot { get; }
        public int ChargeLevel { get; }
        public SkillData LegacySkill { get; }
        public float DeltaTime { get; }

        public CarrierContext(CarrierSlot slot, int chargeLevel, SkillData legacySkill, float deltaTime)
        {
            Slot = slot;
            ChargeLevel = chargeLevel;
            LegacySkill = legacySkill;
            DeltaTime = deltaTime;
        }
    }

    /// <summary>载体尝试执行后的最小结果。</summary>
    public readonly struct CarrierResult
    {
        public bool CastStarted { get; }
        public bool ShouldConsumeCharge { get; }

        public CarrierResult(bool castStarted, bool shouldConsumeCharge)
        {
            CastStarted = castStarted;
            ShouldConsumeCharge = shouldConsumeCharge;
        }
    }

    public interface ICarrierAction
    {
        CarrierResult Execute(in CarrierContext context);
    }
}
