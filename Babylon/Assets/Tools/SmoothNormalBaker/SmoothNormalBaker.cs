using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 平滑法线烘焙工具：
/// 计算模型的面积加权平滑法线（遍历三角面，用叉积累加面法线，天然携带面积权重），
/// 将平滑法线的 x/y 固定存入 UV3(TEXCOORD3).xy 中（2通道编码）。
/// 
/// ========== UV 通道分配约定 ==========
///   UV0 (TEXCOORD0) : 主纹理坐标
///   UV1 (TEXCOORD1) : Lightmap / 自定义数据
///   UV2 (TEXCOORD2) : Bent Normal 数据 (由 Bent Normal Baker 写入)
///   UV3 (TEXCOORD3) : 平滑法线 (由本工具写入，供 PBRToon 描边使用)
/// =====================================
/// 
/// 编码方式：
///   TEXCOORD3.xy = (smoothNormal.x, smoothNormal.y)
/// 
/// 在 Shader 中解码：
///   float2 sn = uv3.xy;
///   float3 smoothNormal = float3(sn.xy, sqrt(1 - saturate(dot(sn, sn))));
///   （因为法线已归一化，z 分量可通过勾股定理重建）
///
/// 注意：此编码假设平滑法线 z 分量 >= 0（即法线大致朝外）。
/// 对于绝大多数角色模型和描边用途，此假设成立。
///
/// 使用方法：
/// 1. 在 Hierarchy 中选择含有 MeshFilter / SkinnedMeshRenderer 的物体
/// 2. 打开 Tools > ArtTools > 平滑法线烘焙工具
/// 3. 点击"烘焙平滑法线到 UV3"
/// 4. 输出的 Mesh 保存在原 Mesh 同目录下，后缀为 _SmoothN
/// </summary>
public class SmoothNormalBaker : EditorWindow
{
    private const string OutputSuffix = "_SmoothN";
    private const float PositionThreshold = 0.0001f;

    // 固定写入 UV3 (TEXCOORD3)
    // UV2 已被 Bent Normal Baker 占用
    private const int TargetUVChannel = 3;

    [MenuItem("Tools/ArtTools/平滑法线烘焙工具")]
    public static void ShowWindow()
    {
        var win = GetWindow<SmoothNormalBaker>("平滑法线烘焙工具");
        win.minSize = new Vector2(400, 300);
    }

    private void OnGUI()
    {
        GUILayout.Label("平滑法线烘焙工具", EditorStyles.boldLabel);
        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "将模型的平滑法线（共享顶点位置的法线平均值）固定烘焙到 UV3 (TEXCOORD3).xy 中。\n\n" +
            "UV 通道分配约定：\n" +
            "  UV2 (TEXCOORD2) → Bent Normal 数据\n" +
            "  UV3 (TEXCOORD3) → 平滑法线（本工具写入）\n\n" +
            "编码方式（2通道）：\n" +
            "  TEXCOORD3.xy = (smoothNormal.x, smoothNormal.y)\n\n" +
            "Shader 解码：z = sqrt(1 - x² - y²)\n\n" +
            "使用方式：在 Hierarchy 中选择包含 Mesh 的 GameObject，点击烘焙。",
            MessageType.Info);

        GUILayout.Space(8);

        // 显示当前选中信息
        GameObject[] selectedGOs = Selection.gameObjects;
        int meshCount = 0;
        if (selectedGOs != null)
        {
            foreach (var go in selectedGOs)
            {
                if (go.GetComponent<MeshFilter>() != null || go.GetComponent<SkinnedMeshRenderer>() != null)
                    meshCount++;
            }
        }

        EditorGUILayout.HelpBox($"当前选中 {meshCount} 个包含 Mesh 的 GameObject", MessageType.Info);
        GUILayout.Space(8);

        GUI.enabled = meshCount > 0;
        if (GUILayout.Button("烘焙平滑法线到 UV3 (TEXCOORD3)", GUILayout.Height(36)))
        {
            BakeSelection(selectedGOs);
        }
        GUI.enabled = true;

