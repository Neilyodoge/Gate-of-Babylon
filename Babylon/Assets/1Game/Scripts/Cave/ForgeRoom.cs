using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 炼器房 · 第四个洞府模块（v0.5 Week 4）。
    ///
    /// 玩家把【寒铁矿 / 妖兽骨片】（CaveMaterial）+ 灵药等送入炼器炉，
    /// 选定一份"炼器配方"消耗对应素材 → 永久解锁一件【灵物 SO】，
    /// 该灵物 id 写入 <see cref="SaveDataV1.unlockedItemIds"/>，
    /// 下次入梦时由 <see cref="UnlockedItemPoolLoader"/> 注入 GameManager 的 itemPool，
    /// 使本局所有掉落 / 商店 / 宝箱 / 升级台都可能出现该灵物。
    ///
    /// 工艺：选定配方 → 检查素材库存 → 一键炼制（无延迟，瞬时完成），
    /// 与炼丹房的"按 GameTime 烧炼"风格区分，避免 4 个洞府模块都是"放进去等"。
    /// </summary>
    public class ForgeRoom : CaveModule
    {
        public override string ModuleName => "炼器房";
        public override string ModuleIcon => "⚒";
        public override string ModuleRole => "矿石 + 骨片 → 永久灵物";
        public override Color ModuleColor => new Color(0.55f, 0.65f, 0.85f);

        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private Vector2 _scroll;
        private int _selectedRecipeIdx = -1;

        protected override void BuildBody()
        {
            Color flame = new Color(1f, 0.55f, 0.2f);      // 炉火橙
            Color steel = new Color(0.55f, 0.7f, 0.9f);    // 蓝白钢

            // —— 地面六角符印（炼器符）——
            CaveVfx.SpawnGroundRune(transform, Vector3.zero, 1.6f,
                ModuleColor, sides: 6, lineWidth: 0.08f);

            // —— 主体：方形铁砧（拉宽） ——
            var anvil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anvil.name = "ForgeAnvil";
            anvil.transform.SetParent(transform, false);
            anvil.transform.localPosition = new Vector3(0, 0.4f, 0);
            anvil.transform.localScale = new Vector3(1.4f, 0.8f, 1f);
            var col = anvil.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = anvil.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.18f, 0.18f, 0.24f), ModuleColor * 0.45f);
                rend.material = mat;
            }

            // —— 砧上斜横的"铁锤"造型（拉长 Cube） ——
            var hammer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hammer.name = "ForgeHammer";
            hammer.transform.SetParent(transform, false);
            hammer.transform.localPosition = new Vector3(0.5f, 1.0f, 0.5f);
            hammer.transform.localRotation = Quaternion.Euler(0f, 30f, -35f);
            hammer.transform.localScale = new Vector3(0.2f, 0.18f, 1.1f);
            var hcol = hammer.GetComponent<Collider>();
            if (hcol != null) Destroy(hcol);
            var hrend = hammer.GetComponent<Renderer>();
            if (hrend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.35f, 0.3f, 0.28f), new Color(0.5f, 0.4f, 0.3f) * 0.6f);
                hrend.material = mat;
            }
            // 锤头
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "HammerHead";
            head.transform.SetParent(hammer.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.45f);
            head.transform.localScale = new Vector3(2.2f, 2.2f, 0.45f);
            var hdcol = head.GetComponent<Collider>();
            if (hdcol != null) Destroy(hdcol);
            var hdrend = head.GetComponent<Renderer>();
            if (hdrend != null)
            {
                var mat = MaterialHelper.CreateLitEmissive(
                    new Color(0.22f, 0.22f, 0.26f), flame * 0.6f);
                hdrend.material = mat;
            }

            // —— 砧前发光炉心（悬浮 + 呼吸 + 自转） ——
            CaveVfx.SpawnHoveringObject(transform, new Vector3(0, 1.5f, -0.05f),
                PrimitiveType.Sphere, Vector3.one * 0.5f,
                steel, flame * 2.2f,
                hoverAmp: 0.1f, hoverFreq: 1.4f, spinSpeed: 35f);

            // —— 4 颗火星沿圆周快速飞溅 ——
            CaveVfx.SpawnOrbitingParticles(transform, new Vector3(0, 1.5f, 0),
                count: 6, orbitRadius: 0.95f, orbitHeight: 0f,
                particleSize: 0.12f, color: flame,
                orbitSpeed: 200f, verticalBob: 0.25f);

            // —— 上升火星烟（炉膛冒气） ——
            CaveVfx.SpawnSmokeEmitter(transform, new Vector3(0, 0.9f, 0),
                color: flame, particleSize: 0.14f, spawnInterval: 0.18f,
                riseSpeed: 0.9f, lifetime: 1.0f, jitterRadius: 0.35f);
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel()
        {
            _panelOpen = false;
            _selectedRecipeIdx = -1;
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
            GUILayout.Label("⚒ 炼器房 · 凡铁妖骨锻成灵宝", titleStyle);
            GUILayout.Space(4);

            GUILayout.Label("一旦炼成，该灵物永久加入秘境掉落池（商店 / 宝箱 / 升级台均可能出现）。",
                new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(8);

            // 左侧：配方列表；右侧：详情
            GUILayout.BeginHorizontal();
            DrawRecipeList();
            GUILayout.Space(8);
            DrawRecipeDetail();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(28))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                ClosePanel();
        }

        private void DrawRecipeList()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(280));
            GUILayout.Label("配方列表", new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });
            _scroll = GUILayout.BeginScrollView(_scroll);

            var unlocked = new HashSet<string>(SaveSystem.Instance.Data.unlockedItemIds);
            var recipes = ForgeLibrary.AllRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                bool isUnlocked = unlocked.Contains(r.itemName);
                bool isSelected = i == _selectedRecipeIdx;

                var btnStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = true };
                string prefix = isUnlocked ? "<color=#88ff88>✓</color> " : "  ";
                string colorHex = "#" + ColorUtility.ToHtmlStringRGB(r.displayColor);
                string label = $"{prefix}<color={colorHex}>{r.itemName}</color>";

                Color prev = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = ModuleColor;
                if (GUILayout.Button(label, btnStyle, GUILayout.Height(28)))
                {
                    _selectedRecipeIdx = i;
                }
                GUI.backgroundColor = prev;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawRecipeDetail()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(400));

            var recipes = ForgeLibrary.AllRecipes;
            if (_selectedRecipeIdx < 0 || _selectedRecipeIdx >= recipes.Count)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("← 从左侧选一份配方",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            var recipe = recipes[_selectedRecipeIdx];
            var save = SaveSystem.Instance.Data;
            bool already = save.unlockedItemIds.Contains(recipe.itemName);

            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true };
            nameStyle.normal.textColor = recipe.displayColor;
            GUILayout.Label(recipe.itemName, nameStyle);

            GUILayout.Label(recipe.description,
                new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true });

            GUILayout.Space(8);
            GUILayout.Label("所需材料：", new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold });

            bool affordable = true;
            foreach (var cost in recipe.costs)
            {
                int have = SaveSystem.Instance.GetCaveItemCount(cost.materialName);
                bool ok = have >= cost.amount;
                if (!ok) affordable = false;
                var entryStyle = new GUIStyle(GUI.skin.label) { richText = true };
                string color = ok ? "#a0d090" : "#ff9090";
                GUILayout.Label($"  · <color={color}>{cost.materialName}  ×{cost.amount}（持有 {have}）</color>", entryStyle);
            }

            GUILayout.FlexibleSpace();

            if (already)
            {
                var okStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                okStyle.normal.textColor = new Color(0.6f, 0.95f, 0.6f);
                GUILayout.Label("✓ 已炼制 · 已加入秘境掉落池", okStyle);
            }
            else
            {
                GUI.enabled = affordable;
                if (GUILayout.Button(affordable ? "🔥 炼制" : "材料不足", GUILayout.Height(34)))
                {
                    TryForge(recipe);
                }
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
        }

        // ============================== 逻辑 ==============================

        private void TryForge(ForgeRecipe recipe)
        {
            // 二次检查
            foreach (var cost in recipe.costs)
            {
                if (SaveSystem.Instance.GetCaveItemCount(cost.materialName) < cost.amount)
                {
                    Debug.Log($"<color=red>[炼器房] {cost.materialName} 不足 {cost.amount}</color>");
                    return;
                }
            }

            // 消耗
            foreach (var cost in recipe.costs)
                SaveSystem.Instance.ConsumeCaveItem(cost.materialName, cost.amount);

            // 永久解锁
            var save = SaveSystem.Instance.Data;
            if (!save.unlockedItemIds.Contains(recipe.itemName))
                save.unlockedItemIds.Add(recipe.itemName);
            SaveSystem.Instance.Save();

            GameEvents.Publish(new GameEvents.ForgeItemUnlocked
            {
                ItemName = recipe.itemName,
                TotalUnlocked = save.unlockedItemIds.Count
            });

            Debug.Log($"<color=#b0d0ff>[炼器房] 炼制成功：{recipe.itemName}（已永久加入梦境掉落池）</color>");
        }
    }

    // ===================================================================
    //                          配方定义
    // ===================================================================

    [System.Serializable]
    public struct ForgeCost
    {
        public string materialName;
        public int amount;
        public ForgeCost(string n, int c) { materialName = n; amount = c; }
    }

    /// <summary>炼器配方：素材消耗 + 输出灵物 SO 的构造参数。</summary>
    public class ForgeRecipe
    {
        public string itemName;          // 兼作 unlockedItemIds 中的 id
        public string description;
        public Color displayColor;
        public List<ForgeCost> costs;
        public System.Action<ItemData> configure;   // 把 SO 配成对应词条
    }

    /// <summary>
    /// 炼器配方库 —— 写死 5 张配方，覆盖攻 / 防 / 速 / 暴 / 灼烧五种 build 风格。
    /// 后续可改为 ScriptableObject 配表，避免 hardcode。
    /// </summary>
    public static class ForgeLibrary
    {
        private static List<ForgeRecipe> _cache;
        public static IReadOnlyList<ForgeRecipe> AllRecipes
        {
            get
            {
                if (_cache == null) Build();
                return _cache;
            }
        }

        public static ForgeRecipe GetByName(string itemName)
        {
            foreach (var r in AllRecipes)
                if (r.itemName == itemName) return r;
            return null;
        }

        private static void Build()
        {
            _cache = new List<ForgeRecipe>();

            _cache.Add(new ForgeRecipe
            {
                itemName = "破劫之矛",
                description = "<i>「凡铁三铸，三焚于雷劫，方成破障之兵。」</i>\n· 攻击力 +35%\n· 攻速 +15%",
                displayColor = new Color(1f, 0.65f, 0.3f),
                costs = new List<ForgeCost> { new("寒铁矿", 5), new("妖兽骨片", 3) },
                configure = it =>
                {
                    it.category = ItemCategory.Attack;
                    it.rarity = ItemRarity.Di;
                    it.attackBonusPercent = 0.35f;
                    it.attackSpeedBonusPercent = 0.15f;
                }
            });

            _cache.Add(new ForgeRecipe
            {
                itemName = "玄铁护心镜",
                description = "<i>「上古玄铁淬炼成镜，可挡七劫三灾。」</i>\n· 最大生命 +30%\n· 受到伤害减免 +10%",
                displayColor = new Color(0.7f, 0.85f, 1f),
                costs = new List<ForgeCost> { new("寒铁矿", 4), new("火灵草灵药", 2) },
                configure = it =>
                {
                    it.category = ItemCategory.Defense;
                    it.rarity = ItemRarity.Di;
                    it.maxHpBonusPercent = 0.30f;
                    it.damageReductionBonus = 0.10f;
                }
            });

            _cache.Add(new ForgeRecipe
            {
                itemName = "灵犀步靴",
                description = "<i>「轻如鹿步，灵似游鱼，能避万钧之雷。」</i>\n· 移速 +25%\n· 攻速 +10%",
                displayColor = new Color(0.6f, 0.95f, 0.8f),
                costs = new List<ForgeCost> { new("妖兽骨片", 4), new("寒霜花灵药", 2) },
                configure = it =>
                {
                    it.category = ItemCategory.Movement;
                    it.rarity = ItemRarity.Di;
                    it.moveSpeedBonusPercent = 0.25f;
                    it.attackSpeedBonusPercent = 0.10f;
                }
            });

            _cache.Add(new ForgeRecipe
            {
                itemName = "百兽噬魂珠",
                description = "<i>「饮百兽之血，吸其灵魂为我所用。」</i>\n· 暴击率 +15%\n· 击杀回血 +5",
                displayColor = new Color(1f, 0.55f, 0.55f),
                costs = new List<ForgeCost> { new("妖兽骨片", 6) },
                configure = it =>
                {
                    it.category = ItemCategory.Attack;
                    it.rarity = ItemRarity.Di;
                    it.critRateBonus = 0.15f;
                    it.healOnKill = 5f;
                }
            });

            _cache.Add(new ForgeRecipe
            {
                itemName = "离火炼狱符",
                description = "<i>「天南离火炼狱之符，使敌身焚于无形之中。」</i>\n· 攻击附带灼烧 10 / 秒\n· 攻击力 +20%",
                displayColor = new Color(1f, 0.4f, 0.2f),
                costs = new List<ForgeCost> { new("寒铁矿", 3), new("妖兽骨片", 3), new("火灵草灵药", 1) },
                configure = it =>
                {
                    it.category = ItemCategory.Anomaly;
                    it.rarity = ItemRarity.Tian;
                    it.burnDamagePerSecond = 10f;
                    it.attackBonusPercent = 0.20f;
                    it.modTag = ElementTag.Fire;
                }
            });

            // v0.5 Week 6 · 妖丹 / 灵砂 配方
            _cache.Add(new ForgeRecipe
            {
                itemName = "九转赤金丸",
                description = "<i>「九转炼丹之核，蕴含妖兽精魄之力。」</i>\n· 攻击力 +35%\n· 攻速 +15%\n· 击杀回血 +3",
                displayColor = new Color(1f, 0.65f, 0.15f),
                costs = new List<ForgeCost> { new("妖丹", 1), new("灵砂", 4), new("妖兽骨片", 4) },
                configure = it =>
                {
                    it.category = ItemCategory.Attack;
                    it.rarity = ItemRarity.Tian;
                    it.attackBonusPercent = 0.35f;
                    it.attackSpeedBonusPercent = 0.15f;
                    it.healOnKill = 3f;
                }
            });

            _cache.Add(new ForgeRecipe
            {
                itemName = "业火印玺",
                description = "<i>「以灵砂炼成的印玺，凡所拓印皆生业火。」</i>\n· 攻击附带灼烧 14 / 秒\n· 暴击率 +12%\n· 攻击力 +15%",
                displayColor = new Color(1f, 0.35f, 0.1f),
                costs = new List<ForgeCost> { new("灵砂", 5), new("寒铁矿", 2) },
                configure = it =>
                {
                    it.category = ItemCategory.Anomaly;
                    it.rarity = ItemRarity.Di;
                    it.burnDamagePerSecond = 14f;
                    it.critRateBonus = 0.12f;
                    it.attackBonusPercent = 0.15f;
                    it.modTag = ElementTag.Fire;
                }
            });
        }
    }

    /// <summary>
    /// 已解锁灵物注入器 —— 把 <see cref="SaveDataV1.unlockedItemIds"/> 转成运行时 <see cref="ItemData"/> 实例，
    /// 追加到 GameManager 的 itemPool 中，使本局所有掉落 / 商店 / 宝箱都可能出现。
    /// </summary>
    public static class UnlockedItemPoolLoader
    {
        /// <summary>
        /// 把基础 itemPool（Inspector 配置）和 SaveData 中已解锁的灵物合并成新数组。
        /// 不修改原数组，调用方决定是否替换。
        /// </summary>
        public static ItemData[] Augment(ItemData[] basePool)
        {
            var ids = SaveSystem.Instance.Data.unlockedItemIds;
            if (ids == null || ids.Count == 0) return basePool;

            var list = new List<ItemData>();
            if (basePool != null) list.AddRange(basePool);

            int added = 0;
            foreach (var id in ids)
            {
                // 跳过已存在（防止 Inspector 已配 + 又解锁导致重复）
                if (list.Exists(x => x != null && x.itemName == id)) continue;

                var recipe = ForgeLibrary.GetByName(id);
                if (recipe == null) continue;

                var so = ScriptableObject.CreateInstance<ItemData>();
                so.name = recipe.itemName;
                so.itemName = recipe.itemName;
                so.description = recipe.description;
                so.scope = ItemScope.RunOnly;
                so.stackable = true;
                so.qualitativeThresholds = new[] { 3, 5 };
                so.rarity = ItemRarity.Di;
                recipe.configure?.Invoke(so);

                list.Add(so);
                added++;
            }

            if (added > 0)
                Debug.Log($"<color=#b0d0ff>[UnlockedItemPoolLoader] 已解锁 {added} 件炼器灵物注入梦境掉落池</color>");

            return list.ToArray();
        }
    }
}
