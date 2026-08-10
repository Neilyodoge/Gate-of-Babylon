using System;
using System.Collections.Generic;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// Edgar Grid3D 地图提供者。实体地牢本身就是进度载体，因此不再弹出独立地图 UI。
    /// </summary>
    public sealed class EdgarMapProvider : IMapProvider
    {
        public const int RequiredBossCount = 1;

        private readonly List<IReadOnlyList<RoomType>> _floors = new();
        private EdgarDungeonRuntime _runtime;
        private int _realm;
        private int _roomIndex;
        private int _runSeed;
        private string _spawnNodeName = "O2";
        private string _bossNodeName = "I4";
        private readonly Dictionary<string, int> _eventIdsByNode = new();

        // 首测只从各区离连接区商店最远的普通房降落；房内落点与整图朝向仍随机。
        private static readonly string[] OuterLandingCandidates = { "O4" };
        private static readonly string[] InnerLandingCandidates = { "I3" };

        public bool IsReady => Runtime != null && Runtime.IsReady;
        public int CurrentActId => _realm + 1;
        public bool CurrentNodeHasNext => IsReady && _roomIndex < Runtime.RoomCount - 1;

        private EdgarDungeonRuntime Runtime
        {
            get
            {
                if (_runtime != null) return _runtime;

                var existing = UnityEngine.Object.FindFirstObjectByType<EdgarDungeonRuntime>();
                if (existing != null)
                    return _runtime = existing;

                var go = new GameObject("Edgar 地牢运行时");
                var systems = GameObject.Find("Systems");
                if (systems != null)
                    go.transform.SetParent(systems.transform);
                return _runtime = go.AddComponent<EdgarDungeonRuntime>();
            }
        }

        public void StartRun()
        {
            _realm = 0;
            _roomIndex = 0;
            ConfigureRunVariant();
            LevelDesignDirector.Instance.StartNewRun();
            Runtime.Clear();
            RebuildFloors(Runtime.ConfiguredRoomCount);
        }

        public void OnEnterRealm(int realm)
        {
            _realm = Mathf.Max(0, realm);
            _roomIndex = 0;
            ConfigureRunVariant();
            LevelDesignDirector.Instance.BeginAct(_realm + 1);
            Runtime.Clear();
            RebuildFloors(Runtime.ConfiguredRoomCount);
        }

        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors() => _floors;

        public float GetEnemyScale(int floor) => WithStructure(floor, (s, f) => s.GetEnemyScale(f), 1f);
        public int GetRarityBias(int floor) => WithStructure(floor, (s, f) => s.GetRarityBias(f), 0);
        public bool GetHasStageReturn(int floor) => WithStructure(floor, (s, f) => s.GetHasStageReturn(f), true);

        public void TryTriggerRoomEvent(Action onCompleted)
        {
            string nodeName = Runtime.GetNodeName(_roomIndex);
            if (!_eventIdsByNode.TryGetValue(nodeName, out int eventID))
                throw new InvalidOperationException(
                    $"事件节点 {nodeName} 未分配 EventID：Realm={_realm}, Seed={_runSeed}。");

            bool triggered = StoryEventService.Instance.TryTriggerEvent(
                eventID,
                _ => onCompleted?.Invoke());
            if (!triggered)
                throw new InvalidOperationException(
                    $"事件 {eventID} 无法触发：Node={nodeName}, Realm={_realm}, Seed={_runSeed}。");
        }

        public void MarkCurrentCleared()
        {
            Runtime.UnlockActiveRoom();
        }

        public bool TryShowNavigation(bool bossNext, Action<RoomType> onChosen)
        {
            // Edgar 的实体门与走廊承担导航，不再显示独立的 STS 地图。
            return false;
        }

        public bool TryGetCurrentPlacement(out EdgarRoomPlacement placement)
        {
            if (!Runtime.IsReady)
                GenerateRealm();

            bool found = Runtime.TryGetPlacement(_roomIndex, out placement);
            if (found)
                Runtime.ActivateRoom(_roomIndex);
            return found;
        }

        public void SelectRoom(int roomIndex)
        {
            _roomIndex = Mathf.Clamp(roomIndex, 0, Mathf.Max(0, Runtime.ConfiguredRoomCount - 1));
        }

        public bool TryFindRoomIndex(RoomType roomType, out int roomIndex)
        {
            if (!Runtime.IsReady)
                GenerateRealm();

            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                if (ResolveRoomType(Runtime.GetNodeName(i)) != roomType)
                    continue;
                roomIndex = i;
                return true;
            }

            roomIndex = -1;
            return false;
        }

        public void ClearDungeon()
        {
            if (_runtime != null)
                _runtime.Clear();
            _roomIndex = 0;
        }

        private void GenerateRealm()
        {
            bool generated = Runtime.Generate(_runSeed, _spawnNodeName);
            RebuildFloors(generated ? Runtime.RoomCount : 0);
            if (generated)
                BuildLandmarkLabels();
        }

        private void BuildLandmarkLabels()
        {
            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                if (!Runtime.TryGetPlacement(i, out EdgarRoomPlacement placement)
                    || placement.Instance?.RoomTemplateInstance == null)
                    continue;

                string nodeName = placement.NodeName;
                string text = null;
                Color color = Color.white;
                if (nodeName == _bossNodeName)
                {
                    text = "Boss 房";
                    color = new Color(1f, 0.22f, 0.18f);
                }
                else if (nodeName == _spawnNodeName)
                {
                    text = "安全降落点";
                    color = new Color(0.2f, 0.9f, 1f);
                }
                else if (nodeName == "O3" || nodeName == "I2")
                {
                    text = "精英房";
                    color = new Color(1f, 0.62f, 0.12f);
                }
                else if (nodeName == "O1" || nodeName == "I1")
                {
                    text = "事件房";
                    color = new Color(0.3f, 0.86f, 1f);
                }
                else if (nodeName == "C0")
                {
                    text = "商店";
                    color = new Color(0.28f, 1f, 0.4f);
                }
                else if (nodeName == "O0" || nodeName == "I4")
                {
                    text = "地标";
                    color = new Color(0.78f, 0.58f, 1f);
                }

                if (text != null)
                    DungeonLandmarkLabel.Create(
                        placement.Instance.RoomTemplateInstance.transform,
                        text,
                        color);
            }
        }

        private void RebuildFloors(int generatedRoomCount)
        {
            _floors.Clear();
            int realms = GameManager.Instance != null ? GameManager.Instance.RealmCount : 3;
            int roomCount = Mathf.Max(1, generatedRoomCount);

            for (int realm = 0; realm < realms; realm++)
            {
                var rooms = new List<RoomType>(roomCount);
                for (int i = 0; i < roomCount; i++)
                    rooms.Add(ResolveRoomType(GetOrderedNodeName(i, roomCount)));
                _floors.Add(rooms);
            }
        }

        private void ConfigureRunVariant()
        {
            // 使用正整数种子便于日志复现；Guid 避免同一毫秒内重复初始化得到相同布局。
            _runSeed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            if (_runSeed == 0)
                _runSeed = 1;
            bool outerSpawn = (_runSeed & 1) == 0;
            string[] candidates = outerSpawn ? OuterLandingCandidates : InnerLandingCandidates;
            int candidateIndex = (int)(((uint)_runSeed >> 1) % candidates.Length);
            _spawnNodeName = candidates[candidateIndex];
            _bossNodeName = outerSpawn ? "I4" : "O0";
            ConfigureRoomEvents();
        }

        private void ConfigureRoomEvents()
        {
            _eventIdsByNode.Clear();
            int actPrefix = (_realm + 1) * 1000;
            var candidates = new List<int>();
            foreach (var pair in ConfigDatabase.Instance.StoryEvents)
            {
                var row = pair.Value;
                if (pair.Key > actPrefix
                    && pair.Key < actPrefix + 1000
                    && string.IsNullOrWhiteSpace(row.PrereqFlag)
                    && row.Options != null
                    && row.Options.Length > 0)
                    candidates.Add(pair.Key);
            }

            candidates.Sort();
            if (candidates.Count < 2)
                throw new InvalidOperationException(
                    $"第 {_realm + 1} 层至少需要 2 个无前置条件事件，当前只有 {candidates.Count} 个。");

            int start = (int)((uint)_runSeed % candidates.Count);
            _eventIdsByNode["O1"] = candidates[start];
            _eventIdsByNode["I1"] = candidates[(start + 1) % candidates.Count];
        }

        private string GetOrderedNodeName(int index, int roomCount)
        {
            if (Runtime.IsReady)
                return Runtime.GetNodeName(index);

            string[] order =
            {
                "O0", "O1", "O2", "O3", "O4", "C0",
                "C1", "I0", "I1", "I2", "I3", "I4",
            };
            if (roomCount != order.Length || index < 0 || index >= order.Length)
                return string.Empty;
            bool reverse = _spawnNodeName.StartsWith("I", StringComparison.Ordinal);
            int preferredIndex = Array.IndexOf(order, _spawnNodeName);
            if (index == 0 && preferredIndex >= 0)
                return _spawnNodeName;

            int current = 0;
            for (int i = 0; i < order.Length; i++)
            {
                int orderedIndex = reverse ? order.Length - 1 - i : i;
                if (orderedIndex == preferredIndex)
                    continue;
                current++;
                if (current == index)
                    return order[orderedIndex];
            }
            return string.Empty;
        }

        private RoomType ResolveRoomType(string nodeName)
        {
            if (nodeName == _bossNodeName) return RoomType.Boss;
            if (nodeName == _spawnNodeName) return RoomType.Landing;
            if (nodeName == "O3" || nodeName == "I2") return RoomType.Elite;
            if (nodeName == "O1" || nodeName == "I1") return RoomType.Event;
            if (nodeName == "C0") return RoomType.Shop;
            return RoomType.Battle;
        }

        private static T WithStructure<T>(int floor, Func<MapStructureRow, int, T> selector, T fallback)
        {
            var db = ConfigDatabase.Instance;
            if (db == null) return fallback;

            int actId = (GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0) + 1;
            foreach (var pair in db.MapStructures)
            {
                if (pair.Value.ActID == actId)
                    return selector(pair.Value, floor);
            }

            return fallback;
        }
    }
}
