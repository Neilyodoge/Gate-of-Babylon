using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XianTu
{
    /// <summary>
    /// #6 大秘境层数选择（正式进入挑战前弹出，最高 100 层）。
    ///
    /// 玩家在缓冲区装备好 Build、走到挑战之门后弹出本面板：选定层数 → 「开始挑战」。
    /// 层数越高敌人越强、奖励越丰厚（数值缩放见 <see cref="RiftChamber"/>）。
    /// uGUI + TMP（UGuiKit），与其余 UI 方案一致。
    /// </summary>
    public class RiftTierSelectUI : MonoBehaviour
    {
        public const int MaxTier = 100;
        public const int MinTier = 1;

        private static RiftTierSelectUI _instance;

        private GameObject _root;
        private TextMeshProUGUI _valueLabel;
        private int _tier = 1;
        private Action<int> _onConfirm;

        public static bool IsVisible => _instance != null && _instance._root != null && _instance._root.activeSelf;

        /// <summary>弹出层数选择。<paramref name="startTier"/> 为默认层数（一般=当前已达层数）。</summary>
        public static void Show(int startTier, Action<int> onConfirm)
        {
            if (_instance == null)
            {
                var go = new GameObject("RiftTierSelectUI");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RiftTierSelectUI>();
                _instance.Build();
            }
            _instance._onConfirm = onConfirm;
            _instance._tier = Mathf.Clamp(startTier, MinTier, MaxTier);
            _instance.RefreshValue();
            _instance._root.SetActive(true);
        }

        public static void Hide()
        {
            if (_instance != null && _instance._root != null)
                _instance._root.SetActive(false);
        }

        private void Build()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("RiftTierSelectCanvas", 130, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform);

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(560f, 10f), UGuiKit.Panel);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(panel, 14f, new RectOffset(32, 32, 28, 28), TextAnchor.UpperCenter);

            var title = UGuiKit.CreateText(panel, "选择大秘境层数", 34, UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(title, 48f);

            var sub = UGuiKit.CreateText(panel, $"层数越高 · 敌人越强 · 奖励越丰厚（{MinTier}~{MaxTier} 层）", 18,
                UGuiKit.TextDim, TextAlignmentOptions.Center);
            UGuiKit.SetHeight(sub, 28f);

            _valueLabel = UGuiKit.CreateText(panel, "", 56, UGuiKit.TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(_valueLabel, 76f);

            // 步进行：-10 / -1 / +1 / +10
            var stepRow = UGuiKit.CreateRow(panel, 12f, 52f);
            stepRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            MakeStep(stepRow, "-10", -10);
            MakeStep(stepRow, "-1", -1);
            MakeStep(stepRow, "+1", 1);
            MakeStep(stepRow, "+10", 10);

            // 快捷预设
            var presetRow = UGuiKit.CreateRow(panel, 12f, 46f);
            presetRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            MakePreset(presetRow, 1);
            MakePreset(presetRow, 25);
            MakePreset(presetRow, 50);
            MakePreset(presetRow, 100);

            // 确认 / 取消
            var actRow = UGuiKit.CreateRow(panel, 20f, 56f);
            actRow.gameObject.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var start = UGuiKit.CreateButton(actRow.transform, "开始挑战", OnConfirm, UGuiKit.BtnPrimary, 26, new Vector2(220f, 56f));
            UGuiKit.SetHeight(start.GetComponent<RectTransform>(), 56f);
            start.GetComponent<LayoutElement>().preferredWidth = 220f;
            var cancel = UGuiKit.CreateButton(actRow.transform, "取消", Hide, UGuiKit.BtnNormal, 26, new Vector2(160f, 56f));
            UGuiKit.SetHeight(cancel.GetComponent<RectTransform>(), 56f);
            cancel.GetComponent<LayoutElement>().preferredWidth = 160f;

            _root.SetActive(false);
        }

        private void MakeStep(RectTransform row, string label, int delta)
        {
            var btn = UGuiKit.CreateButton(row.transform, label, () => AddTier(delta), UGuiKit.BtnNormal, 24, new Vector2(96f, 52f));
            UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 52f);
            btn.GetComponent<LayoutElement>().preferredWidth = 96f;
        }

        private void MakePreset(RectTransform row, int value)
        {
            var btn = UGuiKit.CreateButton(row.transform, value.ToString(), () => SetTier(value), UGuiKit.BtnNormal, 22, new Vector2(88f, 46f));
            UGuiKit.SetHeight(btn.GetComponent<RectTransform>(), 46f);
            btn.GetComponent<LayoutElement>().preferredWidth = 88f;
        }

        private void AddTier(int delta) => SetTier(_tier + delta);

        private void SetTier(int value)
        {
            _tier = Mathf.Clamp(value, MinTier, MaxTier);
            RefreshValue();
        }

        private void RefreshValue()
        {
            if (_valueLabel != null) _valueLabel.text = $"第 {_tier} 层";
        }

        private void OnConfirm()
        {
            var cb = _onConfirm;
            _onConfirm = null;
            Hide();
            cb?.Invoke(_tier);
        }
    }
}
