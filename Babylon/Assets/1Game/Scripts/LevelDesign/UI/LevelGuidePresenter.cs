using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XianTu.LevelDesign
{
    /// <summary>聚合关卡阶段、事件分配和选项后果，供 Tab 与基地情报碑显示。</summary>
    public static class LevelGuidePresenter
    {
        private static readonly int[] LevelAEventIDs = { 1004, 1005, 1007, 1006 };

        public enum FlowOptionState
        {
            Available,
            Unavailable,
            Selected,
        }

        public sealed class FlowOption
        {
            public string Title;
            public string Immediate;
            public string Night;
            public string StateLabel;
            public FlowOptionState State;
        }

        public sealed class FlowEvent
        {
            public string Name;
            public string Status;
            public bool IsAvailable;
            public bool IsCompleted;
            public readonly List<FlowOption> Options = new();
        }

        public static List<FlowEvent> BuildFlowData()
        {
            var result = new List<FlowEvent>();
            var edgar = MapProviders.Current as EdgarMapProvider;
            IReadOnlyDictionary<string, int> assignments =
                edgar?.AssignedRoomEvents ?? new Dictionary<string, int>();
            string storyId = StoryTemplateRuntime.Current?.ID;
            if (string.IsNullOrWhiteSpace(storyId))
                storyId = LevelAPhaseRuntime.CurrentStoryTemplateId;

            bool nightPhase = LevelAPhaseRuntime.IsNightMapActive
                              || LevelAPhaseRuntime.IsNightPending;
            int shards = PlayerResources.Instance != null
                ? PlayerResources.Instance.SpiritShards
                : 0;

            foreach (int eventID in LevelAEventIDs)
            {
                if (!ConfigDatabase.Instance.StoryEvents.TryGetValue(eventID, out var row)
                    || row?.Options == null)
                    continue;

                string node = ResolveEventNode(assignments, eventID);
                bool assigned = !string.IsNullOrWhiteSpace(node);
                bool mandatory = IsMandatoryEvent(eventID, storyId);
                bool completed = StoryEventService.Instance.IsCompletedInAct(eventID)
                                 || (assigned
                                     && LevelAPhaseRuntime.HasRecordedOutcome(node, eventID));
                bool dayEvent = eventID == 1004 || eventID == 1005;
                bool layoutEvent = eventID == 1004 || eventID == 1007;
                bool phaseMatches = dayEvent ? !nightPhase : nightPhase;
                bool eventAvailable = assigned && !completed && phaseMatches;
                string status = assigned
                    ? $"{node} · {(dayEvent ? "白昼" : "永夜")} " +
                      $"{(layoutEvent ? "Layout" : "Strength")}"
                    : "本局未出现";
                status += completed
                    ? " · 已处理"
                    : eventAvailable ? " · 可处理" : " · 不可处理";

                var flowEvent = new FlowEvent
                {
                    Name = row.Name_CN,
                    Status = status,
                    IsAvailable = eventAvailable,
                    IsCompleted = completed,
                };

                foreach (var option in row.Options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.Text))
                        continue;

                    bool selected = IsOptionSelected(completed, option);
                    bool affordable = option.CostID <= 0 || shards >= option.CostID;
                    FlowOptionState optionState = selected
                        ? FlowOptionState.Selected
                        : eventAvailable && affordable
                            ? FlowOptionState.Available
                            : FlowOptionState.Unavailable;

                    string stateLabel = optionState switch
                    {
                        FlowOptionState.Selected => "已选择",
                        FlowOptionState.Available => "当前可选",
                        _ when !assigned => "本局不可选",
                        _ when !phaseMatches => dayEvent
                            ? "仅白昼可处理"
                            : "仅永夜可处理",
                        _ when completed => "事件已处理",
                        _ when !affordable => $"灵力碎片不足（需 {option.CostID}）",
                        _ => "当前不可选",
                    };

                    flowEvent.Options.Add(new FlowOption
                    {
                        Title = option.Text,
                        Immediate = CompactImmediate(option),
                        Night = CompactNight(option, mandatory, assigned),
                        State = optionState,
                        StateLabel = stateLabel,
                    });
                }

                result.Add(flowEvent);
            }

            return result;
        }

        public static string BuildFlowSummary()
        {
            var edgar = MapProviders.Current as EdgarMapProvider;
            bool hasAssignments = edgar?.AssignedRoomEvents.Count > 0;
            string storyId = StoryTemplateRuntime.Current?.ID;
            if (string.IsNullOrWhiteSpace(storyId))
                storyId = LevelAPhaseRuntime.CurrentStoryTemplateId;
            string story = string.IsNullOrWhiteSpace(storyId)
                ? "尚未抽取"
                : storyId.Replace("Story_", "");
            return $"阶段：{ResolvePhase(hasAssignments)}　　主线：{story}";
        }

        public static string BuildGuideText()
        {
            var builder = new StringBuilder(2048);
            var edgar = MapProviders.Current as EdgarMapProvider;
            IReadOnlyDictionary<string, int> assignments =
                edgar?.AssignedRoomEvents ?? new Dictionary<string, int>();

            string phase = ResolvePhase(assignments.Count > 0);
            string storyId = StoryTemplateRuntime.Current?.ID;
            if (string.IsNullOrWhiteSpace(storyId))
                storyId = LevelAPhaseRuntime.CurrentStoryTemplateId;

            builder.Append("<color=#8de5ff>状态：</color>").Append(phase);
            if (!string.IsNullOrWhiteSpace(storyId))
                builder.Append("　<color=#8de5ff>主线：</color>").Append(storyId.Replace("Story_", ""));
            builder.Append('\n');

            var recap = LevelAPhaseRuntime.GetRecapLines();
            if (LevelAPhaseRuntime.IsNightPending && recap.Count > 0)
            {
                builder.Append("<color=#b8dfff>上次行动：</color>")
                    .Append(string.Join("；", recap))
                    .Append('\n');
            }

            builder.Append("<color=#d8c48f>每个阶段固定 1 个 Layout 与 1 个 Strength 事件。</color>\n");

            foreach (int eventID in LevelAEventIDs)
            {
                if (!ConfigDatabase.Instance.StoryEvents.TryGetValue(eventID, out var row)
                    || row?.Options == null)
                    continue;

                string node = ResolveEventNode(assignments, eventID);
                bool assigned = !string.IsNullOrWhiteSpace(node);
                bool mandatory = IsMandatoryEvent(eventID, storyId);
                bool completed = StoryEventService.Instance.IsCompletedInAct(eventID);

                builder.Append("\n<b><color=#ffd98a>")
                    .Append(row.Name_CN)
                    .Append("</color></b>");
                if (assigned)
                    builder.Append("　[").Append(node)
                        .Append(mandatory ? " · 主事件" : " · 辅助事件")
                        .Append(completed ? " · 已处理]" : " · 未处理]");
                else
                    builder.Append("　[可能出现]");
                builder.Append('\n');

                foreach (var option in row.Options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.Text))
                        continue;
                    builder.Append("• <b>").Append(option.Text).Append("</b>　现：")
                        .Append(StoryEventOptionFormatter.DescribeImmediate(option))
                        .Append("　夜：")
                        .Append(ShortenNight(StoryEventOptionFormatter.DescribeNight(option)))
                        .Append('\n');
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string ResolvePhase(bool hasAssignments)
        {
            if (LevelAPhaseRuntime.IsNightMapActive)
                return "永夜探索中";
            if (LevelAPhaseRuntime.IsNightPending)
                return "永夜待续 · 下次重新降落";
            return hasAssignments ? "白昼探索中" : "基地整备中 · 首次进入为白昼";
        }

        private static bool IsMandatoryEvent(int eventID, string storyId)
        {
            var definition = StoryTemplateRuntime.GetByMandatoryEvent(eventID);
            return definition != null
                   && !string.IsNullOrWhiteSpace(storyId)
                   && definition.ID == storyId;
        }

        private static string ShortenNight(string text)
        {
            return string.IsNullOrEmpty(text)
                ? "无"
                : text.Replace("；若为主事件，", "；主事件：")
                    .Replace("若为主事件，", "主事件：")
                    .Replace("保持默认", "默认");
        }

        private static string ResolveEventNode(
            IReadOnlyDictionary<string, int> assignments,
            int eventID)
        {
            if (MapProviders.Current is EdgarMapProvider edgar
                && edgar.TryGetEventNode(eventID, out string nodeName))
                return nodeName;
            return assignments.FirstOrDefault(x => x.Value == eventID).Key;
        }

        private static bool IsOptionSelected(bool completed, EventOption option)
        {
            if (!completed || option == null || string.IsNullOrWhiteSpace(option.FlagName))
                return false;
            string resolvedFlag = option.FlagName switch
            {
                "bridge_opened_pending" => "bridge_opened",
                "crown_light_disabled_pending" => "crown_light_disabled",
                "summon_array_destroyed_pending" => "summon_array_destroyed",
                _ => option.FlagName,
            };
            return BossFlagSet.Instance.GetValue(resolvedFlag) == option.FlagValue;
        }

        private static string CompactImmediate(EventOption option)
        {
            return StoryEventOptionFormatter.DescribeImmediate(option)
                .Replace("立即", "")
                .Replace("本局", "");
        }

        private static string CompactNight(EventOption option, bool mandatory, bool assigned)
        {
            string scene = option.FlagName switch
            {
                "bridge_opened_pending" => "清增援后，封藏室昼夜开放",
                "bridge_opened" => "巡礼桥昼夜开放",
                "bridge_sabotaged" => "白昼进封藏室，永夜坍塌",
                "bridge_kept_closed" => "放弃桥后封藏室",
                "crown_light_disabled_pending" => "清守光禁卫后禁用冠光",
                "crown_light_misaligned" => "冠光只锁定Boss脚下",
                "crown_light_intact" => "冠光继续追踪玩家",
                "summon_array_destroyed_pending" => "击败失控禁卫后禁用召唤",
                "summon_array_outer_broken" => "Boss 改召禁卫队长",
                "summon_array_intact" => "Boss 保留禁卫小队召唤",
                "night_lift_restored" => "升降井双向连接中庭",
                "night_lift_dropped" => "升降井单向返回中庭",
                "night_lift_sealed" => "升降井保持封锁",
                "route_opened" => "安全通路",
                "route_forced" => "破口警戒",
                "route_ignored" => "通路封锁",
                "facility_powered" => "设施支援",
                "facility_salvaged" => "设施失能",
                "facility_ignored" => "设施失能",
                "hazard_sealed" => "危险封存",
                "hazard_released" => "高压余波",
                "hazard_ignored" => "危险维持",
                _ => "无实体变化",
            };
            string boss = option.FlagName switch
            {
                "summon_array_destroyed_pending" => "Boss不再召唤",
                "summon_array_outer_broken" => "Boss召单精英",
                "summon_array_intact" => "Boss召普通小队",
                "crown_light_disabled_pending" => "摄政官失去冠光AOE",
                "crown_light_misaligned" => "冠光AOE不再追踪",
                "crown_light_intact" => "冠光AOE保持默认",
                "night_lift_restored" => "双向跨区捷径",
                "night_lift_dropped" => "单向返程捷径",
                "night_lift_sealed" => "标准路线",
                "route_forced" => "Boss攻+10% 移+5%",
                "facility_powered" => "Boss攻-10%",
                "facility_salvaged" => "Boss血+10%",
                "hazard_sealed" => "Boss血/攻-10%",
                "hazard_released" => "Boss血/攻+15% 移+5%",
                _ => "Boss默认",
            };

            if (mandatory)
                return $"{scene}；{boss}";
            return assigned ? scene : $"{scene}；主事件时 {boss}";
        }
    }
}
