using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12 关卡设计系统启动器。
    ///
    /// 设计目标：不修改 GameManager 主循环，通过订阅 GameEvents 实现：
    ///   · RealmBreakthrough → 进入下一区域 / 重新生成树状图（Demo2 完整接入时启用）
    ///   · RoomCleared      → 按当前房间配置概率触发事件
    ///   · GameStarted      → 初始化 ConfigDatabase + 重置 Flag
    ///
    /// 调试入口已统一进 <see cref="DebugConsole"/>（按 Tab 打开），
    /// 不再使用 F8/F9/F10 等独立快捷键。
    /// </summary>
    public class LevelDesignBootstrap : MonoBehaviour
    {
        private static LevelDesignBootstrap _instance;

        /// <summary>是否在每次 RoomCleared 时自动尝试触发事件（v0.5 兼容模式）。DebugConsole 可切换。</summary>
        public bool autoRollEventOnRoomClear = true;

        /// <summary>
        /// 线性事件调度：(境界Level, 房间Index) → 强制触发的 EventID。
        /// V0.4.1 起地图为 silverua 全图（无逐节点 EventID 配置），房间剧情事件统一走此表。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<(int realm, int room), int> _linearEventSchedule = new()
        {
            { (0, 1), 1001 }, // Act1 第 2 间房间 → 叶修之死
            { (1, 1), 1003 }, // Act1 后段 → 古修遗宝
            { (2, 1), 2001 }, // Act2 开头 → 幽魂哀歌
            { (3, 0), 2002 }, // Act2 后段 → 冥河渡口
            { (4, 1), 3001 }, // Act3 开头 → 龙骨祭坛
            { (5, 0), 3004 }, // Act3 后段 → 劫雷降世
        };

        /// <summary>累计已清场房间索引（用于线性事件触发）</summary>
        private int _linearRoomsClearedThisRealm;
        private int _lastRealmObserved = -1;

        /// <summary>
        /// 本局已处理过 meta 标记的区域 Level（防止 SpawnCurrentRoom 每次发布
        /// RealmBreakthrough 时重复 MarkActCleared）。回到 realm 0 视为新局并复位。
        /// </summary>
        private readonly System.Collections.Generic.HashSet<int> _actAlreadyStarted = new();

        public static LevelDesignBootstrap Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("LevelDesignBootstrap");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<LevelDesignBootstrap>();
                }
                return _instance;
            }
        }

        /// <summary>在游戏启动早期手动调用一次（Bootstrap 入口）</summary>
        public static void EnsureInitialized()
        {
            var _ = Instance;
        }

        /// <summary>Unity 启动时自动初始化（无需在场景中放置）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            EnsureInitialized();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LevelDesignDirector.Instance.EnsureConfigLoaded();
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Subscribe<GameEvents.PlayerDied>(OnPlayerDied);
            Debug.Log("[LevelDesign] Bootstrap 已启动（调试入口：Tab 打开 DebugConsole）");
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
            GameEvents.Unsubscribe<GameEvents.PlayerDied>(OnPlayerDied);
        }

        // ------------------------------------------------------------
        // 事件钩子
        // ------------------------------------------------------------

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            if (!autoRollEventOnRoomClear) return;
            if (MapProviders.Current is EdgarMapProvider)
                return;

            // V0.4.1：地图改用 silverua 原生全图后，房间事件统一走线性事件调度表
            // Edgar 使用房间自身分配的事件；这里只保留 silverua 线性调度。
            var dir = LevelDesignDirector.Instance;
            var gm = GameManager.Instance;
            if (gm == null) return;

            int realm = gm.CurrentLevel;
            int roomIdx = _linearRoomsClearedThisRealm;
            _linearRoomsClearedThisRealm++;

            if (_linearEventSchedule.TryGetValue((realm, roomIdx), out int eventID))
            {
                Debug.Log($"[LevelDesign] 线性事件调度：境界 {realm} 房间 {roomIdx} → 触发事件 {eventID}");
                dir.ForceTriggerEvent(eventID);
            }
        }

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            // 切换境界时重置线性房间计数；回到 realm 0 视为新局 → 复位区域标记（本单例常驻跨局）。
            if (evt.NewRealmLevel != _lastRealmObserved)
            {
                _linearRoomsClearedThisRealm = 0;
                if (evt.NewRealmLevel == 0) _actAlreadyStarted.Clear();
                _lastRealmObserved = evt.NewRealmLevel;
            }

            // V0.4.1：整局/区域的 Flag & 状态重置改由 SilveruaMapProvider.StartRun/OnEnterRealm
            // 调用 Director.StartNewRun()/BeginAct() 完成；此处仅做「区域通关」的 meta 标记（每局一次）。
            if (_actAlreadyStarted.Contains(evt.NewRealmLevel)) return;
            _actAlreadyStarted.Add(evt.NewRealmLevel);

            if (evt.NewRealmLevel == 2)
                PlayerStateHooks.Instance.MarkActCleared(1);
            else if (evt.NewRealmLevel == 4)
                PlayerStateHooks.Instance.MarkActCleared(2);
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            PlayerStateHooks.Instance.IncrementKillCount();
        }

        private void OnPlayerDied(GameEvents.PlayerDied evt)
        {
            PlayerStateHooks.Instance.MarkDeath();
        }

    }
}
