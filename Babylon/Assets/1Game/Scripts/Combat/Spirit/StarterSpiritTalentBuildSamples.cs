using System;
using System.Collections.Generic;

namespace XianTu
{
    public readonly struct StarterSpiritTalentBuildSample
    {
        public string Name { get; }
        public StableConfigId SpeciesId { get; }
        public IReadOnlyList<string> TalentIds { get; }

        public StarterSpiritTalentBuildSample(
            string name,
            StableConfigId speciesId,
            params string[] talentIds)
        {
            Name = name ?? "";
            SpeciesId = speciesId;
            TalentIds = talentIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 九条5点纯路线基准。只用于验收树结构和调参，不替玩家自动点天赋。
    /// </summary>
    public static class StarterSpiritTalentBuildSamples
    {
        private static readonly StarterSpiritTalentBuildSample[] Samples =
        {
            new("火花狸·速燃",
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.quick-temper",
                "talent.spark.chasing-fire",
                "talent.spark.swift-spark",
                "talent.spark.fire-trail",
                "talent.spark.chain-burn"),
            new("火花狸·爆火",
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.bright-spark",
                "talent.spark.spark-shards",
                "talent.spark.compressed-flame",
                "talent.spark.echo-fed-heat",
                "talent.spark.burst-core"),
            new("火花狸·蓄热",
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                "talent.spark.banked-heat",
                "talent.spark.undying-core",
                "talent.spark.warm-return",
                "talent.spark.bounce-back",
                "talent.spark.carried-ember"),
            new("响响鸮·清响",
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.clear-echo",
                "talent.echo.duet",
                "talent.echo.sharp-tone",
                "talent.echo.finish-spark",
                "talent.echo.only-the-point"),
            new("响响鸮·远听",
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.half-beat-late",
                "talent.echo.echo-mark",
                "talent.echo.wide-wave",
                "talent.echo.threefold-confirm",
                "talent.echo.valley-chorus"),
            new("响响鸮·留声",
                FirstSpiritCircuitContent.EchoOwlSpecies,
                "talent.echo.remember-target",
                "talent.echo.long-hearing",
                "talent.echo.second-memory",
                "talent.echo.bounce-chamber",
                "talent.echo.saved-line"),
            new("弹弹胶·连弹",
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.more-elastic",
                "talent.gel.third-hop",
                "talent.gel.quick-rebound",
                "talent.gel.echo-jump",
                "talent.gel.pinball"),
            new("弹弹胶·重击",
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.stick-then-bounce",
                "talent.gel.fat-projectile",
                "talent.gel.heavy-drop",
                "talent.gel.wrap-spark",
                "talent.gel.big-crater"),
            new("弹弹胶·回弹",
                FirstSpiritCircuitContent.BounceGelSpecies,
                "talent.gel.kickback",
                "talent.gel.soft-landing",
                "talent.gel.elastic-return",
                "talent.gel.return-start",
                "talent.gel.accelerating")
        };

        public static IReadOnlyList<StarterSpiritTalentBuildSample> All =>
            Samples;
    }
}
