using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum StarterSpiritChoiceResult
    {
        Success = 0,
        InvalidSave = 1,
        InvalidSpecies = 2,
        AlreadyChosen = 3,
        RosterNotEmpty = 4,
        InvalidInstanceId = 5,
    }

    /// <summary>
    /// 新手关卡初始灵宠三选一合同。只在显式选择时修改数据，
    /// 不负责展示UI、推进关卡或自动向空档赠送灵宠。
    /// </summary>
    public static class StarterSpiritChoice
    {
        public static readonly StableConfigId DefaultPersonality =
            new("personality.starter.default");

        public static IReadOnlyList<StableConfigId> Options { get; } =
            new[]
            {
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                FirstSpiritCircuitContent.BounceGelSpecies
            };

        public static StarterSpiritChoiceResult TryChoose(
            SaveDataV1 save,
            StableConfigId species,
            out SpiritInstanceState spirit,
            Func<Guid> createInstanceId = null)
        {
            spirit = null;
            if (save == null)
                return StarterSpiritChoiceResult.InvalidSave;
            if (!IsOption(species))
                return StarterSpiritChoiceResult.InvalidSpecies;
            if (!string.IsNullOrWhiteSpace(
                    save.starterSpiritSpeciesId))
            {
                return StarterSpiritChoiceResult.AlreadyChosen;
            }

            save.spiritRoster ??= new List<SpiritInstanceSave>();
            save.activeSpiritInstanceGuids ??= new List<string>();
            if (save.spiritRoster.Count > 0)
                return StarterSpiritChoiceResult.RosterNotEmpty;

            Guid instanceId = createInstanceId != null
                ? createInstanceId()
                : Guid.NewGuid();
            if (instanceId == Guid.Empty)
                return StarterSpiritChoiceResult.InvalidInstanceId;

            spirit = new SpiritInstanceState(new SpiritIdentity(
                instanceId,
                species,
                DefaultPersonality));
            save.spiritRoster.Add(SpiritSaveMapper.ToSave(spirit));
            save.activeSpiritInstanceGuids.Clear();
            save.activeSpiritInstanceGuids.Add(
                instanceId.ToString("N"));
            save.starterSpiritSpeciesId = species.Value;
            save.starterPrologueCarrier = -1;
            StarterPrologueProgression.RecordStarterChosen(save);
            return StarterSpiritChoiceResult.Success;
        }

        public static StarterSpiritChoiceResult ChooseActiveSave(
            StableConfigId species,
            out SpiritInstanceState spirit)
        {
            SaveSystem saveSystem = SaveSystem.Instance;
            StarterSpiritChoiceResult result = TryChoose(
                saveSystem.Data,
                species,
                out spirit);
            if (result == StarterSpiritChoiceResult.Success)
                saveSystem.Save();
            return result;
        }

        public static bool IsOption(StableConfigId species)
        {
            foreach (StableConfigId option in Options)
            {
                if (species == option)
                    return true;
            }
            return false;
        }
    }
}
