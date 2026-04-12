using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 敌人投射物 —— 只伤害玩家
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        private float _damage;
        private Vector3 _direction;
        private float _speed;
        private bool _initialized;
        private float _lifeTimer;

        /// <summary>投射物最大存活时间（秒）</summary>
        private const float MAX_LIFETIME = 5f;

        public void Initialize(float damage, Vector3 direction, float speed)
        {
            _damage = damage;
            _direction = direction.normalized;
            _speed = speed;
            _lifeTimer = MAX_LIFETIME;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            transform.position += _direction * (_speed * Time.deltaTime);

            // 生命周期超时销毁
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0)
            {
                _initialized = false;
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;

            // 忽略同层敌人
            if (other.CompareTag("Enemy")) return;
            if (other.GetComponent<EnemyProjectile>() != null) return;
            // 忽略玩家投射物
            if (other.GetComponent<Projectile>() != null) return;

            // 伤害玩家
            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.OnDamage(_damage, transform.position, gameObject);
            }

            // 碰到玩家或墙壁/障碍物都销毁
            _initialized = false;
            Destroy(gameObject);
        }
    }
}
