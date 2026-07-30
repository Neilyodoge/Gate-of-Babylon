using System;
using System.Collections.Generic;
using Map;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// V0.4.1：接入 silverua 杀戮尖塔地图后的地图提供者（替换 <see cref="LevelDesignMapProvider"/>）。
    ///
    /// 复用现有 <see cref="IMapProvider"/> 接缝：地图仍「逐间弹出全图、玩家点节点选下一处」，
    /// 只是把弹出的图换成 silverua 全屏 STS 图（<see cref="StsMapScreen"/> 承载 MapManager/MapViewUI）。
    /// 进度真源 = 地图：每境（realm）一张分叉图，节点数（层数）= 该境房间数，末层为 Boss。
    ///
    /// 数值查询（敌人缩放 / 稀有度 / 阶段返回）仍复用 <c>ConfigDatabase.MapStructures</c> 配表，
    /// 与具体拓扑无关。
    /// </summary>
    public class SilveruaMapProvider : IMapProvider
    {
        private StsMapScreen _screen;
        private StsMapScreen Screen => _screen != null ? _screen : (_screen = StsMapScreen.Create());

        public bool IsReady => true;

        public int CurrentActId => (GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0) + 1;

        public void StartRun()
        {
            // 整局重置：清 Boss Flag / 剧情 / 玩家状态（原 LevelDesignMapProvider 的职责，删除后移到此处）。
            LevelDesignDirector.Instance.StartNewRun();
            Screen.SetActConfig(1);
            Screen.ResetForRealm();
        }

        public void OnEnterRealm(int realm)
        {
            // 换境：清区域 Flag（Director.BeginAct）+ 切该 Act 的地图配置 + 下次 Show 重生成。
            LevelDesignDirector.Instance.BeginAct(realm + 1);
            Screen.SetActConfig(realm + 1);
            Screen.ResetForRealm();
        }

        /// <summary>
        /// 每境房间脚手架：长度 = 地图层数，末间为 Boss，其余为 Battle 占位
        /// （实际房型由玩家在全图上点选后 <c>OverrideNextRoomType</c> 覆盖）。
        /// </summary>
        public IReadOnlyList<IReadOnlyList<RoomType>> GetFloors()
        {
            int realms = GameManager.Instance != null ? GameManager.Instance.RealmCount : 3;
            int layers = Mathf.Max(2, Screen.LayerCount);

            var floors = new List<IReadOnlyList<RoomType>>(realms);
            for (int r = 0; r < realms; r++)
            {
                var rooms = new List<RoomType>(layers);
                for (int i = 0; i < layers - 1; i++)
                    rooms.Add(RoomType.Battle);
                rooms.Add(RoomType.Boss);
                floors.Add(rooms);
            }
            return floors;
        }

        public float GetEnemyScale(int floor) => WithStructure(floor, (s, f) => s.GetEnemyScale(f), 1f);
        public int GetRarityBias(int floor) => WithStructure(floor, (s, f) => s.GetRarityBias(f), 0);
        public bool GetHasStageReturn(int floor) => WithStructure(floor, (s, f) => s.GetHasStageReturn(f), true);

        public void TryTriggerRoomEvent(Action onCompleted)
        {
            // 事件房本身直接放行；剧情事件由 LevelDesignBootstrap 的线性调度表在 RoomCleared 时触发。
            onCompleted?.Invoke();
        }

        public void MarkCurrentCleared() { /* silverua 路径在点选节点时已推进，无需额外标记 */ }

        // 房间脚手架长度已与地图层数对齐，无需 GameManager 动态扩展槽位。
        public bool CurrentNodeHasNext => false;

        public bool TryShowNavigation(bool bossNext, Action<RoomType> onChosen)
        {
            // Boss 为全图唯一收束点、无分支可选：保持线性，不弹图，直接进 Boss 房。
            if (bossNext) return false;

            Screen.Show(nodeType => onChosen?.Invoke(ToRoomType(nodeType)));
            return true;
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------

        private static T WithStructure<T>(int floor, Func<MapStructureRow, int, T> f, T fallback)
        {
            var db = ConfigDatabase.Instance;
            if (db == null) return fallback;
            int actId = (GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 0) + 1;
            foreach (var kv in db.MapStructures)
                if (kv.Value.ActID == actId)
                    return f(kv.Value, floor);
            return fallback;
        }

        private static RoomType ToRoomType(NodeType t) => t switch
        {
            NodeType.MinorEnemy => RoomType.Battle,
            NodeType.EliteEnemy => RoomType.Elite,
            NodeType.RestSite => RoomType.Rest,
            NodeType.Treasure => RoomType.Treasure,
            NodeType.Store => RoomType.Shop,
            NodeType.Boss => RoomType.Boss,
            NodeType.Mystery => RoomType.Event,
            _ => RoomType.Battle
        };
    }
}
