using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 悟道蒲团 · 第三个洞府模块（v0.5）—— 消耗永久悟性解锁化身天赋节点。
    ///
    /// 流程：
    /// 1. 玩家在梦境用悟性 buff → 撤离时 50% 转入 SaveData.accumulatedInsight
    /// 2. 在洞府坐蒲团 → 看到天赋节点表 → 选未解锁节点点【参悟】消耗永久悟性
    /// 3. 解锁的天赋 id 进入 SaveData.unlockedTalentIds → 下次入梦时 PermanentTalentLoader 自动挂 StatusEffect
    ///
    /// 实现细节：天赋节点定义直接复用 RealmRewardLibrary 中的 Talent_* 奖励。
    /// </summary>
    public class WuDaoCushion : CaveModule
    {
        public override string ModuleName => "悟道蒲团";
        public override string ModuleIcon => "🧘";
        public override string ModuleRole => "悟性 → 永久天赋";
        public override Color ModuleColor => new Color(0.78f, 0.68f, 1f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private Vector2 _scroll;

        protected override void BuildBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cushion";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.15f, 0);
            body.transform.localScale = new Vector3(1.4f, 0.15f, 1.4f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.45f, 0.25f, 0.55f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ModuleColor * 0.6f);
                rend.material = mat;
            }
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel() => _panelOpen = false;

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 640f, H = 480f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("🧘 悟道蒲团 · 参悟化身天赋", titleStyle);

            var insight = InsightSystem.Instance;
            GUILayout.Label($"<color=#dfcfff>永久悟性：{insight.PermanentInsight}</color>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontSize = 14 });

            GUILayout.Space(6);
            GUILayout.Label("化身天赋节点（解锁后跨局永久生效）", new GUIStyle(GUI.skin.label) { fontSize = 12 });
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawTalentList();
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) ClosePanel();
        }

        private void DrawTalentList()
        {
            // 从 RealmRewardLibrary 拿所有 Talent_* 奖励
            var unlocked = new HashSet<string>(SaveSystem.Instance.Data.unlockedTalentIds);

            foreach (var entry in PermanentTalentRegistry.AllTalents)
            {
                var t = entry.reward;
                bool isUnlocked = unlocked.Contains(t.id);

                GUILayout.BeginHorizontal(GUI.skin.box);

                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
                nameStyle.normal.textColor = t.displayColor;
                GUILayout.Label(t.displayName, nameStyle, GUILayout.Width(150));
                GUILayout.Label(t.description, new GUIStyle(GUI.skin.label) { wordWrap = true }, GUILayout.Width(330));

                if (isUnlocked)
                {
                    var okStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                    okStyle.normal.textColor = new Color(0.6f, 0.95f, 0.6f);
                    GUILayout.Label("✓ 已悟", okStyle, GUILayout.Width(100));
                }
                else
                {
                    int cost = entry.insightCost;
                    GUI.enabled = InsightSystem.Instance.PermanentInsight >= cost;
                    if (GUILayout.Button($"参悟 ({cost} 悟性)", GUILayout.Width(100)))
                    {
                        TryUnlock(entry);
                    }
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }
        }

        private void TryUnlock(PermanentTalentRegistry.TalentEntry entry)
        {
            if (!InsightSystem.Instance.SpendPermanentInsight(entry.insightCost))
            {
                Debug.Log("<color=red>[悟道蒲团] 悟性不足</color>");
                return;
            }
            SaveSystem.Instance.Data.unlockedTalentIds.Add(entry.reward.id);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#dfcfff>[悟道蒲团] 参悟成功：{entry.reward.displayName}（消耗 {entry.insightCost} 悟性）</color>");
        }
    }

    /// <summary>
    /// 永久天赋注册表 —— 暴露所有可解锁的化身天赋及其悟性消耗。
    /// 数据来自 RealmRewardLibrary 中的 Talent_* 条目。
    /// </summary>
    public static class PermanentTalentRegistry
    {
        public struct TalentEntry
        {
            public RealmReward reward;
            public int insightCost;
        }

        private static List<TalentEntry> _cache;

        public static IReadOnlyList<TalentEntry> AllTalents
        {
            get
            {
                if (_cache == null) Rebuild();
                return _cache;
            }
        }

        /// <summary>
        /// 自动从 <see cref="RealmRewardLibrary"/> 拉所有 SpiritTalent 类别奖励。
        /// 后续在 RealmRewardLibrary 加新天赋时无需再改本注册表（v0.5 Week 8 技术债 7 清理）。
        ///
        /// 悟性消耗规则（可后续做成数据驱动）：
        /// - 默认 80（与原版一致）
        /// - 后续若加"高阶天赋"，可在 reward.id 命名约定中加 "_Tier2" / "_Tier3" 等标识来分级
        /// </summary>
        public static void Rebuild()
        {
            _cache = new List<TalentEntry>();
            var all = RealmRewardLibrary.ListByCategory(RealmRewardCategory.SpiritTalent);
            foreach (var def in all)
            {
                int cost = ResolveCost(def.id);
                _cache.Add(new TalentEntry { reward = def, insightCost = cost });
            }
        }

        private static int ResolveCost(string id)
        {
            // 命名约定：含 _Tier2 → 160 / _Tier3 → 320 / 其他 → 80
            if (id != null)
            {
                if (id.Contains("_Tier3")) return 320;
                if (id.Contains("_Tier2")) return 160;
            }
            return 80;
        }

        /// <summary>测试 / Editor 热重载用</summary>
        public static void ClearCache() => _cache = null;
    }
}
