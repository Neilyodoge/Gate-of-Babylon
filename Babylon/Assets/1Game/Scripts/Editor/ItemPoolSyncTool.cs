using UnityEditor;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 一键把 <c>Assets/1Game/Data/Items</c> 下所有 <see cref="ItemData"/> 复制到
    /// <c>Assets/1Game/Resources/Items</c>，让 <see cref="ItemPool"/> 在打包后也能在运行时自动加载。
    ///
    /// 用法：菜单【仙途梦境/Items/同步 Data Items → Resources Items】 或【重新加载 ItemPool 缓存】。
    /// </summary>
    public static class ItemPoolSyncTool
    {
        private const string SrcDir = "Assets/1Game/Data/Items";
        private const string DstDir = "Assets/1Game/Resources/Items";

        [MenuItem("仙途梦境/Items/同步 Data Items → Resources Items")]
        public static void Sync()
        {
            if (!AssetDatabase.IsValidFolder(SrcDir))
            {
                EditorUtility.DisplayDialog("ItemPool 同步",
                    $"源目录不存在：{SrcDir}", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/1Game/Resources"))
                AssetDatabase.CreateFolder("Assets/1Game", "Resources");
            if (!AssetDatabase.IsValidFolder(DstDir))
                AssetDatabase.CreateFolder("Assets/1Game/Resources", "Items");

            var guids = AssetDatabase.FindAssets("t:ItemData", new[] { SrcDir });
            int copied = 0, skipped = 0;

            foreach (var guid in guids)
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileName(srcPath);
                var dstPath = $"{DstDir}/{fileName}";

                if (AssetDatabase.LoadAssetAtPath<ItemData>(dstPath) != null)
                {
                    AssetDatabase.DeleteAsset(dstPath);
                }
                if (AssetDatabase.CopyAsset(srcPath, dstPath))
                    copied++;
                else
                    skipped++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ItemPool.Reload();

            var msg = $"同步完成：复制 {copied} 件，失败 {skipped} 件。\nResources/Items 现已可供运行时自动加载。";
            Debug.Log("<color=cyan>[ItemPoolSyncTool] " + msg + "</color>");
            EditorUtility.DisplayDialog("ItemPool 同步", msg, "OK");
        }

        [MenuItem("仙途梦境/Items/重新加载 ItemPool 缓存")]
        public static void ReloadCache()
        {
            ItemPool.Reload();
            EditorUtility.DisplayDialog("ItemPool", $"已重新加载 {ItemPool.All.Length} 件灵物。", "OK");
        }
    }
}
