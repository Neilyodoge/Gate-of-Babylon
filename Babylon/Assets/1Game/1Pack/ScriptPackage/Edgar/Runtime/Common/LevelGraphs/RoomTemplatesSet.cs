using System.Collections.Generic;
using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Set of room templates that can be stored inside a scriptable object.
    /// </summary>
    [CreateAssetMenu(fileName = "房间模板集合", menuName = "ProjectR/Legacy/关卡/Edgar房间模板集合")]
    public class RoomTemplatesSet : ScriptableObject
    {
        [InspectorName("房间模板")]
        public List<GameObject> RoomTemplates = new List<GameObject>();
    }
}