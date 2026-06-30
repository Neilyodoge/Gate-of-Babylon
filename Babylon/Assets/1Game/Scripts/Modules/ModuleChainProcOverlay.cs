using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// 在技能栏 Q/E/R 槽位上叠加模块链状态指示。
    /// · Active 模式 + Proc'd → 闪烁绿框 + "按 Q/E/R"
    /// · Passive 模式 + Proc'd → 瞬间消费（不会停留在 READY）
    /// · CD 中 → 红色倒计时
    /// · 积累中 → "2/3" 进度
    /// · 链名标签显示在槽位下方
    /// </summary>
    public class ModuleChainProcOverlay : MonoBehaviour
    {
        private RectTransform[] _skillSlots;
        private GameObject[] _overlays = new GameObject[3];
        private Text[] _overlayTexts = new Text[3];
        private Image[] _overlayBgs = new Image[3];
        private Text[] _chainLabels = new Text[3];
        private float _pulseTimer;

        private static readonly string[] SlotKeys = { "Q", "E", "R" };

        public void SetSkillSlots(RectTransform[] slots) { _skillSlots = slots; }

        private void Start()
        {
            if (_skillSlots == null) return;

            for (int i = 0; i < 3 && i < _skillSlots.Length; i++)
            {
                if (_skillSlots[i] == null) continue;

                var go = new GameObject($"ModuleProcOverlay_{i}");
                go.transform.SetParent(_skillSlots[i], false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-2, -2);
                rt.offsetMax = new Vector2(2, 2);

                var img = go.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                img.raycastTarget = false;
                _overlayBgs[i] = img;

                var textGo = new GameObject("ProcText");
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0, 0.5f);
                trt.anchorMax = new Vector2(1, 1);
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<Text>();
                text.fontSize = 11;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = FontStyle.Bold;
                text.raycastTarget = false;
                text.color = Color.white;
                text.supportRichText = true;
                var outline = textGo.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.9f);
                outline.effectDistance = new Vector2(1, -1);
                _overlayTexts[i] = text;

                _overlays[i] = go;
                go.SetActive(false);

                var parent = _skillSlots[i].parent;
                if (parent != null)
                {
                    var labelTf = parent.Find($"ChainLabel_{i}");
                    if (labelTf != null)
                        _chainLabels[i] = labelTf.GetComponent<Text>();
                }
            }
        }

        private void Update()
        {
            var player = PlayerController.Instance;
            if (player == null) return;

            var slots = player.GetComponent<ModuleSlotManager>();
            if (slots == null) return;

            _pulseTimer += Time.deltaTime;

            for (int i = 0; i < 3; i++)
            {
                if (_overlays[i] == null) continue;

                if (!slots.HasChain(i))
                {
                    _overlays[i].SetActive(false);
                    if (_chainLabels[i] != null) _chainLabels[i].text = "";
                    continue;
                }

                _overlays[i].SetActive(true);

                if (_chainLabels[i] != null)
                {
                    var chain = slots.GetChain(i);
                    _chainLabels[i].text = chain != null ? chain.DisplayName : "";
                }

                var tracker = slots.GetTracker(i);
                if (tracker == null) continue;

                var kind = slots.GetConsumeKind(i);
                bool auto = kind == ConsumeKind.Auto;

                if (tracker.CooldownRemaining > 0f)
                {
                    _overlayTexts[i].text = $"<color=#ff6666>{tracker.CooldownRemaining:F1}s</color>";
                    _overlayBgs[i].color = new Color(0.3f, 0.1f, 0.1f, 0.4f);
                }
                else if (tracker.IsProc && !auto)
                {
                    float pulse = Mathf.Sin(_pulseTimer * 6f) * 0.5f + 0.5f;
                    _overlayBgs[i].color = new Color(0f, 1f, 0.5f, 0.15f + pulse * 0.25f);
                    string hint = kind == ConsumeKind.Stacks
                        ? $"<color=#00ff88>按{SlotKeys[i]} ×{tracker.CurrentStacks}</color>"
                        : $"<color=#00ff88>按{SlotKeys[i]}</color>";
                    _overlayTexts[i].text = hint;
                }
                else if (!tracker.IsProc)
                {
                    int cur = tracker.CurrentStacks;
                    int max = tracker.Threshold;
                    string modeHint = auto ? "AUTO" : "●";
                    _overlayTexts[i].text = $"<color=#aaaaaa>{modeHint} {cur}/{max}</color>";
                    _overlayBgs[i].color = new Color(0.1f, 0.1f, 0.2f, 0.3f);
                }
                else
                {
                    _overlayTexts[i].text = "<color=#88ff88>○ AUTO</color>";
                    _overlayBgs[i].color = new Color(0.1f, 0.2f, 0.1f, 0.3f);
                }
            }
        }
    }
}
