using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XianTu
{
    /// <summary>
    /// 灵物总注册器（v0.5 Week 6）—— 解决"Inspector 手挂 itemPool"易遗忘的问题。
    /// 编辑器同步工具在 <c>Assets/1Game/Scripts/Editor/ItemPoolSyncTool.cs</c>。
    ///
    /// 加载顺序（运行时）：
    /// 1. <c>Resources/Items</c> 目录下所有 <see cref="ItemData"/>（scope = RunOnly）
    /// 2. 如果 #1 为空且处于 Editor，则用 <c>AssetDatabase</c> 扫描 <c>Assets/1Game/Data/Items</c>
    /// 3. 如果 #1 和 #2 都为空，调用方应回退到 GameManager.itemPool（Inspector 手挂）
    ///
    /// 配套 Editor 工具：菜单 "仙途秘境/Items/同步 Data/Items → Resources/Items"
    /// 把 Data/Items 下所有 ItemData 复制到 Resources/Items（已存在的会覆盖），
    /// 一次同步后，所有 build 都能在运行时无需 Inspector 手挂即可加载。
    /// </summary>
    public static class ItemPool
    {
        private const string ResourcesPath = "Items";

        private static ItemData[] _runtimeCache;

        /// <summary>获取所有"局内灵物"（scope = RunOnly）。空池时返回长度 0 的数组而不是 null。</summary>
        public static ItemData[] All
        {
            get
            {
                if (_runtimeCache == null) Reload();
                return _runtimeCache;
            }
        }

        /// <summary>强制刷新缓存。Demo1Setup / 编辑器工具 用</summary>
        public static void Reload()
        {
            var loaded = Resources.LoadAll<ItemData>(ResourcesPath);
            if (loaded != null && loaded.Length > 0)
            {
                var filtered = new List<ItemData>(loaded.Length);
                foreach (var it in loaded)
                {
                    if (it != null && it.scope == ItemScope.RunOnly) filtered.Add(it);
                }
                _runtimeCache = filtered.ToArray();
                Debug.Log($"<color=cyan>[ItemPool] 从 Resources/{ResourcesPath}/ 加载 {_runtimeCache.Length} 件灵物（运行时自动注册）</color>");
                return;
            }

#if UNITY_EDITOR
            // 编辑器降级：从 Data/Items 扫
            var guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/1Game/Data/Items" });
            if (guids != null && guids.Length > 0)
            {
                var items = new List<ItemData>();
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var it = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                    if (it != null && it.scope == ItemScope.RunOnly) items.Add(it);
                }
                _runtimeCache = items.ToArray();
                Debug.LogWarning($"<color=yellow>[ItemPool] Resources/Items 为空，编辑器降级从 Data/Items 加载 {_runtimeCache.Length} 件 ——\n" +
                    "正式打包前请执行：菜单【仙途秘境/Items/同步 Data Items → Resources/Items】</color>");
                return;
            }
#endif
            _runtimeCache = new ItemData[0];
            Debug.LogWarning("[ItemPool] 未找到任何 ItemData。请检查 Resources/Items/ 或 Data/Items/ 目录。");
        }

        /// <summary>把 Inspector 配置的 itemPool 与运行时池合并（Inspector 优先 / 不重复）</summary>
        public static ItemData[] MergeWithInspector(ItemData[] inspectorPool)
        {
            var auto = All;
            if (inspectorPool == null || inspectorPool.Length == 0) return auto;
            if (auto.Length == 0) return inspectorPool;

            var merged = new List<ItemData>(inspectorPool.Length + auto.Length);
            var seen = new HashSet<string>();

            foreach (var it in inspectorPool)
            {
                if (it == null) continue;
                if (seen.Add(it.itemName)) merged.Add(it);
            }
            foreach (var it in auto)
            {
                if (it == null) continue;
                if (seen.Add(it.itemName)) merged.Add(it);
            }
            return merged.ToArray();
        }
    }

}
