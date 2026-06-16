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
        public bool fromPrefab;     // 是否来自 Prefab/Prefab 变体引用（而非文件夹内直接的 .mat）
        public string sourceLabel;  // 首个引用该材质的 Prefab 名称，用于界面提示
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
    bool m_ShowOnlyModified;

    [MenuItem("nTools/TA工具/mat批量调参")]
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

        if (string.IsNullOrEmpty(m_FolderPath) || !AssetDatabase.IsValidFolder(m_FolderPath))
            return;

        var map = new Dictionary<Shader, ShaderGroup>();
        // 去重：同一个源材质可能既是文件夹内的 .mat，又被多个 Prefab 引用
        var seenMaterials = new HashSet<Material>();

        try
        {
            // 1. 直接扫描文件夹内的 .mat 材质
            var matGuids = AssetDatabase.FindAssets("t:Material", new[] { m_FolderPath });
            for (int i = 0; i < matGuids.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("扫描材质",
                    $"材质 ({i + 1}/{matGuids.Length})", (float)i / matGuids.Length))
                {
                    return;
                }

                var path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                AddMaterialToGroup(map, seenMaterials, mat, path, null);
            }

            // 2. 扫描文件夹内的 Prefab / Prefab 变体，收集其引用的源材质
            //    即便文件夹内没有 .mat，只要 Prefab 引用了 .mat 也能显示出来；
            //    由于取的是 sharedMaterial 指向的源资源，修改时直接写回源 .mat。
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { m_FolderPath });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("扫描 Prefab 材质",
                    $"Prefab ({i + 1}/{prefabGuids.Length})", (float)i / prefabGuids.Length))
                {
                    return;
                }

                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue;

                CollectPrefabMaterials(map, seenMaterials, prefab);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        m_TotalMats = seenMaterials.Count;
        m_Groups = map.Values.OrderBy(g => g.shaderName).ToList();
    }

    /// <summary>
    /// 将一个材质加入对应 Shader 分组（自动去重）。
    /// </summary>
    /// <param name="fromPrefabName">非空表示该材质来自 Prefab 引用，用于界面提示。</param>
    void AddMaterialToGroup(Dictionary<Shader, ShaderGroup> map, HashSet<Material> seen,
        Material mat, string path, string fromPrefabName)
    {
        if (mat == null || mat.shader == null) return;
        if (!seen.Add(mat)) return; // 已收录，跳过

        if (!map.TryGetValue(mat.shader, out var group))
        {
            group = new ShaderGroup { shader = mat.shader, shaderName = mat.shader.name };
            map[mat.shader] = group;
        }

        group.materials.Add(new MatEntry
        {
            material = mat,
            path = path,
            fromPrefab = !string.IsNullOrEmpty(fromPrefabName),
            sourceLabel = fromPrefabName
        });
    }

    /// <summary>
    /// 收集 Prefab（含 Prefab 变体）所有 Renderer 引用的源材质（仅限独立的 .mat 资源）。
    /// </summary>
    void CollectPrefabMaterials(Dictionary<Shader, ShaderGroup> map, HashSet<Material> seen, GameObject prefab)
    {
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var mats = renderer.sharedMaterials;
            if (mats == null) continue;

            foreach (var mat in mats)
            {
                if (mat == null) continue;

                // 只收录作为独立 .mat 资源存在的源材质（可写回）。
                // 排除内置材质、FBX 内嵌的子资源材质等不可单独编辑的情况。
                string matPath = AssetDatabase.GetAssetPath(mat);
                if (string.IsNullOrEmpty(matPath)) continue;
                if (!matPath.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) continue;

                AddMaterialToGroup(map, seen, mat, matPath, prefab.name);
            }
        }
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
                if (Mathf.Approximately(prop.rangeMin, 0f) && Mathf.Approximately(prop.rangeMax, 1f))
                    prop.isToggle = true;
            }

            // Float 类型：名字含 Toggle/Enable/Use 视为 Toggle
            if (propType == ShaderUtil.ShaderPropertyType.Float)
            {
                string lower = propName.ToLowerInvariant();
                if (lower.Contains("toggle") || lower.Contains("enable") || lower.Contains("_use"))
                    prop.isToggle = true;
            }

            // 从第一个材质读默认值作为参考
            if (group.materials.Count > 0)
            {
                var refMat = group.materials[0].material;
                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        prop.floatValue = refMat.GetFloat(propName);
                        if (propType == ShaderUtil.ShaderPropertyType.Float
                            && (Mathf.Approximately(prop.floatValue, 0f) || Mathf.Approximately(prop.floatValue, 1f)))
                            prop.isToggle = true;
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

        EditorGUILayout.LabelField($"文件夹: {(string.IsNullOrEmpty(m_FolderPath) ? "未指定" : m_FolderPath)}    " +
            $"材质总数: {m_TotalMats}    Shader 分组: {m_Groups.Count}", EditorStyles.miniLabel);
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
            // 标识来自 Prefab 引用的源材质，提示修改会写回源 .mat
            if (entry.fromPrefab)
            {
                GUILayout.Label(new GUIContent("Prefab",
                    $"该材质来自 Prefab「{entry.sourceLabel}」引用，修改将写回源材质：\n{entry.path}"),
                    EditorStyles.miniBoldLabel, GUILayout.Width(48));
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
