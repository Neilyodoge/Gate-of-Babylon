using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 小n的 Prefab 资源提取工具
/// 菜单路径：nTools/美术工具/Prefab资源快速复制
/// 将选中 Prefab 中引用的贴图、模型、材质球复制到指定目录，并按类型分类存放
/// 支持将 Prefab 内的资产引用替换为新复制出来的资产
/// </summary>
public class PrefabAssetExtractor : EditorWindow
{
    // ==================== 设置 ====================
    private string _outputFolder = "Assets/ExtractedAssets";
    private string _renamePrefix = "";  // 自定义命名前缀，留空则保留原始文件名
    private readonly List<GameObject> _prefabTargets = new List<GameObject>();
    private Vector2 _scrollPos;
    private Vector2 _logScrollPos;

    // ==================== 复制模式 ====================
    private enum CopyMode
    {
        All,                // 全部复制（模型 + 材质 + 贴图）
        ModelsOnly,         // 仅复制模型
        MaterialsAndTextures // 仅复制材质和贴图
    }
    private CopyMode _copyMode = CopyMode.All;

    // ==================== 替换引用选项 ====================
    private bool _replaceReferences = true;  // 是否将 Prefab 内的资产引用替换为新复制的资产

    // ==================== 提取结果日志 ====================
    private readonly List<string> _logMessages = new List<string>();
    private bool _hasExtracted = false;

