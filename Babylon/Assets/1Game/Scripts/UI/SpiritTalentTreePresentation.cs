using System.Collections.Generic;

namespace XianTu
{
    public enum SpiritTalentTreeNodeState
    {
        Active = 0,
        Available = 1,
        OpenWithoutPoint = 2,
        MissingPrerequisite = 3,
        PermanentlyLocked = 4,
        ConflictingCapstone = 5,
    }

    public readonly struct SpiritTalentTreeNodePresentation
    {
        public SpiritTalentDefinition Definition { get; }
        public int Branch { get; }
        public int VisualIndex { get; }
        public int VisualSlot { get; }
        public SpiritTalentTreeNodeState State { get; }

        public SpiritTalentTreeNodePresentation(
            SpiritTalentDefinition definition,
            int branch,
            int visualIndex,
            SpiritTalentTreeNodeState state)
        {
            Definition = definition;
            Branch = branch;
            VisualIndex = visualIndex;
            VisualSlot = TierOffset(definition.Tier) + visualIndex;
            State = state;
        }

        private static int TierOffset(int tier)
        {
            return tier switch
            {
                1 => 0,
                2 => 3,
                3 => 9,
                _ => 15
            };
        }
    }

    /// <summary>
    /// 把运行时状态转换成固定四层三路线视图，避免UI复制解锁规则。
    /// </summary>
    public static class SpiritTalentTreePresentation
    {
        public static IReadOnlyList<SpiritTalentTreeNodePresentation>
            Build(SpiritRunTalentState state)
        {
            var result =
                new List<SpiritTalentTreeNodePresentation>(18);
            if (state == null)
                return result;

            IReadOnlyList<SpiritTalentDefinition> definitions =
                StarterSpiritTalentCatalog.ForSpecies(
                    state.Spirit.Identity.SpeciesConfigId);
            var byId = new Dictionary<
                StableConfigId,
                SpiritTalentDefinition>();
            var rootBranches =
                new Dictionary<StableConfigId, int>();
            bool hasCapstone = false;
            foreach (SpiritTalentDefinition definition in definitions)
            {
                byId[definition.ConfigId] = definition;
                if (definition.Tier == 1)
                    rootBranches[definition.ConfigId] =
                        rootBranches.Count;
                if (definition.Tier == 4 &&
                    state.IsActive(definition.ConfigId.Value))
                {
                    hasCapstone = true;
                }
            }

            foreach (SpiritTalentDefinition definition in definitions)
            {
                int branch = ResolveBranch(
                    definition,
                    byId,
                    rootBranches);
                int visualIndex = definition.VisualIndex >= 0
                    ? definition.VisualIndex
                    : definition.Tier == 1 ||
                      definition.Tier == 4
                        ? branch
                        : branch * 2;
                result.Add(new SpiritTalentTreeNodePresentation(
                    definition,
                    branch,
                    visualIndex,
                    ResolveState(
                        state,
                        definition,
                        hasCapstone)));
            }
            result.Sort(CompareNodes);
            return result;
        }

        private static SpiritTalentTreeNodeState ResolveState(
            SpiritRunTalentState state,
            SpiritTalentDefinition definition,
            bool hasCapstone)
        {
            if (state.IsActive(definition.ConfigId.Value))
                return SpiritTalentTreeNodeState.Active;
            if (definition.Tier > 1 &&
                !state.Spirit.IsTalentUnlocked(definition.ConfigId))
            {
                return SpiritTalentTreeNodeState.PermanentlyLocked;
            }
            if (definition.Tier == 4 && hasCapstone)
                return SpiritTalentTreeNodeState.ConflictingCapstone;
            if (!state.ArePrerequisitesMet(definition))
            {
                return SpiritTalentTreeNodeState.MissingPrerequisite;
            }
            return state.UnspentPoints > 0
                ? SpiritTalentTreeNodeState.Available
                : SpiritTalentTreeNodeState.OpenWithoutPoint;
        }

        private static int ResolveBranch(
            SpiritTalentDefinition definition,
            IReadOnlyDictionary<
                StableConfigId,
                SpiritTalentDefinition> byId,
            IReadOnlyDictionary<StableConfigId, int> rootBranches)
        {
            if (definition.Branch >= 0)
                return definition.Branch;
            SpiritTalentDefinition current = definition;
            var visited = new HashSet<StableConfigId>();
            while (!current.PrerequisiteId.IsEmpty &&
                   visited.Add(current.ConfigId) &&
                   byId.TryGetValue(
                       current.PrerequisiteId,
                       out SpiritTalentDefinition prerequisite))
            {
                current = prerequisite;
            }
            return rootBranches.TryGetValue(
                current.ConfigId,
                out int branch)
                    ? branch
                    : 0;
        }

        private static int CompareNodes(
            SpiritTalentTreeNodePresentation left,
            SpiritTalentTreeNodePresentation right)
        {
            int tier = left.Definition.Tier.CompareTo(
                right.Definition.Tier);
            return tier != 0
                ? tier
                : left.VisualIndex.CompareTo(right.VisualIndex);
        }
    }
}
