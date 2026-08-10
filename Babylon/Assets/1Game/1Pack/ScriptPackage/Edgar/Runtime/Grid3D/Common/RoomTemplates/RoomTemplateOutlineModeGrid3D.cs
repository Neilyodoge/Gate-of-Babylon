using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Enum that controls how is the outline of a room template computed.
    /// </summary>
    public enum RoomTemplateOutlineModeGrid3D
    {
        [InspectorName("从碰撞体计算")]
        FromColliders = 0,
        [InspectorName("自定义占用格")]
        Custom = 1,
    }
}