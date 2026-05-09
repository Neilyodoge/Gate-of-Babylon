using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Outline 平滑法线烘焙工具：
/// 使用角度加权算法计算模型的平滑法线，转换到切线空间后存入指定 UV 通道（默认 UV3）的 xyz 中，
/// 主要供 PBRToon 等卡通描边方案使用（背面法线外扩描边法）。
///
/// ========== UV 通道分配约定（默认） ==========
///   UV0 (TEXCOORD0) : 主纹理坐标
///   UV1 (TEXCOORD1) : Lightmap / 自定义数据
///   UV2 (TEXCOORD2) : Bent Normal 数据 (由 Bent Normal Baker 写入)
///   UV3 (TEXCOORD3) : 平滑法线 (由本工具写入，供 PBRToon 描边使用)
/// 工具内可选写入到 UV0~UV7 任意通道。
/// =====================================
///
/// 算法说明（参考 Best-Smooth-Normal-Tool）：
///   1. 遍历所有三角面，按顶点位置分组
///   2. 以当前顶点在三角面中的夹角作为权重，累加面法线
///   3. 归一化得到对象空间平滑法线
///   4. 将对象空间平滑法线转换到切线空间（使用 TBN 矩阵的转置）
///   5. 将切线空间平滑法线的 xyz 存入选择的 UV 通道
///
/// 编码方式：
///   TEXCOORDn.xyz = tangentSpaceSmoothNormal.xyz
///
/// 在 Shader 中解码：
///   float3 snTS = uvN.xyz;  // 切线空间平滑法线
///   float3 smoothNormalOS = snTS.x * tangentOS.xyz + snTS.y * bitangentOS + snTS.z * normalOS;
///
/// 输出策略：
///   · 始终在源 Mesh 同目录生成 _SmoothN.asset（不会修改原始 .asset 或 FBX 文件）
///   · [H] (Hierarchy): 烘焙后会自动把 GameObject 的 MeshFilter / SkinnedMeshRenderer 引用
///     替换为新生成的 _SmoothN.asset
///   · [P] (Project)  : 仅生成 _SmoothN.asset，不修改场景中的引用
///
/// 还原功能（↺ 用原始资源替换）：
///   · 仅作用于 [H] 条目；将场景对象当前引用的 _SmoothN Mesh 替换回原始 Mesh（不带后缀）
///   · 不会修改 .asset 文件本身，只更新场景 / Prefab 上的引用
/// </summary>
public class SmoothNormalBaker : EditorWindow
{
    private const string OutputSuffix = "_SmoothN";

    /// <summary>
    /// 带权重的法线结构
    /// </summary>
    private struct WeightedNormal
    {
        public Vector3 normal;
        public float weight;
    }

    /// <summary>
    /// 通用 UV 通道数据缓存（保留原有维度：2D / 3D / 4D）
    /// </summary>
    private struct UVChannelData
    {
        public int dimension; // 0 = 无数据，2 / 3 / 4
        public List<Vector2> uv2;
        public List<Vector3> uv3;
        public List<Vector4> uv4;
        public bool HasData =>
            (uv2 != null && uv2.Count > 0) ||
            (uv3 != null && uv3.Count > 0) ||
            (uv4 != null && uv4.Count > 0);
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

    // 写入的 UV 通道（0~7，默认 UV3 = TEXCOORD3）
    private int targetUVChannel = 3;
    // 还原时是否同步删除被替换掉的 _SmoothN.asset 文件，默认关闭以防误删
    private bool deleteSmoothNAssetOnRestore = false;

    private List<MeshEntry> meshEntries = new List<MeshEntry>();
    private Vector2 scrollPosition;
    private bool showHelp = false;
    private bool autoTrackSelection = true; // 是否自动跟踪选中对象

    private const string WindowTitle = "Outline平滑法线烘焙";

    [MenuItem("nTools/美术工具/Outline平滑法线烘焙", false, 54)]
    public static void ShowWindow()
    {
        var win = GetWindow<SmoothNormalBaker>(WindowTitle);
        win.minSize = new Vector2(440, 460);
    }

