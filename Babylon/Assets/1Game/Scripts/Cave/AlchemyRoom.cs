using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 炼丹房 · 第二个洞府模块（v0.5 搜打撤产业链：灵药 → 丹药）。
    ///
    /// 输入：灵田收获的灵药（Herb 类）
    /// 输出：丹药（Pill 类，记录在 SaveSystem.caveInventory，以 "X 丹" 命名）
    /// 工艺：选定灵药 → 投入丹炉 → 按 GameTime 烧炼一段时间 → 出丹
    ///       可花灵气加速烧炼。
    ///
    /// 丹药用途：出梦前在 PortalUI 中选择"携带 N 颗丹药入梦"，进入梦境后作为 RunOnly 消耗品。
    /// </summary>
    public class AlchemyRoom : CaveModule
    {
        public override string ModuleName => "炼丹房";
        public override string ModuleIcon => "⚱";
        public override string ModuleRole => "灵药 → 丹药 · 烧炼输出";
        public override Color ModuleColor => new Color(0.95f, 0.55f, 0.35f);

        private const int FurnaceCount = 2;
        private const float DefaultRefineDuration = 480f;  // 默认 8 游戏分钟
        // v0.5 Week 6 · 灵砂催化（直接推进 30% 进度，1 颗 / 次）
        private const string CatalystMaterial = "灵砂";
        private const float CatalystProgressBoost = 0.30f;

        [System.Serializable]
        public class Furnace
        {
            public string herbItemName;
            public string pillItemName;
            public float startedAt;
            public float refineDuration;

            public bool IsEmpty => string.IsNullOrEmpty(herbItemName);
            public bool IsDone(float now) => !IsEmpty && now - startedAt >= refineDuration;
            public float Progress(float now) => refineDuration > 0f ? Mathf.Clamp01((now - startedAt) / refineDuration) : 1f;
        }

        private readonly Furnace[] _furnaces = new Furnace[FurnaceCount];
        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        private int _herbPickerForFurnace = -1;

        protected override void Awake()
        {
            for (int i = 0; i < FurnaceCount; i++) _furnaces[i] = new Furnace();
            base.Awake();
            BuildFurnaceVisuals();
        }

        private void BuildFurnaceVisuals()
        {
            for (int i = 0; i < FurnaceCount; i++)
            {
                var furnace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                furnace.name = $"Furnace_{i}";
                furnace.transform.SetParent(transform, false);
                furnace.transform.localPosition = new Vector3((i - 0.5f) * 1.6f, 0.7f, 1.6f);
                furnace.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                var col = furnace.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = furnace.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(0.35f, 0.22f, 0.18f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", ModuleColor * 0.5f);
                    rend.material = mat;
                }
            }
        }

        protected override void OpenPanel() => _panelOpen = true;
        public override void ClosePanel() => _panelOpen = false;

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 580f, H = 380f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("⚱ 炼丹房 · 灵药入炉，丹成出梦", titleStyle);
            GUILayout.Space(4);

            GUILayout.Label($"游戏时间：{GameTime.FormatDuration(GameTime.Instance.Time)}    灵气：{CaveEconomy.Instance.Qi}");
            GUILayout.Space(6);

            float now = GameTime.Instance.Time;
            for (int i = 0; i < FurnaceCount; i++) DrawFurnace(i, now);

            GUILayout.Space(8);
            GUILayout.Label("提示：灵田收获的灵药入炉烧炼后，可在山门处携入梦境。", new GUIStyle(GUI.skin.label) { fontSize = 11 });

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(30))) ClosePanel();
            GUILayout.EndArea();

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) ClosePanel();

            OnGUI_HerbPicker();
        }

        private void DrawFurnace(int idx, float now)
        {
            var f = _furnaces[idx];
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.Label($"丹炉 {idx + 1}", GUILayout.Width(70));

            if (f.IsEmpty)
            {
                GUILayout.Label("（空）", GUILayout.Width(200));
                if (GUILayout.Button("投入灵药", GUILayout.Width(100))) _herbPickerForFurnace = idx;
            }
            else if (f.IsDone(now))
            {
                GUILayout.Label($"<color=#ffaa66>{f.pillItemName} · 已成</color>", new GUIStyle(GUI.skin.label) { richText = true }, GUILayout.Width(220));
                if (GUILayout.Button("出丹", GUILayout.Width(80))) CollectPill(idx);
            }
            else
            {
                float prog = f.Progress(now);
                float remaining = f.refineDuration - (now - f.startedAt);
                GUILayout.Label($"{f.herbItemName} → {f.pillItemName}  {(prog * 100f):F0}%（剩 {GameTime.FormatDuration(remaining)}）", GUILayout.Width(220));
                if (GUILayout.Button("灵气×15", GUILayout.Width(90))) Accelerate(idx);

                int sandCount = SaveSystem.Instance.GetCaveItemCount(CatalystMaterial);
                GUI.enabled = sandCount > 0;
                if (GUILayout.Button($"灵砂 ×1（+{(CatalystProgressBoost * 100):F0}%）", GUILayout.Width(140)))
                    UseCatalyst(idx);
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
        }

        /// <summary>v0.5 Week 6 · 用 1 颗灵砂催化：直接推进 CatalystProgressBoost 比例的进度。</summary>
        private void UseCatalyst(int idx)
        {
            if (!SaveSystem.Instance.ConsumeCaveItem(CatalystMaterial, 1))
            {
                Debug.Log($"<color=red>[炼丹房] {CatalystMaterial} 不足</color>");
                return;
            }
            var f = _furnaces[idx];
            if (f == null || f.IsEmpty) return;

            float boost = f.refineDuration * CatalystProgressBoost;
            f.startedAt -= boost;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffaa66>[炼丹房] 灵砂催化丹炉 {idx + 1}：进度 +{(CatalystProgressBoost * 100):F0}%</color>");
        }

        private void OnGUI_HerbPicker()
        {
            if (_herbPickerForFurnace < 0) return;
            const float W = 380f, H = 280f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Label($"选择灵药入炉（丹炉 {_herbPickerForFurnace + 1}）");
            var herbs = ListAvailableHerbs();
            if (herbs.Count == 0)
            {
                GUILayout.Label("无可用灵药 · 去灵田种植收获");
            }
            else
            {
                foreach (var h in herbs)
                {
                    int count = SaveSystem.Instance.GetCaveItemCount(h);
                    if (GUILayout.Button($"{h}  ×{count}", GUILayout.Height(28)))
                        Refine(_herbPickerForFurnace, h);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Height(28))) _herbPickerForFurnace = -1;
            GUILayout.EndArea();
        }

        private List<string> ListAvailableHerbs()
        {
            var result = new List<string>();
            foreach (var e in SaveSystem.Instance.Data.caveInventory)
            {
                if (e.count <= 0) continue;
                var so = CaveMaterialPool.GetByName(e.itemName);
                if (so != null)
                {
                    if (so.category == ItemCategory.Herb) result.Add(e.itemName);
                }
                else if (e.itemName.Contains("灵药"))
                {
                    result.Add(e.itemName);  // 旧字符串兜底
                }
            }
            return result;
        }

        private void Refine(int idx, string herbName)
        {
            if (!SaveSystem.Instance.ConsumeCaveItem(herbName, 1))
            {
                Debug.Log($"<color=red>[炼丹房] {herbName} 库存不足</color>");
                return;
            }
            SaveSystem.Instance.Save();

            // 通过 SO 链查产物，无 SO 时按字符串替换兜底
            string pillName;
            var herbSo = CaveMaterialPool.GetByName(herbName);
            if (herbSo != null && !string.IsNullOrEmpty(herbSo.processedProductName))
            {
                pillName = herbSo.processedProductName;
            }
            else
            {
                pillName = herbName.Replace("灵药", "丹");  // 旧兜底
                Debug.LogWarning($"[炼丹房] 灵药 {herbName} 缺 processedProductName，用字符串替换兜底为 {pillName}");
            }

            _furnaces[idx].herbItemName = herbName;
            _furnaces[idx].pillItemName = pillName;
            _furnaces[idx].startedAt = GameTime.Instance.Time;
            _furnaces[idx].refineDuration = DefaultRefineDuration;
            _herbPickerForFurnace = -1;

            Debug.Log($"<color=#ffaa66>[炼丹房] 丹炉 {idx + 1} 投入 {herbName} → 将产出 {pillName}</color>");
        }

        private void CollectPill(int idx)
        {
            var f = _furnaces[idx];
            if (f.IsEmpty) return;
            SaveSystem.Instance.AddCaveItem(f.pillItemName, 1);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffaa66>[炼丹房] 出丹：{f.pillItemName}</color>");
            _furnaces[idx] = new Furnace();
        }

        private void Accelerate(int idx)
        {
            const int qiCost = 15;
            const float speedSec = 90f;
            if (!CaveEconomy.Instance.SpendQi(qiCost)) return;
            _furnaces[idx].startedAt -= speedSec;
            Debug.Log($"<color=#88ccff>[炼丹房] 丹炉 {idx + 1} 加速 {speedSec}s（-{qiCost} 灵气）</color>");
        }
    }
}
