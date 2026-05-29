using System;
using System.Collections.Generic;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>
    /// 洞府机缘事件（v0.5.4 · GDD 9.1.10）。
    ///
    /// 每次撤离回洞府时按灵脉等级概率触发一个机缘事件：
    ///   触发率：枯脉 5% / 凡脉 10% / 灵脉 15% / 福地 20% / 洞天 30%；连续 5 次不触发 → 第 6 次必触发。
    ///   品质门槛：事件 tier ≤ 灵脉等级才会进池（灵脉越高，越能撞见高级机缘）。
    ///
    /// 选项效果落到已有系统：因果/道心（<see cref="PlayerStateHooks"/>）、灵气（caveQi）、
    /// 灵脉经验（<see cref="SpiritVeinSystem"/>）、历练值存量（修为）、永久悟性（天赋）。
    /// </summary>
    public class CaveOpportunitySystem : MonoBehaviour
    {
        private static CaveOpportunitySystem _instance;
        public static CaveOpportunitySystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CaveOpportunitySystem");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CaveOpportunitySystem>();
                }
                return _instance;
            }
        }

        // 触发率（按灵脉等级 0~4）
        private static readonly float[] TriggerChance = { 0.05f, 0.10f, 0.15f, 0.20f, 0.30f };
        private const int PityThreshold = 5;   // 连续未触发保底

        private int _missStreak;

        public class Opportunity
        {
            public string id;
            public string title;
            public string text;
            /// <summary>所需灵脉等级门槛（tier）：0 枯脉 / 2 灵脉 / 3 福地 / 4 洞天。</summary>
            public int requiredVeinLevel;
            public List<Option> options;
        }

        public class Option
        {
            public string label;
            public string resultText;
            public Action effect;
        }

        /// <summary>撤离回洞府时调用：按灵脉概率 + 保底决定是否触发机缘。</summary>
        public void OnReturnToCave()
        {
            int veinLv = SpiritVeinSystem.Instance.Level;
            float chance = TriggerChance[Mathf.Clamp(veinLv, 0, TriggerChance.Length - 1)];

            bool pity = _missStreak >= PityThreshold;
            if (!pity && UnityEngine.Random.value > chance)
            {
                _missStreak++;
                return;
            }
            _missStreak = 0;

            var pool = BuildPool(veinLv);
            if (pool.Count == 0) return;
            var opp = pool[UnityEngine.Random.Range(0, pool.Count)];
            CaveOpportunityUI.Show(opp);
        }

        /// <summary>调试 / 测试：无视概率，直接按当前灵脉等级筛池触发一个机缘。</summary>
        public void ForceTrigger()
        {
            _missStreak = 0;
            var pool = BuildPool(SpiritVeinSystem.Instance.Level);
            if (pool.Count == 0) return;
            CaveOpportunityUI.Show(pool[UnityEngine.Random.Range(0, pool.Count)]);
        }

        /// <summary>按灵脉等级筛选可触发的机缘（tier ≤ 当前灵脉等级）。</summary>
        private List<Opportunity> BuildPool(int veinLevel)
        {
            var all = AllOpportunities();
            var pool = new List<Opportunity>();
            foreach (var o in all)
                if (o.requiredVeinLevel <= veinLevel) pool.Add(o);
            return pool;
        }

        // ========== 效果快捷方法 ==========

        private static void Karma(int d) => PlayerStateHooks.Instance.ChangeKarma(d);
        private static void Daoxin(int d) => PlayerStateHooks.Instance.ChangeDaoxin(d);
        private static void Qi(int n) { SaveSystem.Instance.Data.caveQi += n; SaveSystem.Instance.Save(); }
        private static void Vein(int n) => SpiritVeinSystem.Instance.InjectExp(n, "机缘");
        private static void Tempering(int n) => CultivationSystem.Instance.GrantPool(n);
        private static void Insight(int n) => InsightSystem.Instance.GrantPermanent(n);

        // ========== 机缘事件池（Demo2 首批）==========

        private static List<Opportunity> AllOpportunities()
        {
            return new List<Opportunity>
            {
                // —— 低级（枯脉+）——
                new Opportunity
                {
                    id = "earth_pulse", title = "地脉异动", requiredVeinLevel = 0,
                    text = "洞府地下传来阵阵震动，灵气自石缝中丝丝涌出。",
                    options = new List<Option>
                    {
                        new Option { label = "探查地脉（灵脉 +60）", resultText = "你顺着地脉探入，引来一缕地气。", effect = () => Vein(60) },
                        new Option { label = "封固洞府（灵气 +25）", resultText = "你以阵法封固，化震动为灵气。", effect = () => Qi(25) },
                    }
                },
                new Opportunity
                {
                    id = "spirit_rain", title = "天降灵雨", requiredVeinLevel = 0,
                    text = "天穹垂下灵雨，草木为之一振。",
                    options = new List<Option>
                    {
                        new Option { label = "承接灵雨（灵气 +35）", resultText = "灵雨入瓮，化作丝丝灵气。", effect = () => Qi(35) },
                        new Option { label = "灌注灵脉（灵脉 +50）", resultText = "你引灵雨入脉，根基微固。", effect = () => Vein(50) },
                    }
                },

                // —— 中级（灵脉+，需灵脉等级 ≥2）——
                new Opportunity
                {
                    id = "wandering_cultivator", title = "游方散修", requiredVeinLevel = 2,
                    text = "一位面容苍老的散修叩响洞府，似有所求。",
                    options = new List<Option>
                    {
                        new Option { label = "赠予灵药（道心 +6）", resultText = "散修感念，临别留下一句机锋。", effect = () => Daoxin(6) },
                        new Option { label = "索取财货（灵气 +50，道心 -6）", resultText = "你强取其囊中灵石，心头掠过一丝阴影。", effect = () => { Qi(50); Daoxin(-6); } },
                        new Option { label = "婉言相送", resultText = "你以礼相送，互道珍重。", effect = () => { } },
                    }
                },
                new Opportunity
                {
                    id = "demon_whisper", title = "心魔试探", requiredVeinLevel = 2,
                    text = "闭关之际，心底浮起一个诱人的声音：“何必苦修，取捷径便是。”",
                    options = new List<Option>
                    {
                        new Option { label = "坚守道心（道心 +8）", resultText = "你心如止水，杂念尽散。", effect = () => Daoxin(8) },
                        new Option { label = "倾听低语（修为 +120，因果 +10）", resultText = "你借了外力，修为大涨，却也欠下业债。", effect = () => { Tempering(120); Karma(10); } },
                    }
                },

                // —— 高级（福地+，需灵脉等级 ≥3）——
                new Opportunity
                {
                    id = "ancient_sword", title = "古剑遗灵", requiredVeinLevel = 3,
                    text = "一柄古剑自地脉浮出，剑灵残识犹存，欲择主而栖。",
                    options = new List<Option>
                    {
                        new Option { label = "接纳剑灵（悟性 +120）", resultText = "剑灵入识海，剑道感悟如泉涌。", effect = () => Insight(120) },
                        new Option { label = "封印参研（灵脉 +120）", resultText = "你封存古剑，借其残气壮大灵脉。", effect = () => Vein(120) },
                    }
                },

                // —— 传说（洞天，需灵脉等级 4）——
                new Opportunity
                {
                    id = "spirit_spring", title = "灵泉涌现", requiredVeinLevel = 4,
                    text = "洞府一角骤然涌出一眼灵泉，清气冲霄。",
                    options = new List<Option>
                    {
                        new Option { label = "引入灵脉（灵脉 +300）", resultText = "灵泉汇入地脉，洞天气象初成。", effect = () => Vein(300) },
                        new Option { label = "凝练精魄（悟性 +200）", resultText = "你以灵泉淬炼神识，悟性大进。", effect = () => Insight(200) },
                    }
                },
            };
        }
    }
}
