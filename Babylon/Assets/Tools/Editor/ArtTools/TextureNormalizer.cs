using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 贴图规范化工具：根据贴图文件名后缀自动设置 sRGB 和 Texture Type。
///
/// 默认规则：
/// - 以 D 结尾的贴图 → 勾选 sRGB（Diffuse / BaseColor 等颜色贴图）
/// - 以 N 结尾的贴图 → Texture Type 设为 Normal Map，关闭 sRGB
/// - 其他贴图 → 关闭 sRGB（Mask、AO、金属度等线性数据贴图）
///
/// 使用方法：
/// 1. 在 Project 视图中选择一个或多个文件夹
/// 2. 打开 Tools > ArtTools > 贴图规范化工具
/// 3. 根据需要修改后缀匹配规则（可填写多个后缀，用逗号分隔）
/// 4. 点击"执行规范化"
/// </summary>
public class TextureNormalizer : EditorWindow
{
    /// <summary>
    /// 以这些后缀结尾的贴图文件名 → 勾选 sRGB（颜色贴图）
    /// 多个后缀用英文逗号分隔，匹配时忽略大小写
    /// </summary>
    private string sRGBSuffixes = "D";

    /// <summary>
    /// 以这些后缀结尾的贴图文件名 → Texture Type 改为 Normal Map
    /// 多个后缀用英文逗号分隔，匹配时忽略大小写
    /// </summary>
    private string normalMapSuffixes = "N";

    /// <summary>
    /// 是否递归处理子文件夹
    /// </summary>
    private bool recursive = true;

    /// <summary>
    /// 是否在执行前预览将要修改的内容
    /// </summary>
    private bool previewBeforeApply = true;

    /// <summary>
    /// 滚动位置（用于预览列表）
    /// </summary>
    private Vector2 scrollPos;

    /// <summary>
    /// 预览数据
    /// </summary>
    private List<PreviewEntry> previewEntries = new List<PreviewEntry>();
    private bool hasPreview = false;

    private struct PreviewEntry
    {
        public string assetPath;
        public string fileName;
        public bool willSetSRGB;
        public bool willSetNormalMap;
        public bool currentSRGB;
        public bool currentIsNormalMap;
        public bool needsChange;
    }

    [MenuItem("nTools/美术工具/贴图规范化", false, 52)]
    public static void ShowWindow()
    {
        var win = GetWindow<TextureNormalizer>("贴图规范化工具");
        win.minSize = new Vector2(420, 520);
    }

    private void OnEnable()
    {
        // 初始时自动刷新预览
        AutoRefreshPreview();
    }

    private void OnSelectionChange()
    {
        // 选中变化时自动刷新预览
        AutoRefreshPreview();
        Repaint();
    }

