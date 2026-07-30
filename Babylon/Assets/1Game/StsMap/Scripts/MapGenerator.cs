using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Map
{
    public static class MapGenerator
    {
        private static MapConfig config;

        private static List<float> layerDistances;
        // ALL nodes by layer:
        private static readonly List<List<Node>> nodes = new List<List<Node>>();

        public static Map GetMap(MapConfig conf)
        {
            if (conf == null)
            {
                Debug.LogWarning("Config was null in MapGenerator.Generate()");
                return null;
            }

            config = conf;
            nodes.Clear();

            if (conf.useRouteBudget)
                return GenerateRouteBudgetMap(conf);

            GenerateLayerDistances();

            for (int i = 0; i < conf.layers.Count; i++)
                PlaceLayer(i);

            List<List<Vector2Int>> paths = GeneratePaths();

            RandomizeNodePositions();

            SetUpConnections(paths);

            RemoveCrossConnections();

            // select all the nodes with connections:
            List<Node> nodesList = nodes.SelectMany(n => n).Where(n => n.incoming.Count > 0 || n.outgoing.Count > 0).ToList();

            // pick a random name of the boss level for this map:
            string bossNodeName = config.nodeBlueprints.Where(b => b.nodeType == NodeType.Boss).ToList().Random().name;
            return new Map(conf.name, bossNodeName, nodesList, new List<Vector2Int>());
        }

        /// <summary>
        /// 《仙途秘境》V0.4.1：把 24~26 个总节点拆为三条长短不同的独立路线，
        /// 最后汇入一个共享 Boss。原 silverua 算法的 layers 表示纵向层数，因此把
        /// layers 扩到 24 会误变成“每条路线 24 间”；预算模式直接以总节点数为约束。
        /// </summary>
        private static Map GenerateRouteBudgetMap(MapConfig conf)
        {
            int routeCount = Mathf.Max(2, conf.routeCount);
            int totalNodeCount = Mathf.Max(routeCount + 2, conf.totalNodeCount.GetValue());
            int normalNodeCount = totalNodeCount - 1; // 共享 Boss 占 1
            int[] routeLengths = DistributeRouteLengths(normalNodeCount, routeCount);

            var result = new List<Node>(totalNodeCount);
            float horizontalSpacing = 4f;
            float verticalSpacing = 3f;
            float xOffset = (routeCount - 1) * horizontalSpacing * 0.5f;
            int maxLength = routeLengths.Max();

            for (int route = 0; route < routeCount; route++)
            {
                Node previous = null;
                int eliteCount = 0;
                int storeCount = 0;

                for (int step = 0; step < routeLengths[route]; step++)
                {
                    NodeType type = PickBudgetNodeType(step, routeLengths[route], ref eliteCount, ref storeCount);
                    string blueprintName = GetBlueprintName(conf, type);
                    var node = new Node(type, blueprintName, new Vector2Int(route, step))
                    {
                        position = new Vector2(
                            route * horizontalSpacing - xOffset,
                            step * verticalSpacing)
                    };

                    if (previous != null)
                    {
                        previous.AddOutgoing(node.point);
                        node.AddIncoming(previous.point);
                    }

                    result.Add(node);
                    previous = node;
                }
            }

            string bossNodeName = GetBlueprintName(conf, NodeType.Boss);
            var boss = new Node(NodeType.Boss, bossNodeName,
                new Vector2Int(routeCount / 2, maxLength))
            {
                position = new Vector2(0f, maxLength * verticalSpacing + 2f)
            };

            for (int route = 0; route < routeCount; route++)
            {
                var last = result.First(n => n.point.x == route && n.point.y == routeLengths[route] - 1);
                last.AddOutgoing(boss.point);
                boss.AddIncoming(last.point);
            }
            result.Add(boss);

            Debug.Log($"[MapGenerator] 路线预算地图：总节点={result.Count}，路线长度={string.Join("/", routeLengths)}（共享 Boss 另计）");
            return new Map(conf.name, bossNodeName, result, new List<Vector2Int>());
        }

        private static int[] DistributeRouteLengths(int total, int routeCount)
        {
            var lengths = new int[routeCount];
            int baseLength = total / routeCount;
            int remainder = total % routeCount;
            for (int i = 0; i < routeCount; i++)
                lengths[i] = baseLength + (i < remainder ? 1 : 0);

            // 总数恰好整除时也制造长短差，避免三条路线完全等长。
            if (remainder == 0 && baseLength > 2)
            {
                lengths[0]--;
                lengths[routeCount - 1]++;
            }
            lengths.Shuffle();
            return lengths;
        }

        private static NodeType PickBudgetNodeType(int step, int routeLength,
            ref int eliteCount, ref int storeCount)
        {
            if (step == 0) return NodeType.MinorEnemy;
            if (step == routeLength - 1 && Random.value < 0.5f) return NodeType.RestSite;

            float roll = Random.value;
            if (roll < 0.08f && eliteCount < 2)
            {
                eliteCount++;
                return NodeType.EliteEnemy;
            }
            if (roll < 0.18f && storeCount < 4)
            {
                storeCount++;
                return NodeType.Store;
            }
            if (roll < 0.30f) return NodeType.Mystery;
            if (roll < 0.40f) return NodeType.Treasure;
            if (roll < 0.50f) return NodeType.RestSite;
            return NodeType.MinorEnemy;
        }

        private static string GetBlueprintName(MapConfig conf, NodeType type)
        {
            var matches = conf.nodeBlueprints.Where(b => b != null && b.nodeType == type).ToList();
            if (matches.Count > 0) return matches.Random().name;

            // 配置缺某类型蓝图时回退普通战斗，避免生成空节点。
            var fallback = conf.nodeBlueprints.Where(b => b != null && b.nodeType == NodeType.MinorEnemy).ToList();
            return fallback.Count > 0 ? fallback.Random().name : string.Empty;
        }

        private static void GenerateLayerDistances()
        {
            layerDistances = new List<float>();
            foreach (MapLayer layer in config.layers)
                layerDistances.Add(layer.distanceFromPreviousLayer.GetValue());
        }

        private static float GetDistanceToLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex > layerDistances.Count) return 0f;

            return layerDistances.Take(layerIndex + 1).Sum();
        }

        private static void PlaceLayer(int layerIndex)
        {
            MapLayer layer = config.layers[layerIndex];
            List<Node> nodesOnThisLayer = new List<Node>();

            // offset of this layer to make all the nodes centered:
            float offset = layer.nodesApartDistance * config.GridWidth / 2f;

            for (int i = 0; i < config.GridWidth; i++)
            {
                var supportedRandomNodeTypes =
                    config.randomNodes.Where(t => config.nodeBlueprints.Any(b => b.nodeType == t)).ToList();
                NodeType nodeType = Random.Range(0f, 1f) < layer.randomizeNodes && supportedRandomNodeTypes.Count > 0
                    ? supportedRandomNodeTypes.Random()
                    : layer.nodeType;
                string blueprintName = config.nodeBlueprints.Where(b => b.nodeType == nodeType).ToList().Random().name;
                Node node = new Node(nodeType, blueprintName, new Vector2Int(i, layerIndex))
                {
                    position = new Vector2(-offset + i * layer.nodesApartDistance, GetDistanceToLayer(layerIndex))
                };
                nodesOnThisLayer.Add(node);
            }

            nodes.Add(nodesOnThisLayer);
        }

        private static void RandomizeNodePositions()
        {
            for (int index = 0; index < nodes.Count; index++)
            {
                List<Node> list = nodes[index];
                MapLayer layer = config.layers[index];
                float distToNextLayer = index + 1 >= layerDistances.Count
                    ? 0f
                    : layerDistances[index + 1];
                float distToPreviousLayer = layerDistances[index];

                foreach (Node node in list)
                {
                    float xRnd = Random.Range(-0.5f, 0.5f);
                    float yRnd = Random.Range(-0.5f, 0.5f);

                    float x = xRnd * layer.nodesApartDistance;
                    float y = yRnd < 0 ? distToPreviousLayer * yRnd: distToNextLayer * yRnd;

                    node.position += new Vector2(x, y) * layer.randomizePosition;
                }
            }
        }

        private static void SetUpConnections(List<List<Vector2Int>> paths)
        {
            foreach (List<Vector2Int> path in paths)
            {
                for (int i = 0; i < path.Count - 1; ++i)
                {
                    Node node = GetNode(path[i]);
                    Node nextNode = GetNode(path[i + 1]);
                    node.AddOutgoing(nextNode.point);
                    nextNode.AddIncoming(node.point);
                }
            }
        }

        private static void RemoveCrossConnections()
        {
            for (int i = 0; i < config.GridWidth - 1; ++i)
                for (int j = 0; j < config.layers.Count - 1; ++j)
                {
                    Node node = GetNode(new Vector2Int(i, j));
                    if (node == null || node.HasNoConnections()) continue;
                    Node right = GetNode(new Vector2Int(i + 1, j));
                    if (right == null || right.HasNoConnections()) continue;
                    Node top = GetNode(new Vector2Int(i, j + 1));
                    if (top == null || top.HasNoConnections()) continue;
                    Node topRight = GetNode(new Vector2Int(i + 1, j + 1));
                    if (topRight == null || topRight.HasNoConnections()) continue;

                    // Debug.Log("Inspecting node for connections: " + node.point);
                    if (!node.outgoing.Any(element => element.Equals(topRight.point))) continue;
                    if (!right.outgoing.Any(element => element.Equals(top.point))) continue;

                    // Debug.Log("Found a cross node: " + node.point);

                    // we managed to find a cross node:
                    // 1) add direct connections:
                    node.AddOutgoing(top.point);
                    top.AddIncoming(node.point);

                    right.AddOutgoing(topRight.point);
                    topRight.AddIncoming(right.point);

                    float rnd = Random.Range(0f, 1f);
                    if (rnd < 0.2f)
                    {
                        // remove both cross connections:
                        // a) 
                        node.RemoveOutgoing(topRight.point);
                        topRight.RemoveIncoming(node.point);
                        // b) 
                        right.RemoveOutgoing(top.point);
                        top.RemoveIncoming(right.point);
                    }
                    else if (rnd < 0.6f)
                    {
                        // a) 
                        node.RemoveOutgoing(topRight.point);
                        topRight.RemoveIncoming(node.point);
                    }
                    else
                    {
                        // b) 
                        right.RemoveOutgoing(top.point);
                        top.RemoveIncoming(right.point);
                    }
                }
        }

        private static Node GetNode(Vector2Int p)
        {
            if (p.y >= nodes.Count) return null;
            if (p.x >= nodes[p.y].Count) return null;

            return nodes[p.y][p.x];
        }

        private static Vector2Int GetFinalNode()
        {
            int y = config.layers.Count - 1;
            if (config.GridWidth % 2 == 1)
                return new Vector2Int(config.GridWidth / 2, y);

            return Random.Range(0, 2) == 0
                ? new Vector2Int(config.GridWidth / 2, y)
                : new Vector2Int(config.GridWidth / 2 - 1, y);
        }

        private static List<List<Vector2Int>> GeneratePaths()
        {
            Vector2Int finalNode = GetFinalNode();
            var paths = new List<List<Vector2Int>>();
            int numOfStartingNodes = config.numOfStartingNodes.GetValue();
            int numOfPreBossNodes = config.numOfPreBossNodes.GetValue();

            List<int> candidateXs = new List<int>();
            for (int i = 0; i < config.GridWidth; i++)
                candidateXs.Add(i);

            candidateXs.Shuffle();
            IEnumerable<int> startingXs = candidateXs.Take(numOfStartingNodes);
            List<Vector2Int> startingPoints = (from x in startingXs select new Vector2Int(x, 0)).ToList();

            candidateXs.Shuffle();
            IEnumerable<int> preBossXs = candidateXs.Take(numOfPreBossNodes);
            List<Vector2Int> preBossPoints = (from x in preBossXs select new Vector2Int(x, finalNode.y - 1)).ToList();

            int numOfPaths = Mathf.Max(numOfStartingNodes, numOfPreBossNodes) + Mathf.Max(0, config.extraPaths);
            for (int i = 0; i < numOfPaths; ++i)
            {
                Vector2Int startNode = startingPoints[i % numOfStartingNodes];
                Vector2Int endNode = preBossPoints[i % numOfPreBossNodes];
                List<Vector2Int> path = Path(startNode, endNode);
                path.Add(finalNode);
                paths.Add(path);
            }

            return paths;
        }

        // Generates a random path bottom up.
        private static List<Vector2Int> Path(Vector2Int fromPoint, Vector2Int toPoint)
        {
            int toRow = toPoint.y;
            int toCol = toPoint.x;

            int lastNodeCol = fromPoint.x;

            List<Vector2Int> path = new List<Vector2Int> { fromPoint };
            List<int> candidateCols = new List<int>();
            for (int row = 1; row < toRow; ++row)
            {
                candidateCols.Clear();

                int verticalDistance = toRow - row;
                int horizontalDistance;

                int forwardCol = lastNodeCol;
                horizontalDistance = Mathf.Abs(toCol - forwardCol);
                if (horizontalDistance <= verticalDistance)
                    candidateCols.Add(lastNodeCol);

                int leftCol = lastNodeCol - 1;
                horizontalDistance = Mathf.Abs(toCol - leftCol);
                if (leftCol >= 0 && horizontalDistance <= verticalDistance)
                    candidateCols.Add(leftCol);

                int rightCol = lastNodeCol + 1;
                horizontalDistance = Mathf.Abs(toCol - rightCol);
                if (rightCol < config.GridWidth && horizontalDistance <= verticalDistance)
                    candidateCols.Add(rightCol);

                int randomCandidateIndex = Random.Range(0, candidateCols.Count);
                int candidateCol = candidateCols[randomCandidateIndex];
                Vector2Int nextPoint = new Vector2Int(candidateCol, row);

                path.Add(nextPoint);

                lastNodeCol = candidateCol;
            }

            path.Add(toPoint);

            return path;
        }
    }
}