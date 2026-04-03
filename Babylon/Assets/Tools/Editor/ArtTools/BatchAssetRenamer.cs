using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 小n的 批量重命名工具
/// 菜单路径：nTools/美术工具/批量重命名
/// 对选中文件夹内的资产进行批量重命名，按序号排列（01, 02, ... 或 001, 002, ...）
/// </summary>
public class BatchAssetRenamer : EditorWindow
{
    // ==================== 设置 ====================
    private string _renamePrefix = "";  // 重命名前缀
    private Vector2 _folderScrollPos;
    private Vector2 _assetScrollPos;
    private Vector2 _logScrollPos;

    // ==================== 来源列表（文件夹 + 散文件） ====================
    private readonly List<string> _folderPaths = new List<string>();
    private readonly List<string> _loosFilePaths = new List<string>(); // 直接拖入的散文件路径

    // ==================== 资产类别分组（同类别内按去扩展名比较冲突） ====================
    private static readonly Dictionary<string, string> ExtensionToCategory = new Dictionary<string, string>
    {
        // 贴图类
        { ".png", "texture" }, { ".tga", "texture" }, { ".jpg", "texture" }, { ".jpeg", "texture" },
        { ".psd", "texture" }, { ".exr", "texture" }, { ".hdr", "texture" }, { ".bmp", "texture" },
        { ".tif", "texture" }, { ".tiff", "texture" },
        // 模型类
        { ".fbx", "model" }, { ".obj", "model" }, { ".blend", "model" }, { ".dae", "model" },
        { ".3ds", "model" }, { ".max", "model" }, { ".ma", "model" }, { ".mb", "model" },
        // 音频类
        { ".wav", "audio" }, { ".mp3", "audio" }, { ".ogg", "audio" }, { ".aiff", "audio" },
        { ".flac", "audio" },
        // 动画类
        { ".anim", "animation" }, { ".controller", "animation" }, { ".overridecontroller", "animation" },
        // 材质类
        { ".mat", "material" },
        // Shader类
        { ".shader", "shader" }, { ".shadergraph", "shader" }, { ".cginc", "shader" },
        { ".hlsl", "shader" }, { ".glsl", "shader" },
    };

    /// <summary>
    /// 获取扩展名对应的资产类别，未知类别返回扩展名本身（即不与其他类型冲突）
    /// </summary>
    private static string GetAssetCategory(string extension)
    {
        string ext = extension.ToLower();
        return ExtensionToCategory.ContainsKey(ext) ? ExtensionToCategory[ext] : ext;
    }

    // ==================== 资产列表 ====================
    private class AssetEntry
    {
        public string assetPath;       // 资产路径
        public string fileName;        // 文件名（含扩展名）
        public string extension;       // 扩展名
        public bool selected;          // 是否勾选
        public Object cachedAsset;     // 缓存的资产引用（用于显示图标）
        public string previewNewName;  // 预览的新名称
        public bool hasConflict;       // 是否存在命名冲突
        public string conflictReason;  // 冲突原因
    }
    private readonly List<AssetEntry> _assetEntries = new List<AssetEntry>();
    private bool _assetsScanned = false;

    // ==================== 日志 ====================
    private readonly List<string> _logMessages = new List<string>();
    private bool _hasRenamed = false;

    // ==================== 全选状态 ====================
    private bool _selectAll = true;

    // ==================== 冲突检测 ====================
    private int _conflictCount = 0;

