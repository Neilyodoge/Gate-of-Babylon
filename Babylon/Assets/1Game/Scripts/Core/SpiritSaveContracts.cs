using System;
using System.Collections.Generic;

namespace XianTu
{
    [Serializable]
    public sealed class SpiritInstanceSave
    {
        public string instanceGuid = "";
        public string speciesConfigId = "";
        public string primaryPersonalityId = "";
        public string secondaryPersonalityId = "";
        public int playerBond;
        public int talentExpeditions;
        public int talentKeyVictories;
        public List<SpiritRelationshipSave> relationships = new();
        public List<SpiritEnlightenmentSave> enlightenmentPool = new();
        public List<string> carrierAdaptationIds = new();
        public List<string> unlockedTalentIds = new();
    }

    [Serializable]
    public sealed class SpiritRelationshipSave
    {
        public string otherSpiritGuid = "";
        public string relationshipConfigId = "";
        public int affinity;
    }

    [Serializable]
    public sealed class SpiritEnlightenmentSave
    {
        public string configId = "";
        public int kind;
    }

    /// <summary>
    /// 器灵永久领域态与JsonUtility DTO之间的单向边界。
    /// 临时悟法和局内附着不进入永久存档。
    /// </summary>
    public static class SpiritSaveMapper
    {
        public static SpiritInstanceSave ToSave(SpiritInstanceState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            SpiritIdentity identity = state.Identity;
            var save = new SpiritInstanceSave
            {
                instanceGuid = identity.InstanceId.ToString("N"),
                speciesConfigId = identity.SpeciesConfigId.Value,
                primaryPersonalityId =
                    identity.PrimaryPersonalityId.Value,
                secondaryPersonalityId =
                    identity.SecondaryPersonalityId.Value ?? "",
                playerBond = state.PlayerBond,
                talentExpeditions = state.TalentExpeditions,
                talentKeyVictories = state.TalentKeyVictories
            };

            foreach (SpiritRelationship relationship
                     in state.Relationships.Values)
            {
                save.relationships.Add(new SpiritRelationshipSave
                {
                    otherSpiritGuid =
                        relationship.OtherSpiritId.ToString("N"),
                    relationshipConfigId =
                        relationship.RelationshipConfigId.Value,
                    affinity = relationship.Affinity
                });
            }
            save.relationships.Sort((left, right) =>
                string.CompareOrdinal(
                    left.otherSpiritGuid,
                    right.otherSpiritGuid));

            foreach (SpiritEnlightenment enlightenment
                     in state.EnlightenmentPool)
            {
                save.enlightenmentPool.Add(
                    new SpiritEnlightenmentSave
                    {
                        configId = enlightenment.ConfigId.Value,
                        kind = (int)enlightenment.Kind
                    });
            }
            save.enlightenmentPool.Sort((left, right) =>
                string.CompareOrdinal(left.configId, right.configId));

            foreach (StableConfigId adaptation
                     in state.CarrierAdaptations)
            {
                save.carrierAdaptationIds.Add(adaptation.Value);
            }
            save.carrierAdaptationIds.Sort(
                StringComparer.Ordinal);

            foreach (StableConfigId talent in state.UnlockedTalents)
                save.unlockedTalentIds.Add(talent.Value);
            save.unlockedTalentIds.Sort(StringComparer.Ordinal);
            return save;
        }

        public static bool TryRestore(
            SpiritInstanceSave save,
            out SpiritInstanceState state)
        {
            state = null;
            if (save == null ||
                !Guid.TryParse(save.instanceGuid, out Guid instanceId) ||
                instanceId == Guid.Empty ||
                string.IsNullOrWhiteSpace(save.speciesConfigId) ||
                string.IsNullOrWhiteSpace(
                    save.primaryPersonalityId))
            {
                return false;
            }

            var identity = new SpiritIdentity(
                instanceId,
                new StableConfigId(save.speciesConfigId),
                new StableConfigId(save.primaryPersonalityId),
                string.IsNullOrWhiteSpace(
                    save.secondaryPersonalityId)
                    ? default
                    : new StableConfigId(
                        save.secondaryPersonalityId));
            state = new SpiritInstanceState(identity);
            state.SetPlayerBond(save.playerBond);
            state.SetTalentProgress(
                save.talentExpeditions,
                save.talentKeyVictories);

            RestoreRelationships(save.relationships, state);
            RestoreEnlightenments(save.enlightenmentPool, state);
            RestoreAdaptations(save.carrierAdaptationIds, state);
            RestoreTalents(save.unlockedTalentIds, state);
            return true;
        }

        public static bool Normalize(SpiritInstanceSave save)
        {
            if (save == null)
                return false;

            bool changed = false;
            changed |= EnsureList(ref save.relationships);
            changed |= EnsureList(ref save.enlightenmentPool);
            changed |= EnsureList(ref save.carrierAdaptationIds);
            changed |= EnsureList(ref save.unlockedTalentIds);
            return changed;
        }

        private static void RestoreRelationships(
            List<SpiritRelationshipSave> saves,
            SpiritInstanceState state)
        {
            if (saves == null)
                return;

            foreach (SpiritRelationshipSave save in saves)
            {
                if (save == null ||
                    !Guid.TryParse(
                        save.otherSpiritGuid,
                        out Guid otherId) ||
                    otherId == Guid.Empty ||
                    otherId == state.Identity.InstanceId ||
                    string.IsNullOrWhiteSpace(
                        save.relationshipConfigId))
                {
                    continue;
                }

                state.SetRelationship(new SpiritRelationship(
                    otherId,
                    new StableConfigId(
                        save.relationshipConfigId),
                    save.affinity));
            }
        }

        private static void RestoreEnlightenments(
            List<SpiritEnlightenmentSave> saves,
            SpiritInstanceState state)
        {
            if (saves == null)
                return;

            foreach (SpiritEnlightenmentSave save in saves)
            {
                if (save == null ||
                    string.IsNullOrWhiteSpace(save.configId) ||
                    !Enum.IsDefined(
                        typeof(EnlightenmentKind),
                        save.kind))
                {
                    continue;
                }

                state.AddToEnlightenmentPool(
                    new SpiritEnlightenment(
                        new StableConfigId(save.configId),
                        (EnlightenmentKind)save.kind));
            }
        }

        private static void RestoreAdaptations(
            List<string> saves,
            SpiritInstanceState state)
        {
            if (saves == null)
                return;

            foreach (string configId in saves)
            {
                if (!string.IsNullOrWhiteSpace(configId))
                {
                    state.UnlockCarrierAdaptation(
                        new StableConfigId(configId));
                }
            }
        }

        private static void RestoreTalents(
            List<string> saves,
            SpiritInstanceState state)
        {
            if (saves == null)
                return;

            foreach (string configId in saves)
            {
                if (!string.IsNullOrWhiteSpace(configId))
                    state.UnlockTalent(new StableConfigId(configId));
            }
        }

        private static bool EnsureList<T>(ref List<T> list)
        {
            if (list != null)
                return false;

            list = new List<T>();
            return true;
        }
    }
}