    /// <summary>
    /// 自动刷新预览（选中文件夹时自动生成预览数据）
    /// </summary>
    private void AutoRefreshPreview()
    {
        List<string> selectedFolders = GetSelectedFolders();
        if (selectedFolders.Count > 0)
        {
            GeneratePreview(selectedFolders);
        }
        else
        {
            previewEntries.Clear();
            hasPreview = false;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("贴图规范化工具", EditorStyles.boldLabel);
        GUILayout.Label("选择 Project 视图中的文件夹，根据文件名后缀自动设置贴图导入参数。", EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "规则说明：\n" +
            "• 文件名（不含扩展名）以指定后缀结尾 → 匹配对应规则\n" +
            "• sRGB 后缀匹配 → 勾选 sRGB（颜色空间贴图，如 Diffuse）\n" +
            "• Normal Map 后缀匹配 → Texture Type 设为 Normal Map，关闭 sRGB\n" +
            "• 两者都不匹配 → 关闭 sRGB（线性数据贴图，如 Mask、AO）\n" +
            "• 若同时匹配 sRGB 和 Normal Map 后缀，Normal Map 优先",
            MessageType.Info);
        GUILayout.Space(6);

        // ===== 后缀规则设置 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("后缀匹配规则（多个后缀用英文逗号分隔，忽略大小写）", EditorStyles.boldLabel);
        GUILayout.Space(4);

        sRGBSuffixes = EditorGUILayout.TextField("sRGB 后缀（颜色贴图）", sRGBSuffixes);
        normalMapSuffixes = EditorGUILayout.TextField("Normal Map 后缀", normalMapSuffixes);

        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 选项 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("选项", EditorStyles.boldLabel);
        GUILayout.Space(2);
        recursive = EditorGUILayout.Toggle("递归处理子文件夹", recursive);
        previewBeforeApply = EditorGUILayout.Toggle("执行前预览", previewBeforeApply);
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 当前选中信息 =====
        List<string> selectedFolders = GetSelectedFolders();
        if (selectedFolders.Count > 0)
        {
            EditorGUILayout.HelpBox($"当前选中 {selectedFolders.Count} 个文件夹：\n" +
                string.Join("\n", selectedFolders), MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("请在 Project 视图中选择一个或多个文件夹。", MessageType.Warning);
        }
        GUILayout.Space(4);

        // ===== 执行按钮 =====
        GUI.enabled = selectedFolders.Count > 0;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新预览", GUILayout.Height(28)))
        {
            AutoRefreshPreview();
        }
        if (GUILayout.Button("执行规范化", GUILayout.Height(28)))
        {
            if (previewBeforeApply)
            {
                GeneratePreview(selectedFolders);
                int changeCount = 0;
                foreach (var e in previewEntries)
                    if (e.needsChange) changeCount++;

                if (changeCount == 0)
                {
                    EditorUtility.DisplayDialog("贴图规范化", "所有贴图已符合规范，无需修改。", "确定");
                }
                else if (EditorUtility.DisplayDialog("贴图规范化",
                    $"即将修改 {changeCount} 张贴图的导入设置，是否继续？", "执行", "取消"))
                {
                    ApplyChanges();
                }
            }
            else
            {
                ApplyToFolders(selectedFolders);
            }
        }
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;
        GUILayout.Space(4);

        // ===== 预览列表 =====
        if (hasPreview && previewEntries.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label($"预览结果（共 {previewEntries.Count} 张贴图）", EditorStyles.boldLabel);
            GUILayout.Space(2);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(150));
            foreach (var entry in previewEntries)
            {
                if (!entry.needsChange)
                    continue;

                EditorGUILayout.BeginHorizontal();

                // 图标指示修改类型
                string changeDesc = "";
                if (entry.willSetNormalMap && !entry.currentIsNormalMap)
                    changeDesc += "[→NormalMap] ";
                if (entry.willSetSRGB != entry.currentSRGB)
                    changeDesc += entry.willSetSRGB ? "[sRGB ✓] " : "[sRGB ✗] ";

                GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
                style.richText = true;
                EditorGUILayout.LabelField(
                    $"<color=#FFA500>{changeDesc}</color>{entry.assetPath}",
                    style);

                EditorGUILayout.EndHorizontal();
            }

            // 显示无需修改的统计
            int noChangeCount = 0;
            foreach (var e in previewEntries)
                if (!e.needsChange) noChangeCount++;
            if (noChangeCount > 0)
            {
                GUILayout.Space(2);
                EditorGUILayout.LabelField($"（另有 {noChangeCount} 张贴图已符合规范，无需修改）",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 获取 Project 视图中选中的文件夹路径
    /// </summary>
    private List<string> GetSelectedFolders()
    {
        List<string> folders = new List<string>();
        Object[] selected = Selection.objects;
        if (selected == null) return folders;

        foreach (var obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                folders.Add(path);
            }
        }
        return folders;
    }

    /// <summary>
    /// 解析后缀字符串为数组
    /// </summary>
    private string[] ParseSuffixes(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new string[0];

        string[] parts = input.Split(',');
        List<string> result = new List<string>();
        foreach (string p in parts)
        {
            string trimmed = p.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                result.Add(trimmed);
        }
        return result.ToArray();
    }

    /// <summary>
    /// 判断文件名（不含扩展名）是否以指定后缀结尾（忽略大小写）
    /// </summary>
    private bool MatchesSuffix(string fileNameWithoutExt, string[] suffixes)
    {
        foreach (string suffix in suffixes)
        {
            if (fileNameWithoutExt.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 收集文件夹下所有贴图的 asset path
    /// </summary>
    private List<string> CollectTexturePaths(List<string> folders)
    {
        List<string> texturePaths = new List<string>();
        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (string folder in folders)
        {
            // 转为绝对路径搜索
            string fullFolder = Path.Combine(
                Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
                folder);

            if (!Directory.Exists(fullFolder))
                continue;

            string[] files = Directory.GetFiles(fullFolder, "*.*", searchOption);
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                // 跳过 .meta 和非贴图格式
                if (ext == ".meta" || ext == ".cs" || ext == ".shader" || ext == ".hlsl" ||
                    ext == ".mat" || ext == ".asset" || ext == ".prefab" || ext == ".unity" ||
                    ext == ".md" || ext == ".txt" || ext == ".json" || ext == ".asmdef")
                    continue;

                // 常见贴图格式
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" ||
                    ext == ".bmp" || ext == ".psd" || ext == ".tif" || ext == ".tiff" ||
                    ext == ".exr" || ext == ".hdr")
                {
                    // 转回相对路径
                    string relativePath = file.Replace("\\", "/");
                    int assetsIndex = relativePath.IndexOf("Assets/");
                    if (assetsIndex >= 0)
                    {
                        string assetPath = relativePath.Substring(assetsIndex);
                        texturePaths.Add(assetPath);
                    }
                }
            }
        }

        return texturePaths;
    }

    /// <summary>
    /// 生成预览数据
    /// </summary>
    private void GeneratePreview(List<string> folders)
    {
        previewEntries.Clear();
        hasPreview = true;

        string[] srgbSuffix = ParseSuffixes(sRGBSuffixes);
        string[] normalSuffix = ParseSuffixes(normalMapSuffixes);
        List<string> texturePaths = CollectTexturePaths(folders);

        foreach (string assetPath in texturePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                continue;

            string fileNameNoExt = Path.GetFileNameWithoutExtension(assetPath);

            bool isNormalMap = MatchesSuffix(fileNameNoExt, normalSuffix);
            bool isSRGB = !isNormalMap && MatchesSuffix(fileNameNoExt, srgbSuffix);

            // 当前状态
            bool currentSRGB = importer.sRGBTexture;
            bool currentIsNormalMap = importer.textureType == TextureImporterType.NormalMap;

            // 目标状态
            bool targetSRGB = isSRGB;
            bool targetIsNormalMap = isNormalMap;

            bool needsChange = (targetSRGB != currentSRGB) ||
                               (targetIsNormalMap && !currentIsNormalMap) ||
                               (!targetIsNormalMap && currentIsNormalMap);

            PreviewEntry entry = new PreviewEntry
            {
                assetPath = assetPath,
                fileName = Path.GetFileName(assetPath),
                willSetSRGB = targetSRGB,
                willSetNormalMap = targetIsNormalMap,
                currentSRGB = currentSRGB,
                currentIsNormalMap = currentIsNormalMap,
                needsChange = needsChange
            };

            previewEntries.Add(entry);
        }

        Repaint();
    }

    /// <summary>
    /// 应用预览中的修改
    /// </summary>
    private void ApplyChanges()
    {
        int changeCount = 0;
        int total = previewEntries.Count;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < previewEntries.Count; i++)
            {
                PreviewEntry entry = previewEntries[i];
                if (!entry.needsChange)
                    continue;

                EditorUtility.DisplayProgressBar("贴图规范化",
                    $"处理中 ({i + 1}/{total}): {entry.fileName}", (float)i / total);

                TextureImporter importer = AssetImporter.GetAtPath(entry.assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                bool changed = false;

                // 设置 Texture Type
                if (entry.willSetNormalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                    Debug.Log($"[贴图规范化] '{entry.assetPath}' → Texture Type: Normal Map");
                }
                else if (!entry.willSetNormalMap && importer.textureType == TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                    Debug.Log($"[贴图规范化] '{entry.assetPath}' → Texture Type: Default");
                }

                // 设置 sRGB
                if (importer.sRGBTexture != entry.willSetSRGB)
                {
                    importer.sRGBTexture = entry.willSetSRGB;
                    changed = true;
                    Debug.Log($"[贴图规范化] '{entry.assetPath}' → sRGB: {entry.willSetSRGB}");
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    changeCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[贴图规范化] 完成，共修改 {changeCount} 张贴图。");
        EditorUtility.DisplayDialog("贴图规范化", $"完成！共修改 {changeCount} 张贴图。", "确定");
    }

    /// <summary>
    /// 直接对文件夹执行（不预览）
    /// </summary>
    private void ApplyToFolders(List<string> folders)
    {
        string[] srgbSuffix = ParseSuffixes(sRGBSuffixes);
        string[] normalSuffix = ParseSuffixes(normalMapSuffixes);
        List<string> texturePaths = CollectTexturePaths(folders);

        int changeCount = 0;
        int total = texturePaths.Count;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < texturePaths.Count; i++)
            {
                string assetPath = texturePaths[i];
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                string fileNameNoExt = Path.GetFileNameWithoutExtension(assetPath);

                EditorUtility.DisplayProgressBar("贴图规范化",
                    $"处理中 ({i + 1}/{total}): {Path.GetFileName(assetPath)}", (float)i / total);

                bool isNormalMap = MatchesSuffix(fileNameNoExt, normalSuffix);
                bool isSRGB = !isNormalMap && MatchesSuffix(fileNameNoExt, srgbSuffix);
                bool changed = false;

                // 设置 Texture Type
                if (isNormalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
                else if (!isNormalMap && importer.textureType == TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }

                // 设置 sRGB
                if (importer.sRGBTexture != isSRGB)
                {
                    importer.sRGBTexture = isSRGB;
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    changeCount++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        Debug.Log($"[贴图规范化] 完成，共修改 {changeCount} 张贴图。");
        EditorUtility.DisplayDialog("贴图规范化", $"完成！共修改 {changeCount} 张贴图。", "确定");
    }
}
