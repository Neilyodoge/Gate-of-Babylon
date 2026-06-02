using System;
using System.Collections.Generic;

namespace XianTu
{
    /// <summary>
    /// v0.5 存档数据 V1 —— 跨局持久化的全部内容。
    ///
    /// 设计原则：
    /// 1. 只存 id / 数量 / 时间戳 等"可序列化原始数据"，不存 ScriptableObject 引用
    /// 2. 加 schemaVersion 字段，后续 schema 变更时支持迁移
    /// 3. 使用 JsonUtility 序列化（Unity 内置，简单可靠）
    /// </summary>
    [Serializable]
    public class SaveDataV1
    {
        /// <summary>存档格式版本号，未来 schema 变更时支持迁移</summary>
        public int schemaVersion = 1;

        /// <summary>洞府素材库存：itemName → 数量（用 itemName 当 id，因为 ItemData 是 SO 无 GUID）</summary>
        public List<ItemCountEntry> caveInventory = new();

        /// <summary>洞府灵气（跨局持久化的加速 / 残魂补偿资源）</summary>
        public int caveQi = 0;

        /// <summary>已永久解锁的化身天赋 id 列表（对接 RealmReward 的 talent_xxx id）</summary>
        public List<string> unlockedTalentIds = new();

        /// <summary>已永久解锁的功法 id 列表（从藏经阁拼合古籍残页获得）</summary>
        public List<string> unlockedSkillIds = new();

        /// <summary>已永久解锁的灵物 id 列表（从炼器房炼制获得，进入梦境掉落池）</summary>
        public List<string> unlockedItemIds = new();

        /// <summary>累积悟性（消耗以解锁天赋节点）</summary>
        public int accumulatedInsight = 0;

        /// <summary>最后存档时间戳（Unix seconds），用于 UI 显示</summary>
        public long lastSaveTimestamp = 0;

        /// <summary>魂伤 debuff 剩余时长（游戏内秒）—— 死亡后这段时间内无法入梦</summary>
        public float soulHurtRemainingSec = 0f;

        /// <summary>累积通关次数（统计 / 解锁条件用）</summary>
        public int totalRunsCompleted = 0;
        /// <summary>累积死亡次数</summary>
        public int totalDeaths = 0;

        // ========== v0.5 Week 4：剩余洞府模块（阵法台 / 灵兽园 / 藏经阁起手） ==========

        /// <summary>
        /// 已布置但尚未消耗的阵法 buff id（来自阵法台 <c>FormationLibrary</c>）。
        /// 入梦时被 <see cref="GameManager.StartNewRun"/> 一次性应用并清空，仅作用于下一局。
        /// </summary>
        public string pendingFormationBuffId = "";

        /// <summary>当前活跃的灵兽伙伴 id（来自灵兽园 <c>SpiritBeastLibrary</c>）。入梦时 spawn 跟随玩家。</summary>
        public string activeSpiritBeastId = "";

        /// <summary>下一次入秘境时希望起手装备的功法 id（来自藏经阁解锁库）。入秘境时装入 0 号槽位。</summary>
        public string pendingStartSkillId = "";

        // ========== v0.5.4：本体境界（历练值 → 修为 → 境界 · 身死道消转世归零） ==========

        /// <summary>本体境界阶：0=炼气 1=筑基 2=金丹 3=元婴 4=化神 5=渡劫。陨落转世归零。</summary>
        public int cultivationRealm = 0;

        /// <summary>当前累积修为（朝下一次突破累积）。陨落转世归零。</summary>
        public int cultivationExp = 0;

        /// <summary>各阶境界成色：index = 境界阶，值 0=瑕品 1=凡品 2=上品 3=完美。陨落转世归零。</summary>
        public List<int> realmQualities = new();

        /// <summary>转世次数（轮回流 · 宿慧加成用）。跨转世累加，不归零。</summary>
        public int reincarnationCount = 0;

        /// <summary>未分配的历练值存量（撤离带回，在洞府分配给修为 or 灵脉）。陨落转世归零（属"你的道"）。</summary>
        public int temperingPool = 0;

        /// <summary>灵脉经验（决定灵脉等级）。属洞府家业，**陨落转世保留**。</summary>
        public int spiritVeinExp = 0;

        // ========== v0.5.5：洞府机缘 · 链式状态 ==========

        /// <summary>累计撤离回府次数（链式机缘的计时基准）。属洞府家业，转世保留。</summary>
        public int caveReturnCount = 0;

        /// <summary>已触发机缘留下的标记（用于条件分支 / 防重复触发）。</summary>
        public List<string> opportunityFlags = new();

        /// <summary>待回访的链式机缘：到达 dueAtReturn 次回府时优先触发其回访事件。</summary>
        public List<OpportunityChainEntry> pendingOpportunities = new();
    }

    /// <summary>链式机缘的"待回访"条目：某个机缘选择会埋下一个 N 局后回访的后续事件。</summary>
    [Serializable]
    public struct OpportunityChainEntry
    {
        /// <summary>要触发的回访事件 id（对应 CaveOpportunitySystem 的 followup 池）。</summary>
        public string opportunityId;
        /// <summary>到达第几次回府时触发（caveReturnCount >= dueAtReturn 即到期）。</summary>
        public int dueAtReturn;
    }

    /// <summary>洞府素材 / 灵物库存条目（itemName → count）</summary>
    [Serializable]
    public struct ItemCountEntry
    {
        public string itemName;
        public int count;
    }
}
