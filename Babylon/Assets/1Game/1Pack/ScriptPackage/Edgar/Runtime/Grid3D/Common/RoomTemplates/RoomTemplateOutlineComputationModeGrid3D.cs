using UnityEngine;

namespace Edgar.Unity
{
    public enum RoomTemplateOutlineComputationModeGrid3D
    {
        [InspectorName("运行时计算")]
        AtRuntime = 0,
        [InspectorName("编辑器内预计算")]
        InsideEditor = 1,
    }
}