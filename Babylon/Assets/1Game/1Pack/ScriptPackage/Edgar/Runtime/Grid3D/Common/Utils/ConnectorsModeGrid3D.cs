using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Controls how to handle room template connectors.
    /// </summary>
    public enum ConnectorsModeGrid3D
    {
        /// <summary>
        /// Connectors are never added.
        /// </summary>
        [InspectorName("从不添加")]
        Never = 0,

        /// <summary>
        /// Only room connectors are added.
        /// </summary>
        [InspectorName("仅房间")]
        RoomsOnly = 1,

        /// <summary>
        /// Only corridor connectors are added.
        /// </summary>
        [InspectorName("仅走廊")]
        CorridorsOnly = 2,

        /// <summary>
        /// Both room and corridor connectors are added.
        /// </summary>
        [InspectorName("房间与走廊")]
        RoomsAndCorridors = 3,

        /// <summary>
        /// Prefers to use corridors for connectors but if there is no corridor, uses room connectors.
        /// </summary>
        [InspectorName("优先走廊")]
        PreferCorridors = 4,
    }
}