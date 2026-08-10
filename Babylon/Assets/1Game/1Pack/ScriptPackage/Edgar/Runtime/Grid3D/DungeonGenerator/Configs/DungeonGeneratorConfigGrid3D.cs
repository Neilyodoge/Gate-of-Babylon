using System;
using UnityEngine;

namespace Edgar.Unity
{
    [Serializable]
    public class DungeonGeneratorConfigGrid3D
    {
        [InspectorName("生成关卡根对象")]
        public GameObject RootGameObject;

        /// <summary>
        /// Number of milliseconds before the current attempt to generate
        /// a layout is aborted.
        /// </summary>
        [InspectorName("单次尝试超时（毫秒）")]
        public int Timeout = 10000;

        /// <summary>
        /// Whether to override repeat mode configuration of individual room templates.
        /// </summary>
        [InspectorName("房间重复模式覆盖")]
        public RepeatModeOverride RepeatModeOverride;

        /// <summary>
        /// What is the minimum number of tiles there must be between non-neighboring rooms.
        /// </summary>
        [Range(0, 5)]
        [InspectorName("非相邻房间最小间距")]
        public int MinimumRoomDistance = 1;

        [InspectorName("生成器设置")]
        public GeneratorSettingsGrid3D GeneratorSettings;
    }
}