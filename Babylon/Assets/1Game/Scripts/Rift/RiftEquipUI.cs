using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境 Build 装备面板（Phase3）。
    /// 缓冲区装备台打开：列出局外背包中所有 Build，玩家选一套装备到角色。
    /// 装备后才可通过挑战门开始计时挑战（GDD §11.4.1 1.2）。
    /// </summary>
    public class RiftEquipUI : MonoBehaviour
    {
        private static RiftEquipUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _listContainer;
        private Label _statusLabel;
        private Action<BuildSnapshot> _onEquipped;

        /// <summary>本次大秘境是否已装备 Build。</summary>
        public static bool HasEquipped { get; private set; }
        /// <summary>当前已装备的 Build 名称。</summary>
        public static string EquippedName { get; private set; }

        public static bool IsVisible =>
            _instance != null && _instance._overlay != null
            && _instance._overlay.style.display == DisplayStyle.Flex;

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
            _instance._overlay.style.display = DisplayStyle.Flex;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public static void Hide()
        {
            if (_instance != null && _instance._overlay != null)
                _instance._overlay.style.display = DisplayStyle.None;
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
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 16f;

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "rift-equip-overlay" };
            SetFull(_overlay);
            _overlay.style.backgroundColor = new Color(0.03f, 0.02f, 0.06f, 0.95f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.FlexStart;
            _overlay.style.paddingTop = 60;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var title = new Label("装备 Build");
            title.style.fontSize = 32;
            title.style.color = new Color(0.85f, 0.7f, 1f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            _overlay.Add(title);

            var sub = new Label("选择一套局内带出的 Build 装备到角色，装备后即可开始挑战");
            sub.style.fontSize = 14;
            sub.style.color = new Color(0.6f, 0.6f, 0.7f);
            sub.style.marginBottom = 12;
            _overlay.Add(sub);

            _statusLabel = new Label();
            _statusLabel.style.fontSize = 15;
            _statusLabel.style.color = new Color(0.5f, 0.95f, 0.7f);
            _statusLabel.style.marginBottom = 12;
            _overlay.Add(_statusLabel);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.maxHeight = 460;
            scrollView.style.width = 700;
            _overlay.Add(scrollView);
            _listContainer = scrollView.contentContainer;

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
            _statusLabel.text = HasEquipped ? $"当前已装备：{EquippedName}" : "尚未装备 Build";

            var backpack = SaveSystem.Instance.Data?.buildBackpack;
            if (backpack == null || backpack.Count == 0)
            {
                var empty = new Label("背包中暂无 Build——先完成一次秘境探索带出 Build");
                empty.style.fontSize = 16;
                empty.style.color = new Color(0.6f, 0.5f, 0.5f);
                empty.style.marginTop = 30;
                _listContainer.Add(empty);
                ChineseFontHelper.Apply(_listContainer);
                return;
            }

            for (int i = backpack.Count - 1; i >= 0; i--)
            {
                var snap = backpack[i];
                _listContainer.Add(BuildRow(snap));
            }
            ChineseFontHelper.Apply(_listContainer);
        }

        private VisualElement BuildRow(BuildSnapshot snap)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 6;
            row.style.paddingTop = 10;
            row.style.paddingBottom = 10;
            row.style.paddingLeft = 14;
            row.style.paddingRight = 14;
            row.style.backgroundColor = new Color(0.11f, 0.10f, 0.18f, 1f);
            SetBorder(row, 1, new Color(0.4f, 0.35f, 0.55f, 0.6f), 6);
            row.style.alignItems = Align.Center;

            var nameLabel = new Label(snap.buildName);
            nameLabel.style.fontSize = 16;
            nameLabel.style.color = new Color(0.88f, 0.85f, 0.98f);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.width = 200;
            row.Add(nameLabel);

            var summary = new Label(snap.Summary);
            summary.style.fontSize = 13;
            summary.style.color = new Color(0.62f, 0.62f, 0.72f);
            summary.style.flexGrow = 1;
            row.Add(summary);

            var equipBtn = new Button(() => EquipBuild(snap)) { text = "装备" };
            equipBtn.style.width = 90;
            equipBtn.style.height = 32;
            equipBtn.style.fontSize = 14;
            equipBtn.style.backgroundColor = new Color(0.4f, 0.3f, 0.65f, 0.9f);
            equipBtn.style.color = Color.white;
            SetBorder(equipBtn, 1, new Color(0.6f, 0.45f, 0.9f), 6);
            row.Add(equipBtn);

            return row;
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
