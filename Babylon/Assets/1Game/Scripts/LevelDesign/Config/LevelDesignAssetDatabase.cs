using System.Collections.Generic;
using UnityEngine;

namespace XianTu.LevelDesign
{
    [CreateAssetMenu(
        fileName = "关卡数据库",
        menuName = "仙途秘境/关卡/关卡数据库")]
    public sealed class LevelDesignAssetDatabase : ScriptableObject
    {
        public const string ResourcePath = "LevelDesign/关卡数据库";

        [InspectorName("秘境结构")]
        [Tooltip("每个秘境一条，配置敌人数值倍率、模块稀有度偏移和阶段返回点。")]
        public List<MapStructureRow> MapStructures = new();

        [InspectorName("房间内容")]
        [Tooltip("按房间角色、分区、深度和权重选择内容。")]
        public List<RoomContentRow> RoomContents = new();

        [InspectorName("战斗遭遇")]
        [Tooltip("配置刷怪方式、波次和增援时机；普通小怪数量由关卡生成配置自动计算。")]
        public List<EncounterRow> Encounters = new();

        [InspectorName("剧情事件")]
        [Tooltip("配置事件正文、触发条件、玩家选项和结果。")]
        public List<StoryEventRow> StoryEvents = new();

        [InspectorName("首领阶段")]
        [Tooltip("按首领编号和事件条件选择对白及属性修正。")]
        public List<BossPhaseRow> BossPhases = new();
    }
}
