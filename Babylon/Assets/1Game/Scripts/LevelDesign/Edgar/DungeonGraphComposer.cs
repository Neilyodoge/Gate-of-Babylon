using System;
using System.Collections.Generic;
using System.Linq;
using Edgar.Unity;
using UnityEngine;

namespace XianTu.LevelDesign
{
    public sealed class DungeonInjectedRoomInfo
    {
        public string NodeName;
        public RoomRole Role;
        public int EventID;
        public string LandmarkLabel;
        public string InjectionID;
    }

    public sealed class DungeonBuildingAssignmentInfo
    {
        public string NodeName;
        public string PoolID;
        public string BuildingID;
        public string DisplayName;
        public GameObject RoomTemplate;
    }

    public sealed class DungeonGraphComposition : IDisposable
    {
        private readonly List<UnityEngine.Object> _ownedObjects = new();

        public LevelGraph Graph { get; internal set; }
        public Dictionary<string, DungeonInjectedRoomInfo> InjectedRooms { get; } =
            new(StringComparer.Ordinal);
        public Dictionary<string, DungeonBuildingAssignmentInfo> BuildingAssignments { get; } =
            new(StringComparer.Ordinal);

        internal void Own(UnityEngine.Object value)
        {
            if (value != null)
                _ownedObjects.Add(value);
        }

