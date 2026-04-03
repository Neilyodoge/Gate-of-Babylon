using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通道重映射工具：重新调整贴图 RGBA 各通道的位置，并支持各通道的数值反转 (1 - x)。
/// 输出贴图以 _ChanFix 为后缀保存在原始贴图同目录下。
///
/// 使用方法：
/// 1. 在 Project 视图中选择贴图或文件夹
/// 2. 打开 Tools > ArtTools > Channel Remapper
/// 3. 可使用后缀筛选来过滤文件
/// 4. 在文件列表中勾选需要处理的贴图
/// 5. 为输出的 R/G/B/A 各通道指定来源通道
/// 6. 点击"重映射并保存"
/// </summary>
public class ChannelRemapper : EditorWindow
{
    private const string OutputSuffix = "_ChanFix";
    private const string TemplatePrefsKey = "ChannelRemapper_Templates";
    private const string LastTemplatePrefsKey = "ChannelRemapper_LastTemplate";
    private const string DefaultTemplateName = "原始 (RGBA)";

    /// <summary>
    /// 可选的来源通道
    /// </summary>
    private enum SourceChannel
    {
        R = 0,
        G = 1,
        B = 2,
        A = 3,
        White = 4,  // 常量 1
        Black = 5,  // 常量 0
    }

    // 输出 R 通道的来源与反转
    private SourceChannel outR_Source = SourceChannel.R;
    private bool outR_Invert = false;

    // 输出 G 通道的来源与反转
    private SourceChannel outG_Source = SourceChannel.G;
    private bool outG_Invert = false;

    // 输出 B 通道的来源与反转
    private SourceChannel outB_Source = SourceChannel.B;
    private bool outB_Invert = false;

    // 输出 A 通道的来源与反转
    private SourceChannel outA_Source = SourceChannel.A;
    private bool outA_Invert = false;

    // ===== 模板系统 =====
    private string newTemplateName = "";
    private int selectedTemplateIndex = -1;
    private List<ChannelTemplate> templates = new List<ChannelTemplate>();
    private string[] templateDisplayNames = new string[0];

    // ===== 文件列表与筛选 =====
    private string suffixFilter = "";                       // 后缀筛选，如 "_ABC"
    private List<string> collectedPaths = new List<string>();       // 扫描到的所有贴图路径
    private List<bool> collectedChecked = new List<bool>();         // 每个文件的勾选状态
    private Vector2 fileListScrollPos;

    /// <summary>
    /// 通道映射模板数据
    /// </summary>
    [System.Serializable]
    private class ChannelTemplate
    {
        public string name;
        public int rSource;
        public bool rInvert;
        public int gSource;
        public bool gInvert;
        public int bSource;
        public bool bInvert;
        public int aSource;
        public bool aInvert;
    }

    [System.Serializable]
    private class TemplateList
    {
        public List<ChannelTemplate> items = new List<ChannelTemplate>();
    }

    [MenuItem("nTools/TA工具/通道重映射", false, 150)]
    public static void ShowWindow()
    {
        var win = GetWindow<ChannelRemapper>("通道重映射工具");
        win.minSize = new Vector2(420, 600);
    }

