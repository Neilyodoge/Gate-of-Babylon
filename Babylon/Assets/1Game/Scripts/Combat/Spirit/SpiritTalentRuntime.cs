using System;
using System.Collections.Generic;

namespace XianTu
{
    public enum SpiritTalentActivationResult
    {
        Success = 0,
        AlreadyActive = 1,
        NoPoint = 2,
        WrongSpecies = 3,
        PermanentlyLocked = 4,
        MissingPrerequisite = 5,
        UnknownTalent = 6,
        ConflictingCapstone = 7,
    }

    public enum SpiritTalentPrerequisiteMode
    {
        All = 0,
        Any = 1,
    }

    public readonly struct SpiritTalentDefinition
    {
        public StableConfigId ConfigId { get; }
        public StableConfigId SpeciesId { get; }
        public StableConfigId PrerequisiteId { get; }
        public IReadOnlyList<StableConfigId> PrerequisiteIds { get; }
        public SpiritTalentPrerequisiteMode PrerequisiteMode { get; }
        public int Branch { get; }
        public int VisualIndex { get; }
        public int Tier { get; }
        public string Name { get; }
        public string Description { get; }

        public SpiritTalentDefinition(
            string configId,
            StableConfigId speciesId,
            int tier,
            string name,
            string description,
            string prerequisiteId = null)
        {
            if (speciesId.IsEmpty)
                throw new ArgumentException(
                    "Talent species ID cannot be empty.",
                    nameof(speciesId));
            if (tier < 1 || tier > 4)
                throw new ArgumentOutOfRangeException(nameof(tier));

            ConfigId = new StableConfigId(configId);
            SpeciesId = speciesId;
            PrerequisiteId = string.IsNullOrWhiteSpace(prerequisiteId)
                ? default
                : new StableConfigId(prerequisiteId);
            PrerequisiteIds = PrerequisiteId.IsEmpty
                ? Array.Empty<StableConfigId>()
                : new[] { PrerequisiteId };
            PrerequisiteMode = SpiritTalentPrerequisiteMode.All;
            Branch = -1;
            VisualIndex = -1;
            Tier = tier;
            Name = name ?? "";
            Description = description ?? "";
        }

        public SpiritTalentDefinition(
            string configId,
            StableConfigId speciesId,
            int tier,
            string name,
            string description,
            int branch,
            int visualIndex,
            SpiritTalentPrerequisiteMode prerequisiteMode,
            params string[] prerequisiteIds)
        {
            if (speciesId.IsEmpty)
                throw new ArgumentException(
                    "Talent species ID cannot be empty.",
                    nameof(speciesId));
            if (tier < 1 || tier > 4)
                throw new ArgumentOutOfRangeException(nameof(tier));
            int slotCount = tier == 1 || tier == 4 ? 3 : 6;
            if (branch < 0 || branch > 2)
                throw new ArgumentOutOfRangeException(nameof(branch));
            if (visualIndex < 0 || visualIndex >= slotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(visualIndex));
            }

            ConfigId = new StableConfigId(configId);
            SpeciesId = speciesId;
            Tier = tier;
            Name = name ?? "";
            Description = description ?? "";
            Branch = branch;
            VisualIndex = visualIndex;
            PrerequisiteMode = prerequisiteMode;
            var ids = new List<StableConfigId>();
            if (prerequisiteIds != null)
            {
                foreach (string prerequisite in prerequisiteIds)
                {
                    if (!string.IsNullOrWhiteSpace(prerequisite))
                        ids.Add(new StableConfigId(prerequisite));
                }
            }
            PrerequisiteIds = ids.ToArray();
            PrerequisiteId = ids.Count > 0 ? ids[0] : default;
        }

