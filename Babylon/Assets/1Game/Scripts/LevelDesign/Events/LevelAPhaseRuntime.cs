using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    public enum LevelAPhase
    {
        Day = 0,
        Night = 1,
    }

    /// <summary>无暮王城双阶段的最小持久状态入口。</summary>
    public static class LevelAPhaseRuntime
    {
        public const int MaxPendingOutcomes = 4;

        public static bool IsNightPending => State.phase == (int)LevelAPhase.Night
                                             && State.dayCompleted
                                             && State.runSeed > 0;
        public static bool IsNightMapActive { get; private set; }
        public static LevelAPhase CurrentPhase =>
            IsNightPending ? LevelAPhase.Night : LevelAPhase.Day;
        public static string CurrentStoryTemplateId =>
            StoryTemplateRuntime.Current?.ID ?? "Story_昼夜机关";

        private static LevelAProgressState State
        {
            get
            {
                var data = SaveSystem.Instance.Data;
                data.levelAProgress ??= new LevelAProgressState();
                data.levelAProgress.pendingOutcomes ??= new List<LevelAPendingOutcome>();
                return data.levelAProgress;
            }
        }

        public static void BeginNewDay(
            int runSeed,
            string spawnNode,
            string bossNode,
            string storyTemplateId)
        {
            SaveSystem.Instance.Data.levelAProgress = new LevelAProgressState
            {
                phase = (int)LevelAPhase.Day,
                dayCompleted = false,
                runSeed = runSeed,
                spawnNode = spawnNode ?? "",
                bossNode = bossNode ?? "",
                storyTemplateId = storyTemplateId ?? "",
                pendingOutcomes = new List<LevelAPendingOutcome>(),
            };
            IsNightMapActive = false;
            SaveSystem.Instance.Save();
        }

        public static bool TryGetMapVariant(
            out int runSeed,
            out string spawnNode,
            out string bossNode)
        {
            var state = State;
            runSeed = state.runSeed;
            spawnNode = state.spawnNode;
            bossNode = state.bossNode;
            return runSeed > 0
                   && !string.IsNullOrWhiteSpace(spawnNode)
                   && !string.IsNullOrWhiteSpace(bossNode);
        }

        public static void RecordOutcome(
            string nodeName,
            int eventId,
            EventOption option)
        {
            if (option == null)
                return;

            var outcomes = State.pendingOutcomes;
            int existing = outcomes.FindIndex(x => x != null && x.nodeName == nodeName);
            var outcome = new LevelAPendingOutcome
            {
                eventId = eventId,
                sceneResult = (int)option.SceneResult,
                nodeName = nodeName ?? "",
                flagName = option.FlagName ?? "",
                flagValue = option.FlagValue,
                recapText = BuildRecap(option),
            };

            if (existing >= 0)
                outcomes[existing] = outcome;
            else if (outcomes.Count < MaxPendingOutcomes)
                outcomes.Add(outcome);
            else
                Debug.LogWarning($"[无暮王城] 待落位结果已达 {MaxPendingOutcomes} 条，忽略事件 {eventId}。");

            SaveSystem.Instance.Save();
        }

        public static bool TryGetRecordedOutcome(
            string nodeName,
            int eventId,
            out LevelAPendingOutcome outcome)
        {
            outcome = State.pendingOutcomes.Find(x =>
                x != null
                && x.nodeName == nodeName
                && x.eventId == eventId);
            return outcome != null;
        }

        public static bool HasRecordedOutcome(string nodeName, int eventId) =>
            TryGetRecordedOutcome(nodeName, eventId, out _);

        public static void CommitNight()
        {
            var state = State;
            if (state.runSeed <= 0)
                throw new System.InvalidOperationException("提交永夜失败：白昼地图状态尚未建立。");

            state.phase = (int)LevelAPhase.Night;
            state.dayCompleted = true;
            SaveSystem.Instance.Save();
        }

        public static void RestorePendingFlags()
        {
            foreach (var outcome in State.pendingOutcomes)
            {
                if (outcome == null || string.IsNullOrWhiteSpace(outcome.flagName))
                    continue;
                BossFlagSet.Instance.Set(outcome.flagName, outcome.flagValue);
            }
        }

        public static IReadOnlyList<LevelAPendingOutcome> GetPendingOutcomes() =>
            State.pendingOutcomes;

        public static IReadOnlyList<string> GetRecapLines()
        {
            var lines = new List<string>();
            foreach (var outcome in State.pendingOutcomes)
            {
                if (outcome == null
                    || (outcome.eventId != 1004 && outcome.eventId != 1005)
                    || string.IsNullOrWhiteSpace(outcome.recapText))
                    continue;
                lines.Add(outcome.recapText);
            }
            return lines;
        }

        public static string BuildEntrySummary()
        {
            var lines = GetRecapLines();
            if (lines.Count == 0)
                return "无暮王城当前处于永夜";

            return $"无暮王城当前处于永夜 · {string.Join("；", lines)}";
        }

        public static void SetNightMapActive(bool active)
        {
            IsNightMapActive = active && IsNightPending;
        }

        public static void ResetAfterNightVictory()
        {
            SaveSystem.Instance.Data.levelAProgress = new LevelAProgressState();
            IsNightMapActive = false;
            SaveSystem.Instance.Save();
        }

        private static string BuildRecap(EventOption option)
        {
            return option.FlagName switch
            {
                "route_opened" => "通路已稳定开启",
                "route_forced" => "封锁通路已被强拆",
                "facility_powered" => "失能设施已重新供能",
                "facility_salvaged" => "设施核心已被取走",
                "hazard_sealed" => "失控危险物已完成封存",
                "hazard_released" => "危险物的力量已被释放",
                "bridge_opened" => "巡礼桥已稳定开启",
                "bridge_sabotaged" => "巡礼桥机构已被破坏",
                "bridge_kept_closed" => "巡礼桥保持封锁",
                "crown_light_disabled" => "冠光仪主镜已被摧毁",
                "crown_light_misaligned" => "冠光仪镜组已被偏转",
                "crown_light_intact" => "冠光仪保持完整",
                "summon_array_destroyed" => "禁卫召集阵已被摧毁",
                "summon_array_outer_broken" => "召集阵外环已被破坏",
                "summon_array_intact" => "禁卫召集阵保持完整",
                "night_lift_restored" => "狱城升降井已恢复双向运行",
                "night_lift_dropped" => "升降井已坠落并形成单向捷径",
                "night_lift_sealed" => "狱城升降井保持封锁",
                _ => string.IsNullOrWhiteSpace(option.Text)
                    ? "上次行动已影响永夜"
                    : option.Text,
            };
        }
    }
}
