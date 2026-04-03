using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 平滑法线烘焙工具：
/// 使用角度加权算法计算模型的平滑法线，转换到切线空间后存入 UV3(TEXCOORD3).xyz 中（3通道编码）。
/// 
/// ========== UV 通道分配约定 ==========
///   UV0 (TEXCOORD0) : 主纹理坐标
///   UV1 (TEXCOORD1) : Lightmap / 自定义数据
///   UV2 (TEXCOORD2) : Bent Normal 数据 (由 Bent Normal Baker 写入)
///   UV3 (TEXCOORD3) : 平滑法线 (由本工具写入，供 PBRToon 描边使用)
/// =====================================
/// 
/// 算法说明（参考 Best-Smooth-Normal-Tool）：
///   1. 遍历所有三角面，按顶点位置分组
///   2. 以当前顶点在三角面中的夹角作为权重，累加面法线
///   3. 归一化得到对象空间平滑法线
///   4. 将对象空间平滑法线转换到切线空间（使用 TBN 矩阵的转置）
///   5. 将切线空间平滑法线的 xyz 存入 UV3
/// 
/// 编码方式：
///   TEXCOORD3.xyz = tangentSpaceSmoothNormal.xyz
/// 
/// 在 Shader 中解码：
///   float3 snTS = uv3.xyz;  // 切线空间平滑法线
///   float3 smoothNormalOS = snTS.x * tangentOS.xyz + snTS.y * bitangentOS + snTS.z * normalOS;
///   即用 TBN 矩阵将切线空间法线还原到对象空间
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

    /// <summary>
    /// 带权重的法线结构
    /// </summary>
    private struct WeightedNormal
    {
        public Vector3 normal;
        public float weight;
    }

    // ========== UI 状态 ==========

    /// <summary>
    /// 资源列表项
    /// </summary>
    private class MeshEntry
    {
        public Object sourceObject;     // 源对象（GameObject / Mesh / Model 文件）
        public Mesh mesh;               // 实际的 Mesh 引用
        public string displayName;      // 显示名称
        public string assetPath;        // 资源路径
        public bool selected = true;    // 是否勾选
        public string status;           // 状态文本（如 "需要烘焙"、"已有 _SmoothN"）
        public MeshEntrySource entrySource; // 来源类型
    }

    private enum MeshEntrySource
    {
        Hierarchy,  // 来自 Hierarchy 选中的 GameObject
        Project,    // 来自 Project 选中的资源
    }

    private List<MeshEntry> meshEntries = new List<MeshEntry>();
    private Vector2 scrollPosition;
    private bool showHelp = false;
    private bool autoTrackSelection = true; // 是否自动跟踪选中对象

    [MenuItem("nTools/美术工具/平滑法线烘焙", false, 54)]
    public static void ShowWindow()
    {
        var win = GetWindow<SmoothNormalBaker>("平滑法线烘焙工具");
        win.minSize = new Vector2(420, 400);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        // 初始化时同步一次当前选中
        SyncFromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    /// <summary>
    /// Unity 选中对象变化时自动同步列表
    /// </summary>
    private void OnSelectionChanged()
    {
        if (autoTrackSelection)
        {
            SyncFromSelection();
            Repaint();
        }
    }

    private void OnGUI()
    {
        // ====== 标题栏 ======
        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("平滑法线烘焙工具", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(showHelp ? "收起说明 ▲" : "展开说明 ▼", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            showHelp = !showHelp;
        }
        EditorGUILayout.EndHorizontal();

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "将模型的平滑法线（角度加权 + 切线空间）固定烘焙到 UV3 (TEXCOORD3).xyz 中。\n\n" +
                "UV 通道分配约定：\n" +
                "  UV2 (TEXCOORD2) → Bent Normal 数据\n" +
                "  UV3 (TEXCOORD3) → 平滑法线（本工具写入）\n\n" +
                "算法：角度加权平滑法线 → 转换到切线空间 → 存入 UV3.xyz\n\n" +
                "Shader 解码：用 TBN 矩阵将切线空间法线还原到对象空间",
                MessageType.Info);
        }

        GUILayout.Space(4);

        // ====== 拖拽区域 + 操作栏 ======
        DrawDragDropArea();

        GUILayout.Space(2);

        // 操作栏：自动跟踪开关 + 清空
        EditorGUILayout.BeginHorizontal();
        autoTrackSelection = EditorGUILayout.ToggleLeft("自动识别选中", autoTrackSelection, GUILayout.Width(110));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清空全部", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            meshEntries.Clear();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ====== 列表标题栏 ======
        int totalCount = meshEntries.Count;
        int selectedCount = meshEntries.Count(e => e.selected);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label($"Mesh 列表 (共 {totalCount} 个, 已选 {selectedCount} 个)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // ====== 全选 / 取消全选 ======
        if (totalCount > 0)
        {
            EditorGUILayout.BeginHorizontal();
            bool allSelected = meshEntries.All(e => e.selected);
            bool newAllSelected = EditorGUILayout.ToggleLeft("全选 / 取消全选", allSelected, EditorStyles.miniLabel);
            if (newAllSelected != allSelected)
            {
                foreach (var entry in meshEntries)
                    entry.selected = newAllSelected;
            }
            EditorGUILayout.EndHorizontal();
        }

        // ====== 可滚动资源列表 ======
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        if (totalCount == 0)
        {
            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("列表为空，请在 Hierarchy / Project 中选中对象，或拖入此处", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(20);
        }
        else
        {
            // 需要删除的项索引
            int removeIndex = -1;

            for (int i = 0; i < meshEntries.Count; i++)
            {
                var entry = meshEntries[i];
                DrawMeshEntryRow(entry, i, ref removeIndex);
            }

            if (removeIndex >= 0 && removeIndex < meshEntries.Count)
            {
                meshEntries.RemoveAt(removeIndex);
            }
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);

        // ====== 底部烘焙按钮 ======
        GUI.enabled = selectedCount > 0;
        if (GUILayout.Button($"▶ 烘焙选中的 {selectedCount} 个 Mesh 到 UV3", GUILayout.Height(36)))
        {
            BakeSelectedEntries();
        }
        GUI.enabled = true;

        GUILayout.Space(4);
    }

    /// <summary>
    /// 绘制单个 Mesh 条目行
    /// </summary>
    private void DrawMeshEntryRow(MeshEntry entry, int index, ref int removeIndex)
    {
        const float rowHeight = 22f;

        // 使用统一的背景色区分奇偶行
        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
        if (index % 2 == 0)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, 0.4f));
        }

        // 勾选框
        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16), GUILayout.Height(rowHeight));

        // 名称（可点击定位）
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(2, 2, 0, 0)
        };
        string sourceIcon = entry.entrySource == MeshEntrySource.Hierarchy ? "<color=#88ccff>[H]</color>" : "<color=#ffcc88>[P]</color>";
        string displayText = $"{sourceIcon} {entry.displayName}";

        if (GUILayout.Button(displayText, nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight)))
        {
            // 点击名称时 Ping 对应资源
            if (entry.mesh != null)
                EditorGUIUtility.PingObject(entry.mesh);
            else if (entry.sourceObject != null)
                EditorGUIUtility.PingObject(entry.sourceObject);
        }

        // 状态标签
        if (!string.IsNullOrEmpty(entry.status))
        {
            Color statusColor;
            if (entry.status.Contains("已完成"))
                statusColor = new Color(0.4f, 0.9f, 0.4f);
            else if (entry.status.Contains("已有"))
                statusColor = new Color(0.5f, 0.8f, 0.5f);
            else
                statusColor = new Color(1f, 0.8f, 0.4f);

            var statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = statusColor },
                padding = new RectOffset(0, 4, 0, 0)
            };
            GUILayout.Label(entry.status, statusStyle, GUILayout.Width(80), GUILayout.Height(rowHeight));
        }

        // 删除按钮
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(rowHeight)))
        {
            removeIndex = index;
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 自动同步当前选中对象到列表（替换模式，非追加）
    /// </summary>
    private void SyncFromSelection()
    {
        // 保留已烘焙完成的条目（状态为 "已完成 ✓" 的不清除）
        meshEntries.RemoveAll(e => e.status != "已完成 ✓");

        // 从 Hierarchy 选中的 GameObject 添加
        GameObject[] selectedGOs = Selection.gameObjects;
        if (selectedGOs != null)
        {
            foreach (var go in selectedGOs)
            {
                AddMeshesFromGameObject(go, MeshEntrySource.Hierarchy);
            }
        }

        // 从 Project 选中的资源添加
        Object[] selectedAssets = Selection.objects;
        if (selectedAssets != null)
        {
            foreach (var obj in selectedAssets)
            {
                if (obj is GameObject) continue; // 已在上面处理
                AddMeshesFromObject(obj, MeshEntrySource.Project);
            }
        }
    }

    /// <summary>
    /// 从 GameObject 提取 Mesh 并添加到列表（递归遍历所有子级）
    /// </summary>
    private void AddMeshesFromGameObject(GameObject go, MeshEntrySource source)
    {
        if (go == null) return;

        // 处理自身
        AddMeshesFromSingleGameObject(go, source);

        // 递归遍历所有子级
        foreach (Transform child in go.transform)
        {
            AddMeshesFromGameObject(child.gameObject, source);
        }
    }

    /// <summary>
    /// 从单个 GameObject 提取 Mesh（不递归）
    /// </summary>
    private void AddMeshesFromSingleGameObject(GameObject go, MeshEntrySource source)
    {
        if (go == null) return;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && !IsMeshAlreadyInList(mf.sharedMesh))
        {
            var entry = CreateEntryFromMesh(mf.sharedMesh, go, source);
            meshEntries.Add(entry);
        }

        SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null && !IsMeshAlreadyInList(smr.sharedMesh))
        {
            var entry = CreateEntryFromMesh(smr.sharedMesh, go, source);
            meshEntries.Add(entry);
        }
    }

    /// <summary>
    /// 从 Object（Mesh / 模型文件 / GameObject）提取 Mesh 并添加到列表
    /// </summary>
    private void AddMeshesFromObject(Object obj, MeshEntrySource source)
    {
        if (obj == null) return;

        if (obj is Mesh mesh)
        {
            if (!IsMeshAlreadyInList(mesh))
            {
                var entry = CreateEntryFromMesh(mesh, obj, source);
                meshEntries.Add(entry);
            }
        }
        else
        {
            // 检查是否为 FBX 等模型文件（包括 GameObject 类型的 FBX 根对象）
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null)
                {
                    Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var sub in subAssets)
                    {
                        if (sub is Mesh subMesh && !IsMeshAlreadyInList(subMesh))
                        {
                            var entry = CreateEntryFromMesh(subMesh, obj, source);
                            meshEntries.Add(entry);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 绘制拖拽放置区域
    /// </summary>
    private void DrawDragDropArea()
    {
        Rect dropRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));

        // 绘制虚线边框风格的拖拽区域
        var bgStyle = new GUIStyle("Box")
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal,
            fontSize = 11
        };
        bgStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        GUI.Box(dropRect, "将 GameObject / Mesh / FBX 拖拽到此处添加", bgStyle);

        // 处理拖拽事件
        Event evt = Event.current;
        if (dropRect.Contains(evt.mousePosition))
        {
            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use();
                    }
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    int addedCount = 0;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        int before = meshEntries.Count;
                        if (obj is GameObject go)
                        {
                            // 判断是否为 Project 中的资源（FBX 等模型文件拖入时是 GameObject）
                            string dragPath = AssetDatabase.GetAssetPath(go);
                            if (!string.IsNullOrEmpty(dragPath))
                            {
                                // 检查是否为模型文件（FBX/OBJ 等）
                                var importer = AssetImporter.GetAtPath(dragPath) as ModelImporter;
                                if (importer != null)
                                {
                                    // 是模型文件，提取所有子 Mesh
                                    AddMeshesFromObject(go, MeshEntrySource.Project);
                                }
                                else
                                {
                                    // 普通 Prefab 或场景中的 GameObject，递归遍历
                                    AddMeshesFromGameObject(go, MeshEntrySource.Hierarchy);
                                }
                            }
                            else
                            {
                                // 场景中的 GameObject（无 AssetPath），递归遍历
                                AddMeshesFromGameObject(go, MeshEntrySource.Hierarchy);
                            }
                        }
                        else
                        {
                            AddMeshesFromObject(obj, MeshEntrySource.Project);
                        }
                        addedCount += meshEntries.Count - before;
                    }
                    if (addedCount > 0)
                        Debug.Log($"[平滑法线烘焙] 拖入添加了 {addedCount} 个 Mesh。");
                    evt.Use();
                    break;
            }
        }
    }

    /// <summary>
    /// 检查 Mesh 是否已在列表中（避免重复添加）
    /// </summary>
    private bool IsMeshAlreadyInList(Mesh mesh)
    {
        foreach (var entry in meshEntries)
        {
            if (entry.mesh == mesh) return true;
        }
        return false;
    }

    /// <summary>
    /// 从 Mesh 创建列表条目
    /// </summary>
    private MeshEntry CreateEntryFromMesh(Mesh mesh, Object source, MeshEntrySource entrySource)
    {
        var entry = new MeshEntry();
        entry.mesh = mesh;
        entry.sourceObject = source;
        entry.displayName = mesh.name;
        entry.assetPath = AssetDatabase.GetAssetPath(mesh);
        entry.selected = true;
        entry.entrySource = entrySource;

        // 判断状态
        if (mesh.name.EndsWith(OutputSuffix))
        {
            entry.status = "已有 _SmoothN";
        }
        else
        {
            // 检查同目录下是否已有对应的 _SmoothN.asset
            string dir = !string.IsNullOrEmpty(entry.assetPath) ? Path.GetDirectoryName(entry.assetPath) : "";
            if (!string.IsNullOrEmpty(dir))
            {
                string smoothNPath = Path.Combine(dir, mesh.name + OutputSuffix + ".asset").Replace("\\", "/");
                if (File.Exists(smoothNPath))
                {
                    entry.status = "已有输出";
                }
                else
                {
                    entry.status = "需要烘焙";
                }
            }
            else
            {
                entry.status = "需要烘焙";
            }
        }

        return entry;
    }

    /// <summary>
    /// 烘焙列表中已勾选的条目
    /// </summary>
    private void BakeSelectedEntries()
    {
        var selectedEntries = meshEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0) return;

        int processed = 0;
        int total = selectedEntries.Count;

        for (int i = 0; i < selectedEntries.Count; i++)
        {
            var entry = selectedEntries[i];

            EditorUtility.DisplayProgressBar("平滑法线烘焙",
                $"正在处理 ({i + 1}/{total}): {entry.displayName}",
                (float)i / total);

            if (entry.entrySource == MeshEntrySource.Hierarchy && entry.sourceObject is GameObject go)
            {
                // Hierarchy 模式：烘焙后替换 GameObject 上的 Mesh 引用
                Mesh srcMesh = ResolveSourceMesh(entry.mesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh);
                if (newMesh != null)
                {
                    Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);

                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == entry.mesh)
                        mf.sharedMesh = savedMesh;

                    SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh == entry.mesh)
                        smr.sharedMesh = savedMesh;

                    Debug.Log($"[平滑法线烘焙] {go.name} → {AssetDatabase.GetAssetPath(savedMesh)} (UV3, 切线空间)");
                    entry.status = "已完成 ✓";
                    processed++;
                }
            }
            else
            {
                // Project 模式：直接烘焙 Mesh 资源
                Mesh srcMesh = ResolveSourceMesh(entry.mesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh);
                if (newMesh != null)
                {
                    Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                    Debug.Log($"[平滑法线烘焙] {entry.displayName} → {AssetDatabase.GetAssetPath(savedMesh)} (UV3, 切线空间)");
                    entry.status = "已完成 ✓";
                    processed++;
                }
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[平滑法线烘焙] 完成，共处理 {processed}/{total} 个 Mesh。");
        Repaint();
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
                Mesh srcMesh = ResolveSourceMesh(mf.sharedMesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh);
                if (newMesh != null)
                {
                    Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                    mf.sharedMesh = savedMesh;
                    Debug.Log($"[平滑法线烘焙] {go.name} → {AssetDatabase.GetAssetPath(savedMesh)} (UV3, 切线空间)");
                    processed++;
                }
            }

            // SkinnedMeshRenderer
            SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                Mesh srcMesh = ResolveSourceMesh(smr.sharedMesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh);
                if (newMesh != null)
                {
                    Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                    smr.sharedMesh = savedMesh;
                    Debug.Log($"[平滑法线烘焙] {go.name} → {AssetDatabase.GetAssetPath(savedMesh)} (UV3, 切线空间)");
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
                    Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                    Debug.Log($"[平滑法线烘焙] {srcMesh.name} → {AssetDatabase.GetAssetPath(savedMesh)} (UV3, 切线空间)");
                    processed++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[平滑法线烘焙] 完成，共处理 {processed} 个 Mesh。");
    }

    /// <summary>
    /// 如果当前 Mesh 已经是 _SmoothN 后缀的输出 Mesh，尝试找到原始源 Mesh（FBX 中的 Mesh）。
    /// 这样可以确保始终从原始数据烘焙，避免从空数据的旧 _SmoothN 资源重复烘焙。
    /// </summary>
    private Mesh ResolveSourceMesh(Mesh mesh)
    {
        if (mesh == null) return null;

        string meshName = mesh.name;
        if (!meshName.EndsWith(OutputSuffix)) return mesh;

        // 当前 Mesh 是 _SmoothN 输出，尝试找到原始 Mesh
        string originalName = meshName.Substring(0, meshName.Length - OutputSuffix.Length);
        string assetPath = AssetDatabase.GetAssetPath(mesh);
        string directory = !string.IsNullOrEmpty(assetPath) ? Path.GetDirectoryName(assetPath) : "";

        // 策略1：在同目录下搜索同名的 FBX/模型文件中的子 Mesh
        if (!string.IsNullOrEmpty(directory))
        {
            string[] guids = AssetDatabase.FindAssets(originalName, new[] { directory.Replace("\\", "/") });
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                // 跳过 _SmoothN.asset 自身
                if (candidatePath.EndsWith(OutputSuffix + ".asset")) continue;

                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(candidatePath);
                foreach (var sub in subAssets)
                {
                    if (sub is Mesh m && m.name == originalName)
                    {
                        Debug.Log($"[平滑法线烘焙] 找到原始 Mesh: {candidatePath} -> {m.name}");
                        return m;
                    }
                }
            }
        }

        Debug.LogWarning($"[平滑法线烘焙] 未找到 '{meshName}' 的原始 Mesh '{originalName}'，将使用当前 Mesh 作为源。");
        return mesh;
    }

    /// <summary>
    /// 核心算法：角度加权平滑法线 + 切线空间转换，写入 TEXCOORD3.xyz（3通道编码）
    /// 
    /// 算法说明（参考 Best-Smooth-Normal-Tool）：
    /// 1. 临时开启 Mesh 可读，提取所有原始数据（顶点、法线、切线、UV、骨骼等）
    /// 2. 遍历所有三角面，按顶点位置分组，以夹角为权重累加面法线
    /// 3. 归一化得到对象空间平滑法线
    /// 4. 用 TBN 矩阵的转置将对象空间平滑法线转换到切线空间
    /// 5. 将切线空间平滑法线 xyz 存入 UV3
    /// </summary>
    private Mesh BakeSmoothNormals(Mesh sourceMesh)
    {
        if (sourceMesh == null) return null;

        // ====== 第一阶段：确保源 Mesh 可读，并提取所有数据到 CPU 端变量 ======
        bool wasReadable = sourceMesh.isReadable;
        string assetPath = AssetDatabase.GetAssetPath(sourceMesh);
        string sourceMeshName = sourceMesh.name;
        ModelImporter modelImporter = null;

        if (!wasReadable && !string.IsNullOrEmpty(assetPath))
        {
            modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                Debug.Log($"[平滑法线烘焙] 源 Mesh 不可读，临时开启 Read/Write: {assetPath}");
                modelImporter.isReadable = true;
                modelImporter.SaveAndReimport();
                // 重新加载 Mesh（重新导入后引用可能变化）
                sourceMesh = null;
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var sub in subAssets)
                {
                    if (sub is Mesh m && m.name == sourceMeshName)
                    {
                        sourceMesh = m;
                        break;
                    }
                }
                if (sourceMesh == null)
                {
                    Debug.LogError($"[平滑法线烘焙] 重新导入后找不到 Mesh: {sourceMeshName}");
                    modelImporter.isReadable = false;
                    modelImporter.SaveAndReimport();
                    return null;
                }
            }
            else
            {
                Debug.Log($"[平滑法线烘焙] .asset 文件不可读，临时开启 Read/Write: {assetPath}");
                var so = new SerializedObject(sourceMesh);
                var prop = so.FindProperty("m_IsReadable");
                if (prop != null)
                {
                    prop.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                }
            }
        }

        // 从源 Mesh 提取所有需要的数据到 CPU 端本地变量
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector4[] tangents = sourceMesh.tangents;
        Vector2[] uv0 = sourceMesh.uv;
        Vector2[] uv1 = sourceMesh.uv2;
        Vector2[] uv2 = sourceMesh.uv3; // TEXCOORD2
        BoneWeight[] boneWeights = sourceMesh.boneWeights;
        Matrix4x4[] bindposes = sourceMesh.bindposes;
        int subMeshCount = sourceMesh.subMeshCount;

        // 提取所有子网格的三角形索引（合并为一个完整的三角形数组用于平滑法线计算）
        int[][] subMeshTriangles = new int[subMeshCount][];
        List<int> allTrianglesList = new List<int>();
        for (int s = 0; s < subMeshCount; s++)
        {
            subMeshTriangles[s] = sourceMesh.GetTriangles(s);
            allTrianglesList.AddRange(subMeshTriangles[s]);
        }
        int[] allTriangles = allTrianglesList.ToArray();

        // 提取其他可能存在的 UV 通道
        List<Vector2> existingUV4 = new List<Vector2>();
        sourceMesh.GetUVs(4, existingUV4);
        List<Vector2> existingUV5 = new List<Vector2>();
        sourceMesh.GetUVs(5, existingUV5);
        List<Vector2> existingUV6 = new List<Vector2>();
        sourceMesh.GetUVs(6, existingUV6);
        List<Vector2> existingUV7 = new List<Vector2>();
        sourceMesh.GetUVs(7, existingUV7);

        int vertexCount = vertices.Length;

        // 数据有效性检查
        Debug.Log($"[平滑法线烘焙] 源Mesh: {sourceMeshName}, 顶点数: {vertexCount}, 三角形索引数: {allTriangles.Length}, subMeshCount: {subMeshCount}");

        if (vertexCount == 0)
        {
            Debug.LogError($"[平滑法线烘焙] {sourceMeshName} 顶点数为 0，无法烘焙。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        if (allTriangles.Length == 0)
        {
            Debug.LogError($"[平滑法线烘焙] {sourceMeshName} 三角形索引数为 0，无法计算平滑法线。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        if (normals == null || normals.Length == 0)
        {
            Debug.LogError($"[平滑法线烘焙] {sourceMeshName} 没有法线数据，无法计算切线空间。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        if (tangents == null || tangents.Length == 0)
        {
            Debug.LogError($"[平滑法线烘焙] {sourceMeshName} 没有切线数据，无法计算切线空间。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        // ====== 第二阶段：角度加权平滑法线计算 ======
        // 按顶点位置分组，累加角度加权的面法线
        Dictionary<Vector3, List<WeightedNormal>> normalDict = new Dictionary<Vector3, List<WeightedNormal>>();

        for (int i = 0; i <= allTriangles.Length - 3; i += 3)
        {
            int idx0 = allTriangles[i];
            int idx1 = allTriangles[i + 1];
            int idx2 = allTriangles[i + 2];

            Vector3 v0 = vertices[idx0];
            Vector3 v1 = vertices[idx1];
            Vector3 v2 = vertices[idx2];

            // 三角形三个顶点的索引
            int[] triIndices = { idx0, idx1, idx2 };

            for (int j = 0; j < 3; j++)
            {
                int vertexIndex = triIndices[j];
                Vector3 vertex = vertices[vertexIndex];

                if (!normalDict.ContainsKey(vertex))
                {
                    normalDict.Add(vertex, new List<WeightedNormal>());
                }

                // 获取当前顶点出发的两条边
                Vector3 lineA, lineB;
                if (j == 0)
                {
                    lineA = v1 - v0;
                    lineB = v2 - v0;
                }
                else if (j == 1)
                {
                    lineA = v2 - v1;
                    lineB = v0 - v1;
                }
                else
                {
                    lineA = v0 - v2;
                    lineB = v1 - v2;
                }

                // 精度优化：放大边向量避免浮点精度问题
                // 参考: https://www.bilibili.com/read/cv27148724
                lineA *= 10000.0f;
                lineB *= 10000.0f;

                // 角度加权：以当前顶点在三角面中的夹角作为权重
                float dotAB = Vector3.Dot(lineA, lineB);
                float magProduct = lineA.magnitude * lineB.magnitude;
                float cosAngle = Mathf.Clamp(dotAB / magProduct, -1f, 1f);
                float angle = Mathf.Acos(cosAngle);

                // 面法线 = 两边叉积的归一化
                Vector3 faceNormal = Vector3.Cross(lineA, lineB).normalized;

                WeightedNormal wn;
                wn.normal = faceNormal;
                wn.weight = angle;
                normalDict[vertex].Add(wn);
            }
        }

        // ====== 第三阶段：归一化 + 转换到切线空间 ======
        Vector3[] smoothNormals = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 vertex = vertices[i];
            if (!normalDict.ContainsKey(vertex))
            {
                // 没有被任何三角面引用的顶点，使用原始法线
                smoothNormals[i] = normals[i];
                continue;
            }

            List<WeightedNormal> normalList = normalDict[vertex];

            // 计算权重总和
            float weightSum = 0f;
            for (int j = 0; j < normalList.Count; j++)
            {
                weightSum += normalList[j].weight;
            }

            // 加权累加法线
            Vector3 smoothNormal = Vector3.zero;
            for (int j = 0; j < normalList.Count; j++)
            {
                smoothNormal += normalList[j].normal * normalList[j].weight / weightSum;
            }

            // 归一化得到对象空间平滑法线
            smoothNormal = smoothNormal.normalized;

            // 将对象空间平滑法线转换到切线空间
            // TBN 矩阵: T(切线), B(副切线), N(法线) 构成正交基
            // 切线空间法线 = TBN^T * objectSpaceNormal
            Vector4 T = tangents[i];
            Vector3 N = normals[i];
            Vector3 B = (Vector3.Cross(N, new Vector3(T.x, T.y, T.z)) * T.w).normalized;

            // 构建 TBN 矩阵（列向量为 T, B, N）
            Matrix4x4 TBN = new Matrix4x4(
                new Vector4(T.x, T.y, T.z, 0),
                new Vector4(B.x, B.y, B.z, 0),
                new Vector4(N.x, N.y, N.z, 0),
                new Vector4(0, 0, 0, 1)
            );
            // 转置后乘以平滑法线 = 将对象空间法线投影到切线空间
            TBN = TBN.transpose;
            smoothNormals[i] = TBN.MultiplyVector(smoothNormal).normalized;
        }

        // ====== 第四阶段：编码平滑法线到 UV3（3通道，切线空间） ======
        Vector3[] uvData = new Vector3[vertexCount];
        int nonZeroCount = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            uvData[i] = smoothNormals[i];
            if (uvData[i].sqrMagnitude > 0.0001f)
                nonZeroCount++;
        }

        Debug.Log($"[平滑法线烘焙] 平滑法线统计: 总顶点={vertexCount}, 非零UV3(xyz)={nonZeroCount}");

        if (nonZeroCount == 0)
        {
            Debug.LogError($"[平滑法线烘焙] 所有 UV3 数据均为零！平滑法线计算失败。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        // ====== 数据已全部提取完毕，恢复 isReadable ======
        RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);

        // ====== 第五阶段：构建最终输出 Mesh ======
        Mesh mesh = new Mesh();
        mesh.name = sourceMeshName.Replace(OutputSuffix, "") + OutputSuffix;

        if (vertexCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        // 保留原始法线和切线
        if (normals != null && normals.Length > 0) mesh.normals = normals;
        if (tangents != null && tangents.Length > 0) mesh.tangents = tangents;
        if (uv0 != null && uv0.Length > 0) mesh.uv = uv0;
        if (uv1 != null && uv1.Length > 0) mesh.uv2 = uv1;
        if (uv2 != null && uv2.Length > 0) mesh.uv3 = uv2;
        if (boneWeights != null && boneWeights.Length > 0) mesh.boneWeights = boneWeights;
        if (bindposes != null && bindposes.Length > 0) mesh.bindposes = bindposes;

        // 设置子网格
        mesh.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++)
        {
            mesh.SetTriangles(subMeshTriangles[s], s);
        }

        // 恢复其他 UV 通道（UV4~UV7）
        if (existingUV4.Count > 0) mesh.SetUVs(4, existingUV4);
        if (existingUV5.Count > 0) mesh.SetUVs(5, existingUV5);
        if (existingUV6.Count > 0) mesh.SetUVs(6, existingUV6);
        if (existingUV7.Count > 0) mesh.SetUVs(7, existingUV7);

        // 写入切线空间平滑法线到 UV3（3通道 xyz）
        mesh.SetUVs(TargetUVChannel, uvData);

        // 验证写入是否成功
        List<Vector3> verifyUV = new List<Vector3>();
        mesh.GetUVs(TargetUVChannel, verifyUV);
        if (verifyUV.Count == 0)
        {
            Debug.LogError($"[平滑法线烘焙] SetUVs 后验证失败：UV3 通道为空！");
        }
        else
        {
            int verifyNonZero = 0;
            for (int i = 0; i < Mathf.Min(verifyUV.Count, 100); i++)
            {
                if (verifyUV[i].sqrMagnitude > 0.0001f)
                    verifyNonZero++;
            }
            Debug.Log($"[平滑法线烘焙] SetUVs 验证: UV3 通道有 {verifyUV.Count} 个值, 前100个中非零={verifyNonZero}");
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 恢复 Mesh 的 isReadable 状态
    /// </summary>
    private void RestoreReadable(bool wasReadable, ModelImporter modelImporter, Mesh sourceMesh, string assetPath)
    {
        if (wasReadable) return;

        if (modelImporter != null)
        {
            modelImporter.isReadable = false;
            modelImporter.SaveAndReimport();
        }
        else if (!string.IsNullOrEmpty(assetPath))
        {
            var so = new SerializedObject(sourceMesh);
            var prop = so.FindProperty("m_IsReadable");
            if (prop != null)
            {
                prop.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }
        }
    }

    /// <summary>
    /// 将 Mesh 保存为 .asset 文件，返回磁盘上持久化的 Mesh 引用
    /// </summary>
    private Mesh SaveMeshAsset(Mesh originalMesh, Mesh newMesh)
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

        // 防止后缀重复：如果 baseName 已经包含 OutputSuffix，先移除
        if (baseName.EndsWith(OutputSuffix))
        {
            baseName = baseName.Substring(0, baseName.Length - OutputSuffix.Length);
        }

        string outputPath = Path.Combine(directory, baseName + OutputSuffix + ".asset").Replace("\\", "/");

        Debug.Log($"[平滑法线烘焙] 保存路径: {outputPath}");

        // 检查是否已存在
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
        if (existing != null)
        {
            // 覆盖已有资源，将数据拷贝到持久化对象上
            EditorUtility.CopySerialized(newMesh, existing);
            AssetDatabase.SaveAssets();
            // 销毁临时的 newMesh，返回磁盘上的持久化引用
            Object.DestroyImmediate(newMesh);
            return existing;
        }

        AssetDatabase.CreateAsset(newMesh, outputPath);
        return newMesh;
    }
}
