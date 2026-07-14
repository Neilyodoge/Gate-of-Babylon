using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.3.3 图鉴 · UI Toolkit —— 展示已知模块和核心技能的完整目录。
    /// 按 Tab 页切换（模块/技能），支持按大类筛选。
    /// 主菜单和暂停菜单均可打开。
    /// </summary>
    public class CodexUITK : MonoBehaviour
    {
        private static CodexUITK _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _tabsBar;
        private VisualElement _filtersBar;
        private ScrollView _list;
        private Label _countLabel;

        private enum Tab { Modules, Skills }
        private Tab _activeTab = Tab.Modules;
        private int _activeFilter = -1; // -1 = all

        public static void Show()
        {
            EnsureInstance();
            if (_instance == null) return;
            _instance._visible = true;
            _instance._activeTab = Tab.Modules;
            _instance._activeFilter = -1;
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
            _doc.sortingOrder = 12f;
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root == null) return;
            if (root.childCount == 0 && tree != null) tree.CloneTree(root);

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

            // Update title
            var titleEl = root.Q<Label>("title");
            if (titleEl != null) titleEl.text = "图鉴";

            BuildTabs();
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        private void BuildTabs()
        {
            if (_tabsBar == null) return;
            _tabsBar.Clear();
            AddTab("模块", Tab.Modules);
            AddTab("核心技能", Tab.Skills);
        }

        private void AddTab(string label, Tab tab)
        {
            var b = new Button(() => { _activeTab = tab; _activeFilter = -1; RebuildAll(); }) { text = label };
            b.AddToClassList("cx-tab");
            if (tab == _activeTab) b.AddToClassList("cx-tab--active");
            _tabsBar.Add(b);
        }

        private void RebuildAll()
        {
            BuildTabs();
            RebuildFilters();
            RebuildList();
        }

        private void RebuildFilters()
        {
            if (_filtersBar == null) return;
            _filtersBar.Clear();
            _filtersBar.style.display = DisplayStyle.Flex;

            if (_activeTab == Tab.Modules)
            {
                AddFilter("全部", -1);
                AddFilter("触发器", (int)ModuleCategory.Trigger);
                AddFilter("效果器", (int)ModuleCategory.Effect);
                AddFilter("改造件", (int)ModuleCategory.Modifier);
                AddFilter("万能件", (int)ModuleCategory.Universal);
            }
            else
            {
                _filtersBar.style.display = DisplayStyle.None;
            }
        }

        private void AddFilter(string label, int value)
        {
            var b = new Button(() => { _activeFilter = value; RebuildList(); }) { text = label };
            b.AddToClassList("cx-tab");
            if (_activeFilter == value) b.AddToClassList("cx-tab--active");
            _filtersBar.Add(b);
        }

        private void RebuildList()
        {
            if (_list == null) return;
            _list.Clear();

            if (_activeTab == Tab.Modules)
                RebuildModuleList();
            else
                RebuildSkillList();
        }

        private void RebuildModuleList()
        {
            var allModules = Resources.LoadAll<ModuleDef>("Modules");
            if (allModules == null || allModules.Length == 0)
            {
                _list.Add(EmptyLabel("暂无模块数据"));
                UpdateCount(0);
                return;
            }

            var filtered = new List<ModuleDef>();
            foreach (var m in allModules)
            {
                if (m == null) continue;
                if (_activeFilter >= 0 && (int)m.category != _activeFilter) continue;
                filtered.Add(m);
            }

            filtered.Sort((a, b) =>
            {
                int cat = a.category.CompareTo(b.category);
                if (cat != 0) return cat;
                return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
            });

            foreach (var m in filtered)
                _list.Add(BuildModuleCard(m));

            UpdateCount(filtered.Count);
        }

        private VisualElement BuildModuleCard(ModuleDef m)
        {
            var card = new VisualElement();
            card.AddToClassList("cx-card");
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.FlexStart;
            card.style.marginBottom = 4;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.backgroundColor = new Color(0.09f, 0.1f, 0.14f, 0.9f);
            SetBorder(card, 1, CategoryColor(m.category, 0.5f), 6);

            // Left: category badge + rarity
            var badge = new VisualElement();
            badge.style.width = 48;
            badge.style.height = 48;
            badge.style.marginRight = 10;
            badge.style.backgroundColor = CategoryColor(m.category, 0.25f);
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            SetBorder(badge, 1, CategoryColor(m.category, 0.6f), 8);

            var glyph = new Label(CategoryGlyph(m.category));
            glyph.style.fontSize = 22;
            glyph.style.color = CategoryColor(m.category, 1f);
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(glyph);
            card.Add(badge);

            // Right: info
            var info = new VisualElement();
            info.style.flexGrow = 1;

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 2;

            var nameLabel = new Label(m.displayName);
            nameLabel.style.fontSize = 15;
            nameLabel.style.color = RarityColor(m.rarity);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginRight = 8;
            nameRow.Add(nameLabel);

            var catTag = new Label(CategoryName(m.category));
            catTag.style.fontSize = 10;
            catTag.style.color = CategoryColor(m.category, 0.8f);
            catTag.style.backgroundColor = CategoryColor(m.category, 0.15f);
            catTag.style.paddingLeft = 5; catTag.style.paddingRight = 5;
            catTag.style.paddingTop = 1; catTag.style.paddingBottom = 1;
            SetBorder(catTag, 1, CategoryColor(m.category, 0.3f), 3);
            nameRow.Add(catTag);

            var rarityTag = new Label(RarityName(m.rarity));
            rarityTag.style.fontSize = 10;
            rarityTag.style.color = RarityColor(m.rarity);
            rarityTag.style.marginLeft = 4;
            nameRow.Add(rarityTag);

            info.Add(nameRow);

            string desc = !string.IsNullOrEmpty(m.uiDescription) ? m.uiDescription
                        : !string.IsNullOrEmpty(m.description) ? m.description
                        : "（无描述）";
            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = new Color(0.7f, 0.72f, 0.78f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 3;
            info.Add(descLabel);

            // Extra info: consumeKind for triggers, effectRole for effects
            if (m.category == ModuleCategory.Trigger || m.category == ModuleCategory.Universal)
            {
                var ckLabel = new Label($"消费模型: {m.consumeKind}");
                ckLabel.style.fontSize = 10;
                ckLabel.style.color = new Color(0.55f, 0.6f, 0.7f);
                info.Add(ckLabel);
            }

            card.Add(info);
            return card;
        }

        private void RebuildSkillList()
        {
            var allSkills = Resources.LoadAll<SkillData>("Skills");
            if (allSkills == null || allSkills.Length == 0)
            {
                allSkills = Resources.LoadAll<SkillData>("");
                if (allSkills != null)
                {
                    var temp = new List<SkillData>();
                    foreach (var s in allSkills)
                        if (s != null) temp.Add(s);
                    allSkills = temp.ToArray();
                }
            }

            if (allSkills == null || allSkills.Length == 0)
            {
                _list.Add(EmptyLabel("暂无技能数据"));
                UpdateCount(0);
                return;
            }

            Array.Sort(allSkills, (a, b) => string.Compare(a.skillName, b.skillName, StringComparison.Ordinal));

            foreach (var s in allSkills)
                _list.Add(BuildSkillCard(s));

            UpdateCount(allSkills.Length);
        }

        private VisualElement BuildSkillCard(SkillData s)
        {
            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.FlexStart;
            card.style.marginBottom = 4;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 10; card.style.paddingRight = 10;
            card.style.backgroundColor = new Color(0.09f, 0.1f, 0.14f, 0.9f);
            SetBorder(card, 1, new Color(0.4f, 0.55f, 0.7f, 0.4f), 6);

            var badge = new VisualElement();
            badge.style.width = 48;
            badge.style.height = 48;
            badge.style.marginRight = 10;
            badge.style.backgroundColor = new Color(0.15f, 0.2f, 0.3f, 0.6f);
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            SetBorder(badge, 1, new Color(0.4f, 0.55f, 0.8f, 0.5f), 8);

            var glyph = new Label("⚡");
            glyph.style.fontSize = 22;
            glyph.style.color = new Color(0.7f, 0.85f, 1f);
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(glyph);
            card.Add(badge);

            var info = new VisualElement();
            info.style.flexGrow = 1;

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 2;

            var nameLabel = new Label(s.skillName);
            nameLabel.style.fontSize = 15;
            nameLabel.style.color = RarityColor(s.rarity);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.marginRight = 8;
            nameRow.Add(nameLabel);

            var typeTag = new Label(s.skillType.ToString());
            typeTag.style.fontSize = 10;
            typeTag.style.color = new Color(0.65f, 0.75f, 0.9f);
            typeTag.style.backgroundColor = new Color(0.2f, 0.25f, 0.35f, 0.5f);
            typeTag.style.paddingLeft = 5; typeTag.style.paddingRight = 5;
            typeTag.style.paddingTop = 1; typeTag.style.paddingBottom = 1;
            SetBorder(typeTag, 1, new Color(0.35f, 0.4f, 0.55f, 0.4f), 3);
            nameRow.Add(typeTag);
            info.Add(nameRow);

            string desc = !string.IsNullOrEmpty(s.description) ? s.description : "（无描述）";
            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12;
            descLabel.style.color = new Color(0.7f, 0.72f, 0.78f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.marginBottom = 3;
            info.Add(descLabel);

            var statsStr = $"CD: {s.cooldown:F1}s  |  伤害倍率: {s.baseDamage:F1}";
            var statsLabel = new Label(statsStr);
            statsLabel.style.fontSize = 10;
            statsLabel.style.color = new Color(0.55f, 0.6f, 0.7f);
            info.Add(statsLabel);

            card.Add(info);
            return card;
        }

        private void UpdateCount(int count)
        {
            if (_countLabel != null) _countLabel.text = $"共 {count} 条";
        }

        private static Label EmptyLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("cx-empty");
            l.style.fontSize = 14;
            l.style.color = new Color(0.5f, 0.52f, 0.58f);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginTop = 40;
            return l;
        }

        private static string CategoryName(ModuleCategory cat) => cat switch
        {
            ModuleCategory.Trigger => "触发器",
            ModuleCategory.Effect => "效果器",
            ModuleCategory.Modifier => "改造件",
            ModuleCategory.Universal => "万能件",
            _ => "未知"
        };

        private static string CategoryGlyph(ModuleCategory cat) => cat switch
        {
            ModuleCategory.Trigger => "T",
            ModuleCategory.Effect => "E",
            ModuleCategory.Modifier => "M",
            ModuleCategory.Universal => "U",
            _ => "?"
        };

        private static Color CategoryColor(ModuleCategory cat, float alpha) => cat switch
        {
            ModuleCategory.Trigger => new Color(0.3f, 0.7f, 1f, alpha),
            ModuleCategory.Effect => new Color(1f, 0.6f, 0.3f, alpha),
            ModuleCategory.Modifier => new Color(0.5f, 0.9f, 0.5f, alpha),
            ModuleCategory.Universal => new Color(0.9f, 0.7f, 1f, alpha),
            _ => new Color(0.6f, 0.6f, 0.6f, alpha)
        };

        private static Color RarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Fan => new Color(0.75f, 0.75f, 0.75f),
            ItemRarity.Ling => new Color(0.3f, 0.9f, 0.4f),
            ItemRarity.Xuan => new Color(0.4f, 0.6f, 1f),
            ItemRarity.Di => new Color(0.75f, 0.4f, 1f),
            ItemRarity.Tian => new Color(1f, 0.85f, 0.15f),
            _ => Color.white
        };

        private static string RarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Fan => "凡",
            ItemRarity.Ling => "灵",
            ItemRarity.Xuan => "玄",
            ItemRarity.Di => "地",
            ItemRarity.Tian => "天",
            _ => "?"
        };

        private static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopColor = color; e.style.borderBottomColor = color;
            e.style.borderLeftColor = color; e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }
    }
}