    private void OnEnable()
    {
        // 强制刷新 titleContent；防止旧布局缓存里的旧标题（如"平滑法线烘焙工具"）残留
        titleContent = new GUIContent(WindowTitle);
        Selection.selectionChanged += OnSelectionChanged;
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
        GUILayout.Label("Outline 平滑法线烘焙", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ====== 拖拽区域 ======
        DrawDragDropArea();

        GUILayout.Space(4);

        // ====== 设置区 ======
        DrawSettingsArea();

        GUILayout.Space(2);

        // 操作栏：清空 / 自动跟踪
        EditorGUILayout.BeginHorizontal();
        autoTrackSelection = EditorGUILayout.ToggleLeft(
            new GUIContent("自动识别选中", "勾选后，Unity 中选择对象会自动同步到列表。"),
            autoTrackSelection,
            GUILayout.Width(110));
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
        int warningCount = meshEntries.Count(e => e.selected && MeshHasUVData(e.mesh, targetUVChannel));

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        string toolbarText = $"Mesh 列表 (共 {totalCount} 个, 已选 {selectedCount} 个";
        if (warningCount > 0) toolbarText += $", ⚠ {warningCount} 个目标 UV 已有数据";
        toolbarText += ")";
        GUILayout.Label(toolbarText, EditorStyles.miniLabel);
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
            int removeIndex = -1;

            for (int i = 0; i < meshEntries.Count; i++)
            {
                DrawMeshEntryRow(meshEntries[i], i, ref removeIndex);
            }

            if (removeIndex >= 0 && removeIndex < meshEntries.Count)
            {
                meshEntries.RemoveAt(removeIndex);
            }
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(4);

        // ====== 底部按钮：烘焙 / 还原 ======
        GUI.enabled = selectedCount > 0;
        string bakeBtnLabel = $"▶ 烘焙选中的 {selectedCount} 个 Mesh 到 UV{targetUVChannel}";
        if (warningCount > 0) bakeBtnLabel += $"  (⚠ 将覆盖 {warningCount} 个已有数据)";
        if (GUILayout.Button(bakeBtnLabel, GUILayout.Height(36)))
        {
            BakeSelectedEntries();
        }

        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        var restoreBtnContent = new GUIContent(
            $"↺ 用原始资源替换选中的 {selectedCount} 个",
            "把选中的场景对象当前引用的 mesh_SmoothN 还原成原始 mesh（去掉 _SmoothN 后缀的同名资源）。\n" +
            "仅修改场景 / Prefab 上的引用，不会动 .asset 文件。\n" +
            "对 [P] Project 条目和已经是原始资源的条目，会自动跳过。");
        if (GUILayout.Button(restoreBtnContent, GUILayout.ExpandWidth(true), GUILayout.Height(26)))
        {
            RestoreSelectedToOriginal();
        }

        deleteSmoothNAssetOnRestore = EditorGUILayout.ToggleLeft(
            new GUIContent("删除原始资源",
                "勾选后：还原成功的条目，会连带从硬盘上删除被替换掉的 _SmoothN.asset 文件。\n" +
                "为防误删，仅当工具列表中没有其它条目仍引用该资源时才会真正删除。\n" +
                "默认不勾选，仅断开场景引用、保留 _SmoothN.asset。"),
            deleteSmoothNAssetOnRestore,
            GUILayout.Width(110), GUILayout.Height(26));

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        GUILayout.Space(4);

        // ====== 使用说明（折叠在最下方） ======
        DrawHelpFooter();
    }

    /// <summary>
    /// 绘制顶部设置区：UV 通道、替换原文件
    /// </summary>
    private void DrawSettingsArea()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("烘焙设置", EditorStyles.miniBoldLabel);

        // UV 通道选择
        GUIContent[] channelOptions =
        {
            new GUIContent("UV0 (TEXCOORD0)"),
            new GUIContent("UV1 (TEXCOORD1)"),
            new GUIContent("UV2 (TEXCOORD2)"),
            new GUIContent("UV3 (TEXCOORD3)  [推荐]"),
            new GUIContent("UV4 (TEXCOORD4)"),
            new GUIContent("UV5 (TEXCOORD5)"),
            new GUIContent("UV6 (TEXCOORD6)"),
            new GUIContent("UV7 (TEXCOORD7)"),
        };
        int[] channelValues = { 0, 1, 2, 3, 4, 5, 6, 7 };

        targetUVChannel = EditorGUILayout.IntPopup(
            new GUIContent("写入 UV 通道",
                "选择平滑法线写入到哪个 UV 通道（TEXCOORD0~7）。\n" +
                "默认 UV3，配合 PBRToon 描边使用。\n" +
                "若选中通道在某些 Mesh 上已有数据，将在列表中标注 ⚠。"),
            targetUVChannel,
            channelOptions,
            channelValues);
        targetUVChannel = Mathf.Clamp(targetUVChannel, 0, 7);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 底部使用说明
    /// </summary>
    private void DrawHelpFooter()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(showHelp ? "收起使用说明 ▲" : "展开使用说明 ▼", EditorStyles.miniButton))
        {
            showHelp = !showHelp;
        }
        EditorGUILayout.EndHorizontal();

        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "将模型的平滑法线（角度加权 + 切线空间）烘焙到指定 UV 通道的 xyz 中。\n\n" +
                "[添加 Mesh]\n" +
                "  · 在 Hierarchy / Project 中选中对象会自动加入列表\n" +
                "  · 也可将 GameObject / Mesh / FBX 拖到上方拖拽区\n" +
                "  · [H] = 来自场景对象，[P] = 来自 Project 资源\n\n" +
                "[输出策略]\n" +
                "  · 始终在源 Mesh 同目录生成 _SmoothN.asset，不修改任何原文件\n" +
                "  · [H] 烘焙后会自动把场景对象的 MeshFilter / SkinnedMeshRenderer 引用换为新 _SmoothN\n" +
                "  · [P] 仅生成 _SmoothN.asset，不动场景\n\n" +
                "[还原原始资源 ↺]\n" +
                "  · 仅作用于 [H] 条目（场景对象）\n" +
                "  · 将当前引用的 mesh_SmoothN 替换回原始 mesh（去掉 _SmoothN 后缀的同名资源）\n" +
                "  · 在同/上级目录、最后回退到全工程依次搜索原始 Mesh\n" +
                "  · 默认仅改场景 / Prefab 上的引用，不动 .asset 文件\n" +
                "  · 勾选 \"删除原始资源\" 会在还原成功后顺手删掉对应的 _SmoothN.asset（前提是工具列表里没人再引用它）\n\n" +
                "[UV 通道]\n" +
                "  · 默认 UV3 (TEXCOORD3)，可选 UV0~UV7\n" +
                "  · 若目标通道在某些 Mesh 上已有数据，列表中会显示 ⚠（仍可烘焙，会覆盖该通道）\n\n" +
                "[算法]\n" +
                "  角度加权平滑法线 → 转换到切线空间 → 写入 UVN.xyz\n\n" +
                "[Shader 解码]\n" +
                "  float3 snTS = uvN.xyz;\n" +
                "  float3 smoothNormalOS = snTS.x*tangentOS + snTS.y*bitangentOS + snTS.z*normalOS;",
                MessageType.Info);
        }
    }

    /// <summary>
    /// 绘制单个 Mesh 条目行
    /// </summary>
    private void DrawMeshEntryRow(MeshEntry entry, int index, ref int removeIndex)
    {
        const float rowHeight = 22f;

        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
        if (index % 2 == 0)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, 0.4f));
        }

        // 勾选框
        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16), GUILayout.Height(rowHeight));

        // UV 占用警告（实时检测，不缓存）
        bool hasUVConflict = MeshHasUVData(entry.mesh, targetUVChannel);
        string warningTip = hasUVConflict
            ? $"该 Mesh 的 UV{targetUVChannel} 已存在数据，烘焙将覆盖原有数据。"
            : null;

        // 名称（可点击定位）
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(2, 2, 0, 0)
        };
        string sourceIcon = entry.entrySource == MeshEntrySource.Hierarchy
            ? "<color=#88ccff>[H]</color>"
            : "<color=#ffcc88>[P]</color>";
        string warnIcon = hasUVConflict ? "<color=#ffaa44>⚠</color> " : "";
        string displayText = $"{sourceIcon} {warnIcon}{entry.displayName}";

        if (GUILayout.Button(new GUIContent(displayText, warningTip),
                nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight)))
        {
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
    /// 检测 Mesh 在指定 UV 通道是否已有数据
    /// </summary>
    private static bool MeshHasUVData(Mesh mesh, int channel)
    {
        if (mesh == null || channel < 0 || channel > 7) return false;
        VertexAttribute attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
        return mesh.HasVertexAttribute(attr);
    }

    /// <summary>
    /// 自动同步当前选中对象到列表（替换模式，非追加）
    /// </summary>
    private void SyncFromSelection()
    {
        // 保留已烘焙完成的条目
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

        AddMeshesFromSingleGameObject(go, source);

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

        var bgStyle = new GUIStyle("Box")
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal,
            fontSize = 11
        };
        bgStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        GUI.Box(dropRect, "将 GameObject / Mesh / FBX 拖拽到此处添加", bgStyle);

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
                            string dragPath = AssetDatabase.GetAssetPath(go);
                            if (!string.IsNullOrEmpty(dragPath))
                            {
                                var importer = AssetImporter.GetAtPath(dragPath) as ModelImporter;
                                if (importer != null)
                                {
                                    AddMeshesFromObject(go, MeshEntrySource.Project);
                                }
                                else
                                {
                                    AddMeshesFromGameObject(go, MeshEntrySource.Hierarchy);
                                }
                            }
                            else
                            {
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

        if (mesh.name.EndsWith(OutputSuffix))
        {
            entry.status = "已有 _SmoothN";
        }
        else
        {
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

        try
        {
            for (int i = 0; i < selectedEntries.Count; i++)
            {
                var entry = selectedEntries[i];

                EditorUtility.DisplayProgressBar("平滑法线烘焙",
                    $"正在处理 ({i + 1}/{total}): {entry.displayName}",
                    (float)i / total);

                Mesh srcMesh = ResolveSourceMesh(entry.mesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh, targetUVChannel);
                if (newMesh == null) continue;

                Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                if (savedMesh == null) continue;

                // [H] 模式：把场景对象的 Mesh 引用换成新生成的 _SmoothN.asset
                if (entry.entrySource == MeshEntrySource.Hierarchy
                    && entry.sourceObject is GameObject go)
                {
                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == entry.mesh)
                    {
                        Undo.RecordObject(mf, "Bake Smooth Normals");
                        mf.sharedMesh = savedMesh;
                        EditorUtility.SetDirty(mf);
                    }

                    SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh == entry.mesh)
                    {
                        Undo.RecordObject(smr, "Bake Smooth Normals");
                        smr.sharedMesh = savedMesh;
                        EditorUtility.SetDirty(smr);
                    }
                }

                // 同步更新 entry，让后续重复烘焙能命中正确的对象
                entry.mesh = savedMesh;
                entry.assetPath = AssetDatabase.GetAssetPath(savedMesh);
                entry.displayName = savedMesh.name;

                string outPath = AssetDatabase.GetAssetPath(savedMesh);
                Debug.Log($"[平滑法线烘焙] {entry.displayName} → {outPath} (UV{targetUVChannel}, 切线空间)");

                entry.status = "已完成 ✓";
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[平滑法线烘焙] 完成，共处理 {processed}/{total} 个 Mesh。");
        Repaint();
    }

    /// <summary>
    /// 把选中的场景对象当前引用的 mesh_SmoothN 还原为原始 mesh（去掉 _SmoothN 后缀的同名资源）。
    /// 仅修改场景 / Prefab 上的 MeshFilter / SkinnedMeshRenderer 引用，不会修改任何 .asset 文件。
    /// </summary>
    private void RestoreSelectedToOriginal()
    {
        var selectedEntries = meshEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0) return;

        int processed = 0;
        int skipped = 0;
        int total = selectedEntries.Count;

        // 收集本次还原中被替换掉的 _SmoothN.asset 路径，在循环结束后统一检查 / 删除
        var disconnectedAssetPaths = new HashSet<string>();

        try
        {
            for (int i = 0; i < total; i++)
            {
                var entry = selectedEntries[i];
                if (entry.mesh == null) { skipped++; continue; }

                EditorUtility.DisplayProgressBar("用原始资源替换",
                    $"正在处理 ({i + 1}/{total}): {entry.displayName}",
                    (float)i / total);

                // 仅作用于场景对象
                if (entry.entrySource != MeshEntrySource.Hierarchy
                    || !(entry.sourceObject is GameObject go))
                {
                    Debug.Log($"[平滑法线烘焙] {entry.displayName} 不是场景对象（[P] 项无场景引用可换），跳过。");
                    skipped++;
                    continue;
                }

                // 仅处理名称带 _SmoothN 后缀的 Mesh
                if (!entry.mesh.name.EndsWith(OutputSuffix))
                {
                    Debug.Log($"[平滑法线烘焙] {entry.displayName} 已经是非 _SmoothN 资源，无需还原。");
                    skipped++;
                    continue;
                }

                Mesh originalMesh = FindOriginalMesh(entry.mesh);
                if (originalMesh == null || originalMesh == entry.mesh)
                {
                    Debug.LogWarning($"[平滑法线烘焙] 未找到 {entry.displayName} 对应的原始 Mesh，跳过。");
                    skipped++;
                    continue;
                }

                // 在改写引用之前，记录旧 _SmoothN.asset 的磁盘路径
                string oldAssetPath = AssetDatabase.GetAssetPath(entry.mesh);

                // 仅替换场景 / Prefab 上的引用，不动任何资产
                bool changed = false;
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == entry.mesh)
                {
                    Undo.RecordObject(mf, "Restore Original Mesh");
                    mf.sharedMesh = originalMesh;
                    EditorUtility.SetDirty(mf);
                    changed = true;
                }

                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh == entry.mesh)
                {
                    Undo.RecordObject(smr, "Restore Original Mesh");
                    smr.sharedMesh = originalMesh;
                    EditorUtility.SetDirty(smr);
                    changed = true;
                }

                if (!changed)
                {
                    Debug.LogWarning($"[平滑法线烘焙] {go.name} 上未找到引用 {entry.mesh.name} 的渲染组件，跳过。");
                    skipped++;
                    continue;
                }

                Debug.Log($"[平滑法线烘焙] {go.name}: {entry.mesh.name} → {originalMesh.name} (原始来源: {AssetDatabase.GetAssetPath(originalMesh)})");

                if (deleteSmoothNAssetOnRestore
                    && !string.IsNullOrEmpty(oldAssetPath)
                    && oldAssetPath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    disconnectedAssetPaths.Add(oldAssetPath);
                }

                entry.mesh = originalMesh;
                entry.assetPath = AssetDatabase.GetAssetPath(originalMesh);
                entry.displayName = originalMesh.name;
                entry.status = "已还原 ↺";
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // 统一处理删除：仅当工具列表中没有其它条目仍引用该资源时才删除
        int deleted = 0, kept = 0;
        if (deleteSmoothNAssetOnRestore && disconnectedAssetPaths.Count > 0)
        {
            foreach (var path in disconnectedAssetPaths)
            {
                bool stillReferenced = meshEntries.Any(e =>
                    e.mesh != null
                    && string.Equals(AssetDatabase.GetAssetPath(e.mesh), path, System.StringComparison.OrdinalIgnoreCase));

                if (stillReferenced)
                {
                    Debug.LogWarning($"[平滑法线烘焙] {path} 仍被工具列表中其它条目引用，未删除。");
                    kept++;
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path))
                {
                    Debug.Log($"[平滑法线烘焙] 已删除被替换的 _SmoothN: {path}");
                    deleted++;
                }
                else
                {
                    Debug.LogWarning($"[平滑法线烘焙] 删除失败: {path}");
                }
            }
            AssetDatabase.Refresh();
        }

        string summary = $"[平滑法线烘焙] 还原完成: {processed} 个成功, {skipped} 个跳过 / 共 {total} 个。";
        if (deleteSmoothNAssetOnRestore && disconnectedAssetPaths.Count > 0)
        {
            summary += $" 删除 _SmoothN.asset: {deleted} 个" + (kept > 0 ? $"，保留 {kept} 个（仍被引用）" : "");
        }
        Debug.Log(summary);
        Repaint();
    }

    /// <summary>
    /// 查找带 _SmoothN 后缀的 Mesh 对应的原始 Mesh（不带后缀的同名资源）。
    /// 搜索顺序：所在目录 → 上一级目录 → 全工程；优先返回 FBX 等 Model 子 Mesh，
    /// 其次是任意带该名称的 Mesh。
    /// 若 Mesh 名称不带 _SmoothN 后缀，返回 null。
    /// </summary>
    private Mesh FindOriginalMesh(Mesh smoothMesh)
    {
        if (smoothMesh == null) return null;
        string meshName = smoothMesh.name;
        if (!meshName.EndsWith(OutputSuffix)) return null;

        string originalName = meshName.Substring(0, meshName.Length - OutputSuffix.Length);

        string assetPath = AssetDatabase.GetAssetPath(smoothMesh);
        string directory = !string.IsNullOrEmpty(assetPath) ? Path.GetDirectoryName(assetPath) : "";

        var searchDirs = new List<string>();
        if (!string.IsNullOrEmpty(directory))
        {
            searchDirs.Add(directory.Replace("\\", "/"));
            string parent = Path.GetDirectoryName(directory);
            if (!string.IsNullOrEmpty(parent))
                searchDirs.Add(parent.Replace("\\", "/"));
        }
        searchDirs.Add("Assets"); // 全工程兜底

        Mesh fallback = null;
        foreach (string searchDir in searchDirs)
        {
            if (string.IsNullOrEmpty(searchDir) || !AssetDatabase.IsValidFolder(searchDir)) continue;

            string[] guids = AssetDatabase.FindAssets(originalName, new[] { searchDir });
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                // 跳过本身
                if (candidatePath == assetPath) continue;
                // 跳过同样以 _SmoothN.asset 结尾的输出文件
                if (candidatePath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase)) continue;

                bool isModel = AssetImporter.GetAtPath(candidatePath) is ModelImporter;
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(candidatePath);
                foreach (var sub in subAssets)
                {
                    if (sub is Mesh m && m.name == originalName)
                    {
                        // 优先返回 Model 子 Mesh（FBX/OBJ 等），最贴近"最原始"
                        if (isModel) return m;
                        if (fallback == null) fallback = m;
                    }
                }
            }

            // 在当前目录已找到 Model 子 Mesh 就直接返回；fallback 留待外层兜底
            if (fallback != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(fallback)) is ModelImporter)
                return fallback;
        }

        return fallback;
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

        string originalName = meshName.Substring(0, meshName.Length - OutputSuffix.Length);
        string assetPath = AssetDatabase.GetAssetPath(mesh);
        string directory = !string.IsNullOrEmpty(assetPath) ? Path.GetDirectoryName(assetPath) : "";

        if (!string.IsNullOrEmpty(directory))
        {
            string[] guids = AssetDatabase.FindAssets(originalName, new[] { directory.Replace("\\", "/") });
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
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
    /// 核心算法：角度加权平滑法线 + 切线空间转换，写入指定 UV 通道的 xyz（3通道编码）
    /// </summary>
    private Mesh BakeSmoothNormals(Mesh sourceMesh, int targetChannel)
    {
        if (sourceMesh == null) return null;
        targetChannel = Mathf.Clamp(targetChannel, 0, 7);

        // ====== 第一阶段：确保源 Mesh 可读 ======
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

        // 提取顶点数据
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector4[] tangents = sourceMesh.tangents;
        BoneWeight[] boneWeights = sourceMesh.boneWeights;
        Matrix4x4[] bindposes = sourceMesh.bindposes;
        int subMeshCount = sourceMesh.subMeshCount;

        // 提取所有 UV 通道（保留维度）
        UVChannelData[] originalUVs = new UVChannelData[8];
        for (int c = 0; c < 8; c++)
        {
            originalUVs[c] = ReadUVChannel(sourceMesh, c);
        }

        // 提取所有子网格的三角形索引
        int[][] subMeshTriangles = new int[subMeshCount][];
        List<int> allTrianglesList = new List<int>();
        for (int s = 0; s < subMeshCount; s++)
        {
            subMeshTriangles[s] = sourceMesh.GetTriangles(s);
            allTrianglesList.AddRange(subMeshTriangles[s]);
        }
        int[] allTriangles = allTrianglesList.ToArray();

        int vertexCount = vertices.Length;

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
        Dictionary<Vector3, List<WeightedNormal>> normalDict = new Dictionary<Vector3, List<WeightedNormal>>();

        for (int i = 0; i <= allTriangles.Length - 3; i += 3)
        {
            int idx0 = allTriangles[i];
            int idx1 = allTriangles[i + 1];
            int idx2 = allTriangles[i + 2];

            Vector3 v0 = vertices[idx0];
            Vector3 v1 = vertices[idx1];
            Vector3 v2 = vertices[idx2];

            int[] triIndices = { idx0, idx1, idx2 };

            for (int j = 0; j < 3; j++)
            {
                int vertexIndex = triIndices[j];
                Vector3 vertex = vertices[vertexIndex];

                if (!normalDict.ContainsKey(vertex))
                {
                    normalDict.Add(vertex, new List<WeightedNormal>());
                }

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
                lineA *= 10000.0f;
                lineB *= 10000.0f;

                float dotAB = Vector3.Dot(lineA, lineB);
                float magProduct = lineA.magnitude * lineB.magnitude;
                float cosAngle = Mathf.Clamp(dotAB / magProduct, -1f, 1f);
                float angle = Mathf.Acos(cosAngle);

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
                smoothNormals[i] = normals[i];
                continue;
            }

            List<WeightedNormal> normalList = normalDict[vertex];

            float weightSum = 0f;
            for (int j = 0; j < normalList.Count; j++)
            {
                weightSum += normalList[j].weight;
            }

            Vector3 smoothNormal = Vector3.zero;
            for (int j = 0; j < normalList.Count; j++)
            {
                smoothNormal += normalList[j].normal * normalList[j].weight / weightSum;
            }

            smoothNormal = smoothNormal.normalized;

            Vector4 T = tangents[i];
            Vector3 N = normals[i];
            Vector3 B = (Vector3.Cross(N, new Vector3(T.x, T.y, T.z)) * T.w).normalized;

            Matrix4x4 TBN = new Matrix4x4(
                new Vector4(T.x, T.y, T.z, 0),
                new Vector4(B.x, B.y, B.z, 0),
                new Vector4(N.x, N.y, N.z, 0),
                new Vector4(0, 0, 0, 1)
            );
            TBN = TBN.transpose;
            smoothNormals[i] = TBN.MultiplyVector(smoothNormal).normalized;
        }

        // ====== 第四阶段：编码平滑法线（3通道，切线空间） ======
        Vector3[] uvData = new Vector3[vertexCount];
        int nonZeroCount = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            uvData[i] = smoothNormals[i];
            if (uvData[i].sqrMagnitude > 0.0001f)
                nonZeroCount++;
        }

        Debug.Log($"[平滑法线烘焙] 平滑法线统计: 总顶点={vertexCount}, 非零UV{targetChannel}(xyz)={nonZeroCount}");

        if (nonZeroCount == 0)
        {
            Debug.LogError($"[平滑法线烘焙] 所有 UV{targetChannel} 数据均为零！平滑法线计算失败。");
            RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);
            return null;
        }

        // 数据已全部提取，恢复 isReadable
        RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);

        // ====== 第五阶段：构建最终输出 Mesh ======
        Mesh mesh = new Mesh();
        mesh.name = sourceMeshName.Replace(OutputSuffix, "") + OutputSuffix;

        if (vertexCount > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.vertices = vertices;
        if (normals != null && normals.Length > 0) mesh.normals = normals;
        if (tangents != null && tangents.Length > 0) mesh.tangents = tangents;
        if (boneWeights != null && boneWeights.Length > 0) mesh.boneWeights = boneWeights;
        if (bindposes != null && bindposes.Length > 0) mesh.bindposes = bindposes;

        mesh.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++)
        {
            mesh.SetTriangles(subMeshTriangles[s], s);
        }

        // 写回所有非目标通道（保留原维度），目标通道写入平滑法线（3维）
        for (int c = 0; c < 8; c++)
        {
            if (c == targetChannel) continue;
            WriteUVChannel(mesh, c, originalUVs[c]);
        }
        mesh.SetUVs(targetChannel, uvData);

        // 验证写入是否成功
        List<Vector3> verifyUV = new List<Vector3>();
        mesh.GetUVs(targetChannel, verifyUV);
        if (verifyUV.Count == 0)
        {
            Debug.LogError($"[平滑法线烘焙] SetUVs 后验证失败：UV{targetChannel} 通道为空！");
        }
        else
        {
            int verifyNonZero = 0;
            for (int i = 0; i < Mathf.Min(verifyUV.Count, 100); i++)
            {
                if (verifyUV[i].sqrMagnitude > 0.0001f)
                    verifyNonZero++;
            }
            Debug.Log($"[平滑法线烘焙] SetUVs 验证: UV{targetChannel} 通道有 {verifyUV.Count} 个值, 前100个中非零={verifyNonZero}");
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 读取指定 UV 通道，保留其原始维度（2/3/4）
    /// </summary>
    private static UVChannelData ReadUVChannel(Mesh mesh, int channel)
    {
        var data = new UVChannelData();
        if (mesh == null || channel < 0 || channel > 7) return data;

        VertexAttribute attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
        if (!mesh.HasVertexAttribute(attr))
        {
            data.dimension = 0;
            return data;
        }

        int dim = mesh.GetVertexAttributeDimension(attr);
        data.dimension = dim;
        if (dim == 2)
        {
            data.uv2 = new List<Vector2>();
            mesh.GetUVs(channel, data.uv2);
        }
        else if (dim == 3)
        {
            data.uv3 = new List<Vector3>();
            mesh.GetUVs(channel, data.uv3);
        }
        else if (dim == 4)
        {
            data.uv4 = new List<Vector4>();
            mesh.GetUVs(channel, data.uv4);
        }
        return data;
    }

    /// <summary>
    /// 将缓存的 UV 数据写回 Mesh 的指定通道
    /// </summary>
    private static void WriteUVChannel(Mesh mesh, int channel, UVChannelData data)
    {
        if (mesh == null || !data.HasData) return;
        if (data.dimension == 2 && data.uv2 != null && data.uv2.Count > 0)
            mesh.SetUVs(channel, data.uv2);
        else if (data.dimension == 3 && data.uv3 != null && data.uv3.Count > 0)
            mesh.SetUVs(channel, data.uv3);
        else if (data.dimension == 4 && data.uv4 != null && data.uv4.Count > 0)
            mesh.SetUVs(channel, data.uv4);
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
    /// 持久化烘焙结果：始终在源 Mesh 同目录生成 _SmoothN.asset（已存在则覆盖其数据，保留命名）。
    /// 不会修改任何原始 .asset 或 FBX 文件。
    /// </summary>
    /// <param name="srcMesh">实际用于计算的源 Mesh（FBX 子 Mesh 或 .asset），决定输出目录</param>
    /// <param name="newMesh">新构建的 Mesh 对象</param>
    private Mesh SaveMeshAsset(Mesh srcMesh, Mesh newMesh)
    {
        string anchorPath = AssetDatabase.GetAssetPath(srcMesh);

        string directory;
        string baseName;

        if (!string.IsNullOrEmpty(anchorPath))
        {
            directory = Path.GetDirectoryName(anchorPath);
            baseName = srcMesh != null ? srcMesh.name : newMesh.name;
        }
        else
        {
            directory = "Assets";
            baseName = newMesh.name;
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }

        if (baseName.EndsWith(OutputSuffix))
        {
            baseName = baseName.Substring(0, baseName.Length - OutputSuffix.Length);
        }

        string outputPath = Path.Combine(directory, baseName + OutputSuffix + ".asset").Replace("\\", "/");
        Debug.Log($"[平滑法线烘焙] 保存路径: {outputPath}");

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
        if (existing != null)
        {
            string oldName = existing.name;
            EditorUtility.CopySerialized(newMesh, existing);
            existing.name = oldName;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(newMesh);
            return existing;
        }

        AssetDatabase.CreateAsset(newMesh, outputPath);
        return newMesh;
    }
}
