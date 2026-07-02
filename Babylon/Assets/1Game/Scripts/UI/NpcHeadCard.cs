using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// NPC 头顶简易标签（v0.3.3 简化版）。
    ///
    /// 头顶两行：
    ///   - 主名称（始终显示）：图标 + 名称（主题色 + 描边）
    ///   - 按 F 提示（靠近时显示）：主题色小条 + 白字
    ///
    /// 没有复杂背景 / 光柱 / 浮动动画，就一个纯文字标签。
    /// </summary>
    public class NpcHeadCard : MonoBehaviour
    {
        public struct Config
        {
            public string displayName;     // "散修商人"
            public string icon;            // "✦" / "📜"
            public string roleSub;         // 例："模块配置" —— 显示为括号副标题
            public string hintText;        // "按 [F] 交易"
            public Color themeColor;       // 主题色
            public float yOffset;          // 头顶高度（默认 2.4m）
            public bool showLongRangeMarker; // 兼容字段，简化版忽略
        }

        private GameObject _nameCanvas;
        private GameObject _hintCanvas;

        public static NpcHeadCard Attach(Transform host, Config cfg)
        {
            if (host == null) return null;
            var go = new GameObject("NpcHeadCard");
            go.transform.SetParent(host, false);
            go.transform.localPosition = Vector3.zero;
            var card = go.AddComponent<NpcHeadCard>();
            card.Build(cfg);
            return card;
        }

        private void Build(Config cfg)
        {
            if (cfg.themeColor.a <= 0.01f) cfg.themeColor.a = 1f;
            float yOffset = cfg.yOffset <= 0.01f ? 2.4f : cfg.yOffset;

            BuildName(cfg, yOffset);
            BuildHint(cfg, yOffset);
        }

        // ============= 主名称：图标 + 名称（必要时附副标题） =============

        private void BuildName(Config cfg, float yOffset)
        {
            var canvasGo = new GameObject("NameLabel");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, yOffset, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(5f, 0.7f);
            canvasGo.transform.localScale = Vector3.one * 0.03f;

            string labelText;
            if (!string.IsNullOrEmpty(cfg.icon) && !string.IsNullOrEmpty(cfg.roleSub))
                labelText = $"{cfg.icon} {cfg.displayName}（{cfg.roleSub}）";
            else if (!string.IsNullOrEmpty(cfg.icon))
                labelText = $"{cfg.icon} {cfg.displayName}";
            else if (!string.IsNullOrEmpty(cfg.roleSub))
                labelText = $"{cfg.displayName}（{cfg.roleSub}）";
            else
                labelText = cfg.displayName;

            CreateText(canvasGo.transform, "Text", labelText, 22, cfg.themeColor, FontStyle.Bold);

            canvasGo.AddComponent<BillboardUI>();
            _nameCanvas = canvasGo;
        }

        // ============= 按 F 提示（默认隐藏） =============

        private void BuildHint(Config cfg, float yOffset)
        {
            var canvasGo = new GameObject("HintLabel");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, yOffset + 0.55f, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(4f, 0.55f);
            canvasGo.transform.localScale = Vector3.one * 0.028f;

            // 主题色实心小条做背景，更显眼一点
            var bg = new GameObject("BG");
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.1f, 0.05f);
            bgRT.anchorMax = new Vector2(0.9f, 0.95f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            var bgC = cfg.themeColor;
            bgC.a = 0.75f;
            bgImg.color = bgC;
            bgImg.raycastTarget = false;

            string hint = string.IsNullOrEmpty(cfg.hintText) ? "按 [F] 互动" : cfg.hintText;
            CreateText(canvasGo.transform, "Text", hint, 18, Color.white, FontStyle.Bold);

            canvasGo.AddComponent<BillboardUI>();
            _hintCanvas = canvasGo;
            _hintCanvas.SetActive(false);
        }

        // ============= 公共 API =============

        public void SetHintVisible(bool visible)
        {
            if (_hintCanvas != null && _hintCanvas.activeSelf != visible)
                _hintCanvas.SetActive(visible);
        }

        public void SetCardVisible(bool visible)
        {
            if (_nameCanvas != null) _nameCanvas.SetActive(visible);
        }

        public void UpdateName(string newName)
        {
            if (_nameCanvas == null) return;
            var t = _nameCanvas.transform.Find("Text");
            if (t != null)
            {
                var txt = t.GetComponent<Text>();
                if (txt != null) txt.text = newName;
            }
        }

        public void UpdateHintText(string newHint)
        {
            if (_hintCanvas == null) return;
            var t = _hintCanvas.transform.Find("Text");
            if (t != null)
            {
                var txt = t.GetComponent<Text>();
                if (txt != null) txt.text = newHint;
            }
        }

        // ============= helper =============

        private static void CreateText(Transform parent, string name, string content, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }
    }
}
