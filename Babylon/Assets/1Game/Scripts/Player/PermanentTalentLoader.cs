using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 永久天赋加载器 —— 玩家进入战斗场景时把 SaveData.unlockedTalentIds 中的天赋
    /// 自动挂载到 PlayerController 身上（StatusEffect 形式，duration=-1 整局）。
    ///
    /// 调用时机：StartNewRun → PlayerController Awake → 由 PermanentTalentLoader.Apply(player) 触发。
    /// </summary>
    public static class PermanentTalentLoader
    {
        public static void Apply(PlayerController player)
        {
            if (player == null) return;
            var status = player.GetComponent<StatusEffectController>();
            if (status == null) return;

            var unlocked = new HashSet<string>(SaveSystem.Instance.Data.unlockedTalentIds);
            if (unlocked.Count == 0) return;

            int applied = 0;
            foreach (var entry in PermanentTalentRegistry.AllTalents)
            {
                if (!unlocked.Contains(entry.reward.id)) continue;
                // 复用 RealmReward 的 apply（它会挂 StatusEffect with id=Talent_*）
                try
                {
                    entry.reward.apply?.Invoke(player);
                    applied++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PermanentTalent] apply 失败 {entry.reward.id}: {e.Message}");
                }
            }
            Debug.Log($"<color=#dfcfff>[PermanentTalent] 自动加载 {applied} 个跨局解锁的天赋</color>");
        }
    }
}
