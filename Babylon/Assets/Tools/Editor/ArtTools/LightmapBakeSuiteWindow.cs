using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EditorTools.LightmapTools
{
    /// <summary>
    /// Lightmap 烘焙套件（合并「单物体烘焙」+「Lightmap 转换」两个工具）。
    /// · 单物体烘焙：只让选中/拖入的物体烘到 Lightmap，其它走 Light Probes；可勾选光源切 Baked；支持一键还原。
    /// · Lightmap 转换：把烘焙 lightmap 源图（RGBM/DLDR/FULL_HDR）解码，可选 ST 重映射，导出 EXR/PNG。
    /// 顶部用 Tab 切换两个功能页。
    /// </summary>
    public class LightmapBakeSuiteWindow : EditorWindow
    {
        enum Tab { Bake = 0, Convert = 1 }
        Tab m_Tab = Tab.Bake;

        [MenuItem("Tools_3D/美术/场景/Lightmap 烘焙套件")]
        public static void Open()
        {
            var win = GetWindow<LightmapBakeSuiteWindow>(false, "Lightmap 烘焙套件", true);
            win.minSize = new Vector2(440, 460);
            win.Show();
            win.Focus();
        }

        void OnEnable()
        {
            RefreshLights();
            if (m_Source == null && Selection.activeObject is Texture2D t) m_Source = t;
            Selection.selectionChanged += Repaint;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        void OnGUI()
        {
            m_Tab = (Tab)GUILayout.Toolbar((int)m_Tab, new[] { "单物体烘焙", "Lightmap 转换" });
            EditorGUILayout.Space(4);

            if (m_Tab == Tab.Bake) DrawBakeTab();
            else DrawConvertTab();
        }

        // =================================================================================
        //  Tab 1：单物体烘焙
        // =================================================================================

        const string SessionKey = "LightmapBakeSuite.State";

        [Serializable]
        class RendererState
        {
            public int instanceId;   // MeshRenderer 所在 GameObject 的 InstanceID
            public int staticFlags;  // 原 StaticEditorFlags
            public int receiveGI;    // 原 ReceiveGI
        }

        [Serializable]
        class LightState
        {
            public int instanceId;   // Light 组件 InstanceID
            public int bakeType;     // 原 LightmapBakeType
        }

        [Serializable]
        class BakeState
        {
            public List<RendererState> renderers = new List<RendererState>();
            public List<LightState> lights = new List<LightState>();
        }

        /// <summary>光源列表项（UI 用，不持久化）。</summary>
        class LightItem
        {
            public Light light;
            public bool bake; // 勾选：Apply 时切到 Baked
        }

        [SerializeField] List<GameObject> m_Targets = new List<GameObject>();
        [SerializeField] List<GameObject> m_Excludes = new List<GameObject>(); // 完全不参与烘焙：去掉 ContributeGI
        [SerializeField] LightingSettings m_LightingSettings; // 可选：烘焙前应用到已加载场景的光照设置
        readonly List<LightItem> m_Lights = new List<LightItem>();
        Vector2 m_BakeScroll;

        bool IsApplied => SessionState.GetString(SessionKey, string.Empty).Length > 0;

        void DrawBakeTab()
        {
            m_BakeScroll = EditorGUILayout.BeginScrollView(m_BakeScroll);

            EditorGUILayout.HelpBox(
                "用法：\n" +
                "1) 把要单独烘焙的物体拖入下方列表，或在场景中选中后点「用当前选中」。\n" +
                "2) 可选：把完全不参与烘焙的 prefab/物体拖入「排除列表」，应用时会去掉它们的 ContributeGI。\n" +
                "3) 点「应用」：非排除的 MeshRenderer 勾 Static(ContributeGI)，目标 Receive GI=Lightmaps，其它=Light Probes；排除的去掉 ContributeGI。\n" +
                "4) 点「开始烘焙」执行 Bake。\n" +
                "5) 点「一键还原」恢复所有物体的原始 Static / Receive GI / 光源模式。",
                MessageType.Info);

            DrawTargetList();
            DrawExcludeList();
            DrawLights();
            DrawLightingSettings();

            EditorGUILayout.Space();

            if (!IsApplied)
            {
                int count = CountTargetRenderers();
                using (new EditorGUI.DisabledScope(count == 0))
                {
                    if (GUILayout.Button($"应用（目标含 {count} 个 MeshRenderer）", GUILayout.Height(30)))
                        Apply();
                }
                if (count == 0)
                    EditorGUILayout.LabelField("目标列表为空或不含 MeshRenderer。", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox("当前已处于「单物体烘焙」状态，烘焙完成后请点「一键还原」。", MessageType.Warning);

                if (GUILayout.Button("开始烘焙 (Lightmapping.Bake)", GUILayout.Height(28)))
                    Bake();

                GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                if (GUILayout.Button("一键还原", GUILayout.Height(28)))
                    Restore();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawTargetList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("烘焙目标对象", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(IsApplied))
            {
                if (GUILayout.Button("用当前选中", EditorStyles.miniButton, GUILayout.Width(80)))
                    UseSelection();
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    m_Targets.Clear();
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(IsApplied))
            {
                int removeIdx = -1;
                for (int i = 0; i < m_Targets.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    m_Targets[i] = (GameObject)EditorGUILayout.ObjectField(m_Targets[i], typeof(GameObject), true);
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)))
                        removeIdx = i;
                    EditorGUILayout.EndHorizontal();
                }
                if (removeIdx >= 0) m_Targets.RemoveAt(removeIdx);

                // 末尾留一个空槽用于拖入新对象
                var added = (GameObject)EditorGUILayout.ObjectField("拖入新对象", null, typeof(GameObject), true);
                if (added != null && !m_Targets.Contains(added))
                    m_Targets.Add(added);
            }

            EditorGUILayout.EndVertical();
        }

        void DrawExcludeList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排除对象（不参与烘焙）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(IsApplied))
            {
                if (GUILayout.Button("加入选中", EditorStyles.miniButton, GUILayout.Width(80)))
                    AddSelectionToExcludes();
                if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    m_Excludes.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("列表内物体（含子层级）会被去掉 Static(ContributeGI)，完全不参与 GI/烘焙。",
                EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(IsApplied))
            {
                int removeIdx = -1;
                for (int i = 0; i < m_Excludes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    m_Excludes[i] = (GameObject)EditorGUILayout.ObjectField(m_Excludes[i], typeof(GameObject), true);
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)))
                        removeIdx = i;
                    EditorGUILayout.EndHorizontal();
                }
                if (removeIdx >= 0) m_Excludes.RemoveAt(removeIdx);

                // 末尾留一个空槽用于拖入新对象
                var added = (GameObject)EditorGUILayout.ObjectField("拖入新对象", null, typeof(GameObject), true);
                if (added != null && !m_Excludes.Contains(added))
                    m_Excludes.Add(added);
            }

            EditorGUILayout.EndVertical();
        }

        void AddSelectionToExcludes()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go != null && !m_Excludes.Contains(go))
                    m_Excludes.Add(go);
            }
        }

        /// <summary>收集排除对象（含子层级）里所有 MeshRenderer 的 GameObject InstanceID。</summary>
        HashSet<int> CollectExcludeRenderers()
        {
            var set = new HashSet<int>();
            foreach (var go in m_Excludes)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    set.Add(r.gameObject.GetInstanceID());
            }
            return set;
        }

        void DrawLights()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"场景光源 ({m_Lights.Count})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(IsApplied))
            {
                if (GUILayout.Button("刷新光源", EditorStyles.miniButton, GUILayout.Width(70)))
                    RefreshLights();
                if (GUILayout.Button("全勾", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                    foreach (var it in m_Lights) it.bake = true;
                if (GUILayout.Button("全不勾", EditorStyles.miniButtonRight, GUILayout.Width(52)))
                    foreach (var it in m_Lights) it.bake = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("勾选的光源在「应用」时切到 Baked 模式（未勾选保持不变），还原时恢复。",
                EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(IsApplied))
            {
                for (int i = 0; i < m_Lights.Count; i++)
                {
                    var it = m_Lights[i];
                    if (it.light == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    it.bake = GUILayout.Toggle(it.bake, "Bake", EditorStyles.miniButton, GUILayout.Width(48));
                    EditorGUILayout.ObjectField(it.light, typeof(Light), true);
                    GUILayout.Label(new GUIContent(it.light.lightmapBakeType.ToString(), "当前 Light Mode"),
                        EditorStyles.miniLabel, GUILayout.Width(64));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        void DrawLightingSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("烘焙光照设置 (可选)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            m_LightingSettings = (LightingSettings)EditorGUILayout.ObjectField(
                new GUIContent("Lighting Settings", "指定一个 Lighting Settings 资源；点「开始烘焙」前会应用到当前所有已加载场景。留空则用场景当前设置"),
                m_LightingSettings, typeof(LightingSettings), false);
            if (GUILayout.Button("读取当前", EditorStyles.miniButton, GUILayout.Width(64)))
            {
                var cur = Lightmapping.GetLightingSettingsForScene(EditorSceneManager.GetActiveScene());
                if (cur != null) m_LightingSettings = cur;
            }
            EditorGUILayout.EndHorizontal();

            if (m_LightingSettings == null)
                EditorGUILayout.LabelField("未指定：烘焙时沿用场景当前的 Lighting Settings。", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        void UseSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go != null && !m_Targets.Contains(go))
                    m_Targets.Add(go);
            }
        }

        /// <summary>收集目标对象（含子层级）里所有 MeshRenderer 的 GameObject InstanceID。</summary>
        HashSet<int> CollectTargetRenderers()
        {
            var set = new HashSet<int>();
            foreach (var go in m_Targets)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    set.Add(r.gameObject.GetInstanceID());
            }
            return set;
        }

        int CountTargetRenderers() => CollectTargetRenderers().Count;

        /// <summary>扫描所有已加载场景的 Light，保留已有勾选状态。</summary>
        void RefreshLights()
        {
            var prevBake = new HashSet<int>();
            foreach (var it in m_Lights)
                if (it.light != null && it.bake) prevBake.Add(it.light.GetInstanceID());

            m_Lights.Clear();
            for (int s = 0; s < EditorSceneManager.sceneCount; s++)
            {
                var scene = EditorSceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var l in root.GetComponentsInChildren<Light>(true))
                        m_Lights.Add(new LightItem { light = l, bake = prevBake.Contains(l.GetInstanceID()) });
            }
        }

        /// <summary>当前所有已加载场景里所有的 MeshRenderer（含未激活物体）。</summary>
        static List<MeshRenderer> CollectAllSceneRenderers()
        {
            var list = new List<MeshRenderer>();
            for (int s = 0; s < EditorSceneManager.sceneCount; s++)
            {
                var scene = EditorSceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    list.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
            }
            return list;
        }

        void Apply()
        {
            if (IsApplied)
            {
                EditorUtility.DisplayDialog("单物体 Lightmap 烘焙", "已处于应用状态，请先「一键还原」。", "OK");
                return;
            }

            var targetIds = CollectTargetRenderers();
            if (targetIds.Count == 0)
            {
                EditorUtility.DisplayDialog("单物体 Lightmap 烘焙", "目标对象里没有 MeshRenderer。", "OK");
                return;
            }

            var excludeIds = CollectExcludeRenderers();
            var allRenderers = CollectAllSceneRenderers();
            var state = new BakeState();
            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();

            int targetCount = 0, excludeCount = 0, otherCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                var go = r.gameObject;
                // 排除优先于目标：既在排除又在目标里时，按"不参与烘焙"处理
                bool isExcluded = excludeIds.Contains(go.GetInstanceID());
                bool isTarget = !isExcluded && targetIds.Contains(go.GetInstanceID());

                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                state.renderers.Add(new RendererState
                {
                    instanceId = go.GetInstanceID(),
                    staticFlags = (int)flags,
                    receiveGI = (int)r.receiveGI,
                });

                Undo.RegisterCompleteObjectUndo(go, "Single Object Bake");
                Undo.RegisterCompleteObjectUndo(r, "Single Object Bake");

                if (isExcluded)
                {
                    // 排除对象：去掉 ContributeGI，完全不参与烘焙
                    GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.LightProbes;
                    excludeCount++;
                }
                else
                {
                    // 非排除：勾 Static(ContributeGI)；目标 -> Lightmaps，其它 -> Light Probes
                    GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
                    r.receiveGI = isTarget ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
                    if (isTarget) targetCount++; else otherCount++;
                }

                EditorUtility.SetDirty(go);
                EditorUtility.SetDirty(r);
                dirtyScenes.Add(go.scene);
            }

            // 勾选的光源切到 Baked 模式，并备份原 LightmapBakeType
            int bakedLights = 0;
            foreach (var it in m_Lights)
            {
                if (it.light == null || !it.bake) continue;
                var l = it.light;

                state.lights.Add(new LightState
                {
                    instanceId = l.GetInstanceID(),
                    bakeType = (int)l.lightmapBakeType,
                });

                Undo.RegisterCompleteObjectUndo(l, "Single Object Bake");
                l.lightmapBakeType = LightmapBakeType.Baked;
                EditorUtility.SetDirty(l);
                dirtyScenes.Add(l.gameObject.scene);
                bakedLights++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (var sc in dirtyScenes)
                if (sc.IsValid()) EditorSceneManager.MarkSceneDirty(sc);

            SessionState.SetString(SessionKey, JsonUtility.ToJson(state));

            Debug.Log($"[单物体烘焙] 已应用：目标 {targetCount} 个(Receive GI=Lightmaps)，其它 {otherCount} 个(Receive GI=Light Probes)，" +
                $"排除 {excludeCount} 个(去掉 ContributeGI，不参与烘焙)；{bakedLights} 个光源切到 Baked。");
            Repaint();
        }

        void Restore()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (json.Length == 0)
            {
                EditorUtility.DisplayDialog("单物体 Lightmap 烘焙", "没有可还原的记录。", "OK");
                return;
            }

            var state = JsonUtility.FromJson<BakeState>(json);
            int restored = 0, missing = 0;
            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var rs in state.renderers)
            {
                var go = EditorUtility.InstanceIDToObject(rs.instanceId) as GameObject;
                if (go == null) { missing++; continue; }

                Undo.RegisterCompleteObjectUndo(go, "Restore Single Object Bake");
                GameObjectUtility.SetStaticEditorFlags(go, (StaticEditorFlags)rs.staticFlags);

                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Undo.RegisterCompleteObjectUndo(mr, "Restore Single Object Bake");
                    mr.receiveGI = (ReceiveGI)rs.receiveGI;
                    EditorUtility.SetDirty(mr);
                }

                EditorUtility.SetDirty(go);
                dirtyScenes.Add(go.scene);
                restored++;
            }

            int lightsRestored = 0;
            if (state.lights != null)
            {
                foreach (var ls in state.lights)
                {
                    var l = EditorUtility.InstanceIDToObject(ls.instanceId) as Light;
                    if (l == null) { missing++; continue; }

                    Undo.RegisterCompleteObjectUndo(l, "Restore Single Object Bake");
                    l.lightmapBakeType = (LightmapBakeType)ls.bakeType;
                    EditorUtility.SetDirty(l);
                    dirtyScenes.Add(l.gameObject.scene);
                    lightsRestored++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (var sc in dirtyScenes)
                if (sc.IsValid()) EditorSceneManager.MarkSceneDirty(sc);

            SessionState.EraseString(SessionKey);
            Debug.Log($"[单物体烘焙] 已还原 {restored} 个 MeshRenderer、{lightsRestored} 个光源" + (missing > 0 ? $"（{missing} 个对象已不存在，跳过）" : "") + "。");
            RefreshLights();
            Repaint();
        }

        void Bake()
        {
            if (Lightmapping.isRunning)
            {
                EditorUtility.DisplayDialog("单物体 Lightmap 烘焙", "已有烘焙任务在进行中。", "OK");
                return;
            }

            // 可选：把指定的 Lighting Settings 应用到所有已加载场景
            if (m_LightingSettings != null)
            {
                int applied = 0;
                for (int s = 0; s < EditorSceneManager.sceneCount; s++)
                {
                    var scene = EditorSceneManager.GetSceneAt(s);
                    if (!scene.isLoaded) continue;
                    Lightmapping.SetLightingSettingsForScene(scene, m_LightingSettings);
                    applied++;
                }
                Debug.Log($"[单物体烘焙] 已应用 Lighting Settings「{m_LightingSettings.name}」到 {applied} 个场景。");
            }

            Debug.Log("[单物体烘焙] 开始烘焙当前场景…");
            bool ok = Lightmapping.Bake();
            Debug.Log(ok ? "[单物体烘焙] 烘焙完成。" : "[单物体烘焙] 烘焙失败或被取消。");
        }

        // =================================================================================
        //  Tab 2：Lightmap 转换
        // =================================================================================

        public enum DecodeMode { RGBM, DLDR, FULL_HDR }
        public enum OutputFormat { EXR_HDR, PNG_LDR }

        Texture2D m_Source;
        DecodeMode m_Decode = DecodeMode.RGBM;   // 默认 RGBM
        Vector2 m_ConvScroll;

        // ST 重映射（可选）：默认勾选，配合自动取 ST 一键把 lightmapScaleOffset 烘进贴图
        bool m_ApplyST = true;
        bool m_AutoGrabST = true;
        Vector2 m_Tiling = Vector2.one;
        Vector2 m_Offset = Vector2.zero;
        Color m_FillColor = Color.black;

        // 输出
        OutputFormat m_Output = OutputFormat.PNG_LDR;
        bool m_CustomSize = false;
        int m_OutW = 0, m_OutH = 0;
        string m_Suffix = "_conv";

        void DrawConvertTab()
        {
            m_ConvScroll = EditorGUILayout.BeginScrollView(m_ConvScroll);

            EditorGUILayout.HelpBox(
                "把烘焙 lightmap 源贴图转成可贴在模型上的普通贴图。\n" +
                "流程：解码(RGBM/DLDR/FULL_HDR) → 可选 ST 重映射 → 输出 EXR/PNG。",
                MessageType.Info);

            // 1. 源贴图
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1. 源贴图", EditorStyles.boldLabel);
            m_Source = (Texture2D)EditorGUILayout.ObjectField("Lightmap 源", m_Source, typeof(Texture2D), false);
            DrawSourceInfo();

            // 2. 解码
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. 解码格式", EditorStyles.boldLabel);
            m_Decode = (DecodeMode)EditorGUILayout.EnumPopup("解码方式", m_Decode);
            EditorGUILayout.LabelField(" ", DecodeFormulaHint(), EditorStyles.miniLabel);

            // 3. ST 重映射（可选）
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3. ST 重映射（可选）", EditorStyles.boldLabel);
            m_ApplyST = EditorGUILayout.ToggleLeft("把 lightmapScaleOffset 烘进贴图（之后材质 ST 保持 1/0）", m_ApplyST);
            if (m_ApplyST)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "需要在【场景 Hierarchy】中选中『参与烘焙的物体』(lightmapIndex>=0)，\n" +
                    "生成时会自动取它的 lightmapScaleOffset；选副本/未烘焙物体会取到无效的单位值。",
                    MessageType.Warning);

                DrawSelectionStatus();

                m_AutoGrabST = EditorGUILayout.ToggleLeft(
                    "生成时自动从选中 Renderer 取 ST（推荐）", m_AutoGrabST);

                using (new EditorGUI.DisabledScope(m_AutoGrabST))
                {
                    m_Tiling = EditorGUILayout.Vector2Field("Tiling (scale)", m_Tiling);
                    m_Offset = EditorGUILayout.Vector2Field("Offset", m_Offset);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("立即从选中 Renderer 取"))
                            GrabFromSelection();
                        if (GUILayout.Button("重置 1/0", GUILayout.Width(80)))
                        { m_Tiling = Vector2.one; m_Offset = Vector2.zero; }
                    }
                }

                m_FillColor = EditorGUILayout.ColorField("越界填充色", m_FillColor);
                EditorGUI.indentLevel--;
            }

            // 4. 输出
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("4. 输出", EditorStyles.boldLabel);
            m_Output = (OutputFormat)EditorGUILayout.EnumPopup("输出格式", m_Output);
            if (m_Output == OutputFormat.PNG_LDR && m_Decode != DecodeMode.FULL_HDR)
                EditorGUILayout.HelpBox("解码出的 HDR(>1) 在 8-bit PNG 会被截断；保留高光请选 EXR_HDR。", MessageType.Warning);

            m_CustomSize = EditorGUILayout.ToggleLeft("自定义输出尺寸（默认同源图）", m_CustomSize);
            if (m_CustomSize)
            {
                EditorGUI.indentLevel++;
                m_OutW = EditorGUILayout.IntField("宽", m_OutW);
                m_OutH = EditorGUILayout.IntField("高", m_OutH);
                EditorGUI.indentLevel--;
            }
            m_Suffix = EditorGUILayout.TextField("文件名后缀", m_Suffix);

            // 执行
            EditorGUILayout.Space();
            bool needSel = m_ApplyST && m_AutoGrabST;
            bool selOK = !needSel || HasValidStSelection();
            if (m_Source == null)
                EditorGUILayout.LabelField("● 请先指定 Lightmap 源贴图", RedStyle);
            else if (!selOK)
                EditorGUILayout.LabelField("● 请在场景中选中『参与烘焙的物体(lightmapIndex>=0)』后再生成", RedStyle);

            using (new EditorGUI.DisabledScope(m_Source == null || !selOK))
            {
                if (GUILayout.Button("生成贴图", GUILayout.Height(34)))
                    Convert();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawSourceInfo()
        {
            if (m_Source == null) return;
            string path = AssetDatabase.GetAssetPath(m_Source);
            var imp = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            string srgb = imp == null ? "?" : imp.sRGBTexture.ToString();
            EditorGUILayout.LabelField(
                $"  {m_Source.width}x{m_Source.height}  格式={m_Source.format}  sRGB={srgb}",
                EditorStyles.miniLabel);
        }

        string DecodeFormulaHint()
        {
            switch (m_Decode)
            {
                case DecodeMode.RGBM:
                    return "  RGBM(Linear): rgb × pow(a,2.2) × 34.493242";
                case DecodeMode.DLDR:
                    return "  DLDR(Linear): rgb × 4.59";
                default:
                    return "  FULL_HDR: rgb（不解码）";
            }
        }

        GUIStyle m_RedStyle, m_GreenStyle;
        GUIStyle RedStyle => m_RedStyle ?? (m_RedStyle = new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = new Color(0.85f, 0.2f, 0.2f) }, wordWrap = true });
        GUIStyle GreenStyle => m_GreenStyle ?? (m_GreenStyle = new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = new Color(0.2f, 0.65f, 0.2f) }, wordWrap = true });

        /// <summary>显示当前 Hierarchy 选中物体是否适合取 lightmapScaleOffset（红=不可取，绿=可取）。</summary>
        void DrawSelectionStatus()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorGUILayout.LabelField("● 当前未选中场景物体", RedStyle);
                return;
            }
            var r = go.GetComponent<MeshRenderer>();
            if (r == null)
            {
                EditorGUILayout.LabelField($"● 选中「{go.name}」无 MeshRenderer", RedStyle);
                return;
            }
            if (r.lightmapIndex < 0)
            {
                EditorGUILayout.LabelField(
                    $"● 选中「{go.name}」lightmapIndex={r.lightmapIndex}（未参与烘焙，取值无效）", RedStyle);
                return;
            }
            EditorGUILayout.LabelField(
                $"√ 选中「{go.name}」idx={r.lightmapIndex}  SO={r.lightmapScaleOffset.ToString("F4")}", GreenStyle);
        }

        /// <summary>
        /// 尝试从当前选中的 Renderer 取 lightmapScaleOffset 写入 m_Tiling/m_Offset。
        /// 返回是否取到『有效』值（选中了 lightmapIndex>=0 的 MeshRenderer）。
        /// </summary>
        bool TryGrabST()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("[Lightmap转换] 未选中 GameObject，无法取 ST"); return false; }
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) { Debug.LogWarning($"[Lightmap转换] 选中「{go.name}」无 MeshRenderer，无法取 ST"); return false; }

            var so = r.lightmapScaleOffset;
            m_Tiling = new Vector2(so.x, so.y);
            m_Offset = new Vector2(so.z, so.w);
            Repaint();

            if (r.lightmapIndex < 0)
            {
                Debug.LogWarning($"[Lightmap转换] {go.name} 的 lightmapIndex={r.lightmapIndex}（未参与烘焙），" +
                    $"取到的 ST={so.ToString("F5")} 多半是单位值，烘焙后无变化。\n" +
                    "请选中『真正参与烘焙的原始物体』（lightmapIndex>=0）。");
                return false;
            }

            Debug.Log($"[Lightmap转换] 取自 {go.name}(lightmapIndex={r.lightmapIndex}): Tiling={m_Tiling.ToString("F5")} Offset={m_Offset.ToString("F5")}");
            return true;
        }

        void GrabFromSelection() => TryGrabST();

        /// <summary>当前是否选中了可取 ST 的有效 Renderer（MeshRenderer 且 lightmapIndex>=0）。</summary>
        static bool HasValidStSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null) return false;
            var r = go.GetComponent<MeshRenderer>();
            return r != null && r.lightmapIndex >= 0;
        }

        void Convert()
        {
            if (m_ApplyST && m_AutoGrabST && !TryGrabST())
            {
                EditorUtility.DisplayDialog("Lightmap 转换工具",
                    "已开启『生成时自动取 ST』，但当前选中物体取不到有效的 lightmapScaleOffset。\n\n" +
                    "请在场景 Hierarchy 中选中『参与烘焙的物体(lightmapIndex>=0)』后再生成，\n" +
                    "或关闭自动取值改为手动填写 Tiling/Offset。",
                    "好的");
                return;
            }

            string srcPath = AssetDatabase.GetAssetPath(m_Source);
            var importer = string.IsNullOrEmpty(srcPath) ? null : AssetImporter.GetAtPath(srcPath) as TextureImporter;
            bool srcSRGB = importer == null || importer.sRGBTexture;

            ReadSourcePixels(srcPath, importer, out Color[] src, out int sw, out int sh);

            var lin = new Color[src.Length];
            for (int i = 0; i < src.Length; i++)
                lin[i] = Decode(src[i], m_Decode, srcSRGB);

            Vector2 tiling = m_ApplyST ? m_Tiling : Vector2.one;
            Vector2 offset = m_ApplyST ? m_Offset : Vector2.zero;

            int ow = m_CustomSize && m_OutW > 0 ? m_OutW : sw;
            int oh = m_CustomSize && m_OutH > 0 ? m_OutH : sh;

            bool exr = m_Output == OutputFormat.EXR_HDR;
            Color fillOut = exr ? m_FillColor.linear : m_FillColor;

            var dst = new Color[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                float v = (y + 0.5f) / oh;
                for (int x = 0; x < ow; x++)
                {
                    float u = (x + 0.5f) / ow;
                    float su = u * tiling.x + offset.x;
                    float sv = v * tiling.y + offset.y;

                    if (m_ApplyST && (su < 0f || su > 1f || sv < 0f || sv > 1f))
                    {
                        dst[y * ow + x] = fillOut;
                        continue;
                    }
                    su = Mathf.Clamp01(su);
                    sv = Mathf.Clamp01(sv);

                    Color c = SampleBilinear(lin, sw, sh, su, sv);
                    if (exr)
                        dst[y * ow + x] = c;
                    else
                        dst[y * ow + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f).gamma;
                }
            }

            WriteOutput(srcPath, dst, ow, oh, exr);
        }

        void WriteOutput(string srcPath, Color[] dst, int ow, int oh, bool exr)
        {
            string ext = exr ? ".exr" : ".png";
            string dir, fileName;
            if (string.IsNullOrEmpty(srcPath))
            {
                dir = "Assets";
                fileName = m_Source.name + m_Suffix + ext;
            }
            else
            {
                dir = Path.GetDirectoryName(srcPath);
                fileName = Path.GetFileNameWithoutExtension(srcPath) + m_Suffix + ext;
            }
            string outPath = Path.Combine(dir, fileName).Replace('\\', '/');

            if (exr)
            {
                var tex = new Texture2D(ow, oh, TextureFormat.RGBAFloat, false, true); // linear
                tex.SetPixels(dst); tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
                DestroyImmediate(tex);
            }
            else
            {
                var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false, false); // sRGB 字节
                tex.SetPixels(dst); tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var outImporter = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (outImporter != null)
            {
                outImporter.textureType = TextureImporterType.Default;
                outImporter.sRGBTexture = !exr;          // EXR=线性数据；PNG=sRGB
                outImporter.mipmapEnabled = false;
                outImporter.wrapMode = TextureWrapMode.Clamp;
                outImporter.textureCompression = TextureImporterCompression.Uncompressed;
                outImporter.SaveAndReimport();
            }

            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
            string stInfo = m_ApplyST ? $"，ST(T={m_Tiling.ToString("F4")},O={m_Offset.ToString("F4")})" : "";
            Debug.Log($"[Lightmap转换] 已生成：{outPath}（{ow}x{oh}，{m_Decode}，{(exr ? "EXR-HDR" : "PNG-sRGB")}{stInfo}）");
        }

        /// <summary>
        /// 解码单像素为线性 HDR。rgb 取“采样器会给的值”：sRGB 源先转线性，非 sRGB(EXR) 用原值；alpha 不转。
        /// </summary>
        static Color Decode(Color c, DecodeMode mode, bool srcSRGB)
        {
            float r = c.r, g = c.g, b = c.b;
            if (srcSRGB) { Color l = c.linear; r = l.r; g = l.g; b = l.b; }

            float mul;
            switch (mode)
            {
                case DecodeMode.RGBM:
                {
                    float a = Mathf.Max(0f, c.a);
                    mul = Mathf.Pow(a, 2.2f) * 34.493242f;   // Linear 分支
                    break;
                }
                case DecodeMode.DLDR:
                    mul = 4.59f;                              // Linear 分支
                    break;
                default: // FULL_HDR
                    mul = 1.0f;
                    break;
            }
            return new Color(r * mul, g * mul, b * mul, 1f);
        }

        static void ReadSourcePixels(string srcPath, TextureImporter importer, out Color[] px, out int w, out int h)
        {
            if (importer == null)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
                w = t.width; h = t.height; px = t.GetPixels();
                return;
            }

            bool oReadable = importer.isReadable;
            bool oMip = importer.mipmapEnabled;
            var oComp = importer.textureCompression;
            var oNpot = importer.npotScale;
            int oMax = importer.maxTextureSize;
            try
            {
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 8192;
                importer.SaveAndReimport();

                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(srcPath);
                w = t.width; h = t.height; px = t.GetPixels();
            }
            finally
            {
                importer.isReadable = oReadable;
                importer.mipmapEnabled = oMip;
                importer.textureCompression = oComp;
                importer.npotScale = oNpot;
                importer.maxTextureSize = oMax;
                importer.SaveAndReimport();
            }
        }

        /// <summary>线性数据上的双线性插值（输入已是线性，不做色彩空间转换）。</summary>
        static Color SampleBilinear(Color[] px, int w, int h, float u, float v)
        {
            float fx = u * w - 0.5f;
            float fy = v * h - 0.5f;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            int x1 = Mathf.Clamp(x0 + 1, 0, w - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, h - 1);
            x0 = Mathf.Clamp(x0, 0, w - 1);
            y0 = Mathf.Clamp(y0, 0, h - 1);

            Color c00 = px[y0 * w + x0];
            Color c10 = px[y0 * w + x1];
            Color c01 = px[y1 * w + x0];
            Color c11 = px[y1 * w + x1];
            return Color.Lerp(Color.Lerp(c00, c10, tx), Color.Lerp(c01, c11, tx), ty);
        }
    }
}
