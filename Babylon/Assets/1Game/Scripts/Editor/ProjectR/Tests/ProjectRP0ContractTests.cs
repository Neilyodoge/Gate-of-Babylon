using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace XianTu.Editor.Tests
{
    public class ProjectRP0ContractTests
    {
        [SetUp]
        public void SetUp()
        {
            FeatureFlags.ResetRuntimeOverrides();
            RunCombatStats.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FeatureFlags.ResetRuntimeOverrides();
            RunCombatStats.Reset();
        }

        private sealed class FakeSkillEffectHost : ISkillEffectHost
        {
            public bool AllowCast = true;
            public bool Started;
            public bool GateChecked;
            public string DispatchedEffect;
            public int ReceivedSlot = -1;
            public int LoggedChargeLevel;
            public float DamageMultiplier;
            public float RadiusMultiplier;

            public void PublishSkillCastStarted(SkillData skill, int slotIndex)
            {
                Started = true;
                ReceivedSlot = slotIndex;
            }

            public bool TryBeginSkillCast(SkillData skill)
            {
                GateChecked = true;
                return AllowCast;
            }

            public void LogSkillCast(SkillData skill, int chargeLevel)
            {
                LoggedChargeLevel = chargeLevel;
            }

            public void CastArea(SkillData skill, float damageMultiplier, float radiusMultiplier, int slotIndex)
            {
                DispatchedEffect = "Area";
                DamageMultiplier = damageMultiplier;
                RadiusMultiplier = radiusMultiplier;
                ReceivedSlot = slotIndex;
            }

            public void CastProjectile(SkillData skill, float damageMultiplier)
            {
                DispatchedEffect = "Projectile";
                DamageMultiplier = damageMultiplier;
            }

            public void CastDash(SkillData skill) => DispatchedEffect = "Dash";

            public void CastBuff(SkillData skill, int slotIndex)
            {
                DispatchedEffect = "Buff";
                ReceivedSlot = slotIndex;
            }

            public void CastZone(SkillData skill, float damageMultiplier)
            {
                DispatchedEffect = "Zone";
                DamageMultiplier = damageMultiplier;
            }

            public void CastHeal(SkillData skill) => DispatchedEffect = "Heal";

            public void CastSummon(SkillData skill) => DispatchedEffect = "Summon";
        }

        private sealed class FakeImmediateSkillEffectHost : IImmediateSkillEffectHost
        {
            public float AttackDamage { get; set; }
            public float CurrentHealth { get; private set; }
            public float MaxHealth { get; set; }
            public float ArmedDuration = -1f;
            public float ShiftDuration = -1f;
            public StatusEffect AppliedStatus;
            public bool SlotModifiersApplied;
            public bool VisualPlayed;
            public bool HealthPublished;
            public float PublishedHeal = -1f;
            public float RequestedHeal = -1f;
            public float RecordedHeal = -1f;
            public string LastLog;

            public FakeImmediateSkillEffectHost(float currentHealth = 50f)
            {
                CurrentHealth = currentHealth;
            }

            public void SetCurrentHealth(float value) => CurrentHealth = value;
            public void RecordHealing(float requestedHeal, float actualHeal)
            {
                RequestedHeal = requestedHeal;
                RecordedHeal = actualHeal;
            }
            public void ArmLethalGuard(float duration) => ArmedDuration = duration;
            public void ActivateHeavenEarthShift(float duration) => ShiftDuration = duration;
            public void ApplyBuffStatus(StatusEffect effect) => AppliedStatus = effect;
            public void ApplyBuffSlotModifiers(SkillData skill, int slotIndex)
                => SlotModifiersApplied = true;
            public void PlayImmediateVisual(
                SkillData skill,
                ImmediateSkillVisual visual,
                Color fallbackColor)
                => VisualPlayed = true;
            public void PublishHealthChanged() => HealthPublished = true;
            public void PublishHealNumber(float actualHeal) => PublishedHeal = actualHeal;
            public void LogImmediateSkill(string message) => LastLog = message;
        }

        private sealed class FakeWorldSkillEffectHost : IWorldSkillEffectHost
        {
            public Vector3 Origin { get; set; }
            public Vector3 AimDirection { get; set; }
            public bool HasGroundPointer = true;
            public Vector3 GroundPointer;
            public bool PointerQueried;
            public Vector3 SpawnPosition;
            public float ReceivedDamageMultiplier;
            public float SummonDamage = 25f;
            public float SummonDuration;
            public bool ZoneSpawned;
            public bool DecoySpawned;
            public bool SummonSpawned;

            public bool TryGetGroundPointer(out Vector3 worldPosition)
            {
                PointerQueried = true;
                worldPosition = GroundPointer;
                return HasGroundPointer;
            }

            public void SpawnZone(SkillData skill, Vector3 position, float damageMultiplier)
            {
                ZoneSpawned = true;
                SpawnPosition = position;
                ReceivedDamageMultiplier = damageMultiplier;
            }

            public void SpawnDecoy(Vector3 position, float duration)
            {
                DecoySpawned = true;
                SpawnPosition = position;
                SummonDuration = duration;
            }

            public float BuildSummonDamage(float attackRatio, float flatDamage)
                => SummonDamage;

            public void SpawnSummon(
                SkillData skill,
                Vector3 position,
                float damage,
                float duration)
            {
                SummonSpawned = true;
                SpawnPosition = position;
                SummonDamage = damage;
                SummonDuration = duration;
            }

            public void LogWorldSkill(string message)
            {
            }
        }

        private sealed class FakeDashSkillEffectHost : IDashSkillEffectHost
        {
            public Vector3 Origin { get; set; }
            public Vector3 AimDirection { get; set; }
            public Vector3 ResolvedDestination;
            public float RequestedDistance;
            public Vector3 MovedTo;
            public float InvincibleDuration = -1f;
            public bool TrailShown;
            public bool TrailDamageApplied;
            public bool VisualPlayed;

            public Vector3 ResolveDashDestination(
                Vector3 start,
                Vector3 direction,
                float distance)
            {
                RequestedDistance = distance;
                return ResolvedDestination;
            }

            public void ShowDashTrail(
                SkillData skill,
                Vector3 start,
                Vector3 destination)
                => TrailShown = true;

            public void MoveTo(Vector3 destination) => MovedTo = destination;
            public void SetInvincible(float duration) => InvincibleDuration = duration;
            public void ApplyDashTrailDamage(
                SkillData skill,
                Vector3 start,
                Vector3 destination)
                => TrailDamageApplied = true;
            public void PlayDashVisual(SkillData skill, Vector3 destination)
                => VisualPlayed = true;
            public void LogDash(string message)
            {
            }
        }

        private sealed class FakeAreaSkillEffectHost : IAreaSkillEffectHost
        {
            public bool EnhancementActive { get; set; }
            public float EnhancementRadiusMultiplier { get; set; } = 1f;
            public bool HasSustainedEnhancement { get; set; }
            public bool HasDelayedBlastEnhancement { get; set; }
            public float TotalPlayerDamage { get; set; }
            public bool HasTarget = true;
            public Vector3 TargetPosition;
            public ElementTag Element;
            public System.Collections.Generic.IReadOnlyList<AreaSkillTarget> Targets =
                Array.Empty<AreaSkillTarget>();
            public float CalculatedDamage = 10f;
            public float BaseMultiplier = 2f;
            public bool ChanceResult;
            public float VisualRadius;
            public float AppliedDamage;
            public int DamageApplications;
            public bool FreezeApplied;
            public bool HitPublished;
            public bool SlotModifiersApplied;
            public float SustainedMultiplier;
            public float DelayedMultiplier;
            public float DelayedRadius;

            public bool TryGetAreaTarget(out Vector3 worldPosition)
            {
                worldPosition = TargetPosition;
                return HasTarget;
            }

            public ElementTag ResolveAreaElement(SkillData skill) => Element;

            public void PlayAreaVisual(
                SkillData skill,
                Vector3 position,
                float visualScale,
                float actualRadius,
                ElementTag element)
                => VisualRadius = actualRadius;

            public System.Collections.Generic.IReadOnlyList<AreaSkillTarget>
                FindAreaTargets(Vector3 position, float radius)
                => Targets;

            public float CalculateAreaDamage(SkillData skill, float targetDefense)
                => CalculatedDamage;

            public float CalculateAreaBaseMultiplier(SkillData skill)
                => BaseMultiplier;

            public void ApplyAreaDamage(AreaSkillTarget target, float damage)
            {
                AppliedDamage = damage;
                DamageApplications++;
            }

            public void TrackAreaEnhancementTarget(AreaSkillTarget target)
            {
            }

            public bool RollAreaChance(float probability) => ChanceResult;

            public void ApplyAreaFreeze(AreaSkillTarget target, float duration)
                => FreezeApplied = true;

            public void PublishAreaHit(
                SkillData skill,
                int slotIndex,
                AreaSkillTarget target)
                => HitPublished = true;

            public void ApplyAreaElementImpact(
                ElementTag element,
                Vector3 position,
                System.Collections.Generic.IReadOnlyList<AreaSkillTarget> targets)
            {
            }

            public void ApplyAreaSlotModifiers(
                SkillData skill,
                int slotIndex,
                Vector3 position,
                float radius,
                System.Collections.Generic.IReadOnlyList<AreaSkillTarget> targets)
                => SlotModifiersApplied = true;

            public void SpawnSustainedArea(
                Vector3 position,
                float radius,
                float damageMultiplier,
                ElementTag element)
                => SustainedMultiplier = damageMultiplier;

            public void SpawnDelayedArea(
                Vector3 position,
                float radius,
                float damageMultiplier,
                ElementTag element)
            {
                DelayedRadius = radius;
                DelayedMultiplier = damageMultiplier;
            }
        }

        private sealed class FakeProjectileSkillEffectHost : IProjectileSkillEffectHost
        {
            public Vector3 ProjectileOrigin { get; set; }
            public Vector3 AimDirection { get; set; }
            public bool EnhancementActive { get; set; }
            public bool TargetFarthest { get; set; }
            public bool SurroundPattern { get; set; }
            public bool RingPattern { get; set; }
            public bool WallPattern { get; set; }
            public bool ImpactZone { get; set; }
            public float ProjectileCountMultiplier { get; set; } = 1f;
            public int ExtraProjectiles { get; set; }
            public bool HasFarthestTarget;
            public Vector3 FarthestDirection;
            public float CalculatedDamage = 10f;
            public readonly System.Collections.Generic.List<Vector3> Positions = new();
            public readonly System.Collections.Generic.List<Vector3> Directions = new();
            public float ReceivedDamage;
            public bool EnhancementApplied;
            public bool ImpactZoneApplied;

            public bool TryGetFarthestTargetDirection(
                Vector3 origin,
                float maxRange,
                out Vector3 direction)
            {
                direction = FarthestDirection;
                return HasFarthestTarget;
            }

            public float CalculateProjectileDamage(SkillData skill)
                => CalculatedDamage;

            public ElementTag ResolveProjectileElement(SkillData skill)
                => ElementTag.Fire;

            public void SpawnProjectile(
                SkillData skill,
                Vector3 position,
                Vector3 direction,
                float damage,
                ElementTag element,
                bool applyEnhancement,
                bool impactZone)
            {
                Positions.Add(position);
                Directions.Add(direction);
                ReceivedDamage = damage;
                EnhancementApplied = applyEnhancement;
                ImpactZoneApplied = impactZone;
            }
        }

        private sealed class FakeSkillChargeEffectHost : ISkillChargeEffectHost
        {
            private float _moveSpeed = 10f;

            public bool CanAdjustMovement { get; set; } = true;
            public readonly System.Collections.Generic.List<string> Calls = new();
            public bool LastIsCharging;

            public float MoveSpeed
            {
                get => _moveSpeed;
                set
                {
                    _moveSpeed = value;
                    Calls.Add($"speed:{value:F1}");
                }
            }

            public void PublishChargeProgress(
                int slotIndex,
                float chargeTime,
                int chargeLevel,
                bool isCharging)
            {
                LastIsCharging = isCharging;
                Calls.Add($"publish:{isCharging}");
            }

            public void LogChargeEffect(string message) => Calls.Add("log");
        }

        private sealed class FakeMeleeAttackHost : IMeleeAttackHost
        {
            public bool IsRangedBasic { get; set; }
            public bool IsHitWindowOpen { get; set; }
            public int ComboStep { get; set; }
            public int AttackRequests;
            public int ResolveCalls;
            public int PublishCalls;
            public int CooldownReductions;
            public bool? LastDrawActive;
            public MeleeHitResult HitResult =
                new(true, "enemy", Vector3.one);

            public void RequestMeleeAttack(float attackSpeed)
                => AttackRequests++;

            public void DrawMeleeRange(bool activeWindow)
                => LastDrawActive = activeWindow;

            public MeleeHitResult ResolveMeleeHits(int comboStep)
            {
                ResolveCalls++;
                return HitResult;
            }

            public void PublishMeleeHit(int comboStep, MeleeHitResult result)
                => PublishCalls++;

            public void ReduceCooldownOnComboFinisher()
                => CooldownReductions++;
        }

        private sealed class FakeBasicAttackEffectHost : IBasicAttackEffectHost
        {
            public Vector3 MeleeOrigin { get; set; }
            public Vector3 AimDirection { get; set; } = Vector3.forward;
            public Vector3 ForwardFallback { get; set; } = Vector3.right;
            public float MeleeRange { get; set; } = 3f;
            public float MeleeHalfAngle { get; set; } = 60f;
            public bool ConvertMeleeDamageToHealing { get; set; }
            public Vector3 RangedProjectileOrigin { get; set; } = Vector3.one;
            public float RangedProjectileSpeed { get; set; } = 18f;
            public float RangedDamageMultiplier { get; set; } = 1f;
            public ElementTag RangedElement { get; set; } = ElementTag.Fire;
            public System.Collections.Generic.IReadOnlyList<BasicAttackTarget> Targets =
                Array.Empty<BasicAttackTarget>();
            public float ComboMultiplier = 1f;
            public float BaseDamage = 10f;
            public int DamageApplications;
            public float AppliedDamage;
            public int HealingApplications;
            public float AppliedHealing;
            public int HitVisuals;
            public bool ProjectileSpawned;
            public Vector3 ProjectileDirection;
            public float ProjectileDamage;
            public int CooldownReductions;

            public System.Collections.Generic.IReadOnlyList<BasicAttackTarget>
                FindBasicAttackTargets(Vector3 origin, float range)
                => Targets;

            public float GetBasicComboMultiplier(int comboStep)
                => ComboMultiplier;

            public float CalculateBasicDamage(float targetDefense)
                => BaseDamage;

            public void ApplyBasicDamage(BasicAttackTarget target, float damage)
            {
                DamageApplications++;
                AppliedDamage = damage;
            }

            public void ApplyConvertedBasicHealing(
                float healAmount,
                Vector3 hitPoint)
            {
                HealingApplications++;
                AppliedHealing = healAmount;
            }

            public void PlayBasicHitVisual(Vector3 hitPoint) => HitVisuals++;

            public void SpawnBasicProjectile(
                Vector3 position,
                Vector3 direction,
                float speed,
                float damage,
                ElementTag element)
            {
                ProjectileSpawned = true;
                ProjectileDirection = direction;
                ProjectileDamage = damage;
            }

            public void ReduceBasicCooldownOnFinisher()
                => CooldownReductions++;
        }

        private sealed class FakeEvadeCommandHost : IEvadeCommandHost
        {
            public bool PlayAccepted = true;
            public int PlayRequests;
            public int BufferedRequests;
            public float InvincibleDuration;
            public int ChargeUpdates;
            public int LastCharges;
            public float LastRechargeProgress;
            public int FinishedEvents;
            public Vector3 FinishedPosition;
            public Vector3 FinishedDirection;

            public bool TryPlayEvade()
            {
                PlayRequests++;
                return PlayAccepted;
            }

            public void BufferEvade() => BufferedRequests++;

            public void SetEvadeInvincible(float duration)
                => InvincibleDuration = duration;

            public void PublishEvadeCharge(
                int currentCharges,
                int maxCharges,
                float rechargeProgress)
            {
                ChargeUpdates++;
                LastCharges = currentCharges;
                LastRechargeProgress = rechargeProgress;
            }

            public void PublishEvadeFinished(
                Vector3 endPosition,
                Vector3 direction)
            {
                FinishedEvents++;
                FinishedPosition = endPosition;
                FinishedDirection = direction;
            }
        }

        private sealed class FakeCircuitRule : ICircuitRule
        {
            public StableConfigId RuleId { get; set; }
            public Guid OwnerSpiritId { get; set; }
            public Func<CircuitEvent, bool> Match { get; set; }
            public Func<CircuitEvent, CircuitEmission> EmitEvent { get; set; }

            public bool Matches(in CircuitEvent input)
            {
                return Match(input);
            }

            public CircuitEmission Emit(in CircuitEvent input)
            {
                return EmitEvent(input);
            }
        }

        private sealed class FakeCircuitEventSink : ICircuitEventSink
        {
            public readonly System.Collections.Generic.List<CircuitEvent>
                Events = new();

            public void Record(in CircuitEvent circuitEvent)
            {
                Events.Add(circuitEvent);
            }
        }

        private sealed class FakeStructuredCombatResultSink :
            IStructuredCombatResultSink
        {
            public int Count;
            public StructuredCombatResult LastResult;

            public void Record(in StructuredCombatResult result)
            {
                Count++;
                LastResult = result;
            }
        }

        private static SpiritInstanceState CreateSpiritState(string suffix)
        {
            return new SpiritInstanceState(new SpiritIdentity(
                Guid.NewGuid(),
                new StableConfigId($"spirit.{suffix}"),
                new StableConfigId("personality.test")));
        }

        private static SpiritInstanceState CreateFirstPetState(
            StableConfigId species)
        {
            return new SpiritInstanceState(new SpiritIdentity(
                Guid.NewGuid(),
                species,
                new StableConfigId("personality.test")));
        }

        private static SaveDataV1 CreateFirstPetSaveData()
        {
            SpiritInstanceState spark = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            SpiritInstanceState echo = CreateFirstPetState(
                FirstSpiritCircuitContent.EchoOwlSpecies);
            SpiritInstanceState bounce = CreateFirstPetState(
                FirstSpiritCircuitContent.BounceGelSpecies);
            var save = new SaveDataV1();
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(echo));
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(bounce));
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(spark));
            save.activeSpiritInstanceGuids.Add(
                bounce.Identity.InstanceId.ToString("N"));
            save.activeSpiritInstanceGuids.Add(
                spark.Identity.InstanceId.ToString("N"));
            save.activeSpiritInstanceGuids.Add(
                echo.Identity.InstanceId.ToString("N"));
            return save;
        }

        private static FirstSpiritCircuitRuntime CreateFirstPetCircuit(
            ICircuitEventSink sink = null)
        {
            return new FirstSpiritCircuitRuntime(
                CreateFirstPetState(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                CarrierSlot.Weapon,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                CarrierSlot.TechniqueQ,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                CarrierSlot.Mobility,
                1,
                new StableConfigId("entity.player"),
                new CircuitExecutionLimits(8, 2, 64, 0f),
                sink);
        }

        [Test]
        public void ProjectRFlags_DefaultsMatchCurrentRollout()
        {
            var config = GameConfig.Instance;
            if (config != null)
            {
                Assert.That(config.启用载体运行时, Is.True);
                Assert.That(config.启用回路运行时, Is.False);
                Assert.That(config.启用生态运行时, Is.False);
            }

            Assert.That(FeatureFlags.EnableCarrierRuntime, Is.True);
            Assert.That(FeatureFlags.EnableCircuitRuntime, Is.False);
            Assert.That(FeatureFlags.EnableEcologyRuntime, Is.False);
        }

        [Test]
        public void ProjectRFlags_RuntimeOverrideWins()
        {
            FeatureFlags.EnableCarrierRuntime = true;
            FeatureFlags.EnableCircuitRuntime = true;
            FeatureFlags.EnableEcologyRuntime = true;

            Assert.That(FeatureFlags.EnableCarrierRuntime, Is.True);
            Assert.That(FeatureFlags.EnableCircuitRuntime, Is.True);
            Assert.That(FeatureFlags.EnableEcologyRuntime, Is.True);
        }

        [TestCase(0, CarrierSlot.TechniqueQ)]
        [TestCase(1, CarrierSlot.TechniqueE)]
        [TestCase(2, CarrierSlot.TechniqueR)]
        public void LegacySlotMapping_IsStable(int legacySlot, CarrierSlot expected)
        {
            Assert.That(LegacyModuleChainAdapter.SlotIndexToCarrier(legacySlot), Is.EqualTo(expected));
        }

        [Test]
        public void LegacySnapshot_PreservesEnhancementFields()
        {
            var config = new ChainConfig
            {
                effectRole = EffectRole.Enhancement,
                elementTag = ElementTag.Fire,
                enhanceDamageMult = 1.25f,
                enhanceRadiusMult = 1.5f,
                enhanceProjectileMult = 2f,
                enhanceExtraProjectiles = 3,
                enhanceChainCount = 4,
                enhanceSurround = true,
                enhanceSustained = true,
                enhanceDelayedBlast = true,
                enhanceTargetFarthest = true,
                enhanceShape = ShapeMode.Ring,
            };

            var snapshot = LegacyModuleChainAdapter.ToEnhancementSnapshot(config);

            Assert.That(snapshot.Role, Is.EqualTo(CarrierEnhancementRole.Enhancement));
            Assert.That(snapshot.ElementTag, Is.EqualTo(config.elementTag));
            Assert.That(snapshot.DamageMultiplier, Is.EqualTo(config.enhanceDamageMult));
            Assert.That(snapshot.RadiusMultiplier, Is.EqualTo(config.enhanceRadiusMult));
            Assert.That(snapshot.ProjectileMultiplier, Is.EqualTo(config.enhanceProjectileMult));
            Assert.That(snapshot.ExtraProjectiles, Is.EqualTo(config.enhanceExtraProjectiles));
            Assert.That(snapshot.ChainCount, Is.EqualTo(config.enhanceChainCount));
            Assert.That(snapshot.Surround, Is.EqualTo(config.enhanceSurround));
            Assert.That(snapshot.Sustained, Is.EqualTo(config.enhanceSustained));
            Assert.That(snapshot.DelayedBlast, Is.EqualTo(config.enhanceDelayedBlast));
            Assert.That(snapshot.TargetFarthest, Is.EqualTo(config.enhanceTargetFarthest));
            Assert.That(snapshot.Shape, Is.EqualTo(CarrierShapeMode.Ring));
        }

        [Test]
        public void CircuitEvent_RejectsNegativeDepth()
        {
            var source = new CircuitEntityRef(1, CarrierSlot.Weapon, Guid.Empty);
            var instigator = new CircuitEntityRef(2, null, Guid.NewGuid());

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CircuitEvent(7, -1, CircuitEventKind.Source, source, instigator, ElementTag.None, 0));
        }

        [Test]
        public void CircuitEvent_PreservesCausalIdentity()
        {
            var spiritId = Guid.NewGuid();
            var source = new CircuitEntityRef(1, CarrierSlot.TechniqueQ, spiritId);
            var instigator = new CircuitEntityRef(2, CarrierSlot.Weapon, Guid.Empty);

            var circuitEvent = new CircuitEvent(
                42,
                2,
                CircuitEventKind.Response,
                source,
                instigator,
                ElementTag.Thunder,
                0b101);

            Assert.That(circuitEvent.CausalChainId, Is.EqualTo(42));
            Assert.That(circuitEvent.Depth, Is.EqualTo(2));
            Assert.That(circuitEvent.Source.SpiritInstanceId, Is.EqualTo(spiritId));
            Assert.That(circuitEvent.TagMask, Is.EqualTo(0b101));
        }

        [Test]
        public void StableConfigId_TrimsAndUsesOrdinalIdentity()
        {
            var trimmed = new StableConfigId(" spirit.fire ");
            var exact = new StableConfigId("spirit.fire");
            var differentCase = new StableConfigId("Spirit.Fire");

            Assert.That(trimmed, Is.EqualTo(exact));
            Assert.That(trimmed, Is.Not.EqualTo(differentCase));
            Assert.That(trimmed.ToString(), Is.EqualTo("spirit.fire"));
        }

        [Test]
        public void SpiritIdentity_RequiresStableGuidAndConfig()
        {
            Guid instanceId = Guid.NewGuid();
            var identity = new SpiritIdentity(
                instanceId,
                new StableConfigId("spirit.fox"),
                new StableConfigId("personality.bold"));

            Assert.That(identity.InstanceId, Is.EqualTo(instanceId));
            Assert.That(
                identity.SpeciesConfigId,
                Is.EqualTo(new StableConfigId("spirit.fox")));
            Assert.Throws<ArgumentException>(() => new SpiritIdentity(
                Guid.Empty,
                new StableConfigId("spirit.fox"),
                new StableConfigId("personality.bold")));
        }

        [Test]
        public void SpiritState_PreservesRelationshipsAndEnlightenment()
        {
            Guid instanceId = Guid.NewGuid();
            Guid companionId = Guid.NewGuid();
            var state = new SpiritInstanceState(new SpiritIdentity(
                instanceId,
                new StableConfigId("spirit.fox"),
                new StableConfigId("personality.curious")));
            var enlightenment = new SpiritEnlightenment(
                new StableConfigId("enlightenment.fox-fire"),
                EnlightenmentKind.Source);

            state.SetPlayerBond(12);
            state.SetRelationship(new SpiritRelationship(
                companionId,
                new StableConfigId("relationship.trust"),
                7));
            state.UnlockCarrierAdaptation(
                new StableConfigId("carrier.weapon.sword"));
            state.AddToEnlightenmentPool(enlightenment);
            state.LearnTemporary(enlightenment);

            Assert.That(state.PlayerBond, Is.EqualTo(12));
            Assert.That(state.Relationships.ContainsKey(companionId), Is.True);
            Assert.That(state.CarrierAdaptations.Count, Is.EqualTo(1));
            Assert.That(state.EnlightenmentPool.Count, Is.EqualTo(1));
            Assert.That(state.TemporaryEnlightenments.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpiritSaveMapper_RoundTripsPermanentStateOnly()
        {
            Guid instanceId = Guid.NewGuid();
            Guid relatedId = Guid.NewGuid();
            var source = new SpiritInstanceState(new SpiritIdentity(
                instanceId,
                new StableConfigId("spirit.save.test"),
                new StableConfigId("personality.primary"),
                new StableConfigId("personality.secondary")));
            source.SetPlayerBond(17);
            source.SetRelationship(new SpiritRelationship(
                relatedId,
                new StableConfigId("relationship.friend"),
                8));
            source.AddToEnlightenmentPool(new SpiritEnlightenment(
                new StableConfigId("enlightenment.permanent"),
                EnlightenmentKind.Source));
            source.LearnTemporary(new SpiritEnlightenment(
                new StableConfigId("enlightenment.temporary"),
                EnlightenmentKind.Transform));
            source.UnlockCarrierAdaptation(
                new StableConfigId("carrier.sword"));

            SpiritInstanceSave save = SpiritSaveMapper.ToSave(source);
            bool restored = SpiritSaveMapper.TryRestore(
                save,
                out SpiritInstanceState target);

            Assert.That(restored, Is.True);
            Assert.That(target.Identity.InstanceId, Is.EqualTo(instanceId));
            Assert.That(target.PlayerBond, Is.EqualTo(17));
            Assert.That(target.Relationships.ContainsKey(relatedId), Is.True);
            Assert.That(target.EnlightenmentPool.Count, Is.EqualTo(1));
            Assert.That(
                target.TemporaryEnlightenments.Count,
                Is.Zero);
            Assert.That(target.CarrierAdaptations.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpiritSaveMapper_SkipsInvalidNestedEntries()
        {
            var save = new SpiritInstanceSave
            {
                instanceGuid = Guid.NewGuid().ToString("N"),
                speciesConfigId = "spirit.valid",
                primaryPersonalityId = "personality.valid",
                relationships =
                    new System.Collections.Generic.List<
                        SpiritRelationshipSave>
                    {
                        new()
                        {
                            otherSpiritGuid = "invalid",
                            relationshipConfigId = "relationship.invalid"
                        }
                    },
                enlightenmentPool =
                    new System.Collections.Generic.List<
                        SpiritEnlightenmentSave>
                    {
                        new()
                        {
                            configId = "enlightenment.invalid",
                            kind = 99
                        }
                    },
                carrierAdaptationIds =
                    new System.Collections.Generic.List<string>
                    {
                        "",
                        "carrier.valid"
                    }
            };

            bool restored = SpiritSaveMapper.TryRestore(
                save,
                out SpiritInstanceState state);

            Assert.That(restored, Is.True);
            Assert.That(state.Relationships.Count, Is.Zero);
            Assert.That(state.EnlightenmentPool.Count, Is.Zero);
            Assert.That(state.CarrierAdaptations.Count, Is.EqualTo(1));
        }

        [Test]
        public void StarterSpiritTalentCatalog_DefinesEighteenPerSpecies()
        {
            Assert.That(
                StarterSpiritTalentCatalog.ForSpecies(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies).Count,
                Is.EqualTo(18));
            Assert.That(
                StarterSpiritTalentCatalog.ForSpecies(
                    FirstSpiritCircuitContent.EchoOwlSpecies).Count,
                Is.EqualTo(18));
            Assert.That(
                StarterSpiritTalentCatalog.ForSpecies(
                    FirstSpiritCircuitContent.BounceGelSpecies).Count,
                Is.EqualTo(18));
        }

        [Test]
        public void StarterSpiritTalentCatalog_FillsEveryVisualSlot()
        {
            StableConfigId[] species =
            {
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                FirstSpiritCircuitContent.BounceGelSpecies
            };
            foreach (StableConfigId speciesId in species)
            {
                SpiritInstanceState spirit =
                    CreateFirstPetState(speciesId);
                IReadOnlyList<SpiritTalentTreeNodePresentation> nodes =
                    SpiritTalentTreePresentation.Build(
                        new SpiritRunTalentState(spirit));
                var slots = new HashSet<int>();
                foreach (SpiritTalentTreeNodePresentation node in nodes)
                    slots.Add(node.VisualSlot);
                Assert.That(nodes.Count, Is.EqualTo(18));
                Assert.That(slots.Count, Is.EqualTo(18));
                for (int slot = 0; slot < 18; slot++)
                    Assert.That(slots.Contains(slot), Is.True);
            }
        }

        [Test]
        public void SpiritTalentTreePresentation_MapsFourTiersAndThreeBranches()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            var run = new SpiritRunTalentState(spirit);
            run.AddExperience(2);

            IReadOnlyList<SpiritTalentTreeNodePresentation> nodes =
                SpiritTalentTreePresentation.Build(run);

            Assert.That(nodes.Count, Is.EqualTo(18));
            for (int tier = 1; tier <= 4; tier++)
            {
                var branches = new HashSet<int>();
                foreach (SpiritTalentTreeNodePresentation node in nodes)
                {
                    if (node.Definition.Tier == tier)
                        branches.Add(node.Branch);
                }
                Assert.That(
                    branches.SetEquals(new[] { 0, 1, 2 }),
                    Is.True);
            }
            var states = new Dictionary<
                StableConfigId,
                SpiritTalentTreeNodeState>();
            foreach (SpiritTalentTreeNodePresentation node in nodes)
                states[node.Definition.ConfigId] = node.State;
            Assert.That(
                states[new StableConfigId(
                    "talent.spark.quick-temper")],
                Is.EqualTo(SpiritTalentTreeNodeState.Available));
            Assert.That(
                states[new StableConfigId(
                    "talent.spark.chasing-fire")],
                Is.EqualTo(
                    SpiritTalentTreeNodeState.PermanentlyLocked));
        }

        [Test]
        public void SpiritRunTalentState_HonorsAllAndAnyPrerequisites()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            foreach (SpiritTalentDefinition definition
                     in StarterSpiritTalentCatalog.ForSpecies(
                         spirit.Identity.SpeciesConfigId))
            {
                if (definition.Tier > 1)
                    spirit.UnlockTalent(definition.ConfigId);
            }
            var run = new SpiritRunTalentState(spirit);
            run.AddExperience(27);
            string[] firstSteps =
            {
                "talent.spark.quick-temper",
                "talent.spark.chasing-fire"
            };
            foreach (string id in firstSteps)
            {
                Assert.That(
                    run.TryActivate(new StableConfigId(id)),
                    Is.EqualTo(SpiritTalentActivationResult.Success));
            }
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.fire-trail")),
                Is.EqualTo(
                    SpiritTalentActivationResult.MissingPrerequisite));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.swift-spark")),
                Is.EqualTo(SpiritTalentActivationResult.Success));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.fire-trail")),
                Is.EqualTo(SpiritTalentActivationResult.Success));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.chain-burn")),
                Is.EqualTo(SpiritTalentActivationResult.Success));
        }

        [Test]
        public void StarterSpiritTalentBuildSamples_ReachNineCapstonesInFivePoints()
        {
            Assert.That(
                StarterSpiritTalentBuildSamples.All.Count,
                Is.EqualTo(9));
            foreach (StarterSpiritTalentBuildSample build
                     in StarterSpiritTalentBuildSamples.All)
            {
                SpiritInstanceState spirit =
                    CreateFirstPetState(build.SpeciesId);
                foreach (SpiritTalentDefinition definition
                         in StarterSpiritTalentCatalog.ForSpecies(
                             build.SpeciesId))
                {
                    if (definition.Tier > 1)
                        spirit.UnlockTalent(definition.ConfigId);
                }
                var run = new SpiritRunTalentState(spirit);
                run.AddExperience(20);
                Assert.That(build.TalentIds.Count, Is.EqualTo(5));
                foreach (string talentId in build.TalentIds)
                {
                    Assert.That(
                        run.TryActivate(
                            new StableConfigId(talentId)),
                        Is.EqualTo(
                            SpiritTalentActivationResult.Success),
                        build.Name);
                }
                Assert.That(run.ActiveTalents.Count, Is.EqualTo(5));
                Assert.That(run.UnspentPoints, Is.Zero);
                Assert.That(
                    StarterSpiritTalentCatalog.TryGet(
                        new StableConfigId(build.TalentIds[4]),
                        out SpiritTalentDefinition capstone),
                    Is.True);
                Assert.That(capstone.Tier, Is.EqualTo(4));
            }
        }

        [Test]
        public void StarterSpiritTalentBuildSamples_ProduceDistinctTuningProfiles()
        {
            StarterSpiritSingleTalentTuning Tune(string name)
            {
                foreach (StarterSpiritTalentBuildSample build
                         in StarterSpiritTalentBuildSamples.All)
                {
                    if (build.Name != name)
                        continue;
                    var active = new HashSet<string>(build.TalentIds);
                    return StarterSpiritSingleTalentTuningBuilder.Build(
                        build.SpeciesId,
                        active.Contains);
                }
                Assert.Fail($"Missing build sample: {name}");
                return default;
            }

            StarterSpiritSingleTalentTuning sparkSpeed =
                Tune("火花狸·速燃");
            StarterSpiritSingleTalentTuning sparkBurst =
                Tune("火花狸·爆火");
            StarterSpiritSingleTalentTuning sparkHeat =
                Tune("火花狸·蓄热");
            Assert.That(sparkSpeed.WeaponHitsPerActivation, Is.EqualTo(2));
            Assert.That(sparkSpeed.RadiusBonus, Is.GreaterThan(0f));
            Assert.That(sparkSpeed.SparkFireTrail, Is.True);
            Assert.That(sparkSpeed.SparkChainBurn, Is.True);
            Assert.That(sparkBurst.DamageFactor, Is.GreaterThan(1.3f));
            Assert.That(sparkBurst.SparkBurstCore, Is.True);
            Assert.That(
                sparkHeat.RetainedWeaponProgress,
                Is.EqualTo(2));
            Assert.That(sparkHeat.SparkCarriedEmber, Is.True);

            StarterSpiritSingleTalentTuning echoClear =
                Tune("响响鸮·清响");
            StarterSpiritSingleTalentTuning echoFar =
                Tune("响响鸮·远听");
            StarterSpiritSingleTalentTuning echoMemory =
                Tune("响响鸮·留声");
            Assert.That(echoClear.DamageFactor, Is.GreaterThan(1.2f));
            Assert.That(echoClear.EchoDuet, Is.True);
            Assert.That(echoClear.EchoOnlyPoint, Is.True);
            Assert.That(echoFar.RadiusBonus, Is.GreaterThan(1f));
            Assert.That(echoFar.EchoMark, Is.True);
            Assert.That(echoFar.EchoValleyChorus, Is.True);
            Assert.That(echoMemory.EchoRetarget, Is.True);
            Assert.That(echoMemory.EchoSavedLine, Is.True);

            StarterSpiritSingleTalentTuning gelChain =
                Tune("弹弹胶·连弹");
            StarterSpiritSingleTalentTuning gelHeavy =
                Tune("弹弹胶·重击");
            StarterSpiritSingleTalentTuning gelReturn =
                Tune("弹弹胶·回弹");
            Assert.That(gelChain.GelTargetCount, Is.EqualTo(5));
            Assert.That(gelChain.RadiusBonus, Is.GreaterThan(0f));
            Assert.That(gelChain.GelPinball, Is.True);
            Assert.That(gelHeavy.DamageFactor, Is.GreaterThan(1.1f));
            Assert.That(gelHeavy.GelProjectileScale, Is.GreaterThan(0.3f));
            Assert.That(gelHeavy.GelWrapSpark, Is.True);
            Assert.That(gelHeavy.GelBigCrater, Is.True);
            Assert.That(gelReturn.RetainedWeaponProgress, Is.EqualTo(1));
            Assert.That(gelReturn.GelSoftLanding, Is.True);
            Assert.That(gelReturn.GelReturnStart, Is.True);
            Assert.That(gelReturn.GelAccelerating, Is.True);
        }

        [Test]
        public void SpiritTalentTreeVisuals_ExposeDistinctNodeGrammar()
        {
            Assert.That(SpiritTalentTreeVisuals.Circle, Is.Not.Null);
            Assert.That(SpiritTalentTreeVisuals.Star, Is.Not.Null);
            Assert.That(SpiritTalentTreeVisuals.Ultimate, Is.Not.Null);
            Assert.That(
                SpiritTalentTreeVisuals.Circle,
                Is.Not.SameAs(SpiritTalentTreeVisuals.Star));
            Assert.That(
                SpiritTalentTreeVisuals.Star,
                Is.Not.SameAs(SpiritTalentTreeVisuals.Ultimate));
            Assert.That(
                SpiritTalentTreeVisuals.RoundedPanel.border.x,
                Is.GreaterThan(0f));
            Assert.That(
                SpiritTalentTreeVisuals.RouteColor(0),
                Is.Not.EqualTo(
                    SpiritTalentTreeVisuals.RouteColor(1)));
        }

        [Test]
        public void SpiritRunTalentState_EarnsPointsAndHonorsPermanentLocks()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            var run = new SpiritRunTalentState(spirit);

            Assert.That(run.AddExperience(2), Is.EqualTo(1));
            Assert.That(run.Level, Is.EqualTo(1));
            Assert.That(run.UnspentPoints, Is.EqualTo(1));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.quick-temper")),
                Is.EqualTo(SpiritTalentActivationResult.Success));

            Assert.That(run.AddExperience(3), Is.EqualTo(1));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.chasing-fire")),
                Is.EqualTo(
                    SpiritTalentActivationResult.PermanentlyLocked));

            spirit.UnlockTalent(
                new StableConfigId("talent.spark.chasing-fire"));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.chasing-fire")),
                Is.EqualTo(SpiritTalentActivationResult.Success));
        }

        [Test]
        public void SpiritSaveMapper_RoundTripsUnlockedTalentNodes()
        {
            SpiritInstanceState source = CreateFirstPetState(
                FirstSpiritCircuitContent.EchoOwlSpecies);
            var talentId =
                new StableConfigId("talent.echo.long-hearing");
            source.SetTalentProgress(3, 1);
            source.UnlockTalent(talentId);

            SpiritInstanceSave save = SpiritSaveMapper.ToSave(source);
            bool restored = SpiritSaveMapper.TryRestore(
                save,
                out SpiritInstanceState target);

            Assert.That(restored, Is.True);
            Assert.That(save.unlockedTalentIds.Count, Is.EqualTo(1));
            Assert.That(target.IsTalentUnlocked(talentId), Is.True);
            Assert.That(target.TalentExpeditions, Is.EqualTo(3));
            Assert.That(target.TalentKeyVictories, Is.EqualTo(1));
        }

        [Test]
        public void SpiritTalentProgression_OpensTiersAtExpeditionMilestones()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);

            Assert.That(
                SpiritTalentPermanentProgression.RecordExpedition(
                    spirit,
                    false),
                Is.EqualTo(6));
            Assert.That(
                SpiritTalentPermanentProgression
                    .GetPermanentlyAvailableTier(spirit),
                Is.EqualTo(2));
            SpiritTalentPermanentProgression.RecordExpedition(
                spirit,
                false);
            Assert.That(
                SpiritTalentPermanentProgression.RecordExpedition(
                    spirit,
                    false),
                Is.EqualTo(6));
            for (int i = 0; i < 3; i++)
            {
                SpiritTalentPermanentProgression.RecordExpedition(
                    spirit,
                    i == 2);
            }

            Assert.That(
                SpiritTalentPermanentProgression
                    .GetPermanentlyAvailableTier(spirit),
                Is.EqualTo(4));
            Assert.That(spirit.UnlockedTalents.Count, Is.EqualTo(15));
        }

        [Test]
        public void SpiritTalentProgression_CommitsOnlyActiveRosterMembers()
        {
            SpiritInstanceState active = CreateFirstPetState(
                FirstSpiritCircuitContent.EchoOwlSpecies);
            SpiritInstanceState inactive = CreateFirstPetState(
                FirstSpiritCircuitContent.BounceGelSpecies);
            var save = new SaveDataV1
            {
                spiritRoster = new System.Collections.Generic.List<
                    SpiritInstanceSave>
                {
                    SpiritSaveMapper.ToSave(active),
                    SpiritSaveMapper.ToSave(inactive)
                },
                activeSpiritInstanceGuids =
                    new System.Collections.Generic.List<string>
                    {
                        active.Identity.InstanceId.ToString("N")
                    }
            };

            int unlocked =
                SpiritTalentPermanentProgression.CommitActiveExpedition(
                    save,
                    false);

            Assert.That(unlocked, Is.EqualTo(6));
            Assert.That(
                SpiritSaveMapper.TryRestore(
                    save.spiritRoster[0],
                    out SpiritInstanceState restoredActive),
                Is.True);
            Assert.That(
                SpiritSaveMapper.TryRestore(
                    save.spiritRoster[1],
                    out SpiritInstanceState restoredInactive),
                Is.True);
            Assert.That(restoredActive.TalentExpeditions, Is.EqualTo(1));
            Assert.That(restoredInactive.TalentExpeditions, Is.Zero);
        }

        [Test]
        public void SpiritRunTalentState_RefundsOnlyRunSelections()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.BounceGelSpecies);
            var run = new SpiritRunTalentState(spirit);
            run.AddExperience(2);
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.gel.more-elastic")),
                Is.EqualTo(SpiritTalentActivationResult.Success));

            Assert.That(run.RefundActiveTalents(), Is.EqualTo(1));
            Assert.That(run.UnspentPoints, Is.EqualTo(1));
            Assert.That(run.ActiveTalents.Count, Is.Zero);
            Assert.That(spirit.UnlockedTalents.Count, Is.Zero);
        }

        [Test]
        public void SpiritRunTalentState_AllowsOnlyOneCapstonePerSpirit()
        {
            SpiritInstanceState spirit = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            foreach (SpiritTalentDefinition definition
                     in StarterSpiritTalentCatalog.ForSpecies(
                         spirit.Identity.SpeciesConfigId))
            {
                if (definition.Tier > 1)
                    spirit.UnlockTalent(definition.ConfigId);
            }
            var run = new SpiritRunTalentState(spirit);
            run.AddExperience(65);
            string[] firstPath =
            {
                "talent.spark.bright-spark",
                "talent.spark.spark-shards",
                "talent.spark.compressed-flame",
                "talent.spark.echo-fed-heat",
                "talent.spark.burst-core"
            };
            string[] secondPath =
            {
                "talent.spark.quick-temper",
                "talent.spark.chasing-fire",
                "talent.spark.swift-spark",
                "talent.spark.fire-trail"
            };
            foreach (string id in firstPath)
            {
                Assert.That(
                    run.TryActivate(new StableConfigId(id)),
                    Is.EqualTo(SpiritTalentActivationResult.Success));
            }
            foreach (string id in secondPath)
            {
                Assert.That(
                    run.TryActivate(new StableConfigId(id)),
                    Is.EqualTo(SpiritTalentActivationResult.Success));
            }

            Assert.That(run.UnspentPoints, Is.EqualTo(1));
            Assert.That(
                run.TryActivate(
                    new StableConfigId("talent.spark.chain-burn")),
                Is.EqualTo(
                    SpiritTalentActivationResult.ConflictingCapstone));
            Assert.That(run.UnspentPoints, Is.EqualTo(1));
        }

        [Test]
        public void FirstPetCircuitTuning_ChangesHeatThresholdAndRefund()
        {
            var circuit = new FirstSpiritCircuitRuntime(
                CreateFirstPetState(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                CarrierSlot.Weapon,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                CarrierSlot.TechniqueQ,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                CarrierSlot.Mobility,
                1,
                new StableConfigId("player.tuned"),
                tuning: new FirstSpiritCircuitTuning(
                    2, 0.6f, 0.8f, 11f, 20f, 2));
            var instigator = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.Empty);

            circuit.BeginFrame();
            Assert.That(
                circuit.RegisterDirectHit(1, 0f, instigator).ChainStarted,
                Is.False);
            Assert.That(
                circuit.RegisterDirectHit(2, 0f, instigator).ChainStarted,
                Is.True);
            Assert.That(
                circuit.RegisterBounceResolved(circuit.Events[3]),
                Is.True);
            Assert.That(circuit.Heat, Is.EqualTo(1));
            Assert.That(
                circuit.Tuning.EchoDamageMultiplier,
                Is.EqualTo(0.6f));
        }

        [Test]
        public void FirstPetCircuitTuning_ExposesDevelopmentEffects()
        {
            var tuning = new FirstSpiritCircuitTuning(
                3,
                0.45f,
                0.7f,
                12f,
                20f,
                1,
                retainedHeatOnSpark: 1,
                sparkShardDamageMultiplier: 0.2f,
                duetDamageMultiplier: 0.225f,
                echoMarkDuration: 4f,
                maxProjectileTargets: 3,
                projectileScale: 0.32f,
                softLandingDamageMultiplier: 0.25f,
                bounceDelay: 0.2f,
                initialEchoDelay: 0.2f,
                echoHitHeatRefund: 1,
                fireTrailDamageMultiplier: 0.15f,
                threefoldConfirm: true,
                echoJump: true,
                wrappedSparkDamageMultiplier: 0.18f,
                returnStartDamageMultiplier: 0.3f,
                chainBurn: true,
                burstCore: true,
                carriedEmber: true,
                valleyChorus: true,
                onlyThePoint: true,
                savedLine: true,
                pinball: true,
                bigCrater: true,
                acceleratingBounce: true);
            var heat = new FirstSpiritHeatRuntime();
            heat.Configure(
                tuning.DirectHitsPerSpark,
                tuning.RetainedHeatOnSpark);

            heat.RegisterDirectHit();
            heat.RegisterDirectHit();
            Assert.That(heat.RegisterDirectHit(), Is.True);
            Assert.That(heat.Heat, Is.EqualTo(1));
            Assert.That(tuning.MaxProjectileTargets, Is.EqualTo(3));
            Assert.That(tuning.SparkShardDamageMultiplier, Is.EqualTo(0.2f));
            Assert.That(tuning.DuetDamageMultiplier, Is.EqualTo(0.225f));
            Assert.That(tuning.EchoMarkDuration, Is.EqualTo(4f));
            Assert.That(tuning.ProjectileScale, Is.EqualTo(0.32f));
            Assert.That(tuning.SoftLandingDamageMultiplier, Is.EqualTo(0.25f));
            Assert.That(tuning.BounceDelay, Is.EqualTo(0.2f));
            Assert.That(tuning.InitialEchoDelay, Is.EqualTo(0.2f));
            Assert.That(tuning.EchoHitHeatRefund, Is.EqualTo(1));
            Assert.That(tuning.FireTrailDamageMultiplier, Is.EqualTo(0.15f));
            Assert.That(tuning.ThreefoldConfirm, Is.True);
            Assert.That(tuning.EchoJump, Is.True);
            Assert.That(
                tuning.WrappedSparkDamageMultiplier,
                Is.EqualTo(0.18f));
            Assert.That(
                tuning.ReturnStartDamageMultiplier,
                Is.EqualTo(0.3f));
            Assert.That(tuning.ChainBurn, Is.True);
            Assert.That(tuning.BurstCore, Is.True);
            Assert.That(tuning.CarriedEmber, Is.True);
            Assert.That(tuning.ValleyChorus, Is.True);
            Assert.That(tuning.OnlyThePoint, Is.True);
            Assert.That(tuning.SavedLine, Is.True);
            Assert.That(tuning.Pinball, Is.True);
            Assert.That(tuning.BigCrater, Is.True);
            Assert.That(tuning.AcceleratingBounce, Is.True);
        }

        [Test]
        public void FirstPetCircuit_LinkTalentRefundsHeatOnlyOnEcho()
        {
            var circuit = new FirstSpiritCircuitRuntime(
                CreateFirstPetState(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                CarrierSlot.Weapon,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                CarrierSlot.TechniqueQ,
                CreateFirstPetState(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                CarrierSlot.Mobility,
                1,
                new StableConfigId("player.link-tuned"),
                tuning: new FirstSpiritCircuitTuning(
                    3,
                    0.45f,
                    0.7f,
                    8f,
                    16f,
                    1,
                    echoHitHeatRefund: 1));
            var instigator = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.Empty);
            circuit.BeginFrame();
            circuit.RegisterDirectHit(1, 0f, instigator);
            circuit.RegisterDirectHit(2, 0f, instigator);
            circuit.RegisterDirectHit(3, 0f, instigator);

            Assert.That(
                circuit.RegisterEchoResolved(circuit.Events[2]),
                Is.True);
            Assert.That(circuit.Heat, Is.EqualTo(1));
            Assert.That(
                circuit.RegisterEchoResolved(circuit.Events[3]),
                Is.False);
            Assert.That(circuit.Heat, Is.EqualTo(1));
        }

        [Test]
        public void SaveSystem_MigratesSpiritFieldsToSchemaEight()
        {
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            Guid third = Guid.NewGuid();
            Guid fourth = Guid.NewGuid();
            SpiritInstanceSave CreateSave(Guid id) => new()
            {
                instanceGuid = id.ToString("N"),
                speciesConfigId = "spirit.migration",
                primaryPersonalityId = "personality.migration"
            };
            var data = new SaveDataV1
            {
                schemaVersion = 4,
                spiritRoster =
                    new System.Collections.Generic.List<
                        SpiritInstanceSave>
                    {
                        CreateSave(first),
                        CreateSave(second),
                        CreateSave(third),
                        CreateSave(fourth)
                    },
                activeSpiritInstanceGuids =
                    new System.Collections.Generic.List<string>
                    {
                        first.ToString(),
                        first.ToString("N"),
                        "invalid",
                        second.ToString("N"),
                        third.ToString("N"),
                        fourth.ToString("N")
                    },
                starterSpiritSpeciesId =
                    FirstSpiritCircuitContent
                        .SparkRaccoonSpecies.Value
            };
            var method = typeof(SaveSystem).GetMethod(
                "NormalizeAndMigrate",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            bool changed = (bool)method.Invoke(
                null,
                new object[] { data });

            Assert.That(changed, Is.True);
            Assert.That(data.schemaVersion, Is.EqualTo(9));
            Assert.That(data.spiritRoster, Is.Not.Null);
            Assert.That(
                data.activeSpiritInstanceGuids.Count,
                Is.EqualTo(3));
            Assert.That(
                data.activeSpiritInstanceGuids[0],
                Is.EqualTo(first.ToString("N")));
            Assert.That(data.starterTechniqueUnlocked, Is.True);
            Assert.That(
                data.starterPrologueStep,
                Is.EqualTo(
                    (int)StarterPrologueStep.StarterChosen));
            Assert.That(data.starterPrologueCarrier, Is.EqualTo(-1));
        }

        [Test]
        public void StarterSpiritChoice_AddsExactlyOneSelectedPet()
        {
            var save = new SaveDataV1();
            Guid instanceId = Guid.NewGuid();

            StarterSpiritChoiceResult result =
                StarterSpiritChoice.TryChoose(
                    save,
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    out SpiritInstanceState spirit,
                    () => instanceId);

            Assert.That(
                result,
                Is.EqualTo(StarterSpiritChoiceResult.Success));
            Assert.That(spirit.Identity.InstanceId, Is.EqualTo(instanceId));
            Assert.That(save.spiritRoster.Count, Is.EqualTo(1));
            Assert.That(
                save.activeSpiritInstanceGuids,
                Is.EqualTo(new[] { instanceId.ToString("N") }));
            Assert.That(
                save.starterSpiritSpeciesId,
                Is.EqualTo(
                    FirstSpiritCircuitContent.EchoOwlSpecies.Value));
            Assert.That(save.starterTechniqueUnlocked, Is.True);
            Assert.That(
                save.starterPrologueStep,
                Is.EqualTo(
                    (int)StarterPrologueStep.StarterChosen));
            Assert.That(
                spirit.Identity.PrimaryPersonalityId,
                Is.EqualTo(StarterSpiritChoice.DefaultPersonality));
        }

        [Test]
        public void SaveSystem_MigratesOldCompletedPrologueToSchemaNine()
        {
            var data = new SaveDataV1
            {
                schemaVersion = 8,
                starterSpiritSpeciesId =
                    FirstSpiritCircuitContent
                        .SparkRaccoonSpecies.Value,
                starterPrologueStep = 3,
                starterPrologueCarrier =
                    (int)CarrierSlot.Weapon,
                starterTechniqueUnlocked = true
            };
            var method = typeof(SaveSystem).GetMethod(
                "NormalizeAndMigrate",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);

            bool changed = (bool)method.Invoke(
                null,
                new object[] { data });

            Assert.That(changed, Is.True);
            Assert.That(data.schemaVersion, Is.EqualTo(9));
            Assert.That(
                data.starterPrologueStep,
                Is.EqualTo(
                    (int)StarterPrologueStep.Completed));
        }

        [Test]
        public void StarterSpiritChoice_RejectsRepeatAndNonEmptyRoster()
        {
            var chosenSave = new SaveDataV1();
            StarterSpiritChoice.TryChoose(
                chosenSave,
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                out _);

            StarterSpiritChoiceResult repeated =
                StarterSpiritChoice.TryChoose(
                    chosenSave,
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    out _);
            SaveDataV1 occupiedSave = CreateFirstPetSaveData();
            occupiedSave.starterSpiritSpeciesId = "";
            StarterSpiritChoiceResult occupied =
                StarterSpiritChoice.TryChoose(
                    occupiedSave,
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    out _);

            Assert.That(
                repeated,
                Is.EqualTo(
                    StarterSpiritChoiceResult.AlreadyChosen));
            Assert.That(chosenSave.spiritRoster.Count, Is.EqualTo(1));
            Assert.That(
                occupied,
                Is.EqualTo(
                    StarterSpiritChoiceResult.RosterNotEmpty));
            Assert.That(occupiedSave.spiritRoster.Count, Is.EqualTo(3));
        }

        [Test]
        public void StarterSpiritChoicePresentation_ExposesOnlyThreeProfiles()
        {
            Assert.That(
                StarterSpiritChoicePresentation.Profiles.Count,
                Is.EqualTo(3));
            for (int i = 0;
                 i < StarterSpiritChoicePresentation.Profiles.Count;
                 i++)
            {
                StarterSpiritChoiceProfile profile =
                    StarterSpiritChoicePresentation.Profiles[i];
                Assert.That(
                    profile.SpeciesId,
                    Is.EqualTo(StarterSpiritChoice.Options[i]));
                Assert.That(profile.DisplayName, Is.Not.Empty);
                Assert.That(profile.PersonalityLine, Is.Not.Empty);
                Assert.That(profile.CombatTendency, Is.Not.Empty);
            }
        }

        [Test]
        public void StarterSpiritChoiceSession_RequiresExplicitConfirmation()
        {
            var save = new SaveDataV1();
            var session = new StarterSpiritChoiceSession();

            Assert.That(
                session.Commit(save, out _),
                Is.EqualTo(
                    StarterSpiritChoiceSubmitResult.NotConfirming));
            Assert.That(save.spiritRoster, Is.Empty);
            Assert.That(
                session.BeginConfirmation(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                Is.True);
            Assert.That(session.CancelConfirmation(), Is.True);
            Assert.That(save.spiritRoster, Is.Empty);
            Assert.That(
                session.BeginConfirmation(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                Is.True);
            Assert.That(
                session.Commit(save, out SpiritInstanceState spirit),
                Is.EqualTo(
                    StarterSpiritChoiceSubmitResult.Success));
            Assert.That(
                spirit.Identity.SpeciesConfigId,
                Is.EqualTo(
                    FirstSpiritCircuitContent.EchoOwlSpecies));
            Assert.That(
                session.Stage,
                Is.EqualTo(StarterSpiritChoiceStage.Committed));
            Assert.That(
                session.Commit(save, out _),
                Is.EqualTo(
                    StarterSpiritChoiceSubmitResult.NotConfirming));
            Assert.That(save.spiritRoster.Count, Is.EqualTo(1));
        }

        [Test]
        public void StarterSpiritChoiceUI_RequiresOneEntityPerProfile()
        {
            var objects = new GameObject[3];
            var entities = new StarterSpiritChoiceWorldEntity[3];
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    objects[i] = new GameObject($"Starter-{i}");
                    entities[i] = objects[i]
                        .AddComponent<StarterSpiritChoiceWorldEntity>();
                    entities[i].Configure(
                        StarterSpiritChoice.Options[i]);
                }

                Assert.That(
                    StarterSpiritChoiceUI.HasCompleteEntitySet(entities),
                    Is.True);
                Assert.That(
                    StarterSpiritChoiceUI.HasCompleteEntitySet(
                        new[]
                        {
                            entities[0],
                            entities[1],
                            entities[0]
                        }),
                    Is.False);

                Vector3 baseScale = entities[0].transform.localScale;
                entities[0].SetFocused(true);
                Assert.That(
                    entities[0].transform.localScale.sqrMagnitude,
                    Is.GreaterThan(baseScale.sqrMagnitude));
                entities[0].SetFocused(false);
                Assert.That(
                    entities[0].transform.localScale,
                    Is.EqualTo(baseScale));
            }
            finally
            {
                foreach (GameObject go in objects)
                {
                    if (go != null)
                        UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        [Test]
        public void StarterPrologueScene_LoadsReusableWhiteboxPrefab()
        {
            const string prefabPath =
                "Assets/1Game/Prefabs/Tutorial/" +
                "StarterPrologueWhitebox.prefab";
            const string activeLayoutPath =
                "Assets/1Game/Prefabs/Tutorial/" +
                "StarterPrologueLayoutV3.prefab";
            const string scenePath =
                "Assets/1Game/Scenes/StarterPrologue.unity";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueMarker>(true).Length,
                Is.EqualTo(6));
            Assert.That(
                System.Array.Exists(
                    prefab.GetComponentsInChildren<
                        StarterPrologueMarker>(true),
                    marker =>
                        marker.Kind ==
                        StarterPrologueMarkerKind.RescueResolved),
                Is.True);
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterSpiritChoiceWorldEntity>(true).Length,
                Is.EqualTo(3));
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueChoiceTrigger>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueAttachmentGate>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueEnemySpawnMarker>(true).Length,
                Is.EqualTo(3));
            StarterPrologueEnemySpawnMarker[] encounterMarkers =
                prefab.GetComponentsInChildren<
                    StarterPrologueEnemySpawnMarker>(true);
            Assert.That(
                encounterMarkers.Count(
                    marker => marker.Role ==
                              StarterPrologueEnemyRole.Melee),
                Is.EqualTo(2));
            Assert.That(
                encounterMarkers.Count(
                    marker => marker.Role ==
                              StarterPrologueEnemyRole.Ranged),
                Is.EqualTo(1));
            Assert.That(
                prefab.transform.Find(
                    "Geometry/HomeOutskirts_Floor"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find(
                    "Geometry/PossessionClearing_Floor"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find(
                    "Interactions/RescueCaretaker_Whitebox"),
                Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueTrainingEncounter>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                prefab.GetComponentsInChildren<
                    StarterPrologueCompletionGate>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.GetDependencies(scenePath, true),
                Does.Contain(activeLayoutPath));
        }

        [Test]
        public void ProjectRUITheme_LoadsGeneratedCoreArt()
        {
            ProjectRUITheme theme =
                Resources.Load<ProjectRUITheme>(
                    "UI/ProjectR/ProjectRUITheme");

            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.HasCompleteCoreSet(), Is.True);
            Assert.That(theme.HasCompleteHudChrome(), Is.True);
            Assert.That(
                theme.SpiritPortrait(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                Is.Not.Null);
            Assert.That(
                theme.SpiritPortrait(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                Is.Not.Null);
            Assert.That(
                theme.SpiritPortrait(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                Is.Not.Null);
            for (int i = 0; i < 5; i++)
                Assert.That(theme.CarrierIcon(i), Is.Not.Null);
            Assert.That(theme.Health, Is.Not.Null);
            Assert.That(theme.HealthFrame, Is.Not.Null);
            Assert.That(theme.SpiritFrame, Is.Not.Null);
            Assert.That(theme.SkillFrame, Is.Not.Null);
            Assert.That(theme.SkillFrameActive, Is.Not.Null);
            Assert.That(theme.SkillFrameLocked, Is.Not.Null);
            Assert.That(theme.Keycap, Is.Not.Null);
            Assert.That(theme.TalentTreePanel, Is.Not.Null);
            Assert.That(
                theme.TalentIconAtlas(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                Is.Not.Null);
            Assert.That(
                theme.TalentIconAtlas(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                Is.Not.Null);
            Assert.That(
                theme.TalentIconAtlas(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                Is.Not.Null);
            Assert.That(
                theme.TalentIllustration(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies),
                Is.Not.Null);
            Assert.That(
                theme.TalentIllustration(
                    FirstSpiritCircuitContent.EchoOwlSpecies),
                Is.Not.Null);
            Assert.That(
                theme.TalentIllustration(
                    FirstSpiritCircuitContent.BounceGelSpecies),
                Is.Not.Null);
        }

        [Test]
        public void StarterPrologueTraining_RunsAgitatedThenPossessedWaves()
        {
            var runtime = new StarterPrologueTrainingRuntime();

            Assert.That(
                runtime.Begin(new[] { 1, 1 }),
                Is.EqualTo(
                    StarterPrologueTrainingStartResult.InvalidRoster));
            Assert.That(
                runtime.Begin(new[] { 11, 22 }),
                Is.EqualTo(
                    StarterPrologueTrainingStartResult.Success));
            Assert.That(
                runtime.CurrentWave,
                Is.EqualTo(
                    StarterPrologueTrainingWave.AgitatedSpirits));
            Assert.That(
                runtime.BeginPossessedHost(new[] { 44 }),
                Is.EqualTo(
                    StarterPrologueTrainingStartResult
                        .InvalidTransition));
            Assert.That(
                runtime.RegisterDefeated(99).Remaining,
                Is.EqualTo(2));
            Assert.That(
                runtime.RegisterDefeated(11).Completed,
                Is.False);
            Assert.That(
                runtime.RegisterDefeated(11).Remaining,
                Is.EqualTo(1));
            StarterPrologueTrainingStep completed =
                runtime.RegisterDefeated(22);
            Assert.That(completed.Completed, Is.True);
            Assert.That(completed.Remaining, Is.Zero);
            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(
                runtime.RegisterDefeated(33).Completed,
                Is.False);

            Assert.That(
                runtime.BeginPossessedHost(new[] { 44 }),
                Is.EqualTo(
                    StarterPrologueTrainingStartResult.Success));
            Assert.That(
                runtime.CurrentWave,
                Is.EqualTo(
                    StarterPrologueTrainingWave.PossessedHost));
            StarterPrologueTrainingStep encounterCompleted =
                runtime.RegisterDefeated(44);
            Assert.That(encounterCompleted.Completed, Is.True);
            Assert.That(
                runtime.CurrentWave,
                Is.EqualTo(
                    StarterPrologueTrainingWave.Completed));
        }

        [Test]
        public void StarterPrologueCombatTargets_AcceptBounceGelImpulse()
        {
            Assert.That(
                typeof(ICombatImpulseReceiver).IsAssignableFrom(
                    typeof(EnemyBase)),
                Is.True);
            Assert.That(
                typeof(ICombatImpulseReceiver).IsAssignableFrom(
                    typeof(StarterProloguePossessedHost)),
                Is.True);
        }

        [Test]
        public void StarterPrologueResume_UsesCheckpointMarkers()
        {
            var save = new SaveDataV1();
            Assert.That(
                StarterPrologueSceneBootstrap.ResumeMarkerFor(save),
                Is.EqualTo(
                    StarterPrologueMarkerKind.PlayerSpawn));

            save.starterPrologueStep =
                (int)StarterPrologueStep.StarterChosen;
            Assert.That(
                StarterPrologueSceneBootstrap.ResumeMarkerFor(save),
                Is.EqualTo(
                    StarterPrologueMarkerKind.RescueClearing));

            save.starterPrologueStep =
                (int)StarterPrologueStep.AttachmentChosen;
            Assert.That(
                StarterPrologueSceneBootstrap.ResumeMarkerFor(save),
                Is.EqualTo(
                    StarterPrologueMarkerKind.PossessionClearing));

            save.starterPrologueStep =
                (int)StarterPrologueStep.RescueCompleted;
            Assert.That(
                StarterPrologueSceneBootstrap.ResumeMarkerFor(save),
                Is.EqualTo(
                    StarterPrologueMarkerKind.RescueResolved));

            save.starterPrologueStep =
                (int)StarterPrologueStep.Completed;
            Assert.That(
                StarterPrologueSceneBootstrap.ResumeMarkerFor(save),
                Is.EqualTo(
                    StarterPrologueMarkerKind.HomeReturn));
        }

        [Test]
        public void StarterPrologue_RequiresExactlyOneFirstAttachment()
        {
            var save = new SaveDataV1();
            Assert.That(
                StarterPrologueProgression.RequiresFirstAttachment(save),
                Is.False);

            StarterSpiritChoice.TryChoose(
                save,
                FirstSpiritCircuitContent.BounceGelSpecies,
                out _);
            Assert.That(
                StarterPrologueProgression.RequiresFirstAttachment(save),
                Is.True);

            StarterPrologueProgression.RecordAttachment(
                save,
                CarrierSlot.Mobility);
            Assert.That(
                StarterPrologueProgression.RequiresFirstAttachment(save),
                Is.False);

            StarterPrologueProgression.RecordRescueCompleted(save);
            Assert.That(
                StarterPrologueProgression.RequiresFirstAttachment(save),
                Is.False);
        }

        [Test]
        public void StarterPrologue_AdvancesMonotonicallyAndRestoresCarrier()
        {
            var save = new SaveDataV1();
            StarterSpiritChoice.TryChoose(
                save,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                out _);

            Assert.That(
                StarterPrologueProgression.RecordAttachment(
                    save,
                    CarrierSlot.TechniqueQ),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.Success));
            Assert.That(
                StarterPrologueProgression.RecordAttachment(
                    save,
                    CarrierSlot.TechniqueQ),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.NoChange));
            Assert.That(
                StarterPrologueProgression.RecordRescueCompleted(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.Success));
            Assert.That(
                StarterPrologueProgression.GetStep(save),
                Is.EqualTo(
                    StarterPrologueStep.RescueCompleted));
            Assert.That(
                StarterPrologueProgression.RecordPrologueCompleted(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.Success));
            Assert.That(
                StarterPrologueProgression.RecordAttachment(
                    save,
                    CarrierSlot.Mobility),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.NoChange));
            Assert.That(
                StarterPrologueProgression.TryGetAttachment(
                    save,
                    out CarrierSlot restored),
                Is.True);
            Assert.That(restored, Is.EqualTo(CarrierSlot.TechniqueQ));
        }

        [Test]
        public void StarterPrologue_RejectsSkippedOrInvalidProgress()
        {
            var save = new SaveDataV1();

            Assert.That(
                StarterPrologueProgression.RecordAttachment(
                    save,
                    CarrierSlot.Weapon),
                Is.EqualTo(
                    StarterPrologueAdvanceResult
                        .PrerequisiteMissing));
            StarterSpiritChoice.TryChoose(
                save,
                FirstSpiritCircuitContent.BounceGelSpecies,
                out _);
            Assert.That(
                StarterPrologueProgression.RecordRescueCompleted(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult
                        .PrerequisiteMissing));
            Assert.That(
                StarterPrologueProgression
                    .RecordPrologueCompleted(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult
                        .PrerequisiteMissing));
            Assert.That(
                StarterPrologueProgression.RecordAttachment(
                    save,
                    CarrierSlot.TechniqueE),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.InvalidCarrier));
        }

        [Test]
        public void StarterPrologueHomeTransition_CommitsOnlyAfterRescue()
        {
            var save = new SaveDataV1();
            Assert.That(
                StarterPrologueHomeTransition.CanTransition(
                    StarterPrologueProgression.GetStep(save)),
                Is.False);
            Assert.That(
                ProjectRHomeSceneBootstrap.TryCommitArrival(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult
                        .PrerequisiteMissing));

            StarterSpiritChoice.TryChoose(
                save,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                out _);
            StarterPrologueProgression.RecordAttachment(
                save,
                CarrierSlot.TechniqueQ);
            StarterPrologueProgression.RecordRescueCompleted(
                save);

            Assert.That(
                StarterPrologueHomeTransition.CanTransition(
                    StarterPrologueProgression.GetStep(save)),
                Is.True);
            Assert.That(
                ProjectRHomeSceneBootstrap.TryCommitArrival(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.Success));
            Assert.That(
                StarterPrologueProgression.GetStep(save),
                Is.EqualTo(StarterPrologueStep.Completed));
            Assert.That(
                ProjectRHomeSceneBootstrap.TryCommitArrival(save),
                Is.EqualTo(
                    StarterPrologueAdvanceResult.NoChange));
            Assert.That(
                StarterPrologueHomeTransition.HomeSceneName,
                Is.EqualTo("ProjectRHome"));
        }

        [Test]
        public void StarterPrologueTiming_UsesInclusiveSixToSevenPointFiveMinuteTarget()
        {
            Assert.That(
                StarterPrologueTimingTracker.IsTargetDuration(359f),
                Is.False);
            Assert.That(
                StarterPrologueTimingTracker.IsTargetDuration(360f),
                Is.True);
            Assert.That(
                StarterPrologueTimingTracker.IsTargetDuration(450f),
                Is.True);
            Assert.That(
                StarterPrologueTimingTracker.IsTargetDuration(451f),
                Is.False);
            Assert.That(
                StarterPrologueTimingTracker.Format(405f),
                Is.EqualTo("06:45"));
        }

        [Test]
        public void StarterPrologueOpening_UsesV5NonBlockingLines()
        {
            Assert.That(
                StarterPrologueOpeningBeat.ShouldPlay(
                    StarterPrologueStep.NotStarted),
                Is.True);
            Assert.That(
                StarterPrologueOpeningBeat.ShouldPlay(
                    StarterPrologueStep.StarterChosen),
                Is.False);
            Assert.That(
                StarterPrologueOpeningBeat.ProtagonistLine,
                Is.EqualTo("照料员怎么还没来……"));
            Assert.That(
                StarterPrologueOpeningBeat.MovementObjective,
                Is.EqualTo("沿林间小路去看看"));
            Assert.That(
                StarterPrologueChoiceTrigger.CaretakerWarning,
                Is.EqualTo(
                    "先别过来！它们被失控的能量惊到了！"));
        }

        [Test]
        public void StarterPrologueObjective_LabelsDistinctPhases()
        {
            Assert.That(
                StarterPrologueObjectiveHUD.PhaseLabelFor(
                    StarterPrologueStep.NotStarted,
                    "前往救援空地"),
                Is.EqualTo("序章 · 遇险"));
            Assert.That(
                StarterPrologueObjectiveHUD.PhaseLabelFor(
                    StarterPrologueStep.StarterChosen,
                    "将灵宠附着到一个动作"),
                Is.EqualTo("初契 · 选择附着"));
            Assert.That(
                StarterPrologueObjectiveHUD.PhaseLabelFor(
                    StarterPrologueStep.AttachmentChosen,
                    "平息躁动灵宠"),
                Is.EqualTo("救援 · 躁动灵宠"));
            Assert.That(
                StarterPrologueObjectiveHUD.PhaseLabelFor(
                    StarterPrologueStep.AttachmentChosen,
                    "稳定失控宿主"),
                Is.EqualTo("救援 · 失控宿主"));
            Assert.That(
                StarterPrologueObjectiveHUD.PhaseLabelFor(
                    StarterPrologueStep.RescueCompleted,
                    "准备返回家园"),
                Is.EqualTo("转场 · 家园"));
        }

        [Test]
        public void StarterProloguePathGuide_BuildsThreeOrderedPoints()
        {
            Vector3[] points =
                StarterProloguePathGuide.BuildGuidePoints(
                    Vector3.zero,
                    Vector3.forward * 10f);

            Assert.That(points.Length, Is.EqualTo(3));
            Assert.That(points[0].z, Is.LessThan(points[1].z));
            Assert.That(points[1].z, Is.LessThan(points[2].z));
            Assert.That(points[0].z, Is.GreaterThan(0f));
            Assert.That(points[2].z, Is.LessThan(10f));
            Assert.That(points[0].y, Is.GreaterThan(0.5f));
        }

        [Test]
        public void StarterPrologueBoundary_RevealsOnlyNearItsEdge()
        {
            float center =
                StarterPrologueCombatBoundary.EvaluateRingAlpha(
                    0f,
                    10f,
                    0.5f);
            float nearEdge =
                StarterPrologueCombatBoundary.EvaluateRingAlpha(
                    9.5f,
                    10f,
                    0.5f);
            float outside =
                StarterPrologueCombatBoundary.EvaluateRingAlpha(
                    10.1f,
                    10f,
                    0.5f);

            Assert.That(center, Is.LessThan(0.1f));
            Assert.That(nearEdge, Is.GreaterThan(center));
            Assert.That(outside, Is.GreaterThan(nearEdge));
            Assert.That(
                StarterPrologueCombatBoundary.EvaluateRingAlpha(
                    1f,
                    0f,
                    0.5f),
                Is.EqualTo(0f));
        }

        [Test]
        public void SpiritAttachmentLayout_RecognizesThreePatterns()
        {
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            Guid third = Guid.NewGuid();

            var oneOneOne = new SpiritAttachmentLayout();
            oneOneOne.TryAttach(first, CarrierSlot.Weapon);
            oneOneOne.TryAttach(second, CarrierSlot.TechniqueQ);
            oneOneOne.TryAttach(third, CarrierSlot.Mobility);

            var twoOne = new SpiritAttachmentLayout();
            twoOne.TryAttach(first, CarrierSlot.Weapon);
            twoOne.TryAttach(second, CarrierSlot.Weapon);
            twoOne.TryAttach(third, CarrierSlot.Mobility);

            var threeZero = new SpiritAttachmentLayout();
            threeZero.TryAttach(first, CarrierSlot.Weapon);
            threeZero.TryAttach(second, CarrierSlot.Weapon);
            threeZero.TryAttach(third, CarrierSlot.Weapon);

            Assert.That(
                oneOneOne.Pattern,
                Is.EqualTo(SpiritAttachmentPattern.OneOneOne));
            Assert.That(
                twoOne.Pattern,
                Is.EqualTo(SpiritAttachmentPattern.TwoOne));
            Assert.That(
                threeZero.Pattern,
                Is.EqualTo(SpiritAttachmentPattern.ThreeZero));
        }

        [Test]
        public void SpiritAttachmentLayout_MovesWithoutDroppingStateAndCapsRoster()
        {
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            Guid third = Guid.NewGuid();
            Guid fourth = Guid.NewGuid();
            var state = new SpiritInstanceState(new SpiritIdentity(
                first,
                new StableConfigId("spirit.fox"),
                new StableConfigId("personality.bold")));
            state.LearnTemporary(new SpiritEnlightenment(
                new StableConfigId("enlightenment.echo"),
                EnlightenmentKind.Response));
            var layout = new SpiritAttachmentLayout();

            Assert.That(
                layout.TryAttach(first, CarrierSlot.Weapon),
                Is.True);
            Assert.That(
                layout.TryAttach(second, CarrierSlot.TechniqueQ),
                Is.True);
            Assert.That(
                layout.TryAttach(third, CarrierSlot.Mobility),
                Is.True);
            Assert.That(
                layout.TryAttach(fourth, CarrierSlot.TechniqueE),
                Is.False);
            Assert.That(
                layout.TryAttach(first, CarrierSlot.TechniqueR),
                Is.True);
            Assert.That(layout.Count, Is.EqualTo(3));
            Assert.That(
                layout.TryGetCarrier(first, out CarrierSlot carrier),
                Is.True);
            Assert.That(carrier, Is.EqualTo(CarrierSlot.TechniqueR));
            Assert.That(state.TemporaryEnlightenments.Count, Is.EqualTo(1));
        }

        [Test]
        public void SpiritLoadoutRuntime_AddsThreeAndRejectsFourth()
        {
            var runtime = new SpiritLoadoutRuntime();
            SpiritInstanceState first = CreateSpiritState("first");
            SpiritInstanceState second = CreateSpiritState("second");
            SpiritInstanceState third = CreateSpiritState("third");
            SpiritInstanceState fourth = CreateSpiritState("fourth");

            Assert.That(
                runtime.Add(first, CarrierSlot.Weapon),
                Is.EqualTo(SpiritLoadoutChangeResult.Success));
            Assert.That(
                runtime.Add(second, CarrierSlot.TechniqueQ),
                Is.EqualTo(SpiritLoadoutChangeResult.Success));
            Assert.That(
                runtime.Add(third, CarrierSlot.Mobility),
                Is.EqualTo(SpiritLoadoutChangeResult.Success));
            Assert.That(
                runtime.Add(fourth, CarrierSlot.TechniqueE),
                Is.EqualTo(SpiritLoadoutChangeResult.RosterFull));
            Assert.That(
                runtime.Pattern,
                Is.EqualTo(SpiritAttachmentPattern.OneOneOne));
        }

        [Test]
        public void SpiritLoadoutRuntime_BlocksMigrationInCombat()
        {
            var runtime = new SpiritLoadoutRuntime();
            SpiritInstanceState spirit = CreateSpiritState("guarded");
            runtime.Add(spirit, CarrierSlot.Weapon);

            SpiritLoadoutChangeResult result = runtime.Migrate(
                spirit.Identity.InstanceId,
                CarrierSlot.Mobility,
                true);

            Assert.That(
                result,
                Is.EqualTo(SpiritLoadoutChangeResult.BlockedInCombat));
            Assert.That(
                runtime.TryGetCarrier(
                    spirit.Identity.InstanceId,
                    out CarrierSlot carrier),
                Is.True);
            Assert.That(carrier, Is.EqualTo(CarrierSlot.Weapon));
        }

        [Test]
        public void SpiritLoadoutRuntime_MigratesWithoutClearingGrowth()
        {
            var runtime = new SpiritLoadoutRuntime();
            SpiritInstanceState spirit = CreateSpiritState("growing");
            spirit.LearnTemporary(new SpiritEnlightenment(
                new StableConfigId("enlightenment.temporary"),
                EnlightenmentKind.Transform));
            runtime.Add(spirit, CarrierSlot.Weapon);

            SpiritLoadoutChangeResult result = runtime.Migrate(
                spirit.Identity.InstanceId,
                CarrierSlot.TechniqueR,
                false);

            Assert.That(
                result,
                Is.EqualTo(SpiritLoadoutChangeResult.Success));
            Assert.That(
                runtime.TryGetSpirit(
                    spirit.Identity.InstanceId,
                    out SpiritInstanceState stored),
                Is.True);
            Assert.That(
                stored.TemporaryEnlightenments.Count,
                Is.EqualTo(1));
            Assert.That(
                runtime.TryGetCarrier(
                    spirit.Identity.InstanceId,
                    out CarrierSlot carrier),
                Is.True);
            Assert.That(carrier, Is.EqualTo(CarrierSlot.TechniqueR));
        }

        [Test]
        public void SpiritLoadoutRuntime_NoChangeIsSafeButRemovalIsGated()
        {
            var runtime = new SpiritLoadoutRuntime();
            SpiritInstanceState spirit = CreateSpiritState("steady");
            runtime.Add(spirit, CarrierSlot.Weapon);

            Assert.That(
                runtime.Migrate(
                    spirit.Identity.InstanceId,
                    CarrierSlot.Weapon,
                    true),
                Is.EqualTo(SpiritLoadoutChangeResult.NoChange));
            Assert.That(
                runtime.Remove(spirit.Identity.InstanceId, true),
                Is.EqualTo(SpiritLoadoutChangeResult.BlockedInCombat));
            Assert.That(
                runtime.Remove(spirit.Identity.InstanceId, false),
                Is.EqualTo(SpiritLoadoutChangeResult.Success));
            Assert.That(runtime.Count, Is.Zero);
        }

        [Test]
        public void CircuitEntityRef_PreservesStableConfigIds()
        {
            var entityConfigId = new StableConfigId("enemy.wisp");
            var spiritConfigId = new StableConfigId("spirit.fox");
            var entityRef = new CircuitEntityRef(
                17,
                CarrierSlot.TechniqueQ,
                Guid.NewGuid(),
                entityConfigId,
                spiritConfigId);

            Assert.That(
                entityRef.EntityConfigId,
                Is.EqualTo(entityConfigId));
            Assert.That(
                entityRef.SpiritConfigId,
                Is.EqualTo(spiritConfigId));
        }

        [Test]
        public void DeclarativeCircuitRule_MatchesKindsTagsAndElement()
        {
            SpiritInstanceState spirit = CreateSpiritState("matcher");
            var definition = new DeclarativeCircuitRuleDefinition(
                new StableConfigId("rule.matcher"),
                CircuitEventKind.Source,
                CircuitEventKind.Response,
                0b001,
                0b100,
                ElementTag.Fire,
                0b010,
                ElementTag.Fire,
                false,
                false);
            DeclarativeCircuitRule rule = SpiritCircuitRuleBinder.Bind(
                definition,
                spirit,
                CarrierSlot.TechniqueQ,
                1,
                new StableConfigId("carrier.technique.test"));
            var source = new CircuitEntityRef(
                7,
                CarrierSlot.Weapon,
                Guid.Empty);
            var matching = new CircuitEvent(
                30, 0, CircuitEventKind.Source, source, default,
                ElementTag.Fire, 0b011);
            var forbidden = new CircuitEvent(
                30, 0, CircuitEventKind.Source, source, default,
                ElementTag.Fire, 0b101);
            var wrongElement = new CircuitEvent(
                30, 0, CircuitEventKind.Source, source, default,
                ElementTag.Water, 0b001);

            Assert.That(rule.Matches(matching), Is.True);
            Assert.That(rule.Matches(forbidden), Is.False);
            Assert.That(rule.Matches(wrongElement), Is.False);
        }

        [Test]
        public void DeclarativeCircuitRule_CanPreserveTagsAndElement()
        {
            SpiritInstanceState spirit = CreateSpiritState("preserver");
            var definition = new DeclarativeCircuitRuleDefinition(
                new StableConfigId("rule.preserve"),
                CircuitEventKind.Response,
                CircuitEventKind.Transform,
                0,
                0,
                null,
                0b010,
                ElementTag.None,
                true,
                true);
            DeclarativeCircuitRule rule = SpiritCircuitRuleBinder.Bind(
                definition,
                spirit,
                CarrierSlot.Mobility,
                2,
                new StableConfigId("carrier.mobility.test"));
            var input = new CircuitEvent(
                31,
                1,
                CircuitEventKind.Response,
                default,
                default,
                ElementTag.Thunder,
                0b001);

            CircuitEmission emission = rule.Emit(input);

            Assert.That(emission.TagMask, Is.EqualTo(0b011));
            Assert.That(emission.Element, Is.EqualTo(ElementTag.Thunder));
            Assert.That(
                emission.Source.SpiritInstanceId,
                Is.EqualTo(spirit.Identity.InstanceId));
            Assert.That(
                emission.Source.SpiritConfigId,
                Is.EqualTo(spirit.Identity.SpeciesConfigId));
        }

        [Test]
        public void SpiritCircuitRuleBinder_PreservesCarrierIdentity()
        {
            SpiritInstanceState spirit = CreateSpiritState("binder");
            var carrierConfig =
                new StableConfigId("carrier.weapon.test");
            var definition = new DeclarativeCircuitRuleDefinition(
                new StableConfigId("rule.bound"),
                CircuitEventKind.Source,
                CircuitEventKind.Response,
                0,
                0,
                null,
                1,
                ElementTag.None,
                false,
                false);

            DeclarativeCircuitRule rule = SpiritCircuitRuleBinder.Bind(
                definition,
                spirit,
                CarrierSlot.Weapon,
                42,
                carrierConfig);
            CircuitEmission emission = rule.Emit(default);

            Assert.That(rule.OwnerSpiritId, Is.EqualTo(spirit.Identity.InstanceId));
            Assert.That(emission.Source.EntityId, Is.EqualTo(42));
            Assert.That(
                emission.Source.Carrier,
                Is.EqualTo(CarrierSlot.Weapon));
            Assert.That(
                emission.Source.EntityConfigId,
                Is.EqualTo(carrierConfig));
        }

        [Test]
        public void DeclarativeCircuitRules_FormBoundedThreeSpiritLoop()
        {
            SpiritInstanceState first = CreateSpiritState("loop.first");
            SpiritInstanceState second = CreateSpiritState("loop.second");
            SpiritInstanceState third = CreateSpiritState("loop.third");
            var sink = new FakeCircuitEventSink();
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(8, 2, 20, 0f),
                sink);

            runtime.Register(SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.loop.response"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    0b001, 0, null, 0b010, ElementTag.Fire, false, true),
                second,
                CarrierSlot.TechniqueQ,
                2,
                new StableConfigId("carrier.technique.q")));
            runtime.Register(SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.loop.transform"),
                    CircuitEventKind.Response,
                    CircuitEventKind.Transform,
                    0b010, 0, null, 0b100, ElementTag.Fire, false, true),
                third,
                CarrierSlot.Mobility,
                3,
                new StableConfigId("carrier.mobility")));
            runtime.Register(SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.loop.source"),
                    CircuitEventKind.Transform,
                    CircuitEventKind.Source,
                    0b100, 0, null, 0b001, ElementTag.Fire, false, true),
                first,
                CarrierSlot.Weapon,
                1,
                new StableConfigId("carrier.weapon")));
            var rootSource = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                first.Identity.InstanceId,
                new StableConfigId("carrier.weapon"),
                first.Identity.SpeciesConfigId);
            var root = new CircuitEvent(
                32,
                0,
                CircuitEventKind.Source,
                rootSource,
                default,
                ElementTag.Fire,
                0b001);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.ProcessedEvents, Is.EqualTo(7));
            Assert.That(report.EmittedEvents, Is.EqualTo(6));
            Assert.That(report.RepeatBlocks, Is.EqualTo(1));
            Assert.That(
                sink.Events[1].Source.SpiritInstanceId,
                Is.EqualTo(second.Identity.InstanceId));
            Assert.That(
                sink.Events[2].Source.SpiritInstanceId,
                Is.EqualTo(third.Identity.InstanceId));
            Assert.That(
                sink.Events[3].Source.SpiritInstanceId,
                Is.EqualTo(first.Identity.InstanceId));
        }

        [Test]
        public void CircuitRuleDescriptor_PreservesExplainableIdentity()
        {
            SpiritInstanceState spirit = CreateSpiritState("descriptor");
            var carrierConfig =
                new StableConfigId("carrier.descriptor");
            DeclarativeCircuitRule rule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.descriptor"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    1, 2, ElementTag.Fire, 4,
                    ElementTag.Thunder, false, false),
                spirit,
                CarrierSlot.TechniqueE,
                8,
                carrierConfig);

            CircuitRuleDescriptor descriptor = rule.Describe();

            Assert.That(
                descriptor.Rule.SpiritInstanceId,
                Is.EqualTo(spirit.Identity.InstanceId));
            Assert.That(
                descriptor.Carrier,
                Is.EqualTo(CarrierSlot.TechniqueE));
            Assert.That(
                descriptor.CarrierConfigId,
                Is.EqualTo(carrierConfig));
            Assert.That(descriptor.RequiredTagMask, Is.EqualTo(1));
            Assert.That(descriptor.ForbiddenTagMask, Is.EqualTo(2));
        }

        [Test]
        public void CircuitPreviewGraph_DistinguishesGuaranteedAndPossibleEdges()
        {
            SpiritInstanceState producer = CreateSpiritState("producer");
            SpiritInstanceState consumer = CreateSpiritState("consumer");
            var consumerRule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.preview.consumer"),
                    CircuitEventKind.Response,
                    CircuitEventKind.Transform,
                    1, 0, ElementTag.Fire, 2,
                    ElementTag.Fire, false, false),
                consumer,
                CarrierSlot.Mobility,
                2,
                new StableConfigId("carrier.consumer"));
            var guaranteedProducer = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.preview.guaranteed"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    0, 0, null, 1,
                    ElementTag.Fire, false, false),
                producer,
                CarrierSlot.Weapon,
                1,
                new StableConfigId("carrier.producer"));
            var possibleProducer = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.preview.possible"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    0, 0, null, 0,
                    ElementTag.None, true, true),
                producer,
                CarrierSlot.Weapon,
                1,
                new StableConfigId("carrier.producer"));

            CircuitPreviewGraph guaranteed = CircuitPreviewGraphBuilder.Build(
                new[] { guaranteedProducer, consumerRule });
            CircuitPreviewGraph possible = CircuitPreviewGraphBuilder.Build(
                new[] { possibleProducer, consumerRule });

            Assert.That(guaranteed.Edges.Count, Is.EqualTo(1));
            Assert.That(
                guaranteed.Edges[0].Certainty,
                Is.EqualTo(CircuitConnectionCertainty.Guaranteed));
            Assert.That(possible.Edges.Count, Is.EqualTo(1));
            Assert.That(
                possible.Edges[0].Certainty,
                Is.EqualTo(CircuitConnectionCertainty.Possible));
        }

        [Test]
        public void CircuitPreviewGraph_ReportsInputAndOutputBreakpoints()
        {
            SpiritInstanceState first = CreateSpiritState("break.first");
            SpiritInstanceState second = CreateSpiritState("break.second");
            DeclarativeCircuitRule sourceRule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.break.source"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    0, 0, null, 1,
                    ElementTag.None, false, false),
                first,
                CarrierSlot.Weapon,
                1,
                new StableConfigId("carrier.first"));
            DeclarativeCircuitRule disconnected =
                SpiritCircuitRuleBinder.Bind(
                    new DeclarativeCircuitRuleDefinition(
                        new StableConfigId("rule.break.disconnected"),
                        CircuitEventKind.Transform,
                        CircuitEventKind.Source,
                        0, 0, null, 2,
                        ElementTag.None, false, false),
                    second,
                    CarrierSlot.Mobility,
                    2,
                    new StableConfigId("carrier.second"));

            CircuitPreviewGraph graph = CircuitPreviewGraphBuilder.Build(
                new[] { sourceRule, disconnected });

            Assert.That(graph.HasCycle, Is.False);
            Assert.That(graph.InputBreakpoints.Count, Is.EqualTo(1));
            Assert.That(
                graph.InputBreakpoints[0],
                Is.EqualTo(disconnected.Describe().Rule));
            Assert.That(graph.OutputBreakpoints.Count, Is.EqualTo(1));
            Assert.That(
                graph.OutputBreakpoints[0],
                Is.EqualTo(sourceRule.Describe().Rule));
        }

        [Test]
        public void CircuitPreviewGraph_DetectsThreeRuleCycle()
        {
            SpiritInstanceState first = CreateSpiritState("graph.first");
            SpiritInstanceState second = CreateSpiritState("graph.second");
            SpiritInstanceState third = CreateSpiritState("graph.third");
            DeclarativeCircuitRule firstRule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.graph.first"),
                    CircuitEventKind.Transform,
                    CircuitEventKind.Source,
                    4, 0, null, 1,
                    ElementTag.None, false, true),
                first, CarrierSlot.Weapon, 1,
                new StableConfigId("carrier.first"));
            DeclarativeCircuitRule secondRule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.graph.second"),
                    CircuitEventKind.Source,
                    CircuitEventKind.Response,
                    1, 0, null, 2,
                    ElementTag.None, false, true),
                second, CarrierSlot.TechniqueQ, 2,
                new StableConfigId("carrier.second"));
            DeclarativeCircuitRule thirdRule = SpiritCircuitRuleBinder.Bind(
                new DeclarativeCircuitRuleDefinition(
                    new StableConfigId("rule.graph.third"),
                    CircuitEventKind.Response,
                    CircuitEventKind.Transform,
                    2, 0, null, 4,
                    ElementTag.None, false, true),
                third, CarrierSlot.Mobility, 3,
                new StableConfigId("carrier.third"));

            CircuitPreviewGraph graph = CircuitPreviewGraphBuilder.Build(
                new[] { firstRule, secondRule, thirdRule });

            Assert.That(graph.HasCycle, Is.True);
            Assert.That(graph.Edges.Count, Is.EqualTo(3));
            Assert.That(graph.InputBreakpoints.Count, Is.Zero);
            Assert.That(graph.OutputBreakpoints.Count, Is.Zero);
        }

        [Test]
        public void FirstPetCircuit_WaitsForThreeDirectHits()
        {
            FirstSpiritCircuitRuntime circuit = CreateFirstPetCircuit();
            var instigator = new CircuitEntityRef(
                10,
                CarrierSlot.Weapon,
                Guid.Empty,
                new StableConfigId("entity.player"),
                default);
            circuit.BeginFrame();

            FirstSpiritCircuitStep first =
                circuit.RegisterDirectHit(1, 0f, instigator);
            FirstSpiritCircuitStep second =
                circuit.RegisterDirectHit(2, 0f, instigator);
            FirstSpiritCircuitStep third =
                circuit.RegisterDirectHit(3, 0f, instigator);

            Assert.That(first.ChainStarted, Is.False);
            Assert.That(second.ChainStarted, Is.False);
            Assert.That(third.ChainStarted, Is.True);
            Assert.That(third.Report.EmittedEvents, Is.EqualTo(3));
            Assert.That(circuit.Events.Count, Is.EqualTo(4));
            Assert.That(circuit.Heat, Is.Zero);
        }

        [Test]
        public void FirstPetCircuit_EmitsSparkEchoAndBounce()
        {
            FirstSpiritCircuitRuntime circuit = CreateFirstPetCircuit();
            var instigator = new CircuitEntityRef(
                10,
                CarrierSlot.Weapon,
                Guid.Empty);
            circuit.BeginFrame();
            circuit.RegisterDirectHit(1, 0f, instigator);
            circuit.RegisterDirectHit(2, 0f, instigator);
            circuit.RegisterDirectHit(3, 0f, instigator);

            Assert.That(
                circuit.Events[1].TagMask,
                Is.EqualTo(FirstSpiritCircuitContent.SparkTag));
            Assert.That(
                circuit.Events[2].TagMask,
                Is.EqualTo(FirstSpiritCircuitContent.EchoAttackTag));
            Assert.That(
                circuit.Events[3].TagMask,
                Is.EqualTo(
                    FirstSpiritCircuitContent.BounceAttackTag |
                    FirstSpiritCircuitContent.HeatRefundTag));
            Assert.That(
                circuit.Events[3].Source.SpiritConfigId,
                Is.EqualTo(
                    FirstSpiritCircuitContent.BounceGelSpecies));
        }

        [Test]
        public void FirstPetCircuit_PreviewExplainsLinearChain()
        {
            CircuitPreviewGraph preview =
                CreateFirstPetCircuit().Preview;

            Assert.That(preview.Rules.Count, Is.EqualTo(3));
            Assert.That(preview.Edges.Count, Is.EqualTo(2));
            Assert.That(preview.InputBreakpoints.Count, Is.Zero);
            Assert.That(preview.OutputBreakpoints.Count, Is.EqualTo(1));
            Assert.That(preview.HasCycle, Is.False);
        }

        [Test]
        public void FirstPetCircuit_RequiresPlayerInputForNextCycle()
        {
            FirstSpiritCircuitRuntime circuit = CreateFirstPetCircuit();
            var instigator = new CircuitEntityRef(
                10,
                CarrierSlot.Weapon,
                Guid.Empty);
            circuit.BeginFrame();
            circuit.RegisterDirectHit(1, 0f, instigator);
            circuit.RegisterDirectHit(2, 0f, instigator);
            FirstSpiritCircuitStep firstCycle =
                circuit.RegisterDirectHit(3, 0f, instigator);
            Assert.That(
                circuit.RegisterBounceResolved(circuit.Events[3]),
                Is.True);
            FirstSpiritCircuitStep oneMore =
                circuit.RegisterDirectHit(4, 1f, instigator);
            FirstSpiritCircuitStep secondCycle =
                circuit.RegisterDirectHit(5, 1f, instigator);

            Assert.That(firstCycle.ChainStarted, Is.True);
            Assert.That(oneMore.ChainStarted, Is.False);
            Assert.That(secondCycle.ChainStarted, Is.True);
            Assert.That(circuit.Events.Count, Is.EqualTo(8));
            Assert.That(circuit.Heat, Is.Zero);
        }

        [Test]
        public void FirstPetCircuit_RefundsOnlyOneResolvedBouncePerChain()
        {
            FirstSpiritCircuitRuntime circuit = CreateFirstPetCircuit();
            var instigator = new CircuitEntityRef(
                10,
                CarrierSlot.Weapon,
                Guid.Empty);
            circuit.BeginFrame();
            circuit.RegisterDirectHit(1, 0f, instigator);
            circuit.RegisterDirectHit(2, 0f, instigator);
            circuit.RegisterDirectHit(3, 0f, instigator);
            CircuitEvent echo = circuit.Events[2];
            CircuitEvent bounce = circuit.Events[3];

            Assert.That(circuit.Heat, Is.Zero);
            Assert.That(circuit.RegisterBounceResolved(echo), Is.False);
            Assert.That(circuit.RegisterBounceResolved(bounce), Is.True);
            Assert.That(circuit.RegisterBounceResolved(bounce), Is.False);
            Assert.That(circuit.Heat, Is.EqualTo(1));
        }

        [Test]
        public void FirstPetCircuit_FinalEventSupportsShadowAttribution()
        {
            FirstSpiritCircuitRuntime circuit = CreateFirstPetCircuit();
            var instigator = new CircuitEntityRef(
                10,
                CarrierSlot.Weapon,
                Guid.Empty);
            circuit.BeginFrame();
            circuit.RegisterDirectHit(1, 0f, instigator);
            circuit.RegisterDirectHit(2, 0f, instigator);
            circuit.RegisterDirectHit(3, 0f, instigator);
            CircuitEvent origin = circuit.Events[3];
            var attribution = new CircuitAttributionRecorder();
            var shadow = new CircuitShadowRecorder();
            var bridge = new CircuitCombatResultBridge(
                attribution,
                shadow);
            var metric = new StableConfigId(
                "combat.player.damage.first-pet");

            bridge.Record(StructuredCombatResult.FromLegacy(
                metric,
                CombatOutcomeKind.Damage,
                8f,
                8f,
                default));
            bridge.Record(StructuredCombatResult.FromCircuit(
                metric,
                CombatOutcomeKind.Damage,
                8f,
                8f,
                default,
                origin));

            Assert.That(
                shadow.Compare(0f, 0f)[0].IsWithinTolerance,
                Is.True);
            Assert.That(attribution.Outcomes.Count, Is.EqualTo(1));
            Assert.That(
                attribution.Outcomes[0]
                    .Attribution.SpiritConfigId,
                Is.EqualTo(
                    FirstSpiritCircuitContent.BounceGelSpecies));
        }

        [Test]
        public void FirstPetLoadoutProvider_RestoresActiveRosterAndCarriers()
        {
            SaveDataV1 save = CreateFirstPetSaveData();

            bool restored = FirstSpiritCircuitLoadoutProvider.TryRestore(
                save,
                out FirstSpiritCircuitLoadoutBinding binding);

            Assert.That(restored, Is.True);
            Assert.That(binding.Loadout.Count, Is.EqualTo(3));
            Assert.That(
                binding.Loadout.TryGetCarrier(
                    binding.SparkRaccoon.Identity.InstanceId,
                    out CarrierSlot sparkCarrier),
                Is.True);
            Assert.That(sparkCarrier, Is.EqualTo(CarrierSlot.Weapon));
            Assert.That(
                binding.Loadout.TryGetCarrier(
                    binding.EchoOwl.Identity.InstanceId,
                    out CarrierSlot echoCarrier),
                Is.True);
            Assert.That(echoCarrier, Is.EqualTo(CarrierSlot.TechniqueQ));
            Assert.That(
                binding.Loadout.TryGetCarrier(
                    binding.BounceGel.Identity.InstanceId,
                    out CarrierSlot bounceCarrier),
                Is.True);
            Assert.That(bounceCarrier, Is.EqualTo(CarrierSlot.Mobility));
        }

        [Test]
        public void FirstPetLoadoutProvider_RejectsIncompleteActiveRoster()
        {
            SaveDataV1 save = CreateFirstPetSaveData();
            save.activeSpiritInstanceGuids.RemoveAt(2);

            bool restored = FirstSpiritCircuitLoadoutProvider.TryRestore(
                save,
                out FirstSpiritCircuitLoadoutBinding binding);

            Assert.That(restored, Is.False);
            Assert.That(binding, Is.Null);
        }

        [Test]
        public void SpiritCircuitHUD_MapsFiveCarrierSlotsInVisualOrder()
        {
            Assert.That(
                SpiritCircuitHUD.VisualIndexForCarrier(
                    CarrierSlot.Weapon),
                Is.EqualTo(4));
            Assert.That(
                SpiritCircuitHUD.VisualIndexForCarrier(
                    CarrierSlot.TechniqueQ),
                Is.EqualTo(0));
            Assert.That(
                SpiritCircuitHUD.VisualIndexForCarrier(
                    CarrierSlot.TechniqueE),
                Is.EqualTo(1));
            Assert.That(
                SpiritCircuitHUD.VisualIndexForCarrier(
                    CarrierSlot.TechniqueR),
                Is.EqualTo(2));
            Assert.That(
                SpiritCircuitHUD.VisualIndexForCarrier(
                    CarrierSlot.Mobility),
                Is.EqualTo(3));
        }

        [Test]
        public void SpiritCircuitHUD_MapsOnlyStarterAttachmentTargets()
        {
            Assert.That(
                SpiritCircuitHUD.TryCarrierForVisualIndex(
                    4,
                    out CarrierSlot weapon),
                Is.True);
            Assert.That(weapon, Is.EqualTo(CarrierSlot.Weapon));
            Assert.That(
                SpiritCircuitHUD.TryCarrierForVisualIndex(
                    0,
                    out CarrierSlot technique),
                Is.True);
            Assert.That(
                technique,
                Is.EqualTo(CarrierSlot.TechniqueQ));
            Assert.That(
                SpiritCircuitHUD.TryCarrierForVisualIndex(
                    3,
                    out CarrierSlot mobility),
                Is.True);
            Assert.That(
                mobility,
                Is.EqualTo(CarrierSlot.Mobility));
            Assert.That(
                SpiritCircuitHUD.TryCarrierForVisualIndex(
                    1,
                    out _),
                Is.False);
            Assert.That(
                SpiritCircuitHUD.TryCarrierForVisualIndex(
                    2,
                    out _),
                Is.False);
        }

        [Test]
        public void StarterSpiritCarrier_ChangesTriggerAndPreservesProgress()
        {
            var runtime = new StarterSpiritCarrierRuntime(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);

            StarterSpiritCarrierStep first =
                runtime.RegisterWeaponHit();
            Assert.That(first.Activated, Is.False);
            Assert.That(runtime.Progress, Is.EqualTo(1));
            Assert.That(
                runtime.TryAttach(
                    CarrierSlot.TechniqueQ,
                    false),
                Is.EqualTo(StarterSpiritAttachmentResult.Success));
            Assert.That(
                runtime.RegisterTechniqueHit(
                    CarrierSlot.TechniqueQ).Activated,
                Is.True);
            Assert.That(runtime.Progress, Is.EqualTo(1));
            Assert.That(
                runtime.TryAttach(
                    CarrierSlot.Mobility,
                    false),
                Is.EqualTo(StarterSpiritAttachmentResult.Success));
            Assert.That(
                runtime.RegisterMobilityFinished().Activated,
                Is.True);
            Assert.That(runtime.Progress, Is.EqualTo(1));
            Assert.That(
                runtime.TryAttach(CarrierSlot.Weapon, false),
                Is.EqualTo(StarterSpiritAttachmentResult.Success));

            Assert.That(runtime.RegisterWeaponHit().Activated, Is.False);
            Assert.That(runtime.RegisterWeaponHit().Activated, Is.True);
            Assert.That(runtime.Progress, Is.Zero);
        }

        [Test]
        public void StarterSpiritCarrierController_AppliesAndResetsRunTalent()
        {
            FeatureFlags.EnableCircuitRuntime = true;
            SpiritInstanceState spark = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            var save = new SaveDataV1();
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(spark));
            save.activeSpiritInstanceGuids.Add(
                spark.Identity.InstanceId.ToString("N"));
            save.starterTechniqueUnlocked = true;
            var player = new GameObject("SingleSpiritTalentPlayer");
            try
            {
                PlayerCombat combat =
                    player.AddComponent<PlayerCombat>();
                SpiritTalentRunController talents =
                    player.AddComponent<SpiritTalentRunController>();
                Assert.That(talents.TryConfigure(save), Is.True);
                StarterSpiritCarrierController carrier =
                    player.AddComponent<StarterSpiritCarrierController>();
                Assert.That(carrier.TryConfigure(save), Is.True);
                Assert.That(
                    combat.GetSkillInSlot(0),
                    Is.EqualTo(
                        Resources.Load<SkillData>(
                            StarterSpiritCarrierController
                                .StarterTechniqueResourcePath)));

                talents.GrantSharedExperience(2, "test");
                Assert.That(
                    talents.TryActivate(
                        spark.Identity.InstanceId,
                        new StableConfigId(
                            "talent.spark.quick-temper")),
                    Is.EqualTo(SpiritTalentActivationResult.Success));
                carrier.RefreshTalentTuning();
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.True);
                Assert.That(
                    carrier.TryAttach(
                        CarrierSlot.TechniqueQ,
                        false),
                    Is.EqualTo(
                        StarterSpiritAttachmentResult.Success));
                Assert.That(
                    carrier.TryAttach(
                        CarrierSlot.Weapon,
                        false),
                    Is.EqualTo(
                        StarterSpiritAttachmentResult.Success));
                Assert.That(
                    talents.TryGetState(
                        spark.Identity.InstanceId,
                        out SpiritRunTalentState state),
                    Is.True);
                Assert.That(
                    state.IsActive("talent.spark.quick-temper"),
                    Is.True);
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.True);

                Assert.That(
                    talents.ResetTalents(spark.Identity.InstanceId),
                    Is.EqualTo(1));
                carrier.RefreshTalentTuning();
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    carrier.Runtime.RegisterWeaponHit().Activated,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void StarterSpiritCarrier_BlocksCombatAndLockedTechnique()
        {
            var runtime = new StarterSpiritCarrierRuntime(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);

            Assert.That(
                runtime.TryAttach(
                    CarrierSlot.TechniqueQ,
                    true),
                Is.EqualTo(StarterSpiritAttachmentResult.InCombat));
            Assert.That(
                runtime.TryAttach(
                    CarrierSlot.TechniqueE,
                    false),
                Is.EqualTo(
                    StarterSpiritAttachmentResult.UnsupportedCarrier));
            Assert.That(runtime.Attachment, Is.EqualTo(CarrierSlot.Weapon));
        }

        [Test]
        public void StarterSpiritCarrier_AllThreeSpeciesUseThreeCarriers()
        {
            StableConfigId[] species =
            {
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                FirstSpiritCircuitContent.BounceGelSpecies
            };

            foreach (StableConfigId speciesId in species)
            {
                var runtime =
                    new StarterSpiritCarrierRuntime(speciesId);
                Assert.That(
                    runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    runtime.RegisterWeaponHit().Activated,
                    Is.False);
                Assert.That(
                    runtime.RegisterWeaponHit().Activated,
                    Is.True);
                Assert.That(
                    runtime.TryAttach(
                        CarrierSlot.TechniqueQ,
                        false),
                    Is.EqualTo(
                        StarterSpiritAttachmentResult.Success));
                Assert.That(
                    runtime.RegisterTechniqueHit(
                        CarrierSlot.TechniqueQ).Activated,
                    Is.True);
                Assert.That(
                    runtime.TryAttach(
                        CarrierSlot.Mobility,
                        false),
                    Is.EqualTo(
                        StarterSpiritAttachmentResult.Success));
                Assert.That(
                    runtime.RegisterMobilityFinished().Activated,
                    Is.True);
            }
        }

        [Test]
        public void Projectile_PreservesAndResetsSkillSource()
        {
            var go = new GameObject("ProjectileSkillSourceTest");
            SkillData skill =
                ScriptableObject.CreateInstance<SkillData>();
            try
            {
                Projectile projectile = go.AddComponent<Projectile>();
                projectile.Initialize(
                    1f,
                    Vector3.forward,
                    1f,
                    0,
                    0f);
                projectile.SetSkillSource(0, skill);
                Assert.That(projectile.SourceSkillSlot, Is.Zero);
                Assert.That(projectile.SourceSkill, Is.SameAs(skill));

                projectile.Initialize(
                    1f,
                    Vector3.forward,
                    1f,
                    0,
                    0f);
                Assert.That(projectile.SourceSkillSlot, Is.EqualTo(-1));
                Assert.That(projectile.SourceSkill, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void Projectile_IgnoresNonDamageableLogicTriggers()
        {
            var projectileObject =
                new GameObject("ProjectileTriggerTest");
            var triggerObject =
                new GameObject("TutorialLogicTrigger");
            try
            {
                Projectile projectile =
                    projectileObject.AddComponent<Projectile>();
                projectile.Initialize(
                    1f,
                    Vector3.forward,
                    1f,
                    0,
                    0f);
                BoxCollider trigger =
                    triggerObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;

                typeof(Projectile)
                    .GetMethod(
                        "OnTriggerEnter",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    ?.Invoke(projectile, new object[] { trigger });

                bool initialized = (bool)typeof(Projectile)
                    .GetField(
                        "_initialized",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic)
                    ?.GetValue(projectile);
                Assert.That(initialized, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectileObject);
                UnityEngine.Object.DestroyImmediate(triggerObject);
            }
        }

        [Test]
        public void StarterSpiritCarrierProvider_RestoresOnlySingleSpark()
        {
            SpiritInstanceState spark = CreateFirstPetState(
                FirstSpiritCircuitContent.SparkRaccoonSpecies);
            var save = new SaveDataV1();
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(spark));
            save.activeSpiritInstanceGuids.Add(
                spark.Identity.InstanceId.ToString("N"));

            Assert.That(
                StarterSpiritCarrierLoadoutProvider.TryRestoreSingle(
                    save,
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    out SpiritInstanceState restored),
                Is.True);
            Assert.That(
                restored.Identity.InstanceId,
                Is.EqualTo(spark.Identity.InstanceId));

            SpiritInstanceState echo = CreateFirstPetState(
                FirstSpiritCircuitContent.EchoOwlSpecies);
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(echo));
            save.activeSpiritInstanceGuids.Add(
                echo.Identity.InstanceId.ToString("N"));
            Assert.That(
                StarterSpiritCarrierLoadoutProvider.TryRestoreSingle(
                    save,
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    out _),
                Is.False);
        }

        [Test]
        public void FirstPetCircuitController_AcceptsOnlyResolvedCarrierHits()
        {
            FeatureFlags.EnableCircuitRuntime = true;
            var player = new GameObject("CircuitPlayer");
            var target = new GameObject("CircuitTarget");
            try
            {
                FirstSpiritCircuitController controller =
                    player.AddComponent<FirstSpiritCircuitController>();
                Assert.That(
                    controller.TryConfigure(CreateFirstPetSaveData()),
                    Is.True);
                CircuitEntityRef targetRef =
                    LegacyCombatResultRecorder.BuildTarget(target);

                for (int i = 0; i < 3; i++)
                {
                    controller.RecordResolvedPlayerDamage(
                        new GameEvents.PlayerDamageResolved
                    {
                        Attacker = player,
                        Target = target,
                        RequestedAmount = 10f,
                        AppliedAmount = 8f,
                        TargetRef = targetRef,
                        IsPlayerOwnedDamage = true
                    });
                    controller.RecordCarrierHit(target);
                }

                controller.RecordCarrierHit(target);

                Assert.That(controller.AcceptedHitCount, Is.EqualTo(3));
                Assert.That(controller.LastStep.ChainStarted, Is.True);
                Assert.That(
                    controller.LastStep.Report.EmittedEvents,
                    Is.EqualTo(3));
                Assert.That(controller.Runtime.Heat, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void FirstPetCircuitController_StagesOneOneOneByPlayerAction()
        {
            FeatureFlags.EnableCircuitRuntime = true;
            var player = new GameObject("StagedCircuitPlayer");
            var target = new GameObject("StagedCircuitTarget");
            GameEvents.FirstPetCircuitAdvanced latest = default;
            void Capture(GameEvents.FirstPetCircuitAdvanced evt)
                => latest = evt;
            GameEvents.Subscribe<GameEvents.FirstPetCircuitAdvanced>(
                Capture);
            try
            {
                FirstSpiritCircuitController controller =
                    player.AddComponent<FirstSpiritCircuitController>();
                Assert.That(
                    controller.TryConfigure(CreateFirstPetSaveData()),
                    Is.True);
                CircuitEntityRef targetRef =
                    LegacyCombatResultRecorder.BuildTarget(target);

                void Resolve(CarrierSlot action)
                {
                    controller.RecordResolvedPlayerDamage(
                        new GameEvents.PlayerDamageResolved
                        {
                            Attacker = player,
                            Target = target,
                            RequestedAmount = 10f,
                            AppliedAmount = 8f,
                            TargetRef = targetRef,
                            IsPlayerOwnedDamage = true
                        });
                    controller.RecordCarrierAction(target, action);
                }

                Resolve(CarrierSlot.Weapon);
                Resolve(CarrierSlot.Weapon);
                Resolve(CarrierSlot.Weapon);
                Assert.That(
                    controller.LastStep.ChainStarted,
                    Is.False);
                Assert.That(
                    latest.PendingStage,
                    Is.EqualTo(
                        SpiritCircuitPendingStage.AwaitingEcho));
                Assert.That(
                    latest.NextCarrier,
                    Is.EqualTo(CarrierSlot.TechniqueQ));
                Assert.That(latest.RemainingSeconds, Is.GreaterThan(7f));

                Resolve(CarrierSlot.TechniqueQ);
                Assert.That(
                    controller.LastStep.ChainStarted,
                    Is.False);
                Assert.That(
                    latest.PendingStage,
                    Is.EqualTo(
                        SpiritCircuitPendingStage.AwaitingGel));
                Assert.That(
                    latest.NextCarrier,
                    Is.EqualTo(CarrierSlot.Mobility));
                Assert.That(latest.RemainingSeconds, Is.GreaterThan(5f));

                controller.RecordCarrierAction(
                    null,
                    CarrierSlot.Mobility);
                Assert.That(
                    controller.LastStep.ChainStarted,
                    Is.True);
                Assert.That(
                    controller.LastStep.Report.EmittedEvents,
                    Is.EqualTo(3));
            }
            finally
            {
                GameEvents.Unsubscribe<
                    GameEvents.FirstPetCircuitAdvanced>(Capture);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void FirstPetCircuitController_MigrationRebuildsPreview()
        {
            FeatureFlags.EnableCircuitRuntime = true;
            var player = new GameObject("MigratingCircuitPlayer");
            try
            {
                FirstSpiritCircuitController controller =
                    player.AddComponent<FirstSpiritCircuitController>();
                Assert.That(
                    controller.TryConfigure(CreateFirstPetSaveData()),
                    Is.True);
                SpiritInstanceState spark = null;
                foreach (SpiritInstanceState spirit in
                         controller.Loadout.ActiveSpirits)
                {
                    if (spirit.Identity.SpeciesConfigId ==
                        FirstSpiritCircuitContent.SparkRaccoonSpecies)
                    {
                        spark = spirit;
                    }
                }
                Assert.That(spark, Is.Not.Null);

                Assert.That(
                    controller.TryMigrateAttachment(
                        spark.Identity.InstanceId,
                        CarrierSlot.TechniqueQ,
                        false),
                    Is.EqualTo(SpiritLoadoutChangeResult.Success));
                Assert.That(
                    controller.Loadout.Pattern,
                    Is.EqualTo(SpiritAttachmentPattern.TwoOne));
                CircuitRuleDescriptor sparkRule =
                    controller.Runtime.Preview.Rules
                        .First(rule =>
                            rule.SpiritConfigId ==
                            FirstSpiritCircuitContent
                                .SparkRaccoonSpecies);
                Assert.That(
                    sparkRule.Carrier,
                    Is.EqualTo(CarrierSlot.TechniqueQ));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void FirstPetCircuitController_StaysDormantWhenFlagIsOff()
        {
            FeatureFlags.EnableCircuitRuntime = false;
            var player = new GameObject("CircuitPlayerDisabled");
            var target = new GameObject("CircuitTargetDisabled");
            try
            {
                FirstSpiritCircuitController controller =
                    player.AddComponent<FirstSpiritCircuitController>();
                controller.RecordResolvedPlayerDamage(
                    new GameEvents.PlayerDamageResolved
                {
                    Attacker = player,
                    Target = target,
                    RequestedAmount = 10f,
                    AppliedAmount = 10f,
                    TargetRef =
                        LegacyCombatResultRecorder.BuildTarget(target),
                    IsPlayerOwnedDamage = true
                });
                controller.RecordCarrierHit(target);

                Assert.That(controller.Runtime, Is.Null);
                Assert.That(controller.AcceptedHitCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CircuitRuntime_ChainsSourceResponseAndTransform()
        {
            var sink = new FakeCircuitEventSink();
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(4, 2, 10, 0f),
                sink);
            var responseSource = new CircuitEntityRef(
                2,
                CarrierSlot.TechniqueQ,
                Guid.NewGuid());
            var transformSource = new CircuitEntityRef(
                3,
                CarrierSlot.Mobility,
                Guid.NewGuid());
            runtime.Register(new FakeCircuitRule
            {
                RuleId = new StableConfigId("rule.response"),
                OwnerSpiritId = responseSource.SpiritInstanceId,
                Match = input => input.Kind == CircuitEventKind.Source,
                EmitEvent = _ => new CircuitEmission(
                    CircuitEventKind.Response,
                    responseSource,
                    ElementTag.Fire,
                    0b001)
            });
            runtime.Register(new FakeCircuitRule
            {
                RuleId = new StableConfigId("rule.transform"),
                OwnerSpiritId = transformSource.SpiritInstanceId,
                Match = input => input.Kind == CircuitEventKind.Response,
                EmitEvent = _ => new CircuitEmission(
                    CircuitEventKind.Transform,
                    transformSource,
                    ElementTag.Thunder,
                    0b011)
            });
            var rootSource = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            var root = new CircuitEvent(
                99,
                0,
                CircuitEventKind.Source,
                rootSource,
                default,
                ElementTag.None,
                0b001);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.ProcessedEvents, Is.EqualTo(3));
            Assert.That(report.EmittedEvents, Is.EqualTo(2));
            Assert.That(sink.Events[0].Kind, Is.EqualTo(CircuitEventKind.Source));
            Assert.That(sink.Events[1].Kind, Is.EqualTo(CircuitEventKind.Response));
            Assert.That(sink.Events[2].Kind, Is.EqualTo(CircuitEventKind.Transform));
            Assert.That(sink.Events[2].Depth, Is.EqualTo(2));
            Assert.That(
                sink.Events[2].Instigator.EntityId,
                Is.EqualTo(responseSource.EntityId));
        }

        [Test]
        public void CircuitRuntime_BlocksBeyondMaximumDepth()
        {
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(2, 10, 10, 0f));
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            runtime.Register(new FakeCircuitRule
            {
                RuleId = new StableConfigId("rule.loop.depth"),
                OwnerSpiritId = source.SpiritInstanceId,
                Match = input => input.Kind == CircuitEventKind.Response,
                EmitEvent = _ => new CircuitEmission(
                    CircuitEventKind.Response,
                    source,
                    ElementTag.Fire,
                    1)
            });
            var root = new CircuitEvent(
                1,
                0,
                CircuitEventKind.Response,
                source,
                default,
                ElementTag.Fire,
                1);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.ProcessedEvents, Is.EqualTo(3));
            Assert.That(report.EmittedEvents, Is.EqualTo(2));
            Assert.That(report.DepthBlocks, Is.EqualTo(1));
        }

        [Test]
        public void CircuitRuntime_BlocksRepeatedRuleWithinChain()
        {
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(8, 2, 10, 0f));
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            runtime.Register(new FakeCircuitRule
            {
                RuleId = new StableConfigId("rule.loop.repeat"),
                OwnerSpiritId = source.SpiritInstanceId,
                Match = input => input.Kind == CircuitEventKind.Response,
                EmitEvent = _ => new CircuitEmission(
                    CircuitEventKind.Response,
                    source,
                    ElementTag.Fire,
                    1)
            });
            var root = new CircuitEvent(
                2,
                0,
                CircuitEventKind.Response,
                source,
                default,
                ElementTag.Fire,
                1);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.ProcessedEvents, Is.EqualTo(3));
            Assert.That(report.EmittedEvents, Is.EqualTo(2));
            Assert.That(report.RepeatBlocks, Is.EqualTo(1));
        }

        [Test]
        public void CircuitRuntime_BlocksRepeatedEventKindAcrossRules()
        {
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(8, 10, 20, 0f, 2));
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            for (int i = 0; i < 3; i++)
            {
                int ruleIndex = i;
                runtime.Register(new FakeCircuitRule
                {
                    RuleId = new StableConfigId($"rule.kind.{ruleIndex}"),
                    OwnerSpiritId = Guid.NewGuid(),
                    Match = input => input.Kind == CircuitEventKind.Source,
                    EmitEvent = _ => new CircuitEmission(
                        CircuitEventKind.Response,
                        source,
                        ElementTag.Fire,
                        ruleIndex)
                });
            }
            var root = new CircuitEvent(
                5,
                0,
                CircuitEventKind.Source,
                source,
                default,
                ElementTag.Fire,
                0);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.EmittedEvents, Is.EqualTo(2));
            Assert.That(report.KindBlocks, Is.EqualTo(1));
        }

        [Test]
        public void CircuitRuntime_EnforcesRuleFrequencyAcrossFrames()
        {
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(4, 2, 10, 1f));
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            runtime.Register(new FakeCircuitRule
            {
                RuleId = new StableConfigId("rule.frequency"),
                OwnerSpiritId = source.SpiritInstanceId,
                Match = input => input.Kind == CircuitEventKind.Source,
                EmitEvent = _ => new CircuitEmission(
                    CircuitEventKind.Response,
                    source,
                    ElementTag.Water,
                    1)
            });
            var root = new CircuitEvent(
                3,
                0,
                CircuitEventKind.Source,
                source,
                default,
                ElementTag.Water,
                1);

            runtime.BeginFrame();
            CircuitExecutionReport first = runtime.Execute(root, 0f);
            runtime.BeginFrame();
            CircuitExecutionReport blocked = runtime.Execute(root, 0.5f);
            runtime.BeginFrame();
            CircuitExecutionReport recovered = runtime.Execute(root, 1f);

            Assert.That(first.EmittedEvents, Is.EqualTo(1));
            Assert.That(blocked.EmittedEvents, Is.Zero);
            Assert.That(blocked.FrequencyBlocks, Is.EqualTo(1));
            Assert.That(recovered.EmittedEvents, Is.EqualTo(1));
        }

        [Test]
        public void CircuitRuntime_StopsAtSharedFrameBudget()
        {
            var sink = new FakeCircuitEventSink();
            var runtime = new CircuitRuntime(
                new CircuitExecutionLimits(4, 2, 2, 0f),
                sink);
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                Guid.NewGuid());
            for (int i = 0; i < 2; i++)
            {
                int ruleIndex = i;
                runtime.Register(new FakeCircuitRule
                {
                    RuleId = new StableConfigId($"rule.budget.{ruleIndex}"),
                    OwnerSpiritId = Guid.NewGuid(),
                    Match = input => input.Kind == CircuitEventKind.Source,
                    EmitEvent = _ => new CircuitEmission(
                        CircuitEventKind.Response,
                        source,
                        ElementTag.None,
                        ruleIndex)
                });
            }
            var root = new CircuitEvent(
                4,
                0,
                CircuitEventKind.Source,
                source,
                default,
                ElementTag.None,
                0);

            runtime.BeginFrame();
            CircuitExecutionReport report = runtime.Execute(root, 0f);

            Assert.That(report.BudgetExhausted, Is.True);
            Assert.That(report.EmittedEvents, Is.EqualTo(1));
            Assert.That(sink.Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void CircuitAttributionRecorder_PreservesBranchSources()
        {
            var recorder = new CircuitAttributionRecorder();
            Guid firstSpirit = Guid.NewGuid();
            Guid secondSpirit = Guid.NewGuid();
            var firstSource = new CircuitEntityRef(
                1,
                CarrierSlot.Weapon,
                firstSpirit,
                new StableConfigId("enemy.first"),
                new StableConfigId("spirit.first"));
            var secondSource = new CircuitEntityRef(
                2,
                CarrierSlot.TechniqueQ,
                secondSpirit,
                new StableConfigId("enemy.second"),
                new StableConfigId("spirit.second"));
            var firstEvent = new CircuitEvent(
                20,
                1,
                CircuitEventKind.Response,
                firstSource,
                default,
                ElementTag.Fire,
                1);
            var secondEvent = new CircuitEvent(
                20,
                1,
                CircuitEventKind.Transform,
                secondSource,
                default,
                ElementTag.Water,
                2);

            recorder.Record(firstEvent);
            recorder.Record(secondEvent);
            recorder.RecordOutcome(
                firstEvent,
                CombatOutcomeKind.Damage,
                10f);
            recorder.RecordOutcome(
                secondEvent,
                CombatOutcomeKind.Damage,
                25f);

            Assert.That(recorder.GetTrace(20).Count, Is.EqualTo(2));
            Assert.That(recorder.Outcomes.Count, Is.EqualTo(2));
            Assert.That(
                recorder.Outcomes[0].Attribution.SpiritInstanceId,
                Is.EqualTo(firstSpirit));
            Assert.That(
                recorder.Outcomes[1].Attribution.SpiritInstanceId,
                Is.EqualTo(secondSpirit));
        }

        [Test]
        public void CircuitAttributionRecorder_AggregatesEquivalentSources()
        {
            var recorder = new CircuitAttributionRecorder();
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.Mobility,
                Guid.NewGuid(),
                new StableConfigId("player"),
                new StableConfigId("spirit.guard"));
            var origin = new CircuitEvent(
                21,
                2,
                CircuitEventKind.Transform,
                source,
                default,
                ElementTag.Earth,
                4);
            var key = new CircuitAttributionKey(
                CombatOutcomeKind.Defense,
                source,
                ElementTag.Earth);

            recorder.RecordOutcome(
                origin,
                CombatOutcomeKind.Defense,
                12f);
            recorder.RecordOutcome(
                origin,
                CombatOutcomeKind.Defense,
                8f);

            Assert.That(recorder.GetTotal(key), Is.EqualTo(20f));
            Assert.That(
                recorder.RecordOutcome(
                    origin,
                    CombatOutcomeKind.Defense,
                    float.NaN),
                Is.False);
        }

        [Test]
        public void CircuitShadowRecorder_AggregatesAndComparesTolerance()
        {
            var recorder = new CircuitShadowRecorder();
            var damage = new StableConfigId("metric.damage");
            recorder.RecordLegacy(damage, 60f);
            recorder.RecordLegacy(damage, 40f);
            recorder.RecordCircuit(damage, 102f);

            ShadowMetricComparison comparison =
                recorder.Compare(0.5f, 0.02f)[0];

            Assert.That(comparison.LegacyValue, Is.EqualTo(100f));
            Assert.That(comparison.CircuitValue, Is.EqualTo(102f));
            Assert.That(comparison.Delta, Is.EqualTo(2f));
            Assert.That(comparison.IsWithinTolerance, Is.True);
        }

        [Test]
        public void CircuitShadowRecorder_ReportsMissingSideAndResets()
        {
            var recorder = new CircuitShadowRecorder();
            var healing = new StableConfigId("metric.healing");
            recorder.RecordLegacy(healing, 5f);

            ShadowMetricComparison comparison =
                recorder.Compare(0.1f, 0f)[0];

            Assert.That(comparison.CircuitValue, Is.Zero);
            Assert.That(comparison.IsWithinTolerance, Is.False);

            recorder.Reset();
            Assert.That(recorder.Compare(0f, 0f).Count, Is.Zero);
        }

        [Test]
        public void CircuitCombatResultBridge_AttributesCircuitResult()
        {
            var attribution = new CircuitAttributionRecorder();
            var shadow = new CircuitShadowRecorder();
            var sink = new FakeStructuredCombatResultSink();
            var bridge = new CircuitCombatResultBridge(
                attribution,
                shadow,
                sink);
            var source = new CircuitEntityRef(
                1,
                CarrierSlot.TechniqueQ,
                Guid.NewGuid(),
                new StableConfigId("carrier.technique.q"),
                new StableConfigId("spirit.source"));
            var target = new CircuitEntityRef(
                99,
                null,
                Guid.Empty,
                new StableConfigId("enemy.target"),
                default);
            var origin = new CircuitEvent(
                40,
                2,
                CircuitEventKind.Transform,
                source,
                default,
                ElementTag.Fire,
                1);
            var metric = new StableConfigId("metric.damage");
            StructuredCombatResult result =
                StructuredCombatResult.FromCircuit(
                    metric,
                    CombatOutcomeKind.Damage,
                    15f,
                    10f,
                    target,
                    origin);

            bridge.Record(result);

            var key = new CircuitAttributionKey(
                CombatOutcomeKind.Damage,
                source,
                ElementTag.Fire);
            Assert.That(attribution.GetTotal(key), Is.EqualTo(10f));
            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(sink.LastResult.Target.EntityId, Is.EqualTo(99));
            Assert.That(sink.LastResult.RequestedAmount, Is.EqualTo(15f));
            Assert.That(shadow.Compare(0f, 0f)[0].CircuitValue, Is.EqualTo(10f));
        }

        [Test]
        public void CircuitCombatResultBridge_RecordsLegacyWithoutAttribution()
        {
            var attribution = new CircuitAttributionRecorder();
            var shadow = new CircuitShadowRecorder();
            var bridge = new CircuitCombatResultBridge(attribution, shadow);
            var metric = new StableConfigId("metric.healing");
            StructuredCombatResult result =
                StructuredCombatResult.FromLegacy(
                    metric,
                    CombatOutcomeKind.Healing,
                    12f,
                    8f,
                    default);

            bridge.Record(result);

            Assert.That(attribution.Outcomes.Count, Is.Zero);
            ShadowMetricComparison comparison =
                shadow.Compare(0f, 0f)[0];
            Assert.That(comparison.LegacyValue, Is.EqualTo(8f));
            Assert.That(comparison.CircuitValue, Is.Zero);
        }

        [Test]
        public void CircuitCombatResultBridge_ComparesAppliedAmounts()
        {
            var attribution = new CircuitAttributionRecorder();
            var shadow = new CircuitShadowRecorder();
            var bridge = new CircuitCombatResultBridge(attribution, shadow);
            var metric = new StableConfigId("metric.defense");
            var origin = new CircuitEvent(
                41,
                1,
                CircuitEventKind.Response,
                new CircuitEntityRef(
                    1,
                    CarrierSlot.Mobility,
                    Guid.NewGuid()),
                default,
                ElementTag.Earth,
                1);
            bridge.Record(StructuredCombatResult.FromLegacy(
                metric,
                CombatOutcomeKind.Defense,
                20f,
                10f,
                default));
            bridge.Record(StructuredCombatResult.FromCircuit(
                metric,
                CombatOutcomeKind.Defense,
                18f,
                10f,
                default,
                origin));

            ShadowMetricComparison comparison =
                shadow.Compare(0f, 0f)[0];

            Assert.That(comparison.LegacyValue, Is.EqualTo(10f));
            Assert.That(comparison.CircuitValue, Is.EqualTo(10f));
            Assert.That(comparison.IsWithinTolerance, Is.True);
        }

        [Test]
        public void StructuredCombatResult_RejectsInvalidMetricAndAmounts()
        {
            Assert.Throws<ArgumentException>(() =>
                StructuredCombatResult.FromLegacy(
                    default,
                    CombatOutcomeKind.Resource,
                    1f,
                    1f,
                    default));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StructuredCombatResult.FromLegacy(
                    new StableConfigId("metric.resource"),
                    CombatOutcomeKind.Resource,
                    float.PositiveInfinity,
                    1f,
                    default));
        }

        [Test]
        public void RunCombatStats_LegacyOverloadRemainsCompatible()
        {
            RunCombatStats.AddPlayerDamage(10f);

            Assert.That(RunCombatStats.TotalPlayerDamage, Is.EqualTo(10f));
            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(1));
            Assert.That(
                RunCombatStats.Results[0].RequestedAmount,
                Is.EqualTo(10f));
            Assert.That(
                RunCombatStats.Results[0].AppliedAmount,
                Is.EqualTo(10f));
        }

        [Test]
        public void RunCombatStats_RecordsRequestedAppliedAndTarget()
        {
            var target = new CircuitEntityRef(
                77,
                null,
                Guid.Empty,
                new StableConfigId("legacy.enemy.test"),
                default);

            RunCombatStats.AddPlayerDamage(15f, 9f, target);

            Assert.That(RunCombatStats.TotalPlayerDamage, Is.EqualTo(9f));
            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(1));
            StructuredCombatResult result = RunCombatStats.Results[0];
            Assert.That(result.Source, Is.EqualTo(CombatResultSource.Legacy));
            Assert.That(result.RequestedAmount, Is.EqualTo(15f));
            Assert.That(result.AppliedAmount, Is.EqualTo(9f));
            Assert.That(result.Target.EntityId, Is.EqualTo(77));
            Assert.That(
                RunCombatStats.CompareDamageShadow(0f, 0f)[0].LegacyValue,
                Is.EqualTo(9f));
        }

        [Test]
        public void RunCombatStats_IgnoresInvalidAppliedAndResetClearsDiagnostics()
        {
            RunCombatStats.AddPlayerDamage(float.PositiveInfinity);
            Assert.That(RunCombatStats.TotalPlayerDamage, Is.Zero);
            Assert.That(RunCombatStats.Results.Count, Is.Zero);

            RunCombatStats.AddPlayerDamage(4f);
            RunCombatStats.Reset();

            Assert.That(RunCombatStats.TotalPlayerDamage, Is.Zero);
            Assert.That(RunCombatStats.Results.Count, Is.Zero);
            Assert.That(
                RunCombatStats.CompareDamageShadow(0f, 0f).Count,
                Is.Zero);
        }

        [Test]
        public void RunCombatStats_ComparesFirstPetCircuitAgainstBaseline()
        {
            var metric = new StableConfigId(
                "combat.player.damage.first-pet.test");
            var target = new CircuitEntityRef(
                81,
                null,
                Guid.Empty,
                new StableConfigId("legacy.enemy.test"),
                default);
            var source = new CircuitEntityRef(
                10,
                CarrierSlot.Mobility,
                Guid.NewGuid(),
                new StableConfigId("runtime.player"),
                FirstSpiritCircuitContent.BounceGelSpecies);
            var origin = new CircuitEvent(
                9,
                3,
                CircuitEventKind.Transform,
                source,
                source,
                ElementTag.Fire,
                FirstSpiritCircuitContent.BounceAttackTag);

            RunCombatStats.RecordLegacyDamageBaseline(
                metric,
                6f,
                5f,
                target);
            RunCombatStats.RecordCircuitDamage(
                metric,
                6f,
                4f,
                target,
                origin);

            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(2));
            Assert.That(
                RunCombatStats.Results[1].Source,
                Is.EqualTo(CombatResultSource.Circuit));
            Assert.That(RunCombatStats.TotalPlayerDamage, Is.Zero);
            ShadowMetricComparison comparison =
                RunCombatStats.CompareShadow(0f, 0f)[0];
            Assert.That(comparison.LegacyValue, Is.EqualTo(5f));
            Assert.That(comparison.CircuitValue, Is.EqualTo(4f));
            Assert.That(comparison.IsWithinTolerance, Is.False);
        }

        [Test]
        public void RunCombatStats_RecordsHealingIncludingOverheal()
        {
            var target = new CircuitEntityRef(
                91,
                null,
                Guid.Empty,
                new StableConfigId("legacy.player"),
                default);

            RunCombatStats.AddPlayerHealing(12f, 3f, target);
            RunCombatStats.AddPlayerHealing(5f, 0f, target);

            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(2));
            Assert.That(
                RunCombatStats.Results[0].Outcome,
                Is.EqualTo(CombatOutcomeKind.Healing));
            Assert.That(
                RunCombatStats.Results[0].RequestedAmount,
                Is.EqualTo(12f));
            Assert.That(
                RunCombatStats.Results[0].AppliedAmount,
                Is.EqualTo(3f));
            Assert.That(
                RunCombatStats.Results[1].AppliedAmount,
                Is.Zero);
        }

        [Test]
        public void RunCombatStats_SeparatesDamageTakenAndDefense()
        {
            var target = new CircuitEntityRef(
                92,
                null,
                Guid.Empty,
                new StableConfigId("legacy.player"),
                default);

            RunCombatStats.AddPlayerDamageTaken(20f, 14f, target);
            RunCombatStats.AddPlayerDefense(20f, 6f, target);

            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(2));
            Assert.That(
                RunCombatStats.Results[0].Outcome,
                Is.EqualTo(CombatOutcomeKind.Damage));
            Assert.That(
                RunCombatStats.Results[0].AppliedAmount,
                Is.EqualTo(14f));
            Assert.That(
                RunCombatStats.Results[1].Outcome,
                Is.EqualTo(CombatOutcomeKind.Defense));
            Assert.That(
                RunCombatStats.Results[1].AppliedAmount,
                Is.EqualTo(6f));
            Assert.That(
                RunCombatStats.CompareShadow(0f, 0f).Count,
                Is.EqualTo(2));
        }

        [Test]
        public void RunCombatStats_RecordsSignedResourceDeltas()
        {
            var metric = new StableConfigId(
                "combat.player.resource.test");
            var target = new CircuitEntityRef(
                93,
                null,
                Guid.Empty,
                new StableConfigId("legacy.player"),
                default);

            RunCombatStats.AddPlayerResource(metric, 8f, 8f, target);
            RunCombatStats.AddPlayerResource(metric, -3f, -3f, target);
            RunCombatStats.AddPlayerResource(metric, -10f, 0f, target);

            Assert.That(RunCombatStats.Results.Count, Is.EqualTo(3));
            Assert.That(
                RunCombatStats.Results[0].Outcome,
                Is.EqualTo(CombatOutcomeKind.Resource));
            Assert.That(
                RunCombatStats.Results[1].AppliedAmount,
                Is.EqualTo(-3f));
            Assert.That(
                RunCombatStats.Results[2].RequestedAmount,
                Is.EqualTo(-10f));
            Assert.That(
                RunCombatStats.Results[2].AppliedAmount,
                Is.Zero);
            Assert.That(
                RunCombatStats.CompareShadow(0f, 0f)[0].LegacyValue,
                Is.EqualTo(5f));
        }

        [Test]
        public void SpiritCircuitOverview_AggregatesCombatStatsForDisplay()
        {
            RunCombatStats.AddPlayerDamage(4f);
            RunCombatStats.AddPlayerDamage(3f);
            RunCombatStats.AddPlayerResource(
                new StableConfigId("combat.player.resource.heat"),
                2f,
                2f,
                default);

            string summary =
                SpiritCircuitOverviewUI.BuildStatsText(
                    RunCombatStats.Results);

            StringAssert.Contains("玩家动作", summary);
            StringAssert.Contains("伤害 7", summary);
            StringAssert.Contains("2次", summary);
            StringAssert.Contains("combat.player.resource.heat", summary);
        }

        [Test]
        public void LegacyCombatResultRecorder_BuildsTypedTargetIdentity()
        {
            var target = new GameObject("TestDestructible");
            try
            {
                target.AddComponent<Destructible>();

                CircuitEntityRef entity =
                    LegacyCombatResultRecorder.BuildTarget(target);

                Assert.That(
                    entity.EntityId,
                    Is.EqualTo(target.GetInstanceID()));
                Assert.That(
                    entity.EntityConfigId.Value,
                    Is.EqualTo("legacy.destructible.Destructible"));

                RunCombatStats.RecordPlayerDamage(7f, 5f, entity);
                Assert.That(RunCombatStats.TotalPlayerDamage, Is.Zero);
                Assert.That(
                    RunCombatStats.Results[0].AppliedAmount,
                    Is.EqualTo(5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SkillCarrierAction_ForwardsTechniqueContext()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            int receivedSlot = -1;
            int receivedCharge = -1;
            SkillData receivedSkill = null;
            var action = new SkillCarrierAction((value, slot, charge) =>
            {
                receivedSkill = value;
                receivedSlot = slot;
                receivedCharge = charge;
                return true;
            });

            try
            {
                var context = new CarrierContext(CarrierSlot.TechniqueE, 3, skill, 0.016f);
                var result = action.Execute(context);

                Assert.That(result.CastStarted, Is.True);
                Assert.That(result.ShouldConsumeCharge, Is.True);
                Assert.That(receivedSkill, Is.SameAs(skill));
                Assert.That(receivedSlot, Is.EqualTo(1));
                Assert.That(receivedCharge, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillCarrierAction_RejectsNonTechniqueCarrier()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            bool invoked = false;
            var action = new SkillCarrierAction((_, _, _) =>
            {
                invoked = true;
                return true;
            });

            try
            {
                var context = new CarrierContext(CarrierSlot.Weapon, 1, skill, 0.016f);
                var result = action.Execute(context);

                Assert.That(result.CastStarted, Is.False);
                Assert.That(invoked, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void WeaponCarrierAction_ForwardsWeaponSlot()
        {
            int calls = 0;
            var action = new WeaponCarrierAction(() =>
            {
                calls++;
                return true;
            });

            CarrierResult result = action.Execute(
                new CarrierContext(
                    CarrierSlot.Weapon,
                    1,
                    null,
                    0.016f));

            Assert.That(result.CastStarted, Is.True);
            Assert.That(result.ShouldConsumeCharge, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void WeaponCarrierAction_RejectsMobilitySlot()
        {
            int calls = 0;
            var action = new WeaponCarrierAction(() =>
            {
                calls++;
                return true;
            });

            CarrierResult result = action.Execute(
                new CarrierContext(
                    CarrierSlot.Mobility,
                    1,
                    null,
                    0.016f));

            Assert.That(result.CastStarted, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void MobilityCarrierAction_ForwardsMobilitySlot()
        {
            int calls = 0;
            var action = new MobilityCarrierAction(() =>
            {
                calls++;
                return true;
            });

            CarrierResult result = action.Execute(
                new CarrierContext(
                    CarrierSlot.Mobility,
                    1,
                    null,
                    0.016f));

            Assert.That(result.CastStarted, Is.True);
            Assert.That(result.ShouldConsumeCharge, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void MobilityCarrierAction_RejectsWeaponSlot()
        {
            int calls = 0;
            var action = new MobilityCarrierAction(() =>
            {
                calls++;
                return true;
            });

            CarrierResult result = action.Execute(
                new CarrierContext(
                    CarrierSlot.Weapon,
                    1,
                    null,
                    0.016f));

            Assert.That(result.CastStarted, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void SkillChargeRuntime_InitializesWithClampedBonus()
        {
            var runtime = new SkillChargeRuntime();
            runtime.AddBonus(0, 2);
            runtime.Initialize(0, 2);

            Assert.That(runtime.GetMaxCharges(0), Is.EqualTo(3));
            Assert.That(runtime.GetCurrentCharges(0), Is.EqualTo(3));
            Assert.That(runtime.GetRemainingTime(0), Is.Zero);
        }

        [Test]
        public void SkillChargeRuntime_RecoversMultipleChargesSequentially()
        {
            var runtime = new SkillChargeRuntime();
            runtime.Initialize(1, 3);
            Assert.That(runtime.Consume(1, 5f), Is.True);
            Assert.That(runtime.Consume(1, 5f), Is.True);

            runtime.Tick(1, 5f, 5f);
            Assert.That(runtime.GetCurrentCharges(1), Is.EqualTo(2));
            Assert.That(runtime.GetRemainingTime(1), Is.EqualTo(5f));

            runtime.Tick(1, 5f, 5f);
            Assert.That(runtime.GetCurrentCharges(1), Is.EqualTo(3));
            Assert.That(runtime.GetRemainingTime(1), Is.Zero);
        }

        [Test]
        public void SkillChargeRuntime_ResetTimerPreservesChargeCount()
        {
            var runtime = new SkillChargeRuntime();
            runtime.Initialize(2, 2);
            runtime.Consume(2, 4f);

            runtime.ResetTimer(2);

            Assert.That(runtime.GetCurrentCharges(2), Is.EqualTo(1));
            Assert.That(runtime.GetRemainingTime(2), Is.Zero);
            runtime.Tick(2, 0.016f, 4f);
            Assert.That(runtime.GetCurrentCharges(2), Is.EqualTo(2));
        }

        [Test]
        public void SkillChargeRuntime_ReducesOnlyRemainingPercentage()
        {
            var runtime = new SkillChargeRuntime();
            runtime.Initialize(0, 1);
            runtime.Consume(0, 10f);

            float reduced = runtime.ReduceRemainingByPercent(0, 0.25f);

            Assert.That(reduced, Is.EqualTo(2.5f));
            Assert.That(runtime.GetRemainingTime(0), Is.EqualTo(7.5f));
            Assert.That(runtime.GetRechargeDuration(0), Is.EqualTo(10f));
        }

        [Test]
        public void SkillChargeInputRuntime_StartsAtLevelOne()
        {
            var runtime = new SkillChargeInputRuntime();
            runtime.Start(1);

            Assert.That(runtime.IsCharging, Is.True);
            Assert.That(runtime.SlotIndex, Is.EqualTo(1));
            Assert.That(runtime.ElapsedTime, Is.Zero);
            Assert.That(runtime.ChargeLevel, Is.EqualTo(1));
        }

        [Test]
        public void SkillChargeInputRuntime_AdvancesAcrossThresholds()
        {
            var runtime = new SkillChargeInputRuntime();
            runtime.Start(0);

            Assert.That(runtime.Advance(0.4f, 0.5f, 1.5f), Is.False);
            Assert.That(runtime.Advance(0.2f, 0.5f, 1.5f), Is.True);
            Assert.That(runtime.ChargeLevel, Is.EqualTo(2));
            Assert.That(runtime.Advance(1f, 0.5f, 1.5f), Is.True);
            Assert.That(runtime.ChargeLevel, Is.EqualTo(3));
        }

        [Test]
        public void SkillChargeInputRuntime_StopReturnsSnapshotAndResets()
        {
            var runtime = new SkillChargeInputRuntime();
            runtime.Start(2);
            runtime.Advance(0.75f, 0.5f, 1.5f);

            SkillChargeRelease release = runtime.Stop();

            Assert.That(release.SlotIndex, Is.EqualTo(2));
            Assert.That(release.ChargeLevel, Is.EqualTo(2));
            Assert.That(release.ChargeTime, Is.EqualTo(0.75f));
            Assert.That(runtime.IsCharging, Is.False);
            Assert.That(runtime.SlotIndex, Is.EqualTo(-1));
            Assert.That(runtime.ChargeLevel, Is.EqualTo(1));
        }

        [Test]
        public void SkillEffectExecutor_ForwardsAreaMultipliers()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillType = SkillType.AreaDamage;
            skill.chargeLv2DamageMultiplier = 1.5f;
            skill.chargeLv2RadiusMultiplier = 1.2f;
            var host = new FakeSkillEffectHost();

            try
            {
                bool cast = new SkillEffectExecutor(host).Execute(skill, 2, 2, 2f);

                Assert.That(cast, Is.True);
                Assert.That(host.Started, Is.True);
                Assert.That(host.GateChecked, Is.True);
                Assert.That(host.DispatchedEffect, Is.EqualTo("Area"));
                Assert.That(host.ReceivedSlot, Is.EqualTo(2));
                Assert.That(host.DamageMultiplier, Is.EqualTo(3f));
                Assert.That(host.RadiusMultiplier, Is.EqualTo(1.2f));
                Assert.That(host.LoggedChargeLevel, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillEffectExecutor_BuffBypassesAnimationGate()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillType = SkillType.Buff;
            var host = new FakeSkillEffectHost { AllowCast = false };

            try
            {
                bool cast = new SkillEffectExecutor(host).Execute(skill, 1, 3);

                Assert.That(cast, Is.True);
                Assert.That(host.Started, Is.True);
                Assert.That(host.GateChecked, Is.False);
                Assert.That(host.DispatchedEffect, Is.EqualTo("Buff"));
                Assert.That(host.ReceivedSlot, Is.EqualTo(1));
                Assert.That(host.LoggedChargeLevel, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillEffectExecutor_GateRejectionPreventsDispatch()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillType = SkillType.Projectile;
            var host = new FakeSkillEffectHost { AllowCast = false };

            try
            {
                bool cast = new SkillEffectExecutor(host).Execute(skill, 0, 1);

                Assert.That(cast, Is.False);
                Assert.That(host.Started, Is.True);
                Assert.That(host.GateChecked, Is.True);
                Assert.That(host.DispatchedEffect, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ImmediateSkillEffectRuntime_HealClampsAndPublishesActualAmount()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.healAmount = 30f;
            skill.healScaling = 0.5f;
            var host = new FakeImmediateSkillEffectHost(50f)
            {
                AttackDamage = 100f,
                MaxHealth = 100f
            };

            try
            {
                float actualHeal = new ImmediateSkillEffectRuntime(host).CastHeal(skill);

                Assert.That(actualHeal, Is.EqualTo(50f));
                Assert.That(host.CurrentHealth, Is.EqualTo(100f));
                Assert.That(host.HealthPublished, Is.True);
                Assert.That(host.PublishedHeal, Is.EqualTo(50f));
                Assert.That(host.RequestedHeal, Is.EqualTo(80f));
                Assert.That(host.RecordedHeal, Is.EqualTo(50f));
                Assert.That(host.VisualPlayed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ImmediateSkillEffectRuntime_BuffBuildsFallbackReduction()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.skillName = "测试护盾";
            skill.buffDuration = 0f;
            skill.vfxDuration = 4f;
            var host = new FakeImmediateSkillEffectHost();

            try
            {
                new ImmediateSkillEffectRuntime(host).CastBuff(skill, -1);

                Assert.That(host.AppliedStatus, Is.Not.Null);
                Assert.That(host.AppliedStatus.duration, Is.EqualTo(4f));
                Assert.That(host.AppliedStatus.modifiers, Has.Count.EqualTo(1));
                Assert.That(host.AppliedStatus.modifiers[0].type, Is.EqualTo(StatType.DamageReduction));
                Assert.That(host.AppliedStatus.modifiers[0].isPercent, Is.False);
                Assert.That(host.AppliedStatus.modifiers[0].value, Is.EqualTo(0.5f));
                Assert.That(host.VisualPlayed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ImmediateSkillEffectRuntime_LethalGuardUsesCooldownFallback()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.armLethalGuard = true;
            skill.lethalGuardDuration = 0f;
            skill.cooldown = 12f;
            var host = new FakeImmediateSkillEffectHost();

            try
            {
                new ImmediateSkillEffectRuntime(host).CastBuff(skill, 0);

                Assert.That(host.ArmedDuration, Is.EqualTo(12f));
                Assert.That(host.AppliedStatus, Is.Null);
                Assert.That(host.VisualPlayed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void WorldSkillEffectRuntime_ZoneUsesGroundPointer()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.zoneFollowPlayer = false;
            var host = new FakeWorldSkillEffectHost
            {
                Origin = new Vector3(1f, 0f, 1f),
                GroundPointer = new Vector3(8f, 0f, 6f)
            };

            try
            {
                Vector3 position = new WorldSkillEffectRuntime(host).CastZone(skill, 1.5f);

                Assert.That(position, Is.EqualTo(host.GroundPointer));
                Assert.That(host.ZoneSpawned, Is.True);
                Assert.That(host.ReceivedDamageMultiplier, Is.EqualTo(1.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void WorldSkillEffectRuntime_FollowZoneUsesOrigin()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.zoneFollowPlayer = true;
            var host = new FakeWorldSkillEffectHost
            {
                Origin = new Vector3(2f, 0f, 3f),
                GroundPointer = new Vector3(9f, 0f, 9f)
            };

            try
            {
                Vector3 position = new WorldSkillEffectRuntime(host).CastZone(skill, 1f);

                Assert.That(position, Is.EqualTo(host.Origin));
                Assert.That(host.PointerQueried, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void WorldSkillEffectRuntime_DecoyUsesDurationFallback()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.summonIsDecoy = true;
            skill.summonDuration = 0f;
            var host = new FakeWorldSkillEffectHost { Origin = Vector3.one };

            try
            {
                new WorldSkillEffectRuntime(host).CastSummon(skill);

                Assert.That(host.DecoySpawned, Is.True);
                Assert.That(host.SummonSpawned, Is.False);
                Assert.That(host.SpawnPosition, Is.EqualTo(Vector3.one));
                Assert.That(host.SummonDuration, Is.EqualTo(3f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void WorldSkillEffectRuntime_SummonUsesAimOffsetAndDamage()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.summonIsDecoy = false;
            skill.summonDuration = 8f;
            var host = new FakeWorldSkillEffectHost
            {
                Origin = new Vector3(1f, 0f, 1f),
                AimDirection = Vector3.forward,
                SummonDamage = 42f
            };

            try
            {
                new WorldSkillEffectRuntime(host).CastSummon(skill);

                Assert.That(host.SummonSpawned, Is.True);
                Assert.That(host.DecoySpawned, Is.False);
                Assert.That(host.SpawnPosition, Is.EqualTo(new Vector3(1f, 0f, 3f)));
                Assert.That(host.SummonDamage, Is.EqualTo(42f));
                Assert.That(host.SummonDuration, Is.EqualTo(8f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void DashSkillEffectRuntime_UsesDefaultDistance()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.dashDistance = 0f;
            var host = new FakeDashSkillEffectHost
            {
                Origin = Vector3.zero,
                AimDirection = Vector3.forward,
                ResolvedDestination = Vector3.forward * 8f
            };

            try
            {
                float traveled = new DashSkillEffectRuntime(host).Cast(skill);

                Assert.That(host.RequestedDistance, Is.EqualTo(8f));
                Assert.That(host.MovedTo, Is.EqualTo(Vector3.forward * 8f));
                Assert.That(traveled, Is.EqualTo(8f));
                Assert.That(host.TrailShown, Is.True);
                Assert.That(host.VisualPlayed, Is.True);
                Assert.That(host.TrailDamageApplied, Is.False);
                Assert.That(host.InvincibleDuration, Is.EqualTo(-1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void DashSkillEffectRuntime_AppliesOptionalBranches()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.dashDistance = 5f;
            skill.dashInvulnerable = true;
            skill.dashInvulnDuration = 1.2f;
            skill.leaveTrail = true;
            var host = new FakeDashSkillEffectHost
            {
                Origin = Vector3.zero,
                AimDirection = Vector3.forward,
                ResolvedDestination = Vector3.forward * 3f
            };

            try
            {
                float traveled = new DashSkillEffectRuntime(host).Cast(skill);

                Assert.That(host.RequestedDistance, Is.EqualTo(5f));
                Assert.That(traveled, Is.EqualTo(3f));
                Assert.That(host.InvincibleDuration, Is.EqualTo(1.2f));
                Assert.That(host.TrailDamageApplied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void AreaSkillEffectRuntime_AppliesRadiusAndDamageMultipliers()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.aoeRadius = 3f;
            var host = new FakeAreaSkillEffectHost
            {
                Targets = new[]
                {
                    new AreaSkillTarget("target", Vector3.one, 5f)
                },
                CalculatedDamage = 10f
            };

            try
            {
                bool cast = new AreaSkillEffectRuntime(host).Cast(skill, 1.5f, 2f, 0);

                Assert.That(cast, Is.True);
                Assert.That(host.VisualRadius, Is.EqualTo(6f));
                Assert.That(host.AppliedDamage, Is.EqualTo(15f));
                Assert.That(host.DamageApplications, Is.EqualTo(1));
                Assert.That(host.HitPublished, Is.True);
                Assert.That(host.SlotModifiersApplied, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void AreaSkillEffectRuntime_UsesRunTotalAndSkipsNonDamageable()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.damageFromRunTotal = true;
            skill.runTotalDamageRatio = 0.1f;
            skill.freezeOnHitChance = 1f;
            var host = new FakeAreaSkillEffectHost
            {
                TotalPlayerDamage = 200f,
                ChanceResult = true,
                Targets = new[]
                {
                    new AreaSkillTarget("wall", Vector3.zero, 0f, false),
                    new AreaSkillTarget("enemy", Vector3.one, 0f)
                }
            };

            try
            {
                new AreaSkillEffectRuntime(host).Cast(skill, 2f, 1f, -1);

                Assert.That(host.DamageApplications, Is.EqualTo(1));
                Assert.That(host.AppliedDamage, Is.EqualTo(40f));
                Assert.That(host.FreezeApplied, Is.True);
                Assert.That(host.HitPublished, Is.True);
                Assert.That(host.SlotModifiersApplied, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void AreaSkillEffectRuntime_ExpandsAndSpawnsEnhancementBranches()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.aoeRadius = 4f;
            var host = new FakeAreaSkillEffectHost
            {
                EnhancementActive = true,
                EnhancementRadiusMultiplier = 1.5f,
                HasSustainedEnhancement = true,
                HasDelayedBlastEnhancement = true,
                BaseMultiplier = 2f
            };

            try
            {
                new AreaSkillEffectRuntime(host).Cast(skill, 3f, 2f, -1);

                Assert.That(host.VisualRadius, Is.EqualTo(12f));
                Assert.That(host.SustainedMultiplier, Is.EqualTo(2.1f).Within(0.0001f));
                Assert.That(host.DelayedMultiplier, Is.EqualTo(9f));
                Assert.That(host.DelayedRadius, Is.EqualTo(13.8f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ProjectileSkillEffectRuntime_BuildsSymmetricSpread()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.projectileCount = 3;
            skill.spreadAngle = 20f;
            var host = new FakeProjectileSkillEffectHost
            {
                AimDirection = Vector3.forward,
                CalculatedDamage = 10f
            };

            try
            {
                int count = new ProjectileSkillEffectRuntime(host).Cast(skill, 2f);

                Assert.That(count, Is.EqualTo(3));
                Assert.That(host.Directions, Has.Count.EqualTo(3));
                Assert.That(Vector3.Dot(host.Directions[1], Vector3.forward), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(host.Directions[0], Is.Not.EqualTo(host.Directions[2]));
                Assert.That(host.ReceivedDamage, Is.EqualTo(20f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ProjectileSkillEffectRuntime_RingUsesAtLeastEightShots()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.projectileCount = 1;
            var host = new FakeProjectileSkillEffectHost
            {
                AimDirection = Vector3.forward,
                EnhancementActive = true,
                RingPattern = true
            };

            try
            {
                int count = new ProjectileSkillEffectRuntime(host).Cast(skill, 1f);

                Assert.That(count, Is.EqualTo(8));
                Assert.That(host.Directions, Has.Count.EqualTo(8));
                Assert.That(host.EnhancementApplied, Is.True);
                Assert.That(host.Directions[0], Is.EqualTo(Vector3.forward));
                Assert.That(host.Directions[2].x, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ProjectileSkillEffectRuntime_WallOffsetsFiveParallelShots()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.projectileCount = 1;
            var host = new FakeProjectileSkillEffectHost
            {
                ProjectileOrigin = Vector3.zero,
                AimDirection = Vector3.forward,
                EnhancementActive = true,
                WallPattern = true
            };

            try
            {
                int count = new ProjectileSkillEffectRuntime(host).Cast(skill, 1f);

                Assert.That(count, Is.EqualTo(5));
                Assert.That(host.Positions[0].x, Is.EqualTo(-2.2f).Within(0.0001f));
                Assert.That(host.Positions[2], Is.EqualTo(Vector3.zero));
                Assert.That(host.Positions[4].x, Is.EqualTo(2.2f).Within(0.0001f));
                Assert.That(host.Directions[0], Is.EqualTo(Vector3.forward));
                Assert.That(host.Directions[4], Is.EqualTo(Vector3.forward));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void ProjectileSkillEffectRuntime_UsesFarthestTargetOverride()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.projectileCount = 1;
            var host = new FakeProjectileSkillEffectHost
            {
                AimDirection = Vector3.forward,
                EnhancementActive = true,
                TargetFarthest = true,
                HasFarthestTarget = true,
                FarthestDirection = Vector3.right
            };

            try
            {
                new ProjectileSkillEffectRuntime(host).Cast(skill, 1f);

                Assert.That(host.Directions[0], Is.EqualTo(Vector3.right));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillChargeEffectCoordinator_AppliesAndRestoresMovement()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.chargeMoveSpeedMultiplier = 0.4f;
            var host = new FakeSkillChargeEffectHost();
            var coordinator = new SkillChargeEffectCoordinator(host);

            try
            {
                coordinator.Begin(1, skill);

                Assert.That(host.MoveSpeed, Is.EqualTo(4f));
                Assert.That(host.LastIsCharging, Is.True);
                Assert.That(coordinator.SessionActive, Is.True);

                coordinator.Complete(1);

                Assert.That(host.MoveSpeed, Is.EqualTo(10f));
                Assert.That(host.LastIsCharging, Is.False);
                Assert.That(coordinator.SessionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillChargeEffectCoordinator_SkipsMovementAtFullMultiplier()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.chargeMoveSpeedMultiplier = 1f;
            var host = new FakeSkillChargeEffectHost();
            var coordinator = new SkillChargeEffectCoordinator(host);

            try
            {
                coordinator.Begin(0, skill);
                coordinator.Complete(0);

                Assert.That(host.MoveSpeed, Is.EqualTo(10f));
                Assert.That(host.Calls, Does.Not.Contain("speed:10.0"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void SkillChargeEffectCoordinator_CancelResetsBeforeEndEvent()
        {
            var skill = ScriptableObject.CreateInstance<SkillData>();
            skill.chargeMoveSpeedMultiplier = 0.5f;
            var host = new FakeSkillChargeEffectHost();
            var coordinator = new SkillChargeEffectCoordinator(host);

            try
            {
                coordinator.Begin(2, skill);
                host.Calls.Clear();
                coordinator.Cancel(2, () => host.Calls.Add("reset"));

                Assert.That(host.Calls[0], Is.EqualTo("speed:10.0"));
                Assert.That(host.Calls[1], Is.EqualTo("reset"));
                Assert.That(host.Calls[2], Is.EqualTo("publish:False"));
                Assert.That(host.Calls[3], Is.EqualTo("log"));
                Assert.That(coordinator.SessionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void MeleeAttackRuntime_RequestsOnlyValidInput()
        {
            var host = new FakeMeleeAttackHost();
            var runtime = new MeleeAttackRuntime(host);

            Assert.That(runtime.TryRequestAttack(false, false, false, 1f), Is.False);
            Assert.That(runtime.TryRequestAttack(true, true, false, 1f), Is.False);
            Assert.That(runtime.TryRequestAttack(true, false, true, 1f), Is.False);
            Assert.That(runtime.TryRequestAttack(true, false, false, 1f), Is.True);
            Assert.That(host.AttackRequests, Is.EqualTo(1));
        }

        [Test]
        public void MeleeAttackRuntime_ClosedWindowOnlyDrawsPreview()
        {
            var host = new FakeMeleeAttackHost
            {
                IsHitWindowOpen = false
            };
            var runtime = new MeleeAttackRuntime(host);

            Assert.That(runtime.TickHitWindow(), Is.False);
            Assert.That(host.LastDrawActive, Is.False);
            Assert.That(host.ResolveCalls, Is.Zero);
        }

        [Test]
        public void MeleeAttackRuntime_HitsOnceUntilSwingReset()
        {
            var host = new FakeMeleeAttackHost
            {
                IsHitWindowOpen = true,
                ComboStep = 2
            };
            var runtime = new MeleeAttackRuntime(host);

            Assert.That(runtime.TickHitWindow(), Is.True);
            Assert.That(runtime.TickHitWindow(), Is.False);
            Assert.That(host.ResolveCalls, Is.EqualTo(1));
            Assert.That(host.PublishCalls, Is.EqualTo(1));
            Assert.That(host.CooldownReductions, Is.EqualTo(1));

            runtime.ResetSwing();

            Assert.That(runtime.TickHitWindow(), Is.True);
            Assert.That(host.ResolveCalls, Is.EqualTo(2));
        }

        [Test]
        public void MeleeAttackRuntime_RangedBasicSkipsMeleeWindow()
        {
            var host = new FakeMeleeAttackHost
            {
                IsRangedBasic = true,
                IsHitWindowOpen = true
            };
            var runtime = new MeleeAttackRuntime(host);

            Assert.That(runtime.TickHitWindow(), Is.False);
            Assert.That(host.LastDrawActive, Is.Null);
            Assert.That(host.ResolveCalls, Is.Zero);
        }

        [Test]
        public void BasicAttackEffectRuntime_MeleeFiltersArcAndAppliesComboDamage()
        {
            var host = new FakeBasicAttackEffectHost
            {
                MeleeOrigin = Vector3.zero,
                AimDirection = Vector3.forward,
                ComboMultiplier = 1.5f,
                BaseDamage = 20f,
                Targets = new[]
                {
                    new BasicAttackTarget(
                        "front",
                        "front-event",
                        Vector3.forward,
                        Vector3.forward,
                        0f),
                    new BasicAttackTarget(
                        "behind",
                        "behind-event",
                        Vector3.back,
                        Vector3.back,
                        0f)
                }
            };

            MeleeHitResult result =
                new BasicAttackEffectRuntime(host).ResolveMeleeHits(2);

            Assert.That(result.HitAny, Is.True);
            Assert.That(result.FirstTarget, Is.EqualTo("front-event"));
            Assert.That(host.DamageApplications, Is.EqualTo(1));
            Assert.That(host.AppliedDamage, Is.EqualTo(30f));
            Assert.That(host.HitVisuals, Is.EqualTo(1));
        }

        [Test]
        public void BasicAttackEffectRuntime_ConvertsMeleeDamageToHealing()
        {
            var host = new FakeBasicAttackEffectHost
            {
                MeleeOrigin = Vector3.zero,
                ConvertMeleeDamageToHealing = true,
                BaseDamage = 24f,
                Targets = new[]
                {
                    new BasicAttackTarget(
                        "enemy",
                        "enemy-event",
                        Vector3.forward,
                        Vector3.forward,
                        0f)
                }
            };

            MeleeHitResult result =
                new BasicAttackEffectRuntime(host).ResolveMeleeHits(0);

            Assert.That(result.HitAny, Is.True);
            Assert.That(result.FirstTarget, Is.Null);
            Assert.That(host.DamageApplications, Is.Zero);
            Assert.That(host.HealingApplications, Is.EqualTo(1));
            Assert.That(host.AppliedHealing, Is.EqualTo(12f));
        }

        [Test]
        public void BasicAttackEffectRuntime_RangedUsesFallbackAndFinisher()
        {
            var host = new FakeBasicAttackEffectHost
            {
                AimDirection = Vector3.zero,
                ForwardFallback = Vector3.right,
                ComboMultiplier = 1.5f,
                BaseDamage = 20f,
                RangedDamageMultiplier = 1.2f
            };

            float damage = new BasicAttackEffectRuntime(host).FireRangedBasic(2);

            Assert.That(host.ProjectileSpawned, Is.True);
            Assert.That(host.ProjectileDirection, Is.EqualTo(Vector3.right));
            Assert.That(damage, Is.EqualTo(36f).Within(0.0001f));
            Assert.That(host.ProjectileDamage, Is.EqualTo(36f).Within(0.0001f));
            Assert.That(host.CooldownReductions, Is.EqualTo(1));
        }

        [Test]
        public void EvadeCommandRuntime_PrefersMovementAndConsumesCharge()
        {
            var host = new FakeEvadeCommandHost();
            var runtime = new EvadeCommandRuntime(host);
            runtime.Configure(6f, 0.3f, 2, 1.5f);
            runtime.BeginFrame();

            bool executed = runtime.HandleInput(
                true,
                false,
                Vector3.right,
                Vector3.forward);

            Assert.That(executed, Is.True);
            Assert.That(runtime.RequestedThisFrame, Is.True);
            Assert.That(runtime.IsDashing, Is.True);
            Assert.That(runtime.Direction, Is.EqualTo(Vector3.right));
            Assert.That(runtime.Charges, Is.EqualTo(1));
            Assert.That(host.InvincibleDuration, Is.EqualTo(0.3f));
            Assert.That(host.LastCharges, Is.EqualTo(1));
        }

        [Test]
        public void EvadeCommandRuntime_UsesAimFallbackAndPublishesFinish()
        {
            var host = new FakeEvadeCommandHost();
            var runtime = new EvadeCommandRuntime(host);
            runtime.Configure(5f, 0.2f, 2, 1.5f);
            runtime.HandleInput(
                true,
                false,
                Vector3.zero,
                Vector3.forward);

            Assert.That(
                runtime.TickDash(0.2f, Vector3.one, out Vector3 velocity),
                Is.True);
            Assert.That(velocity, Is.EqualTo(Vector3.forward * 25f));
            Assert.That(runtime.IsDashing, Is.False);
            Assert.That(host.FinishedEvents, Is.EqualTo(1));
            Assert.That(host.FinishedPosition, Is.EqualTo(Vector3.one));
            Assert.That(host.FinishedDirection, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void EvadeCommandRuntime_BuffersWhenChargesAreEmpty()
        {
            var host = new FakeEvadeCommandHost();
            var runtime = new EvadeCommandRuntime(host);
            runtime.Configure(5f, 0.2f, 1, 1.5f);
            runtime.HandleInput(
                true,
                false,
                Vector3.forward,
                Vector3.right);
            runtime.TickDash(0.2f, Vector3.zero, out _);
            runtime.BeginFrame();

            bool executed = runtime.HandleInput(
                true,
                false,
                Vector3.forward,
                Vector3.right);

            Assert.That(executed, Is.False);
            Assert.That(runtime.RequestedThisFrame, Is.True);
            Assert.That(host.BufferedRequests, Is.EqualTo(1));
            Assert.That(host.PlayRequests, Is.EqualTo(1));
        }

        [Test]
        public void EvadeCommandRuntime_RechargesSequentially()
        {
            var host = new FakeEvadeCommandHost();
            var runtime = new EvadeCommandRuntime(host);
            runtime.Configure(5f, 0.1f, 2, 1f);
            runtime.HandleInput(
                true,
                false,
                Vector3.forward,
                Vector3.right);
            runtime.TickDash(0.1f, Vector3.zero, out _);
            runtime.HandleBuffered(true, Vector3.forward, Vector3.right);

            runtime.TickRecharge(1f);
            Assert.That(runtime.Charges, Is.EqualTo(1));
            Assert.That(host.LastRechargeProgress, Is.EqualTo(0f));

            runtime.TickRecharge(1f);
            Assert.That(runtime.Charges, Is.EqualTo(2));
            Assert.That(host.LastRechargeProgress, Is.EqualTo(1f));
        }
    }

    public static class ProjectRP0ContractTestRunner
    {
        [MenuItem("ProjectR/开发工具/运行载体与回路契约测试")]
        public static void RunAll()
        {
            var fixture = new ProjectRP0ContractTests();
            int passed = 0;

            Run(fixture, fixture.ProjectRFlags_DefaultsMatchCurrentRollout, ref passed);
            Run(fixture, fixture.ProjectRFlags_RuntimeOverrideWins, ref passed);
            Run(fixture, () => fixture.LegacySlotMapping_IsStable(0, CarrierSlot.TechniqueQ), ref passed);
            Run(fixture, () => fixture.LegacySlotMapping_IsStable(1, CarrierSlot.TechniqueE), ref passed);
            Run(fixture, () => fixture.LegacySlotMapping_IsStable(2, CarrierSlot.TechniqueR), ref passed);
            Run(fixture, fixture.LegacySnapshot_PreservesEnhancementFields, ref passed);
            Run(fixture, fixture.CircuitEvent_RejectsNegativeDepth, ref passed);
            Run(fixture, fixture.CircuitEvent_PreservesCausalIdentity, ref passed);
            Run(fixture, fixture.StableConfigId_TrimsAndUsesOrdinalIdentity, ref passed);
            Run(fixture, fixture.SpiritIdentity_RequiresStableGuidAndConfig, ref passed);
            Run(fixture, fixture.SpiritState_PreservesRelationshipsAndEnlightenment, ref passed);
            Run(fixture, fixture.SpiritSaveMapper_RoundTripsPermanentStateOnly, ref passed);
            Run(fixture, fixture.SpiritSaveMapper_SkipsInvalidNestedEntries, ref passed);
            Run(fixture, fixture.StarterSpiritTalentCatalog_DefinesEighteenPerSpecies, ref passed);
            Run(fixture, fixture.StarterSpiritTalentCatalog_FillsEveryVisualSlot, ref passed);
            Run(fixture, fixture.SpiritTalentTreePresentation_MapsFourTiersAndThreeBranches, ref passed);
            Run(fixture, fixture.SpiritRunTalentState_HonorsAllAndAnyPrerequisites, ref passed);
            Run(fixture, fixture.StarterSpiritTalentBuildSamples_ReachNineCapstonesInFivePoints, ref passed);
            Run(fixture, fixture.StarterSpiritTalentBuildSamples_ProduceDistinctTuningProfiles, ref passed);
            Run(fixture, fixture.SpiritTalentTreeVisuals_ExposeDistinctNodeGrammar, ref passed);
            Run(fixture, fixture.SpiritRunTalentState_EarnsPointsAndHonorsPermanentLocks, ref passed);
            Run(fixture, fixture.SpiritSaveMapper_RoundTripsUnlockedTalentNodes, ref passed);
            Run(fixture, fixture.SpiritTalentProgression_OpensTiersAtExpeditionMilestones, ref passed);
            Run(fixture, fixture.SpiritTalentProgression_CommitsOnlyActiveRosterMembers, ref passed);
            Run(fixture, fixture.SpiritRunTalentState_RefundsOnlyRunSelections, ref passed);
            Run(fixture, fixture.SpiritRunTalentState_AllowsOnlyOneCapstonePerSpirit, ref passed);
            Run(fixture, fixture.FirstPetCircuitTuning_ChangesHeatThresholdAndRefund, ref passed);
            Run(fixture, fixture.FirstPetCircuitTuning_ExposesDevelopmentEffects, ref passed);
            Run(fixture, fixture.FirstPetCircuit_LinkTalentRefundsHeatOnlyOnEcho, ref passed);
            Run(fixture, fixture.SaveSystem_MigratesSpiritFieldsToSchemaEight, ref passed);
            Run(fixture, fixture.SaveSystem_MigratesOldCompletedPrologueToSchemaNine, ref passed);
            Run(fixture, fixture.StarterSpiritChoice_AddsExactlyOneSelectedPet, ref passed);
            Run(fixture, fixture.StarterSpiritChoice_RejectsRepeatAndNonEmptyRoster, ref passed);
            Run(fixture, fixture.StarterSpiritChoicePresentation_ExposesOnlyThreeProfiles, ref passed);
            Run(fixture, fixture.StarterSpiritChoiceSession_RequiresExplicitConfirmation, ref passed);
            Run(fixture, fixture.StarterSpiritChoiceUI_RequiresOneEntityPerProfile, ref passed);
            Run(fixture, fixture.StarterPrologueScene_LoadsReusableWhiteboxPrefab, ref passed);
            Run(fixture, fixture.ProjectRUITheme_LoadsGeneratedCoreArt, ref passed);
            Run(fixture, fixture.StarterPrologueTraining_RunsAgitatedThenPossessedWaves, ref passed);
            Run(fixture, fixture.StarterPrologueCombatTargets_AcceptBounceGelImpulse, ref passed);
            Run(fixture, fixture.StarterPrologueResume_UsesCheckpointMarkers, ref passed);
            Run(fixture, fixture.StarterPrologue_RequiresExactlyOneFirstAttachment, ref passed);
            Run(fixture, fixture.StarterPrologue_AdvancesMonotonicallyAndRestoresCarrier, ref passed);
            Run(fixture, fixture.StarterPrologue_RejectsSkippedOrInvalidProgress, ref passed);
            Run(fixture, fixture.StarterPrologueHomeTransition_CommitsOnlyAfterRescue, ref passed);
            Run(fixture, fixture.StarterPrologueTiming_UsesInclusiveSixToSevenPointFiveMinuteTarget, ref passed);
            Run(fixture, fixture.StarterPrologueOpening_UsesV5NonBlockingLines, ref passed);
            Run(fixture, fixture.StarterPrologueObjective_LabelsDistinctPhases, ref passed);
            Run(fixture, fixture.StarterProloguePathGuide_BuildsThreeOrderedPoints, ref passed);
            Run(fixture, fixture.StarterPrologueBoundary_RevealsOnlyNearItsEdge, ref passed);
            Run(fixture, fixture.SpiritAttachmentLayout_RecognizesThreePatterns, ref passed);
            Run(fixture, fixture.SpiritAttachmentLayout_MovesWithoutDroppingStateAndCapsRoster, ref passed);
            Run(fixture, fixture.SpiritLoadoutRuntime_AddsThreeAndRejectsFourth, ref passed);
            Run(fixture, fixture.SpiritLoadoutRuntime_BlocksMigrationInCombat, ref passed);
            Run(fixture, fixture.SpiritLoadoutRuntime_MigratesWithoutClearingGrowth, ref passed);
            Run(fixture, fixture.SpiritLoadoutRuntime_NoChangeIsSafeButRemovalIsGated, ref passed);
            Run(fixture, fixture.CircuitEntityRef_PreservesStableConfigIds, ref passed);
            Run(fixture, fixture.DeclarativeCircuitRule_MatchesKindsTagsAndElement, ref passed);
            Run(fixture, fixture.DeclarativeCircuitRule_CanPreserveTagsAndElement, ref passed);
            Run(fixture, fixture.SpiritCircuitRuleBinder_PreservesCarrierIdentity, ref passed);
            Run(fixture, fixture.DeclarativeCircuitRules_FormBoundedThreeSpiritLoop, ref passed);
            Run(fixture, fixture.CircuitRuleDescriptor_PreservesExplainableIdentity, ref passed);
            Run(fixture, fixture.CircuitPreviewGraph_DistinguishesGuaranteedAndPossibleEdges, ref passed);
            Run(fixture, fixture.CircuitPreviewGraph_ReportsInputAndOutputBreakpoints, ref passed);
            Run(fixture, fixture.CircuitPreviewGraph_DetectsThreeRuleCycle, ref passed);
            Run(fixture, fixture.FirstPetCircuit_WaitsForThreeDirectHits, ref passed);
            Run(fixture, fixture.FirstPetCircuit_EmitsSparkEchoAndBounce, ref passed);
            Run(fixture, fixture.FirstPetCircuit_PreviewExplainsLinearChain, ref passed);
            Run(fixture, fixture.FirstPetCircuit_RequiresPlayerInputForNextCycle, ref passed);
            Run(fixture, fixture.FirstPetCircuit_RefundsOnlyOneResolvedBouncePerChain, ref passed);
            Run(fixture, fixture.FirstPetCircuit_FinalEventSupportsShadowAttribution, ref passed);
            Run(fixture, fixture.FirstPetLoadoutProvider_RestoresActiveRosterAndCarriers, ref passed);
            Run(fixture, fixture.FirstPetLoadoutProvider_RejectsIncompleteActiveRoster, ref passed);
            Run(fixture, fixture.SpiritCircuitHUD_MapsFiveCarrierSlotsInVisualOrder, ref passed);
            Run(fixture, fixture.SpiritCircuitHUD_MapsOnlyStarterAttachmentTargets, ref passed);
            Run(fixture, fixture.StarterSpiritCarrier_ChangesTriggerAndPreservesProgress, ref passed);
            Run(fixture, fixture.StarterSpiritCarrierController_AppliesAndResetsRunTalent, ref passed);
            Run(fixture, fixture.StarterSpiritCarrier_BlocksCombatAndLockedTechnique, ref passed);
            Run(fixture, fixture.StarterSpiritCarrier_AllThreeSpeciesUseThreeCarriers, ref passed);
            Run(fixture, fixture.Projectile_PreservesAndResetsSkillSource, ref passed);
            Run(fixture, fixture.Projectile_IgnoresNonDamageableLogicTriggers, ref passed);
            Run(fixture, fixture.StarterSpiritCarrierProvider_RestoresOnlySingleSpark, ref passed);
            Run(fixture, fixture.FirstPetCircuitController_AcceptsOnlyResolvedCarrierHits, ref passed);
            Run(fixture, fixture.FirstPetCircuitController_StagesOneOneOneByPlayerAction, ref passed);
            Run(fixture, fixture.FirstPetCircuitController_MigrationRebuildsPreview, ref passed);
            Run(fixture, fixture.FirstPetCircuitController_StaysDormantWhenFlagIsOff, ref passed);
            Run(fixture, fixture.CircuitRuntime_ChainsSourceResponseAndTransform, ref passed);
            Run(fixture, fixture.CircuitRuntime_BlocksBeyondMaximumDepth, ref passed);
            Run(fixture, fixture.CircuitRuntime_BlocksRepeatedRuleWithinChain, ref passed);
            Run(fixture, fixture.CircuitRuntime_BlocksRepeatedEventKindAcrossRules, ref passed);
            Run(fixture, fixture.CircuitRuntime_EnforcesRuleFrequencyAcrossFrames, ref passed);
            Run(fixture, fixture.CircuitRuntime_StopsAtSharedFrameBudget, ref passed);
            Run(fixture, fixture.CircuitAttributionRecorder_PreservesBranchSources, ref passed);
            Run(fixture, fixture.CircuitAttributionRecorder_AggregatesEquivalentSources, ref passed);
            Run(fixture, fixture.CircuitShadowRecorder_AggregatesAndComparesTolerance, ref passed);
            Run(fixture, fixture.CircuitShadowRecorder_ReportsMissingSideAndResets, ref passed);
            Run(fixture, fixture.CircuitCombatResultBridge_AttributesCircuitResult, ref passed);
            Run(fixture, fixture.CircuitCombatResultBridge_RecordsLegacyWithoutAttribution, ref passed);
            Run(fixture, fixture.CircuitCombatResultBridge_ComparesAppliedAmounts, ref passed);
            Run(fixture, fixture.StructuredCombatResult_RejectsInvalidMetricAndAmounts, ref passed);
            Run(fixture, fixture.RunCombatStats_LegacyOverloadRemainsCompatible, ref passed);
            Run(fixture, fixture.RunCombatStats_RecordsRequestedAppliedAndTarget, ref passed);
            Run(fixture, fixture.RunCombatStats_IgnoresInvalidAppliedAndResetClearsDiagnostics, ref passed);
            Run(fixture, fixture.RunCombatStats_ComparesFirstPetCircuitAgainstBaseline, ref passed);
            Run(fixture, fixture.RunCombatStats_RecordsHealingIncludingOverheal, ref passed);
            Run(fixture, fixture.RunCombatStats_SeparatesDamageTakenAndDefense, ref passed);
            Run(fixture, fixture.RunCombatStats_RecordsSignedResourceDeltas, ref passed);
            Run(fixture, fixture.SpiritCircuitOverview_AggregatesCombatStatsForDisplay, ref passed);
            Run(fixture, fixture.LegacyCombatResultRecorder_BuildsTypedTargetIdentity, ref passed);
            Run(fixture, fixture.SkillCarrierAction_ForwardsTechniqueContext, ref passed);
            Run(fixture, fixture.SkillCarrierAction_RejectsNonTechniqueCarrier, ref passed);
            Run(fixture, fixture.WeaponCarrierAction_ForwardsWeaponSlot, ref passed);
            Run(fixture, fixture.WeaponCarrierAction_RejectsMobilitySlot, ref passed);
            Run(fixture, fixture.MobilityCarrierAction_ForwardsMobilitySlot, ref passed);
            Run(fixture, fixture.MobilityCarrierAction_RejectsWeaponSlot, ref passed);
            Run(fixture, fixture.SkillChargeRuntime_InitializesWithClampedBonus, ref passed);
            Run(fixture, fixture.SkillChargeRuntime_RecoversMultipleChargesSequentially, ref passed);
            Run(fixture, fixture.SkillChargeRuntime_ResetTimerPreservesChargeCount, ref passed);
            Run(fixture, fixture.SkillChargeRuntime_ReducesOnlyRemainingPercentage, ref passed);
            Run(fixture, fixture.SkillChargeInputRuntime_StartsAtLevelOne, ref passed);
            Run(fixture, fixture.SkillChargeInputRuntime_AdvancesAcrossThresholds, ref passed);
            Run(fixture, fixture.SkillChargeInputRuntime_StopReturnsSnapshotAndResets, ref passed);
            Run(fixture, fixture.SkillEffectExecutor_ForwardsAreaMultipliers, ref passed);
            Run(fixture, fixture.SkillEffectExecutor_BuffBypassesAnimationGate, ref passed);
            Run(fixture, fixture.SkillEffectExecutor_GateRejectionPreventsDispatch, ref passed);
            Run(fixture, fixture.ImmediateSkillEffectRuntime_HealClampsAndPublishesActualAmount, ref passed);
            Run(fixture, fixture.ImmediateSkillEffectRuntime_BuffBuildsFallbackReduction, ref passed);
            Run(fixture, fixture.ImmediateSkillEffectRuntime_LethalGuardUsesCooldownFallback, ref passed);
            Run(fixture, fixture.WorldSkillEffectRuntime_ZoneUsesGroundPointer, ref passed);
            Run(fixture, fixture.WorldSkillEffectRuntime_FollowZoneUsesOrigin, ref passed);
            Run(fixture, fixture.WorldSkillEffectRuntime_DecoyUsesDurationFallback, ref passed);
            Run(fixture, fixture.WorldSkillEffectRuntime_SummonUsesAimOffsetAndDamage, ref passed);
            Run(fixture, fixture.DashSkillEffectRuntime_UsesDefaultDistance, ref passed);
            Run(fixture, fixture.DashSkillEffectRuntime_AppliesOptionalBranches, ref passed);
            Run(fixture, fixture.AreaSkillEffectRuntime_AppliesRadiusAndDamageMultipliers, ref passed);
            Run(fixture, fixture.AreaSkillEffectRuntime_UsesRunTotalAndSkipsNonDamageable, ref passed);
            Run(fixture, fixture.AreaSkillEffectRuntime_ExpandsAndSpawnsEnhancementBranches, ref passed);
            Run(fixture, fixture.ProjectileSkillEffectRuntime_BuildsSymmetricSpread, ref passed);
            Run(fixture, fixture.ProjectileSkillEffectRuntime_RingUsesAtLeastEightShots, ref passed);
            Run(fixture, fixture.ProjectileSkillEffectRuntime_WallOffsetsFiveParallelShots, ref passed);
            Run(fixture, fixture.ProjectileSkillEffectRuntime_UsesFarthestTargetOverride, ref passed);
            Run(fixture, fixture.SkillChargeEffectCoordinator_AppliesAndRestoresMovement, ref passed);
            Run(fixture, fixture.SkillChargeEffectCoordinator_SkipsMovementAtFullMultiplier, ref passed);
            Run(fixture, fixture.SkillChargeEffectCoordinator_CancelResetsBeforeEndEvent, ref passed);
            Run(fixture, fixture.MeleeAttackRuntime_RequestsOnlyValidInput, ref passed);
            Run(fixture, fixture.MeleeAttackRuntime_ClosedWindowOnlyDrawsPreview, ref passed);
            Run(fixture, fixture.MeleeAttackRuntime_HitsOnceUntilSwingReset, ref passed);
            Run(fixture, fixture.MeleeAttackRuntime_RangedBasicSkipsMeleeWindow, ref passed);
            Run(fixture, fixture.BasicAttackEffectRuntime_MeleeFiltersArcAndAppliesComboDamage, ref passed);
            Run(fixture, fixture.BasicAttackEffectRuntime_ConvertsMeleeDamageToHealing, ref passed);
            Run(fixture, fixture.BasicAttackEffectRuntime_RangedUsesFallbackAndFinisher, ref passed);
            Run(fixture, fixture.EvadeCommandRuntime_PrefersMovementAndConsumesCharge, ref passed);
            Run(fixture, fixture.EvadeCommandRuntime_UsesAimFallbackAndPublishesFinish, ref passed);
            Run(fixture, fixture.EvadeCommandRuntime_BuffersWhenChargesAreEmpty, ref passed);
            Run(fixture, fixture.EvadeCommandRuntime_RechargesSequentially, ref passed);

            Debug.Log(
                $"<color=green>[ProjectR] {passed}项载体与回路契约测试通过。</color>");
        }

        private static void Run(ProjectRP0ContractTests fixture, Action test, ref int passed)
        {
            fixture.SetUp();
            try
            {
                test();
                passed++;
            }
            finally
            {
                fixture.TearDown();
            }
        }
    }
}
