using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 投射物（灵力弹、技能投射物等）
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("基础参数")]
        [SerializeField] private float lifetime = 5f;

        // 运行时数据
        private float _damage;
        private Vector3 _direction;
        private float _speed;
        private int _pierceRemaining;
        private float _burnDPS;
        private float _lifeTimer;
        private bool _initialized;

        /// <summary>
        /// 初始化投射物参数
        /// </summary>
        public void Initialize(float damage, Vector3 direction, float speed, int pierceCount, float burnDPS)
        {
            _damage = damage;
            _direction = direction.normalized;
            _speed = speed;
            _pierceRemaining = pierceCount;
            _burnDPS = burnDPS;
            _lifeTimer = lifetime;
            _initialized = true;

            transform.rotation = Quaternion.LookRotation(_direction);
        }

        private void Update()
        {
            if (!_initialized) return;

            // 移动
            transform.position += _direction * (_speed * Time.deltaTime);

            // 生命周期
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0)
                Recycle();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_initialized) return;

            // 不伤害玩家
            if (other.CompareTag("Player")) return;

            // 忽略其他投射物
            if (other.GetComponent<Projectile>() != null) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.OnDamage(_damage, transform.position, gameObject);

                // 灼烧效果
                if (_burnDPS > 0)
                {
                    var burn = other.GetComponent<BurnEffect>();
                    if (burn == null)
                        burn = other.gameObject.AddComponent<BurnEffect>();
                    burn.Apply(_burnDPS, 3f); // 灼烧3秒
                }

                // 穿透判定
                if (_pierceRemaining > 0)
                {
                    _pierceRemaining--;
                    return; // 继续飞行
                }
            }

            Recycle();
        }

        private void Recycle()
        {
            _initialized = false;
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Return(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            _initialized = false;
        }
    }
}
