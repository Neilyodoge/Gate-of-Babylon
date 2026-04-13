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

            if (_tickTimer <= 0)
            {
                _tickTimer = TICK_INTERVAL;
                DealBurnDamage();
            }

            if (_remainingTime <= 0)
            {
                _dps = 0;
                Destroy(this);
            }
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
