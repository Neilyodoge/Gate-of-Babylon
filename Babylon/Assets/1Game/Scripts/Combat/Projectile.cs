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

        private float _damage;
        private float _armorPen;
        private Vector3 _direction;
        private float _speed;
        private int _pierceRemaining;
        private float _burnDPS;
        private float _lifeTimer;
        private bool _initialized;
        private ElementTag _elementTag;
        private PlayerController _ownerPlayer;

        /// <summary>
        /// 初始化投射物参数
        /// </summary>
        public void Initialize(float damage, Vector3 direction, float speed, int pierceCount, float burnDPS)
        {
            Initialize(damage, direction, speed, pierceCount, burnDPS, ElementTag.None, null);
        }

        /// <summary>
        /// 初始化投射物参数（含元素 + 释放者引用，用于命中元素表现）
        /// </summary>
        public void Initialize(float damage, Vector3 direction, float speed, int pierceCount, float burnDPS,
                                ElementTag elementTag, PlayerController owner, float armorPen = 0f)
        {
            _damage = damage;
            _armorPen = armorPen;
            _direction = direction.normalized;
            _speed = speed;
            _pierceRemaining = pierceCount;
            _burnDPS = burnDPS;
            _lifeTimer = lifetime;
            _elementTag = elementTag;
            _ownerPlayer = owner;
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
            if (other.GetComponent<EnemyProjectile>() != null) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float finalDmg = _damage;
                if (damageable.Stats != null)
                    finalDmg = Mathf.Max(1f, _damage - damageable.Stats.defense * (1f - Mathf.Clamp01(_armorPen)));
                damageable.OnDamage(finalDmg, transform.position, gameObject);

                // 灼烧效果
                if (_burnDPS > 0)
                {
                    var burn = other.GetComponent<BurnEffect>();
                    if (burn == null)
                        burn = other.gameObject.AddComponent<BurnEffect>();
                    burn.Apply(_burnDPS, 3f); // 灼烧3秒
                }

                // 元素命中表现（cube 颜色 + 灼烧 / 冻结 / 雷击）
                if (_elementTag != ElementTag.None && _ownerPlayer != null)
                {
                    var list = new System.Collections.Generic.List<Collider> { other };
                    SkillModifierApplier.ApplyElementImpact(_elementTag, transform.position, list, _ownerPlayer);
                }

                // v0.3.3 融合层：投射物命中也算技能命中（御剑术等）
                GameEvents.Publish(new GameEvents.SkillHitConnected
                {
                    SlotIndex = -1,  // 投射物来源槽位未跟踪，置 -1
                    Skill = null,
                    HitPoint = transform.position,
                    Target = other.gameObject
                });

                // 穿透判定
                if (_pierceRemaining > 0)
                {
                    _pierceRemaining--;
                    return; // 继续飞行
                }
            }

            // 碰到墙壁、障碍物等环境物体也回收
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
