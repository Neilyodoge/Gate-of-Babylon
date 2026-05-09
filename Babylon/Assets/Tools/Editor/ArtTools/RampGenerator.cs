// ============================================================================
// RampGenerator.cs
// Ramp 生成工具
//
// 把若干 Unity Gradient 串联烘成一张横向 Ramp 贴图，给 PBRToon / 卡通描边等
// 用 ramp 控制明暗交界 / 高光段的 shader 用。设计参考 DanbaidongRP
// (https://github.com/danbaidong1111/DanbaidongRP) 的 GradientsRampEditorWindow。
//
// 序列化策略：
//   * 像素结果写到 PNG (RGBA8)
//   * Gradient 列表 JSON 写进 TextureImporter.userData，下次打开时还能继续编辑
//   * Mipmap 关、压缩关、Wrap=Clamp、Filter=Bilinear（弱化条间硬边）
//
// 入口：
//   * 菜单：nTools/美术工具/Ramp生成工具
//   * Project 视图右键 PNG 后 "Open in Ramp 生成工具" 直接载入该贴图
//
// 工作流：
//   1) 顶部贴图槽位拖入已有 ramp PNG → 工具会从 importer userData 还原 Gradient 列表
//   2) 没贴图时点 "New..." 选保存路径，工具会写一张占位 PNG 并自动绑定
//   3) 中间是 Gradient ReorderableList，+ - 增删，可上下拖排序
//   4) 底部 SingleRampSize（X 宽度，Y 每条像素高），点 Save 写回 PNG
//
// 反推功能（外部 ramp 图导入时用）：
//   * 当贴图没有 userData JSON（来自第三方 / Photoshop / 美术手画）时，
//     拖入贴图会自动调用反推流程生成可调的 Gradient 列表
//   * 自动识别条数：扫描相邻行颜色差找"行边界"，段长一致 → 直接用识别值；
//     否则展开折叠区，让用户手填条数（默认 1 = 整图当一条）
//   * 采样数固定 8（最大值）；可选模式：均匀（等距 8 点）/ 自适应（梯度最大处 8 点）
//   * 透明像素当作黑色，输出 Gradient 全部 alpha=1（ramp 不带透明度）
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RampGenerator
{
    // ------------------------------------------------------------------------
    // 让 Unity 的 EditorJsonUtility 能直接序列化 Gradient（Gradient 不是
    // ScriptableObject，不能直接 ToJson；必须放进一个 ScriptableObject 字段里）
    // ------------------------------------------------------------------------
    internal sealed class GradientSerializeHelper : ScriptableObject
    {
        public Gradient gradient;
    }

    // ------------------------------------------------------------------------
    // 数据 / IO 层
    // ------------------------------------------------------------------------
    [System.Serializable]
    public class GradientsRamp
    {
        // 当前编辑/绑定的目标贴图
        public Texture2D rampTexture;

        // 每条横向 Gradient（自上而下排列；存到贴图时第 0 条画在 Y=top）
        public List<Gradient> gradients = new List<Gradient>();

        // 单条 Ramp 像素尺寸；y 表示每条占多少像素高（>1 时同一 Gradient 在 Y 方向复制）
        public Vector2Int singleRampSize = new Vector2Int(256, 4);

        // userData JSON 之间的分隔符（DanbaidongRP 同款，方便互导）
        const string k_GradientSeparator = "#";

        // ---- 加载：从已有贴图反推 Gradient 列表 ----
        public bool LoadFromTexture(Texture2D rampTex)
        {
            rampTexture = rampTex;
            if (rampTex == null)
            {
                gradients = new List<Gradient>();
                return false;
            }

            string path = AssetDatabase.GetAssetPath(rampTex);
            var importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.userData))
            {
                gradients = JsonToGradients(importer.userData);
            }
            else
            {
                gradients = new List<Gradient>();
            }

            int count = Mathf.Max(1, gradients.Count);
            singleRampSize = new Vector2Int(rampTex.width, rampTex.height / count);
            return true;
        }

        // ---- 保存：把 gradients 烘成 PNG 写回 rampTexture 路径，并把 JSON 塞 userData ----
        public bool Save()
        {
            if (rampTexture == null || gradients == null || gradients.Count == 0)
            {
                Debug.LogError("[RampGenerator] 没有可保存的目标贴图或 Gradient 列表为空");
                return false;
            }

            string path = AssetDatabase.GetAssetPath(rampTexture);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[RampGenerator] 目标贴图不在工程内，无法定位 PNG 路径");
                return false;
            }

            // 烘像素
            Texture2D baked = BakeGradientsToTexture(gradients, singleRampSize.x, singleRampSize.y);
            File.WriteAllBytes(path, baked.EncodeToPNG());
            Object.DestroyImmediate(baked);

            // 落 importer 设置 + userData
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.userData = GradientsToJson(gradients);
                importer.SaveAndReimport();
            }

            return true;
        }

        // ---- 在工程目录新建一张占位 PNG（小尺寸，Save() 时会按 singleRampSize 重写） ----
        public static Texture2D CreatePlaceholderPng(string assetPath, Vector2Int size)
        {
            var tmp = new Texture2D(Mathf.Max(1, size.x), Mathf.Max(1, size.y), TextureFormat.RGBA32, false);
            for (int x = 0; x < tmp.width; x++)
                for (int y = 0; y < tmp.height; y++)
                    tmp.SetPixel(x, y, Color.white);
            tmp.Apply();

            File.WriteAllBytes(assetPath, tmp.EncodeToPNG());
            Object.DestroyImmediate(tmp);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public static Gradient CreateSampleGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.10f, 0.18f, 0.35f), 0.0f),  // 阴影侧（冷紫蓝）
                    new GradientColorKey(new Color(0.85f, 0.55f, 0.45f), 0.45f), // 明暗交界（暖橙）
                    new GradientColorKey(Color.white,                  1.0f),    // 受光侧（白）
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f),
                });
            return g;
        }

        // --------------------------------------------------------------------
        // 反推：扫描贴图像素，生成可调 Gradient 列表
        // --------------------------------------------------------------------

        public enum ReverseMode
        {
            Uniform = 0,    // 等距采 N 个 stop
            Adaptive = 1,   // 取颜色梯度最大的 N 个位置作为 stop
        }

        /// <summary>
        /// 自动识别贴图里有几条 ramp。
        /// 算法：扫描相邻行的颜色差异，找"行边界"（颜色突变行）；段长一致才算识别成功。
        /// 返回 (条数, 每条行高)；失败时返回 (-1, -1)。
        /// </summary>
        public static (int rampCount, int rowHeight) DetectRampLayout(Texture2D tex)
        {
            if (tex == null) return (-1, -1);
            int H = tex.height;
            if (H <= 1) return (1, Mathf.Max(1, H));

            // 临时切开 isReadable
            string path = AssetDatabase.GetAssetPath(tex);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            bool wasReadable = importer != null && importer.isReadable;
            if (importer != null && !wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            int rampCount = -1;
            int rowHeight = -1;

            try
            {
                int W = tex.width;

                // 找所有"行边界"：当前行与上一行不一致的位置
                var boundaries = new List<int> { 0 };
                for (int y = 1; y < H; y++)
                {
                    if (!RowsMatch(tex, W, y - 1, y, 0.02f))
                        boundaries.Add(y);
                }
                boundaries.Add(H);

                // 验证段长是否一致（容许 1 像素误差）
                int n = boundaries.Count - 1;
                if (n <= 0) return (1, H);

                int firstLen = boundaries[1] - boundaries[0];
                if (firstLen <= 0) return (-1, -1);

                bool consistent = true;
                for (int i = 1; i < n; i++)
                {
                    int segLen = boundaries[i + 1] - boundaries[i];
                    if (Mathf.Abs(segLen - firstLen) > 1)
                    {
                        consistent = false;
                        break;
                    }
                }

                if (consistent)
                {
                    rampCount = n;
                    rowHeight = firstLen;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RampGenerator] 自动识别失败：{e.Message}");
            }
            finally
            {
                if (importer != null && !wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            return (rampCount, rowHeight);
        }

        /// <summary>判断两行像素是否一致（采样 5 个 X 位置，全部相似才算一致）</summary>
        static bool RowsMatch(Texture2D tex, int W, int y1, int y2, float epsilon)
        {
            int[] xs = { 0, W / 4, W / 2, (3 * W) / 4, W - 1 };
            float eps2 = epsilon * epsilon;
            foreach (int x in xs)
            {
                var c1 = tex.GetPixel(x, y1);
                var c2 = tex.GetPixel(x, y2);
                float dr = c1.r - c2.r;
                float dg = c1.g - c2.g;
                float db = c1.b - c2.b;
                if (dr * dr + dg * dg + db * db > eps2) return false;
            }
            return true;
        }

        /// <summary>
        /// 从贴图反推 Gradient 列表。
        /// 自动按 rampRowHeight 把贴图分成多条 ramp（或 wholeImageAsOne=true 时整图当一条）。
        /// 临时切开 importer.isReadable，结束后恢复，不影响源贴图导入设置。
        /// </summary>
        /// <param name="rampTex">输入 ramp 贴图</param>
        /// <param name="rampRowHeight">每条 ramp 占多少行（来自 singleRampSize.y）</param>
        /// <param name="sampleCount">每条 ramp 采几个 stop（2-8）</param>
        /// <param name="mode">采样策略</param>
        /// <param name="wholeImageAsOne">true=整图当一条；false=按 rampRowHeight 拆分</param>
        public static List<Gradient> ReverseEngineerFromTexture(
            Texture2D rampTex,
            int rampRowHeight,
            int sampleCount,
            ReverseMode mode,
            bool wholeImageAsOne)
        {
            var result = new List<Gradient>();
            if (rampTex == null) return result;

            // 临时切开 isReadable
            string path = AssetDatabase.GetAssetPath(rampTex);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            bool wasReadable = importer != null && importer.isReadable;
            if (importer != null && !wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            try
            {
                int W = rampTex.width;
                int H = rampTex.height;
                int rowH = wholeImageAsOne ? H : Mathf.Max(1, rampRowHeight);
                int rampCount = Mathf.Max(1, H / rowH);
                sampleCount = Mathf.Clamp(sampleCount, 2, 8);

                for (int gi = 0; gi < rampCount; gi++)
                {
                    // 第 0 条画在最上面（top），按相同坐标系反推
                    int yTop = H - 1 - gi * rowH;
                    int yMid = yTop - rowH / 2;
                    yMid = Mathf.Clamp(yMid, 0, H - 1);

                    var rowPixels = rampTex.GetPixels(0, yMid, W, 1);

                    Gradient g = mode == ReverseMode.Adaptive
                        ? BuildAdaptiveGradient(rowPixels, sampleCount)
                        : BuildUniformGradient(rowPixels, sampleCount);
                    result.Add(g);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RampGenerator] 反推失败：{e.Message}");
            }
            finally
            {
                // 恢复 isReadable
                if (importer != null && !wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            return result;
        }

        /// <summary>
        /// 反推取色规则：
        /// · 透明像素 (alpha &lt; 0.01) 当作黑色处理（外部图常见的透明背景不应被识别成 stop）
        /// · 其余像素强制 alpha=1，输出的 Gradient 永远不带透明度
        /// 与 ramp 贴图的语义匹配（ramp = 颜色 LUT，不需要透明通道）
        /// </summary>
        static Color NormalizeRampPixel(Color c)
        {
            if (c.a <= 0.01f) return new Color(0f, 0f, 0f, 1f);
            return new Color(c.r, c.g, c.b, 1f);
        }

        /// <summary>所有反推 Gradient 共用的固定 alpha=1 双端 key</summary>
        static readonly GradientAlphaKey[] s_OpaqueAlphaKeys = new[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f),
        };

        /// <summary>等距采 sampleCount 个位置，每个位置取像素颜色作为 stop</summary>
        static Gradient BuildUniformGradient(Color[] row, int sampleCount)
        {
            int W = row.Length;
            sampleCount = Mathf.Clamp(sampleCount, 2, 8);

            var ckeys = new GradientColorKey[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                int px = Mathf.Clamp(Mathf.RoundToInt(t * (W - 1)), 0, W - 1);
                ckeys[i] = new GradientColorKey(NormalizeRampPixel(row[px]), t);
            }

            var g = new Gradient();
            g.SetKeys(ckeys, s_OpaqueAlphaKeys);
            return g;
        }

        /// <summary>
        /// 自适应：找颜色梯度最大的 (sampleCount - 2) 个内点 + 首尾 → 共 sampleCount 个 stop。
        /// 加 NMS（非极大值抑制）防止关键点聚集在同一拐点附近。
        /// 注：梯度只用 RGB 三个通道（已对透明像素 NormalizeRampPixel 归一化为黑色）。
        /// </summary>
        static Gradient BuildAdaptiveGradient(Color[] row, int sampleCount)
        {
            int W = row.Length;
            sampleCount = Mathf.Clamp(sampleCount, 2, 8);
            if (W <= sampleCount) return BuildUniformGradient(row, sampleCount);

            // 先把像素归一化（透明 → 黑色，alpha 强制 1）
            var nrow = new Color[W];
            for (int i = 0; i < W; i++) nrow[i] = NormalizeRampPixel(row[i]);

            // 计算每像素与左邻居的 RGB 颜色差（不含 alpha）
            float[] grad = new float[W];
            grad[0] = 0f;
            for (int i = 1; i < W; i++)
            {
                float dr = nrow[i].r - nrow[i - 1].r;
                float dg = nrow[i].g - nrow[i - 1].g;
                float db = nrow[i].b - nrow[i - 1].b;
                grad[i] = Mathf.Sqrt(dr * dr + dg * dg + db * db);
            }

            // 选 sampleCount-2 个内点
            int innerCount = sampleCount - 2;
            int minStride = Mathf.Max(1, W / (sampleCount * 2));

            // 候选 = 1..W-2，按 grad 降序排
            var candidates = new (int idx, float g)[W - 2];
            for (int i = 0; i < W - 2; i++)
                candidates[i] = (i + 1, grad[i + 1]);
            System.Array.Sort(candidates, (a, b) => b.g.CompareTo(a.g));

            // NMS：依次选，跟已选点距离都 >= minStride 才算
            var selected = new List<int>();
            foreach (var (idx, _) in candidates)
            {
                if (selected.Count >= innerCount) break;
                bool tooClose = false;
                foreach (var s in selected)
                {
                    if (Mathf.Abs(s - idx) < minStride) { tooClose = true; break; }
                }
                // 也要和首尾保持距离
                if (idx < minStride || idx > W - 1 - minStride) tooClose = true;
                if (!tooClose) selected.Add(idx);
            }

            // 不够的话用均匀填补
            while (selected.Count < innerCount)
            {
                int t = (selected.Count + 1) * W / (innerCount + 1);
                if (!selected.Contains(t)) selected.Add(t);
                else break;
            }

            // 拼成关键点 [0, ...selected, W-1] 并按 idx 排序
            var indices = new List<int> { 0 };
            indices.AddRange(selected);
            indices.Add(W - 1);
            indices = indices.Distinct().OrderBy(i => i).ToList();

            // 兜底：如果最终少于 2 个（不应发生），回退均匀
            if (indices.Count < 2) return BuildUniformGradient(row, sampleCount);

            var ckeys = new GradientColorKey[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                int idx = indices[i];
                float t = (float)idx / (W - 1);
                ckeys[i] = new GradientColorKey(nrow[idx], t);
            }

            var g2 = new Gradient();
            g2.SetKeys(ckeys, s_OpaqueAlphaKeys);
            return g2;
        }

        // ---- 烘焙：N 条 Gradient 横向铺满 [0,1] 区间，纵向每条复制 rampHeight 个像素 ----
        static Texture2D BakeGradientsToTexture(List<Gradient> gradients, int rampWidth, int rampHeight)
        {
            int n = gradients.Count;
            int width = Mathf.Max(1, rampWidth);
            int rowH = Mathf.Max(1, rampHeight);
            int height = rowH * n;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float t = (width == 1) ? 0f : (float)x / (width - 1);
                for (int gi = 0; gi < n; gi++)
                {
                    // 第 0 条画在最上面（top），所以 y = height - 1 - gi*rowH 起算
                    int yTop = height - 1 - gi * rowH;
                    Color c = gradients[gi].Evaluate(t);
                    for (int yy = 0; yy < rowH; yy++)
                    {
                        tex.SetPixel(x, yTop - yy, c);
                    }
                }
            }
            tex.Apply(false, false);
            return tex;
        }

        // ---- Gradient <-> JSON ----
        static string GradientsToJson(List<Gradient> gradients)
        {
            var arr = new string[gradients.Count];
            for (int i = 0; i < gradients.Count; i++)
            {
                arr[i] = SingleGradientToJson(gradients[i]);
            }
            return string.Join(k_GradientSeparator, arr);
        }

        static List<Gradient> JsonToGradients(string json)
        {
            var list = new List<Gradient>();
            if (string.IsNullOrEmpty(json)) return list;

            string[] parts = json.Split(new[] { k_GradientSeparator }, System.StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                Gradient g = SingleJsonToGradient(parts[i]);
                if (g != null) list.Add(g);
            }
            return list;
        }

        static string SingleGradientToJson(Gradient gradient)
        {
            var helper = ScriptableObject.CreateInstance<GradientSerializeHelper>();
            helper.gradient = gradient;
            string s = EditorJsonUtility.ToJson(helper);
            Object.DestroyImmediate(helper);
            return s;
        }

        static Gradient SingleJsonToGradient(string gradientJson)
        {
            var helper = ScriptableObject.CreateInstance<GradientSerializeHelper>();
            helper.gradient = new Gradient();
            EditorJsonUtility.FromJsonOverwrite(gradientJson, helper);
            Gradient g = helper.gradient;
            Object.DestroyImmediate(helper);
            return g;
        }
    }

    // ------------------------------------------------------------------------
    // 编辑器窗口
    // ------------------------------------------------------------------------
    public class RampGeneratorWindow : EditorWindow
    {
        // 用于 SerializedObject 双向同步：必须是字段而不是属性
        [SerializeField] private GradientsRamp m_Ramp = new GradientsRamp();

        SerializedObject m_SerializedSelf;
        SerializedProperty m_RampObjProp;
        SerializedProperty m_RampTexProp;
        SerializedProperty m_GradientsProp;
        ReorderableList m_GradientsList;

        // 反推 (Reverse-Engineer) 配置
        // 采样数固定走最大值 8（更细密的 stop = 更接近原图）
        const int k_ReverseSampleCount = 8;
        [SerializeField] GradientsRamp.ReverseMode m_ReverseMode = GradientsRamp.ReverseMode.Adaptive;
        [SerializeField] bool m_FoldReverse = false;

        // 自动识别结果（拖图时填充）
        // > 0  : 识别成功，UI 隐藏"条数"输入框，直接用识别值
        // <= 0 : 识别失败，UI 显示"条数"输入框让用户手填
        int m_DetectedRampCount = -1;
        int m_DetectedRowHeight = -1;
        // 自动识别失败时用户手填的条数
        [SerializeField] int m_ManualRampCount = 1;

        // 默认保存文件夹：优先 PBRToon 旁，没有就回落到 Assets 根
        const string k_DefaultSaveFolder = "Assets/Effect/PBRToon";
        const string k_MenuPath = "nTools/美术工具/Ramp生成工具";
        const string k_AssetMenuPath = "Assets/Open in Ramp 生成工具";

        // ---- 菜单入口 ----
        [MenuItem(k_MenuPath, false, 56)]
        public static void ShowWindow()
        {
            var window = GetWindow<RampGeneratorWindow>("Ramp生成工具");
            window.minSize = new Vector2(420, 480);
        }

        public static void ShowWindow(Texture2D rampTex)
        {
            var window = GetWindow<RampGeneratorWindow>("Ramp生成工具");
            window.minSize = new Vector2(420, 480);
            window.m_Ramp.LoadFromTexture(rampTex);
            window.RebuildSerialized();
            window.TryDetectRampLayout();
            // 没 userData 的外部图自动反推
            if (rampTex != null && window.m_Ramp.gradients.Count == 0)
            {
                window.ReverseEngineerInPlace();
            }
        }

        // 在 Project 右键 PNG 时弹一个 "Open in Ramp 生成工具"
        [MenuItem(k_AssetMenuPath, validate = true)]
        static bool ValidateOpenSelected()
        {
            return Selection.activeObject is Texture2D;
        }

        [MenuItem(k_AssetMenuPath, priority = 2200)]
        static void OpenSelected()
        {
            ShowWindow(Selection.activeObject as Texture2D);
        }

        // ---- 生命周期 ----
        void OnEnable()
        {
            if (m_Ramp == null) m_Ramp = new GradientsRamp();
            RebuildSerialized();
        }

        void RebuildSerialized()
        {
            m_SerializedSelf = new SerializedObject(this);
            m_RampObjProp = m_SerializedSelf.FindProperty("m_Ramp");
            m_RampTexProp = m_RampObjProp.FindPropertyRelative("rampTexture");
            m_GradientsProp = m_RampObjProp.FindPropertyRelative("gradients");

            m_GradientsList = new ReorderableList(m_SerializedSelf, m_GradientsProp,
                draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true)
            {
                drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Gradient List");
                },
                drawElementCallback = (Rect rect, int index, bool active, bool focused) =>
                {
                    var elem = m_GradientsList.serializedProperty.GetArrayElementAtIndex(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, elem, GUIContent.none);
                },
                onAddCallback = (ReorderableList list) =>
                {
                    int newIndex = list.count;
                    m_GradientsProp.arraySize = newIndex + 1;
                    m_SerializedSelf.ApplyModifiedProperties();

                    if (m_Ramp.gradients.Count <= newIndex)
                        m_Ramp.gradients.Add(GradientsRamp.CreateSampleGradient());
                    else
                        m_Ramp.gradients[newIndex] = GradientsRamp.CreateSampleGradient();

                    m_SerializedSelf.Update();
                },
            };
        }


        // ---- GUI ----
        void OnGUI()
        {
            if (m_SerializedSelf == null) RebuildSerialized();
            m_SerializedSelf.Update();

            EditorGUILayout.Space(4);

            // 1) 贴图绑定行 + New 按钮
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_RampTexProp, new GUIContent("Ramp Texture"));
                if (EditorGUI.EndChangeCheck())
                {
                    m_SerializedSelf.ApplyModifiedProperties();
                    m_Ramp.LoadFromTexture(m_Ramp.rampTexture);
                    TryDetectRampLayout();
                    // 拖入贴图但贴图没有 userData（外部图）→ 自动反推
                    if (m_Ramp.rampTexture != null && m_Ramp.gradients.Count == 0)
                    {
                        ReverseEngineerInPlace();
                    }
                    m_SerializedSelf.Update();
                }

                if (GUILayout.Button("New...", GUILayout.Width(70)))
                {
                    CreateNewRampTexture();
                }
            }

            EditorGUILayout.Space(8);

            // 2) 贴图预览（带 checker 背景，照顾透明度）
            DrawTexturePreview();

            EditorGUILayout.Space(8);

            // 没贴图时下面所有控件不可交互
            bool hasTex = m_Ramp.rampTexture != null;
            using (new EditorGUI.DisabledScope(!hasTex))
            {
                // 2.5) 反推 Gradient (从图像生成)
                DrawReverseEngineerSection();
                EditorGUILayout.Space(6);

                // 3) Gradient 列表
                m_GradientsList.DoLayoutList();
                EditorGUILayout.Space(6);

                // 4) Single Ramp Size + Save / Close
                using (new EditorGUILayout.HorizontalScope())
                {
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel);
                    if (m_Ramp.singleRampSize.y > 64)
                        labelStyle.normal.textColor = new Color(0.95f, 0.4f, 0.3f);

                    GUILayout.Label("SingleRampSize:", labelStyle, GUILayout.Width(120));

                    EditorGUI.BeginChangeCheck();
                    Vector2Int s = EditorGUILayout.Vector2IntField(GUIContent.none,
                        m_Ramp.singleRampSize, GUILayout.Width(150));
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_Ramp.singleRampSize = new Vector2Int(
                            Mathf.Max(1, s.x),
                            Mathf.Max(1, s.y));
                    }

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Save", GUILayout.Width(80)))
                    {
                        if (m_Ramp.Save())
                        {
                            EditorUtility.SetDirty(m_Ramp.rampTexture);
                            ShowNotification(new GUIContent("Saved"));
                        }
                    }
                    if (GUILayout.Button("Close", GUILayout.Width(80)))
                    {
                        Close();
                    }
                }
            }

            m_SerializedSelf.ApplyModifiedProperties();
        }

        // ====================================================================
        // 反推 Gradient 区块
        // 自动识别成功 → 折叠默认收起，里面只有"模式 + 重新反推"
        // 自动识别失败 → 折叠默认展开，多一个"ramp 条数"手填框
        // ====================================================================
        void DrawReverseEngineerSection()
        {
            // 头部加状态后缀，方便 collapsed 时也能一眼看到识别结果
            string headerStatus = m_DetectedRampCount > 0
                ? $"  [✓ 已识别 {m_DetectedRampCount} 条，行高 {m_DetectedRowHeight}px]"
                : (m_Ramp.rampTexture != null ? "  [⚠ 未能自动识别条数]" : "");
            m_FoldReverse = EditorGUILayout.BeginFoldoutHeaderGroup(
                m_FoldReverse, "📥 反推参数 (Reverse-Engineer)" + headerStatus);
            if (m_FoldReverse)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    // 状态行：识别成功 / 失败 二选一
                    if (m_DetectedRampCount > 0)
                    {
                        EditorGUILayout.HelpBox(
                            $"已自动识别为 {m_DetectedRampCount} 条 ramp，行高 {m_DetectedRowHeight} 像素。",
                            MessageType.Info);
                    }
                    else if (m_Ramp.rampTexture != null)
                    {
                        EditorGUILayout.HelpBox(
                            "未能自动识别 ramp 条数（可能是带噪声的手绘图 / 渐变行高不一致）。\n" +
                            "请在下面手填条数；行高会按 H/条数 自动计算。",
                            MessageType.Warning);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("ramp 条数", GUILayout.Width(80));
                            EditorGUI.BeginChangeCheck();
                            int v = EditorGUILayout.IntField(m_ManualRampCount, GUILayout.Width(60));
                            if (EditorGUI.EndChangeCheck())
                            {
                                m_ManualRampCount = Mathf.Max(1, v);
                                // 同步给烘焙输出尺寸
                                if (m_Ramp.rampTexture != null)
                                {
                                    int rowH = Mathf.Max(1, m_Ramp.rampTexture.height / m_ManualRampCount);
                                    m_Ramp.singleRampSize = new Vector2Int(m_Ramp.rampTexture.width, rowH);
                                }
                            }
                            int rowHCalc = (m_Ramp.rampTexture != null)
                                ? Mathf.Max(1, m_Ramp.rampTexture.height / Mathf.Max(1, m_ManualRampCount))
                                : 1;
                            EditorGUILayout.LabelField($"  行高 = {rowHCalc} px", EditorStyles.miniLabel);
                        }
                    }

                    // 模式选择
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("模式", GUILayout.Width(60));
                        m_ReverseMode = (GradientsRamp.ReverseMode)EditorGUILayout.EnumPopup(
                            m_ReverseMode, GUILayout.Width(120));
                    }

                    string modeHint = m_ReverseMode == GradientsRamp.ReverseMode.Uniform
                        ? "等距采样：在 [0,1] 上均匀取 8 个点。结果稳定但可能错过陡变拐点。"
                        : "自适应：取颜色梯度最大的 8 个位置。能精确捕捉颜色突变（如卡通 ramp 的明暗交界）。";
                    EditorGUILayout.LabelField(modeHint, EditorStyles.miniLabel);

                    EditorGUILayout.Space(4);

                    if (GUILayout.Button(
                        new GUIContent("🔄 用当前参数重新反推",
                            "拖图时已经自动反推一次。改完上面的模式（或手填条数）后点这里用新参数重新生成。"),
                        GUILayout.Height(26)))
                    {
                        ReverseEngineerInPlace();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// 拖图后调用：扫描像素自动识别 ramp 条数。
        /// 识别成功 → 同步给 m_Ramp.singleRampSize.y，反推时直接用这个行高
        /// 识别失败 → 让用户手填 m_ManualRampCount
        /// </summary>
        void TryDetectRampLayout()
        {
            if (m_Ramp.rampTexture == null)
            {
                m_DetectedRampCount = -1;
                m_DetectedRowHeight = -1;
                m_FoldReverse = false;
                return;
            }

            var (count, rowH) = GradientsRamp.DetectRampLayout(m_Ramp.rampTexture);
            m_DetectedRampCount = count;
            m_DetectedRowHeight = rowH;

            if (count > 0)
            {
                // 识别成功：同步行高
                m_Ramp.singleRampSize = new Vector2Int(m_Ramp.rampTexture.width, rowH);
            }
            else
            {
                // 识别失败：默认 1 条 = 整图当一条，等用户主动展开折叠区改条数
                m_ManualRampCount = 1;
                m_Ramp.singleRampSize = new Vector2Int(m_Ramp.rampTexture.width, m_Ramp.rampTexture.height);
            }
            // 两种情况都默认收起折叠区（识别成功不需要调，失败时默认 1 多数也对，需要调时再展开）
            m_FoldReverse = false;
        }

        /// <summary>反推并直接覆盖 m_Ramp.gradients（拖图自动调用 + 手动按钮共用）</summary>
        void ReverseEngineerInPlace()
        {
            if (m_Ramp.rampTexture == null) return;

            // 决定行高：识别成功用识别值，否则按 H/手填条数
            int rowHeight;
            int rampCount;
            if (m_DetectedRampCount > 0)
            {
                rowHeight = m_DetectedRowHeight;
                rampCount = m_DetectedRampCount;
            }
            else
            {
                rampCount = Mathf.Max(1, m_ManualRampCount);
                rowHeight = Mathf.Max(1, m_Ramp.rampTexture.height / rampCount);
            }

            var generated = GradientsRamp.ReverseEngineerFromTexture(
                m_Ramp.rampTexture,
                rowHeight,
                k_ReverseSampleCount,
                m_ReverseMode,
                wholeImageAsOne: false);

            if (generated.Count == 0)
            {
                ShowNotification(new GUIContent("反推失败"));
                return;
            }

            m_Ramp.gradients = generated;
            // 同步烘焙输出尺寸
            m_Ramp.singleRampSize = new Vector2Int(m_Ramp.rampTexture.width, rowHeight);
            if (m_SerializedSelf != null) m_SerializedSelf.Update();
            Debug.Log($"[RampGenerator] 反推完成：{m_Ramp.rampTexture.name} 生成 {generated.Count} 条 Gradient（行高={rowHeight}，模式={m_ReverseMode}）");
        }

        void DrawTexturePreview()
        {
            if (m_Ramp.rampTexture == null)
            {
                EditorGUILayout.HelpBox("拖一张已有 ramp 贴图过来，或点 New... 新建一张", MessageType.Info);
                return;
            }

            var rect = EditorGUILayout.GetControlRect(true, 80);
            rect.xMin += 4;
            rect.xMax -= 4;

            // 纯色背景（ramp 是颜色 LUT，没有透明度，不需要棋盘格）
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.18f));

            // 临时把 ramp 贴图改成 Bilinear 避免预览像素化（绘完恢复）
            var oriFilter = m_Ramp.rampTexture.filterMode;
            m_Ramp.rampTexture.filterMode = FilterMode.Bilinear;
            // alphaBlend=false：忽略贴图自带的 alpha，直接显示 RGB
            GUI.DrawTexture(rect, m_Ramp.rampTexture, ScaleMode.StretchToFill, false);
            m_Ramp.rampTexture.filterMode = oriFilter;
        }

        void CreateNewRampTexture()
        {
            string folder = AssetDatabase.IsValidFolder(k_DefaultSaveFolder)
                ? k_DefaultSaveFolder
                : "Assets";

            string path = EditorUtility.SaveFilePanelInProject(
                "New Ramp Texture", "Ramp", "png",
                "Pick a save location for the new ramp PNG.",
                folder);

            if (string.IsNullOrEmpty(path)) return;

            // 写一张占位图，让 importer 出现，后面 Save() 会按 singleRampSize 覆盖
            var tex = GradientsRamp.CreatePlaceholderPng(path,
                m_Ramp.singleRampSize.x > 0 ? m_Ramp.singleRampSize : new Vector2Int(256, 4));

            // 默认填一条示例 Gradient，方便用户看到效果
            m_Ramp.rampTexture = tex;
            m_Ramp.gradients.Clear();
            m_Ramp.gradients.Add(GradientsRamp.CreateSampleGradient());

            // 立刻烘一次，让磁盘上的 PNG 不再是空白占位
            m_Ramp.Save();

            // 让 SerializedObject 重新读取
            m_SerializedSelf.Update();
            Repaint();
        }
    }
}
#endif
