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
        private readonly List<IReadOnlyList<RoomType>> _floors = new();
        private EdgarDungeonRuntime _runtime;
        private int _realm;
        private int _roomIndex;

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

                var go = new GameObject("Edgar Dungeon Runtime");
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
            GenerateRealm();
        }

        public void OnEnterRealm(int realm)
        {
            _realm = Mathf.Max(0, realm);
            _roomIndex = 0;
            LevelDesignDirector.Instance.BeginAct(_realm + 1);
            GenerateRealm();
        }

        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors() => _floors;

        public float GetEnemyScale(int floor) => WithStructure(floor, (s, f) => s.GetEnemyScale(f), 1f);
        public int GetRarityBias(int floor) => WithStructure(floor, (s, f) => s.GetRarityBias(f), 0);
        public bool GetHasStageReturn(int floor) => WithStructure(floor, (s, f) => s.GetHasStageReturn(f), true);

        public void TryTriggerRoomEvent(Action onCompleted) => onCompleted?.Invoke();

        public void MarkCurrentCleared()
        {
            Runtime.UnlockActiveRoom();
            _roomIndex++;
        }

        public bool TryShowNavigation(bool bossNext, Action<RoomType> onChosen)
        {
            // Edgar 的实体门与走廊承担导航，不再显示独立的 STS 地图。
            return false;
        }

        public bool TryGetCurrentPlacement(out EdgarRoomPlacement placement)
        {
            bool found = Runtime.TryGetPlacement(_roomIndex, out placement);
            if (found)
                Runtime.ActivateRoom(_roomIndex);
            return found;
        }

        public void ClearDungeon()
        {
            if (_runtime != null)
                _runtime.Clear();
            _roomIndex = 0;
        }

        private void GenerateRealm()
        {
            int seed = unchecked(Environment.TickCount * 397) ^ (_realm + 1) * 7919;
            bool generated = Runtime.Generate(seed);
            RebuildFloors(generated ? Runtime.RoomCount : 0);
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
                    rooms.Add(i == roomCount - 1 ? RoomType.Boss : RoomType.Battle);
                _floors.Add(rooms);
            }
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
