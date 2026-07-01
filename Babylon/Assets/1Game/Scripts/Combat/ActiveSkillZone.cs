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

        // V.08 增强 payload：区域每 tick 附加链的 modifier 状态（灼烧/冰冻/毒）
        private bool _hasEnh;
        private ChainConfig _enh;

        /// <summary>
        /// V.08：给已召唤的区域注入增强——元素覆盖 + 范围倍率 + 每 tick 附加状态（灼烧/冰冻/毒）。
        /// 在 Spawn 之后调用。控制类（击退/眩晕）不逐 tick 施加，避免持续弹飞。
        /// </summary>
        public void SetEnhancement(ChainConfig cfg, ElementTag elementOverride, float radiusMult)
        {
            _enh = cfg;
            _hasEnh = true;
            if (elementOverride != ElementTag.None) _element = elementOverride;
            if (radiusMult > 0.01f) _radius *= radiusMult;
        }

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

        /// <summary>
        /// V.08 Sustained：用自定义参数召唤一个持续地带（增强把瞬发范围技变成留地 DoT）。
        /// damagePerTickMul 为技能倍率（同 CalcSkillDamage 第二参），element 决定表现色。
        /// </summary>
        public static ActiveSkillZone SpawnCustom(Vector3 pos, PlayerController player, LayerMask enemyMask,
            float radius, float life, float tickInterval, float damagePerTickMul, ElementTag element)
        {
            var go = new GameObject("ActiveSkillZone_Sustained");
            go.transform.position = pos;
            var zone = go.AddComponent<ActiveSkillZone>();
            zone._player = player;
            zone._enemyMask = enemyMask;
            zone._element = element;
            zone._radius = Mathf.Max(0.5f, radius);
            zone._life = Mathf.Max(0.1f, life);
            zone._tickInterval = Mathf.Max(0.05f, tickInterval);
            zone._damagePerTick = Mathf.Max(0f, damagePerTickMul);

            SkillModifierApplier.SpawnCubeVfx(pos + Vector3.up * 0.05f, SkillModifierApplier.ColorOf(element), Mathf.Max(1f, zone._radius * 0.6f), zone._life);
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
                    var dmgable = c.GetComponent<IDamageable>();
                    if (dmgable != null)
                    {
                        float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                        var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, _damagePerTick);
                        dmgable.OnDamage(dmg, c.transform.position, _player.gameObject);
                    }
                }

                if (_slowPct > 0f)
                {
                    var enemy = c.GetComponent<EnemyBase>();
                    if (enemy != null) enemy.ApplySlow(_tickInterval + 0.3f, _slowPct);
                }

                if (_burnDPS > 0f)
                    SkillModifierApplier.ApplyBurn(c.gameObject, _burnDPS, _burnDuration);

                // V.08 增强：每 tick 附加链的 modifier 状态（灼烧/冰冻/毒）
                if (_hasEnh)
                    SkillModifierApplier.ApplyEnhancementStatus(_enh, c.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = SkillModifierApplier.ColorOf(_element);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
