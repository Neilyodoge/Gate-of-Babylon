using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵兽园 · 第六个洞府模块（v0.5 Week 4）。
    ///
    /// 玩家在洞府消耗【妖兽骨片】+ 灵药孕育灵兽伙伴；解锁后只能"携带一只"入梦，
    /// 入梦时由 <see cref="SpiritBeastCompanion"/> 在玩家身边 spawn 跟随，
    /// 自动锁定 6m 内最近的敌人攻击。
    /// </summary>
    public class SpiritBeastGarden : CaveModule
    {
        public override string ModuleName => "灵兽园";
        public override string ModuleIcon => "🐉";
        public override string ModuleRole => "妖兽材料 → 跟随灵兽";
        public override Color ModuleColor => new Color(0.55f, 0.85f, 0.55f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private Vector2 _scroll;
        private int _selectedIdx = -1;

        protected override void BuildBody()
        {
            Color leafGreen = new Color(0.45f, 0.95f, 0.5f);
            Color amber = new Color(1f, 0.75f, 0.35f);

            // —— 圆形草坪 ——
            var lawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lawn.name = "BeastLawn";
            lawn.transform.SetParent(transform, false);
            lawn.transform.localPosition = new Vector3(0, 0.05f, 0);
            lawn.transform.localScale = new Vector3(2.4f, 0.1f, 2.4f);
            var col = lawn.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = lawn.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLit(new Color(0.28f, 0.5f, 0.22f));
                rend.material = mat;
            }

            // —— 地面木纹符印（草坪上的灵气印）——
            CaveVfx.SpawnGroundRune(transform, new Vector3(0, 0.1f, 0), 1.9f,
                leafGreen, sides: 5, lineWidth: 0.06f);

            // —— 中央树桩 ——
            var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = "BeastStump";
            stump.transform.SetParent(transform, false);
            stump.transform.localPosition = new Vector3(0, 0.45f, 0);
            stump.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            var scol = stump.GetComponent<Collider>();
            if (scol != null) Destroy(scol);
            var srend = stump.GetComponent<Renderer>();
            if (srend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.45f, 0.28f, 0.15f), ModuleColor * 0.4f);
                srend.material = mat;
            }

            // —— 树桩上的孵化灵蛋（金光自发光，悬浮 + 自转）——
            CaveVfx.SpawnHoveringObject(transform, new Vector3(0, 1.15f, 0),
                PrimitiveType.Sphere, new Vector3(0.45f, 0.55f, 0.45f),
                new Color(0.95f, 0.85f, 0.55f), amber * 2.0f,
                hoverAmp: 0.06f, hoverFreq: 1.0f, spinSpeed: 25f);

            // —— 3 座小图腾石（圆周排布 + 上下浮动）——
            for (int i = 0; i < 3; i++)
            {
                float ang = (i / 3f) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * 1.8f, 0.6f, Mathf.Sin(ang) * 1.8f);
                var totem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                totem.name = $"Totem_{i}";
                totem.transform.SetParent(transform, false);
                totem.transform.localPosition = pos;
                totem.transform.localRotation = Quaternion.Euler(0, ang * Mathf.Rad2Deg, 0);
                totem.transform.localScale = new Vector3(0.32f, 1.2f, 0.32f);
                var tcol = totem.GetComponent<Collider>();
                if (tcol != null) Destroy(tcol);
                var trend = totem.GetComponent<Renderer>();
                if (trend != null)
                {
                    var mat = MaterialHelper.CreateLitEmissive(
                        new Color(0.35f, 0.32f, 0.28f), leafGreen * 0.6f);
                    trend.material = mat;
                }
                // 顶部小灵珠
                CaveVfx.SpawnHoveringObject(transform, pos + Vector3.up * 0.85f,
                    PrimitiveType.Sphere, Vector3.one * 0.2f,
                    new Color(0.7f, 1f, 0.7f), leafGreen * 2.0f,
                    hoverAmp: 0.08f, hoverFreq: 1.6f, spinSpeed: 60f);
            }

            // —— 灵蛋周围的萤火粒子（绿色轨道）——
            CaveVfx.SpawnOrbitingParticles(transform, new Vector3(0, 1.15f, 0),
                count: 6, orbitRadius: 0.7f, orbitHeight: 0f,
                particleSize: 0.1f, color: leafGreen,
                orbitSpeed: 90f, verticalBob: 0.25f);
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel()
        {
            _panelOpen = false;
            _selectedIdx = -1;
        }

        // ============================== UI ==============================

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 700f, H = 460f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("🐉 灵兽园 · 妖兽骨片中孕育灵伴", titleStyle);

            var save = SaveSystem.Instance.Data;
            string active = save.activeSpiritBeastId;
            GUILayout.Label($"当前出征灵兽：{(string.IsNullOrEmpty(active) ? "无" : active)}",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            DrawBeastList();
            GUILayout.Space(8);
            DrawBeastDetail();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                ClosePanel();
        }

        private void DrawBeastList()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(280));
            GUILayout.Label("灵兽图鉴", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });
            _scroll = GUILayout.BeginScrollView(_scroll);

            var save = SaveSystem.Instance.Data;
            var beasts = SpiritBeastLibrary.AllBeasts;
            for (int i = 0; i < beasts.Count; i++)
            {
                var b = beasts[i];
                bool isUnlocked = save.unlockedBeastIds.Contains(b.beastName);
                bool isSelected = i == _selectedIdx;

                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true };
                string prefix = isUnlocked ? "<color=#88ff88>✓</color> " : "  ";
                string colorHex = "#" + ColorUtility.ToHtmlStringRGB(b.displayColor);
                string label = $"{prefix}<color={colorHex}>{b.beastName}</color>";

                Color prev = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = ModuleColor;
                if (GUILayout.Button(label, style, GUILayout.Height(28)))
                    _selectedIdx = i;
                GUI.backgroundColor = prev;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawBeastDetail()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(400));

            var beasts = SpiritBeastLibrary.AllBeasts;
            if (_selectedIdx < 0 || _selectedIdx >= beasts.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 从左侧选一只灵兽",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            var entry = beasts[_selectedIdx];
            var save = SaveSystem.Instance.Data;
            bool unlocked = save.unlockedBeastIds.Contains(entry.beastName);

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
            nameStyle.normal.textColor = entry.displayColor;
            GUILayout.Label(entry.beastName, nameStyle);

            GUILayout.Label(entry.description,
                new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true });

            GUILayout.Space(8);
            GUILayout.Label("所需材料：", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });
            bool affordable = true;
            foreach (var cost in entry.costs)
            {
                int have = SaveSystem.Instance.GetCaveItemCount(cost.materialName);
                bool ok = have >= cost.amount;
                if (!ok) affordable = false;
                string color = ok ? "#a0d090" : "#ff9090";
                GUILayout.Label($"  · <color={color}>{cost.materialName}  ×{cost.amount}（持有 {have}）</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }

            GUILayout.FlexibleSpace();

            if (unlocked)
            {
                bool isActive = save.activeSpiritBeastId == entry.beastName;
                if (isActive)
                {
                    var okStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                    okStyle.normal.textColor = entry.displayColor;
                    GUILayout.Label("● 当前出征灵兽", okStyle);
                    if (GUILayout.Button("取消出征", GUILayout.Height(28)))
                    {
                        save.activeSpiritBeastId = "";
                        SaveSystem.Instance.Save();
                    }
                }
                else
                {
                    if (GUILayout.Button("🐾 派为出征灵兽（下次入秘境跟随）", GUILayout.Height(34)))
                    {
                        save.activeSpiritBeastId = entry.beastName;
                        SaveSystem.Instance.Save();
                        Debug.Log($"<color=#a0d090>[灵兽园] 出征灵兽设为：{entry.beastName}</color>");
                    }
                }
            }
            else
            {
                GUI.enabled = affordable;
                if (GUILayout.Button(affordable ? "🥚 孕育灵兽" : "材料不足", GUILayout.Height(34)))
                {
                    TryHatch(entry);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        // ============================== 逻辑 ==============================

        private void TryHatch(SpiritBeastEntry entry)
        {
            foreach (var cost in entry.costs)
            {
                if (SaveSystem.Instance.GetCaveItemCount(cost.materialName) < cost.amount)
                {
                    Debug.Log($"<color=red>[灵兽园] {cost.materialName} 不足 {cost.amount}</color>");
                    return;
                }
            }
            foreach (var cost in entry.costs)
                SaveSystem.Instance.ConsumeCaveItem(cost.materialName, cost.amount);

            var save = SaveSystem.Instance.Data;
            if (!save.unlockedBeastIds.Contains(entry.beastName))
                save.unlockedBeastIds.Add(entry.beastName);
            SaveSystem.Instance.Save();

            GameEvents.Publish(new GameEvents.SpiritBeastHatched
            {
                BeastName = entry.beastName
            });

            Debug.Log($"<color=#a0d090>[灵兽园] 孕育成功：{entry.beastName}</color>");
        }
    }

    /// <summary>SaveDataV1 的扩展：方便 UI 直接检查"是否已孕育过任何灵兽"。</summary>
    public static class SaveDataBeastExt
    {
        public static bool HasOwnUnlockedBeasts(this SaveDataV1 data)
        {
            return data != null && data.unlockedBeastIds != null && data.unlockedBeastIds.Count > 0;
        }
    }

    // ===================================================================
    //                          灵兽配方
    // ===================================================================

    public class SpiritBeastEntry
    {
        public string beastName;
        public string description;
        public Color displayColor;
        public float attackDamage;
        public float attackInterval;
        public float scanRadius;
        public List<ForgeCost> costs;
    }

    public static class SpiritBeastLibrary
    {
        private static List<SpiritBeastEntry> _cache;
        public static IReadOnlyList<SpiritBeastEntry> AllBeasts
        {
            get { if (_cache == null) Build(); return _cache; }
        }

        public static SpiritBeastEntry GetByName(string name)
        {
            foreach (var e in AllBeasts)
                if (e.beastName == name) return e;
            return null;
        }

        private static void Build()
        {
            _cache = new List<SpiritBeastEntry>();

            _cache.Add(new SpiritBeastEntry
            {
                beastName = "青鸾",
                description = "<i>「南海青鸾，灵动如风，攻速极快。」</i>\n· 攻击间隔 0.6s · 单次伤害 18\n· 索敌半径 7m",
                displayColor = new Color(0.4f, 0.85f, 1f),
                attackDamage = 18f,
                attackInterval = 0.6f,
                scanRadius = 7f,
                costs = new List<ForgeCost> { new("妖兽骨片", 3), new("寒霜花灵药", 1) }
            });

            _cache.Add(new SpiritBeastEntry
            {
                beastName = "赤虎",
                description = "<i>「火山赤虎，焰息逼人，攻击重创。」</i>\n· 攻击间隔 1.2s · 单次伤害 42\n· 索敌半径 6m",
                displayColor = new Color(1f, 0.5f, 0.3f),
                attackDamage = 42f,
                attackInterval = 1.2f,
                scanRadius = 6f,
                costs = new List<ForgeCost> { new("妖兽骨片", 4), new("火灵草灵药", 1) }
            });

            _cache.Add(new SpiritBeastEntry
            {
                beastName = "玄龟",
                description = "<i>「北海玄龟，护主擒敌，攻击附带眩晕。」</i>\n· 攻击间隔 1.4s · 单次伤害 30\n· 索敌半径 5m",
                displayColor = new Color(0.5f, 0.9f, 0.6f),
                attackDamage = 30f,
                attackInterval = 1.4f,
                scanRadius = 5f,
                costs = new List<ForgeCost> { new("妖兽骨片", 5), new("灵藤芽灵药", 1) }
            });
        }
    }
}
