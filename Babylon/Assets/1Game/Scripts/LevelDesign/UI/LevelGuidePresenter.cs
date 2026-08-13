using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XianTu.LevelDesign
{
    /// <summary>聚合关卡阶段、事件分配和选项后果，供 Tab 与基地情报碑显示。</summary>
    public static class LevelGuidePresenter
    {
        private static readonly int[] LevelAEventIDs = { 1004, 1006 };

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

                string node = assignments.FirstOrDefault(x => x.Value == eventID).Key;
                bool assigned = !string.IsNullOrWhiteSpace(node);
                bool mandatory = IsMandatoryEvent(eventID, storyId);
                bool completed = StoryEventService.Instance.IsCompletedInAct(eventID)
                                 || (assigned
                                     && LevelAPhaseRuntime.HasRecordedOutcome(node, eventID));
                bool phaseMatches = eventID == 1004 ? !nightPhase : nightPhase;
                bool eventAvailable = assigned && !completed && phaseMatches;
                string status = assigned
                    ? $"{node} · {(eventID == 1004 ? "白昼 Layout" : "永夜 Strength")}"
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

                    bool selected = completed
                                    && !string.IsNullOrWhiteSpace(option.FlagName)
                                    && BossFlagSet.Instance.GetValue(option.FlagName) == option.FlagValue;
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
                        _ when !phaseMatches => eventID == 1004
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

            builder.Append("<color=#d8c48f>“现”立即生效；“夜”带入永夜。</color>\n");

            foreach (int eventID in LevelAEventIDs)
            {
                if (!ConfigDatabase.Instance.StoryEvents.TryGetValue(eventID, out var row)
                    || row?.Options == null)
                    continue;

                string node = assignments.FirstOrDefault(x => x.Value == eventID).Key;
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
                "bridge_opened" => "巡礼桥昼夜开放",
                "bridge_sabotaged" => "白昼临时通桥，永夜坍塌",
                "bridge_kept_closed" => "巡礼桥保持封锁",
                "summon_array_destroyed_pending" => "击败失控禁卫后禁用召唤",
                "summon_array_outer_broken" => "Boss 改召禁卫队长",
                "summon_array_intact" => "Boss 保留禁卫小队召唤",
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
