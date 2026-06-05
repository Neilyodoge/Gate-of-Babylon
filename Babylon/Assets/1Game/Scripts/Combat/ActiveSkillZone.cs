using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 通用「主动持续区域」——由 Zone 类技能直接召唤（区别于 <see cref="SkillZoneEffect"/> 的灵物修饰落点）。
    /// 支持：周期伤害 / 减速 / 黑洞吸引 / 灼烧 / 随玩家移动。
    /// 一套组件覆盖 混沌吞噬(吸引+DoT) / 天罡北斗阵(随身+减速) / 九天玄火阵(大范围+灼烧) / 冥河召唤(满屏+大幅减速)。
    /// </summary>
    public class ActiveSkillZone : MonoBehaviour
    {
        private PlayerController _player;
        private LayerMask _enemyMask;
        private ElementTag _element;

        private float _radius;
        private float _life;
        private float _tickInterval;
        private float _damagePerTick;
        private float _slowPct;
        private float _pullSpeed;
        private bool _follow;
        private float _burnDPS;
        private float _burnDuration;

        private float _tickTimer;
        private static readonly Collider[] _buf = new Collider[64];

        /// <summary>从 SkillData 召唤一个区域；damageMul 为蓄力等额外伤害倍率。</summary>
        public static ActiveSkillZone Spawn(SkillData skill, Vector3 pos, PlayerController player, LayerMask enemyMask, float damageMul = 1f)
        {
            var go = new GameObject($"ActiveSkillZone_{skill.skillName}");
            go.transform.position = pos;
            var zone = go.AddComponent<ActiveSkillZone>();

            float radius = skill.zoneRadius > 0f ? skill.zoneRadius : (skill.aoeRadius > 0f ? skill.aoeRadius : 4f);
            float life = skill.zoneDuration > 0f ? skill.zoneDuration : (skill.vfxDuration > 0f ? skill.vfxDuration : 5f);

            zone._player = player;
            zone._enemyMask = enemyMask;
            zone._element = skill.elementTag;
            zone._radius = radius;
            zone._life = life;
            zone._tickInterval = Mathf.Max(0.05f, skill.zoneTickInterval);
            zone._damagePerTick = skill.zoneDamagePerTick * Mathf.Max(0f, damageMul);
            zone._slowPct = skill.zoneSlowPct;
            zone._pullSpeed = skill.zonePullSpeed;
            zone._follow = skill.zoneFollowPlayer;
            zone._burnDPS = skill.zoneBurnDPS;
            zone._burnDuration = skill.zoneTickInterval * 2f;

            // 地面区域指示（褪色 cube）
            SkillModifierApplier.SpawnCubeVfx(pos + Vector3.up * 0.05f, SkillModifierApplier.ColorOf(skill.elementTag), Mathf.Max(1f, radius * 0.6f), life);
            return zone;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_player != null && _follow)
                transform.position = _player.transform.position;

            // 黑洞吸引：每帧把范围内敌人拉向中心
            if (_pullSpeed > 0f)
            {
                int pn = Physics.OverlapSphereNonAlloc(transform.position, _radius, _buf, _enemyMask);
                for (int i = 0; i < pn; i++)
                {
                    var c = _buf[i];
                    if (c == null) continue;
                    Vector3 here = c.transform.position;
                    Vector3 center = new Vector3(transform.position.x, here.y, transform.position.z);
                    if ((center - here).sqrMagnitude < 0.25f) continue; // 已在核心附近
                    c.transform.position = Vector3.MoveTowards(here, center, _pullSpeed * Time.deltaTime);
                }
            }

            if (_tickInterval <= 0f) return;
            _tickTimer += Time.deltaTime;
            if (_tickTimer < _tickInterval) return;
            _tickTimer = 0f;

            int n = Physics.OverlapSphereNonAlloc(transform.position, _radius, _buf, _enemyMask);
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i];
                if (c == null) continue;

                if (_damagePerTick > 0f && _player != null)
                {
                    float dmg = _player.Stats.attackDamage * _damagePerTick;
                    var dmgable = c.GetComponent<IDamageable>();
                    if (dmgable != null)
                        dmgable.OnDamage(dmg, c.transform.position, _player.gameObject);
                }

                if (_slowPct > 0f)
                {
                    var enemy = c.GetComponent<EnemyBase>();
                    if (enemy != null) enemy.ApplySlow(_tickInterval + 0.3f, _slowPct);
                }

                if (_burnDPS > 0f)
                    SkillModifierApplier.ApplyBurn(c.gameObject, _burnDPS, _burnDuration);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = SkillModifierApplier.ColorOf(_element);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
