using System;

namespace XianTu
{
    public readonly struct SkillChargeRelease
    {
        public int SlotIndex { get; }
        public int ChargeLevel { get; }
        public float ChargeTime { get; }

        public SkillChargeRelease(int slotIndex, int chargeLevel, float chargeTime)
        {
            SlotIndex = slotIndex;
            ChargeLevel = chargeLevel;
            ChargeTime = chargeTime;
        }
    }

    /// <summary>
    /// 术法蓄力的纯输入状态；移动减速、动画、事件和技能执行由PlayerCombat兼容门面处理。
    /// </summary>
    public sealed class SkillChargeInputRuntime
    {
        public int SlotIndex { get; private set; } = -1;
        public float ElapsedTime { get; private set; }
        public int ChargeLevel { get; private set; } = 1;
        public bool IsCharging => SlotIndex >= 0;

        public void Start(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SkillChargeRuntime.SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Skill slot must be Q, E, or R.");

            SlotIndex = slotIndex;
            ElapsedTime = 0f;
            ChargeLevel = 1;
        }

        public bool Advance(float deltaTime, float level2Time, float level3Time)
        {
            if (!IsCharging)
                return false;
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time cannot be negative.");

            ElapsedTime += deltaTime;
            int nextLevel = ElapsedTime >= level3Time
                ? 3
                : ElapsedTime >= level2Time
                    ? 2
                    : 1;
            if (nextLevel == ChargeLevel)
                return false;

            ChargeLevel = nextLevel;
            return true;
        }

        public SkillChargeRelease Stop()
        {
            var release = new SkillChargeRelease(SlotIndex, ChargeLevel, ElapsedTime);
            Reset();
            return release;
        }

        public void Reset()
        {
            SlotIndex = -1;
            ElapsedTime = 0f;
            ChargeLevel = 1;
        }
    }
}
