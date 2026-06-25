using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorTools.LightmapTools
{
    /// <summary>
    /// Lightmap 转换工具：把烘焙 lightmap 源贴图（EXR/RGBM 等）转成可直接贴在模型上的普通贴图。
    ///
    /// 处理流程：
    ///   1) 读源像素 → 2) 按所选格式解码成线性 HDR 光照 → 3) 可选 ST 重映射(把 lightmapScaleOffset 烘进图)
    ///   → 4) 输出 HDR EXR 或 8-bit PNG
    ///
    /// 解码格式（公式与 URP EntityLighting.hlsl 一致）：
    ///   · RGBM     ：Linear→rgb×pow(a,2.2)×34.493242 ; Gamma→rgb×a×5.0   （用到 alpha 通道）
    ///   · DLDR     ：Linear→rgb×4.59                 ; Gamma→rgb×2.0
    ///   · FULL_HDR ：identity，直接用 rgb（不解码）
    /// </summary>
    public class LightmapConvertWindow : EditorWindow
    {
        public enum DecodeMode { RGBM, DLDR, FULL_HDR }
        public enum OutputFormat { EXR_HDR, PNG_LDR }

        Texture2D m_Source;

        // 解码（固定 Linear 分支，URP 工程标准）
        DecodeMode m_Decode = DecodeMode.RGBM;   // 默认 RGBM

        Vector2 m_Scroll;                        // 内容超出窗口时的竖向滚动

        // ST 重映射（可选）：把 renderer.lightmapScaleOffset 烘进贴图，之后材质 ST 保持 1/0
        bool m_ApplyST = false;
        bool m_AutoGrabST = true;   // 生成时自动从选中 Renderer 取 ST
        Vector2 m_Tiling = Vector2.one;
        Vector2 m_Offset = Vector2.zero;
        Color m_FillColor = Color.black;

        // 输出
        OutputFormat m_Output = OutputFormat.PNG_LDR;  // 默认 LDR PNG
        bool m_CustomSize = false;
        int m_OutW = 0, m_OutH = 0;
        string m_Suffix = "_conv";

        [MenuItem("Tools_3D/Lightmap 转换工具")]
        public static void Open()
        {
            var w = GetWindow<LightmapConvertWindow>("Lightmap 转换工具");
            w.minSize = new Vector2(360, 420);
        }

        void OnEnable()
        {
            if (m_Source == null && Selection.activeObject is Texture2D t) m_Source = t;
            Selection.selectionChanged += Repaint;   // 选中变化时刷新选中状态提示
        }

        void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.HelpBox(
                "把烘焙 lightmap 源贴图转成可贴在模型上的普通贴图。\n" +
                "流程：解码(RGBM/DLDR/FULL_HDR) → 可选 ST 重映射 → 输出 EXR/PNG。",
                MessageType.Info);

            // ── 1. 源贴图 ─────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1. 源贴图", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            m_Source = (Texture2D)EditorGUILayout.ObjectField("Lightmap 源", m_Source, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && m_Source != null && m_CustomSize == false)
            { /* 尺寸默认跟随源图，无需处理 */ }
            DrawSourceInfo();

            // ── 2. 解码 ───────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. 解码格式", EditorStyles.boldLabel);
            m_Decode = (DecodeMode)EditorGUILayout.EnumPopup("解码方式", m_Decode);
            EditorGUILayout.LabelField(" ", DecodeFormulaHint(), EditorStyles.miniLabel);

            // ── 3. ST 重映射（可选）─────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3. ST 重映射（可选）", EditorStyles.boldLabel);
            m_ApplyST = EditorGUILayout.ToggleLeft("把 lightmapScaleOffset 烘进贴图（之后材质 ST 保持 1/0）", m_ApplyST);
            if (m_ApplyST)
            {
                EditorGUI.indentLevel++;

                // 醒目提示：必须在场景中选中『参与烘焙的物体(lightmapIndex>=0)』才能取到正确的 ST
                EditorGUILayout.HelpBox(
                    "需要在【场景 Hierarchy】中选中『参与烘焙的物体』(lightmapIndex>=0)，\n" +
                    "生成时会自动取它的 lightmapScaleOffset；选副本/未烘焙物体会取到无效的单位值。",
                    MessageType.Warning);

                // 当前选中状态（无效时标红，有效时标绿）
                DrawSelectionStatus();

                m_AutoGrabST = EditorGUILayout.ToggleLeft(
                    "生成时自动从选中 Renderer 取 ST（推荐）", m_AutoGrabST);

                // 自动取值时手填的 Tiling/Offset 仅作展示，禁用编辑
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

            // ── 4. 输出 ───────────────────────────────────────────
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

            // ── 执行 ──────────────────────────────────────────────
            EditorGUILayout.Space();
            // 自动取 ST 时必须选中有效的烘焙 Renderer，否则禁用生成按钮
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

            // lightmapIndex<0 表示该物体没参与烘焙，scaleOffset 多半是单位值(1,1,0,0)，取了无意义。
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
            // 启用 ST + 勾选自动取值：生成前自动从选中 Renderer 取 lightmapScaleOffset。
            // 取到无效值（未选中/无 Renderer/未烘焙）则中止，避免生成错误结果。
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

            // 1) 解码成线性 HDR（之后全程线性空间处理）
            var lin = new Color[src.Length];
            for (int i = 0; i < src.Length; i++)
                lin[i] = Decode(src[i], m_Decode, srcSRGB);

            // 2) ST 重映射（线性空间双线性）；不启用则等价 Tiling=1/Offset=0 的直采
            Vector2 tiling = m_ApplyST ? m_Tiling : Vector2.one;
            Vector2 offset = m_ApplyST ? m_Offset : Vector2.zero;

            int ow = m_CustomSize && m_OutW > 0 ? m_OutW : sw;
            int oh = m_CustomSize && m_OutH > 0 ? m_OutH : sh;

            bool exr = m_Output == OutputFormat.EXR_HDR;
            // 越界填充色：EXR 存线性，PNG 存 sRGB
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

                    Color c = SampleBilinear(lin, sw, sh, su, sv);  // 线性 HDR
                    // 输出编码：EXR 存线性；PNG clamp 后转回 sRGB 字节
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
