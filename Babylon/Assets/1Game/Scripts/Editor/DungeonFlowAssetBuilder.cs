#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Edgar.Unity;
using UnityEditor;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu.Editor
{
    public static class DungeonFlowAssetBuilder
    {
        private const string Root =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D";
        private const string FlowRoot = Root + "/Flows";
        private const string BuildingRoot =
            Root + "/RoomTemplates/FlowBuildings";
        private const string FlowRoomRoot =
            Root + "/RoomTemplates/FlowRooms";
        private const string ProfilePath = Root + "/地牢生成总控.asset";
        private const string DefaultGraphPath = Root + "/WhiteboxLevelGraph.asset";
        private const string GeneratedRooms =
            Root + "/RoomTemplates/Generated/Rooms";

        private sealed class FlowSpec
        {
            public string ID;
            public string DisplayName;
            public (string From, string To)[] Edges;
            public Dictionary<string, Vector2> Positions;
            public Dictionary<string, string> TemplateOverrides = new();
        }

        [MenuItem("仙途秘境/关卡工具/生成多 Flow 关卡图", false, 302)]
        public static void Build()
        {
            EnsureFolder(FlowRoot);
            EnsureFolder(BuildingRoot);
            EnsureFolder(FlowRoomRoot);
            LevelGraph source =
                AssetDatabase.LoadAssetAtPath<LevelGraph>(DefaultGraphPath);
            if (source == null)
                throw new InvalidOperationException(
                    $"缺少基础 Edgar 图：{DefaultGraphPath}");

            Dictionary<string, GameObject> flowRooms = BuildFlowRoomPrefabs();
            ApplyBaseFlowOverrides(source, flowRooms);
            Dictionary<string, GameObject> templates = LoadNodeTemplates(source);
            foreach (var pair in flowRooms)
                templates[pair.Key] = pair.Value;
            Dictionary<string, GameObject> buildings = BuildBuildingPrefabs();
            var generated = new Dictionary<string, LevelGraph>
            {
                ["Layout_A"] = source,
            };
            foreach (FlowSpec spec in CreateFlowSpecs())
                generated[spec.ID] = BuildFlow(spec, source, templates);

            ConfigureProfile(generated, buildings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            DungeonGenerationProfile.ClearCache();
            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<DungeonGenerationProfile>(ProfilePath);
            Debug.Log(
                "<color=#66ff99>[多 Flow]</color> 已生成 Layout_B/C/D，" +
                "并登记 4 选 3 建筑池与 38% 连接段伸缩规则。");
        }

        private static IEnumerable<FlowSpec> CreateFlowSpecs()
        {
            yield return new FlowSpec
            {
                ID = "Layout_B",
                DisplayName = "中央辐射",
                Positions = Positions(
                    ("O4", 0, 0), ("O3", 140, 0), ("O0", 280, 0),
                    ("O1", 280, -130), ("B0", 280, -250),
                    ("I0", 500, 0), ("I1", 500, -140), ("I2", 500, 140),
                    ("C1", 650, 0), ("C0", 790, 0), ("O2", 920, 0),
                    ("I3", 1060, 0), ("I4", 1200, 0)),
                Edges = new[]
                {
                    ("O4", "O3"), ("O3", "O0"), ("O0", "O1"), ("O1", "B0"),
                    ("O0", "I0"), ("I0", "I1"), ("I0", "I2"), ("I0", "C1"),
                    ("C1", "C0"), ("C0", "O2"), ("O2", "I3"), ("I3", "I4"),
                },
            };
            yield return new FlowSpec
            {
                ID = "Layout_C",
                DisplayName = "大环寻宝",
                Positions = Positions(
                    ("O4", 0, 0), ("O3", 130, 0), ("O0", 260, 0),
                    ("O1", 260, -130), ("B0", 260, -250),
                    ("O2", 430, -100), ("C0", 600, -120), ("C1", 760, -80),
                    ("I0", 900, 0), ("I1", 900, -150), ("I2", 900, 150),
                    ("I3", 1040, 0), ("I4", 1180, 0)),
                TemplateOverrides = new Dictionary<string, string>
                {
                    ["O2"] = "WB_Flow_Hub4",
                    ["C1"] = "WB_Flow_Hub4",
                },
                Edges = new[]
                {
                    ("O4", "O3"), ("O3", "O0"), ("O0", "O1"), ("O1", "B0"),
                    ("O0", "O2"), ("O2", "C0"), ("C0", "C1"), ("C1", "I0"),
                    ("C1", "I1"), ("I0", "I2"), ("I0", "I3"), ("I3", "I4"),
                },
            };
            yield return new FlowSpec
            {
                ID = "Layout_D",
                DisplayName = "商店必经",
                Positions = Positions(
                    ("O4", 0, 100), ("O3", 130, 70), ("O0", 260, 30),
                    ("O2", 260, -120), ("O1", 420, -120), ("B0", 560, -120),
                    ("C0", 430, 30), ("C1", 590, 30), ("I0", 750, 30),
                    ("I1", 750, -120), ("I2", 750, 170),
                    ("I3", 920, 30), ("I4", 1080, 30)),
                Edges = new[]
                {
                    ("O4", "O3"), ("O3", "O0"), ("O0", "O2"), ("O2", "O1"),
                    ("O1", "B0"), ("O0", "C0"), ("C0", "C1"), ("C1", "I0"),
                    ("I0", "I1"), ("I0", "I2"), ("I0", "I3"), ("I3", "I4"),
                },
            };
        }

        private static LevelGraph BuildFlow(
            FlowSpec spec,
            LevelGraph source,
            IReadOnlyDictionary<string, GameObject> templates)
        {
            string path = $"{FlowRoot}/{spec.ID}.asset";
            if (AssetDatabase.LoadAssetAtPath<LevelGraph>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var graph = ScriptableObject.CreateInstance<LevelGraph>();
            graph.name = spec.ID;
            graph.RoomType = typeof(Room).FullName;
            graph.ConnectionType = typeof(Connection).FullName;
            graph.IsDirected = false;
            foreach (GameObject corridor in source.CorridorIndividualRoomTemplates)
                if (corridor != null)
                    graph.CorridorIndividualRoomTemplates.Add(corridor);
            AssetDatabase.CreateAsset(graph, path);

            var nodes = new Dictionary<string, Room>();
            foreach (string nodeName in spec.Positions.Keys)
            {
                string templateName = spec.TemplateOverrides.TryGetValue(
                    nodeName,
                    out string replacement)
                    ? replacement
                    : nodeName;
                if (!templates.TryGetValue(templateName, out GameObject template))
                    throw new InvalidOperationException(
                        $"{spec.ID} 缺少节点模板：{nodeName}/{templateName}");
                var room = ScriptableObject.CreateInstance<Room>();
                room.name = nodeName;
                room.Name = nodeName;
                room.Position = spec.Positions[nodeName];
                room.IndividualRoomTemplates.Add(template);
                room.hideFlags = HideFlags.HideInHierarchy;
                graph.Rooms.Add(room);
                nodes[nodeName] = room;
                AssetDatabase.AddObjectToAsset(room, graph);
            }
            foreach ((string from, string to) in spec.Edges)
            {
                var connection = ScriptableObject.CreateInstance<Connection>();
                connection.name = $"{from}_{to}";
                connection.From = nodes[from];
                connection.To = nodes[to];
                connection.hideFlags = HideFlags.HideInHierarchy;
                graph.Connections.Add(connection);
                AssetDatabase.AddObjectToAsset(connection, graph);
            }
            EditorUtility.SetDirty(graph);
            return graph;
        }

        private static Dictionary<string, GameObject> BuildFlowRoomPrefabs()
        {
            return new Dictionary<string, GameObject>
            {
                ["WB_Flow_Hub3"] = BuildTaggedPrefab(
                    $"{GeneratedRooms}/WB_Outer_Endpoint.prefab",
                    $"{FlowRoomRoot}/WB_Flow_Hub3.prefab",
                    "Hub", "Landmark", "Junction"),
                ["WB_Flow_Hub4"] = BuildTaggedPrefab(
                    $"{GeneratedRooms}/WB_Inner_Battle.prefab",
                    $"{FlowRoomRoot}/WB_Flow_Hub4.prefab",
                    "Hub", "Connector", "Junction"),
                ["WB_Flow_Connector2"] = BuildTaggedPrefab(
                    $"{GeneratedRooms}/WB_Transition_Battle.prefab",
                    $"{FlowRoomRoot}/WB_Flow_Connector2.prefab",
                    "Connector", "Breather", "Transition"),
            };
        }

        private static void ApplyBaseFlowOverrides(
            LevelGraph graph,
            IReadOnlyDictionary<string, GameObject> flowRooms)
        {
            foreach (Room room in graph.Rooms.OfType<Room>())
            {
                GameObject replacement = room.Name switch
                {
                    "O0" => flowRooms["WB_Flow_Hub3"],
                    "C1" => flowRooms["WB_Flow_Connector2"],
                    _ => null,
                };
                if (replacement == null)
                    continue;
                room.IndividualRoomTemplates = new List<GameObject> { replacement };
                EditorUtility.SetDirty(room);
            }
            EditorUtility.SetDirty(graph);
        }

        private static Dictionary<string, GameObject> LoadNodeTemplates(
            LevelGraph source)
        {
            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (Room room in source.Rooms.OfType<Room>())
            {
                GameObject template = room.IndividualRoomTemplates.FirstOrDefault(
                    value => value != null);
                if (template != null)
                    result[room.Name] = template;
            }
            result["WB_Inner_Battle"] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{GeneratedRooms}/WB_Inner_Battle.prefab");
            return result;
        }

        private static Dictionary<string, GameObject> BuildBuildingPrefabs()
        {
            string source = $"{GeneratedRooms}/WB_Outer_Event.prefab";
            var definitions = new[]
            {
                (ID: "Archive", Name: "王城档案馆", Tags: new[] { "Building", "Landmark", "Archive", "Event" }),
                (ID: "Ritual", Name: "冠光观星台", Tags: new[] { "Building", "Landmark", "Ritual", "Event" }),
                (ID: "Barracks", Name: "禁卫兵营", Tags: new[] { "Building", "Landmark", "Barracks", "Combat", "Event" }),
                (ID: "Transit", Name: "狱城转运院", Tags: new[] { "Building", "Landmark", "Transit", "Event" }),
            };
            var result = new Dictionary<string, GameObject>();
            foreach (var definition in definitions)
            {
                string path = $"{BuildingRoot}/WB_Building_{definition.ID}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null
                    && !AssetDatabase.CopyAsset(source, path))
                    throw new InvalidOperationException($"无法创建建筑模板：{path}");
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name = $"WB_Building_{definition.ID}";
                    DungeonRoomAuthoring authoring =
                        root.GetComponent<DungeonRoomAuthoring>();
                    if (authoring == null)
                        throw new InvalidOperationException(
                            $"{path} 缺少 DungeonRoomAuthoring。");
                    var serialized = new SerializedObject(authoring);
                    SerializedProperty tags = serialized.FindProperty("roomTags");
                    tags.arraySize = definition.Tags.Length;
                    for (int i = 0; i < definition.Tags.Length; i++)
                        tags.GetArrayElementAtIndex(i).stringValue = definition.Tags[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                result[definition.ID] =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return result;
        }

        private static GameObject BuildTaggedPrefab(
            string source,
            string path,
            params string[] roomTags)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null
                && !AssetDatabase.CopyAsset(source, path))
                throw new InvalidOperationException($"无法创建 Flow 房间模板：{path}");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(path);
                DungeonRoomAuthoring authoring =
                    root.GetComponent<DungeonRoomAuthoring>();
                if (authoring == null)
                    throw new InvalidOperationException(
                        $"{path} 缺少 DungeonRoomAuthoring。");
                var serialized = new SerializedObject(authoring);
                SerializedProperty tags = serialized.FindProperty("roomTags");
                tags.arraySize = roomTags.Length;
                for (int i = 0; i < roomTags.Length; i++)
                    tags.GetArrayElementAtIndex(i).stringValue = roomTags[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void ConfigureProfile(
            IReadOnlyDictionary<string, LevelGraph> graphs,
            IReadOnlyDictionary<string, GameObject> buildings)
        {
            DungeonGenerationProfile profile =
                AssetDatabase.LoadAssetAtPath<DungeonGenerationProfile>(ProfilePath);
            if (profile == null)
                throw new InvalidOperationException($"缺少地牢生成总控：{ProfilePath}");

            foreach ((string id, LevelGraph graph) in graphs)
            {
                DungeonLayoutCandidate layout = profile.Layouts.FirstOrDefault(
                    value => value != null && value.ID == id);
                if (layout == null)
                {
                    layout = new DungeonLayoutCandidate();
                    profile.Layouts.Add(layout);
                }
                layout.ID = id;
                layout.DisplayName = id switch
                {
                    "Layout_A" => "双区主轴",
                    "Layout_B" => "中央辐射",
                    "Layout_C" => "大环寻宝",
                    "Layout_D" => "商店必经",
                    _ => id,
                };
                layout.LevelGraph = graph;
                layout.Weight = 100;
                layout.Enabled = true;
                layout.StartNodeName = "O4";
                layout.BossNodeName = "I4";
                layout.AlternateStartNodeName =
                    id == "Layout_D" ? "I4" : "I3";
                layout.AlternateBossNodeName = "O0";
                layout.LayoutEventNodeName = "O1";
                layout.StrengthEventNodeName = "I1";
                layout.ShopNodeName = "C0";
                layout.EliteNodeNames = new[] { "O3", "I2" };
                layout.LandmarkNodeNames = new[] { "O0", "I4" };
                layout.OptionalBranchSourceNodeName = "O1";
                layout.OptionalBranchTargetNodeName = "B0";
                layout.EnforceLegacyLandmarkRelationships = id == "Layout_A";
            }

            GameObject passage = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{GeneratedRooms}/WB_Inner_Battle_Passage.prefab");
            profile.EdgeExpansions = new List<DungeonEdgeExpansionRule>
            {
                new()
                {
                    ID = "TransitionStretch",
                    FromNodeName = "C0",
                    ToNodeName = "C1",
                    Chance = 3800,
                    MinRooms = 1,
                    MaxRooms = 1,
                    NodeNamePrefix = "Link",
                    RoomTemplates = new List<GameObject> { passage },
                },
            };

            profile.BuildingPools = new List<DungeonBuildingPoolRule>
            {
                new()
                {
                    ID = "CityBuildings",
                    SlotNodeNames = new[] { "O1", "I1", "O2" },
                    SelectCount = 3,
                    Candidates = new List<DungeonBuildingCandidate>
                    {
                        Building("Archive", "王城档案馆", buildings["Archive"]),
                        Building("Ritual", "冠光观星台", buildings["Ritual"]),
                        Building("Barracks", "禁卫兵营", buildings["Barracks"]),
                        Building("Transit", "狱城转运院", buildings["Transit"]),
                    },
                },
            };
            profile.Shortcuts ??= new List<DungeonShortcutRule>();
            profile.Shortcuts.RemoveAll(shortcut =>
                shortcut != null && shortcut.ID == "LayoutCLoop");
            profile.Shortcuts.Add(new DungeonShortcutRule
            {
                ID = "LayoutCLoop",
                LayoutIDs = new[] { "Layout_C" },
                AllowedPhases = LevelPhaseMask.Both,
                SourceNodeName = "O2",
                TargetNodeName = "I0",
                Bidirectional = true,
                SourceSocket = DungeonContentSocketType.Event,
                TargetSocket = DungeonContentSocketType.PlayerSpawn,
                ForwardTitle = "穿过王城回环",
                ReverseTitle = "返回外环支路",
            });
            EditorUtility.SetDirty(profile);
        }

        private static DungeonBuildingCandidate Building(
            string id,
            string displayName,
            GameObject template)
        {
            return new DungeonBuildingCandidate
            {
                ID = id,
                DisplayName = displayName,
                Weight = 100,
                AllowedPhases = LevelPhaseMask.Both,
                RoomTemplate = template,
            };
        }

        private static Dictionary<string, Vector2> Positions(
            params (string Name, float X, float Y)[] values)
        {
            return values.ToDictionary(
                value => value.Name,
                value => new Vector2(value.X, value.Y));
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
