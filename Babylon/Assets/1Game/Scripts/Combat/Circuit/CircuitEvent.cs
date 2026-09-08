using System;

namespace XianTu
{
    public enum CircuitEventKind
    {
        Unknown = 0,
        Source = 1,
        Response = 2,
        Transform = 3,
    }

    /// <summary>回路事件中的稳定来源引用；空GUID表示没有器灵实例。</summary>
    public readonly struct CircuitEntityRef
    {
        public int EntityId { get; }
        public CarrierSlot? Carrier { get; }
        public Guid SpiritInstanceId { get; }
        public StableConfigId EntityConfigId { get; }
        public StableConfigId SpiritConfigId { get; }

        public CircuitEntityRef(int entityId, CarrierSlot? carrier, Guid spiritInstanceId)
            : this(
                entityId,
                carrier,
                spiritInstanceId,
                default,
                default)
        {
        }

        public CircuitEntityRef(
            int entityId,
            CarrierSlot? carrier,
            Guid spiritInstanceId,
            StableConfigId entityConfigId,
            StableConfigId spiritConfigId)
        {
            EntityId = entityId;
            Carrier = carrier;
            SpiritInstanceId = spiritInstanceId;
            EntityConfigId = entityConfigId;
            SpiritConfigId = spiritConfigId;
        }
    }

    /// <summary>
    /// ProjectR回路的只读事件载荷。P0不发布该事件，仅冻结来源、标签和因果字段。
    /// </summary>
    public readonly struct CircuitEvent
    {
        public ulong CausalChainId { get; }
        public int Depth { get; }
        public CircuitEventKind Kind { get; }
        public CircuitEntityRef Source { get; }
        public CircuitEntityRef Instigator { get; }
        public ElementTag Element { get; }
        public int TagMask { get; }

        public CircuitEvent(
            ulong causalChainId,
            int depth,
            CircuitEventKind kind,
            in CircuitEntityRef source,
            in CircuitEntityRef instigator,
            ElementTag element,
            int tagMask)
        {
            if (depth < 0)
                throw new ArgumentOutOfRangeException(nameof(depth), "Circuit depth cannot be negative.");

            CausalChainId = causalChainId;
            Depth = depth;
            Kind = kind;
            Source = source;
            Instigator = instigator;
            Element = element;
            TagMask = tagMask;
        }
    }
}
