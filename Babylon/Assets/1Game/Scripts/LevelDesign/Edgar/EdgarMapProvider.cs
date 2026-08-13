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
        public string CurrentStoryTemplateID => StoryTemplateRuntime.Current?.ID;
        public LevelAPhase CurrentPhase => LevelAPhaseRuntime.CurrentPhase;
        public IReadOnlyDictionary<string, int> AssignedRoomEvents => _eventIdsByNode;
        public int CurrentEventID
        {
            get
            {
                if (!IsReady)
                    return 0;
                string nodeName = Runtime.GetNodeName(_roomIndex);
                return _eventIdsByNode.TryGetValue(nodeName, out int eventID)
                    ? eventID
                    : 0;
            }
        }

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
            LevelDesignDirector.Instance.StartNewRun();
            if (LevelAPhaseRuntime.IsNightPending)
                RestoreRunVariant();
            else
                ConfigureRunVariant();
            Runtime.Clear();
            RebuildFloors(Runtime.ConfiguredRoomCount);
        }

        public void EnterNightPhase()
        {
            if (!LevelAPhaseRuntime.IsNightPending)
                throw new InvalidOperationException("无法进入永夜：白昼阶段尚未提交。");

            _realm = 0;
            _roomIndex = 0;
            RestoreRunVariant();
            Runtime.Clear();
            RebuildFloors(Runtime.ConfiguredRoomCount);
        }

        public void OnEnterRealm(int realm)
        {
            _realm = Mathf.Max(0, realm);
            _roomIndex = 0;
            LevelDesignDirector.Instance.BeginAct(_realm + 1);
            ConfigureRunVariant();
            Runtime.Clear();
            RebuildFloors(Runtime.ConfiguredRoomCount);
        }

        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors() => _floors;

        public float GetEnemyScale(int floor) => WithStructure(floor, (s, f) => s.GetEnemyScale(f), 1f);
        public int GetRarityBias(int floor) => WithStructure(floor, (s, f) => s.GetRarityBias(f), 0);
        public bool GetHasStageReturn(int floor) => WithStructure(floor, (s, f) => s.GetHasStageReturn(f), true);

        public void TryTriggerRoomEvent(Action<EventOption> onCompleted)
        {
            string nodeName = Runtime.GetNodeName(_roomIndex);
            if (!_eventIdsByNode.TryGetValue(nodeName, out int eventID))
                throw new InvalidOperationException(
                    $"事件节点 {nodeName} 未分配 EventID：Realm={_realm}, Seed={_runSeed}。");

            bool triggered = StoryEventService.Instance.TryTriggerEvent(
                eventID,
                selected => onCompleted?.Invoke(selected));
            if (!triggered)
                throw new InvalidOperationException(
                    $"事件 {eventID} 无法触发：Node={nodeName}, Realm={_realm}, Seed={_runSeed}。");
        }

        public void CompleteCurrentRoomEvent(EventOption selected)
        {
            string nodeName = Runtime.GetNodeName(_roomIndex);
            if (!_eventIdsByNode.TryGetValue(nodeName, out int eventID))
                throw new InvalidOperationException(
                    $"完成事件时节点 {nodeName} 未分配 EventID。");
            LevelAPhaseRuntime.RecordOutcome(nodeName, eventID, selected);
        }

        public bool IsCurrentEventRecorded()
        {
            string nodeName = Runtime.GetNodeName(_roomIndex);
            return _eventIdsByNode.TryGetValue(nodeName, out int eventID)
                   && LevelAPhaseRuntime.HasRecordedOutcome(nodeName, eventID);
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
            LevelAPhaseRuntime.SetNightMapActive(false);
        }

        private void GenerateRealm()
        {
            bool generated = Runtime.Generate(_runSeed, _spawnNodeName);
            RebuildFloors(generated ? Runtime.RoomCount : 0);
            if (generated)
            {
                LevelAPhaseRuntime.SetNightMapActive(LevelAPhaseRuntime.IsNightPending);
                BuildLandmarkLabels();
                ConfigureEventVariantRoots();
                ApplyNightOutcomes();
                LevelAPhaseVisuals.Apply(LevelAPhaseRuntime.CurrentPhase);
            }
        }

        private void ConfigureEventVariantRoots()
        {
            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                if (!Runtime.TryGetPlacement(i, out var placement)
                    || !_eventIdsByNode.TryGetValue(placement.NodeName, out int eventID))
                    continue;
                Transform roomRoot = placement.Instance?.RoomTemplateInstance?.transform;
                if (roomRoot == null)
                    continue;

                bool active = eventID == 1004
                              || (LevelAPhaseRuntime.IsNightPending && eventID == 1006);
                DungeonEventVariantRoot.ActivateOnly(roomRoot, active ? eventID : -1);
            }
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
                    int eventID = _eventIdsByNode.TryGetValue(nodeName, out int assigned)
                        ? assigned
                        : 0;
                    text = eventID switch
                    {
                        1004 when LevelAPhaseRuntime.IsNightPending => "巡礼桥遗址",
                        1004 => "断裂巡礼桥",
                        1006 when LevelAPhaseRuntime.IsNightPending => "禁卫召集阵",
                        1006 => "封闭阵室",
                        _ => "事件房",
                    };
                    color = eventID == 1006
                        ? new Color(0.72f, 0.34f, 1f)
                        : new Color(0.3f, 0.86f, 1f);
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
            StoryTemplateRuntime.SelectForAct(_realm + 1, _runSeed);
            ConfigureRoomEvents();
            LevelAPhaseRuntime.BeginNewDay(
                _runSeed,
                _spawnNodeName,
                _bossNodeName,
                StoryTemplateRuntime.Current?.ID);
        }

        private void RestoreRunVariant()
        {
            if (!LevelAPhaseRuntime.TryGetMapVariant(
                    out _runSeed,
                    out _spawnNodeName,
                    out _bossNodeName))
                throw new InvalidOperationException("永夜阶段缺少可复现的地图 Seed、出生节点或 Boss 节点。");

            StoryTemplateRuntime.SelectForAct(_realm + 1, _runSeed);
            ConfigureRoomEvents();
            LevelAPhaseRuntime.RestorePendingFlags();
            Debug.Log(
                $"[无暮王城] 恢复永夜地图：Seed={_runSeed}，" +
                $"Landing={_spawnNodeName}，Boss={_bossNodeName}");
        }

        private void ApplyNightOutcomes()
        {
            if (!LevelAPhaseRuntime.IsNightPending)
                return;

            foreach (var outcome in LevelAPhaseRuntime.GetPendingOutcomes())
            {
                if (outcome == null
                    || outcome.sceneResult == (int)EventSceneResult.None)
                    continue;

                EdgarRoomPlacement target = default;
                bool foundTarget = false;
                for (int i = 0; i < Runtime.RoomCount; i++)
                {
                    if (!Runtime.TryGetPlacement(i, out var placement)
                        || placement.NodeName != outcome.nodeName)
                        continue;
                    target = placement;
                    foundTarget = true;
                    break;
                }

                Transform roomRoot = foundTarget
                    ? target.Instance?.RoomTemplateInstance?.transform
                    : null;
                if (!foundTarget || roomRoot == null)
                    throw new InvalidOperationException(
                        $"永夜事件结果无法落位：Node={outcome.nodeName}，Event={outcome.eventId}。");

                Vector3 position = roomRoot.position;
                foreach (var socket in roomRoot.GetComponentsInChildren<DungeonContentSocket>(true))
                {
                    if (socket.SocketType != DungeonContentSocketType.Event)
                        continue;
                    position = socket.transform.position;
                    break;
                }

                EventSceneOutcome.Apply(
                    new EventOption
                    {
                        SceneResult = (EventSceneResult)outcome.sceneResult,
                        FlagName = outcome.flagName,
                        FlagValue = outcome.flagValue,
                    },
                    roomRoot,
                    position);
            }
        }

        private void ConfigureRoomEvents()
        {
            _eventIdsByNode.Clear();
            if (!ConfigDatabase.Instance.StoryEvents.ContainsKey(1004)
                || !ConfigDatabase.Instance.StoryEvents.ContainsKey(1006))
                throw new InvalidOperationException(
                    "无暮王城 MVP 需要事件 1004“断裂巡礼桥”和 1006“禁卫召集阵”。");

            bool spawnInOuter = _spawnNodeName.StartsWith("O", StringComparison.Ordinal);
            string layoutNode = spawnInOuter ? "O1" : "I1";
            string strengthNode = spawnInOuter ? "I1" : "O1";
            _eventIdsByNode[layoutNode] = 1004;
            _eventIdsByNode[strengthNode] = 1006;
            Debug.Log(
                $"[无暮王城事件] 白昼 Layout=1004@{layoutNode} | " +
                $"永夜 Strength=1006@{strengthNode}");
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
            if (_eventIdsByNode.TryGetValue(nodeName, out int eventID))
            {
                if (!LevelAPhaseRuntime.IsNightPending && eventID == 1004)
                    return RoomType.Event;
                if (LevelAPhaseRuntime.IsNightPending && eventID == 1006)
                    return RoomType.Event;
            }
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
