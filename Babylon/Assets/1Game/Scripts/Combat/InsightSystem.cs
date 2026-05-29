using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 悟性系统（v0.5；v0.5.4 移除"顿悟时刻 3 选 1"，悟性回归纯积累资源）。
    ///
    /// 悟性是【局内 + 局外混合资源】：
    /// - 局内：每次击杀敌人 / 完美闪避 / 完美连段 → 加悟性
    /// - 撤离成功 → 50% 当前悟性转入 SaveData.accumulatedInsight（永久）
    /// - 死亡 → 全部丢失（残念不补偿，因为悟性是"修为"，必须真实撤离换）
    ///
    /// 永久悟性唯一用途：在【悟道蒲团】消耗解锁化身天赋节点（跨局保留）。
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

        // ========== 永久悟性 ==========

        public int PermanentInsight => SaveSystem.Instance.Data.accumulatedInsight;

        // ========== 加悟性 ==========

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

        /// <summary>撤离成功：50% 悟性转入永久</summary>
        public void CommitOnExtract()
        {
            if (RunInsight == 0) return;
            int transferred = RunInsight / 2;
            SaveSystem.Instance.Data.accumulatedInsight += transferred;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[InsightSystem] 撤离 · {transferred} 悟性转入永久（当前永久 {PermanentInsight}）</color>");
            RunInsight = 0;
        }

        /// <summary>死亡：全部丢失（残念不补偿）</summary>
        public void AbandonOnDeath()
        {
            if (RunInsight > 0)
                Debug.Log($"<color=#ff8866>[InsightSystem] 秘境中身亡 · {RunInsight} 悟性散尽</color>");
            RunInsight = 0;
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
