using UnityEngine;
using XianTu.LevelDesign;

namespace XianTu
{
    public sealed class CombatRoomContentHandler : MonoBehaviour, IRoomContentHandler
    {
        private RoomContentContext _context;
        private BattleRoom _battleRoom;
        private EncounterRow _encounter;
        private bool _activated;

        public void Initialize(in RoomContentContext context)
        {
            _context = context;
            _encounter = ConfigDatabase.Instance.GetEncounter(context.Content.ContentConfigID);
            if (_encounter == null)
                throw new System.InvalidOperationException(
                    $"缺失 Encounter={context.Content.ContentConfigID}，Room={context.Spawn.roomIndex}，Seed={context.EncounterSeed}。");

            _battleRoom = gameObject.AddComponent<BattleRoom>();
            float hpMul = (1f + context.Spawn.level * context.Spawn.hpScalePerLevel)
                          * context.Spawn.floorScale;
            float dmgMul = (1f + context.Spawn.level * context.Spawn.dmgScalePerLevel)
                           * context.Spawn.floorScale;
            if (context.RoomType == RoomType.Elite)
                _battleRoom.SetEliteRoom(true);

            _battleRoom.Initialize(
                context.Spawn.roomIndex,
                0,
                hpMul,
                dmgMul,
                context.Spawn.roomSize,
                context.Spawn.roomSize,
                context.Spawn.buildRoomGeometry,
                context.Spawn.contentRoot);
            _battleRoom.SetSkillPool(context.Spawn.skillPool);
            _battleRoom.SetModulePool(context.Spawn.modulePool);
            _battleRoom.SetEnemyHitVFX(context.Spawn.enemyHitVFX);
            _battleRoom.ConfigureEncounter(
                _encounter,
                context.EncounterSeed,
                context.Content.DistrictEnum);
            _battleRoom.Cleared += HandleCleared;
        }

        public void Arm()
        {
            _battleRoom.PrepareEncounter();
            if (_encounter.SpawnModeEnum == SpawnMode.PreplacedDormant)
                _battleRoom.PreSpawnDormant();
            else if (_encounter.SpawnModeEnum == SpawnMode.PatrolActive)
            {
                _activated = true;
                _battleRoom.StartBattle();
            }
        }

        public void Activate()
        {
            if (_activated) return;
            _activated = true;
            _battleRoom.StartBattle();

            if (_encounter.SpawnModeEnum != SpawnMode.ScriptedBoss)
                return;

            Vector3 bossPos = _battleRoom.GetBossSpawnPosition();
            int bossID = _context.State != null ? _context.State.SelectedBossID : 0;
            if (bossID <= 0)
            {
                var config = DungeonLevelAuthoringConfig.Instance;
                bossID = config != null
                    ? config.ResolveBossID(
                        _context.Content.DistrictEnum,
                        _context.EncounterSeed,
                        RoomContentResolver.MergeNormalizedTags(
                            _context.Spawn.roomTags,
                            _context.Content.PrefabTags),
                        _context.Spawn.bossActId)
                    : _context.Spawn.bossActId;
                if (_context.State != null)
                    _context.State.SelectedBossID = bossID;
            }
            var boss = EnemyBoss.Spawn(
                bossPos,
                _battleRoom.HpMultiplier,
                _battleRoom.DmgMultiplier,
                bossID);
            boss.ConfigureSummons(_battleRoom.RegisterEnemy, _context.ContentRoot);
            _battleRoom.RegisterEnemy(boss.gameObject);
        }

        public void RestoreCompleted()
        {
            _activated = true;
        }

        private void OnDestroy()
        {
            if (_battleRoom != null)
                _battleRoom.Cleared -= HandleCleared;
        }

        private void HandleCleared()
        {
            if (_context.RoomType == RoomType.Battle
                || IsOptionalAnnex(_context.ContentRoot))
                SpawnRoomMaterials(_context);
            _context.OnCompleted?.Invoke();
        }

        private static void SpawnRoomMaterials(in RoomContentContext context)
        {
            if (context.State == null || context.State.MaterialsSpawned)
                return;
            if (context.ContentRoot == null)
                throw new System.InvalidOperationException(
                    $"普通战斗房 {context.Spawn.roomIndex} 缺少实体房根节点，无法读取 Material 插槽。");

            var sockets = context.ContentRoot.GetComponentsInChildren<DungeonContentSocket>(true);
            System.Array.Sort(
                sockets,
                (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

            int spawned = 0;
            foreach (var socket in sockets)
            {
                if (socket.SocketType != DungeonContentSocketType.Material)
                    continue;
                if (!DungeonSpawnSafety.TryFindGroundedPoint(
                        context.ContentRoot,
                        socket.transform.position,
                        0.2f,
                        0.6f,
                        0.08f,
                        out Vector3 grounded))
                    throw new System.InvalidOperationException(
                        $"普通战斗房 {context.Spawn.roomIndex} 的材料点 {socket.name} 无法投射到安全地面。");

                if (HasMaterialPickupNear(grounded))
                {
                    spawned++;
                    continue;
                }

                if (CaveMaterialPool.SpawnDeterministic(
                        grounded,
                        context.EncounterSeed + spawned * 7919) != null)
                    spawned++;
            }

            if (spawned == 0)
                throw new System.InvalidOperationException(
                    $"普通战斗房 {context.Spawn.roomIndex} 缺少可用 Material 插槽。");

            context.State.MaterialsSpawned = true;
        }

        private static bool HasMaterialPickupNear(Vector3 point)
        {
            var pickups = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None);
            foreach (var pickup in pickups)
            {
                if (pickup.itemData != null
                    && pickup.itemData.scope == ItemScope.CaveMaterial
                    && Vector3.SqrMagnitude(pickup.transform.position - point) <= 2.25f)
                    return true;
            }
            return false;
        }

        private static bool IsOptionalAnnex(Transform contentRoot)
        {
            if (contentRoot == null)
                return false;
            DungeonRoomAuthoring authoring =
                contentRoot.GetComponentInChildren<DungeonRoomAuthoring>(true);
            if (authoring?.RoomTags == null)
                return false;
            foreach (string tag in authoring.RoomTags)
                if (tag == "OptionalAnnex")
                    return true;
            return false;
        }
    }
}
