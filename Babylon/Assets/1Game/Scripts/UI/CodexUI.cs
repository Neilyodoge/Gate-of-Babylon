using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 仙物图鉴（v0.5 Week 9）—— 4 个 tab：
    /// 1. 灵物（ItemPool 全部 30 件）
    /// 2. 协同（SynergySystem 全部 30 个）
    /// 3. 境界突破奖励（RealmRewardLibrary 全部 32 个）
    /// 4. 化身天赋（5 化身 × 4 节点）
    ///
    /// 显示来源：直接从静态库 / Resources 加载，无需绑定。
    /// 暂停菜单 / 主菜单都可打开。
    /// </summary>
    public class CodexUI : MonoBehaviour
    {
        private static CodexUI _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private int _tabIndex;     // 0=灵物 1=协同 2=境界奖励 3=化身天赋
        private Vector2 _scroll;
        private int _itemFilterCategory = -1;       // -1=全部
        private SpiritRootType _talentFilterRoot = SpiritRootType.None;

        // 样式
        private GUIStyle _titleStyle, _tabStyle, _tabActiveStyle, _entryNameStyle, _entryDescStyle, _maskStyle, _tagStyle;
        private Texture2D _maskTex;
        private bool _stylesReady;

        public static void Show()
        {
            if (_instance == null)
            {
                var go = new GameObject("CodexUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CodexUI>();
            }
            _instance._visible = true;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _maskTex = new Texture2D(1, 1);
            _maskTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
            _maskTex.Apply();
            _maskStyle = new GUIStyle();
            _maskStyle.normal.background = _maskTex;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.92f, 0.78f);

            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _tabActiveStyle = new GUIStyle(_tabStyle);
            _tabActiveStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

            _entryNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, richText = true,
                alignment = TextAnchor.MiddleLeft
            };
            _entryDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _entryDescStyle.normal.textColor = new Color(0.78f, 0.85f, 0.92f);

            _tagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                richText = true
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none, _maskStyle);

            const float W = 940f, H = 660f;
            float x = (Screen.width - W) * 0.5f;
            float y = (Screen.height - H) * 0.5f;

            GUI.Box(new Rect(x, y, W, H), "");

            GUI.Label(new Rect(x, y + 14f, W, 38f), "📜 仙物图鉴", _titleStyle);

            // Tabs
            const float TabW = 150f, TabH = 34f;
            float tabsStartX = x + (W - TabW * 4 - 24f) * 0.5f;
            float tabY = y + 60f;
            DrawTab(tabsStartX + 0 * (TabW + 8f), tabY, TabW, TabH, "🔮 灵物 30", 0);
            DrawTab(tabsStartX + 1 * (TabW + 8f), tabY, TabW, TabH, "✦ 协同 30", 1);
            DrawTab(tabsStartX + 2 * (TabW + 8f), tabY, TabW, TabH, "★ 境界奖励 32", 2);
            DrawTab(tabsStartX + 3 * (TabW + 8f), tabY, TabW, TabH, "🪞 化身天赋 20", 3);

            // 内容区
            var contentRect = new Rect(x + 24f, y + 110f, W - 48f, H - 170f);
            GUILayout.BeginArea(contentRect);
            DrawContent();
            GUILayout.EndArea();

            // 关闭按钮
            if (GUI.Button(new Rect(x + W - 130f, y + H - 50f, 110f, 36f), "关闭 [Esc]"))
                Hide();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Hide();
                Event.current.Use();
            }
        }

        private void DrawTab(float x, float y, float w, float h, string label, int idx)
        {
            var style = _tabIndex == idx ? _tabActiveStyle : _tabStyle;
            if (GUI.Button(new Rect(x, y, w, h), label, style))
            {
                _tabIndex = idx;
                _scroll = Vector2.zero;
            }
            if (_tabIndex == idx)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(x, y + h - 2f, w, 2f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        private void DrawContent()
        {
            switch (_tabIndex)
            {
                case 0: DrawItemsTab(); break;
                case 1: DrawSynergiesTab(); break;
                case 2: DrawRewardsTab(); break;
                case 3: DrawTalentsTab(); break;
            }
        }

        // ========== Tab 1：灵物 ==========

        private static readonly string[] _categoryNames =
        {
            "⚔ 攻伐", "🛡 护体", "👟 身法", "🔮 异变", "💊 丹药", "📜 功法"
        };

        private void DrawItemsTab()
        {
            // 分类筛选条
            GUILayout.BeginHorizontal();
            if (DrawFilterChip("全部 (30)", _itemFilterCategory == -1)) _itemFilterCategory = -1;
            for (int i = 0; i < _categoryNames.Length; i++)
            {
                if (DrawFilterChip(_categoryNames[i], _itemFilterCategory == i))
                    _itemFilterCategory = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll);

            var allItems = ItemPool.All;
            int shown = 0;
            foreach (var item in allItems)
            {
                if (item == null) continue;
                if (item.scope != ItemScope.RunOnly) continue;
                if (_itemFilterCategory >= 0 && (int)item.category != _itemFilterCategory) continue;

                DrawItemEntry(item);
                shown++;
            }
            if (shown == 0)
            {
                GUILayout.Label("<i>当前分类无灵物</i>", _entryDescStyle);
            }

            GUILayout.EndScrollView();
        }

        private void DrawItemEntry(ItemData item)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            // 品阶色色块
            var rarityCol = item.GetRarityColor();
            var prev = GUI.color;
            GUI.color = rarityCol;
            GUILayout.Label("●", _tagStyle, GUILayout.Width(20));
            GUI.color = prev;

            // 名字 + 品阶 + 分类
            GUILayout.BeginVertical(GUILayout.Width(180));
            string rarityCol_str = ColorUtility.ToHtmlStringRGB(rarityCol);
            GUILayout.Label($"<color=#{rarityCol_str}>{item.itemName}</color>", _entryNameStyle);
            GUILayout.Label($"<color=#888>{RarityName(item.rarity)} · {CategoryName(item.category)}</color>",
                new GUIStyle(_entryDescStyle) { fontSize = 11 });
            GUILayout.EndVertical();

            // 描述
            GUILayout.Label(item.description, _entryDescStyle, GUILayout.Width(440));

            // 元素 tag
            if (item.modTag != ElementTag.None)
            {
                GUILayout.Label($"<color=#{ColorUtility.ToHtmlStringRGB(ElementColor(item.modTag))}>{ElementName(item.modTag)}</color>",
                    _tagStyle, GUILayout.Width(60));
            }

            GUILayout.EndHorizontal();
        }

        // ========== Tab 2：协同 ==========

        private void DrawSynergiesTab()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            var all = SynergySystem.GetAllSynergies();
            var active = SynergySystem.GetActiveSynergies();
            foreach (var s in all)
            {
                bool isActive = active.Contains(s.name);
                GUILayout.BeginHorizontal(GUI.skin.box);

                // 颜色色块
                var prev = GUI.color;
                GUI.color = s.displayColor;
                GUILayout.Label("◆", _tagStyle, GUILayout.Width(20));
                GUI.color = prev;

                GUILayout.BeginVertical(GUILayout.Width(160));
                string col = ColorUtility.ToHtmlStringRGB(s.displayColor);
                string activeTag = isActive ? " <color=#88ff88>● 激活</color>" : "";
                GUILayout.Label($"<color=#{col}>{s.name}</color>{activeTag}", _entryNameStyle);
                GUILayout.Label($"<color=#888>{FormatRequirement(s)}</color>",
                    new GUIStyle(_entryDescStyle) { fontSize = 11 });
                GUILayout.EndVertical();

                GUILayout.Label(s.description, _entryDescStyle, GUILayout.Width(540));

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private static string FormatRequirement(SynergySystem.SynergyDef s)
        {
            if (s.requiredCategories == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.requiredCategories.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append($"{CategoryName(s.requiredCategories[i])}×{s.requiredCounts[i]}");
            }
            return sb.ToString();
        }

        // ========== Tab 3：境界突破奖励 ==========

        private void DrawRewardsTab()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            // 通用奖励
            var generals = new List<RealmReward>();
            generals.AddRange(RealmRewardLibrary.ListByCategory(RealmRewardCategory.Numeric));
            generals.AddRange(RealmRewardLibrary.ListByCategory(RealmRewardCategory.Mechanic));
            generals.AddRange(RealmRewardLibrary.ListByCategory(RealmRewardCategory.Structural));
            generals.AddRange(RealmRewardLibrary.ListByCategory(RealmRewardCategory.Risk));

            GUILayout.Label($"<color=#ffd47a>=== 通用奖励 ({generals.Count}) ===</color>", _entryNameStyle);
            GUILayout.Space(2);
            foreach (var r in generals) DrawRewardEntry(r);

            GUILayout.Space(10);
            var talents = RealmRewardLibrary.ListByCategory(RealmRewardCategory.SpiritTalent);
            GUILayout.Label($"<color=#dfcfff>=== 化身天赋 ({talents.Count}) ===</color>", _entryNameStyle);
            GUILayout.Space(2);
            foreach (var r in talents) DrawRewardEntry(r);

            GUILayout.EndScrollView();
        }

        private void DrawRewardEntry(RealmReward r)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            var prev = GUI.color;
            GUI.color = r.displayColor;
            GUILayout.Label("★", _tagStyle, GUILayout.Width(20));
            GUI.color = prev;

            GUILayout.BeginVertical(GUILayout.Width(180));
            string col = ColorUtility.ToHtmlStringRGB(r.displayColor);
            GUILayout.Label($"<color=#{col}>{r.displayName}</color>", _entryNameStyle);
            string subTag = $"{RewardCategoryName(r.category)}";
            if (r.applicableRoot != SpiritRootType.None) subTag += $" · {RootName(r.applicableRoot)}";
            GUILayout.Label($"<color=#888>{subTag}</color>", new GUIStyle(_entryDescStyle) { fontSize = 11 });
            GUILayout.EndVertical();

            GUILayout.Label(r.description, _entryDescStyle, GUILayout.Width(520));

            GUILayout.EndHorizontal();
        }

        // ========== Tab 4：化身天赋 ==========

        private static readonly SpiritRootType[] _allRoots =
        {
            SpiritRootType.Metal, SpiritRootType.Wood, SpiritRootType.Water,
            SpiritRootType.Fire, SpiritRootType.Earth
        };

        private void DrawTalentsTab()
        {
            // 化身筛选
            GUILayout.BeginHorizontal();
            if (DrawFilterChip("全部 (20)", _talentFilterRoot == SpiritRootType.None))
                _talentFilterRoot = SpiritRootType.None;
            foreach (var root in _allRoots)
            {
                if (DrawFilterChip(RootName(root), _talentFilterRoot == root))
                    _talentFilterRoot = root;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll);

            var unlocked = new HashSet<string>(SaveSystem.Instance.Data.unlockedTalentIds);
            var all = PermanentTalentRegistry.AllTalents;
            foreach (var entry in all)
            {
                if (_talentFilterRoot != SpiritRootType.None && entry.reward.applicableRoot != _talentFilterRoot) continue;
                DrawTalentEntry(entry, unlocked);
            }

            GUILayout.EndScrollView();
        }

        private void DrawTalentEntry(PermanentTalentRegistry.TalentEntry entry, HashSet<string> unlocked)
        {
            var r = entry.reward;
            bool isUnlocked = unlocked.Contains(r.id);

            GUILayout.BeginHorizontal(GUI.skin.box);

            var prev = GUI.color;
            GUI.color = isUnlocked ? r.displayColor : new Color(0.3f, 0.3f, 0.3f);
            GUILayout.Label(isUnlocked ? "✓" : "○", _tagStyle, GUILayout.Width(20));
            GUI.color = prev;

            GUILayout.BeginVertical(GUILayout.Width(180));
            string col = ColorUtility.ToHtmlStringRGB(isUnlocked ? r.displayColor : new Color(0.5f, 0.5f, 0.5f));
            GUILayout.Label($"<color=#{col}>{r.displayName}</color>", _entryNameStyle);
            GUILayout.Label($"<color=#888>{RootName(r.applicableRoot)} · {entry.insightCost} 悟性</color>",
                new GUIStyle(_entryDescStyle) { fontSize = 11 });
            GUILayout.EndVertical();

            GUILayout.Label(r.description, _entryDescStyle, GUILayout.Width(500));

            string statusText = isUnlocked
                ? "<color=#88ff88>已悟</color>"
                : "<color=#777>未悟</color>";
            GUILayout.Label(statusText, _tagStyle, GUILayout.Width(60));

            GUILayout.EndHorizontal();
        }

        // ========== Helpers ==========

        private bool DrawFilterChip(string label, bool isActive)
        {
            var prev = GUI.color;
            if (isActive) GUI.color = new Color(1f, 0.85f, 0.4f);
            bool clicked = GUILayout.Button(label, GUILayout.Height(28));
            GUI.color = prev;
            GUILayout.Space(4);
            return clicked;
        }

        private static string RarityName(ItemRarity r) => r switch
        {
            ItemRarity.Fan => "凡品",
            ItemRarity.Ling => "灵品",
            ItemRarity.Xuan => "玄品",
            ItemRarity.Di => "地品",
            ItemRarity.Tian => "天品",
            _ => "未知"
        };

        private static string CategoryName(ItemCategory c) => c switch
        {
            ItemCategory.Attack => "攻伐",
            ItemCategory.Defense => "护体",
            ItemCategory.Movement => "身法",
            ItemCategory.Anomaly => "异变",
            ItemCategory.Pill => "丹药",
            ItemCategory.Skill => "功法",
            ItemCategory.Herb => "灵药",
            ItemCategory.Ore => "灵矿",
            ItemCategory.BeastMaterial => "妖兽材料",
            ItemCategory.ScripturePage => "古籍残页",
            ItemCategory.PlantSeed => "灵植种子",
            ItemCategory.ArraySigil => "阵法符",
            _ => "?"
        };

        private static string ElementName(ElementTag t) => t switch
        {
            ElementTag.Fire => "火",
            ElementTag.Ice => "冰",
            ElementTag.Thunder => "雷",
            ElementTag.Wind => "风",
            ElementTag.Wood => "木",
            ElementTag.Water => "水",
            ElementTag.Earth => "土",
            ElementTag.Pierce => "穿",
            ElementTag.Life => "生",
            _ => ""
        };

        private static Color ElementColor(ElementTag t) => t switch
        {
            ElementTag.Fire => new Color(1f, 0.55f, 0.25f),
            ElementTag.Ice => new Color(0.55f, 0.80f, 1f),
            ElementTag.Thunder => new Color(0.75f, 0.55f, 1f),
            ElementTag.Wind => new Color(0.65f, 0.95f, 0.75f),
            ElementTag.Wood => new Color(0.45f, 0.92f, 0.45f),
            ElementTag.Water => new Color(0.35f, 0.75f, 1f),
            ElementTag.Earth => new Color(0.88f, 0.72f, 0.42f),
            ElementTag.Pierce => new Color(0.95f, 0.95f, 0.95f),
            ElementTag.Life => new Color(1f, 0.65f, 0.85f),
            _ => Color.gray
        };

        private static string RewardCategoryName(RealmRewardCategory c) => c switch
        {
            RealmRewardCategory.Numeric => "数值类",
            RealmRewardCategory.Mechanic => "机制类",
            RealmRewardCategory.Structural => "结构类",
            RealmRewardCategory.Risk => "风险类",
            RealmRewardCategory.SpiritTalent => "化身天赋",
            _ => "?"
        };

        private static string RootName(SpiritRootType r) => r switch
        {
            SpiritRootType.Metal => "金 · 剑魄",
            SpiritRootType.Wood => "木 · 青囊",
            SpiritRootType.Water => "水 · 影刃",
            SpiritRootType.Fire => "火 · 业火",
            SpiritRootType.Earth => "土 · 御物",
            _ => "通用"
        };
    }
}
