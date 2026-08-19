using System;
using System.Collections.Generic;
using Edgar.Unity;
using UnityEngine;

namespace XianTu.LevelDesign
{
    [Serializable]
    public sealed class DungeonLayoutCandidate
    {
        [InspectorName("布局编号")]
        public string ID = "Layout_A";
        [InspectorName("显示名称")]
        public string DisplayName = "双区主轴";
        [InspectorName("Edgar 关卡图")]
        public LevelGraph LevelGraph;
        [InspectorName("抽取权重"), Min(0)]
        public int Weight = 100;
        [InspectorName("启用")]
        public bool Enabled = true;
        [InspectorName("默认起点节点")]
        public string StartNodeName = "O4";
        [InspectorName("默认首领节点")]
        public string BossNodeName = "I4";
        [InspectorName("反向起点节点")]
        public string AlternateStartNodeName = "I3";
        [InspectorName("反向首领节点")]
        public string AlternateBossNodeName = "O0";
        [InspectorName("路线事件节点")]
        public string LayoutEventNodeName = "O1";
        [InspectorName("战斗事件节点")]
        public string StrengthEventNodeName = "I1";
        [InspectorName("商店节点")]
        public string ShopNodeName = "C0";
        [InspectorName("精英节点")]
        public string[] EliteNodeNames = { "O3", "I2" };
        [InspectorName("地标节点")]
        public string[] LandmarkNodeNames = { "O0", "I4" };
        [InspectorName("可选分支入口节点")]
        public string OptionalBranchSourceNodeName = "O1";
        [InspectorName("可选分支目标节点")]
        public string OptionalBranchTargetNodeName = "B0";
        [InspectorName("沿用双区相对方位约束")]
        [Tooltip("仅旧版双区主轴需要。新 Flow 通常关闭，改由图节奏校验保证结构。")]
        public bool EnforceLegacyLandmarkRelationships = true;

        public string ResolveStartNode(bool alternate)
        {
            string configured = alternate ? AlternateStartNodeName : StartNodeName;
            return string.IsNullOrWhiteSpace(configured)
                ? (alternate ? StartNodeName : AlternateStartNodeName)
                : configured.Trim();
        }

        public string ResolveBossNode(bool alternate)
        {
            string configured = alternate ? AlternateBossNodeName : BossNodeName;
            return string.IsNullOrWhiteSpace(configured)
                ? (alternate ? BossNodeName : AlternateBossNodeName)
                : configured.Trim();
        }

        public bool IsEliteNode(string nodeName)
        {
            return ContainsNode(EliteNodeNames, nodeName);
        }

        public bool IsLandmarkNode(string nodeName)
        {
            return ContainsNode(LandmarkNodeNames, nodeName);
        }

