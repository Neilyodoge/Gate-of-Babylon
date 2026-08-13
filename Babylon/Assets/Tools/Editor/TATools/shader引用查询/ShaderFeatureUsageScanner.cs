using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通用 Shader 功能使用检测工具。
/// 选择任意 Shader，自动提取 Toggle 属性和 shader_feature 关键字，
/// 扫描项目中使用该 Shader 的材质，并可继续查找材质引用所在的 Prefab。
/// </summary>
public class ShaderFeatureUsageScanner : EditorWindow
{
    Shader m_TargetShader;
    Vector2 m_Scroll;
    bool m_Scanned;
    int m_TotalMaterialCount;
    bool m_MaterialListFoldout;

    readonly List<FeatureResult> m_Results = new List<FeatureResult>();
    readonly List<Material> m_ShaderMaterials = new List<Material>();
    readonly Dictionary<Material, PrefabUsageResult> m_PrefabUsageResults =
        new Dictionary<Material, PrefabUsageResult>();

    enum FeatureType
    {
        Keyword,
        Property
    }

    class FeatureResult
    {
        public string label;
        public string key;
        public FeatureType type;
        public int count;
        public readonly List<Material> materials = new List<Material>();
        public bool foldout;
    }

    class PrefabUsageResult
    {
        public bool foldout = true;
        public readonly List<GameObject> prefabs = new List<GameObject>();
    }

    [MenuItem("Tools_3D/美术/Shader 功能使用检测")]
    static void Open()
    {
        GetWindow<ShaderFeatureUsageScanner>("Shader功能使用检测").Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Shader 功能使用检测", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选择一个 Shader，自动提取所有 Toggle/keyword 属性和变体关键字，\n" +
            "扫描项目中使用该 Shader 的材质，并可继续查找材质所在的 Prefab。",
            MessageType.Info);

        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        m_TargetShader =
            EditorGUILayout.ObjectField("目标 Shader", m_TargetShader, typeof(Shader), false) as Shader;
        if (EditorGUI.EndChangeCheck())
            m_Scanned = false;

        if (Selection.activeObject is Material selectedMaterial && selectedMaterial.shader != null)
        {
            if (GUILayout.Button(
                $"使用选中材质的 Shader: {selectedMaterial.shader.name}",
                EditorStyles.miniButton))
            {
                m_TargetShader = selectedMaterial.shader;
                m_Scanned = false;
            }
        }

        EditorGUILayout.Space(4);

        using (new EditorGUI.DisabledScope(m_TargetShader == null))
        {
            if (GUILayout.Button("开始扫描", GUILayout.Height(28)))
                Scan();
        }

        if (!m_Scanned)
            return;

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Shader: {m_TargetShader.name}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"共 {m_TotalMaterialCount} 个材质", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        int unusedCount = m_Results.Count(result => result.count == 0);
        if (unusedCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"有 {unusedCount} 个功能/变体在项目中完全未使用",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        DrawShaderMaterialList();

        foreach (FeatureResult result in m_Results)
            DrawFeatureRow(result);

        EditorGUILayout.EndScrollView();
    }

    void DrawShaderMaterialList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        m_MaterialListFoldout = EditorGUILayout.Foldout(
            m_MaterialListFoldout,
            $"使用该 Shader 的材质（{m_ShaderMaterials.Count}）",
            true);

        using (new EditorGUI.DisabledScope(m_ShaderMaterials.Count == 0))
        {
            if (GUILayout.Button("选中全部", EditorStyles.miniButton, GUILayout.Width(64)))
                Selection.objects = m_ShaderMaterials.Cast<Object>().ToArray();
        }
        EditorGUILayout.EndHorizontal();

