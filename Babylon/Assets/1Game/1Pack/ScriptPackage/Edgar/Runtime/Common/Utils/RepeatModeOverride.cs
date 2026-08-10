using UnityEngine;

namespace Edgar.Unity
{
    public enum RepeatModeOverride
    {
        [InspectorName("不覆盖")]
        NoOverride = 0,
        [InspectorName("允许重复")]
        AllowRepeat = 1,
        [InspectorName("禁止紧邻重复")]
        NoImmediate = 2,
        [InspectorName("禁止重复")]
        NoRepeat = 3,
    }
}