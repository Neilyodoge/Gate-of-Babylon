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

        /// <summary>下一次入梦时希望起手装备的功法 id（来自藏经阁解锁库）。入梦时由 RealmRewardController 装入 0 号槽位。</summary>
        public string pendingStartSkillId = "";
    }

    /// <summary>洞府素材 / 灵物库存条目（itemName → count）</summary>
    [Serializable]
    public struct ItemCountEntry
    {
        public string itemName;
        public int count;
    }
}
