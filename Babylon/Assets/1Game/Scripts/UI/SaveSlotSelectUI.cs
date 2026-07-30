using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 存档槽位选择面板（V0.4.6 改 uGUI+TMP）。
    /// 「开始游戏」→ 显示 3 个存档槽（已有/新建），选中后创建或覆盖存档并进入游戏。
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

            var row = UGuiKit.CreateCardRow(_center, 24f);

            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                int slot = i;
                bool exists = SaveSystem.Instance.SlotExists(slot);
                string summary = SaveSystem.Instance.GetSlotSummary(slot);

                var accent = exists ? new Color(0.4f, 0.6f, 1f, 0.7f) : new Color(0.3f, 0.3f, 0.35f, 0.5f);
                var card = UGuiKit.CreateCard(row, new Vector2(280f, 280f), accent);

                var slotLabel = UGuiKit.CreateText(card, $"存档 {slot + 1}", 20,
                    exists ? new Color(0.7f, 0.85f, 1f) : new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
                UGuiKit.SetHeight(slotLabel, 28f);

                var info = UGuiKit.CreateText(card, summary, 13, new Color(0.65f, 0.68f, 0.75f), TextAlignmentOptions.Top);
                info.enableWordWrapping = true;
                var ile = info.gameObject.AddComponent<LayoutElement>(); ile.flexibleHeight = 1f; ile.minHeight = 60f;

                if (exists)
                {
                    var loadBtn = UGuiKit.CreateButton(card, "继续此存档", () => OnLoadSlot(slot), new Color(0.3f, 0.5f, 0.8f, 0.95f), 15, new Vector2(240f, 38f));
                    UGuiKit.SetHeight(loadBtn.GetComponent<RectTransform>(), 38f);
                    var owBtn = UGuiKit.CreateButton(card, "覆盖存档", () => OnOverwriteSlot(slot), new Color(0.6f, 0.3f, 0.2f, 0.9f), 15, new Vector2(240f, 38f));
                    UGuiKit.SetHeight(owBtn.GetComponent<RectTransform>(), 38f);
                }
                else
                {
                    var newBtn = UGuiKit.CreateButton(card, "创建新存档", () => OnNewSlot(slot), new Color(0.25f, 0.6f, 0.35f, 0.95f), 15, new Vector2(240f, 38f));
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

        private void OnOverwriteSlot(int slot)
        {
            ShowOverwriteConfirm(slot);
        }

        private void ShowOverwriteConfirm(int slot)
        {
            ClearCenter();

            var msg = UGuiKit.CreateText(_center, $"确定要覆盖存档 {slot + 1} 吗？\n之前的所有数据将被删除！", 20, new Color(1f, 0.7f, 0.5f), TextAlignmentOptions.Center);
            msg.enableWordWrapping = true;
            UGuiKit.SetHeight(msg, 80f);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(_center, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 12f;

            var btnRow = UGuiKit.CreateRow(_center, 20f, 42f);
            btnRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            var yes = UGuiKit.CreateButton(btnRow, "确认覆盖", () =>
            {
                SaveSystem.Instance.DeleteSlot(slot);
                SaveSystem.Instance.CreateSlot(slot);
                Hide();
                _onSlotSelected?.Invoke();
            }, new Color(0.7f, 0.3f, 0.2f, 0.95f), 16, new Vector2(180f, 42f));
            UGuiKit.SetHeight(yes.GetComponent<RectTransform>(), 42f); yes.GetComponent<LayoutElement>().preferredWidth = 180f;

            var no = UGuiKit.CreateButton(btnRow, "取消", Refresh, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 42f));
            UGuiKit.SetHeight(no.GetComponent<RectTransform>(), 42f); no.GetComponent<LayoutElement>().preferredWidth = 180f;
        }
    }
}
