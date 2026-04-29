using System.Collections.Generic;
using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 由 <see cref="SkillModifierApplier"/> 创建的"落点持续区域"。
    /// 周期性 OverlapSphere 命中区域内敌人，按 modifier 配置造成伤害 / 灼烧。
    /// </summary>
    public class SkillZoneEffect : MonoBehaviour
    {
        private PlayerController _player;
        private SkillModifierDef _mod;
        private float _radius;
        private LayerMask _enemyMask;
        private float _life;
        private float _tickTimer;

        private static readonly Collider[] _buf = new Collider[16];

        public void Init(PlayerController player, SkillModifierDef mod, float radius, LayerMask enemyMask)
        {
            _player = player;
            _mod = mod;
            _radius = Mathf.Max(0.5f, mod.zoneRadius > 0 ? mod.zoneRadius : radius);
            _enemyMask = enemyMask;
            _life = mod.zoneDuration;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_mod == null || _player == null) return;
            if (_mod.zoneTickInterval <= 0f) return;

            _tickTimer += Time.deltaTime;
            if (_tickTimer < _mod.zoneTickInterval) return;
            _tickTimer = 0f;

            int n = Physics.OverlapSphereNonAlloc(transform.position, _radius, _buf, _enemyMask);
            for (int i = 0; i < n; i++)
            {
                var c = _buf[i];
                if (c == null) continue;

                float dmg = _player.Stats.attackDamage * Mathf.Max(0f, _mod.zoneDamageMul);
                if (dmg > 0f)
                {
                    var dmgable = c.GetComponent<IDamageable>();
                    if (dmgable != null)
                    {
                        dmgable.OnDamage(dmg, c.transform.position, _player.gameObject);
                    }
                }

                if (_mod.addBurn && _mod.burnDPS > 0f)
                {
                    SkillModifierApplier.ApplyBurn(c.gameObject, _mod.burnDPS, _mod.burnDuration);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
