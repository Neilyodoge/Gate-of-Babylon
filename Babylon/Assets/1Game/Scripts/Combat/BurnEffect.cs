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
        /// 应用灼烧效果（可叠加刷新持续时间）
        /// </summary>
        public void Apply(float dps, float duration)
        {
            _dps = Mathf.Max(_dps, dps); // 取较高伤害
            _remainingTime = Mathf.Max(_remainingTime, duration); // 刷新持续时间

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
            _target.OnDamage(damage, transform.position, null);
        }
    }
}
