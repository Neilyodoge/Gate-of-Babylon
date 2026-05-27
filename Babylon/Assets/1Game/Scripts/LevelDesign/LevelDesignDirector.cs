using System;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12 关卡设计系统总控（Bridge / Facade）。
    ///
    /// 设计意图：把第 12 章的所有子系统（ConfigDatabase / BossFlagSet / TreeMap /
    /// StoryEventService / BossPhaseSelector）封装到一个稳定 API 后面，
    /// 让现有 GameManager / VillageHub 只需调用 Director 即可，
    /// 后续替换 / 扩展子系统不破坏接入点。
    ///
    /// 当前状态：第 12 章功能可独立运行（不破坏现有 v0.5 线性流程），
    /// 但**实际接入到 GameManager 主循环**需要后续把 GameManager 的房间推进改造为读 TreeMap。
    /// 这一步留作 Demo2 实施（GDD §12.6 任务 #6 整合）。
    /// </summary>
    public class LevelDesignDirector : MonoBehaviour
    {
        private static LevelDesignDirector _instance;
        public static LevelDesignDirector Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("LevelDesignDirector");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<LevelDesignDirector>();
                }
                return _instance;
            }
        }

        public TreeMap CurrentMap { get; private set; }

        // ------------------------------------------------------------
        // 初始化
        // ------------------------------------------------------------

        public void EnsureConfigLoaded()
        {
            var _ = ConfigDatabase.Instance; // 触发懒加载
        }

        /// <summary>整局开始：清空 Flag + 重置玩家状态 + 摇命格 + 生成 Act 1 树状图</summary>
        public void StartNewRun()
        {
            EnsureConfigLoaded();
            BossFlagSet.Instance.ClearAll();
            StoryEventService.Instance.ResetForNewRun();
            PlayerStateHooks.Instance.ResetForNewRun();
            BeginAct(1);
            Debug.Log("[LevelDesign] 新局已开始");
        }

        /// <summary>进入指定区域：生成树状图、清空区域 Flag</summary>
        public void BeginAct(int actID)
        {
            BossFlagSet.Instance.ClearAct();
            StoryEventService.Instance.ResetForNewAct();
            CurrentMap = TreeMapGenerator.Generate(actID);
        }

        // ------------------------------------------------------------
        // 树状图导航
        // ------------------------------------------------------------

        /// <summary>
        /// 展示树状图。
        /// readOnly = false（默认）：选择模式，玩家选完节点后触发 onChosen 回调并推进 CurrentNode。
        /// readOnly = true：查看模式，候选节点不可点击，仅可按 ESC 关闭。
        /// </summary>
        public void ShowMap(Action<TreeNode> onChosen = null, bool readOnly = false)
        {
            if (CurrentMap == null)
            {
                Debug.LogWarning("[LevelDesign] 当前没有树状图，请先调用 BeginAct()");
                onChosen?.Invoke(null);
                return;
            }
            TreeMapUI.Show(CurrentMap, onChosen, readOnly);
        }

        public TreeNode CurrentMapNode => CurrentMap?.CurrentNode;

        // ------------------------------------------------------------
        // 事件触发（房间清场后调用）
        // ------------------------------------------------------------

        /// <summary>
        /// 房间清场时调用：根据当前节点类型 + 房间配置触发事件。
        /// 特殊事件房 → 100% 触发；战斗 / 精英房 → 按 EventTriggerRate 概率。
        /// </summary>
        public void TryTriggerRoomEvent(Action onCompleted = null)
        {
            var node = CurrentMapNode;
            if (node == null) { onCompleted?.Invoke(); return; }

            var db = ConfigDatabase.Instance;
            if (!db.RoomSockets.TryGetValue(node.RoomConfigID, out var roomCfg))
            {
                onCompleted?.Invoke();
                return;
            }

            if (roomCfg.EventID <= 0) { onCompleted?.Invoke(); return; }

            int rate = node.RoomType == LevelRoomType.Event ? 100 : roomCfg.EventTriggerRate;
            StoryEventService.Instance.RollAndTrigger(roomCfg.EventID, rate, _ => onCompleted?.Invoke());
        }

        public void ForceTriggerEvent(int eventID, Action onCompleted = null)
        {
            StoryEventService.Instance.TryTriggerEvent(eventID, _ => onCompleted?.Invoke());
        }

        // ------------------------------------------------------------
        // Boss 形态查询
        // ------------------------------------------------------------

        /// <summary>
        /// 进入 Boss 房时调用，按当前 FlagSet 选定形态，并返回数值修正后的 stats。
        /// </summary>
        public BossPhaseRow ResolveBossPhase(int bossID)
        {
            return BossPhaseSelector.SelectMainPhase(bossID);
        }

        public void PlayBossEntrance(BossPhaseRow phase)
        {
            if (phase == null) return;
            BossDialogueUI.Show(phase.PhaseName, phase.DialogueLines);
        }

        /// <summary>
        /// 给定一个 EnemyBoss 实例和 bossID，按当前 FlagSet 选定形态，
        /// 应用数值修正（HP / ATK / 移速），并播报出场对白。
        /// EnemyBoss.Spawn() 在末尾调用此方法，向下兼容（v1 数据缺失时无副作用）。
        /// </summary>
        public BossPhaseRow ApplyBossPhase(EnemyBoss boss, int bossID)
        {
            if (boss == null) return null;
            if (!ConfigDatabase.Instance.Loaded || ConfigDatabase.Instance.BossPhases.Count == 0)
                return null; // 表格未配置 → 不做任何修改

            var phase = ResolveBossPhase(bossID);
            if (phase == null) return null;

            // 应用数值修正
            float hp = boss.Stats.maxHp;
            float atk = boss.Stats.attackDamage;
            float spd = boss.Stats.moveSpeed;
            BossPhaseSelector.ApplyStatModifier(phase, ref hp, ref atk, ref spd);
            boss.Stats.maxHp = hp;
            boss.Stats.currentHp = hp;
            boss.Stats.attackDamage = atk;
            boss.Stats.moveSpeed = spd;

            // 播报出场对白
            PlayBossEntrance(phase);

            Debug.Log($"[LevelDesign] Boss 应用形态：{phase.PhaseName} | HP→{hp:F0} ATK→{atk:F1} SPD→{spd:F2}");
            return phase;
        }

        // ------------------------------------------------------------
        // 房间清算（节点标记 + 进入下一节点）
        // ------------------------------------------------------------

        public void MarkCurrentNodeCleared()
        {
            if (CurrentMapNode == null) return;
            CurrentMapNode.Cleared = true;
        }

        public bool IsCurrentNodeBoss => CurrentMapNode != null && CurrentMapNode.RoomType == LevelRoomType.Boss;
    }
}
