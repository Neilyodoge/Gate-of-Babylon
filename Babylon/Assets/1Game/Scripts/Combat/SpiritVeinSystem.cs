using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灵脉系统（v0.5.4 · GDD 9.1.9）—— 洞府的"收益品质"纵线，与修为并列。
    ///
    /// 历练值的核心抉择：投【修为】（能走多深 / 根基多稳）vs 投【灵脉】（每趟捞得更好）。
    /// - 喂养：历练值存量注入（<see cref="InjectFromPool"/>）/ 秘境灵脉道具（<see cref="InjectExp"/>）/ 机缘事件
    /// - 作用（两维）：秘境掉落品质（<see cref="DropBonus"/>）+ 机缘品质上限（<see cref="MaxOpportunityTier"/>）
    ///   次要：洞府模块效率（<see cref="ModuleEfficiency"/>）
    ///
    /// 灵脉属【洞府家业】，陨落转世**保留**（不归零）。
    /// </summary>
    public class SpiritVeinSystem : MonoBehaviour
    {
        private static SpiritVeinSystem _instance;
        public static SpiritVeinSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SpiritVeinSystem");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SpiritVeinSystem>();
                }
                return _instance;
            }
        }

        /// <summary>灵脉等级名（index 0~4）。</summary>
        public static readonly string[] LevelNames = { "枯脉", "凡脉", "灵脉", "福地", "洞天" };
        public const int MaxLevel = 4;

        /// <summary>升到各等级所需的累积灵脉经验。</summary>
        private static readonly int[] LevelThreshold = { 0, 150, 400, 900, 1800 };

        private SaveDataV1 Data => SaveSystem.Instance.Data;

        public int Exp => Data.spiritVeinExp;

        /// <summary>当前灵脉等级（0=枯脉 … 4=洞天）。</summary>
        public int Level
        {
            get
            {
                int lv = 0;
                for (int i = 0; i <= MaxLevel; i++)
                    if (Data.spiritVeinExp >= LevelThreshold[i]) lv = i;
                return lv;
            }
        }

        public string LevelName => LevelNames[Mathf.Clamp(Level, 0, MaxLevel)];
        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>升下一级还需多少经验；已满级返回 -1。</summary>
        public int ExpToNextLevel => IsMaxLevel ? -1 : LevelThreshold[Level + 1] - Data.spiritVeinExp;

        /// <summary>下一级阈值跨度（用于进度条）。</summary>
        public int NextLevelSpan => IsMaxLevel ? 1 : LevelThreshold[Level + 1] - LevelThreshold[Level];

        public int ExpIntoCurrentLevel => Data.spiritVeinExp - LevelThreshold[Mathf.Clamp(Level, 0, MaxLevel)];

        // ========== 作用 ==========

        /// <summary>秘境洞府素材额外掉率加成：枯/凡 0 / 灵 +10% / 福地 +15% / 洞天 +20%。</summary>
        public float DropBonus => Level switch
        {
            2 => 0.10f,
            3 => 0.15f,
            4 => 0.20f,
            _ => 0f
        };

        /// <summary>机缘品质上限 tier（= 灵脉等级；机缘系统据此 gate 高级机缘）。</summary>
        public int MaxOpportunityTier => Level;

        /// <summary>洞府模块效率乘数（灵田生长 / 炼器 / 灵兽育成等可乘上此值）。</summary>
        public float ModuleEfficiency => Level switch
        {
            0 => 0.9f,
            1 => 1.0f,
            2 => 1.1f,
            3 => 1.25f,
            4 => 1.5f,
            _ => 1f
        };

        // ========== 注入 ==========

        /// <summary>直接加灵脉经验（秘境灵脉道具 / 机缘）。</summary>
        public void InjectExp(int amount, string reason)
        {
            if (amount <= 0) return;
            int beforeLv = Level;
            Data.spiritVeinExp += amount;
            SaveSystem.Instance.Save();
            Debug.Log($"<color=#9be0c0>[灵脉] +{amount} 经验（{reason}）→ {Exp}（{LevelName}）</color>");
            if (Level > beforeLv)
                Debug.Log($"<color=#9be0c0>★ 灵脉晋升 → {LevelName}！</color>");
        }

        /// <summary>从历练值存量注入灵脉（核心抉择：跟修为争夺同一资源）。返回实际注入量。</summary>
        public int InjectFromPool(int amount)
        {
            int n = Mathf.Clamp(amount, 0, CultivationSystem.Instance.TemperingPool);
            if (n <= 0) return 0;
            CultivationSystem.Instance.SpendPool(n);
            InjectExp(n, "历练值注入");
            return n;
        }
    }
}
