using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that takes one or more textures (TGA, PNG, etc.) and
/// converts a specified channel to a signed distance field (SDF).
///
/// The window allows selecting which channel (R, G, B, or A) to process,
/// then generates an output texture with the suffix "_sdf.png".
/// The SDF is stored in the red channel of the resulting image.
///
/// This tool was inspired by the vegetation work shown at the GDC talk
/// by Ninja Theory/Horizon; it is primarily intended for foliage
/// alpha-clip textures.
/// </summary>
public class SDFGenerator : EditorWindow
{
    private const string OutputSuffix = "_sdf";
    private float alphaThreshold = 0.5f;
    private float spreadRange = 32f;
    private bool processRed = false;
    private bool processGreen = false;
    private bool processBlue = false;
    private bool processAlpha = true;
    private bool invert = false;

    // 预览
    private Texture2D _previewTex;
    private string _previewName;
    private Vector2 _scroll;

    [MenuItem("nTools/美术工具/SDF Generator", false, 53)]
    public static void ShowWindow()
    {
        GetWindow<SDFGenerator>("SDF生成器");
    }

    private void OnDisable()
    {
        if (_previewTex != null) { DestroyImmediate(_previewTex); _previewTex = null; }
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        GUILayout.Label("在项目视图中选择一个或多个纹理。", EditorStyles.wordWrappedLabel);
        GUILayout.Label("该工具将选中的通道转换为SDF，并将其存储在输出纹理中。", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8);

        GUILayout.Label("设置", EditorStyles.boldLabel);
        
        // 通道选择复选框
        GUILayout.Label("处理通道", EditorStyles.boldLabel);
        processRed = EditorGUILayout.Toggle("红色 (R)", processRed);
        processGreen = EditorGUILayout.Toggle("绿色 (G)", processGreen);
        processBlue = EditorGUILayout.Toggle("蓝色 (B)", processBlue);
        processAlpha = EditorGUILayout.Toggle("Alpha (A)", processAlpha);
        
        GUILayout.Space(8);
        alphaThreshold = EditorGUILayout.Slider("阈值", alphaThreshold, 0f, 1f);
        spreadRange = EditorGUILayout.FloatField("扩展范围(像素)", spreadRange);
        spreadRange = Mathf.Max(1f, spreadRange);
        invert = EditorGUILayout.Toggle(new GUIContent("反转 (Invert)", "翻转内外：距离取反，内亮外暗"), invert);
        GUILayout.Space(8);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("预览选中纹理", GUILayout.Height(30)))
                GeneratePreview();
            if (GUILayout.Button("生成选中纹理的SDF", GUILayout.Height(30)))
                ProcessSelection();
        }

        DrawPreview();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPreview()
    {
        if (_previewTex == null) return;

        GUILayout.Space(10);
        GUILayout.Label($"预览：{_previewName}", EditorStyles.boldLabel);
        GUILayout.Label("（仅预览首个选中纹理，未写入磁盘）", EditorStyles.miniLabel);

        float maxW = Mathf.Max(64f, EditorGUIUtility.currentViewWidth - 30f);
        float side = Mathf.Min(maxW, 256f);
        float aspect = _previewTex.height > 0 ? (float)_previewTex.width / _previewTex.height : 1f;
        float drawW = side, drawH = side;
        if (aspect >= 1f) drawH = side / aspect; else drawW = side * aspect;

        Rect r = GUILayoutUtility.GetRect(drawW, drawH, GUILayout.ExpandWidth(false));
        // 棋盘底，便于观察 Alpha 通道结果
        EditorGUI.DrawTextureTransparent(r, _previewTex, ScaleMode.ScaleToFit);
    }

    private void ProcessSelection()
    {
        // 检查是否选择了至少一个通道
        if (!processRed && !processGreen && !processBlue && !processAlpha)
        {
            EditorUtility.DisplayDialog("SDF生成器", "请选择至少一个通道来处理。", "确定");
            return;
        }

        Object[] items = Selection.objects;
        if (items == null || items.Length == 0)
        {
            EditorUtility.DisplayDialog("SDF生成器", "没有选择任何资源。", "确定");
            return;
        }

        foreach (Object obj in items)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;

            // 一次性处理所有选中的通道
            GenerateSdfForAllChannels(path, tex, alphaThreshold, spreadRange, processRed, processGreen, processBlue, processAlpha, invert);
        }

        AssetDatabase.Refresh();
        Debug.Log("SDF生成完成。");
    }

    /// <summary>为项目视图中首个选中纹理生成内存预览（不写盘）。</summary>
    private void GeneratePreview()
    {
        if (!processRed && !processGreen && !processBlue && !processAlpha)
        {
            EditorUtility.DisplayDialog("SDF生成器", "请选择至少一个通道来处理。", "确定");
            return;
        }

        Texture2D src = null;
        foreach (Object obj in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(p)) continue;
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (t != null) { src = t; break; }
        }
        if (src == null)
        {
            EditorUtility.DisplayDialog("SDF生成器", "没有选择任何纹理。", "确定");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(src);
        if (!ReadPixels(assetPath, src, out Color32[] pixels, out int w, out int h))
        {
            EditorUtility.DisplayDialog("SDF生成器", "无法读取该纹理像素。", "确定");
            return;
        }

        Color32[] outPixels = BuildSdfPixels(pixels, w, h, alphaThreshold, spreadRange,
            processRed, processGreen, processBlue, processAlpha, invert);

        if (_previewTex == null || _previewTex.width != w || _previewTex.height != h)
        {
            if (_previewTex != null) DestroyImmediate(_previewTex);
            _previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        }
        _previewTex.SetPixels32(outPixels);
        _previewTex.Apply();
        _previewName = System.IO.Path.GetFileName(assetPath);
        Repaint();
    }

    /// <summary>读取纹理像素（必要时临时开启可读，读完还原）。</summary>
    private static bool ReadPixels(string assetPath, Texture2D tex, out Color32[] pixels, out int w, out int h)
    {
        pixels = null; w = 0; h = 0;
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        bool restore = false;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            restore = true;
        }
        try
        {
            w = tex.width;
            h = tex.height;
            pixels = tex.GetPixels32();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"读取像素失败：{e.Message}");
            return false;
        }
        finally
        {
            if (restore && importer != null)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
        return pixels != null;
    }

    private static void GenerateSdfForAllChannels(string assetPath, Texture2D original, float alphaThreshold, float spreadRange, bool procR, bool procG, bool procB, bool procA, bool invert)
    {
        // 确保纹理可读
        string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), assetPath);
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
        Color32[] pixels = original.GetPixels32();

        Color32[] outPixels = BuildSdfPixels(pixels, w, h, alphaThreshold, spreadRange, procR, procG, procB, procA, invert);

        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels32(outPixels);
        outTex.Apply();

        // 保存输出
        string directory = Path.GetDirectoryName(fullPath);
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string originalExtension = Path.GetExtension(assetPath).ToLower();
        
        // 根据原始文件格式保存
        string newFilename = Path.Combine(directory, filename + OutputSuffix + originalExtension);
        byte[] bytes;
        
        if (originalExtension == ".tga")
        {
            bytes = outTex.EncodeToTGA();
        }
        else if (originalExtension == ".exr")
        {
            bytes = outTex.EncodeToEXR();
        }
        else
        {
            // 默认保存为 PNG
            bytes = outTex.EncodeToPNG();
            if (originalExtension != ".png")
                newFilename = Path.Combine(directory, filename + OutputSuffix + ".png");
        }
        
        File.WriteAllBytes(newFilename, bytes);

        if (restoreReadable && importer != null)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        // 计算相对路径用于日志
        string relativeOutputName = filename + OutputSuffix;
        if (originalExtension == ".tga" || originalExtension == ".exr")
            relativeOutputName += originalExtension;
        else
            relativeOutputName += ".png";
        
        string relativeNewPath = Path.GetDirectoryName(assetPath) + "/" + relativeOutputName;
        AssetDatabase.ImportAsset(relativeNewPath);
        Debug.Log($"从 '{assetPath}' 生成多通道SDF '{relativeNewPath}'。");

        DestroyImmediate(outTex);
    }

    /// <summary>把选中通道转成 SDF 并组合为输出像素；未处理的通道保留原值。invert=反转内外（距离取反）。</summary>
    private static Color32[] BuildSdfPixels(Color32[] pixels, int w, int h, float threshold, float maxDist,
        bool procR, bool procG, bool procB, bool procA, bool invert)
    {
        maxDist = Mathf.Max(1f, maxDist);
        float sign = invert ? -1f : 1f;

        float[] sdfRed = procR ? ComputeChannelSDF(pixels, w, h, "Red", threshold, maxDist) : null;
        float[] sdfGreen = procG ? ComputeChannelSDF(pixels, w, h, "Green", threshold, maxDist) : null;
        float[] sdfBlue = procB ? ComputeChannelSDF(pixels, w, h, "Blue", threshold, maxDist) : null;
        float[] sdfAlpha = procA ? ComputeChannelSDF(pixels, w, h, "Alpha", threshold, maxDist) : null;

        Color32[] outPixels = new Color32[w * h];
        for (int i = 0; i < w * h; ++i)
        {
            byte r = procR ? Encode(sdfRed[i], sign, maxDist) : pixels[i].r;
            byte g = procG ? Encode(sdfGreen[i], sign, maxDist) : pixels[i].g;
            byte b = procB ? Encode(sdfBlue[i], sign, maxDist) : pixels[i].b;
            byte a = procA ? Encode(sdfAlpha[i], sign, maxDist) : pixels[i].a;
            outPixels[i] = new Color32(r, g, b, a);
        }
        return outPixels;
    }

    private static byte Encode(float sdf, float sign, float maxDist)
    {
        return (byte)(Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(sign * sdf / maxDist, -1f, 1f)) * 255f);
    }

    private static float[] ComputeChannelSDF(Color32[] pixels, int w, int h, string channelName, float threshold, float maxDist)
    {
        // 提取指定通道
        byte[] channelValues = new byte[pixels.Length];
        bool hasVariation = false;
        byte firstValue = 0;
        
        for (int i = 0; i < pixels.Length; ++i)
        {
            byte value = 0;
            switch (channelName)
            {
                case "Red":
                    value = pixels[i].r;
                    break;
                case "Green":
                    value = pixels[i].g;
                    break;
                case "Blue":
                    value = pixels[i].b;
                    break;
                case "Alpha":
                default:
                    value = pixels[i].a;
                    break;
            }
            channelValues[i] = value;
            
            if (i == 0)
                firstValue = value;
            else if (value != firstValue)
                hasVariation = true;
        }

        if (!hasVariation)
        {
            Debug.LogWarning($"通道 {channelName} 没有变化; 使用默认值。");
            float[] defaultSDF = new float[pixels.Length];
            for (int i = 0; i < defaultSDF.Length; ++i)
                defaultSDF[i] = 0f;
            return defaultSDF;
        }

        // 内外标记
        bool[] inside = new bool[w * h];
        for (int i = 0; i < channelValues.Length; ++i)
            inside[i] = channelValues[i] / 255f >= threshold;

        // 距离变换：8SSEDT（两遍扫描，O(N)）。
        // grid1 以"内部"像素为种子 → 每个像素到最近内部像素的距离；
        // grid2 以"外部"像素为种子 → 每个像素到最近外部像素的距离；
        // 有符号距离 = dist(grid1) - dist(grid2)（内部为负、外部为正，与旧实现约定一致）。
        var grid1 = new SdfPoint[w * h];
        var grid2 = new SdfPoint[w * h];
        for (int i = 0; i < inside.Length; ++i)
        {
            if (inside[i]) { grid1[i] = SdfPoint.Seed; grid2[i] = SdfPoint.Empty; }
            else { grid1[i] = SdfPoint.Empty; grid2[i] = SdfPoint.Seed; }
        }

        GenerateDT(grid1, w, h);
        GenerateDT(grid2, w, h);

        float[] sdf = new float[w * h];
        for (int i = 0; i < sdf.Length; ++i)
        {
            float d1 = Mathf.Sqrt(grid1[i].DistSq());
            float d2 = Mathf.Sqrt(grid2[i].DistSq());
            sdf[i] = d1 - d2;
        }

        return sdf;
    }

    // ==================== 8SSEDT 距离变换 ====================
    private struct SdfPoint
    {
        public int dx, dy;
        // 用 long 计算，避免大偏移在传播 +1 后平方和溢出 int（溢出会变负数→被误判更近→NaN→黑图）。
        public long DistSq() => (long)dx * dx + (long)dy * dy;
        // 种子（自身即边界内/外集合成员，距离 0）
        public static SdfPoint Seed => new SdfPoint { dx = 0, dy = 0 };
        // 空（尚未找到最近种子，取足够大的偏移，DistSq 远大于任何真实距离）
        public static SdfPoint Empty => new SdfPoint { dx = 32767, dy = 32767 };
    }

    private static SdfPoint Get(SdfPoint[] g, int w, int h, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return SdfPoint.Empty;
        return g[y * w + x];
    }

    private static void Compare(SdfPoint[] g, int w, int h, ref SdfPoint p, int x, int y, int ox, int oy)
    {
        SdfPoint other = Get(g, w, h, x + ox, y + oy);
        other.dx += ox;
        other.dy += oy;
        if (other.DistSq() < p.DistSq()) p = other;
    }

    private static void GenerateDT(SdfPoint[] g, int w, int h)
    {
        // 前向遍历
        for (int y = 0; y < h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                SdfPoint p = g[y * w + x];
                Compare(g, w, h, ref p, x, y, -1, 0);
                Compare(g, w, h, ref p, x, y, 0, -1);
                Compare(g, w, h, ref p, x, y, -1, -1);
                Compare(g, w, h, ref p, x, y, 1, -1);
                g[y * w + x] = p;
            }
            for (int x = w - 1; x >= 0; --x)
            {
                SdfPoint p = g[y * w + x];
                Compare(g, w, h, ref p, x, y, 1, 0);
                g[y * w + x] = p;
            }
        }

        // 反向遍历
        for (int y = h - 1; y >= 0; --y)
        {
            for (int x = w - 1; x >= 0; --x)
            {
                SdfPoint p = g[y * w + x];
                Compare(g, w, h, ref p, x, y, 1, 0);
                Compare(g, w, h, ref p, x, y, 0, 1);
                Compare(g, w, h, ref p, x, y, -1, 1);
                Compare(g, w, h, ref p, x, y, 1, 1);
                g[y * w + x] = p;
            }
            for (int x = 0; x < w; ++x)
            {
                SdfPoint p = g[y * w + x];
                Compare(g, w, h, ref p, x, y, -1, 0);
                g[y * w + x] = p;
            }
        }
    }
}
