#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Edgar.Unity;
using UnityEditor;
using UnityEngine;
using XianTu.LevelDesign;
using Object = UnityEngine.Object;

namespace XianTu.Editor
{
    /// <summary>
    /// 以 Resources 中的通用 Edgar 房间模板为单一基础库，
    /// 复制门拓扑后生成带游戏语义、内容槽和刷新范围的节点模板。
    /// </summary>
    public static class EdgarWhiteboxBuilder
    {
        private const string SourceRoot =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D/RoomTemplates";
        private const string TargetRoot =
            SourceRoot + "/Generated";
        private const string MaterialRoot =
            "Assets/1Game/Materials/LevelDesign/Whitebox";
        private const string GraphPath =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D/WhiteboxLevelGraph.asset";

        private const string RoomA = SourceRoot + "/Room_01.prefab";
        private const string RoomB = SourceRoot + "/Room_02.prefab";
        private const string RoomC = SourceRoot + "/Room_03.prefab";
        private const string Straight = SourceRoot + "/Corridor_01.prefab";
        private const string Turn = SourceRoot + "/Corridor_02.prefab";

        private sealed class TemplateSpec
        {
            public string Name;
            public string Source;
            public District District;
            public string[] Tags;
            public Color Color;
            public MarkerKind Marker;
            public bool IsCorridor;
            public int DoorCount;
        }

        private enum MarkerKind
        {
            None,
            Endpoint,
            Event,
            Elite,
            Shop,
            Stairs,
        }

