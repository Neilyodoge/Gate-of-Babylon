using System.Collections.Generic;

namespace XianTu.LevelDesign
{
    /// <summary>事件选择 UI 与 Tab 关卡提示共用的玩家可读后果说明。</summary>
    public static class StoryEventOptionFormatter
    {
        public static string BuildChoiceLabel(EventOption option)
        {
            if (option == null)
                return string.Empty;

            var tags = BuildImmediateParts(option);
            string suffix = tags.Count > 0
                ? "    " + string.Join("  ·  ", tags)
                : "";
            return $"▸ {option.Text}{suffix}";
        }

        public static string DescribeImmediate(EventOption option)
        {
            if (option == null)
                return "无";
            var parts = BuildImmediateParts(option);
            return parts.Count > 0 ? string.Join("；", parts) : "无即时收益或代价";
        }

        public static string DescribeNight(EventOption option)
        {
            if (option == null)
                return "无";

            return option.FlagName switch
            {
                "bridge_opened" => "巡礼桥在白昼与永夜保持开放",
                "bridge_sabotaged" => "白昼临时通桥；永夜机构失效并坍塌",
                "bridge_kept_closed" => "巡礼桥保持封锁，标准主路不受影响",
                "summon_array_destroyed_pending" => "击败失控禁卫后，永夜 Boss 失去召唤机制",
                "summon_array_outer_broken" => "永夜 Boss 每次只召唤一名禁卫队长",
                "summon_array_intact" => "永夜 Boss 保留召唤普通禁卫小队的机制",
                "route_opened" => "保留安全通路；若为主事件，Boss 数值不变",
                "route_forced" => "保留破口并提高警戒；若为主事件，Boss 攻击+10%、移速+5%",
                "route_ignored" => "通路保持默认封锁状态，Boss 数值不变",
                "facility_powered" => "设施继续照明与支援；若为主事件，Boss 攻击-10%",
                "facility_salvaged" => "设施失去供能；若为主事件，Boss 生命+10%",
                "facility_ignored" => "设施维持失能，Boss 数值不变",
                "hazard_sealed" => "危险物保持封存；若为主事件，Boss 生命/攻击-10%",
                "hazard_released" => "追加高压余波；若为主事件，Boss 生命/攻击+15%、移速+5%",
                "hazard_ignored" => "危险物维持默认状态，Boss 数值不变",
                _ => option.SceneResult == EventSceneResult.None
                    ? "不产生跨阶段实体变化"
                    : $"在永夜保留“{DescribeSceneResult(option.SceneResult)}”结果",
            };
        }

        public static string DescribeReward(int rewardID)
        {
            return rewardID switch
            {
                0 => "",
                2001 => "获得1件随机材料",
                3001 => "获得2件随机材料",
                4001 => "本局攻击+10%",
                5001 => "本局攻击+20%",
                6001 => "本局攻击+20%",
                6002 => "本局攻击+10%、最大生命+20%",
                6003 => "本局技能伤害+15%",
                _ => $"奖励#{rewardID}",
            };
        }

        private static List<string> BuildImmediateParts(EventOption option)
        {
            var parts = new List<string>();
            string reward = DescribeReward(option.RewardID);
            if (!string.IsNullOrEmpty(reward))
                parts.Add(reward);
            if (option.CostID > 0)
                parts.Add($"消耗{option.CostID}灵力碎片");
            if (option.KarmaChange != 0)
                parts.Add($"因果{Signed(option.KarmaChange)}");
            if (option.DaoxinChange != 0)
                parts.Add($"道心{Signed(option.DaoxinChange)}");
            if (option.LifespanChange != 0)
                parts.Add($"寿元{Signed(option.LifespanChange)}");
            if (option.SceneResult != EventSceneResult.None)
                parts.Add(DescribeSceneResult(option.SceneResult));
            return parts;
        }

        private static string DescribeSceneResult(EventSceneResult result)
        {
            return result switch
            {
                EventSceneResult.OpenRoute => "立即开启通路",
                EventSceneResult.Power => "立即启动设施",
                EventSceneResult.Seal => "立即封存目标",
                EventSceneResult.BridgeSabotaged => "临时放下桥梁并破坏机构",
                EventSceneResult.SummonArrayDestroyed => "摧毁禁卫召集阵",
                EventSceneResult.SummonArrayOuterBroken => "破坏召集阵外环",
                _ => "场景发生变化",
            };
        }

        private static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString();
    }
}
