using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Outline 平滑法线烘焙工具：
/// 使用角度加权算法计算模型的平滑法线，转换到切线空间后存入指定 UV 通道（默认 UV3）的 xyz 中，
/// 主要供 PBRToon 等卡通描边方案使用（背面法线外扩描边法）。
///
/// ========== UV 通道分配约定（默认） ==========
///   UV0 (TEXCOORD0) : 主纹理坐标
///   UV1 (TEXCOORD1) : Lightmap / 自定义数据
///   UV2 (TEXCOORD2) : Bent Normal 数据 (由 Bent Normal Baker 写入)
///   UV3 (TEXCOORD3) : 平滑法线 (本工具默认写入此通道，供 PBRToon 描边使用)
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
/// 在 Shader 中解码（默认 UV3）：
///   float3 snTS = uv3.xyz;  // 切线空间平滑法线
///   float3 smoothNormalOS = snTS.x * tangentOS.xyz + snTS.y * bitangentOS + snTS.z * normalOS;
///
/// ========== 来源类型与输出策略 ==========
///   [P] PrefabAsset           : Project 中的 .prefab 资源（Regular / Variant）
///                                烘焙后直接修改原 prefab：在 PreviewScene 中实例化原 prefab，
///                                把所有需要平滑法线的 SMR/MF.sharedMesh 替换为 _SmoothN，然后用
///                                TryApplyMeshOverrideToPrefab 沿 prefab 嵌套链「逐层 Apply」，
///                                确保 outer prefab 与所有嵌套 prefab（包括嵌套 FBX Variant）都被改写。
///
///   [S] ScenePrefabInstance   : 场景中的 prefab 实例
///                                烘焙后修改场景对象的 MeshFilter / SkinnedMeshRenderer 引用，
///                                并用同样的「逐层 Apply」机制写回所有相关 prefab 源文件。
///
///   [O] Other                 : 普通场景 GameObject / Mesh.asset / FBX 子 Mesh / 其他
///                                工具无法将修改保存到原始资源；烘焙仅在源 Mesh 同目录生成 _SmoothN.asset，
///                                不修改任何引用。列表中以红色 ⛔ 标注以提示。
///
/// FBX PrefabVariant 功能（可选开关）：
///   烘焙完成后，自动扫描源 prefab 中嵌套的 FBX，为每个 FBX 生成同名 PrefabVariant 并替换进 prefab。
///   Variant 命名 = FBX 原文件名，生成在 FBX 同目录。Variant 包含 prefab 对 FBX 子树的所有修改。
///   之后原 prefab 内嵌套的就是 Variant 而非 FBX，后续烘焙作用于 Variant，原 prefab 自动用上平滑法线版。
///
/// SkinnedMesh 处理：完整保留 boneWeights / bindposes / blendShapes，骨骼绑定与表情动画不受影响。
///
/// 还原功能（↺ 用原始资源替换）：
///   · [P] 与烘焙完全相同的写回机制，只是把 _SmoothN 换回原 mesh
///   · [S] 写回原始引用并逐层 Apply 到所有相关 prefab 源文件
///   · [O] 跳过
///   · 勾选「删除原始资源」会在删除前主动扫描场景把仍引用 _SmoothN 的组件切回原 mesh，使用中的不会被删除
/// </summary>
public class SmoothNormalBaker : EditorWindow
{
    private const string OutputSuffix = "_SmoothN";

    private struct WeightedNormal
    {
        public Vector3 normal;
        public float weight;
    }

    private struct UVChannelData
    {
        public int dimension;
        public List<Vector2> uv2;
        public List<Vector3> uv3;
        public List<Vector4> uv4;
        public bool HasData =>
            (uv2 != null && uv2.Count > 0) ||
            (uv3 != null && uv3.Count > 0) ||
            (uv4 != null && uv4.Count > 0);
    }

    private struct BlendShapeFrame
    {
        public string shapeName;
        public float weight;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;
    }

    private enum MeshOwnerType
    {
        PrefabAsset,
        ScenePrefabInstance,
        Other,
    }

    private enum ComponentKind
    {
        None,
        MeshFilter,
        SkinnedMeshRenderer,
    }

    private class MeshEntry
    {
        public Object sourceObject;
        public Mesh mesh;
        public string displayName;
        public string assetPath;
        public bool selected = true;
        public string status;
        public MeshOwnerType ownerType;
        public ComponentKind componentKind;

        public string prefabAssetPath;
        public string componentTransformPath;

        public GameObject sceneOwner;

        public string lastBakedAssetPath;
    }

    private int targetUVChannel = 3;
    private bool deleteSmoothNAssetOnRestore = false;
    private bool generateFbxVariantOnDrop = false;

    private List<MeshEntry> meshEntries = new List<MeshEntry>();
    private Vector2 scrollPosition;
    private bool showHelp = false;
    private bool autoTrackSelection = true;
    private Object lastDroppedObject = null;

    private const string WindowTitle = "Outline平滑法线烘焙";

    [MenuItem("nTools/美术工具/Outline平滑法线烘焙", false, 54)]
    public static void ShowWindow()
    {
        var win = GetWindow<SmoothNormalBaker>(WindowTitle);
        win.minSize = new Vector2(460, 480);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent(WindowTitle);
        Selection.selectionChanged += OnSelectionChanged;
        SyncFromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        if (autoTrackSelection)
        {
            SyncFromSelection();
            Repaint();
        }
    }

    // ============================================================
    //                          UI
    // ============================================================

    private void OnGUI()
    {
        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Outline 平滑法线烘焙", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        DrawDragDropArea();

        GUILayout.Space(4);

        DrawSettingsArea();

        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        autoTrackSelection = EditorGUILayout.ToggleLeft(
            new GUIContent("自动识别选中",
                "勾选后，Unity 中选择对象会自动同步到列表（仅识别合法的 Prefab 资源 / Prefab 实例；非 Prefab 仅在拖入时才会弹窗提示）。"),
            autoTrackSelection,
            GUILayout.Width(110));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清空全部", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            meshEntries.Clear();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        int totalCount = meshEntries.Count;
        int selectedCount = meshEntries.Count(e => e.selected);
        int warningCount = meshEntries.Count(e => e.selected && MeshHasUVData(e.mesh, targetUVChannel));
        int otherCount = meshEntries.Count(e => e.selected && e.ownerType == MeshOwnerType.Other);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        string toolbarText = $"Mesh 列表 (共 {totalCount} 个, 已选 {selectedCount} 个";
        if (warningCount > 0) toolbarText += $", ⚠ {warningCount} 个目标 UV 已有数据";
        if (otherCount > 0) toolbarText += $", ⛔ {otherCount} 个无法写回原始资源";
        toolbarText += ")";
        GUILayout.Label(toolbarText, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

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

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        if (totalCount == 0)
        {
            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("列表为空，推荐拖入 Prefab 资源（也可拖入场景对象 / Mesh / FBX）", EditorStyles.centeredGreyMiniLabel);
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

        GUI.enabled = selectedCount > 0;
        string bakeBtnLabel = $"▶ 烘焙选中的 {selectedCount} 个 Mesh 到 UV{targetUVChannel}";
        if (warningCount > 0) bakeBtnLabel += $"  (⚠ 覆盖 {warningCount} 个已有 UV 数据)";
        if (otherCount > 0) bakeBtnLabel += $"  (⛔ {otherCount} 个仅生成 _SmoothN)";
        if (GUILayout.Button(bakeBtnLabel, GUILayout.Height(36)))
        {
            BakeSelectedEntries();
        }

        GUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        var restoreBtnContent = new GUIContent(
            $"↺ 用原始资源替换选中的 {selectedCount} 个",
            "把选中条目当前引用的 mesh_SmoothN 还原成原始 mesh（去掉 _SmoothN 后缀）。\n" +
            "  · [P] 在 PreviewScene 中实例化原 prefab，把 sharedMesh 改回原 mesh，逐层 Apply 写回所有嵌套层\n" +
            "  · [S] 写回场景引用并自动逐层 Apply 到所有相关 prefab 源文件\n" +
            "  · [O] 跳过（无引用可改，仅可选删除 _SmoothN.asset）");
        if (GUILayout.Button(restoreBtnContent, GUILayout.ExpandWidth(true), GUILayout.Height(26)))
        {
            RestoreSelectedToOriginal();
        }

        deleteSmoothNAssetOnRestore = EditorGUILayout.ToggleLeft(
            new GUIContent("删除原始资源",
                "勾选后：还原成功的条目，会连带从硬盘上删除被替换掉的 _SmoothN.asset 文件。\n\n" +
                "安全策略（不会误删使用中的资源）：\n" +
                "  · 删除前会先扫描所有打开的场景，把仍引用待删 _SmoothN 的 SMR/MF 自动切回原 mesh\n" +
                "  · 仅当工具列表中没有其它条目仍引用该资源时才会真正删除\n" +
                "  · 使用中的（仍被场景或工具列表引用的）不会被删除，会保留并在 Console 给出提示\n\n" +
                "默认不勾选：只断开引用、保留 _SmoothN.asset。"),
            deleteSmoothNAssetOnRestore,
            GUILayout.Width(110), GUILayout.Height(26));

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        GUILayout.Space(4);

        DrawHelpFooter();
    }

    private void DrawSettingsArea()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("烘焙设置", EditorStyles.miniBoldLabel);

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
                "若选中通道在某些 Mesh 上已有数据（如 Lightmap UV），将在列表中标注 ⚠。"),
            targetUVChannel,
            channelOptions,
            channelValues);
        targetUVChannel = Mathf.Clamp(targetUVChannel, 0, 7);

