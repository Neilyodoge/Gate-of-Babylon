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

        // V.08 增强 payload：投射物携带链的控制/状态，命中时施加
        private bool _hasEnh;
        private ChainConfig _enh;

        // V.08 形态改造·链锁弹射：命中后自动寻找附近下一个敌人反弹
        private int _chainRemaining;
        private LayerMask _chainMask;
        private const float ChainSearchRadius = 9f;
        private const float ChainDamageFalloff = 0.8f;
        private readonly System.Collections.Generic.List<GameObject> _chainHistory = new();

        /// <summary>设置增强 payload（命中敌人时施加控制/附加状态）。在 Initialize 之后调用。</summary>
        public void SetEnhancement(ChainConfig cfg)
        {
            _enh = cfg;
            _hasEnh = true;
        }

        /// <summary>设置链锁弹射：命中后向附近未命中的敌人反弹 count 次。在 Initialize 之后调用。</summary>
        public void SetChain(int count, LayerMask enemyMask)
        {
            _chainRemaining = Mathf.Max(0, count);
            _chainMask = enemyMask;
        }

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
            _hasEnh = false; // 对象池复用：清除上一次的增强 payload，等待 SetEnhancement 重新设置
            _chainRemaining = 0; // 对象池复用：清除上一次的链锁状态
            _chainHistory.Clear();

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

                // V.08 增强：投射物携带的控制/附加状态作用到命中敌人
                if (_hasEnh)
                {
                    Vector3 center = _ownerPlayer != null ? _ownerPlayer.transform.position : transform.position - _direction;
                    SkillModifierApplier.ApplyEnhancementToEnemy(_enh, other.gameObject, center, _ownerPlayer);
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

                // V.08 链锁弹射：寻找附近下一个未命中的敌人反弹
                if (_chainRemaining > 0)
                {
                    _chainHistory.Add(other.gameObject);
                    var next = FindChainTarget();
                    if (next != null)
                    {
                        _chainRemaining--;
                        _damage *= ChainDamageFalloff;
                        Vector3 d = next.transform.position - transform.position;
                        d.y = 0f;
                        _direction = d.sqrMagnitude > 0.0001f ? d.normalized : _direction;
                        transform.rotation = Quaternion.LookRotation(_direction);
                        return; // 转向下一目标继续飞行
                    }
                }
            }

            // 碰到墙壁、障碍物等环境物体也回收
            Recycle();
        }

        /// <summary>寻找搜索半径内最近的、尚未被本次链锁命中的敌人。</summary>
        private GameObject FindChainTarget()
        {
            var hits = Physics.OverlapSphere(transform.position, ChainSearchRadius, _chainMask);
            GameObject best = null;
            float bestSqr = float.MaxValue;
            foreach (var h in hits)
            {
                var go = h.gameObject;
                if (_chainHistory.Contains(go)) continue;
                if (go.GetComponent<IDamageable>() == null) continue;
                float sqr = (go.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = go;
                }
            }
            return best;
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
