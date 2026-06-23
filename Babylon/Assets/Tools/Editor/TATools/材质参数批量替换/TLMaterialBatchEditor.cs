using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 材质批量调参工具。
/// 扫描指定文件夹下的所有材质，按 Shader 分组显示，
/// 可以批量修改同 Shader 下所有/选中材质的属性值。
/// </summary>
public class TLMaterialBatchEditor : EditorWindow
{
    // ================================================================
    //  数据结构
    // ================================================================

    class ShaderGroup
    {
        public Shader shader;
        public string shaderName;
        public bool foldout;
        public List<MatEntry> materials = new List<MatEntry>();
        public List<PropInfo> properties = new List<PropInfo>();
        public bool propsFetched;
        public bool selectAll = true;
    }

    class MatEntry
    {
        public Material material;
        public string path;
        public bool selected = true;
        public bool isExternal;   // 材质球不在当前选中文件夹内（来自文件夹内 Prefab 的引用）
        public string sourceInfo; // 来源说明（如来自哪个 Prefab），用于鼠标悬停提示
    }

    class PropInfo
    {
        public string name;
        public string displayName;
        public ShaderUtil.ShaderPropertyType type;
        public bool enabled; // 左侧勾选：是否要批量修改此属性
        public float floatValue;
        public Color colorValue = Color.white;
        public Vector4 vectorValue;
        public Texture textureValue;
        public Vector2 textureScale = Vector2.one;
        public Vector2 textureOffset = Vector2.zero;
        public bool isToggle; // 是否为 Toggle 类型（Range 0~1 或 Float 且值为 0/1）
        public float rangeMin;
        public float rangeMax;
    }

    // ================================================================
    //  字段
    // ================================================================

    List<ShaderGroup> m_Groups = new List<ShaderGroup>();
    Vector2 m_Scroll;
    string m_SearchFilter = "";
    string m_FolderPath = "";
    DefaultAsset m_FolderAsset;
    int m_TotalMats;
    int m_ExternalMats; // 不在所选文件夹内的材质数量（来自 Prefab 引用）
    bool m_Scanned;     // 是否已执行过扫描（用于区分「未扫描」与「扫描后为空」）
    bool m_ShowOnlyModified;

    [MenuItem("Tools_3D/美术/材质批量调参")]
    public static void Open()
    {
        var win = GetWindow<TLMaterialBatchEditor>(false, "材质批量调参", true);
        win.minSize = new Vector2(480, 360);
        win.Show();
        win.Focus();
    }

    // ================================================================
    //  扫描
    // ================================================================

    void ScanFolder()
    {
        m_Groups.Clear();
        m_TotalMats = 0;
        m_ExternalMats = 0;
        m_Scanned = true;

        if (string.IsNullOrEmpty(m_FolderPath) || !AssetDatabase.IsValidFolder(m_FolderPath))
            return;

        var map = new Dictionary<Shader, ShaderGroup>();
        var seenMats = new HashSet<Material>(); // 去重：同一材质只收集一次

        try
        {
            // 1. 收集文件夹内的独立材质资产
            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { m_FolderPath });
            for (int i = 0; i < matGuids.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("扫描材质",
                    $"材质 ({i + 1}/{matGuids.Length})", (float)i / Mathf.Max(1, matGuids.Length)))
                    return;

                var path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                AddMaterial(map, seenMats, mat, false, null);
            }

            // 2. 收集文件夹内 Prefab 上引用的材质（Renderer.sharedMaterials）
            //    这些材质可能位于其它文件夹，标记为「外部材质」并给出来源提示
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { m_FolderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("扫描 Prefab 材质",
                    $"Prefab ({i + 1}/{prefabGuids.Length})", (float)i / Mathf.Max(1, prefabGuids.Length)))
                    return;

                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (go == null) continue;

