using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.1.1 Boss Flag 系统 —— 事件与 Boss 形态之间的桥梁。
    /// 单个区域内独立，进入下一区域时调用 ClearAct() 重置；
    /// 跨区域的状态（如因果债）通过 SystemHooks 单独维护。
    ///
    /// Flag 类型支持：
    ///   - 布尔型：Set(name, 1)，用 Has() 查询
    ///   - 累计型：Add(name, delta)，用 GetValue() 取数值
    ///
    /// 表达式解析（用于 §12.3 RequiredFlags）：
    ///   "flagA=1"          → flagA == 1
    ///   "flagA>=3"         → flagA >= 3
    ///   "flagA=1&flagB>=2" → AND 组合
    ///   空字符串            → 始终满足（默认形态）
    /// </summary>
    public class BossFlagSet
    {
        private static BossFlagSet _instance;
        public static BossFlagSet Instance => _instance ??= new BossFlagSet();

        private readonly Dictionary<string, int> _flags = new();

        public event Action<string, int> OnFlagChanged;

        // ------------------------------------------------------------
        // 写入接口（由事件 / 战斗行为 / 系统联动调用）
        // ------------------------------------------------------------

        public void Set(string flagName, int value)
        {
            if (string.IsNullOrEmpty(flagName)) return;
            _flags[flagName] = value;
            OnFlagChanged?.Invoke(flagName, value);
            Debug.Log($"[BossFlag] Set {flagName}={value}");
        }

        public void Add(string flagName, int delta)
        {
            if (string.IsNullOrEmpty(flagName) || delta == 0) return;
            _flags.TryGetValue(flagName, out int cur);
            cur += delta;
            _flags[flagName] = cur;
            OnFlagChanged?.Invoke(flagName, cur);
            Debug.Log($"[BossFlag] Add {flagName} {(delta >= 0 ? "+" : "")}{delta} → {cur}");
        }

        // ------------------------------------------------------------
        // 读取接口
        // ------------------------------------------------------------

        public bool Has(string flagName) =>
            !string.IsNullOrEmpty(flagName) && _flags.ContainsKey(flagName) && _flags[flagName] != 0;

        public int GetValue(string flagName) =>
            !string.IsNullOrEmpty(flagName) && _flags.TryGetValue(flagName, out var v) ? v : 0;

        public IReadOnlyDictionary<string, int> AllFlags => _flags;

        // ------------------------------------------------------------
        // 表达式判定：用于 BossPhaseRow.RequiredFlags / EventRow.PrereqFlag
        // ------------------------------------------------------------

        /// <summary>
        /// 判定一个 RequiredFlags 表达式是否在当前 FlagSet 下成立。
        /// 空表达式 → true（默认满足）。
        /// </summary>
        public bool Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true;

            var clauses = expression.Split('&');
            foreach (var raw in clauses)
            {
                var clause = raw.Trim();
                if (clause.Length == 0) continue;
                if (!EvaluateClause(clause)) return false;
            }
            return true;
        }

        private bool EvaluateClause(string clause)
        {
            // 支持的比较运算符（按长度优先匹配）
            string[] ops = { ">=", "<=", "!=", "=", ">", "<" };
            foreach (var op in ops)
            {
                int idx = clause.IndexOf(op, StringComparison.Ordinal);
                if (idx <= 0) continue;
                var name = clause.Substring(0, idx).Trim();
                var rhs = clause.Substring(idx + op.Length).Trim();
                if (!int.TryParse(rhs, out int target))
                {
                    Debug.LogWarning($"[BossFlag] 表达式右侧无法解析为整数：{clause}");
                    return false;
                }
                int actual = GetValue(name);
                return op switch
                {
                    "=" => actual == target,
                    "!=" => actual != target,
                    ">=" => actual >= target,
                    "<=" => actual <= target,
                    ">" => actual > target,
                    "<" => actual < target,
                    _ => false
                };
            }
            // 无运算符 → 当作 "name>=1" 处理（兼容简写）
            return GetValue(clause.Trim()) >= 1;
        }

        // ------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------

        /// <summary>进入下一区域时调用，清空本区域 Flag</summary>
        public void ClearAct()
        {
            if (_flags.Count == 0) return;
            Debug.Log($"[BossFlag] 区域结束，清空 {_flags.Count} 个 Flag");
            _flags.Clear();
        }

        /// <summary>整局结束时调用</summary>
        public void ClearAll() => ClearAct();
    }
}
