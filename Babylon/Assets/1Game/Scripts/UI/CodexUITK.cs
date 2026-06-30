using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 图鉴 · UI Toolkit 版（v0.6 UI 迁移）—— 已取代旧 IMGUI 版（旧版已删除）。
    /// 结构 Resources/UI/CodexUI.uxml，样式 CodexUI.uss。
    /// GDD V.07：灵物 / 化身天赋系统均已移除，图鉴暂为占位（后续承载模块图鉴）。
    /// 复用 AvatarSelectPanelSettings 做渲染设置（置顶覆盖层）。对外保持 Show/Hide/IsVisible。
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
            var b = new Button(() => RebuildAll()) { text = "图鉴" };
            b.AddToClassList("cx-tab");
            b.AddToClassList("cx-tab--active");
            _tabsBar.Add(b);
        }

        private void RebuildAll()
        {
            RebuildFilters();
            RebuildList();
        }

        // ==================== Filters ====================

        private void RebuildFilters()
        {
            if (_filtersBar == null) return;
            _filtersBar.Clear();
            _filtersBar.style.display = DisplayStyle.None;
        }

        // ==================== List ====================

        private void RebuildList()
        {
            if (_list == null) return;
            _list.Clear();
            var empty = new Label("图鉴开发中");
            empty.AddToClassList("cx-empty");
            _list.Add(empty);
            if (_countLabel != null) _countLabel.text = "共 0 条";
        }

    }
}
