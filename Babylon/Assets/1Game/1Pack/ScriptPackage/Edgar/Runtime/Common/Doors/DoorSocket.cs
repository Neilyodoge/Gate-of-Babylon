using Edgar.GraphBasedGenerator.Common.Doors;
using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    /// Basic implementation of door sockets. Two sockets are compatible if they are the same instances.
    /// </summary>
    [CreateAssetMenu(menuName = "仙途秘境/Edgar/门 Socket", fileName = "门Socket")]
    public class DoorSocket : DoorSocketBase
    {
        [InspectorName("显示颜色")]
        public Color Color = Color.red;

        public override bool IsCompatibleWith(IDoorSocket otherSocket)
        {
            return ReferenceEquals(this, otherSocket);
        }

        public override Color GetColor()
        {
            return Color;
        }
    }
}