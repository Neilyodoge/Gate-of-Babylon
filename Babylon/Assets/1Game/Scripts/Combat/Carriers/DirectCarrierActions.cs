using System;

namespace XianTu
{
    /// <summary>将现有普攻请求接入统一武器载体槽位。</summary>
    public sealed class WeaponCarrierAction : ICarrierAction
    {
        private readonly Func<bool> _executor;

        public WeaponCarrierAction(Func<bool> executor)
        {
            _executor = executor ??
                throw new ArgumentNullException(nameof(executor));
        }

        public CarrierResult Execute(in CarrierContext context)
        {
            if (context.Slot != CarrierSlot.Weapon)
                return default;

            bool started = _executor();
            return new CarrierResult(started, false);
        }
    }

    /// <summary>将现有闪避请求接入统一身法载体槽位。</summary>
    public sealed class MobilityCarrierAction : ICarrierAction
    {
        private readonly Func<bool> _executor;

        public MobilityCarrierAction(Func<bool> executor)
        {
            _executor = executor ??
                throw new ArgumentNullException(nameof(executor));
        }

        public CarrierResult Execute(in CarrierContext context)
        {
            if (context.Slot != CarrierSlot.Mobility)
                return default;

            bool started = _executor();
            return new CarrierResult(started, false);
        }
    }
}
