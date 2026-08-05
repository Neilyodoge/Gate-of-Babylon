using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 存档槽位选择面板（V0.4.6 改 uGUI+TMP）。
    /// 「开始游戏」→ 从上到下显示 3 个存档槽（已有/新建）。
    /// 「继续游戏」→ 加载最近槽位直接进入。
    /// </summary>
    public class SaveSlotSelectUI : MonoBehaviour
    {
        private static SaveSlotSelectUI _instance;
        private GameObject _root;
        private RectTransform _center;
        private Action _onSlotSelected;

        public static void Show(Action onSlotSelected)
        {
            EnsureInstance();
            _instance._onSlotSelected = onSlotSelected;
            _instance.Refresh();
            _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance._root.SetActive(false);
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
            var canvas = UGuiKit.CreateOverlayCanvas("SaveSlotCanvas", 132, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.03f, 0.06f, 0.95f));

            _center = UGuiKit.CreatePanel(_root.transform, "Center", new Vector2(1000f, 10f), new Color(0, 0, 0, 0));
            var fit = _center.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var v = UGuiKit.AddVLayout(_center, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter, false, false);
            v.childControlWidth = false;

            _root.SetActive(false);
        }

        private void ClearCenter()
        {
            for (int i = _center.childCount - 1; i >= 0; i--) Destroy(_center.GetChild(i).gameObject);
        }

        private void Refresh()
        {
            ClearCenter();

            var title = UGuiKit.CreateText(_center, "选择存档", 36, new Color(0.95f, 0.90f, 0.75f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 50f);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(_center, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 12f;

            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                int slot = i;
                bool exists = SaveSystem.Instance.SlotExists(slot);
                string summary = SaveSystem.Instance.GetSlotSummary(slot);

                var accent = exists ? new Color(0.4f, 0.6f, 1f, 0.7f) : new Color(0.3f, 0.3f, 0.35f, 0.5f);
                var card = UGuiKit.CreateCard(_center, new Vector2(820f, 190f), accent);

                var slotLabel = UGuiKit.CreateText(card, $"存档 {slot + 1}", 20,
                    exists ? new Color(0.7f, 0.85f, 1f) : new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Left, FontStyles.Bold);
                UGuiKit.SetHeight(slotLabel, 28f);

                var info = UGuiKit.CreateText(card, summary, 13, new Color(0.65f, 0.68f, 0.75f), TextAlignmentOptions.Left);
                info.enableWordWrapping = true;
                UGuiKit.SetHeight(info, 64f);

                if (exists)
                {
                    var buttons = UGuiKit.CreateRow(card, 12f, 38f);
                    buttons.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                    var loadBtn = UGuiKit.CreateButton(buttons, "读取存档", () => OnLoadSlot(slot), new Color(0.3f, 0.5f, 0.8f, 0.95f), 15, new Vector2(150f, 38f));
                    SetButtonLayout(loadBtn, 150f, 38f);
                    var overwriteBtn = UGuiKit.CreateButton(buttons, "覆盖新建", () => ShowDestructiveConfirm(slot, overwrite: true), new Color(0.55f, 0.38f, 0.18f, 0.9f), 15, new Vector2(150f, 38f));
                    SetButtonLayout(overwriteBtn, 150f, 38f);
                    var deleteBtn = UGuiKit.CreateButton(buttons, "删除存档", () => ShowDestructiveConfirm(slot, overwrite: false), new Color(0.6f, 0.3f, 0.2f, 0.9f), 15, new Vector2(150f, 38f));
                    SetButtonLayout(deleteBtn, 150f, 38f);
                }
                else
                {
                    var newBtn = UGuiKit.CreateButton(card, "创建新存档", () => OnNewSlot(slot), new Color(0.25f, 0.6f, 0.35f, 0.95f), 15, new Vector2(180f, 38f));
                    UGuiKit.SetHeight(newBtn.GetComponent<RectTransform>(), 38f);
                }
            }

            var spacer2 = new GameObject("Spacer2", typeof(RectTransform), typeof(LayoutElement));
            spacer2.transform.SetParent(_center, false);
            spacer2.GetComponent<LayoutElement>().preferredHeight = 16f;

            var cancelBtn = UGuiKit.CreateButton(_center, "返回", Hide, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 40f));
            UGuiKit.SetHeight(cancelBtn.GetComponent<RectTransform>(), 40f);
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

        private void ShowDestructiveConfirm(int slot, bool overwrite)
        {
            ClearCenter();

            string action = overwrite ? "覆盖" : "删除";
            var msg = UGuiKit.CreateText(_center, $"确定要{action}存档 {slot + 1} 吗？\n此操作无法撤销。", 20, new Color(1f, 0.7f, 0.5f), TextAlignmentOptions.Center);
            msg.enableWordWrapping = true;
            UGuiKit.SetHeight(msg, 80f);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(_center, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 12f;

            var btnRow = UGuiKit.CreateRow(_center, 20f, 42f);
            btnRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            var yes = UGuiKit.CreateButton(btnRow, $"确认{action}", () =>
            {
                SaveSystem.Instance.DeleteSlot(slot);
                if (overwrite)
                {
                    SaveSystem.Instance.CreateSlot(slot);
                    Hide();
                    _onSlotSelected?.Invoke();
                }
                else
                {
                    Refresh();
                }
            }, new Color(0.7f, 0.3f, 0.2f, 0.95f), 16, new Vector2(180f, 42f));
            UGuiKit.SetHeight(yes.GetComponent<RectTransform>(), 42f); yes.GetComponent<LayoutElement>().preferredWidth = 180f;

            var no = UGuiKit.CreateButton(btnRow, "取消", Refresh, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 42f));
            UGuiKit.SetHeight(no.GetComponent<RectTransform>(), 42f); no.GetComponent<LayoutElement>().preferredWidth = 180f;
        }

        private static void SetButtonLayout(Button button, float width, float height)
        {
            var layout = UGuiKit.SetHeight(button.GetComponent<RectTransform>(), height);
            layout.preferredWidth = width;
            layout.minWidth = width;
        }
    }
}
