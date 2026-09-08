using System;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 灵宠共同探索里程碑。长期成长只开放局内候选，不直接点亮节点。
    /// </summary>
    public static class SpiritTalentPermanentProgression
    {
        public const int TierTwoExpeditions = 1;
        public const int TierThreeExpeditions = 3;
        public const int TierFourExpeditions = 6;
        public const int TierFourKeyVictories = 1;

        public static int RecordExpedition(
            SpiritInstanceState spirit,
            bool keyVictory)
        {
            if (spirit == null)
                throw new ArgumentNullException(nameof(spirit));

            spirit.RecordTalentExpedition(keyVictory);
            int maxTier = GetPermanentlyAvailableTier(spirit);
            int unlocked = 0;
            foreach (SpiritTalentDefinition definition
                     in StarterSpiritTalentCatalog.ForSpecies(
                         spirit.Identity.SpeciesConfigId))
            {
                if (definition.Tier <= 1 ||
                    definition.Tier > maxTier ||
                    spirit.IsTalentUnlocked(definition.ConfigId))
                {
                    continue;
                }
                spirit.UnlockTalent(definition.ConfigId);
                unlocked++;
            }
            return unlocked;
        }

        public static int GetPermanentlyAvailableTier(
            SpiritInstanceState spirit)
        {
            if (spirit.TalentExpeditions <
                TierTwoExpeditions)
            {
                return 1;
            }
            if (spirit.TalentExpeditions <
                TierThreeExpeditions)
            {
                return 2;
            }
            if (spirit.TalentExpeditions <
                    TierFourExpeditions ||
                spirit.TalentKeyVictories <
                    TierFourKeyVictories)
            {
                return 3;
            }
            return 4;
        }

        public static int CommitActiveExpedition(
            SaveDataV1 save,
            bool keyVictory)
        {
            if (save?.spiritRoster == null ||
                save.activeSpiritInstanceGuids == null)
            {
                return 0;
            }

            var activeIds = new HashSet<Guid>();
            foreach (string guidText in save.activeSpiritInstanceGuids)
            {
                if (Guid.TryParse(guidText, out Guid id) &&
                    id != Guid.Empty)
                {
                    activeIds.Add(id);
                }
            }

            int unlocked = 0;
            for (int i = 0; i < save.spiritRoster.Count; i++)
            {
                SpiritInstanceSave entry = save.spiritRoster[i];
                if (entry == null ||
                    !Guid.TryParse(entry.instanceGuid, out Guid id) ||
                    !activeIds.Contains(id) ||
                    !SpiritSaveMapper.TryRestore(
                        entry,
                        out SpiritInstanceState spirit) ||
                    !StarterSpiritCarrierRuntime.IsStarterSpecies(
                        spirit.Identity.SpeciesConfigId))
                {
                    continue;
                }

                unlocked += RecordExpedition(spirit, keyVictory);
                save.spiritRoster[i] = SpiritSaveMapper.ToSave(spirit);
            }
            return unlocked;
        }
    }
}
