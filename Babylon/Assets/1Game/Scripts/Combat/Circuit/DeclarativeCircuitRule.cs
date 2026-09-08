using System;

namespace XianTu
{
    /// <summary>
    /// 不执行任意代码的源／应／化规则数据；正式内容可由配表构建该定义。
    /// </summary>
    public readonly struct DeclarativeCircuitRuleDefinition
    {
        public StableConfigId RuleId { get; }
        public CircuitEventKind InputKind { get; }
        public CircuitEventKind OutputKind { get; }
        public int RequiredTagMask { get; }
        public int ForbiddenTagMask { get; }
        public ElementTag? RequiredElement { get; }
        public int OutputTagMask { get; }
        public ElementTag OutputElement { get; }
        public bool PreserveInputTags { get; }
        public bool PreserveInputElement { get; }

        public DeclarativeCircuitRuleDefinition(
            StableConfigId ruleId,
            CircuitEventKind inputKind,
            CircuitEventKind outputKind,
            int requiredTagMask,
            int forbiddenTagMask,
            ElementTag? requiredElement,
            int outputTagMask,
            ElementTag outputElement,
            bool preserveInputTags,
            bool preserveInputElement)
        {
            if (ruleId.IsEmpty)
                throw new ArgumentException(
                    "Circuit rule ID cannot be empty.",
                    nameof(ruleId));
            if (inputKind == CircuitEventKind.Unknown)
                throw new ArgumentException(
                    "Input event kind cannot be unknown.",
                    nameof(inputKind));
            if (outputKind == CircuitEventKind.Unknown)
                throw new ArgumentException(
                    "Output event kind cannot be unknown.",
                    nameof(outputKind));
            if ((requiredTagMask & forbiddenTagMask) != 0)
                throw new ArgumentException(
                    "Required and forbidden tag masks overlap.");

            RuleId = ruleId;
            InputKind = inputKind;
            OutputKind = outputKind;
            RequiredTagMask = requiredTagMask;
            ForbiddenTagMask = forbiddenTagMask;
            RequiredElement = requiredElement;
            OutputTagMask = outputTagMask;
            OutputElement = outputElement;
            PreserveInputTags = preserveInputTags;
            PreserveInputElement = preserveInputElement;
        }
    }

    public sealed class DeclarativeCircuitRule : ICircuitRule
    {
        private readonly DeclarativeCircuitRuleDefinition _definition;
        private readonly CircuitEntityRef _source;

        public StableConfigId RuleId => _definition.RuleId;
        public Guid OwnerSpiritId => _source.SpiritInstanceId;

        internal DeclarativeCircuitRule(
            in DeclarativeCircuitRuleDefinition definition,
            in CircuitEntityRef source)
        {
            if (source.SpiritInstanceId == Guid.Empty)
                throw new ArgumentException(
                    "A bound circuit rule requires a spirit instance.",
                    nameof(source));

            _definition = definition;
            _source = source;
        }

        public bool Matches(in CircuitEvent input)
        {
            if (input.Kind != _definition.InputKind)
                return false;
            if ((input.TagMask & _definition.RequiredTagMask) !=
                _definition.RequiredTagMask)
            {
                return false;
            }
            if ((input.TagMask & _definition.ForbiddenTagMask) != 0)
                return false;
            if (_definition.RequiredElement.HasValue &&
                input.Element != _definition.RequiredElement.Value)
            {
                return false;
            }

            return true;
        }

        public CircuitEmission Emit(in CircuitEvent input)
        {
            int tagMask = _definition.PreserveInputTags
                ? input.TagMask | _definition.OutputTagMask
                : _definition.OutputTagMask;
            ElementTag element = _definition.PreserveInputElement
                ? input.Element
                : _definition.OutputElement;
            return new CircuitEmission(
                _definition.OutputKind,
                _source,
                element,
                tagMask);
        }

        public CircuitRuleDescriptor Describe()
        {
            var rule = new CircuitRuleRef(
                RuleId,
                OwnerSpiritId);
            return new CircuitRuleDescriptor(
                rule,
                _source.Carrier,
                _source.EntityConfigId,
                _source.SpiritConfigId,
                _definition);
        }
    }

    public static class SpiritCircuitRuleBinder
    {
        public static DeclarativeCircuitRule Bind(
            in DeclarativeCircuitRuleDefinition definition,
            SpiritInstanceState spirit,
            CarrierSlot carrier,
            int entityId,
            StableConfigId entityConfigId)
        {
            if (spirit == null)
                throw new ArgumentNullException(nameof(spirit));

            var source = new CircuitEntityRef(
                entityId,
                carrier,
                spirit.Identity.InstanceId,
                entityConfigId,
                spirit.Identity.SpeciesConfigId);
            return new DeclarativeCircuitRule(definition, source);
        }
    }
}
