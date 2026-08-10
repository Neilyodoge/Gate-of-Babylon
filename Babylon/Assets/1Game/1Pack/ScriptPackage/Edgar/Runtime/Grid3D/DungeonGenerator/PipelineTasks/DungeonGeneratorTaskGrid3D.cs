using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Edgar.GraphBasedGenerator.Common;
using Edgar.GraphBasedGenerator.Grid2D;
using UnityEngine;

namespace Edgar.Unity
{
    // TODO(Grid3D-later): Keep DRY
    public class DungeonGeneratorTaskGrid3D : PipelineTaskGrid3D
    {
        private readonly DungeonGeneratorConfigGrid3D config;

        public DungeonGeneratorTaskGrid3D(DungeonGeneratorConfigGrid3D config)
        {
            this.config = config;
        }

        public override IEnumerator Process()
        {
            var levelDescription = Payload.LevelDescription;

            if (config.Timeout <= 0)
            {
                throw new ConfigurationException($"{nameof(config.Timeout)} 必须大于 0。");
            }

            if (config.GeneratorSettings == null)
            {
                throw new ConfigurationException($"请在 {nameof(DungeonGeneratorGrid3D)} 组件中指定 {nameof(DungeonGeneratorConfigGrid3D.GeneratorSettings)} 字段。");
            }

            // Check that all the room template have the same generator settings as the generator
            var nonMatchingGeneratorSettings = new List<GameObject>();
            foreach (var roomTemplate in levelDescription.GetPrefabToRoomTemplateMapping().Keys)
            {
                var roomTemplateSettings = roomTemplate.GetComponent<RoomTemplateSettingsGrid3D>();
                var generatorSettings = roomTemplateSettings.GeneratorSettings;

                if (generatorSettings != config.GeneratorSettings)
                {
                    nonMatchingGeneratorSettings.Add(roomTemplate);
                }
            }

            if (nonMatchingGeneratorSettings.Count != 0)
            {
                throw new ConfigurationException($"所有房间模板必须与生成器使用相同的生成器设置。以下模板设置不一致：{string.Join(", ", nonMatchingGeneratorSettings.Select(x => x.name))}");
            }

            var rootGameObject = config.RootGameObject;

            // If the root game objects was not set in the config, we do the following:
            // 1. Check if there already exists a game objects with a name reserved for the generated level
            // 2. Otherwise, we create a new empty game object
            if (rootGameObject == null)
            {
                rootGameObject = GameObject.Find("Generated Level");

                if (rootGameObject == null)
                {
                    rootGameObject = new GameObject("Generated Level");
                }
            }
            
            // We need to reset the rotation here in case the object was previously rotated and is now reused
            rootGameObject.transform.rotation = Quaternion.identity;

            // We delete all the children from the root game object - we do not want to combine levels from different runs of the algorithm
            foreach (var child in rootGameObject.transform.Cast<Transform>().ToList())
            {
                child.transform.parent = null;
                PostProcessUtilsGrid2D.Destroy(child.gameObject);
            }

            // The LevelDescription class must be converted to MapDescription
            var levelDescriptionGrid2D = levelDescription.GetLevelDescription();
            levelDescriptionGrid2D.MinimumRoomDistance = config.MinimumRoomDistance;
            levelDescriptionGrid2D.RoomTemplateRepeatModeOverride = GeneratorUtilsGrid2D.GetRepeatMode(config.RepeatModeOverride);

            var configuration = new GraphBasedGeneratorConfiguration<RoomBase>()
            {
                EarlyStopIfTimeExceeded = TimeSpan.FromMilliseconds(config.Timeout),
            };

            // We create the instance of the dungeon generator and inject the correct Random instance
            var generator = new GraphBasedGeneratorGrid2D<RoomBase>(levelDescriptionGrid2D, configuration);
            generator.InjectRandomGenerator(Payload.Random);

            #if UNITY_WEBGL
            var layout = generator.GenerateLayout();

            if (layout == null)
            {
                throw new TimeoutException();
            }
            #else
            // Run the generator in a different thread so that the computation is not blocking
            LayoutGrid2D<RoomBase> layout = null;
            var task = Task.Run(() => layout = generator.GenerateLayout());

            while (!task.IsCompleted)
            {
                yield return null;
            }

            // Throw an exception when a timeout is reached
            // TODO(Grid3D-later): this should be our own exception and not a generic exception
            if (layout == null)
            {
                if (task.Exception != null)
                {
                    if (task.Exception.InnerException != null)
                    {
                        throw task.Exception.InnerException;
                    }

                    throw task.Exception;
                }
                else
                {
                    throw new TimeoutException();
                }
            }

            #endif

            // Transform the level to its Unity representation
            var generatedLevel = GeneratorUtilsGrid3D.TransformLayout(layout, levelDescription, rootGameObject, Payload.Random, config.GeneratorSettings, Payload.Seed);

            var stats = new GeneratorStats()
            {
                Iterations = generator.IterationsCount,
                TimeTotal = generator.TimeTotal,
            };

            Debug.Log($"Edgar 布局生成完成，用时 {stats.TimeTotal / 1000f:F} 秒");
            Debug.Log($"共迭代 {stats.Iterations} 次，每秒约 {stats.Iterations / (stats.TimeTotal / 1000d):0} 次");

            Payload.GeneratedLevel = generatedLevel;
            Payload.GeneratorStats = stats;

            yield return null;
        }
    }
}