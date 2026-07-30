using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// V0.4.1 大秘境结算/奖励面板（Phase3 · 框架，V0.4.6 改 uGUI+TMP）。
    /// GDD Q-009：奖励先搭框架，实际产出（强化材料/天赋点等）后续书面讨论。
    /// 目前仅展示成功/失败 + 用时 + 层数，并预留奖励区域占位。
    /// </summary>
    public class RiftRewardUI : MonoBehaviour
    {
        private static RiftRewardUI _instance;
        private GameObject _root;
        private RectTransform _panelHolder;
        private Action _onDone;

        public static void Show(int tier, float clearSeconds, bool isSuccess, Action onDone)
        {
            EnsureInstance();
            _instance._onDone = onDone;
            _instance.Populate(tier, clearSeconds, isSuccess);
            _instance._root.SetActive(true);
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
            var canvas = UGuiKit.CreateOverlayCanvas("RiftRewardUI", 138, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.02f, 0.05f, 0.95f));

            _panelHolder = UGuiKit.CreateStretch(_root.transform, "Holder");
            var hl = _panelHolder.gameObject.AddComponent<VerticalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

            _root.SetActive(false);
        }

        private void Populate(int tier, float clearSeconds, bool isSuccess)
        {
            for (int i = _panelHolder.childCount - 1; i >= 0; i--) Destroy(_panelHolder.GetChild(i).gameObject);

            var accent = isSuccess ? new Color(0.9f, 0.75f, 0.35f, 0.9f) : new Color(0.6f, 0.3f, 0.3f, 0.8f);
            var frame = UGuiKit.CreateCard(_panelHolder, new Vector2(560f, 440f), accent);
            var fv = frame.gameObject.GetComponent<VerticalLayoutGroup>();
            fv.padding = new RectOffset(40, 40, 30, 30); fv.spacing = 8f; fv.childAlignment = TextAnchor.UpperCenter;

            var title = UGuiKit.CreateText(frame, isSuccess ? "大秘境通关" : "挑战失败", 34,
                isSuccess ? new Color(1f, 0.85f, 0.4f) : new Color(0.95f, 0.5f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 48f);

            AddInfo(frame, $"大秘境层数：第 {tier} 层");
            if (isSuccess)
                AddInfo(frame, $"通关用时：{clearSeconds:F1} 秒");
            else
                AddInfo(frame, "未能击败 Boss，Build 已保留于背包");

            // 奖励占位区
            var rewardGo = new GameObject("RewardBox", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rbrt = (RectTransform)rewardGo.transform; rbrt.SetParent(frame, false);
            rewardGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);
            var rle = rewardGo.GetComponent<LayoutElement>(); rle.preferredHeight = 120f; rle.minHeight = 120f;
            var rv = rewardGo.AddComponent<VerticalLayoutGroup>();
            rv.padding = new RectOffset(20, 20, 14, 14); rv.spacing = 6f; rv.childAlignment = TextAnchor.MiddleCenter;
            rv.childControlWidth = true; rv.childForceExpandWidth = true; rv.childControlHeight = true; rv.childForceExpandHeight = false;

            var rewardTitle = UGuiKit.CreateText(rbrt, "奖励", 16, new Color(0.75f, 0.78f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(rewardTitle, 24f);
            var rewardPlaceholder = UGuiKit.CreateText(rbrt, isSuccess
                ? "（奖励框架预留——强化材料 / 天赋点等产出待策划书面确认）"
                : "（失败无奖励）", 13, new Color(0.55f, 0.58f, 0.65f), TextAlignmentOptions.Center);
            rewardPlaceholder.enableWordWrapping = true;
            var ple = rewardPlaceholder.gameObject.AddComponent<LayoutElement>(); ple.flexibleHeight = 1f; ple.minHeight = 40f;

            var doneBtn = UGuiKit.CreateButton(frame, "返回大秘境入口", OnDone, new Color(0.3f, 0.35f, 0.55f, 0.95f), 17, new Vector2(240f, 44f));
            UGuiKit.SetHeight(doneBtn.GetComponent<RectTransform>(), 44f);
        }

        private void AddInfo(RectTransform parent, string text)
        {
            var l = UGuiKit.CreateText(parent, text, 16, new Color(0.8f, 0.82f, 0.88f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(l, 26f);
        }

        private void OnDone()
        {
            if (_root != null) _root.SetActive(false);
            Time.timeScale = 1f;
            var cb = _onDone;
            _onDone = null;
            cb?.Invoke();
        }
    }
}
