using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 房间 Prefab 内的内容候选点。Edgar 只负责空间拼接，具体敌群、掉落和事件由游戏层按类型注入。
    /// </summary>
    public sealed class DungeonContentSocket : MonoBehaviour
    {
        [SerializeField] private DungeonContentSocketType socketType;

        public DungeonContentSocketType SocketType => socketType;

        public void Configure(DungeonContentSocketType type)
        {
            socketType = type;
        }
    }

    public enum DungeonContentSocketType
    {
        PlayerSpawn = 0,
        EnemySpawn = 1,
        Material = 2,
        RewardDrop = 3,
        BossSpawn = 4,
        Event = 5,
        ExitPortal = 6,
    }
}
