using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.3.3 图鉴（V0.4.6 改 uGUI+TMP）—— 展示已知模块和核心技能的完整目录。
    /// 按 Tab 页切换（模块/技能），支持按大类筛选。主菜单和暂停菜单均可打开。
    /// 类名保留 CodexUITK 以兼容既有调用。
    /// </summary>
    public class CodexUITK : MonoBehaviour
    {
        private static CodexUITK _instance;
        public static bool IsVisible => _instance != null && _instance._visible;

        private bool _visible;

        private GameObject _root;
        private RectTransform _tabsBar;
        private RectTransform _filtersBar;
        private RectTransform _list;        // scroll content
        private TextMeshProUGUI _countLabel;

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
            if (_instance._root != null) _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            if (_instance._root != null) _instance._root.SetActive(false);
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
            var canvas = UGuiKit.CreateOverlayCanvas("CodexCanvas", 122, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.03f, 0.06f, 0.94f));

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(840f, 720f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 10f, new RectOffset(24, 24, 18, 18), TextAnchor.UpperCenter);

            var header = UGuiKit.CreateRow(panel, 10f, 44f);
            header.gameObject.GetComponent<HorizontalLayoutGroup>().childControlWidth = false;
            var title = UGuiKit.CreateText(header, "图鉴", 30, new Color(0.95f, 0.85f, 0.55f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(title, 40f); title.GetComponent<LayoutElement>().preferredWidth = 560f;
            _countLabel = UGuiKit.CreateText(header, "", 16, UGuiKit.TextDim, TextAlignmentOptions.Right);
            UGuiKit.SetHeight(_countLabel, 40f); _countLabel.GetComponent<LayoutElement>().preferredWidth = 160f;
            var close = UGuiKit.CreateButton(header, "✕", Hide, UGuiKit.BtnNormal, 20, new Vector2(40f, 40f));
            UGuiKit.SetHeight(close.GetComponent<RectTransform>(), 40f); close.GetComponent<LayoutElement>().preferredWidth = 40f;

            _tabsBar = UGuiKit.CreateRow(panel, 8f, 40f);
            _tabsBar.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            _filtersBar = UGuiKit.CreateRow(panel, 6f, 36f);
            _filtersBar.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            _list = UGuiKit.CreateScroll(panel, "List", out _, 4f, new RectOffset(6, 6, 6, 6));
            var scrollRoot = (RectTransform)_list.parent;
            var le = UGuiKit.SetHeight(scrollRoot, 520f); le.flexibleHeight = 1f;

            _root.SetActive(false);
        }

        private void Update()
        {
            if (!_visible) return;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
        }

        private void RebuildAll()
        {
            BuildTabs();
            RebuildFilters();
            RebuildList();
        }

        private void BuildTabs()
        {
            for (int i = _tabsBar.childCount - 1; i >= 0; i--) Destroy(_tabsBar.GetChild(i).gameObject);
            AddTab("模块", Tab.Modules);
            AddTab("核心技能", Tab.Skills);
        }

        private void AddTab(string label, Tab tab)
        {
            var b = UGuiKit.CreateButton(_tabsBar, label, () => { _activeTab = tab; _activeFilter = -1; RebuildAll(); },
                tab == _activeTab ? UGuiKit.BtnPrimary : UGuiKit.BtnNormal, 20, new Vector2(140f, 36f));
            UGuiKit.SetHeight(b.GetComponent<RectTransform>(), 36f); b.GetComponent<LayoutElement>().preferredWidth = 140f;
        }

        private void RebuildFilters()
        {
            for (int i = _filtersBar.childCount - 1; i >= 0; i--) Destroy(_filtersBar.GetChild(i).gameObject);
            bool show = _activeTab == Tab.Modules;
            _filtersBar.gameObject.SetActive(show);
            if (!show) return;

            AddFilter("全部", -1);
            AddFilter("触发器", (int)ModuleCategory.Trigger);
            AddFilter("效果器", (int)ModuleCategory.Effect);
            AddFilter("改造件", (int)ModuleCategory.Modifier);
            AddFilter("万能件", (int)ModuleCategory.Universal);
        }

        private void AddFilter(string label, int value)
        {
            var b = UGuiKit.CreateButton(_filtersBar, label, () => { _activeFilter = value; RebuildList(); RefreshFilterColors(); },
                _activeFilter == value ? UGuiKit.BtnPrimary : UGuiKit.BtnNormal, 16, new Vector2(96f, 32f));
            UGuiKit.SetHeight(b.GetComponent<RectTransform>(), 32f); b.GetComponent<LayoutElement>().preferredWidth = 96f;
        }

        private void RefreshFilterColors()
        {
            int[] values = { -1, (int)ModuleCategory.Trigger, (int)ModuleCategory.Effect, (int)ModuleCategory.Modifier, (int)ModuleCategory.Universal };
            for (int i = 0; i < _filtersBar.childCount && i < values.Length; i++)
            {
                var img = _filtersBar.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = (_activeFilter == values[i]) ? UGuiKit.BtnPrimary : UGuiKit.BtnNormal;
            }
        }

        private void RebuildList()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
            if (_activeTab == Tab.Modules) RebuildModuleList();
            else RebuildSkillList();
        }

        private void RebuildModuleList()
        {
            var allModules = Resources.LoadAll<ModuleDef>("Modules");
            if (allModules == null || allModules.Length == 0)
            {
                EmptyLabel("暂无模块数据");
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
                BuildModuleCard(m);

            UpdateCount(filtered.Count);
        }

        private void BuildModuleCard(ModuleDef m)
        {
            var accent = CategoryColor(m.category, 1f);
            string extra = (m.category == ModuleCategory.Trigger || m.category == ModuleCategory.Universal) ? $"消费模型: {m.consumeKind}" : null;
            string desc = !string.IsNullOrEmpty(m.uiDescription) ? m.uiDescription
                        : !string.IsNullOrEmpty(m.description) ? m.description : "（无描述）";
            string nameLine = $"<b><color=#{Hex(RarityColor(m.rarity))}>{m.displayName}</color></b>  " +
                              $"<size=75%><color=#{Hex(CategoryColor(m.category, 0.9f))}>{CategoryName(m.category)} · {RarityName(m.rarity)}</color></size>";
            BuildEntryCard(CategoryGlyph(m.category), accent, nameLine, desc, extra);
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
                EmptyLabel("暂无技能数据");
                UpdateCount(0);
                return;
            }

            Array.Sort(allSkills, (a, b) => string.Compare(a.skillName, b.skillName, StringComparison.Ordinal));
            foreach (var s in allSkills)
            {
                string nameLine = $"<b><color=#{Hex(RarityColor(s.rarity))}>{s.skillName}</color></b>  " +
                                  $"<size=75%><color=#a6bee6>{s.skillType}</color></size>";
                string desc = !string.IsNullOrEmpty(s.description) ? s.description : "（无描述）";
                string extra = $"CD: {s.cooldown:F1}s  |  伤害倍率: {s.baseDamage:F1}";
                BuildEntryCard("⚡", new Color(0.7f, 0.85f, 1f), nameLine, desc, extra);
            }
            UpdateCount(allSkills.Length);
        }

        /// <summary>通用条目卡：左徽章 + 右信息（名称富文本 / 描述 / 附加）。固定高度。</summary>
        private void BuildEntryCard(string glyph, Color accent, string nameLine, string desc, string extra)
        {
            var rowGo = new GameObject("Entry", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_list, false);
            rowGo.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.14f, 0.9f);
            var le = rowGo.GetComponent<LayoutElement>(); le.preferredHeight = 84f; le.minHeight = 84f;
            UGuiKit.AddHLayout(row, 10f, new RectOffset(10, 10, 8, 8), TextAnchor.MiddleLeft, false, true);
            rowGo.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

            // 徽章格
            var cellGo = new GameObject("BadgeCell", typeof(RectTransform), typeof(LayoutElement));
            var cell = (RectTransform)cellGo.transform; cell.SetParent(row, false);
            cellGo.GetComponent<LayoutElement>().preferredWidth = 56f; cellGo.GetComponent<LayoutElement>().minWidth = 56f;
            var badge = UGuiKit.CreateBox(cell, new Color(accent.r, accent.g, accent.b, 0.25f), new Vector2(48f, 48f));
            var brt = (RectTransform)badge.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.anchoredPosition = Vector2.zero;
            var g = UGuiKit.CreateText(brt, glyph, 22, accent, TextAlignmentOptions.Center, FontStyles.Bold);
            var grt = (RectTransform)g.transform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;

            // 信息列
            var infoGo = new GameObject("Info", typeof(RectTransform), typeof(LayoutElement));
            var info = (RectTransform)infoGo.transform; info.SetParent(row, false);
            infoGo.GetComponent<LayoutElement>().flexibleWidth = 1f; infoGo.GetComponent<LayoutElement>().preferredWidth = 700f;
            var iv = infoGo.AddComponent<VerticalLayoutGroup>();
            iv.spacing = 2f; iv.childControlWidth = true; iv.childForceExpandWidth = true; iv.childControlHeight = true; iv.childForceExpandHeight = false;
            iv.childAlignment = TextAnchor.UpperLeft;

            var nameLbl = UGuiKit.CreateText(info, nameLine, 15, UGuiKit.TextMain, TextAlignmentOptions.Left);
            UGuiKit.SetHeight(nameLbl, 22f);
            var descLbl = UGuiKit.CreateText(info, desc, 12, new Color(0.7f, 0.72f, 0.78f), TextAlignmentOptions.TopLeft);
            descLbl.enableWordWrapping = true; descLbl.overflowMode = TextOverflowModes.Ellipsis;
            var dle = descLbl.gameObject.AddComponent<LayoutElement>(); dle.flexibleHeight = 1f; dle.minHeight = 20f;
            if (!string.IsNullOrEmpty(extra))
            {
                var ex = UGuiKit.CreateText(info, extra, 11, new Color(0.55f, 0.6f, 0.7f), TextAlignmentOptions.Left);
                UGuiKit.SetHeight(ex, 16f);
            }
        }

        private void UpdateCount(int count)
        {
            if (_countLabel != null) _countLabel.text = $"共 {count} 条";
        }

        private void EmptyLabel(string text)
        {
            var l = UGuiKit.CreateText(_list, text, 14, new Color(0.5f, 0.52f, 0.58f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(l, 60f);
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

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
    }
}
