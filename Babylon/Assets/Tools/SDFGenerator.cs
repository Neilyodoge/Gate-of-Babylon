using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that takes one or more textures (TGA, PNG, etc.) and
/// converts the alpha channel to a signed distance field (SDF).
///
/// The window is intentionally simple: pick one or several textures in the
/// Project view, then hit the button.  A new file with the suffix
/// "_sdf.png" will be written next to each source texture.  The SDF is
/// stored in the red channel of the resulting image (greyscale output).
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

    [MenuItem("Tools/SDF Generator")]
    public static void ShowWindow()
    {
        GetWindow<SDFGenerator>("SDF Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Select one or more textures in the Project view.", EditorStyles.wordWrappedLabel);
        GUILayout.Label("The tool converts the alpha channel to SDF and stores it in the output's alpha channel, preserving RGB.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8);

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);
        spreadRange = EditorGUILayout.FloatField("Spread Range (pixels)", spreadRange);
        spreadRange = Mathf.Max(1f, spreadRange);
        GUILayout.Space(8);

        if (GUILayout.Button("Generate SDF for selected textures", GUILayout.Height(30)))
        {
            ProcessSelection();
        }
    }

    private void ProcessSelection()
    {
        Object[] items = Selection.objects;
        if (items == null || items.Length == 0)
        {
            EditorUtility.DisplayDialog("SDF Generator", "No assets selected.", "OK");
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

            GenerateSdfForTexture(path, tex, alphaThreshold, spreadRange);
        }

        AssetDatabase.Refresh();
        Debug.Log("SDF generation complete.");
    }

    private static void GenerateSdfForTexture(string assetPath, Texture2D original, float alphaThreshold, float spreadRange)
    {
        // ensure the texture is readable
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

        // Check if there's actual alpha variation in the texture
        bool hasAlpha = false;
        byte firstAlpha = pixels[0].a;
        foreach (Color32 pixel in pixels)
        {
            if (pixel.a != firstAlpha)
            {
                hasAlpha = true;
                break;
            }
        }

        if (!hasAlpha)
        {
            Debug.LogWarning($"Texture '{assetPath}' does not have varying alpha channel; skipping.");
            return;
        }
        float[] sdf = new float[w * h];
        bool[] inside = new bool[w * h];
        float maxDist = spreadRange;

        // mark inside/outside
        for (int i = 0; i < pixels.Length; ++i)
        {
            inside[i] = pixels[i].a / 255f >= alphaThreshold;
        }

        // brute-force distance transform (not particularly fast, but textures
        // used for foliage are usually modest in size).  For each pixel we
        // scan the entire image looking for the closest pixel with the
        // opposite "sign" (inside vs. outside) and record the signed distance.
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

        // normalize SDF and write into alpha channel, preserve RGB from original
        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] outPixels = new Color32[w * h];
        for (int i = 0; i < sdf.Length; ++i)
        {
            float d = Mathf.Clamp(sdf[i] / maxDist, -1f, 1f);
            float v = 0.5f + 0.5f * d;
            byte sdfAlpha = (byte)(Mathf.Clamp01(v) * 255f);
            // Keep RGB from original, only replace alpha with SDF
            outPixels[i] = new Color32(pixels[i].r, pixels[i].g, pixels[i].b, sdfAlpha);
        }
        outTex.SetPixels32(outPixels);
        outTex.Apply();

        string directory = Path.GetDirectoryName(fullPath);
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string newFilename = Path.Combine(directory, filename + OutputSuffix + ".png");
        byte[] bytes = outTex.EncodeToPNG();
        File.WriteAllBytes(newFilename, bytes);

        if (restoreReadable && importer != null)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        string relativeNewPath = Path.GetDirectoryName(assetPath) + "/" + filename + OutputSuffix + ".png";
        AssetDatabase.ImportAsset(relativeNewPath);
        Debug.Log($"Generated SDF '{relativeNewPath}' from '{assetPath}'.");
    }
}
