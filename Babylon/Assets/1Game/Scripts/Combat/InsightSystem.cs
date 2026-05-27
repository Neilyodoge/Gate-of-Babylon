using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 顿悟系统（v0.5 修仙独有战斗机制 #2）。
    ///
    /// 悟性是【局内 + 局外混合资源】：
    /// - 局内：每次击杀敌人 / 完美闪避 / 完美连段 → 加悟性，到阈值触发"顿悟时刻"（3 选 1 免费 buff）
    /// - 撤离成功 → 50% 当前悟性转入 SaveData.accumulatedInsight（永久）
    /// - 死亡 → 全部丢失（残魂不补偿，因为悟性是"修为"，必须真实通关换）
    ///
    /// 永久悟性用途：在【悟道蒲团】消耗解锁化身天赋节点（跨局保留）。
    /// </summary>
    public class InsightSystem : MonoBehaviour
    {
        private static InsightSystem _instance;
        public static InsightSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("InsightSystem");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<InsightSystem>();
                }
                return _instance;
            }
        }

        // ========== 局内悟性 ==========

        public int RunInsight { get; private set; }

        /// <summary>下一次顿悟时刻阈值（每触发一次后递增 +50）</summary>
        public int NextMomentThreshold { get; private set; } = 50;

        public int TotalMomentsThisRun { get; private set; } = 0;

        /// <summary>单局顿悟时刻最大触发次数（防刷 · v0.5 Week 8 技术债清理）</summary>
        public const int MaxMomentsPerRun = 6;

        /// <summary>本局是否已用尽顿悟次数</summary>
        public bool IsMomentExhausted => TotalMomentsThisRun >= MaxMomentsPerRun;

        // ========== 永久悟性 ==========

        public int PermanentInsight => SaveSystem.Instance.Data.accumulatedInsight;

        // ========== 加悟性 ==========

        /// <summary>击杀普通怪 +1 / 精英怪 +3 / Boss +10（灵气浓度浓郁 +50%，灵脉 +100%）</summary>
        public void AddRunInsight(int amount, string reason)
        {
            if (amount <= 0) return;
            float densityMul = SpiritDensity.Current switch
            {
                SpiritDensityLevel.Rich => 1.5f,
                SpiritDensityLevel.Vein => 2.0f,
                _ => 1f
            };
            int real = Mathf.Max(1, Mathf.RoundToInt(amount * densityMul));
            RunInsight += real;

            GameEvents.Publish(new GameEvents.InsightChanged
            {
                NewRunInsight = RunInsight,
                Delta = real,
                Reason = reason,
                NextThreshold = NextMomentThreshold
            });

            CheckMomentTrigger();
        }

        private void CheckMomentTrigger()
        {
            if (RunInsight < NextMomentThreshold) return;
            // 上限保护：超出本局最大次数后悟性继续积累（撤离能转永久），但不再弹顿悟时刻
            if (TotalMomentsThisRun >= MaxMomentsPerRun)
            {
                NextMomentThreshold = int.MaxValue;  // 提到天上，不再触发
                return;
            }
            // 触发顿悟时刻
            TotalMomentsThisRun++;
            GameEvents.Publish(new GameEvents.InsightMomentTriggered
            {
                Threshold = NextMomentThreshold,
                MomentIndex = TotalMomentsThisRun
            });
            // 阶梯递增 + 后期更陡：1→50, 2→120, 3→210, 4→320, 5→450, 6→600
            NextMomentThreshold += 50 + TotalMomentsThisRun * 20;
            Debug.Log($"<color=#dfcfff>[InsightSystem] 顿悟时刻 #{TotalMomentsThisRun}/{MaxMomentsPerRun}！下次阈值 {NextMomentThreshold}</color>");
        }

        // ========== 局结束 ==========

        /// <summary>撤离成功：50% 悟性转入永久</summary>
        public void CommitOnExtract()
        {
            if (RunInsight == 0) return;
            int transferred = RunInsight / 2;
            SaveSystem.Instance.Data.accumulatedInsight += transferred;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[InsightSystem] 撤离 · {transferred} 悟性转入永久（当前永久 {PermanentInsight}）</color>");
            RunInsight = 0;
            NextMomentThreshold = 50;
            TotalMomentsThisRun = 0;
        }

        /// <summary>死亡：全部丢失（残魂不补偿）</summary>
        public void AbandonOnDeath()
        {
            if (RunInsight > 0)
                Debug.Log($"<color=#ff8866>[InsightSystem] 梦中身亡 · {RunInsight} 悟性散尽</color>");
            RunInsight = 0;
            NextMomentThreshold = 50;
            TotalMomentsThisRun = 0;
        }

        // ========== 永久悟性消耗（悟道蒲团调用）==========

        public bool SpendPermanentInsight(int amount)
        {
            var data = SaveSystem.Instance.Data;
            if (data.accumulatedInsight < amount) return false;
            data.accumulatedInsight -= amount;
            SaveSystem.Instance.Save();
            return true;
        }
    }
}
