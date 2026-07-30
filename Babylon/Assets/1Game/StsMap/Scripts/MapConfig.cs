using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    [CreateAssetMenu]
    public class MapConfig : ScriptableObject
    {
        public List<NodeBlueprint> nodeBlueprints;
        [Tooltip("Nodes that will be used on layers with Randomize Nodes > 0")]
        public List<NodeType> randomNodes = new List<NodeType>
            {NodeType.Mystery, NodeType.Store, NodeType.Treasure, NodeType.MinorEnemy, NodeType.RestSite};
        public int GridWidth => Mathf.Max(numOfPreBossNodes.max, numOfStartingNodes.max);

        public IntMinMax numOfPreBossNodes;
        public IntMinMax numOfStartingNodes;

        [Tooltip("Increase this number to generate more paths")]
        public int extraPaths;
        public List<MapLayer> layers;

        [Header("仙途秘境：按总房间预算生成三路线")]
        [Tooltip("开启后不再把 Layers 数量当成每条路线长度，而是把 totalNodeCount 个节点拆到 routeCount 条长短不同的路线。")]
        public bool useRouteBudget;
        [Min(2)] public int routeCount = 3;
        [Tooltip("包含三条路线的全部节点与共享 Boss；V0.4.1 目标为 24~26。")]
        public IntMinMax totalNodeCount = new IntMinMax { min = 24, max = 26 };
    }
}