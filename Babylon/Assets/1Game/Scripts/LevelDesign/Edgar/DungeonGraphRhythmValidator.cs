using System;
using System.Collections.Generic;
using System.Linq;
using Edgar.Unity;
using UnityEngine;

namespace XianTu.LevelDesign
{
    public enum DungeonValidationSeverity
    {
        Info,
        Warning,
        Error,
    }

    public readonly struct DungeonValidationMessage
    {
        public readonly DungeonValidationSeverity Severity;
        public readonly string Text;

        public DungeonValidationMessage(
            DungeonValidationSeverity severity,
            string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    public sealed class DungeonGraphValidationReport
    {
        public readonly List<DungeonValidationMessage> Messages = new();
        public bool IsValid => Messages.All(
            message => message.Severity != DungeonValidationSeverity.Error);

        public void Add(DungeonValidationSeverity severity, string text)
        {
            Messages.Add(new DungeonValidationMessage(severity, text));
        }
    }

    public static class DungeonGraphRhythmValidator
    {
        private static readonly string[] LandmarkTags =
        {
            "Landmark", "Archive", "OptionalAnnex", "Ritual", "Barracks", "Transit",
        };

        private static readonly string[] RewardTags =
        {
            "Combat", "Event", "Elite", "Shop", "Boss", "Archive", "OptionalAnnex", "Treasure",
        };

        public static DungeonGraphValidationReport Validate(
            LevelGraph graph,
            string startNodeName,
            string bossNodeName,
            DungeonRhythmValidationSettings settings,
            IReadOnlyList<DungeonShortcutRule> shortcuts = null)
        {
            var report = new DungeonGraphValidationReport();
            if (graph == null)
            {
                report.Add(DungeonValidationSeverity.Error, "没有指定 Edgar 关卡图。");
                return report;
            }
            settings ??= new DungeonRhythmValidationSettings();

            List<RoomBase> rooms = graph.Rooms.Where(room => room != null).ToList();
            var adjacency = BuildAdjacency(rooms, graph.Connections);
            RoomBase start = FindRoom(rooms, startNodeName);
            RoomBase boss = FindRoom(rooms, bossNodeName);
            if (start == null)
                report.Add(DungeonValidationSeverity.Error, $"找不到起点节点：{startNodeName}。");
            if (boss == null)
                report.Add(DungeonValidationSeverity.Error, $"找不到首领节点：{bossNodeName}。");
            if (start == null || boss == null)
                return report;

            Dictionary<RoomBase, RoomBase> parents = BreadthFirst(start, adjacency);
            if (settings.RequireConnectedGraph && parents.Count != rooms.Count)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"关卡图未完全连通：可达 {parents.Count}/{rooms.Count} 个节点。");
            if (!parents.ContainsKey(boss))
            {
                report.Add(DungeonValidationSeverity.Error, "降落点无法到达首领房。");
                return report;
            }

            List<RoomBase> criticalPath = BuildPath(start, boss, parents);
            int bossDepth = Mathf.Max(0, criticalPath.Count - 1);
            if (bossDepth < settings.MinBossDepth || bossDepth > settings.MaxBossDepth)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"首领路径深度为 {bossDepth} 条连接，应在 {settings.MinBossDepth}～{settings.MaxBossDepth}。");
            else
                report.Add(DungeonValidationSeverity.Info, $"首领路径深度：{bossDepth} 条连接。");

            int consecutiveCombat = MaxConsecutiveCombat(criticalPath);
            if (consecutiveCombat > settings.MaxConsecutiveCombatRooms)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"主路线连续战斗房达到 {consecutiveCombat} 间，上限为 {settings.MaxConsecutiveCombatRooms} 间。");
            else
                report.Add(
                    DungeonValidationSeverity.Info,
                    $"主路线最多连续战斗房：{consecutiveCombat} 间。");

            int eventCount = rooms.Count(room => GetTags(room).Contains("Event"));
            if (eventCount < settings.MinEventRooms || eventCount > settings.MaxEventRooms)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"事件房数量为 {eventCount}，应在 {settings.MinEventRooms}～{settings.MaxEventRooms}。");

