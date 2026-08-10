using System;
using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Configuration of builtin post-processing logic.
    /// </summary>
    [Serializable]
    public class PostProcessingConfigGrid3D
    {
        [InspectorName("关卡居中")]
        public bool CenterLevel = true;

        [InspectorName("处理连接物与封门物")]
        public bool ProcessConnectorsAndBlockers = true;

        /// <summary>
        /// How to handle connectors and blockers.
        /// </summary>
        [ConditionalHide(nameof(ProcessConnectorsAndBlockers))]
        [InspectorName("添加连接物")]
        public ConnectorsModeGrid3D AddConnectors = ConnectorsModeGrid3D.RoomsOnly;

        [ConditionalHide(nameof(ProcessConnectorsAndBlockers))]
        [InspectorName("添加封门物")]
        public bool AddBlockers = true;
    }
}