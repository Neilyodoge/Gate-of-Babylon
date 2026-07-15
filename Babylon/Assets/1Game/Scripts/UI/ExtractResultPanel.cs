using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XianTu
{
    /// <summary>
    /// 局末结算面板（UITK）—— 死亡/通关后弹出。
    /// 展示经验明细 + 层深倍率 + 遗产模块选择（选 1 个模块带入下局）。
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

        private IReadOnlyList<ModuleDef> _legacyModules;
        private ModuleDef _selectedLegacy;

        private void Display(int layerIndex, string realmName,
            int insightRaw, int temperingRaw, int materialsCount,
            EndType endType, IReadOnlyList<ModuleDef> modulesForLegacy,
            System.Action onConfirm)
        {
            _onConfirm = onConfirm;
            _legacyModules = modulesForLegacy;
            _selectedLegacy = null;

            if (_overlay == null) { onConfirm?.Invoke(); return; }

            var root = _doc.rootVisualElement;

            float expMul = endType switch
            {
                EndType.Death => 0.5f,
                EndType.Extract => 1f, // V0.4: 保留枚举值兼容，不再使用
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

            root.Q<Label>("realm").text = $"{endLabel} · {realmName}（第 {layerIndex + 1} 层）";

            var rows = root.Q<VisualElement>("rows");
            rows.Clear();

            string mulLabel = endType == EndType.Death ? "死亡（×0.5）" : "经验";
            AddRow(rows, mulLabel, $"{insightRaw} → {insightFinal}");
            AddRow(rows, "历练", $"{temperingRaw} → {temperingFinal}");
            if (materialsCount > 0)
                AddRow(rows, "收集素材", $"{materialsCount} 件");

            // V0.2.5：显示单局时长
            float runSec = GameManager.Instance != null ? GameManager.Instance.RunElapsedSeconds : 0f;
            if (runSec > 0f)
            {
                int min = Mathf.FloorToInt(runSec / 60f);
                int sec = Mathf.FloorToInt(runSec % 60f);
                AddRow(rows, "探索时长", $"{min:D2}:{sec:D2}");
            }

            string bonusText = totalMul > 1.001f
                ? $"总倍率 ×{totalMul:F2}（结算 ×{expMul:F1} · 层深 ×{layerMul:F2}）"
                : totalMul < 0.999f
                    ? $"总倍率 ×{totalMul:F2}（死亡 ×{expMul:F1}）"
                    : "基础倍率";
            root.Q<Label>("bonus").text = bonusText;

            // V0.2.2：遗产模块选择区域
            BuildLegacySection(rows);

            var btn = root.Q<Button>("confirm");
            btn.clicked -= OnConfirm;
            btn.clicked += OnConfirm;
            btn.text = _legacyModules != null && _legacyModules.Count > 0
                ? "确认遗产 · 返回基地"
                : "返回基地";

            _overlay.style.display = DisplayStyle.Flex;
            Time.timeScale = 0f;
        }

        private void BuildLegacySection(VisualElement parent)
        {
            if (_legacyModules == null || _legacyModules.Count == 0) return;

            var header = new Label("── 选择 1 件遗产模块（下局首战掉落）──");
            header.AddToClassList("erp-row__label");
            header.style.marginTop = 12;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(header);

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            grid.style.marginTop = 6;
            parent.Add(grid);

            for (int i = 0; i < _legacyModules.Count; i++)
            {
                var mod = _legacyModules[i];
                if (mod == null) continue;

                var card = new Button();
                card.AddToClassList("erp-legacy-card");
                card.style.width = 80;
                card.style.height = 60;
                card.style.marginLeft = 4;
                card.style.marginRight = 4;
                card.style.marginBottom = 4;
                card.style.backgroundColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
                card.style.borderTopWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftWidth = 2;
                card.style.borderRightWidth = 2;
                card.style.borderTopColor = new Color(0.4f, 0.4f, 0.5f);
                card.style.borderBottomColor = new Color(0.4f, 0.4f, 0.5f);
                card.style.borderLeftColor = new Color(0.4f, 0.4f, 0.5f);
                card.style.borderRightColor = new Color(0.4f, 0.4f, 0.5f);

                string shortName = mod.displayName.Length > 4
                    ? mod.displayName.Substring(0, 4)
                    : mod.displayName;
                var lbl = new Label(shortName);
                lbl.style.fontSize = 11;
                lbl.style.color = Color.white;
                lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                card.Add(lbl);

                var catLbl = new Label(mod.category.ToString().Substring(0, 1));
                catLbl.style.fontSize = 9;
                catLbl.style.color = new Color(0.7f, 0.7f, 0.7f);
                catLbl.style.unityTextAlign = TextAnchor.LowerCenter;
                card.Add(catLbl);

                var captured = mod;
                var capturedCard = card;
                card.clicked += () => SelectLegacy(captured, capturedCard, grid);

                grid.Add(card);
            }
        }

        private void SelectLegacy(ModuleDef mod, Button card, VisualElement grid)
        {
            _selectedLegacy = mod;

            // Reset all cards
            foreach (var child in grid.Children())
            {
                if (child is Button btn)
                {
                    btn.style.borderTopColor = new Color(0.4f, 0.4f, 0.5f);
                    btn.style.borderBottomColor = new Color(0.4f, 0.4f, 0.5f);
                    btn.style.borderLeftColor = new Color(0.4f, 0.4f, 0.5f);
                    btn.style.borderRightColor = new Color(0.4f, 0.4f, 0.5f);
                }
            }

            // Highlight selected
            var gold = new Color(1f, 0.85f, 0.3f);
            card.style.borderTopColor = gold;
            card.style.borderBottomColor = gold;
            card.style.borderLeftColor = gold;
            card.style.borderRightColor = gold;

            Debug.Log($"<color=#ffcc33>[遗产] 选中：{mod.displayName}（{mod.category}）</color>");
        }

        private void OnConfirm()
        {
            // V0.2.2：保存遗产选择
            if (_selectedLegacy != null)
            {
                SaveSystem.Instance.Data.lastRunLegacyModuleId = _selectedLegacy.moduleId;
                SaveSystem.Instance.Save();
                Debug.Log($"<color=#ffcc33>[遗产] 已保存：{_selectedLegacy.displayName} → 下局首战掉落</color>");
            }

            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
            _onConfirm?.Invoke();
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
    }
}