        if (m_MaterialListFoldout)
        {
            if (m_ShaderMaterials.Count == 0)
            {
                EditorGUILayout.LabelField("项目中没有材质使用该 Shader。", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUI.indentLevel++;
                foreach (Material material in m_ShaderMaterials)
                    DrawMaterialRow(material);
                EditorGUI.indentLevel--;
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    void DrawMaterialRow(Material material)
    {
        m_PrefabUsageResults.TryGetValue(material, out PrefabUsageResult prefabUsage);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(material, typeof(Material), false);
        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(42)))
            EditorGUIUtility.PingObject(material);
        if (GUILayout.Button("查 Prefab", EditorStyles.miniButton, GUILayout.Width(68)))
        {
            prefabUsage = ScanPrefabsUsingMaterial(material);
            m_PrefabUsageResults[material] = prefabUsage;
        }
        EditorGUILayout.EndHorizontal();

        if (prefabUsage == null)
            return;

        prefabUsage.foldout = EditorGUILayout.Foldout(
            prefabUsage.foldout,
            $"引用该材质的 Prefab（{prefabUsage.prefabs.Count}）",
            true);
        if (!prefabUsage.foldout)
            return;

        EditorGUI.indentLevel++;
        if (prefabUsage.prefabs.Count == 0)
        {
            EditorGUILayout.LabelField("未找到引用该材质的 Prefab。", EditorStyles.miniLabel);
        }
        else
        {
            foreach (GameObject prefab in prefabUsage.prefabs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(42)))
                    EditorGUIUtility.PingObject(prefab);
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUI.indentLevel--;
    }

    PrefabUsageResult ScanPrefabsUsingMaterial(Material material)
    {
        var result = new PrefabUsageResult();
        if (material == null)
            return result;

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrEmpty(materialPath))
            return result;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "查找材质引用",
                        $"正在检查 Prefab... ({i}/{prefabGuids.Length})",
                        prefabGuids.Length > 0 ? (float)i / prefabGuids.Length : 1f);
                }

                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);

                // 先按路径快速过滤，再精确比较对象，避免同一 FBX 内多个子材质造成误报。
                string[] dependencyPaths = AssetDatabase.GetDependencies(prefabPath, true);
                if (!dependencyPaths.Contains(materialPath))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                Object[] dependencies = EditorUtility.CollectDependencies(new Object[] { prefab });
                if (dependencies.Any(dependency => dependency == material))
                    result.prefabs.Add(prefab);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        result.prefabs.Sort((left, right) =>
            string.Compare(
                AssetDatabase.GetAssetPath(left),
                AssetDatabase.GetAssetPath(right),
                System.StringComparison.OrdinalIgnoreCase));

        Debug.Log(
            $"[ShaderFeatureUsageScanner] 材质引用扫描完成：{material.name}，" +
            $"共找到 {result.prefabs.Count} 个 Prefab");
        return result;
    }

    void DrawFeatureRow(FeatureResult result)
    {
        Color barColor;
        if (result.count == 0)
            barColor = new Color(0.9f, 0.3f, 0.3f, 0.3f);
        else if (result.count <= 50)
            barColor = new Color(0.9f, 0.8f, 0.2f, 0.3f);
        else
            barColor = new Color(0.3f, 0.8f, 0.4f, 0.2f);

        Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.DrawRect(rect, barColor);

        EditorGUILayout.BeginHorizontal();
        string typeTag = result.type == FeatureType.Keyword ? "[K]" : "[P]";
        string status = result.count == 0 ? "未使用" : $"{result.count} 个";

        EditorGUILayout.LabelField(typeTag, GUILayout.Width(24));
        EditorGUILayout.LabelField(result.label, EditorStyles.boldLabel, GUILayout.Width(200));
        EditorGUILayout.SelectableLabel(
            result.key,
            EditorStyles.miniLabel,
            GUILayout.MinWidth(120),
            GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(38)))
            EditorGUIUtility.systemCopyBuffer = result.key;
        EditorGUILayout.LabelField(status, GUILayout.Width(60));

        float ratio = m_TotalMaterialCount > 0
            ? (float)result.count / m_TotalMaterialCount
            : 0f;
        Rect barRect = GUILayoutUtility.GetRect(80, 16, GUILayout.Width(80));
        EditorGUI.ProgressBar(barRect, ratio, $"{ratio * 100:F0}%");
        EditorGUILayout.EndHorizontal();

        if (result.count > 0 && result.count <= 50)
        {
            result.foldout =
                EditorGUILayout.Foldout(result.foldout, $"查看 {result.count} 个材质", true);
            if (result.foldout)
            {
                EditorGUI.indentLevel++;
                foreach (Material material in result.materials)
                    EditorGUILayout.ObjectField(material, typeof(Material), false);
                EditorGUI.indentLevel--;
            }
        }
        else if (result.count == 0)
        {
            EditorGUILayout.LabelField(
                "  → 完全未使用，可考虑移除此功能/变体",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(1);
    }

    void Scan()
    {
        m_Results.Clear();
        m_ShaderMaterials.Clear();
        m_PrefabUsageResults.Clear();
        m_Scanned = false;
        m_TotalMaterialCount = 0;

        if (m_TargetShader == null)
            return;

        var keywordResults = new Dictionary<string, FeatureResult>();
        int propertyCount = ShaderUtil.GetPropertyCount(m_TargetShader);

        ExtractToggleKeywords(propertyCount, keywordResults);
        ExtractShaderFeatureKeywords(keywordResults);
        List<PropertyCheck> propertyChecks = ExtractToggleProperties(propertyCount);
        ScanMaterials(keywordResults, propertyChecks);

        m_Results.Sort((left, right) => left.count.CompareTo(right.count));
        m_ShaderMaterials.Sort((left, right) =>
            string.Compare(
                AssetDatabase.GetAssetPath(left),
                AssetDatabase.GetAssetPath(right),
                System.StringComparison.OrdinalIgnoreCase));
        m_Scanned = true;

        Debug.Log(
            $"[ShaderFeatureUsageScanner] 扫描完成：{m_TargetShader.name}，" +
            $"共 {m_TotalMaterialCount} 个材质，{m_Results.Count} 个功能/变体");
    }

    void ExtractToggleKeywords(
        int propertyCount,
        Dictionary<string, FeatureResult> keywordResults)
    {
        for (int i = 0; i < propertyCount; i++)
        {
            ShaderUtil.ShaderPropertyType propertyType =
                ShaderUtil.GetPropertyType(m_TargetShader, i);
            if (propertyType != ShaderUtil.ShaderPropertyType.Float &&
                propertyType != ShaderUtil.ShaderPropertyType.Range)
            {
                continue;
            }

            string propertyName = ShaderUtil.GetPropertyName(m_TargetShader, i);
            string propertyDescription = ShaderUtil.GetPropertyDescription(m_TargetShader, i);

            foreach (string attribute in m_TargetShader.GetPropertyAttributes(i))
            {
                if (!attribute.StartsWith("Toggle"))
                    continue;
                if (attribute == "ToggleUI" || attribute == "ToggleOff")
                    break;

                string keyword = null;
                int start = attribute.IndexOf('(');
                int end = attribute.IndexOf(')');
                if (start >= 0 && end > start)
                    keyword = attribute.Substring(start + 1, end - start - 1).Trim();
                else if (attribute == "Toggle")
                    keyword = propertyName.ToUpperInvariant() + "_ON";

                AddKeywordResult(
                    keywordResults,
                    keyword,
                    $"{propertyDescription} ({propertyName})");
                break;
            }
        }
    }

    void ExtractShaderFeatureKeywords(Dictionary<string, FeatureResult> keywordResults)
    {
        string shaderPath = AssetDatabase.GetAssetPath(m_TargetShader);
        if (string.IsNullOrEmpty(shaderPath))
            return;

        string shaderSource = System.IO.File.ReadAllText(shaderPath);
        shaderSource = System.Text.RegularExpressions.Regex.Replace(
            shaderSource,
            @"/\*.*?\*/",
            " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var featureRegex = new System.Text.RegularExpressions.Regex(
            @"^[ \t]*#pragma[ \t]+shader_feature[\w_]*[ \t]+([^\r\n]*)",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in featureRegex.Matches(shaderSource))
        {
            string declaration = match.Groups[1].Value;
            int commentIndex =
                declaration.IndexOf("//", System.StringComparison.Ordinal);
            if (commentIndex >= 0)
                declaration = declaration.Substring(0, commentIndex);

            string[] keywords = declaration.Trim().Split(
                new[] { ' ', '\t' },
                System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string keywordToken in keywords)
            {
                string keyword = keywordToken.Trim();
                if (keyword == "_" ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        keyword,
                        @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    continue;
                }

                AddKeywordResult(keywordResults, keyword, keyword);
            }
        }
    }

    void AddKeywordResult(
        Dictionary<string, FeatureResult> keywordResults,
        string keyword,
        string label)
    {
        if (string.IsNullOrEmpty(keyword) || keywordResults.ContainsKey(keyword))
            return;

        var result = new FeatureResult
        {
            label = label,
            key = keyword,
            type = FeatureType.Keyword
        };
        keywordResults.Add(keyword, result);
        m_Results.Add(result);
    }

    List<PropertyCheck> ExtractToggleProperties(int propertyCount)
    {
        var propertyChecks = new List<PropertyCheck>();
        for (int i = 0; i < propertyCount; i++)
        {
            ShaderUtil.ShaderPropertyType propertyType =
                ShaderUtil.GetPropertyType(m_TargetShader, i);
            if (propertyType != ShaderUtil.ShaderPropertyType.Float &&
                propertyType != ShaderUtil.ShaderPropertyType.Range)
            {
                continue;
            }

            string propertyName = ShaderUtil.GetPropertyName(m_TargetShader, i);
            string[] attributes = m_TargetShader.GetPropertyAttributes(i);
            if (!attributes.Any(attribute => attribute == "ToggleUI"))
                continue;

            var result = new FeatureResult
            {
                label =
                    $"{ShaderUtil.GetPropertyDescription(m_TargetShader, i)} ({propertyName})",
                key = propertyName,
                type = FeatureType.Property
            };
            m_Results.Add(result);
            propertyChecks.Add(new PropertyCheck(propertyName, result));
        }
        return propertyChecks;
    }

    void ScanMaterials(
        Dictionary<string, FeatureResult> keywordResults,
        List<PropertyCheck> propertyChecks)
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        string[] allKeywords = keywordResults.Keys.ToArray();

        try
        {
            for (int i = 0; i < materialGuids.Length; i++)
            {
                if (i % 200 == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "扫描材质",
                        $"正在扫描... ({i}/{materialGuids.Length})",
                        materialGuids.Length > 0 ? (float)i / materialGuids.Length : 1f);
                }

                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader != m_TargetShader)
                    continue;

                m_TotalMaterialCount++;
                m_ShaderMaterials.Add(material);

                var materialKeywords = new HashSet<string>(material.shaderKeywords);
                foreach (string keyword in allKeywords)
                {
                    if (!materialKeywords.Contains(keyword))
                        continue;

                    keywordResults[keyword].count++;
                    keywordResults[keyword].materials.Add(material);
                }

                foreach (PropertyCheck propertyCheck in propertyChecks)
                {
                    if (!material.HasProperty(propertyCheck.name) ||
                        material.GetFloat(propertyCheck.name) <= 0.5f)
                    {
                        continue;
                    }

                    propertyCheck.result.count++;
                    propertyCheck.result.materials.Add(material);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    readonly struct PropertyCheck
    {
        public readonly string name;
        public readonly FeatureResult result;

        public PropertyCheck(string name, FeatureResult result)
        {
            this.name = name;
            this.result = result;
        }
    }
}
