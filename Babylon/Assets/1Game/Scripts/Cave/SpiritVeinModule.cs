using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵脉台（v0.5.4 · GDD 9.1.9）—— 把历练值存量注入洞府灵脉。
    ///
    /// 与闭关石室共享同一份"历练值存量"：投修为（自身变强）vs 投灵脉（每趟收益更好），
    /// 这是洞府 meta 的核心抉择。灵脉等级影响秘境掉落品质 + 机缘品质 + 洞府模块效率。
    /// </summary>
    public class SpiritVeinModule : CaveModule
    {
        public override string ModuleName => "灵脉台";
        public override string ModuleIcon => "💎";
        public override string ModuleRole => "历练值 → 灵脉（收益品质）";
        public override Color ModuleColor => new Color(0.55f, 0.92f, 0.78f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        protected override void BuildBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "VeinCrystal";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.7f, 0);
            body.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.2f, 0.45f, 0.4f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ModuleColor * 0.7f);
                rend.material = mat;
            }
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel() => _panelOpen = false;

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 600f, H = 400f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("💎 灵脉台 · 洞府根基", titleStyle);

            var vein = SpiritVeinSystem.Instance;
            var cult = CultivationSystem.Instance;
            var center = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUILayout.Label($"<color=#9be0c0>当前灵脉：{vein.LevelName}</color>", center);

            // 进度
            GUILayout.Space(6);
            if (vein.IsMaxLevel)
            {
                GUILayout.Label("<color=#cfffe8>已臻洞天福地 —— 灵脉至盛</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter });
            }
            else
            {
                string nextName = SpiritVeinSystem.LevelNames[Mathf.Clamp(vein.Level + 1, 0, SpiritVeinSystem.MaxLevel)];
                GUILayout.Label($"灵脉经验：{vein.ExpIntoCurrentLevel} / {vein.NextLevelSpan}（升 {nextName}）",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                var barRect = GUILayoutUtility.GetRect(W - 80, 16);
                barRect.x += 40;
                var prev = GUI.color;
                GUI.color = new Color(0.08f, 0.18f, 0.15f, 0.9f);
                GUI.DrawTexture(barRect, Texture2D.whiteTexture);
                GUI.color = new Color(0.5f, 0.92f, 0.78f, 0.95f);
                float prog = vein.NextLevelSpan > 0 ? Mathf.Clamp01((float)vein.ExpIntoCurrentLevel / vein.NextLevelSpan) : 0f;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * prog, barRect.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // 当前作用
            GUILayout.Space(8);
            GUILayout.Label(
                $"<color=#a8c8bc>秘境素材额外掉率 +{vein.DropBonus * 100:F0}%　模块效率 ×{vein.ModuleEfficiency:F2}　机缘上限 {SpiritVeinSystem.LevelNames[vein.MaxOpportunityTier]}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 11 });

            // 注入
            GUILayout.Space(14);
            GUILayout.Label($"<color=#ffd47a>历练值存量：{cult.TemperingPool}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 13 });
            if (!vein.IsMaxLevel)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUI.enabled = cult.TemperingPool > 0;
                if (GUILayout.Button("💎 注入 +50 灵脉", GUILayout.Height(30))) vein.InjectFromPool(50);
                if (GUILayout.Button("💎 全部注入", GUILayout.Height(30))) vein.InjectFromPool(cult.TemperingPool);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("<color=#888>※ 投灵脉 = 每趟收益更好；投修为（闭关石室）= 自身更强。历练值有限，二选一。</color>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 11 });

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) ClosePanel();
        }
    }
}