                var renderers = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (var mat in mats)
                    {
                        if (mat == null) continue;
                        var matPath = AssetDatabase.GetAssetPath(mat);
                        // 材质 path 以「所选文件夹/」开头才算文件夹内
                        bool inFolder = !string.IsNullOrEmpty(matPath)
                            && matPath.StartsWith(m_FolderPath + "/");
                        AddMaterial(map, seenMats, mat, !inFolder, prefabPath);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        foreach (var g in map.Values)
        {
            m_TotalMats += g.materials.Count;
            m_ExternalMats += g.materials.Count(e => e.isExternal);
        }

        m_Groups = map.Values.OrderBy(g => g.shaderName).ToList();
    }

    /// <summary>
    /// 将材质加入对应 Shader 分组（自动去重）。
    /// </summary>
    /// <param name="isExternal">材质是否不在当前选中文件夹内</param>
    /// <param name="sourcePrefabPath">外部材质来源 Prefab 路径（用于提示）</param>
    void AddMaterial(Dictionary<Shader, ShaderGroup> map, HashSet<Material> seen,
        Material mat, bool isExternal, string sourcePrefabPath)
    {
        if (mat == null || mat.shader == null) return;
        if (!seen.Add(mat)) return; // 已收集过则跳过

        if (!map.TryGetValue(mat.shader, out var group))
        {
            group = new ShaderGroup { shader = mat.shader, shaderName = mat.shader.name };
            map[mat.shader] = group;
        }

        var entry = new MatEntry
        {
            material = mat,
            path = AssetDatabase.GetAssetPath(mat),
            isExternal = isExternal,
        };
        if (isExternal && !string.IsNullOrEmpty(sourcePrefabPath))
            entry.sourceInfo = $"引用自 Prefab: {System.IO.Path.GetFileName(sourcePrefabPath)}\n材质路径: {entry.path}";

        group.materials.Add(entry);
    }

    /// <summary>
    /// 获取 shader 的所有用户可见属性
    /// </summary>
    void FetchShaderProperties(ShaderGroup group)
    {
        group.properties.Clear();
        group.propsFetched = true;

        if (group.shader == null) return;

        int count = ShaderUtil.GetPropertyCount(group.shader);
        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.IsShaderPropertyHidden(group.shader, i))
                continue;

            var propType = ShaderUtil.GetPropertyType(group.shader, i);
            string propName = ShaderUtil.GetPropertyName(group.shader, i);
            string propDesc = ShaderUtil.GetPropertyDescription(group.shader, i);

            var prop = new PropInfo
            {
                name = propName,
                displayName = string.IsNullOrEmpty(propDesc) ? propName : propDesc,
                type = propType
            };

            // 获取 Range 限制
            if (propType == ShaderUtil.ShaderPropertyType.Range)
            {
                prop.rangeMin = ShaderUtil.GetRangeLimits(group.shader, i, 1);
                prop.rangeMax = ShaderUtil.GetRangeLimits(group.shader, i, 2);
            }

            // 仅「驱动变体(keyword)」的属性才视为开关：[Toggle]/[Toggle(_KW)]/[KeywordEnum]。
            // 避免把 Range(0,1) 滑条、名字含 enable/use、或恰好取值 0/1 的普通 Float 误判成开关。
            prop.isToggle = IsKeywordToggleProperty(group.shader, propName);

