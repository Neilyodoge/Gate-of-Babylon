using System;
using System.Collections.Generic;
using System.Linq;

namespace XianTu
{
    public enum SpiritAttachmentPattern
    {
        Empty = 0,
        Partial = 1,
        OneOneOne = 2,
        TwoOne = 3,
        ThreeZero = 4,
    }

    /// <summary>
    /// 本局最多三只器灵的载体附着布局。
    /// 只记录实例GUID到槽位的关系，不拥有器灵成长状态。
    /// </summary>
    public sealed class SpiritAttachmentLayout
    {
        public const int MaxActiveSpirits = 3;

        private readonly Dictionary<CarrierSlot, List<Guid>> _byCarrier =
            new();
        private readonly Dictionary<Guid, CarrierSlot> _bySpirit = new();

        public int Count => _bySpirit.Count;

        public SpiritAttachmentPattern Pattern
        {
            get
            {
                if (Count == 0)
                    return SpiritAttachmentPattern.Empty;
                if (Count < MaxActiveSpirits)
                    return SpiritAttachmentPattern.Partial;

                int occupiedCarriers = _byCarrier.Count(
                    pair => pair.Value.Count > 0);
                return occupiedCarriers switch
                {
                    3 => SpiritAttachmentPattern.OneOneOne,
                    2 => SpiritAttachmentPattern.TwoOne,
                    1 => SpiritAttachmentPattern.ThreeZero,
                    _ => SpiritAttachmentPattern.Partial,
                };
            }
        }

        public bool TryAttach(Guid spiritInstanceId, CarrierSlot carrier)
        {
            if (spiritInstanceId == Guid.Empty)
                return false;

            if (_bySpirit.TryGetValue(
                    spiritInstanceId,
                    out CarrierSlot currentCarrier))
            {
                if (currentCarrier == carrier)
                    return true;

                _byCarrier[currentCarrier].Remove(spiritInstanceId);
            }
            else if (Count >= MaxActiveSpirits)
            {
                return false;
            }

            if (!_byCarrier.TryGetValue(carrier, out List<Guid> spirits))
            {
                spirits = new List<Guid>();
                _byCarrier.Add(carrier, spirits);
            }

            spirits.Add(spiritInstanceId);
            _bySpirit[spiritInstanceId] = carrier;
            return true;
        }

        public bool Detach(Guid spiritInstanceId)
        {
            if (!_bySpirit.TryGetValue(
                    spiritInstanceId,
                    out CarrierSlot carrier))
            {
                return false;
            }

            _bySpirit.Remove(spiritInstanceId);
            List<Guid> spirits = _byCarrier[carrier];
            spirits.Remove(spiritInstanceId);
            if (spirits.Count == 0)
                _byCarrier.Remove(carrier);
            return true;
        }

        public bool TryGetCarrier(
            Guid spiritInstanceId,
            out CarrierSlot carrier)
        {
            return _bySpirit.TryGetValue(spiritInstanceId, out carrier);
        }

        public IReadOnlyList<Guid> GetSpirits(CarrierSlot carrier)
        {
            return _byCarrier.TryGetValue(carrier, out List<Guid> spirits)
                ? spirits.ToArray()
                : Array.Empty<Guid>();
        }
    }
}
