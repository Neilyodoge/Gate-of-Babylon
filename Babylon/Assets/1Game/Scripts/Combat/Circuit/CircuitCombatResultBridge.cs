using System;

namespace XianTu
{
    public enum CombatResultSource
    {
        Legacy = 0,
        Circuit = 1,
    }

    public readonly struct StructuredCombatResult
    {
        public CombatResultSource Source { get; }
        public StableConfigId MetricId { get; }
        public CombatOutcomeKind Outcome { get; }
        public float RequestedAmount { get; }
        public float AppliedAmount { get; }
        public CircuitEntityRef Target { get; }
        public CircuitEvent? Origin { get; }

        private StructuredCombatResult(
            CombatResultSource source,
            StableConfigId metricId,
            CombatOutcomeKind outcome,
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target,
            CircuitEvent? origin)
        {
            if (metricId.IsEmpty)
                throw new ArgumentException(
                    "Combat result metric ID cannot be empty.",
                    nameof(metricId));
            if (!IsFinite(requestedAmount))
                throw new ArgumentOutOfRangeException(
                    nameof(requestedAmount));
            if (!IsFinite(appliedAmount))
                throw new ArgumentOutOfRangeException(
                    nameof(appliedAmount));
            if (source == CombatResultSource.Circuit &&
                !origin.HasValue)
            {
                throw new ArgumentException(
                    "Circuit combat results require an origin event.",
                    nameof(origin));
            }

            Source = source;
            MetricId = metricId;
            Outcome = outcome;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            Target = target;
            Origin = origin;
        }

        public static StructuredCombatResult FromLegacy(
            StableConfigId metricId,
            CombatOutcomeKind outcome,
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target)
        {
            return new StructuredCombatResult(
                CombatResultSource.Legacy,
                metricId,
                outcome,
                requestedAmount,
                appliedAmount,
                target,
                null);
        }

        public static StructuredCombatResult FromCircuit(
            StableConfigId metricId,
            CombatOutcomeKind outcome,
            float requestedAmount,
            float appliedAmount,
            in CircuitEntityRef target,
            in CircuitEvent origin)
        {
            return new StructuredCombatResult(
                CombatResultSource.Circuit,
                metricId,
                outcome,
                requestedAmount,
                appliedAmount,
                target,
                origin);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public interface IStructuredCombatResultSink
    {
        void Record(in StructuredCombatResult result);
    }

    /// <summary>
    /// 将已完成的战斗结算旁路写入来源归因与Shadow指标。
    /// 本桥不计算、不修改伤害和资源数值。
    /// </summary>
    public sealed class CircuitCombatResultBridge
    {
        private readonly CircuitAttributionRecorder _attribution;
        private readonly CircuitShadowRecorder _shadow;
        private readonly IStructuredCombatResultSink _sink;

        public CircuitCombatResultBridge(
            CircuitAttributionRecorder attribution,
            CircuitShadowRecorder shadow,
            IStructuredCombatResultSink sink = null)
        {
            _attribution = attribution ??
                throw new ArgumentNullException(nameof(attribution));
            _shadow = shadow ??
                throw new ArgumentNullException(nameof(shadow));
            _sink = sink;
        }

        public void Record(in StructuredCombatResult result)
        {
            if (result.Source == CombatResultSource.Circuit)
            {
                CircuitEvent origin = result.Origin.Value;
                _attribution.RecordOutcome(
                    origin,
                    result.Outcome,
                    result.AppliedAmount);
                _shadow.RecordCircuit(
                    result.MetricId,
                    result.AppliedAmount);
            }
            else
            {
                _shadow.RecordLegacy(
                    result.MetricId,
                    result.AppliedAmount);
            }

            _sink?.Record(result);
        }
    }
}
