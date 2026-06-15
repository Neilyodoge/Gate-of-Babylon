using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 仙物图鉴 · UI Toolkit 版（v0.6 UI 迁移）—— 已取代旧 IMGUI 版（旧版已删除）。
    /// 结构 Resources/UI/CodexUI.uxml，样式 CodexUI.uss，3 标签（灵物 / 协同 / 化身天赋）。
    /// 复用 AvatarSelectPanelSettings 做渲染设置（置顶覆盖层）。对外保持 Show/Hide/IsVisible。
    /// </summary>
    public class CodexUITK : MonoBehaviour
    {
        private static CodexUITK _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;
        private int _tabIndex;
        private int _itemFilterCategory = -1;
        private SpiritRootType _talentFilterRoot = SpiritRootType.None;

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _tabsBar;
        private VisualElement _filtersBar;
        private ScrollView _list;
        private Label _countLabel;
        private readonly Button[] _tabButtons = new Button[3];

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._visible = true;
            _instance.RebuildAll();
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._overlay != null) _instance._overlay.style.display = DisplayStyle.None;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("CodexUITK");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CodexUITK>();
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/CodexUI");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 12f;   // 可从暂停菜单(10)上方打开
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);
            // 样式经 UXML <Style src> 加载（避免 Resources 空规则缓存坑）

            _overlay = root.Q<VisualElement>("overlay");
            _tabsBar = root.Q<VisualElement>("tabs");
            _filtersBar = root.Q<VisualElement>("filters");
            _list = root.Q<ScrollView>("list");
            if (_list != null)
            {
                _list.mode = ScrollViewMode.Vertical;
                _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            }
            _countLabel = root.Q<Label>("count");
            var close = root.Q<Button>("close");
            if (close != null) close.clicked += Hide;

            BuildTabs();
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        // ==================== Tabs ====================

        private void BuildTabs()
        {
            if (_tabsBar == null) return;
            _tabsBar.Clear();
            string[] labels = { "灵物 30", "协同 30", "化身天赋 20" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = new Button(() => SelectTab(idx)) { text = labels[i] };
                b.AddToClassList("cx-tab");
                _tabsBar.Add(b);
                _tabButtons[i] = b;
            }
        }

        private void SelectTab(int idx)
        {
            _tabIndex = idx;
            RebuildAll();
        }

        private void RebuildAll()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                _tabButtons[i].EnableInClassList("cx-tab--active", i == _tabIndex);
            }
            RebuildFilters();
            RebuildList();
        }

        // ==================== Filters ====================

        private static readonly string[] _categoryNames =
        {
            "攻伐", "护体", "身法", "异变", "功法"
        };

        private static readonly ItemCategory[] _categoryValues =
        {
            ItemCategory.StatStacking, ItemCategory.MechanicEnhance, ItemCategory.MechanicModify,
            ItemCategory.Skill
        };

        private static readonly SpiritRootType[] _allRoots =
        {
            SpiritRootType.Metal, SpiritRootType.Wood, SpiritRootType.Water,
            SpiritRootType.Fire, SpiritRootType.Earth
        };

        private void RebuildFilters()
        {
            if (_filtersBar == null) return;
            _filtersBar.Clear();

            if (_tabIndex == 0)
            {
                AddChip("全部", _itemFilterCategory == -1, () => { _itemFilterCategory = -1; RebuildAll(); });
                for (int i = 0; i < _categoryNames.Length; i++)
                {
                    int ci = (int)_categoryValues[i];
                    AddChip(_categoryNames[i], _itemFilterCategory == ci, () => { _itemFilterCategory = ci; RebuildAll(); });
                }
            }
            else if (_tabIndex == 2)
            {
                AddChip("全部", _talentFilterRoot == SpiritRootType.None, () => { _talentFilterRoot = SpiritRootType.None; RebuildAll(); });
                foreach (var root in _allRoots)
                {
                    var rr = root;
                    AddChip(RootName(root), _talentFilterRoot == root, () => { _talentFilterRoot = rr; RebuildAll(); });
                }
            }
            _filtersBar.style.display = _filtersBar.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddChip(string label, bool active, Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.AddToClassList("cx-chip");
            if (active) b.AddToClassList("cx-chip--active");
            _filtersBar.Add(b);
        }

        // ==================== List ====================

        private void RebuildList()
        {
            if (_list == null) return;
            _list.Clear();
            int shown = 0;
            switch (_tabIndex)
            {
                case 0: shown = BuildItems(); break;
                case 1: shown = BuildSynergies(); break;
                case 2: shown = BuildTalents(); break;
            }
            if (shown == 0)
            {
                var empty = new Label("当前分类暂无条目");
                empty.AddToClassList("cx-empty");
                _list.Add(empty);
            }
            if (_countLabel != null) _countLabel.text = $"共 {shown} 条";
        }

        private int BuildItems()
        {
            int shown = 0;
            foreach (var item in ItemPool.All)
            {
                if (item == null || item.scope != ItemScope.RunOnly) continue;
                if (_itemFilterCategory >= 0 && (int)item.category != _itemFilterCategory) continue;

                var rarity = item.GetRarityColor();
                string tag = item.modTag != ElementTag.None ? ElementName(item.modTag) : null;
                _list.Add(MakeRow(rarity, "●", item.itemName, rarity,
                    $"{RarityName(item.rarity)} · {CategoryName(item.category)}",
                    item.description, tag, tag != null ? ElementColor(item.modTag) : Color.gray));
                shown++;
            }
            return shown;
        }

        private int BuildSynergies()
        {
            int shown = 0;
            var active = new HashSet<string>();
            var act = SynergySystem.GetActiveSynergies();
            if (act != null) foreach (var a in act) active.Add(a);
            foreach (var s in SynergySystem.GetAllSynergies())
            {
                bool on = active.Contains(s.name);
                _list.Add(MakeRow(s.displayColor, "◆", s.name, s.displayColor,
                    FormatRequirement(s), s.description,
                    on ? "● 激活" : null, new Color(0.55f, 1f, 0.55f)));
                shown++;
            }
            return shown;
        }

        private int BuildTalents()
        {
            int shown = 0;
            var unlocked = new HashSet<string>();
            var ids = SaveSystem.Instance != null ? SaveSystem.Instance.Data?.unlockedTalentIds : null;
            if (ids != null) foreach (var id in ids) unlocked.Add(id);
            foreach (var entry in PermanentTalentRegistry.AllTalents)
            {
                var r = entry.reward;
                if (_talentFilterRoot != SpiritRootType.None && r.applicableRoot != _talentFilterRoot) continue;
                bool isUnlocked = unlocked.Contains(r.id);
                Color nameCol = isUnlocked ? r.displayColor : new Color(0.5f, 0.5f, 0.5f);
                _list.Add(MakeRow(isUnlocked ? r.displayColor : new Color(0.35f, 0.35f, 0.35f),
                    isUnlocked ? "✓" : "○", r.displayName, nameCol,
                    $"{RootName(r.applicableRoot)} · {entry.insightCost} 灵力",
                    r.description,
                    isUnlocked ? "已悟" : "未悟", isUnlocked ? new Color(0.55f, 1f, 0.55f) : new Color(0.5f, 0.5f, 0.5f)));
                shown++;
            }
            return shown;
        }

        private VisualElement MakeRow(Color dotColor, string dot, string name, Color nameColor,
            string sub, string desc, string tag, Color tagColor)
        {
            var row = new VisualElement();
            row.AddToClassList("cx-row");
            row.style.borderLeftColor = dotColor;

            var d = new Label(dot);
            d.AddToClassList("cx-row__dot");
            d.style.color = dotColor;
            row.Add(d);

            var col = new VisualElement();
            col.AddToClassList("cx-row__namecol");
            var n = new Label(name);
            n.AddToClassList("cx-row__name");
            n.style.color = nameColor;
            col.Add(n);
            var s = new Label(sub);
            s.AddToClassList("cx-row__sub");
            col.Add(s);
            row.Add(col);

            var ds = new Label(desc);
            ds.AddToClassList("cx-row__desc");
            row.Add(ds);

            var t = new Label(string.IsNullOrEmpty(tag) ? "" : tag);
            t.AddToClassList("cx-row__tag");
            t.style.color = tagColor;
            row.Add(t);

            return row;
        }

        // ==================== Helpers（与旧 CodexUI 一致）====================

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
            ItemCategory.StatStacking => "数值堆叠",
            ItemCategory.MechanicEnhance => "机制增强",
            ItemCategory.MechanicModify => "机制修改",
            ItemCategory.Skill => "功法",
            ItemCategory.Herb => "灵药",
            ItemCategory.Ore => "灵矿",
            ItemCategory.BeastMaterial => "妖兽材料",
            ItemCategory.ScripturePage => "古籍残页",
            ItemCategory.PlantSeed => "灵植种子",
            ItemCategory.ArraySigil => "阵法符",
#pragma warning disable CS0612, CS0618
            ItemCategory.Attack => "数值堆叠",
            ItemCategory.Defense => "机制增强",
            ItemCategory.Movement => "数值堆叠",
            ItemCategory.Anomaly => "机制修改",
#pragma warning restore CS0612, CS0618
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
