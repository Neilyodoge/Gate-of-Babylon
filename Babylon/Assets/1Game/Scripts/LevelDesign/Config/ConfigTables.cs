using System;
using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    // ============================================================
    // GDD §12 表格数据结构定义 —— 与 JSON 文件一一对应
    // 所有表格放在 Resources/LevelDesign/ 下，文件名同 typeof(T).Name
    // ============================================================

    /// <summary>GDD §12.1.4 房间类型（数字编码）</summary>
    public enum LevelRoomType
    {
        Start = 0,
        Battle = 1,
        Elite = 2,
        Shop = 3,
        Event = 4,
        Boss = 5
    }

    /// <summary>GDD §12.2.3 事件类型</summary>
    public enum StoryEventType
    {
        MultiChoice = 1,
        SingleChoice = 2,
        ConditionalEvent = 3
    }

    // ------------------------------------------------------------
    // §12.2.1 Map_Structure_Config — 区域路线结构
    // ------------------------------------------------------------
    [Serializable]
    public class MapStructureRow
    {
        public int ID;
        public int ActID;
        public int MaxFloor;
        public int MinNodes;
        public int MaxNodes;
        public int NormalWeight = 75;
        public int SpecialWeight = 25;
        public int EliteMinCount;
        public int EliteMaxCount;
        public int EventMinCount;
        public int ShopMinCount;
        /// <summary>引用 Room_Socket_Group_Config 的 ID 列表</summary>
        public int[] RoomPoolID;
    }

    // ------------------------------------------------------------
    // §12.2.2 Room_Socket_Group_Config — 房间内容配置
    // ------------------------------------------------------------
    [Serializable]
    public class RoomSocketRow
    {
        public int ID;
        public string SceneName;
        public int RoomType;
        public int[] EnemySquadID;
        public int[] ItemDropIDs;
        public int[] ItemDropWeights;
        public int EventID;
        public int EventTriggerRate;
        public int Weight = 100;

        public LevelRoomType TypeEnum => (LevelRoomType)RoomType;
    }

    // ------------------------------------------------------------
    // §12.2.3 Event_Story_Config — 事件配置
    // ------------------------------------------------------------
    [Serializable]
    public class EventOption
    {
        public string Text;
        public string FlagName;
        public int FlagValue;
        public int RewardID;
        public int CostID;
        public int KarmaChange;
        public int DaoxinChange;
        public int LifespanChange;
    }

    [Serializable]
    public class StoryEventRow
    {
        public int ID;
        public string Name_CN;
        public int Type;
        public string PrereqFlag;
        public string Text_CN;
        public EventOption[] Options;

        public StoryEventType TypeEnum => (StoryEventType)Type;
    }

    // ------------------------------------------------------------
    // §12.3.1 Boss_Phase_Config — Boss 形态配置
    // ------------------------------------------------------------
    [Serializable]
    public class BossPhaseRow
    {
        public int ID;
        public int BossID;
        public string PhaseName;
        /// <summary>格式：flagA=1&amp;flagB>=2，支持 AND 组合；空 = 无前置</summary>
        public string RequiredFlags;
        public int Priority;
        public string[] DialogueLines;
        public int SkillSetID;
        /// <summary>格式：hp*1.2,atk*1.5,spd*0.8</summary>
        public string StatModifier;
        public int SummonSquadID;
    }

    // ------------------------------------------------------------
    // §12.2.3 Item_InRun_Config — 局内道具
    // ------------------------------------------------------------
    [Serializable]
    public class ItemInRunRow
    {
        public int ID;
        public string Name_CN;
        public string Text_CN;
        public int Type;
        public string Icon;
        public int MaxStack = 1;
        public int EffectID;
        public float Duration;
    }

    // ------------------------------------------------------------
    // §12.2.3 Material_CaveRes_Config — 洞府素材
    // ------------------------------------------------------------
    [Serializable]
    public class MaterialCaveResRow
    {
        public int ID;
        public string Name_CN;
        public string Text_CN;
        /// <summary>1=灵植种子 / 2=灵药 / 3=灵矿 / 4=妖兽材料 / 5=古籍残页 / 6=阵法符</summary>
        public int Type;
        public string Icon;
        public int MaxStack = 99;
    }

    // ------------------------------------------------------------
    // JSON 顶层包装器（Unity JsonUtility 不支持顶级数组）
    // ------------------------------------------------------------
    [Serializable] public class MapStructureTable { public MapStructureRow[] Rows; }
    [Serializable] public class RoomSocketTable { public RoomSocketRow[] Rows; }
    [Serializable] public class StoryEventTable { public StoryEventRow[] Rows; }
    [Serializable] public class BossPhaseTable { public BossPhaseRow[] Rows; }
    [Serializable] public class ItemInRunTable { public ItemInRunRow[] Rows; }
    [Serializable] public class MaterialCaveResTable { public MaterialCaveResRow[] Rows; }
}
