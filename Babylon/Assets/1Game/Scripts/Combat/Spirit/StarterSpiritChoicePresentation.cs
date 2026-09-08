using System.Collections.Generic;

namespace XianTu
{
    public readonly struct StarterSpiritChoiceProfile
    {
        public StableConfigId SpeciesId { get; }
        public string DisplayName { get; }
        public string PersonalityLine { get; }
        public string CombatTendency { get; }

        public StarterSpiritChoiceProfile(
            StableConfigId speciesId,
            string displayName,
            string personalityLine,
            string combatTendency)
        {
            SpeciesId = speciesId;
            DisplayName = displayName;
            PersonalityLine = personalityLine;
            CombatTendency = combatTendency;
        }
    }

    public static class StarterSpiritChoicePresentation
    {
        public static IReadOnlyList<StarterSpiritChoiceProfile>
            Profiles { get; } =
            new[]
            {
                new StarterSpiritChoiceProfile(
                    FirstSpiritCircuitContent.SparkRaccoonSpecies,
                    "火花狸",
                    "急性，爱凑热闹。",
                    "越连续进攻越来劲"),
                new StarterSpiritChoiceProfile(
                    FirstSpiritCircuitContent.EchoOwlSpecies,
                    "响响鸮",
                    "安静，爱模仿动静。",
                    "擅长让动作再次发生"),
                new StarterSpiritChoiceProfile(
                    FirstSpiritCircuitContent.BounceGelSpecies,
                    "弹弹胶",
                    "活泼，总想把东西弹开。",
                    "擅长扩散和位移"),
            };

        public static bool TryGetProfile(
            StableConfigId species,
            out StarterSpiritChoiceProfile profile)
        {
            foreach (StarterSpiritChoiceProfile candidate in Profiles)
            {
                if (candidate.SpeciesId != species)
                    continue;
                profile = candidate;
                return true;
            }

            profile = default;
            return false;
        }
    }

    public enum StarterSpiritChoiceStage
    {
        Browsing = 0,
        Confirming = 1,
        Committed = 2,
    }

    public enum StarterSpiritChoiceSubmitResult
    {
        Success = 0,
        NotConfirming = 1,
        ChoiceRejected = 2,
    }

    /// <summary>
    /// 初契展示的纯状态合同。首次点击只进入确认，
    /// 只有确认提交才调用永久选择合同。
    /// </summary>
    public sealed class StarterSpiritChoiceSession
    {
        public StarterSpiritChoiceStage Stage { get; private set; }
        public StableConfigId FocusedSpecies { get; private set; }
        public StarterSpiritChoiceResult LastChoiceResult { get; private set; }

        public bool TryFocus(StableConfigId species)
        {
            if (Stage != StarterSpiritChoiceStage.Browsing ||
                !StarterSpiritChoicePresentation.TryGetProfile(
                    species,
                    out _))
            {
                return false;
            }

            FocusedSpecies = species;
            return true;
        }

        public bool BeginConfirmation(StableConfigId species)
        {
            if (!TryFocus(species))
                return false;
            Stage = StarterSpiritChoiceStage.Confirming;
            return true;
        }

        public bool CancelConfirmation()
        {
            if (Stage != StarterSpiritChoiceStage.Confirming)
                return false;
            Stage = StarterSpiritChoiceStage.Browsing;
            return true;
        }

        public StarterSpiritChoiceSubmitResult Commit(
            SaveDataV1 save,
            out SpiritInstanceState spirit)
        {
            spirit = null;
            if (Stage != StarterSpiritChoiceStage.Confirming)
                return StarterSpiritChoiceSubmitResult.NotConfirming;

            LastChoiceResult = StarterSpiritChoice.TryChoose(
                save,
                FocusedSpecies,
                out spirit);
            if (LastChoiceResult != StarterSpiritChoiceResult.Success)
                return StarterSpiritChoiceSubmitResult.ChoiceRejected;

            Stage = StarterSpiritChoiceStage.Committed;
            return StarterSpiritChoiceSubmitResult.Success;
        }
    }
}
