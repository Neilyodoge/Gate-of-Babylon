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

    [MenuItem("nTools/美术工具/SDF Generator", false, 53)]
    public static void ShowWindow()
    {
        GetWindow<SDFGenerator>("SDF生成器");
    }

    private void OnGUI()
    {
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
        GUILayout.Space(8);

        if (GUILayout.Button("生成选中纹理的SDF", GUILayout.Height(30)))
        {
            ProcessSelection();
        }
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
            GenerateSdfForAllChannels(path, tex, alphaThreshold, spreadRange, processRed, processGreen, processBlue, processAlpha);
        }

        AssetDatabase.Refresh();
        Debug.Log("SDF生成完成。");
    }

    private static void GenerateSdfForAllChannels(string assetPath, Texture2D original, float alphaThreshold, float spreadRange, bool procR, bool procG, bool procB, bool procA)
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
        float maxDist = spreadRange;

        // 为每个通道计算 SDF
        float[] sdfRed = null, sdfGreen = null, sdfBlue = null, sdfAlpha = null;

        if (procR)
            sdfRed = ComputeChannelSDF(pixels, w, h, "Red", alphaThreshold, maxDist);
        if (procG)
            sdfGreen = ComputeChannelSDF(pixels, w, h, "Green", alphaThreshold, maxDist);
        if (procB)
            sdfBlue = ComputeChannelSDF(pixels, w, h, "Blue", alphaThreshold, maxDist);
        if (procA)
            sdfAlpha = ComputeChannelSDF(pixels, w, h, "Alpha", alphaThreshold, maxDist);

        // 组合结果到输出纹理
        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] outPixels = new Color32[w * h];
        for (int i = 0; i < w * h; ++i)
        {
            // 如果处理该通道，使用 SDF；否则保留原始值
            byte r = procR ? (byte)(Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(sdfRed[i] / maxDist, -1f, 1f)) * 255f) : pixels[i].r;
            byte g = procG ? (byte)(Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(sdfGreen[i] / maxDist, -1f, 1f)) * 255f) : pixels[i].g;
            byte b = procB ? (byte)(Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(sdfBlue[i] / maxDist, -1f, 1f)) * 255f) : pixels[i].b;
            byte a = procA ? (byte)(Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(sdfAlpha[i] / maxDist, -1f, 1f)) * 255f) : pixels[i].a;
            outPixels[i] = new Color32(r, g, b, a);
        }
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

        // 计算 SDF
        float[] sdf = new float[w * h];
        bool[] inside = new bool[w * h];

        // 标记内外
        for (int i = 0; i < channelValues.Length; ++i)
        {
            inside[i] = channelValues[i] / 255f >= threshold;
        }

        // 距离变换
        for (int y = 0; y < h; ++y)
        {
            for (int x = 0; x < w; ++x)
            {
                int idx = y * w + x;
                bool inShape = inside[idx];
                float best = maxDist;

                for (int yy = 0; yy < h; ++yy)
                {
                    int row = yy * w;
                    for (int xx = 0; xx < w; ++xx)
                    {
                        int j = row + xx;
                        if (inside[j] != inShape)
                        {
                            float dx = x - xx;
                            float dy = y - yy;
                            float d2 = dx * dx + dy * dy;
                            if (d2 < best * best)
                                best = Mathf.Sqrt(d2);
                        }
                    }
                }

                sdf[idx] = inShape ? -best : best;
            }
        }

        return sdf;
    }
}