    [MenuItem("nTools/美术工具/Prefab资源快速复制", false, 101)]
    public static void ShowWindow()
    {
        var window = GetWindow<PrefabAssetExtractor>("Prefab资源快速复制");
        window.minSize = new Vector2(420, 400);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Prefab 资源提取工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "将选中的 Prefab 中引用的 贴图(Texture)、模型(Model)、材质球(Material) 复制到指定目录下。\n" +
            "输出目录下会自动创建 Textures、Models、Materials 三个子文件夹。\n" +
            "注意：支持从 Scene 中拖入 Prefab 实例，工具会自动追溯到磁盘上的源 Prefab 进行提取。\n" +
            "若实例有未 Apply 的修改，提取的仍是磁盘上的原始版本，请先 Apply 再提取。",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ===== 输出目录 =====
        EditorGUILayout.LabelField("输出目录", EditorStyles.miniBoldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            _outputFolder = EditorGUILayout.TextField(_outputFolder);
            if (GUILayout.Button("选择…", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel("选择输出目录", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    // 转换为 Assets 相对路径
                    string dataPath = Application.dataPath.Replace("\\", "/");
                    selected = selected.Replace("\\", "/");
                    if (selected.StartsWith(dataPath))
                    {
                        _outputFolder = "Assets" + selected.Substring(dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", "请选择项目 Assets 目录下的文件夹", "确定");
                    }
                }
            }
        }

        EditorGUILayout.Space(4);

        // ===== 自定义命名 =====
        EditorGUILayout.LabelField("资源重命名（留空保留原名）", EditorStyles.miniBoldLabel);
        _renamePrefix = EditorGUILayout.TextField("命名前缀", _renamePrefix);
        if (!string.IsNullOrEmpty(_renamePrefix))
        {
            EditorGUILayout.HelpBox(
                $"示例：贴图 → {_renamePrefix}_tex_01, {_renamePrefix}_tex_02...\n" +
                $"　　　模型 → {_renamePrefix}_mod_01, {_renamePrefix}_mod_02...\n" +
                $"　　　材质 → {_renamePrefix}_mat_01, {_renamePrefix}_mat_02...",
                MessageType.None);
        }

        EditorGUILayout.Space(4);

        // ===== 复制模式选项 =====
        EditorGUILayout.LabelField("复制模式", EditorStyles.miniBoldLabel);
        _copyMode = (CopyMode)EditorGUILayout.EnumPopup("选择复制内容", _copyMode);
        switch (_copyMode)
        {
            case CopyMode.All:
                EditorGUILayout.HelpBox("将复制所有类型的资源：模型、材质球、贴图", MessageType.None);
                break;
            case CopyMode.ModelsOnly:
                EditorGUILayout.HelpBox("仅复制模型文件（FBX/OBJ 等）", MessageType.None);
                break;
            case CopyMode.MaterialsAndTextures:
                EditorGUILayout.HelpBox("仅复制材质球和贴图", MessageType.None);
                break;
        }

        EditorGUILayout.Space(4);

        // ===== 替换引用选项 =====
        _replaceReferences = EditorGUILayout.ToggleLeft("提取后替换 Prefab 内的资产引用为新复制的资产", _replaceReferences);
        if (_replaceReferences)
        {
            EditorGUILayout.HelpBox(
                "开启后，Prefab 中引用的资产将被替换为新复制出来的资产。\n" +
                "新复制的材质中的贴图引用也会同步替换为新复制的贴图（仅在同时复制材质和贴图时生效）。",
                MessageType.Info);
        }

        EditorGUILayout.Space(6);

        // ===== Prefab 列表 =====
        EditorGUILayout.LabelField("Prefab 列表", EditorStyles.miniBoldLabel);
        DrawPrefabList();

        EditorGUILayout.Space(6);

        // ===== 操作按钮 =====
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = _prefabTargets.Count > 0 && !string.IsNullOrEmpty(_outputFolder);
            if (GUILayout.Button("▶ 提取资源", GUILayout.Height(30)))
            {
                ExtractAssets();
            }
            GUI.enabled = true;

            if (_hasExtracted && GUILayout.Button("清空日志", GUILayout.Width(80), GUILayout.Height(30)))
            {
                _logMessages.Clear();
                _hasExtracted = false;
            }
        }

        // ===== 日志输出 =====
        if (_hasExtracted && _logMessages.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("提取日志", EditorStyles.miniBoldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScrollPos, EditorStyles.helpBox, GUILayout.MaxHeight(200)))
            {
                _logScrollPos = scroll.scrollPosition;
                foreach (var msg in _logMessages)
                {
                    EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }
    }

    // ==================================================================================
    //  Prefab 列表绘制（支持拖拽和手动添加）
    // ==================================================================================
    private void DrawPrefabList()
    {
        // 拖拽区域
        Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        var dropStyle = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };

        bool isDraggingOver = dropArea.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0;
        Color oldBg = GUI.backgroundColor;
        if (isDraggingOver) GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 1f);
        GUI.Box(dropArea, _prefabTargets.Count == 0 ? "拖拽 Prefab 到此处添加" : $"拖拽添加（已有 {_prefabTargets.Count} 个）", dropStyle);
        GUI.backgroundColor = oldBg;

        // 处理拖拽事件
        if (dropArea.Contains(Event.current.mousePosition))
        {
            if (Event.current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    var prefabAsset = GetPrefabAssetFromObject(obj);
                    if (prefabAsset != null && !_prefabTargets.Contains(prefabAsset))
                        _prefabTargets.Add(prefabAsset);
                }
                Event.current.Use();
                Repaint();
            }
        }

