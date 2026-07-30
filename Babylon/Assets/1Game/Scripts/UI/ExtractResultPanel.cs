using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace XianTu
{
    /// <summary>
    /// 局末结算面板（V0.4.6 改 uGUI+TMP）—— 死亡/通关后弹出。
    /// 展示经验明细 + 层深倍率 + 遗产模块选择（选 1 个模块带入下局）。
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
            System.Action onConfirm)
        {
            Show(layerIndex, realmName, insightEarned, temperingEarned, materialsEarned,
                 EndType.Death, null, onConfirm);
        }

        public static void Show(int layerIndex, string realmName,
            int insightEarned, int temperingEarned, int materialsEarned,
            EndType endType, IReadOnlyList<ModuleDef> modulesForLegacy,
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
                endType, modulesForLegacy, onConfirm);
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

        private IReadOnlyList<ModuleDef> _legacyModules;
        private ModuleDef _selectedLegacy;
        private readonly List<Image> _legacyFrames = new();

        private void Display(int layerIndex, string realmName,
            int insightRaw, int temperingRaw, int materialsCount,
            EndType endType, IReadOnlyList<ModuleDef> modulesForLegacy,
            System.Action onConfirm)
        {
            _onConfirm = onConfirm;
            _legacyModules = modulesForLegacy;
            _selectedLegacy = null;
            _legacyFrames.Clear();

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

            BuildLegacySection();

            _confirmLabel.text = _legacyModules != null && _legacyModules.Count > 0
                ? "确认遗产 · 返回基地"
                : "返回基地";

            _root.SetActive(true);
            Time.timeScale = 0f;
        }

        private void BuildLegacySection()
        {
            if (_legacyModules == null || _legacyModules.Count == 0) return;

            var header = UGuiKit.CreateText(_rows, "── 选择 1 件遗产模块（下局首战掉落）──", 14, new Color(0.75f, 0.78f, 0.85f), TextAlignmentOptions.Center);
            UGuiKit.SetHeight(header, 28f);

            var grid = UGuiKit.CreateGrid(_rows, new Vector2(84f, 60f), new Vector2(8f, 8f), 5);

            for (int i = 0; i < _legacyModules.Count; i++)
            {
                var mod = _legacyModules[i];
                if (mod == null) continue;

                var cardGo = new GameObject("Legacy", typeof(RectTransform), typeof(Image), typeof(Button));
                var crt = (RectTransform)cardGo.transform;
                crt.SetParent(grid, false);
                var frame = cardGo.GetComponent<Image>();
                frame.color = new Color(0.4f, 0.4f, 0.5f);
                var btn = cardGo.GetComponent<Button>();
                btn.targetGraphic = frame;

                var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)inner.transform; irt.SetParent(crt, false);
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(2f, 2f); irt.offsetMax = new Vector2(-2f, -2f);
                inner.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.3f, 0.95f);
                var iv = inner.AddComponent<VerticalLayoutGroup>();
                iv.padding = new RectOffset(2, 2, 4, 4); iv.childAlignment = TextAnchor.MiddleCenter;
                iv.childControlWidth = true; iv.childForceExpandWidth = true; iv.childControlHeight = true; iv.childForceExpandHeight = false;

                string shortName = mod.displayName.Length > 4 ? mod.displayName.Substring(0, 4) : mod.displayName;
                var lbl = UGuiKit.CreateText(irt, shortName, 12, Color.white, TextAlignmentOptions.Center);
                UGuiKit.SetHeight(lbl, 20f);
                var catLbl = UGuiKit.CreateText(irt, mod.category.ToString().Substring(0, 1), 10, new Color(0.7f, 0.7f, 0.7f), TextAlignmentOptions.Center);
                UGuiKit.SetHeight(catLbl, 14f);

                _legacyFrames.Add(frame);
                var captured = mod;
                var capturedFrame = frame;
                btn.onClick.AddListener(() => SelectLegacy(captured, capturedFrame));
            }
        }

        private void SelectLegacy(ModuleDef mod, Image frame)
        {
            _selectedLegacy = mod;
            foreach (var f in _legacyFrames)
                if (f != null) f.color = new Color(0.4f, 0.4f, 0.5f);
            frame.color = new Color(1f, 0.85f, 0.3f);
            Debug.Log($"<color=#ffcc33>[遗产] 选中：{mod.displayName}（{mod.category}）</color>");
        }

        private void OnConfirm()
        {
            if (_selectedLegacy != null)
            {
                SaveSystem.Instance.Data.lastRunLegacyModuleId = _selectedLegacy.moduleId;
                SaveSystem.Instance.Save();
                Debug.Log($"<color=#ffcc33>[遗产] 已保存：{_selectedLegacy.displayName} → 下局首战掉落</color>");
            }

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
