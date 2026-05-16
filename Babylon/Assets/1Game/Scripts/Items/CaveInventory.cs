using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府素材跨局背包（v0.5 搜打撤核心）。
    ///
    /// 设计：玩家在梦境秘境中拾取的【洞府素材】（CaveMaterial scope）暂存在【本局缓冲】中，
    /// 只有"撤离成功" 时才会通过 <see cref="CommitCurrentRun"/> 持久化到 SaveSystem；
    /// "死亡" 时则调用 <see cref="AbandonCurrentRun"/> 全部丢失（残魂折算成灵气补偿）。
    ///
    /// 这是搜打撤的核心张力："拾到的好东西要带活才能算"。
    /// </summary>
    public class CaveInventory : MonoBehaviour
    {
        private static CaveInventory _instance;
        public static CaveInventory Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CaveInventory");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CaveInventory>();
                }
                return _instance;
            }
        }

        /// <summary>本局战斗中拾取的洞府素材缓冲（撤离前临时存储）</summary>
        private readonly Dictionary<string, int> _currentRunBuffer = new();

        public IReadOnlyDictionary<string, int> CurrentRunBuffer => _currentRunBuffer;

        /// <summary>本局缓冲中是否有洞府素材（用于 UI 显示"未带回"提示）</summary>
        public bool HasPendingMaterials => _currentRunBuffer.Count > 0;

        /// <summary>本局缓冲中所有素材的总数量</summary>
        public int TotalPendingCount
        {
            get
            {
                int total = 0;
                foreach (var kv in _currentRunBuffer) total += kv.Value;
                return total;
            }
        }

        // ========== 拾取（写入本局缓冲）==========

        /// <summary>
        /// 玩家在梦中拾取了一件洞府素材 —— 暂存在本局缓冲，撤离才能持久化。
        /// </summary>
        public void AddToBuffer(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return;
            if (item.scope != ItemScope.CaveMaterial)
            {
                Debug.LogWarning($"<color=yellow>[CaveInventory] 尝试添加非 CaveMaterial 灵物：{item.itemName} (scope={item.scope})</color>");
                return;
            }

            if (_currentRunBuffer.ContainsKey(item.itemName))
                _currentRunBuffer[item.itemName] += amount;
            else
                _currentRunBuffer[item.itemName] = amount;

            // 由 RunHUD / 浮字 UI 监听 CaveMaterialPickedUp 显示，不再复用 DamageNumberRequested
            Debug.Log($"<color=#a0d090>[CaveInventory] 本局缓冲 +{amount} {item.itemName}（当前缓冲总数 {TotalPendingCount}）</color>");
        }

        // ========== 撤离成功 / 死亡分支 ==========

        /// <summary>撤离成功 —— 把本局缓冲全部提交到存档</summary>
        public void CommitCurrentRun()
        {
            if (_currentRunBuffer.Count == 0)
            {
                Debug.Log("<color=gray>[CaveInventory] 撤离成功，但本局没有洞府素材</color>");
                return;
            }

            var save = SaveSystem.Instance;
            foreach (var kv in _currentRunBuffer)
            {
                save.AddCaveItem(kv.Key, kv.Value);
            }
            int commitTotal = TotalPendingCount;
            _currentRunBuffer.Clear();
            save.Save();

            Debug.Log($"<color=#88ff88>[CaveInventory] 撤离成功，{commitTotal} 件洞府素材已永久带回洞府</color>");
        }

        /// <summary>死亡分支 —— 全部丢失，按 10% 残魂折算成灵气补偿</summary>
        /// <returns>转化的灵气数量</returns>
        public int AbandonCurrentRun(float soulCompensationRate = 0.10f)
        {
            if (_currentRunBuffer.Count == 0) return 0;

            // 简化版残魂转化：每件素材给 5 点灵气 × 数量 × 10%
            const int qiPerItem = 5;
            int totalQi = 0;
            foreach (var kv in _currentRunBuffer)
            {
                totalQi += Mathf.FloorToInt(kv.Value * qiPerItem * soulCompensationRate);
            }

            int lostTotal = TotalPendingCount;
            _currentRunBuffer.Clear();

            if (totalQi > 0)
            {
                CaveEconomy.Instance.AddQi(totalQi);
                Debug.Log($"<color=#ff8888>[CaveInventory] 梦中死亡 · 失去 {lostTotal} 件洞府素材 · 残魂转化 {totalQi} 点灵气</color>");
            }
            else
            {
                Debug.Log($"<color=#ff8888>[CaveInventory] 梦中死亡 · 失去 {lostTotal} 件洞府素材</color>");
            }

            return totalQi;
        }

        // ========== 查询永久存档中的洞府素材 ==========

        public int GetPermanentCount(string itemName) => SaveSystem.Instance.GetCaveItemCount(itemName);
        public int GetPermanentCount(ItemData item) => item != null ? GetPermanentCount(item.itemName) : 0;

        /// <summary>从永久存档中消耗指定素材（洞府模块加工用）</summary>
        public bool ConsumePermanent(string itemName, int amount)
        {
            bool ok = SaveSystem.Instance.ConsumeCaveItem(itemName, amount);
            if (ok) SaveSystem.Instance.Save();
            return ok;
        }
    }
}
