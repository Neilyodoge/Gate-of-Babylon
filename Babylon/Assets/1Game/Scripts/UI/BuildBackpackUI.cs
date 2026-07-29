using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 Build 背包查看面板。
    /// 展示局外背包中所有已保存的 Build 快照，支持预览、重命名和删除。
    /// 后续大秘境实装后将在此选择装备 Build 进入秘境。
    /// </summary>
    public class BuildBackpackUI : MonoBehaviour
    {
        private static BuildBackpackUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _listContainer;
        private Label _emptyLabel;
        private Label _countLabel;

        public static bool IsVisible =>
            _instance != null && _instance._overlay != null
            && _instance._overlay.style.display == DisplayStyle.Flex;

        public static void Show()
        {
            EnsureInstance();
            _instance.Refresh();
            _instance._overlay.style.display = DisplayStyle.Flex;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._overlay != null)
                _instance._overlay.style.display = DisplayStyle.None;
        }

        public static void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("BuildBackpackUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BuildBackpackUI>();
            _instance.Build();
        }

        private void Build()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 14f;

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "build-backpack-overlay" };
            SetFull(_overlay);
            _overlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.94f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.FlexStart;
            _overlay.style.paddingTop = 60;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var title = new Label("Build 背包");
            title.style.fontSize = 32;
            title.style.color = new Color(0.95f, 0.85f, 0.55f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            _overlay.Add(title);

            _countLabel = new Label();
            _countLabel.style.fontSize = 14;
            _countLabel.style.color = new Color(0.55f, 0.58f, 0.65f);
            _countLabel.style.marginBottom = 16;
            _overlay.Add(_countLabel);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.maxHeight = 500;
            scrollView.style.width = 700;
            _overlay.Add(scrollView);

            _listContainer = scrollView.contentContainer;

            _emptyLabel = new Label("暂无保存的 Build，完成秘境探索后将自动保存");
            _emptyLabel.style.fontSize = 16;
            _emptyLabel.style.color = new Color(0.5f, 0.5f, 0.55f);
            _emptyLabel.style.marginTop = 40;
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            var closeBtn = new Button(Hide) { text = "关闭" };
            closeBtn.style.marginTop = 20;
            closeBtn.style.width = 160;
            closeBtn.style.height = 38;
            closeBtn.style.fontSize = 16;
            closeBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            closeBtn.style.color = new Color(0.7f, 0.7f, 0.75f);
            SetBorder(closeBtn, 1, new Color(0.4f, 0.4f, 0.45f), 6);
            _overlay.Add(closeBtn);

            ChineseFontHelper.Apply(root);
        }

        private void Refresh()
        {
            _listContainer.Clear();

            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || backpack.Count == 0)
            {
                _listContainer.Add(_emptyLabel);
                _countLabel.text = "共 0 套 Build";
                return;
            }

            _countLabel.text = $"共 {backpack.Count} 套 Build";

            for (int i = backpack.Count - 1; i >= 0; i--)
            {
                int idx = i;
                var snap = backpack[i];
                var row = BuildRow(snap, idx);
                _listContainer.Add(row);
            }

            ChineseFontHelper.Apply(_listContainer);
        }

        private VisualElement BuildRow(BuildSnapshot snap, int index)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 6;
            row.style.paddingTop = 10;
            row.style.paddingBottom = 10;
            row.style.paddingLeft = 14;
            row.style.paddingRight = 14;
            row.style.backgroundColor = new Color(0.10f, 0.12f, 0.17f, 1f);
            SetBorder(row, 1, new Color(0.3f, 0.35f, 0.45f, 0.6f), 6);
            row.style.alignItems = Align.Center;

            // 名称
            var nameLabel = new Label(snap.buildName);
            nameLabel.style.fontSize = 16;
            nameLabel.style.color = new Color(0.85f, 0.88f, 0.95f);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.width = 200;
            row.Add(nameLabel);

            // 摘要
            var summary = new Label(snap.Summary);
            summary.style.fontSize = 13;
            summary.style.color = new Color(0.6f, 0.65f, 0.72f);
            summary.style.flexGrow = 1;
            row.Add(summary);

            // 时间
            if (snap.savedTimestamp > 0)
            {
                var time = System.DateTimeOffset.FromUnixTimeSeconds(snap.savedTimestamp)
                    .LocalDateTime.ToString("MM/dd HH:mm");
                var timeLabel = new Label(time);
                timeLabel.style.fontSize = 12;
                timeLabel.style.color = new Color(0.45f, 0.48f, 0.55f);
                timeLabel.style.width = 80;
                row.Add(timeLabel);
            }

            // 删除按钮
            var delBtn = new Button(() => DeleteBuild(index)) { text = "✕" };
            delBtn.style.width = 30;
            delBtn.style.height = 28;
            delBtn.style.fontSize = 14;
            delBtn.style.backgroundColor = new Color(0.5f, 0.2f, 0.15f, 0.6f);
            delBtn.style.color = new Color(0.9f, 0.5f, 0.4f);
            SetBorder(delBtn, 1, new Color(0.6f, 0.3f, 0.25f, 0.5f), 4);
            row.Add(delBtn);

            return row;
        }

        private void DeleteBuild(int index)
        {
            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || index < 0 || index >= backpack.Count) return;
            backpack.RemoveAt(index);
            SaveSystem.Instance.Save();
            Refresh();
        }

        private static void SetFull(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0;
            e.style.top = 0; e.style.bottom = 0;
        }

        private static void SetBorder(VisualElement e, float w, Color c, float r)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }
    }
}
