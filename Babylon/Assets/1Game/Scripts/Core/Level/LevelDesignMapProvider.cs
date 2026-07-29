using System;
using System.Collections.Generic;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 默认地图提供者：基于第 12 章 LevelDesign 系统（TreeMap + 配表）实现 <see cref="IMapProvider"/>。
    ///
    /// 把原先散落在 GameManager / BattleRoom / ShopRoom 里对
    /// <c>LevelDesignDirector</c> / <c>ConfigDatabase</c> / <c>TreeMap</c> 的直接访问全部收口于此，
    /// 并在此完成 <see cref="LevelRoomType"/> → <see cref="RoomType"/> 的边界映射。
    /// </summary>
    public class LevelDesignMapProvider : IMapProvider
    {
        private static LevelDesignDirector Dir => LevelDesignDirector.Instance;

        public bool IsReady => Dir != null && Dir.CurrentMap != null;

        public int CurrentActId => Dir?.CurrentMap?.ActID ?? 1;

        public void StartRun() => Dir.StartNewRun();

        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors()
        {
            var map = Dir?.CurrentMap;
            if (map == null || map.Floors.Count == 0) return null;

            var floors = new List<IReadOnlyList<RoomType>>(map.Floors.Count);
            foreach (var floor in map.Floors)
            {
                var rooms = new List<RoomType>(floor.Count);
                foreach (var node in floor)
                    rooms.Add(Map(node.RoomType));
                if (rooms.Count == 0) rooms.Add(RoomType.Battle);
                floors.Add(rooms);
            }
            return floors;
        }

        public float GetEnemyScale(int floor) => WithStructure(floor, (s, f) => s.GetEnemyScale(f), 1f);

        public int GetRarityBias(int floor) => WithStructure(floor, (s, f) => s.GetRarityBias(f), 0);

        public bool GetHasStageReturn(int floor) => WithStructure(floor, (s, f) => s.GetHasStageReturn(f), true);

        public void TryTriggerRoomEvent(Action onCompleted)
        {
            if (Dir == null) { onCompleted?.Invoke(); return; }
            Dir.TryTriggerRoomEvent(onCompleted);
        }

        public void MarkCurrentCleared() => Dir?.MarkCurrentNodeCleared();

        public bool CurrentNodeHasNext
        {
            get
            {
                var node = Dir?.CurrentMapNode;
                return node != null && node.Next != null && node.Next.Count > 0;
            }
        }

        public bool TryShowNavigation(bool bossNext, Action<RoomType> onChosen)
        {
            if (Dir == null || Dir.CurrentMap == null) return false;

            var cur = Dir.CurrentMap.CurrentNode;
            if (cur == null || cur.Next == null || cur.Next.Count == 0) return false;

            // Boss 房保持线性叙事，不弹导航。
            if (bossNext) return false;

            Dir.ShowMap(node =>
            {
                onChosen?.Invoke(node != null ? Map(node.RoomType) : RoomType.Battle);
            });
            return true;
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------

        /// <summary>按当前 Act 从 Map_Structure_Config 找到对应行并求值，缺行回退 <paramref name="fallback"/>。</summary>
        private static T WithStructure<T>(int floor, Func<MapStructureRow, int, T> f, T fallback)
        {
            var map = Dir?.CurrentMap;
            if (map == null) return fallback;
            var db = ConfigDatabase.Instance;
            if (db == null) return fallback;
            foreach (var kv in db.MapStructures)
                if (kv.Value.ActID == map.ActID)
                    return f(kv.Value, floor);
            return fallback;
        }

        private static RoomType Map(LevelRoomType t) => t switch
        {
            LevelRoomType.Battle => RoomType.Battle,
            LevelRoomType.Elite => RoomType.Elite,
            LevelRoomType.Shop => RoomType.Shop,
            LevelRoomType.Event => RoomType.Event,
            LevelRoomType.Boss => RoomType.Boss,
            _ => RoomType.Battle
        };
    }
}