        public bool ArePrerequisitesMet(
            ISet<StableConfigId> activeTalents)
        {
            if (PrerequisiteIds.Count == 0)
                return true;
            if (activeTalents == null)
                return false;

            bool any = false;
            foreach (StableConfigId prerequisite in PrerequisiteIds)
            {
                bool active = activeTalents.Contains(prerequisite);
                if (PrerequisiteMode ==
                    SpiritTalentPrerequisiteMode.All &&
                    !active)
                {
                    return false;
                }
                any |= active;
            }
            return PrerequisiteMode ==
                SpiritTalentPrerequisiteMode.All || any;
        }
    }

    /// <summary>
    /// 首批三宠垂直切片天赋目录。配置先保持纯C#，验证后再迁配表。
    /// </summary>
    public static class StarterSpiritTalentCatalog
    {
        private static readonly Dictionary<
            StableConfigId,
            SpiritTalentDefinition> ById = new();
        private static readonly Dictionary<
            StableConfigId,
            List<SpiritTalentDefinition>> BySpecies = new();

        static StarterSpiritTalentCatalog()
        {
            AddSparkRaccoon();
            AddEchoOwl();
            AddBounceGel();
        }

        public static bool TryGet(
            StableConfigId configId,
            out SpiritTalentDefinition definition)
        {
            return ById.TryGetValue(configId, out definition);
        }

        public static IReadOnlyList<SpiritTalentDefinition> ForSpecies(
            StableConfigId speciesId)
        {
            return BySpecies.TryGetValue(speciesId, out var definitions)
                ? definitions
                : Array.Empty<SpiritTalentDefinition>();
        }

        private static void AddSparkRaccoon()
        {
            StableConfigId species =
                FirstSpiritCircuitContent.SparkRaccoonSpecies;
            AddNetwork("talent.spark.quick-temper", species, 1,
                "急性子", "2次直接命中产生火花，但回声伤害降低15%",
                0, 0);
            AddNetwork("talent.spark.banked-heat", species, 1,
                "藏余温", "完整回路返还的热度提高到2",
                2, 2);
            AddNetwork("talent.spark.bright-spark", species, 1,
                "亮火星", "火花引出的第一次回声伤害提高20%",
                1, 1);

            AddNetwork("talent.spark.chasing-fire", species, 2,
                "追着烧", "火花寻找目标的范围扩大3米",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.spark.quick-temper");
            AddNetwork("talent.spark.swift-spark", species, 2,
                "飞火", "火花与回声弹道速度提高20%",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.spark.quick-temper");
            AddNetwork("talent.spark.spark-shards", species, 2,
                "碎火屑", "第一次回声命中时溅出弱火伤害",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.spark.bright-spark");
            AddNetwork("talent.spark.compressed-flame", species, 2,
                "压火", "第一次回声伤害额外提高15%",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.spark.bright-spark");
            AddNetwork("talent.spark.undying-core", species, 2,
                "不灭芯", "产生火花时最多保留1点溢出热度",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.spark.banked-heat");
            AddNetwork("talent.spark.warm-return", species, 2,
                "回温", "完整回路额外返还1点热度",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.spark.banked-heat");

            AddNetwork("talent.spark.fire-trail", species, 3,
                "沿路点火", "回声弹道更快并留下短暂火痕",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.spark.chasing-fire",
                "talent.spark.swift-spark");
            AddNetwork("talent.spark.rushing-shards", species, 3,
                "追火碎星", "高速火花命中时扩大碎火溅射",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.spark.swift-spark",
                "talent.spark.spark-shards");
            AddNetwork("talent.spark.echo-fed-heat", species, 3,
                "借响生火", "第一次回声命中后返还1点热度",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.spark.spark-shards",
                "talent.spark.compressed-flame");
            AddNetwork("talent.spark.ember-cache", species, 3,
                "余烬匣", "压缩火花提高伤害并保留额外余温",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.spark.compressed-flame",
                "talent.spark.undying-core");
            AddNetwork("talent.spark.bounce-back", species, 3,
                "弹回来", "第二目标命中后额外返还1点热度",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.spark.undying-core",
                "talent.spark.warm-return");
            AddNetwork("talent.spark.returning-flame", species, 3,
                "回火疾行", "扩大追踪范围，完整回路再返还1点热度",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.spark.warm-return",
                "talent.spark.chasing-fire");

            AddNetwork("talent.spark.chain-burn", species, 4,
                "连燃", "每第二颗火花触发一次较弱复响",
                0, 0, SpiritTalentPrerequisiteMode.Any,
                "talent.spark.fire-trail",
                "talent.spark.rushing-shards",
                "talent.spark.returning-flame");
            AddNetwork("talent.spark.burst-core", species, 4,
                "爆芯", "把后续回路集中为第一次范围爆炸",
                1, 1, SpiritTalentPrerequisiteMode.Any,
                "talent.spark.echo-fed-heat",
                "talent.spark.rushing-shards",
                "talent.spark.ember-cache");
            AddNetwork("talent.spark.carried-ember", species, 4,
                "留火种", "完整回路为下一次动作储存弱火花",
                2, 2, SpiritTalentPrerequisiteMode.Any,
                "talent.spark.bounce-back",
                "talent.spark.ember-cache",
                "talent.spark.returning-flame");
        }

        private static void AddEchoOwl()
        {
            StableConfigId species =
                FirstSpiritCircuitContent.EchoOwlSpecies;
            AddNetwork("talent.echo.clear-echo", species, 1,
                "清亮回声", "第一次回声伤害提高15%", 0, 0);
            AddNetwork("talent.echo.half-beat-late", species, 1,
                "迟半拍", "回声稍晚发生但命中范围扩大", 1, 1);
            AddNetwork("talent.echo.remember-target", species, 1,
                "记住目标", "原目标离场时重新寻找附近目标", 2, 2);

            AddNetwork("talent.echo.duet", species, 2,
                "双声部", "第一次回声后生成一次较弱延迟复响",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.echo.clear-echo");
            AddNetwork("talent.echo.sharp-tone", species, 2,
                "脆声", "第一次回声伤害额外提高10%",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.echo.clear-echo");
            AddNetwork("talent.echo.echo-mark", species, 2,
                "回音标记", "回声目标更易被下一次显化选中",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.echo.half-beat-late");
            AddNetwork("talent.echo.wide-wave", species, 2,
                "宽声场", "延迟回声的搜索与命中范围扩大",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.echo.half-beat-late");
            AddNetwork("talent.echo.long-hearing", species, 2,
                "远处也听见", "重新寻敌和弹射搜索范围扩大",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.echo.remember-target");
            AddNetwork("talent.echo.second-memory", species, 2,
                "再记一次", "目标离场后的重寻范围进一步扩大",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.echo.remember-target");

            AddNetwork("talent.echo.finish-spark", species, 3,
                "替火花说完", "消耗火花后为火花狸保留1点热度",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.echo.duet",
                "talent.echo.sharp-tone");
            AddNetwork("talent.echo.rising-chorus", species, 3,
                "清声合唱", "清亮复响与回音标记共同强化额外复响",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.echo.sharp-tone",
                "talent.echo.echo-mark");
            AddNetwork("talent.echo.threefold-confirm", species, 3,
                "三声确认", "三宠完整联动后强化下一次回声",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.echo.echo-mark",
                "talent.echo.wide-wave");
            AddNetwork("talent.echo.far-refrain", species, 3,
                "远方副歌", "扩大声场并延长回音标记",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.echo.wide-wave",
                "talent.echo.long-hearing");
            AddNetwork("talent.echo.bounce-chamber", species, 3,
                "弹腔", "第二目标受到的弹射衰减降低",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.echo.long-hearing",
                "talent.echo.second-memory");
            AddNetwork("talent.echo.remembered-duet", species, 3,
                "记忆重唱", "重寻成功时补一次较弱延迟复响",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.echo.second-memory",
                "talent.echo.duet");

            AddNetwork("talent.echo.only-the-point", species, 4,
                "只说重点", "取消扩散并集中为强力单体命中",
                0, 0, SpiritTalentPrerequisiteMode.Any,
                "talent.echo.finish-spark",
                "talent.echo.rising-chorus",
                "talent.echo.remembered-duet");
            AddNetwork("talent.echo.valley-chorus", species, 4,
                "满谷回响", "回声向附近两个目标扩散弱复响",
                1, 1, SpiritTalentPrerequisiteMode.Any,
                "talent.echo.threefold-confirm",
                "talent.echo.rising-chorus",
                "talent.echo.far-refrain");
            AddNetwork("talent.echo.saved-line", species, 4,
                "留一句以后说", "每两次回路储存一次延后复响",
                2, 2, SpiritTalentPrerequisiteMode.Any,
                "talent.echo.bounce-chamber",
                "talent.echo.far-refrain",
                "talent.echo.remembered-duet");
        }

        private static void AddBounceGel()
        {
            StableConfigId species =
                FirstSpiritCircuitContent.BounceGelSpecies;
            AddNetwork("talent.gel.more-elastic", species, 1,
                "更有弹性", "第二目标弹射伤害提高20%", 0, 0);
            AddNetwork("talent.gel.stick-then-bounce", species, 1,
                "黏住再弹", "短暂停留后追向更远的第二目标", 1, 1);
            AddNetwork("talent.gel.kickback", species, 1,
                "回弹一下", "第二目标命中后额外返还1点热度", 2, 2);

            AddNetwork("talent.gel.third-hop", species, 2,
                "第三跳", "附近有新目标时追加一次衰减弹射",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.gel.more-elastic");
            AddNetwork("talent.gel.quick-rebound", species, 2,
                "轻快回弹", "弹射速度提高15%",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.gel.more-elastic");
            AddNetwork("talent.gel.fat-projectile", species, 2,
                "胖弹道", "弹射体积和命中范围增大",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.gel.stick-then-bounce");
            AddNetwork("talent.gel.heavy-drop", species, 2,
                "沉甸甸", "弹射伤害提高15%，弹体略微增大",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.gel.stick-then-bounce");
            AddNetwork("talent.gel.soft-landing", species, 2,
                "软着陆", "没有第二目标时产生弱范围脉冲",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.gel.kickback");
            AddNetwork("talent.gel.elastic-return", species, 2,
                "弹性回收", "完成弹射后额外返还1点热度",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.gel.kickback");

            AddNetwork("talent.gel.echo-jump", species, 3,
                "借声起跳", "额外复响也能被改造成弱弹射",
                0, 0, SpiritTalentPrerequisiteMode.All,
                "talent.gel.third-hop",
                "talent.gel.quick-rebound");
            AddNetwork("talent.gel.rolling-impact", species, 3,
                "滚动冲击", "高速弹射提高后续命中伤害",
                0, 1, SpiritTalentPrerequisiteMode.All,
                "talent.gel.quick-rebound",
                "talent.gel.fat-projectile");
            AddNetwork("talent.gel.wrap-spark", species, 3,
                "裹住火星", "弹射命中后留下短暂燃烧胶点",
                1, 2, SpiritTalentPrerequisiteMode.All,
                "talent.gel.fat-projectile",
                "talent.gel.heavy-drop");
            AddNetwork("talent.gel.sticky-crater", species, 3,
                "黏性落坑", "强化软着陆，并留下短暂胶点",
                1, 3, SpiritTalentPrerequisiteMode.All,
                "talent.gel.heavy-drop",
                "talent.gel.soft-landing");
            AddNetwork("talent.gel.return-start", species, 3,
                "弹回起点", "没有新目标时可弱化弹回第一目标",
                2, 4, SpiritTalentPrerequisiteMode.All,
                "talent.gel.soft-landing",
                "talent.gel.elastic-return");
            AddNetwork("talent.gel.looping-hop", species, 3,
                "绕圈再跳", "回弹后允许再寻找一个新目标",
                2, 5, SpiritTalentPrerequisiteMode.All,
                "talent.gel.elastic-return",
                "talent.gel.third-hop");

            AddNetwork("talent.gel.pinball", species, 4,
                "满场乱弹", "最多5次不重复目标的衰减弹射",
                0, 0, SpiritTalentPrerequisiteMode.Any,
                "talent.gel.echo-jump",
                "talent.gel.rolling-impact",
                "talent.gel.looping-hop");
            AddNetwork("talent.gel.big-crater", species, 4,
                "砸个大坑", "取消后续弹射并强化第二次范围冲击",
                1, 1, SpiritTalentPrerequisiteMode.Any,
                "talent.gel.wrap-spark",
                "talent.gel.rolling-impact",
                "talent.gel.sticky-crater");
            AddNetwork("talent.gel.accelerating", species, 4,
                "越弹越快", "每次新目标提高弹速并减少伤害衰减",
                2, 2, SpiritTalentPrerequisiteMode.Any,
                "talent.gel.return-start",
                "talent.gel.sticky-crater",
                "talent.gel.looping-hop");
        }

        private static void Add(
            string id,
            StableConfigId species,
            int tier,
            string name,
            string description,
            string prerequisite = null)
        {
            var definition = new SpiritTalentDefinition(
                id,
                species,
                tier,
                name,
                description,
                prerequisite);
            ById.Add(definition.ConfigId, definition);
            if (!BySpecies.TryGetValue(species, out var definitions))
            {
                definitions = new List<SpiritTalentDefinition>();
                BySpecies.Add(species, definitions);
            }
            definitions.Add(definition);
        }

        private static void AddNetwork(
            string id,
            StableConfigId species,
            int tier,
            string name,
            string description,
            int branch,
            int visualIndex,
            SpiritTalentPrerequisiteMode prerequisiteMode =
                SpiritTalentPrerequisiteMode.All,
            params string[] prerequisites)
        {
            var definition = new SpiritTalentDefinition(
                id,
                species,
                tier,
                name,
                description,
                branch,
                visualIndex,
                prerequisiteMode,
                prerequisites);
            ById.Add(definition.ConfigId, definition);
            if (!BySpecies.TryGetValue(species, out var definitions))
            {
                definitions = new List<SpiritTalentDefinition>();
                BySpecies.Add(species, definitions);
            }
            definitions.Add(definition);
        }
    }

    public sealed class SpiritRunTalentState
    {
        private readonly HashSet<StableConfigId> _activeTalents = new();

        public SpiritInstanceState Spirit { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int UnspentPoints { get; private set; }
        public IReadOnlyCollection<StableConfigId> ActiveTalents
            => _activeTalents;
        public int ExperienceToNextPoint => Level + 2;

        public SpiritRunTalentState(SpiritInstanceState spirit)
        {
            Spirit = spirit ??
                throw new ArgumentNullException(nameof(spirit));
        }

        public int AddExperience(int amount)
        {
            if (amount <= 0)
                return 0;

            int gainedLevels = 0;
            Experience += amount;
            while (Experience >= ExperienceToNextPoint)
            {
                Experience -= ExperienceToNextPoint;
                Level++;
                UnspentPoints++;
                gainedLevels++;
            }
            return gainedLevels;
        }

        public SpiritTalentActivationResult TryActivate(
            StableConfigId talentId)
        {
            if (!StarterSpiritTalentCatalog.TryGet(
                    talentId,
                    out SpiritTalentDefinition definition))
            {
                return SpiritTalentActivationResult.UnknownTalent;
            }
            if (definition.SpeciesId !=
                Spirit.Identity.SpeciesConfigId)
            {
                return SpiritTalentActivationResult.WrongSpecies;
            }
            if (_activeTalents.Contains(talentId))
                return SpiritTalentActivationResult.AlreadyActive;
            if (UnspentPoints <= 0)
                return SpiritTalentActivationResult.NoPoint;
            if (definition.Tier > 1 &&
                !Spirit.IsTalentUnlocked(talentId))
            {
                return SpiritTalentActivationResult.PermanentlyLocked;
            }
            if (definition.Tier == 4 && HasActiveTier(4))
                return SpiritTalentActivationResult.ConflictingCapstone;
            if (!definition.ArePrerequisitesMet(_activeTalents))
            {
                return SpiritTalentActivationResult.MissingPrerequisite;
            }

            _activeTalents.Add(talentId);
            UnspentPoints--;
            return SpiritTalentActivationResult.Success;
        }

        private bool HasActiveTier(int tier)
        {
            foreach (StableConfigId activeId in _activeTalents)
            {
                if (StarterSpiritTalentCatalog.TryGet(
                        activeId,
                        out SpiritTalentDefinition active) &&
                    active.Tier == tier)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsActive(string talentId)
        {
            return _activeTalents.Contains(
                new StableConfigId(talentId));
        }

        public bool ArePrerequisitesMet(
            SpiritTalentDefinition definition)
        {
            return definition.ArePrerequisitesMet(_activeTalents);
        }

        public int RefundActiveTalents()
        {
            int refunded = _activeTalents.Count;
            if (refunded == 0)
                return 0;

            _activeTalents.Clear();
            UnspentPoints += refunded;
            return refunded;
        }

        public IReadOnlyList<SpiritTalentDefinition>
            GetAvailableChoices()
        {
            var choices = new List<SpiritTalentDefinition>();
            if (UnspentPoints <= 0)
                return choices;

            foreach (SpiritTalentDefinition definition
                     in StarterSpiritTalentCatalog.ForSpecies(
                         Spirit.Identity.SpeciesConfigId))
            {
                if (_activeTalents.Contains(definition.ConfigId) ||
                    definition.Tier > 1 &&
                    !Spirit.IsTalentUnlocked(definition.ConfigId) ||
                    definition.Tier == 4 &&
                    HasActiveTier(4) ||
                    !definition.ArePrerequisitesMet(_activeTalents))
                {
                    continue;
                }
                choices.Add(definition);
            }
            return choices;
        }

        public void ResetRun()
        {
            Level = 0;
            Experience = 0;
            UnspentPoints = 0;
            _activeTalents.Clear();
        }
    }
}
