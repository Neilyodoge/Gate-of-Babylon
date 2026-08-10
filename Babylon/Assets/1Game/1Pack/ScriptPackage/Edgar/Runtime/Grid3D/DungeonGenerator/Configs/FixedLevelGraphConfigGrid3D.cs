using System;
using UnityEngine;

namespace Edgar.Unity
{
    [Serializable]
    public class FixedLevelGraphConfigGrid3D
    {
        /// <summary>
        /// Level graph that will be used in the generator.
        /// </summary>
        [InspectorName("关卡图")]
        public LevelGraph LevelGraph;

        /// <summary>
        /// Whether to add corridors between individual rooms in the level graph.
        /// </summary>
        [InspectorName("使用走廊")]
        public bool UseCorridors = true;

        /// <summary>
        /// Global override for the "Allow rotation" setting on individual room templates.
        /// </summary>
        [InspectorName("允许旋转覆盖")]
        public AllowRotationOverrideGrid3D AllowRotationOverride = AllowRotationOverrideGrid3D.NoOverride;

        [InspectorName("修正环形路径内部高度")]
        public bool FixElevationsInsideCycles = false;
    }
}