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

        /// <summary>每次境界突破（里程碑）额外发放的「灵力」（局外成长树唯一货币）。</summary>
        private const int LingliPerBreakthrough = 60;

        // ========== 局内历练值 ==========

        /// <summary>本局累积的历练值（撤离转永久修为，陨落归零）。</summary>
        public int RunTempering { get; private set; }

        // ========== 持久查询 ==========

        private SaveDataV1 Data => SaveSystem.Instance.Data;

        public int CurrentRealm => Mathf.Clamp(Data.cultivationRealm, 0, MaxRealm);
        public int CurrentExp => Data.cultivationExp;
        public string CurrentRealmName => RealmNames[CurrentRealm];
        public bool IsMaxRealm => CurrentRealm >= MaxRealm;

        /// <summary>未分配的历练值存量（撤离带回，在洞府分配给修为 or 灵脉）。</summary>
        public int TemperingPool => Data.temperingPool;

        /// <summary>当前「精通点」余额（境界突破发放，用于系精通加点）。</summary>
        public int MasteryPoints => Data.masteryPoints;

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

        /// <summary>撤离成功：本局历练值 100% 转入"历练值存量"，由玩家在洞府分配给修为 or 灵脉。</summary>
        /// <summary>撤离提交历练值（可带层深倍率）。返回提交前的原始 RunTempering。</summary>
        public int CommitOnExtract(float multiplier = 1f)
        {
            if (RunTempering <= 0) return 0;
            int raw = RunTempering;
            int final = Mathf.RoundToInt(RunTempering * multiplier);
            Data.temperingPool += final;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd47a>[Cultivation] 撤离 · {final} 历练值入存量（×{multiplier:F2}）（当前存量 {Data.temperingPool}）</color>");
            RunTempering = 0;
            return raw;
        }

        /// <summary>直接给历练值存量（机缘事件 / 奖励用）。</summary>
        public void GrantPool(int amount)
        {
            if (amount <= 0) return;
            Data.temperingPool += amount;
            SaveSystem.Instance.Save();
        }

        /// <summary>从历练值存量直接扣除（供灵脉注入等其他 sink 用）。返回实际扣除量。</summary>
        public int SpendPool(int amount)
        {
            int n = Mathf.Clamp(amount, 0, Data.temperingPool);
            if (n <= 0) return 0;
            Data.temperingPool -= n;
            SaveSystem.Instance.Save();
            return n;
        }

        /// <summary>闭关：消耗历练值存量 → 修为（朝突破累积）。返回实际消耗量。</summary>
        public int CultivateToExp(int amount)
        {
            int n = Mathf.Clamp(amount, 0, Data.temperingPool);
            if (n <= 0) return 0;
            Data.temperingPool -= n;
            Data.cultivationExp += n;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd47a>[Cultivation] 闭关 · 历练值 {n} → 修为（修为 {Data.cultivationExp}/{NextBreakthroughCost}）</color>");
            return n;
        }

        /// <summary>
        /// 死亡（v0.6 阶段C 重定位 · §7）：本体境界改由"累积成长里程碑"驱动、**终身保留**（累积只增，不再身死归零）。
        /// 死亡只丢失"本局未撤离的收益"（局内历练值 RunTempering）；已突破境界 / 精通点 / 系精通 / 已银行历练值存量 均保留。
        /// </summary>
        public void ReincarnateOnDeath()
        {
            Data.reincarnationCount++;
            RunTempering = 0;   // 本局未撤离的历练值散尽
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ff8866>[Cultivation] 陨落 · 本局历练散尽；本体境界 {CurrentRealmName} 与洞府家业犹存（第 {Data.reincarnationCount} 世）</color>");
        }

        /// <summary>里程碑发灵力：对尚未发放的境界阶（realmMilestonesGranted &lt; 当前境界）补发灵力，避免转世重练重领。</summary>
        private void GrantBreakthroughPoints()
        {
            int granted = 0;
            while (Data.realmMilestonesGranted < Data.cultivationRealm)
            {
                Data.realmMilestonesGranted++;
                Data.accumulatedInsight += LingliPerBreakthrough;   // 灵力 = 局外成长唯一货币
                granted += LingliPerBreakthrough;
            }
            if (granted > 0)
                Debug.Log($"<color=#dfcfff>[Cultivation] 境界里程碑 · 发放灵力 +{granted}（当前 {Data.accumulatedInsight}）</color>");
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
            GrantBreakthroughPoints();   // v0.6 阶段C：里程碑发精通点（防重领守卫）
            SaveSystem.Instance.Save();

            Debug.Log($"<color=#ffe88a>[Cultivation] 突破成功！本体境界 → {CurrentRealmName}（{QualityNames[quality]}）</color>");
            GameEvents.Publish(new GameEvents.CultivationBreakthrough
            {
                NewRealm = Data.cultivationRealm,
                RealmName = CurrentRealmName,
                Quality = quality
            });

            // v0.6：渡劫突破奖励"洞天残核"（灵脉经验 +200）
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                SpiritVeinPickup.Spawn("洞天残核", 200,
                    pc.transform.position + pc.transform.forward * 2f + Vector3.right * 1f);
            }

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