        GUILayout.Space(8);

        // 批量处理：选中 Project 中的 Mesh 资源
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("批量处理（Project 选中 Mesh 资源）", EditorStyles.boldLabel);

        Object[] selectedAssets = Selection.objects;
        int assetMeshCount = 0;
        if (selectedAssets != null)
        {
            foreach (var obj in selectedAssets)
            {
                if (obj is Mesh)
                    assetMeshCount++;
                else
                {
                    // 检查是否为 FBX 等模型文件
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (importer != null)
                            assetMeshCount++;
                    }
                }
            }
        }

        EditorGUILayout.HelpBox($"当前选中 {assetMeshCount} 个 Mesh/模型资源", MessageType.Info);

        GUI.enabled = assetMeshCount > 0;
        if (GUILayout.Button("从 Project 选中的资源烘焙到 UV3 (TEXCOORD3)", GUILayout.Height(36)))
        {
            BakeFromProjectSelection(selectedAssets);
        }
        GUI.enabled = true;
    }

    /// <summary>
    /// 从 Hierarchy 选中的 GameObject 烘焙
    /// </summary>
    private void BakeSelection(GameObject[] gameObjects)
    {
        if (gameObjects == null || gameObjects.Length == 0) return;

        int processed = 0;
        foreach (var go in gameObjects)
        {
            // MeshFilter
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh newMesh = BakeSmoothNormals(mf.sharedMesh);
                if (newMesh != null)
                {
                    string savedPath = SaveMeshAsset(mf.sharedMesh, newMesh);
                    mf.sharedMesh = newMesh;
                    Debug.Log($"[平滑法线烘焙] {go.name} → {savedPath} (UV3)");
                    processed++;
                }
            }

            // SkinnedMeshRenderer
            SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                Mesh newMesh = BakeSmoothNormals(smr.sharedMesh);
                if (newMesh != null)
                {
                    string savedPath = SaveMeshAsset(smr.sharedMesh, newMesh);
                    smr.sharedMesh = newMesh;
                    Debug.Log($"[平滑法线烘焙] {go.name} → {savedPath} (UV3)");
                    processed++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[平滑法线烘焙] 完成，共处理 {processed} 个 Mesh。");
    }

    /// <summary>
    /// 从 Project 选中的 Mesh 资源直接烘焙
    /// </summary>
    private void BakeFromProjectSelection(Object[] assets)
    {
        if (assets == null || assets.Length == 0) return;

        int processed = 0;
        foreach (var obj in assets)
        {
            List<Mesh> meshes = new List<Mesh>();
            string assetPath = AssetDatabase.GetAssetPath(obj);

            if (obj is Mesh mesh)
            {
                meshes.Add(mesh);
            }
            else if (!string.IsNullOrEmpty(assetPath))
            {
                // 模型文件，提取所有子 Mesh
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var sub in subAssets)
                {
                    if (sub is Mesh subMesh)
                        meshes.Add(subMesh);
                }
            }

            foreach (var srcMesh in meshes)
            {
                Mesh newMesh = BakeSmoothNormals(srcMesh);
                if (newMesh != null)
                {
                    string savedPath = SaveMeshAsset(srcMesh, newMesh);
                    Debug.Log($"[平滑法线烘焙] {srcMesh.name} → {savedPath} (UV3)");
                    processed++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[平滑法线烘焙] 完成，共处理 {processed} 个 Mesh。");
    }

    /// <summary>
    /// 核心算法：计算面积加权平滑法线并写入 TEXCOORD3.xy（2通道编码）
    /// 
    /// 算法说明：
    /// 遍历所有三角面，用叉积计算面法线（叉积的模 = 三角形面积×2，天然携带面积权重），
    /// 按顶点位置分组累加后归一化，得到面积加权的平滑法线。
    /// 相比等权平均，面积加权在网格密度不均匀时表现更稳定，
    /// 大三角面贡献更大权重，避免密集小面导致法线偏移。
    /// </summary>
    private Mesh BakeSmoothNormals(Mesh sourceMesh)
    {
        if (sourceMesh == null) return null;

        // 复制一份 Mesh
        Mesh mesh = Object.Instantiate(sourceMesh);
        mesh.name = sourceMesh.name + OutputSuffix;

        Vector3[] vertices = mesh.vertices;
        int vertexCount = vertices.Length;

        // 1. 按位置分组，使用面积加权累加面法线
        Dictionary<Vector3Int, Vector3> smoothNormalMap = new Dictionary<Vector3Int, Vector3>();

        // 初始化所有顶点位置的条目
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3Int key = PositionToKey(vertices[i]);
            if (!smoothNormalMap.ContainsKey(key))
            {
                smoothNormalMap[key] = Vector3.zero;
            }
        }

        // 遍历所有子网格的三角形，用叉积累加面法线（天然面积加权）
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            int[] triangles = mesh.GetTriangles(subMesh);

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];

                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];

                // 叉积 = 面法线 × 2倍面积，天然携带面积权重
                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v0;
                Vector3 faceNormal = Vector3.Cross(edge1, edge2);

                // 累加到三个顶点对应的位置分组
                Vector3Int key0 = PositionToKey(v0);
                Vector3Int key1 = PositionToKey(v1);
                Vector3Int key2 = PositionToKey(v2);

                smoothNormalMap[key0] += faceNormal;
                smoothNormalMap[key1] += faceNormal;
                smoothNormalMap[key2] += faceNormal;
            }
        }

        // 2. 归一化得到最终平滑法线
        List<Vector3Int> keys = new List<Vector3Int>(smoothNormalMap.Keys);
        foreach (var key in keys)
        {
            smoothNormalMap[key] = smoothNormalMap[key].normalized;
        }

        // 3. 2通道编码：只存 xy，z 在 shader 中通过 sqrt(1 - x² - y²) 重建
        // 平滑法线已经在对象空间中（因为 mesh.normals 就是对象空间的）
        Vector2[] uvData = new Vector2[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3Int key = PositionToKey(vertices[i]);
            Vector3 smoothNormal = smoothNormalMap[key];

            uvData[i] = new Vector2(smoothNormal.x, smoothNormal.y);
        }

        mesh.SetUVs(TargetUVChannel, uvData);

        return mesh;
    }

    /// <summary>
    /// 将位置量化为整数 Key，用于相同位置顶点的分组
    /// </summary>
    private Vector3Int PositionToKey(Vector3 pos)
    {
        // 精度约 0.0001
        return new Vector3Int(
            Mathf.RoundToInt(pos.x / PositionThreshold),
            Mathf.RoundToInt(pos.y / PositionThreshold),
            Mathf.RoundToInt(pos.z / PositionThreshold)
        );
    }

    /// <summary>
    /// 将 Mesh 保存为 .asset 文件
    /// </summary>
    private string SaveMeshAsset(Mesh originalMesh, Mesh newMesh)
    {
        string originalPath = AssetDatabase.GetAssetPath(originalMesh);
        string directory;
        string baseName;

        if (!string.IsNullOrEmpty(originalPath))
        {
            directory = Path.GetDirectoryName(originalPath);
            baseName = originalMesh.name;
        }
        else
        {
            directory = "Assets";
            baseName = newMesh.name;
        }

        // 清理名称中不能作为文件名的字符
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }

        string outputPath = Path.Combine(directory, baseName + OutputSuffix + ".asset").Replace("\\", "/");

        // 检查是否已存在
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
        if (existing != null)
        {
            // 覆盖已有资源
            EditorUtility.CopySerialized(newMesh, existing);
            AssetDatabase.SaveAssets();
            return outputPath;
        }

        AssetDatabase.CreateAsset(newMesh, outputPath);
        return outputPath;
    }
}
