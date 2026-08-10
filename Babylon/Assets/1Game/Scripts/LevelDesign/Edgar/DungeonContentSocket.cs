using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 房间 Prefab 内的内容候选点。Edgar 只负责空间拼接，具体敌群、掉落和事件由游戏层按类型注入。
    /// </summary>
    public sealed class DungeonContentSocket : MonoBehaviour
    {
        [SerializeField, InspectorName("内容点类型")]
        private DungeonContentSocketType socketType;

        public DungeonContentSocketType SocketType => socketType;

        public void Configure(DungeonContentSocketType type)
        {
            socketType = type;
        }
    }

    public enum DungeonContentSocketType
    {
        [InspectorName("玩家出生点")]
        PlayerSpawn = 0,
        [InspectorName("普通敌人出生点")]
        EnemySpawn = 1,
        [InspectorName("材料点")]
        Material = 2,
        [InspectorName("奖励掉落点")]
        RewardDrop = 3,
        [InspectorName("首领出生点")]
        BossSpawn = 4,
        [InspectorName("事件点")]
        Event = 5,
        [InspectorName("出口传送门")]
        ExitPortal = 6,
    }
}
