using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 将现有载体命中事件接到首批三宠原型。
    /// 只消费同帧已完成扣血的目标，避免未命中或旧事件推进热度。
    /// </summary>
    public sealed class FirstSpiritCircuitController :
        MonoBehaviour,
        ICircuitEventSink
    {
        private const float SparkPendingDuration = 12f;
        private const float EchoPendingDuration = 10f;

        private readonly Dictionary<int, CircuitEntityRef>
            _resolvedTargets = new();
        private readonly Dictionary<GameObject, float> _echoMarks = new();

        private FirstSpiritCircuitRuntime _runtime;
        private int _resolvedFrame = -1;
        private int _runtimeFrame = -1;
        private ulong _nextChainId;
        private GameObject _pendingTriggerTarget;
        private CircuitEvent? _pendingEchoOrigin;
        private bool _threefoldReady;
        private bool _carriedEmberReady;
        private bool _savedLineReady;
        private int _sparksGenerated;
        private int _completedCircuits;
        private CircuitEvent? _storedCapstoneOrigin;
        private int _stagedHeat;
        private bool _stagedSpark;
        private bool _stagedEcho;
        private bool _usingActionStaging;
        private GameObject _stagedTarget;
        private CircuitEntityRef _stagedTargetRef;
        private float _stagedSparkExpiresAt;
        private float _stagedEchoExpiresAt;

        public FirstSpiritCircuitRuntime Runtime => _runtime;
        public SpiritLoadoutRuntime Loadout { get; private set; }
        public FirstSpiritCircuitStep LastStep { get; private set; }
        public int AcceptedHitCount { get; private set; }
        public int SpawnedProjectileCount { get; private set; }
        public int ResolvedCircuitHits { get; private set; }

        private void Awake()
        {
            EnsureRuntime();
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.PlayerDamageResolved>(
                OnPlayerDamageResolved);
            GameEvents.Subscribe<GameEvents.MeleeHitConnected>(
                OnMeleeHitConnected);
            GameEvents.Subscribe<GameEvents.SkillHitConnected>(
                OnSkillHitConnected);
            GameEvents.Subscribe<GameEvents.DodgeFinished>(
                OnDodgeFinished);
            GameEvents.Subscribe<GameEvents.SpiritTalentActivated>(
                OnSpiritTalentActivated);
            GameEvents.Subscribe<GameEvents.SpiritTalentsReset>(
                OnSpiritTalentsReset);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.PlayerDamageResolved>(
                OnPlayerDamageResolved);
            GameEvents.Unsubscribe<GameEvents.MeleeHitConnected>(
                OnMeleeHitConnected);
            GameEvents.Unsubscribe<GameEvents.SkillHitConnected>(
                OnSkillHitConnected);
            GameEvents.Unsubscribe<GameEvents.DodgeFinished>(
                OnDodgeFinished);
            GameEvents.Unsubscribe<GameEvents.SpiritTalentActivated>(
                OnSpiritTalentActivated);
            GameEvents.Unsubscribe<GameEvents.SpiritTalentsReset>(
                OnSpiritTalentsReset);
        }

        private void Update()
        {
            if (!_usingActionStaging)
                return;
            if (ExpireStagedProducts())
                PublishStagedState(false, 0);
        }

        public void Record(in CircuitEvent circuitEvent)
        {
            if (circuitEvent.Kind == CircuitEventKind.Response &&
                (circuitEvent.TagMask &
                 FirstSpiritCircuitContent.EchoAttackTag) != 0)
            {
                _pendingEchoOrigin = circuitEvent;
                return;
            }

            if (circuitEvent.Kind == CircuitEventKind.Transform &&
                (circuitEvent.TagMask &
                 FirstSpiritCircuitContent.BounceAttackTag) != 0 &&
                _pendingTriggerTarget != null &&
                _pendingEchoOrigin.HasValue)
            {
                SpawnBounceProjectile(
                    _pendingTriggerTarget,
                    _pendingEchoOrigin.Value,
                    circuitEvent);
                _pendingEchoOrigin = null;
            }
        }

        private void OnPlayerDamageResolved(
            GameEvents.PlayerDamageResolved evt)
        {
            RecordResolvedPlayerDamage(evt);
        }

        public void RecordResolvedPlayerDamage(
            in GameEvents.PlayerDamageResolved evt)
        {
            if (!FeatureFlags.EnableCircuitRuntime ||
                !evt.IsPlayerOwnedDamage ||
                evt.Target == null ||
                evt.Target.GetComponent<Destructible>() != null ||
                evt.AppliedAmount <= 0f)
            {
                return;
            }

            BeginResolvedFrame();
            _resolvedTargets[evt.Target.GetInstanceID()] = evt.TargetRef;
        }

        private void OnMeleeHitConnected(GameEvents.MeleeHitConnected evt)
        {
            RecordCarrierAction(evt.Target, CarrierSlot.Weapon);
        }

        private void OnSkillHitConnected(GameEvents.SkillHitConnected evt)
        {
            if (evt.SlotIndex < 0 || evt.SlotIndex > 2)
                return;
            RecordCarrierAction(
                evt.Target,
                evt.SlotIndex switch
                {
                    0 => CarrierSlot.TechniqueQ,
                    1 => CarrierSlot.TechniqueE,
                    _ => CarrierSlot.TechniqueR
                });
        }

        private void OnDodgeFinished(GameEvents.DodgeFinished evt)
        {
            RecordCarrierAction(null, CarrierSlot.Mobility);
        }

        public void RecordCarrierHit(GameObject target)
        {
            if (!FeatureFlags.EnableCircuitRuntime ||
                target == null ||
                _resolvedFrame != Time.frameCount ||
                !_resolvedTargets.Remove(
                    target.GetInstanceID(),
                    out CircuitEntityRef targetRef))
            {
                return;
            }

            EnsureRuntime();
            if (_runtime == null)
                return;
            ExecuteCircuit(target, targetRef, false);
        }

        public void RecordCarrierAction(
            GameObject target,
            CarrierSlot action)
        {
            if (!FeatureFlags.EnableCircuitRuntime)
                return;

            EnsureRuntime();
            if (_runtime == null || Loadout == null)
                return;

            CircuitEntityRef targetRef = default;
            if (target != null)
            {
                if (_resolvedFrame != Time.frameCount ||
                    !_resolvedTargets.Remove(
                        target.GetInstanceID(),
                        out targetRef))
                {
                    return;
                }
                AcceptedHitCount++;
            }
            else
            {
                target = FindNearestTarget(
                    transform.position,
                    null,
                    null);
                if (target != null)
                {
                    targetRef =
                        LegacyCombatResultRecorder.BuildTarget(target);
                }
            }

            AdvanceAttachedAction(action, target, targetRef);
        }

        private void AdvanceAttachedAction(
            CarrierSlot action,
            GameObject target,
            in CircuitEntityRef targetRef)
        {
            ExpireStagedProducts();
            if (!TryCarrierForSpecies(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    out CarrierSlot sparkCarrier) ||
                !TryCarrierForSpecies(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    out CarrierSlot echoCarrier) ||
                !TryCarrierForSpecies(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    out CarrierSlot gelCarrier))
            {
                return;
            }

            _usingActionStaging = true;
            int threshold = _runtime.Tuning.DirectHitsPerSpark;
            if (action == sparkCarrier)
            {
                _stagedHeat++;
                if (_stagedHeat >= threshold)
                {
                    _stagedHeat =
                        _runtime.Tuning.RetainedHeatOnSpark;
                    _stagedSpark = true;
                    _stagedSparkExpiresAt =
                        Time.time + SparkPendingDuration;
                }
            }
            if (action == echoCarrier && _stagedSpark)
            {
                _stagedSpark = false;
                _stagedSparkExpiresAt = 0f;
                _stagedEcho = true;
                _stagedEchoExpiresAt =
                    Time.time + EchoPendingDuration;
                if (target != null)
                {
                    _stagedTarget = target;
                    _stagedTargetRef = targetRef;
                }
            }
            if (action == gelCarrier && _stagedEcho)
            {
                GameObject resolvedTarget =
                    target != null ? target : _stagedTarget;
                CircuitEntityRef resolvedRef =
                    target != null ? targetRef : _stagedTargetRef;
                if (resolvedTarget != null)
                {
                    _stagedEcho = false;
                    _stagedEchoExpiresAt = 0f;
                    _stagedTarget = null;
                    _runtime.ResetHeat();
                    ExecuteCircuit(resolvedTarget, resolvedRef, true);
                    return;
                }
            }

            PublishStagedState(false, 0);
        }

        private bool TryCarrierForSpecies(
            StableConfigId species,
            out CarrierSlot carrier)
        {
            foreach (SpiritInstanceState spirit in Loadout.ActiveSpirits)
            {
                if (spirit.Identity.SpeciesConfigId == species)
                {
                    return Loadout.TryGetCarrier(
                        spirit.Identity.InstanceId,
                        out carrier);
                }
            }
            carrier = default;
            return false;
        }

        private bool ExpireStagedProducts()
        {
            bool changed = false;
            if (_stagedSpark &&
                Time.time >= _stagedSparkExpiresAt)
            {
                _stagedSpark = false;
                _stagedSparkExpiresAt = 0f;
                changed = true;
            }
            if (_stagedEcho &&
                Time.time >= _stagedEchoExpiresAt)
            {
                _stagedEcho = false;
                _stagedEchoExpiresAt = 0f;
                _stagedTarget = null;
                _stagedTargetRef = default;
                changed = true;
            }
            return changed;
        }

        private void PublishStagedState(
            bool chainStarted,
            int emittedEvents)
        {
            SpiritCircuitPendingStage stage =
                _stagedEcho
                    ? SpiritCircuitPendingStage.AwaitingGel
                    : _stagedSpark
                        ? SpiritCircuitPendingStage.AwaitingEcho
                        : SpiritCircuitPendingStage.Heating;
            StableConfigId nextSpecies = stage switch
            {
                SpiritCircuitPendingStage.AwaitingEcho =>
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                SpiritCircuitPendingStage.AwaitingGel =>
                    FirstSpiritCircuitContent.BounceGelSpecies,
                _ => FirstSpiritCircuitContent.SparkRaccoonSpecies
            };
            TryCarrierForSpecies(
                nextSpecies,
                out CarrierSlot nextCarrier);
            float remaining = stage switch
            {
                SpiritCircuitPendingStage.AwaitingEcho =>
                    Mathf.Max(0f, _stagedSparkExpiresAt - Time.time),
                SpiritCircuitPendingStage.AwaitingGel =>
                    Mathf.Max(0f, _stagedEchoExpiresAt - Time.time),
                _ => 0f
            };
            GameEvents.Publish(new GameEvents.FirstPetCircuitAdvanced
            {
                ChainStarted = chainStarted,
                Heat = _stagedHeat,
                HeatRequired = _runtime.Tuning.DirectHitsPerSpark,
                EmittedEvents = emittedEvents,
                CausalChainId = _nextChainId,
                PendingStage = stage,
                NextCarrier = nextCarrier,
                RemainingSeconds = remaining
            });
        }

        private void ExecuteCircuit(
            GameObject target,
            in CircuitEntityRef targetRef,
            bool readyChain)
        {

            if (_runtimeFrame != Time.frameCount)
            {
                _runtime.BeginFrame();
                _runtimeFrame = Time.frameCount;
            }

            if (!readyChain)
                AcceptedHitCount++;
            _pendingTriggerTarget = target;
            _pendingEchoOrigin = null;
            bool releaseCarriedEmber =
                _carriedEmberReady && _storedCapstoneOrigin.HasValue;
            bool releaseSavedLine =
                _savedLineReady && _storedCapstoneOrigin.HasValue;
            CircuitEvent storedOrigin =
                _storedCapstoneOrigin.GetValueOrDefault();
            _carriedEmberReady = false;
            _savedLineReady = false;
            try
            {
                ulong chainId = ++_nextChainId;
                LastStep = readyChain
                    ? _runtime.ExecuteReadyChain(
                        chainId,
                        Time.time,
                        BuildPlayerRef(targetRef))
                    : _runtime.RegisterDirectHit(
                        chainId,
                        Time.time,
                        BuildPlayerRef(targetRef));
            }
            finally
            {
                _pendingTriggerTarget = null;
                _pendingEchoOrigin = null;
            }
            Vector3 targetPoint =
                target.transform.position + Vector3.up * 0.6f;
            if (releaseCarriedEmber)
            {
                ApplyAdditionalDamage(
                    target,
                    targetPoint,
                    0.25f,
                    FirstSpiritCircuitContent.CarriedEmberDamageMetric,
                    storedOrigin);
            }
            if (releaseSavedLine)
            {
                ApplyAdditionalDamage(
                    target,
                    targetPoint,
                    0.35f,
                    FirstSpiritCircuitContent.SavedLineDamageMetric,
                    storedOrigin);
            }
            if (readyChain)
            {
                PublishStagedState(
                    LastStep.ChainStarted,
                    LastStep.Report.EmittedEvents);
            }
            else
            {
                GameEvents.Publish(
                    new GameEvents.FirstPetCircuitAdvanced
                    {
                        ChainStarted = LastStep.ChainStarted,
                        Heat = LastStep.Heat,
                        HeatRequired =
                            _runtime.Tuning.DirectHitsPerSpark,
                        EmittedEvents =
                            LastStep.Report.EmittedEvents,
                        CausalChainId = _nextChainId,
                        PendingStage =
                            SpiritCircuitPendingStage.Heating
                    });
            }
        }

        private void SpawnBounceProjectile(
            GameObject triggerTarget,
            in CircuitEvent echoOrigin,
            in CircuitEvent bounceOrigin)
        {
            GameObject primary = FindNearestTarget(
                transform.position,
                null,
                triggerTarget);
            if (primary == null)
                return;

            FirstSpiritCircuitTuning tuning = _runtime.Tuning;
            int maxTargets = tuning.BurstCore || tuning.OnlyThePoint
                ? 1
                : tuning.Pinball
                    ? 5
                    : tuning.AcceleratingBounce
                        ? 4
                        : tuning.MaxProjectileTargets;
            List<GameObject> targets =
                BuildTargetSequence(primary, maxTargets);
            GameObject secondary =
                targets.Count > 1 ? targets[1] : null;
            GameObject projectileObject =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "FirstPetEchoProjectile";
            projectileObject.transform.position =
                transform.position + Vector3.up * 0.9f;
            projectileObject.transform.localScale =
                Vector3.one * _runtime.Tuning.ProjectileScale;
            Collider collider = projectileObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            TintProjectile(projectileObject);

            FirstSpiritBounceProjectile projectile =
                projectileObject.AddComponent<
                    FirstSpiritBounceProjectile>();
            CircuitEvent echoEvent = echoOrigin;
            CircuitEvent bounceEvent = bounceOrigin;
            float chainEchoMultiplier = _threefoldReady ? 1.35f : 1f;
            _threefoldReady = false;
            if (tuning.OnlyThePoint)
                chainEchoMultiplier *= 1.35f;
            bool triggerChainBurn =
                tuning.ChainBurn && ++_sparksGenerated % 2 == 0;
            projectile.Initialize(
                targets,
                tuning.ProjectileSpeed,
                4f,
                tuning.BounceDelay,
                tuning.InitialEchoDelay,
                tuning.AcceleratingBounce ? 1.2f : 1f,
                (target, hitPoint, hitIndex) =>
                    ResolveProjectileHit(
                        projectileObject,
                        target,
                        hitPoint,
                        hitIndex,
                        secondary != null,
                        chainEchoMultiplier,
                        triggerChainBurn,
                        echoEvent,
                        bounceEvent));
            SpawnedProjectileCount++;
        }

        private GameObject FindNearestTarget(
            Vector3 origin,
            GameObject excluded,
            GameObject fallback,
            GameObject excludedSecond = null,
            ISet<GameObject> excludedTargets = null)
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            LayerMask mask = combat != null
                ? combat.EnemyLayer
                : Physics.AllLayers;
            Collider[] hits = Physics.OverlapSphere(
                origin,
                _runtime?.Tuning.TargetSearchRadius ??
                FirstSpiritCircuitContent.TargetSearchRadius,
                mask);
            GameObject nearest = null;
            float nearestSqr = float.MaxValue;
            GameObject marked = null;
            float markedSqr = float.MaxValue;
            foreach (Collider hit in hits)
            {
                IDamageable damageable =
                    hit.GetComponentInParent<IDamageable>();
                if (damageable == null ||
                    damageable is Destructible ||
                    damageable is Component component &&
                    (component.gameObject == excluded ||
                     component.gameObject == excludedSecond ||
                     excludedTargets != null &&
                     excludedTargets.Contains(component.gameObject) ||
                     component.gameObject == gameObject) ||
                    damageable.Stats != null &&
                    !damageable.Stats.IsAlive)
                {
                    continue;
                }

                GameObject candidate =
                    damageable is Component targetComponent
                        ? targetComponent.gameObject
                        : hit.gameObject;
                float sqr =
                    (candidate.transform.position - origin).sqrMagnitude;
                if (_echoMarks.TryGetValue(
                        candidate,
                        out float markedUntil) &&
                    markedUntil > Time.time &&
                    sqr < markedSqr)
                {
                    marked = candidate;
                    markedSqr = sqr;
                }
                if (sqr < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = sqr;
                }
            }

            if (marked != null)
                return marked;
            if (nearest != null)
                return nearest;
            return IsValidTarget(
                fallback,
                excluded,
                excludedSecond)
                ? fallback
                : null;
        }

        private List<GameObject> BuildTargetSequence(
            GameObject primary,
            int maxTargets)
        {
            var targets = new List<GameObject> { primary };
            var excluded = new HashSet<GameObject> { primary };
            GameObject current = primary;
            while (targets.Count < maxTargets)
            {
                GameObject next = FindNearestTarget(
                    current.transform.position,
                    null,
                    null,
                    null,
                    excluded);
                if (next == null)
                    break;
                targets.Add(next);
                excluded.Add(next);
                current = next;
            }
            return targets;
        }

        private void ResolveProjectileHit(
            GameObject projectile,
            GameObject target,
            Vector3 hitPoint,
            int hitIndex,
            bool hadBounceTarget,
            float chainEchoMultiplier,
            bool triggerChainBurn,
            in CircuitEvent echoOrigin,
            in CircuitEvent bounceOrigin)
        {
            IDamageable damageable = target?.GetComponent<IDamageable>();
            if (damageable == null)
                return;

            CombatStats targetStats = damageable.Stats;
            float attackDamage =
                GetComponent<PlayerController>()?.Stats.attackDamage ?? 10f;
            float multiplier = hitIndex == 0
                ? _runtime.Tuning.EchoDamageMultiplier *
                  chainEchoMultiplier
                : _runtime.Tuning.EchoDamageMultiplier *
                  CalculateBounceDecay(hitIndex);
            float defense = targetStats != null
                ? targetStats.defense
                : 0f;
            float requestedDamage = Mathf.Max(
                1f,
                attackDamage * multiplier - defense);
            float healthBefore = targetStats != null
                ? Mathf.Max(0f, targetStats.currentHp)
                : requestedDamage;
            float expectedApplied = Mathf.Min(
                requestedDamage,
                healthBefore);
            CircuitEntityRef targetRef =
                LegacyCombatResultRecorder.BuildTarget(target);
            StableConfigId metricId = hitIndex == 0
                ? FirstSpiritCircuitContent.EchoDamageMetric
                : FirstSpiritCircuitContent.BounceDamageMetric;
            CircuitEvent origin = hitIndex == 0
                ? echoOrigin
                : bounceOrigin;

            RunCombatStats.RecordLegacyDamageBaseline(
                metricId,
                requestedDamage,
                expectedApplied,
                targetRef);
            damageable.OnDamage(
                requestedDamage,
                hitPoint,
                projectile);
            float appliedDamage = targetStats != null
                ? Mathf.Max(
                    0f,
                    healthBefore -
                    Mathf.Max(0f, targetStats.currentHp))
                : requestedDamage;
            RunCombatStats.RecordCircuitDamage(
                metricId,
                requestedDamage,
                appliedDamage,
                targetRef,
                origin);
            ResolvedCircuitHits++;

            if (hitIndex == 0)
            {
                if (_runtime.RegisterEchoResolved(echoOrigin))
                    PublishCurrentHeat(echoOrigin.CausalChainId);
                ResolvePrimaryTalentEffects(
                    target,
                    hitPoint,
                    hadBounceTarget,
                    triggerChainBurn,
                    echoOrigin);
            }
            else
            {
                ResolveBounceTalentEffects(
                    target,
                    hitPoint,
                    hitIndex,
                    bounceOrigin);
            }

            if (hitIndex == 1 &&
                _runtime.RegisterBounceResolved(bounceOrigin))
            {
                if (_runtime.Tuning.ThreefoldConfirm)
                    _threefoldReady = true;
                _storedCapstoneOrigin = bounceOrigin;
                if (_runtime.Tuning.CarriedEmber)
                    _carriedEmberReady = true;
                if (_runtime.Tuning.SavedLine &&
                    ++_completedCircuits % 2 == 0)
                {
                    _savedLineReady = true;
                }
                PublishCurrentHeat(bounceOrigin.CausalChainId);
            }
        }

        private float CalculateBounceDecay(int hitIndex)
        {
            float result = 1f;
            float baseDecay = _runtime.Tuning.BounceDamageMultiplier;
            for (int i = 1; i <= hitIndex; i++)
            {
                float stepDecay = _runtime.Tuning.AcceleratingBounce
                    ? Mathf.Min(0.95f, baseDecay + (i - 1) * 0.08f)
                    : baseDecay;
                result *= stepDecay;
            }
            return result;
        }

        private void PublishCurrentHeat(ulong causalChainId)
        {
            if (_usingActionStaging)
            {
                _stagedHeat = Mathf.Max(
                    _stagedHeat,
                    _runtime.Heat);
                PublishStagedState(true, 0);
                return;
            }
            GameEvents.Publish(
                new GameEvents.FirstPetCircuitAdvanced
                {
                    ChainStarted = true,
                    Heat = _usingActionStaging
                        ? _stagedHeat
                        : _runtime.Heat,
                    EmittedEvents = 0,
                    CausalChainId = causalChainId
                });
        }

        private void ResolvePrimaryTalentEffects(
            GameObject target,
            Vector3 hitPoint,
            bool hadBounceTarget,
            bool triggerChainBurn,
            in CircuitEvent echoOrigin)
        {
            FirstSpiritCircuitTuning tuning = _runtime.Tuning;
            if (tuning.EchoMarkDuration > 0f)
                _echoMarks[target] = Time.time + tuning.EchoMarkDuration;

            if (tuning.SparkShardDamageMultiplier > 0f)
            {
                ResolveAreaTalentDamage(
                    target,
                    hitPoint,
                    2.5f,
                    tuning.SparkShardDamageMultiplier,
                    FirstSpiritCircuitContent.SparkShardDamageMetric,
                    echoOrigin);
            }
            if (tuning.DuetDamageMultiplier > 0f)
            {
                StartCoroutine(ResolveDelayedEcho(
                    target,
                    echoOrigin,
                    tuning.DuetDamageMultiplier));
            }
            if (!hadBounceTarget &&
                tuning.SoftLandingDamageMultiplier > 0f)
            {
                ApplyAdditionalDamage(
                    target,
                    hitPoint,
                    tuning.SoftLandingDamageMultiplier,
                    FirstSpiritCircuitContent.SoftLandingDamageMetric,
                    echoOrigin);
            }
            if (!hadBounceTarget &&
                tuning.ReturnStartDamageMultiplier > 0f)
            {
                StartCoroutine(ResolveReturnStart(
                    target,
                    echoOrigin,
                    tuning.ReturnStartDamageMultiplier));
            }
            if (triggerChainBurn)
            {
                StartCoroutine(ResolveCapstoneEcho(
                    target,
                    echoOrigin,
                    0.5f,
                    FirstSpiritCircuitContent.ChainBurnDamageMetric));
            }
            if (tuning.BurstCore)
            {
                ResolveAreaTalentDamage(
                    null,
                    hitPoint,
                    3.5f,
                    0.35f,
                    FirstSpiritCircuitContent.BurstCoreDamageMetric,
                    echoOrigin);
            }
            if (tuning.ValleyChorus)
            {
                ResolveAreaTalentDamage(
                    target,
                    hitPoint,
                    5f,
                    0.3f,
                    FirstSpiritCircuitContent.ValleyChorusDamageMetric,
                    echoOrigin,
                    2);
            }
        }

        private void ResolveBounceTalentEffects(
            GameObject target,
            Vector3 hitPoint,
            int hitIndex,
            in CircuitEvent bounceOrigin)
        {
            FirstSpiritCircuitTuning tuning = _runtime.Tuning;
            if (tuning.FireTrailDamageMultiplier > 0f)
            {
                ResolveAreaTalentDamage(
                    null,
                    hitPoint,
                    1.8f,
                    tuning.FireTrailDamageMultiplier,
                    FirstSpiritCircuitContent.FireTrailDamageMetric,
                    bounceOrigin);
            }
            if (tuning.WrappedSparkDamageMultiplier > 0f)
            {
                ResolveAreaTalentDamage(
                    null,
                    hitPoint,
                    2.2f,
                    tuning.WrappedSparkDamageMultiplier,
                    FirstSpiritCircuitContent.WrappedSparkDamageMetric,
                    bounceOrigin);
            }
            if (hitIndex == 1 && tuning.BigCrater)
            {
                ResolveAreaTalentDamage(
                    null,
                    hitPoint,
                    3.5f,
                    0.35f,
                    FirstSpiritCircuitContent.BigCraterDamageMetric,
                    bounceOrigin);
            }
        }

        private IEnumerator ResolveCapstoneEcho(
            GameObject preferredTarget,
            CircuitEvent origin,
            float multiplier,
            StableConfigId metricId)
        {
            yield return new WaitForSeconds(0.16f);
            GameObject target = IsValidTarget(
                    preferredTarget,
                    null,
                    null)
                ? preferredTarget
                : FindNearestTarget(
                    transform.position,
                    null,
                    null);
            if (target != null)
            {
                ApplyAdditionalDamage(
                    target,
                    target.transform.position + Vector3.up * 0.6f,
                    multiplier,
                    metricId,
                    origin);
            }
        }

        private IEnumerator ResolveDelayedEcho(
            GameObject preferredTarget,
            CircuitEvent origin,
            float multiplier)
        {
            yield return new WaitForSeconds(0.2f);
            GameObject target = IsValidTarget(
                    preferredTarget,
                    null,
                    null)
                ? preferredTarget
                : FindNearestTarget(
                    transform.position,
                    null,
                    null);
            if (target != null)
            {
                ApplyAdditionalDamage(
                    target,
                    target.transform.position + Vector3.up * 0.6f,
                    multiplier,
                    FirstSpiritCircuitContent.DuetDamageMetric,
                    origin);
                if (_runtime.Tuning.EchoJump)
                {
                    GameObject bounceTarget = FindNearestTarget(
                        target.transform.position,
                        target,
                        null);
                    if (bounceTarget != null)
                    {
                        ApplyAdditionalDamage(
                            bounceTarget,
                            bounceTarget.transform.position +
                            Vector3.up * 0.6f,
                            multiplier *
                            _runtime.Tuning.BounceDamageMultiplier,
                            FirstSpiritCircuitContent.BounceDamageMetric,
                            origin);
                    }
                }
            }
        }

        private IEnumerator ResolveReturnStart(
            GameObject target,
            CircuitEvent origin,
            float multiplier)
        {
            yield return new WaitForSeconds(0.2f);
            if (!IsValidTarget(target, null, null))
                yield break;

            ApplyAdditionalDamage(
                target,
                target.transform.position + Vector3.up * 0.6f,
                multiplier,
                FirstSpiritCircuitContent.ReturnStartDamageMetric,
                origin);
        }

        private void ResolveAreaTalentDamage(
            GameObject centerTarget,
            Vector3 hitPoint,
            float radius,
            float multiplier,
            StableConfigId metricId,
            in CircuitEvent origin,
            int maxTargets = int.MaxValue)
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            LayerMask mask = combat != null
                ? combat.EnemyLayer
                : Physics.AllLayers;
            Collider[] hits = Physics.OverlapSphere(
                hitPoint,
                radius,
                mask);
            var resolved = new HashSet<GameObject>();
            int appliedTargets = 0;
            foreach (Collider hit in hits)
            {
                IDamageable damageable =
                    hit.GetComponentInParent<IDamageable>();
                if (damageable is not Component component ||
                    component.gameObject == centerTarget ||
                    !resolved.Add(component.gameObject) ||
                    !IsValidTarget(component.gameObject, null, null))
                {
                    continue;
                }
                ApplyAdditionalDamage(
                    component.gameObject,
                    hitPoint,
                    multiplier,
                    metricId,
                    origin);
                appliedTargets++;
                if (appliedTargets >= maxTargets)
                    break;
            }
        }

        private void ApplyAdditionalDamage(
            GameObject target,
            Vector3 hitPoint,
            float multiplier,
            StableConfigId metricId,
            in CircuitEvent origin)
        {
            IDamageable damageable = target?.GetComponent<IDamageable>();
            if (damageable == null)
                return;

            CombatStats targetStats = damageable.Stats;
            float attackDamage =
                GetComponent<PlayerController>()?.Stats.attackDamage ?? 10f;
            float defense = targetStats != null ? targetStats.defense : 0f;
            float requestedDamage = Mathf.Max(
                1f,
                attackDamage * multiplier - defense);
            float healthBefore = targetStats != null
                ? Mathf.Max(0f, targetStats.currentHp)
                : requestedDamage;
            float expectedApplied = Mathf.Min(
                requestedDamage,
                healthBefore);
            CircuitEntityRef targetRef =
                LegacyCombatResultRecorder.BuildTarget(target);
            RunCombatStats.RecordLegacyDamageBaseline(
                metricId,
                requestedDamage,
                expectedApplied,
                targetRef);
            damageable.OnDamage(requestedDamage, hitPoint, gameObject);
            float appliedDamage = targetStats != null
                ? Mathf.Max(
                    0f,
                    healthBefore - Mathf.Max(0f, targetStats.currentHp))
                : requestedDamage;
            RunCombatStats.RecordCircuitDamage(
                metricId,
                requestedDamage,
                appliedDamage,
                targetRef,
                origin);
            ResolvedCircuitHits++;
        }

        private static bool IsValidTarget(
            GameObject target,
            GameObject excluded,
            GameObject excludedSecond = null)
        {
            if (target == null ||
                target == excluded ||
                target == excludedSecond)
                return false;

            IDamageable damageable = target.GetComponent<IDamageable>();
            return damageable != null &&
                damageable is not Destructible &&
                (damageable.Stats == null ||
                 damageable.Stats.IsAlive);
        }

        private static void TintProjectile(GameObject projectile)
        {
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer == null)
                return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var color = new Color(1f, 0.55f, 0.1f, 1f);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private void BeginResolvedFrame()
        {
            if (_resolvedFrame == Time.frameCount)
                return;

            _resolvedTargets.Clear();
            _resolvedFrame = Time.frameCount;
        }

        private void EnsureRuntime()
        {
            if (_runtime != null || !FeatureFlags.EnableCircuitRuntime)
                return;

            TryConfigure(SaveSystem.Instance.Data);
        }

        public bool TryConfigure(SaveDataV1 save)
        {
            if (_runtime != null)
                return true;
            if (!FeatureFlags.EnableCircuitRuntime ||
                !FirstSpiritCircuitLoadoutProvider.TryRestore(
                    save,
                    out FirstSpiritCircuitLoadoutBinding binding))
            {
                return false;
            }

            StableConfigId ownerConfigId =
                new("runtime.player.first-pet-prototype");
            if (!binding.Loadout.TryGetCarrier(
                binding.SparkRaccoon.Identity.InstanceId,
                out CarrierSlot sparkCarrier) ||
                !binding.Loadout.TryGetCarrier(
                binding.EchoOwl.Identity.InstanceId,
                out CarrierSlot echoCarrier) ||
                !binding.Loadout.TryGetCarrier(
                binding.BounceGel.Identity.InstanceId,
                out CarrierSlot bounceCarrier))
            {
                return false;
            }
            Loadout = binding.Loadout;
            _runtime = new FirstSpiritCircuitRuntime(
                binding.SparkRaccoon,
                sparkCarrier,
                binding.EchoOwl,
                echoCarrier,
                binding.BounceGel,
                bounceCarrier,
                gameObject.GetInstanceID(),
                ownerConfigId,
                sink: this,
                tuning: BuildTuning());
            return true;
        }

        public SpiritLoadoutChangeResult TryMigrateAttachment(
            System.Guid spiritInstanceId,
            CarrierSlot carrier)
        {
            return TryMigrateAttachment(
                spiritInstanceId,
                carrier,
                HasNearbyEnemy());
        }

        public SpiritLoadoutChangeResult TryMigrateAttachment(
            System.Guid spiritInstanceId,
            CarrierSlot carrier,
            bool isInCombat)
        {
            if (Loadout == null)
                return SpiritLoadoutChangeResult.InvalidSpirit;

            SpiritLoadoutChangeResult result = Loadout.Migrate(
                spiritInstanceId,
                carrier,
                isInCombat);
            if (result != SpiritLoadoutChangeResult.Success)
                return result;
            if (!RebuildRuntimeFromLoadout())
                return SpiritLoadoutChangeResult.InvalidSpirit;

            _stagedHeat = 0;
            _stagedSpark = false;
            _stagedEcho = false;
            _stagedTarget = null;
            _stagedTargetRef = default;
            _stagedSparkExpiresAt = 0f;
            _stagedEchoExpiresAt = 0f;
            if (Loadout.TryGetSpirit(
                    spiritInstanceId,
                    out SpiritInstanceState spirit))
            {
                GameEvents.Publish(new GameEvents.SpiritLoadoutChanged
                {
                    SpiritInstanceId = spiritInstanceId,
                    SpeciesId = spirit.Identity.SpeciesConfigId,
                    Carrier = carrier,
                    Pattern = Loadout.Pattern
                });
            }
            return result;
        }

        private bool RebuildRuntimeFromLoadout()
        {
            SpiritInstanceState spark = null;
            SpiritInstanceState echo = null;
            SpiritInstanceState gel = null;
            foreach (SpiritInstanceState spirit in Loadout.ActiveSpirits)
            {
                if (spirit.Identity.SpeciesConfigId ==
                    FirstSpiritCircuitContent.SparkRaccoonSpecies)
                {
                    spark = spirit;
                }
                else if (spirit.Identity.SpeciesConfigId ==
                         FirstSpiritCircuitContent.EchoOwlSpecies)
                {
                    echo = spirit;
                }
                else if (spirit.Identity.SpeciesConfigId ==
                         FirstSpiritCircuitContent.BounceGelSpecies)
                {
                    gel = spirit;
                }
            }
            if (spark == null || echo == null || gel == null ||
                !Loadout.TryGetCarrier(
                    spark.Identity.InstanceId,
                    out CarrierSlot sparkCarrier) ||
                !Loadout.TryGetCarrier(
                    echo.Identity.InstanceId,
                    out CarrierSlot echoCarrier) ||
                !Loadout.TryGetCarrier(
                    gel.Identity.InstanceId,
                    out CarrierSlot gelCarrier))
            {
                return false;
            }

            _runtime = new FirstSpiritCircuitRuntime(
                spark,
                sparkCarrier,
                echo,
                echoCarrier,
                gel,
                gelCarrier,
                gameObject.GetInstanceID(),
                new StableConfigId(
                    "runtime.player.first-pet-prototype"),
                sink: this,
                tuning: BuildTuning());
            return true;
        }

        private bool HasNearbyEnemy()
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            LayerMask mask =
                combat != null ? combat.EnemyLayer : Physics.AllLayers;
            foreach (Collider hit in Physics.OverlapSphere(
                         transform.position,
                         10f,
                         mask))
            {
                IDamageable damageable =
                    hit.GetComponentInParent<IDamageable>();
                if (damageable != null &&
                    damageable is not Destructible &&
                    (damageable.Stats == null ||
                     damageable.Stats.IsAlive))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnSpiritTalentActivated(
            GameEvents.SpiritTalentActivated evt)
        {
            if (_runtime != null)
                _runtime.ApplyTuning(BuildTuning());
        }

        private void OnSpiritTalentsReset(
            GameEvents.SpiritTalentsReset evt)
        {
            if (_runtime != null)
            {
                _runtime.ApplyTuning(BuildTuning());
                _threefoldReady = false;
                _carriedEmberReady = false;
                _savedLineReady = false;
                _storedCapstoneOrigin = null;
            }
        }

        private FirstSpiritCircuitTuning BuildTuning()
        {
            SpiritTalentRunController talents =
                GetComponent<SpiritTalentRunController>();
            bool SparkActive(string id) =>
                talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    id);
            bool EchoActive(string id) =>
                talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    id);
            bool GelActive(string id) =>
                talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    id);
            bool quickTemper = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.quick-temper");
            float echoMultiplier =
                FirstSpiritCircuitContent.EchoDamageMultiplier;
            if (quickTemper)
                echoMultiplier *= 0.85f;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    "talent.spark.bright-spark"))
            {
                echoMultiplier *= 1.2f;
            }
            if (SparkActive("talent.spark.compressed-flame"))
                echoMultiplier *= 1.15f;
            if (SparkActive("talent.spark.ember-cache"))
                echoMultiplier *= 1.1f;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    "talent.echo.clear-echo"))
            {
                echoMultiplier *= 1.15f;
            }
            if (EchoActive("talent.echo.sharp-tone"))
                echoMultiplier *= 1.1f;

            float bounceMultiplier =
                FirstSpiritCircuitContent.BounceDamageMultiplier;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    "talent.gel.more-elastic"))
            {
                bounceMultiplier *= 1.2f;
            }
            if (GelActive("talent.gel.heavy-drop"))
                bounceMultiplier *= 1.15f;
            if (GelActive("talent.gel.rolling-impact"))
                bounceMultiplier *= 1.1f;

            float radius = FirstSpiritCircuitContent.TargetSearchRadius;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    "talent.spark.chasing-fire"))
            {
                radius += 3f;
            }
            if (SparkActive("talent.spark.returning-flame"))
                radius += 1.5f;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    "talent.echo.long-hearing"))
            {
                radius += 3f;
            }
            if (EchoActive("talent.echo.wide-wave"))
                radius += 1.5f;
            if (EchoActive("talent.echo.second-memory"))
                radius += 1f;
            if (EchoActive("talent.echo.far-refrain"))
                radius += 1.5f;

            int heatRefund = 1;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    "talent.spark.banked-heat"))
            {
                heatRefund++;
            }
            if (SparkActive("talent.spark.warm-return"))
                heatRefund++;
            if (SparkActive("talent.spark.returning-flame"))
                heatRefund++;
            if (GelActive("talent.gel.elastic-return"))
                heatRefund++;
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    "talent.gel.kickback"))
            {
                heatRefund++;
            }
            if (talents != null && talents.IsActive(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    "talent.spark.bounce-back"))
            {
                heatRefund++;
            }

            float projectileSpeed =
                FirstSpiritCircuitContent.ProjectileSpeed;
            bool fireTrail = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.fire-trail");
            if (fireTrail)
            {
                projectileSpeed *= 1.25f;
            }
            if (SparkActive("talent.spark.swift-spark"))
                projectileSpeed *= 1.2f;
            bool rushingShards =
                SparkActive("talent.spark.rushing-shards");
            if (rushingShards)
                projectileSpeed *= 1.1f;
            if (GelActive("talent.gel.quick-rebound"))
                projectileSpeed *= 1.15f;
            if (GelActive("talent.gel.rolling-impact"))
                projectileSpeed *= 1.1f;

            bool sparkShards = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.spark-shards") || rushingShards;
            bool undyingCore = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.undying-core");
            bool echoFedHeat = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.echo-fed-heat");
            bool risingChorus =
                EchoActive("talent.echo.rising-chorus");
            bool rememberedDuet =
                EchoActive("talent.echo.remembered-duet");
            bool duet = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.duet") ||
                risingChorus ||
                rememberedDuet;
            bool halfBeatLate = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.half-beat-late");
            bool farRefrain =
                EchoActive("talent.echo.far-refrain");
            bool echoMark = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.echo-mark") ||
                risingChorus ||
                farRefrain;
            bool finishSpark = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.finish-spark");
            bool bounceChamber = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.bounce-chamber");
            bool threefoldConfirm = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.threefold-confirm");
            bool loopingHop =
                GelActive("talent.gel.looping-hop");
            bool thirdHop = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.third-hop") ||
                loopingHop;
            bool fatProjectile = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.fat-projectile");
            bool stickyCrater =
                GelActive("talent.gel.sticky-crater");
            bool softLanding = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.soft-landing") ||
                stickyCrater;
            bool stickThenBounce = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.stick-then-bounce");
            bool echoJump = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.echo-jump");
            bool wrapSpark = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.wrap-spark") ||
                stickyCrater;
            bool returnStart = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.return-start") ||
                loopingHop;
            bool chainBurn = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.chain-burn");
            bool burstCore = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.burst-core");
            bool carriedEmber = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.carried-ember");
            bool valleyChorus = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.valley-chorus");
            bool onlyThePoint = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.only-the-point");
            bool savedLine = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.saved-line");
            bool pinball = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.pinball");
            bool bigCrater = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.big-crater");
            bool acceleratingBounce = talents != null && talents.IsActive(
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.accelerating");
            if (bounceChamber)
                bounceMultiplier = Mathf.Max(bounceMultiplier, 0.85f);
            if (halfBeatLate)
                radius += 1.5f;
            if (fatProjectile)
                radius += 2f;

            return new FirstSpiritCircuitTuning(
                quickTemper ? 2 : 3,
                echoMultiplier,
                bounceMultiplier,
                radius,
                projectileSpeed,
                heatRefund,
                retainedHeatOnSpark:
                    undyingCore ||
                    SparkActive("talent.spark.ember-cache") ||
                    finishSpark ? 1 : 0,
                sparkShardDamageMultiplier: sparkShards
                    ? rushingShards ? 0.3f : 0.2f
                    : 0f,
                duetDamageMultiplier: duet
                    ? echoMultiplier *
                      (risingChorus
                          ? 0.65f
                          : rememberedDuet ? 0.55f : 0.5f)
                    : 0f,
                echoMarkDuration: echoMark
                    ? farRefrain ? 6f : 4f
                    : 0f,
                maxProjectileTargets: loopingHop
                    ? 4
                    : thirdHop ? 3 : 2,
                projectileScale: fatProjectile
                    ? 0.32f
                    : GelActive("talent.gel.heavy-drop")
                        ? 0.27f
                        : 0.22f,
                softLandingDamageMultiplier:
                    softLanding
                        ? stickyCrater ? 0.35f : 0.25f
                        : 0f,
                bounceDelay: stickThenBounce ? 0.2f : 0f,
                initialEchoDelay: halfBeatLate ? 0.2f : 0f,
                echoHitHeatRefund: echoFedHeat ? 1 : 0,
                fireTrailDamageMultiplier: fireTrail ? 0.15f : 0f,
                threefoldConfirm: threefoldConfirm,
                echoJump: echoJump,
                wrappedSparkDamageMultiplier:
                    wrapSpark
                        ? stickyCrater ? 0.18f : 0.12f
                        : 0f,
                returnStartDamageMultiplier:
                    returnStart
                        ? loopingHop ? 0.4f : 0.3f
                        : 0f,
                chainBurn: chainBurn,
                burstCore: burstCore,
                carriedEmber: carriedEmber,
                valleyChorus: valleyChorus,
                onlyThePoint: onlyThePoint,
                savedLine: savedLine,
                pinball: pinball,
                bigCrater: bigCrater,
                acceleratingBounce: acceleratingBounce);
        }

        private CircuitEntityRef BuildPlayerRef(
            in CircuitEntityRef fallback)
        {
            CircuitEntityRef playerRef =
                LegacyCombatResultRecorder.BuildPlayerTarget(gameObject);
            return playerRef.EntityId != 0 ? playerRef : fallback;
        }

    }
}
