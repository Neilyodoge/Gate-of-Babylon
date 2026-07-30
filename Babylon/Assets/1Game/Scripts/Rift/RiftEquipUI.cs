using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境 Build 装备面板（Phase3，V0.4.6 改 uGUI+TMP）。
    /// 缓冲区装备台打开：列出局外背包中所有 Build，玩家选一套装备到角色。
    /// 装备后才可通过挑战门开始计时挑战（GDD §11.4.1 1.2）。
    /// </summary>
    public class RiftEquipUI : MonoBehaviour
    {
        private static RiftEquipUI _instance;
        private GameObject _root;
        private RectTransform _listContainer;   // scroll content
        private TextMeshProUGUI _statusLabel;
        private Action<BuildSnapshot> _onEquipped;

        /// <summary>本次大秘境是否已装备 Build。</summary>
        public static bool HasEquipped { get; private set; }
        /// <summary>当前已装备的 Build 名称。</summary>
        public static string EquippedName { get; private set; }

        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.activeSelf;

        public static void ClearEquipped()
        {
            HasEquipped = false;
            EquippedName = null;
        }

        public static void Show(Action<BuildSnapshot> onEquipped)
        {
            EnsureInstance();
            _instance._onEquipped = onEquipped;
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

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("RiftEquipUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RiftEquipUI>();
            _instance.Build();
        }

        private void Build()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("RiftEquipUI", 136, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.03f, 0.02f, 0.06f, 0.95f));

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(760f, 660f), UGuiKit.Panel);
            UGuiKit.AddVLayout(panel, 10f, new RectOffset(24, 24, 20, 20), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(panel, "装备 Build", 32, new Color(0.85f, 0.7f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 42f);
            var sub = UGuiKit.CreateText(panel, "选择一套局内带出的 Build 装备到角色，装备后即可开始挑战", 14, new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(sub, 22f);
            _statusLabel = UGuiKit.CreateText(panel, "", 15, new Color(0.5f, 0.95f, 0.7f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_statusLabel, 24f);

            _listContainer = UGuiKit.CreateScroll(panel, "List", out _, 6f, new RectOffset(6, 6, 6, 6));
            var scrollRoot = (RectTransform)_listContainer.parent;
            var le = UGuiKit.SetHeight(scrollRoot, 460f); le.flexibleHeight = 1f;

            var closeBtn = UGuiKit.CreateButton(panel, "关闭", Hide, new Color(0.25f, 0.25f, 0.3f, 0.9f), 16, new Vector2(180f, 40f));
            UGuiKit.SetHeight(closeBtn.GetComponent<RectTransform>(), 40f);

            _root.SetActive(false);
        }

        private void Refresh()
        {
            for (int i = _listContainer.childCount - 1; i >= 0; i--) Destroy(_listContainer.GetChild(i).gameObject);
            _statusLabel.text = HasEquipped ? $"当前已装备：{EquippedName}" : "尚未装备 Build";

            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || backpack.Count == 0)
            {
                var empty = UGuiKit.CreateText(_listContainer, "背包中暂无 Build——先完成一次秘境探索带出 Build", 16, new Color(0.6f, 0.5f, 0.5f), TextAlignmentOptions.Center);
                UGuiKit.SetHeight(empty, 60f);
                return;
            }

            for (int i = backpack.Count - 1; i >= 0; i--)
                BuildRow(backpack[i]);
        }

        private void BuildRow(BuildSnapshot snap)
        {
            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(_listContainer, false);
            rowGo.GetComponent<Image>().color = new Color(0.11f, 0.10f, 0.18f, 1f);
            var le = rowGo.GetComponent<LayoutElement>(); le.preferredHeight = 52f; le.minHeight = 52f;
            UGuiKit.AddHLayout(row, 10f, new RectOffset(14, 14, 6, 6), TextAnchor.MiddleLeft);

            var nameLabel = UGuiKit.CreateText(row, snap.buildName, 16, new Color(0.88f, 0.85f, 0.98f), TextAlignmentOptions.Left, FontStyles.Bold);
            UGuiKit.SetHeight(nameLabel, 40f); nameLabel.GetComponent<LayoutElement>().preferredWidth = 200f;

            var summary = UGuiKit.CreateText(row, snap.Summary, 13, new Color(0.62f, 0.62f, 0.72f), TextAlignmentOptions.Left);
            UGuiKit.SetHeight(summary, 40f); var sle = summary.GetComponent<LayoutElement>(); sle.flexibleWidth = 1f; sle.preferredWidth = 360f;

            var equip = UGuiKit.CreateButton(row, "装备", () => EquipBuild(snap), new Color(0.4f, 0.3f, 0.65f, 0.95f), 14, new Vector2(90f, 32f));
            UGuiKit.SetHeight(equip.GetComponent<RectTransform>(), 32f); equip.GetComponent<LayoutElement>().preferredWidth = 90f;
        }

        private void EquipBuild(BuildSnapshot snap)
        {
            if (snap == null) return;
            snap.ApplyToPlayer();
            HasEquipped = true;
            EquippedName = snap.buildName;
            _onEquipped?.Invoke(snap);
            _statusLabel.text = $"当前已装备：{EquippedName}——可前往挑战门开始";
            Debug.Log($"<color=#00ffcc>[RiftEquipUI] 装备 Build：{snap.buildName}</color>");
        }
    }
}
