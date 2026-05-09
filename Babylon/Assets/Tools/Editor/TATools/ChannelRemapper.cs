using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通道重映射工具 v3：上下两段式 — 上半段单张映射 + 实时预览，下半段批处理。
///
/// 设计思路：
///   · 上半段（单张测试）= 配规则 + 调试 + 看效果
///       - 拖入 A / B 贴图，每张配独立的"通道可视化"下拉（RGBA/RGB/R/G/B/A 灰度）
///       - 节点图编辑通道映射，第三个预览框实时显示输出
///       - "另存为" 单张产出 _Fix 贴图
///   · 下半段（批处理）= 用上半段定好的规则跑一批
///       - 4 种源：当前选中 / 文件夹 / Prefab+Material（提取它们的贴图）/ 手动列表
///       - 始终走"后缀配对"模式（A 名字 _M → 替换 _AO 找 B）
///       - 文件列表显示每张 A 的配对状态 ✓ / ⚠
///       - 一键批量执行
///   · 共享：节点图状态、输出格式与后缀、模板系统
///
/// 性能：
///   · 预览用 RenderTexture Blit + ReadPixels，不修改源贴图 isReadable
///   · 批处理用 Color32 字节级处理 + StartAssetEditing 包围批量切 isReadable
/// </summary>
public class ChannelRemapper : EditorWindow
{
    // ===== 输出后缀 =====
    private const string DefaultSuffix = "_Fix";

    // ===== 模板存储 =====
    private const string TemplatePrefsKey = "ChannelRemapper_Templates_v2";
    private const string LastTemplatePrefsKey = "ChannelRemapper_LastTemplate_v2";
    private const string DefaultTemplateName = "原始 (A.RGBA)";

    // ===== 节点图布局常量 =====
    private const float NodeCanvasHeight = 290f;
    private const float PinRadius = 7f;
    private const float PinHitRadius = 11f;

    // ===== 预览常量 =====
    private const int PreviewMaxSize = 192;

    // ===== 枚举 =====

    /// <summary>输入来源：A 贴图、B 贴图、常量 1、常量 0。</summary>
    private enum InputSource { A = 0, B = 1, White = 2, Black = 3 }

    /// <summary>通道字母（仅当 InputSource = A/B 时有效）。</summary>
    private enum ChannelLetter { R = 0, G = 1, B = 2, A = 3 }

    /// <summary>输出文件格式。</summary>
    private enum OutputFormat { KeepOriginal = 0, PNG = 1, TGA = 2, EXR = 3 }

    /// <summary>预览框的可视化模式。</summary>
    private enum ChannelView { RGBA = 0, RGB = 1, R = 2, G = 3, B = 4, Alpha = 5 }

    /// <summary>批处理源类型。</summary>
    private enum BatchSourceMode { Selection = 0, Folder = 1, PrefabMaterial = 2, Manual = 3 }

    // ===== 通道映射当前配置（共享） =====
    private InputSource outR_Input = InputSource.A;
    private ChannelLetter outR_Channel = ChannelLetter.R;
    private bool outR_Invert = false;

    private InputSource outG_Input = InputSource.A;
    private ChannelLetter outG_Channel = ChannelLetter.G;
    private bool outG_Invert = false;

    private InputSource outB_Input = InputSource.A;
    private ChannelLetter outB_Channel = ChannelLetter.B;
    private bool outB_Invert = false;

    private InputSource outA_Input = InputSource.A;
    private ChannelLetter outA_Channel = ChannelLetter.A;
    private bool outA_Invert = false;

    // ===== 上半段：单张测试 =====
    private Texture2D singleTexA;
    private Texture2D singleTexB;
    private bool singleEnableB = false;
    private ChannelView viewA = ChannelView.RGBA;
    private ChannelView viewB = ChannelView.RGBA;
    private ChannelView viewOut = ChannelView.RGBA;

    // 预览缓存（哈希驱动重生成）
    private Texture2D previewA;
    private Texture2D previewB;
    private Texture2D previewOut;
    private int lastPreviewHash;

    // ===== 下半段：批处理 =====
    private BatchSourceMode batchSourceMode = BatchSourceMode.Selection;
    private DefaultAsset batchFolder;
    private List<Object> batchPrefabsMaterials = new List<Object>();
    private List<Texture2D> batchManualTextures = new List<Texture2D>();
    private string suffixFilter = "";
    private string suffixSearch = "";
    private string suffixReplace = "";
    private List<string> collectedAPaths = new List<string>();
    private List<bool> collectedChecked = new List<bool>();
    private List<string> collectedBPaths = new List<string>();
    private Vector2 fileListScrollPos;
    private Vector2 dragSlotScrollPos;

    // ===== 输出配置（共享） =====
    private OutputFormat outputFormat = OutputFormat.KeepOriginal;
    private string outputSuffix = DefaultSuffix;

    // ===== 模板系统（共享） =====
    private string newTemplateName = "";
    private int selectedTemplateIndex = -1;
    private List<ChannelTemplate> templates = new List<ChannelTemplate>();
    private string[] templateDisplayNames = new string[0];

    // ===== 折叠状态 =====
    private bool foldSingle = true;
    private bool foldBatch = true;
    private bool foldTemplates = false;
    private bool foldOutput = false;

    // ===== 节点图交互状态 =====
    private bool isDraggingFromOutput = false;
    private bool isDraggingFromInput = false;
    private int draggingOutputIndex = -1;
    private int draggingInputIndex = -1;
    private Vector2 dragMousePos;

    /// <summary>通道映射模板：仅保存映射规则，不保存 B 贴图引用 / 后缀配对规则。</summary>
    [System.Serializable]
    private class ChannelTemplate
    {
        public string name;
        public int rInput, rChannel; public bool rInvert;
        public int gInput, gChannel; public bool gInvert;
        public int bInput, bChannel; public bool bInvert;
        public int aInput, aChannel; public bool aInvert;
    }

    [System.Serializable]
    private class TemplateList
    {
        public List<ChannelTemplate> items = new List<ChannelTemplate>();
    }

