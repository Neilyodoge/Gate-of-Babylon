using System;
using System.Collections.Generic;

namespace XianTu
{
    public static class FirstSpiritCircuitContent
    {
        public static readonly StableConfigId SparkRaccoonSpecies =
            new("pet.spark-raccoon");
        public static readonly StableConfigId EchoOwlSpecies =
            new("pet.echo-owl");
        public static readonly StableConfigId BounceGelSpecies =
            new("pet.bounce-gel");
        public static readonly StableConfigId EchoDamageMetric =
            new("combat.player.damage.first-pet.echo");
        public static readonly StableConfigId BounceDamageMetric =
            new("combat.player.damage.first-pet.bounce");
        public static readonly StableConfigId SparkShardDamageMetric =
            new("combat.player.damage.first-pet.spark-shard");
        public static readonly StableConfigId DuetDamageMetric =
            new("combat.player.damage.first-pet.duet");
        public static readonly StableConfigId SoftLandingDamageMetric =
            new("combat.player.damage.first-pet.soft-landing");
        public static readonly StableConfigId FireTrailDamageMetric =
            new("combat.player.damage.first-pet.fire-trail");
        public static readonly StableConfigId WrappedSparkDamageMetric =
            new("combat.player.damage.first-pet.wrapped-spark");
        public static readonly StableConfigId ReturnStartDamageMetric =
            new("combat.player.damage.first-pet.return-start");
        public static readonly StableConfigId ChainBurnDamageMetric =
            new("combat.player.damage.first-pet.chain-burn");
        public static readonly StableConfigId BurstCoreDamageMetric =
            new("combat.player.damage.first-pet.burst-core");
        public static readonly StableConfigId CarriedEmberDamageMetric =
            new("combat.player.damage.first-pet.carried-ember");
        public static readonly StableConfigId ValleyChorusDamageMetric =
            new("combat.player.damage.first-pet.valley-chorus");
        public static readonly StableConfigId SavedLineDamageMetric =
            new("combat.player.damage.first-pet.saved-line");
        public static readonly StableConfigId BigCraterDamageMetric =
            new("combat.player.damage.first-pet.big-crater");

        public const int HeatReadyTag = 1 << 0;
        public const int SparkTag = 1 << 1;
        public const int EchoAttackTag = 1 << 2;
        public const int BounceAttackTag = 1 << 3;
        public const int HeatRefundTag = 1 << 4;
        public const float EchoDamageMultiplier = 0.45f;
        public const float BounceDamageMultiplier = 0.7f;
        public const float TargetSearchRadius = 8f;
        public const float ProjectileSpeed = 16f;

        public static readonly DeclarativeCircuitRuleDefinition
            ProduceSpark = new(
                new StableConfigId(
                    "pet.spark-raccoon.produce-spark"),
                CircuitEventKind.Source,
                CircuitEventKind.Source,
                HeatReadyTag,
                0,
                null,
                SparkTag,
                ElementTag.Fire,
                false,
                false);

        public static readonly DeclarativeCircuitRuleDefinition
            TriggerEchoAttack = new(
                new StableConfigId(
                    "pet.echo-owl.trigger-echo"),
                CircuitEventKind.Source,
                CircuitEventKind.Response,
                SparkTag,
                0,
                null,
                EchoAttackTag,
                ElementTag.None,
                false,
                true);

        public static readonly DeclarativeCircuitRuleDefinition
            ModifyEchoToBounce = new(
                new StableConfigId(
                    "pet.bounce-gel.modify-bounce"),
                CircuitEventKind.Response,
                CircuitEventKind.Transform,
                EchoAttackTag,
                0,
                null,
                BounceAttackTag | HeatRefundTag,
                ElementTag.None,
                false,
                true);
    }