        [MenuItem("仙途秘境/关卡工具/生成 Edgar 白膜关卡")]
        public static void Build()
        {
            if (AssetDatabase.IsValidFolder(TargetRoot))
                AssetDatabase.DeleteAsset(TargetRoot);
            EnsureFolder(TargetRoot);
            EnsureFolder(TargetRoot + "/Rooms");
            EnsureFolder(TargetRoot + "/Connectors");
            EnsureFolder(MaterialRoot);

            var roomSpecs = CreateRoomSpecs();
            var connectorSpecs = CreateConnectorSpecs();
            var prefabs = new Dictionary<string, GameObject>();

            foreach (var spec in roomSpecs.Concat(connectorSpecs))
                prefabs[spec.Name] = BuildTemplate(spec);
            BuildGraph(prefabs, connectorSpecs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"<color=#66ff99>[Edgar 白膜]</color> 已生成 {roomSpecs.Count} 个主体模板、" +
                $"{connectorSpecs.Count} 个连接模板和 12 节点关卡图：{GraphPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<LevelGraph>(GraphPath);
        }

        private static List<TemplateSpec> CreateRoomSpecs()
        {
            return new List<TemplateSpec>
            {
                Room("WB_Outer_Endpoint", RoomB, District.Outer,
                    new Color(0.22f, 0.42f, 0.62f), MarkerKind.Endpoint, 3,
                    "Endpoint", "Landing", "Boss"),
                Room("WB_Outer_Battle", RoomA, District.Outer,
                    new Color(0.24f, 0.48f, 0.68f), MarkerKind.None, 2, "Combat"),
                Room("WB_Outer_Battle_DeadEnd", RoomA, District.Outer,
                    new Color(0.24f, 0.48f, 0.68f), MarkerKind.None, 1, "Combat", "DeadEnd"),
                Room("WB_Outer_Event", RoomC, District.Outer,
                    new Color(0.12f, 0.65f, 0.72f), MarkerKind.Event, 1,
                    "Event", "UpperBranch"),
                Room("WB_Outer_Elite", RoomB, District.Outer,
                    new Color(0.88f, 0.44f, 0.12f), MarkerKind.Elite, 2,
                    "Elite", "LowerBranch"),
                Room("WB_Transition_Battle", RoomC, District.Transition,
                    new Color(0.72f, 0.62f, 0.24f), MarkerKind.None, 2,
                    "Combat", "Transition"),
                Room("WB_Transition_Shop", RoomA, District.Transition,
                    new Color(0.28f, 0.68f, 0.34f), MarkerKind.Shop, 2,
                    "Shop", "Transition"),
                Room("WB_Inner_Battle", RoomB, District.Inner,
                    new Color(0.48f, 0.34f, 0.66f), MarkerKind.None, 4, "Combat", "Junction"),
                Room("WB_Inner_Battle_Passage", RoomB, District.Inner,
                    new Color(0.48f, 0.34f, 0.66f), MarkerKind.None, 2, "Combat"),
                Room("WB_Inner_Event", RoomC, District.Inner,
                    new Color(0.22f, 0.58f, 0.74f), MarkerKind.Event, 1,
                    "Event", "UpperBranch"),
                Room("WB_Inner_Elite", RoomB, District.Inner,
                    new Color(0.82f, 0.32f, 0.20f), MarkerKind.Elite, 1,
                    "Elite", "LowerBranch"),
                Room("WB_Inner_Endpoint", RoomB, District.Inner,
                    new Color(0.52f, 0.28f, 0.62f), MarkerKind.Endpoint, 1,
                    "Endpoint", "Landing", "Boss"),
            };
        }

        private static List<TemplateSpec> CreateConnectorSpecs()
        {
            return new List<TemplateSpec>
            {
                Connector("WB_Connector_Straight", Straight, MarkerKind.None, 2),
                Connector("WB_Connector_Turn", Turn, MarkerKind.None, 2),
                Connector("WB_Connector_T", RoomB, MarkerKind.None, 3),
                Connector("WB_Connector_Stairs", Straight, MarkerKind.Stairs, 2),
            };
        }

        private static TemplateSpec Room(
            string name,
            string source,
            District district,
            Color color,
            MarkerKind marker,
            int doorCount,
            params string[] tags)
        {
            return new TemplateSpec
            {
                Name = name,
                Source = source,
                District = district,
                Tags = tags,
                Color = color,
                Marker = marker,
                DoorCount = doorCount,
            };
        }

        private static TemplateSpec Connector(
            string name,
            string source,
            MarkerKind marker,
            int doorCount)
        {
            return new TemplateSpec
            {
                Name = name,
                Source = source,
                District = District.Transition,
                Tags = new[] { "Connector" },
                Color = new Color(0.48f, 0.5f, 0.54f),
                Marker = marker,
                IsCorridor = true,
                DoorCount = doorCount,
            };
        }

        private static GameObject BuildTemplate(TemplateSpec spec)
        {
            string folder = spec.IsCorridor ? "Connectors" : "Rooms";
            string path = $"{TargetRoot}/{folder}/{spec.Name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);
            if (!AssetDatabase.CopyAsset(spec.Source, path))
                throw new InvalidOperationException($"无法复制 Edgar 模板：{spec.Source} -> {path}");

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = spec.Name;
                DestroyChild(root.transform, "Visuals");
                DestroyChild(root.transform, "Props");
                DestroyChild(root.transform, "WhiteboxGeometry");
                DestroyChild(root.transform, "SpawnAreas");
                DestroyChild(root.transform, "__房间有效范围");

                ConfigureDoors(root, spec.DoorCount);

                Bounds bounds = GetBlockBounds(root);
                Material material = GetOrCreateMaterial(spec);
                BuildGeometry(root.transform, bounds, material, spec.Marker);
                ConfigureAuthoring(root, bounds, spec);
                if (!spec.IsCorridor)
                {
                    BuildPlayerSpawnSockets(root.transform, bounds);
                    BuildSemanticSockets(root.transform, bounds, spec.Marker, spec.Tags);
                    BuildEventSceneObjects(root.transform, bounds, spec.Marker);
                    BuildSpawnAreas(root.transform, bounds);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void ConfigureDoors(GameObject root, int desiredCount)
        {
            var doors = root.GetComponentsInChildren<DoorHandlerGrid3D>(true).ToList();
            desiredCount = Mathf.Clamp(desiredCount, 1, doors.Count);
            if (desiredCount >= doors.Count)
                return;

            var keep = new HashSet<DoorHandlerGrid3D>();
            if (desiredCount == 2)
            {
                for (int i = 0; i < doors.Count && keep.Count == 0; i++)
                {
                    Vector3 a = ((Vector3)doors[i].DirectionVector).normalized;
                    for (int j = i + 1; j < doors.Count; j++)
                    {
                        Vector3 b = ((Vector3)doors[j].DirectionVector).normalized;
                        if (Vector3.Dot(a, b) <= -0.9f)
                        {
                            keep.Add(doors[i]);
                            keep.Add(doors[j]);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < doors.Count && keep.Count < desiredCount; i++)
                keep.Add(doors[i]);
            foreach (var door in doors)
            {
                if (!keep.Contains(door))
                    Object.DestroyImmediate(door.gameObject);
            }
        }

        private static void BuildGeometry(
            Transform root,
            Bounds bounds,
            Material material,
            MarkerKind marker)
        {
            var geometry = new GameObject("WhiteboxGeometry").transform;
            geometry.SetParent(root, false);
            BuildMarker(geometry, bounds, material, marker);
        }

        private static void BuildWall(
            Transform parent,
            Bounds bounds,
            IReadOnlyList<DoorHandlerGrid3D> doors,
            bool alongX,
            bool positiveSide,
            Material material)
        {
            float min = alongX ? bounds.min.x : bounds.min.z;
            float max = alongX ? bounds.max.x : bounds.max.z;
            float side = alongX
                ? (positiveSide ? bounds.max.z : bounds.min.z)
                : (positiveSide ? bounds.max.x : bounds.min.x);
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(max - min));
            float segmentLength = (max - min) / segmentCount;

            for (int i = 0; i < segmentCount; i++)
            {
                float axis = min + (i + 0.5f) * segmentLength;
                if (HasDoorOpening(
                        doors,
                        alongX,
                        positiveSide,
                        bounds.center,
                        axis,
                        segmentLength))
                    continue;

                Vector3 position = alongX
                    ? new Vector3(axis, bounds.min.y + 1.25f, side)
                    : new Vector3(side, bounds.min.y + 1.25f, axis);
                Vector3 scale = alongX
                    ? new Vector3(segmentLength, 2.5f, 0.2f)
                    : new Vector3(0.2f, 2.5f, segmentLength);
                CreateCube(parent, $"Wall_{(alongX ? "X" : "Z")}_{i:00}", position, scale, material);
            }
        }

        private static bool HasDoorOpening(
            IReadOnlyList<DoorHandlerGrid3D> doors,
            bool alongX,
            bool positiveSide,
            Vector3 roomCenter,
            float axis,
            float segmentLength)
        {
            foreach (var door in doors)
            {
                Vector3 p = door.transform.localPosition;
                Vector3Int direction = door.DirectionVector;
                bool facesThisAxis = alongX
                    ? Mathf.Abs(direction.z) > 0
                    : Mathf.Abs(direction.x) > 0;
                if (!facesThisAxis)
                    continue;

                float doorSide = alongX ? p.z : p.x;
                float centerSide = alongX ? roomCenter.z : roomCenter.x;
                if ((doorSide >= centerSide) != positiveSide)
                    continue;

                float doorAxis = alongX ? p.x : p.z;
                float openingHalfWidth = Mathf.Max(
                    0.75f,
                    door.Width * 0.5f + segmentLength * 0.5f);
                if (Mathf.Abs(doorAxis - axis) <= openingHalfWidth)
                    return true;
            }
            return false;
        }

        private static void BuildMarker(
            Transform parent,
            Bounds bounds,
            Material roomMaterial,
            MarkerKind marker)
        {
            if (marker == MarkerKind.None)
                return;

            Vector3 center = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            switch (marker)
            {
                case MarkerKind.Endpoint:
                    CreateCube(parent, "Endpoint_Dais", center + Vector3.up * 0.15f,
                        new Vector3(3f, 0.3f, 3f), GetOrCreateAccent("Endpoint", new Color(0.78f, 0.18f, 0.16f)));
                    break;
                case MarkerKind.Event:
                    CreateCube(parent, "Event_Plinth", center + Vector3.up * 0.75f,
                        new Vector3(1.5f, 1.5f, 1.5f), GetOrCreateAccent("Event", Color.cyan));
                    break;
                case MarkerKind.Elite:
                    CreateCube(parent, "Elite_Arena", center + Vector3.up * 0.08f,
                        new Vector3(4f, 0.16f, 4f), GetOrCreateAccent("Elite", new Color(1f, 0.28f, 0.05f)));
                    break;
                case MarkerKind.Shop:
                    CreateCube(parent, "Shop_Counter", center + new Vector3(0f, 0.65f, 0.8f),
                        new Vector3(3.5f, 1.3f, 0.8f), GetOrCreateAccent("Shop", new Color(0.2f, 0.85f, 0.32f)));
                    break;
                case MarkerKind.Stairs:
                    for (int i = 0; i < 5; i++)
                        CreateCube(parent, $"StairMarker_{i:00}",
                            center + new Vector3(0f, 0.025f, -1f + i * 0.5f),
                            new Vector3(1.8f, 0.05f, 0.25f),
                            i % 2 == 0 ? roomMaterial : GetOrCreateAccent("Stairs", Color.white));
                    break;
            }
        }

        private static void ConfigureAuthoring(GameObject root, Bounds bounds, TemplateSpec spec)
        {
            var validObject = new GameObject("__房间有效范围");
            validObject.transform.SetParent(root.transform, false);
            validObject.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var valid = validObject.AddComponent<BoxCollider>();
            valid.isTrigger = true;
            valid.center = new Vector3(0f, 1.25f, 0f);
            valid.size = new Vector3(
                Mathf.Max(1f, bounds.size.x - 0.8f),
                2.5f,
                Mathf.Max(1f, bounds.size.z - 0.8f));

            var authoring = root.GetComponent<DungeonRoomAuthoring>()
                            ?? root.AddComponent<DungeonRoomAuthoring>();
            authoring.Configure(valid);
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("district").enumValueIndex = (int)spec.District;
            serialized.FindProperty("roomTags").arraySize = spec.Tags.Length;
            for (int i = 0; i < spec.Tags.Length; i++)
                serialized.FindProperty("roomTags").GetArrayElementAtIndex(i).stringValue = spec.Tags[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSpawnAreas(Transform root, Bounds bounds)
        {
            foreach (var existing in root.GetComponentsInChildren<DungeonEnemySpawnArea>(true))
                Object.DestroyImmediate(existing.gameObject);

            var areas = new GameObject("SpawnAreas").transform;
            areas.SetParent(root, false);
            float insetX = Mathf.Max(1.5f, bounds.size.x - 2.4f);
            float insetZ = Mathf.Max(1.5f, bounds.size.z - 2.4f);
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y + 0.35f, bounds.center.z);

            CreateSpawnArea(areas, "Spawn_Melee_Center", center,
                new Vector3(insetX * 0.58f, 0.5f, insetZ * 0.58f),
                EnemyCategoryMask.Melee, 0, 120, 5f, 1.5f);
            CreateSpawnArea(areas, "Spawn_Ranged_Back", center + Vector3.forward * insetZ * 0.28f,
                new Vector3(insetX * 0.72f, 0.5f, insetZ * 0.28f),
                EnemyCategoryMask.Ranged, 4, 100, 7f, 2f);
            CreateSpawnArea(areas, "Spawn_Magic_Flank", center + Vector3.back * insetZ * 0.28f,
                new Vector3(insetX * 0.72f, 0.5f, insetZ * 0.28f),
                EnemyCategoryMask.Magic, 3, 80, 7f, 2.25f);
        }

        private static void BuildPlayerSpawnSockets(Transform root, Bounds bounds)
        {
            DestroyChild(root, "PlayerSpawns");

            var sockets = new GameObject("PlayerSpawns").transform;
            sockets.SetParent(root, false);
            // Socket 仅提供平面候选位置；运行时会再向下投射到真实地板碰撞面。
            // 先放到包围盒上方，确保不同基础模板的抬高地板都不会盖住标记点。
            Vector3 center = new Vector3(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);
            float offsetX = Mathf.Max(1f, bounds.extents.x * 0.28f);
            float offsetZ = Mathf.Max(1f, bounds.extents.z * 0.28f);
            Vector3[] offsets =
            {
                Vector3.left * offsetX,
                Vector3.right * offsetX,
                Vector3.back * offsetZ,
                Vector3.forward * offsetZ,
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                var socketObject = new GameObject($"PlayerSpawn_{i + 1:00}");
                socketObject.transform.SetParent(sockets, false);
                socketObject.transform.localPosition = center + offsets[i];
                socketObject.AddComponent<DungeonContentSocket>()
                    .Configure(DungeonContentSocketType.PlayerSpawn);
            }
        }

        private static void BuildSemanticSockets(
            Transform root,
            Bounds bounds,
            MarkerKind marker,
            IReadOnlyCollection<string> tags)
        {
            DestroyChild(root, "SemanticSockets");
            var sockets = new GameObject("SemanticSockets").transform;
            sockets.SetParent(root, false);
            Vector3 center = new(bounds.center.x, bounds.max.y + 0.1f, bounds.center.z);

            if (marker == MarkerKind.Endpoint)
            {
                CreateContentSocket(
                    sockets,
                    "BossSpawn",
                    center,
                    DungeonContentSocketType.BossSpawn);
                CreateContentSocket(
                    sockets,
                    "ExitPortal",
                    center + Vector3.back * bounds.extents.z * 0.35f,
                    DungeonContentSocketType.ExitPortal);
            }
            else if (marker == MarkerKind.Event)
            {
                CreateContentSocket(
                    sockets,
                    "Event",
                    center,
                    DungeonContentSocketType.Event);
            }

            if (tags.Contains("Combat") || tags.Contains("Endpoint"))
            {
                float offsetX = Mathf.Max(1.2f, bounds.extents.x * 0.32f);
                CreateContentSocket(
                    sockets,
                    "Material_01",
                    center + Vector3.left * offsetX,
                    DungeonContentSocketType.Material);
                CreateContentSocket(
                    sockets,
                    "Material_02",
                    center + Vector3.right * offsetX,
                    DungeonContentSocketType.Material);
            }
        }

        private static void CreateContentSocket(
            Transform parent,
            string name,
            Vector3 localPosition,
            DungeonContentSocketType type)
        {
            var socketObject = new GameObject(name);
            socketObject.transform.SetParent(parent, false);
            socketObject.transform.localPosition = localPosition;
            socketObject.AddComponent<DungeonContentSocket>().Configure(type);
        }

        private static void BuildEventSceneObjects(
            Transform root,
            Bounds bounds,
            MarkerKind marker)
        {
            DestroyChild(root, "EventSceneObjects");
            if (marker != MarkerKind.Event)
                return;

            var sceneObjects = new GameObject("EventSceneObjects").transform;
            sceneObjects.SetParent(root, false);
            Vector3 center = new(
                bounds.center.x,
                bounds.min.y + 1.2f,
                bounds.center.z + bounds.extents.z * 0.32f);

            var bridgeVariant = new GameObject("Layout_断裂巡礼桥").transform;
            bridgeVariant.SetParent(sceneObjects, false);
            bridgeVariant.gameObject.AddComponent<DungeonEventVariantRoot>().Configure(1004);
            var routeBlocker = CreateCube(
                bridgeVariant,
                "巡礼桥封锁",
                center,
                new Vector3(
                    Mathf.Min(3.2f, bounds.size.x * 0.28f),
                    2.4f,
                    0.45f),
                GetOrCreateAccent("EventRouteBlocked", new Color(0.72f, 0.16f, 0.08f)));
            routeBlocker.AddComponent<DungeonEventSceneObject>()
                .Configure(EventSceneResult.OpenRoute, EventSceneObjectAction.Disable);
            routeBlocker.AddComponent<DungeonEventSceneObject>()
                .Configure(
                    EventSceneResult.BridgeSabotaged,
                    EventSceneObjectAction.Disable,
                    LevelPhaseMask.Day);

            var bridge = CreateCube(
                bridgeVariant,
                "巡礼桥桥面",
                center + Vector3.forward * 1.8f + Vector3.down * 0.85f,
                new Vector3(3.2f, 0.3f, 3.6f),
                GetOrCreateAccent("EventRouteOpen", new Color(0.12f, 0.72f, 0.34f)));
            bridge.AddComponent<DungeonEventSceneObject>()
                .Configure(EventSceneResult.OpenRoute, EventSceneObjectAction.Enable);
            bridge.AddComponent<DungeonEventSceneObject>()
                .Configure(
                    EventSceneResult.BridgeSabotaged,
                    EventSceneObjectAction.Enable,
                    LevelPhaseMask.Day);
            bridge.SetActive(false);

            var collapsedBridge = CreateCube(
                bridgeVariant,
                "永夜坍塌残骸",
                center + Vector3.forward * 1.8f + Vector3.down * 0.75f,
                new Vector3(3.1f, 0.55f, 1.2f),
                GetOrCreateAccent("EventBridgeCollapsed", new Color(0.58f, 0.12f, 0.08f)));
            collapsedBridge.AddComponent<DungeonEventSceneObject>()
                .Configure(
                    EventSceneResult.BridgeSabotaged,
                    EventSceneObjectAction.Enable,
                    LevelPhaseMask.Night);
            collapsedBridge.SetActive(false);

            var summonVariant = new GameObject("Strength_禁卫召集阵").transform;
            summonVariant.SetParent(sceneObjects, false);
            summonVariant.gameObject.AddComponent<DungeonEventVariantRoot>().Configure(1006);
            var summonCore = CreateCube(
                summonVariant,
                "召集阵阵心",
                center,
                new Vector3(1f, 1.8f, 1f),
                GetOrCreateAccent("SummonArrayCore", new Color(0.22f, 0.5f, 1f)));
            Object.DestroyImmediate(summonCore.GetComponent<Collider>());
            summonCore.AddComponent<DungeonEventSceneObject>()
                .Configure(EventSceneResult.SummonArrayDestroyed, EventSceneObjectAction.Disable);

            var summonRing = CreateCube(
                summonVariant,
                "召集阵外环",
                center + Vector3.down * 0.95f,
                new Vector3(3.8f, 0.16f, 3.8f),
                GetOrCreateAccent("SummonArrayRing", new Color(0.48f, 0.22f, 0.95f)));
            Object.DestroyImmediate(summonRing.GetComponent<Collider>());
            summonRing.AddComponent<DungeonEventSceneObject>()
                .Configure(EventSceneResult.SummonArrayDestroyed, EventSceneObjectAction.Disable);
            summonRing.AddComponent<DungeonEventSceneObject>()
                .Configure(EventSceneResult.SummonArrayOuterBroken, EventSceneObjectAction.Disable);

            var brokenRing = CreateCube(
                summonVariant,
                "破损召集阵外环",
                center + Vector3.right * 1.2f + Vector3.down * 0.92f,
                new Vector3(1.4f, 0.22f, 2.8f),
                GetOrCreateAccent("SummonArrayBroken", new Color(0.82f, 0.18f, 0.62f)));
            Object.DestroyImmediate(brokenRing.GetComponent<Collider>());
            brokenRing.AddComponent<DungeonEventSceneObject>()
                .Configure(
                    EventSceneResult.SummonArrayOuterBroken,
                    EventSceneObjectAction.Enable);
            brokenRing.SetActive(false);

            var destroyedCore = CreateCube(
                summonVariant,
                "召集阵残骸",
                center + Vector3.down * 0.65f,
                new Vector3(1.8f, 0.35f, 1.8f),
                GetOrCreateAccent("SummonArrayDestroyed", new Color(0.72f, 0.08f, 0.16f)));
            Object.DestroyImmediate(destroyedCore.GetComponent<Collider>());
            destroyedCore.AddComponent<DungeonEventSceneObject>()
                .Configure(
                    EventSceneResult.SummonArrayDestroyed,
                    EventSceneObjectAction.Enable);
            destroyedCore.SetActive(false);

            bridgeVariant.gameObject.SetActive(false);
            summonVariant.gameObject.SetActive(false);
        }

        private static void CreateSpawnArea(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size,
            EnemyCategoryMask categories,
            int maxCount,
            int weight,
            float minPlayerDistance,
            float minSeparation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;
            var area = go.AddComponent<DungeonEnemySpawnArea>();
            area.Configure(collider);

            var serialized = new SerializedObject(area);
            serialized.FindProperty("allowedCategories").intValue = (int)categories;
            serialized.FindProperty("maxSpawnCount").intValue = maxCount;
            serialized.FindProperty("weight").intValue = weight;
            serialized.FindProperty("minPlayerDistance").floatValue = minPlayerDistance;
            serialized.FindProperty("minSeparation").floatValue = minSeparation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildGraph(
            IReadOnlyDictionary<string, GameObject> prefabs,
            IReadOnlyList<TemplateSpec> connectorSpecs)
        {
            if (AssetDatabase.LoadAssetAtPath<LevelGraph>(GraphPath) != null)
                AssetDatabase.DeleteAsset(GraphPath);

            var graph = ScriptableObject.CreateInstance<LevelGraph>();
            graph.name = "WhiteboxLevelGraph";
            graph.RoomType = typeof(Room).FullName;
            graph.ConnectionType = typeof(Connection).FullName;
            graph.IsDirected = false;
            // Edgar 的 Connection Corridor 每段只连接两个端点；T 形和阶梯模板保留给后续
            // 手工连接区/特殊节点，不加入自动走廊池，避免三门模板拖死布局搜索。
            graph.CorridorIndividualRoomTemplates.Add(prefabs["WB_Connector_Straight"]);
            graph.CorridorIndividualRoomTemplates.Add(prefabs["WB_Connector_Turn"]);
            AssetDatabase.CreateAsset(graph, GraphPath);

            var nodes = new Dictionary<string, Room>();
            AddRoom(graph, nodes, "O0", new Vector2(0, 0), prefabs["WB_Outer_Endpoint"]);
            AddRoom(graph, nodes, "O1", new Vector2(150, -100), prefabs["WB_Outer_Event"]);
            AddRoom(graph, nodes, "O2", new Vector2(300, -100), prefabs["WB_Outer_Battle"]);
            AddRoom(graph, nodes, "O3", new Vector2(150, 100), prefabs["WB_Outer_Elite"]);
            AddRoom(graph, nodes, "O4", new Vector2(300, 100), prefabs["WB_Outer_Battle_DeadEnd"]);
            AddRoom(graph, nodes, "C0", new Vector2(450, -50), prefabs["WB_Transition_Shop"]);
            AddRoom(graph, nodes, "C1", new Vector2(450, 80), prefabs["WB_Transition_Battle"]);
            AddRoom(graph, nodes, "I0", new Vector2(600, 0), prefabs["WB_Inner_Battle"]);
            AddRoom(graph, nodes, "I1", new Vector2(750, -100), prefabs["WB_Inner_Event"]);
            AddRoom(graph, nodes, "I2", new Vector2(750, 100), prefabs["WB_Inner_Elite"]);
            AddRoom(graph, nodes, "I3", new Vector2(900, -100), prefabs["WB_Inner_Battle_Passage"]);
            AddRoom(graph, nodes, "I4", new Vector2(1050, 0), prefabs["WB_Inner_Endpoint"]);

            AddConnections(graph, nodes,
                ("O0", "O2"), ("O0", "O1"), ("O0", "O3"), ("O3", "O4"),
                ("O2", "C0"), ("C0", "C1"), ("C1", "I0"),
                ("I0", "I3"), ("I3", "I4"), ("I0", "I1"), ("I0", "I2"));

            EditorUtility.SetDirty(graph);
        }

        private static void AddRoom(
            LevelGraph graph,
            IDictionary<string, Room> nodes,
            string name,
            Vector2 position,
            GameObject prefab)
        {
            var room = ScriptableObject.CreateInstance<Room>();
            room.name = name;
            room.Name = name;
            room.Position = position;
            room.IndividualRoomTemplates.Add(prefab);
            room.hideFlags = HideFlags.HideInHierarchy;
            graph.Rooms.Add(room);
            nodes.Add(name, room);
            AssetDatabase.AddObjectToAsset(room, graph);
        }

        private static void AddConnections(
            LevelGraph graph,
            IReadOnlyDictionary<string, Room> nodes,
            params (string From, string To)[] pairs)
        {
            foreach (var pair in pairs)
            {
                var connection = ScriptableObject.CreateInstance<Connection>();
                connection.name = $"{pair.From}_{pair.To}";
                connection.From = nodes[pair.From];
                connection.To = nodes[pair.To];
                connection.hideFlags = HideFlags.HideInHierarchy;
                graph.Connections.Add(connection);
                AssetDatabase.AddObjectToAsset(connection, graph);
            }
        }

        private static Bounds GetBlockBounds(GameObject root)
        {
            Transform blocks = FindOutlineObjectsRoot(root.transform);
            var renderers = blocks != null
                ? blocks.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            if (renderers.Length == 0)
                throw new InvalidOperationException(
                    $"{root.name} 的 Blocks / Objects 下没有 Renderer，无法推导白膜尺寸。");

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static Transform FindOutlineObjectsRoot(Transform root)
        {
            return root.Find("Blocks") ?? root.Find("Objects");
        }

        private static GameObject CreateCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static Material GetOrCreateMaterial(TemplateSpec spec)
        {
            string district = spec.District.ToString();
            return GetOrCreateAccent($"{district}_{spec.Name}", spec.Color);
        }

        private static Material GetOrCreateAccent(string name, Color color)
        {
            string path = $"{MaterialRoot}/WB_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Standard");
                material = new Material(shader) { name = $"WB_{name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            if (parent == null) return;
            Transform child = parent.Find(childName);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
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
