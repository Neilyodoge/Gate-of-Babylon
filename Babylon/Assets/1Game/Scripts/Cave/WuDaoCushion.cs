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
                if (_cache == null)
                {
                    _cache = new List<TalentEntry>();
                    // 用反射 / 简单遍历 RealmRewardLibrary（私有 list 没暴露），改为
                    // 直接 hardcode 6 个天赋。后续如果加 RealmRewardLibrary.AllTalents 公开访问，可切换。
                    AddByTalentId("Talent_Gold_PowerBreak", 80);
                    AddByTalentId("Talent_Wood_FertileSoil", 80);
                    AddByTalentId("Talent_Water_DoubleShadow", 80);
                    AddByTalentId("Talent_Fire_BurningChain", 80);
                    AddByTalentId("Talent_Earth_StoneSkin", 80);
                }
                return _cache;
            }
        }

        private static void AddByTalentId(string id, int cost)
        {
            var def = RealmRewardLibrary.GetById(id);
            if (def != null) _cache.Add(new TalentEntry { reward = def, insightCost = cost });
        }
    }
}
