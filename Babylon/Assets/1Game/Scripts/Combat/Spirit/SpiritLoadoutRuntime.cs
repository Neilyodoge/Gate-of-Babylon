using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum SpiritLoadoutChangeResult
    {
        Success = 0,
        NoChange = 1,
        BlockedInCombat = 2,
        InvalidSpirit = 3,
        DuplicateSpirit = 4,
        RosterFull = 5,
    }

    /// <summary>
    /// 本局三灵编队及附着门面。领域状态由器灵实例持有，
    /// 本类只管理出战成员和脱战迁灵规则。
    /// </summary>
    public sealed class SpiritLoadoutRuntime
    {
        private readonly Dictionary<Guid, SpiritInstanceState> _activeSpirits =
            new();
        private readonly SpiritAttachmentLayout _layout = new();

        public int Count => _activeSpirits.Count;
        public SpiritAttachmentPattern Pattern => _layout.Pattern;
        public IReadOnlyCollection<SpiritInstanceState> ActiveSpirits
            => _activeSpirits.Values;

        public SpiritLoadoutChangeResult Add(
            SpiritInstanceState spirit,
            CarrierSlot initialCarrier)
        {
            if (spirit == null ||
                spirit.Identity.InstanceId == Guid.Empty)
            {
                return SpiritLoadoutChangeResult.InvalidSpirit;
            }

            Guid instanceId = spirit.Identity.InstanceId;
            if (_activeSpirits.ContainsKey(instanceId))
                return SpiritLoadoutChangeResult.DuplicateSpirit;
            if (Count >= SpiritAttachmentLayout.MaxActiveSpirits)
                return SpiritLoadoutChangeResult.RosterFull;
            if (!_layout.TryAttach(instanceId, initialCarrier))
                return SpiritLoadoutChangeResult.InvalidSpirit;

            _activeSpirits.Add(instanceId, spirit);
            return SpiritLoadoutChangeResult.Success;
        }

        public SpiritLoadoutChangeResult Migrate(
            Guid spiritInstanceId,
            CarrierSlot targetCarrier,
            bool isInCombat)
        {
            if (!_activeSpirits.ContainsKey(spiritInstanceId))
                return SpiritLoadoutChangeResult.InvalidSpirit;
            if (_layout.TryGetCarrier(
                    spiritInstanceId,
                    out CarrierSlot currentCarrier) &&
                currentCarrier == targetCarrier)
            {
                return SpiritLoadoutChangeResult.NoChange;
            }
            if (isInCombat)
                return SpiritLoadoutChangeResult.BlockedInCombat;

            return _layout.TryAttach(spiritInstanceId, targetCarrier)
                ? SpiritLoadoutChangeResult.Success
                : SpiritLoadoutChangeResult.InvalidSpirit;
        }

        public SpiritLoadoutChangeResult Remove(
            Guid spiritInstanceId,
            bool isInCombat)
        {
            if (!_activeSpirits.ContainsKey(spiritInstanceId))
                return SpiritLoadoutChangeResult.InvalidSpirit;
            if (isInCombat)
                return SpiritLoadoutChangeResult.BlockedInCombat;

            _layout.Detach(spiritInstanceId);
            _activeSpirits.Remove(spiritInstanceId);
            return SpiritLoadoutChangeResult.Success;
        }

        public bool TryGetSpirit(
            Guid spiritInstanceId,
            out SpiritInstanceState spirit)
        {
            return _activeSpirits.TryGetValue(spiritInstanceId, out spirit);
        }

        public bool TryGetCarrier(
            Guid spiritInstanceId,
            out CarrierSlot carrier)
        {
            return _layout.TryGetCarrier(spiritInstanceId, out carrier);
        }

        public IReadOnlyList<Guid> GetSpirits(CarrierSlot carrier)
        {
            return _layout.GetSpirits(carrier);
        }
    }
}
