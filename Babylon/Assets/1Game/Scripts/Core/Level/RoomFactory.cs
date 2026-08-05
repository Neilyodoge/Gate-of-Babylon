using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 房间生成上下文：把 GameManager 的当局参数打包传给工厂，
    /// 让房间生成逻辑不再依赖 GameManager 的私有字段。
    /// </summary>
    public struct RoomSpawnContext
    {
        public int level;
        public string realmName;
        public Vector3 spawnPos;
        public float roomSize;
        public SkillData[] skillPool;
        public ModuleDef[] modulePool;
        public GameObject enemyHitVFX;
        public int baseEnemyCount;
        public int enemyCountPerLevel;
        public float hpScalePerLevel;
        public float dmgScalePerLevel;
        /// <summary>本层敌人数值缩放（来自 <see cref="IMapProvider.GetEnemyScale"/>）。</summary>
        public float floorScale;
        /// <summary>Boss 房所属 Act（用于 <see cref="EnemyBoss"/> 形态解析）。</summary>
        public int bossActId;
        /// <summary>事件房通关时回填给 <c>RoomCleared.RoomIndex</c>。</summary>
        public int roomIndex;
        /// <summary>本层实体房间总数，用于稳定解析区域段。</summary>
        public int roomCount;
        /// <summary>本房内容与遭遇的固定随机种子；回访时不得重抽。</summary>
        public int encounterSeed;
        /// <summary>是否由旧 RoomBuilder 创建房间几何；Edgar 实体房间中应关闭。</summary>
        public bool buildRoomGeometry;
        /// <summary>Edgar 当前实体房间根节点；用于读取玩家、敌人和 Boss 内容插槽。</summary>
        public Transform contentRoot;
    }

    /// <summary>按类型创建房间的工厂契约。</summary>
    public interface IRoomFactory
    {
        /// <summary>按类型生成房间，返回房间根 GameObject。</summary>
        GameObject Spawn(RoomType type, in RoomSpawnContext ctx);
    }

    /// <summary>
    /// 默认房间工厂（解耦切面 · V0.4.2）。
    ///
    /// 把原先集中在 GameManager 里的各类 <c>Spawn*Room</c> 逻辑搬到这里，
    /// 让"房间生成"独立于"游戏流程控制"。新增房间类型只需在此加分支，
    /// 不再改动 GameManager 主循环。
    /// </summary>
    public class RoomFactory : IRoomFactory
    {
        public GameObject Spawn(RoomType type, in RoomSpawnContext ctx)
        {
            switch (type)
            {
                case RoomType.Battle: return SpawnBattle(ctx);
                case RoomType.Elite: return SpawnElite(ctx);
                case RoomType.Event: return SpawnEvent(ctx);
                case RoomType.Shop: return SpawnShop(ctx);
                case RoomType.Rest: return SpawnRest(ctx);
                case RoomType.Treasure: return SpawnTreasure(ctx);
                case RoomType.Boss: return SpawnBoss(ctx);
                case RoomType.Upgrade: return SpawnUpgrade(ctx);
                default: return SpawnBattle(ctx);
            }
        }

        private static GameObject SpawnBattle(in RoomSpawnContext ctx)
        {
            return SpawnCombat(RoomType.Battle, "BattleRoom", ctx);
        }

        /// <summary>V0.2.1：精英战斗房 — 更少但更强的敌人 + 保底高稀有度模块掉落</summary>
        private static GameObject SpawnElite(in RoomSpawnContext ctx)
        {
            return SpawnCombat(RoomType.Elite, "EliteRoom", ctx);
        }

        private static GameObject SpawnBoss(in RoomSpawnContext ctx)
        {
            return SpawnCombat(RoomType.Boss, "BossRoom", ctx);
        }

        private static GameObject SpawnCombat(
            RoomType roomType,
            string objectName,
            in RoomSpawnContext ctx)
        {
            var go = new GameObject($"{objectName}_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var handler = go.AddComponent<CombatRoomContentHandler>();
            var controller = go.AddComponent<RoomRuntimeController>();
            controller.Initialize(roomType, ctx, handler);
            Debug.Log(
                $"<color=yellow>【{ctx.realmName}】{roomType} 房由内容配表激活 | Room={ctx.roomIndex} | Seed={ctx.encounterSeed}</color>");
            return go;
        }

        private static GameObject SpawnShop(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"ShopRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<ShopRoom>();
            room.Initialize(ctx.level, ctx.skillPool, ctx.modulePool);
            Debug.Log($"<color=yellow>【{ctx.realmName}】商店房间 — 按F离开</color>");
            return go;
        }

        /// <summary>V0.2.1：事件房 — 触发叙事事件（经 <see cref="IMapProvider"/>），完成后自动 RoomCleared</summary>
        private static GameObject SpawnEvent(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"EventRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;

            RoomBuilder.Build(go.transform, ctx.roomSize, ctx.roomSize, ctx.level);
            Debug.Log($"<color=#6677ff>【{ctx.realmName}】事件房 — 触发叙事事件</color>");

            int roomIndex = ctx.roomIndex;
            MapProviders.Current.TryTriggerRoomEvent(() =>
            {
                GameEvents.Publish(new GameEvents.RoomCleared { RoomIndex = roomIndex, IsEvent = true, IsCombatRoom = true });
            });
            return go;
        }

        private static GameObject SpawnRest(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"RestRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<RestRoom>();
            room.Initialize(ctx.level);
            Debug.Log($"<color=cyan>【{ctx.realmName}】休息房间 — 灵泉恢复生命 — 按F离开</color>");
            return go;
        }

        private static GameObject SpawnTreasure(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"TreasureRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<TreasureRoom>();
            room.Initialize(ctx.level);
            Debug.Log($"<color=yellow>【{ctx.realmName}】宝箱房间 — 靠近开启 — 按F离开</color>");
            return go;
        }

        private static GameObject SpawnUpgrade(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"UpgradeRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<UpgradeRoom>();
            room.Initialize(ctx.level);
            Debug.Log($"<color=green>【{ctx.realmName}】升级房间 — 靠近功法宗师按F修炼 — 按F离开</color>");
            return go;
        }
    }
}
