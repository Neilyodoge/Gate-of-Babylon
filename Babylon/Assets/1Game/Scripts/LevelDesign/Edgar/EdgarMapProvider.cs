using System;
using System.Collections.Generic;
using Edgar.Unity;
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
        private DungeonLayoutCandidate _layout;
        private readonly Dictionary<string, int> _eventIdsByNode = new();

        public bool IsReady => Runtime != null && Runtime.IsReady;
        public int CurrentActId => _realm + 1;
        public bool CurrentNodeHasNext => IsReady && _roomIndex < Runtime.RoomCount - 1;
        public string CurrentStoryTemplateID => StoryTemplateRuntime.Current?.ID;
        public LevelAPhase CurrentPhase => LevelAPhaseRuntime.CurrentPhase;
        public IReadOnlyDictionary<string, int> AssignedRoomEvents => _eventIdsByNode;
        public bool TryGetEventNode(int eventID, out string nodeName)
        {
            foreach (var pair in _eventIdsByNode)
            {
                if (pair.Value != eventID)
                    continue;
                nodeName = pair.Key;
                return true;
            }
            string layoutNode = _layout?.LayoutEventNodeName ?? "O1";
            string strengthNode = _layout?.StrengthEventNodeName ?? "I1";
            if (eventID == 1004 || eventID == 1007)
            {
                nodeName = layoutNode;
                return true;
            }
            if (eventID == 1005 || eventID == 1006)
            {
                nodeName = strengthNode;
                return true;
            }
            nodeName = null;
            return false;
        }
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
            RebuildFloors(EdgarDungeonRuntime.GetConfiguredRoomCount(_runSeed));
        }

        public void EnterNightPhase()
        {
            if (!LevelAPhaseRuntime.IsNightPending)
                throw new InvalidOperationException("无法进入永夜：白昼阶段尚未提交。");

            _realm = 0;
            _roomIndex = 0;
            RestoreRunVariant();
            Runtime.Clear();
            RebuildFloors(EdgarDungeonRuntime.GetConfiguredRoomCount(_runSeed));
        }

        public void OnEnterRealm(int realm)
        {
            _realm = Mathf.Max(0, realm);
            _roomIndex = 0;
            LevelDesignDirector.Instance.BeginAct(_realm + 1);
            ConfigureRunVariant();
            Runtime.Clear();
            RebuildFloors(EdgarDungeonRuntime.GetConfiguredRoomCount(_runSeed));
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
            ApplyGlobalEventResult(nodeName, selected);
            if (eventID == 1004)
                RefreshOptionalBranchAccess();
            if (ChangesNavigation(selected.SceneResult))
                Runtime.RebuildNavigation();
            LevelAPhaseRuntime.RecordOutcome(nodeName, eventID, selected);
        }

        public bool DebugCompleteLayoutEvent(out string message)
        {
            if (!IsReady)
            {
                message = "请先进入并生成 Edgar 秘境。";
                return false;
            }

            bool night = LevelAPhaseRuntime.IsNightPending;
            int eventID = night ? 1007 : 1004;
            string nodeName = _layout?.LayoutEventNodeName ?? "O1";
            if (LevelAPhaseRuntime.HasRecordedOutcome(nodeName, eventID))
            {
                message = night
                    ? "本阶段狱城升降井已经完成。"
                    : "本阶段断裂巡礼桥已经完成。";
                return false;
            }

            if (!ConfigDatabase.Instance.StoryEvents.TryGetValue(
                    eventID,
                    out StoryEventRow row)
                || row.Options == null
                || row.Options.Length == 0)
            {
                message = night
                    ? "狱城升降井缺少可用选项。"
                    : "断裂巡礼桥缺少可用选项。";
                return false;
            }

            EventOption source = row.Options[0];
            EventOption selected;
            if (night)
            {
                selected = source;
            }
            else
            {
                // 正常流程的稳定修复还包含两轮守桥战；Debug 直接写最终结果，
                // 避免留下 pending Flag 或仍在等待增援清场。
                selected = new EventOption
                {
                    Text = "Debug：稳定修复并直接放下巡礼桥",
                    FlagName = "bridge_opened",
                    FlagValue = 1,
                    RewardID = source.RewardID,
                    CostID = source.CostID,
                    KarmaChange = source.KarmaChange,
                    DaoxinChange = source.DaoxinChange,
                    LifespanChange = source.LifespanChange,
                    SceneResult = EventSceneResult.OpenRoute,
                };
            }

            if (!StoryEventService.Instance.DebugCompleteEvent(eventID, selected))
            {
                message = $"无法直接完成事件 {eventID}。";
                return false;
            }

            if (TryGetRoomRoot(nodeName, out Transform roomRoot))
            {
                Vector3 position = roomRoot.position;
                foreach (DungeonContentSocket socket in
                         roomRoot.GetComponentsInChildren<DungeonContentSocket>(true))
                {
                    if (socket.SocketType != DungeonContentSocketType.Event)
                        continue;
                    position = socket.transform.position;
                    break;
                }
                EventSceneOutcome.Apply(selected, roomRoot, position);
            }

            ApplyGlobalEventResult(nodeName, selected);
            if (eventID == 1004)
                RefreshOptionalBranchAccess();
            if (ChangesNavigation(selected.SceneResult))
                Runtime.RebuildNavigation();
            LevelAPhaseRuntime.RecordOutcome(nodeName, eventID, selected);

            foreach (EventRoomContentHandler handler in
                     UnityEngine.Object.FindObjectsByType<EventRoomContentHandler>(
                         FindObjectsSortMode.None))
                if (handler.EventID == eventID)
                    handler.DebugMarkCompleted();

            ConfigureEventVariantRoots();
            message = night
                ? "已直接修复狱城升降井并开启双向捷径。"
                : "已直接稳定修复巡礼桥并开放桥后支路。";
            return true;
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
            int roomCount = Runtime.IsReady
                ? Runtime.RoomCount
                : EdgarDungeonRuntime.GetConfiguredRoomCount(_runSeed);
            _roomIndex = Mathf.Clamp(roomIndex, 0, Mathf.Max(0, roomCount - 1));
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
                _layout = Runtime.CurrentLayout ?? _layout;
                LevelAPhaseRuntime.SetNightMapActive(LevelAPhaseRuntime.IsNightPending);
                RegisterInjectedEvents();
                BuildLandmarkLabels();
                ConfigureEventVariantRoots();
                ApplyNightOutcomes();
                RefreshConfiguredShortcuts();
                RefreshOptionalBranchAccess();
                if (!Runtime.RebuildNavigation())
                    throw new InvalidOperationException("Edgar 地牢导航网格构建失败。");
                LevelAPhaseVisuals.Apply(LevelAPhaseRuntime.CurrentPhase);
            }
        }

        private static bool ChangesNavigation(EventSceneResult result)
        {
            return result == EventSceneResult.OpenRoute
                   || result == EventSceneResult.BridgeSabotaged
                   || result == EventSceneResult.NightLiftRestored
                   || result == EventSceneResult.NightLiftDropped;
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

                var activeEventIDs = new HashSet<int> { eventID };
                foreach (var outcome in LevelAPhaseRuntime.GetPendingOutcomes())
                {
                    if (outcome != null && outcome.nodeName == placement.NodeName)
                        activeEventIDs.Add(outcome.eventId);
                }
                DungeonEventVariantRoot.SetActiveEvents(roomRoot, activeEventIDs);
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
                if (Runtime.TryGetInjectedRoomInfo(
                        nodeName,
                        out DungeonInjectedRoomInfo injected)
                    && !string.IsNullOrWhiteSpace(injected.LandmarkLabel))
                {
                    text = injected.LandmarkLabel;
                    color = new Color(0.82f, 0.62f, 1f);
                }
                else if (nodeName == _bossNodeName)
                {
                    text = "Boss 房";
                    color = new Color(1f, 0.22f, 0.18f);
                }
                else if (nodeName == _spawnNodeName)
                {
                    text = "安全降落点";
                    color = new Color(0.2f, 0.9f, 1f);
                }
                else if (_layout?.IsEliteNode(nodeName)
                         ?? (nodeName == "O3" || nodeName == "I2"))
                {
                    text = "精英房";
                    color = new Color(1f, 0.62f, 0.12f);
                }
                else if (nodeName == (_layout?.LayoutEventNodeName ?? "O1")
                         || nodeName == (_layout?.StrengthEventNodeName ?? "I1"))
                {
                    int eventID = _eventIdsByNode.TryGetValue(nodeName, out int assigned)
                        ? assigned
                        : 0;
                    text = eventID switch
                    {
                        1004 when LevelAPhaseRuntime.IsNightPending => "巡礼桥遗址",
                        1004 => "断裂巡礼桥",
                        1005 => "冠光仪",
                        1006 when LevelAPhaseRuntime.IsNightPending => "禁卫召集阵",
                        1007 => "狱城升降井",
                        _ => "事件房",
                    };
                    if (Runtime.TryGetBuildingAssignment(
                            nodeName,
                            out DungeonBuildingAssignmentInfo building)
                        && !string.IsNullOrWhiteSpace(building.DisplayName))
                        text = $"{building.DisplayName}·{text}";
                    color = eventID == 1005 || eventID == 1006
                        ? new Color(0.72f, 0.34f, 1f)
                        : new Color(0.3f, 0.86f, 1f);
                }
                else if (nodeName ==
                         (_layout?.OptionalBranchTargetNodeName ?? "B0"))
                {
                    text = IsOptionalBranchOpen()
                        ? "巡礼封藏室"
                        : "未探索·桥后封藏室";
                    color = IsOptionalBranchOpen()
                        ? new Color(0.9f, 0.68f, 0.28f)
                        : new Color(0.55f, 0.5f, 0.45f);
                }
                else if (nodeName == (_layout?.ShopNodeName ?? "C0"))
                {
                    text = "商店";
                    color = new Color(0.28f, 1f, 0.4f);
                }
                else if (_layout?.IsLandmarkNode(nodeName)
                         ?? (nodeName == "O0" || nodeName == "I4"))
                {
                    text = "地标";
                    color = new Color(0.78f, 0.58f, 1f);
                }
                else if (Runtime.TryGetBuildingAssignment(
                             nodeName,
                             out DungeonBuildingAssignmentInfo assignedBuilding))
                {
                    text = assignedBuilding.DisplayName;
                    color = new Color(0.82f, 0.62f, 1f);
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
            bool alternate = !outerSpawn;
            _layout = EdgarDungeonRuntime.GetConfiguredLayout(_runSeed);
            _spawnNodeName = _layout?.ResolveStartNode(alternate)
                ?? (outerSpawn ? "O4" : "I3");
            _bossNodeName = _layout?.ResolveBossNode(alternate)
                ?? (outerSpawn ? "I4" : "O0");
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

            _layout = EdgarDungeonRuntime.GetConfiguredLayout(_runSeed);
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

                var option = new EventOption
                {
                    SceneResult = (EventSceneResult)outcome.sceneResult,
                    FlagName = outcome.flagName,
                    FlagValue = outcome.flagValue,
                };
                EventSceneOutcome.Apply(
                    option,
                    roomRoot,
                    position);
                ApplyGlobalEventResult(outcome.nodeName, option);
            }
        }

        private void ApplyGlobalEventResult(string nodeName, EventOption option)
        {
            if (option != null)
                RefreshConfiguredShortcuts();
        }

        private void RegisterInjectedEvents()
        {
            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                string nodeName = Runtime.GetNodeName(i);
                if (!Runtime.TryGetInjectedRoomInfo(
                        nodeName,
                        out DungeonInjectedRoomInfo injected)
                    || injected.EventID <= 0)
                    continue;
                if (!ConfigDatabase.Instance.StoryEvents.ContainsKey(injected.EventID))
                    throw new InvalidOperationException(
                        $"注入房 {nodeName} 引用了不存在的 EventID={injected.EventID}。");
                _eventIdsByNode[nodeName] = injected.EventID;
            }
        }

        private void RefreshConfiguredShortcuts()
        {
            DungeonGenerationProfile profile = DungeonGenerationProfile.Instance;
            if (profile?.Shortcuts == null)
                return;

            LevelPhaseMask phase = LevelAPhaseRuntime.IsNightMapActive
                ? LevelPhaseMask.Night
                : LevelPhaseMask.Day;
            foreach (DungeonShortcutRule rule in profile.Shortcuts)
            {
                if (rule == null || !rule.Enabled || (rule.AllowedPhases & phase) == 0
                    || !MatchesCurrentLayout(rule.LayoutIDs))
                    continue;
                if (!string.IsNullOrWhiteSpace(rule.RequiredFlags)
                    && !BossFlagSet.Instance.Evaluate(rule.RequiredFlags))
                    continue;
                if (!string.IsNullOrWhiteSpace(rule.BlockedFlags)
                    && BossFlagSet.Instance.Evaluate(rule.BlockedFlags))
                    continue;
                if (!TryGetRoomRoot(rule.SourceNodeName, out Transform sourceRoom)
                    || !TryGetRoomRoot(rule.TargetNodeName, out Transform targetRoom))
                {
                    Debug.LogWarning(
                        $"[地牢捷径] {rule.ID} 无法落位：{rule.SourceNodeName} -> {rule.TargetNodeName}。");
                    continue;
                }

                string ruleID = string.IsNullOrWhiteSpace(rule.ID)
                    ? $"{rule.SourceNodeName}_{rule.TargetNodeName}"
                    : rule.ID.Trim();
                CreateShortcutDirection(
                    sourceRoom,
                    targetRoom,
                    rule.SourceSocket,
                    rule.TargetSocket,
                    rule.ForwardTitle,
                    $"{ruleID}:Forward");
                if (rule.Bidirectional)
                {
                    CreateShortcutDirection(
                        targetRoom,
                        sourceRoom,
                        rule.TargetSocket,
                        rule.SourceSocket,
                        rule.ReverseTitle,
                        $"{ruleID}:Reverse");
                }
            }
        }

        private bool MatchesCurrentLayout(IReadOnlyList<string> layoutIDs)
        {
            if (layoutIDs == null || layoutIDs.Count == 0)
                return true;
            bool hasRestriction = false;
            foreach (string candidate in layoutIDs)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;
                hasRestriction = true;
                if (string.Equals(
                        candidate.Trim(),
                        _layout?.ID,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return !hasRestriction;
        }

        private static void CreateShortcutDirection(
            Transform sourceRoom,
            Transform targetRoom,
            DungeonContentSocketType sourceSocket,
            DungeonContentSocketType targetSocket,
            string title,
            string ruleKey)
        {
            foreach (DungeonShortcutPortal existing in
                     sourceRoom.GetComponentsInChildren<DungeonShortcutPortal>(true))
                if (existing.RuleKey == ruleKey)
                    return;

            Vector3 portalPosition = ResolveShortcutPoint(sourceRoom, sourceSocket);
            Vector3 targetPoint = ResolveShortcutPoint(targetRoom, targetSocket);
            Vector3 arrival = ResolveShortcutArrival(targetRoom, targetPoint);
            DungeonShortcutPortal.Create(
                portalPosition,
                arrival,
                string.IsNullOrWhiteSpace(title) ? "使用捷径" : title,
                sourceRoom,
                ruleKey);
        }

        private bool TryGetRoomRoot(string nodeName, out Transform roomRoot)
        {
            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                if (!Runtime.TryGetPlacement(i, out var placement)
                    || placement.NodeName != nodeName)
                    continue;
                roomRoot = placement.Instance?.RoomTemplateInstance?.transform;
                return roomRoot != null;
            }
            roomRoot = null;
            return false;
        }

        private static Vector3 ResolveShortcutPoint(
            Transform roomRoot,
            DungeonContentSocketType preferredType)
        {
            Vector3? authoredFallback = null;
            foreach (var socket in roomRoot.GetComponentsInChildren<DungeonContentSocket>(true))
            {
                if (socket.SocketType != preferredType)
                    continue;
                authoredFallback ??= socket.transform.position + Vector3.up * 0.1f;
                if (DungeonSpawnSafety.TryFindGroundedPoint(
                        roomRoot,
                        socket.transform.position,
                        0.45f,
                        1.8f,
                        0.08f,
                        out Vector3 grounded))
                    return grounded;
            }
            if (authoredFallback.HasValue)
                return authoredFallback.Value;
            if (DungeonSpawnSafety.TryFindGroundedPoint(
                    roomRoot,
                    roomRoot.position,
                    0.45f,
                    1.8f,
                    0.08f,
                    out Vector3 fallback))
                return fallback;
            throw new InvalidOperationException(
                $"地牢快捷通道在 {roomRoot.name} 找不到安全落点。");
        }

        private static Vector3 ResolveShortcutArrival(
            Transform roomRoot,
            Vector3 portalPosition)
        {
            Vector3 candidate = portalPosition + roomRoot.forward * 3f;
            return DungeonSpawnSafety.TryFindGroundedPoint(
                roomRoot,
                candidate,
                0.45f,
                1.8f,
                0.08f,
                out Vector3 grounded)
                ? grounded
                : portalPosition + Vector3.up * 0.1f;
        }

        private void ConfigureRoomEvents()
        {
            _eventIdsByNode.Clear();
            if (!ConfigDatabase.Instance.StoryEvents.ContainsKey(1004)
                || !ConfigDatabase.Instance.StoryEvents.ContainsKey(1005)
                || !ConfigDatabase.Instance.StoryEvents.ContainsKey(1006)
                || !ConfigDatabase.Instance.StoryEvents.ContainsKey(1007))
                throw new InvalidOperationException(
                    "无暮王城 MVP 需要事件 1004～1007 四项昼夜事件。");

            string layoutNode = _layout?.LayoutEventNodeName ?? "O1";
            string strengthNode = _layout?.StrengthEventNodeName ?? "I1";
            if (string.IsNullOrWhiteSpace(layoutNode)
                || string.IsNullOrWhiteSpace(strengthNode))
                throw new InvalidOperationException(
                    $"Layout={_layout?.ID ?? "Fallback"} 未配置路线事件或战斗事件节点。");
            bool night = LevelAPhaseRuntime.IsNightPending;
            _eventIdsByNode[layoutNode] = night ? 1007 : 1004;
            _eventIdsByNode[strengthNode] = night ? 1006 : 1005;
            Debug.Log(
                night
                    ? $"[无暮王城事件] 永夜 Layout=1007@{layoutNode} | Strength=1006@{strengthNode}"
                    : $"[无暮王城事件] 白昼 Layout=1004@{layoutNode} | Strength=1005@{strengthNode}");
        }

        private string GetOrderedNodeName(int index, int roomCount)
        {
            if (Runtime.IsReady)
                return Runtime.GetNodeName(index);

            List<string> order = BuildConfiguredNodeOrder();
            if (index < 0 || index >= order.Count)
                return string.Empty;
            return order[index];
        }

        private List<string> BuildConfiguredNodeOrder()
        {
            LevelGraph graph = _layout?.LevelGraph;
            if (graph == null)
                return new List<string>();

            var adjacency = new Dictionary<RoomBase, List<RoomBase>>();
            RoomBase start = null;
            foreach (RoomBase room in graph.Rooms)
            {
                if (room == null)
                    continue;
                adjacency[room] = new List<RoomBase>();
                if (room.GetDisplayName() == _spawnNodeName)
                    start = room;
            }
            foreach (ConnectionBase connection in graph.Connections)
            {
                if (connection?.From == null || connection.To == null
                    || !adjacency.ContainsKey(connection.From)
                    || !adjacency.ContainsKey(connection.To))
                    continue;
                adjacency[connection.From].Add(connection.To);
                adjacency[connection.To].Add(connection.From);
            }
            if (start == null)
                return new List<string>();

            var depths = new Dictionary<RoomBase, int> { [start] = 0 };
            var queue = new Queue<RoomBase>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                RoomBase current = queue.Dequeue();
                foreach (RoomBase next in adjacency[current])
                {
                    if (depths.ContainsKey(next))
                        continue;
                    depths[next] = depths[current] + 1;
                    queue.Enqueue(next);
                }
            }

            var ordered = new List<RoomBase>(adjacency.Keys);
            ordered.Sort((a, b) =>
            {
                int aDepth = depths.TryGetValue(a, out int ad) ? ad : int.MaxValue;
                int bDepth = depths.TryGetValue(b, out int bd) ? bd : int.MaxValue;
                if (aDepth != bDepth)
                    return aDepth.CompareTo(bDepth);
                return string.CompareOrdinal(a.GetDisplayName(), b.GetDisplayName());
            });
            var names = new List<string>(ordered.Count);
            foreach (RoomBase room in ordered)
                names.Add(room.GetDisplayName());
            return names;
        }

        private RoomType ResolveRoomType(string nodeName)
        {
            if (nodeName == _bossNodeName) return RoomType.Boss;
            if (nodeName == _spawnNodeName) return RoomType.Landing;
            if (Runtime.TryGetInjectedRoomInfo(
                    nodeName,
                    out DungeonInjectedRoomInfo injected))
            {
                return injected.Role switch
                {
                    RoomRole.Elite => RoomType.Elite,
                    RoomRole.Event => RoomType.Event,
                    RoomRole.Shop => RoomType.Shop,
                    RoomRole.Rest => RoomType.Rest,
                    RoomRole.Boss => RoomType.Boss,
                    RoomRole.Armory => RoomType.Treasure,
                    RoomRole.Landing => RoomType.Landing,
                    _ => RoomType.Battle,
                };
            }
            if (nodeName == (_layout?.OptionalBranchTargetNodeName ?? "B0"))
                return LevelAPhaseRuntime.IsNightPending ? RoomType.Elite : RoomType.Battle;
            if (_layout?.IsEliteNode(nodeName)
                ?? (nodeName == "O3" || nodeName == "I2"))
                return RoomType.Elite;
            if (_eventIdsByNode.TryGetValue(nodeName, out int eventID))
                return RoomType.Event;
            if (nodeName == (_layout?.ShopNodeName ?? "C0")) return RoomType.Shop;
            return RoomType.Battle;
        }

        private void RefreshOptionalBranchAccess()
        {
            string sourceNode =
                _layout?.OptionalBranchSourceNodeName ?? "O1";
            string targetNode =
                _layout?.OptionalBranchTargetNodeName ?? "B0";
            if (string.IsNullOrWhiteSpace(sourceNode)
                || string.IsNullOrWhiteSpace(targetNode))
                return;
            bool open = IsOptionalBranchOpen();
            Runtime.SetOptionalBranchAccess(sourceNode, targetNode, open);
            for (int i = 0; i < Runtime.RoomCount; i++)
            {
                if (!Runtime.TryGetPlacement(i, out EdgarRoomPlacement placement)
                    || placement.NodeName != targetNode
                    || placement.Instance?.RoomTemplateInstance == null)
                    continue;
                DungeonLandmarkLabel.Create(
                    placement.Instance.RoomTemplateInstance.transform,
                    open ? "巡礼封藏室" : "未探索·桥后封藏室",
                    open
                        ? new Color(0.9f, 0.68f, 0.28f)
                        : new Color(0.55f, 0.5f, 0.45f));
                break;
            }
        }

        private static bool IsOptionalBranchOpen()
        {
            if (BossFlagSet.Instance.Evaluate("bridge_opened=1"))
                return true;
            return !LevelAPhaseRuntime.IsNightMapActive
                   && BossFlagSet.Instance.Evaluate("bridge_sabotaged=1");
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