    [MenuItem("nTools/TA工具/通道重映射", false, 150)]
    public static void ShowWindow()
    {
        var win = GetWindow<ChannelRemapper>("通道重映射");
        win.minSize = new Vector2(520, 720);
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("通道重映射");
        LoadTemplatesFromPrefs();
        EnsureDefaultTemplate();

        string lastTemplate = EditorPrefs.GetString(LastTemplatePrefsKey, "");
        if (!string.IsNullOrEmpty(lastTemplate))
        {
            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i].name == lastTemplate)
                {
                    selectedTemplateIndex = i;
                    ApplyTemplate(templates[i]);
                    break;
                }
            }
        }

        RefreshBatchFileList();
    }

    private void OnDisable()
    {
        // 清理预览贴图（避免内存泄漏）
        DisposePreview(ref previewA);
        DisposePreview(ref previewB);
        DisposePreview(ref previewOut);
    }

    private void OnSelectionChange()
    {
        if (batchSourceMode == BatchSourceMode.Selection)
        {
            RefreshBatchFileList();
        Repaint();
        }
    }

    // ============================================================
    // ==========================  GUI  ===========================
    // ============================================================

    private void OnGUI()
    {
        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("通道重映射工具 v3", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "上半段【单张测试】= 拖 A/B 贴图调节点图 + 看实时预览。\n" +
            "下半段【批处理】= 用上面的规则在文件夹 / Prefab / 选中里跑批量。\n" +
            "PBR 通道参考：Metallic / Smoothness / AO / Highlight",
            MessageType.Info);

        GUILayout.Space(4);

        DrawSingleMappingSection();
        GUILayout.Space(2);
        DrawBatchSection();
        GUILayout.Space(2);
        DrawTemplateSection();
        GUILayout.Space(2);
        DrawOutputSection();

        // 上半段需要根据状态变化重生成预览
        RegeneratePreviewsIfNeeded();
    }

    // ============================================================
    // ==========================  上半段  ========================
    // ============================================================

    /// <summary>上半段：A/B 贴图槽 + 节点图 + 三个预览框 + "另存为" 按钮</summary>
    private void DrawSingleMappingSection()
    {
        foldSingle = EditorGUILayout.BeginFoldoutHeaderGroup(foldSingle, "① 单张测试 (Single Mapping)");
        if (foldSingle)
        {
        EditorGUILayout.BeginVertical("box");

            // ----- 三栏：A 槽 / B 槽 / 输出预览 -----
        EditorGUILayout.BeginHorizontal();
            DrawSinglePreviewSlot("A 主源", ref singleTexA, ref viewA, previewA, true);
            GUILayout.Space(6);
            DrawBSlot();
            GUILayout.Space(6);
            DrawOutputPreviewSlot();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ----- 节点图（通道映射） -----
            GUILayout.Label("通道映射 — 拖线绑定 / 右键菜单", EditorStyles.miniBoldLabel);
            DrawNodeGraph();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("还原默认 (A.RGBA)", EditorStyles.miniButton))
                ResetMappingToDefault();
            if (GUILayout.Button("RGB↔BGR", EditorStyles.miniButton))
            {
                outR_Input = InputSource.A; outR_Channel = ChannelLetter.B;
                outG_Input = InputSource.A; outG_Channel = ChannelLetter.G;
                outB_Input = InputSource.A; outB_Channel = ChannelLetter.R;
            }
            if (GUILayout.Button("反转全部", EditorStyles.miniButton))
        {
            outR_Invert = !outR_Invert;
            outG_Invert = !outG_Invert;
            outB_Invert = !outB_Invert;
            outA_Invert = !outA_Invert;
        }
        EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ----- 单张另存为 -----
            bool canSaveSingle = singleTexA != null;
            GUI.enabled = canSaveSingle;
            string outName = singleTexA != null
                ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(singleTexA)) + outputSuffix
                : "(请先拖入 A 贴图)";
            if (GUILayout.Button($"💾 另存为 {outName}", GUILayout.Height(28)))
            {
                ProcessSingleSave();
            }
            GUI.enabled = true;

            // 警告：用户绑了 B 但 B 没启用
            if (HasAnyBBinding() && (!singleEnableB || singleTexB == null))
            {
                EditorGUILayout.HelpBox(
                    "节点图里有连线指向 B，但当前未启用 B 或未拖入 B 贴图。\n" +
                    "预览中那些通道会显示为 0；批处理时会使用后缀配对找到的 B 贴图。",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>B 槽：包含启用 toggle</summary>
    private void DrawBSlot()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PreviewMaxSize + 12));

            EditorGUILayout.BeginHorizontal();
        GUILayout.Label("B 副源", EditorStyles.miniBoldLabel, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        bool newEnable = EditorGUILayout.ToggleLeft("启用", singleEnableB, GUILayout.Width(50));
        if (newEnable != singleEnableB)
        {
            singleEnableB = newEnable;
            lastPreviewHash = 0; // 强制重生成
        }
        EditorGUILayout.EndHorizontal();

        GUI.enabled = singleEnableB;
        // 预览图
        Rect previewRect = GUILayoutUtility.GetRect(PreviewMaxSize, PreviewMaxSize, GUILayout.Width(PreviewMaxSize), GUILayout.Height(PreviewMaxSize));
        DrawPreviewBox(previewRect, previewB, singleEnableB && singleTexB != null);

        // 拖入字段
        var newTex = EditorGUILayout.ObjectField(singleTexB, typeof(Texture2D), false, GUILayout.Width(PreviewMaxSize)) as Texture2D;
        if (newTex != singleTexB)
        {
            singleTexB = newTex;
            lastPreviewHash = 0;
        }

        // 通道视图选择
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(28));
        var newView = (ChannelView)EditorGUILayout.EnumPopup(viewB, GUILayout.Width(PreviewMaxSize - 30));
        if (newView != viewB)
        {
            viewB = newView;
            lastPreviewHash = 0;
        }
        EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
        EditorGUILayout.EndVertical();
    }

    /// <summary>输出预览槽（只读：贴图由当前节点图实时计算）</summary>
    private void DrawOutputPreviewSlot()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PreviewMaxSize + 12));

        GUILayout.Label("输出预览", EditorStyles.miniBoldLabel);

        Rect previewRect = GUILayoutUtility.GetRect(PreviewMaxSize, PreviewMaxSize, GUILayout.Width(PreviewMaxSize), GUILayout.Height(PreviewMaxSize));
        DrawPreviewBox(previewRect, previewOut, singleTexA != null);

        // 占位（与 A/B 槽对齐）
        GUILayout.Space(EditorGUIUtility.singleLineHeight + 4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(28));
        var newView = (ChannelView)EditorGUILayout.EnumPopup(viewOut, GUILayout.Width(PreviewMaxSize - 30));
        if (newView != viewOut)
        {
            viewOut = newView;
            lastPreviewHash = 0;
        }
            EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        }

    /// <summary>通用单张预览槽（A 槽用）</summary>
    private void DrawSinglePreviewSlot(string label, ref Texture2D tex, ref ChannelView view, Texture2D preview, bool drawDropArea)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(PreviewMaxSize + 12));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 预览图
        Rect previewRect = GUILayoutUtility.GetRect(PreviewMaxSize, PreviewMaxSize, GUILayout.Width(PreviewMaxSize), GUILayout.Height(PreviewMaxSize));
        DrawPreviewBox(previewRect, preview, tex != null);

        // 拖入字段
        var newTex = EditorGUILayout.ObjectField(tex, typeof(Texture2D), false, GUILayout.Width(PreviewMaxSize)) as Texture2D;
        if (newTex != tex)
        {
            tex = newTex;
            lastPreviewHash = 0;
        }

        // 通道视图选择
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(28));
        var newView = (ChannelView)EditorGUILayout.EnumPopup(view, GUILayout.Width(PreviewMaxSize - 30));
        if (newView != view)
        {
            view = newView;
            lastPreviewHash = 0;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>预览框：带边框 + 棋盘背景 + 居中绘制贴图</summary>
    private static void DrawPreviewBox(Rect rect, Texture2D preview, bool hasContent)
    {
        // 背景棋盘格
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.16f));
        DrawCheckerboard(rect, 16);

        if (hasContent && preview != null)
        {
            float w = preview.width;
            float h = preview.height;
            float scale = Mathf.Min(rect.width / w, rect.height / h);
            float dw = w * scale;
            float dh = h * scale;
            Rect drawRect = new Rect(rect.x + (rect.width - dw) * 0.5f,
                                     rect.y + (rect.height - dh) * 0.5f, dw, dh);
            GUI.DrawTexture(drawRect, preview, ScaleMode.StretchToFill, true);
        }
        else
        {
            var hintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            GUI.Label(rect, hasContent ? "(预览生成中...)" : "(无)", hintStyle);
        }

        // 边框
        var borderColor = new Color(0f, 0f, 0f, 0.6f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), borderColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), borderColor);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), borderColor);
    }

    private static void DrawCheckerboard(Rect r, int cellSize)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color c1 = new Color(0.22f, 0.22f, 0.23f);
        Color c2 = new Color(0.16f, 0.16f, 0.17f);
        for (int y = 0; y < r.height; y += cellSize)
        {
            for (int x = 0; x < r.width; x += cellSize)
            {
                bool dark = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                EditorGUI.DrawRect(
                    new Rect(r.x + x, r.y + y,
                             Mathf.Min(cellSize, r.width - x),
                             Mathf.Min(cellSize, r.height - y)),
                    dark ? c1 : c2);
            }
        }
    }

    // ============================================================
    // ====================  预览生成  ============================
    // ============================================================

    private void RegeneratePreviewsIfNeeded()
    {
        int hash = ComputePreviewHash();
        if (hash == lastPreviewHash) return;
        lastPreviewHash = hash;

        GeneratePreview(singleTexA, viewA, ref previewA, isOutputPreview: false);
        GeneratePreview(singleEnableB ? singleTexB : null, viewB, ref previewB, isOutputPreview: false);
        GenerateOutputPreview();

        Repaint();
    }

    private int ComputePreviewHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (singleTexA != null ? singleTexA.GetInstanceID() : 0);
            h = h * 31 + (singleTexB != null ? singleTexB.GetInstanceID() : 0);
            h = h * 31 + (singleEnableB ? 1 : 0);
            h = h * 31 + (int)viewA;
            h = h * 31 + (int)viewB;
            h = h * 31 + (int)viewOut;
            h = h * 31 + (int)outR_Input * 13 + (int)outR_Channel * 7 + (outR_Invert ? 1 : 0);
            h = h * 31 + (int)outG_Input * 13 + (int)outG_Channel * 7 + (outG_Invert ? 1 : 0);
            h = h * 31 + (int)outB_Input * 13 + (int)outB_Channel * 7 + (outB_Invert ? 1 : 0);
            h = h * 31 + (int)outA_Input * 13 + (int)outA_Channel * 7 + (outA_Invert ? 1 : 0);
            return h;
        }
    }

    /// <summary>生成单张预览（输入 → ChannelView 灰度化处理）</summary>
    private void GeneratePreview(Texture2D src, ChannelView view, ref Texture2D dst, bool isOutputPreview)
    {
        DisposePreview(ref dst);
        if (src == null) return;

        var (w, h) = ComputePreviewSize(src);
        Color32[] srcPixels = BlitReadPixels(src, w, h);
        Color32[] displayPixels = ApplyChannelView(srcPixels, view);

        dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.filterMode = FilterMode.Bilinear;
        dst.wrapMode = TextureWrapMode.Clamp;
        dst.hideFlags = HideFlags.HideAndDontSave;
        dst.SetPixels32(displayPixels);
        dst.Apply();
    }

    /// <summary>生成输出预览（按当前通道映射规则计算 A+B → 输出，再做 ChannelView 灰度化）</summary>
    private void GenerateOutputPreview()
    {
        DisposePreview(ref previewOut);
        if (singleTexA == null) return;

        var (w, h) = ComputePreviewSize(singleTexA);
        Color32[] aPixels = BlitReadPixels(singleTexA, w, h);

        Color32[] bPixels = null;
        if (singleEnableB && singleTexB != null)
        {
            bPixels = BlitReadPixels(singleTexB, w, h);
        }

        var outPixels = new Color32[aPixels.Length];
        for (int i = 0; i < aPixels.Length; i++)
        {
            Color32 a = aPixels[i];
            Color32 b = bPixels != null ? bPixels[i] : default(Color32);
            outPixels[i] = new Color32(
                SampleByte(a, b, outR_Input, outR_Channel, outR_Invert),
                SampleByte(a, b, outG_Input, outG_Channel, outG_Invert),
                SampleByte(a, b, outB_Input, outB_Channel, outB_Invert),
                SampleByte(a, b, outA_Input, outA_Channel, outA_Invert));
        }

        Color32[] displayPixels = ApplyChannelView(outPixels, viewOut);

        previewOut = new Texture2D(w, h, TextureFormat.RGBA32, false);
        previewOut.filterMode = FilterMode.Bilinear;
        previewOut.wrapMode = TextureWrapMode.Clamp;
        previewOut.hideFlags = HideFlags.HideAndDontSave;
        previewOut.SetPixels32(displayPixels);
        previewOut.Apply();
    }

    private static (int w, int h) ComputePreviewSize(Texture2D src)
    {
        int srcW = src.width;
        int srcH = src.height;
        if (srcW <= PreviewMaxSize && srcH <= PreviewMaxSize) return (srcW, srcH);

        if (srcW >= srcH)
        {
            int w = PreviewMaxSize;
            int h = Mathf.Max(1, Mathf.RoundToInt((float)srcH / srcW * PreviewMaxSize));
            return (w, h);
        }
        else
        {
            int h = PreviewMaxSize;
            int w = Mathf.Max(1, Mathf.RoundToInt((float)srcW / srcH * PreviewMaxSize));
            return (w, h);
        }
    }

    /// <summary>用 RT 拷贝 + ReadPixels 取像素，不修改源贴图 isReadable</summary>
    private static Color32[] BlitReadPixels(Texture src, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var prevRT = RenderTexture.active;
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;

        var temp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        temp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        temp.Apply();

        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        var pixels = temp.GetPixels32();
        Object.DestroyImmediate(temp);
        return pixels;
    }

    /// <summary>对像素数组应用通道可视化（RGB 显示 / 单通道灰度化等）</summary>
    private static Color32[] ApplyChannelView(Color32[] src, ChannelView view)
    {
        if (view == ChannelView.RGBA) return src;

        var dst = new Color32[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            Color32 c = src[i];
            switch (view)
            {
                case ChannelView.RGB:
                    dst[i] = new Color32(c.r, c.g, c.b, 255);
                    break;
                case ChannelView.R:
                    dst[i] = new Color32(c.r, c.r, c.r, 255);
                    break;
                case ChannelView.G:
                    dst[i] = new Color32(c.g, c.g, c.g, 255);
                    break;
                case ChannelView.B:
                    dst[i] = new Color32(c.b, c.b, c.b, 255);
                    break;
                case ChannelView.Alpha:
                    dst[i] = new Color32(c.a, c.a, c.a, 255);
                    break;
                default:
                    dst[i] = c;
                    break;
            }
        }
        return dst;
    }

    private static void DisposePreview(ref Texture2D tex)
    {
        if (tex != null)
        {
            Object.DestroyImmediate(tex);
            tex = null;
        }
    }

    /// <summary>是否有任意输出通道绑了 B</summary>
    private bool HasAnyBBinding()
    {
        return outR_Input == InputSource.B || outG_Input == InputSource.B
            || outB_Input == InputSource.B || outA_Input == InputSource.B;
    }

    // ============================================================
    // ==========================  下半段  ========================
    // ============================================================

    /// <summary>下半段：批处理</summary>
    private void DrawBatchSection()
    {
        int checkedCount = collectedChecked.Count(c => c);
        int validPairs = CountValidPairs();
        bool needsB = HasAnyBBinding();

        string headerLabel = $"② 批处理 (Batch Process)  [{checkedCount}/{collectedAPaths.Count}]";
        if (needsB) headerLabel += $"  配对 {validPairs}/{collectedAPaths.Count}";

        foldBatch = EditorGUILayout.BeginFoldoutHeaderGroup(foldBatch, headerLabel);
        if (foldBatch)
        {
        EditorGUILayout.BeginVertical("box");

            // ----- 源选择 toolbar -----
            GUILayout.Label("扫描源", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            int newMode = GUILayout.Toolbar((int)batchSourceMode,
                new[] { "当前选中", "文件夹", "Prefab/材质", "手动列表" },
                EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                batchSourceMode = (BatchSourceMode)newMode;
                RefreshBatchFileList();
            }

            DrawBatchSourceConfig();

            GUILayout.Space(4);

            // ----- A 后缀过滤 -----
        EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("A 后缀筛选", GUILayout.Width(80));
        string newFilter = EditorGUILayout.TextField(suffixFilter);
        if (newFilter != suffixFilter)
        {
            suffixFilter = newFilter;
                RefreshBatchFileList();
            }
            if (GUILayout.Button("刷新", GUILayout.Width(50))) RefreshBatchFileList();
            EditorGUILayout.EndHorizontal();

            // ----- 后缀配对规则（仅在节点图绑了 B 时显示） -----
            if (needsB)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("B 后缀配对", GUILayout.Width(80));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("A 中", GUILayout.Width(28));
                suffixSearch = EditorGUILayout.TextField(suffixSearch, GUILayout.Width(80));
                EditorGUILayout.LabelField("→", GUILayout.Width(16));
                suffixReplace = EditorGUILayout.TextField(suffixReplace, GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck()) RefreshBatchFileList();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
                    "示例：A 中 [_M] → [_AO]，则 body_M.png 配对 body_AO.png",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "节点图当前没有绑定 B，无需配对规则。直接对每张 A 做单源映射。",
                    MessageType.None);
            }

        GUILayout.Space(4);

            // ----- 文件列表 -----
            DrawFileList(needsB, validPairs);

            GUILayout.Space(6);

            // ----- 批量执行按钮 -----
            bool canRun = checkedCount > 0
                          && (!needsB || validPairs > 0);
            GUI.enabled = canRun;
            string btnLabel = needsB
                ? $"▶ 批量重映射并保存 ({Mathf.Min(checkedCount, validPairs)} 对)"
                : $"▶ 批量重映射并保存 ({checkedCount} 张)";
            if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
                ProcessBatch();
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>根据当前源模式绘制对应的源配置</summary>
    private void DrawBatchSourceConfig()
    {
        switch (batchSourceMode)
        {
            case BatchSourceMode.Selection:
                EditorGUILayout.HelpBox(
                    "在 Project 中选中贴图或文件夹（可多选），切换选择会自动刷新列表。",
                    MessageType.None);
                break;

            case BatchSourceMode.Folder:
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("文件夹", GUILayout.Width(60));
                EditorGUI.BeginChangeCheck();
                batchFolder = EditorGUILayout.ObjectField(batchFolder, typeof(DefaultAsset), false) as DefaultAsset;
                if (EditorGUI.EndChangeCheck()) RefreshBatchFileList();
                EditorGUILayout.EndHorizontal();
                break;

            case BatchSourceMode.PrefabMaterial:
                DrawObjectListSlot(
                    "拖入 Prefab / Material（可多个）",
                    batchPrefabsMaterials,
                    typeof(Object),
                    o => o is GameObject || o is Material);
                break;

            case BatchSourceMode.Manual:
                DrawTextureListSlot(
                    "拖入需要处理的贴图（可多张）",
                    batchManualTextures);
                break;
        }
    }

    /// <summary>通用拖入列表槽（用于 Prefab/Material）</summary>
    private void DrawObjectListSlot(string hint, List<Object> list, System.Type targetType, System.Func<Object, bool> filter)
    {
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
        if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            list.Clear();
            RefreshBatchFileList();
        }
        EditorGUILayout.EndHorizontal();

        // 拖拽区
        Rect dropRect = GUILayoutUtility.GetRect(0, 50f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(dropRect, new Color(0.20f, 0.20f, 0.22f));
        var dropStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(dropRect, $"拖拽到此处添加 ({list.Count} 项)", dropStyle);
        HandleDropOnRect(dropRect, dropped =>
        {
            int added = 0;
            foreach (var o in dropped)
            {
                if (o == null || !filter(o)) continue;
                if (list.Contains(o)) continue;
                list.Add(o);
                added++;
            }
            if (added > 0) RefreshBatchFileList();
        });

        // 已添加列表
        if (list.Count > 0)
        {
            dragSlotScrollPos = EditorGUILayout.BeginScrollView(dragSlotScrollPos, GUILayout.MaxHeight(80));
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(list[i], targetType, false);
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    list.RemoveAt(i);
                    RefreshBatchFileList();
                EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>专门处理 Texture2D 列表的拖入槽</summary>
    private void DrawTextureListSlot(string hint, List<Texture2D> list)
    {
        EditorGUILayout.BeginVertical("helpbox");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);
        if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            list.Clear();
            RefreshBatchFileList();
        }
        EditorGUILayout.EndHorizontal();

        Rect dropRect = GUILayoutUtility.GetRect(0, 50f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(dropRect, new Color(0.20f, 0.20f, 0.22f));
        var dropStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
        GUI.Label(dropRect, $"拖拽贴图到此处添加 ({list.Count} 张)", dropStyle);
        HandleDropOnRect(dropRect, dropped =>
        {
            int added = 0;
            foreach (var o in dropped)
            {
                if (o is Texture2D tex && !list.Contains(tex))
                {
                    list.Add(tex);
                    added++;
                }
            }
            if (added > 0) RefreshBatchFileList();
        });

        if (list.Count > 0)
        {
            dragSlotScrollPos = EditorGUILayout.BeginScrollView(dragSlotScrollPos, GUILayout.MaxHeight(80));
            for (int i = 0; i < list.Count; i++)
    {
        EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(list[i], typeof(Texture2D), false);
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24)))
                {
                    list.RemoveAt(i);
                    RefreshBatchFileList();
        EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private static void HandleDropOnRect(Rect rect, System.Action<Object[]> onDrop)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;
        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            onDrop(DragAndDrop.objectReferences);
            e.Use();
        }
    }

    /// <summary>文件列表（带配对状态）</summary>
    private void DrawFileList(bool needsB, int validPairs)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("文件列表", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("全选", EditorStyles.miniButton, GUILayout.Width(40)))
            for (int i = 0; i < collectedChecked.Count; i++) collectedChecked[i] = true;
        if (GUILayout.Button("全不选", EditorStyles.miniButton, GUILayout.Width(50)))
            for (int i = 0; i < collectedChecked.Count; i++) collectedChecked[i] = false;
        if (needsB && validPairs < collectedAPaths.Count)
        {
            if (GUILayout.Button("仅勾选已配对", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                for (int i = 0; i < collectedChecked.Count; i++)
                    collectedChecked[i] = !string.IsNullOrEmpty(collectedBPaths[i]);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (collectedAPaths.Count == 0)
        {
            EditorGUILayout.LabelField("未找到匹配的贴图文件", EditorStyles.miniLabel);
                }
                else
                {
            fileListScrollPos = EditorGUILayout.BeginScrollView(fileListScrollPos, GUILayout.MaxHeight(180));
            for (int i = 0; i < collectedAPaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                collectedChecked[i] = EditorGUILayout.Toggle(collectedChecked[i], GUILayout.Width(16));

                string aName = Path.GetFileName(collectedAPaths[i]);
                if (needsB)
                {
                    string bPath = collectedBPaths[i];
                    if (!string.IsNullOrEmpty(bPath))
                    {
                        var okStyle = new GUIStyle(EditorStyles.label) { richText = true };
                        GUILayout.Label(
                            $"<color=#88ccff>✓</color> {aName} ↔ {Path.GetFileName(bPath)}",
                            okStyle);
                    }
                    else
                    {
                        var warnStyle = new GUIStyle(EditorStyles.label)
                        {
                            richText = true,
                            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
                        };
                        GUILayout.Label(
                            $"<color=#ffaa44>⚠</color> {aName}  (未匹配 B)",
                            warnStyle);
                    }
                }
                else
                {
                    GUILayout.Label(aName);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    // ============================================================
    // ====================  批处理源扫描  ========================
    // ============================================================

    private void RefreshBatchFileList()
    {
        var paths = new HashSet<string>();

        switch (batchSourceMode)
        {
            case BatchSourceMode.Selection:
                CollectFromSelection(paths);
                break;
            case BatchSourceMode.Folder:
                if (batchFolder != null)
                {
                    string folderPath = AssetDatabase.GetAssetPath(batchFolder);
                    if (AssetDatabase.IsValidFolder(folderPath))
                        CollectTexturesInFolder(folderPath, paths);
                }
                break;
            case BatchSourceMode.PrefabMaterial:
                foreach (var obj in batchPrefabsMaterials)
                {
                    if (obj is GameObject go) ExtractTexturePathsFromGameObject(go, paths);
                    else if (obj is Material mat) ExtractTexturePathsFromMaterial(mat, paths);
                }
                break;
            case BatchSourceMode.Manual:
                foreach (var tex in batchManualTextures)
                {
                    if (tex == null) continue;
                    string p = AssetDatabase.GetAssetPath(tex);
                    if (!string.IsNullOrEmpty(p)) paths.Add(p);
                }
                break;
        }

        // A 后缀筛选
        var filtered = new List<string>();
        string filter = suffixFilter.Trim();
        foreach (var p in paths)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(p);
                if (!nameNoExt.Contains(filter)) continue;
            }
            // 自动剔除自己生成的 _Fix 文件
            if (Path.GetFileNameWithoutExtension(p).EndsWith(outputSuffix)) continue;

            filtered.Add(p);
        }
        filtered.Sort();

        // 保留旧勾选
        var oldCheckedMap = new Dictionary<string, bool>();
        for (int i = 0; i < collectedAPaths.Count; i++)
            oldCheckedMap[collectedAPaths[i]] = collectedChecked[i];

        collectedAPaths = filtered;
        collectedChecked = new List<bool>(filtered.Count);
        collectedBPaths = new List<string>(filtered.Count);

        for (int i = 0; i < filtered.Count; i++)
        {
            collectedChecked.Add(oldCheckedMap.TryGetValue(filtered[i], out bool wc) ? wc : true);
            collectedBPaths.Add(ResolveBPath(filtered[i]));
        }
    }

    private void CollectFromSelection(HashSet<string> paths)
    {
        Object[] selected = Selection.objects;
        if (selected == null) return;
        foreach (var obj in selected)
        {
            string p = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p)) CollectTexturesInFolder(p, paths);
            else if (AssetDatabase.LoadAssetAtPath<Texture2D>(p) != null) paths.Add(p);
        }
    }

    private static void CollectTexturesInFolder(string folderPath, HashSet<string> results)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            results.Add(path);
        }
    }

    /// <summary>从 Prefab 提取所有 Renderer 上 Material 用到的贴图路径</summary>
    private static void ExtractTexturePathsFromGameObject(GameObject go, HashSet<string> results)
    {
        if (go == null) return;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                ExtractTexturePathsFromMaterial(mat, results);
            }
        }
    }

    /// <summary>从 Material 提取所有 Texture2D 属性的路径</summary>
    private static void ExtractTexturePathsFromMaterial(Material mat, HashSet<string> results)
    {
        if (mat == null || mat.shader == null) return;
        int count = mat.shader.GetPropertyCount();
        for (int i = 0; i < count; i++)
        {
            if (mat.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
            string propName = mat.shader.GetPropertyName(i);
            var tex = mat.GetTexture(propName);
            if (tex is Texture2D tex2d)
            {
                string p = AssetDatabase.GetAssetPath(tex2d);
                if (!string.IsNullOrEmpty(p)) results.Add(p);
            }
        }
    }

    /// <summary>给定 A 路径，按后缀配对规则找出对应 B 路径</summary>
    private string ResolveBPath(string aPath)
    {
        if (!HasAnyBBinding()) return string.Empty;
        if (string.IsNullOrEmpty(suffixSearch)) return string.Empty;

        string aName = Path.GetFileNameWithoutExtension(aPath);
        if (!aName.Contains(suffixSearch)) return string.Empty;

        string bName = aName.Replace(suffixSearch, suffixReplace ?? string.Empty);
        string dir = Path.GetDirectoryName(aPath).Replace("\\", "/");

        string[] extCandidates = { ".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".exr", ".psd" };
        foreach (var ext in extCandidates)
        {
            string candidate = $"{dir}/{bName}{ext}";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(candidate) != null) return candidate;
        }
        return string.Empty;
    }

    private int CountValidPairs()
    {
        if (!HasAnyBBinding()) return collectedAPaths.Count;
        int count = 0;
        for (int i = 0; i < collectedAPaths.Count; i++)
            if (!string.IsNullOrEmpty(collectedBPaths[i])) count++;
        return count;
    }

    // ============================================================
    // ====================  执行：单张 / 批量  ===================
    // ============================================================

    /// <summary>上半段"另存为"按钮：保存单张 A → A_Fix（如启用 B 则同时使用 B）</summary>
    private void ProcessSingleSave()
    {
        if (singleTexA == null) return;
        string aPath = AssetDatabase.GetAssetPath(singleTexA);
        if (string.IsNullOrEmpty(aPath))
        {
            Debug.LogError("[通道重映射] A 贴图必须是项目资产，不能是临时贴图。");
            return;
        }
        string bPath = (singleEnableB && singleTexB != null) ? AssetDatabase.GetAssetPath(singleTexB) : string.Empty;

        var pathsToToggle = new HashSet<string> { aPath };
        if (!string.IsNullOrEmpty(bPath)) pathsToToggle.Add(bPath);
        var backup = SetReadable(pathsToToggle, true);
        try
        {
            RemapTextureCore(aPath, bPath);
        }
        finally
        {
            RestoreReadable(backup);
        }
        AssetDatabase.Refresh();
    }

    /// <summary>下半段批量执行</summary>
    private void ProcessBatch()
    {
        var jobs = new List<(string a, string b)>();
        bool needsB = HasAnyBBinding();

        for (int i = 0; i < collectedAPaths.Count; i++)
        {
            if (!collectedChecked[i]) continue;
            string aPath = collectedAPaths[i];
            string bPath = collectedBPaths[i];

            if (needsB && string.IsNullOrEmpty(bPath))
            {
                Debug.LogWarning($"[通道重映射] 跳过 {Path.GetFileName(aPath)}：未找到 B 贴图。");
                continue;
            }
            jobs.Add((aPath, bPath));
        }

        if (jobs.Count == 0)
        {
            EditorUtility.DisplayDialog("通道重映射", "没有可处理的贴图。", "确定");
            return;
        }

        var pathsToToggle = new HashSet<string>();
        foreach (var (a, b) in jobs)
        {
            pathsToToggle.Add(a);
            if (!string.IsNullOrEmpty(b)) pathsToToggle.Add(b);
        }

        var backup = SetReadable(pathsToToggle, true);

        int processedCount = 0;
        try
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                var (aPath, bPath) = jobs[i];
                EditorUtility.DisplayProgressBar(
                    "通道重映射",
                    $"({i + 1}/{jobs.Count}) {Path.GetFileName(aPath)}",
                    (float)i / jobs.Count);

                if (RemapTextureCore(aPath, bPath))
                    processedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            RestoreReadable(backup);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[通道重映射] 完成，共处理 {processedCount}/{jobs.Count} 张贴图。");
    }

    /// <summary>单张处理核心：读取 A (+B) 像素，应用映射，写盘</summary>
    private bool RemapTextureCore(string aPath, string bPath)
    {
        Texture2D texA = AssetDatabase.LoadAssetAtPath<Texture2D>(aPath);
        if (texA == null) { Debug.LogError($"[通道重映射] 无法加载 A: {aPath}"); return false; }

        Texture2D texB = null;
        if (!string.IsNullOrEmpty(bPath))
        {
            texB = AssetDatabase.LoadAssetAtPath<Texture2D>(bPath);
            if (texB == null) { Debug.LogError($"[通道重映射] 无法加载 B: {bPath}"); return false; }
        }

        int w = texA.width;
        int h = texA.height;
        Color32[] pixA = texA.GetPixels32();

        Color32[] pixB = null;
        if (texB != null)
        {
            if (texB.width == w && texB.height == h)
            {
                pixB = texB.GetPixels32();
            }
            else
            {
                Debug.LogWarning(
                    $"[通道重映射] B 尺寸 ({texB.width}×{texB.height}) 与 A ({w}×{h}) 不一致，使用最近邻缩放：{Path.GetFileName(bPath)}");
                pixB = ResizeNearest(texB.GetPixels32(), texB.width, texB.height, w, h);
            }
        }

        Color32[] outPixels = new Color32[w * h];
        for (int i = 0; i < pixA.Length; i++)
        {
            Color32 a = pixA[i];
            Color32 b = pixB != null ? pixB[i] : default(Color32);
            outPixels[i] = new Color32(
                SampleByte(a, b, outR_Input, outR_Channel, outR_Invert),
                SampleByte(a, b, outG_Input, outG_Channel, outG_Invert),
                SampleByte(a, b, outB_Input, outB_Channel, outB_Invert),
                SampleByte(a, b, outA_Input, outA_Channel, outA_Invert));
        }

        Texture2D outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        outTex.SetPixels32(outPixels);
        outTex.Apply();

        string suffix = string.IsNullOrEmpty(outputSuffix) ? DefaultSuffix : outputSuffix;
        string srcExt = Path.GetExtension(aPath).ToLowerInvariant();
        string outExt = ResolveOutputExtension(srcExt);

        byte[] bytes = outExt switch
        {
            ".tga" => outTex.EncodeToTGA(),
            ".exr" => outTex.EncodeToEXR(),
            _      => outTex.EncodeToPNG(),
        };

        string fullPath = Path.Combine(
            Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length),
            aPath);
        string directory = Path.GetDirectoryName(fullPath);
        string filename = Path.GetFileNameWithoutExtension(fullPath);
        string outFullPath = Path.Combine(directory, filename + suffix + outExt);
        File.WriteAllBytes(outFullPath, bytes);

        string outAssetPath = Path.GetDirectoryName(aPath).Replace("\\", "/")
                              + "/" + filename + suffix + outExt;
        AssetDatabase.ImportAsset(outAssetPath);

        Object.DestroyImmediate(outTex);

        string mappingDesc =
            $"R←{DescribeSource(outR_Input, outR_Channel, outR_Invert)} " +
            $"G←{DescribeSource(outG_Input, outG_Channel, outG_Invert)} " +
            $"B←{DescribeSource(outB_Input, outB_Channel, outB_Invert)} " +
            $"A←{DescribeSource(outA_Input, outA_Channel, outA_Invert)}";

        if (texB != null)
            Debug.Log($"[通道重映射] {Path.GetFileName(aPath)} + {Path.GetFileName(bPath)} → {Path.GetFileName(outAssetPath)}  | {mappingDesc}");
        else
            Debug.Log($"[通道重映射] {Path.GetFileName(aPath)} → {Path.GetFileName(outAssetPath)}  | {mappingDesc}");

        return true;
    }

    /// <summary>批量切 isReadable 为指定值；返回需要恢复的备份</summary>
    private static Dictionary<string, bool> SetReadable(HashSet<string> paths, bool target)
    {
        var backup = new Dictionary<string, bool>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var path in paths)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null && imp.isReadable != target)
                {
                    backup[path] = imp.isReadable;
                    imp.isReadable = target;
                    imp.SaveAndReimport();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        return backup;
    }

    private static void RestoreReadable(Dictionary<string, bool> backup)
    {
        if (backup == null || backup.Count == 0) return;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var kv in backup)
            {
                var imp = AssetImporter.GetAtPath(kv.Key) as TextureImporter;
                if (imp != null)
                {
                    imp.isReadable = kv.Value;
                    imp.SaveAndReimport();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private string ResolveOutputExtension(string srcExt)
    {
        switch (outputFormat)
        {
            case OutputFormat.PNG: return ".png";
            case OutputFormat.TGA: return ".tga";
            case OutputFormat.EXR: return ".exr";
            case OutputFormat.KeepOriginal:
            default:
                if (srcExt == ".tga" || srcExt == ".exr" || srcExt == ".png") return srcExt;
                return ".png";
        }
    }

    // ============================================================
    // ==========================  节点图  ========================
    // ============================================================

    private struct PinInfo
    {
        public InputSource source;
        public ChannelLetter channel;
        public Vector2 center;
        public string label;
        public bool enabled;
    }

    private void DrawNodeGraph()
    {
        Rect canvas = GUILayoutUtility.GetRect(0, NodeCanvasHeight, GUILayout.ExpandWidth(true));

        EditorGUI.DrawRect(canvas, new Color(0.16f, 0.16f, 0.18f));
        DrawCanvasBorder(canvas, new Color(0f, 0f, 0f, 0.4f));

        var inputs = ComputeInputPins(canvas);
        var outputs = ComputeOutputPins(canvas);

        var headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };
        GUI.Label(new Rect(canvas.x + 8, canvas.y + 4, 100, 16), "输入", headerStyle);
        GUI.Label(new Rect(canvas.xMax - 50, canvas.y + 4, 50, 16), "输出", headerStyle);

        var groupLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };
        GUI.Label(new Rect(canvas.x + 8, inputs[0].center.y - 18, 80, 14), "主源 A", groupLabelStyle);
        GUI.Label(new Rect(canvas.x + 8, inputs[4].center.y - 18, 80, 14), "副源 B", groupLabelStyle);
        GUI.Label(new Rect(canvas.x + 8, inputs[8].center.y - 18, 80, 14), "常量", groupLabelStyle);

        if (Event.current.type == EventType.Repaint)
        {
            for (int i = 0; i < 4; i++)
            {
                var (src, ch, inv) = GetOutputBinding(i);
                int inputIdx = GetInputPinIndex(src, ch);
                if (inputIdx < 0) continue;

                Vector2 startPos = inputs[inputIdx].center;
                Vector2 endPos = outputs[i].center;

                Color wireColor;
                if (inv)
                    wireColor = new Color(1f, 0.7f, 0.3f, 0.95f);
                else if (src == InputSource.White || src == InputSource.Black)
                    wireColor = new Color(0.7f, 0.7f, 0.7f, 0.85f);
                else
                    wireColor = OutputPinColor(i) * 0.95f;

                DrawWire(startPos, endPos, wireColor, inv ? 3.5f : 2.8f);
            }

            if (isDraggingFromOutput && draggingOutputIndex >= 0)
                DrawWire(dragMousePos, outputs[draggingOutputIndex].center, new Color(1f, 1f, 1f, 0.55f), 2.2f);
            else if (isDraggingFromInput && draggingInputIndex >= 0)
                DrawWire(inputs[draggingInputIndex].center, dragMousePos, new Color(1f, 1f, 1f, 0.55f), 2.2f);
        }

        var pinLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
        };

        bool dragHoverInput = false;
        int hoverInputIndex = -1;
        if (isDraggingFromOutput)
        {
            hoverInputIndex = HitTestPin(inputs, Event.current.mousePosition);
            if (hoverInputIndex >= 0) dragHoverInput = true;
        }

        for (int i = 0; i < inputs.Count; i++)
        {
            var pin = inputs[i];
            Color color = InputPinColor(pin);
            bool highlight = dragHoverInput && i == hoverInputIndex;
            DrawPin(pin.center, color, highlight);
            GUI.Label(
                new Rect(pin.center.x - 70, pin.center.y - 8, 60, 16),
                pin.label, pinLabelStyle);
        }

        var outLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.95f, 0.95f, 0.95f) },
            fontStyle = FontStyle.Bold,
        };
        var outBindStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 9,
        };

        bool dragHoverOutput = false;
        int hoverOutputIndex = -1;
        if (isDraggingFromInput)
        {
            hoverOutputIndex = HitTestPin(outputs, Event.current.mousePosition);
            if (hoverOutputIndex >= 0) dragHoverOutput = true;
        }

        for (int i = 0; i < outputs.Count; i++)
        {
            var pin = outputs[i];
            Color color = OutputPinColor(i);
            bool highlight = (isDraggingFromOutput && i == draggingOutputIndex)
                           || (dragHoverOutput && i == hoverOutputIndex);
            DrawPin(pin.center, color, highlight);
            GUI.Label(
                new Rect(pin.center.x + PinRadius + 6, pin.center.y - 16, 100, 14),
                "Out." + ((ChannelLetter)i).ToString(), outLabelStyle);
            var (src, ch, inv) = GetOutputBinding(i);
            outBindStyle.normal.textColor = inv
                ? new Color(1f, 0.75f, 0.4f)
                : new Color(0.7f, 0.85f, 1f);
            GUI.Label(
                new Rect(pin.center.x + PinRadius + 6, pin.center.y + 1, 110, 14),
                "← " + DescribeSource(src, ch, inv), outBindStyle);
        }

        var hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.LowerCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
        GUI.Label(
            new Rect(canvas.x, canvas.yMax - 18, canvas.width, 16),
            "拖线连接：左键拖输入↔输出引脚    右键输出引脚：菜单 (反转 / 设常量 / 显式选源)",
            hintStyle);

        HandleNodeGraphEvents(canvas, inputs, outputs);
    }

    private List<PinInfo> ComputeInputPins(Rect canvas)
    {
        var pins = new List<PinInfo>(10);
        float pinX = canvas.x + 100f;
        float topY = canvas.y + 32f;
        float rowH = 22f;

        for (int i = 0; i < 4; i++)
            pins.Add(new PinInfo { source = InputSource.A, channel = (ChannelLetter)i,
                center = new Vector2(pinX, topY + rowH * i), label = "A." + (ChannelLetter)i, enabled = true });

        float bGroupY = topY + rowH * 4 + 18f;
        for (int i = 0; i < 4; i++)
            pins.Add(new PinInfo { source = InputSource.B, channel = (ChannelLetter)i,
                center = new Vector2(pinX, bGroupY + rowH * i), label = "B." + (ChannelLetter)i, enabled = true });

        float constGroupY = bGroupY + rowH * 4 + 18f;
        pins.Add(new PinInfo { source = InputSource.White,
            center = new Vector2(pinX, constGroupY), label = "1 (白)", enabled = true });
        pins.Add(new PinInfo { source = InputSource.Black,
            center = new Vector2(pinX, constGroupY + rowH), label = "0 (黑)", enabled = true });

        return pins;
    }

    private List<PinInfo> ComputeOutputPins(Rect canvas)
    {
        var pins = new List<PinInfo>(4);
        float pinX = canvas.xMax - 110f;
        float topY = canvas.y + 56f;
        float rowH = 56f;
        for (int i = 0; i < 4; i++)
            pins.Add(new PinInfo { channel = (ChannelLetter)i,
                center = new Vector2(pinX, topY + rowH * i), label = "Out." + (ChannelLetter)i, enabled = true });
        return pins;
    }

    private static int GetInputPinIndex(InputSource src, ChannelLetter ch)
    {
        switch (src)
        {
            case InputSource.A: return (int)ch;
            case InputSource.B: return 4 + (int)ch;
            case InputSource.White: return 8;
            case InputSource.Black: return 9;
            default: return -1;
        }
    }

    private static int HitTestPin(List<PinInfo> pins, Vector2 mouse)
    {
        for (int i = 0; i < pins.Count; i++)
            if (Vector2.Distance(pins[i].center, mouse) < PinHitRadius) return i;
        return -1;
    }

    private void HandleNodeGraphEvents(Rect canvas, List<PinInfo> inputs, List<PinInfo> outputs)
    {
        var e = Event.current;
        bool inCanvas = canvas.Contains(e.mousePosition);

        if (e.type == EventType.MouseDown && e.button == 0 && inCanvas)
        {
            int outIdx = HitTestPin(outputs, e.mousePosition);
            if (outIdx >= 0)
            {
                isDraggingFromOutput = true;
                draggingOutputIndex = outIdx;
                dragMousePos = e.mousePosition;
                e.Use();
                return;
            }
            int inIdx = HitTestPin(inputs, e.mousePosition);
            if (inIdx >= 0)
            {
                isDraggingFromInput = true;
                draggingInputIndex = inIdx;
                dragMousePos = e.mousePosition;
                e.Use();
                return;
            }
        }

        if (e.type == EventType.MouseDrag && (isDraggingFromOutput || isDraggingFromInput))
        {
            dragMousePos = e.mousePosition;
            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (isDraggingFromOutput)
            {
                int hit = HitTestPin(inputs, e.mousePosition);
                if (hit >= 0)
                    SetOutputBinding(draggingOutputIndex, inputs[hit].source, inputs[hit].channel);
                isDraggingFromOutput = false;
                draggingOutputIndex = -1;
                e.Use();
                Repaint();
            }
            else if (isDraggingFromInput)
            {
                int hit = HitTestPin(outputs, e.mousePosition);
                if (hit >= 0)
                    SetOutputBinding(hit, inputs[draggingInputIndex].source, inputs[draggingInputIndex].channel);
                isDraggingFromInput = false;
                draggingInputIndex = -1;
                e.Use();
                Repaint();
            }
        }

        if (e.type == EventType.ContextClick && inCanvas)
        {
            int outIdx = HitTestPin(outputs, e.mousePosition);
            if (outIdx >= 0)
            {
                ShowOutputPinMenu(outIdx);
                e.Use();
            }
        }

        if (inCanvas && (isDraggingFromOutput || isDraggingFromInput))
            Repaint();
    }

    private void ShowOutputPinMenu(int outputIndex)
    {
        var menu = new GenericMenu();
        var (src, ch, inv) = GetOutputBinding(outputIndex);

        menu.AddItem(new GUIContent("反转 (1-x)"), inv, () =>
        {
            ToggleOutputInvert(outputIndex);
            Repaint();
        });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("设为常量 1 (白)"), src == InputSource.White, () =>
        {
            SetOutputBinding(outputIndex, InputSource.White, ChannelLetter.R);
            Repaint();
        });
        menu.AddItem(new GUIContent("设为常量 0 (黑)"), src == InputSource.Black, () =>
        {
            SetOutputBinding(outputIndex, InputSource.Black, ChannelLetter.R);
            Repaint();
        });
        menu.AddSeparator("");

        for (int sIdx = 0; sIdx < 2; sIdx++)
        {
            var srcEnum = sIdx == 0 ? InputSource.A : InputSource.B;
            string srcName = sIdx == 0 ? "A" : "B";

            for (int cIdx = 0; cIdx < 4; cIdx++)
            {
                var chEnum = (ChannelLetter)cIdx;
                bool selected = src == srcEnum && ch == chEnum;
                int capturedOutput = outputIndex;
                var capturedSrc = srcEnum;
                var capturedCh = chEnum;
                menu.AddItem(new GUIContent($"设为 {srcName}.{chEnum}"), selected, () =>
                {
                    SetOutputBinding(capturedOutput, capturedSrc, capturedCh);
                    Repaint();
                });
            }
        }

        menu.ShowAsContext();
    }

    private (InputSource src, ChannelLetter ch, bool inv) GetOutputBinding(int idx)
    {
        switch (idx)
        {
            case 0: return (outR_Input, outR_Channel, outR_Invert);
            case 1: return (outG_Input, outG_Channel, outG_Invert);
            case 2: return (outB_Input, outB_Channel, outB_Invert);
            case 3: return (outA_Input, outA_Channel, outA_Invert);
            default: return (InputSource.A, ChannelLetter.R, false);
        }
    }

    private void SetOutputBinding(int idx, InputSource src, ChannelLetter ch)
    {
        switch (idx)
        {
            case 0: outR_Input = src; outR_Channel = ch; break;
            case 1: outG_Input = src; outG_Channel = ch; break;
            case 2: outB_Input = src; outB_Channel = ch; break;
            case 3: outA_Input = src; outA_Channel = ch; break;
        }
    }

    private void ToggleOutputInvert(int idx)
    {
        switch (idx)
        {
            case 0: outR_Invert = !outR_Invert; break;
            case 1: outG_Invert = !outG_Invert; break;
            case 2: outB_Invert = !outB_Invert; break;
            case 3: outA_Invert = !outA_Invert; break;
        }
    }

    private static void DrawPin(Vector2 center, Color color, bool highlight)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (highlight)
        {
            var prev = Handles.color;
            Handles.color = new Color(1f, 0.95f, 0.4f, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, PinRadius + 3f);
            Handles.color = prev;
        }
        var saved = Handles.color;
        Handles.color = color;
        Handles.DrawSolidDisc(center, Vector3.forward, PinRadius);
        Handles.color = new Color(0f, 0f, 0f, 0.85f);
        Handles.DrawWireDisc(center, Vector3.forward, PinRadius);
        Handles.DrawWireDisc(center, Vector3.forward, PinRadius - 0.5f);
        Handles.color = saved;
    }

    private static void DrawWire(Vector2 from, Vector2 to, Color color, float thickness)
    {
        float dx = Mathf.Max(40f, Mathf.Abs(to.x - from.x) * 0.5f);
        Handles.DrawBezier(from, to,
            from + Vector2.right * dx, to + Vector2.left * dx,
            color, null, thickness);
    }

    private static void DrawCanvasBorder(Rect r, Color color)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), color);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), color);
    }

    private static Color InputPinColor(PinInfo pin)
    {
        switch (pin.source)
        {
            case InputSource.A: return new Color(0.45f, 0.75f, 1f);
            case InputSource.B: return new Color(1f, 0.7f, 0.45f);
            case InputSource.White: return new Color(0.95f, 0.95f, 0.95f);
            case InputSource.Black: return new Color(0.25f, 0.25f, 0.25f);
            default: return Color.gray;
        }
    }

    private static Color OutputPinColor(int idx)
    {
        switch (idx)
        {
            case 0: return new Color(1f, 0.4f, 0.4f);
            case 1: return new Color(0.4f, 1f, 0.4f);
            case 2: return new Color(0.4f, 0.6f, 1f);
            case 3: return new Color(1f, 1f, 1f);
            default: return Color.gray;
        }
    }

    // ============================================================
    // ====================  共享：模板 / 输出 / 像素采样  ========
    // ============================================================

    private void ResetMappingToDefault()
    {
        outR_Input = InputSource.A; outR_Channel = ChannelLetter.R; outR_Invert = false;
        outG_Input = InputSource.A; outG_Channel = ChannelLetter.G; outG_Invert = false;
        outB_Input = InputSource.A; outB_Channel = ChannelLetter.B; outB_Invert = false;
        outA_Input = InputSource.A; outA_Channel = ChannelLetter.A; outA_Invert = false;
    }

    private static byte SampleByte(Color32 a, Color32 b, InputSource src, ChannelLetter ch, bool invert)
    {
        byte v;
        switch (src)
        {
            case InputSource.A: v = SampleChannelByte(a, ch); break;
            case InputSource.B: v = SampleChannelByte(b, ch); break;
            case InputSource.White: v = 255; break;
            case InputSource.Black: v = 0; break;
            default: v = 0; break;
        }
        return invert ? (byte)(255 - v) : v;
    }

    private static byte SampleChannelByte(Color32 c, ChannelLetter ch)
    {
        switch (ch)
        {
            case ChannelLetter.R: return c.r;
            case ChannelLetter.G: return c.g;
            case ChannelLetter.B: return c.b;
            case ChannelLetter.A: return c.a;
            default: return 0;
        }
    }

    private static string DescribeSource(InputSource src, ChannelLetter ch, bool invert)
    {
        string s;
        if (src == InputSource.White) s = "1";
        else if (src == InputSource.Black) s = "0";
        else s = $"{src}.{ch}";
        return s + (invert ? "(反)" : "");
    }

    private static Color32[] ResizeNearest(Color32[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new Color32[dstW * dstH];
        for (int y = 0; y < dstH; y++)
        {
            int sy = (int)((long)y * srcH / dstH);
            for (int x = 0; x < dstW; x++)
            {
                int sx = (int)((long)x * srcW / dstW);
                dst[y * dstW + x] = src[sy * srcW + sx];
            }
        }
        return dst;
    }

    /// <summary>模板：保存 / 加载</summary>
    private void DrawTemplateSection()
    {
        foldTemplates = EditorGUILayout.BeginFoldoutHeaderGroup(
            foldTemplates, $"模板 (Templates)  [{templates.Count} 个]");
        if (foldTemplates)
        {
            EditorGUILayout.BeginVertical("box");

            if (templates.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("选择模板", GUILayout.Width(60));
                int newIndex = EditorGUILayout.Popup(selectedTemplateIndex, templateDisplayNames);
                if (newIndex != selectedTemplateIndex && newIndex >= 0 && newIndex < templates.Count)
                {
                    selectedTemplateIndex = newIndex;
                    ApplyTemplate(templates[selectedTemplateIndex]);
                    EditorPrefs.SetString(LastTemplatePrefsKey, templates[selectedTemplateIndex].name);
                }

                bool isDefault = selectedTemplateIndex >= 0 && selectedTemplateIndex < templates.Count
                                 && templates[selectedTemplateIndex].name == DefaultTemplateName;
                GUI.enabled = selectedTemplateIndex >= 0 && selectedTemplateIndex < templates.Count && !isDefault;
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("删除模板",
                        $"确定删除模板 \"{templates[selectedTemplateIndex].name}\" 吗？",
                        "删除", "取消"))
                    {
                        templates.RemoveAt(selectedTemplateIndex);
                        selectedTemplateIndex = Mathf.Min(selectedTemplateIndex, templates.Count - 1);
                        SaveTemplatesToPrefs();
                        RefreshTemplateNames();
                    }
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            newTemplateName = EditorGUILayout.TextField("保存为", newTemplateName);
            GUI.enabled = !string.IsNullOrEmpty(newTemplateName) && newTemplateName != DefaultTemplateName;
            if (GUILayout.Button("保存", GUILayout.Width(50)))
            {
                SaveCurrentAsTemplate(newTemplateName);
                newTemplateName = "";
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "模板只保存通道映射规则；A/B 拖入贴图、批处理源、后缀配对规则不会被保存。",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    /// <summary>输出设置：格式 + 后缀</summary>
    private void DrawOutputSection()
    {
        foldOutput = EditorGUILayout.BeginFoldoutHeaderGroup(foldOutput, "输出 (Output)");
        if (foldOutput)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("格式", GUILayout.Width(60));
            outputFormat = (OutputFormat)EditorGUILayout.EnumPopup(outputFormat, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("后缀", GUILayout.Width(60));
            outputSuffix = EditorGUILayout.TextField(outputSuffix, GUILayout.Width(140));
            if (GUILayout.Button("还原默认 (_Fix)", EditorStyles.miniButton, GUILayout.Width(110)))
                outputSuffix = DefaultSuffix;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "上半段单张另存、下半段批量产出都使用同一后缀。\n例如 body.png + 后缀 _Fix → body_Fix.png。",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void EnsureDefaultTemplate()
    {
        bool hasDefault = templates.Any(t => t.name == DefaultTemplateName);
        if (!hasDefault)
        {
            var def = new ChannelTemplate
            {
                name = DefaultTemplateName,
                rInput = (int)InputSource.A, rChannel = (int)ChannelLetter.R, rInvert = false,
                gInput = (int)InputSource.A, gChannel = (int)ChannelLetter.G, gInvert = false,
                bInput = (int)InputSource.A, bChannel = (int)ChannelLetter.B, bInvert = false,
                aInput = (int)InputSource.A, aChannel = (int)ChannelLetter.A, aInvert = false,
            };
            templates.Insert(0, def);
            SaveTemplatesToPrefs();
            RefreshTemplateNames();
            if (selectedTemplateIndex < 0) selectedTemplateIndex = 0;
        }
    }

    private void SaveCurrentAsTemplate(string name)
    {
        if (name == DefaultTemplateName) return;
        int existingIndex = templates.FindIndex(t => t.name == name);
        var template = new ChannelTemplate
        {
            name = name,
            rInput = (int)outR_Input, rChannel = (int)outR_Channel, rInvert = outR_Invert,
            gInput = (int)outG_Input, gChannel = (int)outG_Channel, gInvert = outG_Invert,
            bInput = (int)outB_Input, bChannel = (int)outB_Channel, bInvert = outB_Invert,
            aInput = (int)outA_Input, aChannel = (int)outA_Channel, aInvert = outA_Invert,
        };
        if (existingIndex >= 0)
        {
            templates[existingIndex] = template;
            selectedTemplateIndex = existingIndex;
        }
        else
        {
            templates.Add(template);
            selectedTemplateIndex = templates.Count - 1;
        }
        SaveTemplatesToPrefs();
        RefreshTemplateNames();
        EditorPrefs.SetString(LastTemplatePrefsKey, name);
        Debug.Log($"[通道重映射] 模板 \"{name}\" 已保存。");
    }

    private void ApplyTemplate(ChannelTemplate t)
    {
        outR_Input = (InputSource)t.rInput; outR_Channel = (ChannelLetter)t.rChannel; outR_Invert = t.rInvert;
        outG_Input = (InputSource)t.gInput; outG_Channel = (ChannelLetter)t.gChannel; outG_Invert = t.gInvert;
        outB_Input = (InputSource)t.bInput; outB_Channel = (ChannelLetter)t.bChannel; outB_Invert = t.bInvert;
        outA_Input = (InputSource)t.aInput; outA_Channel = (ChannelLetter)t.aChannel; outA_Invert = t.aInvert;
        lastPreviewHash = 0;
    }

    private void LoadTemplatesFromPrefs()
    {
        string json = EditorPrefs.GetString(TemplatePrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonUtility.FromJson<TemplateList>(json);
                if (list != null && list.items != null) templates = list.items;
            }
            catch (System.Exception)
            {
                templates = new List<ChannelTemplate>();
            }
        }
        RefreshTemplateNames();
    }

    private void SaveTemplatesToPrefs()
    {
        var list = new TemplateList { items = templates };
        EditorPrefs.SetString(TemplatePrefsKey, JsonUtility.ToJson(list));
    }

    private void RefreshTemplateNames()
    {
        templateDisplayNames = new string[templates.Count];
        for (int i = 0; i < templates.Count; i++)
            templateDisplayNames[i] = templates[i].name;
    }
}
