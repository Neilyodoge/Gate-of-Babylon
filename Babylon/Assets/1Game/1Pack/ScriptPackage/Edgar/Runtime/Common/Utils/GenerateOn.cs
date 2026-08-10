using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Controls when a generator gets called.
    /// </summary>
    public enum GenerateOn
    {
        /// <summary>
        /// Generator does not get called automatically, you must call it via code.
        /// </summary>
        [InspectorName("手动")]
        Manually,
        
        /// <summary>
        /// Generator is called on Awake.
        /// </summary>
        [InspectorName("Awake 时")]
        Awake,
        
        /// <summary>
        /// Generator is called on Start.
        /// </summary>
        [InspectorName("Start 时")]
        Start
    }
}