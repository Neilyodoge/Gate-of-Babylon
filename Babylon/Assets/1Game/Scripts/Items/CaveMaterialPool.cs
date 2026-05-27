using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 洞府素材掉落池（v0.5 搜打撤"搜"环节）。
    ///
    /// 运行时从 <c>Resources/CaveMaterials/</c> 自动加载所有 <see cref="ItemData"/>（要求 <see cref="ItemData.scope"/> = CaveMaterial）。
    /// 精英怪 / Boss / 宝藏房 死亡或开宝箱时调用 <see cref="SpawnRandom"/> 概率性掉一件。
    ///
    /// 跟普通敌人 <c>possibleDrops</c> 完全独立 —— CaveMaterial 是【额外】掉落，不抢正常灵物槽位。
    /// </summary>
    public static class CaveMaterialPool
    {
        private const string ResourcesPath = "CaveMaterials";

        private static ItemData[] _cached;
        private static List<ItemData> _byRarityCache;

        /// <summary>获取池中所有 CaveMaterial（懒加载，仅加载 scope == CaveMaterial 的 SO）</summary>
        public static ItemData[] All
        {
            get
            {
                if (_cached == null)
                {
                    var loaded = Resources.LoadAll<ItemData>(ResourcesPath);
                    var filtered = new List<ItemData>(loaded.Length);
                    foreach (var it in loaded)
                    {
                        if (it != null && it.scope == ItemScope.CaveMaterial) filtered.Add(it);
                    }
                    _cached = filtered.ToArray();
                    Debug.Log($"<color=cyan>[CaveMaterialPool] 加载 {_cached.Length} 件洞府素材（路径 Resources/{ResourcesPath}/）</color>");
                }
                return _cached;
            }
        }

        /// <summary>从池中随机取一件素材 spawn 在指定位置（带 chance 概率）</summary>
        public static ItemPickup SpawnRandom(Vector3 position, float chance = 1f)
        {
            if (All.Length == 0) return null;
            if (chance < 1f && Random.value > chance) return null;

            var picked = All[Random.Range(0, All.Length)];
            if (picked == null) return null;

            // 把素材掉在脚下（确保不沉地）
            Vector3 dropPos = position;
            dropPos.y = Mathf.Max(dropPos.y, 0.1f);
            var pickup = ItemPickup.Spawn(picked, dropPos);
            if (pickup != null)
            {
                Debug.Log($"<color=#a0d090>[CaveMaterialPool] 掉落洞府素材：{picked.itemName}（搜打撤·撤离才能带回）</color>");
            }
            return pickup;
        }

        /// <summary>从池中按 Category 过滤后随机 spawn（用于"Boss 必定掉一颗 PlantSeed"等场景）</summary>
        public static ItemPickup SpawnRandomOfCategory(Vector3 position, ItemCategory category)
        {
            if (All.Length == 0) return null;
            var pool = new List<ItemData>();
            foreach (var it in All)
                if (it != null && it.category == category) pool.Add(it);
            if (pool.Count == 0) return null;
            var picked = pool[Random.Range(0, pool.Count)];
            Vector3 dropPos = position;
            dropPos.y = Mathf.Max(dropPos.y, 0.1f);
            return ItemPickup.Spawn(picked, dropPos);
        }

        /// <summary>测试 / Hot reload 时清缓存（重新从 Resources 加载）</summary>
        public static void ClearCache() => _cached = null;

        /// <summary>按 itemName 查找 CaveMaterial 池中的 SO（用于读取 processedProductName / category）</summary>
        public static ItemData GetByName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            foreach (var it in All)
            {
                if (it != null && it.itemName == itemName) return it;
            }
            return null;
        }

        /// <summary>判断 itemName 对应的 CaveMaterial 是否为指定分类（PlantSeed / Herb / Pill ...）</summary>
        public static bool IsCategory(string itemName, ItemCategory category)
        {
            var it = GetByName(itemName);
            return it != null && it.category == category;
        }
    }
}
