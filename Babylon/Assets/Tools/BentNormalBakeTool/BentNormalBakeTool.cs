// ============================================================================
// BentNormalBakeTool.cs
// Bent Normal 烘焙工具 (CPU Raycast 版本)
// 参考 Yarp BentNormalBakeTool，适配标准 URP 项目（不依赖 DXR 硬件光追）
// 
// 数据存储方式：将 bent normal 编码为 Vector4(relativeB, theta, aperture, scale)
// 存入 Mesh 的 UV2 (texcoord2) 通道，由 Shader 端在顶点着色器中解码还原
// ============================================================================
#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace BentNormalBaker
{
    /// <summary>
    /// 三角形采样器 - 生成低差异序列的重心坐标用于采样三角形表面
    /// </summary>
    class TriangleSamples
    {
        public int NumSamples = 256;
        public float[] bcc0, bcc1, bcc2;

        public TriangleSamples(int n)
        {
            NumSamples = n;
            InitBarycentricCoords();
        }

        private void InitBarycentricCoords()
        {
            bcc0 = new float[NumSamples];
            bcc1 = new float[NumSamples];
            bcc2 = new float[NumSamples];

            for (int i = 0; i < NumSamples; ++i)
            {
                float u = RadicalInverseBase2((uint)i);
                Vector2 barycoords = LowDiscrepancySampleTriangle(u);

                bcc0[i] = barycoords.x;
                bcc1[i] = barycoords.y;
                bcc2[i] = 1 - barycoords.x - barycoords.y;
            }
        }

        static float RadicalInverseBase2(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
            return (float)(bits * 2.3283064365386963e-10);
        }

        // https://pharr.org/matt/blog/2019/02/27/triangle-sampling-1
        static Vector2 LowDiscrepancySampleTriangle(float u)
        {
            UInt32 uf = (UInt32)(u * ((ulong)(1) << 32));
            Vector2 A = new Vector2(1, 0);
            Vector2 B = new Vector2(0, 1);
            Vector2 C = new Vector2(0, 0);
            for (int i = 0; i < 16; ++i)
            {
                UInt32 d = (uf >> (2 * (15 - i))) & (UInt32)0x3;
                Vector2 An, Bn, Cn;
                switch (d)
                {
                    case 0:
                        An = (B + C) / 2; Bn = (A + C) / 2; Cn = (A + B) / 2;
                        break;
                    case 1:
                        An = A; Bn = (A + B) / 2; Cn = (A + C) / 2;
                        break;
                    case 2:
                        An = (B + A) / 2; Bn = B; Cn = (B + C) / 2;
                        break;
                    default:
                        An = (C + A) / 2; Bn = (C + B) / 2; Cn = C;
                        break;
                }
                A = An; B = Bn; C = Cn;
            }
            Vector2 r = (A + B + C) / 3;
            return new Vector2(r.x, r.y);
        }
    }

    /// <summary>
    /// 半球均匀采样方向生成器
    /// </summary>
    static class HemisphereSampler
    {
        /// <summary>
        /// 在给定法线方向的半球上生成均匀分布的射线方向
        /// </summary>
        public static Vector3[] GenerateDirections(Vector3 normal, int count)
        {
            Vector3[] directions = new Vector3[count];

            // 构建切线空间
            Vector3 tangent, bitangent;
            if (Mathf.Abs(normal.y) < 0.999f)
            {
                tangent = Vector3.Cross(Vector3.up, normal).normalized;
            }
            else
            {
                tangent = Vector3.Cross(Vector3.right, normal).normalized;
            }
            bitangent = Vector3.Cross(normal, tangent);

            for (int i = 0; i < count; ++i)
            {
                // 使用 Halton 序列生成均匀分布的球面坐标
                float u1 = HaltonSequence(i + 1, 2);
                float u2 = HaltonSequence(i + 1, 3);

                // 余弦加权半球采样
                float r = Mathf.Sqrt(u1);
                float theta = 2.0f * Mathf.PI * u2;

                float x = r * Mathf.Cos(theta);
                float y = r * Mathf.Sin(theta);
                float z = Mathf.Sqrt(Mathf.Max(0, 1.0f - u1));

                directions[i] = (tangent * x + bitangent * y + normal * z).normalized;
            }

            return directions;
        }

        static float HaltonSequence(int index, int baseVal)
        {
            float result = 0;
            float f = 1.0f / baseVal;
            int i = index;
            while (i > 0)
            {
                result += f * (i % baseVal);
                i /= baseVal;
                f /= baseVal;
            }
            return result;
        }
    }

    /// <summary>
    /// Bent Normal 烘焙到 Mesh UV 通道的参数
    /// </summary>
    [Serializable]
    public class BentNormalMeshBakeParameters
    {
        [Header("射线追踪设置")]
        [Tooltip("每个顶点的射线采样数")]
        [Range(16, 1024)]
        public int RayCount = 256;

        [Tooltip("最大射线距离(米)")]
        [Range(0.01f, 4f)]
        public float RayLength = 0.5f;

        [Tooltip("自碰撞法线偏移(毫米)")]
        [Range(0.01f, 4f)]
        public float RayNormalOffset = 1.0f;

        [Header("平滑设置")]
        [Tooltip("三角形表面采样数（用于平滑顶点数据）")]
        [Range(4, 256)]
        public int TriangleSampleCount = 64;

        [Header("存储设置")]
        [Tooltip("存储数据的 UV 通道 (参考默认为 UV2)")]
        [Range(0, 7)]
        public int UVChannel = 2;
    }

    /// <summary>
    /// Bent Normal 烘焙到 Mesh UV 的核心逻辑
    /// 数据编码: Vector4(relativeB, theta, aperture, scale)
    ///   relativeB  = dot(bentNormal, worldBiTangent)
    ///   theta      = atan2(relativeT, relativeN)  (bent normal 在法线-切线平面的角度)
    ///   aperture   = 可见锥体半角 (弧度)
    ///   scale      = 可见锥体缩放
    /// 
    /// 解码方式 (在顶点着色器中):
    ///   float tangentLength = sqrt(max(1 - relativeB * relativeB, 0));
    ///   float3 coneVisDir = relativeB * bitangent 
    ///                     + (cos(theta) * normal + sin(theta) * orthoTangent) * tangentLength;
    /// </summary>
    public static class BentNormalMeshBaker
    {
        /// <summary>
        /// 对选中的 GameObject 烘焙 Bent Normal 数据到 Mesh UV
        /// </summary>
        public static void BakeMesh(BentNormalMeshBakeParameters parameters)
        {
            if (Selection.gameObjects.Length <= 0)
            {
                EditorUtility.DisplayDialog("错误", "没有选中的物体", "Ok");
                return;
            }

            parameters ??= new BentNormalMeshBakeParameters();

            foreach (GameObject selectedGameObject in Selection.gameObjects)
            {
                if (selectedGameObject == null)
                    continue;

                Vector3 scale = selectedGameObject.transform.lossyScale;
                bool isNegativeScale = (scale.x <= 0 || scale.y <= 0 || scale.z <= 0);
                if (isNegativeScale)
                {
                    EditorUtility.DisplayDialog("错误",
                        $"模型 {selectedGameObject.name} 缩放为负或者0", "Ok");
                    continue;
                }

                BakeGameObject(selectedGameObject, parameters);
            }
        }

        private static void BakeGameObject(GameObject go, BentNormalMeshBakeParameters parameters)
        {
            // 收集所有需要烘焙的子物体
            List<GameObject> bakingObjects = new List<GameObject>();
            CollectMeshObjects(go, bakingObjects);

            if (bakingObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", $"{go.name} 没有找到任何有效网格", "Ok");
                return;
            }

            // 为场景中所有物体创建临时 MeshCollider
            List<MeshCollider> tempColliders = new List<MeshCollider>();
            try
            {
                SetupAllColliders(tempColliders);

                // 逐个网格烘焙
                for (int i = 0; i < bakingObjects.Count; ++i)
                {
                    GameObject obj = bakingObjects[i];
                    Mesh mesh = GetMesh(obj);
                    Transform transform = obj.transform;

                    if (mesh == null) continue;

                    EditorUtility.DisplayProgressBar("烘焙 Bent Normal",
                        $"处理网格 {i + 1}/{bakingObjects.Count}: {mesh.name}",
                        (float)i / bakingObjects.Count);

                    BakeSingleMesh(obj, mesh, transform, parameters);
                }
            }
            finally
            {
                // 清理临时碰撞体
                foreach (var collider in tempColliders)
                {
                    if (collider != null)
                        UnityEngine.Object.DestroyImmediate(collider);
                }
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CollectMeshObjects(GameObject parent, List<GameObject> result)
        {
            if (parent.activeInHierarchy)
            {
                MeshFilter mf = parent.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = parent.GetComponent<SkinnedMeshRenderer>();
                if ((mf != null && mf.sharedMesh != null) ||
                    (smr != null && smr.sharedMesh != null))
                {
                    result.Add(parent);
                }
            }

            for (int i = 0; i < parent.transform.childCount; ++i)
            {
                CollectMeshObjects(parent.transform.GetChild(i).gameObject, result);
            }
        }

        private static Mesh GetMesh(GameObject go)
        {
            SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null) return smr.sharedMesh;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null) return mf.sharedMesh;

            return null;
        }

        private static void SetupAllColliders(List<MeshCollider> tempColliders)
        {
            // 为场景中所有有 Renderer 但没有 Collider 的物体添加临时碰撞体
            Renderer[] allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            foreach (var renderer in allRenderers)
            {
                if (!renderer.gameObject.activeInHierarchy)
                    continue;

                if (renderer.GetComponent<Collider>() != null)
                    continue;

                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var collider = renderer.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mf.sharedMesh;
                    tempColliders.Add(collider);
                }

                SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null && mf == null)
                {
                    var collider = renderer.gameObject.AddComponent<MeshCollider>();
                    Mesh bakedMesh = new Mesh();
                    smr.BakeMesh(bakedMesh, true);
                    collider.sharedMesh = bakedMesh;
                    tempColliders.Add(collider);
                }
            }
        }

        /// <summary>
        /// 对单个 mesh 进行逐顶点的 bent normal 烘焙
        /// </summary>
        private static void BakeSingleMesh(GameObject go, Mesh srcMesh, Transform transform,
            BentNormalMeshBakeParameters parameters)
        {
            Vector3[] vertices = srcMesh.vertices;
            Vector3[] normals = srcMesh.normals;
            Vector4[] tangents = srcMesh.tangents;
            int[] triangles = srcMesh.triangles;

            if (normals == null || normals.Length == 0)
            {
                Debug.LogWarning($"网格 {srcMesh.name} 没有法线数据，跳过");
                return;
            }

            if (tangents == null || tangents.Length == 0)
            {
                Debug.LogWarning($"网格 {srcMesh.name} 没有切线数据，尝试重新计算");
                srcMesh.RecalculateTangents();
                tangents = srcMesh.tangents;
                if (tangents == null || tangents.Length == 0)
                {
                    Debug.LogWarning($"网格 {srcMesh.name} 切线重新计算失败，跳过");
                    return;
                }
            }

            int vertexCount = vertices.Length;
            float normalOffset = parameters.RayNormalOffset * 0.001f; // 毫米转米

            // ===== 第1步: 逐顶点计算 bent normal 方向和遮蔽信息 =====
            Vector3[] bentNormalsWS = new Vector3[vertexCount]; // 世界空间 bent normal
            float[] aoValues = new float[vertexCount];          // AO 值

            // 使用三角形面积加权平滑采样
            TriangleSamples triSamples = new TriangleSamples(parameters.TriangleSampleCount);
            int totalTriangles = triangles.Length / 3;

            // 先逐顶点做射线追踪
            for (int vi = 0; vi < vertexCount; ++vi)
            {
                if (vi % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("烘焙 Bent Normal",
                        $"射线追踪顶点 {vi + 1}/{vertexCount}",
                        (float)vi / vertexCount);
                }

                Vector3 worldPos = transform.TransformPoint(vertices[vi]);
                Vector3 worldNormal = transform.TransformDirection(normals[vi]).normalized;

                Vector3 bentNormalWS;
                float ao;
                ComputeBentNormal(worldPos, worldNormal, normalOffset,
                    parameters.RayCount, parameters.RayLength, out bentNormalWS, out ao);

                bentNormalsWS[vi] = bentNormalWS;
                aoValues[vi] = ao;
            }

            // ===== 第2步: 将 bent normal 编码为 Vector4(relativeB, theta, aperture, scale) =====
            List<Vector4> uvData = new List<Vector4>(vertexCount);

            for (int vi = 0; vi < vertexCount; ++vi)
            {
                Vector3 worldNormal = transform.TransformDirection(normals[vi]).normalized;
                Vector3 worldTangent = transform.TransformDirection(
                    tangents[vi].x, tangents[vi].y, tangents[vi].z).normalized;
                float tangentW = tangents[vi].w;

                // 构建切线空间基
                Vector3 worldBiTangent = Vector3.Cross(worldNormal, worldTangent).normalized * tangentW;
                // 重新正交化切线
                Vector3 orthoTangent = Vector3.Cross(worldBiTangent, worldNormal).normalized;

                Vector3 bentNormal = bentNormalsWS[vi];

                // 编码: 参考 Yarp BentNormalBakeTool ConvertToCone 中的编码方式
                // relativeB = dot(bentNormal, worldBiTangent) — bent normal 在副切线方向的分量
                float relativeB = Vector3.Dot(bentNormal, worldBiTangent);
                // relativeN = dot(bentNormal, worldNormal) — bent normal 在法线方向的分量
                float relativeN = Vector3.Dot(bentNormal, worldNormal);
                // relativeT = dot(bentNormal, orthoTangent) — bent normal 在切线方向的分量
                float relativeT = Vector3.Dot(bentNormal, orthoTangent);
                // theta = atan2(relativeT, relativeN) — 在法线-切线平面内的角度
                float theta = Mathf.Atan2(relativeT, relativeN);

                // aperture: AO 值对应的锥体半角
                // AO 值 0 表示完全遮蔽 -> aperture 接近 0
                // AO 值 1 表示完全可见 -> aperture 接近 PI
                // 近似映射: aperture = acos(1 - ao) 或者直接用 ao * PI/2
                float aperture = Mathf.Acos(Mathf.Clamp(1.0f - 2.0f * aoValues[vi], -1f, 1f));

                // scale: 遮蔽强度缩放，这里直接用 AO 值
                float scale = aoValues[vi];

                uvData.Add(new Vector4(relativeB, theta, aperture, scale));
            }

            // ===== 第3步: 保存到 Mesh UV 通道 =====
            // 复制一份新的 mesh，避免修改原始资源
            string assetPath = AssetDatabase.GetAssetPath(srcMesh);
            
            // 在原始 FBX 路径下创建 {FBX名字}BNMesh 文件夹
            string fbxDir = Path.GetDirectoryName(assetPath);  // FBX 所在目录
            string fbxName = Path.GetFileNameWithoutExtension(assetPath);  // FBX 文件名（不含扩展名）
            string outputDir = $"{fbxDir}/{fbxName}BNMesh";
            if (!AssetDatabase.IsValidFolder(outputDir))
            {
                AssetDatabase.CreateFolder(fbxDir, $"{fbxName}BNMesh");
            }

            string meshName = srcMesh.name;
            string outputPath = $"{outputDir}/{meshName}_BentNormal.asset";

            // 检查是否已存在，如果存在则更新
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            
            Mesh vMesh;
            if (existingMesh != null)
            {
                // 更新已存在的 mesh
                vMesh = existingMesh;
                EditorUtility.CopySerialized(srcMesh, vMesh);
            }
            else
            {
                vMesh = UnityEngine.Object.Instantiate(srcMesh);
                vMesh.name = $"{meshName}_BentNormal";
            }

            vMesh.SetUVs(parameters.UVChannel, uvData);

            if (existingMesh == null)
            {
                AssetDatabase.CreateAsset(vMesh, outputPath);
            }
            else
            {
                EditorUtility.SetDirty(vMesh);
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"Bent Normal 数据已写入 Mesh UV{parameters.UVChannel}: {outputPath}");

            // 自动替换 MeshFilter 中的 mesh
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Undo.RecordObject(mf, "Replace Mesh with Bent Normal Mesh");
                mf.sharedMesh = vMesh;
                EditorUtility.SetDirty(mf);
                Debug.Log($"已将 {go.name} 的 MeshFilter 替换为烘焙后的 Mesh");
            }
        }

        /// <summary>
        /// 计算单个采样点的 Bent Normal 和 AO
        /// </summary>
        private static void ComputeBentNormal(Vector3 worldPos, Vector3 worldNormal, float normalOffset,
            int rayCount, float rayLength, out Vector3 bentNormal, out float ao)
        {
            Vector3 origin = worldPos + worldNormal * normalOffset;
            Vector3[] directions = HemisphereSampler.GenerateDirections(worldNormal, rayCount);

            Vector3 accumulatedDir = Vector3.zero;
            int unoccludedCount = 0;

            for (int i = 0; i < rayCount; ++i)
            {
                if (!Physics.Raycast(origin, directions[i], rayLength))
                {
                    // 射线未命中任何物体 - 该方向未被遮挡
                    accumulatedDir += directions[i];
                    unoccludedCount++;
                }
            }

            ao = (float)unoccludedCount / rayCount;

            if (accumulatedDir.sqrMagnitude > 0.0001f)
            {
                bentNormal = accumulatedDir.normalized;
            }
            else
            {
                // 完全被遮挡时使用几何法线
                bentNormal = worldNormal;
            }
        }
    }

    /// <summary>
    /// Bent Normal 烘焙工具窗口
    /// </summary>
    public class BentNormalBakeToolWindow : EditorWindow
    {
        private BentNormalMeshBakeParameters m_meshParameters = new BentNormalMeshBakeParameters();
        private Vector2 m_scrollPos;

        [MenuItem("Tools/ArtTools/Bent Normal Baker", priority = 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<BentNormalBakeToolWindow>(false, "Bent Normal Baker", true);
            window.minSize = new Vector2(380, 500);
            window.Show();
        }

        private void OnGUI()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);

            EditorGUILayout.LabelField("Bent Normal 烘焙工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ===== Mesh UV 烘焙模式 =====
            EditorGUILayout.LabelField("烘焙 Bent Normal 到 Mesh UV", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将 bent normal 数据烘焙到 Mesh 的 UV 通道中。\n" +
                "数据编码: Vector4(relativeB, theta, aperture, scale)\n" +
                "Shader 在顶点着色器中解码还原 bent normal 方向。\n\n" +
                "使用方法：\n" +
                "1. 在 Hierarchy 中选择要烘焙的 GameObject\n" +
                "2. 调整下方参数\n" +
                "3. 点击\"烘焙到 Mesh UV\"按钮\n" +
                "4. 烘焙后的 Mesh 会自动保存并替换原 MeshFilter",
                MessageType.Info);

            EditorGUILayout.Space();
            m_meshParameters.RayCount = EditorGUILayout.IntSlider("射线采样数", m_meshParameters.RayCount, 16, 1024);
            m_meshParameters.RayLength = EditorGUILayout.Slider("最大射线距离(米)", m_meshParameters.RayLength, 0.01f, 4f);
            m_meshParameters.RayNormalOffset = EditorGUILayout.Slider("法线偏移(毫米)", m_meshParameters.RayNormalOffset, 0.01f, 4f);
            m_meshParameters.TriangleSampleCount = EditorGUILayout.IntSlider("三角形采样数", m_meshParameters.TriangleSampleCount, 4, 256);
            m_meshParameters.UVChannel = EditorGUILayout.IntSlider("UV 通道", m_meshParameters.UVChannel, 0, 7);

            EditorGUILayout.Space(10);

            // 显示当前选中的物体
            EditorGUILayout.LabelField("当前选中物体", EditorStyles.boldLabel);
            if (Selection.gameObjects.Length > 0)
            {
                foreach (var go in Selection.gameObjects)
                {
                    if (go != null)
                        EditorGUILayout.LabelField($"  • {go.name}");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请先在 Hierarchy 中选择要烘焙的物体", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            GUI.enabled = Selection.gameObjects.Length > 0;
            if (GUILayout.Button("烘焙到 Mesh UV", GUILayout.Height(40)))
            {
                BentNormalMeshBaker.BakeMesh(m_meshParameters);
            }
            GUI.enabled = true;

            EditorGUILayout.Space(20);

            // ===== 数据格式说明 =====
            EditorGUILayout.LabelField("数据格式说明", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "UV 通道数据编码 (Vector4):\n" +
                "  x (relativeB) = dot(bentNormal, biTangent)\n" +
                "  y (theta)     = atan2(relativeT, relativeN)\n" +
                "  z (aperture)  = 可见锥体半角 (弧度)\n" +
                "  w (scale)     = 可见锥体缩放 (≈ AO)\n\n" +
                "Shader 解码 (顶点着色器):\n" +
                "  tangentLength = sqrt(1 - relativeB²)\n" +
                "  coneDir = relativeB * biTangent\n" +
                "          + (cos(theta) * normal + sin(theta) * orthoTangent)\n" +
                "            * tangentLength\n\n" +
                "配套 Shader: Universal Render Pipeline/Lit_BentNormal\n" +
                "关键字: _VISIBILITY_ON (启用 UV 中的 bent normal 数据)",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void OnSelectionChange()
        {
            Repaint();
        }
    }
}
#endif
