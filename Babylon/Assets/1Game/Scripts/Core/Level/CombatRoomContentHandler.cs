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
            {
                var config = GameConfig.Instance;
                hpMul *= config != null ? config.精英怪血量倍率 : 3f;
                dmgMul *= config != null ? config.精英怪伤害倍率 : 1.5f;
                _battleRoom.SetEliteRoom(true);
            }

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
            _battleRoom.Cleared += context.OnCompleted;
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
                        _context.Content.PrefabTags,
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
            _battleRoom.RegisterEnemy(boss.gameObject);
        }

        public void RestoreCompleted()
        {
            _activated = true;
        }

        private void OnDestroy()
        {
            if (_battleRoom != null)
                _battleRoom.Cleared -= _context.OnCompleted;
        }
    }
}