            // 从第一个材质读默认值作为参考
            if (group.materials.Count > 0)
            {
                var refMat = group.materials[0].material;
                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        prop.floatValue = refMat.GetFloat(propName);
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        prop.colorValue = refMat.GetColor(propName);
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        prop.vectorValue = refMat.GetVector(propName);
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        prop.textureValue = refMat.GetTexture(propName);
                        prop.textureScale = refMat.GetTextureScale(propName);
                        prop.textureOffset = refMat.GetTextureOffset(propName);
                        break;
                }
            }

            group.properties.Add(prop);
        }
    }

    // ================================================================
    //  GUI
    // ================================================================

    void OnGUI()
    {
        DrawToolbar();
        DrawFolderField();
        DrawGroupList();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
            ScanFolder();

        GUILayout.Space(4);
        m_ShowOnlyModified = GUILayout.Toggle(m_ShowOnlyModified, "仅显示要修改的", EditorStyles.toolbarButton, GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        m_SearchFilter = EditorGUILayout.TextField(m_SearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

        EditorGUILayout.EndHorizontal();

        string extInfo = m_ExternalMats > 0 ? $"（其中 {m_ExternalMats} 个不在文件夹内）" : "";
        EditorGUILayout.LabelField($"文件夹: {(string.IsNullOrEmpty(m_FolderPath) ? "未指定" : m_FolderPath)}    " +
            $"材质总数: {m_TotalMats}{extInfo}    Shader 分组: {m_Groups.Count}", EditorStyles.miniLabel);
    }

    void DrawFolderField()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("目标文件夹:", GUILayout.Width(72));

        EditorGUI.BeginChangeCheck();
        m_FolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(m_FolderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck() && m_FolderAsset != null)
        {
            var path = AssetDatabase.GetAssetPath(m_FolderAsset);
            if (AssetDatabase.IsValidFolder(path))
            {
                m_FolderPath = path;
                ScanFolder();
            }
            else
            {
                Debug.LogWarning("[材质批量调参] 请拖入一个文件夹");
                m_FolderAsset = null;
            }
        }

        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string selected = EditorUtility.OpenFolderPanel("选择材质目录", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                    m_FolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                else
                    m_FolderPath = selected;
                ScanFolder();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawGroupList()
    {
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        // 扫描后没有任何材质时给出提示
        if (m_Scanned && !string.IsNullOrEmpty(m_FolderPath) && m_Groups.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "当前文件夹下未找到任何材质球。\n" +
                "（已同时检索文件夹内 Prefab 引用的材质）\n" +
                "请确认所选文件夹是否正确，或其中是否存在材质 / 带材质的 Prefab。",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        string filter = string.IsNullOrEmpty(m_SearchFilter) ? "" : m_SearchFilter.ToLowerInvariant();

        foreach (var group in m_Groups)
        {
            if (!string.IsNullOrEmpty(filter)
                && group.shaderName.ToLowerInvariant().IndexOf(filter) < 0)
                continue;

            DrawShaderGroup(group);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawShaderGroup(ShaderGroup group)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 标题行
        EditorGUILayout.BeginHorizontal();
        group.foldout = EditorGUILayout.Foldout(group.foldout,
            $"{group.shaderName}    ({group.materials.Count} 材质)", true);

        if (GUILayout.Button("全选材质", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
        {
            Selection.objects = group.materials.Select(m => (Object)m.material).ToArray();
        }
        if (GUILayout.Button("展开属性", EditorStyles.miniButtonRight, GUILayout.Width(60)))
        {
            group.foldout = true;
            if (!group.propsFetched) FetchShaderProperties(group);
        }
        EditorGUILayout.EndHorizontal();

        if (group.foldout)
        {
            if (!group.propsFetched) FetchShaderProperties(group);

            EditorGUI.indentLevel++;

            EditorGUILayout.ObjectField("Shader", group.shader, typeof(Shader), false);

            GUILayout.Space(2);

            DrawMaterialSelection(group);

            GUILayout.Space(4);

            DrawPropertyEditor(group);

            GUILayout.Space(4);

            DrawApplyButton(group);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    void DrawMaterialSelection(ShaderGroup group)
    {
        // 组级提示：该分组存在不在所选文件夹内的材质
        int groupExtCount = group.materials.Count(e => e.isExternal);
        if (groupExtCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"该分组有 {groupExtCount} 个材质球不在当前选中文件夹内（来自 Prefab 引用）。\n" +
                "可以一并修改，但请注意修改会影响其原始资产。",
                MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("材质列表", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("全选", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
        {
            foreach (var m in group.materials) m.selected = true;
            group.selectAll = true;
        }
        if (GUILayout.Button("取消", EditorStyles.miniButtonMid, GUILayout.Width(40)))
        {
            foreach (var m in group.materials) m.selected = false;
            group.selectAll = false;
        }
        if (GUILayout.Button("反选", EditorStyles.miniButtonRight, GUILayout.Width(40)))
        {
            foreach (var m in group.materials) m.selected = !m.selected;
        }

        int selCount = group.materials.Count(m => m.selected);
        GUILayout.Label($"[{selCount}/{group.materials.Count}]", EditorStyles.miniLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        // 全部展示材质列表
        for (int i = 0; i < group.materials.Count; i++)
        {
            var entry = group.materials[i];
            EditorGUILayout.BeginHorizontal();
            entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(16));
            EditorGUILayout.ObjectField(entry.material, typeof(Material), false);
            if (entry.isExternal)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.78f, 0.2f);
                GUILayout.Label(new GUIContent("⚠ 不在所选文件夹", entry.sourceInfo),
                    EditorStyles.miniLabel, GUILayout.Width(110));
                GUI.color = prevColor;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawPropertyEditor(ShaderGroup group)
    {
        EditorGUILayout.LabelField("属性编辑", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "左侧 ☑ = 启用修改（防止误触）。修改值后点「应用」写入选中材质。",
            MessageType.Info);

        // 临时取消缩进，避免 indentLevel 吃掉左侧控件空间
        int savedIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        for (int i = 0; i < group.properties.Count; i++)
        {
            var prop = group.properties[i];

            if (m_ShowOnlyModified && !prop.enabled) continue;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(16);

            // 左侧启用勾选框
            prop.enabled = GUILayout.Toggle(prop.enabled, "", GUILayout.Width(18));

            // 属性类型标签
            string typeLabel = GetTypeLabel(prop.type);
            GUILayout.Label(typeLabel, EditorStyles.miniLabel, GUILayout.Width(18));

            // 属性名
            GUILayout.Label(prop.displayName, GUILayout.Width(140));

            // 值编辑区（未勾选时灰色只读，勾选后可编辑）
            EditorGUI.BeginDisabledGroup(!prop.enabled);

            if (prop.isToggle && (prop.type == ShaderUtil.ShaderPropertyType.Float
                || prop.type == ShaderUtil.ShaderPropertyType.Range))
            {
                bool togVal = prop.floatValue > 0.5f;
                togVal = GUILayout.Toggle(togVal, togVal ? "ON" : "OFF", EditorStyles.miniButton, GUILayout.Width(40));
                prop.floatValue = togVal ? 1f : 0f;
                GUILayout.FlexibleSpace();
            }
            else
            {
                switch (prop.type)
                {
                    case ShaderUtil.ShaderPropertyType.Float:
                        prop.floatValue = EditorGUILayout.FloatField(prop.floatValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Range:
                        prop.floatValue = EditorGUILayout.Slider(prop.floatValue, prop.rangeMin, prop.rangeMax);
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        prop.colorValue = EditorGUILayout.ColorField(prop.colorValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        prop.vectorValue = EditorGUILayout.Vector4Field("", prop.vectorValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        prop.textureValue = (Texture)EditorGUILayout.ObjectField(prop.textureValue, typeof(Texture), false);
                        break;
                }
            }

            EditorGUI.EndDisabledGroup();

            // 从首个选中材质读取当前值
            if (GUILayout.Button("读", EditorStyles.miniButton, GUILayout.Width(26)))
            {
                var firstSel = group.materials.FirstOrDefault(m => m.selected);
                if (firstSel != null)
                    ReadPropertyFromMaterial(prop, firstSel.material);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel = savedIndent;
    }

    void DrawApplyButton(ShaderGroup group)
    {
        int selCount = group.materials.Count(m => m.selected);
        int enabledProps = group.properties.Count(p => p.enabled);

        EditorGUI.BeginDisabledGroup(selCount == 0 || enabledProps == 0);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"✓ 应用到选中材质 ({selCount} 材质, {enabledProps} 属性)", GUILayout.Height(26)))
        {
            ApplyProperties(group);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    // ================================================================
    //  应用逻辑
    // ================================================================

    void ApplyProperties(ShaderGroup group)
    {
        var selectedMats = group.materials.Where(m => m.selected).Select(m => m.material).ToList();
        var enabledProps = group.properties.Where(p => p.enabled).ToList();

        if (selectedMats.Count == 0 || enabledProps.Count == 0) return;

        string propNames = string.Join(", ", enabledProps.Select(p => p.displayName));
        if (!EditorUtility.DisplayDialog("确认批量修改",
            $"将对 {selectedMats.Count} 个材质修改以下属性：\n{propNames}\n\n此操作支持撤销。",
            "确认修改", "取消"))
            return;

        Undo.RecordObjects(selectedMats.Cast<Object>().ToArray(), "Batch Edit Materials");

        int modified = 0;
        foreach (var mat in selectedMats)
        {
            foreach (var prop in enabledProps)
            {
                if (!mat.HasProperty(prop.name)) continue;

                switch (prop.type)
                {
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        mat.SetFloat(prop.name, prop.floatValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Color:
                        mat.SetColor(prop.name, prop.colorValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        mat.SetVector(prop.name, prop.vectorValue);
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        mat.SetTexture(prop.name, prop.textureValue);
                        mat.SetTextureScale(prop.name, prop.textureScale);
                        mat.SetTextureOffset(prop.name, prop.textureOffset);
                        break;
                }

                EditorUtility.SetDirty(mat);
            }
            modified++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[材质批量调参] 已修改 {modified} 个材质的 {enabledProps.Count} 个属性: {propNames}");
    }

    // ================================================================
    //  工具方法
    // ================================================================

    static string GetTypeLabel(ShaderUtil.ShaderPropertyType type)
    {
        switch (type)
        {
            case ShaderUtil.ShaderPropertyType.Float: return "F";
            case ShaderUtil.ShaderPropertyType.Range: return "R";
            case ShaderUtil.ShaderPropertyType.Color: return "C";
            case ShaderUtil.ShaderPropertyType.Vector: return "V";
            case ShaderUtil.ShaderPropertyType.TexEnv: return "T";
            default: return "?";
        }
    }

    static int GetPropertyIndex(Shader shader, string propName)
    {
        int count = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.GetPropertyName(shader, i) == propName)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// 判断属性是否为「驱动变体(keyword)」的开关。
    /// 依据 shader 声明的 attribute：[Toggle] / [Toggle(_KW)] / [KeywordEnum(...)] 才算；
    /// [ToggleUI]（不驱动 keyword）、Range(0,1) 滑条、普通 Float 都不算。
    /// 用属性 attribute 判定，避免按数值/命名启发式误判。
    /// </summary>
    static bool IsKeywordToggleProperty(Shader shader, string propName)
    {
        if (shader == null) return false;

        int idx = shader.FindPropertyIndex(propName);
        if (idx < 0) return false;

        string[] attrs = shader.GetPropertyAttributes(idx);
        if (attrs == null) return false;

        for (int i = 0; i < attrs.Length; i++)
        {
            string a = attrs[i];
            if (string.IsNullOrEmpty(a)) continue;

            // [Toggle] 或 [Toggle(_KEYWORD)]：注意排除 [ToggleUI]（"ToggleUI" 不会命中下面两条）
            if (a == "Toggle" || a.StartsWith("Toggle(")) return true;
            // [KeywordEnum(A,B,C)]：多状态变体
            if (a.StartsWith("KeywordEnum")) return true;
        }
        return false;
    }

    void ReadPropertyFromMaterial(PropInfo prop, Material mat)
    {
        if (!mat.HasProperty(prop.name)) return;

        switch (prop.type)
        {
            case ShaderUtil.ShaderPropertyType.Float:
            case ShaderUtil.ShaderPropertyType.Range:
                prop.floatValue = mat.GetFloat(prop.name);
                break;
            case ShaderUtil.ShaderPropertyType.Color:
                prop.colorValue = mat.GetColor(prop.name);
                break;
            case ShaderUtil.ShaderPropertyType.Vector:
                prop.vectorValue = mat.GetVector(prop.name);
                break;
            case ShaderUtil.ShaderPropertyType.TexEnv:
                prop.textureValue = mat.GetTexture(prop.name);
                prop.textureScale = mat.GetTextureScale(prop.name);
                prop.textureOffset = mat.GetTextureOffset(prop.name);
                break;
        }
    }
}
