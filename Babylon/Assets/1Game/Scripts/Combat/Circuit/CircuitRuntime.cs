using System;
using System.Collections.Generic;

namespace XianTu
{
    public readonly struct CircuitEmission
    {
        public CircuitEventKind Kind { get; }
        public CircuitEntityRef Source { get; }
        public ElementTag Element { get; }
        public int TagMask { get; }

        public CircuitEmission(
            CircuitEventKind kind,
            in CircuitEntityRef source,
            ElementTag element,
            int tagMask)
        {
            if (kind == CircuitEventKind.Unknown)
                throw new ArgumentException(
                    "Circuit emission kind cannot be unknown.",
                    nameof(kind));

            Kind = kind;
            Source = source;
            Element = element;
            TagMask = tagMask;
        }
    }

    public interface ICircuitRule
    {
        StableConfigId RuleId { get; }
        Guid OwnerSpiritId { get; }
        bool Matches(in CircuitEvent input);
        CircuitEmission Emit(in CircuitEvent input);
    }

    public interface ICircuitEventSink
    {
        void Record(in CircuitEvent circuitEvent);
    }

    public readonly struct CircuitExecutionLimits
    {
        public int MaxDepth { get; }
        public int MaxSameRulePerChain { get; }
        public int MaxSameKindPerChain { get; }
        public int MaxEventsPerFrame { get; }
        public float RuleIntervalSeconds { get; }

        public CircuitExecutionLimits(
            int maxDepth,
            int maxSameRulePerChain,
            int maxEventsPerFrame,
            float ruleIntervalSeconds,
            int maxSameKindPerChain = 8)
        {
            if (maxDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            if (maxSameRulePerChain < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxSameRulePerChain));
            if (maxEventsPerFrame < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxEventsPerFrame));
            if (maxSameKindPerChain < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxSameKindPerChain));
            if (ruleIntervalSeconds < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(ruleIntervalSeconds));

