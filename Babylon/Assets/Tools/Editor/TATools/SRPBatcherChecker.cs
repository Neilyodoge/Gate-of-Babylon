using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace TATools
{
    /// <summary>
    /// SRP Batcher 兼容性检查工具
    /// 用于批量检查 Shader 是否兼容 SRP Batcher，并输出不兼容的原因
    /// </summary>
    public class SRPBatcherChecker : EditorWindow
    {
        private List<Shader> m_shaders = new List<Shader>();
        private Vector2 m_scrollPosition;
        private Vector2 m_resultScrollPosition;
        private List<CheckResult> m_results = new List<CheckResult>();
        private bool m_hasResults = false;

        // 缓存反射方法
        private static MethodInfo s_getSRPBatcherCompatibilityCode;
        private static bool s_reflectionInitialized = false;
        private static bool s_reflectionAvailable = false;

        private struct CheckResult
        {
            public Shader Shader;
            public bool IsCompatible;
            public string Message;
        }

        [MenuItem("nTools/TA工具/SRP Batcher Checker", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<SRPBatcherChecker>("SRP Batcher Checker");
            window.minSize = new Vector2(450, 350);
            window.Show();
        }

        private void OnEnable()
        {
            InitReflection();
        }

        /// <summary>
        /// 初始化反射，获取 Unity 内部的 SRP Batcher 兼容性检查方法
        /// </summary>
        private static void InitReflection()
        {
            if (s_reflectionInitialized)
                return;

            s_reflectionInitialized = true;

            try
            {
                // Unity 2019.2+ 提供了 ShaderUtil.GetSRPBatcherCompatibilityCode 内部方法
                var shaderUtilType = typeof(ShaderUtil);
                s_getSRPBatcherCompatibilityCode = shaderUtilType.GetMethod(
                    "GetSRPBatcherCompatibilityCode",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(Shader), typeof(int) },
                    null
                );

                s_reflectionAvailable = s_getSRPBatcherCompatibilityCode != null;

                if (!s_reflectionAvailable)
                {
                    Debug.LogWarning("[SRP Batcher Checker] 无法找到 ShaderUtil.GetSRPBatcherCompatibilityCode 方法，将使用备用检测方案。");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SRP Batcher Checker] 反射初始化失败: {e.Message}");
                s_reflectionAvailable = false;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawShaderList();
            DrawButtons();
            DrawResults();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("SRP Batcher 兼容性检查工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "将 Shader 拖拽到下方列表或点击 \"添加 Shader\" 按钮，然后点击 \"检查\" 按钮查看兼容性结果。",
                MessageType.Info);
            EditorGUILayout.Space(3);
        }

        private void DrawShaderList()
        {
            EditorGUILayout.LabelField("Shader 列表", EditorStyles.boldLabel);

            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.MaxHeight(200));

            for (int i = 0; i < m_shaders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                m_shaders[i] = (Shader)EditorGUILayout.ObjectField(
                    "Shader " + (i + 1), m_shaders[i], typeof(Shader), false);

                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    m_shaders.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 处理拖拽
            HandleDragAndDrop();
        }

        private void DrawButtons()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("添加 Shader", GUILayout.Height(25)))
            {
                m_shaders.Add(null);
            }

            if (GUILayout.Button("从选中对象添加", GUILayout.Height(25)))
            {
                AddShadersFromSelection();
            }

            if (GUILayout.Button("清空列表", GUILayout.Height(25)))
            {
                m_shaders.Clear();
                m_results.Clear();
                m_hasResults = false;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("检查 SRP Batcher 兼容性", GUILayout.Height(30)))
            {
                CheckShaders();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
        }

        private void DrawResults()
        {
            if (!m_hasResults)
                return;

            EditorGUILayout.LabelField("检查结果", EditorStyles.boldLabel);

            int compatibleCount = 0;
            int incompatibleCount = 0;
            foreach (var r in m_results)
            {
                if (r.IsCompatible) compatibleCount++;
                else incompatibleCount++;
            }

            EditorGUILayout.LabelField(
                "共 " + m_results.Count + " 个 Shader：兼容 " + compatibleCount + " 个，不兼容 " + incompatibleCount + " 个");

            EditorGUILayout.Space(3);

            m_resultScrollPosition = EditorGUILayout.BeginScrollView(m_resultScrollPosition);

            foreach (var result in m_results)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                // 状态图标
                var iconName = result.IsCompatible ? "d_greenLight" : "d_redLight";
                GUIContent icon = null;
                try { icon = EditorGUIUtility.IconContent(iconName); } catch { }

                if (icon != null)
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                else
                    GUILayout.Label(result.IsCompatible ? "[OK]" : "[X]", GUILayout.Width(30));

                // Shader 名称（可点击定位）
                if (result.Shader != null)
                {
                    if (GUILayout.Button(result.Shader.name, EditorStyles.linkLabel))
                    {
                        EditorGUIUtility.PingObject(result.Shader);
                        Selection.activeObject = result.Shader;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("(null)");
                }

                EditorGUILayout.EndHorizontal();

                // 详细信息
                if (!string.IsNullOrEmpty(result.Message))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedMiniLabel);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 处理拖拽 Shader 到窗口
        /// </summary>
        private void HandleDragAndDrop()
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragUpdated)
            {
                if (DragAndDrop.objectReferences.Length > 0)
                {
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Shader)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            break;
                        }
                    }
                }
            }

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is Shader shader)
                    {
                        if (!m_shaders.Contains(shader))
                            m_shaders.Add(shader);
                    }
                }

                Repaint();
            }
        }

        /// <summary>
        /// 从当前选中的对象中提取 Shader（支持选中 Shader 文件、Material、GameObject）
        /// </summary>
        private void AddShadersFromSelection()
        {
            HashSet<Shader> existingShaders = new HashSet<Shader>(m_shaders);

            foreach (var obj in Selection.objects)
            {
                Shader shader = null;

                if (obj is Shader s)
                {
                    shader = s;
                }
                else if (obj is Material mat)
                {
                    shader = mat.shader;
                }
                else if (obj is GameObject go)
                {
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        foreach (var sharedMat in renderer.sharedMaterials)
                        {
                            if (sharedMat != null && sharedMat.shader != null && !existingShaders.Contains(sharedMat.shader))
                            {
                                existingShaders.Add(sharedMat.shader);
                                m_shaders.Add(sharedMat.shader);
                            }
                        }
                    }
                    continue;
                }

                if (shader != null && !existingShaders.Contains(shader))
                {
                    existingShaders.Add(shader);
                    m_shaders.Add(shader);
                }
            }
        }

        /// <summary>
        /// 执行 SRP Batcher 兼容性检查
        /// </summary>
        private void CheckShaders()
        {
            m_results.Clear();
            m_hasResults = true;

            List<Shader> validShaders = m_shaders.FindAll(shader => shader != null);

            if (validShaders.Count == 0)
            {
                Debug.LogWarning("[SRP Batcher Checker] 没有有效的 Shader 可供检查。");
                return;
            }

            foreach (var shader in validShaders)
            {
                CheckResult result;

                if (s_reflectionAvailable)
                {
                    result = CheckShaderViaReflection(shader);
                }
                else
                {
                    result = CheckShaderFallback(shader);
                }

                m_results.Add(result);

                // 同时输出到 Console
                if (result.IsCompatible)
                {
                    Debug.Log("[SRP Batcher] <b>" + shader.name + "</b> - 兼容 SRP Batcher\n" + result.Message);
                }
                else
                {
                    Debug.LogWarning("[SRP Batcher] <b>" + shader.name + "</b> - 不兼容 SRP Batcher\n" + result.Message);
                }
            }

            Debug.Log("[SRP Batcher Checker] 检查完成，共 " + validShaders.Count + " 个 Shader。");
            Repaint();
        }

        /// <summary>
        /// 通过反射调用 Unity 内部 API 检查 SRP Batcher 兼容性
        /// </summary>
        private CheckResult CheckShaderViaReflection(Shader shader)
        {
            CheckResult result = new CheckResult { Shader = shader, IsCompatible = true };
            List<string> messages = new List<string>();

            int passCount = shader.passCount;
            bool anyIncompatible = false;

            for (int passIdx = 0; passIdx < passCount; passIdx++)
            {
                try
                {
                    // GetSRPBatcherCompatibilityCode 返回 int:
                    // 0 = 兼容
                    // 非0 = 不兼容（不同的错误码代表不同原因）
                    int code = (int)s_getSRPBatcherCompatibilityCode.Invoke(null, new object[] { shader, passIdx });

                    if (code != 0)
                    {
                        anyIncompatible = true;
                        string reason = GetIncompatibilityReason(code);
                        string passTag = "";
                        try { passTag = shader.FindPassTagValue(passIdx, new ShaderTagId("LightMode")).name; } catch { }
                        string passInfo = string.IsNullOrEmpty(passTag) ? ("Pass " + passIdx) : ("Pass " + passIdx + " (" + passTag + ")");
                        messages.Add("  " + passInfo + ": " + reason + " (code=" + code + ")");
                    }
                }
                catch (Exception e)
                {
                    messages.Add("  Pass " + passIdx + ": 检查异常 - " + e.Message);
                    anyIncompatible = true;
                }
            }

            result.IsCompatible = !anyIncompatible;

            if (anyIncompatible)
            {
                messages.Insert(0, "以下 Pass 不兼容 SRP Batcher:");
                result.Message = string.Join("\n", messages);
            }
            else
            {
                result.Message = "所有 " + passCount + " 个 Pass 均兼容 SRP Batcher。";
            }

            return result;
        }

        /// <summary>
        /// 将 SRP Batcher 不兼容错误码转换为可读的原因描述
        /// </summary>
        private static string GetIncompatibilityReason(int code)
        {
            // Unity 内部错误码定义（基于 Unity 源码）
            switch (code)
            {
                case 1:
                    return "Shader 不兼容 SRP（非 SRP Shader）";
                case 2:
                    return "存在不在 UnityPerMaterial CBUFFER 中的材质属性";
                case 3:
                    return "存在不在 UnityPerDraw CBUFFER 中的内置属性";
                case 4:
                    return "Shader 使用了不支持的特性（如 Instancing 变体冲突）";
                case 5:
                    return "Shader 包含不兼容的 Pass 类型";
                default:
                    return "未知的不兼容原因 (错误码: " + code + ")";
            }
        }

        /// <summary>
        /// 备用检测方案：当反射不可用时，通过基本信息提示用户
        /// </summary>
        private CheckResult CheckShaderFallback(Shader shader)
        {
            CheckResult result = new CheckResult { Shader = shader };

            try
            {
                // 基本检测：Shader 是否受支持
                result.IsCompatible = shader.isSupported;
                result.Message = "备用检测模式：无法通过内部 API 深度分析。\n" +
                                 "建议在 Shader Inspector 面板中查看 \"SRP Batcher\" 兼容性标记。\n" +
                                 "常见不兼容原因：材质属性未包裹在 CBUFFER_START(UnityPerMaterial) 中。";
            }
            catch (Exception e)
            {
                result.IsCompatible = false;
                result.Message = "备用检测失败: " + e.Message;
            }

            return result;
        }
    }
}
