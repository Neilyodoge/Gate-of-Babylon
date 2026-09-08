namespace XianTu
{
    public enum StarterPrologueStep
    {
        NotStarted = 0,
        StarterChosen = 1,
        AttachmentChosen = 2,
        Completed = 3,
    }

    public enum StarterPrologueAdvanceResult
    {
        Success = 0,
        NoChange = 1,
        InvalidSave = 2,
        PrerequisiteMissing = 3,
        InvalidCarrier = 4,
    }

    /// <summary>
    /// 新手初契序章的幂等、单调检查点合同。
    /// 只修改数据，不负责场景、UI或磁盘写入。
    /// </summary>
    public static class StarterPrologueProgression
    {
        public static StarterPrologueStep GetStep(SaveDataV1 save)
        {
            if (save == null)
                return StarterPrologueStep.NotStarted;
            int step = save.starterPrologueStep;
            if (step < (int)StarterPrologueStep.NotStarted)
                step = (int)StarterPrologueStep.NotStarted;
            if (step > (int)StarterPrologueStep.Completed)
                step = (int)StarterPrologueStep.Completed;
            return (StarterPrologueStep)step;
        }

        public static StarterPrologueAdvanceResult RecordStarterChosen(
            SaveDataV1 save)
        {
            if (save == null)
                return StarterPrologueAdvanceResult.InvalidSave;
            if (string.IsNullOrWhiteSpace(
                    save.starterSpiritSpeciesId))
            {
                return StarterPrologueAdvanceResult.PrerequisiteMissing;
            }

            save.starterTechniqueUnlocked = true;
            return AdvanceTo(
                save,
                StarterPrologueStep.StarterChosen);
        }

        public static StarterPrologueAdvanceResult RecordAttachment(
            SaveDataV1 save,
            CarrierSlot carrier)
        {
            if (save == null)
                return StarterPrologueAdvanceResult.InvalidSave;
            if (!StarterSpiritCarrierRuntime.Supports(carrier))
                return StarterPrologueAdvanceResult.InvalidCarrier;
            if (GetStep(save) < StarterPrologueStep.StarterChosen)
            {
                return StarterPrologueAdvanceResult
                    .PrerequisiteMissing;
            }
            if (GetStep(save) >=
                StarterPrologueStep.Completed)
            {
                return StarterPrologueAdvanceResult.NoChange;
            }

            bool carrierChanged =
                save.starterPrologueCarrier != (int)carrier;
            save.starterPrologueCarrier = (int)carrier;
            StarterPrologueAdvanceResult result = AdvanceTo(
                save,
                StarterPrologueStep.AttachmentChosen);
            return result == StarterPrologueAdvanceResult.NoChange &&
                   carrierChanged
                ? StarterPrologueAdvanceResult.Success
                : result;
        }

        public static StarterPrologueAdvanceResult RecordTrialCompleted(
            SaveDataV1 save)
        {
            if (save == null)
                return StarterPrologueAdvanceResult.InvalidSave;
            if (GetStep(save) <
                StarterPrologueStep.AttachmentChosen)
            {
                return StarterPrologueAdvanceResult
                    .PrerequisiteMissing;
            }
            return AdvanceTo(
                save,
                StarterPrologueStep.Completed);
        }

        public static bool TryGetAttachment(
            SaveDataV1 save,
            out CarrierSlot carrier)
        {
            carrier = default;
            if (save == null ||
                GetStep(save) <
                StarterPrologueStep.AttachmentChosen)
            {
                return false;
            }

            carrier = (CarrierSlot)save.starterPrologueCarrier;
            return StarterSpiritCarrierRuntime.Supports(carrier);
        }

        public static bool RequiresFirstAttachment(SaveDataV1 save)
        {
            return save != null &&
                   GetStep(save) ==
                   StarterPrologueStep.StarterChosen &&
                   !string.IsNullOrWhiteSpace(
                       save.starterSpiritSpeciesId);
        }

        private static StarterPrologueAdvanceResult AdvanceTo(
            SaveDataV1 save,
            StarterPrologueStep target)
        {
            StarterPrologueStep current = GetStep(save);
            if (current >= target)
                return StarterPrologueAdvanceResult.NoChange;
            save.starterPrologueStep = (int)target;
            return StarterPrologueAdvanceResult.Success;
        }
    }
}