        // 列表显示
        int removeIndex = -1;
        if (_prefabTargets.Count > 0)
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos, GUILayout.MaxHeight(120)))
            {
                _scrollPos = scroll.scrollPosition;
                for (int i = 0; i < _prefabTargets.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 序号
                        EditorGUILayout.LabelField($"[{i + 1}]", GUILayout.Width(30));

                        // Prefab 引用（支持拖入场景实例，自动追溯到源 Prefab）
                        var newObj = (GameObject)EditorGUILayout.ObjectField(_prefabTargets[i], typeof(GameObject), true);
                        if (newObj != null && newObj != _prefabTargets[i])
                        {
                            var resolved = GetPrefabAssetFromObject(newObj);
                            if (resolved != null)
                                _prefabTargets[i] = resolved;
                        }

                        // 删除按钮
                        if (GUILayout.Button("✕", GUILayout.Width(22)))
                            removeIndex = i;
                    }
                }
            }
        }
        if (removeIndex >= 0) _prefabTargets.RemoveAt(removeIndex);

        // 底部按钮
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ 从选中添加", GUILayout.Height(20)))
            {
                foreach (var obj in Selection.objects)
                {
                    var prefabAsset = GetPrefabAssetFromObject(obj);
                    if (prefabAsset != null && !_prefabTargets.Contains(prefabAsset))
                        _prefabTargets.Add(prefabAsset);
                }
            }
            if (_prefabTargets.Count > 0 && GUILayout.Button("清空列表", GUILayout.Width(70), GUILayout.Height(20)))
            {
                _prefabTargets.Clear();
            }
        }
    }

    // ==================================================================================
    //  核心：提取资源
    // ==================================================================================
    private void ExtractAssets()
    {
        _logMessages.Clear();
        _hasExtracted = true;

        bool copyModels = _copyMode == CopyMode.All || _copyMode == CopyMode.ModelsOnly;
        bool copyMatsAndTex = _copyMode == CopyMode.All || _copyMode == CopyMode.MaterialsAndTextures;

        // 创建输出子目录（按需）
        string texDir = _outputFolder + "/Textures";
        string modDir = _outputFolder + "/Models";
        string matDir = _outputFolder + "/Materials";

        if (copyMatsAndTex) EnsureDirectory(texDir);
        if (copyModels) EnsureDirectory(modDir);
        if (copyMatsAndTex) EnsureDirectory(matDir);

        // 收集所有资源路径（使用 HashSet 去重，只收集路径不加载资源，大幅提速）
        var allTextures = new HashSet<string>();
        var allModels = new HashSet<string>();
        var allMaterials = new HashSet<string>();

        foreach (var prefab in _prefabTargets)
        {
            if (prefab == null) continue;
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            _logMessages.Add($"── 扫描 Prefab: {prefab.name} ({prefabPath}) ──");

            // 获取 Prefab 中所有依赖的资源路径
            string[] deps = AssetDatabase.GetDependencies(prefabPath, true);
            foreach (string dep in deps)
            {
                // 排除自身和脚本
                if (dep == prefabPath) continue;

                // 通过扩展名快速分类，避免 LoadAsset 造成的性能开销
                string ext = Path.GetExtension(dep).ToLower();

                // 跳过脚本/Shader
                if (ext == ".cs" || ext == ".shader" || ext == ".cginc" || ext == ".hlsl"
                    || ext == ".compute" || ext == ".asmdef" || ext == ".asmref") continue;

                // 贴图（按常见贴图扩展名判断）
                if (copyMatsAndTex && IsTextureFile(ext))
                {
                    allTextures.Add(dep);
                }
                // 材质
                else if (copyMatsAndTex && ext == ".mat")
                {
                    allMaterials.Add(dep);
                }
                // 模型文件（FBX/OBJ 等）
                else if (copyModels && IsModelFile(ext))
                {
                    allModels.Add(dep);
                }
            }
        }

        // 复制资源（不使用 StartAssetEditing，避免 CopyAsset 在暂停导入期间失败）
        bool useRename = !string.IsNullOrEmpty(_renamePrefix);
        string prefix = useRename ? _renamePrefix : null;

        // 路径映射表：旧路径 → 新路径（用于后续替换引用）
        var texPathMap = new Dictionary<string, string>();
        var modPathMap = new Dictionary<string, string>();
        var matPathMap = new Dictionary<string, string>();

        int texCount = 0, modCount = 0, matCount = 0;

        if (copyMatsAndTex)
        {
            texCount = CopyAssetsByPath(allTextures, texDir, prefix, "tex", "贴图", texPathMap);
        }
        if (copyModels)
        {
            modCount = CopyAssetsByPath(allModels, modDir, prefix, "mod", "模型", modPathMap);
        }
        if (copyMatsAndTex)
        {
            matCount = CopyAssetsByPath(allMaterials, matDir, prefix, "mat", "材质", matPathMap);
        }

        AssetDatabase.Refresh();

        // 如果同时复制了材质和贴图，更新新材质中的贴图引用
        if (copyMatsAndTex && texPathMap.Count > 0 && matPathMap.Count > 0)
        {
            UpdateMaterialTextureReferences(matPathMap, texPathMap);
        }

        // 替换 Prefab 内的资产引用
        if (_replaceReferences)
        {
            // 合并所有路径映射
            var allPathMap = new Dictionary<string, string>();
            foreach (var kv in texPathMap) allPathMap[kv.Key] = kv.Value;
            foreach (var kv in modPathMap) allPathMap[kv.Key] = kv.Value;
            foreach (var kv in matPathMap) allPathMap[kv.Key] = kv.Value;

            if (allPathMap.Count > 0)
            {
                ReplacePrefabReferences(allPathMap);
            }
        }

        _logMessages.Add("");
        _logMessages.Add($"══ 提取完成 ══");
        if (copyMatsAndTex)
        {
            _logMessages.Add($"  贴图: {texCount} 个 → {texDir}");
            _logMessages.Add($"  材质球: {matCount} 个 → {matDir}");
        }
        if (copyModels)
        {
            _logMessages.Add($"  模型: {modCount} 个 → {modDir}");
        }

        string logParts = "";
        if (copyMatsAndTex) logParts += $"贴图: {texCount}, 材质: {matCount}";
        if (copyModels) logParts += (logParts.Length > 0 ? ", " : "") + $"模型: {modCount}";
        Debug.Log($"[Prefab资源提取工具] Prefab资源提取完成 —— {logParts}，输出目录: {_outputFolder}");

        Repaint();
    }

    // ==================================================================================
    //  统一复制方法（基于路径，不加载资源对象）
    //  pathMap: 记录 旧路径 → 新路径 的映射，用于后续替换引用
    // ==================================================================================
    private int CopyAssetsByPath(HashSet<string> srcPaths, string targetDir, string renamePrefix,
        string typeTag, string displayTag, Dictionary<string, string> pathMap)
    {
        int count = 0;
        int index = 1;
        foreach (string srcPath in srcPaths)
        {
            if (string.IsNullOrEmpty(srcPath)) continue;

            string srcFileName = Path.GetFileName(srcPath);
            string ext = Path.GetExtension(srcPath);
            string newFileName = renamePrefix != null
                ? $"{renamePrefix}_{typeTag}_{index:D2}{ext}"
                : srcFileName;
            string dstPath = GetUniqueAssetPath(targetDir + "/" + newFileName);

            if (AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                string displayName = renamePrefix != null
                    ? $"{srcFileName} → {Path.GetFileName(dstPath)}"
                    : srcFileName;
                _logMessages.Add($"  [{displayTag}] {displayName}  ←  {srcPath}");
                pathMap[srcPath] = dstPath;
                count++;
                index++;
            }
            else
            {
                _logMessages.Add($"  [{displayTag}] ✕ 复制失败: {srcPath}");
            }
        }
        return count;
    }

    // ==================================================================================
    //  更新新复制的材质中的贴图引用，指向新复制的贴图
    // ==================================================================================
    private void UpdateMaterialTextureReferences(Dictionary<string, string> matPathMap, Dictionary<string, string> texPathMap)
    {
        _logMessages.Add("");
        _logMessages.Add("── 更新材质中的贴图引用 ──");

        // 构建旧贴图对象 → 新贴图对象的映射
        var oldTexToNew = new Dictionary<Texture, Texture>();
        foreach (var kv in texPathMap)
        {
            var oldTex = AssetDatabase.LoadAssetAtPath<Texture>(kv.Key);
            var newTex = AssetDatabase.LoadAssetAtPath<Texture>(kv.Value);
            if (oldTex != null && newTex != null)
            {
                oldTexToNew[oldTex] = newTex;
            }
        }

        if (oldTexToNew.Count == 0) return;

        foreach (var kv in matPathMap)
        {
            string newMatPath = kv.Value;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(newMatPath);
            if (mat == null) continue;

            bool modified = false;
            // 遍历材质的所有贴图属性
            var shader = mat.shader;
            int propCount = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < propCount; i++)
            {
                if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;

                string propName = ShaderUtil.GetPropertyName(shader, i);
                var currentTex = mat.GetTexture(propName);
                if (currentTex != null && oldTexToNew.TryGetValue(currentTex, out var newTex))
                {
                    mat.SetTexture(propName, newTex);
                    modified = true;
                    _logMessages.Add($"  [材质贴图替换] {Path.GetFileName(newMatPath)}.{propName}: {currentTex.name} → {newTex.name}");
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(mat);
            }
        }

        AssetDatabase.SaveAssets();
    }

    // ==================================================================================
    //  替换 Prefab 内的资产引用为新复制的资产
    // ==================================================================================
    private void ReplacePrefabReferences(Dictionary<string, string> allPathMap)
    {
        _logMessages.Add("");
        _logMessages.Add("── 替换 Prefab 内的资产引用 ──");

        // 构建旧资产对象 → 新资产对象的映射
        var oldObjToNew = new Dictionary<Object, Object>();
        foreach (var kv in allPathMap)
        {
            // 加载所有子资产（如 FBX 中的 Mesh、Material 等）
            var oldAssets = AssetDatabase.LoadAllAssetsAtPath(kv.Key);
            var newAssets = AssetDatabase.LoadAllAssetsAtPath(kv.Value);

            if (oldAssets == null || newAssets == null) continue;

            // 主资产映射
            var oldMain = AssetDatabase.LoadMainAssetAtPath(kv.Key);
            var newMain = AssetDatabase.LoadMainAssetAtPath(kv.Value);
            if (oldMain != null && newMain != null)
            {
                oldObjToNew[oldMain] = newMain;
            }

            // 子资产按名称和类型匹配
            foreach (var oldSub in oldAssets)
            {
                if (oldSub == null || oldSub == oldMain) continue;
                foreach (var newSub in newAssets)
                {
                    if (newSub == null || newSub == newMain) continue;
                    if (oldSub.GetType() == newSub.GetType() && oldSub.name == newSub.name)
                    {
                        oldObjToNew[oldSub] = newSub;
                        break;
                    }
                }
            }
        }

        if (oldObjToNew.Count == 0)
        {
            _logMessages.Add("  没有需要替换的引用");
            return;
        }

        foreach (var prefab in _prefabTargets)
        {
            if (prefab == null) continue;
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            _logMessages.Add($"  处理 Prefab: {prefab.name}");

            // 打开 Prefab 进行编辑
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            bool prefabModified = false;

            try
            {
                // 遍历 Prefab 中所有组件
                var allComponents = prefabRoot.GetComponentsInChildren<Component>(true);
                foreach (var comp in allComponents)
                {
                    if (comp == null) continue;

                    // 特殊处理 Renderer 组件的材质引用
                    if (comp is Renderer renderer)
                    {
                        var mats = renderer.sharedMaterials;
                        bool matChanged = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] != null && oldObjToNew.TryGetValue(mats[i], out var newMat))
                            {
                                _logMessages.Add($"    [{comp.gameObject.name}] Renderer.材质[{i}]: {mats[i].name} → {((Material)newMat).name}");
                                mats[i] = (Material)newMat;
                                matChanged = true;
                            }
                        }
                        if (matChanged)
                        {
                            renderer.sharedMaterials = mats;
                            prefabModified = true;
                        }
                    }

                    // 特殊处理 MeshFilter 的 Mesh 引用
                    if (comp is MeshFilter meshFilter)
                    {
                        if (meshFilter.sharedMesh != null && oldObjToNew.TryGetValue(meshFilter.sharedMesh, out var newMesh))
                        {
                            _logMessages.Add($"    [{comp.gameObject.name}] MeshFilter.mesh: {meshFilter.sharedMesh.name} → {((Mesh)newMesh).name}");
                            meshFilter.sharedMesh = (Mesh)newMesh;
                            prefabModified = true;
                        }
                    }

                    // 特殊处理 SkinnedMeshRenderer 的 Mesh 引用
                    if (comp is SkinnedMeshRenderer skinnedMesh)
                    {
                        if (skinnedMesh.sharedMesh != null && oldObjToNew.TryGetValue(skinnedMesh.sharedMesh, out var newMesh))
                        {
                            _logMessages.Add($"    [{comp.gameObject.name}] SkinnedMeshRenderer.mesh: {skinnedMesh.sharedMesh.name} → {((Mesh)newMesh).name}");
                            skinnedMesh.sharedMesh = (Mesh)newMesh;
                            prefabModified = true;
                        }
                    }

                    // 通用处理：通过 SerializedObject 遍历所有序列化属性中的对象引用
                    var so = new SerializedObject(comp);
                    var sp = so.GetIterator();
                    bool soModified = false;
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference
                            && sp.objectReferenceValue != null
                            && oldObjToNew.TryGetValue(sp.objectReferenceValue, out var newObj))
                        {
                            _logMessages.Add($"    [{comp.gameObject.name}] {comp.GetType().Name}.{sp.propertyPath}: {sp.objectReferenceValue.name} → {newObj.name}");
                            sp.objectReferenceValue = newObj;
                            soModified = true;
                        }
                    }
                    if (soModified)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        prefabModified = true;
                    }
                }

                if (prefabModified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    _logMessages.Add($"  ✓ Prefab 已保存: {prefabPath}");
                }
                else
                {
                    _logMessages.Add($"  - Prefab 无需修改: {prefab.name}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ==================================================================================
    //  辅助方法
    // ==================================================================================

    /// <summary>
    /// 从任意对象中解析出 Prefab 资源（支持 Project 资源和 Scene 中的 Prefab 实例）
    /// </summary>
    private static GameObject GetPrefabAssetFromObject(Object obj)
    {
        if (!(obj is GameObject go)) return null;

        var assetType = PrefabUtility.GetPrefabAssetType(go);
        if (assetType == PrefabAssetType.NotAPrefab) return null;

        // 如果是 Project 中的 Prefab 资源本体，直接返回
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go)))
            return go;

        // 如果是场景中的 Prefab 实例，追溯到源 Prefab 资源
        var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (source != null)
        {
            // 获取最顶层的 Prefab 根资源
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                return root != null ? root : source;
            }
            return source;
        }

        return null;
    }

    /// <summary>
    /// 判断是否为贴图文件
    /// </summary>
    private static bool IsTextureFile(string extLower)
    {
        return extLower == ".png" || extLower == ".tga" || extLower == ".jpg" || extLower == ".jpeg"
            || extLower == ".psd" || extLower == ".bmp" || extLower == ".gif" || extLower == ".tif"
            || extLower == ".tiff" || extLower == ".exr" || extLower == ".hdr";
    }

    /// <summary>
    /// 判断是否为模型文件（FBX/OBJ/DAE 等）
    /// </summary>
    private static bool IsModelFile(string extLower)
    {
        return extLower == ".fbx" || extLower == ".obj" || extLower == ".dae" || extLower == ".blend"
            || extLower == ".3ds" || extLower == ".max" || extLower == ".ma" || extLower == ".mb"
            || extLower == ".asset";
    }

    /// <summary>
    /// 确保目录存在，不存在则创建
    /// </summary>
    private void EnsureDirectory(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        string[] parts = assetPath.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
                _logMessages.Add($"  创建目录: {next}");
            }
            current = next;
        }
    }

    /// <summary>
    /// 获取唯一的资源路径，如果目标路径已存在则自动加后缀
    /// </summary>
    private static string GetUniqueAssetPath(string path)
    {
        if (!File.Exists(path)) return path;

        string dir = Path.GetDirectoryName(path).Replace("\\", "/");
        string nameNoExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int index = 1;
        string newPath;
        do
        {
            newPath = $"{dir}/{nameNoExt}_{index}{ext}";
            index++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
