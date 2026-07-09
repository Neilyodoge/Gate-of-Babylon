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
        /// v0.5 兼容模式：(境界Level, 房间Index) → 强制触发的 EventID。
        /// 当 LevelDesign 没有 TreeMap（v0.5 线性流程）时使用，让玩家自然走到该房间就触发事件。
        /// Demo2 启用 TreeMap 主循环后会改走 RoomSocketRow.EventID 配置。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<(int realm, int room), int> _linearEventSchedule = new()
        {
            { (0, 1), 1001 }, // 练气期第 2 间房间 → 叶修之死
            { (1, 1), 1003 }, // 筑基期第 2 间房间 → 古修遗宝
            { (2, 0), 1002 }, // 金丹期第 1 间房间 → 灵药宝库（条件事件 saved_yeXiu）
        };

        /// <summary>累计已清场房间索引（用于线性事件触发）</summary>
        private int _linearRoomsClearedThisRealm;
        private int _lastRealmObserved = -1;

        /// <summary>
        /// 已经初始化过的区域 Level。防止 SpawnCurrentRoom 每次发布 RealmBreakthrough
        /// 时反复调用 StartNewRun / BeginAct 导致 TreeMap 被覆盖。
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
            Debug.Log("[LevelDesign] Bootstrap 已启动（调试入口：Tab 打开 DebugConsole）");
        }

        private void OnDestroy()
        {
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
        }

        // ------------------------------------------------------------
        // 事件钩子
        // ------------------------------------------------------------

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            if (!autoRollEventOnRoomClear) return;

            var dir = LevelDesignDirector.Instance;

            // 模式 A：TreeMap 主循环已激活 → 用当前节点配置触发
            if (dir.CurrentMap != null && dir.CurrentMapNode != null)
            {
                dir.TryTriggerRoomEvent();
                return;
            }

            // 模式 B：v0.5 兼容模式 → 用线性事件调度表
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
            // 切换境界时重置线性房间计数
            if (evt.NewRealmLevel != _lastRealmObserved)
            {
                _linearRoomsClearedThisRealm = 0;
                _lastRealmObserved = evt.NewRealmLevel;
            }

            // 每个区域只初始化一次，防止 SpawnCurrentRoom 反复发布
            // RealmBreakthrough 导致 TreeMap 被覆盖（地图重置 Bug）
            if (_actAlreadyStarted.Contains(evt.NewRealmLevel)) return;

            // V0.2：GameManager.StartNewRun() 已直接调用 Director.StartNewRun()，
            // 此处仅对 realm=0 标记"已处理"，不再重复生成地图。
            if (evt.NewRealmLevel == 0)
            {
                _actAlreadyStarted.Clear();
                _actAlreadyStarted.Add(0);
                // 如果 Director 已有地图（GameManager 已生成），跳过
                if (LevelDesignDirector.Instance.CurrentMap != null) return;
                LevelDesignDirector.Instance.StartNewRun();
            }
            else if (evt.NewRealmLevel == 2)
            {
                _actAlreadyStarted.Add(2);
                LevelDesignDirector.Instance.BeginAct(2);
            }
            else if (evt.NewRealmLevel == 4)
            {
                _actAlreadyStarted.Add(4);
                LevelDesignDirector.Instance.BeginAct(3);
            }
        }

    }
}
