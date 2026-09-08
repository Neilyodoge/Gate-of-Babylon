using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum CombatOutcomeKind
    {
        Damage = 0,
        Healing = 1,
        Defense = 2,
        Resource = 3,
    }

    public readonly struct CircuitAttributionKey :
        IEquatable<CircuitAttributionKey>
    {
        public CombatOutcomeKind Outcome { get; }
        public Guid SpiritInstanceId { get; }
        public CarrierSlot? Carrier { get; }
        public StableConfigId EntityConfigId { get; }
        public StableConfigId SpiritConfigId { get; }
        public ElementTag Element { get; }

        public CircuitAttributionKey(
            CombatOutcomeKind outcome,
            in CircuitEntityRef source,
            ElementTag element)
        {
            Outcome = outcome;
            SpiritInstanceId = source.SpiritInstanceId;
            Carrier = source.Carrier;
            EntityConfigId = source.EntityConfigId;
            SpiritConfigId = source.SpiritConfigId;
            Element = element;
        }

        public bool Equals(CircuitAttributionKey other)
        {
            return Outcome == other.Outcome &&
                SpiritInstanceId == other.SpiritInstanceId &&
                Carrier == other.Carrier &&
                EntityConfigId == other.EntityConfigId &&
                SpiritConfigId == other.SpiritConfigId &&
                Element == other.Element;
        }

        public override bool Equals(object obj)
        {
            return obj is CircuitAttributionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Outcome;
                hash = hash * 397 ^ SpiritInstanceId.GetHashCode();
                hash = hash * 397 ^ Carrier.GetHashCode();
                hash = hash * 397 ^ EntityConfigId.GetHashCode();
                hash = hash * 397 ^ SpiritConfigId.GetHashCode();
                hash = hash * 397 ^ (int)Element;
                return hash;
            }
        }
    }

    public readonly struct CircuitOutcomeRecord
    {
        public ulong CausalChainId { get; }
        public int Depth { get; }
        public CircuitAttributionKey Attribution { get; }
        public float Amount { get; }

        public CircuitOutcomeRecord(
            ulong causalChainId,
            int depth,
            in CircuitAttributionKey attribution,
            float amount)
        {
            CausalChainId = causalChainId;
            Depth = depth;
            Attribution = attribution;
            Amount = amount;
        }
    }

    /// <summary>
    /// 记录回路事件轨迹和显式战斗结果。结果必须携带具体CircuitEvent，
    /// 因此分支回路不会错误归到同链最后一个事件。
    /// </summary>
    public sealed class CircuitAttributionRecorder : ICircuitEventSink
    {
        private readonly Dictionary<ulong, List<CircuitEvent>> _traces =
            new();
        private readonly Dictionary<CircuitAttributionKey, float> _totals =
            new();
        private readonly List<CircuitOutcomeRecord> _outcomes = new();

        public IReadOnlyList<CircuitOutcomeRecord> Outcomes
            => _outcomes.AsReadOnly();

        public void Record(in CircuitEvent circuitEvent)
        {
            if (!_traces.TryGetValue(
                    circuitEvent.CausalChainId,
                    out List<CircuitEvent> trace))
            {
                trace = new List<CircuitEvent>();
                _traces.Add(circuitEvent.CausalChainId, trace);
            }

            trace.Add(circuitEvent);
        }

        public bool RecordOutcome(
            in CircuitEvent origin,
            CombatOutcomeKind outcome,
            float amount)
        {
            if (amount == 0f ||
                float.IsNaN(amount) ||
                float.IsInfinity(amount))
            {
                return false;
            }

            var key = new CircuitAttributionKey(
                outcome,
                origin.Source,
                origin.Element);
            _totals.TryGetValue(key, out float total);
            _totals[key] = total + amount;
            _outcomes.Add(new CircuitOutcomeRecord(
                origin.CausalChainId,
                origin.Depth,
                key,
                amount));
            return true;
        }

        public float GetTotal(in CircuitAttributionKey attribution)
        {
            return _totals.TryGetValue(attribution, out float total)
                ? total
                : 0f;
        }

        public IReadOnlyList<CircuitEvent> GetTrace(ulong causalChainId)
        {
            return _traces.TryGetValue(
                causalChainId,
                out List<CircuitEvent> trace)
                ? trace.ToArray()
                : Array.Empty<CircuitEvent>();
        }

        public void Reset()
        {
            _traces.Clear();
            _totals.Clear();
            _outcomes.Clear();
        }
    }

    public readonly struct ShadowMetricComparison
    {
        public StableConfigId MetricId { get; }
        public float LegacyValue { get; }
        public float CircuitValue { get; }
        public float Delta => CircuitValue - LegacyValue;
        public bool IsWithinTolerance { get; }

        public ShadowMetricComparison(
            StableConfigId metricId,
            float legacyValue,
            float circuitValue,
            bool isWithinTolerance)
        {
            MetricId = metricId;
            LegacyValue = legacyValue;
            CircuitValue = circuitValue;
            IsWithinTolerance = isWithinTolerance;
        }
    }

    /// <summary>
    /// Shadow模式只记录Legacy与新回路的同名指标，不参与实际结算。
    /// </summary>
    public sealed class CircuitShadowRecorder
    {
        private readonly Dictionary<StableConfigId, float> _legacy = new();
        private readonly Dictionary<StableConfigId, float> _circuit = new();

        public bool RecordLegacy(StableConfigId metricId, float value)
        {
            return Add(_legacy, metricId, value);
        }

        public bool RecordCircuit(StableConfigId metricId, float value)
        {
            return Add(_circuit, metricId, value);
        }

        public IReadOnlyList<ShadowMetricComparison> Compare(
            float absoluteTolerance,
            float relativeTolerance)
        {
            if (absoluteTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(absoluteTolerance));
            if (relativeTolerance < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(relativeTolerance));

            var metricIds = new HashSet<StableConfigId>(_legacy.Keys);
            metricIds.UnionWith(_circuit.Keys);
            var result = new List<ShadowMetricComparison>(metricIds.Count);
            foreach (StableConfigId metricId in metricIds)
            {
                _legacy.TryGetValue(metricId, out float legacyValue);
                _circuit.TryGetValue(metricId, out float circuitValue);
                float tolerance = Math.Max(
                    absoluteTolerance,
                    Math.Abs(legacyValue) * relativeTolerance);
                result.Add(new ShadowMetricComparison(
                    metricId,
                    legacyValue,
                    circuitValue,
                    Math.Abs(circuitValue - legacyValue) <= tolerance));
            }

            result.Sort((left, right) => string.CompareOrdinal(
                left.MetricId.Value,
                right.MetricId.Value));
            return result;
        }

        public void Reset()
        {
            _legacy.Clear();
            _circuit.Clear();
        }

        private static bool Add(
            Dictionary<StableConfigId, float> target,
            StableConfigId metricId,
            float value)
        {
            if (metricId.IsEmpty ||
                float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                return false;
            }

            target.TryGetValue(metricId, out float total);
            target[metricId] = total + value;
            return true;
        }
    }
}