    public readonly struct FirstSpiritCircuitTuning
    {
        public int DirectHitsPerSpark { get; }
        public float EchoDamageMultiplier { get; }
        public float BounceDamageMultiplier { get; }
        public float TargetSearchRadius { get; }
        public float ProjectileSpeed { get; }
        public int HeatRefund { get; }
        public int RetainedHeatOnSpark { get; }
        public float SparkShardDamageMultiplier { get; }
        public float DuetDamageMultiplier { get; }
        public float EchoMarkDuration { get; }
        public int MaxProjectileTargets { get; }
        public float ProjectileScale { get; }
        public float SoftLandingDamageMultiplier { get; }
        public float BounceDelay { get; }
        public float InitialEchoDelay { get; }
        public int EchoHitHeatRefund { get; }
        public float FireTrailDamageMultiplier { get; }
        public bool ThreefoldConfirm { get; }
        public bool EchoJump { get; }
        public float WrappedSparkDamageMultiplier { get; }
        public float ReturnStartDamageMultiplier { get; }
        public bool ChainBurn { get; }
        public bool BurstCore { get; }
        public bool CarriedEmber { get; }
        public bool ValleyChorus { get; }
        public bool OnlyThePoint { get; }
        public bool SavedLine { get; }
        public bool Pinball { get; }
        public bool BigCrater { get; }
        public bool AcceleratingBounce { get; }

        public FirstSpiritCircuitTuning(
            int directHitsPerSpark,
            float echoDamageMultiplier,
            float bounceDamageMultiplier,
            float targetSearchRadius,
            float projectileSpeed,
            int heatRefund,
            int retainedHeatOnSpark = 0,
            float sparkShardDamageMultiplier = 0f,
            float duetDamageMultiplier = 0f,
            float echoMarkDuration = 0f,
            int maxProjectileTargets = 2,
            float projectileScale = 0.22f,
            float softLandingDamageMultiplier = 0f,
            float bounceDelay = 0f,
            float initialEchoDelay = 0f,
            int echoHitHeatRefund = 0,
            float fireTrailDamageMultiplier = 0f,
            bool threefoldConfirm = false,
            bool echoJump = false,
            float wrappedSparkDamageMultiplier = 0f,
            float returnStartDamageMultiplier = 0f,
            bool chainBurn = false,
            bool burstCore = false,
            bool carriedEmber = false,
            bool valleyChorus = false,
            bool onlyThePoint = false,
            bool savedLine = false,
            bool pinball = false,
            bool bigCrater = false,
            bool acceleratingBounce = false)
        {
            DirectHitsPerSpark = Math.Max(1, directHitsPerSpark);
            EchoDamageMultiplier = Math.Max(
                0f,
                echoDamageMultiplier);
            BounceDamageMultiplier = Math.Max(
                0f,
                bounceDamageMultiplier);
            TargetSearchRadius = Math.Max(0.1f, targetSearchRadius);
            ProjectileSpeed = Math.Max(0.1f, projectileSpeed);
            HeatRefund = Math.Max(0, heatRefund);
            RetainedHeatOnSpark = Math.Max(0, retainedHeatOnSpark);
            SparkShardDamageMultiplier = Math.Max(
                0f,
                sparkShardDamageMultiplier);
            DuetDamageMultiplier = Math.Max(0f, duetDamageMultiplier);
            EchoMarkDuration = Math.Max(0f, echoMarkDuration);
            MaxProjectileTargets = Math.Max(1, maxProjectileTargets);
            ProjectileScale = Math.Max(0.05f, projectileScale);
            SoftLandingDamageMultiplier = Math.Max(
                0f,
                softLandingDamageMultiplier);
            BounceDelay = Math.Max(0f, bounceDelay);
            InitialEchoDelay = Math.Max(0f, initialEchoDelay);
            EchoHitHeatRefund = Math.Max(0, echoHitHeatRefund);
            FireTrailDamageMultiplier = Math.Max(
                0f,
                fireTrailDamageMultiplier);
            ThreefoldConfirm = threefoldConfirm;
            EchoJump = echoJump;
            WrappedSparkDamageMultiplier = Math.Max(
                0f,
                wrappedSparkDamageMultiplier);
            ReturnStartDamageMultiplier = Math.Max(
                0f,
                returnStartDamageMultiplier);
            ChainBurn = chainBurn;
            BurstCore = burstCore;
            CarriedEmber = carriedEmber;
            ValleyChorus = valleyChorus;
            OnlyThePoint = onlyThePoint;
            SavedLine = savedLine;
            Pinball = pinball;
            BigCrater = bigCrater;
            AcceleratingBounce = acceleratingBounce;
        }

        public static FirstSpiritCircuitTuning Default => new(
            FirstSpiritHeatRuntime.DirectHitsPerSpark,
            FirstSpiritCircuitContent.EchoDamageMultiplier,
            FirstSpiritCircuitContent.BounceDamageMultiplier,
            FirstSpiritCircuitContent.TargetSearchRadius,
            FirstSpiritCircuitContent.ProjectileSpeed,
            1);
    }