            int landmarkCount = rooms.Count(
                room => GetTags(room).Overlaps(LandmarkTags));
            if (landmarkCount < settings.MinLandmarkRooms)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"地标房数量为 {landmarkCount}，至少需要 {settings.MinLandmarkRooms}。");

            int unrewardedDeadEnds = rooms.Count(room =>
                room != start
                && room != boss
                && adjacency[room].Count <= 1
                && !GetTags(room).Overlaps(RewardTags));
            if (unrewardedDeadEnds > settings.MaxUnrewardedDeadEnds)
                report.Add(
                    DungeonValidationSeverity.Error,
                    $"无奖励死胡同有 {unrewardedDeadEnds} 个，上限为 {settings.MaxUnrewardedDeadEnds}。");

            ValidateShortcuts(
                rooms,
                adjacency,
                shortcuts,
                settings.MinShortcutSavedEdges,
                report);
            if (report.IsValid)
                report.Add(DungeonValidationSeverity.Info, "图结构与节奏规则通过。");
            return report;
        }

        private static Dictionary<RoomBase, List<RoomBase>> BuildAdjacency(
            IReadOnlyList<RoomBase> rooms,
            IEnumerable<ConnectionBase> connections)
        {
            var result = rooms.ToDictionary(room => room, _ => new List<RoomBase>());
            foreach (ConnectionBase connection in connections)
            {
                if (connection?.From == null || connection.To == null
                    || !result.ContainsKey(connection.From)
                    || !result.ContainsKey(connection.To))
                    continue;
                result[connection.From].Add(connection.To);
                result[connection.To].Add(connection.From);
            }
            return result;
        }

        private static RoomBase FindRoom(IEnumerable<RoomBase> rooms, string name)
        {
            return rooms.FirstOrDefault(
                room => string.Equals(
                    room.GetDisplayName(),
                    name,
                    StringComparison.Ordinal));
        }

        private static Dictionary<RoomBase, RoomBase> BreadthFirst(
            RoomBase start,
            IReadOnlyDictionary<RoomBase, List<RoomBase>> adjacency)
        {
            var parent = new Dictionary<RoomBase, RoomBase> { [start] = null };
            var queue = new Queue<RoomBase>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                RoomBase current = queue.Dequeue();
                foreach (RoomBase next in adjacency[current])
                {
                    if (parent.ContainsKey(next))
                        continue;
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
            return parent;
        }

        private static List<RoomBase> BuildPath(
            RoomBase start,
            RoomBase target,
            IReadOnlyDictionary<RoomBase, RoomBase> parents)
        {
            var path = new List<RoomBase>();
            for (RoomBase current = target; current != null; current = parents[current])
            {
                path.Add(current);
                if (current == start)
                    break;
            }
            path.Reverse();
            return path;
        }

        private static int MaxConsecutiveCombat(IEnumerable<RoomBase> path)
        {
            int current = 0;
            int maximum = 0;
            foreach (RoomBase room in path)
            {
                HashSet<string> tags = GetTags(room);
                if (tags.Contains("Combat") || tags.Contains("Elite") || tags.Contains("Boss"))
                {
                    current++;
                    maximum = Mathf.Max(maximum, current);
                }
                else
                {
                    current = 0;
                }
            }
            return maximum;
        }

        private static HashSet<string> GetTags(RoomBase room)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GameObject template in room.GetRoomTemplates())
            {
                DungeonRoomAuthoring authoring =
                    template != null ? template.GetComponent<DungeonRoomAuthoring>() : null;
                if (authoring?.RoomTags == null)
                    continue;
                foreach (string tag in authoring.RoomTags)
                    if (!string.IsNullOrWhiteSpace(tag))
                        tags.Add(tag.Trim());
            }
            return tags;
        }

        private static void ValidateShortcuts(
            IReadOnlyList<RoomBase> rooms,
            IReadOnlyDictionary<RoomBase, List<RoomBase>> adjacency,
            IReadOnlyList<DungeonShortcutRule> shortcuts,
            int minimumSavedEdges,
            DungeonGraphValidationReport report)
        {
            if (shortcuts == null)
                return;
            foreach (DungeonShortcutRule shortcut in shortcuts)
            {
                if (shortcut == null || !shortcut.Enabled)
                    continue;
                RoomBase source = FindRoom(rooms, shortcut.SourceNodeName);
                RoomBase target = FindRoom(rooms, shortcut.TargetNodeName);
                if (source == null || target == null)
                {
                    report.Add(
                        DungeonValidationSeverity.Warning,
                        $"捷径 {shortcut.ID} 的节点不在此布局中，运行时会跳过。");
                    continue;
                }
                Dictionary<RoomBase, RoomBase> parents = BreadthFirst(source, adjacency);
                if (!parents.ContainsKey(target))
                    continue;
                int distance = BuildPath(source, target, parents).Count - 1;
                int saved = Mathf.Max(0, distance - 1);
                if (saved < minimumSavedEdges)
                    report.Add(
                        DungeonValidationSeverity.Warning,
                    $"捷径 {shortcut.ID} 只节省 {saved} 条房间连接，低于要求的 {minimumSavedEdges} 条。");
            }
        }
    }
}
