using UnityEditor;
using UnityEngine;

namespace TATools.FolderCommentTool
{
    /// <summary>
    /// Project 窗口右键菜单入口：为任意资产（C# 脚本 / 模型 mesh / ScriptableObject / Prefab / 文件夹等）
    /// 打开注释编辑窗口。和文件夹注释一致 —— 右键「编辑注释」后才弹出具体的注释 UI。
    ///
    /// 注：菜单挂在 "Assets/" 下，因此会同时出现在 Project 窗口右键菜单和顶部 Assets 菜单中（Unity 约定）。
    /// </summary>
    public static class AssetCommentContextMenu
    {
        // 放在 Assets 右键菜单较靠下的位置
        private const string MenuPath = "Assets/编辑注释 (Folder Comment)";

        [MenuItem(MenuPath, false, 1100)]
        private static void EditComment()
        {
            string path = GetSelectedAssetPath();
            if (string.IsNullOrEmpty(path))
                return;

            AssetCommentEditorWindow.Open(path);
        }

        [MenuItem(MenuPath, true)]
        private static bool EditCommentValidate()
        {
            return !string.IsNullOrEmpty(GetSelectedAssetPath());
        }

        /// <summary>
        /// 获取当前选中资产的路径（仅限工程内 Assets 下的资产）。
        /// </summary>
        private static string GetSelectedAssetPath()
        {
            Object obj = Selection.activeObject;
            if (obj == null)
                return null;

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                return null;

            // 只处理工程内 Assets 资产（排除场景对象 / 包内资源等）
            if (!path.StartsWith("Assets"))
                return null;

            return path;
        }
    }
}
