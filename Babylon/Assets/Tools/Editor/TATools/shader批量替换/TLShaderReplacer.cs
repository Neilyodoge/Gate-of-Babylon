using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

public class TLShaderReplacer : EditorWindow
{
    enum ScanMode { Scene, Folder }

    class ShaderEntry
    {
        public Shader shader;
        public string name;
        public bool foldout;
        public List<RendererMat> users = new List<RendererMat>();
        public List<Material> folderMats = new List<Material>();
        public List<PrefabRef> prefabRefs = new List<PrefabRef>();
        public KeywordAnalysis keywordAnalysis;
    }

    class KeywordAnalysis
    {
        public bool analyzed;
        public bool foldout;
        public bool usedFoldout;
        public List<KeywordInfo> keywords = new List<KeywordInfo>();
    }

    class KeywordInfo
    {
        public string keyword;
        public bool isLocal;
        public List<Material> enabledBy = new List<Material>();
    }

    struct RendererMat
    {
        public Renderer renderer;
        public int matIndex;
        public Material material;
    }

    struct PrefabRef
    {
        public string prefabPath;
        public string hierarchyPath;
        public Material material;
    }

    List<ShaderEntry> m_Entries = new List<ShaderEntry>();
    Vector2 m_Scroll;
    string m_SearchFilter = "";
    Shader m_ReplaceTarget;
    bool m_IncludeInactive = true;

    ScanMode m_Mode = ScanMode.Scene;
    DefaultAsset m_FolderAsset;
    string m_FolderPath = "";
    int m_TotalMats;
    int m_TotalPrefabs;

    [MenuItem("nTools/TA工具/Shader批量替换")]
    public static void Open()
    {
        var win = GetWindow<TLShaderReplacer>(false, "Shader Replacer", true);
        win.minSize = new Vector2(420, 320);
        win.Show();
        win.Focus();
    }

    void OnEnable()
    {
        ScanScene();
    }

    // ================================================================
    //  Scene Scan
    // ================================================================