    public sealed class FirstSpiritHeatRuntime
    {
        public const int DirectHitsPerSpark = 3;
        private int _directHitsPerSpark = DirectHitsPerSpark;
        private int _retainedHeatOnSpark;
        public int Heat { get; private set; }

        public bool RegisterDirectHit()
        {
            Heat++;
            if (Heat < _directHitsPerSpark)
                return false;

            Heat = Math.Max(
                Heat - _directHitsPerSpark,
                _retainedHeatOnSpark);
            return true;
        }

        public void ApplyRefund(int amount)
        {
            if (amount <= 0)
                return;

            Heat = Math.Min(
                _directHitsPerSpark - 1,
                Heat + amount);
        }

        public void Configure(
            int directHitsPerSpark,
            int retainedHeatOnSpark = 0)
        {
            _directHitsPerSpark = Math.Max(1, directHitsPerSpark);
            _retainedHeatOnSpark = Math.Min(
                _directHitsPerSpark - 1,
                Math.Max(0, retainedHeatOnSpark));
            Heat = Math.Min(Heat, _directHitsPerSpark - 1);
        }

        public void Reset()
        {
            Heat = 0;
        }
    }

    public readonly struct FirstSpiritCircuitStep
    {
        public bool ChainStarted { get; }
        public int Heat { get; }
        public CircuitExecutionReport Report { get; }

        public FirstSpiritCircuitStep(
            bool chainStarted,
            int heat,
            in CircuitExecutionReport report)
        {
            ChainStarted = chainStarted;
            Heat = heat;
            Report = report;
        }
    }

    /// <summary>
    /// 首批三只灵宠的程序原型。玩家直接命中负责推进热度；
    /// 回路只负责产出火花、触发回声和改造成弹射。
    /// </summary>
    public sealed class FirstSpiritCircuitRuntime
    {
        private sealed class EventSink : ICircuitEventSink
        {
            private readonly ICircuitEventSink _forward;
            private readonly List<CircuitEvent> _events = new();

            public IReadOnlyList<CircuitEvent> Events => _events;

            public EventSink(
                ICircuitEventSink forward)
            {
                _forward = forward;
            }

            public void Record(in CircuitEvent circuitEvent)
            {
                _events.Add(circuitEvent);
                _forward?.Record(circuitEvent);
            }
        }

        private readonly FirstSpiritHeatRuntime _heat = new();
        private readonly EventSink _sink;
        private readonly CircuitRuntime _runtime;
        private readonly List<DeclarativeCircuitRule> _rules = new();
        private readonly HashSet<ulong> _refundedChains = new();

        public int Heat => _heat.Heat;
        public IReadOnlyList<CircuitEvent> Events => _sink.Events;
        public CircuitPreviewGraph Preview { get; }
        public FirstSpiritCircuitTuning Tuning { get; private set; }

