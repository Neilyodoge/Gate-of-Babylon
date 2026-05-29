using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵田 · 第一个洞府模块（v0.5 搜打撤核心）。
    ///
    /// 玩家从秘境拾取【灵植种子】(ItemCategory.PlantSeed) 撤离后，
    /// 在灵田种下 → 按 GameTime 生长 → 收获为【灵药】(ItemCategory.Herb)，
    /// 灵药作为【炼器房】辅料 / 【灵兽园】饲料消耗（v0.5.1 炼丹房移除后的去向）。
    ///
    /// 单局 Demo2 实现：3 块田 + IMGUI 面板（种 / 加速 / 收获）。
    /// </summary>
    public class LingTian : CaveModule
    {
        public override string ModuleName => "灵田";
        public override string ModuleIcon => "🌾";
        public override string ModuleRole => "种植 → 灵药（炼器/灵兽辅料）";
        public override Color ModuleColor => new Color(0.55f, 0.85f, 0.45f);

        public override int InteractionPriority => 30;

        private const int PlotCount = 3;
        private const float DefaultGrowDuration = 600f;  // 默认 10 游戏分钟

        [System.Serializable]
        public class Plot
        {
            public string seedItemName;     // 种下的种子的 itemName（"" 表示空地）
            public string harvestItemName;  // 收获时产出的 itemName
            public float plantedAt;          // 种下时的 GameTime.Time
            public float growDuration;       // 生长时长（秒）
            public bool IsEmpty => string.IsNullOrEmpty(seedItemName);
            public bool IsRipe(float now) => !IsEmpty && now - plantedAt >= growDuration;
            public float Progress(float now) => growDuration > 0f ? Mathf.Clamp01((now - plantedAt) / growDuration) : 1f;
        }

        private readonly Plot[] _plots = new Plot[PlotCount];
        private bool _panelOpen;
        public override bool IsPanelOpen => _panelOpen;

        // 视觉化的田垄（拟显示生长进度）
        private GameObject[] _plotVisuals;

        protected override void Awake()
        {
            // 初始化每块田
            for (int i = 0; i < PlotCount; i++) _plots[i] = new Plot();

            base.Awake();
            BuildPlotVisuals();
        }

        private void BuildPlotVisuals()
        {
            _plotVisuals = new GameObject[PlotCount];
            for (int i = 0; i < PlotCount; i++)
            {
                var plot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plot.name = $"Plot_{i}";
                plot.transform.SetParent(transform, false);
                plot.transform.localPosition = new Vector3((i - 1) * 1.5f, 0.05f, 1.8f);
                plot.transform.localScale = new Vector3(1.2f, 0.1f, 1.2f);
                var col = plot.GetComponent<Collider>();
                if (col != null) Destroy(col);
                var rend = plot.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(MaterialHelper.GetLitShader());
                    mat.color = new Color(0.35f, 0.22f, 0.12f);
                    rend.material = mat;
                }
                _plotVisuals[i] = plot;
            }
        }

        protected override void Update()
        {
            base.Update();
            UpdatePlotVisuals();
        }

        private void UpdatePlotVisuals()
        {
            if (_plotVisuals == null) return;
            float now = GameTime.Instance.Time;
            for (int i = 0; i < PlotCount; i++)
            {
                if (_plotVisuals[i] == null) continue;
                var rend = _plotVisuals[i].GetComponent<Renderer>();
                if (rend == null || rend.material == null) continue;

                var plot = _plots[i];
                if (plot.IsEmpty)
                {
                    rend.material.color = new Color(0.35f, 0.22f, 0.12f);
                }
                else if (plot.IsRipe(now))
                {
                    rend.material.color = new Color(0.9f, 0.85f, 0.3f); // 成熟 = 金黄
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", new Color(0.9f, 0.85f, 0.3f) * 0.7f);
                }
                else
                {
                    float p = plot.Progress(now);
                    rend.material.color = Color.Lerp(new Color(0.35f, 0.22f, 0.12f), new Color(0.45f, 0.7f, 0.3f), p);
                }
            }
        }

        protected override void OpenPanel()
        {
            _panelOpen = true;
        }

        public override void ClosePanel()
        {
            _panelOpen = false;
        }

        // ========== IMGUI 面板 ==========

        private void OnGUI()
        {
            if (!_panelOpen) return;

            const float W = 560f, H = 380f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Space(12);

            // 标题
            var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = ModuleColor;
            GUILayout.Label("🌾 灵田 · 修仙者的菜园", titleStyle);
            GUILayout.Space(4);

            // 提示
            GUILayout.Label($"游戏时间：{GameTime.FormatDuration(GameTime.Instance.Time)}    灵气：{CaveEconomy.Instance.Qi}");
            GUILayout.Space(6);

            // 每块田
            float now = GameTime.Instance.Time;
            for (int i = 0; i < PlotCount; i++)
            {
                DrawPlot(i, now);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭 [ESC]", GUILayout.Height(30)))
            {
                ClosePanel();
            }

            GUILayout.EndArea();

            // ESC 关闭
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                ClosePanel();
            }

            // 种子选择子面板
            OnGUI_SeedPicker();
        }

        private void DrawPlot(int idx, float now)
        {
            var plot = _plots[idx];
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.Label($"田 {idx + 1}", GUILayout.Width(60));

            if (plot.IsEmpty)
            {
                GUILayout.Label("（空）", GUILayout.Width(200));
                if (GUILayout.Button("种植", GUILayout.Width(80)))
                {
                    OpenSeedPicker(idx);
                }
            }
            else if (plot.IsRipe(now))
            {
                GUILayout.Label($"<color=#ffd44a>{plot.seedItemName} · 已成熟</color>", GUIStyleRich(), GUILayout.Width(200));
                if (GUILayout.Button("收获", GUILayout.Width(80)))
                {
                    Harvest(idx);
                }
            }
            else
            {
                float prog = plot.Progress(now);
                float remaining = plot.growDuration - (now - plot.plantedAt);
                GUILayout.Label($"{plot.seedItemName}  {(prog * 100f):F0}%（剩 {GameTime.FormatDuration(remaining)}）", GUILayout.Width(260));
                if (GUILayout.Button("加速 10灵气", GUILayout.Width(110)))
                {
                    TryAccelerate(idx);
                }
            }

            GUILayout.EndHorizontal();
        }

        private GUIStyle _richStyle;
        private GUIStyle GUIStyleRich()
        {
            if (_richStyle == null)
            {
                _richStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return _richStyle;
        }

        // ========== 种植 / 收获 / 加速 ==========

        private int _seedPickerForPlot = -1;

        private void OpenSeedPicker(int plotIdx)
        {
            _seedPickerForPlot = plotIdx;
        }

        /// <summary>从永久存档中找所有 PlantSeed 类的素材（优先按 SO category 判断，无 SO 时按 itemName 兜底）</summary>
        private List<string> ListAvailableSeeds()
        {
            var result = new List<string>();
            var data = SaveSystem.Instance.Data;
            foreach (var e in data.caveInventory)
            {
                if (e.count <= 0) continue;
                var so = CaveMaterialPool.GetByName(e.itemName);
                if (so != null)
                {
                    if (so.category == ItemCategory.PlantSeed) result.Add(e.itemName);
                }
                else if (e.itemName.Contains("种子"))
                {
                    result.Add(e.itemName);  // 旧字符串兜底
                }
            }
            return result;
        }

        private void Plant(int plotIdx, string seedItemName)
        {
            // 消耗一颗种子
            if (!SaveSystem.Instance.ConsumeCaveItem(seedItemName, 1))
            {
                Debug.Log($"<color=red>[灵田] 种子不足：{seedItemName}</color>");
                return;
            }
            SaveSystem.Instance.Save();

            // 通过 SO 链查产物，无 SO 时按字符串替换兜底（旧种子无 processedProductName 时）
            string harvestName;
            var seedSo = CaveMaterialPool.GetByName(seedItemName);
            if (seedSo != null && !string.IsNullOrEmpty(seedSo.processedProductName))
            {
                harvestName = seedSo.processedProductName;
            }
            else
            {
                harvestName = seedItemName.Replace("种子", "灵药");  // 旧兜底
                Debug.LogWarning($"[灵田] 种子 {seedItemName} 缺 processedProductName，用字符串替换兜底为 {harvestName}");
            }

            _plots[plotIdx].seedItemName = seedItemName;
            _plots[plotIdx].harvestItemName = harvestName;
            _plots[plotIdx].plantedAt = GameTime.Instance.Time;
            _plots[plotIdx].growDuration = DefaultGrowDuration;

            _seedPickerForPlot = -1;
            Debug.Log($"<color=#88ff88>[灵田] 田 {plotIdx + 1} 种下 {seedItemName} → 将产出 {harvestName}</color>");
        }

        private void Harvest(int plotIdx)
        {
            var plot = _plots[plotIdx];
            if (plot.IsEmpty) return;
            SaveSystem.Instance.AddCaveItem(plot.harvestItemName, 1);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd44a>[灵田] 收获 1 个 {plot.harvestItemName}</color>");

            _plots[plotIdx] = new Plot();
        }

        private void TryAccelerate(int plotIdx)
        {
            const int qiCost = 10;
            const float speedSec = 60f;  // 每 10 灵气推进 60s

            if (!CaveEconomy.Instance.SpendQi(qiCost)) return;
            _plots[plotIdx].plantedAt -= speedSec;
            Debug.Log($"<color=#88ccff>[灵田] 田 {plotIdx + 1} 加速 {speedSec}s（-{qiCost} 灵气）</color>");
        }

        // ========== 种子选择子面板 ==========

        private void OnGUI_SeedPicker()
        {
            if (_seedPickerForPlot < 0) return;

            const float W = 380f, H = 280f;
            var rect = new Rect((Screen.width - W) * 0.5f, (Screen.height - H) * 0.5f, W, H);
            GUI.Box(rect, "");

            GUILayout.BeginArea(rect);
            GUILayout.Label($"选择种子（田 {_seedPickerForPlot + 1}）");
            var seeds = ListAvailableSeeds();
            if (seeds.Count == 0)
            {
                GUILayout.Label("<color=#ffa080>无可用种子 · 从梦境带回</color>", GUIStyleRich());
            }
            else
            {
                foreach (var s in seeds)
                {
                    int count = SaveSystem.Instance.GetCaveItemCount(s);
                    if (GUILayout.Button($"{s} ×{count}", GUILayout.Height(28)))
                    {
                        Plant(_seedPickerForPlot, s);
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Height(28)))
            {
                _seedPickerForPlot = -1;
            }
            GUILayout.EndArea();
        }

    }
}
