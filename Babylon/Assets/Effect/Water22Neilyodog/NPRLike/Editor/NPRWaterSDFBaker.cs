using UnityEngine;
using UnityEditor;
using System.IO;

public class NPRWaterSDFBaker : EditorWindow
{
    MeshRenderer m_WaterRenderer;
    int          m_Resolution   = 256;
    float        m_Padding      = 2f;
    float        m_MaxDistance   = 10f;
    float        m_WaterHeight  = 0f;
    bool         m_AutoHeight   = true;
    LayerMask    m_TerrainLayer = ~0;
    float        m_MinObjSize   = 1f;

    [MenuItem("Window/NPRWater SDF Baker")]
    static void Open() => GetWindow<NPRWaterSDFBaker>("NPRWater SDF Baker");

    void OnEnable()
    {
        if (Selection.activeGameObject != null)
        {
            var mr = Selection.activeGameObject.GetComponent<MeshRenderer>();
            if (mr != null) m_WaterRenderer = mr;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("SDF Baker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择水面物体，设置参数后点击 Bake。\n" +
            "工具会向下发射射线检测地形，生成离岸距离 SDF 贴图，\n" +
            "并自动赋值到水面材质的 Foam SDF 属性。",
            MessageType.Info);

        EditorGUILayout.Space(8);

        m_WaterRenderer = (MeshRenderer)EditorGUILayout.ObjectField(
            "水面物体", m_WaterRenderer, typeof(MeshRenderer), true);

        m_Resolution = EditorGUILayout.IntPopup("分辨率", m_Resolution,
            new[] { "128", "256", "512" }, new[] { 128, 256, 512 });

        m_Padding = EditorGUILayout.FloatField("边界扩展(世界单位)", m_Padding);
        m_MaxDistance = EditorGUILayout.FloatField("最大SDF距离(世界单位)", m_MaxDistance);

        m_AutoHeight = EditorGUILayout.Toggle("自动水面高度", m_AutoHeight);
        if (!m_AutoHeight)
            m_WaterHeight = EditorGUILayout.FloatField("水面高度(Y)", m_WaterHeight);

        m_TerrainLayer = LayerMaskField("地形Layer", m_TerrainLayer);
        m_MinObjSize = EditorGUILayout.FloatField("最小地形尺寸(XZ)", m_MinObjSize);

        EditorGUILayout.Space(12);
        GUI.enabled = m_WaterRenderer != null;
        if (GUILayout.Button("Bake SDF", GUILayout.Height(32)))
            DoBake();
        GUI.enabled = true;
    }

    // ----------------------------------------------------------------

    void DoBake()
    {
        var bounds = m_WaterRenderer.bounds;
        float waterY = m_AutoHeight ? bounds.center.y : m_WaterHeight;

        float minX = bounds.min.x - m_Padding;
        float minZ = bounds.min.z - m_Padding;
        float maxX = bounds.max.x + m_Padding;
        float maxZ = bounds.max.z + m_Padding;
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;

        int w = m_Resolution;
        int h = m_Resolution;

        // Auto-add MeshCollider to renderers that lack any collider
        var tempColliders = new System.Collections.Generic.List<MeshCollider>();
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var r in allRenderers)
        {
            if (r == m_WaterRenderer) continue;
            if ((m_TerrainLayer & (1 << r.gameObject.layer)) == 0) continue;
            if (r.GetComponent<Collider>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>();
            tempColliders.Add(mc);
        }
        if (tempColliders.Count > 0)
            Physics.SyncTransforms();

        EditorUtility.DisplayProgressBar("NPRWater SDF", "Raycasting...", 0.1f);

        // Step 1: Raycast to build binary mask (true = land above water)
        // Uses RaycastAll + size filter to skip small objects
        bool[] isLand = new bool[w * h];
        float rayStart = waterY + 200f;
        float rayDist  = 400f;
        float minSizeSq = m_MinObjSize * m_MinObjSize;

        for (int iy = 0; iy < h; iy++)
        {
            for (int ix = 0; ix < w; ix++)
            {
                float wx = minX + sizeX * ((ix + 0.5f) / w);
                float wz = minZ + sizeZ * ((iy + 0.5f) / h);
                var origin = new Vector3(wx, rayStart, wz);

                var hits = Physics.RaycastAll(origin, Vector3.down, rayDist, m_TerrainLayer);
                bool land = false;
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == m_WaterRenderer.gameObject) continue;
                    var sz = hit.collider.bounds.size;
                    if (sz.x * sz.z < minSizeSq) continue;
                    if (hit.point.y >= waterY) { land = true; break; }
                }
                isLand[iy * w + ix] = land;
            }
        }

