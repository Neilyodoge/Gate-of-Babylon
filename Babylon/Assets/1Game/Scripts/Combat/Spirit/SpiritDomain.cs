using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum EnlightenmentKind
    {
        Source = 0,
        Response = 1,
        Transform = 2,
    }

    public readonly struct SpiritEnlightenment
    {
        public StableConfigId ConfigId { get; }
        public EnlightenmentKind Kind { get; }

        public SpiritEnlightenment(
            StableConfigId configId,
            EnlightenmentKind kind)
        {
            if (configId.IsEmpty)
                throw new ArgumentException(
                    "Enlightenment config ID cannot be empty.",
                    nameof(configId));

            ConfigId = configId;
            Kind = kind;
        }
    }

    public readonly struct SpiritIdentity
    {
        public Guid InstanceId { get; }
        public StableConfigId SpeciesConfigId { get; }
        public StableConfigId PrimaryPersonalityId { get; }
        public StableConfigId SecondaryPersonalityId { get; }

        public SpiritIdentity(
            Guid instanceId,
            StableConfigId speciesConfigId,
            StableConfigId primaryPersonalityId,
            StableConfigId secondaryPersonalityId = default)
        {
            if (instanceId == Guid.Empty)
                throw new ArgumentException(
                    "Spirit instance GUID cannot be empty.",
                    nameof(instanceId));
            if (speciesConfigId.IsEmpty)
                throw new ArgumentException(
                    "Spirit species config ID cannot be empty.",
                    nameof(speciesConfigId));
            if (primaryPersonalityId.IsEmpty)
                throw new ArgumentException(
                    "Primary personality config ID cannot be empty.",
                    nameof(primaryPersonalityId));

            InstanceId = instanceId;
            SpeciesConfigId = speciesConfigId;
            PrimaryPersonalityId = primaryPersonalityId;
            SecondaryPersonalityId = secondaryPersonalityId;
        }
    }

    public readonly struct SpiritRelationship
    {
        public Guid OtherSpiritId { get; }
        public StableConfigId RelationshipConfigId { get; }
        public int Affinity { get; }

        public SpiritRelationship(
            Guid otherSpiritId,
            StableConfigId relationshipConfigId,
            int affinity)
        {
            if (otherSpiritId == Guid.Empty)
                throw new ArgumentException(
                    "Related spirit GUID cannot be empty.",
                    nameof(otherSpiritId));
            if (relationshipConfigId.IsEmpty)
                throw new ArgumentException(
                    "Relationship config ID cannot be empty.",
                    nameof(relationshipConfigId));

            OtherSpiritId = otherSpiritId;
            RelationshipConfigId = relationshipConfigId;
            Affinity = affinity;
        }
    }

    /// <summary>
    /// 单个器灵在局外与局内共享的领域状态。
    /// 附着布局单独保存，因此迁灵不会清除悟法投资。
    /// </summary>
    public sealed class SpiritInstanceState
    {
        private readonly Dictionary<Guid, SpiritRelationship> _relationships =
            new();
        private readonly Dictionary<StableConfigId, SpiritEnlightenment>
            _enlightenmentPool = new();
        private readonly Dictionary<StableConfigId, SpiritEnlightenment>
            _temporaryEnlightenments = new();
        private readonly HashSet<StableConfigId> _carrierAdaptations = new();
        private readonly HashSet<StableConfigId> _unlockedTalents = new();

        public SpiritIdentity Identity { get; }
        public int PlayerBond { get; private set; }
        public int TalentExpeditions { get; private set; }
        public int TalentKeyVictories { get; private set; }
        public IReadOnlyDictionary<Guid, SpiritRelationship> Relationships
            => _relationships;
        public IReadOnlyCollection<SpiritEnlightenment> EnlightenmentPool
            => _enlightenmentPool.Values;
        public IReadOnlyCollection<SpiritEnlightenment> TemporaryEnlightenments
            => _temporaryEnlightenments.Values;
        public IReadOnlyCollection<StableConfigId> CarrierAdaptations
            => _carrierAdaptations;
        public IReadOnlyCollection<StableConfigId> UnlockedTalents
            => _unlockedTalents;

        public SpiritInstanceState(SpiritIdentity identity)
        {
            Identity = identity;
        }

        public void SetPlayerBond(int bond)
        {
            PlayerBond = Math.Max(0, bond);
        }

        public void SetTalentProgress(
            int expeditions,
            int keyVictories)
        {
            TalentExpeditions = Math.Max(0, expeditions);
            TalentKeyVictories = Math.Max(0, keyVictories);
        }

        public void RecordTalentExpedition(bool keyVictory)
        {
            TalentExpeditions++;
            if (keyVictory)
                TalentKeyVictories++;
        }

        public void SetRelationship(SpiritRelationship relationship)
        {
            if (relationship.OtherSpiritId == Identity.InstanceId)
                throw new ArgumentException(
                    "A spirit cannot relate to itself.",
                    nameof(relationship));

            _relationships[relationship.OtherSpiritId] = relationship;
        }

        public void UnlockCarrierAdaptation(StableConfigId carrierConfigId)
        {
            if (carrierConfigId.IsEmpty)
                throw new ArgumentException(
                    "Carrier config ID cannot be empty.",
                    nameof(carrierConfigId));

            _carrierAdaptations.Add(carrierConfigId);
        }

        public void UnlockTalent(StableConfigId talentConfigId)
        {
            if (talentConfigId.IsEmpty)
                throw new ArgumentException(
                    "Talent config ID cannot be empty.",
                    nameof(talentConfigId));

            _unlockedTalents.Add(talentConfigId);
        }

        public bool IsTalentUnlocked(StableConfigId talentConfigId)
        {
            return _unlockedTalents.Contains(talentConfigId);
        }

        public void AddToEnlightenmentPool(
            SpiritEnlightenment enlightenment)
        {
            _enlightenmentPool[enlightenment.ConfigId] = enlightenment;
        }

        public void LearnTemporary(SpiritEnlightenment enlightenment)
        {
            _temporaryEnlightenments[enlightenment.ConfigId] = enlightenment;
        }

        public void ClearTemporaryEnlightenments()
        {
            _temporaryEnlightenments.Clear();
        }
    }
}