        GUILayout.Space(2);

        generateFbxVariantOnDrop = EditorGUILayout.ToggleLeft(
            new GUIContent("烘焙后为嵌套 FBX 生成 PrefabVariant",
                "烘焙完成后，自动扫描源 prefab 中嵌套的 FBX，生成同名 PrefabVariant 并替换进 prefab。\n" +
                "  · Variant 命名 = FBX 原文件名，生成在 FBX 同目录\n" +
                "  · Variant 包含 prefab 对 FBX 子树的所有修改（组件 / 属性 / 子物体）\n" +
                "  · 原 prefab 中的 FBX 嵌套会被替换为 Variant 嵌套\n" +
                "  · 无嵌套 FBX 的 prefab 不受影响"),
            generateFbxVariantOnDrop);

        EditorGUILayout.EndVertical();
    }

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
                "角度加权平滑法线 → 切线空间 → 写入 UV 通道 xyz，供描边 Shader 使用。\n\n" +
                "[来源] [P] Prefab 资源  [S] 场景 Prefab 实例  [O] 其他（仅生成 _SmoothN.asset）\n" +
                "[输出] 在源 Mesh 同目录生成 _SmoothN.asset；[P]/[S] 自动逐层 Apply 写回所有嵌套 prefab\n" +
                "[FBX Variant] 勾选开关后，烘焙完成会为嵌套 FBX 生成同名 PrefabVariant 并替换进 prefab\n" +
                "[还原] ↺ 按钮把 mesh 改回原始；勾选「删除原始资源」会顺手删掉 _SmoothN.asset（使用中的不删）\n" +
                "[SkinnedMesh] 完整保留 boneWeights / bindposes / blendShapes\n\n" +
                "[Shader 解码] float3 snTS = uv3.xyz;\n" +
                "  float3 smoothNormalOS = snTS.x*tangentOS + snTS.y*bitangentOS + snTS.z*normalOS;",
                MessageType.Info);
        }
    }

    private void DrawMeshEntryRow(MeshEntry entry, int index, ref int removeIndex)
    {
        const float rowHeight = 22f;

        Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(rowHeight));
        bool hasVerifyError = entry.status != null && entry.status.Contains("未替换");
        if (hasVerifyError)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.15f, 0.15f, 0.45f));
        }
        else if (index % 2 == 0)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, 0.4f));
        }

        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16), GUILayout.Height(rowHeight));

        bool hasUVConflict = MeshHasUVData(entry.mesh, targetUVChannel);
        bool isOther = entry.ownerType == MeshOwnerType.Other;

        var tipParts = new List<string>();
        if (isOther) tipParts.Add("该对象不是 Prefab，工具无法将修改保存到原始资源；烘焙仅会生成 _SmoothN.asset。");
        if (hasUVConflict) tipParts.Add($"该 Mesh 的 UV{targetUVChannel} 已存在数据，烘焙将覆盖原有数据。");
        string warningTip = tipParts.Count > 0 ? string.Join("\n", tipParts) : null;

        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            richText = true,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(2, 2, 0, 0)
        };

        string sourceIcon;
        switch (entry.ownerType)
        {
            case MeshOwnerType.PrefabAsset:
                sourceIcon = "<color=#88ccff>[P]</color>";
                break;
            case MeshOwnerType.ScenePrefabInstance:
                sourceIcon = "<color=#aaee88>[S]</color>";
                break;
            default:
                sourceIcon = "<color=#ff8866>[O]</color>";
                break;
        }

        string warnIcon = "";
        if (isOther) warnIcon += "<color=#ff6644>⛔</color> ";
        if (hasUVConflict) warnIcon += "<color=#ffaa44>⚠</color> ";
        string displayText = $"{sourceIcon} {warnIcon}{entry.displayName}";

        if (GUILayout.Button(new GUIContent(displayText, warningTip),
                nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(rowHeight)))
        {
            if (entry.sceneOwner != null)
                EditorGUIUtility.PingObject(entry.sceneOwner);
            else if (entry.mesh != null)
                EditorGUIUtility.PingObject(entry.mesh);
            else if (entry.sourceObject != null)
                EditorGUIUtility.PingObject(entry.sourceObject);
        }

        if (!string.IsNullOrEmpty(entry.status))
        {
            Color statusColor;
            if (entry.status.Contains("失败") || entry.status.StartsWith("跳过") || entry.status.Contains("未替换"))
                statusColor = new Color(1f, 0.5f, 0.45f);
            else if (entry.status.Contains("已完成") || entry.status.Contains("已写回") || entry.status.Contains("已 Apply"))
                statusColor = new Color(0.4f, 0.9f, 0.4f);
            else if (entry.status.Contains("已还原"))
                statusColor = new Color(0.6f, 0.85f, 1f);
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
            GUILayout.Label(entry.status, statusStyle, GUILayout.Width(140), GUILayout.Height(rowHeight));
        }

        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(rowHeight)))
        {
            removeIndex = index;
        }

        EditorGUILayout.EndHorizontal();
    }

    private static bool MeshHasUVData(Mesh mesh, int channel)
    {
        if (mesh == null || channel < 0 || channel > 7) return false;
        VertexAttribute attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
        return mesh.HasVertexAttribute(attr);
    }

    // ============================================================
    //                       拖拽 / 选中 / 添加
    // ============================================================

    private void SyncFromSelection()
    {
        meshEntries.RemoveAll(e =>
            string.IsNullOrEmpty(e.status)
            || (!e.status.Contains("已完成") && !e.status.Contains("已还原") && !e.status.Contains("已写回") && !e.status.Contains("已 Apply")));

        GameObject[] selectedGOs = Selection.gameObjects;
        if (selectedGOs != null)
        {
            foreach (var go in selectedGOs)
            {
                AddObject(go, silentSkipOther: true);
            }
        }

        Object[] selectedAssets = Selection.objects;
        if (selectedAssets != null)
        {
            foreach (var obj in selectedAssets)
            {
                if (obj is GameObject) continue;
                AddObject(obj, silentSkipOther: true);
            }
        }
    }

    private void DrawDragDropArea()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("拖入对象", GUILayout.Width(56));
        GUI.enabled = false;
        EditorGUILayout.ObjectField(lastDroppedObject, typeof(Object), true);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        Rect dropRect = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));

        var bgStyle = new GUIStyle("Box")
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal,
            fontSize = 11
        };
        bgStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        GUI.Box(dropRect, "推荐拖入 Prefab；其他对象（场景物体 / Mesh / FBX）会先弹窗提示", bgStyle);

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
                    if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
                        lastDroppedObject = DragAndDrop.objectReferences[0];
                    if (meshEntries.Count > 0)
                    {
                        Debug.Log($"[平滑法线烘焙] 拖入新对象，已自动清空之前的 {meshEntries.Count} 个条目。");
                        meshEntries.Clear();
                    }
                    int addedCount = 0;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        int before = meshEntries.Count;
                        AddObject(obj, silentSkipOther: false);
                        addedCount += meshEntries.Count - before;
                    }
                    if (addedCount > 0)
                        Debug.Log($"[平滑法线烘焙] 拖入添加了 {addedCount} 个 Mesh。");
                    evt.Use();
                    break;
            }
        }
    }

    private void AddObject(Object obj, bool silentSkipOther)
    {
        if (obj == null) return;

        if (obj is GameObject go)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(go))
            {
                PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(go);
                string prefabPath = AssetDatabase.GetAssetPath(go);

                if ((assetType == PrefabAssetType.Regular || assetType == PrefabAssetType.Variant)
                    && !string.IsNullOrEmpty(prefabPath)
                    && prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefabRoot != null)
                    {
                        AddPrefabAssetEntries(prefabRoot, prefabPath);
                        return;
                    }
                }

                AddOtherFromObject(obj, silentSkipOther);
                return;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                AddScenePrefabInstanceEntries(go);
                return;
            }

            AddOtherFromGameObject(go, silentSkipOther);
            return;
        }

        AddOtherFromObject(obj, silentSkipOther);
    }

    private void AddPrefabAssetEntries(GameObject prefabRoot, string prefabAssetPath)
    {
        if (prefabRoot == null || string.IsNullOrEmpty(prefabAssetPath)) return;

        var meshFilters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(mf.sharedMesh, prefabAssetPath: prefabAssetPath)) continue;

            string transformPath = GetTransformPath(prefabRoot.transform, mf.transform);
            var entry = CreateEntryFromMesh(mf.sharedMesh, prefabRoot, MeshOwnerType.PrefabAsset);
            entry.componentKind = ComponentKind.MeshFilter;
            entry.prefabAssetPath = prefabAssetPath;
            entry.componentTransformPath = transformPath;
            entry.displayName = $"{prefabRoot.name}/{(string.IsNullOrEmpty(transformPath) ? mf.gameObject.name : transformPath)} : {mf.sharedMesh.name}";
            meshEntries.Add(entry);
        }

        var skinned = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinned)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(smr.sharedMesh, prefabAssetPath: prefabAssetPath)) continue;

            string transformPath = GetTransformPath(prefabRoot.transform, smr.transform);
            var entry = CreateEntryFromMesh(smr.sharedMesh, prefabRoot, MeshOwnerType.PrefabAsset);
            entry.componentKind = ComponentKind.SkinnedMeshRenderer;
            entry.prefabAssetPath = prefabAssetPath;
            entry.componentTransformPath = transformPath;
            entry.displayName = $"{prefabRoot.name}/{(string.IsNullOrEmpty(transformPath) ? smr.gameObject.name : transformPath)} : {smr.sharedMesh.name} (Skinned)";
            meshEntries.Add(entry);
        }
    }

    // ============================================================
    //   嵌套 FBX → PrefabVariant 自动生成
    // ============================================================

    private static List<KeyValuePair<GameObject, string>> FindNestedFbxInstanceRootsInPrefab(GameObject prefabInstance)
    {
        var result = new List<KeyValuePair<GameObject, string>>();
        if (prefabInstance == null) return result;

        var seenFbx = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var allTransforms = prefabInstance.GetComponentsInChildren<Transform>(true);
        Debug.Log($"[平滑法线烘焙] FindNestedFbx：开始扫描，根='{prefabInstance.name}'，共 {allTransforms.Length} 个 Transform");

        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            var go = t.gameObject;
            if (go == prefabInstance) continue;

            GameObject originalSrc = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            GameObject directSrc = PrefabUtility.GetCorrespondingObjectFromSource(go);
            bool isInstanceRoot = PrefabUtility.IsAnyPrefabInstanceRoot(go);

            string directPath = directSrc != null ? AssetDatabase.GetAssetPath(directSrc) : null;
            string originalPath = originalSrc != null ? AssetDatabase.GetAssetPath(originalSrc) : null;
            string nearestPath = null;
            try { nearestPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go); } catch { }

            bool isDirectChild = go.transform.parent == prefabInstance.transform;
            if (isDirectChild || isInstanceRoot)
            {
                Debug.Log($"[平滑法线烘焙] FindNestedFbx：'{go.name}' " +
                    $"isDirectChild={isDirectChild}, " +
                    $"isInstanceRoot={isInstanceRoot}, " +
                    $"directSrc={(directSrc != null ? directSrc.name : "null")} @ '{directPath}', " +
                    $"originalSrc={(originalSrc != null ? originalSrc.name : "null")} @ '{originalPath}', " +
                    $"nearestPath='{nearestPath}'");
            }

            string fbxPath = null;
            if (originalSrc != null && !string.IsNullOrEmpty(originalPath) && IsModelAssetPath(originalPath) && originalSrc.transform.parent == null)
            {
                fbxPath = originalPath;
            }
            if (fbxPath == null && directSrc != null && !string.IsNullOrEmpty(directPath) && IsModelAssetPath(directPath) && directSrc.transform.parent == null)
            {
                fbxPath = directPath;
            }
            if (fbxPath == null && isInstanceRoot && !string.IsNullOrEmpty(nearestPath) && IsModelAssetPath(nearestPath))
            {
                fbxPath = nearestPath;
            }

            if (string.IsNullOrEmpty(fbxPath)) continue;

            if (!HasAnyMeshComponent(go)) continue;

            if (seenFbx.Add(fbxPath))
            {
                result.Add(new KeyValuePair<GameObject, string>(go, fbxPath));
                Debug.Log($"[平滑法线烘焙] ✓ 检测到嵌套 FBX：'{go.name}' → {fbxPath}");
            }
        }

        Debug.Log($"[平滑法线烘焙] FindNestedFbx：共检测到 {result.Count} 个嵌套 FBX 子树根。");
        return result;
    }

    private static bool IsModelAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".dae", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnyMeshComponent(GameObject root)
    {
        if (root == null) return false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            if (mf != null && mf.sharedMesh != null) return true;
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr != null && smr.sharedMesh != null) return true;
        return false;
    }

    private List<string> GenerateFbxVariantsFromPrefab(string originalPrefabPath)
    {
        var generatedPaths = new List<string>();
        var fbxToVariantMap = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(originalPrefabPath)) return generatedPaths;

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(originalPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[平滑法线烘焙] 无法加载 prefab：{originalPrefabPath}");
            return generatedPaths;
        }

        var previewSceneA = EditorSceneManager.NewPreviewScene();
        GameObject outerInstanceA = null;
        try
        {
            outerInstanceA = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, previewSceneA);
            if (outerInstanceA == null)
            {
                Debug.LogWarning($"[平滑法线烘焙] InstantiatePrefab 失败：{originalPrefabPath}");
                return generatedPaths;
            }

            var fbxRoots = FindNestedFbxInstanceRootsInPrefab(outerInstanceA);
            if (fbxRoots.Count == 0)
            {
                Debug.Log($"[平滑法线烘焙] 「{Path.GetFileName(originalPrefabPath)}」 中未发现可生成 Variant 的嵌套 FBX，按原 prefab 烘焙。");
                return generatedPaths;
            }

            PrefabUtility.UnpackPrefabInstance(outerInstanceA, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            foreach (var pair in fbxRoots)
            {
                GameObject fbxInstanceRoot = pair.Key;
                string fbxPath = pair.Value;
                if (fbxInstanceRoot == null || string.IsNullOrEmpty(fbxPath)) continue;

                try
                {
                    string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
                    string fbxDir = (Path.GetDirectoryName(fbxPath) ?? "").Replace('\\', '/');
                    if (string.IsNullOrEmpty(fbxDir) || string.IsNullOrEmpty(fbxName))
                    {
                        Debug.LogWarning($"[平滑法线烘焙] FBX 路径解析失败，跳过：{fbxPath}");
                        continue;
                    }

                    string variantPath = $"{fbxDir}/{fbxName}.prefab";

                    if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
                    {
                        AssetDatabase.DeleteAsset(variantPath);
                    }

                    fbxInstanceRoot.transform.SetParent(null, true);
                    fbxInstanceRoot.name = fbxName;

                    bool ok;
                    GameObject variantAsset = PrefabUtility.SaveAsPrefabAsset(fbxInstanceRoot, variantPath, out ok);
                    if (!ok || variantAsset == null)
                    {
                        Debug.LogWarning($"[平滑法线烘焙] 生成 PrefabVariant 失败：{variantPath}（来源 FBX：{fbxPath}）");
                        continue;
                    }

                    string actualPath = AssetDatabase.GetAssetPath(variantAsset);
                    if (!string.IsNullOrEmpty(actualPath)
                        && !string.Equals(actualPath, variantPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
                            AssetDatabase.DeleteAsset(variantPath);
                        string moveErr = AssetDatabase.MoveAsset(actualPath, variantPath);
                        if (string.IsNullOrEmpty(moveErr))
                        {
                            Debug.Log($"[平滑法线烘焙] Unity 默认追加了 \" Variant\" 后缀，已自动重命名：{actualPath} → {variantPath}");
                        }
                        else
                        {
                            Debug.LogWarning($"[平滑法线烘焙] 期望路径 {variantPath} 重命名失败：{moveErr}，沿用 {actualPath}");
                            variantPath = actualPath;
                        }
                    }

                    var savedVariant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                    if (savedVariant != null && savedVariant.name != fbxName)
                    {
                        var so = new SerializedObject(savedVariant);
                        var nameProp = so.FindProperty("m_Name");
                        if (nameProp != null)
                        {
                            nameProp.stringValue = fbxName;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(savedVariant);
                        }
                    }

                    if (!generatedPaths.Contains(variantPath))
                        generatedPaths.Add(variantPath);
                    fbxToVariantMap[fbxPath] = variantPath;

                    Debug.Log($"[平滑法线烘焙] 已生成 PrefabVariant：{variantPath}（来源 FBX：{fbxPath}，原 prefab：{originalPrefabPath}），Variant 已包含 prefab 给 FBX 子树的所有 override（含添加的组件）。");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[平滑法线烘焙] 生成 FBX Variant 时异常：{e.Message}\n{e.StackTrace}");
                }
            }
        }
        finally
        {
            if (outerInstanceA != null) Object.DestroyImmediate(outerInstanceA);
            EditorSceneManager.ClosePreviewScene(previewSceneA);
        }

        if (generatedPaths.Count == 0) return generatedPaths;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        try
        {
            int replaced = ReplaceFbxInstancesInPrefabWithVariants(originalPrefabPath, fbxToVariantMap);
            if (replaced > 0)
            {
                Debug.Log($"[平滑法线烘焙] 已将原 prefab 「{Path.GetFileName(originalPrefabPath)}」 中嵌套的 {replaced} 个 FBX 实例替换为 Variant 实例。");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[平滑法线烘焙] 替换原 prefab 中的 FBX 嵌套时异常：{e.Message}\n{e.StackTrace}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return generatedPaths;
    }

    private int ReplaceFbxInstancesInPrefabWithVariants(string outerPrefabPath, Dictionary<string, string> fbxToVariantMap)
    {
        if (string.IsNullOrEmpty(outerPrefabPath) || fbxToVariantMap == null || fbxToVariantMap.Count == 0)
        {
            Debug.Log($"[平滑法线烘焙] Phase B：跳过（outerPrefabPath 为空或 map 为空，map.Count={fbxToVariantMap?.Count ?? 0}）");
            return 0;
        }

        Debug.Log($"[平滑法线烘焙] Phase B：开始替换 '{outerPrefabPath}' 中的 FBX 嵌套，map 有 {fbxToVariantMap.Count} 条映射：");
        foreach (var kv in fbxToVariantMap)
            Debug.Log($"  {kv.Key} → {kv.Value}");

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(outerPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[平滑法线烘焙] Phase B：加载 prefab 失败：{outerPrefabPath}");
            return 0;
        }

        GameObject prefabContents = PrefabUtility.LoadPrefabContents(outerPrefabPath);
        if (prefabContents == null)
        {
            Debug.LogWarning($"[平滑法线烘焙] Phase B：LoadPrefabContents 失败：{outerPrefabPath}");
            return 0;
        }

        int replaced = 0;
        try
        {
            var fbxRoots = FindNestedFbxInstanceRootsInPrefab(prefabContents);
            Debug.Log($"[平滑法线烘焙] Phase B：在 prefab 内容中检测到 {fbxRoots.Count} 个嵌套 FBX 子树根。");

            if (fbxRoots.Count == 0)
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
                return 0;
            }

            foreach (var pair in fbxRoots)
            {
                GameObject fbxInstanceRoot = pair.Key;
                string fbxPath = pair.Value;
                if (fbxInstanceRoot == null || string.IsNullOrEmpty(fbxPath)) continue;

                if (!fbxToVariantMap.TryGetValue(fbxPath, out string variantPath) || string.IsNullOrEmpty(variantPath))
                {
                    Debug.LogWarning($"[平滑法线烘焙] Phase B：fbxToVariantMap 中找不到 '{fbxPath}' 对应的 Variant");
                    continue;
                }

                GameObject variantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                if (variantAsset == null)
                {
                    Debug.LogWarning($"[平滑法线烘焙] Phase B：找不到刚生成的 Variant：{variantPath}");
                    continue;
                }

                Transform parent = fbxInstanceRoot.transform.parent;
                int siblingIndex = fbxInstanceRoot.transform.GetSiblingIndex();
                Vector3 localPos = fbxInstanceRoot.transform.localPosition;
                Quaternion localRot = fbxInstanceRoot.transform.localRotation;
                Vector3 localScale = fbxInstanceRoot.transform.localScale;
                bool active = fbxInstanceRoot.activeSelf;
                int layer = fbxInstanceRoot.layer;
                string tagName = "Untagged";
                try { tagName = fbxInstanceRoot.tag; } catch { }
                string oldName = fbxInstanceRoot.name;
                StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(fbxInstanceRoot);

                Debug.Log($"[平滑法线烘焙] Phase B：替换 '{oldName}'（FBX: {fbxPath}）→ Variant: {variantPath}");

                Object.DestroyImmediate(fbxInstanceRoot);

                GameObject variantInstance = (GameObject)PrefabUtility.InstantiatePrefab(variantAsset);
                if (variantInstance == null)
                {
                    Debug.LogWarning($"[平滑法线烘焙] Phase B：InstantiatePrefab(Variant) 失败：{variantPath}");
                    continue;
                }

                if (parent != null)
                {
                    variantInstance.transform.SetParent(parent, false);
                    variantInstance.transform.SetSiblingIndex(siblingIndex);
                }
                variantInstance.transform.localPosition = localPos;
                variantInstance.transform.localRotation = localRot;
                variantInstance.transform.localScale = localScale;
                variantInstance.SetActive(active);
                variantInstance.layer = layer;
                try { variantInstance.tag = tagName; } catch { }
                GameObjectUtility.SetStaticEditorFlags(variantInstance, staticFlags);

                replaced++;
                Debug.Log($"[平滑法线烘焙] Phase B：成功替换 '{oldName}'");
            }

            if (replaced > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabContents, outerPrefabPath);
                Debug.Log($"[平滑法线烘焙] Phase B：已保存修改后的 prefab：{outerPrefabPath}，共替换 {replaced} 个");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[平滑法线烘焙] Phase B 内部异常：{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        return replaced;
    }

    private void AddScenePrefabInstanceEntries(GameObject sceneRoot)
    {
        if (sceneRoot == null) return;

        var meshFilters = sceneRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(mf.sharedMesh, sceneOwner: mf.gameObject)) continue;

            var entry = CreateEntryFromMesh(mf.sharedMesh, mf.gameObject, MeshOwnerType.ScenePrefabInstance);
            entry.componentKind = ComponentKind.MeshFilter;
            entry.sceneOwner = mf.gameObject;
            entry.displayName = $"{mf.gameObject.name} : {mf.sharedMesh.name}";
            meshEntries.Add(entry);
        }

        var skinned = sceneRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinned)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(smr.sharedMesh, sceneOwner: smr.gameObject)) continue;

            var entry = CreateEntryFromMesh(smr.sharedMesh, smr.gameObject, MeshOwnerType.ScenePrefabInstance);
            entry.componentKind = ComponentKind.SkinnedMeshRenderer;
            entry.sceneOwner = smr.gameObject;
            entry.displayName = $"{smr.gameObject.name} : {smr.sharedMesh.name} (Skinned)";
            meshEntries.Add(entry);
        }
    }

    private void AddOtherFromGameObject(GameObject go, bool silentSkip)
    {
        if (go == null) return;
        if (silentSkip) return;
        if (!PromptOtherWarning(go.name)) return;

        var meshFilters = go.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(mf.sharedMesh)) continue;

            var entry = CreateEntryFromMesh(mf.sharedMesh, go, MeshOwnerType.Other);
            entry.componentKind = ComponentKind.MeshFilter;
            entry.sceneOwner = mf.gameObject;
            meshEntries.Add(entry);
        }

        var skinned = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in skinned)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            if (IsMeshAlreadyInList(smr.sharedMesh)) continue;

            var entry = CreateEntryFromMesh(smr.sharedMesh, go, MeshOwnerType.Other);
            entry.componentKind = ComponentKind.SkinnedMeshRenderer;
            entry.sceneOwner = smr.gameObject;
            meshEntries.Add(entry);
        }
    }

    private void AddOtherFromObject(Object obj, bool silentSkip)
    {
        if (obj == null) return;

        if (obj is Mesh mesh)
        {
            if (silentSkip) return;
            if (!PromptOtherWarning(mesh.name)) return;
            if (IsMeshAlreadyInList(mesh)) return;
            var entry = CreateEntryFromMesh(mesh, obj, MeshOwnerType.Other);
            entry.componentKind = ComponentKind.None;
            meshEntries.Add(entry);
            return;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path)) return;

        var importer = AssetImporter.GetAtPath(path);
        if (importer is ModelImporter)
        {
            if (silentSkip) return;
            if (!PromptOtherWarning(obj.name + " (Model)")) return;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var sub in subAssets)
            {
                if (sub is Mesh subMesh && !IsMeshAlreadyInList(subMesh))
                {
                    var entry = CreateEntryFromMesh(subMesh, obj, MeshOwnerType.Other);
                    entry.componentKind = ComponentKind.None;
                    meshEntries.Add(entry);
                }
            }
        }
    }

    private bool PromptOtherWarning(string objName)
    {
        return EditorUtility.DisplayDialog(
            "无法保存到原始资源",
            $"对象「{objName}」不是 Prefab，工具无法将平滑法线写回到原始资源。\n\n" +
            "如果继续，烘焙会在源 Mesh 同目录生成 _SmoothN.asset，但不会修改任何 Prefab / 场景 / FBX / Mesh.asset 引用，需要你自行手动使用结果。\n\n" +
            "建议：直接拖入 .prefab 资源，工具会自动写回。",
            "继续添加",
            "取消");
    }

    private bool IsMeshAlreadyInList(Mesh mesh, string prefabAssetPath = null, GameObject sceneOwner = null)
    {
        foreach (var entry in meshEntries)
        {
            if (entry.mesh != mesh) continue;
            if (prefabAssetPath != null)
            {
                if (entry.prefabAssetPath == prefabAssetPath) return true;
            }
            else if (sceneOwner != null)
            {
                if (entry.sceneOwner == sceneOwner) return true;
            }
            else
            {
                return true;
            }
        }
        return false;
    }

    private MeshEntry CreateEntryFromMesh(Mesh mesh, Object source, MeshOwnerType ownerType)
    {
        var entry = new MeshEntry();
        entry.mesh = mesh;
        entry.sourceObject = source;
        entry.displayName = mesh.name;
        entry.assetPath = AssetDatabase.GetAssetPath(mesh);
        entry.selected = true;
        entry.ownerType = ownerType;

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
                entry.status = File.Exists(smoothNPath) ? "已有输出" : "需要烘焙";
            }
            else
            {
                entry.status = "需要烘焙";
            }
        }

        return entry;
    }

    private static string GetTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null) return "";
        if (target == root) return "";

        var sb = new System.Text.StringBuilder();
        Transform current = target;
        while (current != null && current != root)
        {
            if (sb.Length > 0) sb.Insert(0, '/');
            sb.Insert(0, current.name);
            current = current.parent;
        }
        return sb.ToString();
    }

    // ============================================================
    //                          烘焙
    // ============================================================

    private void BakeSelectedEntries()
    {
        var selectedEntries = meshEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0) return;

        int processed = 0;
        int total = selectedEntries.Count;

        try
        {
            var prefabEntries = selectedEntries
                .Where(e => e.ownerType == MeshOwnerType.PrefabAsset && !string.IsNullOrEmpty(e.prefabAssetPath))
                .ToList();

            var bakedForPrefab = new Dictionary<MeshEntry, Mesh>();
            for (int i = 0; i < prefabEntries.Count; i++)
            {
                var entry = prefabEntries[i];
                EditorUtility.DisplayProgressBar("平滑法线烘焙",
                    $"烘焙 [P] ({i + 1}/{prefabEntries.Count}): {entry.displayName}",
                    (float)i / Mathf.Max(prefabEntries.Count, 1));

                Mesh srcMesh = ResolveSourceMesh(entry.mesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh, targetUVChannel);
                if (newMesh == null) { entry.status = "失败 (烘焙)"; continue; }

                Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                if (savedMesh == null) { entry.status = "失败 (保存)"; continue; }

                entry.lastBakedAssetPath = AssetDatabase.GetAssetPath(savedMesh);
                bakedForPrefab[entry] = savedMesh;
            }

            AssetDatabase.SaveAssets();

            var prefabGroups = prefabEntries
                .Where(e => bakedForPrefab.ContainsKey(e))
                .GroupBy(e => e.prefabAssetPath)
                .ToList();

            int groupIdx = 0;
            foreach (var group in prefabGroups)
            {
                groupIdx++;
                string sourcePrefabPath = group.Key;

                EditorUtility.DisplayProgressBar("平滑法线烘焙",
                    $"写回 Prefab ({groupIdx}/{prefabGroups.Count}): {Path.GetFileName(sourcePrefabPath)}",
                    (float)groupIdx / prefabGroups.Count);

                bool ok = ApplyBakedMeshesToOriginalPrefab(sourcePrefabPath, group, bakedForPrefab,
                    out int updated, out var verifyExpect, out var touchedPrefabPaths);

                if (!ok)
                {
                    foreach (var entry in group)
                    {
                        if (string.IsNullOrEmpty(entry.status) || (!entry.status.Contains("已完成") && !entry.status.Contains("失败")))
                            entry.status = "失败 (写回 Prefab 失败)";
                    }
                    continue;
                }

                processed += updated;

                foreach (var prefabPath in touchedPrefabPaths)
                {
                    VerifyPrefabPersistence(prefabPath, group, verifyExpect);
                }
            }

            var sceneAndOtherEntries = selectedEntries
                .Where(e => e.ownerType != MeshOwnerType.PrefabAsset)
                .ToList();

            for (int i = 0; i < sceneAndOtherEntries.Count; i++)
            {
                var entry = sceneAndOtherEntries[i];
                EditorUtility.DisplayProgressBar("平滑法线烘焙",
                    $"处理 ({i + 1}/{sceneAndOtherEntries.Count}): {entry.displayName}",
                    (float)i / sceneAndOtherEntries.Count);

                Mesh srcMesh = ResolveSourceMesh(entry.mesh);
                Mesh newMesh = BakeSmoothNormals(srcMesh, targetUVChannel);
                if (newMesh == null) { entry.status = "失败"; continue; }

                Mesh savedMesh = SaveMeshAsset(srcMesh, newMesh);
                if (savedMesh == null) { entry.status = "失败"; continue; }

                entry.lastBakedAssetPath = AssetDatabase.GetAssetPath(savedMesh);

                if (entry.ownerType == MeshOwnerType.ScenePrefabInstance && entry.sceneOwner != null)
                {
                    Component target = null;
                    if (entry.componentKind == ComponentKind.MeshFilter)
                    {
                        var mf = entry.sceneOwner.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh == entry.mesh)
                        {
                            Undo.RecordObject(mf, "Bake Smooth Normals");
                            mf.sharedMesh = savedMesh;
                            EditorUtility.SetDirty(mf);
                            target = mf;
                        }
                    }
                    else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                    {
                        var smr = entry.sceneOwner.GetComponent<SkinnedMeshRenderer>();
                        if (smr != null && smr.sharedMesh == entry.mesh)
                        {
                            Undo.RecordObject(smr, "Bake Smooth Normals");
                            smr.sharedMesh = savedMesh;
                            EditorUtility.SetDirty(smr);
                            target = smr;
                        }
                    }

                    bool applied = false;
                    List<string> appliedPaths = null;
                    if (target != null && PrefabUtility.IsPartOfPrefabInstance(entry.sceneOwner))
                    {
                        applied = TryApplyMeshOverrideToPrefab(target, out appliedPaths);
                    }

                    if (target == null)
                    {
                        entry.status = "失败 (组件未找到)";
                        Debug.LogWarning($"[平滑法线烘焙] [S] {entry.sceneOwner.name} 上未找到引用 {entry.mesh?.name} 的 {entry.componentKind}，跳过引用替换。");
                    }
                    else
                    {
                        string layersDesc = (appliedPaths != null && appliedPaths.Count > 0)
                            ? string.Join(" → ", appliedPaths.Select(Path.GetFileName))
                            : "<无>";
                        entry.status = applied
                            ? $"已完成 ✓ → 已 Apply 到 {(appliedPaths.Count == 1 ? Path.GetFileName(appliedPaths[0]) : appliedPaths.Count + " 层 prefab")}"
                            : "已完成 ✓ (Apply 失败)";
                        Debug.Log($"[平滑法线烘焙] [S] {entry.sceneOwner.name}: {entry.mesh?.name} → {savedMesh.name} (Apply={applied}, layers={layersDesc})");
                    }
                }
                else if (entry.ownerType == MeshOwnerType.Other)
                {
                    entry.status = "已完成 ✓ (仅 _SmoothN)";
                    Debug.Log($"[平滑法线烘焙] [O] {entry.displayName} → {AssetDatabase.GetAssetPath(savedMesh)} (未修改任何引用)");
                }

                entry.mesh = savedMesh;
                entry.assetPath = AssetDatabase.GetAssetPath(savedMesh);
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

        if (generateFbxVariantOnDrop && processed > 0)
        {
            var prefabPathsToProcess = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var entry in selectedEntries)
            {
                if (entry.ownerType == MeshOwnerType.PrefabAsset && !string.IsNullOrEmpty(entry.prefabAssetPath))
                    prefabPathsToProcess.Add(entry.prefabAssetPath);
                else if (entry.ownerType == MeshOwnerType.ScenePrefabInstance && entry.sceneOwner != null)
                {
                    var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(entry.sceneOwner);
                    if (outermost == null) outermost = entry.sceneOwner;
                    string srcPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(outermost);
                    if (!string.IsNullOrEmpty(srcPath) && srcPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                        prefabPathsToProcess.Add(srcPath);
                }
            }

            foreach (string prefabPath in prefabPathsToProcess)
            {
                Debug.Log($"[平滑法线烘焙] 烘焙后：为 '{prefabPath}' 扫描嵌套 FBX 并生成 Variant…");
                var variantPaths = GenerateFbxVariantsFromPrefab(prefabPath);
                if (variantPaths != null && variantPaths.Count > 0)
                {
                    Debug.Log($"[平滑法线烘焙] 已为 '{Path.GetFileName(prefabPath)}' 生成 {variantPaths.Count} 个 FBX Variant 并替换原 prefab 中的嵌套。");
                }
            }
        }

        if (processed > 0)
        {
            PostBakeVerifyMeshNames(selectedEntries);
        }

        Repaint();
    }

    private void PostBakeVerifyMeshNames(List<MeshEntry> entries)
    {
        var prefabCache = new Dictionary<string, GameObject>();

        foreach (var entry in entries)
        {
            if (entry.status != null && entry.status.Contains("失败")) continue;
            if (entry.status != null && entry.status.Contains("跳过")) continue;
            if (entry.ownerType == MeshOwnerType.Other) continue;

            Mesh actualMesh = null;

            if (entry.ownerType == MeshOwnerType.PrefabAsset && !string.IsNullOrEmpty(entry.prefabAssetPath))
            {
                if (!prefabCache.TryGetValue(entry.prefabAssetPath, out GameObject prefabRoot))
                {
                    prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabAssetPath);
                    prefabCache[entry.prefabAssetPath] = prefabRoot;
                }
                if (prefabRoot == null) continue;

                Transform target = string.IsNullOrEmpty(entry.componentTransformPath)
                    ? prefabRoot.transform
                    : prefabRoot.transform.Find(entry.componentTransformPath);
                if (target == null) continue;

                if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                {
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null) actualMesh = smr.sharedMesh;
                }
                else
                {
                    var mf = target.GetComponent<MeshFilter>();
                    if (mf != null) actualMesh = mf.sharedMesh;
                }
            }
            else if (entry.ownerType == MeshOwnerType.ScenePrefabInstance && entry.sceneOwner != null)
            {
                if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                {
                    var smr = entry.sceneOwner.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null) actualMesh = smr.sharedMesh;
                }
                else
                {
                    var mf = entry.sceneOwner.GetComponent<MeshFilter>();
                    if (mf != null) actualMesh = mf.sharedMesh;
                }
            }

            if (actualMesh == null) continue;

            if (!actualMesh.name.EndsWith(OutputSuffix))
            {
                entry.status = "⚠ Mesh 未替换";
                Debug.LogWarning($"[平滑法线烘焙] 验证失败: '{entry.displayName}' 的 mesh 仍为 '{actualMesh.name}'，未替换为 _SmoothN");
            }
        }
    }

    private static bool TryApplyMeshOverrideToPrefab(Component component, out List<string> outAppliedPaths)
    {
        outAppliedPaths = new List<string>();
        if (component == null) return false;
        if (!PrefabUtility.IsPartOfPrefabInstance(component.gameObject)) return false;

        Mesh targetMesh = null;
        if (component is MeshFilter mf) targetMesh = mf.sharedMesh;
        else if (component is SkinnedMeshRenderer smr) targetMesh = smr.sharedMesh;
        if (targetMesh == null) return false;

        bool anyApplied = false;
        GameObject cursor = component.gameObject;
        var visited = new HashSet<string>();

        while (cursor != null && PrefabUtility.IsPartOfPrefabInstance(cursor))
        {
            GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cursor);
            if (nearestRoot == null) break;

            string layerPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nearestRoot);
            if (string.IsNullOrEmpty(layerPath) || visited.Contains(layerPath))
            {
                if (nearestRoot.transform.parent == null) break;
                cursor = nearestRoot.transform.parent.gameObject;
                continue;
            }
            visited.Add(layerPath);

            if (IsModelAssetPath(layerPath))
            {
                Debug.Log($"[平滑法线烘焙] 跳过 Model Prefab 层：'{layerPath}'（FBX/Model 不可修改，继续向外层 Apply）");
                if (nearestRoot.transform.parent == null) break;
                cursor = nearestRoot.transform.parent.gameObject;
                continue;
            }

            try
            {
                if (component is MeshFilter mfc) mfc.sharedMesh = targetMesh;
                else if (component is SkinnedMeshRenderer smrc) smrc.sharedMesh = targetMesh;
                EditorUtility.SetDirty(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);

                var so = new SerializedObject(component);
                var prop = so.FindProperty("m_Mesh");
                if (prop == null) break;

                PrefabUtility.ApplyPropertyOverride(prop, layerPath, InteractionMode.AutomatedAction);
                outAppliedPaths.Add(layerPath);
                anyApplied = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[平滑法线烘焙] ApplyPropertyOverride 到 '{layerPath}' 失败: {e.Message}");
            }

            if (nearestRoot.transform.parent == null) break;
            cursor = nearestRoot.transform.parent.gameObject;
        }

        return anyApplied;
    }

    private static bool SetSharedMeshSafely(Component component, Mesh newMesh,
        out string outBeforeName, out bool outIsNested)
    {
        outBeforeName = "<null>";
        outIsNested = false;
        if (component == null) return false;

        Mesh before = null;
        if (component is MeshFilter mf) before = mf.sharedMesh;
        else if (component is SkinnedMeshRenderer smr) before = smr.sharedMesh;
        outBeforeName = before != null ? before.name : "<null>";
        outIsNested = PrefabUtility.IsPartOfPrefabInstance(component.gameObject);

        if (component is MeshFilter mfSet) mfSet.sharedMesh = newMesh;
        else if (component is SkinnedMeshRenderer smrSet) smrSet.sharedMesh = newMesh;

        var so = new SerializedObject(component);
        var prop = so.FindProperty("m_Mesh");
        if (prop != null)
        {
            prop.objectReferenceValue = newMesh;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (outIsNested)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        EditorUtility.SetDirty(component);

        Mesh after = null;
        if (component is MeshFilter mfChk) after = mfChk.sharedMesh;
        else if (component is SkinnedMeshRenderer smrChk) after = smrChk.sharedMesh;
        return after == newMesh;
    }

    private static int ReplaceSharedMeshReferences(GameObject root, Mesh oldMesh, Mesh newMesh, Transform excludeComponentOwner)
    {
        if (root == null || oldMesh == null || newMesh == null || oldMesh == newMesh) return 0;

        int count = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh != oldMesh) continue;
            if (mf.transform == excludeComponentOwner) continue;
            if (SetSharedMeshSafely(mf, newMesh, out _, out _)) count++;
        }
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null || smr.sharedMesh != oldMesh) continue;
            if (smr.transform == excludeComponentOwner) continue;
            if (SetSharedMeshSafely(smr, newMesh, out _, out _)) count++;
        }
        return count;
    }

    private static void VerifyPrefabPersistence(string prefabPath,
        IEnumerable<MeshEntry> entries,
        Dictionary<MeshEntry, Mesh> expectedMeshByEntry)
    {
        if (string.IsNullOrEmpty(prefabPath)) return;

        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        var verifyRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (verifyRoot == null)
        {
            Debug.LogError($"[平滑法线烘焙] 持久化验证: 无法重新加载 Prefab '{prefabPath}'");
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.status == null || (!entry.status.Contains("已完成") && !entry.status.Contains("已还原"))) continue;
            if (!expectedMeshByEntry.TryGetValue(entry, out Mesh expectedMesh) || expectedMesh == null) continue;

            Transform vt = string.IsNullOrEmpty(entry.componentTransformPath)
                ? verifyRoot.transform
                : verifyRoot.transform.Find(entry.componentTransformPath);
            if (vt == null)
            {
                Debug.LogError($"[平滑法线烘焙] ⚠ 持久化验证: '{entry.componentTransformPath}' 在重新加载的 Prefab 中找不到");
                entry.status = "失败 (验证: 路径丢失)";
                continue;
            }

            Mesh actualMesh = null;
            if (entry.componentKind == ComponentKind.MeshFilter)
                actualMesh = vt.GetComponent<MeshFilter>()?.sharedMesh;
            else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                actualMesh = vt.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;

            if (actualMesh == expectedMesh)
            {
                Debug.Log($"[平滑法线烘焙] ✓ 持久化验证通过: '{entry.componentTransformPath}' → '{actualMesh.name}'");
            }
            else
            {
                Debug.LogError($"[平滑法线烘焙] ⚠ 持久化验证失败: '{entry.componentTransformPath}' 实际 = '{actualMesh?.name ?? "<null>"}', 期望 '{expectedMesh.name}'");
                entry.status = "失败 (持久化未生效)";
            }
        }
    }

    // ============================================================
    //              Prefab 写回（直接修改原 prefab，含嵌套层）
    // ============================================================

    private bool ApplyBakedMeshesToOriginalPrefab(string originalPrefabPath,
        IEnumerable<MeshEntry> entries,
        Dictionary<MeshEntry, Mesh> bakedMeshes,
        out int updatedCount,
        out Dictionary<MeshEntry, Mesh> verifyExpect,
        out HashSet<string> touchedPrefabPaths)
    {
        updatedCount = 0;
        verifyExpect = new Dictionary<MeshEntry, Mesh>();
        touchedPrefabPaths = new HashSet<string>();

        if (string.IsNullOrEmpty(originalPrefabPath)) return false;

        GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(originalPrefabPath);
        if (originalPrefab == null)
        {
            Debug.LogError($"[平滑法线烘焙] 无法加载源 Prefab: {originalPrefabPath}");
            return false;
        }

        var previewScene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab, previewScene);
            if (instance == null)
            {
                Debug.LogError($"[平滑法线烘焙] InstantiatePrefab 失败: {originalPrefabPath}");
                return false;
            }

            foreach (var entry in entries)
            {
                if (!bakedMeshes.TryGetValue(entry, out Mesh savedMesh) || savedMesh == null) continue;

                Transform target = string.IsNullOrEmpty(entry.componentTransformPath)
                    ? instance.transform
                    : instance.transform.Find(entry.componentTransformPath);
                if (target == null)
                {
                    Debug.LogWarning($"[平滑法线烘焙] 在实例化 prefab 中找不到子物体 '{entry.componentTransformPath}'");
                    entry.status = "跳过 (路径未找到)";
                    continue;
                }

                Component comp = null;
                if (entry.componentKind == ComponentKind.MeshFilter)
                    comp = target.GetComponent<MeshFilter>();
                else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                    comp = target.GetComponent<SkinnedMeshRenderer>();

                if (comp == null)
                {
                    Debug.LogWarning($"[平滑法线烘焙] 在实例化 prefab 中找不到 {entry.componentKind} 组件 @'{entry.componentTransformPath}'");
                    entry.status = "失败 (组件未找到)";
                    continue;
                }

                string componentTag = entry.componentKind == ComponentKind.SkinnedMeshRenderer ? "SMR" : "MF";

                string beforeName = comp is MeshFilter mfb ? mfb.sharedMesh?.name
                                  : comp is SkinnedMeshRenderer smrb ? smrb.sharedMesh?.name
                                  : null;
                if (comp is MeshFilter mfa) mfa.sharedMesh = savedMesh;
                else if (comp is SkinnedMeshRenderer smra) smra.sharedMesh = savedMesh;
                EditorUtility.SetDirty(comp);

                bool applied = TryApplyMeshOverrideToPrefab(comp, out List<string> appliedPaths);
                if (!applied || appliedPaths.Count == 0)
                {
                    Debug.LogError($"[平滑法线烘焙] [P] {componentTag}@'{entry.componentTransformPath}' Apply 全部失败");
                    entry.status = "失败 (Apply 失败)";
                    continue;
                }

                foreach (var p in appliedPaths)
                {
                    if (!string.IsNullOrEmpty(p)) touchedPrefabPaths.Add(p);
                }

                entry.mesh = savedMesh;
                entry.assetPath = AssetDatabase.GetAssetPath(savedMesh);
                string layerLabel = appliedPaths.Count == 1
                    ? Path.GetFileName(appliedPaths[0])
                    : $"{appliedPaths.Count} 层 prefab";
                entry.status = $"已完成 ✓ → 已写回 {layerLabel}";
                verifyExpect[entry] = savedMesh;
                updatedCount++;
                Debug.Log($"[平滑法线烘焙] [P] {componentTag}@'{entry.componentTransformPath}': '{beforeName}' → '{savedMesh.name}', layers=[{string.Join(", ", appliedPaths.Select(Path.GetFileName))}]");
            }

            if (updatedCount == 0)
            {
                Debug.LogWarning($"[平滑法线烘焙] '{originalPrefabPath}' 没有任何 mesh 引用被修改。");
                return false;
            }

            return true;
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    // ============================================================
    //              场景扫描：删除 _SmoothN.asset 前的安全网
    // ============================================================

    private int SweepScenesAndRedirectFromSmoothN(HashSet<string> smoothNAssetPaths)
    {
        if (smoothNAssetPaths == null || smoothNAssetPaths.Count == 0) return 0;
        var pathSet = new HashSet<string>(smoothNAssetPaths, System.StringComparer.OrdinalIgnoreCase);

        int redirected = 0;
        var rootObjects = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            rootObjects.AddRange(scene.GetRootGameObjects());
        }

        var prefabsToReimport = new HashSet<string>();
        foreach (var root in rootObjects)
        {
            if (root == null) continue;

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (TryRedirectComponent(mf, pathSet, prefabsToReimport)) redirected++;
            }

            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in skinned)
            {
                if (TryRedirectComponent(smr, pathSet, prefabsToReimport)) redirected++;
            }
        }

        return redirected;
    }

    private bool TryRedirectComponent(Component comp, HashSet<string> targetPaths, HashSet<string> outPrefabsTouched)
    {
        if (comp == null) return false;

        Mesh current = null;
        if (comp is MeshFilter mf) current = mf.sharedMesh;
        else if (comp is SkinnedMeshRenderer smr) current = smr.sharedMesh;
        if (current == null) return false;

        string p = AssetDatabase.GetAssetPath(current);
        if (string.IsNullOrEmpty(p) || !targetPaths.Contains(p)) return false;

        Mesh original = FindOriginalMesh(current);
        if (original == null || original == current)
        {
            Debug.LogWarning($"[平滑法线烘焙] 场景扫描: 找不到 '{current.name}' 对应的原始 Mesh，{comp.gameObject.name} 上的引用未重定向，删除 _SmoothN 后会变 missing");
            return false;
        }

        Undo.RecordObject(comp, "Redirect _SmoothN to Original (sweep)");
        if (comp is MeshFilter mfc) mfc.sharedMesh = original;
        else if (comp is SkinnedMeshRenderer smrc) smrc.sharedMesh = original;
        EditorUtility.SetDirty(comp);

        List<string> appliedPaths = null;
        if (PrefabUtility.IsPartOfPrefabInstance(comp.gameObject))
        {
            TryApplyMeshOverrideToPrefab(comp, out appliedPaths);
            if (appliedPaths != null)
            {
                foreach (var appliedPath in appliedPaths)
                {
                    if (!string.IsNullOrEmpty(appliedPath)) outPrefabsTouched.Add(appliedPath);
                }
            }
        }

        string layersDesc = (appliedPaths != null && appliedPaths.Count > 0)
            ? " (Apply→" + string.Join(",", appliedPaths.Select(Path.GetFileName)) + ")"
            : "";
        Debug.Log($"[平滑法线烘焙] 场景扫描: {comp.gameObject.name}.{comp.GetType().Name} '{current.name}' → '{original.name}'" + layersDesc);
        return true;
    }

    // ============================================================
    //                          还原
    // ============================================================

    private void RestoreSelectedToOriginal()
    {
        var selectedEntries = meshEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0) return;

        int processed = 0;
        int skipped = 0;
        int total = selectedEntries.Count;

        var disconnectedAssetPaths = new HashSet<string>();

        try
        {
            var prefabGroups = selectedEntries
                .Where(e => e.ownerType == MeshOwnerType.PrefabAsset
                            && !string.IsNullOrEmpty(e.prefabAssetPath))
                .GroupBy(e => e.prefabAssetPath)
                .ToList();

            int groupIdx = 0;
            foreach (var group in prefabGroups)
            {
                groupIdx++;
                string sourcePrefabPath = group.Key;

                EditorUtility.DisplayProgressBar("用原始资源替换",
                    $"还原 Prefab ({groupIdx}/{prefabGroups.Count}): {Path.GetFileName(sourcePrefabPath)}",
                    (float)groupIdx / prefabGroups.Count);

                if (deleteSmoothNAssetOnRestore)
                {
                    foreach (var entry in group)
                    {
                        if (entry.mesh != null)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(entry.mesh);
                            if (!string.IsNullOrEmpty(assetPath)
                                && assetPath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase))
                            {
                                disconnectedAssetPaths.Add(assetPath);
                            }
                        }

                        if (!string.IsNullOrEmpty(entry.lastBakedAssetPath)
                            && entry.lastBakedAssetPath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase))
                        {
                            disconnectedAssetPaths.Add(entry.lastBakedAssetPath);
                        }
                    }
                }

                var restoreTargets = new Dictionary<MeshEntry, Mesh>();
                foreach (var entry in group)
                {
                    if (entry.mesh == null) { entry.status = "跳过 (mesh 为空)"; skipped++; continue; }
                    Mesh originalMesh = FindOriginalMesh(entry.mesh);
                    if (originalMesh == null || originalMesh == entry.mesh)
                    {
                        Debug.LogWarning($"[平滑法线烘焙] [P] 找不到 {entry.displayName} 对应的原始 Mesh，跳过");
                        entry.status = "跳过 (未找到原始)";
                        skipped++;
                        continue;
                    }
                    restoreTargets[entry] = originalMesh;
                }

                if (restoreTargets.Count == 0) continue;

                bool ok = ApplyBakedMeshesToOriginalPrefab(sourcePrefabPath, restoreTargets.Keys, restoreTargets,
                    out int updated, out var verifyExpect, out var touchedPrefabPaths);

                if (!ok)
                {
                    foreach (var entry in restoreTargets.Keys)
                    {
                        if (string.IsNullOrEmpty(entry.status) || (!entry.status.Contains("已完成") && !entry.status.Contains("失败")))
                            entry.status = "失败 (还原写回失败)";
                    }
                    continue;
                }

                foreach (var entry in restoreTargets.Keys)
                {
                    if (entry.status != null && entry.status.StartsWith("已完成"))
                    {
                        entry.status = entry.status.Replace("已完成 ✓", "已还原 ↺");
                    }
                    entry.lastBakedAssetPath = null;
                    entry.displayName = entry.mesh != null ? entry.mesh.name : entry.displayName;
                }

                processed += updated;

                foreach (var prefabPath in touchedPrefabPaths)
                {
                    VerifyPrefabPersistence(prefabPath, restoreTargets.Keys, verifyExpect);
                }
            }

            var others = selectedEntries
                .Where(e => e.ownerType != MeshOwnerType.PrefabAsset)
                .ToList();

            for (int i = 0; i < others.Count; i++)
            {
                var entry = others[i];

                if (entry.mesh == null) { skipped++; continue; }

                EditorUtility.DisplayProgressBar("用原始资源替换",
                    $"正在处理 ({i + 1}/{others.Count}): {entry.displayName}",
                    (float)i / others.Count);

                if (entry.ownerType == MeshOwnerType.Other)
                {
                    Debug.Log($"[平滑法线烘焙] [O] {entry.displayName} 不修改任何引用，跳过还原。");
                    entry.status = "跳过 ([O] 无引用)";
                    skipped++; continue;
                }

                if (!entry.mesh.name.EndsWith(OutputSuffix))
                {
                    Debug.Log($"[平滑法线烘焙] {entry.displayName} 已经是非 _SmoothN 资源，无需还原。");
                    entry.status = "跳过 (非 _SmoothN)";
                    skipped++; continue;
                }

                if (entry.sceneOwner == null) { entry.status = "跳过 (场景对象丢失)"; skipped++; continue; }

                Mesh originalMesh = FindOriginalMesh(entry.mesh);
                if (originalMesh == null || originalMesh == entry.mesh)
                {
                    Debug.LogWarning($"[平滑法线烘焙] 未找到 {entry.displayName} 对应的原始 Mesh，跳过。");
                    entry.status = "跳过 (未找到原始)";
                    skipped++; continue;
                }

                string oldAssetPath = AssetDatabase.GetAssetPath(entry.mesh);
                Component target = null;

                if (entry.componentKind == ComponentKind.MeshFilter)
                {
                    var mf = entry.sceneOwner.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == entry.mesh)
                    {
                        Undo.RecordObject(mf, "Restore Original Mesh");
                        mf.sharedMesh = originalMesh;
                        EditorUtility.SetDirty(mf);
                        target = mf;
                    }
                }
                else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                {
                    var smr = entry.sceneOwner.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh == entry.mesh)
                    {
                        Undo.RecordObject(smr, "Restore Original Mesh");
                        smr.sharedMesh = originalMesh;
                        EditorUtility.SetDirty(smr);
                        target = smr;
                    }
                }

                if (target == null) { entry.status = "跳过 (组件未找到)"; skipped++; continue; }

                bool applied = TryApplyMeshOverrideToPrefab(target, out List<string> appliedPaths);

                if (deleteSmoothNAssetOnRestore
                    && !string.IsNullOrEmpty(oldAssetPath)
                    && oldAssetPath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    disconnectedAssetPaths.Add(oldAssetPath);
                }

                entry.mesh = originalMesh;
                entry.assetPath = AssetDatabase.GetAssetPath(originalMesh);
                entry.displayName = originalMesh.name;
                string layersDesc = (appliedPaths != null && appliedPaths.Count > 0)
                    ? string.Join(" → ", appliedPaths.Select(Path.GetFileName))
                    : "<无>";
                entry.status = applied
                    ? $"已还原 ↺ → 已 Apply 到 {(appliedPaths.Count == 1 ? Path.GetFileName(appliedPaths[0]) : appliedPaths.Count + " 层 prefab")}"
                    : "已还原 ↺ (Apply 失败)";
                Debug.Log($"[平滑法线烘焙] [S] {entry.sceneOwner.name}: → {originalMesh.name} (Apply={applied}, layers={layersDesc})");
                processed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        int sweptRedirected = 0;
        if (deleteSmoothNAssetOnRestore && disconnectedAssetPaths.Count > 0)
        {
            sweptRedirected = SweepScenesAndRedirectFromSmoothN(disconnectedAssetPaths);
            if (sweptRedirected > 0)
            {
                Debug.Log($"[平滑法线烘焙] 场景扫描：删除前主动重定向 {sweptRedirected} 个仍引用 _SmoothN 的组件到原 mesh");
            }
        }

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
            if (sweptRedirected > 0) summary += $"，场景扫描重定向 {sweptRedirected} 个组件";
        }
        Debug.Log(summary);
        Repaint();
    }

    // ============================================================
    //                       通用工具方法
    // ============================================================

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
        searchDirs.Add("Assets");

        Mesh fallback = null;
        foreach (string searchDir in searchDirs)
        {
            if (string.IsNullOrEmpty(searchDir) || !AssetDatabase.IsValidFolder(searchDir)) continue;

            string[] guids = AssetDatabase.FindAssets(originalName, new[] { searchDir });
            foreach (string guid in guids)
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                if (candidatePath == assetPath) continue;
                if (candidatePath.EndsWith(OutputSuffix + ".asset", System.StringComparison.OrdinalIgnoreCase)) continue;

                bool isModel = AssetImporter.GetAtPath(candidatePath) is ModelImporter;
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(candidatePath);
                foreach (var sub in subAssets)
                {
                    if (sub is Mesh m && m.name == originalName)
                    {
                        if (isModel) return m;
                        if (fallback == null) fallback = m;
                    }
                }
            }

            if (fallback != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(fallback)) is ModelImporter)
                return fallback;
        }

        return fallback;
    }

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

    private Mesh BakeSmoothNormals(Mesh sourceMesh, int targetChannel)
    {
        if (sourceMesh == null) return null;
        targetChannel = Mathf.Clamp(targetChannel, 0, 7);

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

        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector4[] tangents = sourceMesh.tangents;
        BoneWeight[] boneWeights = sourceMesh.boneWeights;
        Matrix4x4[] bindposes = sourceMesh.bindposes;
        int subMeshCount = sourceMesh.subMeshCount;

        UVChannelData[] originalUVs = new UVChannelData[8];
        for (int c = 0; c < 8; c++)
        {
            originalUVs[c] = ReadUVChannel(sourceMesh, c);
        }

        int[][] subMeshTriangles = new int[subMeshCount][];
        List<int> allTrianglesList = new List<int>();
        for (int s = 0; s < subMeshCount; s++)
        {
            subMeshTriangles[s] = sourceMesh.GetTriangles(s);
            allTrianglesList.AddRange(subMeshTriangles[s]);
        }
        int[] allTriangles = allTrianglesList.ToArray();

        int vertexCount = vertices.Length;

        List<BlendShapeFrame> blendShapeFrames = ExtractBlendShapes(sourceMesh, vertexCount);

        Debug.Log($"[平滑法线烘焙] 源Mesh: {sourceMeshName}, 顶点数: {vertexCount}, 三角形索引数: {allTriangles.Length}, " +
                  $"subMeshCount: {subMeshCount}, BoneWeights: {boneWeights?.Length ?? 0}, BindPoses: {bindposes?.Length ?? 0}, " +
                  $"BlendShape帧数: {blendShapeFrames.Count}");

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
                if (j == 0) { lineA = v1 - v0; lineB = v2 - v0; }
                else if (j == 1) { lineA = v2 - v1; lineB = v0 - v1; }
                else { lineA = v0 - v2; lineB = v1 - v2; }

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
                weightSum += normalList[j].weight;

            Vector3 smoothNormal = Vector3.zero;
            for (int j = 0; j < normalList.Count; j++)
                smoothNormal += normalList[j].normal * normalList[j].weight / weightSum;

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

        RestoreReadable(wasReadable, modelImporter, sourceMesh, assetPath);

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
            mesh.SetTriangles(subMeshTriangles[s], s);

        for (int c = 0; c < 8; c++)
        {
            if (c == targetChannel) continue;
            WriteUVChannel(mesh, c, originalUVs[c]);
        }
        mesh.SetUVs(targetChannel, uvData);

        WriteBlendShapes(mesh, blendShapeFrames);

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

    private static List<BlendShapeFrame> ExtractBlendShapes(Mesh sourceMesh, int vertexCount)
    {
        var result = new List<BlendShapeFrame>();
        if (sourceMesh == null) return result;

        int blendShapeCount = sourceMesh.blendShapeCount;
        for (int s = 0; s < blendShapeCount; s++)
        {
            string shapeName = sourceMesh.GetBlendShapeName(s);
            int frameCount = sourceMesh.GetBlendShapeFrameCount(s);
            for (int f = 0; f < frameCount; f++)
            {
                var frame = new BlendShapeFrame
                {
                    shapeName = shapeName,
                    weight = sourceMesh.GetBlendShapeFrameWeight(s, f),
                    deltaVertices = new Vector3[vertexCount],
                    deltaNormals = new Vector3[vertexCount],
                    deltaTangents = new Vector3[vertexCount],
                };
                sourceMesh.GetBlendShapeFrameVertices(s, f, frame.deltaVertices, frame.deltaNormals, frame.deltaTangents);
                result.Add(frame);
            }
        }
        return result;
    }

    private static void WriteBlendShapes(Mesh mesh, List<BlendShapeFrame> frames)
    {
        if (mesh == null || frames == null || frames.Count == 0) return;
        mesh.ClearBlendShapes();
        foreach (var f in frames)
        {
            mesh.AddBlendShapeFrame(f.shapeName, f.weight, f.deltaVertices, f.deltaNormals, f.deltaTangents);
        }
    }

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
