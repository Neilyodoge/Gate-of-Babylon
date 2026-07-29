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
            var go = new GameObject($"BattleRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<BattleRoom>();

            int enemyCount = ctx.baseEnemyCount + ctx.level * ctx.enemyCountPerLevel;
            float hpMul = (1f + ctx.level * ctx.hpScalePerLevel) * ctx.floorScale;
            float dmgMul = (1f + ctx.level * ctx.dmgScalePerLevel) * ctx.floorScale;
            room.Initialize(ctx.level, enemyCount, hpMul, dmgMul, ctx.roomSize, ctx.roomSize);
            room.SetSkillPool(ctx.skillPool);
            room.SetModulePool(ctx.modulePool);

            if (ctx.enemyHitVFX != null)
                room.SetEnemyHitVFX(ctx.enemyHitVFX);

            Debug.Log($"<color=yellow>【{ctx.realmName}】战斗房间 | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1} | 层缩放 x{ctx.floorScale:F2}</color>");
            room.StartBattle();
            return go;
        }

        /// <summary>V0.2.1：精英战斗房 — 更少但更强的敌人 + 保底高稀有度模块掉落</summary>
        private static GameObject SpawnElite(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"EliteRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<BattleRoom>();

            var config = GameConfig.Instance;
            float eliteHpMul = config != null ? config.精英怪血量倍率 : 3f;
            float eliteDmgMul = config != null ? config.精英怪伤害倍率 : 1.5f;

            int enemyCount = Mathf.Max(2, ctx.baseEnemyCount - 1);
            float hpMul = (1f + ctx.level * ctx.hpScalePerLevel) * ctx.floorScale * eliteHpMul;
            float dmgMul = (1f + ctx.level * ctx.dmgScalePerLevel) * ctx.floorScale * eliteDmgMul;

            room.Initialize(ctx.level, enemyCount, hpMul, dmgMul, ctx.roomSize, ctx.roomSize);
            room.SetSkillPool(ctx.skillPool);
            room.SetModulePool(ctx.modulePool);
            room.SetEliteRoom(true);

            if (ctx.enemyHitVFX != null)
                room.SetEnemyHitVFX(ctx.enemyHitVFX);

            Debug.Log($"<color=#ff8800>【{ctx.realmName}】★ 精英房 ★ | 敌人 x{enemyCount} | 血量 x{hpMul:F1} | 伤害 x{dmgMul:F1}</color>");
            room.StartBattle();
            return go;
        }

        private static GameObject SpawnBoss(in RoomSpawnContext ctx)
        {
            var go = new GameObject($"BossRoom_Lv{ctx.level}_{ctx.realmName}");
            go.transform.position = ctx.spawnPos;
            var room = go.AddComponent<BattleRoom>();

            float hpMul = 1f + ctx.level * ctx.hpScalePerLevel;
            float dmgMul = 1f + ctx.level * ctx.dmgScalePerLevel;
            int normalEnemyCount = 2;
            room.Initialize(ctx.level, normalEnemyCount, hpMul, dmgMul, ctx.roomSize, ctx.roomSize);
            room.SetSkillPool(ctx.skillPool);
            room.SetModulePool(ctx.modulePool);

            if (ctx.enemyHitVFX != null)
                room.SetEnemyHitVFX(ctx.enemyHitVFX);

            Debug.Log($"<color=red>【{ctx.realmName}】★ Boss 房间 ★</color>");
            room.StartBattle();

            Vector3 bossPos = ctx.spawnPos + new Vector3(0, 0, 8f);
            EnemyBoss.Spawn(bossPos, hpMul, dmgMul, ctx.bossActId);
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
