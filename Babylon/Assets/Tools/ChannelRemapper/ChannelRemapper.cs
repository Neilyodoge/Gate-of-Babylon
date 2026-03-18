using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通道重映射工具：重新调整贴图 RGBA 各通道的位置，并支持各通道的数值反转 (1 - x)。
/// 输出贴图以 _ChanFix 为后缀保存在原始贴图同目录下。
///
/// 使用方法：
/// 1. 在 Project 视图中选择一张或多张贴图
/// 2. 打开 Tools > ArtTools > Channel Remapper
/// 3. 为输出的 R/G/B/A 各通道指定来源通道
/// 4. 勾选"反转"可对该通道执行 1 - x 操作
/// 5. 点击"重映射并保存"
/// </summary>
public class ChannelRemapper : EditorWindow
{
    private const string OutputSuffix = "_ChanFix";

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

    [MenuItem("Tools/ArtTools/通道重映射工具")]
    public static void ShowWindow()
    {
        var win = GetWindow<ChannelRemapper>("通道重映射工具");
        win.minSize = new Vector2(360, 480);
    }

    private void OnGUI()
    {
        GUILayout.Label("通道重映射工具", EditorStyles.boldLabel);
        GUILayout.Label("在 Project 视图中选择一张或多张贴图，为输出的每个通道指定来源。", EditorStyles.wordWrappedLabel);
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

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("粗糙度→光滑度 (A反转)"))
        {
            outR_Source = SourceChannel.R; outR_Invert = false;
            outG_Source = SourceChannel.G; outG_Invert = false;
            outB_Source = SourceChannel.B; outB_Invert = false;
            outA_Source = SourceChannel.A; outA_Invert = true;
        }
        if (GUILayout.Button("交换 R↔A"))
        {
            outR_Source = SourceChannel.A; outR_Invert = false;
            outG_Source = SourceChannel.G; outG_Invert = false;
            outB_Source = SourceChannel.B; outB_Invert = false;
            outA_Source = SourceChannel.R; outA_Invert = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);

        // ===== 当前选中信息 =====
        Object[] selected = Selection.objects;
        int texCount = 0;
        if (selected != null)
        {
            foreach (var obj in selected)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                    texCount++;
            }
        }

        EditorGUILayout.HelpBox($"当前选中 {texCount} 张贴图", MessageType.Info);
        GUILayout.Space(4);

        // ===== 映射预览摘要 =====
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("映射预览", EditorStyles.boldLabel);
        string summary = $"  R ← {outR_Source}{(outR_Invert ? " (反转)" : "")}\n" +
                         $"  G ← {outG_Source}{(outG_Invert ? " (反转)" : "")}\n" +
                         $"  B ← {outB_Source}{(outB_Invert ? " (反转)" : "")}\n" +
                         $"  A ← {outA_Source}{(outA_Invert ? " (反转)" : "")}";
        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel, GUILayout.Height(65));
        EditorGUILayout.EndVertical();
        GUILayout.Space(8);

        // ===== 执行按钮 =====
        GUI.enabled = texCount > 0;
        if (GUILayout.Button("重映射并保存", GUILayout.Height(32)))
        {
            ProcessSelection();
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

    /// <summary>
    /// 处理当前选中的所有贴图
    /// </summary>
    private void ProcessSelection()
    {
        Object[] items = Selection.objects;
        if (items == null || items.Length == 0)
        {
            EditorUtility.DisplayDialog("通道重映射", "没有选择任何资源。", "确定");
            return;
        }

        int processedCount = 0;
        foreach (Object obj in items)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;

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
}
