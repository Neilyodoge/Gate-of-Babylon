using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境结算/奖励面板（Phase3 · 框架）。
    /// GDD Q-009：奖励先搭框架，实际产出（强化材料/天赋点等）后续书面讨论。
    /// 目前仅展示成功/失败 + 用时 + 层数，并预留奖励区域占位。
    /// </summary>
    public class RiftRewardUI : MonoBehaviour
    {
        private static RiftRewardUI _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private Action _onDone;

        public static void Show(int tier, float clearSeconds, bool isSuccess, Action onDone)
        {
            EnsureInstance();
            _instance._onDone = onDone;
            _instance.Populate(tier, clearSeconds, isSuccess);
            _instance._overlay.style.display = DisplayStyle.Flex;
            // 冻结时间，避免结算面板期间挑战间继续刷怪 / 敌人继续攻击
            Time.timeScale = 0f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("RiftRewardUI");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RiftRewardUI>();
            _instance.Build();
        }

        private void Build()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.sortingOrder = 18f;

            var root = _doc.rootVisualElement;
            _overlay = new VisualElement { name = "rift-reward-overlay" };
            SetFull(_overlay);
            _overlay.style.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 0.95f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            ChineseFontHelper.Apply(root);
        }

        private void Populate(int tier, float clearSeconds, bool isSuccess)
        {
            _overlay.Clear();

            var panel = new VisualElement();
            panel.style.width = 520;
            panel.style.paddingTop = 30;
            panel.style.paddingBottom = 30;
            panel.style.paddingLeft = 40;
            panel.style.paddingRight = 40;
            panel.style.backgroundColor = new Color(0.08f, 0.08f, 0.13f, 1f);
            panel.style.alignItems = Align.Center;
            SetBorder(panel, 2, isSuccess
                ? new Color(0.9f, 0.75f, 0.35f, 0.9f)
                : new Color(0.6f, 0.3f, 0.3f, 0.8f), 12);
            _overlay.Add(panel);

            var title = new Label(isSuccess ? "大秘境通关" : "挑战失败");
            title.style.fontSize = 34;
            title.style.color = isSuccess
                ? new Color(1f, 0.85f, 0.4f)
                : new Color(0.95f, 0.5f, 0.45f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            panel.Add(title);

            AddInfo(panel, $"大秘境层数：第 {tier} 层");
            if (isSuccess)
                AddInfo(panel, $"通关用时：{clearSeconds:F1} 秒");
            else
                AddInfo(panel, "未能击败 Boss，Build 已保留于背包");

            // 奖励占位区（框架）
            var rewardBox = new VisualElement();
            rewardBox.style.marginTop = 18;
            rewardBox.style.marginBottom = 18;
            rewardBox.style.paddingTop = 14;
            rewardBox.style.paddingBottom = 14;
            rewardBox.style.paddingLeft = 20;
            rewardBox.style.paddingRight = 20;
            rewardBox.style.width = 400;
            rewardBox.style.alignItems = Align.Center;
            rewardBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.18f, 1f);
            SetBorder(rewardBox, 1, new Color(0.4f, 0.4f, 0.5f, 0.5f), 8);
            panel.Add(rewardBox);

            var rewardTitle = new Label("奖励");
            rewardTitle.style.fontSize = 16;
            rewardTitle.style.color = new Color(0.75f, 0.78f, 0.85f);
            rewardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            rewardTitle.style.marginBottom = 8;
            rewardBox.Add(rewardTitle);

            var rewardPlaceholder = new Label(isSuccess
                ? "（奖励框架预留——强化材料 / 天赋点等产出待策划书面确认）"
                : "（失败无奖励）");
            rewardPlaceholder.style.fontSize = 13;
            rewardPlaceholder.style.color = new Color(0.55f, 0.58f, 0.65f);
            rewardPlaceholder.style.whiteSpace = WhiteSpace.Normal;
            rewardPlaceholder.style.unityTextAlign = TextAnchor.MiddleCenter;
            rewardBox.Add(rewardPlaceholder);

            var doneBtn = new Button(OnDone) { text = "返回大秘境入口" };
            doneBtn.style.marginTop = 10;
            doneBtn.style.width = 220;
            doneBtn.style.height = 42;
            doneBtn.style.fontSize = 17;
            doneBtn.style.backgroundColor = new Color(0.3f, 0.35f, 0.55f, 0.9f);
            doneBtn.style.color = Color.white;
            SetBorder(doneBtn, 1, new Color(0.5f, 0.55f, 0.8f), 8);
            panel.Add(doneBtn);

            ChineseFontHelper.Apply(_overlay);
        }

        private static void AddInfo(VisualElement parent, string text)
        {
            var l = new Label(text);
            l.style.fontSize = 16;
            l.style.color = new Color(0.8f, 0.82f, 0.88f);
            l.style.marginBottom = 6;
            parent.Add(l);
        }

        private void OnDone()
        {
            _overlay.style.display = DisplayStyle.None;
            Time.timeScale = 1f;   // 恢复时间
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke();
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
