using UnityEditor;
using UnityEngine;

namespace TATools.FolderCommentTool
{
    /// <summary>
    /// 在资产 Inspector 头部以「折叠 + 只读」方式展示注释（与文件夹注释一致）：
    ///   · 没有注释的资产：不显示任何内容
    ///   · 有注释的资产：显示一个默认折叠的「资产注释」区域，展开后只读展示标题/注释
    /// 添加 / 编辑 / 删除 统一走 Project 窗口右键「编辑注释」（AssetCommentEditorWindow），
    /// 因此这里不再内嵌编辑控件。
    /// 材质球 / 贴图 / 文件夹跳过（文件夹由 FolderCommentInspector 处理）。
    /// </summary>
    [InitializeOnLoad]
    public static class AssetCommentHeaderInjector
    {
        static readonly Color DefaultColor = new Color(0.4f, 0.8f, 1f);

        // 当前展示的资产 GUID（切换资产时把折叠状态重置回「折叠」）
        static string _viewGuid;
        // 折叠状态，默认折叠
        static bool _foldout;

        static AssetCommentHeaderInjector()
        {
            Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
        }

        static void OnHeaderGUI(Editor editor)
        {
            if (editor == null || editor.targets == null || editor.targets.Length != 1)
                return;

            Object obj = editor.target;
            if (obj == null)
                return;

            // 材质球和贴图不需要注释
            if (obj is Material || obj is Texture || obj is AssetImporter)
                return;

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return;          // 场景对象 / 非资产
            if (!path.StartsWith("Assets")) return;          // 只处理工程内 Assets 资产
            if (AssetDatabase.IsValidFolder(path)) return;   // 文件夹交给 FolderCommentInspector

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return;

            FolderCommentManager.Instance.Initialize();
            var data = FolderCommentManager.Instance.GetFolderComment(guid);
            bool hasComment = data != null &&
                (!string.IsNullOrEmpty(data.title) || !string.IsNullOrEmpty(data.comment));

            // 切换资产时重置为折叠
            if (_viewGuid != guid)
            {
                _viewGuid = guid;
                _foldout = false;
            }

            // 始终显示一个默认折叠的入口（有注释时加一个标记，方便识别）
            EditorGUILayout.Space(2);
            string foldoutLabel = hasComment ? "资产注释  ●" : "资产注释";
            _foldout = EditorGUILayout.Foldout(_foldout, foldoutLabel, true);
            if (!_foldout)
                return;

            EditorGUI.indentLevel++;

            if (hasComment)
            {
                // 只读展示：标题（带颜色）
                if (!string.IsNullOrEmpty(data.title))
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true };
                    titleStyle.normal.textColor = data.titleColor;
                    EditorGUILayout.LabelField(data.title, titleStyle);
                }

                // 只读展示：注释内容（富文本）
                if (!string.IsNullOrEmpty(data.comment))
                {
                    var commentStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                    EditorGUILayout.LabelField(data.comment, commentStyle);
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("编辑注释", GUILayout.Width(90)))
                {
                    AssetCommentEditorWindow.Open(path);
                }
            }
            else
            {
                // 暂无注释：提供添加入口
                EditorGUILayout.LabelField("（暂无注释）", EditorStyles.miniLabel);
                if (GUILayout.Button("添加注释", GUILayout.Width(90)))
                {
                    AssetCommentEditorWindow.Open(path);
                }
            }

            EditorGUI.indentLevel--;
        }
    }
}
