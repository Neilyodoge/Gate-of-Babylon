using System;
using System.Collections.Generic;

namespace XianTu
{
    public readonly struct CircuitRuleRef : IEquatable<CircuitRuleRef>
    {
        public StableConfigId RuleId { get; }
        public Guid SpiritInstanceId { get; }

        public CircuitRuleRef(
            StableConfigId ruleId,
            Guid spiritInstanceId)
        {
            RuleId = ruleId;
            SpiritInstanceId = spiritInstanceId;
        }

        public bool Equals(CircuitRuleRef other)
        {
            return RuleId == other.RuleId &&
                SpiritInstanceId == other.SpiritInstanceId;
        }

        public override bool Equals(object obj)
        {
            return obj is CircuitRuleRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuleId.GetHashCode() * 397) ^
                    SpiritInstanceId.GetHashCode();
            }
        }
    }

    public readonly struct CircuitRuleDescriptor
    {
        public CircuitRuleRef Rule { get; }
        public CarrierSlot? Carrier { get; }
        public StableConfigId CarrierConfigId { get; }
        public StableConfigId SpiritConfigId { get; }
        public CircuitEventKind InputKind { get; }
        public CircuitEventKind OutputKind { get; }
        public int RequiredTagMask { get; }
        public int ForbiddenTagMask { get; }
        public ElementTag? RequiredElement { get; }
        public int OutputTagMask { get; }
        public ElementTag OutputElement { get; }
        public bool PreserveInputTags { get; }
        public bool PreserveInputElement { get; }

        public CircuitRuleDescriptor(
            in CircuitRuleRef rule,
            CarrierSlot? carrier,
            StableConfigId carrierConfigId,
            StableConfigId spiritConfigId,
            in DeclarativeCircuitRuleDefinition definition)
        {
            Rule = rule;
            Carrier = carrier;
            CarrierConfigId = carrierConfigId;
            SpiritConfigId = spiritConfigId;
            InputKind = definition.InputKind;
            OutputKind = definition.OutputKind;
            RequiredTagMask = definition.RequiredTagMask;
            ForbiddenTagMask = definition.ForbiddenTagMask;
            RequiredElement = definition.RequiredElement;
            OutputTagMask = definition.OutputTagMask;
            OutputElement = definition.OutputElement;
            PreserveInputTags = definition.PreserveInputTags;
            PreserveInputElement = definition.PreserveInputElement;
        }
    }

    public enum CircuitConnectionCertainty
    {
        Possible = 0,
        Guaranteed = 1,
    }

    public readonly struct CircuitPreviewEdge
    {
        public CircuitRuleRef From { get; }
        public CircuitRuleRef To { get; }
        public CircuitConnectionCertainty Certainty { get; }

        public CircuitPreviewEdge(
            in CircuitRuleRef from,
            in CircuitRuleRef to,
            CircuitConnectionCertainty certainty)
        {
            From = from;
            To = to;
            Certainty = certainty;
        }
    }

    public sealed class CircuitPreviewGraph
    {
        public IReadOnlyList<CircuitRuleDescriptor> Rules { get; }
        public IReadOnlyList<CircuitPreviewEdge> Edges { get; }
        public IReadOnlyList<CircuitRuleRef> InputBreakpoints { get; }
        public IReadOnlyList<CircuitRuleRef> OutputBreakpoints { get; }
        public bool HasCycle { get; }

        internal CircuitPreviewGraph(
            CircuitRuleDescriptor[] rules,
            CircuitPreviewEdge[] edges,
            CircuitRuleRef[] inputBreakpoints,
            CircuitRuleRef[] outputBreakpoints,
            bool hasCycle)
        {
            Rules = rules;
            Edges = edges;
            InputBreakpoints = inputBreakpoints;
            OutputBreakpoints = outputBreakpoints;
            HasCycle = hasCycle;
        }
    }

    public static class CircuitPreviewGraphBuilder
    {
        public static CircuitPreviewGraph Build(
            IEnumerable<DeclarativeCircuitRule> rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var descriptors = new List<CircuitRuleDescriptor>();
            var knownRules = new HashSet<CircuitRuleRef>();
            foreach (DeclarativeCircuitRule rule in rules)
            {
                if (rule == null)
                    throw new ArgumentException(
                        "Circuit preview cannot contain a null rule.",
                        nameof(rules));

                CircuitRuleDescriptor descriptor = rule.Describe();
                if (!knownRules.Add(descriptor.Rule))
                    throw new InvalidOperationException(
                        $"Duplicate circuit rule: {descriptor.Rule.RuleId}");
                descriptors.Add(descriptor);
            }

            var edges = new List<CircuitPreviewEdge>();
            var incoming = new HashSet<CircuitRuleRef>();
            var outgoing = new HashSet<CircuitRuleRef>();
            for (int fromIndex = 0; fromIndex < descriptors.Count; fromIndex++)
            {
                for (int toIndex = 0; toIndex < descriptors.Count; toIndex++)
                {
                    if (!TryGetCertainty(
                            descriptors[fromIndex],
                            descriptors[toIndex],
                            out CircuitConnectionCertainty certainty))
                    {
                        continue;
                    }

                    var edge = new CircuitPreviewEdge(
                        descriptors[fromIndex].Rule,
                        descriptors[toIndex].Rule,
                        certainty);
                    edges.Add(edge);
                    outgoing.Add(edge.From);
                    incoming.Add(edge.To);
                }
            }

            var inputBreakpoints = new List<CircuitRuleRef>();
            var outputBreakpoints = new List<CircuitRuleRef>();
            foreach (CircuitRuleDescriptor descriptor in descriptors)
            {
                if (descriptor.InputKind != CircuitEventKind.Source &&
                    !incoming.Contains(descriptor.Rule))
                {
                    inputBreakpoints.Add(descriptor.Rule);
                }
                if (!outgoing.Contains(descriptor.Rule))
                    outputBreakpoints.Add(descriptor.Rule);
            }

            return new CircuitPreviewGraph(
                descriptors.ToArray(),
                edges.ToArray(),
                inputBreakpoints.ToArray(),
                outputBreakpoints.ToArray(),
                HasCycle(descriptors, edges));
        }

        private static bool TryGetCertainty(
            in CircuitRuleDescriptor from,
            in CircuitRuleDescriptor to,
            out CircuitConnectionCertainty certainty)
        {
            certainty = CircuitConnectionCertainty.Guaranteed;
            if (from.OutputKind != to.InputKind)
                return false;
            if ((from.OutputTagMask & to.ForbiddenTagMask) != 0)
                return false;

            int missingRequiredTags =
                to.RequiredTagMask & ~from.OutputTagMask;
            if (missingRequiredTags != 0)
            {
                if (!from.PreserveInputTags)
                    return false;
                certainty = CircuitConnectionCertainty.Possible;
            }
            if (from.PreserveInputTags && to.ForbiddenTagMask != 0)
                certainty = CircuitConnectionCertainty.Possible;

            if (to.RequiredElement.HasValue)
            {
                if (from.PreserveInputElement)
                {
                    certainty = CircuitConnectionCertainty.Possible;
                }
                else if (from.OutputElement != to.RequiredElement.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCycle(
            IReadOnlyList<CircuitRuleDescriptor> descriptors,
            IReadOnlyList<CircuitPreviewEdge> edges)
        {
            var adjacency =
                new Dictionary<CircuitRuleRef, List<CircuitRuleRef>>();
            foreach (CircuitRuleDescriptor descriptor in descriptors)
                adjacency[descriptor.Rule] = new List<CircuitRuleRef>();
            foreach (CircuitPreviewEdge edge in edges)
                adjacency[edge.From].Add(edge.To);

            var states = new Dictionary<CircuitRuleRef, int>();
            foreach (CircuitRuleDescriptor descriptor in descriptors)
            {
                if (Visit(descriptor.Rule, adjacency, states))
                    return true;
            }

            return false;
        }

        private static bool Visit(
            CircuitRuleRef current,
            IReadOnlyDictionary<CircuitRuleRef, List<CircuitRuleRef>> adjacency,
            IDictionary<CircuitRuleRef, int> states)
        {
            states.TryGetValue(current, out int state);
            if (state == 1)
                return true;
            if (state == 2)
                return false;

            states[current] = 1;
            foreach (CircuitRuleRef next in adjacency[current])
            {
                if (Visit(next, adjacency, states))
                    return true;
            }

            states[current] = 2;
            return false;
        }
    }
}
