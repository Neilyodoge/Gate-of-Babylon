using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 悟道蒲团 · 第三个洞府模块（v0.5）—— 消耗永久悟性解锁化身天赋节点。
    ///
    /// 流程：
    /// 1. 玩家在梦境用悟性 buff → 撤离时 50% 转入 SaveData.accumulatedInsight
    /// 2. 在洞府坐蒲团 → 看到天赋节点表 → 选未解锁节点点【参悟】消耗永久悟性
    /// 3. 解锁的天赋 id 进入 SaveData.unlockedTalentIds → 下次入梦时 PermanentTalentLoader 自动挂 StatusEffect
    ///
    /// 实现细节：天赋节点定义直接复用 RealmRewardLibrary 中的 Talent_* 奖励。
    /// </summary>
    public class WuDaoCushion : CaveModule
    {
        public override string ModuleName => "悟道蒲团";
        public override string ModuleIcon => "🧘";
        public override string ModuleRole => "灵力 → 化身成长";
        public override Color ModuleColor => new Color(0.78f, 0.68f, 1f);

        // v0.6 阶段C：面板改为 UITK 成长页（GrowthUITK），蒲团仅作入口
        public override bool IsPanelOpen => GrowthUITK.IsVisible;

        protected override void BuildBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cushion";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0, 0.15f, 0);
            body.transform.localScale = new Vector3(1.4f, 0.15f, 1.4f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var rend = body.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(MaterialHelper.GetLitShader());
                mat.color = new Color(0.45f, 0.25f, 0.55f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ModuleColor * 0.6f);
                rend.material = mat;
            }
        }

        protected override void OpenPanel() => GrowthUITK.Show();
        public override void ClosePanel() => GrowthUITK.Hide();
    }

    /// <summary>
    /// 永久天赋注册表 —— 暴露所有可解锁的化身天赋及其悟性消耗。
    /// 数据来自 RealmRewardLibrary 中的 Talent_* 条目。
    /// </summary>
    public static class PermanentTalentRegistry
    {
        public struct TalentEntry
        {
            public RealmReward reward;
            public int insightCost;
        }

        private static List<TalentEntry> _cache;

        public static IReadOnlyList<TalentEntry> AllTalents
        {
            get
            {
                if (_cache == null) Rebuild();
                return _cache;
            }
        }

        /// <summary>
        /// 自动从 <see cref="RealmRewardLibrary"/> 拉所有 SpiritTalent 类别奖励。
        /// 后续在 RealmRewardLibrary 加新天赋时无需再改本注册表（v0.5 Week 8 技术债 7 清理）。
        ///
        /// 悟性消耗规则（可后续做成数据驱动）：
        /// - 默认 80（与原版一致）
        /// - 后续若加"高阶天赋"，可在 reward.id 命名约定中加 "_Tier2" / "_Tier3" 等标识来分级
        /// </summary>
        public static void Rebuild()
        {
            _cache = new List<TalentEntry>();
            var all = RealmRewardLibrary.ListByCategory(RealmRewardCategory.SpiritTalent);
            foreach (var def in all)
            {
                int cost = ResolveCost(def.id);
                _cache.Add(new TalentEntry { reward = def, insightCost = cost });
            }
        }

        private static int ResolveCost(string id)
        {
            // 命名约定：含 _Tier2 → 160 / _Tier3 → 320 / 其他 → 80
            if (id != null)
            {
                if (id.Contains("_Tier3")) return 320;
                if (id.Contains("_Tier2")) return 160;
            }
            return 80;
        }

        /// <summary>测试 / Editor 热重载用</summary>
        public static void ClearCache() => _cache = null;
    }
}
