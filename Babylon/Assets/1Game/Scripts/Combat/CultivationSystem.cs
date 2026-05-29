using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 本体境界系统（v0.5.4）—— 历练值 → 修为 → 本体境界（纵向成长轴）。
    ///
    /// 与悟性（<see cref="InsightSystem"/>）并列、互不重叠：
    /// - 历练值：秘境中击杀 / 探索 / 渡劫积累；撤离 100% 转入永久修为；陨落转世归零
    /// - 修为：累积到阈值可「冲击境界」→ 渡劫战（详见 GDD 6.8.3）→ 成功晋升本体境界
    /// - 本体境界：决定"能走多深 / 根基多稳"（境界压制 / 渡劫底力 / 道伤减免，详见 GDD 8.2 / 9.1.8）
    /// - 成色（瑕品~完美）：由渡劫战表现决定，决定该阶境界增益强度
    ///
    /// 死亡 = 身死道消（转世）：本体境界 + 修为 + 成色归零，洞府家业（天赋/功法/灵物池/灵脉/库存）保留。
    /// </summary>
    public class CultivationSystem : MonoBehaviour
    {
        private static CultivationSystem _instance;
        public static CultivationSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("CultivationSystem");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CultivationSystem>();
                }
                return _instance;
            }
        }

        /// <summary>本体境界阶名（与秘境层"环境境界"同名同序）。</summary>
        public static readonly string[] RealmNames = { "炼气", "筑基", "金丹", "元婴", "化神", "渡劫" };
        public const int MaxRealm = 5;

        /// <summary>境界成色名：index 对应 SaveData.realmQualities 的值。</summary>
        public static readonly string[] QualityNames = { "瑕品", "凡品", "上品", "完美" };

        /// <summary>成色 → 境界增益系数（瑕 0.7 / 凡 1.0 / 上 1.3 / 完美 1.6）。</summary>
        public static readonly float[] QualityFactor = { 0.7f, 1.0f, 1.3f, 1.6f };

        /// <summary>突破阈值（累积修为）：炼气→筑基 … 化神→渡劫，共 5 个，递增。</summary>
        private static readonly int[] BreakthroughCost = { 100, 250, 500, 900, 1500 };

        // ========== 局内历练值 ==========

        /// <summary>本局累积的历练值（撤离转永久修为，陨落归零）。</summary>
        public int RunTempering { get; private set; }

        // ========== 持久查询 ==========

        private SaveDataV1 Data => SaveSystem.Instance.Data;

        public int CurrentRealm => Mathf.Clamp(Data.cultivationRealm, 0, MaxRealm);
        public int CurrentExp => Data.cultivationExp;
        public string CurrentRealmName => RealmNames[CurrentRealm];
        public bool IsMaxRealm => CurrentRealm >= MaxRealm;

        /// <summary>下一次突破所需修为；已满级返回 -1。</summary>
        public int NextBreakthroughCost => IsMaxRealm ? -1 : BreakthroughCost[CurrentRealm];

        /// <summary>修为是否已攒够、可冲击下一境界。</summary>
        public bool CanBreakthrough => !IsMaxRealm && CurrentExp >= NextBreakthroughCost;

        /// <summary>取某一阶境界的成色（0~3）；未达到该阶返回 -1。</summary>
        public int GetRealmQuality(int realm)
        {
            var q = Data.realmQualities;
            return (realm >= 0 && realm < q.Count) ? q[realm] : -1;
        }

        /// <summary>当前境界的增益系数（结合成色）。</summary>
        public float CurrentRealmFactor
        {
            get
            {
                int quality = GetRealmQuality(CurrentRealm);
                if (quality < 0 || quality >= QualityFactor.Length) return 1f;
                return QualityFactor[quality];
            }
        }

        // ========== 加历练值 ==========

        /// <summary>击杀 / 探索 / 渡劫加历练值（灵脉浓郁可加成，留待接入）。</summary>
        public void AddRunTempering(int amount, string reason)
        {
            if (amount <= 0) return;
            RunTempering += amount;
            GameEvents.Publish(new GameEvents.TemperingChanged
            {
                NewRunTempering = RunTempering,
                Delta = amount,
                Reason = reason
            });
        }

        // ========== 局结束 ==========

        /// <summary>撤离成功：本局历练值 100% 转入永久修为。</summary>
        public void CommitOnExtract()
        {
            if (RunTempering <= 0) return;
            Data.cultivationExp += RunTempering;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd47a>[Cultivation] 撤离 · {RunTempering} 历练值转入修为（当前修为 {Data.cultivationExp}）</color>");
            RunTempering = 0;
        }

        /// <summary>陨落 = 身死道消 · 转世：本体境界 / 修为 / 成色归零，洞府家业保留。</summary>
        public void ReincarnateOnDeath()
        {
            bool hadProgress = Data.cultivationRealm > 0 || Data.cultivationExp > 0 || RunTempering > 0;
            Data.cultivationRealm = 0;
            Data.cultivationExp = 0;
            Data.realmQualities.Clear();
            Data.reincarnationCount++;
            RunTempering = 0;
            SaveSystem.Instance.Save();
            if (hadProgress)
                Debug.Log($"<color=#ff8866>[Cultivation] 身死道消 · 本体境界尽散，转世重修（第 {Data.reincarnationCount} 世，洞府家业犹存）</color>");
        }

        // ========== 突破 / 凝实 ==========

        /// <summary>
        /// 渡劫战胜利后调用：晋升本体境界并记录成色（quality 0~3）。
        /// 返回是否成功（修为不足 / 已满级则失败）。
        /// </summary>
        public bool Breakthrough(int quality)
        {
            if (IsMaxRealm) return false;
            if (CurrentExp < NextBreakthroughCost) return false;

            Data.cultivationExp -= NextBreakthroughCost;
            Data.cultivationRealm++;
            quality = Mathf.Clamp(quality, 0, QualityNames.Length - 1);
            SetRealmQuality(Data.cultivationRealm, quality);
            SaveSystem.Instance.Save();

            Debug.Log($"<color=#ffe88a>[Cultivation] 突破成功！本体境界 → {CurrentRealmName}（{QualityNames[quality]}）</color>");
            GameEvents.Publish(new GameEvents.CultivationBreakthrough
            {
                NewRealm = Data.cultivationRealm,
                RealmName = CurrentRealmName,
                Quality = quality
            });
            return true;
        }

        /// <summary>
        /// 凝实：消耗修为打磨当前境界成色（瑕品→凡品→上品；完美靠渡劫表现，不可凝实）。
        /// 返回是否成功。
        /// </summary>
        public bool Refine()
        {
            int realm = CurrentRealm;
            int quality = GetRealmQuality(realm);
            if (quality < 0) return false;            // 当前阶尚无记录（理论不会，炼气为起点）
            if (quality >= 2) return false;           // 上品封顶，完美不可凝实
            int cost = RefineCost(realm, quality);
            if (Data.cultivationExp < cost) return false;

            Data.cultivationExp -= cost;
            SetRealmQuality(realm, quality + 1);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffe88a>[Cultivation] 凝实 · {CurrentRealmName} 成色 → {QualityNames[quality + 1]}（耗修为 {cost}）</color>");
            return true;
        }

        /// <summary>凝实消耗：随境界阶 + 目标成色递增。</summary>
        public int RefineCost(int realm, int currentQuality) => (realm + 1) * 80 * (currentQuality + 1);

        // ========== helpers ==========

        private void SetRealmQuality(int realm, int quality)
        {
            var q = Data.realmQualities;
            while (q.Count <= realm) q.Add(1); // 缺省补"凡品"
            q[realm] = quality;
        }

        /// <summary>
        /// 环境境界 envRealm（秘境层品级 0~5）相对本体境界的"压制差"。
        /// 返回 (本体境界 - 环境境界)：≥0 契合 / 碾压；&lt;0 越级压制（越小越险）。
        /// </summary>
        public int SuppressionDelta(int envRealm) => CurrentRealm - envRealm;
    }
}