        public void Dispose()
        {
            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] == null)
                    continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_ownedObjects[i]);
                else
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
            }
            _ownedObjects.Clear();
            Graph = null;
            InjectedRooms.Clear();
            BuildingAssignments.Clear();
        }
    }

    public static class DungeonGraphComposer
    {
        public static DungeonGraphComposition Compose(
            LevelGraph source,
            DungeonGenerationProfile profile,
            int seed,
            string layoutID = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new DungeonGraphComposition { Graph = source };
            if (!HasRunnableTransform(profile, layoutID))
                return result;

            LevelGraph graph = CloneGraph(source, seed, result);
            result.Graph = graph;

            var random = new System.Random(unchecked(seed ^ 0x43A71F29));
            ApplyEdgeExpansions(
                graph,
                profile?.EdgeExpansions,
                random,
                result,
                layoutID);
            ApplyBuildingPools(
                graph,
                profile?.BuildingPools,
                random,
                result,
                layoutID);
            ApplyRoomInjections(graph, profile?.RoomInjections, random, result);
            return result;
        }

        private static bool HasRunnableTransform(
            DungeonGenerationProfile profile,
            string layoutID)
        {
            return profile != null
                   && ((profile.RoomInjections?.Any(CanRun) ?? false)
                       || (profile.EdgeExpansions?.Any(
                               rule => CanRun(rule)
                                       && MatchesLayout(rule.LayoutIDs, layoutID))
                           ?? false)
                       || (profile.BuildingPools?.Any(
                               rule => CanRun(rule)
                                       && MatchesLayout(rule.LayoutIDs, layoutID))
                           ?? false));
        }

        private static LevelGraph CloneGraph(
            LevelGraph source,
            int seed,
            DungeonGraphComposition result)
        {
            var graph = UnityEngine.Object.Instantiate(source);
            graph.name = $"{source.name}_Run_{seed}";
            graph.hideFlags = HideFlags.HideAndDontSave;
            result.Own(graph);

            var roomMap = new Dictionary<RoomBase, RoomBase>();
            graph.Rooms = new List<RoomBase>();
            foreach (RoomBase sourceRoom in source.Rooms)
            {
                if (sourceRoom == null)
                    continue;
                RoomBase room = UnityEngine.Object.Instantiate(sourceRoom);
                room.name = sourceRoom.name;
                room.hideFlags = HideFlags.HideAndDontSave;
                roomMap[sourceRoom] = room;
                graph.Rooms.Add(room);
                result.Own(room);
            }

            graph.Connections = new List<ConnectionBase>();
            foreach (ConnectionBase sourceConnection in source.Connections)
            {
                if (sourceConnection?.From == null || sourceConnection.To == null
                    || !roomMap.TryGetValue(sourceConnection.From, out RoomBase from)
                    || !roomMap.TryGetValue(sourceConnection.To, out RoomBase to))
                    continue;
                ConnectionBase connection =
                    UnityEngine.Object.Instantiate(sourceConnection);
                connection.name = sourceConnection.name;
                connection.hideFlags = HideFlags.HideAndDontSave;
                connection.From = from;
                connection.To = to;
                graph.Connections.Add(connection);
                result.Own(connection);
            }
            return graph;
        }

        private static void ApplyEdgeExpansions(
            LevelGraph graph,
            IReadOnlyList<DungeonEdgeExpansionRule> rules,
            System.Random random,
            DungeonGraphComposition result,
            string layoutID)
        {
            if (rules == null)
                return;
            foreach (DungeonEdgeExpansionRule rule in rules)
            {
                if (!CanRun(rule)
                    || !MatchesLayout(rule.LayoutIDs, layoutID)
                    || random.Next(10000) >= Mathf.Clamp(rule.Chance, 0, 10000))
                    continue;
                ConnectionBase replaced = graph.Connections.FirstOrDefault(
                    connection => connection != null
                                  && ((NodeName(connection.From) == rule.FromNodeName
                                       && NodeName(connection.To) == rule.ToNodeName)
                                      || (NodeName(connection.From) == rule.ToNodeName
                                          && NodeName(connection.To) == rule.FromNodeName)));
                if (replaced == null)
                {
                    Debug.LogWarning(
                        $"[地牢伸缩] {rule.ID} 找不到边 {rule.FromNodeName} ↔ {rule.ToNodeName}。");
                    continue;
                }

                int min = Mathf.Max(0, Mathf.Min(rule.MinRooms, rule.MaxRooms));
                int max = Mathf.Max(min, rule.MaxRooms);
                int count = random.Next(min, max + 1);
                if (count <= 0)
                    continue;

                RoomBase from = replaced.From;
                RoomBase to = replaced.To;
                graph.Connections.Remove(replaced);
                RoomBase previous = from;
                for (int i = 0; i < count; i++)
                {
                    string prefix = string.IsNullOrWhiteSpace(rule.NodeNamePrefix)
                        ? "Connector"
                        : rule.NodeNamePrefix.Trim();
                    string nodeName = MakeUniqueNodeName(
                        graph,
                        $"{prefix}_{rule.ID}_{i + 1}");
                    Room room = CreateRoom(
                        nodeName,
                        rule.RoomTemplates,
                        result);
                    graph.Rooms.Add(room);
                    graph.Connections.Add(
                        CreateConnection(previous, room, result));
                    previous = room;
                }
                graph.Connections.Add(CreateConnection(previous, to, result));
            }
        }

        private static void ApplyBuildingPools(
            LevelGraph graph,
            IReadOnlyList<DungeonBuildingPoolRule> rules,
            System.Random random,
            DungeonGraphComposition result,
            string layoutID)
        {
            if (rules == null)
                return;
            foreach (DungeonBuildingPoolRule rule in rules)
            {
                if (!CanRun(rule) || !MatchesLayout(rule.LayoutIDs, layoutID))
                    continue;
                var slots = new List<Room>();
                foreach (string slotName in rule.SlotNodeNames)
                {
                    Room slot = graph.Rooms.OfType<Room>().FirstOrDefault(
                        room => room.Name == slotName);
                    if (slot != null && !slots.Contains(slot))
                        slots.Add(slot);
                    else if (slot == null)
                        Debug.LogWarning(
                            $"[建筑池] {rule.ID} 找不到建筑槽节点 {slotName}。");
                }
                Shuffle(slots, random);

                var candidates = rule.Candidates
                    .Where(candidate => candidate != null
                                        && candidate.Weight > 0
                                        && candidate.RoomTemplate != null
                                        && IsPhaseAllowed(candidate.AllowedPhases))
                    .ToList();
                int count = Mathf.Min(
                    Mathf.Max(0, rule.SelectCount),
                    Mathf.Min(slots.Count, candidates.Count));
                for (int i = 0; i < count; i++)
                {
                    DungeonBuildingCandidate selected =
                        TakeWeightedCandidate(candidates, random);
                    if (selected == null)
                        break;
                    Room slot = slots[i];
                    slot.IndividualRoomTemplates = new List<GameObject>
                    {
                        selected.RoomTemplate,
                    };
                    result.BuildingAssignments[slot.Name] =
                        new DungeonBuildingAssignmentInfo
                        {
                            NodeName = slot.Name,
                            PoolID = rule.ID,
                            BuildingID = selected.ID,
                            DisplayName = selected.DisplayName,
                            RoomTemplate = selected.RoomTemplate,
                        };
                }
            }
        }

        private static void ApplyRoomInjections(
            LevelGraph graph,
            IReadOnlyList<DungeonRoomInjectionRule> rules,
            System.Random random,
            DungeonGraphComposition result)
        {
            if (rules == null)
                return;
            var usedAnchors = new HashSet<RoomBase>();
            foreach (var rule in rules)
            {
                if (!CanRun(rule) || random.Next(10000) >= Mathf.Clamp(rule.Chance, 0, 10000))
                    continue;

                int min = Mathf.Max(0, Mathf.Min(rule.MinCount, rule.MaxCount));
                int max = Mathf.Max(min, rule.MaxCount);
                int count = random.Next(min, max + 1);
                for (int i = 0; i < count; i++)
                {
                    List<RoomBase> anchors = graph.Rooms
                        .Where(room => room != null
                                       && !usedAnchors.Contains(room)
                                       && MatchesAnchor(room, rule)
                                       && HasSpareDoor(room, graph))
                        .ToList();
                    if (anchors.Count == 0)
                    {
                        Debug.LogWarning(
                            $"[地牢注入] {rule.ID} 找不到标签兼容且预留空闲门的锚点，本条规则停止注入。");
                        break;
                    }

                    RoomBase anchor = anchors[random.Next(anchors.Count)];
                    usedAnchors.Add(anchor);
                    string prefix = string.IsNullOrWhiteSpace(rule.NodeNamePrefix)
                        ? "X"
                        : rule.NodeNamePrefix.Trim();
                    string nodeName = MakeUniqueNodeName(graph, $"{prefix}_{rule.ID}_{i + 1}");

                    Room room = CreateRoom(nodeName, rule.RoomTemplates, result);
                    Connection connection = CreateConnection(anchor, room, result);

                    graph.Rooms.Add(room);
                    graph.Connections.Add(connection);
                    result.InjectedRooms[nodeName] = new DungeonInjectedRoomInfo
                    {
                        NodeName = nodeName,
                        Role = rule.Role,
                        EventID = rule.EventID,
                        LandmarkLabel = rule.LandmarkLabel,
                        InjectionID = rule.ID,
                    };
                }
            }
        }

        private static bool CanRun(DungeonRoomInjectionRule rule)
        {
            if (rule == null || !rule.Enabled || rule.RoomTemplates == null
                || !rule.RoomTemplates.Any(template => template != null))
                return false;
            return IsPhaseAllowed(rule.AllowedPhases);
        }

        private static bool CanRun(DungeonEdgeExpansionRule rule)
        {
            return rule != null
                   && rule.Enabled
                   && !string.IsNullOrWhiteSpace(rule.FromNodeName)
                   && !string.IsNullOrWhiteSpace(rule.ToNodeName)
                   && rule.MaxRooms > 0
                   && rule.RoomTemplates != null
                   && rule.RoomTemplates.Any(template => template != null)
                   && IsPhaseAllowed(rule.AllowedPhases);
        }

        private static bool CanRun(DungeonBuildingPoolRule rule)
        {
            return rule != null
                   && rule.Enabled
                   && rule.SelectCount > 0
                   && rule.SlotNodeNames != null
                   && rule.SlotNodeNames.Any(node => !string.IsNullOrWhiteSpace(node))
                   && rule.Candidates != null
                   && rule.Candidates.Any(candidate =>
                       candidate != null
                       && candidate.Weight > 0
                       && candidate.RoomTemplate != null
                       && IsPhaseAllowed(candidate.AllowedPhases))
                   && IsPhaseAllowed(rule.AllowedPhases);
        }

        private static bool IsPhaseAllowed(LevelPhaseMask allowed)
        {
            LevelPhaseMask current = LevelAPhaseRuntime.IsNightPending
                ? LevelPhaseMask.Night
                : LevelPhaseMask.Day;
            return (allowed & current) != 0;
        }

        private static bool MatchesLayout(
            IReadOnlyList<string> allowedLayoutIDs,
            string layoutID)
        {
            if (allowedLayoutIDs == null
                || allowedLayoutIDs.All(string.IsNullOrWhiteSpace))
                return true;
            if (string.IsNullOrWhiteSpace(layoutID))
                return false;
            return allowedLayoutIDs.Any(candidate =>
                string.Equals(
                    candidate?.Trim(),
                    layoutID.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        private static Room CreateRoom(
            string nodeName,
            IEnumerable<GameObject> templates,
            DungeonGraphComposition result)
        {
            var room = ScriptableObject.CreateInstance<Room>();
            room.name = nodeName;
            room.Name = nodeName;
            room.hideFlags = HideFlags.HideAndDontSave;
            room.IndividualRoomTemplates = templates
                .Where(template => template != null)
                .Distinct()
                .ToList();
            result.Own(room);
            return room;
        }

        private static Connection CreateConnection(
            RoomBase from,
            RoomBase to,
            DungeonGraphComposition result)
        {
            var connection = ScriptableObject.CreateInstance<Connection>();
            connection.name = $"{from.GetDisplayName()}_{to.GetDisplayName()}";
            connection.hideFlags = HideFlags.HideAndDontSave;
            connection.From = from;
            connection.To = to;
            result.Own(connection);
            return connection;
        }

        private static DungeonBuildingCandidate TakeWeightedCandidate(
            List<DungeonBuildingCandidate> candidates,
            System.Random random)
        {
            int totalWeight = candidates.Sum(candidate => Mathf.Max(0, candidate.Weight));
            if (totalWeight <= 0)
                return null;
            int roll = random.Next(totalWeight);
            for (int i = 0; i < candidates.Count; i++)
            {
                DungeonBuildingCandidate candidate = candidates[i];
                roll -= Mathf.Max(0, candidate.Weight);
                if (roll >= 0)
                    continue;
                candidates.RemoveAt(i);
                return candidate;
            }
            return null;
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (values[i], values[swap]) = (values[swap], values[i]);
            }
        }

        private static string NodeName(RoomBase room)
        {
            return room?.GetDisplayName() ?? string.Empty;
        }

        private static bool MatchesAnchor(RoomBase room, DungeonRoomInjectionRule rule)
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

            if (rule.RequiredAnchorTags != null)
                foreach (string required in rule.RequiredAnchorTags)
                    if (!string.IsNullOrWhiteSpace(required) && !tags.Contains(required.Trim()))
                        return false;
            if (rule.BlockedAnchorTags != null)
                foreach (string blocked in rule.BlockedAnchorTags)
                    if (!string.IsNullOrWhiteSpace(blocked) && tags.Contains(blocked.Trim()))
                        return false;
            return true;
        }

        private static bool HasSpareDoor(RoomBase room, LevelGraph graph)
        {
            int currentDegree = graph.Connections.Count(connection =>
                connection != null && (connection.From == room || connection.To == room));
            foreach (GameObject template in room.GetRoomTemplates())
            {
                if (template == null)
                    continue;
                int doorCount = template
                    .GetComponentsInChildren<DoorHandlerGrid3D>(true)
                    .Length;
                if (doorCount > currentDegree)
                    return true;
            }
            return false;
        }

        private static string MakeUniqueNodeName(LevelGraph graph, string requested)
        {
            string candidate = requested;
            int suffix = 2;
            while (graph.Rooms.OfType<Room>().Any(room => room.Name == candidate))
                candidate = $"{requested}_{suffix++}";
            return candidate;
        }
    }
}
