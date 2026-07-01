using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// V.08 节奏改造·延迟爆炸（DelayedBlast）——增强让范围核心技在落点追加一次带预警的延迟重爆。
    /// 预警期显示地面指示，延迟后对范围内敌人造成一次伤害 + 元素表现 + 附加状态（灼烧/冰冻/毒）。
    /// </summary>
    public class DelayedAreaBlast : MonoBehaviour
    {
        private PlayerController _player;
        private LayerMask _enemyMask;
        private Vector3 _center;
        private float _radius;
        private float _delay;
        private float _skillMul;     // CalcSkillDamage 第二参
        private ElementTag _element;
        private bool _hasEnh;
        private ChainConfig _enh;
        private bool _detonated;

        /// <summary>
        /// 召唤一个延迟爆炸。skillMul 为技能倍率（同 CalcSkillDamage 第二参），delay 为预警时长。
        /// </summary>
        public static DelayedAreaBlast Spawn(Vector3 center, float delay, float radius,
            PlayerController player, LayerMask enemyMask, float skillMul, ElementTag element, ChainConfig enh, bool hasEnh)
        {
            var go = new GameObject("DelayedAreaBlast");
            go.transform.position = center;
            var b = go.AddComponent<DelayedAreaBlast>();
            b._center = center;
            b._delay = Mathf.Max(0.05f, delay);
            b._radius = Mathf.Max(0.5f, radius);
            b._player = player;
            b._enemyMask = enemyMask;
            b._skillMul = Mathf.Max(0f, skillMul);
            b._element = element;
            b._enh = enh;
            b._hasEnh = hasEnh;

            // 预警地面指示（爆炸前闪烁的元素色圈）
            var c = SkillModifierApplier.ColorOf(element);
            c.a = 0.5f;
            SkillModifierApplier.SpawnCubeVfx(center + Vector3.up * 0.05f, c, Mathf.Max(1f, radius * 0.8f), b._delay);
            return b;
        }

        private void Update()
        {
            if (_detonated) return;
            _delay -= Time.deltaTime;
            if (_delay > 0f) return;

            _detonated = true;
            Detonate();
            Destroy(gameObject, 0.1f);
        }

        private void Detonate()
        {
            if (_player == null) return;

            if (_element != ElementTag.None)
                FxFactory.SpawnElementBurst(_center + Vector3.up * 0.05f, _element, _radius, 0.6f);

            var hits = Physics.OverlapSphere(_center, _radius, _enemyMask);
            var list = new System.Collections.Generic.List<Collider>(hits);
            foreach (var hit in hits)
            {
                var dmgable = hit.GetComponent<IDamageable>();
                if (dmgable != null)
                {
                    float tDef = dmgable.Stats != null ? dmgable.Stats.defense : 0f;
                    var (dmg, _) = _player.Stats.CalcSkillDamage(tDef, _skillMul);
                    dmgable.OnDamage(dmg, hit.transform.position, _player.gameObject);
                }
                if (_hasEnh)
                    SkillModifierApplier.ApplyEnhancementStatus(_enh, hit.gameObject);
            }

            if (_element != ElementTag.None)
                SkillModifierApplier.ApplyElementImpact(_element, _center, list, _player);
        }
    }
}
