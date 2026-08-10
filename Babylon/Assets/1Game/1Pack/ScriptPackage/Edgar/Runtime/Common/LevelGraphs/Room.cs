using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Edgar.Unity
{
    /// <summary>
    ///     Represents a room in a level graph.
    /// </summary>
    public class Room : RoomBase
    {
        /// <summary>
        ///     Name of the room.
        /// </summary>
        [InspectorName("房间名称")]
        public string Name = "房间";

        /// <summary>
        ///     Room templates assigned to the room.
        /// </summary>
        [InspectorName("独立房间模板")]
        public List<GameObject> IndividualRoomTemplates = new List<GameObject>();

        /// <summary>
        ///     Assigned room template sets.
        /// </summary>
        [InspectorName("房间模板集合")]
        public List<RoomTemplatesSet> RoomTemplateSets = new List<RoomTemplatesSet>();

        public override List<GameObject> GetRoomTemplates()
        {
            return IndividualRoomTemplates
                .Union(RoomTemplateSets
                    .Where(x => x != null)
                    .SelectMany(x => x.RoomTemplates)
                )
                .Distinct()
                .ToList();
        }

        public override string GetDisplayName()
        {
            return Name;
        }
    }
}