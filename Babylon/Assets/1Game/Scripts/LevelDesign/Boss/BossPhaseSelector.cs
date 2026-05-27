using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.3 Boss 形态选择系统。
    ///
    /// 用法（在 SpawnBossRoom 中）：
    ///   var phase = BossPhaseSelector.SelectMainPhase(bossID);
    ///   if (phase != null)
    ///       BossPhaseSelector.ApplyPhase(spawnedBoss, phase);
    ///
    /// 选择规则：
    ///   1. 从 Boss_Phase_Config 中筛选 BossID 匹配的所有记录
    ///   2. 对每条记录用 BossFlagSet.Evaluate(RequiredFlags) 判定是否满足
    ///   3. 按 Priority 降序排序
    ///   4. 取第一条作为主形态；若有次高 Priority，作为 P2 阶段（血量 &lt;50% 切换）
    /// </summary>
    public static class BossPhaseSelector
    {
        public class PhaseSelectResult
        {
            public BossPhaseRow MainPhase;
            public BossPhaseRow Phase2;
        }

        public static PhaseSelectResult Select(int bossID)
        {
            var db = ConfigDatabase.Instance;
            var flags = BossFlagSet.Instance;

            // 1. 筛选 + 求值
            var matched = new List<BossPhaseRow>();
            foreach (var kv in db.BossPhases)
            {
                var row = kv.Value;
                if (row.BossID != bossID) continue;
                if (!flags.Evaluate(row.RequiredFlags)) continue;
                matched.Add(row);
            }

            // 2. 按 Priority 降序
            matched.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            if (matched.Count == 0)
            {
                Debug.LogWarning($"[BossPhase] BossID={bossID} 没有任何形态匹配，将使用默认配置。");
                return null;
            }

            var result = new PhaseSelectResult { MainPhase = matched[0] };
            if (matched.Count > 1 && matched[1].Priority < matched[0].Priority)
                result.Phase2 = matched[1];

            Debug.Log($"[BossPhase] BossID={bossID} 选定主形态：{result.MainPhase.PhaseName} (Priority={result.MainPhase.Priority})" +
                      (result.Phase2 != null ? $"，P2 形态：{result.Phase2.PhaseName}" : ""));
            return result;
        }

        public static BossPhaseRow SelectMainPhase(int bossID) => Select(bossID)?.MainPhase;

        // ------------------------------------------------------------
        // 应用形态到具体的 EnemyBoss 实例
        // ------------------------------------------------------------

        /// <summary>
        /// 在 Boss 已经 Spawn 完成后调用，按形态修正数值并播报对白。
        /// 实际接入 EnemyBoss 由 LevelDesignBridge 完成（避免本程序集污染 Enemy 模块）。
        /// </summary>
        public static void ApplyStatModifier(BossPhaseRow phase, ref float hp, ref float atk, ref float spd)
        {
            if (phase == null || string.IsNullOrWhiteSpace(phase.StatModifier)) return;

            // 格式：hp*1.2,atk*1.5,spd*0.8
            var parts = phase.StatModifier.Split(',');
            foreach (var raw in parts)
            {
                var part = raw.Trim();
                int starIdx = part.IndexOf('*');
                if (starIdx <= 0) continue;
                var key = part.Substring(0, starIdx).Trim().ToLowerInvariant();
                var valStr = part.Substring(starIdx + 1).Trim();
                if (!float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float mul))
                    continue;
                switch (key)
                {
                    case "hp": hp *= mul; break;
                    case "atk": atk *= mul; break;
                    case "spd": spd *= mul; break;
                }
            }
        }
    }
}
