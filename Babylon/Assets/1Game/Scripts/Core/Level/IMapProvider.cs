using System;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// 关卡地图 / 拓扑抽象（解耦切面 · V0.4.2）。
    ///
    /// 设计意图：<see cref="GameManager"/> 与各房间只依赖本接口，不再直接触碰
    /// <c>LevelDesign.LevelDesignDirector</c> / <c>ConfigDatabase</c> / <c>TreeMap</c>。
    /// 后续接入杀戮尖塔式地图（如 silverua 那套）只需实现一个新的 IMapProvider，
    /// 并把它赋给 <see cref="MapProviders.Current"/>，游戏流程代码一行不改。
    /// </summary>
    public interface IMapProvider
    {
        /// <summary>地图是否已生成就绪（未就绪时 GameManager 回退固定布局）。</summary>
        bool IsReady { get; }

        /// <summary>当前区域（Act）ID，供 Boss 形态 / 数值查询使用。</summary>
        int CurrentActId { get; }

        /// <summary>整局开始：生成本局地图 / 拓扑。</summary>
        void StartRun();

        /// <summary>
        /// 返回按层组织的房间类型；<c>null</c> 表示地图未就绪，
        /// 由调用方回退到固定布局。
        /// </summary>
        IReadOnlyList<IReadOnlyList<RoomType>> GetFloors();

        /// <summary>某层敌人数值缩放倍率（查不到配表回退 1）。</summary>
        float GetEnemyScale(int floor);

        /// <summary>某层模块稀有度偏移（查不到配表回退 0）。</summary>
        int GetRarityBias(int floor);

        /// <summary>某层是否显示阶段返回点（查不到配表回退 true）。</summary>
        bool GetHasStageReturn(int floor);

        /// <summary>事件房触发叙事事件，完成后回调 <paramref name="onCompleted"/>。</summary>
        void TryTriggerRoomEvent(Action onCompleted);

        /// <summary>标记当前节点已通关。</summary>
        void MarkCurrentCleared();

        /// <summary>当前节点是否还有后续节点（用于动态扩展本层槽位）。</summary>
        bool CurrentNodeHasNext { get; }

        /// <summary>
        /// 尝试弹出地图导航 UI 让玩家选下一间；返回 <c>false</c> 表示当前情境不适用
        /// （无地图 / 下一间是 Boss / 无候选节点）。玩家选定后以 <see cref="RoomType"/> 回调。
        /// </summary>
        /// <param name="bossNext">下一槽位是否为 Boss 房（Boss 房保持线性叙事，不弹导航）。</param>
        bool TryShowNavigation(bool bossNext, Action<RoomType> onChosen);
    }

    /// <summary>
    /// <see cref="IMapProvider"/> 全局访问点（可替换）。
    /// 默认使用第 12 章 LevelDesign 系统的 <see cref="LevelDesignMapProvider"/>；
    /// 替换地图系统时在启动处赋值 <see cref="Current"/> 即可。
    /// </summary>
    public static class MapProviders
    {
        private static IMapProvider _current;

        public static IMapProvider Current
        {
            get => _current ??= new LevelDesignMapProvider();
            set => _current = value;
        }
    }
}
