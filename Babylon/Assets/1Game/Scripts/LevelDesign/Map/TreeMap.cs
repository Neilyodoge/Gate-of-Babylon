using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace XianTu.LevelDesign
{
    /// <summary>
    /// GDD §12.2.1 树状关卡图运行时数据。
    /// 由 TreeMapGenerator.Generate(ActID) 创建，由 TreeMapUI 渲染，由 GameManager 消费。
    /// </summary>
    public class TreeMap
    {
        public int ActID;
        public int MaxFloor;

        /// <summary>按层组织的节点列表，Floors[0] = 起点层，Floors[^1] = Boss 层</summary>
        public List<List<TreeNode>> Floors = new();

        /// <summary>玩家当前所在节点（null = 尚未开始）</summary>
        public TreeNode CurrentNode;

        public TreeNode StartNode => Floors.Count > 0 && Floors[0].Count > 0 ? Floors[0][0] : null;
        public TreeNode BossNode => Floors.Count > 0 && Floors[^1].Count > 0 ? Floors[^1][0] : null;

        public bool IsBossReached => CurrentNode != null && CurrentNode.Floor == MaxFloor - 1;
    }

    public class TreeNode
    {
        public int Floor;
        public int IndexInFloor;
        public LevelRoomType RoomType;
        /// <summary>引用 Room_Socket_Group_Config 的 ID</summary>
        public int RoomConfigID;
        /// <summary>可达的下一层节点列表</summary>
        public List<TreeNode> Next = new();
        public bool Visited;
        public bool Cleared;

        public string Icon
        {
            get
            {
                return RoomType switch
                {
                    LevelRoomType.Start => "起",
                    LevelRoomType.Battle => "战",
                    LevelRoomType.Elite => "精",
                    LevelRoomType.Shop => "商",
                    LevelRoomType.Event => "?",
                    LevelRoomType.Boss => "王",
                    _ => "·"
                };
            }
        }

        public Color Color
        {
            get
            {
                return RoomType switch
                {
                    LevelRoomType.Start => new Color(0.85f, 0.85f, 0.85f),
                    LevelRoomType.Battle => new Color(0.85f, 0.3f, 0.3f),
                    LevelRoomType.Elite => new Color(0.95f, 0.5f, 0.2f),
                    LevelRoomType.Shop => new Color(1f, 0.85f, 0.3f),
                    LevelRoomType.Event => new Color(0.6f, 0.7f, 1f),
                    LevelRoomType.Boss => new Color(0.7f, 0.2f, 0.7f),
                    _ => Color.gray
                };
            }
        }
    }

    /// <summary>
    /// 树状图生成算法 —— 按 GDD §12.2.1 实现。
    /// 满足：
    ///   · 节点数 = Random(MinNodes, MaxNodes)
    ///   · 起点/Boss 各 1 个固定
    ///   · 精英房 / 商店 / 事件房 至少最小数量
    ///   · 每节点至少 1 入 1 出
    ///   · 跨层连线不超过 1 层跨度
    /// </summary>
    public static class TreeMapGenerator
    {
        // V0.4.5：改为 Slay the Spire 式分层分叉生成（移植自 silverua/slay-the-spire-map-in-unity
        // 的 MapGenerator 思路：固定列宽 grid + 多条从起点到 Boss 的列随机游走路径 + 汇合分叉）。
        // 产出仍是现有 TreeMap/TreeNode 结构（Floors + Next），供 TreeMapUI 渲染、GameManager 导航消费。
        // 深度 = 本区房间数（MinNodes/MaxNodes 现语义 = 起点→Boss 的层深）；宽度取固定列数更贴近 STS。
        public static TreeMap Generate(int actID, int? seed = null)
        {
            if (seed.HasValue) Random.InitState(seed.Value);

            var db = ConfigDatabase.Instance;
            MapStructureRow structure = null;
            foreach (var kv in db.MapStructures)
            {
                if (kv.Value.ActID == actID)
                {
                    structure = kv.Value;
                    break;
                }
            }
            if (structure == null)
            {
                Debug.LogWarning($"[TreeMap] 找不到 ActID={actID} 的 Map_Structure_Config，使用兜底");
                return GenerateFallback(actID);
            }

            int depth = Mathf.Clamp(Mathf.Max(structure.MaxNodes, 4), 4, 20);   // 层深 = 房间数
            const int width = 4;                                                 // 每层最大分叉列数

            var map = new TreeMap { ActID = actID, MaxFloor = depth };
            var roomPool = BuildRoomPool(structure.RoomPoolID);

            // 起点层（单节点，作为第一间可玩房间）与 Boss 层（单节点）
            var startNode = new TreeNode { Floor = 0, IndexInFloor = 0, RoomType = LevelRoomType.Battle };
            var bossNode = new TreeNode { Floor = depth - 1, IndexInFloor = 0, RoomType = LevelRoomType.Boss };

            int interiorLo = 1, interiorHi = depth - 2;   // 中间可分叉层区间 [lo, hi]
            var grid = new Dictionary<int, Dictionary<int, TreeNode>>();
            var edges = new HashSet<(int, int, int, int)>();

            TreeNode GetOrCreate(int r, int c)
            {
                if (r <= 0) return startNode;
                if (r >= depth - 1) return bossNode;
                if (!grid.TryGetValue(r, out var row)) { row = new Dictionary<int, TreeNode>(); grid[r] = row; }
                if (!row.TryGetValue(c, out var node))
                {
                    node = new TreeNode { Floor = r, IndexInFloor = c, RoomType = LevelRoomType.Battle };
                    row[c] = node;
                }
                return node;
            }

            void Connect(TreeNode a, TreeNode b)
            {
                var key = (a.Floor, a.IndexInFloor, b.Floor, b.IndexInFloor);
                if (!edges.Add(key)) return;
                if (!a.Next.Contains(b)) a.Next.Add(b);
            }

            if (interiorHi < interiorLo)
            {
                Connect(startNode, bossNode);
            }
            else
            {
                int numPaths = Mathf.Clamp(width, 3, 5) + 1;   // 略多于宽度 → 汇合 + 分叉
                for (int p = 0; p < numPaths; p++)
                {
                    int col = Random.Range(0, width);
                    TreeNode prev = startNode;
                    for (int r = interiorLo; r <= interiorHi; r++)
                    {
                        col = Mathf.Clamp(col + Random.Range(-1, 2), 0, width - 1);   // 列随机游走
                        var node = GetOrCreate(r, col);
                        Connect(prev, node);
                        prev = node;
                    }
                    Connect(prev, bossNode);
                }
            }

            // 组装 Floors：起点 → 各中间层（按列排序、重排 IndexInFloor） → Boss
            map.Floors.Add(new List<TreeNode> { startNode });
            for (int r = interiorLo; r <= interiorHi; r++)
            {
                var list = new List<TreeNode>();
                if (grid.TryGetValue(r, out var row))
                {
                    var cols = new List<int>(row.Keys);
                    cols.Sort();
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var node = row[cols[i]];
                        node.IndexInFloor = i;
                        list.Add(node);
                    }
                }
                if (list.Count == 0)
                    list.Add(new TreeNode { Floor = r, IndexInFloor = 0, RoomType = LevelRoomType.Battle });
                map.Floors.Add(list);
            }
            map.Floors.Add(new List<TreeNode> { bossNode });

            // 补断层，保证相邻层至少 1 入 1 出（filler 层或路径缺口兜底）
            EnsureLayerConnectivity(map);

            // 分配房型（保底精英/商店/事件 + 权重）
            AssignRoomTypes(map, structure, roomPool);

            map.CurrentNode = startNode;
            startNode.Visited = true;

            int total = 0; foreach (var l in map.Floors) total += l.Count;
            Debug.Log($"[TreeMap] Act {actID} STS 生成：{depth} 层 / {total} 节点");
            return map;
        }

        /// <summary>补断层：确保相邻层每个节点至少 1 出边、每个下层节点至少 1 入边。</summary>
        private static void EnsureLayerConnectivity(TreeMap map)
        {
            for (int f = 0; f < map.Floors.Count - 1; f++)
            {
                var from = map.Floors[f];
                var to = map.Floors[f + 1];
                if (from.Count == 0 || to.Count == 0) continue;

                foreach (var n in from)
                    if (n.Next.Count == 0)
                        n.Next.Add(to[Mathf.Clamp(n.IndexInFloor, 0, to.Count - 1)]);

                foreach (var t in to)
                {
                    bool hasIn = false;
                    foreach (var n in from) if (n.Next.Contains(t)) { hasIn = true; break; }
                    if (!hasIn)
                    {
                        var src = from[Mathf.Clamp(t.IndexInFloor, 0, from.Count - 1)];
                        if (!src.Next.Contains(t)) src.Next.Add(t);
                    }
                }
            }
        }

        /// <summary>按结构表分配房型：起点=战斗、末层=Boss，中间层按保底数量 + 常规/特殊权重。</summary>
        private static void AssignRoomTypes(TreeMap map, MapStructureRow s,
                                            Dictionary<LevelRoomType, List<RoomSocketRow>> roomPool)
        {
            int eliteRem = Random.Range(s.EliteMinCount, Mathf.Max(s.EliteMinCount, s.EliteMaxCount) + 1);
            int eventRem = s.EventMinCount;
            int shopRem = s.ShopMinCount;
            int interiorLayers = map.Floors.Count - 2;

            for (int f = 1; f <= interiorLayers; f++)
            {
                var layer = map.Floors[f];
                bool prevElite = AnyOfType(map.Floors[f - 1], LevelRoomType.Elite);
                foreach (var node in layer)
                {
                    var type = PickRoomType(s, ref eliteRem, ref eventRem, ref shopRem, interiorLayers - f, prevElite);
                    node.RoomType = type;
                    node.RoomConfigID = PickRoomConfig(roomPool, type);
                }
            }

            if (map.StartNode != null) map.StartNode.RoomConfigID = PickRoomConfig(roomPool, LevelRoomType.Battle);
            if (map.BossNode != null) map.BossNode.RoomConfigID = PickRoomConfig(roomPool, LevelRoomType.Boss);
        }

        private static TreeMap GenerateFallback(int actID)
        {
            var map = new TreeMap { ActID = actID, MaxFloor = 4 };
            var start = new TreeNode { Floor = 0, IndexInFloor = 0, RoomType = LevelRoomType.Start };
            map.Floors.Add(new List<TreeNode> { start });

            for (int f = 1; f <= 2; f++)
            {
                var layer = new List<TreeNode>();
                int nodeCount = Random.Range(2, 4);
                for (int i = 0; i < nodeCount; i++)
                {
                    var type = i == 0 ? LevelRoomType.Battle :
                               (i == 1 ? LevelRoomType.Shop : LevelRoomType.Event);
                    layer.Add(new TreeNode { Floor = f, IndexInFloor = i, RoomType = type });
                }
                map.Floors.Add(layer);
            }
            var boss = new TreeNode { Floor = 3, IndexInFloor = 0, RoomType = LevelRoomType.Boss };
            map.Floors.Add(new List<TreeNode> { boss });

            ConnectFloors(map);
            map.CurrentNode = start;
            start.Visited = true;
            return map;
        }

        // ------------------------------------------------------------
        // 房间池处理
        // ------------------------------------------------------------

        private static Dictionary<LevelRoomType, List<RoomSocketRow>> BuildRoomPool(int[] roomPoolIDs)
        {
            var pool = new Dictionary<LevelRoomType, List<RoomSocketRow>>();
            var db = ConfigDatabase.Instance;
            if (roomPoolIDs == null) return pool;

            foreach (var id in roomPoolIDs)
            {
                if (!db.RoomSockets.TryGetValue(id, out var row)) continue;
                if (!pool.ContainsKey(row.TypeEnum)) pool[row.TypeEnum] = new List<RoomSocketRow>();
                pool[row.TypeEnum].Add(row);
            }
            return pool;
        }

        private static int PickRoomConfig(Dictionary<LevelRoomType, List<RoomSocketRow>> pool, LevelRoomType type)
        {
            if (!pool.TryGetValue(type, out var list) || list.Count == 0) return 0;

            // 按 Weight 加权随机
            int total = 0;
            foreach (var r in list) total += Mathf.Max(1, r.Weight);
            int roll = Random.Range(0, total);
            int acc = 0;
            foreach (var r in list)
            {
                acc += Mathf.Max(1, r.Weight);
                if (roll < acc) return r.ID;
            }
            return list[0].ID;
        }

        private static LevelRoomType PickRoomType(MapStructureRow s, ref int eliteRem, ref int eventRem, ref int shopRem,
                                                  int floorsRemaining, bool prevFloorHadElite)
        {
            // 1. 强制保底：剩余层数不足以填完最小数量时，优先生成
            if (eliteRem > floorsRemaining && !prevFloorHadElite) { eliteRem--; return LevelRoomType.Elite; }
            if (eventRem > floorsRemaining) { eventRem--; return LevelRoomType.Event; }
            if (shopRem > floorsRemaining) { shopRem--; return LevelRoomType.Shop; }

            // 2. 按常规/特殊大类权重
            int normalW = Mathf.Max(1, s.NormalWeight);
            int specialW = Mathf.Max(0, s.SpecialWeight);
            int roll = Random.Range(0, normalW + specialW);
            bool isNormal = roll < normalW;

            if (isNormal)
            {
                // 常规：精英概率取决于 EliteRemaining
                bool canElite = eliteRem > 0 && !prevFloorHadElite;
                bool pickElite = canElite && Random.value < 0.3f;
                if (pickElite) { eliteRem--; return LevelRoomType.Elite; }
                return LevelRoomType.Battle;
            }
            else
            {
                bool canShop = shopRem > 0;
                bool canEvent = eventRem > 0;
                if (canShop && canEvent)
                {
                    if (Random.value < 0.5f) { shopRem--; return LevelRoomType.Shop; }
                    eventRem--; return LevelRoomType.Event;
                }
                if (canShop) { shopRem--; return LevelRoomType.Shop; }
                if (canEvent) { eventRem--; return LevelRoomType.Event; }
                return LevelRoomType.Battle;
            }
        }

        private static bool AnyOfType(List<TreeNode> layer, LevelRoomType t)
        {
            foreach (var n in layer) if (n.RoomType == t) return true;
            return false;
        }

        // ------------------------------------------------------------
        // 连线：保证可达性
        // ------------------------------------------------------------

        private static void ConnectFloors(TreeMap map)
        {
            for (int f = 0; f < map.Floors.Count - 1; f++)
            {
                var from = map.Floors[f];
                var to = map.Floors[f + 1];
                if (from.Count == 0 || to.Count == 0) continue;

                // Pass 1：保证每个起点有至少 1 个出边
                for (int i = 0; i < from.Count; i++)
                {
                    int j = Mathf.Clamp(Mathf.RoundToInt((float)i / Mathf.Max(1, from.Count - 1) * (to.Count - 1)), 0, to.Count - 1);
                    if (!from[i].Next.Contains(to[j])) from[i].Next.Add(to[j]);
                }
                // Pass 2：保证每个终点有至少 1 个入边
                for (int j = 0; j < to.Count; j++)
                {
                    bool hasIn = false;
                    foreach (var n in from)
                    {
                        if (n.Next.Contains(to[j])) { hasIn = true; break; }
                    }
                    if (!hasIn)
                    {
                        int i = Mathf.Clamp(Mathf.RoundToInt((float)j / Mathf.Max(1, to.Count - 1) * (from.Count - 1)), 0, from.Count - 1);
                        from[i].Next.Add(to[j]);
                    }
                }
                // Pass 3：随机额外分叉（增加选择感）
                foreach (var n in from)
                {
                    if (Random.value < 0.35f && to.Count >= 2)
                    {
                        var pick = to[Random.Range(0, to.Count)];
                        if (!n.Next.Contains(pick)) n.Next.Add(pick);
                    }
                }
            }
        }
    }
}
