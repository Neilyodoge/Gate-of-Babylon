namespace XianTu
{
    /// <summary>
    /// 单宠三动作路径读取的天赋快照。只保存可执行参数，不拥有成长状态。
    /// </summary>
    public readonly struct StarterSpiritSingleTalentTuning
    {
        public int WeaponHitsPerActivation { get; }
        public int RetainedWeaponProgress { get; }
        public float DamageFactor { get; }
        public float RadiusBonus { get; }
        public float EchoDelay { get; }
        public bool EchoDuet { get; }
        public bool EchoRetarget { get; }
        public bool EchoMark { get; }
        public bool SparkShards { get; }
        public int GelTargetCount { get; }
        public float GelProjectileScale { get; }
        public float GelBounceDelay { get; }
        public bool GelSoftLanding { get; }
        public bool SparkFireTrail { get; }
        public bool SparkChainBurn { get; }
        public bool SparkBurstCore { get; }
        public bool SparkCarriedEmber { get; }
        public bool EchoValleyChorus { get; }
        public bool EchoOnlyPoint { get; }
        public bool EchoSavedLine { get; }
        public bool GelWrapSpark { get; }
        public bool GelReturnStart { get; }
        public bool GelPinball { get; }
        public bool GelBigCrater { get; }
        public bool GelAccelerating { get; }

        public StarterSpiritSingleTalentTuning(
            int weaponHitsPerActivation = 3,
            int retainedWeaponProgress = 0,
            float damageFactor = 1f,
            float radiusBonus = 0f,
            float echoDelay = 0.22f,
            bool echoDuet = false,
            bool echoRetarget = false,
            bool echoMark = false,
            bool sparkShards = false,
            int gelTargetCount = 1,
            float gelProjectileScale = 0.2f,
            float gelBounceDelay = 0f,
            bool gelSoftLanding = false,
            bool sparkFireTrail = false,
            bool sparkChainBurn = false,
            bool sparkBurstCore = false,
            bool sparkCarriedEmber = false,
            bool echoValleyChorus = false,
            bool echoOnlyPoint = false,
            bool echoSavedLine = false,
            bool gelWrapSpark = false,
            bool gelReturnStart = false,
            bool gelPinball = false,
            bool gelBigCrater = false,
            bool gelAccelerating = false)
        {
            WeaponHitsPerActivation =
                System.Math.Max(1, weaponHitsPerActivation);
            RetainedWeaponProgress = System.Math.Max(
                0,
                System.Math.Min(
                    WeaponHitsPerActivation - 1,
                    retainedWeaponProgress));
            DamageFactor = System.Math.Max(0f, damageFactor);
            RadiusBonus = System.Math.Max(0f, radiusBonus);
            EchoDelay = System.Math.Max(0f, echoDelay);
            EchoDuet = echoDuet;
            EchoRetarget = echoRetarget;
            EchoMark = echoMark;
            SparkShards = sparkShards;
            GelTargetCount = System.Math.Max(1, gelTargetCount);
            GelProjectileScale = System.Math.Max(
                0.05f,
                gelProjectileScale);
            GelBounceDelay = System.Math.Max(0f, gelBounceDelay);
            GelSoftLanding = gelSoftLanding;
            SparkFireTrail = sparkFireTrail;
            SparkChainBurn = sparkChainBurn;
            SparkBurstCore = sparkBurstCore;
            SparkCarriedEmber = sparkCarriedEmber;
            EchoValleyChorus = echoValleyChorus;
            EchoOnlyPoint = echoOnlyPoint;
            EchoSavedLine = echoSavedLine;
            GelWrapSpark = gelWrapSpark;
            GelReturnStart = gelReturnStart;
            GelPinball = gelPinball;
            GelBigCrater = gelBigCrater;
            GelAccelerating = gelAccelerating;
        }
    }

    public static class StarterSpiritSingleTalentTuningBuilder
    {
        public static StarterSpiritSingleTalentTuning Build(
            SpiritTalentRunController talents,
            StableConfigId species)
        {
            return Build(
                species,
                id => talents != null && talents.IsActive(species, id));
        }

        public static StarterSpiritSingleTalentTuning Build(
            StableConfigId species,
            System.Func<string, bool> isActive)
        {
            bool Active(string id) =>
                isActive != null && isActive(id);

            if (species == FirstSpiritCircuitContent.SparkRaccoonSpecies)
            {
                bool quick = Active("talent.spark.quick-temper");
                int retained = 0;
                if (Active("talent.spark.banked-heat"))
                    retained++;
                if (Active("talent.spark.undying-core"))
                    retained++;
                if (Active("talent.spark.warm-return"))
                    retained++;
                if (Active("talent.spark.ember-cache"))
                    retained++;
                if (Active("talent.spark.returning-flame"))
                    retained++;
                float damage = quick ? 0.85f : 1f;
                if (Active("talent.spark.bright-spark"))
                    damage *= 1.2f;
                if (Active("talent.spark.compressed-flame"))
                    damage *= 1.15f;
                if (Active("talent.spark.ember-cache"))
                    damage *= 1.1f;
                if (Active("talent.spark.echo-fed-heat"))
                    retained++;
                if (Active("talent.spark.bounce-back"))
                    retained++;
                float radiusBonus = 0f;
                if (Active("talent.spark.chasing-fire"))
                    radiusBonus += 1.2f;
                if (Active("talent.spark.swift-spark"))
                    radiusBonus += 0.6f;
                if (Active("talent.spark.rushing-shards"))
                    radiusBonus += 0.6f;
                if (Active("talent.spark.returning-flame"))
                    radiusBonus += 0.8f;
                return new StarterSpiritSingleTalentTuning(
                    weaponHitsPerActivation: quick ? 2 : 3,
                    retainedWeaponProgress: retained,
                    damageFactor: damage,
                    radiusBonus: radiusBonus,
                    sparkShards:
                        Active("talent.spark.spark-shards") ||
                        Active("talent.spark.rushing-shards"),
                    sparkFireTrail:
                        Active("talent.spark.fire-trail"),
                    sparkChainBurn:
                        Active("talent.spark.chain-burn"),
                    sparkBurstCore:
                        Active("talent.spark.burst-core"),
                    sparkCarriedEmber:
                        Active("talent.spark.carried-ember"));
            }

            if (species == FirstSpiritCircuitContent.EchoOwlSpecies)
            {
                float radiusBonus = 0f;
                if (Active("talent.echo.long-hearing"))
                    radiusBonus += 1.2f;
                if (Active("talent.echo.half-beat-late"))
                    radiusBonus += 0.4f;
                if (Active("talent.echo.wide-wave"))
                    radiusBonus += 0.8f;
                if (Active("talent.echo.second-memory"))
                    radiusBonus += 0.6f;
                if (Active("talent.echo.far-refrain"))
                    radiusBonus += 1f;
                float echoDamage =
                    Active("talent.echo.clear-echo") ? 1.15f : 1f;
                if (Active("talent.echo.sharp-tone"))
                    echoDamage *= 1.1f;
                if (Active("talent.echo.bounce-chamber"))
                    echoDamage *= 1.1f;
                return new StarterSpiritSingleTalentTuning(
                    damageFactor: echoDamage,
                    radiusBonus: radiusBonus,
                    echoDelay:
                        Active("talent.echo.half-beat-late") ? 0.35f : 0.22f,
                    echoDuet:
                        Active("talent.echo.duet") ||
                        Active("talent.echo.finish-spark") ||
                        Active("talent.echo.threefold-confirm") ||
                        Active("talent.echo.rising-chorus") ||
                        Active("talent.echo.remembered-duet"),
                    echoRetarget:
                        Active("talent.echo.remember-target") ||
                        Active("talent.echo.second-memory") ||
                        Active("talent.echo.remembered-duet"),
                    echoMark:
                        Active("talent.echo.echo-mark") ||
                        Active("talent.echo.rising-chorus") ||
                        Active("talent.echo.far-refrain"),
                    echoValleyChorus:
                        Active("talent.echo.valley-chorus"),
                    echoOnlyPoint:
                        Active("talent.echo.only-the-point"),
                    echoSavedLine:
                        Active("talent.echo.saved-line"));
            }

            int gelTargets =
                Active("talent.gel.third-hop") ? 3 : 1;
            if (Active("talent.gel.echo-jump"))
                gelTargets = System.Math.Max(gelTargets, 3);
            if (Active("talent.gel.looping-hop"))
                gelTargets = 4;
            if (Active("talent.gel.pinball"))
                gelTargets = 5;
            if (Active("talent.gel.big-crater"))
                gelTargets = 1;
            if (Active("talent.gel.accelerating"))
                gelTargets = System.Math.Max(gelTargets, 4);
            float gelDamage =
                Active("talent.gel.more-elastic") ? 1.2f : 1f;
            if (Active("talent.gel.heavy-drop"))
                gelDamage *= 1.15f;
            if (Active("talent.gel.rolling-impact"))
                gelDamage *= 1.1f;
            float gelRadius =
                Active("talent.gel.fat-projectile") ? 2f : 0f;
            if (Active("talent.gel.quick-rebound"))
                gelRadius += 0.5f;
            if (Active("talent.gel.sticky-crater"))
                gelRadius += 1f;
            float gelScale =
                Active("talent.gel.fat-projectile") ? 0.3f : 0.2f;
            if (Active("talent.gel.heavy-drop"))
                gelScale += 0.04f;
            return new StarterSpiritSingleTalentTuning(
                retainedWeaponProgress:
                    Active("talent.gel.kickback") ||
                    Active("talent.gel.elastic-return") ? 1 : 0,
                damageFactor: gelDamage,
                radiusBonus: gelRadius,
                gelTargetCount: gelTargets,
                gelProjectileScale: gelScale,
                gelBounceDelay:
                    Active("talent.gel.stick-then-bounce") ? 0.2f : 0f,
                gelSoftLanding:
                    Active("talent.gel.soft-landing") ||
                    Active("talent.gel.sticky-crater"),
                gelWrapSpark:
                    Active("talent.gel.wrap-spark") ||
                    Active("talent.gel.sticky-crater"),
                gelReturnStart:
                    Active("talent.gel.return-start") ||
                    Active("talent.gel.looping-hop"),
                gelPinball: Active("talent.gel.pinball"),
                gelBigCrater: Active("talent.gel.big-crater"),
                gelAccelerating:
                    Active("talent.gel.accelerating"));
        }
    }
}
