using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 玩家资源管理器 —— 管理灵力碎片等货币资源
    /// 灵力碎片通过分解灵物/功法获得，可用于商店购买
    /// </summary>
    public class PlayerResources : MonoBehaviour
    {
        /// <summary>灵力碎片数量</summary>
        private int _spiritShards;

        public int SpiritShards => _spiritShards;

        // 单例（Demo1 简化用）
        public static PlayerResources Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>增加灵力碎片</summary>
        public void AddShards(int amount)
        {
            if (amount <= 0) return;
            _spiritShards += amount;

            GameEvents.Publish(new GameEvents.ResourceChanged
            {
                SpiritShards = _spiritShards,
                Delta = amount
            });

            Debug.Log($"<color=#88CCFF>获得灵力碎片 +{amount}（总计：{_spiritShards}）</color>");
        }

        /// <summary>消耗灵力碎片（返回是否成功）</summary>
        public bool SpendShards(int amount)
        {
            if (amount <= 0 || _spiritShards < amount) return false;
            _spiritShards -= amount;

            GameEvents.Publish(new GameEvents.ResourceChanged
            {
                SpiritShards = _spiritShards,
                Delta = -amount
            });

            return true;
        }

        /// <summary>是否有足够的灵力碎片</summary>
        public bool HasShards(int amount)
        {
            return _spiritShards >= amount;
        }

        /// <summary>根据灵物品阶计算分解获得的碎片数量</summary>
        public static int GetDecomposeShards(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Fan => 5,
                ItemRarity.Ling => 15,
                ItemRarity.Xuan => 40,
                ItemRarity.Di => 100,
                ItemRarity.Tian => 250,
                _ => 5
            };
        }

        /// <summary>清空资源（新一局开始时）</summary>
        public void Clear()
        {
            _spiritShards = 0;
            GameEvents.Publish(new GameEvents.ResourceChanged
            {
                SpiritShards = 0,
                Delta = 0
            });
        }
    }
}
