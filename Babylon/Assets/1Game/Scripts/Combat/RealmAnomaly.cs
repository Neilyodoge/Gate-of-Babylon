using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    /// <summary>秘境异象类型（v0.5.5 · 替代隐藏命格的"每局变量"，挂在秘境/地图上而非角色上）。</summary>
    public enum RealmAnomaly
    {
        None,           // 寂灭之地（无异象 · 留白档）
        LingChao,       // 灵潮汹涌：洞府素材/灵脉掉落↑，但敌人更多
        LeiZe,          // 雷泽：全程常驻天劫落雷
        BloodMoon,      // 血月：敌人攻击↑，但击杀收益翻倍；击杀积因果
        DemonGrowth,    // 心魔滋生：心魔积累×2，但历练↑；每层侵蚀道心
        Revival,        // 万灵复苏：精英首次死亡满血复活一次
        DaoHeartTrial,  // 道心试炼：道心变动×2，入定额外攻+10%，入魔额外减伤-15%
        KarmaEcho,      // 因果轮回：每层结束因果反噬/善缘治疗
        OpportunityRush,// 机缘频现：机缘触发率×2、灵力+20%，但敌人攻击+15%
    }

    /// <summary>
    /// 秘境异象系统（v0.5.5）—— 借鉴明日方舟"坍缩范式 / 环境"思路：
    /// 改写本局规则的、明牌的、挂在地图上的变量（不是纯数值、不是角色属性）。
    ///
    /// 结构（混合）：入秘境时随机 1 个基础异象（含"无异象"留白）；
    /// 深入到第 3 层及以后，有概率再叠加一个不同异象（坍缩式·越贪越乱，最多 3 个）。
    ///
    /// 各异象的效果通过本系统的查询属性暴露，由对应系统读取：
    ///   敌人数量 / 敌人伤害 / 洞府掉率 / 击杀收益 / 历练倍率 / 心魔速率 / 精英复活 / 落雷。
    /// </summary>
    public class RealmAnomalySystem : MonoBehaviour
    {
        private static RealmAnomalySystem _instance;
        public static RealmAnomalySystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RealmAnomalySystem");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<RealmAnomalySystem>();
                }
                return _instance;
            }
        }
        public static bool HasInstance => _instance != null;

        private readonly List<RealmAnomaly> _active = new();
        public IReadOnlyList<RealmAnomaly> Active => _active;

        private bool _runActive;
        private int _lastLevel;
        private Coroutine _hazardCo;

        private const int MaxAnomalies = 3;

        private void OnEnable()
        {
            GameEvents.Subscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Subscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Subscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            GameEvents.Unsubscribe<GameEvents.RealmBreakthrough>(OnRealmBreakthrough);
            GameEvents.Unsubscribe<GameEvents.RoomCleared>(OnRoomCleared);
            GameEvents.Unsubscribe<GameEvents.EnemyKilled>(OnEnemyKilled);
        }

        // ==================== 生命周期 ====================

        /// <summary>入秘境：清空并随机一个基础异象（约 35% 留白）。在 GameManager.StartNewRun 调用。</summary>
        public void RollForNewRun()
        {
            _active.Clear();
            _lastLevel = 0;
            _runActive = true;

            var pick = RollOne();
            if (pick != RealmAnomaly.None) _active.Add(pick);

            RestartHazard();
            AnnounceCurrent();
        }

        /// <summary>撤离 / 陨落：结束本局异象（停落雷）。</summary>
        public void EndRun()
        {
            _runActive = false;
            _active.Clear();
            StopHazard();
        }

        private void OnRealmBreakthrough(GameEvents.RealmBreakthrough evt)
        {
            if (!_runActive) return;
            if (evt.NewRealmLevel <= _lastLevel) return;  // 同层重复 spawn 不触发
            _lastLevel = evt.NewRealmLevel;

            // 坍缩式：深入第 3 层（index≥2）及以后，45% 叠加一个新异象（最多 3）
            if (evt.NewRealmLevel >= 2 && _active.Count < MaxAnomalies && Random.value < 0.45f)
            {
                var add = RollDistinct();
                if (add != RealmAnomaly.None)
                {
                    _active.Add(add);
                    RestartHazard();
                    var info = Info(add);
                    GameEvents.Publish(new GameEvents.RealmAnomalyAnnounced
                    {
                        Title = $"{info.icon} 秘境异变：{info.name}",
                        Desc = info.desc, IsAddition = true
                    });
                    Debug.Log($"<color=#c8a0ff>[秘境异象] 越深越乱 → 叠加「{info.name}」</color>");
                }
            }

            // 心魔滋生 → 每过一层侵蚀道心 -5
            if (DemonErodeDaoxin)
            {
                PlayerStateHooks.Instance.ChangeDaoxin(-5);
                Debug.Log("<color=#c060c0>[秘境异象] 心魔滋生 → 道心 -5（魔气侵蚀）</color>");
            }
        }

        // ==================== 联动钩子（v0.6） ====================

        private void OnRoomCleared(GameEvents.RoomCleared evt)
        {
            if (!_runActive || !KarmaEchoActive) return;
            var hooks = PlayerStateHooks.Instance;
            var p = PlayerController.Instance;
            if (p == null) return;

            if (hooks.KarmaDebt > 0)
            {
                float dmg = p.Stats.maxHp * hooks.KarmaDebt * 0.02f;
                p.OnDamage(dmg, p.transform.position, null);
                Debug.Log($"<color=#d0a080>[因果轮回] 因果反噬 {dmg:F0} 伤害（债={hooks.KarmaDebt}）</color>");
            }
            else if (hooks.KarmaDebt < 0)
            {
                float heal = p.Stats.maxHp * Mathf.Abs(hooks.KarmaDebt) * 0.015f;
                p.Stats.currentHp = Mathf.Min(p.Stats.currentHp + heal, p.Stats.maxHp);
                GameEvents.Publish(new GameEvents.HealthChanged
                {
                    CurrentHp = p.Stats.currentHp,
                    MaxHp = p.Stats.maxHp
                });
                Debug.Log($"<color=#80d080>[因果轮回] 善缘治愈 {heal:F0}（善缘={hooks.KarmaDebt}）</color>");
            }
        }

        private void OnEnemyKilled(GameEvents.EnemyKilled evt)
        {
            if (!_runActive) return;

            // 血月 → 击杀积因果
            if (BloodMoonKarma)
                PlayerStateHooks.Instance.ChangeKarma(1);
        }

        // ==================== 随机 ====================

        private static RealmAnomaly RollOne()
        {
            // 35% 留白；其余 5 种均分
            if (Random.value < 0.35f) return RealmAnomaly.None;
            return RandomNonNone();
        }

        private RealmAnomaly RollDistinct()
        {
            for (int tries = 0; tries < 8; tries++)
            {
                var a = RandomNonNone();
                if (!_active.Contains(a)) return a;
            }
            return RealmAnomaly.None;
        }

        private static RealmAnomaly RandomNonNone()
        {
            return (RealmAnomaly)Random.Range(1, System.Enum.GetValues(typeof(RealmAnomaly)).Length);
        }

        public bool Has(RealmAnomaly a) => _active.Contains(a);

        // ==================== 效果查询（各系统读取） ====================

        /// <summary>每房敌人数量倍率（灵潮汹涌 ×1.5）。</summary>
        public float EnemyCountMul => Has(RealmAnomaly.LingChao) ? 1.5f : 1f;
        /// <summary>敌人伤害倍率（血月 ×1.25，机缘频现 ×1.15，可叠加）。</summary>
        public float EnemyDamageMul => EnemyDamageMulTotal;
        /// <summary>洞府素材/灵脉额外掉率（灵潮汹涌 +25%）。</summary>
        public float CaveDropBonus => Has(RealmAnomaly.LingChao) ? 0.25f : 0f;
        /// <summary>灵脉道具掉落概率倍率（灵潮汹涌 ×2）。</summary>
        public float SpiritVeinDropMul => Has(RealmAnomaly.LingChao) ? 2f : 1f;
        /// <summary>击杀收益（灵力碎片）倍率（血月 ×2）。</summary>
        public float KillRewardMul => Has(RealmAnomaly.BloodMoon) ? 2f : 1f;
        /// <summary>历练值获取倍率（心魔滋生 ×1.5）。</summary>
        public float TemperingMul => Has(RealmAnomaly.DemonGrowth) ? 1.5f : 1f;
        /// <summary>心魔值积累速率倍率（心魔滋生 ×2）。</summary>
        public float InnerDemonRateMul => Has(RealmAnomaly.DemonGrowth) ? 2f : 1f;
        /// <summary>精英首次死亡满血复活一次（万灵复苏）。</summary>
        public bool EliteReviveOnce => Has(RealmAnomaly.Revival);
        /// <summary>全程常驻落雷（雷泽）。</summary>
        public bool LightningActive => Has(RealmAnomaly.LeiZe);

        // ── 新联动异象查询（v0.6） ──

        /// <summary>道心变动倍率（道心试炼 ×2）。</summary>
        public float DaoxinDeltaMul => Has(RealmAnomaly.DaoHeartTrial) ? 2f : 1f;
        /// <summary>道心试炼：入定(≥80)额外攻击+10%。</summary>
        public float DaoTrialAtkBonus => Has(RealmAnomaly.DaoHeartTrial) ? 0.10f : 0f;
        /// <summary>道心试炼：入魔(&lt;20)额外减伤-15%。</summary>
        public float DaoTrialDmgRedPenalty => Has(RealmAnomaly.DaoHeartTrial) ? -0.15f : 0f;
        /// <summary>因果轮回激活。</summary>
        public bool KarmaEchoActive => Has(RealmAnomaly.KarmaEcho);
        /// <summary>机缘频现：机缘触发率倍率（×2）。</summary>
        public float OpportunityMul => Has(RealmAnomaly.OpportunityRush) ? 2f : 1f;
        /// <summary>悟性/灵力获取倍率（机缘频现 ×1.2）。</summary>
        public float InsightGainMul => Has(RealmAnomaly.OpportunityRush) ? 1.2f : 1f;
        /// <summary>敌人伤害倍率（组合：血月 + 机缘频现）。</summary>
        public float EnemyDamageMulTotal
        {
            get
            {
                float mul = 1f;
                if (Has(RealmAnomaly.BloodMoon)) mul *= 1.25f;
                if (Has(RealmAnomaly.OpportunityRush)) mul *= 1.15f;
                return mul;
            }
        }
        /// <summary>血月：击杀积因果。</summary>
        public bool BloodMoonKarma => Has(RealmAnomaly.BloodMoon);
        /// <summary>心魔滋生：每层侵蚀道心。</summary>
        public bool DemonErodeDaoxin => Has(RealmAnomaly.DemonGrowth);

        // ==================== 雷泽：常驻落雷驱动 ====================

        private const float BoltRadius = 3.2f;
        private const float BoltTelegraph = 1.2f;
        private const float BoltInterval = 3.5f;
        private const float BoltDamagePercent = 0.12f;

        private void RestartHazard()
        {
            StopHazard();
            if (_runActive && LightningActive)
                _hazardCo = StartCoroutine(LightningLoop());
        }

        private void StopHazard()
        {
            if (_hazardCo != null) { StopCoroutine(_hazardCo); _hazardCo = null; }
        }

        private IEnumerator LightningLoop()
        {
            while (_runActive && LightningActive)
            {
                yield return new WaitForSeconds(BoltInterval);
                if (!_runActive || !LightningActive) yield break;

                var p = PlayerController.Instance;
                if (p == null) continue;
                // 仅在有敌人时落雷（避免休息/商店房无意义挨雷）
                if (!AnyEnemyAlive()) continue;

                Vector3 jitter = new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-2.5f, 2.5f));
                StartCoroutine(SpawnBolt(p.transform.position + jitter));
            }
        }

        private static bool AnyEnemyAlive()
        {
            var go = GameObject.FindWithTag("Enemy");
            return go != null;
        }

        private IEnumerator SpawnBolt(Vector3 pos)
        {
            FxFactory.SpawnAOERing(pos + Vector3.up * 0.05f, BoltRadius,
                new Color(0.7f, 0.8f, 1f, 1f), lifetime: BoltTelegraph);

            yield return new WaitForSeconds(BoltTelegraph);
            if (!_runActive) yield break;

            FxFactory.SpawnElementBurst(pos, ElementTag.Thunder, BoltRadius * 0.7f);
            FxFactory.SpawnAOERing(pos + Vector3.up * 0.05f, BoltRadius * 1.1f,
                new Color(1f, 1f, 0.5f, 1f), lifetime: 0.4f);

            var p = PlayerController.Instance;
            if (p == null) yield break;
            if (Vector3.Distance(p.transform.position, pos) <= BoltRadius)
            {
                p.OnDamage(p.Stats.maxHp * BoltDamagePercent, pos, gameObject);
                CameraShake.TriggerMedium();
            }
        }

        // ==================== 元数据（名称 / 图标 / 描述 / 颜色） ====================

        public struct AnomalyInfo { public string name; public string icon; public string desc; public Color color; }

        public static AnomalyInfo Info(RealmAnomaly a)
        {
            switch (a)
            {
                case RealmAnomaly.LingChao:
                    return new AnomalyInfo { name = "灵潮汹涌", icon = "🌊", color = new Color(0.4f, 0.85f, 0.9f),
                        desc = "地脉灵气暴涨——洞府素材与灵脉掉落大增，但每处秘境涌出更多敌人。" };
                case RealmAnomaly.LeiZe:
                    return new AnomalyInfo { name = "雷泽", icon = "⚡", color = new Color(0.65f, 0.75f, 1f),
                        desc = "秘境天雷不息——全程随机落雷，需时刻走位躲避。" };
                case RealmAnomaly.BloodMoon:
                    return new AnomalyInfo { name = "血月", icon = "🩸", color = new Color(1f, 0.4f, 0.4f),
                        desc = "血月当空，凶气滔天——敌人攻击大增，击杀所得翻倍，但每次杀生积一分因果。" };
                case RealmAnomaly.DemonGrowth:
                    return new AnomalyInfo { name = "心魔滋生", icon = "😈", color = new Color(0.8f, 0.4f, 0.8f),
                        desc = "此地魔气缠身——心魔积累翻倍、历练大增，但每过一层道心被侵蚀。" };
                case RealmAnomaly.Revival:
                    return new AnomalyInfo { name = "万灵复苏", icon = "♻", color = new Color(0.6f, 0.9f, 0.6f),
                        desc = "生死颠倒之地——精英妖物首次陨落会满血复活一次。" };
                case RealmAnomaly.DaoHeartTrial:
                    return new AnomalyInfo { name = "道心试炼", icon = "☯", color = new Color(0.7f, 0.85f, 1f),
                        desc = "此地道韵浓厚——道心变动幅度翻倍。入定额外加攻，入魔额外削减抗性，修心者得利、堕者速亡。" };
                case RealmAnomaly.KarmaEcho:
                    return new AnomalyInfo { name = "因果轮回", icon = "⚖", color = new Color(0.9f, 0.8f, 0.5f),
                        desc = "因果法则显化——每过一层，业障之人受因果反噬（伤害），善缘之人获治愈回馈。" };
                case RealmAnomaly.OpportunityRush:
                    return new AnomalyInfo { name = "机缘频现", icon = "✨", color = new Color(1f, 0.9f, 0.5f),
                        desc = "秘境灵机涌动——机缘触发率翻倍、灵力获取+20%，但秘境之敌亦受激励而攻势更猛。" };
                default:
                    return new AnomalyInfo { name = "寂灭之地", icon = "⛰", color = new Color(0.7f, 0.7f, 0.75f),
                        desc = "秘境一片死寂，并无异象。" };
            }
        }

        private void AnnounceCurrent()
        {
            if (_active.Count == 0)
            {
                var none = Info(RealmAnomaly.None);
                GameEvents.Publish(new GameEvents.RealmAnomalyAnnounced { Title = $"{none.icon} {none.name}", Desc = none.desc, IsAddition = false });
                Debug.Log("<color=#b0b0c0>[秘境异象] 本次：寂灭之地（无异象）</color>");
                return;
            }
            var info = Info(_active[0]);
            GameEvents.Publish(new GameEvents.RealmAnomalyAnnounced { Title = $"{info.icon} 秘境异象：{info.name}", Desc = info.desc, IsAddition = false });
            Debug.Log($"<color=#c8a0ff>[秘境异象] 本次：{info.name} —— {info.desc}</color>");
        }

        // ==================== 调试 ====================

        /// <summary>调试：强制设定唯一异象。</summary>
        public void DebugSet(RealmAnomaly a)
        {
            _active.Clear();
            _runActive = true;
            _lastLevel = 0;
            if (a != RealmAnomaly.None) _active.Add(a);
            RestartHazard();
            AnnounceCurrent();
        }
    }
}