    private void OnEnable()
    {
        LoadTemplatesFromPrefs();
        EnsureDefaultTemplate();

        // 自动恢复上次使用的模板
        string lastTemplate = EditorPrefs.GetString(LastTemplatePrefsKey, "");
        if (!string.IsNullOrEmpty(lastTemplate))
        {
            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i].name == lastTemplate)
                {
                    selectedTemplateIndex = i;
                    ApplyTemplate(templates[i]);
                    break;
                }
            }
        }

        // 初始扫描当前选中
        RefreshFileList();
    }

    private void OnSelectionChange()
    {
        RefreshFileList();
        Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Label("通道重映射工具", EditorStyles.boldLabel);
        GUILayout.Label("在 Project 视图中选择贴图或文件夹，为输出的每个通道指定来源。", EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);
        EditorGUILayout.HelpBox("当前PBRToon使用的是 Metallic / Smoothness / AO / Highlight", MessageType.Info);
        GUILayout.Space(6);

        // ===== 通道映射设置 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("通道映射设置", EditorStyles.boldLabel);
        GUILayout.Space(4);

        DrawChannelRow("输出 R ←", ref outR_Source, ref outR_Invert);
        DrawChannelRow("输出 G ←", ref outG_Source, ref outG_Invert);
        DrawChannelRow("输出 B ←", ref outB_Source, ref outB_Invert);
        DrawChannelRow("输出 A ←", ref outA_Source, ref outA_Invert);

        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 快捷预设 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("快捷预设", EditorStyles.boldLabel);
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("还原默认 (RGBA→RGBA)"))
        {
            outR_Source = SourceChannel.R; outR_Invert = false;
            outG_Source = SourceChannel.G; outG_Invert = false;
            outB_Source = SourceChannel.B; outB_Invert = false;
            outA_Source = SourceChannel.A; outA_Invert = false;
        }
        if (GUILayout.Button("全部反转"))
        {
            outR_Invert = !outR_Invert;
            outG_Invert = !outG_Invert;
            outB_Invert = !outB_Invert;
            outA_Invert = !outA_Invert;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 自定义模板 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("自定义模板", EditorStyles.boldLabel);
        GUILayout.Space(2);

        // 加载模板（下拉框）
        if (templates.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("选择模板", GUILayout.Width(60));
            int newIndex = EditorGUILayout.Popup(selectedTemplateIndex, templateDisplayNames);
            if (newIndex != selectedTemplateIndex && newIndex >= 0 && newIndex < templates.Count)
            {
                selectedTemplateIndex = newIndex;
                ApplyTemplate(templates[selectedTemplateIndex]);
                EditorPrefs.SetString(LastTemplatePrefsKey, templates[selectedTemplateIndex].name);
            }

            // 只有非默认模板才能删除
            bool isDefault = selectedTemplateIndex >= 0 && selectedTemplateIndex < templates.Count
                             && templates[selectedTemplateIndex].name == DefaultTemplateName;
            GUI.enabled = selectedTemplateIndex >= 0 && selectedTemplateIndex < templates.Count && !isDefault;
            if (GUILayout.Button("删除", GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("删除模板",
                    $"确定删除模板 \"{templates[selectedTemplateIndex].name}\" 吗？", "删除", "取消"))
                {
                    templates.RemoveAt(selectedTemplateIndex);
                    selectedTemplateIndex = Mathf.Min(selectedTemplateIndex, templates.Count - 1);
                    SaveTemplatesToPrefs();
                    RefreshTemplateNames();
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(2);

        // 保存新模板
        EditorGUILayout.BeginHorizontal();
        newTemplateName = EditorGUILayout.TextField("保存为", newTemplateName);
        GUI.enabled = !string.IsNullOrEmpty(newTemplateName) && newTemplateName != DefaultTemplateName;
        if (GUILayout.Button("保存", GUILayout.Width(50)))
        {
            SaveCurrentAsTemplate(newTemplateName);
            newTemplateName = "";
            GUI.FocusControl(null);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 文件筛选与扫描 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("文件筛选", EditorStyles.boldLabel);
        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("后缀筛选", GUILayout.Width(60));
        string newFilter = EditorGUILayout.TextField(suffixFilter);
        if (newFilter != suffixFilter)
        {
            suffixFilter = newFilter;
            RefreshFileList();
        }
        if (GUILayout.Button("刷新", GUILayout.Width(50)))
        {
            RefreshFileList();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "在 Project 中选择贴图或文件夹，工具会自动扫描。\n" +
            "后缀筛选示例：输入 _ABC 则只显示文件名含 _ABC 的贴图。",
            MessageType.Info);

        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        // ===== 文件列表（带勾选） =====
        int checkedCount = 0;
        for (int i = 0; i < collectedChecked.Count; i++)
        {
            if (collectedChecked[i]) checkedCount++;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"文件列表 ({checkedCount}/{collectedPaths.Count})", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("全选", GUILayout.Width(40)))
        {
            for (int i = 0; i < collectedChecked.Count; i++) collectedChecked[i] = true;
        }
        if (GUILayout.Button("全不选", GUILayout.Width(50)))
        {
            for (int i = 0; i < collectedChecked.Count; i++) collectedChecked[i] = false;
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(2);

        if (collectedPaths.Count > 0)
        {
            fileListScrollPos = EditorGUILayout.BeginScrollView(fileListScrollPos, GUILayout.MaxHeight(200));
            for (int i = 0; i < collectedPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                collectedChecked[i] = EditorGUILayout.ToggleLeft(
                    Path.GetFileName(collectedPaths[i]),
                    collectedChecked[i]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.LabelField("未找到匹配的贴图文件", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);

        // ===== 执行按钮 =====
        GUI.enabled = checkedCount > 0;
        if (GUILayout.Button("重映射并保存", GUILayout.Height(32)))
        {
            ProcessCheckedFiles();
        }
        GUI.enabled = true;
    }

    /// <summary>
    /// 绘制单个通道的映射行
    /// </summary>
    private void DrawChannelRow(string label, ref SourceChannel source, ref bool invert)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(70));
        source = (SourceChannel)EditorGUILayout.EnumPopup(source, GUILayout.Width(80));
        invert = EditorGUILayout.ToggleLeft("反转 (1-x)", invert, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
    }

    // ===== 文件扫描与筛选 =====

    /// <summary>
    /// 根据当前 Selection 和后缀筛选刷新文件列表
    /// </summary>
    private void RefreshFileList()
    {
        var paths = new HashSet<string>();
        Object[] selected = Selection.objects;

        if (selected != null)
        {
            foreach (var obj in selected)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;

                // 如果是文件夹，递归扫描
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    CollectTexturesInFolder(assetPath, paths);
                }
                else
                {
                    // 单个贴图
                    if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) != null)
                        paths.Add(assetPath);
                }
            }
        }

        // 应用后缀筛选
        var filtered = new List<string>();
        string filter = suffixFilter.Trim();
        foreach (var p in paths)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(p);
                if (!nameNoExt.Contains(filter))
                    continue;
            }
            filtered.Add(p);
        }

        filtered.Sort();

        // 保留之前的勾选状态
        var oldCheckedMap = new Dictionary<string, bool>();
        for (int i = 0; i < collectedPaths.Count; i++)
            oldCheckedMap[collectedPaths[i]] = collectedChecked[i];

        collectedPaths = filtered;
        collectedChecked = new List<bool>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            // 新文件默认勾选，已有文件保留之前状态
            if (oldCheckedMap.TryGetValue(filtered[i], out bool wasChecked))
                collectedChecked.Add(wasChecked);
            else
                collectedChecked.Add(true);
        }
    }

    /// <summary>
    /// 递归收集文件夹下所有贴图
    /// </summary>
    private void CollectTexturesInFolder(string folderPath, HashSet<string> results)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            results.Add(path);
        }
    }

    /// <summary>
    /// 处理文件列表中勾选的所有贴图
    /// </summary>
    private void ProcessCheckedFiles()
    {
        int processedCount = 0;
        for (int i = 0; i < collectedPaths.Count; i++)
        {
            if (!collectedChecked[i]) continue;

            string path = collectedPaths[i];
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            RemapTexture(path, tex);
            processedCount++;
        }

        AssetDatabase.Refresh();

        if (processedCount > 0)
            Debug.Log($"[通道重映射] 完成，共处理 {processedCount} 张贴图。");
        else
            EditorUtility.DisplayDialog("通道重映射", "未找到有效的贴图资源。", "确定");
    }

    /// <summary>
    /// 对单张贴图执行通道重映射
    /// </summary>
    private void RemapTexture(string assetPath, Texture2D original)
    {
        // 确保纹理可读
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        bool restoreReadable = false;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            restoreReadable = true;
        }

        int w = original.width;
        int h = original.height;
        Color[] srcPixels = original.GetPixels();

        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] outPixels = new Color[w * h];

        for (int i = 0; i < srcPixels.Length; i++)
        {
            Color src = srcPixels[i];

            float r = SampleChannel(src, outR_Source);
            float g = SampleChannel(src, outG_Source);
            float b = SampleChannel(src, outB_Source);
            float a = SampleChannel(src, outA_Source);

            if (outR_Invert) r = 1f - r;
            if (outG_Invert) g = 1f - g;
            if (outB_Invert) b = 1f - b;
            if (outA_Invert) a = 1f - a;

            outPixels[i] = new Color(r, g, b, a);
        }

        outTex.SetPixels(outPixels);
        outTex.Apply();

        // 根据原始格式保存
        string fullPath = Path.Combine(
            Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
            assetPath);
        string directory = Path.GetDirectoryName(fullPath);
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string ext = Path.GetExtension(assetPath).ToLower();

        byte[] bytes;
        string outputExt;
        if (ext == ".tga")
        {
            bytes = outTex.EncodeToTGA();
            outputExt = ".tga";
        }
        else if (ext == ".exr")
        {
            bytes = outTex.EncodeToEXR();
            outputExt = ".exr";
        }
        else
        {
            bytes = outTex.EncodeToPNG();
            outputExt = ".png";
        }

        string newFullPath = Path.Combine(directory, filename + OutputSuffix + outputExt);
        File.WriteAllBytes(newFullPath, bytes);

        // 恢复原始纹理的可读性设置
        if (restoreReadable && importer != null)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        // 导入新资源
        string relativeOutputName = filename + OutputSuffix + outputExt;
        string relativeNewPath = Path.GetDirectoryName(assetPath) + "/" + relativeOutputName;
        AssetDatabase.ImportAsset(relativeNewPath);

        Debug.Log($"[通道重映射] '{assetPath}' → '{relativeNewPath}'  " +
                  $"R←{outR_Source}{(outR_Invert ? "(反)" : "")} " +
                  $"G←{outG_Source}{(outG_Invert ? "(反)" : "")} " +
                  $"B←{outB_Source}{(outB_Invert ? "(反)" : "")} " +
                  $"A←{outA_Source}{(outA_Invert ? "(反)" : "")}");

        DestroyImmediate(outTex);
    }

    /// <summary>
    /// 从源像素中采样指定通道的值
    /// </summary>
    private static float SampleChannel(Color src, SourceChannel channel)
    {
        switch (channel)
        {
            case SourceChannel.R: return src.r;
            case SourceChannel.G: return src.g;
            case SourceChannel.B: return src.b;
            case SourceChannel.A: return src.a;
            case SourceChannel.White: return 1f;
            case SourceChannel.Black: return 0f;
            default: return 0f;
        }
    }

    // ===== 模板持久化方法 =====

    /// <summary>
    /// 确保默认的"原始 (RGBA)"模板存在
    /// </summary>
    private void EnsureDefaultTemplate()
    {
        bool hasDefault = templates.Any(t => t.name == DefaultTemplateName);
        if (!hasDefault)
        {
            var defaultTemplate = new ChannelTemplate
            {
                name = DefaultTemplateName,
                rSource = (int)SourceChannel.R, rInvert = false,
                gSource = (int)SourceChannel.G, gInvert = false,
                bSource = (int)SourceChannel.B, bInvert = false,
                aSource = (int)SourceChannel.A, aInvert = false,
            };
            templates.Insert(0, defaultTemplate);
            SaveTemplatesToPrefs();
            RefreshTemplateNames();

            // 如果没有选中模板，默认选中原始模板
            if (selectedTemplateIndex < 0)
                selectedTemplateIndex = 0;
        }
    }

    /// <summary>
    /// 将当前通道映射配置保存为模板
    /// </summary>
    private void SaveCurrentAsTemplate(string name)
    {
        // 不允许覆盖默认模板
        if (name == DefaultTemplateName) return;

        // 如果同名模板已存在，覆盖它
        int existingIndex = templates.FindIndex(t => t.name == name);
        var template = new ChannelTemplate
        {
            name = name,
            rSource = (int)outR_Source, rInvert = outR_Invert,
            gSource = (int)outG_Source, gInvert = outG_Invert,
            bSource = (int)outB_Source, bInvert = outB_Invert,
            aSource = (int)outA_Source, aInvert = outA_Invert,
        };

        if (existingIndex >= 0)
        {
            templates[existingIndex] = template;
            selectedTemplateIndex = existingIndex;
        }
        else
        {
            templates.Add(template);
            selectedTemplateIndex = templates.Count - 1;
        }

        SaveTemplatesToPrefs();
        RefreshTemplateNames();
        EditorPrefs.SetString(LastTemplatePrefsKey, name);
        Debug.Log($"[通道重映射] 模板 \"{name}\" 已保存。");
    }

    /// <summary>
    /// 应用模板到当前通道映射配置
    /// </summary>
    private void ApplyTemplate(ChannelTemplate template)
    {
        outR_Source = (SourceChannel)template.rSource; outR_Invert = template.rInvert;
        outG_Source = (SourceChannel)template.gSource; outG_Invert = template.gInvert;
        outB_Source = (SourceChannel)template.bSource; outB_Invert = template.bInvert;
        outA_Source = (SourceChannel)template.aSource; outA_Invert = template.aInvert;
    }

    /// <summary>
    /// 从 EditorPrefs 加载模板列表
    /// </summary>
    private void LoadTemplatesFromPrefs()
    {
        string json = EditorPrefs.GetString(TemplatePrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonUtility.FromJson<TemplateList>(json);
                if (list != null && list.items != null)
                    templates = list.items;
            }
            catch (System.Exception)
            {
                templates = new List<ChannelTemplate>();
            }
        }
        RefreshTemplateNames();
    }

    /// <summary>
    /// 将模板列表保存到 EditorPrefs
    /// </summary>
    private void SaveTemplatesToPrefs()
    {
        var list = new TemplateList { items = templates };
        string json = JsonUtility.ToJson(list);
        EditorPrefs.SetString(TemplatePrefsKey, json);
    }

    /// <summary>
    /// 刷新模板显示名称数组（用于 Popup 下拉框）
    /// </summary>
    private void RefreshTemplateNames()
    {
        templateDisplayNames = new string[templates.Count];
        for (int i = 0; i < templates.Count; i++)
        {
            templateDisplayNames[i] = templates[i].name;
        }
    }
}
