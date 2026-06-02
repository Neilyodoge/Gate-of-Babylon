using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 配置速查面板 —— 一站式查看和跳转所有配置文件
    /// 菜单：仙途秘境 → ⑦ 配置速查
    /// </summary>
    public class ConfigDashboard : EditorWindow
    {
        // ============================================================
        // 常量
        // ============================================================
        private const string GAME_CONFIG_PATH = "Assets/1Game/Resources/GameConfig.asset";
        private const string AUDIO_CONFIG_PATH = "Assets/1Game/Resources/AudioConfig.asset";
        private const string MONSTER_PREFABS_PATH = "Assets/1Game/Resources/MonsterPrefabs.asset";
        private const string ITEMS_DIR = "Assets/1Game/Data/Items";
        private const string SKILLS_DIR = "Assets/1Game/Data/Skills";

        // ============================================================
        // 状态
        // ============================================================
        private Vector2 _scrollPos;
        private bool _foldGlobal = true;
        private bool _foldAudio = true;
        private bool _foldMonster = true;
        private bool _foldItems = true;
        private bool _foldSkills = true;
        private bool _foldScene = true;
        private bool _foldQuickStart = false;

        // 缓存
        private List<string> _itemAssets = new List<string>();
        private List<string> _skillAssets = new List<string>();
        private double _lastRefreshTime;

        // 样式缓存
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _statusOk;
        private GUIStyle _statusMissing;
        private GUIStyle _tipStyle;
        private GUIStyle _sectionBgStyle;
        private bool _stylesInitialized;

        // ============================================================
        // 菜单入口
        // ============================================================
        [MenuItem("仙途秘境/⑦ 配置速查", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigDashboard>("⚡ 配置速查");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        // ============================================================
        // 初始化
        // ============================================================
        private void OnEnable()
        {
            RefreshAssetLists();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(4, 4, 8, 4)
            };

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                margin = new RectOffset(4, 4, 4, 2)
            };

            _statusOk = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
            };

            _statusMissing = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(1f, 0.5f, 0.3f) }
            };

            _tipStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _sectionBgStyle = new GUIStyle("box")
            {
                margin = new RectOffset(2, 2, 2, 6),
                padding = new RectOffset(8, 8, 6, 6)
            };

            _stylesInitialized = true;
        }

        // ============================================================
        // 刷新资产列表
        // ============================================================
        private void RefreshAssetLists()
        {
            _itemAssets.Clear();
            _skillAssets.Clear();

            if (AssetDatabase.IsValidFolder(ITEMS_DIR))
            {
                var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ITEMS_DIR });
                _itemAssets = guids.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".asset"))
                    .OrderBy(p => Path.GetFileNameWithoutExtension(p))
                    .ToList();
            }

            if (AssetDatabase.IsValidFolder(SKILLS_DIR))
            {
                var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { SKILLS_DIR });
                _skillAssets = guids.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".asset"))
                    .OrderBy(p => Path.GetFileNameWithoutExtension(p))
                    .ToList();
            }

            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        // ============================================================
        // 绘制
        // ============================================================
        private void OnGUI()
        {
            InitStyles();

            // 自动刷新（每 5 秒）
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > 5)
                RefreshAssetLists();

            // 标题栏
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("⚡ 仙途秘境 · 配置速查", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50)))
                RefreshAssetLists();
            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // ---- 快速上手 ----
            DrawQuickStart();

            EditorGUILayout.Space(4);

            // ---- 1. 全局配置 ----
            DrawGlobalConfig();

            // ---- 2. 音效配置 ----
            DrawAudioConfig();

            // ---- 3. 怪物配置 ----
            DrawMonsterConfig();

            // ---- 4. 灵物数据 ----
            DrawItemData();

            // ---- 5. 功法数据 ----
            DrawSkillData();

            // ---- 6. 场景配置 ----
            DrawSceneConfig();

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        // 各区域绘制
        // ============================================================

        private void DrawQuickStart()
        {
            _foldQuickStart = EditorGUILayout.Foldout(_foldQuickStart, "🚀 快速上手（首次搭建流程）", true, EditorStyles.foldoutHeader);
            if (!_foldQuickStart) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            DrawMenuButton("① 配置 Tags 和 Layers", "仙途秘境/① 配置 Tags 和 Layers", "设置 Enemy Tag/Layer，配置物理碰撞矩阵");
            DrawMenuButton("② 创建 Demo1 测试数据", "仙途秘境/② 创建 Demo1 测试数据", "批量创建灵物和功法 SO 资产");
            DrawMenuButton("③ 创建 Animator Controller", "仙途秘境/③ 创建 Animator Controller", "创建玩家动画状态机");
            DrawMenuButton("④ 创建 Demo1 场景文件", "仙途秘境/⑤ 创建 Demo1 场景文件", "在 Scenes/ 下生成 Demo1.unity");
            DrawMenuButton("⑤ 自动配置 Demo1 场景", "仙途秘境/④ 自动配置 Demo1 场景", "自动绑定所有数据到场景");
            DrawMenuButton("⑥ 创建怪物预制体配置", "仙途秘境/⑥ 创建怪物预制体配置", "创建 MonsterPrefabs.asset");
            DrawMenuButton("⑦ 配置速查（本窗口）", "仙途秘境/⑦ 配置速查", "打开本面板");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("完成以上步骤后点击 Play 即可运行 ▶", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawGlobalConfig()
        {
            _foldGlobal = EditorGUILayout.Foldout(_foldGlobal, "🎮 GameConfig — 游戏数值配置", true, EditorStyles.foldoutHeader);
            if (!_foldGlobal) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            bool exists = AssetExists(GAME_CONFIG_PATH);
            DrawAssetRow("GameConfig.asset", GAME_CONFIG_PATH, exists,
                "玩家属性 · 闪避充能 · 敌人属性 · 难度曲线 · 房间大小 · 精英怪 · 可破坏物 · 掉落概率 · 功法掉落 · 近战参数 · 技能速度");

            if (exists)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("常用调整：", _subHeaderStyle);
                DrawTipRow("让玩家更肉", "↑ 玩家最大生命值、↑ 玩家减伤比例");
                DrawTipRow("让战斗更快", "↑ 玩家攻击速度、↑ 玩家移动速度");
                DrawTipRow("让掉落更多", "↑ 敌人掉落概率、↑ 通关掉落概率、↑ 通关额外掉落数");
                DrawTipRow("让功法更多", "↑ 功法掉落概率、↑ 通关功法掉落概率");
                DrawTipRow("降低难度", "↓ 基础敌人数量、↓ 每层血量倍率");
                DrawTipRow("调整暴击", "↑ 玩家暴击率、↑ 玩家暴击伤害");
                DrawTipRow("增加闪避", "↑ 闪避充能层数（默认2层）");
                DrawTipRow("调精英怪", "↑↓ 精英怪出现概率、↑↓ 精英怪最低层数");
                DrawTipRow("调可破坏物", "↑↓ 可破坏物数量、↑↓ 可破坏物掉落概率");
                DrawTipRow("调高品质掉率", "↓ 凡品掉率权重、↑ 地品/天品掉率权重");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAudioConfig()
        {
            _foldAudio = EditorGUILayout.Foldout(_foldAudio, "🔊 AudioConfig — 音效资源配置", true, EditorStyles.foldoutHeader);
            if (!_foldAudio) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            bool exists = AssetExists(AUDIO_CONFIG_PATH);
            DrawAssetRow("AudioConfig.asset", AUDIO_CONFIG_PATH, exists,
                "所有音效和 BGM 的 AudioClip 引用槽位");

            if (exists)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("配置方法：在 Inspector 中将音频文件拖入对应槽位", _tipStyle);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("槽位分类：", _subHeaderStyle);

                var audioConfig = AssetDatabase.LoadAssetAtPath<AudioConfig>(AUDIO_CONFIG_PATH);
                if (audioConfig != null)
                {
                    int total = 0, filled = 0;
                    CountAudioSlots(audioConfig, ref total, ref filled);

                    var rect = EditorGUILayout.GetControlRect(false, 20);
                    float progress = total > 0 ? (float)filled / total : 0;
                    EditorGUI.ProgressBar(rect, progress, $"音效配置进度：{filled} / {total} 个槽位已填入");
                    EditorGUILayout.Space(4);
                }

                DrawTipRow("玩家·攻击", "meleeAttacks[0~2]、meleeHits[]、critHit、killConfirm");
                DrawTipRow("玩家·动作", "dash、playerHurt[]、playerDeath、footstep");
                DrawTipRow("技能", "skillCastDefault、projectileFire/Hit、aoeExplosion、buffApply");
                DrawTipRow("敌人", "enemyHurt[]、enemyDeath[]、enemyAttack、bossAppear");
                DrawTipRow("灵物", "itemPickup[0~4]、skillPickup、itemDecompose、质变/Synergy");
                DrawTipRow("UI", "uiClick、uiOpen/Close、shopBuy/Fail、realmBreakthrough");
                DrawTipRow("环境", "portalActivate/Teleport、chestOpen、springHeal、陷阱");
                DrawTipRow("BGM", "bgmBattle[0~2]、bgmBoss、bgmShop、bgmVictory");
                DrawTipRow("音量", "masterVolume、sfxVolume、bgmVolume、uiVolume");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMonsterConfig()
        {
            _foldMonster = EditorGUILayout.Foldout(_foldMonster, "👾 MonsterPrefabs — 怪物模型配置", true, EditorStyles.foldoutHeader);
            if (!_foldMonster) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            bool exists = AssetExists(MONSTER_PREFABS_PATH);
            DrawAssetRow("MonsterPrefabs.asset", MONSTER_PREFABS_PATH, exists,
                "5 种敌人类型的模型 Prefab 引用（为空则自动使用胶囊体）");

            if (exists)
            {
                var mp = AssetDatabase.LoadAssetAtPath<MonsterPrefabs>(MONSTER_PREFABS_PATH);
                if (mp != null)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("配置方法：将怪物模型 Prefab 拖入对应槽位", _tipStyle);
                    EditorGUILayout.Space(2);

                    DrawPrefabSlot("普通小怪", mp.普通小怪Prefab, "基础近战 · Creeper 系列");
                    DrawPrefabSlot("远程弓箭手", mp.远程敌人Prefab, "远程投射物 · Haunt 系列");
                    DrawPrefabSlot("冲锋型", mp.冲锋敌人Prefab, "蓄力冲锋 · Lurker 系列");
                    DrawPrefabSlot("AOE 法师", mp.法师敌人Prefab, "范围魔法 · Soul Mage 系列");
                    DrawPrefabSlot("Boss", mp.Boss敌人Prefab, "多阶段 · Dragon Darkness 系列");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawItemData()
        {
            string label = $"🔮 灵物数据 — {_itemAssets.Count} 个";
            _foldItems = EditorGUILayout.Foldout(_foldItems, label, true, EditorStyles.foldoutHeader);
            if (!_foldItems) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            EditorGUILayout.LabelField($"目录：{ITEMS_DIR}/", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            if (_itemAssets.Count == 0)
            {
                EditorGUILayout.LabelField("暂无灵物数据，请执行 ② 创建 Demo1 测试数据", _statusMissing);
            }
            else
            {
                foreach (var path in _itemAssets)
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                    if (item == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    // 品阶颜色标记
                    string rarityIcon = GetRarityIcon(item.rarity);
                    string categoryIcon = GetCategoryIcon(item.category);

                    if (GUILayout.Button($"{rarityIcon} {item.itemName} {categoryIcon}",
                        EditorStyles.linkLabel, GUILayout.Height(18)))
                    {
                        Selection.activeObject = item;
                        EditorGUIUtility.PingObject(item);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{item.rarity} · {item.category}", EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新建灵物", GUILayout.Height(22)))
            {
                // 在 Items 目录下创建新灵物
                if (!AssetDatabase.IsValidFolder(ITEMS_DIR))
                {
                    AssetDatabase.CreateFolder("Assets/1Game/Data", "Items");
                }
                var newItem = ScriptableObject.CreateInstance<ItemData>();
                string newPath = AssetDatabase.GenerateUniqueAssetPath(ITEMS_DIR + "/新灵物.asset");
                AssetDatabase.CreateAsset(newItem, newPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = newItem;
                EditorGUIUtility.PingObject(newItem);
                RefreshAssetLists();
            }
            if (GUILayout.Button("打开目录", GUILayout.Height(22)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(ITEMS_DIR);
                if (folder != null)
                {
                    Selection.activeObject = folder;
                    EditorGUIUtility.PingObject(folder);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSkillData()
        {
            string label = $"📜 功法数据 — {_skillAssets.Count} 个";
            _foldSkills = EditorGUILayout.Foldout(_foldSkills, label, true, EditorStyles.foldoutHeader);
            if (!_foldSkills) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            EditorGUILayout.LabelField($"目录：{SKILLS_DIR}/", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            if (_skillAssets.Count == 0)
            {
                EditorGUILayout.LabelField("暂无功法数据，请执行 ② 创建 Demo1 测试数据", _statusMissing);
            }
            else
            {
                foreach (var path in _skillAssets)
                {
                    var skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                    if (skill == null) continue;

                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button($"📜 {skill.skillName}",
                        EditorStyles.linkLabel, GUILayout.Height(18)))
                    {
                        Selection.activeObject = skill;
                        EditorGUIUtility.PingObject(skill);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{skill.skillType} · CD {skill.cooldown}s · 伤害 {skill.baseDamage}",
                        EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新建功法", GUILayout.Height(22)))
            {
                if (!AssetDatabase.IsValidFolder(SKILLS_DIR))
                {
                    AssetDatabase.CreateFolder("Assets/1Game/Data", "Skills");
                }
                var newSkill = ScriptableObject.CreateInstance<SkillData>();
                string newPath = AssetDatabase.GenerateUniqueAssetPath(SKILLS_DIR + "/新功法.asset");
                AssetDatabase.CreateAsset(newSkill, newPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = newSkill;
                EditorGUIUtility.PingObject(newSkill);
                RefreshAssetLists();
            }
            if (GUILayout.Button("打开目录", GUILayout.Height(22)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(SKILLS_DIR);
                if (folder != null)
                {
                    Selection.activeObject = folder;
                    EditorGUIUtility.PingObject(folder);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSceneConfig()
        {
            _foldScene = EditorGUILayout.Foldout(_foldScene, "🎬 Demo1Setup — 场景配置", true, EditorStyles.foldoutHeader);
            if (!_foldScene) return;

            EditorGUILayout.BeginVertical(_sectionBgStyle);

            EditorGUILayout.LabelField("场景中 Demo1Setup 组件的 Inspector 字段：", _tipStyle);
            EditorGUILayout.Space(2);

            DrawTipRow("itemPool", "灵物数据数组（掉落池）— 可选，自动配置会填入");
            DrawTipRow("testSkillQ/E/R", "Q/E/R 槽位默认技能 — 可选，有兜底");
            DrawTipRow("playerModelPrefab", "角色模型 Prefab — 可选，为空用胶囊体");
            DrawTipRow("animatorController", "动画控制器 — 可选，自动创建");
            DrawTipRow("slashVFXPrefab", "刀光特效 — 可选");
            DrawTipRow("hitVFXPrefab", "打击特效 — 可选");
            DrawTipRow("projectilePrefab", "投射物 Prefab — 可选，自动创建");

            EditorGUILayout.Space(4);

            if (GUILayout.Button("自动配置场景（绑定所有数据）", GUILayout.Height(24)))
            {
                EditorApplication.ExecuteMenuItem("仙途秘境/④ 自动配置 Demo1 场景");
            }

            EditorGUILayout.EndVertical();
        }

        // ============================================================
        // 辅助绘制方法
        // ============================================================

        /// <summary>绘制资产行：名称 + 状态 + 选中按钮</summary>
        private void DrawAssetRow(string displayName, string assetPath, bool exists, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();

            // 状态图标
            GUILayout.Label(exists ? "✅" : "❌", GUILayout.Width(20));

            // 名称（可点击）
            if (exists)
            {
                if (GUILayout.Button(displayName, EditorStyles.linkLabel, GUILayout.Height(18)))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
            }
            else
            {
                GUILayout.Label(displayName + "（未创建）", _statusMissing);
            }

            GUILayout.FlexibleSpace();

            // 路径提示
            GUILayout.Label(assetPath, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            // 用途说明
            EditorGUILayout.LabelField("    " + tooltip, EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>绘制菜单按钮行</summary>
        private void DrawMenuButton(string label, string menuPath, string description)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(label, GUILayout.Height(22), GUILayout.Width(260)))
            {
                EditorApplication.ExecuteMenuItem(menuPath);
            }
            GUILayout.Label(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制提示行：左侧标签 + 右侧说明</summary>
        private void DrawTipRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("  " + label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(value, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>绘制 Prefab 槽位状态</summary>
        private void DrawPrefabSlot(string label, GameObject prefab, string description)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(prefab != null ? "  ✅" : "  ⬜", GUILayout.Width(30));
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(80));

            if (prefab != null)
                GUILayout.Label(prefab.name, EditorStyles.miniLabel, GUILayout.Width(120));
            else
                GUILayout.Label("（未配置，使用胶囊体）", _statusMissing, GUILayout.Width(120));

            GUILayout.Label(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // 工具方法
        // ============================================================

        private static bool AssetExists(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Object>(path) != null;
        }

        /// <summary>统计 AudioConfig 中已填入的音效槽位数</summary>
        private void CountAudioSlots(AudioConfig config, ref int total, ref int filled)
        {
            // 单个 AudioClip 字段
            var singleFields = new[]
            {
                config.critHit, config.killConfirm, config.dash, config.playerDeath,
                config.footstep, config.skillCastDefault, config.skillReady,
                config.projectileFire, config.projectileHit, config.aoeExplosion,
                config.buffApply, config.enemyAttack, config.enemyCharge,
                config.enemyProjectile, config.bossAppear, config.bossSpecialAttack,
                config.skillPickup, config.itemDecompose, config.qualitativeTransmute,
                config.synergyActivate, config.itemDrop, config.uiClick, config.uiOpen,
                config.uiClose, config.shopBuy, config.shopFail, config.realmBreakthrough,
                config.gameWin, config.gameLose, config.portalActivate, config.portalTeleport,
                config.chestOpen, config.springHeal, config.trapSpike, config.trapFire,
                config.bgmMenu, config.bgmShop, config.bgmBoss, config.bgmVictory
            };

            total += singleFields.Length;
            filled += singleFields.Count(c => c != null);

            // 数组字段
            CountArray(config.meleeAttacks, 3, ref total, ref filled);
            CountArray(config.meleeHits, ref total, ref filled);
            CountArray(config.playerHurt, ref total, ref filled);
            CountArray(config.enemyHurt, ref total, ref filled);
            CountArray(config.enemyDeath, ref total, ref filled);
            CountArray(config.itemPickup, 5, ref total, ref filled);
            CountArray(config.bgmBattle, ref total, ref filled);
        }

        private void CountArray(AudioClip[] arr, ref int total, ref int filled)
        {
            if (arr == null || arr.Length == 0) return;
            total += arr.Length;
            filled += arr.Count(c => c != null);
        }

        private void CountArray(AudioClip[] arr, int expectedLength, ref int total, ref int filled)
        {
            total += expectedLength;
            if (arr == null) return;
            filled += arr.Take(expectedLength).Count(c => c != null);
        }

        private string GetRarityIcon(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Fan: return "⚪";
                case ItemRarity.Ling: return "🟢";
                case ItemRarity.Xuan: return "🔵";
                case ItemRarity.Di: return "🟣";
                case ItemRarity.Tian: return "🟡";
                default: return "⚪";
            }
        }

        private string GetCategoryIcon(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Attack: return "⚔️";
                case ItemCategory.Defense: return "🛡️";
                case ItemCategory.Movement: return "👟";
                case ItemCategory.Anomaly: return "🔮";
                case ItemCategory.Pill: return "💊";
                case ItemCategory.Skill: return "📜";
                default: return "";
            }
        }
    }
}
