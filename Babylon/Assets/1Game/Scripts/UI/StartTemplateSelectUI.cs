using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.1.13 起始模板选择面板（UITK，程序化构建，无需 uxml/uss）。
    /// 主菜单「开始」后弹出；选中后回调 onConfirm(template) 并隐藏。
    /// 若无任何模板资产，直接回调 null（Demo1Setup 退回默认分配），保证仍能开始游戏。
    /// </summary>
    public class StartTemplateSelectUI : MonoBehaviour
    {
        private static StartTemplateSelectUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private Action<StartTemplate> _onConfirm;

        public static void Show(Action<StartTemplate> onConfirm)
        {
            var templates = StartTemplateRegistry.All;
            if (templates == null || templates.Count == 0)
            {
                onConfirm?.Invoke(null);
                return;
            }

            if (_instance == null)
            {
                var go = new GameObject("StartTemplateSelectUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<StartTemplateSelectUI>();
                _instance.Build();
            }
            _instance._onConfirm = onConfirm;
            _instance.Populate(templates);
            _instance._overlay.style.display = DisplayStyle.Flex;
        }

        private void Build()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 12f; // 高于主菜单(0)

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "template-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            root.Add(_overlay);

            var title = new Label("选择起始模板");
            title.style.fontSize = 34;
            title.style.color = new Color(0.95f, 0.92f, 0.8f);
            title.style.marginBottom = 8;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _overlay.Add(title);

            var subtitle = new Label("不同模板决定开局的 3 个核心技能、角色形态与起手模块");
            subtitle.style.fontSize = 15;
            subtitle.style.color = new Color(0.7f, 0.72f, 0.78f);
            subtitle.style.marginBottom = 22;
            _overlay.Add(subtitle);

            var row = new VisualElement { name = "cards" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            _overlay.Add(row);

            ChineseFontHelper.Apply(root);
        }

        private void Populate(System.Collections.Generic.IReadOnlyList<StartTemplate> templates)
        {
            var row = _overlay.Q<VisualElement>("cards");
            row.Clear();
            foreach (var tpl in templates)
            {
                if (tpl == null) continue;
                row.Add(BuildCard(tpl));
            }
        }

        private VisualElement BuildCard(StartTemplate tpl)
        {
            var card = new VisualElement();
            card.style.width = 240;
            card.style.marginLeft = 10; card.style.marginRight = 10;
            card.style.marginBottom = 12;
            card.style.paddingTop = 14; card.style.paddingBottom = 14;
            card.style.paddingLeft = 16; card.style.paddingRight = 16;
            card.style.backgroundColor = new Color(0.1f, 0.12f, 0.16f, 1f);
            SetBorder(card, 2, new Color(tpl.themeColor.r, tpl.themeColor.g, tpl.themeColor.b, 0.9f), 10);

            var name = new Label(tpl.displayName);
            name.style.fontSize = 22;
            name.style.color = tpl.themeColor;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.marginBottom = 6;
            card.Add(name);

            if (!string.IsNullOrEmpty(tpl.description))
            {
                var desc = new Label(tpl.description);
                desc.style.fontSize = 13;
                desc.style.color = new Color(0.72f, 0.74f, 0.8f);
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.marginBottom = 10;
                card.Add(desc);
            }

            card.Add(MiniLabel($"Q  {SkillName(tpl.skillQ)}"));
            card.Add(MiniLabel($"E  {SkillName(tpl.skillE)}"));
            card.Add(MiniLabel($"R  {SkillName(tpl.skillR)}"));
            int modCount = tpl.startingModules != null ? tpl.startingModules.Length : 0;
            var mod = MiniLabel($"起手模块 ×{modCount}");
            mod.style.color = new Color(0.55f, 0.85f, 0.65f);
            mod.style.marginTop = 6;
            card.Add(mod);

            var pick = new Button(() => Confirm(tpl)) { text = "选择此模板" };
            pick.style.marginTop = 12;
            pick.style.height = 34;
            pick.style.fontSize = 15;
            card.Add(pick);

            return card;
        }

        private static Label MiniLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 13;
            l.style.color = new Color(0.82f, 0.84f, 0.88f);
            l.style.marginBottom = 2;
            return l;
        }

        private static string SkillName(SkillData s) => s != null ? s.skillName : "（探索获取）";

        private static void SetBorder(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = width; e.style.borderBottomWidth = width;
            e.style.borderLeftWidth = width; e.style.borderRightWidth = width;
            e.style.borderTopColor = color; e.style.borderBottomColor = color;
            e.style.borderLeftColor = color; e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = radius; e.style.borderTopRightRadius = radius;
            e.style.borderBottomLeftRadius = radius; e.style.borderBottomRightRadius = radius;
        }

        private void Confirm(StartTemplate tpl)
        {
            StartTemplateRegistry.Selected = tpl;
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            var cb = _onConfirm;
            _onConfirm = null;
            cb?.Invoke(tpl);
        }
    }
}