    [MenuItem("nTools/美术工具/批量重命名", false, 50)]
    public static void ShowWindow()
    {
        var window = GetWindow<BatchAssetRenamer>("批量重命名");
        window.minSize = new Vector2(500, 450);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("批量重命名工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "将资产按序号批量重命名（支持拖入文件夹或直接拖入文件）。\n" +
            "命名规则：前缀_序号.扩展名（如 hero_01.png, hero_02.fbx）\n" +
            "资产数量 ≤ 99 时使用两位序号（01-99），超过 99 时自动使用三位序号（001-999）。\n" +
            "冲突检测按类别进行：同类别（如贴图 tga/png/jpg）内不允许去掉扩展名后同名。",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ===== 重命名前缀 =====
        EditorGUILayout.LabelField("重命名前缀（必填）", EditorStyles.miniBoldLabel);
        _renamePrefix = EditorGUILayout.TextField("命名前缀", _renamePrefix);

        EditorGUILayout.Space(4);

        // ===== 来源列表 =====
        EditorGUILayout.LabelField("拖入文件夹或文件", EditorStyles.miniBoldLabel);
        DrawSourceList();

        EditorGUILayout.Space(4);

        // ===== 资产列表（勾选） =====
        if (_assetsScanned && _assetEntries.Count > 0)
        {
            DrawAssetList();

            EditorGUILayout.Space(4);

            // ===== 冲突提示 =====
            if (_conflictCount > 0)
            {
                EditorGUILayout.HelpBox($"存在 {_conflictCount} 个命名冲突，请修改前缀或调整勾选项后再执行重命名。", MessageType.Error);
            }

            // ===== 重命名按钮 =====
            int selectedCount = _assetEntries.Count(e => e.selected);
            bool hasConflicts = _conflictCount > 0;
            GUI.enabled = selectedCount > 0 && !string.IsNullOrEmpty(_renamePrefix) && !hasConflicts;
            string btnText = hasConflicts
                ? "⚠ 请先修改命名冲突"
                : $"▶ 重命名选中的 {selectedCount} 个资产";
            if (GUILayout.Button(btnText, GUILayout.Height(30)))
            {
                ExecuteRename();
            }
            GUI.enabled = true;
        }
        else if (_assetsScanned)
        {
            EditorGUILayout.HelpBox("未在指定文件夹中找到任何资产。", MessageType.Warning);
        }

        // ===== 日志输出 =====
        if (_hasRenamed && _logMessages.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("重命名日志", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("清空日志", GUILayout.Width(80), GUILayout.Height(20)))
                {
                    _logMessages.Clear();
                    _hasRenamed = false;
                }
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScrollPos, EditorStyles.helpBox, GUILayout.MaxHeight(160)))
            {
                _logScrollPos = scroll.scrollPosition;
                foreach (var msg in _logMessages)
                {
                    EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    // ==================================================================================
    //  来源列表绘制（支持拖拽文件夹和文件）
    // ==================================================================================
    private void DrawSourceList()
    {
        // 拖拽区域
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        var dropStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };

        int totalSources = _folderPaths.Count + _loosFilePaths.Count;
        bool isDraggingOver = dropArea.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0;
        Color oldBg = GUI.backgroundColor;
        if (isDraggingOver) GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 1f);
        string dropHint = totalSources == 0
            ? "拖拽文件夹或文件到此处添加"
            : $"拖拽添加（已有 {_folderPaths.Count} 个文件夹, {_loosFilePaths.Count} 个散文件）";
        GUI.Box(dropArea, dropHint, dropStyle);
        GUI.backgroundColor = oldBg;

        // 处理拖拽事件
        if (dropArea.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(path)) continue;

                    if (AssetDatabase.IsValidFolder(path))
                    {
                        // 文件夹
                        if (!_folderPaths.Contains(path))
                            _folderPaths.Add(path);
                    }
                    else
                    {
                        // 文件
                        string ext = Path.GetExtension(path).ToLower();
                        if (ext != ".meta" && !_loosFilePaths.Contains(path))
                            _loosFilePaths.Add(path);
                    }
                }
                // 添加后自动扫描资产
                ScanAssets();
                Event.current.Use();
                Repaint();
            }
        }

        // 列表显示
        int removeFolderIndex = -1;
        int removeFileIndex = -1;
        if (totalSources > 0)
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_folderScrollPos, GUILayout.MaxHeight(100)))
            {
                _folderScrollPos = scroll.scrollPosition;
                // 文件夹列表
                for (int i = 0; i < _folderPaths.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("📁", GUILayout.Width(20));
                        EditorGUILayout.LabelField(_folderPaths[i]);
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                            removeFolderIndex = i;
                    }
                }
                // 散文件列表
                for (int i = 0; i < _loosFilePaths.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("📄", GUILayout.Width(20));
                        EditorGUILayout.LabelField(_loosFilePaths[i]);
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                            removeFileIndex = i;
                    }
                }
            }
        }
        if (removeFolderIndex >= 0)
        {
            _folderPaths.RemoveAt(removeFolderIndex);
            ScanAssets();
        }
        if (removeFileIndex >= 0)
        {
            _loosFilePaths.RemoveAt(removeFileIndex);
            ScanAssets();
        }

        // 底部按钮
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 从选中添加", GUILayout.Height(20)))
            {
                foreach (var obj in Selection.objects)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(path)) continue;

                    if (AssetDatabase.IsValidFolder(path))
                    {
                        if (!_folderPaths.Contains(path))
                            _folderPaths.Add(path);
                    }
                    else
                    {
                        string ext = Path.GetExtension(path).ToLower();
                        if (ext != ".meta" && !_loosFilePaths.Contains(path))
                            _loosFilePaths.Add(path);
                    }
                }
                ScanAssets();
            }
            if (totalSources > 0 && GUILayout.Button("清空全部", GUILayout.Width(80), GUILayout.Height(20)))
            {
                _folderPaths.Clear();
                _loosFilePaths.Clear();
                _assetEntries.Clear();
                _assetsScanned = false;
            }
        }
    }

    // ==================================================================================
    //  资产列表绘制（带勾选和预览）
    // ==================================================================================
    private void DrawAssetList()
    {
        EditorGUILayout.LabelField($"资产列表（共 {_assetEntries.Count} 个，已选 {_assetEntries.Count(e => e.selected)} 个）", EditorStyles.miniBoldLabel);

        // 全选/取消全选
        using (new EditorGUILayout.HorizontalScope())
        {
            bool newSelectAll = EditorGUILayout.ToggleLeft("全选 / 取消全选", _selectAll, GUILayout.Width(140));
            if (newSelectAll != _selectAll)
            {
                _selectAll = newSelectAll;
                foreach (var entry in _assetEntries)
                    entry.selected = _selectAll;
            }

            GUILayout.FlexibleSpace();

            // 实时更新预览名称
            if (!string.IsNullOrEmpty(_renamePrefix))
            {
                EditorGUILayout.LabelField("预览已更新", EditorStyles.miniLabel, GUILayout.Width(70));
            }
        }

        // 更新预览名称并检测冲突
        UpdatePreviewNames();
        CheckConflicts();

        // 构建显示顺序：冲突项排在最前面
        var sortedIndices = new List<int>();
        for (int i = 0; i < _assetEntries.Count; i++)
        {
            if (_assetEntries[i].hasConflict) sortedIndices.Add(i);
        }
        for (int i = 0; i < _assetEntries.Count; i++)
        {
            if (!_assetEntries[i].hasConflict) sortedIndices.Add(i);
        }

        // 资产列表
        using (var scroll = new EditorGUILayout.ScrollViewScope(_assetScrollPos, GUILayout.MaxHeight(200)))
        {
            _assetScrollPos = scroll.scrollPosition;
            for (int si = 0; si < sortedIndices.Count; si++)
            {
                var entry = _assetEntries[sortedIndices[si]];

                // 冲突项背景高亮
                if (entry.hasConflict)
                {
                    Rect rowRect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.DrawRect(rowRect, new Color(0.8f, 0.15f, 0.15f, 0.15f));
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                }

                // 勾选框
                bool newSelected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));
                if (newSelected != entry.selected)
                {
                    entry.selected = newSelected;
                    _selectAll = _assetEntries.All(e => e.selected);
                }

                // 冲突标记
                if (entry.hasConflict)
                {
                    var errorIconStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
                    EditorGUILayout.LabelField("⚠", errorIconStyle, GUILayout.Width(18));
                }

                // 资产图标
                if (entry.cachedAsset == null)
                    entry.cachedAsset = AssetDatabase.LoadMainAssetAtPath(entry.assetPath);

                var icon = AssetDatabase.GetCachedIcon(entry.assetPath);
                if (icon != null)
                {
                    GUILayout.Label(new GUIContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                }

                // 原名称（冲突时红色）
                if (entry.hasConflict)
                {
                    var redStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
                    EditorGUILayout.LabelField(entry.fileName, redStyle, GUILayout.MinWidth(100));
                }
                else
                {
                    EditorGUILayout.LabelField(entry.fileName, GUILayout.MinWidth(100));
                }

                // 箭头
                EditorGUILayout.LabelField("→", GUILayout.Width(20));

                // 预览新名称
                if (entry.selected && !string.IsNullOrEmpty(entry.previewNewName))
                {
                    if (entry.hasConflict)
                    {
                        // 冲突：红色显示新名称 + 冲突原因
                        var conflictStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
                        EditorGUILayout.LabelField($"{entry.previewNewName}  [{entry.conflictReason}]", conflictStyle, GUILayout.MinWidth(180));
                    }
                    else
                    {
                        var greenStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
                        EditorGUILayout.LabelField(entry.previewNewName, greenStyle, GUILayout.MinWidth(120));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(entry.selected ? "(需要前缀)" : "(未选中)", EditorStyles.miniLabel, GUILayout.MinWidth(120));
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // ==================================================================================
    //  扫描文件夹中的资产
    // ==================================================================================
    private void ScanAssets()
    {
        _assetEntries.Clear();
        _assetsScanned = true;
        _selectAll = true;
        _conflictCount = 0;

        var addedPaths = new HashSet<string>();

        // ---- 扫描文件夹中的资产 ----
        foreach (string folderPath in _folderPaths)
        {
            if (!AssetDatabase.IsValidFolder(folderPath)) continue;

            string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // 跳过子文件夹中的资产（只处理直接子级）
                string assetDir = Path.GetDirectoryName(assetPath).Replace("\\", "/");
                if (assetDir != folderPath) continue;

                if (AssetDatabase.IsValidFolder(assetPath)) continue;
                if (addedPaths.Contains(assetPath)) continue;
                addedPaths.Add(assetPath);

                string ext = Path.GetExtension(assetPath).ToLower();
                if (ext == ".meta") continue;

                _assetEntries.Add(new AssetEntry
                {
                    assetPath = assetPath,
                    fileName = Path.GetFileName(assetPath),
                    extension = ext,
                    selected = true,
                    cachedAsset = null,
                    previewNewName = "",
                    hasConflict = false,
                    conflictReason = ""
                });
            }
        }

        // ---- 添加直接拖入的散文件 ----
        foreach (string filePath in _loosFilePaths)
        {
            if (addedPaths.Contains(filePath)) continue;
            addedPaths.Add(filePath);

            string ext = Path.GetExtension(filePath).ToLower();
            if (ext == ".meta") continue;

            _assetEntries.Add(new AssetEntry
            {
                assetPath = filePath,
                fileName = Path.GetFileName(filePath),
                extension = ext,
                selected = true,
                cachedAsset = null,
                previewNewName = "",
                hasConflict = false,
                conflictReason = ""
            });
        }

        // 按文件名排序
        _assetEntries.Sort((a, b) => string.Compare(a.fileName, b.fileName, System.StringComparison.OrdinalIgnoreCase));

        UpdatePreviewNames();
        CheckConflicts();
        Repaint();
    }

    // ==================================================================================
    //  更新预览名称
    // ==================================================================================
    private void UpdatePreviewNames()
    {
        if (string.IsNullOrEmpty(_renamePrefix))
        {
            foreach (var entry in _assetEntries)
                entry.previewNewName = "";
            return;
        }

        // 统计选中数量，决定序号位数
        int selectedCount = _assetEntries.Count(e => e.selected);
        string format = selectedCount > 99 ? "D3" : "D2";

        int index = 1;
        foreach (var entry in _assetEntries)
        {
            if (entry.selected)
            {
                entry.previewNewName = $"{_renamePrefix}_{index.ToString(format)}{entry.extension}";
                index++;
            }
            else
            {
                entry.previewNewName = "";
            }
        }
    }

    // ==================================================================================
    //  冲突检测
    //  按资产类别检查：同类别内（如贴图 tga/png/jpg）去掉扩展名后同名即为冲突
    // ==================================================================================
    private void CheckConflicts()
    {
        _conflictCount = 0;

        // 先清除所有冲突标记
        foreach (var entry in _assetEntries)
        {
            entry.hasConflict = false;
            entry.conflictReason = "";
        }

        if (string.IsNullOrEmpty(_renamePrefix)) return;

        // ---- 构建每个目标文件夹中实际存在的文件信息（从磁盘读取） ----
        // key: 文件夹路径, value: 该文件夹下所有文件的 (去扩展名小写, 类别) 集合
        var existingFilesPerFolder = new Dictionary<string, List<System.Tuple<string, string>>>();

        // 收集所有涉及的文件夹
        var allFolders = new HashSet<string>(_folderPaths);
        foreach (var entry in _assetEntries)
        {
            string folder = Path.GetDirectoryName(entry.assetPath).Replace("\\", "/");
            allFolders.Add(folder);
        }

        foreach (string folderPath in allFolders)
        {
            if (existingFilesPerFolder.ContainsKey(folderPath)) continue;

            var fileList = new List<System.Tuple<string, string>>();
            string fullFolderPath = Path.GetFullPath(folderPath);
            if (Directory.Exists(fullFolderPath))
            {
                foreach (string filePath in Directory.GetFiles(fullFolderPath))
                {
                    string fn = Path.GetFileName(filePath);
                    string ext = Path.GetExtension(fn).ToLower();
                    if (ext == ".meta") continue;
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fn).ToLower();
                    string category = GetAssetCategory(ext);
                    fileList.Add(System.Tuple.Create(nameWithoutExt, category));
                }
            }
            existingFilesPerFolder[folderPath] = fileList;
        }

        // ---- 收集所有选中项的信息 ----
        // 选中项的原文件 (去扩展名小写, 类别) 集合（这些文件会被改名，改名后原名就空出来了）
        var selectedOriginalKeys = new HashSet<string>();
        foreach (var entry in _assetEntries)
        {
            if (entry.selected)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(entry.fileName).ToLower();
                string cat = GetAssetCategory(entry.extension);
                selectedOriginalKeys.Add($"{nameNoExt}|{cat}");
            }
        }

        // 选中项的目标 (去扩展名小写, 类别) 映射（用于检测选中项之间的目标名重复）
        // key: "目标名(小写)|类别", value: 对应的 entry 列表
        var targetCategoryMap = new Dictionary<string, List<AssetEntry>>();
        foreach (var entry in _assetEntries)
        {
            if (!entry.selected || string.IsNullOrEmpty(entry.previewNewName)) continue;

            string targetNameNoExt = Path.GetFileNameWithoutExtension(entry.previewNewName).ToLower();
            string category = GetAssetCategory(entry.extension);
            string key = $"{targetNameNoExt}|{category}";

            if (!targetCategoryMap.ContainsKey(key))
                targetCategoryMap[key] = new List<AssetEntry>();
            targetCategoryMap[key].Add(entry);
        }

        // ---- 检测1：选中项之间同类别目标名重复 ----
        foreach (var kvp in targetCategoryMap)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var entry in kvp.Value)
                {
                    entry.hasConflict = true;
                    string cat = kvp.Key.Split('|')[1];
                    entry.conflictReason = $"同类别({cat})内与其他选中资产目标名重复";
                    _conflictCount++;
                }
            }
        }

        // ---- 检测2：目标名与目录中实际存在的同类别文件冲突 ----
        foreach (var entry in _assetEntries)
        {
            if (!entry.selected || string.IsNullOrEmpty(entry.previewNewName)) continue;
            if (entry.hasConflict) continue;

            string targetNameNoExt = Path.GetFileNameWithoutExtension(entry.previewNewName).ToLower();
            string category = GetAssetCategory(entry.extension);
            string targetKey = $"{targetNameNoExt}|{category}";

            // 目标名等于自己的原名（同类别），不算冲突
            string origNameNoExt = Path.GetFileNameWithoutExtension(entry.fileName).ToLower();
            if (targetNameNoExt == origNameNoExt) continue;

            // 获取该资产所在文件夹
            string folder = Path.GetDirectoryName(entry.assetPath).Replace("\\", "/");
            if (!existingFilesPerFolder.ContainsKey(folder)) continue;

            var existingFiles = existingFilesPerFolder[folder];

            // 检查磁盘上是否存在同类别同名文件
            foreach (var existing in existingFiles)
            {
                if (existing.Item1 == targetNameNoExt && existing.Item2 == category)
                {
                    // 但如果这个已存在的文件也是选中项（会被改名腾出位置），则不算冲突
                    string existingKey = $"{existing.Item1}|{existing.Item2}";
                    if (selectedOriginalKeys.Contains(existingKey)) continue;

                    entry.hasConflict = true;
                    entry.conflictReason = $"目录中已存在同类别({category})文件: {targetNameNoExt}.*";
                    _conflictCount++;
                    break;
                }
            }
        }

        // ---- 检测3：选中项的目标名与其他选中项的原名交叉冲突（同类别） ----
        var selectedOriginalCategoryMap = new Dictionary<string, AssetEntry>();
        foreach (var entry in _assetEntries)
        {
            if (entry.selected)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(entry.fileName).ToLower();
                string cat = GetAssetCategory(entry.extension);
                string key = $"{nameNoExt}|{cat}";
                selectedOriginalCategoryMap[key] = entry;
            }
        }

        foreach (var entry in _assetEntries)
        {
            if (!entry.selected || string.IsNullOrEmpty(entry.previewNewName)) continue;
            if (entry.hasConflict) continue;

            string targetNameNoExt = Path.GetFileNameWithoutExtension(entry.previewNewName).ToLower();
            string category = GetAssetCategory(entry.extension);
            string targetKey = $"{targetNameNoExt}|{category}";

            string origNameNoExt = Path.GetFileNameWithoutExtension(entry.fileName).ToLower();
            if (targetNameNoExt == origNameNoExt) continue;

            if (selectedOriginalCategoryMap.ContainsKey(targetKey))
            {
                var otherEntry = selectedOriginalCategoryMap[targetKey];
                if (otherEntry != entry)
                {
                    string otherOrigNameNoExt = Path.GetFileNameWithoutExtension(otherEntry.fileName).ToLower();
                    string otherTargetNameNoExt = string.IsNullOrEmpty(otherEntry.previewNewName)
                        ? otherOrigNameNoExt
                        : Path.GetFileNameWithoutExtension(otherEntry.previewNewName).ToLower();
                    if (otherTargetNameNoExt != otherOrigNameNoExt)
                    {
                        entry.hasConflict = true;
                        entry.conflictReason = $"目标名与 {otherEntry.fileName} 的原名交叉冲突({category})";
                        _conflictCount++;
                    }
                }
            }
        }
    }

    // ==================================================================================
    //  执行重命名
    // ==================================================================================
    private void ExecuteRename()
    {
        _logMessages.Clear();
        _hasRenamed = true;

        var selectedEntries = _assetEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0)
        {
            _logMessages.Add("没有选中任何资产。");
            return;
        }

        string format = selectedEntries.Count > 99 ? "D3" : "D2";
        int successCount = 0;
        int failCount = 0;
        int index = 1;

        _logMessages.Add($"── 开始重命名（前缀: {_renamePrefix}，共 {selectedEntries.Count} 个） ──");

        foreach (var entry in selectedEntries)
        {
            string newName = $"{_renamePrefix}_{index.ToString(format)}";
            string result = AssetDatabase.RenameAsset(entry.assetPath, newName);

            if (string.IsNullOrEmpty(result))
            {
                // 重命名成功
                string newPath = Path.GetDirectoryName(entry.assetPath).Replace("\\", "/") + "/" + newName + entry.extension;
                _logMessages.Add($"  ✓ {entry.fileName} → {newName}{entry.extension}");

                // 更新 entry 信息
                entry.assetPath = newPath;
                entry.fileName = newName + entry.extension;
                entry.cachedAsset = null;
                successCount++;
            }
            else
            {
                // 重命名失败
                _logMessages.Add($"  ✕ {entry.fileName} 重命名失败: {result}");
                failCount++;
            }
            index++;
        }

        AssetDatabase.Refresh();

        _logMessages.Add("");
        _logMessages.Add($"══ 重命名完成 ══");
        _logMessages.Add($"  成功: {successCount} 个，失败: {failCount} 个");

        Debug.Log($"[批量重命名工具] 批量重命名完成 —— 成功: {successCount}, 失败: {failCount}，前缀: {_renamePrefix}");

        // 重新扫描以更新列表
        ScanAssets();
        Repaint();
    }
}
