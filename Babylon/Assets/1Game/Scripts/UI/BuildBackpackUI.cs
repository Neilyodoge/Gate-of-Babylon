using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 Build 背包查看面板（V0.4.6 改 uGUI+TMP）。
    /// 展示局外背包中所有已保存的 Build 快照，支持预览和删除。
    /// 后续大秘境实装后将在此选择装备 Build 进入秘境。
    /// </summary>
    public class BuildBackpackUI : MonoBehaviour
    {
        private static BuildBackpackUI _instance;
        private GameObject _root;
        private RectTransform _listContainer;   // scroll content
        private TextMeshProUGUI _countLabel;

        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.activeSelf;

        public static void Show()
        {
            EnsureInstance();
            _instance.Refresh();
            _instance._root.SetActive(true);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance._root.SetActive(false);
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
            var canvas = UGuiKit.CreateOverlayCanvas("BuildBackpackCanvas", 134, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.03f, 0.06f, 0.94f));

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(760f, 660f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 10f, new RectOffset(24, 24, 20, 20), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(panel, "Build 背包", 32, new Color(0.95f, 0.85f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 44f);

            _countLabel = UGuiKit.CreateText(panel, "", 14, new Color(0.55f, 0.58f, 0.65f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_countLabel, 22f);

            _listContainer = UGuiKit.CreateScroll(panel, "List", out _, 6f, new RectOffset(6, 6, 6, 6));
            var scrollRoot = (RectTransform)_listContainer.parent;
            var le = UGuiKit.SetHeight(scrollRoot, 500f); le.flexibleHeight = 1f;

            var closeBtn = UGuiKit.CreateButton(panel, "关闭", Hide, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 40f));
            UGuiKit.SetHeight(closeBtn.GetComponent<RectTransform>(), 40f);

            _root.SetActive(false);
        }

        private void Refresh()
        {
            for (int i = _listContainer.childCount - 1; i >= 0; i--) Destroy(_listContainer.GetChild(i).gameObject);

            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || backpack.Count == 0)
            {
                var empty = UGuiKit.CreateText(_listContainer, "暂无保存的 Build，完成秘境探索后将自动保存", 16, new Color(0.5f, 0.5f, 0.55f), TextAlignmentOptions.Center);
                UGuiKit.SetHeight(empty, 60f);
                _countLabel.text = "共 0 套 Build";
                return;
            }

            _countLabel.text = $"共 {backpack.Count} 套 Build";
            for (int i = backpack.Count - 1; i >= 0; i--)
                BuildRow(backpack[i], i);
        }

        private void BuildRow(BuildSnapshot snap, int index)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_listContainer, false);
            rowGo.GetComponent<Image>().color = new Color(0.10f, 0.12f, 0.17f, 1f);
            var le = rowGo.GetComponent<LayoutElement>(); le.preferredHeight = 52f; le.minHeight = 52f;
            var hl = UGuiKit.AddHLayout(row, 10f, new RectOffset(14, 14, 6, 6), TextAnchor.MiddleLeft);

            var name = UGuiKit.CreateText(row, snap.buildName, 16, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(name, 40f); name.GetComponent<LayoutElement>().preferredWidth = 200f;

            var summary = UGuiKit.CreateText(row, snap.Summary, 13, new Color(0.6f, 0.65f, 0.72f), TextAlignmentOptions.Left);
            UGuiKit.SetHeight(summary, 40f); var sle = summary.GetComponent<LayoutElement>(); sle.flexibleWidth = 1f; sle.preferredWidth = 300f;

            if (snap.savedTimestamp > 0)
            {
                var time = System.DateTimeOffset.FromUnixTimeSeconds(snap.savedTimestamp).LocalDateTime.ToString("MM/dd HH:mm");
                var timeLabel = UGuiKit.CreateText(row, time, 12, new Color(0.45f, 0.48f, 0.55f), TextAlignmentOptions.Right);
                UGuiKit.SetHeight(timeLabel, 40f); timeLabel.GetComponent<LayoutElement>().preferredWidth = 90f;
            }

            var del = UGuiKit.CreateButton(row, "✕", () => DeleteBuild(index), new Color(0.5f, 0.2f, 0.15f, 0.7f), 14, new Vector2(32f, 30f));
            UGuiKit.SetHeight(del.GetComponent<RectTransform>(), 30f); del.GetComponent<LayoutElement>().preferredWidth = 32f;
        }

        private void DeleteBuild(int index)
        {
            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || index < 0 || index >= backpack.Count) return;
            backpack.RemoveAt(index);
            SaveSystem.Instance.Save();
            Refresh();
        }
    }
}
