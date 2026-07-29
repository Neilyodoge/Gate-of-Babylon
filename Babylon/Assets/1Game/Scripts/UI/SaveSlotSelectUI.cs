using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 存档槽位选择面板。
    /// 「开始游戏」→ 显示 3 个存档槽（已有/新建），选中后创建或覆盖存档并进入游戏。
    /// 「继续游戏」→ 加载最近槽位直接进入。
    /// </summary>
    public class SaveSlotSelectUI : MonoBehaviour
    {
        private static SaveSlotSelectUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private Action _onSlotSelected;

        public static void Show(Action onSlotSelected)
        {
            EnsureInstance();
            _instance._onSlotSelected = onSlotSelected;
            _instance.Refresh();
            _instance._overlay.style.display = DisplayStyle.Flex;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._overlay != null)
                _instance._overlay.style.display = DisplayStyle.None;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("SaveSlotSelectUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveSlotSelectUI>();
            _instance.Build();
        }

        private void Build()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 12f;

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "save-select-overlay" };
            SetFull(_overlay);
            _overlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.95f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            ChineseFontHelper.Apply(root);
        }

        private void Refresh()
        {
            _overlay.Clear();

            var title = new Label("选择存档");
            title.style.fontSize = 36;
            title.style.color = new Color(0.95f, 0.90f, 0.75f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 24;
            _overlay.Add(title);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            _overlay.Add(row);

            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                int slot = i;
                bool exists = SaveSystem.Instance.SlotExists(slot);
                string summary = SaveSystem.Instance.GetSlotSummary(slot);

                var card = new VisualElement();
                card.style.width = 260;
                card.style.marginLeft = 12;
                card.style.marginRight = 12;
                card.style.paddingTop = 20;
                card.style.paddingBottom = 20;
                card.style.paddingLeft = 20;
                card.style.paddingRight = 20;
                card.style.backgroundColor = exists
                    ? new Color(0.12f, 0.14f, 0.20f, 1f)
                    : new Color(0.08f, 0.09f, 0.13f, 1f);
                SetBorder(card, 2, exists
                    ? new Color(0.4f, 0.6f, 1f, 0.7f)
                    : new Color(0.3f, 0.3f, 0.35f, 0.5f), 10);

                var slotLabel = new Label($"存档 {slot + 1}");
                slotLabel.style.fontSize = 20;
                slotLabel.style.color = exists
                    ? new Color(0.7f, 0.85f, 1f)
                    : new Color(0.5f, 0.5f, 0.55f);
                slotLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                slotLabel.style.marginBottom = 10;
                card.Add(slotLabel);

                var infoLabel = new Label(summary);
                infoLabel.style.fontSize = 13;
                infoLabel.style.color = new Color(0.65f, 0.68f, 0.75f);
                infoLabel.style.whiteSpace = WhiteSpace.Normal;
                infoLabel.style.marginBottom = 16;
                card.Add(infoLabel);

                if (exists)
                {
                    var loadBtn = new Button(() => OnLoadSlot(slot))
                    { text = "继续此存档" };
                    StyleBtn(loadBtn, new Color(0.3f, 0.5f, 0.8f, 0.9f));
                    card.Add(loadBtn);

                    var overwriteBtn = new Button(() => OnOverwriteSlot(slot))
                    { text = "覆盖存档" };
                    StyleBtn(overwriteBtn, new Color(0.6f, 0.3f, 0.2f, 0.8f));
                    overwriteBtn.style.marginTop = 8;
                    card.Add(overwriteBtn);
                }
                else
                {
                    var newBtn = new Button(() => OnNewSlot(slot))
                    { text = "创建新存档" };
                    StyleBtn(newBtn, new Color(0.25f, 0.6f, 0.35f, 0.9f));
                    card.Add(newBtn);
                }

                row.Add(card);
            }

            var cancelBtn = new Button(Hide) { text = "返回" };
            cancelBtn.style.marginTop = 24;
            cancelBtn.style.width = 160;
            cancelBtn.style.height = 38;
            cancelBtn.style.fontSize = 16;
            cancelBtn.style.backgroundColor = new Color(0.25f, 0.25f, 0.3f, 0.8f);
            cancelBtn.style.color = new Color(0.7f, 0.7f, 0.75f);
            SetBorder(cancelBtn, 1, new Color(0.4f, 0.4f, 0.45f), 6);
            _overlay.Add(cancelBtn);

            ChineseFontHelper.Apply(_overlay);
        }

        private void OnNewSlot(int slot)
        {
            SaveSystem.Instance.CreateSlot(slot);
            Hide();
            _onSlotSelected?.Invoke();
        }

        private void OnLoadSlot(int slot)
        {
            SaveSystem.Instance.LoadSlot(slot);
            Hide();
            _onSlotSelected?.Invoke();
        }

        private void OnOverwriteSlot(int slot)
        {
            ShowOverwriteConfirm(slot);
        }

        private void ShowOverwriteConfirm(int slot)
        {
            _overlay.Clear();

            var msg = new Label($"确定要覆盖存档 {slot + 1} 吗？\n之前的所有数据将被删除！");
            msg.style.fontSize = 20;
            msg.style.color = new Color(1f, 0.7f, 0.5f);
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.unityTextAlign = TextAnchor.MiddleCenter;
            msg.style.marginBottom = 24;
            _overlay.Add(msg);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            _overlay.Add(btnRow);

            var yesBtn = new Button(() =>
            {
                SaveSystem.Instance.DeleteSlot(slot);
                SaveSystem.Instance.CreateSlot(slot);
                Hide();
                _onSlotSelected?.Invoke();
            }) { text = "确认覆盖" };
            StyleBtn(yesBtn, new Color(0.7f, 0.3f, 0.2f, 0.9f));
            yesBtn.style.marginRight = 16;
            btnRow.Add(yesBtn);

            var noBtn = new Button(Refresh) { text = "取消" };
            StyleBtn(noBtn, new Color(0.25f, 0.25f, 0.3f, 0.8f));
            btnRow.Add(noBtn);

            ChineseFontHelper.Apply(_overlay);
        }

        private static void StyleBtn(Button btn, Color bg)
        {
            btn.style.height = 36;
            btn.style.fontSize = 15;
            btn.style.backgroundColor = bg;
            btn.style.color = Color.white;
            SetBorder(btn, 1, new Color(bg.r + 0.15f, bg.g + 0.15f, bg.b + 0.15f, 0.8f), 6);
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
