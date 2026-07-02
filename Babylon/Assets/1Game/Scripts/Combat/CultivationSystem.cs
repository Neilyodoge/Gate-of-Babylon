using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 角色等级系统 —— 历练 → 进阶经验 → 角色等级（纵向成长轴）。
    ///
    /// 与经验（<see cref="InsightSystem"/>）并列、互不重叠：
    /// - 历练：秘境中击杀 / 探索积累；撤离 100% 转入进阶经验；陨落归零
    /// - 进阶经验：累积到阈值可「晋级」→ 提升角色等级
    /// - 角色等级：决定"能走多深 / 根基多稳"（等级差压制等）
    /// - 品质（粗糙~完美）：由晋级表现决定，决定该阶等级增益强度
    ///
    /// 已晋级等级 + 进阶经验 + 品质终身保留；死亡只丢本局未撤离的历练。
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

        /// <summary>角色等级阶名（与秘境层"环境等级"同名同序）。</summary>
        public static readonly string[] RealmNames = { "一阶", "二阶", "三阶", "四阶", "五阶", "六阶" };
        public const int MaxRealm = 5;

        /// <summary>等级品质名：index 对应 SaveData.realmQualities 的值。</summary>
        public static readonly string[] QualityNames = { "粗糙", "普通", "精良", "完美" };

        /// <summary>品质 → 等级增益系数（粗糙 0.7 / 普通 1.0 / 精良 1.3 / 完美 1.6）。</summary>
        public static readonly float[] QualityFactor = { 0.7f, 1.0f, 1.3f, 1.6f };

        /// <summary>晋级阈值（累积进阶经验）：一阶→二阶 … 五阶→六阶，共 5 个，递增。</summary>
        private static readonly int[] BreakthroughCost = { 100, 250, 500, 900, 1500 };

        /// <summary>每次等级晋升（里程碑）额外发放的「经验」（局外成长树唯一货币）。</summary>
        private const int LingliPerBreakthrough = 60;

        // ========== 局内历练 ==========

        /// <summary>本局累积的历练（撤离转进阶经验，陨落归零）。</summary>
        public int RunTempering { get; private set; }

        // ========== 持久查询 ==========

        private SaveDataV1 Data => SaveSystem.Instance.Data;

        public int CurrentRealm => Mathf.Clamp(Data.cultivationRealm, 0, MaxRealm);
        public int CurrentExp => Data.cultivationExp;
        public string CurrentRealmName => RealmNames[CurrentRealm];
        public bool IsMaxRealm => CurrentRealm >= MaxRealm;

        /// <summary>未分配的历练存量（撤离带回，分配给进阶经验）。</summary>
        public int TemperingPool => Data.temperingPool;

        /// <summary>当前「精通点」余额（等级晋升发放，用于系精通加点）。</summary>
        public int MasteryPoints => Data.masteryPoints;

        /// <summary>下一次晋级所需进阶经验；已满级返回 -1。</summary>
        public int NextBreakthroughCost => IsMaxRealm ? -1 : BreakthroughCost[CurrentRealm];

        /// <summary>进阶经验是否已攒够、可晋升下一等级。</summary>
        public bool CanBreakthrough => !IsMaxRealm && CurrentExp >= NextBreakthroughCost;

        /// <summary>取某一阶等级的品质（0~3）；未达到该阶返回 -1。</summary>
        public int GetRealmQuality(int realm)
        {
            var q = Data.realmQualities;
            return (realm >= 0 && realm < q.Count) ? q[realm] : -1;
        }

        /// <summary>当前等级的增益系数（结合品质）。</summary>
        public float CurrentRealmFactor
        {
            get
            {
                int quality = GetRealmQuality(CurrentRealm);
                if (quality < 0 || quality >= QualityFactor.Length) return 1f;
                return QualityFactor[quality];
            }
        }

        // ========== 加历练 ==========

        /// <summary>击杀 / 探索加历练。</summary>
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

        /// <summary>撤离提交历练（可带层深倍率）。返回提交前的原始 RunTempering。</summary>
        public int CommitOnExtract(float multiplier = 1f)
        {
            if (RunTempering <= 0) return 0;
            int raw = RunTempering;
            int final = Mathf.RoundToInt(RunTempering * multiplier);
            Data.temperingPool += final;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd47a>[Cultivation] 撤离 · {final} 历练入存量（×{multiplier:F2}）（当前存量 {Data.temperingPool}）</color>");
            RunTempering = 0;
            return raw;
        }

        /// <summary>直接给历练存量（奖励用）。</summary>
        public void GrantPool(int amount)
        {
            if (amount <= 0) return;
            Data.temperingPool += amount;
            SaveSystem.Instance.Save();
        }

        /// <summary>从历练存量直接扣除（供其他 sink 用）。返回实际扣除量。</summary>
        public int SpendPool(int amount)
        {
            int n = Mathf.Clamp(amount, 0, Data.temperingPool);
            if (n <= 0) return 0;
            Data.temperingPool -= n;
            SaveSystem.Instance.Save();
            return n;
        }

        /// <summary>修炼：消耗历练存量 → 进阶经验（朝晋级累积）。返回实际消耗量。</summary>
        public int CultivateToExp(int amount)
        {
            int n = Mathf.Clamp(amount, 0, Data.temperingPool);
            if (n <= 0) return 0;
            Data.temperingPool -= n;
            Data.cultivationExp += n;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffd47a>[Cultivation] 修炼 · 历练 {n} → 进阶经验（{Data.cultivationExp}/{NextBreakthroughCost}）</color>");
            return n;
        }

        /// <summary>
        /// 死亡：角色等级由"累积成长里程碑"驱动、**终身保留**（累积只增，不身死归零）。
        /// 死亡只丢失"本局未撤离的收益"（局内历练 RunTempering）；已晋级等级 / 精通点 / 系精通 / 已银行历练存量 均保留。
        /// </summary>
        public void ReincarnateOnDeath()
        {
            Data.reincarnationCount++;
            RunTempering = 0;   // 本局未撤离的历练散尽
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ff8866>[Cultivation] 陨落 · 本局历练散尽；角色等级 {CurrentRealmName} 犹存（第 {Data.reincarnationCount} 世）</color>");
        }

        /// <summary>里程碑发经验：对尚未发放的等级阶（realmMilestonesGranted &lt; 当前等级）补发经验，避免重练重领。</summary>
        private void GrantBreakthroughPoints()
        {
            int granted = 0;
            while (Data.realmMilestonesGranted < Data.cultivationRealm)
            {
                Data.realmMilestonesGranted++;
                Data.accumulatedInsight += LingliPerBreakthrough;   // 经验 = 局外成长唯一货币
                granted += LingliPerBreakthrough;
            }
            if (granted > 0)
                Debug.Log($"<color=#dfcfff>[Cultivation] 等级里程碑 · 发放经验 +{granted}（当前 {Data.accumulatedInsight}）</color>");
        }

        // ========== 晋级 / 精炼 ==========

        /// <summary>
        /// 晋级成功后调用：提升角色等级并记录品质（quality 0~3）。
        /// 返回是否成功（进阶经验不足 / 已满级则失败）。
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

            Debug.Log($"<color=#ffe88a>[Cultivation] 晋级成功！角色等级 → {CurrentRealmName}（{QualityNames[quality]}）</color>");
            GameEvents.Publish(new GameEvents.CultivationBreakthrough
            {
                NewRealm = Data.cultivationRealm,
                RealmName = CurrentRealmName,
                Quality = quality
            });

            return true;
        }

        /// <summary>
        /// 精炼：消耗进阶经验打磨当前等级品质（粗糙→普通→精良；完美靠晋级表现，不可精炼）。
        /// 返回是否成功。
        /// </summary>
        public bool Refine()
        {
            int realm = CurrentRealm;
            int quality = GetRealmQuality(realm);
            if (quality < 0) return false;            // 当前阶尚无记录（理论不会，一阶为起点）
            if (quality >= 2) return false;           // 精良封顶，完美不可精炼
            int cost = RefineCost(realm, quality);
            if (Data.cultivationExp < cost) return false;

            Data.cultivationExp -= cost;
            SetRealmQuality(realm, quality + 1);
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#ffe88a>[Cultivation] 精炼 · {CurrentRealmName} 品质 → {QualityNames[quality + 1]}（耗进阶经验 {cost}）</color>");
            return true;
        }

        /// <summary>精炼消耗：随等级阶 + 目标品质递增。</summary>
        public int RefineCost(int realm, int currentQuality) => (realm + 1) * 80 * (currentQuality + 1);

        // ========== helpers ==========

        private void SetRealmQuality(int realm, int quality)
        {
            var q = Data.realmQualities;
            while (q.Count <= realm) q.Add(1); // 缺省补"普通"
            q[realm] = quality;
        }

        /// <summary>
        /// 环境等级 envRealm（秘境层品级 0~5）相对角色等级的"压制差"。
        /// 返回 (角色等级 - 环境等级)：≥0 契合 / 碾压；&lt;0 越级压制（越小越险）。
        /// </summary>
        public int SuppressionDelta(int envRealm) => CurrentRealm - envRealm;
    }
}
