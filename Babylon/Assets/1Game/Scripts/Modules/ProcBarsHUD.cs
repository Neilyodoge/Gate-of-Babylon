using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// GDD 5.13 增强就绪视觉标识——角色旁三竖条（Q/E/R）。
    /// 屏幕空间跟随玩家世界坐标，显示每条链的 Proc 状态：
    /// - 无链：不显示该竖条。
    /// - 充能中：竖条按阈值/层数进度部分填充（暗色）。
    /// - 已就绪（Proc）：满格 + 元素色 + 发光脉冲；Stacks 显示层数，Window 随倒计时回落。
    /// - 冷却中：竖条由低到高回升（红色）。
    /// - Auto：呼吸式微亮（自动释放，无需按键）。
    /// 竖条颜色取效果器元素；底部 Q/E/R 键名在就绪时高亮。
    /// 挂在 ScreenSpaceOverlay 的 GameCanvas 上。
    /// </summary>
    public class ProcBarsHUD : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _canvasRT;
        private Camera _cam;
        private RectTransform _container;

        private class Bar
        {
            public GameObject go;
            public RectTransform rt;
            public Image bg;
            public Image fill;        // 纵向填充
            public Image glow;        // 就绪脉冲外发光
            public Text keyLabel;     // Q/E/R
            public Text countLabel;   // 层数/倒计时
        }
        private readonly Bar[] _bars = new Bar[3];

        private float _pulse;

        private static readonly string[] Keys = { "Q", "E", "R" };
        private static readonly Color[] KeyColors =
        {
            new Color(0.4f, 0.8f, 1f),
            new Color(1f, 0.6f, 0.35f),
            new Color(0.7f, 0.65f, 1f),
        };

        // 屏幕空间：角色右侧偏移（参考分辨率 1920×1080 像素）
        private const float OffsetX = 95f;   // 右移
        private const float OffsetY = 24f;   // 上移
        private const float BarW = 20f;
        private const float BarH = 82f;
        private const float BarGap = 7f;

        private void Start()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return;
            _canvasRT = _canvas.transform as RectTransform;
            _cam = Camera.main;

            BuildBars();
        }

        private void BuildBars()
        {
            var containerGo = new GameObject("ProcBarsContainer");
            containerGo.transform.SetParent(_canvas.transform, false);
            _container = containerGo.AddComponent<RectTransform>();
            _container.sizeDelta = new Vector2(3 * BarW + 2 * BarGap, BarH + 18f);
            var cg = containerGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            float totalW = 3 * BarW + 2 * BarGap;
            for (int i = 0; i < 3; i++)
            {
                var bar = new Bar();
                float x = -totalW * 0.5f + i * (BarW + BarGap) + BarW * 0.5f;

                bar.go = new GameObject($"ProcBar_{i}");
                bar.go.transform.SetParent(_container, false);
                bar.rt = bar.go.AddComponent<RectTransform>();
                bar.rt.sizeDelta = new Vector2(BarW, BarH);
                bar.rt.anchoredPosition = new Vector2(x, 9f);

                // 外发光（就绪脉冲）
                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(bar.go.transform, false);
                var grt = glowGo.AddComponent<RectTransform>();
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(-5, -5); grt.offsetMax = new Vector2(5, 5);
                bar.glow = glowGo.AddComponent<Image>();
                bar.glow.color = new Color(0, 0, 0, 0);
                bar.glow.raycastTarget = false;

                // 背景
                bar.bg = bar.go.AddComponent<Image>();
                bar.bg.color = new Color(0.08f, 0.09f, 0.13f, 0.85f);
                bar.bg.raycastTarget = false;
                var outline = bar.go.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                // 填充（纵向，底部起）
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bar.go.transform, false);
                var frt = fillGo.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(-2, -2);
                bar.fill = fillGo.AddComponent<Image>();
                bar.fill.color = new Color(0.3f, 0.8f, 0.5f, 0.9f);
                bar.fill.raycastTarget = false;
                bar.fill.type = Image.Type.Filled;
                bar.fill.fillMethod = Image.FillMethod.Vertical;
                bar.fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                bar.fill.fillAmount = 0f;
                // 纯色填充需要一个 sprite；用内置 UI sprite
                bar.fill.sprite = BuiltinSprite();
                bar.glow.sprite = bar.fill.sprite;

                // 层数/倒计时（竖条中部）
                var countGo = new GameObject("Count");
                countGo.transform.SetParent(bar.go.transform, false);
                var crt = countGo.AddComponent<RectTransform>();
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
                bar.countLabel = countGo.AddComponent<Text>();
                bar.countLabel.font = UIBuiltins.LegacyFont;
                bar.countLabel.fontSize = 14;
                bar.countLabel.fontStyle = FontStyle.Bold;
                bar.countLabel.alignment = TextAnchor.MiddleCenter;
                bar.countLabel.raycastTarget = false;
                bar.countLabel.color = Color.white;
                var co = countGo.AddComponent<Outline>();
                co.effectColor = new Color(0, 0, 0, 0.9f);
                co.effectDistance = new Vector2(1, -1);

                // 键名（竖条下方）
                var keyGo = new GameObject("Key");
                keyGo.transform.SetParent(bar.go.transform, false);
                var krt = keyGo.AddComponent<RectTransform>();
                krt.anchorMin = new Vector2(0, 0); krt.anchorMax = new Vector2(1, 0);
                krt.pivot = new Vector2(0.5f, 1f);
                krt.anchoredPosition = new Vector2(0, -1f);
                krt.sizeDelta = new Vector2(BarW + 8f, 16f);
                bar.keyLabel = keyGo.AddComponent<Text>();
                bar.keyLabel.font = UIBuiltins.LegacyFont;
                bar.keyLabel.fontSize = 13;
                bar.keyLabel.fontStyle = FontStyle.Bold;
                bar.keyLabel.alignment = TextAnchor.MiddleCenter;
                bar.keyLabel.raycastTarget = false;
                bar.keyLabel.text = Keys[i];
                bar.keyLabel.color = KeyColors[i];
                var ko = keyGo.AddComponent<Outline>();
                ko.effectColor = new Color(0, 0, 0, 0.9f);
                ko.effectDistance = new Vector2(1, -1);

                _bars[i] = bar;
            }
        }

        private void Update()
        {
            if (_container == null) return;
            var player = PlayerController.Instance;
            if (player == null) { _container.gameObject.SetActive(false); return; }
            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots == null) { _container.gameObject.SetActive(false); return; }

            if (_cam == null) _cam = Camera.main;
            if (_cam == null) { _container.gameObject.SetActive(false); return; }

            // 跟随玩家：世界坐标 → 屏幕坐标 → Canvas 本地坐标
            Vector3 head = player.transform.position + Vector3.up * 1.6f;
            Vector3 sp = _cam.WorldToScreenPoint(head);
            if (sp.z < 0f) { _container.gameObject.SetActive(false); return; }
            _container.gameObject.SetActive(true);

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, sp, _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam, out local);
            _container.anchoredPosition = local + new Vector2(OffsetX, OffsetY);

            _pulse += Time.deltaTime;
            float pulse01 = Mathf.Sin(_pulse * 6f) * 0.5f + 0.5f;

            bool anyVisible = false;
            for (int i = 0; i < 3; i++)
            {
                var bar = _bars[i];
                if (bar == null) continue;

                if (!slots.HasChain(i))
                {
                    bar.go.SetActive(false);
                    continue;
                }
                bar.go.SetActive(true);
                anyVisible = true;

                var tracker = slots.GetTracker(i);
                var kind = slots.GetConsumeKind(i);
                var cfg = slots.GetConfig(i);
                Color elem = ElementColor(cfg.elementTag);

                UpdateBar(bar, tracker, kind, elem, pulse01);
            }

            if (!anyVisible) _container.gameObject.SetActive(false);
        }

        private void UpdateBar(Bar bar, TriggerTracker t, ConsumeKind kind, Color elem, float pulse01)
        {
            bar.countLabel.text = "";
            bar.glow.color = new Color(0, 0, 0, 0);

            if (t == null)
            {
                bar.fill.fillAmount = 0f;
                bar.keyLabel.fontStyle = FontStyle.Normal;
                return;
            }

            // 冷却中：红色由低到高回升
            if (t.CooldownRemaining > 0f && kind != ConsumeKind.Stacks)
            {
                float cd = t.CooldownTotal > 0.01f ? 1f - t.CooldownRemaining / t.CooldownTotal : 1f;
                bar.fill.fillAmount = Mathf.Clamp01(cd);
                bar.fill.color = new Color(0.7f, 0.25f, 0.25f, 0.85f);
                bar.keyLabel.color = new Color(0.6f, 0.4f, 0.4f);
                bar.keyLabel.fontStyle = FontStyle.Normal;
                bar.countLabel.color = new Color(1f, 0.7f, 0.7f);
                bar.countLabel.text = $"{t.CooldownRemaining:F0}";
                return;
            }

            bool proc = t.IsProc;

            if (proc)
            {
                float amount = 1f;
                if (kind == ConsumeKind.Window)
                    amount = t.WindowTotal > 0.01f ? Mathf.Clamp01(t.WindowRemaining / t.WindowTotal) : 1f;
                else if (kind == ConsumeKind.Stacks)
                    amount = t.MaxStacks > 0 ? Mathf.Clamp01((float)t.CurrentStacks / t.MaxStacks) : 1f;

                bar.fill.fillAmount = amount;
                Color c = elem;
                bar.fill.color = new Color(c.r, c.g, c.b, 0.85f + pulse01 * 0.15f);
                bar.glow.color = new Color(c.r, c.g, c.b, 0.25f + pulse01 * 0.45f);
                bar.keyLabel.color = Color.Lerp(KeyColorFor(bar), Color.white, pulse01);
                bar.keyLabel.fontStyle = FontStyle.Bold;

                if (kind == ConsumeKind.Stacks)
                    bar.countLabel.text = $"<color=#ffffff>{t.CurrentStacks}</color>";
                else if (kind == ConsumeKind.Window)
                    bar.countLabel.text = $"<color=#ffffff>{t.WindowRemaining:F0}</color>";
                return;
            }

            // 未就绪
            if (kind == ConsumeKind.Auto)
            {
                // Auto：呼吸式微亮（自动释放）
                float a = 0.25f + pulse01 * 0.35f;
                bar.fill.fillAmount = 0.5f + pulse01 * 0.2f;
                bar.fill.color = new Color(elem.r, elem.g, elem.b, a);
                bar.keyLabel.color = new Color(0.6f, 0.85f, 0.6f);
                bar.keyLabel.fontStyle = FontStyle.Normal;
                bar.countLabel.color = new Color(0.7f, 1f, 0.7f);
                bar.countLabel.text = "A";
                return;
            }

            // 充能中：按阈值/层数进度部分填充（暗）
            float progress = 0f;
            if (kind == ConsumeKind.Stacks)
                progress = t.MaxStacks > 0 ? (float)t.CurrentStacks / t.MaxStacks : 0f;
            else if (t.Threshold > 1)
                progress = (float)t.ThresholdProgress / t.Threshold;

            bar.fill.fillAmount = Mathf.Clamp01(progress);
            bar.fill.color = new Color(elem.r * 0.5f, elem.g * 0.5f, elem.b * 0.5f, 0.55f);
            bar.keyLabel.color = new Color(0.55f, 0.58f, 0.66f);
            bar.keyLabel.fontStyle = FontStyle.Normal;
            if (t.Threshold > 1 && kind != ConsumeKind.Stacks && progress > 0f)
                bar.countLabel.text = $"<color=#cccccc>{t.ThresholdProgress}/{t.Threshold}</color>";
        }

        private Color KeyColorFor(Bar bar)
        {
            for (int i = 0; i < 3; i++) if (_bars[i] == bar) return KeyColors[i];
            return Color.white;
        }

        private static Color ElementColor(ElementTag e) => e switch
        {
            ElementTag.Fire    => new Color(1f, 0.45f, 0.2f),
            ElementTag.Ice     => new Color(0.5f, 0.85f, 1f),
            ElementTag.Thunder => new Color(0.7f, 0.6f, 1f),
            ElementTag.Wind    => new Color(0.6f, 1f, 0.7f),
            ElementTag.Wood    => new Color(0.5f, 0.85f, 0.4f),
            ElementTag.Water   => new Color(0.35f, 0.65f, 1f),
            ElementTag.Earth   => new Color(0.85f, 0.7f, 0.4f),
            ElementTag.Pierce  => new Color(0.85f, 0.9f, 1f),
            ElementTag.Life    => new Color(0.5f, 1f, 0.6f),
            _ => new Color(0.4f, 0.9f, 0.6f),
        };

        private static Sprite _builtin;
        private static Sprite BuiltinSprite()
        {
            if (_builtin != null) return _builtin;
            // 1×1 白色 sprite，作为纯色填充底图
            var tex = Texture2D.whiteTexture;
            _builtin = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _builtin;
        }
    }
}
