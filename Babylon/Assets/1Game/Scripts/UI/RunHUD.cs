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
            GameEvents.Subscribe<GameEvents.SpiritVeinGained>(OnSpiritVeinGained);
            GameEvents.Subscribe<GameEvents.RealmAnomalyAnnounced>(OnRealmAnomalyAnnounced);
            GameEvents.Subscribe<GameEvents.ExtractSuccess>(OnExtractSuccess);
            GameEvents.Subscribe<GameEvents.ExtractInterrupted>(OnExtractInterrupted);
            GameEvents.Subscribe<GameEvents.InsightChanged>(OnInsightChanged);
            GameEvents.Subscribe<GameEvents.TribulationStarted>(OnTribulationStarted);
            GameEvents.Subscribe<GameEvents.TribulationBoltTelegraph>(OnTribulationBoltTelegraph);
            GameEvents.Subscribe<GameEvents.TribulationFinished>(OnTribulationFinished);
            GameEvents.Subscribe<GameEvents.FireBrandExploded>(OnFireBrandExploded);
            GameEvents.Subscribe<GameEvents.EarthRootedStateChanged>(OnEarthRootedChanged);
            GameEvents.Subscribe<GameEvents.EarthSigilDetonated>(OnEarthSigilDetonated);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.CaveMaterialPickedUp>(OnCaveMaterialPickedUp);
            GameEvents.Unsubscribe<GameEvents.SpiritVeinGained>(OnSpiritVeinGained);
            GameEvents.Unsubscribe<GameEvents.RealmAnomalyAnnounced>(OnRealmAnomalyAnnounced);
            GameEvents.Unsubscribe<GameEvents.ExtractSuccess>(OnExtractSuccess);
            GameEvents.Unsubscribe<GameEvents.ExtractInterrupted>(OnExtractInterrupted);
            GameEvents.Unsubscribe<GameEvents.InsightChanged>(OnInsightChanged);
            GameEvents.Unsubscribe<GameEvents.TribulationStarted>(OnTribulationStarted);
            GameEvents.Unsubscribe<GameEvents.TribulationBoltTelegraph>(OnTribulationBoltTelegraph);
            GameEvents.Unsubscribe<GameEvents.TribulationFinished>(OnTribulationFinished);
            GameEvents.Unsubscribe<GameEvents.FireBrandExploded>(OnFireBrandExploded);
            GameEvents.Unsubscribe<GameEvents.EarthRootedStateChanged>(OnEarthRootedChanged);
            GameEvents.Unsubscribe<GameEvents.EarthSigilDetonated>(OnEarthSigilDetonated);
        }

        // ========== 顿悟 / 渡劫 状态 ==========
        private bool _tribulationActive;
        private int _tribulationCurrentBolt;
        private int _tribulationTotalBolts;

        // 悟性飘字降噪：连续 InsightChanged 在 1.5s 窗口内合并，且每 5 次 Delta 才弹一次
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
                ? $"+{_insightAccumDelta} 灵力（累计 {_insightLatestValue}）"
                : $"+{_insightAccumDelta} 灵力 ×{_insightAccumCount}（累计 {_insightLatestValue}）";
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

        // 业焰印引爆飘字（火灵根专属反馈）—— 与"顿悟时刻"同款大字体
        private void OnFireBrandExploded(GameEvents.FireBrandExploded evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"🔥 业焰印 ×{evt.StacksConsumed} 引爆 🔥",
                color = new Color(1f, 0.5f, 0.15f),
                remaining = ToastDuration * 0.8f
            });
        }

        // v0.5 Week 7 · 土化身专属反馈：扎根 / 烙印引爆
        private void OnEarthRootedChanged(GameEvents.EarthRootedStateChanged evt)
        {
            if (!evt.IsRooted) return;
            _toasts.Add(new PickupToast
            {
                text = "🪨 山岳承负 · 扎根",
                color = new Color(0.85f, 0.7f, 0.35f),
                remaining = ToastDuration * 0.7f
            });
        }

        private void OnEarthSigilDetonated(GameEvents.EarthSigilDetonated evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"🪨 地脉镇压 ×{evt.StacksConsumed} (波及 {evt.EnemiesAffected})",
                color = new Color(0.85f, 0.7f, 0.35f),
                remaining = ToastDuration * 0.8f
            });
        }

        private void OnTribulationStarted(GameEvents.TribulationStarted evt)
        {
            _tribulationActive = true;
            _tribulationCurrentBolt = 0;
            _tribulationTotalBolts = evt.BoltCount;
            _toasts.Add(new PickupToast
            {
                text = $"⚡ 天劫降临 · {evt.BoltCount} 道雷劫 · 闪避禁用 · 走位躲避 ⚡",
                color = new Color(0.6f, 0.7f, 1f),
                remaining = ToastDuration * 1.6f
            });
        }

        private void OnTribulationBoltTelegraph(GameEvents.TribulationBoltTelegraph evt)
        {
            _tribulationCurrentBolt = evt.BoltIndex;
        }

        private void OnTribulationFinished(GameEvents.TribulationFinished evt)
        {
            _tribulationActive = false;
            string msg = evt.Outcome switch
            {
                TribulationOutcome.Success => "◆ 渡劫成功 · 破劫者 ◆",
                TribulationOutcome.PartialFail => "◇ 渡劫失利 · 仅余撤离一途 ◇",
                _ => "✗ 渡劫失败 · 形神俱灭 ✗"
            };
            var col = evt.Outcome switch
            {
                TribulationOutcome.Success => new Color(0.55f, 0.95f, 1f),
                TribulationOutcome.PartialFail => new Color(1f, 0.85f, 0.5f),
                _ => new Color(1f, 0.4f, 0.4f)
            };
            _toasts.Add(new PickupToast { text = msg, color = col, remaining = ToastDuration * 2f });
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

        private void OnSpiritVeinGained(GameEvents.SpiritVeinGained evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"💎 {evt.SourceName} · 灵脉 +{evt.Amount}（{evt.LevelName}）",
                color = new Color(0.4f, 0.9f, 0.7f),
                remaining = ToastDuration
            });
        }

        private void OnRealmAnomalyAnnounced(GameEvents.RealmAnomalyAnnounced evt)
        {
            _toasts.Add(new PickupToast
            {
                text = evt.Title,
                color = new Color(0.78f, 0.6f, 1f),
                remaining = ToastDuration * 2.2f
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

            // 悟性累计窗口超时 → Flush（避免少量增量永远不弹）
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

            DrawSidePanel();
            DrawPickupToasts();
            DrawInsightBar();
            DrawAnomalyStatus();
            DrawMoralStatus();
            // V.03（Q7）：局外 meta 暂缓时不显示本体境界 / 历练 / 心魔 HUD
            if (FeatureFlags.EnableCaveMeta)
            {
                DrawCultivationStatus();
                DrawTribulationOverlay();
            }
        }

        // ========== 秘境异象状态条（v0.5.5）==========

        private void DrawAnomalyStatus()
        {
            if (!RealmAnomalySystem.HasInstance) return;
            var active = RealmAnomalySystem.Instance.Active;
            if (active == null || active.Count == 0) return;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < active.Count; i++)
            {
                var info = RealmAnomalySystem.Info(active[i]);
                string hex = ColorUtility.ToHtmlStringRGB(info.color);
                if (i > 0) sb.Append("  ");
                sb.Append($"<color=#{hex}>{info.icon} {info.name}</color>");
            }

            const float W = 420f;
            float x = (Screen.width - W) * 0.5f;
            float y = Screen.height - 30f;  // 屏幕底部居中
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true
            };
            GUI.Label(new Rect(x, y, W, 22f), $"秘境异象 · {sb}", style);
        }

        // ========== 修仙状态：道心 / 因果 / 寿元（v0.5.5，右上角）==========

        private void DrawMoralStatus()
        {
            var h = XianTu.LevelDesign.PlayerStateHooks.Instance;

            const float W = 168f;
            float x = Screen.width - W - 12f;
            float y = 12f;
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

        // ========== 本体境界 + 历练值 + 心魔值（v0.5.4）==========

        private void DrawCultivationStatus()
        {
            var cult = CultivationSystem.Instance;

            const float W = 280f;
            float x = (Screen.width - W) * 0.5f;
            float y = 34f;  // 悟性条（y=12,H=16）正下方

            // —— 本体境界 + 成色 + 本局历练值 ——
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
                $"<color=#b0d0ff>本体 · {cult.CurrentRealmName}{qStr}</color>　历练 {cult.RunTempering}", labelStyle);

            // —— 心魔值条（仅有积累时显示）——
            if (InnerDemonMeter.HasInstance)
            {
                var dm = InnerDemonMeter.Instance;
                if (dm.Meter > 0.5f)
                {
                    const float H = 12f;
                    float by = y + 20f;
                    var bg = new Rect(x + 40f, by, W - 80f, H);
                    var prev = GUI.color;
                    GUI.color = new Color(0.12f, 0.04f, 0.06f, 0.85f);
                    GUI.DrawTexture(bg, Texture2D.whiteTexture);
                    float ratio = Mathf.Clamp01(dm.Meter / InnerDemonMeter.Max);
                    GUI.color = dm.IntrusionActive ? new Color(1f, 0.15f, 0.2f, 0.95f) : new Color(0.8f, 0.2f, 0.3f, 0.9f);
                    GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * ratio, H), Texture2D.whiteTexture);
                    GUI.color = prev;

                    var dmStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter, richText = true };
                    dmStyle.normal.textColor = Color.white;
                    GUI.Label(bg, dm.IntrusionActive ? "心魔乱入！" : $"心魔 {Mathf.RoundToInt(dm.Meter)}/100", dmStyle);
                }
            }
        }

        // ========== 顶部悟性条 ==========

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

            // v0.5.4：悟性回归纯积累资源，无"顿悟阈值"。条满表示有积累，文字显示累计值。
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
            GUI.Label(bgRect, $"本局灵力 {insight.RunInsight}（撤离转 50% 入永久）", style);
        }

        // ========== 渡劫全屏遮罩 ==========

        private void DrawTribulationOverlay()
        {
            if (!_tribulationActive) return;
            var prev = GUI.color;
            // 顶部小圈：当前/总数
            GUI.color = new Color(0.05f, 0.07f, 0.12f, 0.75f);
            var bannerRect = new Rect(0, 50f, Screen.width, 32f);
            GUI.DrawTexture(bannerRect, Texture2D.whiteTexture);
            GUI.color = prev;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, richText = true
            };
            style.normal.textColor = new Color(0.65f, 0.75f, 1f);
            GUI.Label(bannerRect, $"⚡ 渡劫中 · 第 {_tribulationCurrentBolt} / {_tribulationTotalBolts} 道雷劫 · 走位躲避 ⚡", style);

            // —— 道心对成色的影响提示（道心稳→渡劫稳）——
            int shift = TribulationTrial.DaoHeartQualityShift();
            string dxState = XianTu.LevelDesign.PlayerStateHooks.Instance.DaoxinState;
            string shiftStr = shift > 0 ? $"成色 +{shift}" : (shift < 0 ? $"成色 {shift}" : "成色不变");
            Color dxColor = shift > 0 ? new Color(0.6f, 0.9f, 1f) : (shift < 0 ? new Color(1f, 0.6f, 0.45f) : new Color(0.8f, 0.85f, 0.9f));
            var dxStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true
            };
            dxStyle.normal.textColor = dxColor;
            GUI.Label(new Rect(0, 82f, Screen.width, 20f), $"道心 · {dxState}（{shiftStr}）", dxStyle);
        }

        private void DrawSidePanel()
        {
            const float W = 240f;
            const float X = 12f;
            float yCursor = 200f;

            // ===== 本局缓冲 =====
            var cave = CaveInventory.Instance;
            int pendingTotal = cave.TotalPendingCount;

            int pendingPanelHeight = 56 + Mathf.Max(0, cave.CurrentRunBuffer.Count) * 16 + 32;
            var pendingRect = new Rect(X, yCursor, W, pendingPanelHeight);
            GUI.Box(pendingRect, "", _bgStyle);

            GUILayout.BeginArea(pendingRect);
            GUILayout.Space(6);
            GUILayout.Label($"<color=#ffd47a>本局 · 洞府素材 ({pendingTotal})</color>", _titleStyle);

            if (pendingTotal == 0)
            {
                GUILayout.Label("<color=#7a8898>梦中尚未拾到任何素材</color>", _hintStyle);
            }
            else
            {
                foreach (var kv in cave.CurrentRunBuffer)
                {
                    GUILayout.Label($"· {kv.Key}  ×{kv.Value}", _itemStyle);
                }
            }
            GUILayout.Space(4);
            GUILayout.Label("<color=#8aa0b8>需活着到出梦点撤离 · 死亡丢失</color>", _hintStyle);
            GUILayout.EndArea();

            yCursor += pendingPanelHeight + 8;

            // ===== 洞府资源 =====
            var economy = CaveEconomy.Instance;
            var data = SaveSystem.Instance.Data;

            var qiRect = new Rect(X, yCursor, W, 64);
            GUI.Box(qiRect, "", _bgStyle);

            GUILayout.BeginArea(qiRect);
            GUILayout.Space(6);
            GUILayout.Label($"<color=#88ccff>洞府 · 灵气 {economy.Qi}</color>", _titleStyle);
            GUILayout.Label($"<color=#a8b8c8>累积素材种类 {data.caveInventory.Count}</color>", _itemStyle);
            GUILayout.EndArea();
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
