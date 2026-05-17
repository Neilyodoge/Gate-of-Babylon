using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 藏经阁 · 第五个洞府模块（v0.5 Week 4）。
    ///
    /// 玩家从梦境拾取的【古籍残页】（ScripturePage）每 5 张可在藏经阁拼合一卷永久功法，
    /// 功法 id 写入 <see cref="SaveDataV1.unlockedSkillIds"/>。
    /// 已解锁的功法可以选择一卷"下次入梦时起手装备到 Q 槽"。
    ///
    /// 与炼器房不同：藏经阁的功法不会自动加入掉落池（避免商店出现已永久解锁的功法显得没价值），
    /// 而是必须显式"携带"，让玩家每次入梦做一次决策。
    /// </summary>
    public class ScripturePavilion : CaveModule
    {
        public override string ModuleName => "藏经阁";
        public override string ModuleIcon => "📜";
        public override string ModuleRole => "残页 → 永久功法 + 起手携带";
        public override Color ModuleColor => new Color(0.85f, 0.75f, 0.5f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private Vector2 _scroll;
        private int _selectedSkillIdx = -1;
        private const string PageMaterial = "古籍残页";
        private const string ShardMaterial = "道韵碎片"; // v0.5 Week 6 · 高阶功法 + 1 颗
        private const int PagesPerSkill = 5;
        private const int ShardsPerHighTier = 1;

        protected override void BuildBody()
        {
            Color gold = new Color(1f, 0.85f, 0.4f);

            // —— 地面金色八角符印 ——
            CaveVfx.SpawnGroundRune(transform, Vector3.zero, 1.7f,
                gold, sides: 8, lineWidth: 0.07f);

            // —— 高书架 + 顶部斗拱 ——
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "ScriptureShelf";
            shelf.transform.SetParent(transform, false);
            shelf.transform.localPosition = new Vector3(0, 1.1f, 0);
            shelf.transform.localScale = new Vector3(1.6f, 2.2f, 0.5f);
            var col = shelf.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = shelf.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.4f, 0.27f, 0.16f), ModuleColor * 0.3f);
                rend.material = mat;
            }
            // 顶部斗拱（一道横梁）
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0, 2.32f, 0);
            beam.transform.localScale = new Vector3(2.1f, 0.18f, 0.7f);
            var bcol = beam.GetComponent<Collider>();
            if (bcol != null) Destroy(bcol);
            var brend = beam.GetComponent<Renderer>();
            if (brend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.55f, 0.3f, 0.15f), gold * 0.4f);
                brend.material = mat;
            }

            // —— 顶部光柱（金色文气向上）——
            CaveVfx.SpawnLightBeam(transform, new Vector3(0, 2.5f, 0),
                height: 1.8f, baseRadius: 0.32f, color: gold);

            // —— 悬浮经卷（自转 + 上下浮 + emission 呼吸）——
            CaveVfx.SpawnHoveringObject(transform, new Vector3(0, 2.85f, 0),
                PrimitiveType.Cylinder, new Vector3(0.16f, 0.42f, 0.16f),
                new Color(0.95f, 0.85f, 0.55f), gold * 2.4f,
                hoverAmp: 0.08f, hoverFreq: 1.2f, spinSpeed: -45f);

            // —— 3 张飘飞的"金页" Cube ——
            CaveVfx.SpawnOrbitingParticles(transform, new Vector3(0, 2.5f, 0),
                count: 5, orbitRadius: 1.0f, orbitHeight: 0f,
                particleSize: 0.16f, color: gold,
                orbitSpeed: 35f, verticalBob: 0.35f);
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel()
        {
            _panelOpen = false;
            _selectedSkillIdx = -1;
        }

        // ============================== UI ==============================

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 720f, H = 500f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("📜 藏经阁 · 残页拼合，功法永传", titleStyle);

            int pages = SaveSystem.Instance.GetCaveItemCount(PageMaterial);
            int shards = SaveSystem.Instance.GetCaveItemCount(ShardMaterial);
            GUILayout.Label($"古籍残页 ×{pages}  ·  道韵碎片 ×{shards}  （高阶功法需额外消耗道韵碎片）",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            DrawSkillList();
            GUILayout.Space(8);
            DrawSkillDetail();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            DrawStartSkillSection();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                ClosePanel();
        }

        private void DrawSkillList()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(290));
            GUILayout.Label("功法目录", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));

            var unlocked = new HashSet<string>(SaveSystem.Instance.Data.unlockedSkillIds);
            var skills = ScriptureLibrary.AllSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                var s = skills[i];
                bool isUnlocked = unlocked.Contains(s.skillName);
                bool isSelected = i == _selectedSkillIdx;

                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true };
                string prefix = isUnlocked ? "<color=#88ff88>✓</color> " : "  ";
                string colorHex = "#" + ColorUtility.ToHtmlStringRGB(s.displayColor);
                string label = $"{prefix}<color={colorHex}>{s.skillName}</color>";

                Color prev = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = ModuleColor;
                if (GUILayout.Button(label, style, GUILayout.Height(28)))
                    _selectedSkillIdx = i;
                GUI.backgroundColor = prev;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSkillDetail()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(390), GUILayout.Height(240));
            var skills = ScriptureLibrary.AllSkills;
            if (_selectedSkillIdx < 0 || _selectedSkillIdx >= skills.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 从左侧选一卷功法",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            var entry = skills[_selectedSkillIdx];
            var save = SaveSystem.Instance.Data;
            bool unlocked = save.unlockedSkillIds.Contains(entry.skillName);
            int pages = SaveSystem.Instance.GetCaveItemCount(PageMaterial);
            int shards = SaveSystem.Instance.GetCaveItemCount(ShardMaterial);

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
            nameStyle.normal.textColor = entry.displayColor;
            string tier = entry.requiresShard ? " <color=#c080ff>· 高阶</color>" : "";
            GUILayout.Label(entry.skillName + tier, nameStyle);
            GUILayout.Label($"<i>{entry.skillType}</i>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11 });
            GUILayout.Space(4);
            GUILayout.Label(entry.description,
                new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true });

            GUILayout.FlexibleSpace();

            if (unlocked)
            {
                var okStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                okStyle.normal.textColor = new Color(0.6f, 0.95f, 0.6f);
                GUILayout.Label("✓ 已拼合 · 可在下方设为起手携带", okStyle);
            }
            else
            {
                bool hasPages = pages >= PagesPerSkill;
                bool hasShards = !entry.requiresShard || shards >= ShardsPerHighTier;
                bool canAssemble = hasPages && hasShards;

                string label;
                if (entry.requiresShard)
                {
                    if (canAssemble)
                        label = $"📜 拼合（古籍残页 ×{PagesPerSkill} + 道韵碎片 ×{ShardsPerHighTier}）";
                    else if (!hasPages)
                        label = $"残页不足（{pages}/{PagesPerSkill}）";
                    else
                        label = $"道韵碎片不足（{shards}/{ShardsPerHighTier}）";
                }
                else
                {
                    label = canAssemble
                        ? $"📜 拼合（古籍残页 ×{PagesPerSkill}）"
                        : $"残页不足（{pages}/{PagesPerSkill}）";
                }

                GUI.enabled = canAssemble;
                if (GUILayout.Button(label, GUILayout.Height(34)))
                {
                    TryAssemble(entry);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        private void DrawStartSkillSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("下次入梦携带（写入 Q 槽）",
                new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });

            var save = SaveSystem.Instance.Data;
            string current = save.pendingStartSkillId;

            GUILayout.BeginHorizontal();

            // "不携带"按钮
            bool noneSelected = string.IsNullOrEmpty(current);
            Color prev = GUI.backgroundColor;
            if (noneSelected) GUI.backgroundColor = new Color(0.55f, 0.55f, 0.55f);
            if (GUILayout.Button("不携带", GUILayout.Height(26)))
            {
                save.pendingStartSkillId = "";
                SaveSystem.Instance.Save();
            }
            GUI.backgroundColor = prev;

            // 每个已解锁功法一个按钮
            foreach (var id in save.unlockedSkillIds)
            {
                var entry = ScriptureLibrary.GetByName(id);
                bool isCurrent = current == id;
                Color p2 = GUI.backgroundColor;
                if (isCurrent) GUI.backgroundColor = entry != null ? entry.displayColor : Color.green;
                if (GUILayout.Button(id, GUILayout.Height(26)))
                {
                    save.pendingStartSkillId = id;
                    SaveSystem.Instance.Save();
                }
                GUI.backgroundColor = p2;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ============================== 逻辑 ==============================

        private void TryAssemble(ScriptureEntry entry)
        {
            // 检查碎片（高阶功法需要）
            if (entry.requiresShard &&
                SaveSystem.Instance.GetCaveItemCount(ShardMaterial) < ShardsPerHighTier)
            {
                Debug.Log($"<color=red>[藏经阁] 道韵碎片不足 {ShardsPerHighTier} 颗</color>");
                return;
            }

            if (!SaveSystem.Instance.ConsumeCaveItem(PageMaterial, PagesPerSkill))
            {
                Debug.Log($"<color=red>[藏经阁] 古籍残页不足 {PagesPerSkill} 张</color>");
                return;
            }

            if (entry.requiresShard)
            {
                if (!SaveSystem.Instance.ConsumeCaveItem(ShardMaterial, ShardsPerHighTier))
                {
                    // 几乎不可能：上面 GetCaveItemCount 通过了。但为防数据竞争补一句日志
                    Debug.LogWarning("[藏经阁] 道韵碎片消耗失败（数据异常）");
                }
            }

            var save = SaveSystem.Instance.Data;
            if (!save.unlockedSkillIds.Contains(entry.skillName))
                save.unlockedSkillIds.Add(entry.skillName);
            SaveSystem.Instance.Save();

            GameEvents.Publish(new GameEvents.ScriptureSkillUnlocked
            {
                SkillName = entry.skillName,
                TotalUnlocked = save.unlockedSkillIds.Count
            });

            Debug.Log($"<color=#e8d090>[藏经阁] 拼合成功：{entry.skillName}（已永久解锁，下次入梦可设为起手）</color>");
        }
    }

    // ===================================================================
    //                          功法注册表
    // ===================================================================

    /// <summary>藏经阁功法定义（不与商店 / 升级台的池子重叠，专属于古籍残页拼合）。</summary>
    public class ScriptureEntry
    {
        public string skillName;
        public string description;
        public SkillType skillType;
        public Color displayColor;
        /// <summary>v0.5 Week 6：高阶功法 = 需要额外消耗 1 颗道韵碎片才能拼合。</summary>
        public bool requiresShard = false;
        public System.Action<SkillData> configure;
    }

    public static class ScriptureLibrary
    {
        private static List<ScriptureEntry> _cache;
        public static IReadOnlyList<ScriptureEntry> AllSkills
        {
            get
            {
                if (_cache == null) Build();
                return _cache;
            }
        }

        public static ScriptureEntry GetByName(string name)
        {
            foreach (var e in AllSkills)
                if (e.skillName == name) return e;
            return null;
        }

        /// <summary>把藏经阁 entry 转成可装备的 SkillData 实例（运行时 SO）。</summary>
        public static SkillData BuildSkillData(ScriptureEntry entry)
        {
            if (entry == null) return null;
            var sd = ScriptableObject.CreateInstance<SkillData>();
            sd.name = entry.skillName;
            sd.skillName = entry.skillName;
            sd.description = entry.description;
            sd.skillType = entry.skillType;
            sd.rarity = ItemRarity.Tian;
            entry.configure?.Invoke(sd);
            return sd;
        }

        private static void Build()
        {
            _cache = new List<ScriptureEntry>();

            _cache.Add(new ScriptureEntry
            {
                skillName = "太虚剑诀",
                skillType = SkillType.AreaDamage,
                displayColor = new Color(0.85f, 0.9f, 1f),
                description = "<i>「剑光化作虚空，万物俱无形。」</i>\n· 范围伤害 · CD 10s\n· 基础 50 + 攻击力 80% 缩放",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Pierce;
                    sd.baseDamage = 50f;
                    sd.damageScaling = 0.8f;
                    sd.aoeRadius = 4.2f;
                    sd.cooldown = 10f;
                    sd.castSpeed = 1.2f;
                }
            });

            _cache.Add(new ScriptureEntry
            {
                skillName = "回春诀",
                skillType = SkillType.Heal,
                displayColor = new Color(0.65f, 1f, 0.65f),
                description = "<i>「春风化雨，万物复生。」</i>\n· 即时治疗 · CD 18s\n· 治疗 60 + 攻击力 50% 缩放",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Life;
                    sd.healAmount = 60f;
                    sd.healScaling = 0.5f;
                    sd.cooldown = 18f;
                }
            });

            _cache.Add(new ScriptureEntry
            {
                skillName = "逐云步",
                skillType = SkillType.Dash,
                displayColor = new Color(0.7f, 0.95f, 1f),
                description = "<i>「身随云移，步追风迹。」</i>\n· 位移 10m，留下风刃伤害带\n· CD 6s",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Wind;
                    sd.dashDistance = 10f;
                    sd.leaveTrail = true;
                    sd.baseDamage = 35f;
                    sd.damageScaling = 0.4f;
                    sd.cooldown = 6f;
                }
            });

            _cache.Add(new ScriptureEntry
            {
                skillName = "罡气护身",
                skillType = SkillType.Buff,
                displayColor = new Color(1f, 0.85f, 0.4f),
                description = "<i>「罡气环身，万邪难侵。」</i>\n· 6s 减伤 +40%\n· CD 20s",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Earth;
                    sd.cooldown = 20f;
                    sd.castSpeed = 1.5f;
                }
            });

            _cache.Add(new ScriptureEntry
            {
                skillName = "千机引",
                skillType = SkillType.Projectile,
                displayColor = new Color(1f, 0.55f, 0.55f),
                description = "<i>「机括千变，箭如雨下。」</i>\n· 5 发投射，前方扇形扩散 30°\n· CD 8s · 基础 22 + 攻击力 35% 缩放",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Fire;
                    sd.baseDamage = 22f;
                    sd.damageScaling = 0.35f;
                    sd.projectileSpeed = 14f;
                    sd.projectileCount = 5;
                    sd.spreadAngle = 30f;
                    sd.cooldown = 8f;
                    sd.maxCharges = 2;
                }
            });

            // ============= v0.5 Week 6 · 高阶功法（需道韵碎片）=============

            // 高阶 1：太虚剑诀（原"太虚剑诀"是 Tian 品但成本只有残页，现在改为需碎片）
            // 直接给"太虚剑诀"打 requiresShard 标记
            foreach (var e in _cache)
            {
                if (e.skillName == "太虚剑诀") e.requiresShard = true;
            }

            // 高阶 2：玄冥九转引（新功法）
            _cache.Add(new ScriptureEntry
            {
                skillName = "玄冥九转引",
                skillType = SkillType.AreaDamage,
                displayColor = new Color(0.65f, 0.5f, 1f),
                requiresShard = true,
                description = "<i>「九转玄冥气，万象皆封于冰。」</i>\n· 大范围冰元素 AOE · CD 12s\n· 基础 70 + 攻击力 90% · 必定冻结 1.5s",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Ice;
                    sd.baseDamage = 70f;
                    sd.damageScaling = 0.9f;
                    sd.aoeRadius = 5.0f;
                    sd.cooldown = 12f;
                    sd.castSpeed = 1.0f;
                }
            });

            // 高阶 3：太初雷音（新功法）—— 单发巨雷，速度极快，无穿透但伤害极高
            _cache.Add(new ScriptureEntry
            {
                skillName = "太初雷音",
                skillType = SkillType.Projectile,
                displayColor = new Color(1f, 0.95f, 0.4f),
                requiresShard = true,
                description = "<i>「一声太初雷音，可破诸天神光。」</i>\n· 单发巨型雷电 · CD 9s\n· 基础 90 + 攻击力 110%",
                configure = sd =>
                {
                    sd.elementTag = ElementTag.Thunder;
                    sd.baseDamage = 90f;
                    sd.damageScaling = 1.1f;
                    sd.projectileSpeed = 22f;
                    sd.projectileCount = 1;
                    sd.cooldown = 9f;
                    sd.maxCharges = 1;
                }
            });
        }
    }

    /// <summary>
    /// 起手功法装载器 —— 入梦时把 <see cref="SaveDataV1.pendingStartSkillId"/> 装备到 Q 槽。
    /// 装备完即清空 pendingStartSkillId，让玩家每次入梦都做一次选择（避免遗忘）。
    /// </summary>
    public static class StartSkillLoader
    {
        public static void Apply(PlayerController player)
        {
            if (player == null) return;
            var save = SaveSystem.Instance.Data;
            if (string.IsNullOrEmpty(save.pendingStartSkillId)) return;

            var entry = ScriptureLibrary.GetByName(save.pendingStartSkillId);
            if (entry == null)
            {
                Debug.LogWarning($"<color=yellow>[StartSkillLoader] 未知功法 id：{save.pendingStartSkillId}</color>");
                save.pendingStartSkillId = "";
                SaveSystem.Instance.Save();
                return;
            }

            var sd = ScriptureLibrary.BuildSkillData(entry);
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.EquipSkillToSlot(sd, 0);
                Debug.Log($"<color=#e8d090>[StartSkillLoader] 起手携带：{entry.skillName} → Q 槽位</color>");
            }

            // 一次性 —— 用完即清
            save.pendingStartSkillId = "";
            SaveSystem.Instance.Save();
        }
    }
}
