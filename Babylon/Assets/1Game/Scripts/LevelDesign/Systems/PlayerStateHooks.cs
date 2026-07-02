using System;
using UnityEngine;

// ReSharper disable once RedundantUsingDirective
using XianTu;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.4 修仙原生系统接入桥（v0.5 占位实现）。
    ///
    /// 因果 / 道心 / 命格 / 寿元 四大系统当前还没有完整玩法层实现，
    /// 本类提供：
    ///   1. 数值容器：让事件 / Boss 形态先有"地方写值"
    ///   2. 事件钩子：通知 UI 与玩法层（未来真实系统实现时直接订阅）
    ///   3. 表达式集成：Boss Flag 表达式中可以读取 karma_xxx / daoxin / lifespan 等隐式 Flag
    ///
    /// 这是"先开口子，后填血肉"的占位层 —— 事件配置可以正常写 KarmaChange / DaoxinChange，
    /// 等真实系统就位后只需把数值写入实际数据源即可。
    /// </summary>
    public class PlayerStateHooks
    {
        private static PlayerStateHooks _instance;
        public static PlayerStateHooks Instance => _instance ??= new PlayerStateHooks();

        // ------------------------------------------------------------
        // 因果债（GDD 因果系统）
        // - 杀戮债 / 盗宝债 / 辜负债 / 入魔债 等子项暂合并为单一计数
        // - 跨区域保留，整局结束清零
        // ------------------------------------------------------------
        public int KarmaDebt { get; private set; }
        public event Action<int> OnKarmaChanged;

        public void ChangeKarma(int delta)
        {
            if (delta == 0) return;
            KarmaDebt += delta;
            OnKarmaChanged?.Invoke(KarmaDebt);
            // 同步写入 BossFlagSet，便于 RequiredFlags 直接读
            BossFlagSet.Instance.Set("karma_debt", KarmaDebt);
            Debug.Log($"[PlayerState] 因果 {(delta >= 0 ? "+" : "")}{delta} → 当前 {KarmaDebt}");
        }

        // ------------------------------------------------------------
        // 道心（GDD §12.4.2，0~100）
        //   入定 80~100 / 清明 50~80 / 心摇 20~50 / 入魔 0~20
        // ------------------------------------------------------------
        public int Daoxin { get; private set; } = 60; // 默认清明
        public event Action<int> OnDaoxinChanged;

        public string DaoxinState
        {
            get
            {
                if (Daoxin >= 80) return "入定";
                if (Daoxin >= 50) return "清明";
                if (Daoxin >= 20) return "心摇";
                return "入魔";
            }
        }

        public void ChangeDaoxin(int delta)
        {
            if (delta == 0) return;
            Daoxin = Mathf.Clamp(Daoxin + delta, 0, 100);
            OnDaoxinChanged?.Invoke(Daoxin);
            BossFlagSet.Instance.Set("daoxin", Daoxin);
            Debug.Log($"[PlayerState] 道心 {(delta >= 0 ? "+" : "")}{delta} → {Daoxin} ({DaoxinState})");
        }

        // ------------------------------------------------------------
        // 寿元（GDD §12.4.4，单位：年）
        // ------------------------------------------------------------
        public int Lifespan { get; private set; } = 100;
        public event Action<int> OnLifespanChanged;

        public void ChangeLifespan(int delta)
        {
            if (delta == 0) return;
            Lifespan = Mathf.Max(0, Lifespan + delta);
            OnLifespanChanged?.Invoke(Lifespan);
            BossFlagSet.Instance.Set("lifespan", Lifespan);
            Debug.Log($"[PlayerState] 寿元 {(delta >= 0 ? "+" : "")}{delta} 年 → 剩余 {Lifespan} 年");
        }

        // ------------------------------------------------------------
        // 命格（GDD §12.4.3，隐藏，整局开始时随机一次）
        // ------------------------------------------------------------
        public enum FateType
        {
            天命之子,
            凡夫俗子,
            大凶之徒,
            道祖转世,
            妖族遗孤
        }

        public FateType Fate { get; private set; } = FateType.凡夫俗子;

        public void RollFate()
        {
            // 凡夫俗子权重最高（60%），其他各 10%
            // 注：命格的"局内战力被动"已退役；这里仍保留 RollFate 写入 BossFlag（fate_xxx），供关卡/事件系统的条件判定使用。
            float r = UnityEngine.Random.value;
            Fate = r switch
            {
                < 0.10f => FateType.天命之子,
                < 0.70f => FateType.凡夫俗子,
                < 0.80f => FateType.大凶之徒,
                < 0.90f => FateType.道祖转世,
                _ => FateType.妖族遗孤
            };
            BossFlagSet.Instance.Set($"fate_{Fate}", 1);
            Debug.Log($"[PlayerState] 本局命格已定（隐藏 · 仅供事件条件）：{Fate}");
        }

        // ------------------------------------------------------------
        // 整局重置（VillageHub.StartNewRun 时调用）
        // ------------------------------------------------------------
        public void ResetForNewRun()
        {
            KarmaDebt = 0;
            Daoxin = 60;
            Lifespan = 100;
            RollFate();
        }
    }
}