        private static bool ContainsNode(IEnumerable<string> nodes, string nodeName)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(nodeName))
                return false;
            foreach (string candidate in nodes)
                if (string.Equals(candidate?.Trim(), nodeName, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }

    [Serializable]
    public sealed class DungeonRhythmValidationSettings
    {
        [InspectorName("首领最小路径深度（连接数）"), Min(1)]
        [Tooltip("从降落房到首领房的最短路线至少经过多少条房间连接。")]
        public int MinBossDepth = 5;
        [InspectorName("首领最大路径深度（连接数）"), Min(1)]
        [Tooltip("从降落房到首领房的最短路线最多经过多少条房间连接。")]
        public int MaxBossDepth = 10;
        [InspectorName("主路线连续战斗房上限"), Min(1)]
        [Tooltip("最短首领路线中，普通战斗、精英和首领房最多允许连续出现多少间。")]
        public int MaxConsecutiveCombatRooms = 3;
        [InspectorName("事件房数量下限"), Min(0)]
        public int MinEventRooms = 2;
        [InspectorName("事件房数量上限"), Min(0)]
        public int MaxEventRooms = 3;
        [InspectorName("地标房数量下限"), Min(0)]
        public int MinLandmarkRooms = 1;
        [InspectorName("无奖励死路数量上限"), Min(0)]
        [Tooltip("只有一个入口、且不含事件、精英、商店、首领、建筑或宝箱的房间数量上限。")]
        public int MaxUnrewardedDeadEnds;
        [InspectorName("捷径最少节省连接数"), Min(1)]
        [Tooltip("使用捷径后，相比正常步行路线至少应少经过多少条房间连接。")]
        public int MinShortcutSavedEdges = 2;
        [InspectorName("必须保证全部房间连通")]
        public bool RequireConnectedGraph = true;
    }

    [Serializable]
    public sealed class DungeonRoomInjectionRule
    {
        [InspectorName("注入编号")]
        public string ID = "OptionalRoom";
        [InspectorName("显示名称")]
        public string DisplayName = "可选特殊房";
        [InspectorName("启用")]
        public bool Enabled = true;
        [InspectorName("允许阶段")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
        [InspectorName("触发概率（万分比）"), Range(0, 10000)]
        public int Chance = 10000;
        [InspectorName("最少注入数"), Min(0)]
        public int MinCount = 1;
        [InspectorName("最多注入数"), Min(0)]
        public int MaxCount = 1;
        [InspectorName("节点名前缀")]
        public string NodeNamePrefix = "X";
        [InspectorName("房间角色")]
        public RoomRole Role = RoomRole.Battle;
        [InspectorName("事件编号")]
        [Tooltip("角色为事件房时可填写；0 表示不绑定事件。")]
        public int EventID;
        [InspectorName("地标名称")]
        public string LandmarkLabel;
        [InspectorName("锚点必须拥有全部标签")]
        public string[] RequiredAnchorTags = Array.Empty<string>();
        [InspectorName("锚点不能拥有任一标签")]
        public string[] BlockedAnchorTags = Array.Empty<string>();
        [InspectorName("注入房间模板")]
        public List<GameObject> RoomTemplates = new();
    }

    [Serializable]
    public sealed class DungeonEdgeExpansionRule
    {
        [InspectorName("伸缩编号")]
        public string ID = "ExpandableEdge";
        [InspectorName("启用")]
        public bool Enabled = true;
        [InspectorName("允许阶段")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
        [InspectorName("限定布局编号")]
        [Tooltip("为空表示适用于全部布局；否则只匹配列出的布局编号。")]
        public string[] LayoutIDs = Array.Empty<string>();
        [InspectorName("起点节点")]
        public string FromNodeName;
        [InspectorName("终点节点")]
        public string ToNodeName;
        [InspectorName("触发概率（万分比）"), Range(0, 10000)]
        public int Chance = 3500;
        [InspectorName("最少插入房数"), Min(0)]
        public int MinRooms;
        [InspectorName("最多插入房数"), Min(0)]
        public int MaxRooms = 1;
        [InspectorName("节点名前缀")]
        public string NodeNamePrefix = "Connector";
        [InspectorName("连接房模板池")]
        [Tooltip("模板必须至少有两个可连接的门连接点。首版建议每条连接最多插入 1 间房。")]
        public List<GameObject> RoomTemplates = new();
    }

    [Serializable]
    public sealed class DungeonBuildingCandidate
    {
        [InspectorName("建筑编号")]
        public string ID = "Building";
        [InspectorName("显示名称")]
        public string DisplayName = "特殊建筑";
        [InspectorName("抽取权重"), Min(0)]
        public int Weight = 100;
        [InspectorName("允许阶段")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
        [InspectorName("房间模板")]
        public GameObject RoomTemplate;
    }

    [Serializable]
    public sealed class DungeonBuildingPoolRule
    {
        [InspectorName("建筑池编号")]
        public string ID = "CityBuildings";
        [InspectorName("启用")]
        public bool Enabled = true;
        [InspectorName("允许阶段")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
        [InspectorName("限定布局编号")]
        [Tooltip("为空表示适用于全部布局；否则只匹配列出的布局编号。")]
        public string[] LayoutIDs = Array.Empty<string>();
        [InspectorName("建筑槽节点")]
        public string[] SlotNodeNames = Array.Empty<string>();
        [InspectorName("当局抽取数量"), Min(0)]
        public int SelectCount = 3;
        [InspectorName("候选建筑")]
        [Tooltip("同一候选当局最多出现一次；按权重无放回抽取。")]
        public List<DungeonBuildingCandidate> Candidates = new();
    }

    [Serializable]
    public sealed class DungeonShortcutRule
    {
        [InspectorName("捷径编号")]
        public string ID = "Shortcut";
        [InspectorName("启用")]
        public bool Enabled = true;
        [InspectorName("允许阶段")]
        public LevelPhaseMask AllowedPhases = LevelPhaseMask.Both;
        [InspectorName("限定布局编号")]
        [Tooltip("为空表示适用于全部布局；否则只匹配列出的布局编号。")]
        public string[] LayoutIDs = Array.Empty<string>();
        [InspectorName("起点节点")]
        public string SourceNodeName;
        [InspectorName("终点节点")]
        public string TargetNodeName;
        [InspectorName("双向")]
        public bool Bidirectional = true;
        [InspectorName("需要的事件条件")]
        public string RequiredFlags;
        [InspectorName("排除条件")]
        public string BlockedFlags;
        [InspectorName("起点内容插槽")]
        public DungeonContentSocketType SourceSocket = DungeonContentSocketType.Event;
        [InspectorName("终点内容插槽")]
        public DungeonContentSocketType TargetSocket = DungeonContentSocketType.PlayerSpawn;
        [InspectorName("正向提示")]
        public string ForwardTitle = "使用捷径";
        [InspectorName("反向提示")]
        public string ReverseTitle = "返回";
    }

    [CreateAssetMenu(
        fileName = "地牢生成总控",
        menuName = "仙途秘境/关卡/地牢生成总控")]
    public sealed class DungeonGenerationProfile : ScriptableObject
    {
        public const string ResourcePath = "LevelDesign/EdgarGrid3D/地牢生成总控";
        private static DungeonGenerationProfile _instance;
        private static DungeonGenerationProfile _fallback;

        [InspectorName("候选布局")]
        [Tooltip("按权重抽取一个关卡图。列表为空时继续使用 WhiteboxLevelGraph。")]
        public List<DungeonLayoutCandidate> Layouts = new();

        [InspectorName("关卡节奏校验参数")]
        public DungeonRhythmValidationSettings Validation = new();

        [InspectorName("特殊房间注入规则")]
        [Tooltip("基础图确定后，把特殊房作为叶子分支连接到标签兼容且预留空闲门连接点的锚点。")]
        public List<DungeonRoomInjectionRule> RoomInjections = new();

        [InspectorName("连接段伸缩规则")]
        [Tooltip("把指定 A→B 边替换为 A→连接房→B；不配置时不改变基础图。")]
        public List<DungeonEdgeExpansionRule> EdgeExpansions = new();

        [InspectorName("特殊建筑池")]
        [Tooltip("从候选建筑中按权重无放回抽取，并固定到指定建筑槽节点。")]
        public List<DungeonBuildingPoolRule> BuildingPools = new();

        [InspectorName("受控捷径规则")]
        [Tooltip("只在明确节点和条件之间创建传送捷径，不按空间距离自动连边。")]
        public List<DungeonShortcutRule> Shortcuts = new();

        public static DungeonGenerationProfile Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<DungeonGenerationProfile>(ResourcePath);
                if (_instance != null)
                    return _instance;
                if (_fallback == null)
                {
                    _fallback = CreateInstance<DungeonGenerationProfile>();
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                    _fallback.ResetToDefaults();
                }
                return _fallback;
            }
        }

        public DungeonLayoutCandidate SelectLayout(int seed)
        {
            var candidates = new List<DungeonLayoutCandidate>();
            int totalWeight = 0;
            if (Layouts != null)
            {
                foreach (DungeonLayoutCandidate layout in Layouts)
                {
                    if (layout == null || !layout.Enabled || layout.Weight <= 0
                        || layout.LevelGraph == null)
                        continue;
                    candidates.Add(layout);
                    totalWeight += layout.Weight;
                }
            }
            if (candidates.Count == 0 || totalWeight <= 0)
                return null;

            var random = new System.Random(unchecked(seed ^ 0x28D4A63B));
            int roll = random.Next(totalWeight);
            foreach (DungeonLayoutCandidate candidate in candidates)
            {
                roll -= candidate.Weight;
                if (roll < 0)
                    return candidate;
            }
            return candidates[candidates.Count - 1];
        }

        public void ResetToDefaults()
        {
            Validation = new DungeonRhythmValidationSettings();
            Shortcuts = new List<DungeonShortcutRule>
            {
                new()
                {
                    ID = "NightLiftRestored",
                    AllowedPhases = LevelPhaseMask.Night,
                    SourceNodeName = "O1",
                    TargetNodeName = "C0",
                    Bidirectional = true,
                    RequiredFlags = "night_lift_restored=1",
                    ForwardTitle = "前往王城中庭",
                    ReverseTitle = "前往狱城升降井",
                },
                new()
                {
                    ID = "NightLiftDropped",
                    AllowedPhases = LevelPhaseMask.Night,
                    SourceNodeName = "O1",
                    TargetNodeName = "C0",
                    Bidirectional = false,
                    RequiredFlags = "night_lift_dropped=1",
                    ForwardTitle = "顺井返回王城中庭",
                },
            };
        }

        public static void ClearCache()
        {
            _instance = null;
        }
    }
}
