using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 闭关石室（v0.5.4）—— 本体境界的洞府交互模块。
    ///
    /// 功能：
    ///   · 查看本体境界 + 成色 + 修为进度
    ///   · 「冲击境界」：修为攒满 → 触发渡劫战（<see cref="TribulationTrial"/>）→ 成功晋升本体境界
    ///   · 「凝实」：消耗修为打磨当前境界成色（瑕→凡→上；完美靠渡劫表现，不可凝实）
    ///
    /// 历练值如何来：秘境击杀积累、撤离 100% 转入修为（<see cref="CultivationSystem"/>）。
    /// </summary>
    public class MeditationChamber : CaveModule
    {
        public override string ModuleName => "闭关石室";
        public override string ModuleIcon => "🧘";
        public override string ModuleRole => "历练值 → 修为 → 本体境界";
        public override Color ModuleColor => new Color(0.55f, 0.78f, 1f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        protected override void BuildBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "MeditationStone";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            body.transform.localScale = new Vector3(1.5f, 1.0f, 1.5f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.22f, 0.30f, 0.42f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ModuleColor * 0.5f);
                rend.material = mat;
            }
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel() => _panelOpen = false;

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 600f, H = 420f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("🧘 闭关石室 · 本体境界", titleStyle);

            var cult = CultivationSystem.Instance;

            // —— 当前境界 + 成色 ——
            int realm = cult.CurrentRealm;
            int quality = cult.GetRealmQuality(realm);
            string qualityStr = quality >= 0 && quality < CultivationSystem.QualityNames.Length
                ? CultivationSystem.QualityNames[quality] : "—";
            var centerLabel = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUILayout.Label($"<color=#bcd8ff>当前境界：{cult.CurrentRealmName}（{qualityStr}）</color>", centerLabel);

            // —— 修为进度 ——
            GUILayout.Space(6);
            if (cult.IsMaxRealm)
            {
                GUILayout.Label("<color=#ffe88a>已至渡劫之巅 —— 飞升之路，另寻机缘</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            }
            else
            {
                int cost = cult.NextBreakthroughCost;
                int exp = cult.CurrentExp;
                string nextName = CultivationSystem.RealmNames[Mathf.Clamp(realm + 1, 0, CultivationSystem.MaxRealm)];
                GUILayout.Label($"修为：{exp} / {cost}（冲击 {nextName}）",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });

                // 进度条
                var barRect = GUILayoutUtility.GetRect(W - 80, 16);
                barRect.x += 40;
                var prev = GUI.color;
                GUI.color = new Color(0.1f, 0.12f, 0.2f, 0.9f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.5f, 0.78f, 1f, 0.95f);
                float prog = cost > 0 ? Mathf.Clamp01((float)exp / cost) : 0f;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * prog, barRect.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            GUILayout.Space(10);
            GUILayout.Label($"<color=#888>本局历练值：{cult.RunTempering}（撤离后转入修为）</color>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 12 });

            GUILayout.Space(16);

            // —— 冲击境界（渡劫战）——
            if (!cult.IsMaxRealm)
            {
                GUI.enabled = cult.CanBreakthrough;
                if (GUILayout.Button(cult.CanBreakthrough ? "⚡ 冲击境界 · 渡劫战" : "⚡ 冲击境界（修为不足）", GUILayout.Height(36)))
                {
                    StartBreakthrough();
                }
                GUI.enabled = true;
            }

            // —— 凝实成色 ——
            GUILayout.Space(6);
            if (!cult.IsMaxRealm || quality >= 0)
            {
                bool canRefine = quality >= 0 && quality < 2;
                int refineCost = canRefine ? cult.RefineCost(realm, quality) : 0;
                if (canRefine)
                {
                    GUI.enabled = cult.CurrentExp >= refineCost;
                    if (GUILayout.Button($"✦ 凝实成色（耗修为 {refineCost} → {CultivationSystem.QualityNames[quality + 1]}）", GUILayout.Height(30)))
                    {
                        cult.Refine();
                    }
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label("<color=#777>当前成色已达上品（或炼气无成色）—— 完美成色需靠渡劫表现</color>",
                        new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 11 });
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) ClosePanel();
        }

        private void StartBreakthrough()
        {
            var cult = CultivationSystem.Instance;
            if (!cult.CanBreakthrough) return;

            ClosePanel();
            int targetRealm = cult.CurrentRealm + 1;
            TribulationTrial.Begin(targetRealm, (success, quality) =>
            {
                if (success)
                {
                    cult.Breakthrough(quality);
                    Debug.Log($"<color=#ffe88a>[闭关石室] 渡劫成功 → {cult.CurrentRealmName}（{CultivationSystem.QualityNames[Mathf.Clamp(quality, 0, 3)]}）</color>");
                }
                else
                {
                    Debug.Log("<color=#ff8866>[闭关石室] 渡劫中止 —— 修为留存，可再来</color>");
                }
            });
        }
    }
}
