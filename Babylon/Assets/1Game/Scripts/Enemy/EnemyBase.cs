using UnityEngine;

namespace XianTu
{
    /// <summary>
    /// 基础敌人 AI
    /// Demo1: 简单追踪玩家 + 近战攻击
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyBase : MonoBehaviour, IDamageable
    {
        [Header("属性")]
        [SerializeField] private CombatStats stats = new()
        {
            maxHp = 30f,
            currentHp = 30f,
            attackDamage = 5f,
            moveSpeed = 3f
        };

        [Header("AI 参数")]
        [SerializeField] private float detectRange = 12f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackInterval = 1.5f;

        [Header("掉落")]
        [SerializeField] private ItemData[] possibleDrops;
        [SerializeField] private float dropChance = 0.3f;

        [Header("受击特效")]
        [SerializeField] private GameObject hitVFXPrefab;

        private CharacterController _cc;
        private Transform _target;
        private float _attackTimer;

        // 受击表现
        private Renderer _renderer;
        private Color _originalColor;
        private float _hitFlashTimer;

        public CombatStats Stats => stats;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
                _originalColor = _renderer.material.color;
        }

        private void Start()
        {
            stats.ResetHp();

            // 寻找玩家
            if (PlayerController.Instance != null)
                _target = PlayerController.Instance.transform;
        }

        private void Update()
        {
            if (!stats.IsAlive) return;
            if (_target == null) return;

            // 受击闪烁恢复
            if (_hitFlashTimer > 0)
            {
                _hitFlashTimer -= Time.deltaTime;
                if (_hitFlashTimer <= 0 && _renderer != null)
                    _renderer.material.color = _originalColor;
            }

            float distToTarget = Vector3.Distance(transform.position, _target.position);

            if (distToTarget <= attackRange)
            {
                // 攻击
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0)
                {
                    Attack();
                    _attackTimer = attackInterval;
                }
            }
            else if (distToTarget <= detectRange)
            {
                // 追踪
                MoveTowards(_target.position);
            }

            // 朝向目标
            Vector3 lookDir = _target.position - transform.position;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }

        private void MoveTowards(Vector3 targetPos)
        {
            Vector3 dir = (targetPos - transform.position).normalized;
            dir.y = 0;
            Vector3 velocity = dir * stats.moveSpeed;
            velocity.y = -9.8f;
            _cc.Move(velocity * Time.deltaTime);
        }

        private void Attack()
        {
            if (_target == null) return;

            var damageable = _target.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float damage = stats.CalculateDamage();
                damageable.OnDamage(damage, transform.position, gameObject);
            }
        }

        // ========== IDamageable ==========

        public void OnDamage(float damage, Vector3 hitPoint, GameObject attacker)
        {
            if (!stats.IsAlive) return;

            float actual = stats.TakeDamage(damage);

            // 发布伤害飘字事件
            GameEvents.Publish(new GameEvents.DamageNumberRequested
            {
                WorldPosition = hitPoint != Vector3.zero ? hitPoint : transform.position,
                Damage = actual,
                IsCrit = actual > damage * 0.9f && damage > stats.attackDamage, // 简单判断是否暴击
                IsPlayerDamage = false
            });

            // 受击闪白
            if (_renderer != null)
            {
                _renderer.material.color = Color.white;
                _hitFlashTimer = 0.1f;
            }

            // 播放受击特效
            if (hitVFXPrefab != null)
            {
                Vector3 vfxPos = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up;
                GameObject vfx;
                if (ObjectPool.Instance != null)
                {
                    vfx = ObjectPool.Instance.Get(hitVFXPrefab, vfxPos, Quaternion.identity);
                    ObjectPool.Instance.Return(vfx, 1f);
                }
                else
                {
                    vfx = Instantiate(hitVFXPrefab, vfxPos, Quaternion.identity);
                    Destroy(vfx, 1f);
                }
            }

            // 击退
            if (attacker != null)
            {
                Vector3 knockback = (transform.position - attacker.transform.position).normalized * 0.5f;
                knockback.y = 0;
                _cc.Move(knockback);
            }

            if (!stats.IsAlive)
                OnDeath();
        }

        public void OnDeath()
        {
            // 掉落灵物
            TryDropItem();

            // 发布事件
            GameEvents.Publish(new GameEvents.EnemyKilled
            {
                Enemy = gameObject,
                Position = transform.position
            });

            // 击杀回复
            if (PlayerController.Instance != null)
            {
                float healAmount = PlayerController.Instance.Inventory.GetHealOnKill();
                if (healAmount > 0)
                    PlayerController.Instance.Stats.Heal(healAmount);
            }

            // 死亡表现（简单缩小消失）
            StartCoroutine(DeathAnimation());
        }

        private void TryDropItem()
        {
            if (possibleDrops == null || possibleDrops.Length == 0) return;
            if (Random.value > dropChance) return;

            var item = possibleDrops[Random.Range(0, possibleDrops.Length)];
            if (item != null)
            {
                ItemPickup.Spawn(item, transform.position);
            }
        }

        private System.Collections.IEnumerator DeathAnimation()
        {
            // 禁用 AI
            enabled = false;
            _cc.enabled = false;

            float timer = 0.3f;
            Vector3 startScale = transform.localScale;
            while (timer > 0)
            {
                timer -= Time.deltaTime;
                float t = timer / 0.3f;
                transform.localScale = startScale * t;
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 工厂方法：生成一个基础敌人
        /// </summary>
        public static EnemyBase Spawn(Vector3 position, float hpMultiplier = 1f, float dmgMultiplier = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Enemy";
            go.tag = "Enemy";
            int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
            go.layer = enemyLayerIndex >= 0 ? enemyLayerIndex : LayerMask.NameToLayer("Default");
            go.transform.position = position;

            // 设置颜色为红色
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.2f, 0.2f);
            }

            // 移除默认碰撞体，CharacterController 自带
            var defaultCol = go.GetComponent<Collider>();
            if (defaultCol != null) Destroy(defaultCol);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 2f;

            var enemy = go.AddComponent<EnemyBase>();
            enemy.stats.maxHp *= hpMultiplier;
            enemy.stats.currentHp = enemy.stats.maxHp;
            enemy.stats.attackDamage *= dmgMultiplier;

            return enemy;
        }

        /// <summary>设置受击特效 Prefab</summary>
        public void SetHitVFXPrefab(GameObject prefab)
        {
            hitVFXPrefab = prefab;
        }
    }
}
