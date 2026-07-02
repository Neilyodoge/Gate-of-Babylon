using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 经验系统（局内 + 局外混合资源）：
    /// - 局内：每次击杀敌人 / 完美闪避 / 完美连段 → 加经验
    /// - 撤离成功 → 50% 当前经验转入 SaveData.accumulatedInsight（永久）
    /// - 死亡 → 全部丢失（必须真实撤离才能换）
    ///
    /// 永久经验用途：消耗解锁天赋节点（跨局保留）。
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

        // ========== 局内经验 ==========

        public int RunInsight { get; private set; }

        // ========== 永久经验 ==========

        public int PermanentInsight => SaveSystem.Instance.Data.accumulatedInsight;

        // ========== 加经验 ==========

        /// <summary>击杀普通怪 +1 / 精英怪 +3 / Boss +10</summary>
        public void AddRunInsight(int amount, string reason)
        {
            if (amount <= 0) return;
            int real = Mathf.Max(1, amount);
            RunInsight += real;

            GameEvents.Publish(new GameEvents.InsightChanged
            {
                NewRunInsight = RunInsight,
                Delta = real,
                Reason = reason
            });
        }

        // ========== 局结束 ==========

        /// <summary>撤离成功：50% 经验转入永久（可带层深倍率）</summary>
        public int CommitOnExtract(float multiplier = 1f)
        {
            if (RunInsight == 0) return 0;
            int baseAmount = RunInsight / 2;
            int transferred = Mathf.RoundToInt(baseAmount * multiplier);
            SaveSystem.Instance.Data.accumulatedInsight += transferred;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[InsightSystem] 撤离 · {transferred} 经验转入永久（×{multiplier:F2}）（当前永久 {PermanentInsight}）</color>");
            int raw = RunInsight;
            RunInsight = 0;
            return raw;
        }

        /// <summary>死亡：全部丢失</summary>
        public void AbandonOnDeath()
        {
            if (RunInsight > 0)
                Debug.Log($"<color=#ff8866>[InsightSystem] 秘境中身亡 · {RunInsight} 经验散尽</color>");
            RunInsight = 0;
        }

        // ========== 永久经验消耗 ==========

        /// <summary>直接给永久经验（奖励用）。</summary>
        public void GrantPermanent(int amount)
        {
            if (amount <= 0) return;
            SaveSystem.Instance.Data.accumulatedInsight += amount;
            SaveSystem.Instance.Save();
        }

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
