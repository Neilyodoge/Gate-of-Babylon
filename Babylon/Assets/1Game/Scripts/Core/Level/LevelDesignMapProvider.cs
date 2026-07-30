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

        // V0.4.5：换境显式重生成该 Act 的分叉图（BeginAct 内会 Generate 并把 CurrentNode 复位到起点）。
        public void OnEnterRealm(int realm) => Dir?.BeginAct(realm + 1);

        // V0.4.5：分叉图（TreeMap）现语义 = 单境（realm）内「深度=房间数」的 STS 分叉导航图，
        // 由 TryShowNavigation 逐间弹出。它不再充当 _levelRooms 的线性脚手架
        // （否则会把 12 层分叉误当成 12 个境）。因此这里返回 null，让 GameManager
        // 用其 3×12 fixedLayout 作线性房间脚手架；每间实际房型由玩家在分叉图上的选择覆盖。
        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors() => null;

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
            if (Dir == null) return false;

            // V0.4.5：每境（realm）惰性重生成对应 Act 的 STS 分叉图，
            // 使分叉图的 CurrentNode 推进与 GameManager 的房间推进锁步。
            int realm = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0;
            int actId = realm + 1;
            if (Dir.CurrentMap == null || Dir.CurrentMap.ActID != actId)
                Dir.BeginAct(actId);

            // Boss 房保持线性叙事，不弹导航。
            if (bossNext) return false;

            var cur = Dir.CurrentMap?.CurrentNode;
            if (cur == null || cur.Next == null || cur.Next.Count == 0) return false;

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
