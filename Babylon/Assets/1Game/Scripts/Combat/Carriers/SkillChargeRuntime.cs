using System;

namespace XianTu
{
    /// <summary>
    /// 三个术法载体槽位的充能与冷却状态。只处理数值状态，
    /// UI事件和SkillData兼容换算仍由PlayerCombat门面负责。
    /// </summary>
    public sealed class SkillChargeRuntime
    {
        public const int SlotCount = 3;

        private readonly int[] _currentCharges = new int[SlotCount];
        private readonly int[] _maxCharges = new int[SlotCount];
        private readonly int[] _chargeBonuses = new int[SlotCount];
        private readonly float[] _remainingTimes = new float[SlotCount];
        private readonly float[] _rechargeDurations = new float[SlotCount];

        public void Initialize(int slotIndex, int baseCharges)
        {
            ValidateSlot(slotIndex);
            int maxCharges = Clamp(baseCharges + _chargeBonuses[slotIndex], 1, 3);
            _maxCharges[slotIndex] = maxCharges;
            _currentCharges[slotIndex] = maxCharges;
            _remainingTimes[slotIndex] = 0f;
            _rechargeDurations[slotIndex] = 0f;
        }

        public void InitializeEmpty(int slotIndex)
        {
            ValidateSlot(slotIndex);
            _maxCharges[slotIndex] = 1;
            _currentCharges[slotIndex] = 1;
            _remainingTimes[slotIndex] = 0f;
            _rechargeDurations[slotIndex] = 0f;
        }

        public bool Consume(int slotIndex, float rechargeDuration)
        {
            ValidateSlot(slotIndex);
            if (_currentCharges[slotIndex] <= 0)
                return false;

            _currentCharges[slotIndex]--;
            if (_currentCharges[slotIndex] < _maxCharges[slotIndex] &&
                _remainingTimes[slotIndex] <= 0f)
            {
                StartRecharge(slotIndex, rechargeDuration);
            }

            return true;
        }

        public bool Tick(int slotIndex, float deltaTime, float rechargeDuration)
        {
            ValidateSlot(slotIndex);
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time cannot be negative.");
            if (_currentCharges[slotIndex] >= _maxCharges[slotIndex])
                return false;

            _remainingTimes[slotIndex] -= deltaTime;
            if (_remainingTimes[slotIndex] <= 0f)
            {
                _currentCharges[slotIndex]++;
                if (_currentCharges[slotIndex] < _maxCharges[slotIndex])
                    StartRecharge(slotIndex, rechargeDuration);
                else
                    _remainingTimes[slotIndex] = 0f;
            }

            return true;
        }

        public void ResetTimer(int slotIndex)
        {
            ValidateSlot(slotIndex);
            _remainingTimes[slotIndex] = 0f;
        }

        public float ReduceRemainingByPercent(int slotIndex, float percent)
        {
            ValidateSlot(slotIndex);
            float clampedPercent = Math.Max(0f, Math.Min(1f, percent));
            float reduction = _remainingTimes[slotIndex] * clampedPercent;
            _remainingTimes[slotIndex] = Math.Max(0f, _remainingTimes[slotIndex] - reduction);
            return reduction;
        }

        public void AddBonus(int slotIndex, int bonus)
        {
            ValidateSlot(slotIndex);
            _chargeBonuses[slotIndex] = Math.Max(0, _chargeBonuses[slotIndex] + bonus);
        }

        public void RemoveBonus(int slotIndex, int bonus)
        {
            ValidateSlot(slotIndex);
            _chargeBonuses[slotIndex] = Math.Max(0, _chargeBonuses[slotIndex] - bonus);
        }

        public int GetCurrentCharges(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return _currentCharges[slotIndex];
        }

        public int GetMaxCharges(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return _maxCharges[slotIndex];
        }

        public float GetRemainingTime(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return _remainingTimes[slotIndex];
        }

        public float GetRechargeDuration(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return _rechargeDurations[slotIndex];
        }

        public bool IsRecharging(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return _remainingTimes[slotIndex] > 0.01f;
        }

        private void StartRecharge(int slotIndex, float rechargeDuration)
        {
            float duration = Math.Max(0f, rechargeDuration);
            _remainingTimes[slotIndex] = duration;
            _rechargeDurations[slotIndex] = duration;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static void ValidateSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, "Skill slot must be Q, E, or R.");
        }
    }
}
