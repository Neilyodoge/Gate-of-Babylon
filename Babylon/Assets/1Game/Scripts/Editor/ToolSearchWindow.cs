using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XianTu
{
    // ========================================================================
    // 工具条目数据
    // ========================================================================

    /// <summary>
    /// 单个工具的注册信息
    /// </summary>
    public class ToolEntry
    {
        /// <summary>工具显示名称</summary>
        public string Name;

        /// <summary>所属分类（对应 Scripts 子目录）</summary>
        public string Category;

        /// <summary>工具描述 / 帮助信息</summary>
        public string Description;

        /// <summary>点击时执行的动作</summary>
        public Action OnClick;

        /// <summary>关联的脚本路径（用于帮助按钮定位）</summary>
        public string ScriptPath;

        /// <summary>是否为专用工具（true=专用工具Tab，false=通用工具Tab）</summary>
        public bool IsSpecialized;

        /// <summary>关联的菜单路径（用于自动检测去重，菜单类工具填写）</summary>
        public string MenuPath;
    }

    // ========================================================================
    // 工具注册表 —— 在此添加/管理所有工具条目
    // ========================================================================

    /// <summary>
    /// 工具注册表，集中管理所有可搜索的工具
    /// </summary>
    public static class ToolRegistry
    {
        private static List<ToolEntry> _tools;

        /// <summary>
        /// 获取所有已注册的工具（懒加载）
        /// </summary>
        public static List<ToolEntry> GetAll()
        {
            if (_tools == null)
                _tools = BuildToolList();
            return _tools;
        }

        /// <summary>
        /// 强制刷新工具列表
        /// </summary>
        public static void Refresh()
        {
            _tools = null;
        }

        /// <summary>
        /// 构建工具列表 —— 在此注册所有工具
        /// </summary>
        private static List<ToolEntry> BuildToolList()
        {
            var list = new List<ToolEntry>();

            // ============================================================
            // 通用工具 —— Tools/Editor 下的编辑器工具
            // ============================================================

            // ---- 美术工具 (ArtTools) ----
            list.Add(MenuTool("批量重命名", "ArtTools", "nTools/美术工具/批量重命名", "Assets/Tools/Editor/ArtTools/BatchAssetRenamer.cs"));
            list.Add(MenuTool("贴图规范化", "ArtTools", "nTools/美术工具/贴图规范化", "Assets/Tools/Editor/ArtTools/TextureNormalizer.cs"));
            list.Add(MenuTool("SDF Generator", "ArtTools", "nTools/美术工具/SDF Generator", "Assets/Tools/Editor/ArtTools/SDFGenerator.cs"));
            list.Add(MenuTool("平滑法线烘焙", "ArtTools", "nTools/美术工具/平滑法线烘焙", "Assets/Tools/Editor/ArtTools/BentNormalBakeTool.cs"));
            list.Add(MenuTool("Prefab资源快速复制", "ArtTools", "nTools/美术工具/Prefab资源快速复制", "Assets/Tools/Editor/ArtTools/PrefabAssetExtractor.cs"));

            // ---- TA工具 (TATools) ----
            list.Add(MenuTool("通道重映射", "TATools", "nTools/TA工具/通道重映射", "Assets/Tools/Editor/TATools/ChannelRemapper.cs"));
            list.Add(MenuTool("SRP Batcher Checker", "TATools", "nTools/TA工具/SRP Batcher Checker", "Assets/Tools/Editor/TATools/SRPBatcherChecker.cs"));
            list.Add(MenuTool("Shader批量替换", "TATools", "Tools_3D/美术/场景/Shader批量替换", "Assets/Tools/Editor/TATools/shader批量替换/TLShaderReplacer.cs"));
            list.Add(MenuTool("材质批量调参", "TATools", "Tools_3D/美术/材质批量调参", "Assets/Tools/Editor/TATools/材质参数批量替换/TLMaterialBatchEditor.cs"));

            // ---- 性能优化 (OptimizeTool) ----
            list.Add(MenuTool("场景优化", "OptimizeTool", "nTools/性能优化/场景优化", "Assets/Tools/Editor/OptimizeTool/SceneOptimizeTool.cs"));

            // ============================================================
            // 专用工具 —— 1Game/Scripts/Editor 下的项目工具
            // ============================================================

            // ---- 核心系统 (Core) ----
            list.Add(new ToolEntry
            {
                Name = "配置 Tags 和 Layers",
                Category = "Core",
                Description = "自动配置项目所需的 Tags 和 Layers（Player/Enemy/Projectile 等）",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/① 配置 Tags 和 Layers"),
                ScriptPath = "Assets/1Game/Scripts/Editor/Demo1DataCreator.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "创建 Demo1 测试数据",
                Category = "Core",
                Description = "一键创建技能数据（落石术/金钟罩）和灵物数据等 ScriptableObject 资产",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/② 创建 Demo1 测试数据"),
                ScriptPath = "Assets/1Game/Scripts/Editor/Demo1DataCreator.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "创建 Animator Controller",
                Category = "Core",
                Description = "自动创建玩家 Animator Controller，包含 Idle/Walk/Attack/Skill/Hit/Evade 状态",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/③ 创建 Animator Controller"),
                ScriptPath = "Assets/1Game/Scripts/Editor/Demo1DataCreator.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "自动配置 Demo1 场景",
                Category = "Core",
                Description = "自动配置当前场景：添加 Demo1Setup、设置相机、灯光、后处理等",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/④ 自动配置 Demo1 场景"),
                ScriptPath = "Assets/1Game/Scripts/Editor/Demo1DataCreator.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "创建 Demo1 场景文件",
                Category = "Core",
                Description = "在 Assets/1Game/Scenes 下创建 Demo1.unity 场景文件",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/⑤ 创建 Demo1 场景文件"),
                ScriptPath = "Assets/1Game/Scripts/Editor/Demo1DataCreator.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "创建游戏配置 (GameConfig)",
                Category = "Core",
                Description = "创建 GameConfig ScriptableObject，集中管理所有游戏数值",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/⑤ 创建游戏配置 (GameConfig)"),
                ScriptPath = "Assets/1Game/Scripts/Editor/GameConfigEditor.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "选中游戏配置",
                Category = "Core",
                Description = "在 Inspector 中选中并高亮 GameConfig 资产，方便快速修改数值",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/⑥ 选中游戏配置"),
                ScriptPath = "Assets/1Game/Scripts/Editor/GameConfigEditor.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "选中音效配置",
                Category = "Core",
                Description = "在 Inspector 中选中 AudioConfig 资产，拖入音频文件到对应槽位即可",
                OnClick = () => SelectAsset("Assets/1Game/Resources/AudioConfig.asset"),
                ScriptPath = "Assets/1Game/Scripts/Core/AudioConfig.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "选中怪物预制体配置",
                Category = "Core",
                Description = "在 Inspector 中选中 MonsterPrefabs 资产，拖入怪物模型 Prefab",
                OnClick = () => SelectAsset("Assets/1Game/Resources/MonsterPrefabs.asset"),
                ScriptPath = "Assets/1Game/Scripts/Core/MonsterPrefabs.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "⚡ 配置速查面板",
                Category = "Core",
                Description = "打开配置速查面板，一站式查看和跳转所有配置文件",
                OnClick = () => EditorApplication.ExecuteMenuItem("仙途秘境/⑦ 配置速查"),
                ScriptPath = "Assets/1Game/Scripts/Editor/ConfigDashboard.cs",
                IsSpecialized = true
            });

            // ---- 战斗系统 (Combat) ----
            list.Add(new ToolEntry
            {
                Name = "选中技能数据 - 落石术",
                Category = "Combat",
                OnClick = () => SelectAsset("Assets/1Game/Data/Skills/落石术.asset"),
                ScriptPath = "Assets/1Game/Scripts/Combat/SkillData.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "选中技能数据 - 金钟罩",
                Category = "Combat",
                OnClick = () => SelectAsset("Assets/1Game/Data/Skills/金钟罩.asset"),
                ScriptPath = "Assets/1Game/Scripts/Combat/SkillData.cs",
                IsSpecialized = true
            });

            // ---- 玩家 (Player) ----
            list.Add(new ToolEntry
            {
                Name = "定位 PlayerController 脚本",
                Category = "Player",
                OnClick = () => PingScript("Assets/1Game/Scripts/Player/PlayerController.cs"),
                ScriptPath = "Assets/1Game/Scripts/Player/PlayerController.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "定位 PlayerCombat 脚本",
                Category = "Player",
                OnClick = () => PingScript("Assets/1Game/Scripts/Player/PlayerCombat.cs"),
                ScriptPath = "Assets/1Game/Scripts/Player/PlayerCombat.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "定位 PlayerAnimator 脚本",
                Category = "Player",
                OnClick = () => PingScript("Assets/1Game/Scripts/Player/PlayerAnimator.cs"),
                ScriptPath = "Assets/1Game/Scripts/Player/PlayerAnimator.cs",
                IsSpecialized = true
            });

            // ---- 敌人 AI (Enemy) ----
            list.Add(new ToolEntry
            {
                Name = "定位 EnemyBase 脚本",
                Category = "Enemy",
                OnClick = () => PingScript("Assets/1Game/Scripts/Enemy/EnemyBase.cs"),
                ScriptPath = "Assets/1Game/Scripts/Enemy/EnemyBase.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "定位 EnemyBoss 脚本",
                Category = "Enemy",
                OnClick = () => PingScript("Assets/1Game/Scripts/Enemy/EnemyBoss.cs"),
                ScriptPath = "Assets/1Game/Scripts/Enemy/EnemyBoss.cs",
                IsSpecialized = true
            });

            // ---- 灵物系统 (Items) ----
            list.Add(new ToolEntry
            {
                Name = "定位 ItemData 脚本",
                Category = "Items",
                OnClick = () => PingScript("Assets/1Game/Scripts/Items/ItemData.cs"),
                ScriptPath = "Assets/1Game/Scripts/Items/ItemData.cs",
                IsSpecialized = true
            });

            // ---- 房间与关卡 (Room) ----
            list.Add(new ToolEntry
            {
                Name = "定位 RoomBuilder 脚本",
                Category = "Room",
                OnClick = () => PingScript("Assets/1Game/Scripts/Room/RoomBuilder.cs"),
                ScriptPath = "Assets/1Game/Scripts/Room/RoomBuilder.cs",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "定位 GameManager 脚本",
                Category = "Room",
                OnClick = () => PingScript("Assets/1Game/Scripts/Core/GameManager.cs"),
                ScriptPath = "Assets/1Game/Scripts/Core/GameManager.cs",
                IsSpecialized = true
            });

            // ---- UI 系统 (UI) ----
            list.Add(new ToolEntry
            {
                Name = "定位 GameHUD 脚本",
                Category = "UI",
                OnClick = () => PingScript("Assets/1Game/Scripts/UI/GameHUD.cs"),
                ScriptPath = "Assets/1Game/Scripts/UI/GameHUD.cs",
                IsSpecialized = true
            });

            // ---- 文档 (Docs) ----
            list.Add(new ToolEntry
            {
                Name = "⚡ 配置速查表",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/配置速查表.md"),
                ScriptPath = "Assets/1Game/Docs/配置速查表.md",
                Description = "一页纸看清所有配置文件：位置、用途、字段速查、快速上手流程",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "📚 文档索引",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/README.md"),
                ScriptPath = "Assets/1Game/Docs/README.md",
                Description = "所有文档的总索引，分为配置、资源、系统三大类",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "打开 Demo1 功能清单",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/Demo1功能清单.md"),
                ScriptPath = "Assets/1Game/Docs/Demo1功能清单.md",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "打开技能播放速度配置方案",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/技能播放速度配置方案.md"),
                ScriptPath = "Assets/1Game/Docs/技能播放速度配置方案.md",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "打开 PostProcess README",
                Category = "Docs",
                OnClick = () => PingScript("Packages/com.unity.render-pipelines.universal/PostProcess_README.md"),
                ScriptPath = "Packages/com.unity.render-pipelines.universal/PostProcess_README.md",
                IsSpecialized = true
            });

            // ---- 配置文档 ----
            list.Add(new ToolEntry
            {
                Name = "GameConfig 配置说明",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/配置_GameConfig说明.md"),
                ScriptPath = "Assets/1Game/Docs/配置_GameConfig说明.md",
                Description = "全局游戏配置字段说明与调参指南",
                IsSpecialized = true
            });

            // ---- 资源文档 ----
            list.Add(new ToolEntry
            {
                Name = "灵物配置指南",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/资源_灵物配置指南.md"),
                ScriptPath = "Assets/1Game/Docs/资源_灵物配置指南.md",
                Description = "如何创建灵物 SO 数据，字段说明、品阶/分类规则、质变阈值配置",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "功法配置指南",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/资源_功法配置指南.md"),
                ScriptPath = "Assets/1Game/Docs/资源_功法配置指南.md",
                Description = "如何创建功法 SO 数据，技能类型、数值配置、VFX 关联",
                IsSpecialized = true
            });

            list.Add(new ToolEntry
            {
                Name = "数据创建工具说明",
                Category = "Docs",
                OnClick = () => PingScript("Assets/1Game/Docs/资源_数据创建工具说明.md"),
                ScriptPath = "Assets/1Game/Docs/资源_数据创建工具说明.md",
                Description = "Demo1DataCreator 编辑器工具的使用方法，一键创建/更新所有测试数据",
                IsSpecialized = true
            });

            // 自动检测：补充扫描到的、尚未手动登记的菜单工具
            AppendAutoDetectedMenuTools(list);

            return list;
        }

        // ============================================================
        // 自动检测
        // ============================================================

        /// <summary>
        /// 自动扫描的菜单根前缀。只有位于这些前缀下的 [MenuItem] 才会被自动收录，
        /// 避免把 Unity 内置菜单（File/Edit/GameObject 等）也扫进来。
        /// 新增工具菜单根时，在此追加即可。
        /// </summary>
        private static readonly string[] AutoScanMenuRoots = { "nTools/", "Tools_3D/美术/" };

        /// <summary>
        /// 自动检测：扫描工程内所有带 [MenuItem] 的方法，把位于指定菜单根下、
        /// 且尚未在上面手动登记过的工具补充进列表。
        /// 这样新增带 [MenuItem] 的工具后，无需手动改本文件即可被搜索到。
        /// </summary>
        private static void AppendAutoDetectedMenuTools(List<ToolEntry> list)
        {
            // 已手动登记的菜单路径，用于去重（保留手写条目的分类/名称/描述）
            var known = new HashSet<string>(
                list.Where(t => !string.IsNullOrEmpty(t.MenuPath)).Select(t => t.MenuPath));

            var methods = TypeCache.GetMethodsWithAttribute<MenuItem>();
            foreach (var method in methods)
            {
                foreach (var attr in method.GetCustomAttributes(typeof(MenuItem), false))
                {
                    var mi = attr as MenuItem;
                    // 跳过空属性与校验函数（validate 重载只是控制可用状态，不是工具入口）
                    if (mi == null || mi.validate)
                        continue;

                    string path = mi.menuItem;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    // 仅收录项目工具菜单根下的项
                    bool underRoot = false;
                    for (int i = 0; i < AutoScanMenuRoots.Length; i++)
                    {
                        if (path.StartsWith(AutoScanMenuRoots[i], StringComparison.Ordinal))
                        {
                            underRoot = true;
                            break;
                        }
                    }
                    if (!underRoot)
                        continue;

                    // 去重：已手动登记或已自动登记过的菜单跳过
                    if (known.Contains(path))
                        continue;
                    known.Add(path);

                    // 名称取菜单叶子节点，分类取上一级菜单目录
                    string capturedPath = path;
                    string[] seg = path.Split('/');
                    string name = seg[seg.Length - 1];
                    string category = seg.Length >= 2 ? seg[seg.Length - 2] : "自动发现";

                    string scriptPath = GetScriptPath(method.DeclaringType);

                    list.Add(new ToolEntry
                    {
                        Name = name,
                        Category = category,
                        MenuPath = capturedPath,
                        OnClick = () => EditorApplication.ExecuteMenuItem(capturedPath),
                        ScriptPath = scriptPath,
                        IsSpecialized = false
                    });
                }
            }
        }

        // 类型 -> 脚本路径 的全局缓存（域重载后自动清空，首次访问时构建）
        private static Dictionary<Type, string> _typeToScriptPath;

        /// <summary>
        /// 根据类型查找其所在的 .cs 脚本路径（用于帮助按钮定位）。
        /// 通过 MonoScript.GetClass() 匹配，能兼容“文件名与类名不一致”的情况。
        /// </summary>
        private static string GetScriptPath(Type type)
        {
            if (type == null)
                return null;

            if (_typeToScriptPath == null)
            {
                _typeToScriptPath = new Dictionary<Type, string>();
                string[] guids = AssetDatabase.FindAssets("t:MonoScript");
                foreach (var guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                    if (ms == null)
                        continue;
                    Type cls = ms.GetClass();
                    if (cls != null && !_typeToScriptPath.ContainsKey(cls))
                        _typeToScriptPath[cls] = assetPath;
                }
            }

            _typeToScriptPath.TryGetValue(type, out string found);
            return found;
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        /// <summary>创建一个菜单类工具条目（同时记录菜单路径，供自动检测去重）</summary>
        private static ToolEntry MenuTool(string name, string category, string menuPath, string scriptPath)
        {
            string capturedPath = menuPath;
            return new ToolEntry
            {
                Name = name,
                Category = category,
                MenuPath = capturedPath,
                OnClick = () => EditorApplication.ExecuteMenuItem(capturedPath),
                ScriptPath = scriptPath
            };
        }

        /// <summary>在 Project 窗口中选中并高亮资产</summary>
        private static void SelectAsset(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
            else
            {
                Debug.LogWarning($"找不到资产：{path}");
            }
        }

        /// <summary>在 Project 窗口中定位脚本文件</summary>
        private static void PingScript(string path)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
            else
            {
                Debug.LogWarning($"找不到文件：{path}");
            }
        }
    }

    // ========================================================================
    // 工具搜索窗口 EditorWindow
    // ========================================================================

    /// <summary>
    /// 工具搜索面板 —— 仿 ArtTools 风格
    /// 功能：三Tab分类 + 搜索过滤 + 收藏星标 + 帮助按钮
    /// </summary>
    public class ToolSearchWindow : EditorWindow
    {
        // ---- 常量 ----
        private const string PREFS_KEY_FAVORITES = "XianTu_ToolSearch_Favorites";
        private const float ITEM_HEIGHT = 28f;
        private const float STAR_WIDTH = 24f;
        private const float HELP_WIDTH = 24f;

        // ---- 主题色（朴素深灰风格，参考 ArtTools） ----
        private static readonly Color COLOR_ITEM_HOVER = new Color(0.35f, 0.35f, 0.35f, 0.5f);      // 条目悬停
        private static readonly Color COLOR_ITEM_LINE = new Color(0.20f, 0.20f, 0.20f, 0.8f);       // 分隔线
        private static readonly Color COLOR_STAR_ON = new Color(1.0f, 0.78f, 0.10f);                 // 收藏星标-已收藏金色
        private static readonly Color COLOR_STAR_OFF = new Color(0.45f, 0.45f, 0.45f);               // 收藏星标-未收藏灰色
        private static readonly Color COLOR_HELP_BTN = new Color(0.45f, 0.65f, 0.90f);               // 帮助按钮蓝色
        private static readonly Color COLOR_COUNT_TEXT = new Color(0.55f, 0.55f, 0.55f);             // 计数文字

        // ---- Tab 定义 ----
        private enum Tab { General, Specialized, Favorites }
        private static readonly string[] TAB_NAMES = { "通用工具", "专用工具", "个人收藏" };

        // ---- 状态 ----
        private Tab _currentTab = Tab.General;
        private string _searchText = "";
        private Vector2 _scrollPos;
        private HashSet<string> _favorites = new HashSet<string>();
        private Dictionary<string, bool> _categoryFoldouts = new Dictionary<string, bool>();

        // ---- 样式（懒初始化） ----
        private GUIStyle _itemStyle;
        private GUIStyle _categoryStyle;
        private GUIStyle _countStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _tabNormalStyle;
        private GUIStyle _tabSelectedStyle;
        private GUIStyle _starOnStyle;
        private GUIStyle _starOffStyle;
        private GUIStyle _helpBtnStyle;
        private bool _stylesInited;

        // ---- 分类中文映射 ----
        private static readonly Dictionary<string, string> CATEGORY_NAMES = new Dictionary<string, string>
        {
            // 通用工具分类（Tools/Editor 下的工具）
            { "ArtTools", "美术工具" },
            { "TATools", "TA工具" },
            { "OptimizeTool", "性能优化" },
            // 专用工具分类（1Game/Scripts/Editor 下的工具）
            { "Core", "核心系统" },
            { "Combat", "战斗系统" },
            { "Player", "玩家" },
            { "Enemy", "敌人 AI" },
            { "Items", "灵物系统" },
            { "Room", "房间与关卡" },
            { "UI", "UI 系统" },
            { "Docs", "文档" }
        };

        // ============================================================
        // 菜单入口
        // ============================================================

        [MenuItem("nTools/工具搜索 _F1", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<ToolSearchWindow>();
            window.titleContent = new GUIContent("工具搜索", EditorGUIUtility.IconContent("d_Search Icon").image);
            window.minSize = new Vector2(320, 400);
            window.Show();
        }

        // ============================================================
        // 生命周期
        // ============================================================

        private void OnEnable()
        {
            LoadFavorites();
        }

        private void OnDisable()
        {
            SaveFavorites();
        }

        // ============================================================
        // 样式初始化
        // ============================================================

        private void InitStyles()
        {
            if (_stylesInited) return;
            _stylesInited = true;

            // 工具条目
            _itemStyle = new GUIStyle(EditorStyles.label)
            {
                fixedHeight = ITEM_HEIGHT,
                padding = new RectOffset(12, 4, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                richText = true
            };

            // 分类标题
            _categoryStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            // 分类计数
            _countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft
            };
            _countStyle.normal.textColor = COLOR_COUNT_TEXT;

            // 搜索框
            _searchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField)
            {
                fixedHeight = 22f,
                margin = new RectOffset(4, 4, 4, 4)
            };

            // Tab 普通
            _tabNormalStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 28f,
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };

            // Tab 选中
            _tabSelectedStyle = new GUIStyle(_tabNormalStyle)
            {
                fontStyle = FontStyle.Bold
            };

            // 收藏星标 - 已收藏
            _starOnStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 20,
                fixedWidth = 20,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _starOnStyle.normal.textColor = COLOR_STAR_ON;

            // 收藏星标 - 未收藏
            _starOffStyle = new GUIStyle(_starOnStyle);
            _starOffStyle.normal.textColor = COLOR_STAR_OFF;

            // 帮助按钮
            _helpBtnStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 20,
                fixedWidth = 20,
                padding = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold
            };
            _helpBtnStyle.normal.textColor = COLOR_HELP_BTN;
        }

        // ============================================================
        // 主绘制
        // ============================================================

        private void OnGUI()
        {
            InitStyles();

            DrawTabs();
            DrawSearchBar();
            DrawToolList();
        }

        // ============================================================
        // 绘制 Tab 栏
        // ============================================================

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                var tab = (Tab)i;
                var style = (_currentTab == tab) ? _tabSelectedStyle : _tabNormalStyle;

                if (GUILayout.Button(TAB_NAMES[i], style, GUILayout.ExpandWidth(true)))
                {
                    _currentTab = tab;
                    _scrollPos = Vector2.zero;
                }
            }

            // 刷新按钮
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Refresh"), EditorStyles.toolbarButton,
                GUILayout.Width(28)))
            {
                ToolRegistry.Refresh();
                _stylesInited = false;
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // 绘制搜索栏
        // ============================================================

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            GUI.SetNextControlName("SearchField");
            _searchText = EditorGUILayout.TextField(_searchText, _searchFieldStyle);

            if (!string.IsNullOrEmpty(_searchText))
            {
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    _searchText = "";
                    GUI.FocusControl(null);
                }
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();
        }

        // ============================================================
        // 绘制工具列表
        // ============================================================

        private void DrawToolList()
        {
            var allTools = ToolRegistry.GetAll();
            var filtered = FilterTools(allTools);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (filtered.Count == 0)
            {
                GUILayout.Space(40);
                EditorGUILayout.LabelField("没有找到匹配的工具", EditorStyles.centeredGreyMiniLabel);
                GUILayout.Space(40);
            }
            else if (!string.IsNullOrEmpty(_searchText))
            {
                // 搜索模式：平铺显示，不分组
                EditorGUILayout.LabelField($"  搜索结果  {filtered.Count}个", _countStyle);
                GUILayout.Space(2);
                foreach (var tool in filtered)
                {
                    DrawToolItem(tool);
                }
            }
            else
            {
                // 分组显示
                var groups = filtered
                    .GroupBy(t => t.Category)
                    .OrderBy(g => GetCategoryOrder(g.Key));

                foreach (var group in groups)
                {
                    DrawCategoryGroup(group.Key, group.ToList());
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制分类折叠组
        /// </summary>
        private void DrawCategoryGroup(string category, List<ToolEntry> tools)
        {
            if (!_categoryFoldouts.ContainsKey(category))
                _categoryFoldouts[category] = true;

            string displayName = CATEGORY_NAMES.ContainsKey(category) ? CATEGORY_NAMES[category] : category;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(4);

            _categoryFoldouts[category] = EditorGUILayout.Foldout(
                _categoryFoldouts[category],
                $"  {displayName}",
                true,
                _categoryStyle
            );

            // 显示数量
            EditorGUILayout.LabelField($"{tools.Count}个", _countStyle, GUILayout.Width(40));

            EditorGUILayout.EndHorizontal();

            if (_categoryFoldouts[category])
            {
                foreach (var tool in tools)
                {
                    DrawToolItem(tool);
                }
            }

            GUILayout.Space(2);
        }

        /// <summary>
        /// 绘制单个工具条目
        /// </summary>
        private void DrawToolItem(ToolEntry tool)
        {
            var rect = EditorGUILayout.GetControlRect(false, ITEM_HEIGHT);

            // 悬停高亮
            bool isHover = rect.Contains(Event.current.mousePosition);
            if (isHover)
            {
                EditorGUI.DrawRect(rect, COLOR_ITEM_HOVER);
            }

            // 底部分隔线
            var lineRect = new Rect(rect.x, rect.yMax - 1, rect.width, 1);
            EditorGUI.DrawRect(lineRect, COLOR_ITEM_LINE);

            // 计算右侧按钮占用宽度
            bool hasHelp = !string.IsNullOrEmpty(tool.Description);
            float rightWidth = STAR_WIDTH + (hasHelp ? HELP_WIDTH + 4 : 0) + 8;

            // ---- 工具名称（可点击） ----
            var nameRect = new Rect(rect.x + 12, rect.y, rect.width - 12 - rightWidth, rect.height);
            EditorGUIUtility.AddCursorRect(nameRect, MouseCursor.Link);

            if (GUI.Button(nameRect, tool.Name, _itemStyle))
            {
                tool.OnClick?.Invoke();
            }

            // ---- 收藏星标 ----
            bool isFav = _favorites.Contains(tool.Name);
            float starX = hasHelp
                ? rect.xMax - STAR_WIDTH - HELP_WIDTH - 8
                : rect.xMax - STAR_WIDTH - 4;
            var starBtnRect = new Rect(starX, rect.y + (rect.height - 20) / 2, 20, 20);

            var starContent = isFav
                ? new GUIContent("★", "取消收藏")
                : new GUIContent("☆", "添加到收藏");

            var starStyle = isFav ? _starOnStyle : _starOffStyle;

            if (GUI.Button(starBtnRect, starContent, starStyle))
            {
                if (isFav)
                    _favorites.Remove(tool.Name);
                else
                    _favorites.Add(tool.Name);
                SaveFavorites();
                Repaint();
            }

            // ---- 帮助按钮（仅在有 Description 时显示） ----
            if (hasHelp)
            {
                var helpRect = new Rect(rect.xMax - HELP_WIDTH - 2,
                    rect.y + (rect.height - 20) / 2, 20, 20);

                if (GUI.Button(helpRect, new GUIContent("?", tool.Description), _helpBtnStyle))
                {
                    if (!string.IsNullOrEmpty(tool.ScriptPath))
                    {
                        var script = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tool.ScriptPath);
                        if (script != null)
                        {
                            EditorGUIUtility.PingObject(script);
                        }
                    }

                    Debug.Log($"<color=cyan>【{tool.Name}】</color> {tool.Description}");
                }
            }
        }

        // ============================================================
        // 过滤逻辑
        // ============================================================

        /// <summary>
        /// 根据当前 Tab 和搜索文本过滤工具列表
        /// </summary>
        private List<ToolEntry> FilterTools(List<ToolEntry> all)
        {
            IEnumerable<ToolEntry> result;

            switch (_currentTab)
            {
                case Tab.General:
                    result = all.Where(t => !t.IsSpecialized);
                    break;
                case Tab.Specialized:
                    result = all.Where(t => t.IsSpecialized);
                    break;
                case Tab.Favorites:
                    result = all.Where(t => _favorites.Contains(t.Name));
                    break;
                default:
                    result = all;
                    break;
            }

            // 搜索过滤（支持拼音首字母 / 中文 / 英文模糊匹配）
            if (!string.IsNullOrEmpty(_searchText))
            {
                string search = _searchText.ToLower();
                result = result.Where(t =>
                    t.Name.ToLower().Contains(search) ||
                    t.Category.ToLower().Contains(search) ||
                    (t.Description != null && t.Description.ToLower().Contains(search))
                );
            }

            return result.ToList();
        }

        // ============================================================
        // 分类排序
        // ============================================================

        private int GetCategoryOrder(string category)
        {
            switch (category)
            {
                // 通用工具分类
                case "ArtTools": return 0;
                case "TATools": return 1;
                case "OptimizeTool": return 2;
                // 专用工具分类
                case "Core": return 10;
                case "Combat": return 11;
                case "Player": return 12;
                case "Enemy": return 13;
                case "Items": return 14;
                case "Room": return 15;
                case "UI": return 16;
                case "Docs": return 17;
                default: return 99;
            }
        }

        // ============================================================
        // 收藏持久化
        // ============================================================

        private void SaveFavorites()
        {
            string data = string.Join("|", _favorites);
            EditorPrefs.SetString(PREFS_KEY_FAVORITES, data);
        }

        private void LoadFavorites()
        {
            _favorites.Clear();
            string data = EditorPrefs.GetString(PREFS_KEY_FAVORITES, "");
            if (!string.IsNullOrEmpty(data))
            {
                foreach (var name in data.Split('|'))
                {
                    if (!string.IsNullOrEmpty(name))
                        _favorites.Add(name);
                }
            }
        }

    }
}
