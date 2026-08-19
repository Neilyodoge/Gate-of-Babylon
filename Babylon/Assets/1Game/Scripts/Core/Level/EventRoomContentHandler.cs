using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    public sealed class EventRoomContentHandler : MonoBehaviour, IRoomContentHandler
    {
        private RoomContentContext _context;
        private EventRoomInteractable _interactable;
        private EventGuardEncounter _guardEncounter;
        private Vector3 _interactionPosition;
        private int _eventID;
        private bool _completed;
        private bool _encounterStarted;

        public void Initialize(in RoomContentContext context)
        {
            _context = context;
            if (context.Spawn.buildRoomGeometry)
                RoomBuilder.Build(
                    transform,
                    context.Spawn.roomSize,
                    context.Spawn.roomSize,
                    context.Spawn.level);

            Vector3 position = ResolveInteractionPosition(context);
            _interactionPosition = position;
            _eventID = (MapProviders.Current as EdgarMapProvider)?.CurrentEventID ?? 0;
            if (_eventID <= 0)
                throw new System.InvalidOperationException(
                    $"事件房无法解析 EventID：Room={context.Spawn.roomIndex}。");
            _interactable = EventRoomInteractable.Create(
                position,
                transform,
                TriggerEvent);
            _interactable.SetAvailable(false);
        }

        public void Arm()
        {
        }

        public void Activate()
        {
            if (_completed)
                return;

            var edgar = MapProviders.Current as EdgarMapProvider;
            if (edgar != null && edgar.IsCurrentEventRecorded())
            {
                StoryEventService.Instance.RestoreCompletedEvent(_eventID);
                RestoreCompleted();
                _context.OnCompleted?.Invoke();
                return;
            }

            if (_eventID == 1004 && !LevelAPhaseRuntime.IsNightPending)
            {
                StartGuardEncounter(
                    meleeCount: 3,
                    rangedCount: 0,
                    includeElite: true,
                    onCleared: () =>
                    {
                        BossFlagSet.Instance.Set("bridge_guard_key", 1);
                        _interactable?.SetAvailable(true);
                        Debug.Log("[断裂巡礼桥] 桥卫队已清除，已取得桥门钥匙。");
                    });
                return;
            }

            if (_interactable != null)
                _interactable.SetAvailable(true);
        }

        public void RestoreCompleted()
        {
            _completed = true;
            _interactable?.Complete();
        }

        public int EventID => _eventID;

        public void DebugMarkCompleted()
        {
            if (_completed || _eventID <= 0)
                return;
            _completed = true;
            _interactable?.Complete();
            _context.OnCompleted?.Invoke();
            GameEvents.Publish(new GameEvents.RoomCleared
            {
                RoomIndex = _context.Spawn.roomIndex,
                IsEvent = true,
                IsCombatRoom = false,
            });
        }

        private void TriggerEvent()
        {
            MapProviders.Current.TryTriggerRoomEvent(CompleteEvent);
        }

        private void CompleteEvent(EventOption selected)
        {
            if (_completed)
                return;

            if (_eventID == 1004
                && selected != null
                && selected.FlagName == "bridge_opened_pending")
            {
                _interactable?.SetAvailable(false);
                StartGuardEncounter(
                    meleeCount: 2,
                    rangedCount: 1,
                    includeElite: false,
                    onCleared: () =>
                    {
                        BossFlagSet.Instance.Set("bridge_opened", 1);
                        FinalizeEvent(new EventOption
                        {
                            Text = "校准两侧配重并稳定放下巡礼桥",
                            FlagName = "bridge_opened",
                            FlagValue = 1,
                            SceneResult = EventSceneResult.OpenRoute,
                        });
                    });
                return;
            }

            if (_eventID == 1006
                && selected != null
                && selected.FlagName == "summon_array_destroyed_pending")
            {
                _interactable?.SetAvailable(false);
                StartGuardEncounter(
                    meleeCount: 2,
                    rangedCount: 1,
                    includeElite: true,
                    onCleared: () =>
                    {
                        BossFlagSet.Instance.Set("summon_array_destroyed", 1);
                        FinalizeEvent(new EventOption
                        {
                            Text = "摧毁阵心并击败失控禁卫",
                            FlagName = "summon_array_destroyed",
                            FlagValue = 1,
                            SceneResult = EventSceneResult.SummonArrayDestroyed,
                        });
                    });
                return;
            }

            if (_eventID == 1005
                && selected != null
                && selected.FlagName == "crown_light_disabled_pending")
            {
                _interactable?.SetAvailable(false);
                StartGuardEncounter(
                    meleeCount: 0,
                    rangedCount: 2,
                    includeElite: true,
                    onCleared: () =>
                    {
                        BossFlagSet.Instance.Set("crown_light_disabled", 1);
                        FinalizeEvent(new EventOption
                        {
                            Text = "摧毁主镜并击败守光禁卫",
                            FlagName = "crown_light_disabled",
                            FlagValue = 1,
                            SceneResult = EventSceneResult.CrownLightDisabled,
                        });
                    });
                return;
            }

            FinalizeEvent(selected);
        }

        private void FinalizeEvent(EventOption selected)
        {
            if (_completed)
                return;
            EventSceneOutcome.Apply(selected, _context.ContentRoot, _interactionPosition);
            if (MapProviders.Current is EdgarMapProvider edgar)
                edgar.CompleteCurrentRoomEvent(selected);
            _completed = true;
            _interactable?.Complete();
            _context.OnCompleted?.Invoke();
            GameEvents.Publish(new GameEvents.RoomCleared
            {
                RoomIndex = _context.Spawn.roomIndex,
                IsEvent = true,
                IsCombatRoom = false,
            });
        }

        private void StartGuardEncounter(
            int meleeCount,
            int rangedCount,
            bool includeElite,
            System.Action onCleared)
        {
            if (_encounterStarted)
                return;
            _encounterStarted = true;
            _interactable?.SetAvailable(false);

            _guardEncounter = gameObject.AddComponent<EventGuardEncounter>();
            float hpMul = (1f + _context.Spawn.level * _context.Spawn.hpScalePerLevel)
                          * _context.Spawn.floorScale;
            float dmgMul = (1f + _context.Spawn.level * _context.Spawn.dmgScalePerLevel)
                           * _context.Spawn.floorScale;
            _guardEncounter.Begin(
                _context.ContentRoot,
                _context.EncounterSeed ^ _eventID,
                hpMul,
                dmgMul,
                meleeCount,
                rangedCount,
                includeElite,
                () =>
                {
                    _encounterStarted = false;
                    onCleared?.Invoke();
                });
        }

        private static Vector3 ResolveInteractionPosition(in RoomContentContext context)
        {
            Transform contentRoot = context.ContentRoot;
            if (contentRoot != null)
            {
                var sockets = contentRoot.GetComponentsInChildren<DungeonContentSocket>(true);
                foreach (var socket in sockets)
                {
                    if (socket.SocketType != DungeonContentSocketType.Event)
                        continue;

                    if (DungeonSpawnSafety.TryFindGroundedPoint(
                            contentRoot,
                            socket.transform.position,
                            0.45f,
                            1.8f,
                            0.05f,
                            out Vector3 grounded))
                        return grounded;
                }
            }

            return context.Spawn.spawnPos;
        }
    }
}
