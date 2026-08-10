using System;
using UnityEngine;

namespace Edgar.Unity
{
    [Serializable]
    public enum DoorDirection
    {
        [InspectorName("无方向")]
        Undirected = 0,
        [InspectorName("入口")]
        Entrance = 1,
        [InspectorName("出口")]
        Exit = 2,
    }
}