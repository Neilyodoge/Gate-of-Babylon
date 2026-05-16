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
            GameEvents.Subscribe<GameEvents.SpiritDensityChanged>(OnSpiritDensityChanged);
            GameEvents.Subscribe<GameEvents.InsightChanged>(OnInsightChanged);
            GameEvents.Subscribe<GameEvents.InsightMomentTriggered>(OnInsightMoment);
            GameEvents.Subscribe<GameEvents.TribulationStarted>(OnTribulationStarted);
            GameEvents.Subscribe<GameEvents.TribulationBoltTelegraph>(OnTribulationBoltTelegraph);
            GameEvents.Subscribe<GameEvents.TribulationFinished>(OnTribulationFinished);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.CaveMaterialPickedUp>(OnCaveMaterialPickedUp);
            GameEvents.Unsubscribe<GameEvents.ExtractSuccess>(OnExtractSuccess);
            GameEvents.Unsubscribe<GameEvents.ExtractInterrupted>(OnExtractInterrupted);
            GameEvents.Unsubscribe<GameEvents.SpiritDensityChanged>(OnSpiritDensityChanged);
            GameEvents.Unsubscribe<GameEvents.InsightChanged>(OnInsightChanged);
            GameEvents.Unsubscribe<GameEvents.InsightMomentTriggered>(OnInsightMoment);
            GameEvents.Unsubscribe<GameEvents.TribulationStarted>(OnTribulationStarted);
            GameEvents.Unsubscribe<GameEvents.TribulationBoltTelegraph>(OnTribulationBoltTelegraph);
            GameEvents.Unsubscribe<GameEvents.TribulationFinished>(OnTribulationFinished);
        }

        private void OnSpiritDensityChanged(GameEvents.SpiritDensityChanged evt)
        {
            if (evt.NewLevel == SpiritDensityLevel.Normal) return;  // 普通灵气不弹提示
            _toasts.Add(new PickupToast
            {
                text = $"◇ {evt.DisplayName} ◇",
                color = evt.Tint,
                remaining = ToastDuration * 1.4f
            });
        }

        // ========== 顿悟 / 渡劫 状态 ==========
        private bool _tribulationActive;
        private int _tribulationCurrentBolt;
        private int _tribulationTotalBolts;

        private void OnInsightChanged(GameEvents.InsightChanged evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"+{evt.Delta} 悟性（{evt.NewRunInsight}/{evt.NextThreshold}）",
                color = new Color(0.78f, 0.68f, 1f),
                remaining = ToastDuration * 0.6f
            });
        }

        private void OnInsightMoment(GameEvents.InsightMomentTriggered evt)
        {
            _toasts.Add(new PickupToast
            {
                text = $"◇ 顿悟时刻 #{evt.MomentIndex} ◇",
                color = new Color(0.85f, 0.75f, 1f),
                remaining = ToastDuration * 1.5f
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
                TribulationOutcome.PartialFail => "◇ 渡劫险胜 · 半残撤离 ◇",
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

            // 减少魂伤（按游戏时间）
            var data = SaveSystem.Instance.Data;
            if (data.soulHurtRemainingSec > 0f)
            {
                data.soulHurtRemainingSec = Mathf.Max(0f, data.soulHurtRemainingSec - GameTime.Instance.DeltaTime);
                // 不每帧 Save，避免 IO 抖动；soulHurtRemainingSec 在重大事件（撤离/死亡）时随其他写入一起持久化即可
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
            EnsureStyles();

            DrawSidePanel();
            DrawPickupToasts();
            DrawPillSlots();
            DrawInsightBar();
            DrawTribulationOverlay();
        }

        // ========== 底部丹药槽位 ==========

        private void DrawPillSlots()
        {
            int total = PendingPillCarry.TotalActive;
            const int slotMax = 3;
            const float slotW = 80f;
            const float slotH = 56f;
            const float gap = 8f;
            float totalW = slotMax * slotW + (slotMax - 1) * gap;
            float startX = (Screen.width - totalW) * 0.5f;
            float y = Screen.height - 110f;

            int drawn = 0;
            // 已携丹药占据前 N 格
            foreach (var kv in PendingPillCarry.ActiveCarry)
            {
                for (int j = 0; j < kv.Value && drawn < slotMax; j++)
                {
                    DrawPillSlot(new Rect(startX + drawn * (slotW + gap), y, slotW, slotH), kv.Key, drawn == 0);
                    drawn++;
                }
            }
            // 剩余空格
            for (int i = drawn; i < slotMax; i++)
            {
                DrawEmptySlot(new Rect(startX + i * (slotW + gap), y, slotW, slotH));
            }

            // "按 G 服丹"提示
            if (total > 0)
            {
                var hintRect = new Rect(0, y + slotH + 4, Screen.width, 20f);
                GUI.Label(hintRect, "<color=#ffaa66>按 [G] 服丹 · 回 40% 最大生命</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 13 });
            }
        }

        private void DrawPillSlot(Rect r, string pillName, bool isNext)
        {
            // 底色（即将使用的高亮）
            var prev = GUI.color;
            GUI.color = isNext ? new Color(1f, 0.65f, 0.35f, 0.95f) : new Color(0.95f, 0.55f, 0.35f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Texture2D.whiteTexture);
            GUI.color = prev;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = true
            };
            nameStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
            GUI.Label(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), $"⚱\n{pillName}", nameStyle);
        }

        private void DrawEmptySlot(Rect r)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.15f, 0.18f, 0.22f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(0.4f, 0.45f, 0.50f);
            GUI.Label(r, "(空)", style);
        }

        // ========== 顶部悟性条 ==========

        private void DrawInsightBar()
        {
            var insight = InsightSystem.Instance;
            if (insight.RunInsight <= 0 && insight.TotalMomentsThisRun == 0) return;  // 还没积累过

            const float W = 280f, H = 16f;
            float x = (Screen.width - W) * 0.5f;
            float y = 12f;

            var bgRect = new Rect(x, y, W, H);
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.14f, 0.85f);
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

            float progress = Mathf.Clamp01((float)insight.RunInsight / insight.NextMomentThreshold);
            GUI.color = new Color(0.78f, 0.68f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, W * progress, H), Texture2D.whiteTexture);
            GUI.color = prev;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            style.normal.textColor = Color.white;
            GUI.Label(bgRect, $"悟性 {insight.RunInsight} / {insight.NextMomentThreshold}", style);
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
        }

        private void DrawSidePanel()
        {
            const float W = 240f;
            const float X = 12f;
            float yCursor = 200f;

            // ===== 灵气浓度（仅非 Normal 时显示）=====
            if (SpiritDensity.Current != SpiritDensityLevel.Normal)
            {
                var densityRect = new Rect(X, yCursor, W, 44);
                GUI.Box(densityRect, "", _bgStyle);
                GUILayout.BeginArea(densityRect);
                GUILayout.Space(6);
                var c = SpiritDensity.AmbientTint;
                GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>◇ {SpiritDensity.DisplayName}</color>", _titleStyle);
                GUILayout.Label($"<color=#a8b8c8>HP×{SpiritDensity.EnemyHpMultiplier:F2}  DROP×{SpiritDensity.ItemDropMultiplier:F2}</color>", _hintStyle);
                GUILayout.EndArea();
                yCursor += 52;
            }

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

            int qiPanelHeight = data.soulHurtRemainingSec > 0f ? 92 : 64;
            var qiRect = new Rect(X, yCursor, W, qiPanelHeight);
            GUI.Box(qiRect, "", _bgStyle);

            GUILayout.BeginArea(qiRect);
            GUILayout.Space(6);
            GUILayout.Label($"<color=#88ccff>洞府 · 灵气 {economy.Qi}</color>", _titleStyle);
            GUILayout.Label($"<color=#a8b8c8>累积素材种类 {data.caveInventory.Count}</color>", _itemStyle);

            if (data.soulHurtRemainingSec > 0f)
            {
                GUILayout.Space(4);
                GUILayout.Label($"<color=#ff8866>魂伤剩余：{GameTime.FormatDuration(data.soulHurtRemainingSec)}</color>", _itemStyle);
                GUILayout.Label("<color=#8a6868>需等魂伤消退才能再入梦</color>", _hintStyle);
            }
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
