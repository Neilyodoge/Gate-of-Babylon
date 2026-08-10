using System;
using System.Collections.Generic;
using Edgar.Unity;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// Edgar Grid3D 的游戏侧运行桥。负责生成实体地牢，并把生成结果转换成现有主循环可消费的房间落点。
    /// </summary>
    public sealed class EdgarDungeonRuntime : MonoBehaviour
    {
        private const string LevelGraphPath = "LevelDesign/EdgarGrid3D/WhiteboxLevelGraph";
        private const string GeneratorSettingsPath = "LevelDesign/EdgarGrid3D/GeneratorSettings";
        private const float DungeonScale = 5f;
        // 只校验门槛体积；更远处会进入合法走廊/相邻空间，不能按“封门”处理。
        private const float DoorClearanceCheckDistance = 1.5f;

        private readonly List<EdgarRoomPlacement> _rooms = new();
        private DungeonGeneratorGrid3D _generator;
        private GameObject _generatedRoot;
        private GameObject _triggerRoot;
        private int _activeRoom = -1;
        private static Material _combatGateMaterial;

        public IReadOnlyList<EdgarRoomPlacement> Rooms => _rooms;
        public int RoomCount => _rooms.Count;
        public bool IsReady => _generatedRoot != null && _rooms.Count > 0;
        public int WorldRotationDegrees { get; private set; }
        public int ConfiguredRoomCount
        {
            get
            {
                var graph = Resources.Load<LevelGraph>(LevelGraphPath);
                return graph != null ? graph.Rooms.Count : 0;
            }
        }

        public bool Generate(int seed, string preferredStartNode)
        {
            Clear();

            var graph = Resources.Load<LevelGraph>(LevelGraphPath);
            var settings = Resources.Load<GeneratorSettingsGrid3D>(GeneratorSettingsPath);
            if (graph == null || settings == null)
            {
                Debug.LogError($"[Edgar] 缺少原型资源：{LevelGraphPath} / {GeneratorSettingsPath}");
                return false;
            }

            _generator = GetComponent<DungeonGeneratorGrid3D>();
            bool reactivateAfterSetup = false;
            if (_generator == null)
            {
                // DungeonGeneratorGrid3D 默认会在 Awake 立即 Generate；运行时添加组件前先停用，
                // 避免其配置尚未注入时用 null FixedLevelGraphConfig 提前生成。
                reactivateAfterSetup = gameObject.activeSelf;
                if (reactivateAfterSetup)
                    gameObject.SetActive(false);
                _generator = gameObject.AddComponent<DungeonGeneratorGrid3D>();
            }

            _generator.GenerateOn = GenerateOn.Manually;
            _generator.UseRandomSeed = false;
            _generator.FixedLevelGraphConfig = new FixedLevelGraphConfigGrid3D
            {
                LevelGraph = graph,
                UseCorridors = true,
            };
            _generator.GeneratorConfig = new DungeonGeneratorConfigGrid3D
            {
                GeneratorSettings = settings,
                MinimumRoomDistance = 1,
                Timeout = 10000,
            };
            _generator.PostProcessingConfig = new PostProcessingConfigGrid3D
            {
                CenterLevel = true,
            };
            _generator.CustomPostProcessingTasks = new List<DungeonGeneratorPostProcessingGrid3D>();
            if (reactivateAfterSetup)
                gameObject.SetActive(true);

            try
            {
                DungeonGeneratorPayloadGrid3D payload = null;
                Exception lastGenerationError = null;
                const int maxLayoutAttempts = 64;
                for (int attempt = 0; attempt < maxLayoutAttempts; attempt++)
                {
                    int layoutSeed = (int)(
                        (unchecked((uint)seed) + (uint)(attempt * 7919))
                        & int.MaxValue);
                    if (layoutSeed == 0)
                        layoutSeed = 1;
                    _generator.RandomGeneratorSeed = layoutSeed;
                    try
                    {
                        var candidate = (DungeonGeneratorPayloadGrid3D)_generator.Generate();
                        if (HasRequiredLandmarkRelationships(candidate.GeneratedLevel.RoomInstances))
                        {
                            payload = candidate;
                            break;
                        }

                        if (candidate.GeneratedLevel.RootGameObject != null)
                            DestroyImmediate(candidate.GeneratedLevel.RootGameObject);
                    }
                    catch (Exception ex)
                    {
                        lastGenerationError = ex;
                    }
                }

                if (payload == null)
                    throw new InvalidOperationException(
                        $"连续 {maxLayoutAttempts} 个布局 Seed 均未满足事件/精英相对方位约束。",
                        lastGenerationError);

                _generatedRoot = payload.GeneratedLevel.RootGameObject;
                if (_generatedRoot != null)
                {
                    _generatedRoot.name = $"Edgar地牢_种子{seed}";
                    ApplySeededDungeonOrientation(seed);
                    _generatedRoot.transform.localScale = Vector3.one * DungeonScale;
                    Physics.SyncTransforms();
                }

                BuildRoomOrder(payload.GeneratedLevel.RoomInstances, settings, preferredStartNode, seed);
                ValidateConnectedDoorClearance(payload.GeneratedLevel.RoomInstances);
                Debug.Log(
                    $"<color=#66ccff>[Edgar] Grid3D 实体地牢生成完成：{_rooms.Count} 个房间，" +
                    $"Scale={DungeonScale:F1}，Rotation={WorldRotationDegrees}°，" +
                    $"RunSeed={seed}，LayoutSeed={payload.Seed}</color>");
                return IsReady;
            }
            catch (Exception ex)
            {
                Clear();
                throw new InvalidOperationException(
                    $"Edgar Grid3D 生成失败：Seed={seed}。请修复房间模板或生成配置错误，旧房间流程不会接管。", ex);
            }
        }

        private static bool HasRequiredLandmarkRelationships(
            IReadOnlyList<RoomInstanceGrid3D> instances)
        {
            var positions = new Dictionary<string, Vector3>();
            foreach (var instance in instances)
            {
                if (instance.IsCorridor)
                    continue;
                positions[GetNodeName(instance)] = instance.RoomTemplateInstance.transform.position;
            }

            string[] required = { "O0", "O1", "O3", "I1", "I2", "I4" };
            foreach (string node in required)
                if (!positions.ContainsKey(node))
                    return false;

            Vector3 axis = positions["I4"] - positions["O0"];
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.001f)
                return false;
            axis.Normalize();

            float Side(string node, string origin)
            {
                Vector3 relative = positions[node] - positions[origin];
                relative.y = 0f;
                return Vector3.Cross(axis, relative).y;
            }

            float outerEvent = Side("O1", "O0");
            float outerElite = Side("O3", "O0");
            float innerEvent = Side("I1", "I4");
            float innerElite = Side("I2", "I4");
            const float minimumSideDistance = 0.5f;
            return Mathf.Abs(outerEvent) >= minimumSideDistance
                   && Mathf.Abs(outerElite) >= minimumSideDistance
                   && Mathf.Abs(innerEvent) >= minimumSideDistance
                   && Mathf.Abs(innerElite) >= minimumSideDistance
                   && Mathf.Sign(outerEvent) != Mathf.Sign(outerElite)
                   && Mathf.Sign(innerEvent) != Mathf.Sign(innerElite)
                   && Mathf.Sign(outerEvent) == Mathf.Sign(innerEvent);
        }

        public bool TryGetPlacement(int index, out EdgarRoomPlacement placement)
        {
            if (index >= 0 && index < _rooms.Count)
            {
                placement = _rooms[index];
                return true;
            }

            placement = default;
            return false;
        }

        public bool TryGetContentSocketPosition(
            int roomIndex,
            DungeonContentSocketType socketType,
            out Vector3 position)
        {
            position = default;
            if (!TryGetPlacement(roomIndex, out EdgarRoomPlacement placement)
                || placement.Instance?.RoomTemplateInstance == null)
                return false;

            Transform roomRoot = placement.Instance.RoomTemplateInstance.transform;
            var sockets = roomRoot.GetComponentsInChildren<DungeonContentSocket>(true);
            foreach (var socket in sockets)
            {
                if (socket.SocketType != socketType)
                    continue;

                if (DungeonSpawnSafety.TryFindGroundedPoint(
                        roomRoot,
                        socket.transform.position,
                        0.5f,
                        2f,
                        0.05f,
                        out position))
                    return true;
            }

            return false;
        }

        public void ActivateRoom(int index)
        {
            _activeRoom = index;
        }

        public void UnlockActiveRoom()
        {
            if (_activeRoom >= 0)
                SetRoomLocked(_activeRoom, false);
        }

        public void Clear()
        {
            RoomRuntimeController.ResetRunState();
            _rooms.Clear();
            _activeRoom = -1;
            WorldRotationDegrees = 0;
            if (_triggerRoot != null)
                DestroyRuntimeObject(_triggerRoot);
            _triggerRoot = null;
            if (_generatedRoot != null)
                DestroyRuntimeObject(_generatedRoot);
            _generatedRoot = null;
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        public string GetNodeName(int index)
        {
            return index >= 0 && index < _rooms.Count ? _rooms[index].NodeName : string.Empty;
        }

        private void BuildRoomOrder(
            IReadOnlyList<RoomInstanceGrid3D> instances,
            GeneratorSettingsGrid3D settings,
            string preferredStartNode,
            int seed)
        {
            var regularRooms = new List<RoomInstanceGrid3D>();
            foreach (var instance in instances)
            {
                if (!instance.IsCorridor)
                    regularRooms.Add(instance);
            }

            // 房间索引同时被内容表和 RoomTrigger 使用，按关卡节点语义排序而非生成器字典顺序。
            // 反向出生时整条顺序反转，使 GameManager 的第 0 房始终是本局降落端点。
            regularRooms.Sort((a, b) =>
            {
                int aRank = GetTraversalRank(GetNodeName(a), preferredStartNode);
                int bRank = GetTraversalRank(GetNodeName(b), preferredStartNode);
                if (aRank != bRank) return aRank.CompareTo(bRank);
                int z = a.RoomTemplateInstance.transform.position.z.CompareTo(
                    b.RoomTemplateInstance.transform.position.z);
                return z != 0
                    ? z
                    : a.RoomTemplateInstance.transform.position.x.CompareTo(
                        b.RoomTemplateInstance.transform.position.x);
            });

            _triggerRoot = new GameObject("Edgar 房间触发器");
            _triggerRoot.transform.SetParent(transform, false);

            foreach (var instance in regularRooms)
            {
                var bounds = GetBounds(instance.RoomTemplateInstance, settings.CellSize);
                int roomIndex = _rooms.Count;
                string nodeName = GetNodeName(instance);
                Vector3 spawnPosition = GetPlayerSpawnPosition(
                    instance.RoomTemplateInstance,
                    bounds,
                    seed,
                    nodeName);
                _rooms.Add(new EdgarRoomPlacement(
                    spawnPosition,
                    Mathf.Max(8f, Mathf.Min(bounds.size.x, bounds.size.z)),
                    instance,
                    nodeName));
                CreateRoomTrigger(roomIndex, bounds);
            }
        }

        private void ApplySeededDungeonOrientation(int seed)
        {
            if (_generatedRoot == null)
                return;

            int quarterTurns = (int)(((uint)seed >> 3) & 3u);
            WorldRotationDegrees = quarterTurns * 90;
            _generatedRoot.transform.rotation =
                Quaternion.Euler(0f, WorldRotationDegrees, 0f);
        }

        private static int GetTraversalRank(string nodeName, string preferredStartNode)
        {
            string[] order =
            {
                "O0", "O1", "O2", "O3", "O4", "C0",
                "C1", "I0", "I1", "I2", "I3", "I4",
            };
            int rank = Array.IndexOf(order, nodeName);
            if (rank < 0) return int.MaxValue;

            int preferredRank = Array.IndexOf(order, preferredStartNode);
            if (preferredRank < 0)
                return rank;
            if (rank == preferredRank)
                return 0;

            bool reverse = preferredStartNode.StartsWith("I", StringComparison.Ordinal);
            int directionalRank = reverse ? order.Length - 1 - rank : rank;
            int directionalPreferred = reverse
                ? order.Length - 1 - preferredRank
                : preferredRank;
            return directionalRank < directionalPreferred
                ? directionalRank + 1
                : directionalRank;
        }

        private static string GetNodeName(RoomInstanceGrid3D instance)
        {
            return instance?.Room is Room room ? room.Name : string.Empty;
        }

        private static Vector3 GetPlayerSpawnPosition(
            GameObject room,
            Bounds fallbackBounds,
            int seed,
            string nodeName)
        {
            var sockets = room.GetComponentsInChildren<DungeonContentSocket>(true);
            var playerSpawns = new List<DungeonContentSocket>();
            foreach (var socket in sockets)
            {
                if (socket.SocketType == DungeonContentSocketType.PlayerSpawn)
                    playerSpawns.Add(socket);
            }
            uint hash = unchecked((uint)seed) ^ 2166136261u;
            foreach (char character in nodeName)
                hash = (hash ^ character) * 16777619u;

            if (playerSpawns.Count > 0)
            {
                int startIndex = (int)(hash % playerSpawns.Count);
                for (int i = 0; i < playerSpawns.Count; i++)
                {
                    Vector3 candidate =
                        playerSpawns[(startIndex + i) % playerSpawns.Count].transform.position;
                    if (TryGetSafeGround(room, fallbackBounds, candidate, out Vector3 grounded))
                        return grounded;
                }
            }

            const int gridSize = 7;
            const int sampleCount = gridSize * gridSize;
            int gridStart = (int)(hash % sampleCount);
            float margin = Mathf.Min(
                Mathf.Min(fallbackBounds.size.x, fallbackBounds.size.z) * 0.2f,
                2f);
            for (int i = 0; i < sampleCount; i++)
            {
                int sample = (gridStart + i) % sampleCount;
                int x = sample % gridSize;
                int z = sample / gridSize;
                float tx = (x + 0.5f) / gridSize;
                float tz = (z + 0.5f) / gridSize;
                Vector3 candidate = new(
                    Mathf.Lerp(fallbackBounds.min.x + margin, fallbackBounds.max.x - margin, tx),
                    fallbackBounds.max.y,
                    Mathf.Lerp(fallbackBounds.min.z + margin, fallbackBounds.max.z - margin, tz));
                if (TryGetSafeGround(room, fallbackBounds, candidate, out Vector3 grounded))
                    return grounded;
            }

            throw new InvalidOperationException(
                $"房间 {nodeName} 没有同时满足地板与角色胶囊净空的玩家出生点。");
        }

        private static bool TryGetSafeGround(
            GameObject room,
            Bounds roomBounds,
            Vector3 candidate,
            out Vector3 grounded)
        {
            Vector3 origin = new(
                candidate.x,
                roomBounds.max.y + 2f,
                candidate.z);
            float distance = Mathf.Max(10f, roomBounds.size.y + 4f);
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => b.distance.CompareTo(a.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null
                    || !hit.collider.transform.IsChildOf(room.transform)
                    || Vector3.Dot(hit.normal, Vector3.up) < 0.7f
                    || !IsFloorSurface(room.transform, hit.collider))
                    continue;

                grounded = hit.point;
                if (IsPlayerVolumeClear(room, grounded))
                    return true;
            }

            grounded = default;
            return false;
        }

        private static bool IsFloorSurface(Transform room, Collider collider)
        {
            Transform current = collider.transform;
            while (current != null && current != room)
            {
                if (current.name.StartsWith("Floor", StringComparison.OrdinalIgnoreCase))
                    return true;
                current = current.parent;
            }

            Bounds bounds = collider.bounds;
            float minHorizontal = Mathf.Min(bounds.size.x, bounds.size.z);
            return minHorizontal > 0.01f && bounds.size.y <= minHorizontal * 0.6f;
        }

        private static bool IsPlayerVolumeClear(GameObject room, Vector3 ground)
        {
            const float radius = 0.45f;
            const float height = 1.8f;
            const float groundClearance = 0.06f;
            Vector3 bottom = ground + Vector3.up * (radius + groundClearance);
            Vector3 top = ground + Vector3.up * (height - radius + groundClearance);
            var overlaps = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (overlap != null && overlap.transform.IsChildOf(room.transform))
                    return false;
            }

            return true;
        }

        private void ValidateConnectedDoorClearance(
            IReadOnlyList<RoomInstanceGrid3D> instances)
        {
            if (_generatedRoot == null)
                return;

            Physics.SyncTransforms();
            Transform generatedRoot = _generatedRoot.transform;
            foreach (var instance in instances)
            {
                if (instance.IsCorridor) continue;
                foreach (var door in instance.Doors)
                {
                    var handler = door.DoorHandler;
                    if (handler == null) continue;

                    Vector3 inward = instance.RoomTemplateInstance.transform.TransformDirection(
                        (Vector3)handler.DirectionVector);
                    inward.y = 0f;
                    if (inward.sqrMagnitude < 0.001f) continue;
                    Vector3 outward = -inward.normalized;
                    Vector3 origin = handler.transform.position + Vector3.up * 1.2f;
                    var hits = Physics.SphereCastAll(
                        origin,
                        0.45f,
                        outward,
                        DoorClearanceCheckDistance,
                        ~0,
                        QueryTriggerInteraction.Ignore);
                    foreach (var hit in hits)
                    {
                        if (hit.collider == null
                            || !hit.collider.transform.IsChildOf(generatedRoot))
                            continue;

                        throw new InvalidOperationException(
                            $"Edgar 连接门通行体积被阻挡：Node={GetNodeName(instance)}，" +
                            $"Door={handler.name}，Collider={hit.collider.name}，" +
                            $"Distance={hit.distance:F2}。");
                    }
                }
            }
        }

        private void CreateRoomTrigger(int roomIndex, Bounds bounds)
        {
            var go = new GameObject($"房间触发器_{roomIndex:00}");
            go.transform.SetParent(_triggerRoot.transform, false);
            go.transform.position = bounds.center;

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(2f, bounds.size.x * 0.7f),
                Mathf.Max(3f, bounds.size.y),
                Mathf.Max(2f, bounds.size.z * 0.7f));

            go.AddComponent<EdgarRoomTrigger>().Initialize(roomIndex);
        }

        private static Bounds GetBounds(GameObject room, Vector3 fallbackSize)
        {
            var renderers = room.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }

            var colliders = room.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                var bounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    bounds.Encapsulate(colliders[i].bounds);
                return bounds;
            }

            return new Bounds(room.transform.position, Vector3.Max(fallbackSize, new Vector3(10f, 3f, 10f)));
        }

        public void SetRoomLocked(int index, bool locked)
        {
            if (!TryGetPlacement(index, out var placement) || placement.Instance == null)
                return;

            foreach (var door in placement.Instance.Doors)
            {
                var handler = door.DoorHandler;
                if (handler == null) continue;

                var gate = handler.transform.Find("__CombatGate");
                if (locked && gate == null)
                {
                    var gateObject = new GameObject("__CombatGate");
                    gate = gateObject.transform;
                    gate.SetParent(handler.transform, false);
                    var collider = gateObject.AddComponent<BoxCollider>();
                    var cell = handler.GeneratorSettings != null
                        ? handler.GeneratorSettings.CellSize
                        : Vector3.one;
                    collider.size = new Vector3(
                        Mathf.Max(0.5f, handler.Width * cell.x),
                        Mathf.Max(1f, handler.Height * cell.y),
                        0.5f);

                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.name = "__CombatGateVisual";
                    visual.transform.SetParent(gate, false);
                    visual.transform.localScale = collider.size;
                    var visualCollider = visual.GetComponent<Collider>();
                    if (visualCollider != null)
                        Destroy(visualCollider);
                    var renderer = visual.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.sharedMaterial = GetCombatGateMaterial();
                }
                else if (!locked && gate != null)
                {
                    Destroy(gate.gameObject);
                }
            }
        }

        private static Material GetCombatGateMaterial()
        {
            if (_combatGateMaterial != null)
                return _combatGateMaterial;

            _combatGateMaterial = new Material(MaterialHelper.GetLitShader())
            {
                name = "战斗封锁_临时材质",
                color = new Color(0.95f, 0.08f, 0.04f),
                hideFlags = HideFlags.DontSave,
            };
            _combatGateMaterial.EnableKeyword("_EMISSION");
            _combatGateMaterial.SetColor(
                "_EmissionColor",
                new Color(1f, 0.03f, 0.01f) * 1.5f);
            return _combatGateMaterial;
        }

        private void OnDestroy()
        {
            if (_generatedRoot != null)
                Destroy(_generatedRoot);
        }
    }

    public readonly struct EdgarRoomPlacement
    {
        public readonly Vector3 SpawnPosition;
        public readonly float RoomSize;
        public readonly RoomInstanceGrid3D Instance;
        public readonly string NodeName;

        public EdgarRoomPlacement(
            Vector3 spawnPosition,
            float roomSize,
            RoomInstanceGrid3D instance,
            string nodeName)
        {
            SpawnPosition = spawnPosition;
            RoomSize = roomSize;
            Instance = instance;
            NodeName = nodeName;
        }
    }
}
