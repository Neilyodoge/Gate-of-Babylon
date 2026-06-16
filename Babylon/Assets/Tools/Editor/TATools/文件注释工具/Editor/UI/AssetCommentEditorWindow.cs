using UnityEditor;
using UnityEngine;

namespace TATools.FolderCommentTool
{
    /// <summary>
    /// 资产注释编辑弹窗：通过 Project 窗口右键菜单打开，
    /// 支持对任意资产（C# 脚本 / 模型 mesh / ScriptableObject / Prefab 等，含文件夹）按 GUID 编辑注释。
    ///
    /// 之所以用独立窗口而不是 Inspector 头部注入：
    ///   C# 脚本走 MonoImporter、模型/mesh 走 ModelImporter，
    ///   AssetCommentHeaderInjector 会跳过 AssetImporter，导致这类资产没有注释入口。
    ///   右键菜单直接拿选中资产的路径/GUID，绕开 Importer 限制，对所有资产都通用。
    ///
    /// 存储复用 FolderCommentManager（按 GUID），因此 Project 窗口标签 / 快速跳转 / 槽位全部自动支持。
    /// </summary>
    public class AssetCommentEditorWindow : EditorWindow
    {
        private static readonly Color DefaultColor = new Color(0.4f, 0.8f, 1f);

        // 目标资产
        private string _guid;
        private string _assetPath;

        // 临时编辑值
        private string _title = string.Empty;
        private string _comment = string.Empty;
        private Color _color = DefaultColor;

        private Vector2 _scroll;

        /// <summary>
        /// 打开注释编辑窗口（针对指定资产路径）
        /// </summary>
        public static void Open(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var win = GetWindow<AssetCommentEditorWindow>(true, "资产注释", true);
            win.minSize = new Vector2(360, 320);
            win.Load(assetPath, guid);
            win.Show();
            win.Focus();
        }

        private void Load(string assetPath, string guid)
        {
            _assetPath = assetPath;
            _guid = guid;

            FolderCommentManager.Instance.Initialize();
            var data = FolderCommentManager.Instance.GetFolderComment(guid);
            _title = data != null ? data.title : string.Empty;
            _comment = data != null ? data.comment : string.Empty;
            _color = data != null ? data.titleColor : DefaultColor;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                EditorGUILayout.HelpBox("未选择有效资产。请在 Project 窗口右键资产后重新打开。", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ===== 目标资产（只读展示，点击可定位）=====
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(_assetPath);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("目标资产", asset, typeof(Object), false);
            }
            EditorGUILayout.LabelField("路径", _assetPath, EditorStyles.miniLabel);

            EditorGUILayout.Space(6);

            // ===== 编辑区 =====
            _title = EditorGUILayout.TextField("标题", _title);
            _color = EditorGUILayout.ColorField("颜色", _color);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("注释（支持富文本 <b>/<i>/<size>/<color>）");
            _comment = EditorGUILayout.TextArea(_comment, FolderCommentStyles.CommentTextAreaStyle, GUILayout.MinHeight(96));

            EditorGUILayout.Space(8);

            // ===== 预览 =====
            if (!string.IsNullOrEmpty(_title) || !string.IsNullOrEmpty(_comment))
            {
                EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (!string.IsNullOrEmpty(_title))
                {
                    var titleStyle = new GUIStyle(EditorStyles.boldLabel) { richText = true };
                    titleStyle.normal.textColor = _color;
                    EditorGUILayout.LabelField(_title, titleStyle);
                }

                if (!string.IsNullOrEmpty(_comment))
                {
                    var commentStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
                    EditorGUILayout.LabelField(_comment, commentStyle);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);

            // ===== 保存 / 删除 =====
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_title) && string.IsNullOrEmpty(_comment)))
            {
                if (GUILayout.Button("保存", GUILayout.Height(28)))
                {
                    FolderCommentManager.Instance.SetFolderComment(_guid, _title, _comment, _color);
                    EditorApplication.RepaintProjectWindow();
                    ShowNotification(new GUIContent("已保存"));
                }
            }
            using (new EditorGUI.DisabledScope(FolderCommentManager.Instance.GetFolderComment(_guid) == null))
            {
                if (GUILayout.Button("删除", GUILayout.Height(28)))
                {
                    FolderCommentManager.Instance.RemoveFolderComment(_guid);
                    _title = string.Empty;
                    _comment = string.Empty;
                    _color = DefaultColor;
                    EditorApplication.RepaintProjectWindow();
                    ShowNotification(new GUIContent("已删除"));
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("快捷定位槽位（Shift+2~9）在「快速跳转」窗口(Shift+1)里分配。", MessageType.None);

            EditorGUILayout.EndScrollView();
        }
    }
}