            MaxDepth = maxDepth;
            MaxSameRulePerChain = maxSameRulePerChain;
            MaxSameKindPerChain = maxSameKindPerChain;
            MaxEventsPerFrame = maxEventsPerFrame;
            RuleIntervalSeconds = ruleIntervalSeconds;
        }

        public static CircuitExecutionLimits Default =>
            new(8, 2, 64, 0.1f);
    }

    public readonly struct CircuitExecutionReport
    {
        public int ProcessedEvents { get; }
        public int EmittedEvents { get; }
        public int DepthBlocks { get; }
        public int RepeatBlocks { get; }
        public int KindBlocks { get; }
        public int FrequencyBlocks { get; }
        public bool BudgetExhausted { get; }

        public CircuitExecutionReport(
            int processedEvents,
            int emittedEvents,
            int depthBlocks,
            int repeatBlocks,
            int kindBlocks,
            int frequencyBlocks,
            bool budgetExhausted)
        {
            ProcessedEvents = processedEvents;
            EmittedEvents = emittedEvents;
            DepthBlocks = depthBlocks;
            RepeatBlocks = repeatBlocks;
            KindBlocks = kindBlocks;
            FrequencyBlocks = frequencyBlocks;
            BudgetExhausted = budgetExhausted;
        }
    }

    internal readonly struct CircuitRuleKey :
        IEquatable<CircuitRuleKey>
    {
        public StableConfigId RuleId { get; }
        public Guid OwnerSpiritId { get; }

        public CircuitRuleKey(
            StableConfigId ruleId,
            Guid ownerSpiritId)
        {
            RuleId = ruleId;
            OwnerSpiritId = ownerSpiritId;
        }

        public bool Equals(CircuitRuleKey other)
        {
            return RuleId == other.RuleId &&
                OwnerSpiritId == other.OwnerSpiritId;
        }

        public override bool Equals(object obj)
        {
            return obj is CircuitRuleKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuleId.GetHashCode() * 397) ^
                    OwnerSpiritId.GetHashCode();
            }
        }
    }

    /// <summary>
    /// 同步处理一条源／应／化因果链。所有子事件的链ID和深度由运行时
    /// 生成，规则不能绕过深度、重复、频率和单帧预算保护。
    /// </summary>
    public sealed class CircuitRuntime
    {
        private readonly CircuitExecutionLimits _limits;
        private readonly ICircuitEventSink _sink;
        private readonly List<ICircuitRule> _rules = new();
        private readonly HashSet<CircuitRuleKey> _registeredRules = new();
        private readonly Dictionary<CircuitRuleKey, float>
            _lastRuleTriggerTime = new();
        private int _acceptedEventsThisFrame;

        public CircuitRuntime(
            CircuitExecutionLimits limits,
            ICircuitEventSink sink = null)
        {
            if (limits.MaxDepth < 0 ||
                limits.MaxSameRulePerChain < 1 ||
                limits.MaxSameKindPerChain < 1 ||
                limits.MaxEventsPerFrame < 1 ||
                limits.RuleIntervalSeconds < 0f)
            {
                throw new ArgumentException(
                    "Circuit execution limits are not initialized.",
                    nameof(limits));
            }

            _limits = limits;
            _sink = sink;
        }

        public void Register(ICircuitRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));
            if (rule.RuleId.IsEmpty)
                throw new ArgumentException(
                    "Circuit rule ID cannot be empty.",
                    nameof(rule));

            var key = new CircuitRuleKey(
                rule.RuleId,
                rule.OwnerSpiritId);
            if (!_registeredRules.Add(key))
                throw new InvalidOperationException(
                    $"Circuit rule already registered: {rule.RuleId}");

            _rules.Add(rule);
        }

        public void BeginFrame()
        {
            _acceptedEventsThisFrame = 0;
        }

        public CircuitExecutionReport Execute(
            in CircuitEvent root,
            float currentTime)
        {
            var queue = new Queue<CircuitEvent>();
            var ruleCounts = new Dictionary<CircuitRuleKey, int>();
            var kindCounts = new Dictionary<CircuitEventKind, int>
            {
                [root.Kind] = 1
            };
            int processed = 0;
            int emitted = 0;
            int depthBlocks = 0;
            int repeatBlocks = 0;
            int kindBlocks = 0;
            int frequencyBlocks = 0;
            bool budgetExhausted = false;

            if (!TryAccept(root, queue))
            {
                return new CircuitExecutionReport(
                    0, 0, 0, 0, 0, 0, true);
            }

            while (queue.Count > 0 && !budgetExhausted)
            {
                CircuitEvent current = queue.Dequeue();
                processed++;

                foreach (ICircuitRule rule in _rules)
                {
                    if (!rule.Matches(current))
                        continue;

                    if (current.Depth >= _limits.MaxDepth)
                    {
                        depthBlocks++;
                        continue;
                    }

                    var key = new CircuitRuleKey(
                        rule.RuleId,
                        rule.OwnerSpiritId);
                    ruleCounts.TryGetValue(key, out int repeatCount);
                    if (repeatCount >= _limits.MaxSameRulePerChain)
                    {
                        repeatBlocks++;
                        continue;
                    }

                    if (_limits.RuleIntervalSeconds > 0f &&
                        _lastRuleTriggerTime.TryGetValue(
                            key,
                            out float lastTriggerTime) &&
                        currentTime - lastTriggerTime <
                            _limits.RuleIntervalSeconds)
                    {
                        frequencyBlocks++;
                        continue;
                    }

                    CircuitEmission emission = rule.Emit(current);
                    var child = new CircuitEvent(
                        current.CausalChainId,
                        current.Depth + 1,
                        emission.Kind,
                        emission.Source,
                        current.Source,
                        emission.Element,
                        emission.TagMask);
                    kindCounts.TryGetValue(
                        child.Kind,
                        out int kindCount);
                    if (kindCount >= _limits.MaxSameKindPerChain)
                    {
                        kindBlocks++;
                        continue;
                    }

                    if (!TryAccept(child, queue))
                    {
                        budgetExhausted = true;
                        break;
                    }

                    ruleCounts[key] = repeatCount + 1;
                    kindCounts[child.Kind] = kindCount + 1;
                    _lastRuleTriggerTime[key] = currentTime;
                    emitted++;
                }
            }

            return new CircuitExecutionReport(
                processed,
                emitted,
                depthBlocks,
                repeatBlocks,
                kindBlocks,
                frequencyBlocks,
                budgetExhausted);
        }

        private bool TryAccept(
            in CircuitEvent circuitEvent,
            Queue<CircuitEvent> queue)
        {
            if (_acceptedEventsThisFrame >= _limits.MaxEventsPerFrame)
                return false;

            _acceptedEventsThisFrame++;
            queue.Enqueue(circuitEvent);
            _sink?.Record(circuitEvent);
            return true;
        }
    }
}
