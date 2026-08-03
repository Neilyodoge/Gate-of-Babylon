using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 局末结算面板（V0.4.6 改 uGUI+TMP）—— 死亡/通关后弹出。
    /// 展示经验明细与层深倍率；本局技能和模块不会带入下一局。
    /// </summary>
    public class ExtractResultPanel : MonoBehaviour
    {
        private static ExtractResultPanel _instance;
        private GameObject _root;
        private TextMeshProUGUI _realmLabel;
        private TextMeshProUGUI _bonusLabel;
        private RectTransform _rows;
        private Button _confirmBtn;
        private TextMeshProUGUI _confirmLabel;
        private System.Action _onConfirm;

        /// <summary>层深倍率：layer 0=100%, 1=115%, 2=130% ... 5=175%</summary>
        public static float LayerMultiplier(int layerIndex)
            => 1f + 0.15f * Mathf.Max(0, layerIndex);

        public enum EndType { Extract, Death, Victory }

        public static void Show(int layerIndex, string realmName,
            int insightEarned, int temperingEarned, int materialsEarned,
            EndType endType,
            System.Action onConfirm)
        {
            if (_instance == null)
            {
                var go = new GameObject("ExtractResultPanel");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ExtractResultPanel>();
            }
            _instance.Display(layerIndex, realmName,
                insightEarned, temperingEarned, materialsEarned,
                endType, onConfirm);
        }

        private void Awake()
        {
            var canvas = UGuiKit.CreateOverlayCanvas("ExtractResultPanel", 126, transform);
            _root = canvas.gameObject;
            UGuiKit.CreateScrim(_root.transform, new Color(0.02f, 0.02f, 0.05f, 0.94f));

            var panel = UGuiKit.CreatePanel(_root.transform, "Panel", new Vector2(600f, 10f), UGuiKit.Panel);
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UGuiKit.AddVLayout(panel, 12f, new RectOffset(32, 32, 26, 26), TextAnchor.UpperCenter);

            _realmLabel = UGuiKit.CreateText(panel, "", 26, UGuiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            UGuiKit.SetHeight(_realmLabel, 36f);

            _rows = new GameObject("Rows", typeof(RectTransform)).GetComponent<RectTransform>();
            _rows.SetParent(panel, false);
            var rv = _rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 6f; rv.childControlWidth = true; rv.childForceExpandWidth = true; rv.childControlHeight = true; rv.childForceExpandHeight = false;

            _bonusLabel = UGuiKit.CreateText(panel, "", 15, new Color(0.7f, 0.78f, 0.9f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(_bonusLabel, 24f);

            _confirmBtn = UGuiKit.CreateButton(panel, "返回基地", OnConfirm, out _confirmLabel, UGuiKit.BtnPrimary, 20, new Vector2(280f, 50f));
            UGuiKit.SetHeight(_confirmBtn.GetComponent<RectTransform>(), 50f);

            _root.SetActive(false);
        }

        private void Display(int layerIndex, string realmName,
            int insightRaw, int temperingRaw, int materialsCount,
            EndType endType,
            System.Action onConfirm)
        {
            _onConfirm = onConfirm;

            if (_root == null) { onConfirm?.Invoke(); return; }

            float expMul = endType switch
            {
                EndType.Death => 0.5f,
                EndType.Extract => 1f,
                EndType.Victory => 2f,
                _ => 1f
            };
            float layerMul = LayerMultiplier(layerIndex);
            float totalMul = expMul * layerMul;
            int insightFinal = Mathf.RoundToInt(insightRaw * totalMul);
            int temperingFinal = Mathf.RoundToInt(temperingRaw * totalMul);

            string endLabel = endType switch
            {
                EndType.Death => "探索失败",
                EndType.Extract => "安全撤离",
                EndType.Victory => "秘境通关",
                _ => "结算"
            };

            _realmLabel.text = $"{endLabel} · {realmName}（第 {layerIndex + 1} 层）";

            for (int i = _rows.childCount - 1; i >= 0; i--) Destroy(_rows.GetChild(i).gameObject);

            string mulLabel = endType == EndType.Death ? "死亡（×0.5）" : "经验";
            AddRow(mulLabel, $"{insightRaw} → {insightFinal}");
            AddRow("历练", $"{temperingRaw} → {temperingFinal}");
            if (materialsCount > 0)
                AddRow("收集素材", $"{materialsCount} 件");

            float runSec = GameManager.Instance != null ? GameManager.Instance.RunElapsedSeconds : 0f;
            if (runSec > 0f)
            {
                int min = Mathf.FloorToInt(runSec / 60f);
                int sec = Mathf.FloorToInt(runSec % 60f);
                AddRow("探索时长", $"{min:D2}:{sec:D2}");
            }

            _bonusLabel.text = totalMul > 1.001f
                ? $"总倍率 ×{totalMul:F2}（结算 ×{expMul:F1} · 层深 ×{layerMul:F2}）"
                : totalMul < 0.999f
                    ? $"总倍率 ×{totalMul:F2}（死亡 ×{expMul:F1}）"
                    : "基础倍率";

            _confirmLabel.text = "返回基地";

            _root.SetActive(true);
            Time.timeScale = 0f;
        }

        private void OnConfirm()
        {
            if (_root != null) _root.SetActive(false);
            Time.timeScale = 1f;
            _onConfirm?.Invoke();
        }

        private void AddRow(string label, string value)
        {
            var row = UGuiKit.CreateRow(_rows, 10f, 30f);
            var hl = row.gameObject.GetComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
            var lbl = UGuiKit.CreateText(row, label, 16, new Color(0.7f, 0.73f, 0.8f), TextAlignmentOptions.Left);
            UGuiKit.SetHeight(lbl, 26f); lbl.GetComponent<LayoutElement>().preferredWidth = 300f;
            var val = UGuiKit.CreateText(row, value, 16, UGuiKit.TextMain, TextAlignmentOptions.Right);
            UGuiKit.SetHeight(val, 26f); val.GetComponent<LayoutElement>().preferredWidth = 220f;
        }
    }
}
