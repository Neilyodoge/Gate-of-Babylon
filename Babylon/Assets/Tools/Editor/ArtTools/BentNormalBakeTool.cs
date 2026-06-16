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
/// 使用角度加权算法计算模型的平滑法线，转换到切线空间后存入指定 UV 通道（默认 UV1）的 xyz 中，
/// 主要供 TLOutline / PBRToon 等卡通描边方案使用（背面法线外扩描边法）。
///
/// ========== UV 通道分配约定（默认） ==========
///   UV0 (TEXCOORD0) : 主纹理坐标
///   UV1 (TEXCOORD1) : 平滑法线 (本工具默认写入此通道，供 TLOutline 描边使用)
///   UV2 (TEXCOORD2) : Lightmap / Bent Normal / 自定义数据
///   UV3 (TEXCOORD3) : 自定义数据
/// 工具内可选写入到 UV0~UV7 任意通道。
///
/// 注意：默认占用的是 UV1，如果模型的 UV1 已经被 Lightmap / 第二套展开占用，
/// 列表中会标记 ⚠ 警告。可以根据实际项目需要在工具里改为其它通道。
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
/// 在 Shader 中解码（默认 UV1）：
///   float3 snTS = uv1.xyz;  // 切线空间平滑法线
///   float3 smoothNormalOS = snTS.x * tangentOS.xyz + snTS.y * bitangentOS + snTS.z * normalOS;
///
/// ========== 来源类型与输出策略 ==========
///   [P] PrefabAsset           : Project 中的 .prefab 资源（Regular / Variant）
///                                烘焙后**直接修改原 prefab**：在 PreviewScene 中实例化原 prefab，
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
/// SkinnedMesh 处理：完整保留 boneWeights / bindposes / blendShapes，骨骼绑定与表情动画不受影响。
///
/// 还原功能（↺ 用原始资源替换）：
///   · [P] 与烘焙完全相同的写回机制，只是把 _SmoothN 换回原 mesh
///   · [S] 写回原始引用并逐层 Apply 到所有相关 prefab 源文件
///   · [O] 跳过
///   · 勾选「删除原始资源」会在删除前主动扫描场景把仍引用 _SmoothN 的组件切回原 mesh，使用中的不会被删除（无引用可改）
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

    /// <summary>
    /// BlendShape 单个帧的全部数据（按通道复制，用于 SkinnedMesh 表情）
    /// </summary>
    private struct BlendShapeFrame
    {
        public string shapeName;
        public float weight;
        public Vector3[] deltaVertices;
        public Vector3[] deltaNormals;
        public Vector3[] deltaTangents;
    }

    // ========== 来源类型 ==========

    /// <summary>
    /// 条目来源类型，决定烘焙后的写回策略
    /// </summary>
    private enum MeshOwnerType
    {
        PrefabAsset,         // [P] Project 中的 .prefab 资源 → 改 prefab 文件
        ScenePrefabInstance, // [S] 场景中的 prefab 实例     → 改场景 + 自动 Apply 到 prefab
        Other,               // [O] 普通 GameObject / Mesh / FBX → 仅生成 _SmoothN.asset
    }

    /// <summary>
    /// 关联的渲染组件类型
    /// </summary>
    private enum ComponentKind
    {
        None,
        MeshFilter,
        SkinnedMeshRenderer,
    }

    // ========== UI 状态 ==========

    /// <summary>
    /// 资源列表项
    /// </summary>
    private class MeshEntry
    {
        public Object sourceObject;     // 用户拖入/选中的源对象（用于 ping 跳转）
        public Mesh mesh;               // 当前关联的 mesh 引用
        public string displayName;
        public string assetPath;        // mesh 资源路径（如果有）
        public bool selected = true;
        public string status;
        public MeshOwnerType ownerType;
        public ComponentKind componentKind;

        // ownerType == PrefabAsset 时使用
        public string prefabAssetPath;
        public string componentTransformPath; // 相对 prefab root 的子物体路径，root 自身为空字符串

        // ownerType == ScenePrefabInstance 时使用
        public GameObject sceneOwner;

        // 烘焙阶段记下"曾经为这个 entry 生成过的 _SmoothN.asset 路径"。
        // 即使后续替换 SMR/MR 失败、entry.mesh 没切到 _SmoothN，还原时仍能定位到这些"孤儿"资源，
        // 配合"删除原始资源"选项把它们一起清理掉，避免目录里留下没人引用的垃圾文件。
        public string lastBakedAssetPath;
    }

    // 写入的 UV 通道（0~7，默认 UV3 = TEXCOORD3，对应本工程描边法线约定）
    private int targetUVChannel = 3;
    // 还原时是否同步删除被替换掉的 _SmoothN.asset 文件，默认关闭以防误删
    private bool deleteSmoothNAssetOnRestore = false;
    // 拖入 [P] prefab 时，是否为里面嵌套的 FBX 自动生成 PrefabVariant（同 FBX 目录、命名 = FBX 原名）
    // 开启后烘焙不再修改原 prefab，而是修改新生成的 Variant；后续别处可直接引用 Variant 拿到平滑法线
    private bool generateFbxVariantOnDrop = false;

    private List<MeshEntry> meshEntries = new List<MeshEntry>();
    private Vector2 scrollPosition;
    private bool showHelp = false;
    private bool autoTrackSelection = true; // 是否自动跟踪选中对象
    // 记录最近一次拖入的原始对象，显示在拖拽区域供用户点击定位
    private Object lastDroppedObject = null;

    private const string WindowTitle = "Outline平滑法线烘焙";

    [MenuItem("nTools/美术工具/平滑法线烘焙", false, 54)]
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

    // ============================================================
    //                          UI
    // ============================================================

    private void OnGUI()
    {
        // ══════════════ 顶部标题 + 模式切换 ══════════════
        GUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Outline 平滑法线烘焙", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        // 模式切换按钮 - 按下时高亮显示
        var prevBg = GUI.backgroundColor;
        if (batchModeEnabled)
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f, 1f);
        string modeLabel = batchModeEnabled ? "✓ 批量模式" : "  批量模式";
        var modeContent = new GUIContent(modeLabel,
            "开启后切换到批量处理面板，可对整个文件夹的 Prefab 进行扫描、检查、批量烘焙。");
        batchModeEnabled = GUILayout.Toggle(batchModeEnabled, modeContent, "Button", GUILayout.Width(80), GUILayout.Height(20));
        GUI.backgroundColor = prevBg;

        EditorGUILayout.EndHorizontal();

        // 分隔线
        DrawSeparator();

        if (batchModeEnabled)
        {
            // ══════════════ 批量处理面板 ══════════════
            DrawBatchPanel();
        }
        else
        {
            // ══════════════ 单体描边处理面板 ══════════════
            DrawSingleProcessPanel();
        }

        GUILayout.Space(4);
        DrawHelpFooter();
    }

    /// <summary>
    /// 绘制水平分隔线
    /// </summary>
    private void DrawSeparator()
    {
        GUILayout.Space(4);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f, 0.8f));
        GUILayout.Space(4);
    }

    /// <summary>
    /// 单体描边处理面板（原始流程）
    /// </summary>
    private void DrawSingleProcessPanel()
    {
        DrawDragDropArea();

        GUILayout.Space(6);

        DrawSettingsArea();

        GUILayout.Space(4);

        // 操作栏：清空 / 自动跟踪
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

        // ====== 列表标题栏 ======
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

        // ====== 底部按钮：烘焙 / 还原 ======
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
    }

    /// <summary>
    /// 绘制顶部设置区：UV 通道
    /// </summary>
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
                "默认 UV3，配合 TLOutline / PBRToon 描边使用。\n" +
                "若选中通道在某些 Mesh 上已有数据（如 Lightmap UV），将在列表中标注 ⚠。"),
            targetUVChannel,
            channelOptions,
            channelValues);
        targetUVChannel = Mathf.Clamp(targetUVChannel, 0, 7);

        GUILayout.Space(2);

        generateFbxVariantOnDrop = EditorGUILayout.ToggleLeft(
            new GUIContent("烘焙后为嵌套 FBX 生成 PrefabVariant",
                "烘焙完成后，自动扫描源 prefab 中嵌套的 FBX，生成 PrefabVariant 并替换进 prefab。\n" +
                "  · Variant 按 Prefab 名命名（多 FBX 时追加 FBX 名区分），防止重名\n" +
                "  · Variant 生成在 FBX 同目录\n" +
                "  · Variant 包含 prefab 对 FBX 子树的所有修改（组件 / 属性 / 子物体）\n" +
                "  · 原 prefab 中的 FBX 嵌套会被替换为 Variant 嵌套\n" +
                "  · 无嵌套 FBX 的 prefab 不受影响"),
            generateFbxVariantOnDrop);

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
                "角度加权平滑法线 → 切线空间 → 写入 UV 通道 xyz，供描边 Shader 使用。\n\n" +
                "[来源] [P] Prefab 资源  [S] 场景 Prefab 实例  [O] 其他（仅生成 _SmoothN.asset）\n" +
                "[输出] 在源 Mesh 同目录生成 _SmoothN.asset；[P]/[S] 自动逐层 Apply 写回所有嵌套 prefab\n" +
                "[FBX Variant] 勾选开关后，烘焙完成会为嵌套 FBX 生成同名 PrefabVariant 并替换进 prefab\n" +
                "[还原] ↺ 按钮把 mesh 改回原始；勾选「删除原始资源」会顺手删掉 _SmoothN.asset（使用中的不删）\n" +
                "[SkinnedMesh] 完整保留 boneWeights / bindposes / blendShapes\n\n" +
                "[Shader 解码] float3 snTS = uv1.xyz;\n" +
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

    /// <summary>
    /// 检测 Mesh 在指定 UV 通道是否已有数据
    /// </summary>
    private static bool MeshHasUVData(Mesh mesh, int channel)
    {
        if (mesh == null || channel < 0 || channel > 7) return false;
        VertexAttribute attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
        return mesh.HasVertexAttribute(attr);
    }

    // ============================================================
    //                       拖拽 / 选中 / 添加
    // ============================================================

    /// <summary>
    /// 自动同步当前选中对象到列表（替换模式，非追加）。
    /// 自动跟踪场景下，非 Prefab 对象会被静默忽略，仅在拖入时才弹窗提示。
    /// </summary>
    private void SyncFromSelection()
    {
        // 保留烘焙/还原产生的稳定结果，避免在选中变化时丢掉用户已经处理过的条目
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
                if (obj is GameObject) continue; // 已在上面处理
                AddObject(obj, silentSkipOther: true);
            }
        }
    }

    /// <summary>
    /// 绘制拖拽放置区域 + 上次拖入的原始对象引用
    /// </summary>
    private void DrawDragDropArea()
    {
        // 带色相的淡色背景区域
        Rect areaRect = EditorGUILayout.BeginVertical();
        // 绘制淡蓝绿色背景
        EditorGUI.DrawRect(areaRect, new Color(0.18f, 0.28f, 0.35f, 0.4f));

        GUILayout.Space(4);

        // 上次拖入的原始对象（只读显示，点击可在 Project / Hierarchy 中定位）
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        EditorGUILayout.LabelField("拖入对象", GUILayout.Width(56));
        GUI.enabled = false;
        EditorGUILayout.ObjectField(lastDroppedObject, typeof(Object), true);
        GUI.enabled = true;
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 拖拽目标区域
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        Rect dropRect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        // 拖放区样式：圆角虚线效果
        EditorGUI.DrawRect(dropRect, new Color(0.15f, 0.22f, 0.3f, 0.5f));
        // 绘制边框
        Handles.BeginGUI();
        Handles.color = new Color(0.4f, 0.7f, 0.85f, 0.6f);
        Handles.DrawLine(new Vector3(dropRect.xMin, dropRect.yMin), new Vector3(dropRect.xMax, dropRect.yMin));
        Handles.DrawLine(new Vector3(dropRect.xMax, dropRect.yMin), new Vector3(dropRect.xMax, dropRect.yMax));
        Handles.DrawLine(new Vector3(dropRect.xMax, dropRect.yMax), new Vector3(dropRect.xMin, dropRect.yMax));
        Handles.DrawLine(new Vector3(dropRect.xMin, dropRect.yMax), new Vector3(dropRect.xMin, dropRect.yMin));
        Handles.EndGUI();

        var dropLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        dropLabelStyle.normal.textColor = new Color(0.6f, 0.8f, 0.9f);
        GUI.Label(dropRect, "将 Prefab / 场景对象 / Mesh / FBX 拖放到此处", dropLabelStyle);

        GUILayout.Space(6);
        EditorGUILayout.EndVertical();

        // 处理拖放事件
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

    /// <summary>
    /// 根据对象类型自动判定来源，并加入列表。
    /// silentSkipOther = true 时，非 Prefab 对象会被静默忽略（用于自动跟踪选中场景）；
    /// silentSkipOther = false 时，非 Prefab 对象会先弹窗提示用户确认后再加入。
    /// </summary>
    private void AddObject(Object obj, bool silentSkipOther)
    {
        if (obj == null) return;

        if (obj is GameObject go)
        {
            // 1) Project 中的 Prefab 资源
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

                // FBX 等 Model（PrefabAssetType.Model），按 Other 处理
                AddOtherFromObject(obj, silentSkipOther);
                return;
            }

            // 2) 场景中的 Prefab 实例
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                AddScenePrefabInstanceEntries(go);
                return;
            }

            // 3) 其他 GameObject（普通场景对象 / PrefabStage 中的根 等）
            AddOtherFromGameObject(go, silentSkipOther);
            return;
        }

        // 非 GameObject（Mesh / Model / 其他）
        AddOtherFromObject(obj, silentSkipOther);
    }

    /// <summary>
    /// 从 Prefab 资源遍历所有 Mesh，添加为 [P] 条目
    /// </summary>
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
    //   嵌套 FBX → PrefabVariant 自动生成（generateFbxVariantOnDrop）
    // ============================================================

    /// <summary>
    /// 在已实例化到 PreviewScene 的 prefab 中扫描所有"嵌套 FBX 子树根" GameObject。
    /// 使用 GetCorrespondingObjectFromOriginalSource 追溯到最终源资源，比 IsAnyPrefabInstanceRoot 更可靠
    /// （后者在 PreviewScene 中不一定能正确识别嵌套 FBX 的 instance root）。
    ///
    /// 满足全部条件才算：
    ///   1. 原始源资源是 Model 文件（.fbx / .obj / .dae）
    ///   2. 原始源 GameObject 是该 Model 的根节点（parent == null）
    ///   3. 自身或子物体至少有一个 MeshFilter / SkinnedMeshRenderer.sharedMesh != null
    /// 同一 FBX 路径只返回一次（多次嵌套同一 FBX 只生成一次 Variant）。
    /// </summary>
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

            // 策略 1：GetCorrespondingObjectFromOriginalSource 追溯到最终源
            GameObject originalSrc = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);

            // 策略 2：GetCorrespondingObjectFromSource 获取直接源
            GameObject directSrc = PrefabUtility.GetCorrespondingObjectFromSource(go);

            // 策略 3：IsAnyPrefabInstanceRoot 传统检测
            bool isInstanceRoot = PrefabUtility.IsAnyPrefabInstanceRoot(go);

            string directPath = directSrc != null ? AssetDatabase.GetAssetPath(directSrc) : null;
            string originalPath = originalSrc != null ? AssetDatabase.GetAssetPath(originalSrc) : null;
            string nearestPath = null;
            try { nearestPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go); } catch { }

            // 只对第一层子物体或 instance root 打印详细日志，避免太多
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

            // 尝试用 originalSrc 检测（追溯到 FBX）
            string fbxPath = null;
            if (originalSrc != null && !string.IsNullOrEmpty(originalPath) && IsModelAssetPath(originalPath) && originalSrc.transform.parent == null)
            {
                fbxPath = originalPath;
            }
            // 兜底：用 directSrc 检测（直接嵌套 FBX 的情况）
            if (fbxPath == null && directSrc != null && !string.IsNullOrEmpty(directPath) && IsModelAssetPath(directPath) && directSrc.transform.parent == null)
            {
                fbxPath = directPath;
            }
            // 兜底：用 nearestPath 检测
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

    /// <summary>
    /// 为指定原 prefab 中嵌套的每个 FBX 子树生成一个 PrefabVariant，并把原 prefab 中嵌套的 FBX 实例
    /// 替换为对新 Variant 的嵌套引用。整个流程分两个阶段：
    ///
    /// 【Phase A】生成 Variant 文件
    ///   1. 在独立 PreviewScene 实例化原 prefab，得到 outerInstance；
    ///      此时 prefab 上对嵌套 FBX 子树做的全部 override（添加的组件 / 修改的属性 / 改名 / 子物体改动等）
    ///      已经反映在实例上。
    ///   2. 对每个嵌套 FBX root，先 SetParent(null) 让它脱离 outer 的 hierarchy
    ///      （仍保留它是 FBX prefab instance 的连接关系，没断开）。
    ///   3. 改名为 FBX 原文件名后调用 SaveAsPrefabAsset，Unity 自动建为 Variant of FBX，
    ///      把当前实例上所有相对 FBX 的 override（即 prefab 给 FBX 子树的修改 + 添加的组件）写入 Variant。
    ///   4. 处理 Unity 自动追加 " Variant" 后缀的命名约定：保存前先删掉已存在的同名 prefab 文件，
    ///      保存后比对实际生成路径与目标路径，不一致就 MoveAsset 强制改回。
    ///
    /// 【Phase B】把原 prefab 中嵌套的 FBX 实例替换为 Variant 实例
    ///   1. 在另一个独立 PreviewScene 重新实例化原 prefab，得到干净的 outerInstance；
    ///   2. 找到每个嵌套 FBX root，记录它的位置信息（parent / siblingIndex / transform / name / active / layer / tag）；
    ///   3. 删除 FBX root，在原位置 InstantiatePrefab(对应 Variant) 创建 Variant 实例，恢复位置信息；
    ///   4. SaveAsPrefabAsset 写回原 prefab 文件——之后原 prefab 内嵌套的就是 Variant 而不是 FBX。
    ///
    /// 返回成功生成的 Variant 资源路径列表（去重，按生成顺序）。
    /// 注意（极少见）：如果 outer prefab 上的脚本有拖拽引用指向 FBX 子树内部的具体子 GameObject（如某个 bone），
    /// Phase B 销毁旧 FBX 实例后这些引用会变成 null；通常 Animator / 脚本挂在 FBX root 自身上，
    /// 已在 Phase A 中随 Variant 一起保留，不受影响。
    /// </summary>
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

        // ===== Phase A: 生成 Variant 文件 =====
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

            // ★ 二次烘焙保护：在 Unpack 之前先剔除「当前嵌套已经就是本工具要生成的 Variant 实例」的项。
            // 典型场景：第一次烘焙时已经为 FBX 生成了同名 Variant 并替换了 prefab 内的嵌套，
            // 第二次烘焙时 FindNestedFbx 通过 originalSrc 仍能追溯到 FBX，但这一层其实是 Variant 包装。
            // 如果继续走 DeleteAsset+SaveAsPrefabAsset 流程，会先删掉已有的 Variant 资源，
            // 导致原 prefab 内对这个 Variant 的嵌套引用变 missing。
            // 同时把这些条目登记进 fbxToVariantMap，让 Phase B 能识别它们「已经是 Variant 实例」、不再替换。
            var filteredFbxRoots = new List<KeyValuePair<GameObject, string>>();
            foreach (var pair in fbxRoots)
            {
                GameObject root = pair.Key;
                string fbxPath = pair.Value;
                if (root == null || string.IsNullOrEmpty(fbxPath))
                {
                    filteredFbxRoots.Add(pair);
                    continue;
                }

                string fbxName = Path.GetFileNameWithoutExtension(fbxPath);
                string fbxDir = (Path.GetDirectoryName(fbxPath) ?? "").Replace('\\', '/');
                if (string.IsNullOrEmpty(fbxDir) || string.IsNullOrEmpty(fbxName))
                {
                    filteredFbxRoots.Add(pair);
                    continue;
                }
                string expectedVariantPath = $"{fbxDir}/{fbxName}.prefab";

                GameObject existingVariantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(expectedVariantPath);
                if (existingVariantAsset == null)
                {
                    // 首次烘焙：目标路径上还没有 Variant，正常走生成流程
                    filteredFbxRoots.Add(pair);
                    continue;
                }

                // ★ 用 GetPrefabAssetPathOfNearestInstanceRoot 判断 root 是哪个 prefab 资源的实例。
                // 注意：不能用 GetCorrespondingObjectFromSource — 嵌套场景下它返回的是「外层 prefab 视角下的
                // 同名 GameObject」，AssetPath 给出的是外层 prefab 路径，而不是真正的 Variant 路径，
                // 会导致 guard 误判 → DeleteAsset 删掉已有 Variant → outer prefab 留下 missing 嵌套引用。
                string nearestInstancePath = null;
                try { nearestInstancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root); } catch { }
                bool alreadyIsTargetVariant = !string.IsNullOrEmpty(nearestInstancePath)
                    && string.Equals(nearestInstancePath, expectedVariantPath, System.StringComparison.OrdinalIgnoreCase);

                if (alreadyIsTargetVariant)
                {
                    Debug.Log($"[平滑法线烘焙] '{root.name}' 已经是目标 Variant '{expectedVariantPath}' 的实例（nearestPath={nearestInstancePath}），跳过重新生成（避免误删原 prefab 内嵌套）。");
                    fbxToVariantMap[fbxPath] = expectedVariantPath;
                    if (!generatedPaths.Contains(expectedVariantPath))
                        generatedPaths.Add(expectedVariantPath);
                    continue;
                }

                filteredFbxRoots.Add(pair);
            }

            if (filteredFbxRoots.Count == 0)
            {
                Debug.Log($"[平滑法线烘焙] 「{Path.GetFileName(originalPrefabPath)}」 内所有嵌套 FBX 都已经是对应 Variant 的实例，无需重新生成。");
                return generatedPaths;
            }

            fbxRoots = filteredFbxRoots;

            // 解开外层 prefab instance 关联（仅 OutermostRoot 层），
            // 让嵌套的 FBX 子树变成独立的 prefab instance，
            // 否则 SaveAsPrefabAsset 会报 "Can't save part of a Prefab instance as a Prefab"
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

                    // 命名约定：先删除已存在的同名 prefab，避免 Unity 自动追加 " Variant" 后缀
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
                    {
                        AssetDatabase.DeleteAsset(variantPath);
                    }

                    // 把 FBX root 拉离 outer 的 hierarchy，仍保留它是 FBX prefab instance 的连接
                    fbxInstanceRoot.transform.SetParent(null, true);
                    // 改名为 FBX 原名，Variant 内部根节点的名字按 FBX 原文件名走
                    fbxInstanceRoot.name = fbxName;

                    bool ok;
                    GameObject variantAsset = PrefabUtility.SaveAsPrefabAsset(fbxInstanceRoot, variantPath, out ok);
                    if (!ok || variantAsset == null)
                    {
                        Debug.LogWarning($"[平滑法线烘焙] 生成 PrefabVariant 失败：{variantPath}（来源 FBX：{fbxPath}）");
                        continue;
                    }

                    // 兜底：若 Unity 因命名约定自动加了 " Variant" 后缀，强制改回目标路径
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

                    // 修正 prefab 内部根节点名：SaveAsPrefabAsset 可能把根节点名改成文件名（含 Variant 后缀），
                    // MoveAsset 只改了文件名，内部根节点名没有跟着变，需要手动修正为 FBX 原名。
                    // 注意：不能用 LoadPrefabContents+SaveAsPrefabAsset，那会破坏 Variant 与 FBX 的关联。
                    // 直接通过 SerializedObject 修改资源的 m_Name 属性即可。
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

        // ===== Phase B: 修改原 prefab，把嵌套 FBX 实例替换为 Variant 实例 =====
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

    /// <summary>
    /// Phase B：在独立 PreviewScene 中重新实例化原 prefab，把嵌套的 FBX 实例替换为对应 Variant 实例，
    /// 然后 SaveAsPrefabAsset 写回原 prefab 文件。
    ///
    /// 关键策略：
    ///   · 完整保留原 FBX 实例的位置信息（parent / siblingIndex / localTransform / name / active / layer / tag），
    ///     新创建的 Variant 实例放在原位置；
    ///   · Variant 内部已经包含 prefab 给 FBX 子树的所有 override（来自 Phase A），
    ///     因此 Variant 实例本身在 outer prefab 中通常不需要额外 override；
    ///   · 极少见：如果 outer prefab 的脚本字段拖拽引用了 FBX 子树内部的具体子 GameObject（如某个 bone），
    ///     替换后这些引用变 null（旧实例被 Destroy）；通常 Animator / 脚本挂在 FBX root 自身上，不受影响。
    ///
    /// 返回成功替换的 FBX 实例数量。
    /// </summary>
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

        // Phase B 使用 LoadPrefabContents 直接编辑 prefab 资源，不需要 PreviewScene
        // 这比 InstantiatePrefab+SaveAsPrefabAsset 更可靠，能正确处理嵌套 prefab 替换
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

                // ★ 二次烘焙保护：如果当前嵌套已经就是目标 Variant 的实例，跳过销毁/重建。
                // 用 GetPrefabAssetPathOfNearestInstanceRoot 判断（嵌套场景下 GetCorrespondingObjectFromSource
                // 会返回外层视角的 GameObject，AssetPath 是外层 prefab 而不是 Variant 自身，不可靠）。
                string nearestInstancePathB = null;
                try { nearestInstancePathB = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fbxInstanceRoot); } catch { }
                if (!string.IsNullOrEmpty(nearestInstancePathB)
                    && string.Equals(nearestInstancePathB, variantPath, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[平滑法线烘焙] Phase B：'{fbxInstanceRoot.name}' 已经是 Variant '{variantPath}' 的实例（nearestPath={nearestInstancePathB}），跳过替换。");
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

                // 在 prefab editing context 中使用 InstantiatePrefab 创建 Variant 的嵌套实例
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
                // 不改名，保留 Variant 自己的名字（FBX 原文件名）
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

    /// <summary>
    /// 从场景中的 Prefab 实例遍历所有 Mesh，添加为 [S] 条目
    /// </summary>
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

    /// <summary>
    /// 从普通 GameObject 提取 Mesh，添加为 [O] 条目（弹窗确认后才会真正加入）
    /// </summary>
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

    /// <summary>
    /// 从普通 Object（Mesh / Model 文件）提取 Mesh，添加为 [O] 条目（弹窗确认后才会真正加入）
    /// </summary>
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

    /// <summary>
    /// 弹窗提示用户：当前对象不是 Prefab，无法保存到原始资源，是否仍要继续添加。
    /// </summary>
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

    /// <summary>
    /// 检查 Mesh 是否已在列表中（避免重复添加）。同一个 Mesh 出现在不同 owner 上视为不同条目。
    /// </summary>
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

    /// <summary>
    /// 从 Mesh 创建列表条目（仅设置共通字段，owner 相关字段由调用方填充）
    /// </summary>
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

    /// <summary>
    /// 计算 target 相对 root 的子物体路径（不含 root 自身），root == target 时返回空字符串。
    /// </summary>
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

    /// <summary>
    /// 烘焙列表中已勾选的条目，按来源类型分流处理：
    /// - [P] 按 prefabAssetPath 分组，在 PreviewScene 实例化原 prefab 后，逐层 Apply 把 sharedMesh 改成 _SmoothN，
    ///       直接修改原 prefab 文件（包括所有嵌套层）
    /// - [S] 修改场景对象引用，并自动逐层 ApplyPropertyOverride 写回所有相关 prefab 源文件
    /// - [O] 仅生成 _SmoothN.asset，不修改任何引用
    /// </summary>
    private void BakeSelectedEntries()
    {
        var selectedEntries = meshEntries.Where(e => e.selected).ToList();
        if (selectedEntries.Count == 0) return;

        int processed = 0;
        int total = selectedEntries.Count;

        try
        {
            // ========== 第一阶段：[P] 烘焙 → 直接写回原 prefab（包括所有嵌套层）==========
            // 关键设计：直接修改原 prefab，不创建 Variant；用 [S] 已经验证过的「逐层 Apply」机制：
            //   1A：所有 [P] 类 mesh 先烘焙落盘（生成 _SmoothN.asset）。
            //   1B：在 PreviewScene 中实例化原 prefab → 修改 SMR/MF.sharedMesh →
            //       TryApplyMeshOverrideToPrefab 沿 prefab 嵌套链一路 Apply 到 outermost。
            //
            // 为什么烘焙必须在 LoadPrefabContents 之外完成：
            //   BakeSmoothNormals 内部为了让源 Mesh 可读会触发 ModelImporter.SaveAndReimport / SaveAssets，
            //   引发 AssetDatabase.Refresh。如果这一步发生在 prefab 修改过程中，可能导致 instance 上刚 set 的
            //   sharedMesh（尤其是 SkinnedMeshRenderer）被刷新回原值。两阶段隔离能避免这个时序问题。
            var prefabEntries = selectedEntries
                .Where(e => e.ownerType == MeshOwnerType.PrefabAsset && !string.IsNullOrEmpty(e.prefabAssetPath))
                .ToList();

            // 阶段 1A：先烘焙所有 [P] 类 Mesh，记录每个 entry 对应的 _SmoothN.asset
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

                // 记下"曾经为这个 entry 生成过的 _SmoothN.asset"，无论后续 prefab 写回是否成功，
                // 都能在还原+删除时定位到这个文件，避免成为无引用的孤儿。
                entry.lastBakedAssetPath = AssetDatabase.GetAssetPath(savedMesh);
                bakedForPrefab[entry] = savedMesh;
            }

            // 让所有资源 IO 在进入 prefab 写回之前彻底落盘
            AssetDatabase.SaveAssets();

            // 阶段 1B：按源 prefab 分组，把烘焙后的 mesh 直接写回原 prefab（逐层 Apply 到所有嵌套层）
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

                // 持久化校验：只对源 prefab 校验。
                // entry.componentTransformPath 是相对源 prefab 根记录的（如 "Root/SM_xxx/part_yyy"），
                // 嵌套层 prefab（Variant、子 prefab 等）的内部层级根节点不同，把同一个 path 喂给嵌套层
                // 会得到 "路径未找到" 的误报。源 prefab 是用户最终面对的资源，验证它就足够。
                // 嵌套层已经由 ApplyPropertyOverride 写回，不必单独再验证。
                VerifyPrefabPersistence(sourcePrefabPath, group, verifyExpect);
            }

            // ========== 第二阶段：[S] / [O] 单条独立处理 ==========
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
                    // 临时禁用 Animator，防止修改 sharedMesh 触发 rebinding 污染 controller 引用
                    // 从 prefab 实例根开始搜索，因为 Animator 通常在 SMR 的父级节点上
                    var prefabRoot = PrefabUtility.IsPartOfPrefabInstance(entry.sceneOwner)
                        ? PrefabUtility.GetOutermostPrefabInstanceRoot(entry.sceneOwner)
                        : entry.sceneOwner;
                    var animatorStates = TemporarilyDisableAnimators(prefabRoot != null ? prefabRoot : entry.sceneOwner);

                    Component target = null;
                    if (entry.componentKind == ComponentKind.MeshFilter)
                    {
                        var mf = entry.sceneOwner.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh == entry.mesh)
                        {
                            // 通过 SerializedObject 设置 mesh，避免直接赋值触发 Animator rebinding
                            var soScene = new SerializedObject(mf);
                            var meshProp = soScene.FindProperty("m_Mesh");
                            if (meshProp != null)
                            {
                                meshProp.objectReferenceValue = savedMesh;
                                soScene.ApplyModifiedProperties();
                            }
                            target = mf;
                        }
                    }
                    else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                    {
                        var smr = entry.sceneOwner.GetComponent<SkinnedMeshRenderer>();
                        if (smr != null && smr.sharedMesh == entry.mesh)
                        {
                            // 通过 SerializedObject 设置 mesh，避免直接赋值触发 Animator rebinding
                            var soScene = new SerializedObject(smr);
                            var meshProp = soScene.FindProperty("m_Mesh");
                            if (meshProp != null)
                            {
                                meshProp.objectReferenceValue = savedMesh;
                                soScene.ApplyModifiedProperties();
                            }
                            target = smr;
                        }
                    }

                    bool applied = false;
                    List<string> appliedPaths = null;
                    if (target != null && PrefabUtility.IsPartOfPrefabInstance(entry.sceneOwner))
                    {
                        applied = TryApplyMeshOverrideToPrefab(target, out appliedPaths);
                    }

                    // 恢复 Animator 启用状态
                    RestoreAnimators(animatorStates);

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

        // ========== 烘焙后：开关开启时，为嵌套 FBX 生成 PrefabVariant 并替换进源 prefab ==========
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

        // ========== 烘焙后自动检查：验证 prefab 中的 mesh 是否已替换为 _SmoothN ==========
        if (processed > 0)
        {
            PostBakeVerifyMeshNames(selectedEntries);
        }

        Repaint();
    }

    /// <summary>
    /// 烘焙后自动验证：重新加载每个 [P] prefab，检查对应组件的 mesh 是否已替换为 _SmoothN。
    /// 有问题的 entry 会被标记为 "⚠ Mesh 未替换"，在列表中显示为红色。
    /// </summary>
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

    /// <summary>
    /// 把场景 Prefab 实例上某个 component 的 m_Mesh 属性 Apply 回到所有相关的 prefab 源文件。
    /// 关键：沿 prefab 嵌套链从 nearest 一路 Apply 到 outermost，每一层 prefab 都被写入。
    /// 这样不论用户后续拖 outer prefab 还是嵌套的 inner prefab 进工具，都能看到修改。
    ///
    /// MeshFilter 与 SkinnedMeshRenderer 的属性名都是 m_Mesh。
    /// 实现思路：
    ///   1. 从 component 所在 GameObject 出发，找到 nearest prefab instance root
    ///   2. 重新在 instance 上确保 m_Mesh override 存在（用 SerializedObject 写一次）
    ///   3. ApplyPropertyOverride 到这一层的 prefab 路径 → 该层 prefab 文件被更新
    ///   4. 跳到该 instance root 的父节点，循环（处理嵌套外层）
    ///   5. 每一次 Apply 后 instance 上的 override 会被消化掉，所以下一轮要重写 sharedMesh 再造 override
    ///
    /// outAppliedPaths 输出所有被 Apply 到的 prefab 路径列表（按 nearest → outermost 顺序）。
    /// </summary>
    private static bool TryApplyMeshOverrideToPrefab(Component component, out List<string> outAppliedPaths)
    {
        outAppliedPaths = new List<string>();
        if (component == null) return false;
        if (!PrefabUtility.IsPartOfPrefabInstance(component.gameObject)) return false;

        // 记下目标 mesh，每一层 Apply 后要重新写到 instance 上以维持 override
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
                // 已经处理过这一层（保护无限循环），跳到上一层
                if (nearestRoot.transform.parent == null) break;
                cursor = nearestRoot.transform.parent.gameObject;
                continue;
            }
            visited.Add(layerPath);

            // 跳过 Model Prefab（FBX / OBJ / DAE 等）：Unity 不允许修改 Model 资源
            // 只有 .prefab 文件才能被 ApplyPropertyOverride 写入
            if (IsModelAssetPath(layerPath))
            {
                Debug.Log($"[平滑法线烘焙] 跳过 Model Prefab 层：'{layerPath}'（FBX/Model 不可修改，继续向外层 Apply）");
                if (nearestRoot.transform.parent == null) break;
                cursor = nearestRoot.transform.parent.gameObject;
                continue;
            }

            try
            {
                // 通过 SerializedObject 写 m_Mesh 属性，而非直接赋值 sharedMesh：
                // 直接赋值 SMR.sharedMesh 会触发 Animator rebinding 回调，导致同级
                // Animator 的 controller 引用被交换、骨骼轴向被重算（污染 transform）。
                // SerializedObject 路径只修改序列化数据，不触发 C++ 层的 rebinding 回调。
                var so = new SerializedObject(component);
                var prop = so.FindProperty("m_Mesh");
                if (prop == null) break;

                prop.objectReferenceValue = targetMesh;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.ApplyPropertyOverride(prop, layerPath, InteractionMode.AutomatedAction);
                outAppliedPaths.Add(layerPath);
                anyApplied = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[平滑法线烘焙] ApplyPropertyOverride 到 '{layerPath}' 失败: {e.Message}");
            }

            // 跳到外一层 prefab instance（如果还有的话）
            if (nearestRoot.transform.parent == null) break;
            cursor = nearestRoot.transform.parent.gameObject;
        }

        return anyApplied;
    }

    /// <summary>
    /// 安全地设置 MeshFilter / SkinnedMeshRenderer 的 sharedMesh，覆盖以下场景：
    /// - 直接挂在 outer prefab root 上的组件 (普通赋值即可)
    /// - 嵌套 prefab 实例（如 outer prefab 引用了 FBX 作为子节点）上的组件
    ///   (需要 SerializedObject 写入以生成 override)
    /// 全部通过 SerializedObject 路径操作，避免直接赋值 sharedMesh 触发 Animator rebinding。
    /// 返回是否成功修改。outBeforeName 输出修改前的 mesh 名，outIsNested 输出是否属于嵌套 prefab 实例。
    /// </summary>
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

        // 通过 SerializedObject 写 m_Mesh，避免直接赋值触发 Animator rebinding
        var so = new SerializedObject(component);
        var prop = so.FindProperty("m_Mesh");
        if (prop != null)
        {
            prop.objectReferenceValue = newMesh;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(component);

        // 验证
        Mesh after = null;
        if (component is MeshFilter mfChk) after = mfChk.sharedMesh;
        else if (component is SkinnedMeshRenderer smrChk) after = smrChk.sharedMesh;
        return after == newMesh;
    }

    /// <summary>
    /// 在 root 子树内，把所有引用 oldMesh 的 MeshFilter / SkinnedMeshRenderer 都替换为 newMesh。
    /// excludeComponentOwner 指定的 transform 上的对应组件不计入计数（它已在调用方主流程中处理过）。
    /// 返回额外替换的组件数量。
    /// </summary>
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

    // ============================================================
    //          Animator 保护：防止修改 mesh 时 rebinding 污染 controller
    // ============================================================

    /// <summary>
    /// 禁用实例子树内的所有 Animator 组件（用于 PreviewScene 中的 prefab 实例）。
    /// 修改 SkinnedMeshRenderer.sharedMesh 时，Unity 会触发 Animator rebinding，
    /// 可能导致同级 Animator 的 runtimeAnimatorController 引用被意外交换。
    /// 在 PreviewScene 中不需要恢复，因为实例最终会被 DestroyImmediate。
    /// </summary>
    private static void DisableAnimatorsOnInstance(GameObject root)
    {
        if (root == null) return;
        var animators = root.GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            if (anim != null && anim.enabled)
            {
                anim.enabled = false;
            }
        }
        if (animators.Length > 0)
        {
            Debug.Log($"[平滑法线烘焙] 已禁用 {animators.Length} 个 Animator 组件（防止 mesh 修改触发 rebinding）");
        }
    }

    /// <summary>
    /// 临时禁用指定层级内的所有 Animator 组件，返回之前的启用状态以便恢复。
    /// 用于 [S] 场景 prefab 实例路径：在修改 mesh + Apply 期间禁用，操作完成后恢复。
    /// </summary>
    private static List<KeyValuePair<Animator, bool>> TemporarilyDisableAnimators(GameObject root)
    {
        var states = new List<KeyValuePair<Animator, bool>>();
        if (root == null) return states;

        var animators = root.GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            if (anim != null)
            {
                states.Add(new KeyValuePair<Animator, bool>(anim, anim.enabled));
                if (anim.enabled)
                {
                    anim.enabled = false;
                }
            }
        }
        return states;
    }

    /// <summary>
    /// 恢复 Animator 组件的启用状态（与 TemporarilyDisableAnimators 配对使用）。
    /// </summary>
    private static void RestoreAnimators(List<KeyValuePair<Animator, bool>> states)
    {
        if (states == null) return;
        foreach (var pair in states)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }
    }

    /// <summary>
    /// 持久化校验：把 prefab 重新从磁盘加载，按 entry 的路径与组件类型读出当前 sharedMesh，
    /// 与 expectedMeshByEntry 中的目标 mesh 对比。任何不一致都视为持久化失败并改写 entry.status。
    /// </summary>
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

    /// <summary>
    /// 把烘焙后的 mesh 直接写回原 prefab（不生成 Variant），处理多层嵌套场景：
    /// 1. 在隐藏 PreviewScene 中实例化原 prefab → 得到一个 prefab instance
    /// 2. 在 instance 上把 SMR/MF 的 sharedMesh 改成 _SmoothN
    /// 3. 用 TryApplyMeshOverrideToPrefab **逐层 Apply** → 沿 prefab 嵌套链一路写到 outermost
    ///    这跟 [S] 用的是同一套机制，能稳定修改多层嵌套结构（outer prefab → 嵌套 FBX Variant）
    /// 4. 销毁实例 + 关闭 PreviewScene
    ///
    /// 注意：本方法**不**调用 SaveAsPrefabAsset，因为 ApplyPropertyOverride 已经直接写到了 prefab 文件。
    /// </summary>
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

            // 禁用所有 Animator 组件，防止修改 SkinnedMeshRenderer.sharedMesh 时
            // 触发 Animator rebinding，导致 runtimeAnimatorController 引用被意外污染
            // （表现为同级别的 Animator 的 controller 被互换）
            DisableAnimatorsOnInstance(instance);

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

                // 1) 通过 SerializedObject 写 m_Mesh（避免直接赋值触发 Animator rebinding 改变轴向）
                string beforeName = comp is MeshFilter mfb ? mfb.sharedMesh?.name
                                  : comp is SkinnedMeshRenderer smrb ? smrb.sharedMesh?.name
                                  : null;
                {
                    var soInit = new SerializedObject(comp);
                    var propInit = soInit.FindProperty("m_Mesh");
                    if (propInit != null)
                    {
                        propInit.objectReferenceValue = savedMesh;
                        soInit.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                // 2) 逐层 Apply：从 SMR 所在的 nearest prefab 一路写到 outermost
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

    /// <summary>
    /// 删除 _SmoothN.asset 之前的安全网：扫描所有打开的场景，把任何 SMR/MF 上仍引用着「待删 _SmoothN 资源」
    /// 的 sharedMesh 主动切回 originalMesh（FindOriginalMesh 推导）。
    /// 如果组件位于 prefab instance，自动 Apply 到最外层 prefab。
    /// 这样不论 [S] 是否替换成功、不论场景里其他对象是否在工具列表外引用了 _SmoothN，
    /// 真正 DeleteAsset 时都不会留下 missing reference。
    /// 返回成功重定向的组件数量。
    /// </summary>
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

    /// <summary>
    /// 单个 MeshFilter / SkinnedMeshRenderer 的重定向：
    /// - 若 sharedMesh 路径不在待删集合中，直接返回 false
    /// - 否则尝试找原 mesh 替换；如果是 prefab instance，再 Apply 到最外层 prefab
    /// </summary>
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

        // 临时禁用 Animator，防止修改 sharedMesh 触发 rebinding 污染 controller 引用
        var sweepPrefabRoot = PrefabUtility.IsPartOfPrefabInstance(comp.gameObject)
            ? PrefabUtility.GetOutermostPrefabInstanceRoot(comp.gameObject)
            : comp.gameObject;
        var sweepAnimatorStates = TemporarilyDisableAnimators(sweepPrefabRoot != null ? sweepPrefabRoot : comp.gameObject);

        // 通过 SerializedObject 修改 mesh，避免直接赋值触发 Animator rebinding
        {
            var soSweep = new SerializedObject(comp);
            var meshProp = soSweep.FindProperty("m_Mesh");
            if (meshProp != null)
            {
                meshProp.objectReferenceValue = original;
                soSweep.ApplyModifiedProperties();
            }
        }

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

        // 恢复 Animator 启用状态
        RestoreAnimators(sweepAnimatorStates);

        string layersDesc = (appliedPaths != null && appliedPaths.Count > 0)
            ? " (Apply→" + string.Join(",", appliedPaths.Select(Path.GetFileName)) + ")"
            : "";
        Debug.Log($"[平滑法线烘焙] 场景扫描: {comp.gameObject.name}.{comp.GetType().Name} '{current.name}' → '{original.name}'" + layersDesc);
        return true;
    }

    // ============================================================
    //                          还原
    // ============================================================

    /// <summary>
    /// 把选中条目当前引用的 mesh_SmoothN 还原为原始 mesh。
    /// 按来源类型分流：
    /// - [P] 在 PreviewScene 中实例化原 prefab，把 sharedMesh 改回原 mesh，逐层 Apply 写回所有嵌套层
    /// - [S] 修改场景引用并逐层 ApplyPropertyOverride
    /// - [O] 跳过
    /// </summary>
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
            // ========== 第一阶段：[P] 在 PreviewScene 中把 mesh 改回原 mesh，逐层 Apply 到所有嵌套 prefab 层 ==========
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

                // 收集所有 _SmoothN.asset 用于可选删除：
                // 1) entry.mesh 当前指向的 _SmoothN（替换成功时的常规情况）
                // 2) entry.lastBakedAssetPath（替换失败时 entry.mesh 没切到 _SmoothN，但磁盘上仍有孤儿资源）
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

                // 为每个 entry 解析它的"目标原 mesh"——即烘焙前的 mesh
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

                // 用与烘焙完全相同的写回机制（只是把 bakedMesh 换成 originalMesh）
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

                // ApplyBakedMeshesToOriginalPrefab 把 status 写成"已完成 ✓ → 已写回 ..."，这里改成还原语义
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

                // 同烘焙：只验证源 prefab，避免拿外层相对路径去校验嵌套层 prefab 时误报「路径未找到」
                VerifyPrefabPersistence(sourcePrefabPath, restoreTargets.Keys, verifyExpect);
            }

            // ========== 第二阶段：[S] / [O] 单条独立处理 ==========
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

                // 临时禁用 Animator，防止修改 sharedMesh 触发 rebinding 污染 controller 引用
                var restorePrefabRoot = PrefabUtility.IsPartOfPrefabInstance(entry.sceneOwner)
                    ? PrefabUtility.GetOutermostPrefabInstanceRoot(entry.sceneOwner)
                    : entry.sceneOwner;
                var animatorStatesRestore = TemporarilyDisableAnimators(restorePrefabRoot != null ? restorePrefabRoot : entry.sceneOwner);

                if (entry.componentKind == ComponentKind.MeshFilter)
                {
                    var mf = entry.sceneOwner.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == entry.mesh)
                    {
                        var soRestore = new SerializedObject(mf);
                        var meshProp = soRestore.FindProperty("m_Mesh");
                        if (meshProp != null)
                        {
                            meshProp.objectReferenceValue = originalMesh;
                            soRestore.ApplyModifiedProperties();
                        }
                        target = mf;
                    }
                }
                else if (entry.componentKind == ComponentKind.SkinnedMeshRenderer)
                {
                    var smr = entry.sceneOwner.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh == entry.mesh)
                    {
                        var soRestore = new SerializedObject(smr);
                        var meshProp = soRestore.FindProperty("m_Mesh");
                        if (meshProp != null)
                        {
                            meshProp.objectReferenceValue = originalMesh;
                            soRestore.ApplyModifiedProperties();
                        }
                        target = smr;
                    }
                }

                if (target == null)
                {
                    RestoreAnimators(animatorStatesRestore);
                    entry.status = "跳过 (组件未找到)"; skipped++; continue;
                }

                bool applied = TryApplyMeshOverrideToPrefab(target, out List<string> appliedPaths);

                // 恢复 Animator 启用状态
                RestoreAnimators(animatorStatesRestore);

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

        // 删除前的安全网：扫所有打开场景，把仍引用 _SmoothN 的 SMR/MF 主动切回 originalMesh
        // 这一步必须放在 DeleteAsset 之前，否则 sharedMesh 引用会先变 missing。
        int sweptRedirected = 0;
        if (deleteSmoothNAssetOnRestore && disconnectedAssetPaths.Count > 0)
        {
            sweptRedirected = SweepScenesAndRedirectFromSmoothN(disconnectedAssetPaths);
            if (sweptRedirected > 0)
            {
                Debug.Log($"[平滑法线烘焙] 场景扫描：删除前主动重定向 {sweptRedirected} 个仍引用 _SmoothN 的组件到原 mesh");
            }
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
            if (sweptRedirected > 0) summary += $"，场景扫描重定向 {sweptRedirected} 个组件";
        }
        Debug.Log(summary);
        Repaint();
    }

    // ============================================================
    //                       通用工具方法
    // ============================================================

    /// <summary>
    /// 查找带 _SmoothN 后缀的 Mesh 对应的原始 Mesh（不带后缀的同名资源）。
    /// 搜索顺序：所在目录 → 上一级目录 → 全工程；优先返回 FBX 等 Model 子 Mesh，
    /// 其次是任意带该名称的 Mesh。
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
    /// 核心算法：角度加权平滑法线 + 切线空间转换，写入指定 UV 通道的 xyz（3通道编码）。
    /// 完整保留 boneWeights / bindposes / blendShapes，确保 SkinnedMesh 不丢失骨骼绑定与表情。
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

        // ====== 提取 BlendShape 数据（SkinnedMesh 表情，必须保留） ======
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

        // ====== 第四阶段：编码平滑法线 ======
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
            mesh.SetTriangles(subMeshTriangles[s], s);

        for (int c = 0; c < 8; c++)
        {
            if (c == targetChannel) continue;
            WriteUVChannel(mesh, c, originalUVs[c]);
        }
        mesh.SetUVs(targetChannel, uvData);

        // 写入 BlendShape（必须在 vertices/normals 设置完毕后执行，且顶点数一致）
        WriteBlendShapes(mesh, blendShapeFrames);

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
    /// 提取所有 BlendShape 帧数据（包含每帧的 deltaVertices / deltaNormals / deltaTangents）。
    /// </summary>
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

    /// <summary>
    /// 把缓存的 BlendShape 帧写回新 Mesh
    /// </summary>
    private static void WriteBlendShapes(Mesh mesh, List<BlendShapeFrame> frames)
    {
        if (mesh == null || frames == null || frames.Count == 0) return;
        mesh.ClearBlendShapes();
        foreach (var f in frames)
        {
            mesh.AddBlendShapeFrame(f.shapeName, f.weight, f.deltaVertices, f.deltaNormals, f.deltaTangents);
        }
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
    /// 持久化烘焙结果：始终在源 Mesh 同目录生成 _SmoothN.asset。
    /// 二次烘焙策略：如果目标路径已存在 _SmoothN.asset，使用 EditorUtility.CopySerialized
    /// **原地全字段覆盖**（顶点 / 法线 / 切线 / 全部 UVs / SubMesh / BindPose / BoneWeights /
    /// BlendShape 等所有序列化字段都会被 newMesh 的数据替换），效果等价于「重新生成数据」，
    /// 但 **保留原 GUID**，所有指向该资源的引用（outer prefab / Variant / 场景 / 其它 prefab）
    /// 都保持有效，不会出现 MeshRenderer 上 mesh missing 的情况。
    ///
    /// 注：早期版本曾尝试 DeleteAsset + CreateAsset 的写法，会让旧 GUID 失效。
    /// 在多层嵌套 prefab（outer prefab → Variant → FBX）场景下，烘焙阶段 1B 的
    /// ApplyPropertyOverride 并不能稳定地把新 GUID 写回到 Variant 这一层，
    /// 实际表现为：二次烘焙后 outer prefab 内 MeshRenderer 的 mesh 引用变 missing。
    /// 所以这里坚持用 CopySerialized 原地覆盖。
    ///
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

        // ★ 二次烘焙：原地用 CopySerialized 整体覆盖现有 _SmoothN.asset 的全部序列化字段，
        // 保留原 GUID。这样指向该资源的引用（含 Variant 层 m_Mesh override）都不会失效。
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
        if (existing != null)
        {
            Debug.Log($"[平滑法线烘焙] 检测到已有 _SmoothN，原地覆盖（保留 GUID 避免引用 missing）：{outputPath}");
            string oldName = existing.name;
            EditorUtility.CopySerialized(newMesh, existing);
            existing.name = oldName;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            // 强制重导，确保 Unity 内存中的 mesh 数据与磁盘一致，避免后续 ApplyPropertyOverride
            // 拿到旧数据视图（旧版本上确认过的偶发问题）
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            Object.DestroyImmediate(newMesh);
            return existing;
        }

        AssetDatabase.CreateAsset(newMesh, outputPath);
        AssetDatabase.SaveAssets();
        return newMesh;
    }

    // ============================================================
    //                    公开批处理 API
    // ============================================================

    /// <summary>
    /// 批量烘焙结果（单个 prefab 的处理结果）
    /// </summary>
    public struct BatchBakeResult
    {
        public string prefabPath;
        public int meshCount;
        public bool success;
        public string error;
    }

    /// <summary>
    /// 批量对指定路径下的所有 Prefab 执行平滑法线烘焙。
    /// 直接调用 AddPrefabAssetEntries 绕过 PrefabAssetType 检查，确保所有 .prefab 都能被处理。
    /// </summary>
    public static List<BatchBakeResult> BatchBakePrefabs(
        string[] prefabPaths,
        int uvChannel = 3,
        bool generateVariant = false,
        bool showProgress = true)
    {
        var results = new List<BatchBakeResult>();
        if (prefabPaths == null || prefabPaths.Length == 0) return results;

        uvChannel = Mathf.Clamp(uvChannel, 0, 7);

        var baker = CreateInstance<SmoothNormalBaker>();
        baker.targetUVChannel = uvChannel;
        baker.generateFbxVariantOnDrop = generateVariant;

        try
        {
            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string path = prefabPaths[i];
                string name = Path.GetFileNameWithoutExtension(path);

                if (showProgress)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("批量平滑法线烘焙",
                        $"({i + 1}/{prefabPaths.Length}) {name}",
                        (float)i / prefabPaths.Length))
                    {
                        Debug.Log($"[平滑法线烘焙-批量] 烘焙被用户取消，已完成 {results.Count(r => r.success)}");
                        break;
                    }
                }

                var result = new BatchBakeResult { prefabPath = path };

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    result.error = "无法加载 Prefab";
                    results.Add(result);
                    continue;
                }

                // 直接调用 AddPrefabAssetEntries，绕过 AddObject 的 PrefabAssetType 检查
                baker.meshEntries.Clear();
                baker.AddPrefabAssetEntries(prefab, path);

                if (baker.meshEntries.Count == 0)
                {
                    result.success = true;
                    result.meshCount = 0;
                    results.Add(result);
                    continue;
                }

                foreach (var entry in baker.meshEntries)
                    entry.selected = true;

                result.meshCount = baker.meshEntries.Count;

                try
                {
                    baker.BakeSelectedEntries();
                    result.success = true;
                    Debug.Log($"[平滑法线烘焙-批量] ({i + 1}/{prefabPaths.Length}) {name}: 成功烘焙 {result.meshCount} 个 Mesh");
                }
                catch (System.Exception ex)
                {
                    result.error = ex.Message;
                    Debug.LogError($"[平滑法线烘焙-批量] 烘焙失败: {name}\n{ex}");
                }

                results.Add(result);
            }
        }
        finally
        {
            if (showProgress)
                EditorUtility.ClearProgressBar();
            DestroyImmediate(baker);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return results;
    }

    /// <summary>
    /// 批量烘焙指定目录下所有 Prefab 的平滑法线（便捷重载）。
    /// </summary>
    public static List<BatchBakeResult> BatchBakePrefabsInFolder(
        string folderPath,
        int uvChannel = 3,
        bool generateVariant = false,
        bool showProgress = true)
    {
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[平滑法线烘焙-批量] 无效的目录: {folderPath}");
            return new List<BatchBakeResult>();
        }

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        var paths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .ToArray();

        if (paths.Length == 0)
        {
            Debug.LogWarning($"[平滑法线烘焙-批量] 目录 '{folderPath}' 中没有找到 Prefab");
            return new List<BatchBakeResult>();
        }

        Debug.Log($"[平滑法线烘焙-批量] 在 '{folderPath}' 中找到 {paths.Length} 个 Prefab，开始批量烘焙...");
        return BatchBakePrefabs(paths, uvChannel, generateVariant, showProgress);
    }

    // ============================================================
    //                    批处理 UI
    // ============================================================

    private bool showBatchPanel = false;
    private bool batchModeEnabled = false; // 批量模式开关
    private string batchFolderPath = "Assets/BundleResources/Prefabs/TopLegendCharacters";
    private Vector2 batchScrollPos;

    /// <summary>
    /// Prefab 检查结果条目
    /// </summary>
    private class PrefabCheckEntry
    {
        public string path;
        public string name;
        public bool selected = true;
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();
        public int meshCount;
        public int animatorCount;
        public bool hasIssues => warnings.Count > 0 || errors.Count > 0;
        public bool foldout = false;
    }

    private List<PrefabCheckEntry> batchPrefabEntries = new List<PrefabCheckEntry>();
    private bool batchChecked = false;
    private string batchFilterText = "";

    /// <summary>
    /// 绘制批量烘焙面板
    /// </summary>
    private void DrawBatchPanel()
    {
        // 批量模式标题
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("📁 批量烘焙 / 检查", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (batchChecked)
        {
            int issueCount = batchPrefabEntries.Count(e => e.hasIssues);
            var statusStyle = new GUIStyle(EditorStyles.miniLabel);
            if (issueCount > 0)
            {
                statusStyle.normal.textColor = new Color(1f, 0.7f, 0.3f);
                GUILayout.Label($"⚠ {issueCount} 个有问题", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.4f, 0.9f, 0.5f);
                GUILayout.Label("✓ 全部正常", statusStyle);
            }
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 目录选择区域（淡蓝色背景，和单体模式拖入区保持一致）
        Rect folderAreaRect = EditorGUILayout.BeginVertical();
        EditorGUI.DrawRect(folderAreaRect, new Color(0.18f, 0.28f, 0.35f, 0.4f));
        GUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        GUILayout.Label("Prefab 目录", GUILayout.Width(76));

        Rect dropRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        batchFolderPath = EditorGUI.TextField(dropRect, batchFolderPath);

        // 处理拖拽事件
        if (dropRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    string dragPath = AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0]);
                    if (!string.IsNullOrEmpty(dragPath) && AssetDatabase.IsValidFolder(dragPath))
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    else
                        DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
                Event.current.Use();
            }
            else if (Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    string dragPath = AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0]);
                    if (!string.IsNullOrEmpty(dragPath) && AssetDatabase.IsValidFolder(dragPath))
                    {
                        batchFolderPath = dragPath;
                        ScanBatchPrefabs();
                    }
                }
                Event.current.Use();
            }
        }

        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择 Prefab 目录", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                    batchFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                else
                    batchFolderPath = selected;
            }
        }
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        // 扫描按钮行
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(6);
        if (GUILayout.Button("扫描目录", GUILayout.Width(72), GUILayout.Height(22)))
        {
            ScanBatchPrefabs();
        }
        if (GUILayout.Button("扫描并检查", GUILayout.Width(84), GUILayout.Height(22)))
        {
            ScanBatchPrefabs();
            CheckAllBatchPrefabs();
        }
        GUILayout.Space(8);
        GUILayout.Label($"共 {batchPrefabEntries.Count} 个 Prefab", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        EditorGUILayout.EndVertical();

        GUILayout.Space(4);

        // 列表过滤 + 选择
        if (batchPrefabEntries.Count > 0)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("过滤:", EditorStyles.miniLabel, GUILayout.Width(28));
            batchFilterText = EditorGUILayout.TextField(batchFilterText, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("仅问题", EditorStyles.toolbarButton, GUILayout.Width(50)))
                batchFilterText = "⚠";
            if (GUILayout.Button("清除", EditorStyles.toolbarButton, GUILayout.Width(36)))
                batchFilterText = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                foreach (var e in batchPrefabEntries) e.selected = true;
            if (GUILayout.Button("取消", EditorStyles.miniButtonMid, GUILayout.Width(40)))
                foreach (var e in batchPrefabEntries) e.selected = false;
            if (GUILayout.Button("选问题", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                foreach (var e in batchPrefabEntries) e.selected = e.hasIssues;
            }
            GUILayout.Space(8);
            int selCount = batchPrefabEntries.Count(e => e.selected);
            GUILayout.Label($"已选 {selCount}/{batchPrefabEntries.Count}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            // Prefab 列表（自适应高度）
            float listHeight = Mathf.Clamp(batchPrefabEntries.Count * 22 + 10, 80, 320);
            batchScrollPos = EditorGUILayout.BeginScrollView(batchScrollPos, GUILayout.ExpandHeight(true), GUILayout.MinHeight(listHeight));
            DrawBatchPrefabList();
            EditorGUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Space(20);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("请指定目录后点击「扫描目录」", EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(20);
        }

        GUILayout.Space(6);

        // 操作按钮区
        EditorGUI.BeginDisabledGroup(batchPrefabEntries.Count == 0);

        EditorGUILayout.BeginHorizontal();
        int selectedForBake = batchPrefabEntries.Count(e => e.selected);
        EditorGUI.BeginDisabledGroup(selectedForBake == 0);
        if (GUILayout.Button($"▶ 批量烘焙选中 ({selectedForBake})", GUILayout.Height(32)))
        {
            ExecuteBatchBake();
        }
        EditorGUI.EndDisabledGroup();

        int unbaked = batchPrefabEntries.Count(e => e.hasIssues && HasUnbakedMeshWarning(e));
        EditorGUI.BeginDisabledGroup(unbaked == 0);
        if (GUILayout.Button($"修复未烘焙 ({unbaked})", GUILayout.Height(32)))
        {
            FixUnbakedPrefabs();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        GUILayout.Space(4);

        // 批量模式下折叠显示烘焙设置
        showBatchPanel = EditorGUILayout.Foldout(showBatchPanel, "烘焙设置（UV通道 / Variant）", true);
        if (showBatchPanel)
        {
            DrawSettingsArea();
        }
    }

    private void DrawBatchPrefabList()
    {
        bool hasFilter = !string.IsNullOrEmpty(batchFilterText);

        for (int i = 0; i < batchPrefabEntries.Count; i++)
        {
            var entry = batchPrefabEntries[i];

            if (hasFilter)
            {
                if (batchFilterText == "⚠")
                {
                    if (!entry.hasIssues) continue;
                }
                else if (entry.name.IndexOf(batchFilterText, System.StringComparison.OrdinalIgnoreCase) < 0
                    && entry.path.IndexOf(batchFilterText, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

            if (entry.errors.Count > 0)
                EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.1f, 0.1f, 0.3f));
            else if (entry.warnings.Count > 0)
                EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.4f, 0.1f, 0.2f));
            else if (i % 2 == 0)
                EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, 0.3f));

            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16));

            string icon = entry.errors.Count > 0 ? "❌" : entry.warnings.Count > 0 ? "⚠" : "✓";
            GUILayout.Label(icon, GUILayout.Width(16));

            if (GUILayout.Button(entry.name, EditorStyles.miniLabel))
            {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                    Selection.activeObject = obj;
                }
            }

            GUILayout.FlexibleSpace();
            if (entry.meshCount > 0)
                GUILayout.Label($"M:{entry.meshCount}", EditorStyles.miniLabel, GUILayout.Width(36));
            if (entry.animatorCount > 0)
                GUILayout.Label($"A:{entry.animatorCount}", EditorStyles.miniLabel, GUILayout.Width(28));

            if (entry.hasIssues)
            {
                entry.foldout = EditorGUILayout.Foldout(entry.foldout, "", true);
            }

            EditorGUILayout.EndHorizontal();

            if (entry.foldout && entry.hasIssues)
            {
                EditorGUI.indentLevel += 2;
                foreach (var err in entry.errors)
                    EditorGUILayout.LabelField($"  ❌ {err}", EditorStyles.miniLabel);
                foreach (var warn in entry.warnings)
                    EditorGUILayout.LabelField($"  ⚠ {warn}", EditorStyles.miniLabel);
                EditorGUI.indentLevel -= 2;
            }
        }
    }

    private void ScanBatchPrefabs()
    {
        batchPrefabEntries.Clear();
        batchChecked = false;

        if (string.IsNullOrEmpty(batchFolderPath) || !AssetDatabase.IsValidFolder(batchFolderPath))
            return;

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { batchFolderPath });
        var paths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p)
            .ToArray();

        foreach (var path in paths)
        {
            batchPrefabEntries.Add(new PrefabCheckEntry
            {
                path = path,
                name = Path.GetFileNameWithoutExtension(path)
            });
        }
    }

    private void CheckAllBatchPrefabs()
    {
        try
        {
            for (int i = 0; i < batchPrefabEntries.Count; i++)
            {
                var entry = batchPrefabEntries[i];

                if (EditorUtility.DisplayCancelableProgressBar("检查 Prefab",
                    $"({i + 1}/{batchPrefabEntries.Count}) {entry.name}",
                    (float)i / batchPrefabEntries.Count))
                    break;

                CheckSinglePrefab(entry);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // 有问题的排到最前面
        batchPrefabEntries.Sort((a, b) =>
        {
            int scoreA = a.errors.Count > 0 ? 2 : a.warnings.Count > 0 ? 1 : 0;
            int scoreB = b.errors.Count > 0 ? 2 : b.warnings.Count > 0 ? 1 : 0;
            if (scoreA != scoreB) return scoreB.CompareTo(scoreA);
            return string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
        });

        batchChecked = true;
        int issueCount = batchPrefabEntries.Count(e => e.hasIssues);
        Debug.Log($"[平滑法线烘焙-检查] 完成检查 {batchPrefabEntries.Count} 个 Prefab，{issueCount} 个有问题");
    }

    /// <summary>
    /// 对单个 Prefab 执行全面检查
    /// </summary>
    private static void CheckSinglePrefab(PrefabCheckEntry entry)
    {
        entry.warnings.Clear();
        entry.errors.Clear();
        entry.meshCount = 0;
        entry.animatorCount = 0;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
        if (prefab == null)
        {
            entry.errors.Add("无法加载 Prefab");
            return;
        }

        // ========== Animator Controller 检查 ==========
        var animators = prefab.GetComponentsInChildren<Animator>(true);
        entry.animatorCount = animators.Length;

        foreach (var anim in animators)
        {
            if (anim == null) continue;
            string animPath = GetCheckHierarchyPath(prefab.transform, anim.transform);

            if (anim.runtimeAnimatorController == null)
            {
                entry.warnings.Add($"Animator 无 Controller: {animPath}");
            }
        }

        // 检测同级 Animator Controller 是否被互换
        var animatorsByParent = animators
            .Where(a => a != null && a.transform.parent != null)
            .GroupBy(a => a.transform.parent);
        foreach (var group in animatorsByParent)
        {
            var siblings = group.ToList();
            if (siblings.Count > 1)
            {
                for (int i = 0; i < siblings.Count; i++)
                {
                    var anim = siblings[i];
                    if (anim.runtimeAnimatorController == null) continue;
                    string ctrlName = anim.runtimeAnimatorController.name.ToLower();
                    string objName = anim.gameObject.name.ToLower();

                    for (int j = 0; j < siblings.Count; j++)
                    {
                        if (i == j) continue;
                        string otherName = siblings[j].gameObject.name.ToLower();
                        if (ctrlName.Contains(otherName) && !ctrlName.Contains(objName) && otherName.Length >= 3)
                        {
                            string path = GetCheckHierarchyPath(prefab.transform, anim.transform);
                            entry.errors.Add($"Controller 疑似互换: '{anim.gameObject.name}' 上的 Controller '{anim.runtimeAnimatorController.name}' 可能属于 '{siblings[j].gameObject.name}' [{path}]");
                        }
                    }
                }
            }
        }

        // ========== Mesh 引用检查（重点：是否已烘焙 _SmoothN）==========
        var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null) continue;
            entry.meshCount++;
            string mfPath = GetCheckHierarchyPath(prefab.transform, mf.transform);

            if (mf.sharedMesh == null)
            {
                entry.errors.Add($"MeshFilter.sharedMesh 为空: {mfPath}");
            }
            else
            {
                string meshName = mf.sharedMesh.name;
                if (!meshName.EndsWith("_SmoothN"))
                {
                    entry.warnings.Add($"MeshFilter 未烘焙（mesh: '{meshName}'）: {mfPath}");
                }
            }
        }

        var smrs = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr == null) continue;
            entry.meshCount++;
            string smrPath = GetCheckHierarchyPath(prefab.transform, smr.transform);

            if (smr.sharedMesh == null)
            {
                entry.errors.Add($"SkinnedMeshRenderer.sharedMesh 为空: {smrPath}");
            }
            else
            {
                string meshName = smr.sharedMesh.name;
                if (!meshName.EndsWith("_SmoothN"))
                {
                    entry.warnings.Add($"SMR 未烘焙（mesh: '{meshName}'）: {smrPath}");
                }

                if (smr.bones != null && smr.bones.Length > 0)
                {
                    int nullBones = smr.bones.Count(b => b == null);
                    if (nullBones > 0)
                    {
                        entry.warnings.Add($"SMR 有 {nullBones} 个空骨骼引用: {smrPath}");
                    }
                }

                if (smr.sharedMesh.bindposes != null && smr.sharedMesh.bindposes.Length > 0
                    && smr.bones != null && smr.bones.Length != smr.sharedMesh.bindposes.Length)
                {
                    entry.warnings.Add($"骨骼数({smr.bones.Length})与 bindposes 数({smr.sharedMesh.bindposes.Length})不一致: {smrPath}");
                }
            }

            if (smr.rootBone == null && smr.bones != null && smr.bones.Length > 0)
            {
                entry.warnings.Add($"SMR 无 rootBone: {smrPath}");
            }
        }

        // ========== Transform 检查 ==========
        var transforms = prefab.GetComponentsInChildren<Transform>(true);
        foreach (var t in transforms)
        {
            if (t == null) continue;
            string tPath = GetCheckHierarchyPath(prefab.transform, t);

            Vector3 s = t.localScale;
            if (s.x == 0 || s.y == 0 || s.z == 0)
            {
                entry.warnings.Add($"Transform Scale 含零: ({s.x:F2}, {s.y:F2}, {s.z:F2}) [{tPath}]");
            }

            if (s.x < 0 || s.y < 0 || s.z < 0)
            {
                if (t.GetComponent<Renderer>() != null)
                {
                    entry.warnings.Add($"Transform 负 Scale（法线翻转）: ({s.x:F2}, {s.y:F2}, {s.z:F2}) [{tPath}]");
                }
            }

            if (float.IsNaN(t.localPosition.x) || float.IsNaN(t.localPosition.y) || float.IsNaN(t.localPosition.z)
                || float.IsInfinity(t.localPosition.x) || float.IsInfinity(t.localPosition.y) || float.IsInfinity(t.localPosition.z))
            {
                entry.errors.Add($"Transform Position 含 NaN/Infinity [{tPath}]");
            }

            if (float.IsNaN(t.localRotation.x) || float.IsNaN(t.localRotation.y)
                || float.IsNaN(t.localRotation.z) || float.IsNaN(t.localRotation.w))
            {
                entry.errors.Add($"Transform Rotation 含 NaN [{tPath}]");
            }
        }
    }

    private static string GetCheckHierarchyPath(Transform root, Transform target)
    {
        if (root == null || target == null || target == root) return target != null ? target.name : "";
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

    private void ExecuteBatchBake()
    {
        var selectedPaths = batchPrefabEntries
            .Where(e => e.selected)
            .Select(e => e.path)
            .ToArray();

        if (selectedPaths.Length == 0) return;

        if (!EditorUtility.DisplayDialog("确认批量烘焙",
            $"将对选中的 {selectedPaths.Length} 个 Prefab 执行平滑法线烘焙。\n\n" +
            $"  · 写入通道: UV{targetUVChannel}\n" +
            $"  · 生成 FBX Variant: {(generateFbxVariantOnDrop ? "是" : "否")}\n\n" +
            "此操作不可撤销，确定继续？",
            "开始执行", "取消"))
            return;

        var results = BatchBakePrefabs(selectedPaths, targetUVChannel, generateFbxVariantOnDrop);
        int success = results.Count(r => r.success);
        int failed = results.Count(r => !r.success);
        int totalMeshes = results.Where(r => r.success).Sum(r => r.meshCount);

        string summary = $"批量烘焙完成！\n\n" +
            $"  成功: {success} 个 Prefab（共 {totalMeshes} 个 Mesh）\n" +
            $"  失败: {failed} 个";
        if (failed > 0)
        {
            summary += "\n\n失败列表:\n" + string.Join("\n",
                results.Where(r => !r.success).Select(r => $"  · {Path.GetFileNameWithoutExtension(r.prefabPath)}: {r.error}"));
        }

        Debug.Log($"[平滑法线烘焙-批量] {summary}");
        EditorUtility.DisplayDialog("批量烘焙完成", summary, "确定");

        CheckAllBatchPrefabs();
    }

    private static bool HasUnbakedMeshWarning(PrefabCheckEntry entry)
    {
        foreach (var w in entry.warnings)
        {
            if (w.Contains("未烘焙")) return true;
        }
        return false;
    }

    private void FixUnbakedPrefabs()
    {
        var unbakedPaths = batchPrefabEntries
            .Where(e => e.hasIssues && HasUnbakedMeshWarning(e))
            .Select(e => e.path)
            .ToArray();

        if (unbakedPaths.Length == 0) return;

        if (!EditorUtility.DisplayDialog("修复未烘焙 Mesh",
            $"将对 {unbakedPaths.Length} 个含未烘焙 Mesh 的 Prefab 执行平滑法线烘焙。\n\n" +
            $"  · 写入通道: UV{targetUVChannel}\n" +
            $"  · 生成 FBX Variant: {(generateFbxVariantOnDrop ? "是" : "否")}\n\n" +
            "此操作不可撤销，确定继续？",
            "开始修复", "取消"))
            return;

        var results = BatchBakePrefabs(unbakedPaths, targetUVChannel, generateFbxVariantOnDrop);
        int success = results.Count(r => r.success);
        int failed = results.Count(r => !r.success);
        int totalMeshes = results.Where(r => r.success).Sum(r => r.meshCount);

        string summary = $"修复完成！\n\n" +
            $"  成功烘焙: {success} 个 Prefab（共 {totalMeshes} 个 Mesh）\n" +
            $"  失败: {failed} 个";
        if (failed > 0)
        {
            summary += "\n\n失败列表:\n" + string.Join("\n",
                results.Where(r => !r.success).Select(r => $"  · {Path.GetFileNameWithoutExtension(r.prefabPath)}: {r.error}"));
        }

        Debug.Log($"[平滑法线烘焙-修复] {summary}");
        EditorUtility.DisplayDialog("修复完成", summary, "确定");

        CheckAllBatchPrefabs();
    }
}
