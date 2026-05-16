using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府经济单例（v0.5 搜打撤核心）—— 跨局持久化的【灵气】资源。
    ///
    /// 跟 <see cref="PlayerResources"/>（局内灵力碎片，单局归零）区别：
    /// - PlayerResources.SpiritShards：局内击杀获得，商店消费，整局清零
    /// - CaveEconomy.Qi：跨局保留，主要用途：
    ///     1. 加速洞府模块（灵田生长 / 炼丹 / 炼器 等的"立即完成"按钮）
    ///     2. 残魂补偿（梦中死亡时把丢失的洞府素材转化为灵气）
    ///     3. 抵御魂伤 debuff
    ///
    /// 数据存储在 SaveSystem.Instance.Data.caveQi。
    /// </summary>
    public class CaveEconomy : MonoBehaviour
    {
        private static CaveEconomy _instance;
        public static CaveEconomy Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CaveEconomy");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CaveEconomy>();
                }
                return _instance;
            }
        }

        // ========== 灵气查询 ==========

        public int Qi => SaveSystem.Instance.Data.caveQi;

        public bool HasQi(int amount) => Qi >= amount;

        // ========== 增减 ==========

        public void AddQi(int amount)
        {
            if (amount == 0) return;
            var data = SaveSystem.Instance.Data;
            data.caveQi = Mathf.Max(0, data.caveQi + amount);
            SaveSystem.Instance.Save();

            GameEvents.Publish(new GameEvents.CaveQiChanged
            {
                NewQi = data.caveQi,
                Delta = amount
            });

            string color = amount > 0 ? "#88ff88" : "#ff8888";
            Debug.Log($"<color={color}>[CaveEconomy] 灵气 {(amount > 0 ? "+" : "")}{amount} → 当前 {data.caveQi}</color>");
        }

        public bool SpendQi(int amount)
        {
            if (!HasQi(amount))
            {
                Debug.Log($"<color=red>[CaveEconomy] 灵气不足：需要 {amount}，当前 {Qi}</color>");
                return false;
            }
            AddQi(-amount);
            return true;
        }

        // ========== 调试 ==========

        public void ResetQi()
        {
            var data = SaveSystem.Instance.Data;
            data.caveQi = 0;
            SaveSystem.Instance.Save();
        }
    }
}
