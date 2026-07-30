using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// uGUI + TMP 统一构建工具（V0.4.6 UI 方案统一）。
    ///
    /// 全项目 UI 从 UITK 迁移到 uGUI+TMP：本 helper 提供一套代码化的
    /// Canvas / 面板 / 文本 / 按钮 构建 API，保证各面板视觉与中文字体一致。
    /// 中文依赖动态 TMP 字体资产 Resources/Fonts/"NotoSansSC SDF"（由
    /// 「仙途秘境/UI/生成中文 TMP 字体资产」菜单生成）。
    ///
    /// 用法：CreateOverlayCanvas → CreatePanel → CreateText / CreateButton。
    /// EventSystem 由 Demo1Setup 用 InputSystemUIInputModule 创建；此处兜底。
    /// </summary>
    public static class UGuiKit
    {
        // ---------- 主题色 ----------
        public static readonly Color Scrim = new Color(0.03f, 0.03f, 0.05f, 0.86f);   // 全屏遮罩
        public static readonly Color Panel = new Color(0.10f, 0.11f, 0.14f, 0.98f);   // 面板底
        public static readonly Color PanelEdge = new Color(0.22f, 0.24f, 0.30f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.82f, 0.45f, 1f);       // 标题金
        public static readonly Color TextMain = new Color(0.92f, 0.93f, 0.96f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.65f, 0.72f, 1f);
        public static readonly Color BtnNormal = new Color(0.16f, 0.18f, 0.23f, 1f);
        public static readonly Color BtnPrimary = new Color(0.20f, 0.36f, 0.52f, 1f);
        public static readonly Color BtnWarn = new Color(0.42f, 0.20f, 0.22f, 1f);
        public static readonly Color BtnDisabled = new Color(0.12f, 0.12f, 0.14f, 1f);

        private static TMP_FontAsset _cjkFont;
        private static bool _triedFont;

        /// <summary>中文 TMP 字体资产（动态图集）。所有文本统一使用，避免 □□□。</summary>
        public static TMP_FontAsset CjkFont
        {
            get
            {
                if (_triedFont) return _cjkFont;
                _triedFont = true;
                _cjkFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansSC SDF");
                if (_cjkFont == null)
                    Debug.LogWarning("[UGuiKit] 未找到 Resources/Fonts/NotoSansSC SDF，中文将无法显示。请执行菜单「仙途秘境/UI/生成中文 TMP 字体资产」。");
                return _cjkFont;
            }
        }

        // ---------- 基础设施 ----------

        /// <summary>创建一个屏幕空间 Overlay Canvas（含缩放器与射线检测），并兜底 EventSystem。</summary>
        public static Canvas CreateOverlayCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        /// <summary>确保场景中存在 EventSystem（新版 Input System 输入模块）。</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ---------- 布局元件 ----------

        /// <summary>全屏铺满的子 RectTransform（四角锚定拉伸）。</summary>
        public static RectTransform CreateStretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>全屏半透明遮罩（可点击拦截背后交互）。</summary>
        public static Image CreateScrim(Transform parent, Color? color = null, bool raycastBlock = true)
        {
            var rt = CreateStretch(parent, "Scrim");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color ?? Scrim;
            img.raycastTarget = raycastBlock;
            return img;
        }

        /// <summary>居中固定尺寸面板（带描边背景）。</summary>
        public static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bg ?? Panel;
            return rt;
        }

        /// <summary>给容器加纵向自动布局。</summary>
        public static VerticalLayoutGroup AddVLayout(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperCenter, bool cChildW = true, bool cChildH = false)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = cChildW;
            v.childControlHeight = cChildH;
            v.childForceExpandWidth = cChildW;
            v.childForceExpandHeight = false;
            return v;
        }

        // ---------- 文本 ----------

        public static TextMeshProUGUI CreateText(Transform parent, string text, int fontSize,
            Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (CjkFont != null) tmp.font = CjkFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color ?? TextMain;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ---------- 按钮 ----------

        /// <summary>标准按钮（Image + Button + TMP 子标签），返回 Button；label 通过 out 取回可动态改文本。</summary>
        public static Button CreateButton(Transform parent, string text, UnityAction onClick,
            Color? bg = null, int fontSize = 30, Vector2? size = null)
        {
            Button btn = CreateButton(parent, text, onClick, out _, bg, fontSize, size);
            return btn;
        }

        public static Button CreateButton(Transform parent, string text, UnityAction onClick,
            out TextMeshProUGUI label, Color? bg = null, int fontSize = 30, Vector2? size = null)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size ?? new Vector2(360f, 56f);

            var img = go.GetComponent<Image>();
            Color baseColor = bg ?? BtnNormal;
            img.color = baseColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(onClick);

            // 让 Image 颜色本身承载语义色，ColorBlock 用乘算高亮
            img.color = baseColor;

            label = CreateText(rt, text, fontSize, TextMain, TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-12f, 0f);
            return btn;
        }

        /// <summary>为按钮设置可用/禁用状态（同时改底色，保证 disabled 视觉清晰）。</summary>
        public static void SetButtonEnabled(Button btn, bool enabled, Color? enabledColor = null)
        {
            if (btn == null) return;
            btn.interactable = enabled;
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = enabled ? (enabledColor ?? BtnNormal) : BtnDisabled;
        }

        // ---------- 更多容器 ----------

        /// <summary>给容器加横向自动布局。</summary>
        public static HorizontalLayoutGroup AddHLayout(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft, bool cChildW = false, bool cChildH = true)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = cChildW;
            h.childControlHeight = cChildH;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>一个空的横向行容器（默认加 HLayout）。</summary>
        public static RectTransform CreateRow(Transform parent, float spacing = 8f, float height = 40f)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0f, height);
            AddHLayout(rt, spacing);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;
            return rt;
        }

        /// <summary>纯色块（背景/描边/填充条底）。</summary>
        public static Image CreateBox(Transform parent, Color color, Vector2 size)
        {
            var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go.GetComponent<Image>();
        }

        /// <summary>
        /// 竖直滚动视图：返回可放内容的 content（已挂 VerticalLayoutGroup + ContentSizeFitter）。
        /// root 为传入 parent 下新建、可自行调 RectTransform 尺寸/锚点。
        /// </summary>
        public static RectTransform CreateScroll(Transform parent, string name, out ScrollRect scroll, float spacing = 8f, RectOffset padding = null)
        {
            var rootGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(parent, false);
            rootGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 近透明，供 mask/raycast

            var contentGo = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentGo.transform;
            content.SetParent(root, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(8, 8, 8, 8);
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true; v.childForceExpandWidth = true;
            v.childControlHeight = true; v.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = rootGo.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = root;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return content;
        }

        // ---------- 控件 ----------

        /// <summary>水平滑条（0~1）。</summary>
        public static Slider CreateSlider(Transform parent, float value, UnityAction<float> onChange,
            float width = 300f, float height = 18f)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, height);

            var bg = CreateBox(rt, new Color(0.06f, 0.07f, 0.09f, 1f), Vector2.zero);
            var bgrt = (RectTransform)bg.transform; bgrt.name = "Background";
            Stretch(bgrt);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            var fart = (RectTransform)fillArea.transform; fart.SetParent(rt, false);
            Stretch(fart); fart.offsetMin = new Vector2(2f, 2f); fart.offsetMax = new Vector2(-2f, -2f);
            var fill = CreateBox(fart, BtnPrimary, Vector2.zero);
            var fillrt = (RectTransform)fill.transform; fillrt.name = "Fill";
            fillrt.anchorMin = Vector2.zero; fillrt.anchorMax = new Vector2(0f, 1f);
            fillrt.sizeDelta = new Vector2(10f, 0f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            var hart = (RectTransform)handleArea.transform; hart.SetParent(rt, false);
            Stretch(hart); hart.offsetMin = new Vector2(2f, 0f); hart.offsetMax = new Vector2(-2f, 0f);
            var handle = CreateBox(hart, Gold, new Vector2(14f, 0f));
            var hrt = (RectTransform)handle.transform; hrt.name = "Handle";
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f);

            var s = go.GetComponent<Slider>();
            s.fillRect = fillrt;
            s.handleRect = hrt;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.value = value;
            if (onChange != null) s.onValueChanged.AddListener(onChange);
            return s;
        }

        /// <summary>开关（复选框 + 标签）。</summary>
        public static Toggle CreateToggle(Transform parent, string label, bool value, UnityAction<bool> onChange, int fontSize = 22)
        {
            var go = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(240f, 30f);

            var box = CreateBox(rt, new Color(0.06f, 0.07f, 0.09f, 1f), new Vector2(24f, 24f));
            var boxrt = (RectTransform)box.transform; boxrt.name = "Background";
            boxrt.anchorMin = new Vector2(0f, 0.5f); boxrt.anchorMax = new Vector2(0f, 0.5f);
            boxrt.pivot = new Vector2(0f, 0.5f);
            boxrt.anchoredPosition = new Vector2(0f, 0f);

            var check = CreateBox(boxrt, Gold, new Vector2(16f, 16f));
            var crt = (RectTransform)check.transform; crt.name = "Checkmark";
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;

            var lbl = CreateText(rt, label, fontSize, TextMain, TextAlignmentOptions.Left);
            var lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(34f, 0f); lrt.offsetMax = new Vector2(0f, 0f);

            var t = go.GetComponent<Toggle>();
            t.targetGraphic = box;
            t.graphic = check;
            t.isOn = value;
            if (onChange != null) t.onValueChanged.AddListener(onChange);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 卡牌：外层为 accent 描边框，内层深色底 + 纵向布局。返回内层 content（放标题/描述/按钮）。
        /// 固定尺寸，便于放进横向行；内层已挂 VerticalLayoutGroup。
        /// </summary>
        public static RectTransform CreateCard(Transform parent, Vector2 size, Color accent, float border = 2f)
        {
            var frameGo = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var frame = (RectTransform)frameGo.transform;
            frame.SetParent(parent, false);
            frame.sizeDelta = size;
            frameGo.GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.85f);
            var le = frameGo.GetComponent<LayoutElement>();
            le.preferredWidth = size.x; le.minWidth = size.x;
            le.preferredHeight = size.y; le.minHeight = size.y;

            var innerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            var inner = (RectTransform)innerGo.transform;
            inner.SetParent(frame, false);
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(border, border);
            inner.offsetMax = new Vector2(-border, -border);
            innerGo.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.17f, 1f);

            var v = innerGo.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.padding = new RectOffset(16, 16, 16, 16);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childForceExpandWidth = true;
            v.childControlHeight = true; v.childForceExpandHeight = false;
            return inner;
        }

        /// <summary>网格容器（固定列数，GridLayoutGroup）。作为纵向布局子项时会自动求高。</summary>
        public static RectTransform CreateGrid(Transform parent, Vector2 cell, Vector2 spacing, int columns)
        {
            var go = new GameObject("Grid", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperLeft;
            return rt;
        }

        /// <summary>小属性卡（顶部小标签 + 底部彩色数值），供信息面板/图鉴复用。</summary>
        public static RectTransform CreateStatCard(Transform parent, string label, string value, Color valueColor)
        {
            var go = new GameObject("StatCard", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.15f, 0.9f);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(10, 10, 6, 6); v.spacing = 2f;
            v.childAlignment = TextAnchor.MiddleLeft;
            v.childControlWidth = true; v.childForceExpandWidth = true;
            v.childControlHeight = true; v.childForceExpandHeight = false;

            var l = CreateText(rt, label, 12, new Color(0.6f, 0.62f, 0.68f), TextAlignmentOptions.Left);
            SetHeight(l, 16f);
            var val = CreateText(rt, value, 17, valueColor, TextAlignmentOptions.Left, FontStyles.Bold);
            SetHeight(val, 24f);
            return rt;
        }

        /// <summary>分节标题（金色粗体 + 底部细线）。</summary>
        public static TextMeshProUGUI CreateSectionTitle(Transform parent, string text)
        {
            var l = CreateText(parent, text, 16, new Color(0.75f, 0.8f, 0.9f), TextAlignmentOptions.Left, FontStyles.Bold);
            SetHeight(l, 28f);
            return l;
        }

        /// <summary>横向卡牌行容器（居中）。</summary>
        public static RectTransform CreateCardRow(Transform parent, float spacing = 24f)
        {
            var go = new GameObject("CardRow", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false; h.childForceExpandWidth = false;
            h.childControlHeight = false; h.childForceExpandHeight = false;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        /// <summary>给任意元素挂 LayoutElement 固定高度（用于 VerticalLayoutGroup 子项）。</summary>
        public static LayoutElement SetHeight(Component c, float h, float minH = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = minH >= 0f ? minH : h;
            return le;
        }
    }
}
