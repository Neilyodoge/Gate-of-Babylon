using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Edgar.Unity
{
    [AddComponentMenu("仙途秘境/Edgar Grid3D/地牢生成器")]
    public class DungeonGeneratorGrid3D : LevelGeneratorBase<DungeonGeneratorPayloadGrid3D>
    {
        [InspectorName("输入类型")]
        public DungeonGeneratorInputTypeGrid2D InputType;

        [ExpandableScriptableObject]
        [InspectorName("自定义输入")]
        public DungeonGeneratorInputBaseGrid3D CustomInput;

        /// <summary>
        /// Whether to use a random seed.
        /// </summary>
        [InspectorName("使用随机种子")]
        public bool UseRandomSeed = true;

        /// <summary>
        /// Which seed should be used for the random numbers generator.
        /// Is used only when UseRandomSeed is false.
        /// </summary>
        [InspectorName("生成种子")]
        public int RandomGeneratorSeed;

        [Obsolete("The ThrowExceptionsImmediately is no longer used. It was previously used inside SmartCoroutine but that piece of code was removed.")]
        protected override bool ThrowExceptionImmediately => false;

        [Expandable]
        [InspectorName("固定关卡图配置")]
        public FixedLevelGraphConfigGrid3D FixedLevelGraphConfig;

        [Expandable]
        [InspectorName("生成器配置")]
        public DungeonGeneratorConfigGrid3D GeneratorConfig;

        [Expandable]
        [InspectorName("后处理配置")]
        public PostProcessingConfigGrid3D PostProcessingConfig;

        [ExpandableScriptableObject(CanFold = false)]
        [InspectorName("自定义后处理任务")]
        public List<DungeonGeneratorPostProcessingGrid3D> CustomPostProcessingTasks;

        /// <summary>
        /// Whether to generate a level on enter play mode.
        /// </summary>
        [Obsolete("Use the GenerateOn field instead.")]
        public bool GenerateOnStart
        {
            get => GenerateOn == GenerateOn.Start;
            set
            {
                if (value)
                {
                    GenerateOn = GenerateOn.Start;
                }
                else
                {
                    GenerateOn = GenerateOn.Manually;
                }
            }
        }
        
        /// <summary>
        /// Whether to generate a level automatically when entering the play mode/opening a scene.
        /// </summary>
        [InspectorName("生成时机")]
        public GenerateOn GenerateOn = GenerateOn.Awake;

        [SerializeField]
        [FormerlySerializedAs("GenerateOnStart")]
        [Obsolete("This field is here only to convert the old GenerateOnStart to GenerateOn")]
        private bool generateOnStart;

        /// <summary>
        /// Disable all custom post-processing tasks.
        /// </summary>
        [InspectorName("禁用自定义后处理")]
        public bool DisableCustomPostProcessing = false;

        public void Start()
        {
            if (GenerateOn == GenerateOn.Start)
            {
                Generate();
            }
        }
        
        public void Awake()
        {
            if (GenerateOn == GenerateOn.Awake)
            {
                Generate();
            }
        }

        protected override (List<IPipelineTask<DungeonGeneratorPayloadGrid3D>> pipelineItems, DungeonGeneratorPayloadGrid3D payload) GetPipelineItemsAndPayload()
        {
            var (random, seed) = GetRandomNumbersGenerator(UseRandomSeed, RandomGeneratorSeed);
            var payload = new DungeonGeneratorPayloadGrid3D()
            {
                Random = random,
                Seed = seed,
                DungeonGenerator = this,
            };

            var postProcessingTasks = !DisableCustomPostProcessing
                ? CustomPostProcessingTasks
                : new List<DungeonGeneratorPostProcessingGrid3D>();

            var postProcessingComponents = !DisableCustomPostProcessing
                ? GetComponents<DungeonGeneratorPostProcessingComponentGrid3D>().ToList()
                : new List<DungeonGeneratorPostProcessingComponentGrid3D>();

            if (InputType == DungeonGeneratorInputTypeGrid2D.CustomInput && CustomInput == null)
            {
                throw new InvalidOperationException("输入类型设为“自定义输入”时，自定义输入脚本不能为空。");
            }
            
            var pipelineItems = new List<IPipelineTask<DungeonGeneratorPayloadGrid3D>>
            {
                InputType == DungeonGeneratorInputTypeGrid2D.FixedLevelGraph
                    ? new FixedLevelGraphInputGrid3D(FixedLevelGraphConfig)
                    : (IPipelineTask<DungeonGeneratorPayloadGrid3D>) CustomInput,
                new DungeonGeneratorTaskGrid3D(GeneratorConfig),
                new PostProcessingTaskGrid3D(PostProcessingConfig, postProcessingTasks, postProcessingComponents)
            };

            return (pipelineItems, payload);
        }
        
        protected override int OnUpgradeSerializedData(int version)
        {
            #pragma warning disable 618
            if (version < 2)
            {
                GenerateOn = generateOnStart ? GenerateOn.Start : GenerateOn.Manually;
            }
            #pragma warning restore 618

            return 2;
        }
    }
}