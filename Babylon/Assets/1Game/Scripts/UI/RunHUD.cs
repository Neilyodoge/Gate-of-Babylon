using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 搜打撤 HUD（v0.5）—— 右侧常驻 UI，分两块：
    ///
    /// 1. 局内：本局 CaveInventory 缓冲（拾到但还没撤离的洞府素材）+ 一句"撤离才能带回"提示
    /// 2. 局外：洞府灵气 + 魂伤剩余倒计时
    ///
    /// 拾取时弹一条飘字（监听 CaveMaterialPickedUp 事件）。
    /// </summary>
    public class RunHUD : MonoBehaviour
    {
        private static RunHUD _instance;
        public static RunHUD Instance => _instance;

        // 拾取飘字队列
        private struct PickupToast
        {
            public string text;
            public Color color;
            public float remaining;
        }
        private readonly List<PickupToast> _toasts = new();
        private const float ToastDuration = 2.5f;

        private GUIStyle _bgStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _toastStyle;
        private bool _stylesReady;

        public static void Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("RunHUD");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RunHUD>();
            }
        }

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.CaveMaterialPickedUp>(OnCaveMaterialPickedUp);
            GameEvents.Subscribe<GameEvents.ExtractSuccess>(OnExtractSuccess);
            GameEvents.Subscribe<GameEvents.ExtractInterrupted>(OnExtractInterrupted);
            GameEvents.Subscribe<GameEvents.InsightChanged>(OnInsightChanged);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.CaveMaterialPickedUp>(OnCaveMaterialPickedUp);
            GameEvents.Unsubscribe<GameEvents.ExtractSuccess>(OnExtractSuccess);
            GameEvents.Unsubscribe<GameEvents.ExtractInterrupted>(OnExtractInterrupted);
            GameEvents.Unsubscribe<GameEvents.InsightChanged>(OnInsightChanged);
        }

        // 经验飘字降噪：连续 InsightChanged 在 1.5s 窗口内合并，且每 5 次 Delta 才弹一次
        private int _insightAccumDelta;
        private int _insightAccumCount;
        private int _insightLatestValue;
        private float _insightFlushTimer;
        private const int InsightFlushCount = 5;
        private const float InsightFlushWindow = 1.5f;

        private void OnInsightChanged(GameEvents.InsightChanged evt)
        {
            // 累计当前批次
            _insightAccumDelta += evt.Delta;
            _insightAccumCount++;
            _insightLatestValue = evt.NewRunInsight;
            _insightFlushTimer = InsightFlushWindow;

            // 达到 InsightFlushCount 次合并 → 弹一条
            if (_insightAccumCount >= InsightFlushCount)
            {
                FlushInsightToast();
            }
        }

        private void FlushInsightToast()
        {
            if (_insightAccumCount <= 0) return;
            string text = _insightAccumCount == 1
                ? $"+{_insightAccumDelta} 经验（累计 {_insightLatestValue}）"
                : $"+{_insightAccumDelta} 经验 ×{_insightAccumCount}（累计 {_insightLatestValue}）";
            _toasts.Add(new PickupToast
            {
                text = text,
                color = new Color(0.78f, 0.68f, 1f),
                remaining = ToastDuration * 0.55f
            });
            _insightAccumDelta = 0;
            _insightAccumCount = 0;
            _insightFlushTimer = 0f;
        }

        private void OnCaveMaterialPickedUp(GameEvents.CaveMaterialPickedUp evt)
        {
            if (evt.Item == null) return;
            _toasts.Add(new PickupToast
            {
                text = $"+{evt.Amount} {evt.Item.itemName} · 撤离后带回",
                color = new Color(0.70f, 0.92f, 0.55f),
                remaining = ToastDuration
            });
        }

        private void OnExtractSuccess(GameEvents.ExtractSuccess evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"撤离成功！{evt.CaveMaterialsCommitted} 件洞府素材已带回",
                color = new Color(0.55f, 0.95f, 0.70f),
                remaining = ToastDuration * 1.5f
            });
        }

        private void OnExtractInterrupted(GameEvents.ExtractInterrupted evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"撤离中断（{evt.Reason}）",
                color = new Color(1f, 0.55f, 0.45f),
                remaining = ToastDuration
            });
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                t.remaining -= Time.deltaTime;
                if (t.remaining <= 0f) _toasts.RemoveAt(i);
                else _toasts[i] = t;
            }

            // 经验累计窗口超时 → Flush（避免少量增量永远不弹）
            if (_insightAccumCount > 0)
            {
                _insightFlushTimer -= Time.deltaTime;
                if (_insightFlushTimer <= 0f) FlushInsightToast();
            }

        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _bgStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.05f, 0.08f, 0.12f, 0.78f));
            bgTex.Apply();
            _bgStyle.normal.background = bgTex;

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            _titleStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            _itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            _itemStyle.normal.textColor = new Color(0.78f, 0.85f, 0.92f);

            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, wordWrap = true };
            _hintStyle.normal.textColor = new Color(0.55f, 0.65f, 0.75f);

            _toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (MainMenu.IsVisible) return;   // 主菜单(UITK)时不画游戏内 HUD
            EnsureStyles();

            DrawPickupToasts();
            DrawInsightBar();
            DrawMoralStatus();
            // V.03（Q7）：局外 meta 暂缓时不显示角色等级 / 历练 HUD
            if (FeatureFlags.EnableCaveMeta)
            {
                DrawCultivationStatus();
            }
        }

        // ========== 修仙状态：道心 / 因果 / 寿元（v0.5.5，右上角）==========

        private void DrawMoralStatus()
        {
            var h = XianTu.LevelDesign.PlayerStateHooks.Instance;

            const float W = 168f;
            float x = Screen.width - W - 12f;
            float y = 115f;
            const float lineH = 20f;

            // 行数：道心常显；因果债≠0 显；寿元≠100 显
            int lines = 1;
            bool showKarma = h.KarmaDebt != 0;
            bool showLifespan = h.Lifespan != 100;
            if (showKarma) lines++;
            if (showLifespan) lines++;

            var panel = new Rect(x, y, W, lines * lineH + 10f);
            GUI.Box(panel, "", _bgStyle);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleLeft, richText = true
            };

            float ly = y + 5f;

            // —— 道心 ——
            string dxHex = h.Daoxin >= 80 ? "6cc0ff" : h.Daoxin >= 50 ? "d8e0e8" : h.Daoxin >= 20 ? "ffb060" : "ff5560";
            GUI.Label(new Rect(x + 8f, ly, W - 12f, lineH),
                $"<color=#{dxHex}>道心 · {h.DaoxinState} {h.Daoxin}</color>", style);
            ly += lineH;

            // —— 因果债 ——
            if (showKarma)
            {
                string kHex = h.KarmaDebt > 0 ? "ff8866" : "8fd08f";
                string kLabel = h.KarmaDebt > 0 ? $"因果债 +{h.KarmaDebt}" : $"善缘 {h.KarmaDebt}";
                GUI.Label(new Rect(x + 8f, ly, W - 12f, lineH), $"<color=#{kHex}>{kLabel}</color>", style);
                ly += lineH;
            }

            // —— 寿元 ——
            if (showLifespan)
            {
                string lHex = h.Lifespan < 30 ? "ffaa66" : "c8d0d8";
                GUI.Label(new Rect(x + 8f, ly, W - 12f, lineH), $"<color=#{lHex}>寿元 {h.Lifespan} 年</color>", style);
            }
        }

        // ========== 角色等级 + 历练 ==========

        private void DrawCultivationStatus()
        {
            var cult = CultivationSystem.Instance;

            const float W = 280f;
            float x = (Screen.width - W) * 0.5f;
            float y = 34f;  // 经验条（y=12,H=16）正下方

            // —— 角色等级 + 品质 + 本局历练 ——
            int realm = cult.CurrentRealm;
            int quality = cult.GetRealmQuality(realm);
            string qStr = (quality >= 0 && quality < CultivationSystem.QualityNames.Length)
                ? "·" + CultivationSystem.QualityNames[quality] : "";
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true
            };
            labelStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            GUI.Label(new Rect(x, y, W, 18f),
                $"<color=#b0d0ff>等级 · {cult.CurrentRealmName}{qStr}</color>　历练 {cult.RunTempering}", labelStyle);
        }

        // ========== 顶部经验条 ==========

        private void DrawInsightBar()
        {
            var insight = InsightSystem.Instance;
            if (insight.RunInsight <= 0) return;  // 还没积累过

            const float W = 280f, H = 16f;
            float x = (Screen.width - W) * 0.5f;
            float y = 12f;

            var bgRect = new Rect(x, y, W, H);
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.14f, 0.85f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            // 经验为纯积累资源。条满表示有积累，文字显示累计值。
            GUI.color = new Color(0.78f, 0.68f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, W, H), Texture2D.whiteTexture);
            GUI.color = prev;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            style.normal.textColor = Color.white;
            GUI.Label(bgRect, $"本局经验 {insight.RunInsight}（撤离转 50% 入永久）", style);
        }

        private void DrawPickupToasts()
        {
            const float W = 360f;
            const float startY = 80f;
            const float lineH = 22f;

            for (int i = 0; i < _toasts.Count; i++)
            {
                var t = _toasts[i];
                float alpha = Mathf.Clamp01(t.remaining / ToastDuration);
                var col = t.color;
                col.a = alpha;

                var rect = new Rect((Screen.width - W) * 0.5f, startY + i * lineH, W, lineH);
                var prev = GUI.color;
                GUI.color = col;
                GUI.Label(rect, t.text, _toastStyle);
                GUI.color = prev;
            }
        }
    }
}
