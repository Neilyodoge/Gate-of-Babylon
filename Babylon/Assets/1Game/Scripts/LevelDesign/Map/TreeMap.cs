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
                Debug.LogWarning($"[TreeMap] 找不到 ActID={actID} 的 Map_Structure_Config，使用兜底 4 层");
                return GenerateFallback(actID);
            }

            var map = new TreeMap { ActID = actID, MaxFloor = Mathf.Max(2, structure.MaxFloor) };

            // 收集房间池（按类型分组）
            var roomPool = BuildRoomPool(structure.RoomPoolID);

            // 第 0 层：起点（找一个起点配置；如果没有就空置）
            var startNode = new TreeNode
            {
                Floor = 0,
                IndexInFloor = 0,
                RoomType = LevelRoomType.Start,
                RoomConfigID = PickRoomConfig(roomPool, LevelRoomType.Start)
            };
            map.Floors.Add(new List<TreeNode> { startNode });

            // 中间层
            int eliteRemaining = Random.Range(structure.EliteMinCount, Mathf.Max(structure.EliteMinCount, structure.EliteMaxCount) + 1);
            int eventRemaining = structure.EventMinCount;
            int shopRemaining = structure.ShopMinCount;
            int middleFloorCount = map.MaxFloor - 2;
            int totalMiddleNodes = 0;

            for (int floor = 1; floor <= middleFloorCount; floor++)
            {
                int nodeCount = Random.Range(structure.MinNodes, structure.MaxNodes + 1);
                nodeCount = Mathf.Max(1, nodeCount);
                var layer = new List<TreeNode>();
                bool prevFloorHadElite = floor > 1 && AnyOfType(map.Floors[floor - 1], LevelRoomType.Elite);

                for (int i = 0; i < nodeCount; i++)
                {
                    var type = PickRoomType(structure, ref eliteRemaining, ref eventRemaining, ref shopRemaining,
                                            middleFloorCount - floor, prevFloorHadElite);
                    var node = new TreeNode
                    {
                        Floor = floor,
                        IndexInFloor = i,
                        RoomType = type,
                        RoomConfigID = PickRoomConfig(roomPool, type)
                    };
                    layer.Add(node);
                    totalMiddleNodes++;
                }
                map.Floors.Add(layer);
            }

            // Boss 层
            var bossNode = new TreeNode
            {
                Floor = map.MaxFloor - 1,
                IndexInFloor = 0,
                RoomType = LevelRoomType.Boss,
                RoomConfigID = PickRoomConfig(roomPool, LevelRoomType.Boss)
            };
            map.Floors.Add(new List<TreeNode> { bossNode });

            // 连线：每个节点至少 1 入 1 出，跨层不超过 1
            ConnectFloors(map);

            // 初始化为起点
            map.CurrentNode = startNode;
            startNode.Visited = true;

            Debug.Log($"[TreeMap] Act {actID} 生成完成：{map.MaxFloor} 层 / {1 + totalMiddleNodes + 1} 节点");
            return map;
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
