using System.Collections;
using System.Linq;
using UnityEngine;

namespace Edgar.Unity
{
    internal class FixedLevelGraphInputGrid3D : PipelineTaskGrid3D
    {
        private readonly FixedLevelGraphConfigGrid3D config;

        public FixedLevelGraphInputGrid3D(FixedLevelGraphConfigGrid3D config)
        {
            this.config = config;
        }

        public override IEnumerator Process()
        {
            if (config.LevelGraph == null)
            {
                throw new ConfigurationException("LevelGraph 字段不能为空。请在生成器组件的“输入配置”中指定关卡图。");
            }

            if (config.LevelGraph.Rooms.Count == 0)
            {
                throw new ConfigurationException($"关卡图至少需要一个房间。请向关卡图“{config.LevelGraph.name}”添加房间。");
            }

            var levelDescription = new LevelDescriptionGrid3D(config.AllowRotationOverride.GetBoolValue());

            // Setup individual rooms
            foreach (var room in config.LevelGraph.Rooms)
            {
                var roomTemplates = InputSetupUtils.GetRoomTemplates(room, config.LevelGraph.DefaultRoomTemplateSets, config.LevelGraph.DefaultIndividualRoomTemplates);

                if (roomTemplates.Count == 0)
                {
                    throw new ConfigurationException($"房间“{room.GetDisplayName()}”没有可用模板，默认房间模板集合中也没有模板。请至少为该房间提供一个房间模板。");
                }

                levelDescription.AddRoom(room, roomTemplates);
            }

            var typeOfRooms = config.LevelGraph.Rooms.First().GetType();

            // Add passages
            foreach (var connection in config.LevelGraph.Connections)
            {
                if (config.UseCorridors)
                {
                    var corridorRoom = (RoomBase) ScriptableObject.CreateInstance(typeOfRooms);

                    if (corridorRoom is Room basicRoom)
                    {
                        basicRoom.Name = "走廊";
                    }

                    levelDescription.AddCorridorConnection(connection, corridorRoom,
                        InputSetupUtils.GetRoomTemplates(connection, config.LevelGraph.CorridorRoomTemplateSets, config.LevelGraph.CorridorIndividualRoomTemplates));
                }
                else
                {
                    levelDescription.AddConnection(connection);
                }
            }

            InputSetupUtils.CheckIfDirected(levelDescription, config.LevelGraph);

            if (config.FixElevationsInsideCycles)
            {
                levelDescription.FixElevationsInsideCycles();
            }

            Payload.LevelDescription = levelDescription;

            yield return null;
        }
    }
}