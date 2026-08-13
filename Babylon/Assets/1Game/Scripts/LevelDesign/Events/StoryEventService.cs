using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.3 事件系统执行层。
    ///
    /// 职责：
    ///   1. 提供"是否触发事件"判定（特殊事件房 100% / 战斗房按 EventTriggerRate）
    ///   2. 检查 PrereqFlag 条件
    ///   3. 展示事件 UI，玩家选项后执行 Trigger
    ///   4. 记录"本区域已触发"避免重复
    /// </summary>
    public class StoryEventService
    {
        private static StoryEventService _instance;
        public static StoryEventService Instance => _instance ??= new StoryEventService();

        private readonly HashSet<int> _completedInAct = new();

        public event Action<StoryEventRow, EventOption> OnEventCompleted;

        public bool IsCompletedInAct(int eventID) => _completedInAct.Contains(eventID);
        public void RestoreCompletedEvent(int eventID)
        {
            if (eventID > 0)
                _completedInAct.Add(eventID);
        }

        // ------------------------------------------------------------
        // 入口：尝试触发指定事件
        // ------------------------------------------------------------

        /// <summary>
        /// 尝试触发一个事件。如果 PrereqFlag 不满足 / 已完成 / EventID 无效，返回 false。
        /// 否则弹出事件 UI（异步），完成后通过回调返回。
        /// </summary>
        public bool TryTriggerEvent(int eventID, Action<EventOption> onCompleted = null)
        {
            if (eventID <= 0) return false;

            var db = ConfigDatabase.Instance;
            if (!db.StoryEvents.TryGetValue(eventID, out var row))
            {
                Debug.LogWarning($"[StoryEvent] EventID={eventID} 不存在");
                return false;
            }

            if (_completedInAct.Contains(eventID))
            {
                Debug.Log($"[StoryEvent] EventID={eventID} ({row.Name_CN}) 本区域已完成，跳过");
                return false;
            }

            // 条件事件：检查前置 Flag
            if (row.TypeEnum == StoryEventType.ConditionalEvent &&
                !BossFlagSet.Instance.Evaluate(row.PrereqFlag))
            {
                Debug.Log($"[StoryEvent] EventID={eventID} 条件事件，前置 Flag 不满足：{row.PrereqFlag}");
                return false;
            }

            // 展示 UI
            StoryEventUI.Show(row, selected =>
            {
                if (selected != null)
                {
                    ApplyTrigger(row, selected);
                    _completedInAct.Add(eventID);
                }
                onCompleted?.Invoke(selected);
                OnEventCompleted?.Invoke(row, selected);
            });

            return true;
        }

        /// <summary>战斗 / 精英房结束后按 EventTriggerRate 概率触发</summary>
        public void RollAndTrigger(int eventID, int triggerRate, Action<EventOption> onCompleted = null)
        {
            if (eventID <= 0 || triggerRate <= 0) { onCompleted?.Invoke(null); return; }
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < triggerRate)
            {
                bool triggered = TryTriggerEvent(eventID, onCompleted);
                if (!triggered) onCompleted?.Invoke(null);
            }
            else
            {
                onCompleted?.Invoke(null);
            }
        }

        // ------------------------------------------------------------
        // 应用选项 Trigger（核心：把事件结果写入各系统）
        // ------------------------------------------------------------

        private void ApplyTrigger(StoryEventRow row, EventOption opt)
        {
            Debug.Log($"[StoryEvent] 玩家在「{row.Name_CN}」选择：{opt.Text}");

            // 1. Boss Flag
            if (!string.IsNullOrEmpty(opt.FlagName))
                BossFlagSet.Instance.Set(opt.FlagName, opt.FlagValue);

            // 2. 道具奖励 / 代价
            if (opt.RewardID > 0)
                GrantItemReward(opt.RewardID);
            if (opt.CostID > 0)
                ConsumeItemCost(opt.CostID);

            // 3. 修仙原生系统
            if (opt.KarmaChange != 0)
                PlayerStateHooks.Instance.ChangeKarma(opt.KarmaChange);
            if (opt.DaoxinChange != 0)
                PlayerStateHooks.Instance.ChangeDaoxin(opt.DaoxinChange);
            if (opt.LifespanChange != 0)
                PlayerStateHooks.Instance.ChangeLifespan(opt.LifespanChange);
        }

        private void GrantItemReward(int rewardID)
        {
            if (rewardID <= 0) return;
            var player = PlayerController.Instance;
            Vector3 dropPosition = player != null
                ? player.transform.position + player.transform.forward * 1.2f
                : Vector3.up * 0.2f;

            switch (rewardID)
            {
                case 2001:
                    CaveMaterialPool.SpawnRandom(dropPosition);
                    break;
                case 3001:
                    CaveMaterialPool.SpawnRandom(dropPosition + Vector3.left * 0.5f);
                    CaveMaterialPool.SpawnRandom(dropPosition + Vector3.right * 0.5f);
                    break;
                case 4001:
                    RequirePlayerStats(rewardID).damageBonusPercent += 0.10f;
                    break;
                case 5001:
                    RequirePlayerStats(rewardID).damageBonusPercent += 0.20f;
                    break;
                case 6001:
                    RequirePlayerStats(rewardID).damageBonusPercent += 0.20f;
                    break;
                case 6002:
                {
                    var stats = RequirePlayerStats(rewardID);
                    stats.damageBonusPercent += 0.10f;
                    float hpGain = stats.maxHp * 0.20f;
                    stats.maxHp += hpGain;
                    stats.currentHp += hpGain;
                    break;
                }
                case 6003:
                    RequirePlayerStats(rewardID).skillDamagePercent += 0.15f;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"事件奖励 RewardID={rewardID} 未配置真实效果，禁止静默占位。");
            }

            BossFlagSet.Instance.Add($"reward_{rewardID}", 1);
            Debug.Log($"[StoryEvent] ✓ 已应用真实奖励：RewardID={rewardID}");
        }

        private void ConsumeItemCost(int costID)
        {
            if (costID <= 0) return;
            if (PlayerResources.Instance == null
                || !PlayerResources.Instance.SpendShards(costID))
                throw new InvalidOperationException(
                    $"事件代价 CostID={costID} 需要消耗同等数量灵力碎片，但当前数量不足。");

            Debug.Log($"[StoryEvent] − 已消耗灵力碎片：{costID}");
        }

        private static CombatStats RequirePlayerStats(int rewardID)
        {
            if (PlayerController.Instance?.Stats == null)
                throw new InvalidOperationException(
                    $"应用事件奖励 RewardID={rewardID} 时找不到玩家战斗属性。");
            return PlayerController.Instance.Stats;
        }

        // ------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------

        public void ResetForNewAct() => _completedInAct.Clear();
        public void ResetForNewRun() => _completedInAct.Clear();
    }
}