    void ScanScene()
    {
        m_Entries.Clear();
        var map = new Dictionary<Shader, ShaderEntry>();

        var renderers = m_IncludeInactive
            ? Resources.FindObjectsOfTypeAll<Renderer>()
            : Object.FindObjectsOfType<Renderer>();

        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!IsSceneObject(r.gameObject)) continue;

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null || mat.shader == null) continue;

                if (!map.TryGetValue(mat.shader, out var entry))
                {
                    entry = new ShaderEntry { shader = mat.shader, name = mat.shader.name };
                    map[mat.shader] = entry;
                }
                entry.users.Add(new RendererMat { renderer = r, matIndex = i, material = mat });
            }
        }

        m_Entries = map.Values.OrderBy(e => e.name).ToList();
    }

    static bool IsSceneObject(GameObject go)
    {
        if (go.scene.name == null) return false;
        if (go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave)
            return false;
        return go.scene == SceneManager.GetActiveScene();
    }

    // ================================================================
    //  Folder Scan
    // ================================================================

    void ScanFolder(string folderPath)
    {
        m_Entries.Clear();
        m_TotalMats = 0;
        m_TotalPrefabs = 0;

        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return;

        var map = new Dictionary<Shader, ShaderEntry>();

        // --- Phase 1: Scan materials ---
        var matGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        m_TotalMats = matGuids.Length;

        for (int i = 0; i < matGuids.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                "扫描材质",
                $"({i + 1}/{matGuids.Length})",
                (float)i / matGuids.Length))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(matGuids[i]);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            if (!map.TryGetValue(mat.shader, out var entry))
            {
                entry = new ShaderEntry { shader = mat.shader, name = mat.shader.name };
                map[mat.shader] = entry;
            }
            entry.folderMats.Add(mat);
        }

        // --- Phase 2: Scan prefabs for material references ---
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        m_TotalPrefabs = prefabGuids.Length;

        var matSet = new HashSet<Material>(map.Values.SelectMany(e => e.folderMats));

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar(
                "扫描Prefab引用",
                $"({i + 1}/{prefabGuids.Length})",
                (float)i / prefabGuids.Length))
            {
                EditorUtility.ClearProgressBar();
                break;
            }

            var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int mi = 0; mi < mats.Length; mi++)
                {
                    var mat = mats[mi];
                    if (mat == null || mat.shader == null) continue;
                    if (!matSet.Contains(mat)) continue;

                    if (map.TryGetValue(mat.shader, out var entry))
                    {
                        entry.prefabRefs.Add(new PrefabRef
                        {
                            prefabPath = prefabPath,
                            hierarchyPath = GetHierarchyPath(r.transform, prefab.transform),
                            material = mat
                        });
                    }
                }
            }
        }

        EditorUtility.ClearProgressBar();
        m_Entries = map.Values.OrderBy(e => e.name).ToList();
    }

    static string GetHierarchyPath(Transform target, Transform root)
    {
        var parts = new List<string>();
        var current = target;
        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        parts.Add(root.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // ================================================================
    //  Keyword Analysis
    // ================================================================

    void AnalyzeKeywords(ShaderEntry entry)
    {
        var analysis = new KeywordAnalysis { analyzed = true, foldout = true };

        var definedKeywords = GetShaderDefinedKeywords(entry.shader, out var localSet);

        List<Material> mats;
        if (m_Mode == ScanMode.Scene)
            mats = entry.users.Select(u => u.material).Distinct().ToList();
        else
            mats = entry.folderMats;

        var keywordMap = new Dictionary<string, KeywordInfo>();
        foreach (var kw in definedKeywords)
        {
            keywordMap[kw] = new KeywordInfo
            {
                keyword = kw,
                isLocal = localSet.Contains(kw)
            };
        }

        foreach (var mat in mats)
        {
            var matKeywords = mat.shaderKeywords;
            foreach (var kw in matKeywords)
            {
                if (!keywordMap.TryGetValue(kw, out var info))
                {
                    info = new KeywordInfo { keyword = kw, isLocal = false };
                    keywordMap[kw] = info;
                }
                info.enabledBy.Add(mat);
            }
        }

        analysis.keywords = keywordMap.Values
            .OrderBy(k => k.enabledBy.Count > 0 ? 1 : 0)
            .ThenBy(k => k.keyword)
            .ToList();

        entry.keywordAnalysis = analysis;
    }

    static HashSet<string> GetShaderDefinedKeywords(Shader shader, out HashSet<string> localKeywords)
    {
        var allKeywords = new HashSet<string>();
        localKeywords = new HashSet<string>();

        if (TryGetKeywordsViaReflection(shader, ref allKeywords, ref localKeywords))
            return allKeywords;

        ParseShaderSourceForKeywords(shader, ref allKeywords, ref localKeywords);
        return allKeywords;
    }

    static bool TryGetKeywordsViaReflection(Shader shader, ref HashSet<string> allKeywords, ref HashSet<string> localKeywords)
    {
        var globalMethod = typeof(ShaderUtil).GetMethod("GetShaderGlobalKeywords",
            BindingFlags.Static | BindingFlags.NonPublic);
        var localMethod = typeof(ShaderUtil).GetMethod("GetShaderLocalKeywords",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (globalMethod == null && localMethod == null)
            return false;

        if (globalMethod != null)
        {
            var result = globalMethod.Invoke(null, new object[] { shader });
            if (result is string[] globalKws)
            {
                foreach (var kw in globalKws)
                {
                    if (!string.IsNullOrEmpty(kw))
                        allKeywords.Add(kw);
                }
            }
        }

        if (localMethod != null)
        {
            var result = localMethod.Invoke(null, new object[] { shader });
            if (result is string[] localKws)
            {
                foreach (var kw in localKws)
                {
                    if (!string.IsNullOrEmpty(kw))
                    {
                        allKeywords.Add(kw);
                        localKeywords.Add(kw);
                    }
                }
            }
        }

        return allKeywords.Count > 0;
    }

    static readonly Regex s_PragmaRegex = new Regex(
        @"#pragma\s+(shader_feature|shader_feature_local|multi_compile|multi_compile_local)[\s_]+(.+)",
        RegexOptions.Compiled);

    static void ParseShaderSourceForKeywords(Shader shader, ref HashSet<string> allKeywords, ref HashSet<string> localKeywords)
    {
        var path = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".shader"))
            return;

        var fullPath = System.IO.Path.GetFullPath(path);
        if (!System.IO.File.Exists(fullPath))
            return;

        var lines = System.IO.File.ReadAllLines(fullPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = s_PragmaRegex.Match(trimmed);
            if (!match.Success) continue;

            var pragmaType = match.Groups[1].Value;
            var tokens = match.Groups[2].Value.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            bool isLocal = pragmaType.Contains("local");

            foreach (var token in tokens)
            {
                if (token == "_" || token.StartsWith("//")) break;
                allKeywords.Add(token);
                if (isLocal)
                    localKeywords.Add(token);
            }
        }
    }

    // ================================================================
    //  GUI
    // ================================================================

    void OnGUI()
    {
        DrawToolbar();

        if (m_Mode == ScanMode.Folder)
            DrawFolderField();

        DrawShaderList();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        m_Mode = (ScanMode)GUILayout.Toolbar((int)m_Mode, new[] { "场景模式", "文件夹模式" },
            EditorStyles.toolbarButton, GUILayout.Width(160));
        if (EditorGUI.EndChangeCheck())
        {
            if (m_Mode == ScanMode.Scene)
                ScanScene();
            else
                ScanFolder(m_FolderPath);
        }

        EditorGUILayout.Space(4);

        if (m_Mode == ScanMode.Scene)
        {
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
                ScanScene();

            EditorGUI.BeginChangeCheck();
            m_IncludeInactive = GUILayout.Toggle(m_IncludeInactive, "包含未激活", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
                ScanScene();
        }
        else
        {
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48)))
                ScanFolder(m_FolderPath);
        }

        GUILayout.FlexibleSpace();

        m_SearchFilter = EditorGUILayout.TextField(m_SearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (m_Mode == ScanMode.Scene)
            EditorGUILayout.LabelField($"场景: {SceneManager.GetActiveScene().name}    Shader 数: {m_Entries.Count}", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField($"文件夹: {(string.IsNullOrEmpty(m_FolderPath) ? "未指定" : m_FolderPath)}    材质: {m_TotalMats}  Prefab: {m_TotalPrefabs}  Shader 数: {m_Entries.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    void DrawFolderField()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("拖入文件夹:", GUILayout.Width(72));

        EditorGUI.BeginChangeCheck();
        m_FolderAsset = (DefaultAsset)EditorGUILayout.ObjectField(m_FolderAsset, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck() && m_FolderAsset != null)
        {
            var path = AssetDatabase.GetAssetPath(m_FolderAsset);
            if (AssetDatabase.IsValidFolder(path))
            {
                m_FolderPath = path;
                ScanFolder(m_FolderPath);
            }
            else
            {
                Debug.LogWarning("[ShaderReplacer] 请拖入一个文件夹，而不是文件");
                m_FolderAsset = null;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void DrawShaderList()
    {
        m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

        string filter = m_SearchFilter.ToLowerInvariant();

        foreach (var entry in m_Entries)
        {
            if (!string.IsNullOrEmpty(filter) && !entry.name.ToLowerInvariant().Contains(filter))
                continue;

            DrawShaderEntry(entry);
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawShaderEntry(ShaderEntry entry)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (m_Mode == ScanMode.Scene)
            DrawShaderEntryScene(entry);
        else
            DrawShaderEntryFolder(entry);

        EditorGUILayout.EndVertical();
    }

    void DrawShaderEntryScene(ShaderEntry entry)
    {
        var uniqueRenderers = entry.users.Select(u => u.renderer).Distinct().ToList();
        var uniqueMats = entry.users.Select(u => u.material).Distinct().ToList();

        EditorGUILayout.BeginHorizontal();
        entry.foldout = EditorGUILayout.Foldout(entry.foldout,
            $"{entry.name}    ({uniqueMats.Count} 材质, {uniqueRenderers.Count} 物体)", true);

        if (GUILayout.Button("全选物体", EditorStyles.miniButtonLeft, GUILayout.Width(60)))
        {
            Selection.objects = uniqueRenderers.Select(r => r.gameObject).Cast<Object>().ToArray();
            if (uniqueRenderers.Count > 0)
                SceneView.lastActiveSceneView?.FrameSelected();
        }

        if (GUILayout.Button("全选材质", EditorStyles.miniButtonRight, GUILayout.Width(60)))
        {
            Selection.objects = uniqueMats.Cast<Object>().ToArray();
        }

        EditorGUILayout.EndHorizontal();

        if (entry.foldout)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.ObjectField("Shader", entry.shader, typeof(Shader), false);

            DrawReplaceRow(entry);

            DrawKeywordAnalysisSection(entry);

            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("材质列表:", EditorStyles.boldLabel);
            foreach (var mat in uniqueMats)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(mat, typeof(Material), false);

                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    var objs = entry.users
                        .Where(u => u.material == mat)
                        .Select(u => u.renderer.gameObject)
                        .Distinct().Cast<Object>().ToArray();
                    Selection.objects = objs;
                    if (objs.Length > 0)
                        SceneView.lastActiveSceneView?.FrameSelected();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("物体列表:", EditorStyles.boldLabel);
            foreach (var r in uniqueRenderers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(r.gameObject, typeof(GameObject), true);

                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    Selection.activeGameObject = r.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }
    }

    void DrawShaderEntryFolder(ShaderEntry entry)
    {
        int prefabCount = entry.prefabRefs.Select(p => p.prefabPath).Distinct().Count();

        EditorGUILayout.BeginHorizontal();
        entry.foldout = EditorGUILayout.Foldout(entry.foldout,
            $"{entry.name}    ({entry.folderMats.Count} 材质, {prefabCount} Prefab)", true);

        if (GUILayout.Button("全选材质", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            Selection.objects = entry.folderMats.Cast<Object>().ToArray();
        }

        EditorGUILayout.EndHorizontal();

        if (entry.foldout)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.ObjectField("Shader", entry.shader, typeof(Shader), false);

            DrawReplaceRow(entry);

            DrawKeywordAnalysisSection(entry);

            EditorGUILayout.Space(2);

            // --- Materials ---
            EditorGUILayout.LabelField("材质列表:", EditorStyles.boldLabel);
            foreach (var mat in entry.folderMats)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(mat, typeof(Material), false);

                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(mat);
                    Selection.activeObject = mat;
                }
                EditorGUILayout.EndHorizontal();
            }

            // --- Prefab References ---
            if (entry.prefabRefs.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Prefab 引用:", EditorStyles.boldLabel);

                var grouped = entry.prefabRefs
                    .GroupBy(p => p.prefabPath)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;
                    foreach (var pref in group)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"  {pref.hierarchyPath}", EditorStyles.miniLabel);
                        EditorGUILayout.ObjectField(pref.material, typeof(Material), false, GUILayout.Width(160));

                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            SelectPrefabChild(group.Key, pref.hierarchyPath);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    void DrawReplaceRow(ShaderEntry entry)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("替换为");
        m_ReplaceTarget = (Shader)EditorGUILayout.ObjectField(m_ReplaceTarget, typeof(Shader), false);
        EditorGUI.BeginDisabledGroup(m_ReplaceTarget == null || m_ReplaceTarget == entry.shader);
        if (GUILayout.Button("替换", GUILayout.Width(48)))
        {
            ReplaceShader(entry, m_ReplaceTarget);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    void DrawKeywordAnalysisSection(ShaderEntry entry)
    {
        EditorGUILayout.Space(2);

        if (entry.keywordAnalysis == null || !entry.keywordAnalysis.analyzed)
        {
            if (GUILayout.Button("分析 Keywords", GUILayout.Width(120)))
            {
                AnalyzeKeywords(entry);
            }
            return;
        }

        var analysis = entry.keywordAnalysis;

        var unused = analysis.keywords.Where(k => k.enabledBy.Count == 0).ToList();
        var used = analysis.keywords.Where(k => k.enabledBy.Count > 0).ToList();

        string label = $"Keyword 分析  (未使用: {unused.Count}, 已使用: {used.Count})";

        EditorGUILayout.BeginHorizontal();
        analysis.foldout = EditorGUILayout.Foldout(analysis.foldout, label, true);
        if (GUILayout.Button("重新分析", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            AnalyzeKeywords(entry);
            return;
        }
        EditorGUILayout.EndHorizontal();

        if (!analysis.foldout) return;

        EditorGUI.indentLevel++;

        // --- Unused keywords (highlighted) ---
        if (unused.Count > 0)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.7f, 0.3f);
            EditorGUILayout.LabelField($"--- 未使用 ({unused.Count}) ---", EditorStyles.boldLabel);
            GUI.color = prevColor;

            foreach (var kw in unused)
            {
                string suffix = kw.isLocal ? "  [local]" : "  [global]";
                EditorGUILayout.LabelField($"    {kw.keyword}{suffix}", EditorStyles.miniLabel);
            }
        }

        // --- Used keywords (with material list, default collapsed) ---
        if (used.Count > 0)
        {
            EditorGUILayout.Space(2);
            analysis.usedFoldout = EditorGUILayout.Foldout(analysis.usedFoldout,
                $"已使用 ({used.Count})", true);

            if (analysis.usedFoldout)
            {
                foreach (var kw in used)
                {
                    string suffix = kw.isLocal ? "  [local]" : "  [global]";
                    EditorGUILayout.LabelField($"    {kw.keyword}{suffix}  ({kw.enabledBy.Count} 材质)", EditorStyles.miniLabel);

                    EditorGUI.indentLevel++;
                    foreach (var mat in kw.enabledBy)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(mat, typeof(Material), false);
                        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(36)))
                        {
                            EditorGUIUtility.PingObject(mat);
                            Selection.activeObject = mat;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        if (analysis.keywords.Count == 0)
        {
            EditorGUILayout.LabelField("    该 Shader 未定义任何 keyword", EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
    }

    // ================================================================
    //  Prefab Navigation
    // ================================================================

    static void SelectPrefabChild(string prefabPath, string hierarchyPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        AssetDatabase.OpenAsset(prefab);

        EditorApplication.delayCall += () =>
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return;

            var root = stage.prefabContentsRoot;
            var parts = hierarchyPath.Split('/');

            Transform target = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                target = target.Find(parts[i]);
                if (target == null) break;
            }

            if (target != null)
            {
                Selection.activeGameObject = target.gameObject;
                EditorGUIUtility.PingObject(target.gameObject);
            }
        };
    }

    // ================================================================
    //  Replace
    // ================================================================

    void ReplaceShader(ShaderEntry entry, Shader newShader)
    {
        List<Material> mats;

        if (m_Mode == ScanMode.Scene)
            mats = entry.users.Select(u => u.material).Distinct().ToList();
        else
            mats = entry.folderMats;

        string desc = m_Mode == ScanMode.Scene
            ? $"影响 {entry.users.Select(u => u.renderer).Distinct().Count()} 个场景物体"
            : $"文件夹: {m_FolderPath}";

        if (!EditorUtility.DisplayDialog("确认替换",
            $"将 {mats.Count} 个材质的 Shader\n" +
            $"从: {entry.name}\n" +
            $"替换为: {newShader.name}\n\n" +
            desc,
            "替换", "取消"))
            return;

        Undo.RecordObjects(mats.Cast<Object>().ToArray(), "Replace Shader");

        foreach (var mat in mats)
            mat.shader = newShader;

        if (m_Mode == ScanMode.Folder)
            AssetDatabase.SaveAssets();

        Debug.Log($"[ShaderReplacer] 已将 {mats.Count} 个材质从 {entry.name} 替换为 {newShader.name}");

        if (m_Mode == ScanMode.Scene)
            ScanScene();
        else
            ScanFolder(m_FolderPath);
    }
}
