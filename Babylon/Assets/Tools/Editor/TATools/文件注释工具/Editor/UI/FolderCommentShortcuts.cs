using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace TATools.FolderCommentTool
{
    /// <summary>
    /// 注释工具的快捷定位快捷键（Shift+2 ~ Shift+9）。
    /// 说明：
    ///   · 搜索/快速跳转窗口 = Shift+1（由 FolderCommentNavigator 的 MenuItem #1 提供）
    ///   · Shift+1 留给搜索，所以定位槽位从 Shift+2 开始
    ///   · 这些用 ShortcutManager，可在 Edit > Shortcuts 里重绑
    /// </summary>
    public static class FolderCommentShortcuts
    {
        [Shortcut("TATools/文件夹注释/定位槽位 2", KeyCode.Alpha2, ShortcutModifiers.Shift)] static void Jump2() => JumpToSlot(2);
        [Shortcut("TATools/文件夹注释/定位槽位 3", KeyCode.Alpha3, ShortcutModifiers.Shift)] static void Jump3() => JumpToSlot(3);
        [Shortcut("TATools/文件夹注释/定位槽位 4", KeyCode.Alpha4, ShortcutModifiers.Shift)] static void Jump4() => JumpToSlot(4);
        [Shortcut("TATools/文件夹注释/定位槽位 5", KeyCode.Alpha5, ShortcutModifiers.Shift)] static void Jump5() => JumpToSlot(5);
        [Shortcut("TATools/文件夹注释/定位槽位 6", KeyCode.Alpha6, ShortcutModifiers.Shift)] static void Jump6() => JumpToSlot(6);
        [Shortcut("TATools/文件夹注释/定位槽位 7", KeyCode.Alpha7, ShortcutModifiers.Shift)] static void Jump7() => JumpToSlot(7);
        [Shortcut("TATools/文件夹注释/定位槽位 8", KeyCode.Alpha8, ShortcutModifiers.Shift)] static void Jump8() => JumpToSlot(8);
        [Shortcut("TATools/文件夹注释/定位槽位 9", KeyCode.Alpha9, ShortcutModifiers.Shift)] static void Jump9() => JumpToSlot(9);

        /// <summary>
        /// 在 Project 中选中并 ping 分配到该槽位的资产/文件夹
        /// </summary>
        public static void JumpToSlot(int slot)
        {
            FolderCommentManager.Instance.Initialize();

            var data = FolderCommentManager.Instance.GetCommentBySlot(slot);
            if (data == null)
            {
                Debug.Log($"[FolderComment] 槽位 Shift+{slot} 还没分配资产");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(data.guid);
            if (string.IsNullOrEmpty(path))
                return;

            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }
    }
}
