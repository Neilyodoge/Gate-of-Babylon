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
        /// <summary>存档格式版本号，未来 schema 变更时支持迁移（v2：阶段C 系精通/突破发点）</summary>
        public int schemaVersion = 2;

        /// <summary>洞府素材库存：itemName → 数量（用 itemName 当 id，因为 ItemData 是 SO 无 GUID）</summary>
        public List<ItemCountEntry> caveInventory = new();

        /// <summary>洞府灵气（跨局持久化的加速 / 残魂补偿资源）</summary>
        public int caveQi = 0;

        /// <summary>已永久解锁的化身天赋 id 列表（对接 RealmReward 的 talent_xxx id）</summary>
        public List<string> unlockedTalentIds = new();

        /// <summary>已永久解锁的功法 id 列表（从藏经阁拼合古籍残页获得）</summary>
        public List<string> unlockedSkillIds = new();

        /// <summary>已孵化的灵兽 id 列表</summary>
        public List<string> unlockedBeastIds = new();

        /// <summary>累积经验（消耗以解锁天赋节点）</summary>
        public int accumulatedInsight = 0;

        /// <summary>最后存档时间戳（Unix seconds），用于 UI 显示</summary>
        public long lastSaveTimestamp = 0;

        /// <summary>[v0.6 废弃] 道伤已移除；字段保留仅为旧存档兼容，不再读写。</summary>
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

        // ========== V0.2.2 遗产系统 ==========

        /// <summary>
        /// 上局选定的遗产模块名称（moduleName）。下一局首战斗房自动掉落此模块作为起手补偿。
        /// 撤离/死亡时由玩家从背包中选择 1 个模块留存。空串=无遗产。
        /// </summary>
        public string lastRunLegacyModuleId = "";

        // ========== 角色等级（历练 → 进阶经验 → 等级 · 终身保留） ==========

        /// <summary>角色等级阶：0~5（对应一阶~六阶）。</summary>
        public int cultivationRealm = 0;

        /// <summary>当前累积进阶经验（朝下一次晋级累积）。</summary>
        public int cultivationExp = 0;

        /// <summary>各阶等级品质：index = 等级阶，值 0=粗糙 1=普通 2=精良 3=完美。</summary>
        public List<int> realmQualities = new();

        /// <summary>转世次数（统计用）。跨转世累加，不归零。</summary>
        public int reincarnationCount = 0;

        /// <summary>未分配的历练存量（撤离带回，分配给进阶经验）。</summary>
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

        // ========== 系精通 + 等级晋升发点 ==========
        // 说明：角色等级由累积经验驱动、累积只增；
        // 故等级里程碑发放的点数 + 已点系精通 + 已解锁天赋 = 终身成长。

        /// <summary>已发放的等级里程碑数（防止重复发点）。</summary>
        public int realmMilestonesGranted = 0;

        /// <summary>未花费的「精通点」余额（等级里程碑发放，用于点系精通节点）。终身保留。</summary>
        public int masteryPoints = 0;

        /// <summary>未花费的「天赋点」余额（等级里程碑发放，用于解锁天赋树节点）。终身保留。</summary>
        public int talentPoints = 0;

        /// <summary>已点亮的系精通节点 id 列表（化身×系节点，二值解锁）。洞府家业，终身保留。</summary>
        public List<string> masteryNodeIds = new();

        // ========== GDD §9.1.7：天赋树渐进解锁 ==========
        /// <summary>
        /// 已解锁的成长树分支标签列表（格式 "avatar_branchLabel"，如 "metal_锐金·破阵"）。
        /// 初始：每化身第一条分支免费解锁；更多分支通过机缘/成就/剧情解锁。
        /// </summary>
        public List<string> unlockedGrowthBranches = new();
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
