using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TATools.FolderCommentTool
{
    public class FolderCommentNavigator : EditorWindow
    {
        Vector2 m_Scroll;
        string  m_Search = "";
        List<FolderCommentData> m_Cache;

        // 搜索/快速跳转：Shift+1（MenuItem 热键 #1）
        [MenuItem("Tools_3D/美术/TA工具/文件夹注释工具/快速跳转 #1")]
        public static void Open()
        {
            var win = GetWindow<FolderCommentNavigator>(true, "注释 - 快速跳转 (Shift+1)");
            win.minSize = new Vector2(420, 300);
            win.Show();
            win.Focus();
        }

        void OnEnable()  => RefreshCache();
        void OnFocus()   => RefreshCache();

        void RefreshCache()
        {
            var mgr = FolderCommentManager.Instance;
            mgr.Initialize();
            var all = mgr.GetAllComments();
            m_Cache = all != null ? all : new List<FolderCommentData>();
        }

        // 槽位下拉：Shift+1 留给搜索，定位槽位用 Shift+2~9
        // 下拉显示索引 0..8 ↔ 槽位值 k_SlotValues
        static readonly int[]    k_SlotValues  = { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        static readonly string[] k_SlotOptions = { "无", "Shift+2", "Shift+3", "Shift+4", "Shift+5", "Shift+6", "Shift+7", "Shift+8", "Shift+9" };

        void OnGUI()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            m_Search = EditorGUILayout.TextField(m_Search, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(40)))
                RefreshCache();
            EditorGUILayout.EndHorizontal();

            if (m_Cache == null || m_Cache.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无带注释的资产/文件夹。\n选中任意资产，在 Inspector 顶部「资产注释」里可添加注释；文件夹则在 Inspector 下方添加。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(2);
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            string searchLower = string.IsNullOrEmpty(m_Search) ? null : m_Search.ToLower();

            foreach (var data in m_Cache)
            {
                if (string.IsNullOrEmpty(data.title) && string.IsNullOrEmpty(data.comment))
                    continue;

                string path = AssetDatabase.GUIDToAssetPath(data.guid);
                if (string.IsNullOrEmpty(path)) continue;

                if (searchLower != null)
                {
                    bool match = path.ToLower().Contains(searchLower)
                              || (!string.IsNullOrEmpty(data.title) && data.title.ToLower().Contains(searchLower))
                              || (!string.IsNullOrEmpty(data.comment) && data.comment.ToLower().Contains(searchLower));
                    if (!match) continue;
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                EditorGUILayout.BeginVertical();
                if (!string.IsNullOrEmpty(data.title))
                {
                    var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = data.titleColor } };
                    EditorGUILayout.LabelField(data.title, style);
                }
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                // 快捷定位槽位下拉（Shift+2~9）
                int curIdx = System.Array.IndexOf(k_SlotValues, Mathf.Clamp(data.shortcutSlot, 0, 9));
                if (curIdx < 0) curIdx = 0; // 兼容历史数据里可能存在的槽位1 → 视为无
                int newIdx = EditorGUILayout.Popup(curIdx, k_SlotOptions, GUILayout.Width(80), GUILayout.Height(32));
                if (newIdx != curIdx)
                {
                    FolderCommentManager.Instance.SetShortcutSlot(data.guid, k_SlotValues[newIdx]);
                }

                if (GUILayout.Button("跳转", GUILayout.Width(50), GUILayout.Height(32)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
