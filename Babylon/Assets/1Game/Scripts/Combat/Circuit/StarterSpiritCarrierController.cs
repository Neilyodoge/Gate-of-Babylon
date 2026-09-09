using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace XianTu
{
    /// <summary>
    /// 火花狸单宠三载体的可玩原型。
    /// 数字键1/2/3仅作为Demo1脱战换挂入口，正式新手关改用附着UI。
    /// </summary>
    public sealed class StarterSpiritCarrierController : MonoBehaviour
    {
        public const string StarterTechniqueResourcePath =
            "Skills/StarterSpiritBolt";

        private static readonly StableConfigId WeaponMetric =
            new("combat.player.damage.spark-raccoon.weapon");
        private static readonly StableConfigId TechniqueMetric =
            new("combat.player.damage.spark-raccoon.technique");
        private static readonly StableConfigId MobilityMetric =
            new("combat.player.damage.spark-raccoon.mobility");
        private static readonly StableConfigId EchoWeaponMetric =
            new("combat.player.damage.echo-owl.weapon");
        private static readonly StableConfigId EchoTechniqueMetric =
            new("combat.player.damage.echo-owl.technique");
        private static readonly StableConfigId EchoMobilityMetric =
            new("combat.player.damage.echo-owl.mobility");
        private static readonly StableConfigId BounceWeaponMetric =
            new("combat.player.damage.bounce-gel.weapon");
        private static readonly StableConfigId BounceTechniqueMetric =
            new("combat.player.damage.bounce-gel.technique");
        private static readonly StableConfigId BounceMobilityMetric =
            new("combat.player.damage.bounce-gel.mobility");
        private static readonly StableConfigId SparkShardMetric =
            new("combat.player.damage.spark-raccoon.single.shard");
        private static readonly StableConfigId EchoDuetMetric =
            new("combat.player.damage.echo-owl.single.duet");
        private static readonly StableConfigId SparkTrailMetric =
            new("combat.player.damage.spark-raccoon.single.trail");
        private static readonly StableConfigId SparkCapstoneMetric =
            new("combat.player.damage.spark-raccoon.single.capstone");
        private static readonly StableConfigId EchoCapstoneMetric =
            new("combat.player.damage.echo-owl.single.capstone");
        private static readonly StableConfigId GelWrappedMetric =
            new("combat.player.damage.bounce-gel.single.wrapped");
        private static readonly StableConfigId GelCraterMetric =
            new("combat.player.damage.bounce-gel.single.crater");
        private static readonly StableConfigId PlayerConfig =
            new("runtime.player.starter-spirit");

        private readonly Dictionary<int, CircuitEntityRef>
            _resolvedTargets = new();
        private readonly HashSet<int> _burstTargets = new();

        private StarterSpiritCarrierRuntime _runtime;
        private SpiritInstanceState _spirit;
        private SaveDataV1 _configuredSave;
        private StarterSpiritSingleTalentTuning _talentTuning;
        private int _resolvedFrame = -1;
        private ulong _nextChainId;
        private int _markedEchoTargetId;
        private int _sparkActivations;
        private int _echoActivations;
        private bool _carriedEmberReady;

        public StarterSpiritCarrierRuntime Runtime => _runtime;
        public SpiritInstanceState Spirit => _spirit;
        public CarrierSlot Attachment =>
            _runtime?.Attachment ?? CarrierSlot.Weapon;
        public int ActivatedCount { get; private set; }

        private void Awake()
        {
            EnsureRuntime();
        }

        private void Start()
        {
            EnsureRuntime();
            EnsureStarterTechnique();
            if (_runtime != null)
                PublishAttachmentChanged();
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
            if (_runtime == null)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit1Key.wasPressedThisFrame)
                TryAttach(CarrierSlot.Weapon);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                TryAttach(CarrierSlot.TechniqueQ);
            else if (keyboard.digit3Key.wasPressedThisFrame)
                TryAttach(CarrierSlot.Mobility);
        }

        public bool TryConfigure(SaveDataV1 save)
        {
            if (_runtime != null)
            {
                if (ReferenceEquals(_configuredSave, save))
                    return true;
                _runtime = null;
                _spirit = null;
                _markedEchoTargetId = 0;
            }
            if (!FeatureFlags.EnableCircuitRuntime ||
                !StarterSpiritCarrierLoadoutProvider.TryRestoreSingle(
                    save,
                    out _spirit))
            {
                return false;
            }

            _configuredSave = save;
            CarrierSlot initialCarrier =
                StarterPrologueProgression.TryGetAttachment(
                    save,
                    out CarrierSlot restoredCarrier)
                    ? restoredCarrier
                    : CarrierSlot.Weapon;
            _runtime = new StarterSpiritCarrierRuntime(
                _spirit.Identity.SpeciesConfigId,
                initialCarrier);
            RefreshTalentTuning();
            EnsureStarterTechnique();
            PublishAttachmentChanged();
            return true;
        }

        public StarterSpiritAttachmentResult TryAttach(
            CarrierSlot carrier)
        {
            return TryAttach(carrier, HasNearbyEnemy());
        }

        public StarterSpiritAttachmentResult TryAttach(
            CarrierSlot carrier,
            bool isInCombat)
        {
            if (_runtime == null)
                return StarterSpiritAttachmentResult.UnsupportedCarrier;

            StarterSpiritAttachmentResult result =
                _runtime.TryAttach(carrier, isInCombat);
            if (result == StarterSpiritAttachmentResult.Success)
                PublishAttachmentChanged();
            if ((result == StarterSpiritAttachmentResult.Success ||
                 result == StarterSpiritAttachmentResult.NoChange) &&
                _configuredSave != null &&
                StarterPrologueProgression.GetStep(_configuredSave) <=
                StarterPrologueStep.AttachmentChosen)
            {
                StarterPrologueAdvanceResult progress =
                    StarterPrologueProgression.RecordAttachment(
                        _configuredSave,
                        carrier);
                if (progress ==
                        StarterPrologueAdvanceResult.Success &&
                    ReferenceEquals(
                        _configuredSave,
                        SaveSystem.Instance.Data) &&
                    SaveSystem.Instance.HasActiveSlot)
                {
                    SaveSystem.Instance.Save();
                }
            }
            return result;
        }

        public void RecordResolvedPlayerDamage(
            in GameEvents.PlayerDamageResolved evt)
        {
            if (_runtime == null ||
                !evt.IsPlayerOwnedDamage ||
                evt.Target == null ||
                evt.Target.GetComponent<Destructible>() != null ||
                evt.AppliedAmount <= 0f)
            {
                return;
            }

            if (_resolvedFrame != Time.frameCount)
            {
                _resolvedTargets.Clear();
                _resolvedFrame = Time.frameCount;
            }
            _resolvedTargets[evt.Target.GetInstanceID()] = evt.TargetRef;
        }

        public StarterSpiritCarrierStep RecordWeaponHit(
            GameObject target,
            Vector3 hitPoint)
        {
            if (!TryConsumeResolved(target))
                return default;

            RefreshTalentTuning();
            StarterSpiritCarrierStep step =
                _runtime.RegisterWeaponHit();
            Advance(step, hitPoint, target);
            return step;
        }

        public StarterSpiritCarrierStep RecordTechniqueHit(
            GameObject target,
            Vector3 hitPoint,
            int slotIndex)
        {
            if (slotIndex != 0 || !TryConsumeResolved(target))
                return default;

            RefreshTalentTuning();
            StarterSpiritCarrierStep step =
                _runtime.RegisterTechniqueHit(CarrierSlot.TechniqueQ);
            Advance(step, hitPoint, target);
            return step;
        }

        public StarterSpiritCarrierStep RecordMobilityFinished(
            Vector3 endPosition)
        {
            if (_runtime == null)
                return default;

            RefreshTalentTuning();
            StarterSpiritCarrierStep step =
                _runtime.RegisterMobilityFinished();
            Advance(step, endPosition, null);
            return step;
        }

        private void OnPlayerDamageResolved(
            GameEvents.PlayerDamageResolved evt)
        {
            RecordResolvedPlayerDamage(evt);
        }

        private void OnMeleeHitConnected(
            GameEvents.MeleeHitConnected evt)
        {
            if (Attachment == CarrierSlot.Weapon)
                RecordWeaponHit(evt.Target, evt.HitPoint);
        }

        private void OnSkillHitConnected(
            GameEvents.SkillHitConnected evt)
        {
            if (Attachment == CarrierSlot.TechniqueQ)
            {
                RecordTechniqueHit(
                    evt.Target,
                    evt.HitPoint,
                    evt.SlotIndex);
            }
        }

        private void OnDodgeFinished(GameEvents.DodgeFinished evt)
        {
            if (Attachment == CarrierSlot.Mobility)
                RecordMobilityFinished(evt.EndPosition);
        }

        private void OnSpiritTalentActivated(
            GameEvents.SpiritTalentActivated evt)
        {
            if (_spirit != null &&
                evt.SpiritInstanceId == _spirit.Identity.InstanceId)
            {
                RefreshTalentTuning();
            }
        }

        private void OnSpiritTalentsReset(
            GameEvents.SpiritTalentsReset evt)
        {
            if (_spirit != null &&
                evt.SpiritInstanceId == _spirit.Identity.InstanceId)
            {
                _markedEchoTargetId = 0;
                RefreshTalentTuning();
            }
        }

        public void RefreshTalentTuning()
        {
            if (_spirit == null || _runtime == null)
                return;

            SpiritTalentRunController talents =
                GetComponent<SpiritTalentRunController>();
            _talentTuning =
                StarterSpiritSingleTalentTuningBuilder.Build(
                    talents,
                    _spirit.Identity.SpeciesConfigId);
            _sparkActivations = 0;
            _echoActivations = 0;
            _carriedEmberReady = false;
            _runtime.ConfigureWeaponProgress(
                _talentTuning.WeaponHitsPerActivation,
                _talentTuning.RetainedWeaponProgress);
        }

        private void Advance(
            in StarterSpiritCarrierStep step,
            Vector3 position,
            GameObject triggerTarget)
        {
            if (_spirit == null)
                return;

            GameEvents.Publish(
                new GameEvents.StarterSpiritCarrierAdvanced
                {
                    SpiritInstanceId =
                        _spirit.Identity.InstanceId,
                    SpeciesId =
                        _spirit.Identity.SpeciesConfigId,
                    Carrier = step.Carrier,
                    Activated = step.Activated,
                    Progress = step.Progress
                });
            if (!step.Activated)
                return;

            ActivatedCount++;
            ApplySpeciesEffect(
                position,
                triggerTarget,
                step.Carrier);
        }

        private void ApplySpeciesEffect(
            Vector3 position,
            GameObject triggerTarget,
            CarrierSlot carrier)
        {
            StableConfigId species =
                _spirit.Identity.SpeciesConfigId;
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                StartCoroutine(ResolveEchoAfterDelay(
                    position,
                    carrier,
                    triggerTarget));
                return;
            }
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
            {
                if (carrier == CarrierSlot.Mobility)
                {
                    ApplyPulseDamage(
                        position,
                        carrier,
                        2.6f + _talentTuning.RadiusBonus,
                        0.30f * _talentTuning.DamageFactor,
                        ElementTag.Water);
                }
                else
                {
                    SpawnGelBounce(
                        position,
                        triggerTarget,
                        carrier);
                }
                return;
            }

            float radius = carrier switch
            {
                CarrierSlot.TechniqueQ => 2.8f,
                CarrierSlot.Mobility => 2.4f,
                _ => 2.2f
            };
            radius += _talentTuning.RadiusBonus;
            float multiplier = carrier switch
            {
                CarrierSlot.TechniqueQ => 0.45f,
                CarrierSlot.Mobility => 0.35f,
                _ => 0.30f
            };
            multiplier *= _talentTuning.DamageFactor;
            if (_talentTuning.SparkBurstCore)
            {
                radius += 1.2f;
                multiplier *= 1.35f;
            }
            if (_carriedEmberReady)
            {
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius + 0.4f,
                    0.18f,
                    ElementTag.Fire,
                    SparkCapstoneMetric);
                _carriedEmberReady = false;
            }
            ApplyPulseDamage(
                position,
                carrier,
                radius,
                multiplier,
                ElementTag.Fire);
            if (_talentTuning.SparkShards)
            {
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius + 0.6f,
                    0.20f,
                    ElementTag.Fire,
                    SparkShardMetric);
            }
            _sparkActivations++;
            if (_talentTuning.SparkChainBurn &&
                _sparkActivations % 2 == 0)
            {
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius,
                    multiplier * 0.45f,
                    ElementTag.Fire,
                    SparkCapstoneMetric);
            }
            if (_talentTuning.SparkFireTrail)
            {
                StartCoroutine(ResolveDelayedPulse(
                    position,
                    carrier,
                    radius * 0.85f,
                    multiplier * 0.35f,
                    0.24f,
                    ElementTag.Fire,
                    SparkTrailMetric));
            }
            if (_talentTuning.SparkCarriedEmber)
                _carriedEmberReady = true;
        }

        private IEnumerator ResolveEchoAfterDelay(
            Vector3 position,
            CarrierSlot carrier,
            GameObject triggerTarget)
        {
            FxFactory.SpawnElementBurst(
                position + Vector3.up * 0.05f,
                ElementTag.None,
                1.1f,
                0.28f);
            yield return new WaitForSeconds(_talentTuning.EchoDelay);
            if (_talentTuning.EchoRetarget &&
                (triggerTarget == null ||
                 triggerTarget.GetComponent<IDamageable>()?.Stats != null &&
                 !triggerTarget.GetComponent<IDamageable>().Stats.IsAlive))
            {
                triggerTarget = FindNearestTarget(position, null);
                if (triggerTarget != null)
                {
                    position =
                        triggerTarget.transform.position +
                        Vector3.up * 0.6f;
                }
            }
            float radius = carrier == CarrierSlot.TechniqueQ
                ? 2.5f
                : 2.1f;
            radius += _talentTuning.RadiusBonus;
            float multiplier = carrier == CarrierSlot.TechniqueQ
                ? 0.40f
                : 0.28f;
            multiplier *= _talentTuning.DamageFactor;
            if (_talentTuning.EchoOnlyPoint)
            {
                radius = 0.85f;
                multiplier *= 1.35f;
            }
            if (_talentTuning.EchoMark &&
                triggerTarget != null &&
                triggerTarget.GetInstanceID() == _markedEchoTargetId)
            {
                multiplier *= 1.2f;
            }
            ApplyPulseDamage(
                position,
                carrier,
                radius,
                multiplier,
                ElementTag.None);
            if (_talentTuning.EchoMark && triggerTarget != null)
                _markedEchoTargetId = triggerTarget.GetInstanceID();
            if (_talentTuning.EchoDuet)
            {
                yield return new WaitForSeconds(0.16f);
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius,
                    multiplier * 0.5f,
                    ElementTag.None,
                    EchoDuetMetric);
            }
            _echoActivations++;
            if (_talentTuning.EchoValleyChorus)
            {
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius + 1.4f,
                    multiplier * 0.35f,
                    ElementTag.None,
                    EchoCapstoneMetric);
            }
            if (_talentTuning.EchoSavedLine &&
                _echoActivations % 2 == 0)
            {
                yield return new WaitForSeconds(0.3f);
                ApplyPulseDamage(
                    position,
                    carrier,
                    radius,
                    multiplier * 0.55f,
                    ElementTag.None,
                    EchoCapstoneMetric);
            }
        }

        private IEnumerator ResolveDelayedPulse(
            Vector3 position,
            CarrierSlot carrier,
            float radius,
            float multiplier,
            float delay,
            ElementTag element,
            StableConfigId metric)
        {
            yield return new WaitForSeconds(delay);
            ApplyPulseDamage(
                position,
                carrier,
                radius,
                multiplier,
                element,
                metric);
        }

        private void ApplyPulseDamage(
            Vector3 position,
            CarrierSlot carrier,
            float radius,
            float multiplier,
            ElementTag element,
            StableConfigId metricOverride = default)
        {
            FxFactory.SpawnElementBurst(
                position + Vector3.up * 0.05f,
                element,
                radius,
                0.55f);

            PlayerController player = GetComponent<PlayerController>();
            PlayerCombat combat = GetComponent<PlayerCombat>();
            float attackDamage = player?.Stats.attackDamage ?? 10f;
            LayerMask mask =
                combat != null ? combat.EnemyLayer : Physics.AllLayers;
            _burstTargets.Clear();
            foreach (Collider hit in Physics.OverlapSphere(
                         position,
                         radius,
                         mask))
            {
                IDamageable damageable =
                    hit.GetComponentInParent<IDamageable>();
                if (damageable == null ||
                    damageable is Destructible ||
                    damageable is Component component &&
                    component.gameObject == gameObject ||
                    damageable.Stats != null &&
                    !damageable.Stats.IsAlive)
                {
                    continue;
                }

                GameObject target =
                    damageable is Component targetComponent
                        ? targetComponent.gameObject
                        : hit.gameObject;
                if (!_burstTargets.Add(target.GetInstanceID()))
                    continue;

                ResolveCarrierDamage(
                    damageable,
                    target,
                    position,
                    attackDamage,
                    multiplier,
                    metricOverride.IsEmpty
                        ? MetricFor(carrier)
                        : metricOverride,
                    carrier);
                if (carrier == CarrierSlot.Mobility &&
                    _spirit.Identity.SpeciesConfigId ==
                    FirstSpiritCircuitContent.BounceGelSpecies)
                {
                    target.GetComponent<ICombatImpulseReceiver>()
                        ?.TryApplyCombatImpulse(
                            position,
                            1.15f +
                            _talentTuning.RadiusBonus * 0.1f,
                            interrupt: true);
                }
            }
        }

        private void SpawnGelBounce(
            Vector3 position,
            GameObject triggerTarget,
            CarrierSlot carrier)
        {
            GameObject target = FindNearestTarget(
                position,
                triggerTarget);
            if (target == null)
            {
                if (_talentTuning.GelSoftLanding)
                {
                    ApplyPulseDamage(
                        position,
                        carrier,
                        2.2f + _talentTuning.RadiusBonus,
                        0.22f * _talentTuning.DamageFactor,
                        ElementTag.Water);
                    return;
                }
                FxFactory.SpawnElementBurst(
                    position + Vector3.up * 0.05f,
                    ElementTag.Water,
                    1.4f,
                    0.45f);
                return;
            }

            var targets = new List<GameObject> { target };
            var excluded = new HashSet<GameObject>
            {
                target,
                triggerTarget
            };
            GameObject current = target;
            while (targets.Count < _talentTuning.GelTargetCount)
            {
                GameObject next = FindNearestTarget(
                    current.transform.position,
                    null,
                    excluded);
                if (next == null)
                    break;
                targets.Add(next);
                excluded.Add(next);
                current = next;
            }
            if (_talentTuning.GelReturnStart &&
                targets.Count < _talentTuning.GelTargetCount &&
                triggerTarget != null &&
                triggerTarget.GetComponent<IDamageable>() is
                    IDamageable returnTarget &&
                (returnTarget.Stats == null ||
                 returnTarget.Stats.IsAlive))
            {
                targets.Add(triggerTarget);
            }

            GameObject projectileObject =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "StarterBounceGelProjectile";
            projectileObject.transform.position =
                position + Vector3.up * 0.6f;
            projectileObject.transform.localScale =
                Vector3.one * _talentTuning.GelProjectileScale;
            Collider collider =
                projectileObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            TintGelProjectile(projectileObject);

            FirstSpiritBounceProjectile projectile =
                projectileObject.AddComponent<
                    FirstSpiritBounceProjectile>();
            projectile.Initialize(
                targets,
                FirstSpiritCircuitContent.ProjectileSpeed *
                (_talentTuning.GelAccelerating ? 1.25f : 1f),
                3f,
                _talentTuning.GelBounceDelay,
                0f,
                1f,
                (hitTarget, hitPoint, hitIndex) =>
                    ResolveGelBounceHit(
                        hitTarget,
                        hitPoint,
                        carrier,
                        hitIndex));
        }

        private void ResolveGelBounceHit(
            GameObject target,
            Vector3 hitPoint,
            CarrierSlot carrier,
            int hitIndex)
        {
            IDamageable damageable =
                target?.GetComponent<IDamageable>();
            if (damageable == null)
                return;
            float attackDamage =
                GetComponent<PlayerController>()
                    ?.Stats.attackDamage ?? 10f;
            float multiplier = carrier == CarrierSlot.TechniqueQ
                ? 0.42f
                : 0.28f;
            multiplier *= _talentTuning.DamageFactor *
                          Mathf.Pow(
                              _talentTuning.GelAccelerating
                                  ? 0.84f
                                  : 0.75f,
                              hitIndex);
            ResolveCarrierDamage(
                damageable,
                target,
                hitPoint,
                attackDamage,
                multiplier,
                MetricFor(carrier),
                carrier);
            if (_talentTuning.GelWrapSpark)
            {
                ApplyPulseDamage(
                    hitPoint,
                    carrier,
                    1.7f + _talentTuning.RadiusBonus,
                    0.12f * _talentTuning.DamageFactor,
                    ElementTag.Fire,
                    GelWrappedMetric);
            }
            if (_talentTuning.GelBigCrater && hitIndex == 0)
            {
                ApplyPulseDamage(
                    hitPoint,
                    carrier,
                    3.2f + _talentTuning.RadiusBonus,
                    0.35f * _talentTuning.DamageFactor,
                    ElementTag.Water,
                    GelCraterMetric);
            }
        }

        private void ResolveCarrierDamage(
            IDamageable damageable,
            GameObject target,
            Vector3 hitPoint,
            float attackDamage,
            float multiplier,
            StableConfigId metric,
            CarrierSlot carrier)
        {
            CombatStats stats = damageable.Stats;
            float defense = stats?.defense ?? 0f;
            float requested = Mathf.Max(
                1f,
                attackDamage * multiplier - defense);
            float healthBefore =
                stats != null ? Mathf.Max(0f, stats.currentHp) : requested;
            float expected = Mathf.Min(requested, healthBefore);
            CircuitEntityRef targetRef =
                LegacyCombatResultRecorder.BuildTarget(target);
            CircuitEvent origin = BuildOrigin(carrier);

            RunCombatStats.RecordLegacyDamageBaseline(
                metric,
                requested,
                expected,
                targetRef);
            damageable.OnDamage(requested, hitPoint, gameObject);
            float applied = stats != null
                ? Mathf.Max(
                    0f,
                    healthBefore - Mathf.Max(0f, stats.currentHp))
                : requested;
            RunCombatStats.RecordCircuitDamage(
                metric,
                requested,
                applied,
                targetRef,
                origin);
        }

        private CircuitEvent BuildOrigin(CarrierSlot carrier)
        {
            StableConfigId species =
                _spirit.Identity.SpeciesConfigId;
            ElementTag element = species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies
                    ? ElementTag.Fire
                    : species ==
                      FirstSpiritCircuitContent.BounceGelSpecies
                        ? ElementTag.Water
                        : ElementTag.None;
            int tag = species ==
                FirstSpiritCircuitContent.SparkRaccoonSpecies
                    ? FirstSpiritCircuitContent.SparkTag
                    : species ==
                      FirstSpiritCircuitContent.EchoOwlSpecies
                        ? FirstSpiritCircuitContent.EchoAttackTag
                        : FirstSpiritCircuitContent.BounceAttackTag;
            var source = new CircuitEntityRef(
                gameObject.GetInstanceID(),
                carrier,
                _spirit.Identity.InstanceId,
                PlayerConfig,
                _spirit.Identity.SpeciesConfigId);
            return new CircuitEvent(
                ++_nextChainId,
                0,
                CircuitEventKind.Source,
                source,
                source,
                element,
                tag);
        }

        private StableConfigId MetricFor(CarrierSlot carrier)
        {
            StableConfigId species =
                _spirit.Identity.SpeciesConfigId;
            if (species ==
                FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                return carrier switch
                {
                    CarrierSlot.TechniqueQ => EchoTechniqueMetric,
                    CarrierSlot.Mobility => EchoMobilityMetric,
                    _ => EchoWeaponMetric
                };
            }
            if (species ==
                FirstSpiritCircuitContent.BounceGelSpecies)
            {
                return carrier switch
                {
                    CarrierSlot.TechniqueQ => BounceTechniqueMetric,
                    CarrierSlot.Mobility => BounceMobilityMetric,
                    _ => BounceWeaponMetric
                };
            }
            return carrier switch
            {
                CarrierSlot.TechniqueQ => TechniqueMetric,
                CarrierSlot.Mobility => MobilityMetric,
                _ => WeaponMetric
            };
        }

        private GameObject FindNearestTarget(
            Vector3 origin,
            GameObject excluded,
            ISet<GameObject> excludedTargets = null)
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            LayerMask mask =
                combat != null ? combat.EnemyLayer : Physics.AllLayers;
            GameObject nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (Collider hit in Physics.OverlapSphere(
                         origin,
                         FirstSpiritCircuitContent.TargetSearchRadius +
                         _talentTuning.RadiusBonus,
                         mask))
            {
                IDamageable damageable =
                    hit.GetComponentInParent<IDamageable>();
                if (damageable == null ||
                    damageable is Destructible ||
                    damageable.Stats != null &&
                    !damageable.Stats.IsAlive)
                {
                    continue;
                }
                GameObject candidate =
                    damageable is Component component
                        ? component.gameObject
                        : hit.gameObject;
                if (candidate == excluded ||
                    excludedTargets != null &&
                    excludedTargets.Contains(candidate) ||
                    candidate == gameObject)
                {
                    continue;
                }
                float sqr =
                    (candidate.transform.position - origin)
                    .sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearest = candidate;
                    nearestSqr = sqr;
                }
            }
            return nearest;
        }

        private static void TintGelProjectile(
            GameObject projectile)
        {
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer == null)
                return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var color = new Color(0.30f, 0.90f, 0.72f, 1f);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private bool TryConsumeResolved(GameObject target)
        {
            return _runtime != null &&
                   target != null &&
                   _resolvedFrame == Time.frameCount &&
                   _resolvedTargets.Remove(target.GetInstanceID());
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

        private void EnsureRuntime()
        {
            if (_runtime == null &&
                FeatureFlags.EnableCircuitRuntime)
            {
                TryConfigure(SaveSystem.Instance.Data);
            }
        }

        private void EnsureStarterTechnique()
        {
            if (_runtime == null)
                return;
            if (_configuredSave == null ||
                !_configuredSave.starterTechniqueUnlocked)
                return;

            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat == null || combat.GetSkillInSlot(0) != null)
                return;

            SkillData starter = Resources.Load<SkillData>(
                StarterTechniqueResourcePath);
            if (starter == null)
            {
                Debug.LogWarning(
                    "[ProjectR] 未找到基础术法灵息弹。");
                return;
            }
            combat.EquipSkillQ(starter);
            GameEvents.Publish(
                new GameEvents.SkillEquipped
                {
                    SlotIndex = 0,
                    Skill = starter
                });
        }

        private void PublishAttachmentChanged()
        {
            if (_spirit == null || _runtime == null)
                return;

            GameEvents.Publish(
                new GameEvents.StarterSpiritAttachmentChanged
                {
                    SpiritInstanceId =
                        _spirit.Identity.InstanceId,
                    SpeciesId =
                        _spirit.Identity.SpeciesConfigId,
                    Carrier = _runtime.Attachment
                });
        }
    }
}
