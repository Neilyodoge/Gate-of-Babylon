using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    public readonly struct StarterSpiritTalentPlaytestResult
    {
        public string BuildName { get; }
        public int TargetCount { get; }
        public float CircuitDamage { get; }
        public int CircuitResults { get; }
        public int DistinctMetrics { get; }
        public int AcceptedHits { get; }
        public int ResolvedCircuitHits { get; }
        public int SpawnedProjectiles { get; }
        public int FinalHeat { get; }

        public StarterSpiritTalentPlaytestResult(
            string buildName,
            int targetCount,
            float circuitDamage,
            int circuitResults,
            int distinctMetrics,
            int acceptedHits,
            int resolvedCircuitHits,
            int spawnedProjectiles,
            int finalHeat)
        {
            BuildName = buildName ?? "";
            TargetCount = targetCount;
            CircuitDamage = circuitDamage;
            CircuitResults = circuitResults;
            DistinctMetrics = distinctMetrics;
            AcceptedHits = acceptedHits;
            ResolvedCircuitHits = resolvedCircuitHits;
            SpawnedProjectiles = spawnedProjectiles;
            FinalHeat = finalHeat;
        }
    }

    /// <summary>
    /// 在真实MonoBehaviour、Physics、协程和伤害归因链上重复18次玩家命中。
    /// 这是开发期可重复采样，不替代人工操作手感测试。
    /// </summary>
    public sealed class StarterSpiritTalentCombatPlaytest :
        MonoBehaviour
    {
        private static readonly List<StarterSpiritTalentPlaytestResult>
            Results = new();

        public static bool IsRunning { get; private set; }
        public static IReadOnlyList<StarterSpiritTalentPlaytestResult>
            LastResults => Results;

        public static bool StartAll()
        {
            if (!Application.isPlaying || IsRunning)
                return false;

            var go = new GameObject("SpiritTalentCombatPlaytest");
            DontDestroyOnLoad(go);
            go.AddComponent<StarterSpiritTalentCombatPlaytest>();
            return true;
        }

        private IEnumerator Start()
        {
            IsRunning = true;
            Results.Clear();
            bool previousFlag = FeatureFlags.EnableCircuitRuntime;
            FeatureFlags.EnableCircuitRuntime = true;
            try
            {
                int[] targetCounts = { 1, 4 };
                foreach (int targetCount in targetCounts)
                {
                    yield return RunBuild(
                        new StarterSpiritTalentBuildSample(
                            "无天赋基准",
                            FirstSpiritCircuitContent.SparkRaccoonSpecies),
                        targetCount);
                    foreach (StarterSpiritTalentBuildSample build
                             in StarterSpiritTalentBuildSamples.All)
                    {
                        yield return RunBuild(build, targetCount);
                    }
                }
            }
            finally
            {
                FeatureFlags.EnableCircuitRuntime = previousFlag;
                IsRunning = false;
            }

            Debug.Log(
                $"<color=#F2B45E>[天赋BD采样] 完成 {Results.Count}/20 组采样。</color>");
            Destroy(gameObject);
        }

        private static IEnumerator RunBuild(
            StarterSpiritTalentBuildSample build,
            int targetCount)
        {
            const int directHits = 18;
            Vector3 origin = new(1000f, 0f, 1000f);
            SaveDataV1 save = BuildSave(
                build.SpeciesId,
                out Guid testedSpiritId);

            bool flag = FeatureFlags.EnableCircuitRuntime;
            FeatureFlags.EnableCircuitRuntime = false;
            var rig = new GameObject($"PlaytestRig_{build.Name}");
            rig.transform.position = origin;
            SpiritTalentRunController talents =
                rig.AddComponent<SpiritTalentRunController>();
            FirstSpiritCircuitController circuit =
                rig.AddComponent<FirstSpiritCircuitController>();
            FeatureFlags.EnableCircuitRuntime = flag;
            talents.TryConfigure(save);
            talents.GrantSharedExperience(20, "标准BD采样");
            foreach (string talentId in build.TalentIds)
            {
                SpiritTalentActivationResult activated =
                    talents.TryActivate(
                        testedSpiritId,
                        new StableConfigId(talentId));
                if (activated != SpiritTalentActivationResult.Success)
                {
                    Debug.LogError(
                        $"[天赋BD采样] {build.Name} 无法点亮 {talentId}：" +
                        activated);
                }
            }
            if (!circuit.TryConfigure(save))
            {
                Debug.LogError(
                    $"[天赋BD采样] {build.Name} 无法配置三宠回路。");
                Destroy(rig);
                yield break;
            }

            var targets = new List<StarterPrologueTrainingDummy>();
            for (int i = 0; i < targetCount; i++)
            {
                StarterPrologueTrainingDummy target =
                    StarterPrologueTrainingDummy.Spawn(
                        origin + new Vector3(2f + i * 1.8f, 0f, 0f),
                        i);
                target.Stats.maxHp = 10000f;
                target.Stats.currentHp = 10000f;
                targets.Add(target);
            }
            Physics.SyncTransforms();
            RunCombatStats.Reset();

            for (int hit = 0; hit < directHits; hit++)
            {
                GameObject target = targets[0].gameObject;
                var resolved = new GameEvents.PlayerDamageResolved
                {
                    Attacker = rig,
                    Target = target,
                    RequestedAmount = 10f,
                    AppliedAmount = 10f,
                    TargetRef =
                        LegacyCombatResultRecorder.BuildTarget(target),
                    IsPlayerOwnedDamage = true
                };
                circuit.RecordResolvedPlayerDamage(resolved);
                circuit.RecordCarrierHit(target);
                yield return new WaitForSeconds(0.08f);
            }
            yield return new WaitForSeconds(1.5f);

            float damage = 0f;
            int circuitResults = 0;
            var metrics = new HashSet<StableConfigId>();
            foreach (StructuredCombatResult result
                     in RunCombatStats.Results)
            {
                if (result.Source != CombatResultSource.Circuit ||
                    result.Outcome != CombatOutcomeKind.Damage)
                {
                    continue;
                }
                damage += result.AppliedAmount;
                circuitResults++;
                metrics.Add(result.MetricId);
            }
            var sample = new StarterSpiritTalentPlaytestResult(
                build.Name,
                targetCount,
                damage,
                circuitResults,
                metrics.Count,
                circuit.AcceptedHitCount,
                circuit.ResolvedCircuitHits,
                circuit.SpawnedProjectileCount,
                circuit.Runtime?.Heat ?? 0);
            Results.Add(sample);
            Debug.Log(
                $"[天赋BD采样] {sample.BuildName} | " +
                $"目标={sample.TargetCount} " +
                $"回路伤害={sample.CircuitDamage:0.##} " +
                $"结果={sample.CircuitResults} " +
                $"来源={sample.DistinctMetrics} " +
                $"命中={sample.AcceptedHits} " +
                $"回路命中={sample.ResolvedCircuitHits} " +
                $"投射物={sample.SpawnedProjectiles} " +
                $"余热={sample.FinalHeat}");

            foreach (StarterPrologueTrainingDummy target in targets)
            {
                if (target != null)
                    Destroy(target.gameObject);
            }
            Destroy(rig);
            yield return null;
        }

        private static SaveDataV1 BuildSave(
            StableConfigId testedSpecies,
            out Guid testedSpiritId)
        {
            StableConfigId[] species =
            {
                FirstSpiritCircuitContent.SparkRaccoonSpecies,
                FirstSpiritCircuitContent.EchoOwlSpecies,
                FirstSpiritCircuitContent.BounceGelSpecies
            };
            testedSpiritId = Guid.Empty;
            var save = new SaveDataV1();
            foreach (StableConfigId speciesId in species)
            {
                var spirit = new SpiritInstanceState(
                    new SpiritIdentity(
                        Guid.NewGuid(),
                        speciesId,
                        new StableConfigId("personality.playtest")));
                foreach (SpiritTalentDefinition definition
                         in StarterSpiritTalentCatalog.ForSpecies(speciesId))
                {
                    if (definition.Tier > 1)
                        spirit.UnlockTalent(definition.ConfigId);
                }
                save.spiritRoster.Add(SpiritSaveMapper.ToSave(spirit));
                save.activeSpiritInstanceGuids.Add(
                    spirit.Identity.InstanceId.ToString("N"));
                if (speciesId == testedSpecies)
                    testedSpiritId = spirit.Identity.InstanceId;
            }
            return save;
        }
    }
}
