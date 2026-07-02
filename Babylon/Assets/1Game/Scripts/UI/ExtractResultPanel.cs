using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 撤离成果面板（UITK）——撤离成功后弹出，展示本次获得的资源明细 + 层深倍率。
    /// 按"返回洞府"关闭面板并恢复游戏。
    /// </summary>
    public class ExtractResultPanel : MonoBehaviour
    {
        private static ExtractResultPanel _instance;
        private UIDocument _doc;
        private VisualElement _overlay;
        private System.Action _onConfirm;

        /// <summary>层深倍率：layer 0=100%, 1=115%, 2=130% ... 5=175%</summary>
        public static float LayerMultiplier(int layerIndex)
            => 1f + 0.15f * Mathf.Max(0, layerIndex);

        public static void Show(int layerIndex, string realmName,
            int insightEarned, int temperingEarned, int materialsEarned,
            System.Action onConfirm)
        {
            if (_instance == null)
            {
                var go = new GameObject("ExtractResultPanel");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ExtractResultPanel>();
            }
            _instance.Display(layerIndex, realmName,
                insightEarned, temperingEarned, materialsEarned, onConfirm);
        }

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>("UI/AvatarSelectPanelSettings");
            var tree = Resources.Load<VisualTreeAsset>("UI/ExtractResultPanel");

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panelSettings;
            _doc.visualTreeAsset = tree;
            _doc.sortingOrder = 15f;
            ChineseFontHelper.Apply(_doc.rootVisualElement);

            var root = _doc.rootVisualElement;
            if (root != null && root.childCount == 0 && tree != null)
                tree.CloneTree(root);

            _overlay = root?.Q<VisualElement>("overlay");
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void Display(int layerIndex, string realmName,
            int insightRaw, int temperingRaw, int materialsCount,
            System.Action onConfirm)
        {
            _onConfirm = onConfirm;
            if (_overlay == null) { onConfirm?.Invoke(); return; }

            var root = _doc.rootVisualElement;
            float mul = LayerMultiplier(layerIndex);
            int insightFinal = Mathf.RoundToInt(insightRaw * mul);
            int temperingFinal = Mathf.RoundToInt(temperingRaw * mul);

            root.Q<Label>("realm").text = $"秘境深度：{realmName}（第 {layerIndex + 1} 层）";

            var rows = root.Q<VisualElement>("rows");
            rows.Clear();

            AddRow(rows, "经验（50%）", $"{insightRaw} → {insightFinal}");
            AddRow(rows, "历练", $"{temperingRaw} → {temperingFinal}");
            if (materialsCount > 0)
                AddRow(rows, "洞府素材", $"{materialsCount} 件");

            string bonusText = mul > 1.001f
                ? $"层深倍率 ×{mul:F2}（深入第 {layerIndex + 1} 层奖励）"
                : "第 1 层：无额外倍率";
            root.Q<Label>("bonus").text = bonusText;

            var btn = root.Q<Button>("confirm");
            btn.clicked -= OnConfirm;
            btn.clicked += OnConfirm;

            _overlay.style.display = DisplayStyle.Flex;
            Time.timeScale = 0f;
        }

        private void AddRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("erp-row");
            var lbl = new Label(label);
            lbl.AddToClassList("erp-row__label");
            row.Add(lbl);
            var val = new Label(value);
            val.AddToClassList("erp-row__value");
            row.Add(val);
            parent.Add(row);
        }

        private void OnConfirm()
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
            _onConfirm?.Invoke();
        }
    }
}