        EditorUtility.DisplayProgressBar("NPRWater SDF", "Computing SDF (JFA)...", 0.4f);

        // Step 2: Jump Flooding Algorithm
        int[] nearX = new int[w * h];
        int[] nearY = new int[w * h];

        for (int i = 0; i < w * h; i++)
        {
            if (isLand[i])
            {
                nearX[i] = i % w;
                nearY[i] = i / w;
            }
            else
            {
                nearX[i] = -9999;
                nearY[i] = -9999;
            }
        }

        int maxDim = Mathf.Max(w, h);
        int step = 1;
        while (step < maxDim) step *= 2;
        step /= 2;

        while (step >= 1)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float bestDist = SqDist(x, y, nearX[idx], nearY[idx]);

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx * step;
                            int ny = y + dy * step;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                            int nidx = ny * w + nx;
                            if (nearX[nidx] < 0) continue;

                            float d = SqDist(x, y, nearX[nidx], nearY[nidx]);
                            if (d < bestDist)
                            {
                                bestDist = d;
                                nearX[idx] = nearX[nidx];
                                nearY[idx] = nearY[nidx];
                            }
                        }
                    }
                }
            }
            step /= 2;
        }

        EditorUtility.DisplayProgressBar("NPRWater SDF", "Saving texture...", 0.8f);

        // Step 3: Compute pixel distances → normalized by max world distance
        float pixelsPerUnitX = w / sizeX;
        float pixelsPerUnitZ = h / sizeZ;
        float avgPixelsPerUnit = (pixelsPerUnitX + pixelsPerUnitZ) * 0.5f;
        float maxPixelDist = m_MaxDistance * avgPixelsPerUnit;

        var tex = new Texture2D(w, h, TextureFormat.RHalf, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int px = i % w;
            int py = i / w;
            float pixDist = Mathf.Sqrt(SqDist(px, py, nearX[i], nearY[i]));
            float norm = Mathf.Clamp01(pixDist / maxPixelDist);
            if (isLand[i]) norm = 0;
            pixels[i] = new Color(norm, 0, 0, 1);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // Step 4: Save asset
        string matPath = AssetDatabase.GetAssetPath(m_WaterRenderer.sharedMaterial);
        string dir = string.IsNullOrEmpty(matPath) ? "Assets" : Path.GetDirectoryName(matPath);
        string texPath = Path.Combine(dir, m_WaterRenderer.sharedMaterial.name + "_FoamSDF.asset")
                             .Replace("\\", "/");

        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(tex, existing);
            Object.DestroyImmediate(tex);
            tex = existing;
            EditorUtility.SetDirty(tex);
        }
        else
        {
            AssetDatabase.CreateAsset(tex, texPath);
        }
        AssetDatabase.SaveAssets();

        // Step 5: Assign to material
        var mat = m_WaterRenderer.sharedMaterial;
        mat.SetTexture("_FoamSDF", tex);
        mat.SetVector("_SDFBoundsMin", new Vector4(minX, minZ, 0, 0));
        mat.SetVector("_SDFBoundsSize", new Vector4(sizeX, sizeZ, m_MaxDistance, 0));
        EditorUtility.SetDirty(mat);

        // Cleanup: remove temporary colliders
        foreach (var mc in tempColliders)
        {
            if (mc != null)
                Object.DestroyImmediate(mc);
        }

        EditorUtility.ClearProgressBar();
        Debug.Log($"[NPRWater] SDF baked: {texPath} ({w}x{h}, maxDist={m_MaxDistance})" +
                  (tempColliders.Count > 0 ? $" (auto-added & removed {tempColliders.Count} temp colliders)" : ""));
    }

    static float SqDist(int x1, int y1, int x2, int y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    static LayerMask LayerMaskField(string label, LayerMask mask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        var layerNumbers = new int[layers.Length];
        for (int i = 0; i < layers.Length; i++)
            layerNumbers[i] = LayerMask.NameToLayer(layers[i]);

        int maskValue = 0;
        for (int i = 0; i < layerNumbers.Length; i++)
        {
            if ((mask & (1 << layerNumbers[i])) != 0)
                maskValue |= (1 << i);
        }

        maskValue = EditorGUILayout.MaskField(label, maskValue, layers);

        int result = 0;
        for (int i = 0; i < layerNumbers.Length; i++)
        {
            if ((maskValue & (1 << i)) != 0)
                result |= (1 << layerNumbers[i]);
        }
        return result;
    }
}
