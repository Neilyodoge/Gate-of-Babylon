using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 灼烧效果（持续伤害 DoT）
    /// 由灵物"火灵珠"等触发
    /// </summary>
    public class BurnEffect : MonoBehaviour
    {
        private float _dps;
        private float _remainingTime;
        private float _tickTimer;
        private const float TICK_INTERVAL = 0.5f; // 每0.5秒结算一次

        // 视觉：每 _vfxInterval 秒在目标周围撒一颗小红 cube，看起来像跳动的火苗
        // 频率比伤害 tick 高，避免出现"puff…puff"的卡顿感
        private float _vfxTimer;
        private const float VFX_INTERVAL = 0.12f;

        private IDamageable _target;

        /// <summary>
        /// 应用灼烧效果（每次攻击刷新DPS和持续时间）
        /// 调用方传入的dps已经是所有灼烧灵物的总DPS，直接覆盖即可
        /// </summary>
        public void Apply(float dps, float duration)
        {
            _dps = dps; // 直接使用最新的总DPS（调用方已累加所有灼烧灵物）
            _remainingTime = duration; // 刷新持续时间

            if (_target == null)
                _target = GetComponent<IDamageable>();
        }

        private void Update()
        {
            if (_remainingTime <= 0) return;

            _remainingTime -= Time.deltaTime;
            _tickTimer -= Time.deltaTime;
            _vfxTimer -= Time.deltaTime;

            if (_tickTimer <= 0)
            {
                _tickTimer = TICK_INTERVAL;
                DealBurnDamage();
            }

            // 火苗视觉：高频率撒小 cube，比伤害 tick 密很多
            if (_vfxTimer <= 0)
            {
                _vfxTimer = VFX_INTERVAL;
                SpawnFlameCube();
            }

            if (_remainingTime <= 0)
            {
                _dps = 0;
                Destroy(this);
            }
        }

        /// <summary>在目标身上随机位置生成一颗小红 cube 当作"火苗"，自带旋转 + 淡出</summary>
        private void SpawnFlameCube()
        {
            // 随机分布在脚下到头顶之间、半径 0.45 的圆筒里
            Vector2 r = Random.insideUnitCircle * 0.45f;
            Vector3 pos = transform.position + new Vector3(r.x, Random.Range(0.2f, 1.4f), r.y);

            // 颜色基底取自统一的元素配色，再随机轻微调亮，做出闪烁感
            Color c = SkillModifierApplier.ColorOf(ElementTag.Fire);
            float lum = Random.Range(0.85f, 1.2f);
            c = new Color(Mathf.Min(1f, c.r * lum), Mathf.Min(1f, c.g * lum), Mathf.Min(1f, c.b * lum), c.a);

            float size = Random.Range(0.12f, 0.22f);
            float life = Random.Range(0.35f, 0.55f);
            // 略微上飘 + 随机大小/亮度，远看像跳动的小火苗
            SkillModifierApplier.SpawnCubeVfx(pos, c, size, life, riseSpeed: Random.Range(0.6f, 1.1f));
        }

        private void DealBurnDamage()
        {
            if (_target == null) return;
            float damage = _dps * TICK_INTERVAL;

            // 直接扣血，不走OnDamage（避免触发硬直/击退等受击表现）
            float actual = _target.Stats.TakeDamage(damage);

            // 发布灼烧专用飘字（橙色+🔥）
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = transform.position + Vector3.up * 1.2f,
                Damage = actual,
                IsCrit = false,
                IsPlayerDamage = false,
                IsBurn = true
            });

            // 检查死亡
            if (!_target.Stats.IsAlive)
                _target.OnDeath();
        }
    }
}
