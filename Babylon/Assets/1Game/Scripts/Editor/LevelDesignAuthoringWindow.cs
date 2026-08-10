using System.Collections.Generic;
using System.Linq;
using Edgar.Unity;
using Edgar.Unity.Diagnostics;
using UnityEditor;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu.Editor
{
    public sealed class LevelDesignAuthoringWindow : EditorWindow
    {
        private const string ConfigPath =
            "Assets/1Game/Resources/LevelDesign/怪物与首领生成配置.asset";
        private const string DatabasePath =
            "Assets/1Game/Resources/LevelDesign/关卡数据库.asset";
        private const string StarterConfigPath =
            "Assets/1Game/Data/LevelDesign/Templates/新手怪物与首领模板.asset";
        private const string StarterRoomPrefabPath =
            "Assets/1Game/Resources/LevelDesign/EdgarGrid3D/RoomTemplates/Generated/Rooms/WB_Outer_Battle.prefab";

        private const string AdvancedModeSessionKey =
            "XianTu.LevelDesignAuthoringWindow.AdvancedMode";

        private static readonly string[] DailyTabs =
        {
            "怪物生成", "首领随机", "房间制作"
        };

        private static readonly string[] AdvancedTabs =
        {
            "怪物生成", "首领随机", "房间制作",
            "秘境结构", "房间规则", "遭遇规则", "剧情事件", "首领阶段"
        };

        private DungeonLevelAuthoringConfig _config;
        private SerializedObject _serializedConfig;
        private LevelDesignAssetDatabase _levelDatabase;
        private SerializedObject _serializedLevelDatabase;
        private int _tab;
        private bool _showAdvanced;
        private Vector2 _scroll;
        private GameObject _roomRoot;
        private DoorHandlerGrid3D _doorTemplate;
        private bool _hideRendererWhenConverting;
        private readonly List<string> _validationMessages = new();

        [MenuItem("仙途秘境/关卡工具/关卡配置与房间预制体", false, 300)]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelDesignAuthoringWindow>("关卡制作工具");
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _showAdvanced = SessionState.GetBool(AdvancedModeSessionKey, false);
            LoadConfig();
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                var selected = Selection.activeGameObject;
                _doorTemplate = selected.GetComponent<DoorHandlerGrid3D>() ?? _doorTemplate;
                _roomRoot = selected.GetComponentInParent<RoomTemplateSettingsGrid3D>()?.gameObject
                            ?? PrefabUtility.GetNearestPrefabInstanceRoot(selected)
                            ?? selected;
            }
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_tab == 2)
            {
                DrawPrefabTools();
                return;
            }

            bool generationTab = _tab <= 1;
            Object target = generationTab ? _config : _levelDatabase;
            SerializedObject serialized = generationTab
                ? _serializedConfig
                : _serializedLevelDatabase;
            if (target == null)
            {
                EditorGUILayout.HelpBox(
                    generationTab
                        ? "尚未创建怪物与首领生成配置。"
                        : "尚未创建关卡数据库。",
                    MessageType.Info);
                if (GUILayout.Button(
                        generationTab ? "创建默认生成配置" : "创建空白关卡数据库",
                        GUILayout.Height(32f)))
                {
                    if (generationTab) CreateDefaultConfig();
                    else CreateEmptyLevelDatabase();
                }
                return;
            }

            serialized.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0:
                    EditorGUILayout.HelpBox(
                        "日常调怪只需要改下面两块：先登记可用小怪，再调整外环、连接区、内环的数量和近战/远程/法术比例。",
                        MessageType.Info);
                    if (_showAdvanced)
                    {
                        DrawConfigProperty(
                            "EnemyPool",
                            "普通小怪池",
                            "高级配置可额外调整单个怪物的威胁成本和同类权重。精英怪不进入这个池。");
                        DrawConfigProperty(
                            "PopulationPresets",
                            "区域生成高级参数",
                            "高级配置：包含威胁预算、同时存活上限、波数和增援时机。");
                    }
                    else
                    {
                        DrawSimpleEnemyPool();
                        DrawSimplePopulationPresets();
                    }
                    break;
                case 1:
                    if (_showAdvanced)
                    {
                        DrawConfigProperty(
                            "BossPool",
                            "首领随机池",
                            "高级配置可额外填写事件条件和房间标签；没有候选时沿用当前区域首领。");
                    }
                    else
                    {
                        DrawSimpleBossPool();
                    }
                    break;
                case 3:
                    DrawDatabaseProperty(
                        "MapStructures",
                        "秘境结构",
                        "每个秘境一条，只配置仍在运行时生效的敌人数值倍率、模块稀有度偏移和阶段返回点。");
                    break;
                case 4:
                    DrawDatabaseProperty(
                        "RoomContents",
                        "房间规则",
                        "高级配置。普通房不需要逐个填写；仅在增加特殊房型、分区规则或触发方式时修改。");
                    break;
                case 5:
                    DrawDatabaseProperty(
                        "Encounters",
                        "遭遇规则",
                        "高级配置。普通战斗沿用已有规则；仅在制作伏击、巡逻、预置休眠或脚本首领等特殊房间时修改。");
                    break;
                case 6:
                    DrawDatabaseProperty(
                        "StoryEvents",
                        "剧情事件",
                        "配置事件正文、前置条件、玩家选项以及每个选项带来的结果。");
                    break;
                case 7:
                    DrawDatabaseProperty(
                        "BossPhases",
                        "首领阶段",
                        "按首领编号、事件条件和优先级选择登场对白及属性修正。");
                    break;
            }
            EditorGUILayout.EndScrollView();

            if (serialized.ApplyModifiedProperties())
                EditorUtility.SetDirty(target);
            DrawConfigFooter(serialized, target);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                _showAdvanced ? "高级配置模式" : "日常制作模式",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            bool nextAdvanced = GUILayout.Toggle(
                _showAdvanced,
                new GUIContent(
                    "显示高级配置",
                    "房间规则、遭遇规则、剧情和首领阶段通常由程序预设，日常制作无需打开。"),
                EditorStyles.toolbarButton,
                GUILayout.Width(110f));
            EditorGUILayout.EndHorizontal();

            if (nextAdvanced != _showAdvanced)
            {
                _showAdvanced = nextAdvanced;
                _tab = 0;
                _scroll = Vector2.zero;
                SessionState.SetBool(AdvancedModeSessionKey, _showAdvanced);
            }

            string[] tabs = _showAdvanced ? AdvancedTabs : DailyTabs;
            _tab = GUILayout.SelectionGrid(
                _tab,
                tabs,
                _showAdvanced ? 4 : 3,
                EditorStyles.toolbarButton);

            if (!_showAdvanced)
            {
                EditorGUILayout.HelpBox(
                    "推荐流程：① 怪物生成调数量和配比　② 首领随机登记候选　③ 房间制作处理范围与连接点。其他规则已有默认配置。",
                    MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        new GUIContent(
                            "一键套用新手配置模板",
                            "把怪物池、三区数量配比和首领池恢复为一套可直接运行的示例。"),
                        GUILayout.Height(28f)))
                    ApplyStarterConfigTemplate();
                if (GUILayout.Button(
                        new GUIContent(
                            "复制新手房间模板",
                            "另存一个已经配置好范围、刷新区、内容点和连接点的可编辑战斗房。"),
                        GUILayout.Height(28f)))
                    DuplicateStarterRoomTemplate();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "高级配置会影响房间生命周期、剧情条件和首领阶段。普通关卡制作无需修改。",
                    MessageType.Warning);
            }
        }

        private void DrawConfigProperty(string propertyName, string title, string help)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(help, MessageType.Info);
            EditorGUILayout.PropertyField(
                _serializedConfig.FindProperty(propertyName),
                new GUIContent(title, help),
                true);
        }

        private void ApplyStarterConfigTemplate()
        {
            var template =
                AssetDatabase.LoadAssetAtPath<DungeonLevelAuthoringConfig>(
                    StarterConfigPath);
            if (template == null)
            {
                EditorUtility.DisplayDialog(
                    "缺少新手模板",
                    $"没有找到：{StarterConfigPath}",
                    "确定");
                return;
            }
            if (_config == null)
            {
                EditorUtility.DisplayDialog(
                    "缺少当前配置",
                    "请先创建怪物与首领生成配置。",
                    "确定");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "套用新手配置模板",
                    "这会覆盖当前的小怪池、三区数量配比和首领池。是否继续？",
                    "套用模板",
                    "取消"))
                return;

            Undo.RecordObject(_config, "套用新手配置模板");
            string currentName = _config.name;
            EditorUtility.CopySerialized(template, _config);
            _config.name = currentName;
            EditorUtility.SetDirty(_config);
            AssetDatabase.SaveAssets();
            DungeonLevelAuthoringConfig.ClearCache();
            ConfigDatabase.Reload();
            _serializedConfig = new SerializedObject(_config);
            ShowNotification(new GUIContent("已套用新手配置模板"));
        }

        private static void DuplicateStarterRoomTemplate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                StarterRoomPrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "缺少房间模板",
                    $"没有找到：{StarterRoomPrefabPath}",
                    "确定");
                return;
            }

            string destination = EditorUtility.SaveFilePanelInProject(
                "复制新手房间模板",
                "我的新战斗房",
                "prefab",
                "选择新房间预制体的保存位置。",
                "Assets/1Game/Prefabs/LevelDesign");
            if (string.IsNullOrEmpty(destination)) return;
            if (!AssetDatabase.CopyAsset(StarterRoomPrefabPath, destination))
            {
                EditorUtility.DisplayDialog(
                    "复制失败",
                    "无法复制房间模板，请确认目标位置和文件名。",
                    "确定");
                return;
            }
            AssetDatabase.Refresh();
            var copy = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
            Selection.activeObject = copy;
            EditorGUIUtility.PingObject(copy);
        }

        private void DrawSimplePopulationPresets()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("区域数量与配比", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "日常只调数量和近战/远程/法术比例。预算、波次和增援等参数保留现有默认值，需要时再到高级配置处理。",
                MessageType.Info);

            SerializedProperty presets =
                _serializedConfig.FindProperty("PopulationPresets");
            if (presets == null) return;

            for (int i = 0; i < presets.arraySize; i++)
            {
                SerializedProperty preset = presets.GetArrayElementAtIndex(i);
                SerializedProperty district = preset.FindPropertyRelative("District");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    DistrictLabel((District)district.enumValueIndex),
                    EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    preset.FindPropertyRelative("MinCount"),
                    new GUIContent("最少怪物"));
                EditorGUILayout.PropertyField(
                    preset.FindPropertyRelative("MaxCount"),
                    new GUIContent("最多怪物"));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(
                    preset.FindPropertyRelative("MeleeRatio"),
                    new GUIContent("近战比例"));
                EditorGUILayout.PropertyField(
                    preset.FindPropertyRelative("RangedRatio"),
                    new GUIContent("远程比例"));
                EditorGUILayout.PropertyField(
                    preset.FindPropertyRelative("MagicRatio"),
                    new GUIContent("法术比例"));
                EditorGUILayout.EndVertical();
            }

            if (presets.arraySize < 3
                && GUILayout.Button(
                    new GUIContent(
                        "补齐外环、连接区、内环默认配置",
                        "只补缺失的分区，不覆盖已经填写的数据。")))
            {
                _serializedConfig.ApplyModifiedProperties();
                Undo.RecordObject(_config, "补齐区域默认配置");
                EnsureDistrictPreset(
                    District.Outer,
                    Preset("外环普通战斗", District.Outer, 5, 7, 3, 5, 5, 70, 25, 5, 1, 1));
                EnsureDistrictPreset(
                    District.Transition,
                    Preset("连接区普通战斗", District.Transition, 7, 10, 4, 6, 5, 55, 30, 15, 1, 2));
                EnsureDistrictPreset(
                    District.Inner,
                    Preset("内环普通战斗", District.Inner, 10, 14, 5, 8, 6, 40, 35, 25, 2, 2));
                EditorUtility.SetDirty(_config);
                _serializedConfig.Update();
            }
        }

        private void DrawSimpleEnemyPool()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("普通小怪池", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "只登记地图可以抽到的小怪、所属战斗分类和允许分区。精英怪不要放入这里。",
                MessageType.Info);

            SerializedProperty enemies = _serializedConfig.FindProperty("EnemyPool");
            if (enemies == null) return;
            int deleteIndex = -1;
            for (int i = 0; i < enemies.arraySize; i++)
            {
                SerializedProperty enemy = enemies.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    enemy.FindPropertyRelative("DisplayName"),
                    new GUIContent("名称"));
                if (GUILayout.Button("删除", GUILayout.Width(52f)))
                    deleteIndex = i;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(
                    enemy.FindPropertyRelative("EnemyKind"),
                    new GUIContent("实际怪物类型"));
                EditorGUILayout.PropertyField(
                    enemy.FindPropertyRelative("Category"),
                    new GUIContent("近战/远程/法术分类"));
                EditorGUILayout.PropertyField(
                    enemy.FindPropertyRelative("AllowedDistricts"),
                    new GUIContent("允许出现的分区"));
                EditorGUILayout.EndVertical();
            }

            if (deleteIndex >= 0)
                enemies.DeleteArrayElementAtIndex(deleteIndex);
            if (GUILayout.Button("添加普通小怪", GUILayout.Height(26f)))
            {
                _serializedConfig.ApplyModifiedProperties();
                Undo.RecordObject(_config, "添加普通小怪");
                _config.EnemyPool.Add(new EnemyPoolEntry());
                EditorUtility.SetDirty(_config);
                _serializedConfig.Update();
            }
        }

        private void DrawSimpleBossPool()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("首领随机池", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "日常只需登记首领编号、出现分区和抽取权重。事件条件与房间标签在高级配置中填写。",
                MessageType.Info);

            SerializedProperty bosses = _serializedConfig.FindProperty("BossPool");
            if (bosses == null) return;
            int deleteIndex = -1;
            for (int i = 0; i < bosses.arraySize; i++)
            {
                SerializedProperty boss = bosses.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(
                    boss.FindPropertyRelative("DisplayName"),
                    new GUIContent("名称"));
                if (GUILayout.Button("删除", GUILayout.Width(52f)))
                    deleteIndex = i;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(
                    boss.FindPropertyRelative("BossID"),
                    new GUIContent("首领编号"));
                EditorGUILayout.PropertyField(
                    boss.FindPropertyRelative("AllowedDistricts"),
                    new GUIContent("允许出现的分区"));
                EditorGUILayout.PropertyField(
                    boss.FindPropertyRelative("Weight"),
                    new GUIContent("抽取权重"));
                EditorGUILayout.EndVertical();
            }

            if (deleteIndex >= 0)
                bosses.DeleteArrayElementAtIndex(deleteIndex);
            if (GUILayout.Button("添加首领候选", GUILayout.Height(26f)))
            {
                _serializedConfig.ApplyModifiedProperties();
                Undo.RecordObject(_config, "添加首领候选");
                _config.BossPool.Add(new BossPoolEntry());
                EditorUtility.SetDirty(_config);
                _serializedConfig.Update();
            }
        }

        private void EnsureDistrictPreset(
            District district,
            EnemyPopulationPreset defaultPreset)
        {
            if (_config.PopulationPresets.All(x => x == null || x.District != district))
                _config.PopulationPresets.Add(defaultPreset);
        }

        private static string DistrictLabel(District district)
        {
            return district switch
            {
                District.Outer => "外环",
                District.Transition => "连接区",
                District.Inner => "内环",
                _ => "未指定分区"
            };
        }

        private void DrawDatabaseProperty(string propertyName, string title, string help)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(help, MessageType.Info);
            EditorGUILayout.PropertyField(
                _serializedLevelDatabase.FindProperty(propertyName),
                new GUIContent(title, help),
                true);
        }

        private void DrawConfigFooter(SerializedObject serialized, Object target)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(
                    new GUIContent("保存配置", "保存配置资产，并让运行时立即重新读取。"),
                    GUILayout.Height(26f)))
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                DungeonLevelAuthoringConfig.ClearCache();
                ConfigDatabase.Reload();
                ShowNotification(new GUIContent("关卡配置已保存"));
            }
            if (GUILayout.Button(
                    new GUIContent("定位配置资产", "在Project窗口中选中配置资产。"),
                    GUILayout.Height(26f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            if (GUILayout.Button(
                    new GUIContent("检查配置", "检查缺失分区、空敌池、非法预算和首领权重。"),
                    GUILayout.Height(26f)))
                ValidateConfig();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPrefabTools()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("房间预制体制作", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "建议在预制体编辑模式中使用。绿色线框是房间有效范围，橙色线框或网格是怪物刷新范围；范围由碰撞体决定，可使用方盒或不规则网格。",
                MessageType.Info);
            if (GUILayout.Button(
                    new GUIContent(
                        "从新手模板复制一个新房间",
                        "模板已经包含四个有效连接点、玩家/敌人内容点、绿色有效范围和橙色刷新范围。"),
                    GUILayout.Height(30f)))
                DuplicateStarterRoomTemplate();

            _roomRoot = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(
                    "房间根节点",
                    "应为挂有 Edgar 房间模板组件的房间预制体根节点。"),
                _roomRoot,
                typeof(GameObject),
                true);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("① 房间有效范围", EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent(
                        "根据当前模型创建绿色有效范围",
                        "统计房间内渲染物体的世界包围盒，创建可手动拉伸的方盒碰撞体辅助范围。"),
                    GUILayout.Height(28f)))
                CreateRoomBounds();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("② 怪物刷新范围", EditorStyles.boldLabel);
            if (GUILayout.Button(
                    new GUIContent(
                        "创建一个橙色方盒刷新范围",
                        "创建空节点和方盒碰撞体，选中后可用Unity自带碰撞体编辑手柄调整大小。"),
                    GUILayout.Height(28f)))
                CreateBoxSpawnArea();

            _hideRendererWhenConverting = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "转换后隐藏所选物体的渲染组件",
                    "仅当所选对象是辅助方块时开启；如果选的是实际地面网格，请保持关闭。"),
                _hideRendererWhenConverting);
            if (GUILayout.Button(
                    new GUIContent(
                        "将当前选中对象转换为怪物刷新范围",
                        "已有碰撞体时直接使用；只有网格时自动补网格碰撞体，支持不规则地面。"),
                    GUILayout.Height(28f)))
                ConvertSelectionToSpawnArea();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("③ 内容标记点", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("玩家出生点", "创建 PlayerSpawn 内容标记。")))
                CreateSocket(DungeonContentSocketType.PlayerSpawn, "玩家出生点");
            if (GUILayout.Button(new GUIContent("首领刷新点", "创建首领刷新内容标记。")))
                CreateSocket(DungeonContentSocketType.BossSpawn, "首领刷新点");
            if (GUILayout.Button(new GUIContent("奖励掉落点", "创建 RewardDrop 内容标记。")))
                CreateSocket(DungeonContentSocketType.RewardDrop, "奖励掉落点");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("事件点", "创建 Event 内容标记。")))
                CreateSocket(DungeonContentSocketType.Event, "事件点");
            if (GUILayout.Button(new GUIContent("材料点", "创建 Material 内容标记。")))
                CreateSocket(DungeonContentSocketType.Material, "材料点");
            if (GUILayout.Button(new GUIContent("出口点", "创建 ExitPortal 内容标记。")))
                CreateSocket(DungeonContentSocketType.ExitPortal, "出口点");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("④ Edgar连接点", EditorStyles.boldLabel);
            _doorTemplate = (DoorHandlerGrid3D)EditorGUILayout.ObjectField(
                new GUIContent(
                    "参考连接点",
                    "可选。指定已有 Door Handler 后点击复制，会连同标准连接件、封堵件、Socket和尺寸一起复制。"),
                _doorTemplate,
                typeof(DoorHandlerGrid3D),
                true);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(
                    new GUIContent(
                        "新建基础连接点",
                        "创建 Edgar Door Handler，并绑定当前房间的 GeneratorSettings。仍需放到房间网格边界并配置连接件/封堵件。")))
                CreateEdgarDoor(false);
            if (GUILayout.Button(
                    new GUIContent(
                        "复制参考连接点",
                        "复制上方参考连接点。推荐复用已有标准门，再调整位置和朝向。")))
                CreateEdgarDoor(true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button(
                    new GUIContent(
                        "检查当前房间预制体",
                        "检查Edgar组件、门、有效范围、玩家点和怪物刷新范围。"),
                    GUILayout.Height(32f)))
                ValidateRoomPrefab();

            foreach (string message in _validationMessages)
            {
                MessageType type = message.StartsWith("错误")
                    ? MessageType.Error
                    : message.StartsWith("警告")
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(message, type);
            }
            EditorGUILayout.EndScrollView();
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<DungeonLevelAuthoringConfig>(ConfigPath);
            _serializedConfig = _config != null ? new SerializedObject(_config) : null;
            _levelDatabase = AssetDatabase.LoadAssetAtPath<LevelDesignAssetDatabase>(DatabasePath);
            _serializedLevelDatabase = _levelDatabase != null
                ? new SerializedObject(_levelDatabase)
                : null;
        }

        private void CreateEmptyLevelDatabase()
        {
            if (_levelDatabase != null) return;
            _levelDatabase = CreateInstance<LevelDesignAssetDatabase>();
            AssetDatabase.CreateAsset(_levelDatabase, DatabasePath);
            AssetDatabase.SaveAssets();
            _serializedLevelDatabase = new SerializedObject(_levelDatabase);
            ConfigDatabase.Reload();
            Selection.activeObject = _levelDatabase;
        }

        private void CreateDefaultConfig()
        {
            if (_config != null) return;
            _config = CreateInstance<DungeonLevelAuthoringConfig>();
            _config.EnemyPool.AddRange(new[]
            {
                Enemy("基础近战", EnemySpawnKind.Melee, EnemyCombatCategory.Melee, 1, 100),
                Enemy("远程弓手", EnemySpawnKind.Ranged, EnemyCombatCategory.Ranged, 2, 100),
                Enemy("冲锋怪", EnemySpawnKind.Charger, EnemyCombatCategory.Melee, 3, 45),
                Enemy("法术怪", EnemySpawnKind.Mage, EnemyCombatCategory.Magic, 3, 100)
            });
            _config.PopulationPresets.AddRange(new[]
            {
                Preset("外环普通战斗", District.Outer, 5, 7, 3, 5, 5, 70, 25, 5, 1, 1),
                Preset("连接区普通战斗", District.Transition, 7, 10, 4, 6, 5, 55, 30, 15, 1, 2),
                Preset("内环普通战斗", District.Inner, 10, 14, 5, 8, 6, 40, 35, 25, 2, 2)
            });
            _config.BossPool.Add(new BossPoolEntry
            {
                DisplayName = "当前默认首领",
                BossID = 1,
                Weight = 100,
                AllowedDistricts = DistrictMask.All
            });

            AssetDatabase.CreateAsset(_config, ConfigPath);
            AssetDatabase.SaveAssets();
            DungeonLevelAuthoringConfig.ClearCache();
            _serializedConfig = new SerializedObject(_config);
            Selection.activeObject = _config;
        }

        private static EnemyPoolEntry Enemy(
            string name,
            EnemySpawnKind kind,
            EnemyCombatCategory category,
            int cost,
            int weight)
        {
            return new EnemyPoolEntry
            {
                DisplayName = name,
                EnemyKind = kind,
                Category = category,
                Cost = cost,
                Weight = weight,
                AllowedDistricts = DistrictMask.All
            };
        }

        private static EnemyPopulationPreset Preset(
            string name,
            District district,
            int minBudget,
            int maxBudget,
            int minCount,
            int maxCount,
            int maxAlive,
            int melee,
            int ranged,
            int magic,
            int minWaves,
            int maxWaves)
        {
            return new EnemyPopulationPreset
            {
                DisplayName = name,
                District = district,
                MinBudget = minBudget,
                MaxBudget = maxBudget,
                MinCount = minCount,
                MaxCount = maxCount,
                MaxAlive = maxAlive,
                MeleeRatio = melee,
                RangedRatio = ranged,
                MagicRatio = magic,
                MinWaves = minWaves,
                MaxWaves = maxWaves,
                ReinforceAtPct = maxWaves > 1 ? 50 : 0,
                ReinforceDelaySec = 0.75f
            };
        }

        private void ValidateConfig()
        {
            var errors = new List<string>();
            if (_config == null)
                errors.Add("缺少怪物与首领生成配置。");
            else
            {
                foreach (District district in System.Enum.GetValues(typeof(District)))
                {
                    if (_config.PopulationPresets.All(x => x == null || x.District != district))
                        errors.Add($"缺少“{district}”分区数量与配比。");
                }
                if (_config.EnemyPool.Count == 0)
                    errors.Add("普通小怪池为空。");
                foreach (var preset in _config.PopulationPresets.Where(x => x != null))
                {
                    if (preset.MinBudget > preset.MaxBudget)
                        errors.Add($"{preset.DisplayName}：最小预算大于最大预算。");
                    if (preset.MinCount > preset.MaxCount)
                        errors.Add($"{preset.DisplayName}：最少数量大于最多数量。");
                    if (preset.MeleeRatio + preset.RangedRatio + preset.MagicRatio <= 0)
                        errors.Add($"{preset.DisplayName}：三种怪物比例均为0。");
                }
            }

            if (_levelDatabase == null)
                errors.Add("缺少关卡数据库。");
            else
            {
                ValidateUniqueIds(_levelDatabase.MapStructures, x => x.ID, "秘境结构", errors);
                ValidateUniqueIds(_levelDatabase.RoomContents, x => x.ID, "房间内容", errors);
                ValidateUniqueIds(_levelDatabase.Encounters, x => x.ID, "战斗遭遇", errors);
                ValidateUniqueIds(_levelDatabase.StoryEvents, x => x.ID, "剧情事件", errors);
                ValidateUniqueIds(_levelDatabase.BossPhases, x => x.ID, "首领阶段", errors);

                var encounterIds = _levelDatabase.Encounters
                    .Where(x => x != null)
                    .Select(x => x.ID)
                    .ToHashSet();
                foreach (var room in _levelDatabase.RoomContents.Where(x => x != null))
                {
                    bool combat = room.RoleEnum == RoomRole.Battle
                                  || room.RoleEnum == RoomRole.Elite
                                  || room.RoleEnum == RoomRole.Boss;
                    if (combat && !encounterIds.Contains(room.ContentConfigID))
                        errors.Add($"房间内容“{room.Name_CN}”引用了不存在的遭遇编号 {room.ContentConfigID}。");
                    if (room.MaxGraphDepth < room.MinGraphDepth)
                        errors.Add($"房间内容“{room.Name_CN}”的最大图深度小于最小图深度。");
                }
            }

            if (errors.Count == 0)
                EditorUtility.DisplayDialog("检查完成", "关卡配置未发现问题。", "确定");
            else
                EditorUtility.DisplayDialog("发现配置问题", string.Join("\n", errors), "确定");
        }

        private static void ValidateUniqueIds<T>(
            IEnumerable<T> rows,
            System.Func<T, int> idSelector,
            string title,
            ICollection<string> errors)
            where T : class
        {
            var seen = new HashSet<int>();
            foreach (var row in rows.Where(x => x != null))
            {
                int id = idSelector(row);
                if (id <= 0)
                    errors.Add($"{title}存在不大于0的编号。");
                else if (!seen.Add(id))
                    errors.Add($"{title}存在重复编号 {id}。");
            }
        }

        private void CreateRoomBounds()
        {
            if (!RequireRoomRoot()) return;
            Undo.RegisterFullObjectHierarchyUndo(_roomRoot, "创建房间有效范围");
            var authoring = _roomRoot.GetComponent<DungeonRoomAuthoring>()
                            ?? Undo.AddComponent<DungeonRoomAuthoring>(_roomRoot);
            var existing = _roomRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == "__房间有效范围");
            GameObject volume = existing != null
                ? existing.gameObject
                : new GameObject("__房间有效范围");
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(volume, "创建房间有效范围");
                volume.transform.SetParent(_roomRoot.transform, false);
            }

            var collider = volume.GetComponent<BoxCollider>()
                           ?? Undo.AddComponent<BoxCollider>(volume);
            collider.isTrigger = true;
            CalculateRendererBounds(_roomRoot, out Vector3 center, out Vector3 size);
            collider.center = center;
            collider.size = size;
            authoring.Configure(collider);
            EditorUtility.SetDirty(authoring);
            Selection.activeGameObject = volume;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void CreateBoxSpawnArea()
        {
            if (!RequireRoomRoot()) return;
            Undo.RegisterFullObjectHierarchyUndo(_roomRoot, "创建怪物刷新范围");
            var go = new GameObject("怪物刷新范围_Box");
            Undo.RegisterCreatedObjectUndo(go, "创建怪物刷新范围");
            go.transform.SetParent(_roomRoot.transform, false);
            var box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(6f, 1f, 6f);
            var area = Undo.AddComponent<DungeonEnemySpawnArea>(go);
            area.Configure(box);
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void ConvertSelectionToSpawnArea()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("无法转换", "请先选中一个Cube或带Mesh的对象。", "确定");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(selected, "转换怪物刷新范围");
            Collider collider = selected.GetComponent<Collider>();
            if (collider == null)
            {
                var filter = selected.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    EditorUtility.DisplayDialog(
                        "无法转换",
                        "所选对象既没有Collider，也没有可用于创建MeshCollider的Mesh。",
                        "确定");
                    return;
                }
                var meshCollider = Undo.AddComponent<MeshCollider>(selected);
                meshCollider.sharedMesh = filter.sharedMesh;
                collider = meshCollider;
            }

            var area = selected.GetComponent<DungeonEnemySpawnArea>()
                       ?? Undo.AddComponent<DungeonEnemySpawnArea>(selected);
            area.Configure(collider);
            if (_hideRendererWhenConverting)
            {
                foreach (var renderer in selected.GetComponents<Renderer>())
                    renderer.enabled = false;
            }
            EditorUtility.SetDirty(area);
            SceneView.RepaintAll();
        }

        private void CreateSocket(DungeonContentSocketType type, string displayName)
        {
            if (!RequireRoomRoot()) return;
            var go = new GameObject(displayName);
            Undo.RegisterCreatedObjectUndo(go, $"创建{displayName}");
            go.transform.SetParent(_roomRoot.transform, false);
            var socket = Undo.AddComponent<DungeonContentSocket>(go);
            socket.Configure(type);
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void CreateEdgarDoor(bool duplicateTemplate)
        {
            if (!RequireRoomRoot()) return;
            var doorsRoot = _roomRoot.transform.Find("Doors");
            if (doorsRoot == null)
            {
                var doorsObject = new GameObject("Doors");
                Undo.RegisterCreatedObjectUndo(doorsObject, "创建Doors根节点");
                doorsObject.transform.SetParent(_roomRoot.transform, false);
                doorsRoot = doorsObject.transform;
            }

            GameObject doorObject;
            DoorHandlerGrid3D handler;
            if (duplicateTemplate)
            {
                if (_doorTemplate == null)
                {
                    EditorUtility.DisplayDialog(
                        "没有参考连接点",
                        "请先在“参考连接点”槽位指定一个已有 Door Handler。",
                        "确定");
                    return;
                }
                doorObject = Instantiate(_doorTemplate.gameObject, doorsRoot);
                doorObject.name = _doorTemplate.gameObject.name + "_复制";
                Undo.RegisterCreatedObjectUndo(doorObject, "复制Edgar连接点");
                handler = doorObject.GetComponent<DoorHandlerGrid3D>();
            }
            else
            {
                doorObject = new GameObject("Edgar连接点_请移动到边界");
                Undo.RegisterCreatedObjectUndo(doorObject, "创建Edgar连接点");
                doorObject.transform.SetParent(doorsRoot, false);
                handler = Undo.AddComponent<DoorHandlerGrid3D>(doorObject);
                var roomSettings = _roomRoot.GetComponent<RoomTemplateSettingsGrid3D>();
                handler.GeneratorSettings = roomSettings != null
                    ? roomSettings.GeneratorSettings
                    : null;
                handler.Width = 1;
                handler.Height = 1;
                handler.Repeat = 0;
            }

            _doorTemplate = handler;
            Selection.activeGameObject = doorObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.DisplayDialog(
                "连接点已创建",
                "请把连接点移动到房间网格边界，并通过旋转确定朝向。最后点击“检查当前房间预制体”；Edgar会报告不在边界或方向错误。",
                "确定");
        }

        private void ValidateRoomPrefab()
        {
            _validationMessages.Clear();
            if (!RequireRoomRoot()) return;

            var settings = _roomRoot.GetComponent<RoomTemplateSettingsGrid3D>();
            if (settings == null)
                _validationMessages.Add("错误：根节点缺少 Edgar RoomTemplateSettingsGrid3D。");
            else if (settings.GeneratorSettings == null)
                _validationMessages.Add("错误：Edgar GeneratorSettings 尚未指定。");

            var authoring = _roomRoot.GetComponent<DungeonRoomAuthoring>();
            if (authoring == null || authoring.ValidBounds == null)
                _validationMessages.Add("错误：尚未创建绿色房间有效范围。");
            if (_roomRoot.GetComponentsInChildren<DungeonEnemySpawnArea>(true).Length == 0)
                _validationMessages.Add("错误：至少需要一个橙色怪物刷新范围。");

            var sockets = _roomRoot.GetComponentsInChildren<DungeonContentSocket>(true);
            if (sockets.All(x => x.SocketType != DungeonContentSocketType.PlayerSpawn))
                _validationMessages.Add("错误：缺少玩家出生点。");
            if (sockets.All(x => x.SocketType != DungeonContentSocketType.BossSpawn))
                _validationMessages.Add("警告：没有首领刷新点；普通房可以忽略。");

            if (settings != null)
            {
                var result = RoomTemplateDiagnosticsGrid3D.CheckAll(_roomRoot);
                foreach (string error in result.Errors)
                    _validationMessages.Add($"错误：Edgar诊断失败——{error}");
            }

            if (_validationMessages.Count == 0)
                _validationMessages.Add("通过：房间预制体基础配置完整。");
        }

        private bool RequireRoomRoot()
        {
            if (_roomRoot != null) return true;
            EditorUtility.DisplayDialog(
                "未选择房间",
                "请在层级面板或预制体编辑模式中选中房间根节点或其任意子物体。",
                "确定");
            return false;
        }

        private static void CalculateRendererBounds(
            GameObject root,
            out Vector3 localCenter,
            out Vector3 localSize)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(x => x.enabled).ToArray();
            if (renderers.Length == 0)
            {
                localCenter = Vector3.zero;
                localSize = new Vector3(10f, 3f, 10f);
                return;
            }

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                world.Encapsulate(renderers[i].bounds);

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                var worldPoint = new Vector3(
                    x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y,
                    z == 0 ? world.min.z : world.max.z);
                Vector3 local = root.transform.InverseTransformPoint(worldPoint);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            localCenter = (min + max) * 0.5f;
            localSize = Vector3.Max(max - min, new Vector3(1f, 1f, 1f));
        }
    }
}