        public FirstSpiritCircuitRuntime(
            SpiritInstanceState sparkRaccoon,
            CarrierSlot sparkCarrier,
            SpiritInstanceState echoOwl,
            CarrierSlot echoCarrier,
            SpiritInstanceState bounceGel,
            CarrierSlot bounceCarrier,
            int ownerEntityId,
            StableConfigId ownerConfigId,
            CircuitExecutionLimits? limits = null,
            ICircuitEventSink sink = null,
            FirstSpiritCircuitTuning? tuning = null)
        {
            ValidateSpecies(
                sparkRaccoon,
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            ValidateSpecies(
                echoOwl,
                FirstSpiritCircuitContent.EchoOwlSpecies);
            ValidateSpecies(
                bounceGel,
                FirstSpiritCircuitContent.BounceGelSpecies);
            if (ownerConfigId.IsEmpty)
                throw new ArgumentException(
                    "Circuit owner config ID cannot be empty.",
                    nameof(ownerConfigId));

            _rules.Add(SpiritCircuitRuleBinder.Bind(
                FirstSpiritCircuitContent.ProduceSpark,
                sparkRaccoon,
                sparkCarrier,
                ownerEntityId,
                ownerConfigId));
            _rules.Add(SpiritCircuitRuleBinder.Bind(
                FirstSpiritCircuitContent.TriggerEchoAttack,
                echoOwl,
                echoCarrier,
                ownerEntityId,
                ownerConfigId));
            _rules.Add(SpiritCircuitRuleBinder.Bind(
                FirstSpiritCircuitContent.ModifyEchoToBounce,
                bounceGel,
                bounceCarrier,
                ownerEntityId,
                ownerConfigId));

            _sink = new EventSink(sink);
            Tuning = tuning ?? FirstSpiritCircuitTuning.Default;
            _heat.Configure(
                Tuning.DirectHitsPerSpark,
                Tuning.RetainedHeatOnSpark);
            _runtime = new CircuitRuntime(
                limits ?? CircuitExecutionLimits.Default,
                _sink);
            foreach (DeclarativeCircuitRule rule in _rules)
                _runtime.Register(rule);
            Preview = CircuitPreviewGraphBuilder.Build(_rules);
        }

        public void BeginFrame()
        {
            _runtime.BeginFrame();
        }

        public FirstSpiritCircuitStep RegisterDirectHit(
            ulong causalChainId,
            float currentTime,
            in CircuitEntityRef instigator)
        {
            if (!_heat.RegisterDirectHit())
            {
                return new FirstSpiritCircuitStep(
                    false,
                    _heat.Heat,
                    default);
            }

            return ExecuteReadyChain(
                causalChainId,
                currentTime,
                instigator);
        }

        public FirstSpiritCircuitStep ExecuteReadyChain(
            ulong causalChainId,
            float currentTime,
            in CircuitEntityRef instigator)
        {
            var root = new CircuitEvent(
                causalChainId,
                0,
                CircuitEventKind.Source,
                instigator,
                instigator,
                ElementTag.None,
                FirstSpiritCircuitContent.HeatReadyTag);
            CircuitExecutionReport report =
                _runtime.Execute(root, currentTime);
            return new FirstSpiritCircuitStep(
                true,
                _heat.Heat,
                report);
        }

        public void ResetHeat()
        {
            _heat.Reset();
            _refundedChains.Clear();
        }

        public void ApplyTuning(in FirstSpiritCircuitTuning tuning)
        {
            Tuning = tuning;
            _heat.Configure(
                tuning.DirectHitsPerSpark,
                tuning.RetainedHeatOnSpark);
        }

        public bool RegisterBounceResolved(in CircuitEvent origin)
        {
            if (origin.Kind != CircuitEventKind.Transform ||
                (origin.TagMask &
                 FirstSpiritCircuitContent.HeatRefundTag) == 0 ||
                !_refundedChains.Add(origin.CausalChainId))
            {
                return false;
            }

            _heat.ApplyRefund(Tuning.HeatRefund);
            return true;
        }

        public bool RegisterEchoResolved(in CircuitEvent origin)
        {
            if (origin.Kind != CircuitEventKind.Response ||
                (origin.TagMask &
                 FirstSpiritCircuitContent.EchoAttackTag) == 0 ||
                Tuning.EchoHitHeatRefund <= 0)
            {
                return false;
            }

            _heat.ApplyRefund(Tuning.EchoHitHeatRefund);
            return true;
        }

        private static void ValidateSpecies(
            SpiritInstanceState spirit,
            StableConfigId expectedSpecies)
        {
            if (spirit == null)
                throw new ArgumentNullException(nameof(spirit));
            if (spirit.Identity.SpeciesConfigId != expectedSpecies)
            {
                throw new ArgumentException(
                    $"Expected species {expectedSpecies}, got " +
                    $"{spirit.Identity.SpeciesConfigId}.",
                    nameof(spirit));
            }
        }
    }
}
