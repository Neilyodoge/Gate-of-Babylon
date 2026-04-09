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

        public void Initialize(float damage, Vector3 direction, float speed)
        {
            _damage = damage;
            _direction = direction.normalized;
            _speed = speed;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            transform.position += _direction * (_speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;

            // 忽略同层敌人
            if (other.CompareTag("Enemy")) return;
            if (other.GetComponent<EnemyProjectile>() != null) return;

            // 伤害玩家
            if (other.CompareTag("Player"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                    damageable.OnDamage(_damage, transform.position, gameObject);
            }

            _initialized = false;
            Destroy(gameObject);
        }
    }
}
