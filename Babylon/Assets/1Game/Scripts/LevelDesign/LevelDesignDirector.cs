using System;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12 关卡设计系统总控（Bridge / Facade）。
    ///
    /// 设计意图：把第 12 章的子系统（ConfigDatabase / BossFlagSet /
    /// StoryEventService / BossPhaseSelector）封装到一个稳定 API 后面。
    ///
    /// V0.4.1：地图已改用 silverua 原生全图（<see cref="SilveruaMapProvider"/> + <see cref="StsMapScreen"/>），
    /// 旧 <c>TreeMap</c>/<c>TreeMapGenerator</c>/<c>TreeMapUI</c> 已删除。Director 此处只保留
    /// 「整局/区域的 Flag & 玩家状态重置」与「Boss 形态解析」，不再持有地图拓扑。
    /// 房间事件触发改由 <see cref="LevelDesignBootstrap"/> 的线性调度表驱动。
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

        // ------------------------------------------------------------
        // 初始化
        // ------------------------------------------------------------

        public void EnsureConfigLoaded()
        {
            var _ = ConfigDatabase.Instance; // 触发懒加载
        }

        /// <summary>整局开始：清空 Flag + 重置玩家状态 + 摇命格 + 清 Act1 Flag。</summary>
        public void StartNewRun()
        {
            EnsureConfigLoaded();
            BossFlagSet.Instance.ClearAll();
            StoryEventService.Instance.ResetForNewRun();
            PlayerStateHooks.Instance.ResetForNewRun();
            BeginAct(1);
            Debug.Log("[LevelDesign] 新局已开始");
        }

        /// <summary>进入指定区域：清空区域 Flag（地图拓扑由 SilveruaMapProvider 负责）。</summary>
        public void BeginAct(int actID)
        {
            BossFlagSet.Instance.ClearAct();
            StoryEventService.Instance.ResetForNewAct();
        }

        // ------------------------------------------------------------
        // 事件触发
        // ------------------------------------------------------------

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
            bool storyApplied = StoryTemplateRuntime.ApplyBossModifier(
                ref hp,
                ref atk,
                ref spd,
                out string storyResult);
            boss.Stats.maxHp = hp;
            boss.Stats.currentHp = hp;
            boss.Stats.attackDamage = atk;
            boss.Stats.moveSpeed = spd;

            // 播报出场对白
            PlayBossEntrance(phase);

            Debug.Log($"[LevelDesign] Boss 应用形态：{phase.PhaseName} | HP→{hp:F0} ATK→{atk:F1} SPD→{spd:F2}");
            if (storyApplied)
                Debug.Log($"[Story Template] Boss 本局修正：{storyResult}");
            return phase;
        }
    }
}
