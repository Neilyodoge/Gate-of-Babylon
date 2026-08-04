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
        private const string LevelGraphPath = "LevelDesign/EdgarGrid3D/PrototypeLevelGraph";
        private const string GeneratorSettingsPath = "LevelDesign/EdgarGrid3D/GeneratorSettings";

        private readonly List<EdgarRoomPlacement> _rooms = new();
        private DungeonGeneratorGrid3D _generator;
        private GameObject _generatedRoot;
        private int _activeRoom = -1;

        public IReadOnlyList<EdgarRoomPlacement> Rooms => _rooms;
        public int RoomCount => _rooms.Count;
        public bool IsReady => _generatedRoot != null && _rooms.Count > 0;

        public bool Generate(int seed)
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
            if (_generator == null)
                _generator = gameObject.AddComponent<DungeonGeneratorGrid3D>();

            _generator.GenerateOn = GenerateOn.Manually;
            _generator.UseRandomSeed = false;
            _generator.RandomGeneratorSeed = seed;
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

            try
            {
                var payload = (DungeonGeneratorPayloadGrid3D)_generator.Generate();
                _generatedRoot = payload.GeneratedLevel.RootGameObject;
                if (_generatedRoot != null)
                    _generatedRoot.name = $"EdgarDungeon_Seed{payload.Seed}";

                BuildRoomOrder(payload.GeneratedLevel.RoomInstances, settings);
                Debug.Log($"<color=#66ccff>[Edgar] Grid3D 实体地牢生成完成：{_rooms.Count} 个房间，Seed={payload.Seed}</color>");
                return IsReady;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Clear();
                return false;
            }
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

        public void ActivateRoom(int index)
        {
            _activeRoom = index;
            SetRoomLocked(index, true);
        }

        public void UnlockActiveRoom()
        {
            if (_activeRoom >= 0)
                SetRoomLocked(_activeRoom, false);
        }

        public void Clear()
        {
            _rooms.Clear();
            _activeRoom = -1;
            if (_generatedRoot != null)
                Destroy(_generatedRoot);
            _generatedRoot = null;
        }

        private void BuildRoomOrder(IReadOnlyList<RoomInstanceGrid3D> instances, GeneratorSettingsGrid3D settings)
        {
            var regularRooms = new List<RoomInstanceGrid3D>();
            foreach (var instance in instances)
            {
                if (!instance.IsCorridor)
                    regularRooms.Add(instance);
            }

            // 生成器返回字典顺序并非契约。按世界坐标稳定排序，保证同一 Seed 的房间索引稳定。
            regularRooms.Sort((a, b) =>
            {
                int z = a.RoomTemplateInstance.transform.position.z.CompareTo(b.RoomTemplateInstance.transform.position.z);
                return z != 0
                    ? z
                    : a.RoomTemplateInstance.transform.position.x.CompareTo(b.RoomTemplateInstance.transform.position.x);
            });

            foreach (var instance in regularRooms)
            {
                var bounds = GetBounds(instance.RoomTemplateInstance, settings.CellSize);
                _rooms.Add(new EdgarRoomPlacement(
                    new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z),
                    Mathf.Max(8f, Mathf.Min(bounds.size.x, bounds.size.z)),
                    instance));
            }
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

        private void SetRoomLocked(int index, bool locked)
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
                }
                else if (!locked && gate != null)
                {
                    Destroy(gate.gameObject);
                }
            }
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

        public EdgarRoomPlacement(Vector3 spawnPosition, float roomSize, RoomInstanceGrid3D instance)
        {
            SpawnPosition = spawnPosition;
            RoomSize = roomSize;
            Instance = instance;
        }
    }
}
