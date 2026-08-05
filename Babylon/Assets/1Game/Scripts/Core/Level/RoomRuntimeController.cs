using System.Collections.Generic;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    public sealed class RoomRuntimeController : MonoBehaviour
    {
        private static readonly Dictionary<int, RoomRuntimeController> ActiveControllers = new();
        private static readonly Dictionary<int, RoomRuntimeState> States = new();

        private IRoomContentHandler _handler;
        private RoomContentRow _content;
        private RoomRuntimeState _state;

        public RoomRuntimeState State => _state;

        public void Initialize(
            RoomType roomType,
            in RoomSpawnContext spawn,
            IRoomContentHandler handler)
        {
            int seed = spawn.encounterSeed;
            if (!States.TryGetValue(spawn.roomIndex, out _state))
            {
                _content = RoomContentResolver.Resolve(
                    roomType, spawn.roomIndex, spawn.roomCount, seed);
                _state = new RoomRuntimeState
                {
                    RoomIndex = spawn.roomIndex,
                    ContentID = _content.ID,
                    EncounterSeed = seed,
                    Phase = RoomRuntimePhase.Dormant
                };
                States.Add(spawn.roomIndex, _state);
            }
            else
            {
                _content = ConfigDatabase.Instance.GetRoomContent(_state.ContentID);
                if (_content == null)
                    throw new System.InvalidOperationException(
                        $"房间状态引用缺失 RoomContent={_state.ContentID}，Room={spawn.roomIndex}，Seed={seed}。");
            }

            _handler = handler;
            _handler.Initialize(new RoomContentContext(
                roomType, _content, spawn, _state, _state.EncounterSeed, Complete));
            ActiveControllers[spawn.roomIndex] = this;

            if (_state.Phase == RoomRuntimePhase.Completed)
            {
                _handler.RestoreCompleted();
                return;
            }

            _state.Phase = RoomRuntimePhase.Armed;
            _handler.Arm();
            if (_content.ActivationModeEnum == ActivationMode.AlwaysActive)
                Enter();
        }

        public void Enter()
        {
            if (_state == null
                || _state.Phase == RoomRuntimePhase.Active
                || _state.Phase == RoomRuntimePhase.Completed)
                return;

            _state.Phase = RoomRuntimePhase.Active;
            bool shouldLock = _content.LockPolicyEnum != LockPolicy.None;
            SetLocked(shouldLock);
            _handler.Activate();
        }

        public void Complete()
        {
            if (_state == null || _state.Phase == RoomRuntimePhase.Completed)
                return;

            _state.Cleared = true;
            _state.Phase = RoomRuntimePhase.Completed;
            SetLocked(false);
        }

        private void SetLocked(bool locked)
        {
            _state.DoorsLocked = locked;
            var runtime = FindFirstObjectByType<EdgarDungeonRuntime>();
            runtime?.SetRoomLocked(_state.RoomIndex, locked);
        }

        public static bool TryEnter(int roomIndex)
        {
            if (!ActiveControllers.TryGetValue(roomIndex, out var controller)
                || controller == null)
                return false;
            controller.Enter();
            return true;
        }

        public static void ResetRunState()
        {
            ActiveControllers.Clear();
            States.Clear();
        }

        private void OnDestroy()
        {
            if (_state != null
                && ActiveControllers.TryGetValue(_state.RoomIndex, out var current)
                && current == this)
                ActiveControllers.Remove(_state.RoomIndex);
        }
    }
}
